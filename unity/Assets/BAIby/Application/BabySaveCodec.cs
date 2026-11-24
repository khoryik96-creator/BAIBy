using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    internal static class BabySaveCodec
    {
        // v13: the care element carries the consequence state (care v2). The baby fields are unchanged, so a
        // v12 baby decodes as before and its care v1 record migrates (see CareRecordCodec).
        // v14: the care element carries the health axis (care v3); a v13 baby decodes as well.
        // v15: a <development> element carries the development tracks, readiness and the setback. A v14 or older baby
        // has none: the simulation seeds it from age, and its first advance keeps the nominal pace.
        private const int Version = 15;
        private const int VersionBeforeDevelopment = 14;
        private const int VersionBeforeCare = 11;
        private const int VersionBeforeAppearance = 10;
        private const int VersionBeforeBlockTowers = 9;
        private const int VersionBeforeWalking = 8;
        private const int VersionBeforeNurseryTheme = 7;
        private const int VersionBeforeNewSound = 6;
        private const int VersionBeforeHealth = 5;
        private const int VersionBeforeJournal = 4;
        private const int VersionBeforeCrawling = 3;
        private const int VersionBeforePlush = 2;
        private const int VersionBeforeEnergy = 1;
        private const double MigratedEnergy = 80;

        public static string Encode(BabyState state) =>
            new XElement("baby",
                new XAttribute("version", Version),
                new XElement("name", state.Name),
                new XElement("checkpoint", state.TimeCheckpoint.UtcDateTime.Ticks),
                new XElement("age", state.Age.Ticks),
                new XElement("satiety", state.Satiety),
                new XElement("comfort", state.Comfort),
                new XElement("energy", state.Energy),
                new XElement("toy-familiarity", state.ToyFamiliarity),
                new XElement("plush-familiarity", state.PlushFamiliarity),
                new XElement("can-sit-and-reach", state.CanSitAndReach),
                new XElement("can-crawl", state.CanCrawl),
                new XElement("connection", state.Connection),
                new XElement("story-sessions", state.StorySessions),
                new XElement("cleanliness", state.Cleanliness),
                new XElement("health", state.Health),
                new XElement("new-sound-resolved", state.NewSoundResolved),
                new XElement("nursery-theme", (int)state.NurseryTheme),
                new XElement("can-walk", state.CanWalk),
                new XElement("block-towers", state.BlockTowers),
                new XElement("appearance",
                    new XAttribute("skin", (int)state.Appearance.SkinTone),
                    new XAttribute("hair-color", (int)state.Appearance.HairColor),
                    new XAttribute("hair-style", (int)state.Appearance.HairStyle),
                    new XAttribute("outfit", (int)state.Appearance.Outfit),
                    new XAttribute("headwear", (int)state.Appearance.Headwear)),
                CareRecordCodec.Encode(state.Care),
                state.Development == null ? null : DevelopmentRecordCodec.Encode(state.Development))
            .ToString(SaveOptions.DisableFormatting);

        public static BabyState Decode(string payload)
        {
            try
            {
                var root = XElement.Parse(payload);
                if (root.Name != "baby" || root.Attribute("version") == null)
                    throw new InvalidDataException("The baby save header is invalid.");

                var version = (int)root.Attribute("version");
                if (version < VersionBeforeEnergy || version > Version)
                    throw new NotSupportedException($"Baby save version {version} is unsupported.");

                return BabyState.Restore((string)Field(root, "name"),
                    new DateTimeOffset((long)Field(root, "checkpoint"), TimeSpan.Zero),
                    TimeSpan.FromTicks((long)Field(root, "age")),
                    (double)Field(root, "satiety"),
                    (double)Field(root, "comfort"),
                    version == VersionBeforeEnergy ? MigratedEnergy : (double)Field(root, "energy"),
                    (int)Field(root, "toy-familiarity"),
                    version > VersionBeforePlush ? (int)Field(root, "plush-familiarity") : 0,
                    (bool)Field(root, "can-sit-and-reach"),
                    version > VersionBeforeCrawling && (bool)Field(root, "can-crawl"),
                    version > VersionBeforeJournal ? (double)Field(root, "connection") : 0,
                    version > VersionBeforeJournal ? (int)Field(root, "story-sessions") : 0,
                    version > VersionBeforeHealth ? (double)Field(root, "cleanliness") : 100,
                    version > VersionBeforeHealth ? (double)Field(root, "health") : 100,
                    version > VersionBeforeNewSound && (bool)Field(root, "new-sound-resolved"),
                    version > VersionBeforeNurseryTheme ? (NurseryTheme)(int)Field(root, "nursery-theme") : NurseryTheme.Sage,
                    version > VersionBeforeWalking && (bool)Field(root, "can-walk"),
                    version > VersionBeforeBlockTowers ? (int)Field(root, "block-towers") : 0,
                    version > VersionBeforeAppearance ? DecodeAppearance(Field(root, "appearance")) : BabyAppearance.Default,
                    // Older babies get an unanchored ledger: their first advance excuses the whole gap.
                    version > VersionBeforeCare ? CareRecordCodec.Decode(Field(root, CareRecordCodec.ElementName)) : null,
                    // A baby that was never advanced since its migration saves no development record yet.
                    version > VersionBeforeDevelopment && root.Element(DevelopmentRecordCodec.ElementName) != null
                        ? DevelopmentRecordCodec.Decode(root.Element(DevelopmentRecordCodec.ElementName))
                        : null);
            }
            catch (Exception exception) when (exception is XmlException || exception is FormatException ||
                exception is ArgumentException || exception is OverflowException)
            {
                throw new InvalidDataException("The baby save contains invalid data.", exception);
            }
        }

        // Out-of-range IDs make the BabyAppearance constructor throw, which surfaces as InvalidDataException.
        private static BabyAppearance DecodeAppearance(XElement appearance) =>
            new BabyAppearance(
                (SkinTone)(int)Attribute(appearance, "skin"),
                (HairColor)(int)Attribute(appearance, "hair-color"),
                (HairStyle)(int)Attribute(appearance, "hair-style"),
                (Outfit)(int)Attribute(appearance, "outfit"),
                (Headwear)(int)Attribute(appearance, "headwear"));

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The baby save is missing {element.Name} {name}.");

        private static XElement Field(XElement root, string name) =>
            root.Element(name) ?? throw new InvalidDataException($"The baby save is missing {name}.");
    }
}
