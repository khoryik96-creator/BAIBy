using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    internal static class HouseholdManifestCodec
    {
        private const int Version = 1;

        public static string Encode(HouseholdManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            var companions = new XElement("companions");
            foreach (var entry in manifest.Companions)
            {
                var element = new XElement("companion",
                    new XAttribute("slot", entry.Slot),
                    new XAttribute("adopted", entry.AdoptedAt.UtcDateTime.Ticks));
                if (entry.Leaving)
                    element.Add(new XAttribute("leaving", true));
                companions.Add(element);
            }

            var celebrations = new XElement("celebrations");
            foreach (var celebration in manifest.Celebrations)
                celebrations.Add(new XElement("celebration",
                    new XAttribute("slot", celebration.Slot),
                    new XAttribute("milestone", (int)celebration.Milestone)));

            var keepsakes = new XElement("keepsakes");
            foreach (var keepsake in manifest.Keepsakes)
                keepsakes.Add(new XElement("keepsake",
                    new XAttribute("name", keepsake.Name),
                    new XAttribute("adopted", keepsake.AdoptedAt.UtcDateTime.Ticks),
                    new XAttribute("farewell", keepsake.FarewellAt.UtcDateTime.Ticks),
                    new XAttribute("age", keepsake.Age.Ticks),
                    new XAttribute("stage", (int)keepsake.Stage),
                    new XAttribute("skin", (int)keepsake.Appearance.SkinTone),
                    new XAttribute("hair-color", (int)keepsake.Appearance.HairColor),
                    new XAttribute("hair-style", (int)keepsake.Appearance.HairStyle),
                    new XAttribute("outfit", (int)keepsake.Appearance.Outfit),
                    new XAttribute("headwear", (int)keepsake.Appearance.Headwear)));

            return new XElement("household",
                    new XAttribute("version", Version),
                    new XAttribute("active", manifest.ActiveSlot),
                    companions, celebrations, keepsakes)
                .ToString(SaveOptions.DisableFormatting);
        }

        public static HouseholdManifest Decode(string payload)
        {
            try
            {
                var root = XElement.Parse(payload);
                if (root.Name != "household" || root.Attribute("version") == null)
                    throw new InvalidDataException("The household manifest header is invalid.");

                var version = (int)root.Attribute("version");
                if (version < 1 || version > Version)
                    throw new NotSupportedException($"Household manifest version {version} is unsupported.");

                var companions = new List<HouseholdEntry>();
                foreach (var element in Field(root, "companions").Elements("companion"))
                {
                    var leaving = element.Attribute("leaving");
                    companions.Add(new HouseholdEntry((int)Attribute(element, "slot"),
                        Instant(Attribute(element, "adopted")), leaving != null && (bool)leaving));
                }

                var celebrations = new List<PendingCelebration>();
                foreach (var element in Field(root, "celebrations").Elements("celebration"))
                    celebrations.Add(new PendingCelebration((int)Attribute(element, "slot"),
                        (CompanionMilestone)(int)Attribute(element, "milestone")));

                var keepsakes = new List<Keepsake>();
                foreach (var element in Field(root, "keepsakes").Elements("keepsake"))
                    keepsakes.Add(new Keepsake((string)Attribute(element, "name"),
                        Instant(Attribute(element, "adopted")),
                        Instant(Attribute(element, "farewell")),
                        TimeSpan.FromTicks((long)Attribute(element, "age")),
                        (BabyStage)(int)Attribute(element, "stage"),
                        new BabyAppearance(
                            (SkinTone)(int)Attribute(element, "skin"),
                            (HairColor)(int)Attribute(element, "hair-color"),
                            (HairStyle)(int)Attribute(element, "hair-style"),
                            (Outfit)(int)Attribute(element, "outfit"),
                            (Headwear)(int)Attribute(element, "headwear"))));

                return new HouseholdManifest(companions, (int)Attribute(root, "active"), celebrations, keepsakes);
            }
            // Undefined IDs, duplicate slots and out-of-range values throw ArgumentException subclasses.
            catch (Exception exception) when (exception is XmlException || exception is FormatException ||
                exception is ArgumentException || exception is OverflowException)
            {
                throw new InvalidDataException("The household manifest contains invalid data.", exception);
            }
        }

        private static DateTimeOffset Instant(XAttribute ticks) => new DateTimeOffset((long)ticks, TimeSpan.Zero);

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The household manifest is missing {element.Name} {name}.");

        private static XElement Field(XElement root, string name) =>
            root.Element(name) ?? throw new InvalidDataException($"The household manifest is missing {name}.");
    }
}
