using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // What a journal entry records. Saved as numbers: append, never renumber.
    public enum JournalKind { Arrival = 0, Milestone = 1, Photo = 2, Sticker = 3, HardDay = 4, Week = 5 }

    // The moment a photo caught, which picks the pose drawn for it. Saved as numbers: append, never renumber.
    public enum PhotoMoment { Calm = 0, Playing = 1, Napping = 2, Story = 3, Cuddle = 4, Meal = 5, Bath = 6, Poorly = 7, Blocks = 8 }

    // A hard spell the little one got through. Saved as numbers: append, never renumber.
    public enum HardSpell { Poorly = 0, Shy = 1 }

    // One journal entry. Immutable.
    // - Arrival: the day the little one came home.
    // - Milestone: Skill is the skill reached.
    // - Photo: Moment, Skill (the stage shown) and Look (the outfit and hair of the day). The picture is drawn from the
    //   pose sprites when the journal is read; nothing else is stored.
    // - Sticker: Count is the rhythm in days.
    // - HardDay: Spell, Since and SinceDay (when it began), and Skill for a shy skill.
    // - Week: Count is the week number and CareDays the care days counted in it.
    public sealed class JournalEntry : IEquatable<JournalEntry>
    {
        public JournalKind Kind { get; }
        public DateTimeOffset At { get; }
        // The little one's day (1 on the day they arrive) when it happened.
        public int Day { get; }
        public MotorSkill Skill { get; }
        public PhotoMoment Moment { get; }
        public BabyAppearance Look { get; }
        public HardSpell Spell { get; }
        public DateTimeOffset Since { get; }
        public int SinceDay { get; }
        public int Count { get; }
        public int CareDays { get; }

        private JournalEntry(JournalKind kind, DateTimeOffset at, int day, MotorSkill skill = MotorSkill.None,
            PhotoMoment moment = PhotoMoment.Calm, BabyAppearance look = null, HardSpell spell = HardSpell.Poorly,
            DateTimeOffset? since = null, int sinceDay = 0, int count = 0, int careDays = 0)
        {
            if (!Enum.IsDefined(typeof(JournalKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (day < 1)
                throw new ArgumentOutOfRangeException(nameof(day));
            if (!Enum.IsDefined(typeof(MotorSkill), skill))
                throw new ArgumentOutOfRangeException(nameof(skill));
            if (!Enum.IsDefined(typeof(PhotoMoment), moment))
                throw new ArgumentOutOfRangeException(nameof(moment));
            if (!Enum.IsDefined(typeof(HardSpell), spell))
                throw new ArgumentOutOfRangeException(nameof(spell));
            if (kind == JournalKind.Milestone && skill == MotorSkill.None)
                throw new ArgumentException("A milestone entry names its skill.", nameof(skill));
            if (kind == JournalKind.Photo && look == null)
                throw new ArgumentException("A photo keeps the look it caught.", nameof(look));
            if (count < 0 || careDays < 0 || sinceDay < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            var start = since ?? at;
            if (start > at || (kind == JournalKind.HardDay && (sinceDay < 1 || sinceDay > day)))
                throw new ArgumentException("A hard spell begins before it ends.", nameof(since));

            Kind = kind;
            At = at.ToUniversalTime();
            Day = day;
            Skill = skill;
            Moment = moment;
            Look = kind == JournalKind.Photo ? look : null;
            Spell = spell;
            Since = start.ToUniversalTime();
            SinceDay = sinceDay;
            Count = count;
            CareDays = careDays;
        }

        public static JournalEntry Arrival(DateTimeOffset at) => new JournalEntry(JournalKind.Arrival, at, 1);

        public static JournalEntry Milestone(DateTimeOffset at, int day, MotorSkill skill) =>
            new JournalEntry(JournalKind.Milestone, at, day, skill);

        public static JournalEntry Photo(DateTimeOffset at, int day, PhotoMoment moment, MotorSkill stage, BabyAppearance look) =>
            new JournalEntry(JournalKind.Photo, at, day, stage, moment, look);

        public static JournalEntry Sticker(DateTimeOffset at, int day, int rhythmDays) =>
            new JournalEntry(JournalKind.Sticker, at, day, count: rhythmDays);

        public static JournalEntry HardDay(DateTimeOffset at, int day, HardSpell spell, DateTimeOffset since, int sinceDay,
            MotorSkill skill = MotorSkill.None) =>
            new JournalEntry(JournalKind.HardDay, at, day, skill, spell: spell, since: since, sinceDay: sinceDay);

        public static JournalEntry Week(DateTimeOffset at, int day, int week, int careDays) =>
            new JournalEntry(JournalKind.Week, at, day, count: week, careDays: careDays);

        // Any entry, from saved fields; validates like the factories.
        public static JournalEntry Restore(JournalKind kind, DateTimeOffset at, int day, MotorSkill skill, PhotoMoment moment,
            BabyAppearance look, HardSpell spell, DateTimeOffset since, int sinceDay, int count, int careDays) =>
            new JournalEntry(kind, at, day, skill, moment, look, spell, since, sinceDay, count, careDays);

        public bool Equals(JournalEntry other) =>
            other != null && Kind == other.Kind && At.UtcTicks == other.At.UtcTicks && Day == other.Day &&
            Skill == other.Skill && Moment == other.Moment && Equals(Look, other.Look) && Spell == other.Spell &&
            Since.UtcTicks == other.Since.UtcTicks && SinceDay == other.SinceDay && Count == other.Count &&
            CareDays == other.CareDays;

        public override bool Equals(object obj) => Equals(obj as JournalEntry);
        public override int GetHashCode() => (int)Kind ^ At.UtcTicks.GetHashCode() ^ Day << 4;
    }

    // The little one's journal: milestones, up to three photos a day, rhythm stickers, hard days they got through and
    // a page for each week, oldest first. Bounded so saves stay small: past MaxEntries the oldest photo goes first,
    // then the oldest week page, sticker or hard day; arrival and milestones always stay. Immutable.
    public sealed class JournalRecord : IEquatable<JournalRecord>
    {
        public const int MaxEntries = 200;
        public const int PhotosPerDay = 3;
        public static readonly JournalRecord Empty = new JournalRecord(new JournalEntry[0], DateTimeOffset.MinValue, 0, 0, 0);
        private readonly JournalEntry[] entries;

        public IReadOnlyList<JournalEntry> Entries => entries;
        // When the current poorly spell began and on which day (MinValue and 0 while well).
        public DateTimeOffset SpellSince { get; }
        public int SpellDay { get; }
        // Weeks already given a page, and the care days counted when the current week began.
        public int Weeks { get; }
        public int WeekStartCareDays { get; }

        public JournalRecord(IEnumerable<JournalEntry> entries, DateTimeOffset spellSince, int spellDay, int weeks,
            int weekStartCareDays)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            var list = new List<JournalEntry>();
            foreach (var entry in entries)
            {
                if (entry == null)
                    throw new ArgumentException("A journal entry is missing.", nameof(entries));
                if (list.Count > 0 && entry.At < list[list.Count - 1].At)
                    throw new ArgumentException("Journal entries are kept oldest first.", nameof(entries));
                list.Add(entry);
            }

            if (list.Count > MaxEntries)
                throw new ArgumentOutOfRangeException(nameof(entries), "A journal holds at most 200 entries.");
            if (spellDay < 0 || weeks < 0 || weekStartCareDays < 0)
                throw new ArgumentOutOfRangeException(nameof(spellDay));

            this.entries = list.ToArray();
            SpellSince = spellSince.ToUniversalTime();
            SpellDay = spellDay;
            Weeks = weeks;
            WeekStartCareDays = weekStartCareDays;
        }

        public bool InSpell => SpellDay > 0;

        // Photos taken on a day of the little one's life.
        public int PhotosOn(int day)
        {
            var count = 0;
            foreach (var entry in entries)
            {
                if (entry.Kind == JournalKind.Photo && entry.Day == day)
                    count++;
            }

            return count;
        }

        // A journal with an entry added in time order, and the oldest optional entry dropped past MaxEntries.
        public JournalRecord With(JournalEntry entry) => With(new[] { entry }, SpellSince, SpellDay, Weeks, WeekStartCareDays);

        public JournalRecord With(IReadOnlyList<JournalEntry> added, DateTimeOffset spellSince, int spellDay, int weeks,
            int weekStartCareDays)
        {
            var list = new List<JournalEntry>(entries);
            if (added != null)
            {
                foreach (var entry in added)
                {
                    if (entry == null)
                        throw new ArgumentNullException(nameof(added));
                    var index = list.Count;
                    while (index > 0 && list[index - 1].At > entry.At)
                        index--;
                    list.Insert(index, entry);
                }
            }

            while (list.Count > MaxEntries)
                list.RemoveAt(Evictable(list));
            return new JournalRecord(list, spellSince, spellDay, weeks, weekStartCareDays);
        }

        private static int Evictable(List<JournalEntry> list)
        {
            foreach (var kind in new[] { JournalKind.Photo, JournalKind.Week, JournalKind.Sticker, JournalKind.HardDay })
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].Kind == kind)
                        return i;
                }
            }

            return 0;
        }

        public bool Equals(JournalRecord other)
        {
            if (other == null || other.entries.Length != entries.Length)
                return false;
            for (var i = 0; i < entries.Length; i++)
            {
                if (!entries[i].Equals(other.entries[i]))
                    return false;
            }

            return SpellSince.UtcTicks == other.SpellSince.UtcTicks && SpellDay == other.SpellDay && Weeks == other.Weeks &&
                WeekStartCareDays == other.WeekStartCareDays;
        }

        public override bool Equals(object obj) => Equals(obj as JournalRecord);
        public override int GetHashCode() => entries.Length ^ Weeks << 8;
    }
}
