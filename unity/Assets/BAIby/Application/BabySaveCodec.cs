using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    internal static class BabySaveCodec
    {
        private const int Version = 2;
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
                new XElement("can-sit-and-reach", state.CanSitAndReach))
            .ToString(SaveOptions.DisableFormatting);

        public static BabyState Decode(string payload)
        {
            try
            {
                var root = XElement.Parse(payload);
                if (root.Name != "baby" || root.Attribute("version") == null)
                    throw new InvalidDataException("The baby save header is invalid.");

                var version = (int)root.Attribute("version");
                if (version != VersionBeforeEnergy && version != Version)
                    throw new NotSupportedException($"Baby save version {version} is unsupported.");

                return BabyState.Restore((string)Field(root, "name"),
                    new DateTimeOffset((long)Field(root, "checkpoint"), TimeSpan.Zero),
                    TimeSpan.FromTicks((long)Field(root, "age")),
                    (double)Field(root, "satiety"),
                    (double)Field(root, "comfort"),
                    version == VersionBeforeEnergy ? MigratedEnergy : (double)Field(root, "energy"),
                    (int)Field(root, "toy-familiarity"),
                    (bool)Field(root, "can-sit-and-reach"));
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
