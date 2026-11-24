using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    // One <journal v="1"> element with an <j> per entry, oldest first. Attributes a kind does not use are left out so
    // a full journal (200 entries) stays small. Invalid data throws InvalidDataException; a newer version throws
    // NotSupportedException so the session leaves that save untouched.
    //
    // v2 adds helper days (kind 6, with feeder portions in n, play top-ups in c and tidy-ups in x). A journal without
    // them is still written as v1 byte for byte; an older build leaves a v2 journal alone.
    //
    // v3 adds First words (kind 7), talk lines (kind 8, the feeling in fe) and talks about something big (kind 9, nothing
    // else). Never any typed words. A journal without them is written as v2 or v1 as before.
    internal static class JournalRecordCodec
    {
        public const string ElementName = "journal";
        private const string EntryName = "j";
        private const int Version = 3;
        private const int VersionBeforeTalk = 2;
        private const int VersionBeforeHelpers = 1;

        public static XElement Encode(JournalRecord journal)
        {
            if (journal == null)
                throw new ArgumentNullException(nameof(journal));

            var element = new XElement(ElementName,
                new XAttribute("v", journal.HasTalkEntries ? Version : journal.HasHelperEntries ? VersionBeforeTalk
                    : VersionBeforeHelpers),
                new XAttribute("spell", journal.InSpell ? journal.SpellSince.UtcTicks : 0),
                new XAttribute("spell-day", journal.SpellDay),
                new XAttribute("weeks", journal.Weeks),
                new XAttribute("week-days", journal.WeekStartCareDays));
            foreach (var entry in journal.Entries)
            {
                var item = new XElement(EntryName,
                    new XAttribute("k", (int)entry.Kind),
                    new XAttribute("t", entry.At.UtcTicks),
                    new XAttribute("d", entry.Day));
                if (entry.Skill != MotorSkill.None)
                    item.Add(new XAttribute("s", (int)entry.Skill));
                switch (entry.Kind)
                {
                    case JournalKind.Photo:
                        var look = entry.Look;
                        item.Add(new XAttribute("m", (int)entry.Moment), new XAttribute("look", string.Join(".",
                            (int)look.SkinTone, (int)look.HairColor, (int)look.HairStyle, (int)look.Outfit, (int)look.Headwear)));
                        break;
                    case JournalKind.HardDay:
                        item.Add(new XAttribute("sp", (int)entry.Spell), new XAttribute("f", entry.Since.UtcTicks),
                            new XAttribute("fd", entry.SinceDay));
                        break;
                    case JournalKind.Sticker:
                        item.Add(new XAttribute("n", entry.Count));
                        break;
                    case JournalKind.Week:
                        item.Add(new XAttribute("n", entry.Count), new XAttribute("c", entry.CareDays));
                        break;
                    case JournalKind.Helper:
                        item.Add(new XAttribute("n", entry.Feeds), new XAttribute("c", entry.Plays),
                            new XAttribute("x", entry.Tidies));
                        break;
                    case JournalKind.Talk:
                        item.Add(new XAttribute("fe", (int)entry.Feeling));
                        break;
                }

                element.Add(item);
            }

            return element;
        }

        public static JournalRecord Decode(XElement element)
        {
            if (element == null)
                throw new InvalidDataException("The save is missing its journal.");

            try
            {
                if (element.Name != ElementName)
                    throw new InvalidDataException("The journal header is invalid.");
                var version = (int)Attribute(element, "v");
                if (version > Version)
                    throw new NotSupportedException($"Journal version {version} is unsupported.");
                if (version < 1)
                    throw new InvalidDataException("The journal version is invalid.");

                var entries = new List<JournalEntry>();
                foreach (var item in element.Elements(EntryName))
                {
                    var kind = (JournalKind)(int)Attribute(item, "k");
                    if (kind == JournalKind.Helper && version < VersionBeforeTalk)
                        throw new InvalidDataException("A journal before helpers cannot hold a helper day.");
                    var talk = kind == JournalKind.FirstWords || kind == JournalKind.Talk || kind == JournalKind.BigTalk;
                    if (talk && version < Version)
                        throw new InvalidDataException("A journal before Talk time cannot hold a talk.");
                    var feeling = kind == JournalKind.Talk ? (Feeling)(int)Attribute(item, "fe") : Feeling.Happy;
                    var at = Instant(Attribute(item, "t"));
                    var skill = item.Attribute("s") == null ? MotorSkill.None : (MotorSkill)(int)item.Attribute("s");
                    var moment = PhotoMoment.Calm;
                    BabyAppearance look = null;
                    var spell = HardSpell.Poorly;
                    var since = at;
                    var sinceDay = 0;
                    if (kind == JournalKind.Photo)
                    {
                        moment = (PhotoMoment)(int)Attribute(item, "m");
                        look = Look((string)Attribute(item, "look"));
                    }
                    else if (kind == JournalKind.HardDay)
                    {
                        spell = (HardSpell)(int)Attribute(item, "sp");
                        since = Instant(Attribute(item, "f"));
                        sinceDay = (int)Attribute(item, "fd");
                    }

                    var count = item.Attribute("n") == null ? 0 : (int)item.Attribute("n");
                    var careDays = item.Attribute("c") == null ? 0 : (int)item.Attribute("c");
                    var tidies = item.Attribute("x") == null ? 0 : (int)item.Attribute("x");
                    entries.Add(JournalEntry.Restore(kind, at, (int)Attribute(item, "d"), skill, moment, look, spell, since,
                        sinceDay, count, careDays, tidies, feeling));
                }

                var spellTicks = (long)Attribute(element, "spell");
                var spellDay = (int)Attribute(element, "spell-day");
                return new JournalRecord(entries, spellDay > 0 ? Instant(spellTicks) : DateTimeOffset.MinValue, spellDay,
                    (int)Attribute(element, "weeks"), (int)Attribute(element, "week-days"));
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException ||
                exception is OverflowException)
            {
                throw new InvalidDataException("The journal contains invalid data.", exception);
            }
        }

        // "skin.hair.cut.outfit.hat" as content IDs; out-of-range IDs make BabyAppearance throw.
        private static BabyAppearance Look(string code)
        {
            var parts = code.Split('.');
            if (parts.Length != 5)
                throw new FormatException("A photo look has five parts.");

            var ids = new int[5];
            for (var i = 0; i < parts.Length; i++)
                ids[i] = int.Parse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture);
            return new BabyAppearance((SkinTone)ids[0], (HairColor)ids[1], (HairStyle)ids[2], (Outfit)ids[3], (Headwear)ids[4]);
        }

        private static DateTimeOffset Instant(XAttribute attribute) => Instant((long)attribute);

        private static DateTimeOffset Instant(long ticks)
        {
            if (ticks < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
                throw new FormatException("A journal instant is out of range.");
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The journal is missing {name}.");
    }
}
