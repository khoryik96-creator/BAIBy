using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    internal static class BabySaveCodec
    {
        private const int Version = 9;
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
                new XElement("can-walk", state.CanWalk))
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
                    version > VersionBeforeWalking && (bool)Field(root, "can-walk"));
            }
            catch (Exception exception) when (exception is XmlException || exception is FormatException ||
                exception is ArgumentException || exception is OverflowException)
            {
                throw new InvalidDataException("The baby save contains invalid data.", exception);
            }
        }

        private static XElement Field(XElement root, string name) =>
            root.Element(name) ?? throw new InvalidDataException($"The baby save is missing {name}.");
    }
}
