using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    internal static class HouseholdManifestCodec
    {
        // v2 adds pets: a kind on pet entries and keepsakes, and <pets> progress. A household with no pet
        // data is still written as v1, byte for byte as before, so an older build keeps managing it. Once
        // pet data exists the manifest is v2, which an older build keeps untouched instead of rewriting it
        // without the pet keepsakes and progress it cannot read.
        // A cat's milestones (pounce, climb, high jump: care slice 7) waiting as celebrations sit in their own
        // <pet-celebrations> element, which a v2 build that cannot read them ignores (and drops: only the pending
        // announcement is lost, and the journal keeps the milestone).
        private const int Version = 2;
        private const int VersionBeforePets = 1;

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
                if (entry.Kind != CompanionKind.Baby)
                    element.Add(new XAttribute("kind", (int)entry.Kind));
                if (entry.Leaving)
                    element.Add(new XAttribute("leaving", true));
                companions.Add(element);
            }

            var celebrations = new XElement("celebrations");
            var petCelebrations = new XElement("pet-celebrations");
            foreach (var celebration in manifest.Celebrations)
                (celebration.Milestone >= CompanionMilestone.Pounce ? petCelebrations : celebrations).Add(new XElement("celebration",
                    new XAttribute("slot", celebration.Slot),
                    new XAttribute("milestone", (int)celebration.Milestone)));

            var keepsakes = new XElement("keepsakes");
            foreach (var keepsake in manifest.Keepsakes)
                keepsakes.Add(keepsake.Kind == CompanionKind.Pet ? EncodePetKeepsake(keepsake) : EncodeBabyKeepsake(keepsake));

            var hasPets = manifest.HasPetData;
            var root = new XElement("household",
                new XAttribute("version", hasPets ? Version : VersionBeforePets),
                new XAttribute("active", manifest.ActiveSlot),
                companions, celebrations, keepsakes);
            if (hasPets)
            {
                var pets = new XElement("pets");
                foreach (var record in manifest.SpeciesRecords)
                    pets.Add(EncodeSpecies(record));
                root.Add(pets);
            }

            if (petCelebrations.HasElements)
                root.Add(petCelebrations);

            return root.ToString(SaveOptions.DisableFormatting);
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
                        Instant(Attribute(element, "adopted")), leaving != null && (bool)leaving, Kind(element)));
                }

                var celebrations = new List<PendingCelebration>();
                var petCelebrations = root.Element("pet-celebrations")?.Elements("celebration") ?? new XElement[0];
                // A kitten's milestones always come before it grows up, so they are read first.
                foreach (var element in petCelebrations)
                    celebrations.Add(new PendingCelebration((int)Attribute(element, "slot"),
                        (CompanionMilestone)(int)Attribute(element, "milestone")));
                foreach (var element in Field(root, "celebrations").Elements("celebration"))
                    celebrations.Add(new PendingCelebration((int)Attribute(element, "slot"),
                        (CompanionMilestone)(int)Attribute(element, "milestone")));

                var keepsakes = new List<Keepsake>();
                foreach (var element in Field(root, "keepsakes").Elements("keepsake"))
                    keepsakes.Add(Kind(element) == CompanionKind.Pet ? DecodePetKeepsake(element) : DecodeBabyKeepsake(element));

                var species = new List<SpeciesRecord>();
                var pets = root.Element("pets");
                if (pets != null)
                {
                    foreach (var element in pets.Elements("species"))
                        species.Add(DecodeSpecies(element));
                }

                return new HouseholdManifest(companions, (int)Attribute(root, "active"), celebrations, keepsakes, species);
            }
            // Undefined IDs, duplicate slots and out-of-range values throw ArgumentException subclasses.
            catch (Exception exception) when (exception is XmlException || exception is FormatException ||
                exception is ArgumentException || exception is OverflowException)
            {
                throw new InvalidDataException("The household manifest contains invalid data.", exception);
            }
        }

        private static XElement EncodeBabyKeepsake(Keepsake keepsake) =>
            new XElement("keepsake",
                new XAttribute("name", keepsake.Name),
                new XAttribute("adopted", keepsake.AdoptedAt.UtcDateTime.Ticks),
                new XAttribute("farewell", keepsake.FarewellAt.UtcDateTime.Ticks),
                new XAttribute("age", keepsake.Age.Ticks),
                new XAttribute("stage", (int)keepsake.Stage),
                new XAttribute("skin", (int)keepsake.Appearance.SkinTone),
                new XAttribute("hair-color", (int)keepsake.Appearance.HairColor),
                new XAttribute("hair-style", (int)keepsake.Appearance.HairStyle),
                new XAttribute("outfit", (int)keepsake.Appearance.Outfit),
                new XAttribute("headwear", (int)keepsake.Appearance.Headwear));

        private static XElement EncodePetKeepsake(Keepsake keepsake)
        {
            var look = keepsake.PetAppearance;
            var element = new XElement("keepsake",
                new XAttribute("kind", (int)CompanionKind.Pet),
                new XAttribute("name", keepsake.Name),
                new XAttribute("adopted", keepsake.AdoptedAt.UtcDateTime.Ticks),
                new XAttribute("farewell", keepsake.FarewellAt.UtcDateTime.Ticks),
                new XAttribute("age", keepsake.Age.Ticks),
                new XAttribute("stage", (int)keepsake.PetStage),
                new XAttribute("species", (int)look.Species),
                new XAttribute("breed", (int)look.Breed),
                new XAttribute("coat", (int)look.Coat),
                new XAttribute("accessory", (int)look.Accessory),
                new XAttribute("accent", (int)look.AccessoryAccent));
            if (look.SpecialCoatId != null)
                element.Add(new XAttribute("special-coat", look.SpecialCoatId));
            return element;
        }

        private static Keepsake DecodeBabyKeepsake(XElement element) =>
            new Keepsake((string)Attribute(element, "name"),
                Instant(Attribute(element, "adopted")),
                Instant(Attribute(element, "farewell")),
                TimeSpan.FromTicks((long)Attribute(element, "age")),
                (BabyStage)(int)Attribute(element, "stage"),
                new BabyAppearance(
                    (SkinTone)(int)Attribute(element, "skin"),
                    (HairColor)(int)Attribute(element, "hair-color"),
                    (HairStyle)(int)Attribute(element, "hair-style"),
                    (Outfit)(int)Attribute(element, "outfit"),
                    (Headwear)(int)Attribute(element, "headwear")));

        // The species is saved beside the breed so a mismatch is caught; a breed whose ID is unknown to
        // this build fails the manifest, which then falls back to its backup.
        private static Keepsake DecodePetKeepsake(XElement element)
        {
            var species = (Species)(int)Attribute(element, "species");
            var breed = (PetBreed)(int)Attribute(element, "breed");
            if (!PetIds.IsDefined(species) || !PetIds.IsDefined(breed) || PetIds.SpeciesOf(breed) != species)
                throw new InvalidDataException("A pet keepsake names an undefined or mismatched breed.");

            return new Keepsake((string)Attribute(element, "name"),
                Instant(Attribute(element, "adopted")),
                Instant(Attribute(element, "farewell")),
                TimeSpan.FromTicks((long)Attribute(element, "age")),
                (PetStage)(int)Attribute(element, "stage"),
                new PetAppearance(breed,
                    (PetCoat)(int)Attribute(element, "coat"),
                    (string)element.Attribute("special-coat"),
                    (PetAccessory)(int)Attribute(element, "accessory"),
                    (Outfit)(int)Attribute(element, "accent")));
        }

        private static XElement EncodeSpecies(SpeciesRecord record)
        {
            var days = record.CareDays;
            return new XElement("species",
                new XAttribute("id", (int)record.Species),
                new XAttribute("bond", record.HighestBond),
                new XAttribute("grown", record.RaisedToGrown),
                new XAttribute("day", days.Day),
                new XAttribute("meals", days.Meals),
                new XAttribute("together", days.Together),
                new XAttribute("bedtime", days.Bedtime),
                new XAttribute("rhythm", days.Rhythm),
                new XAttribute("best-rhythm", days.BestRhythm),
                new XAttribute("last-full-day", days.LastFullDay),
                new XAttribute("full-days", days.FullDays));
        }

        private static SpeciesRecord DecodeSpecies(XElement element) =>
            new SpeciesRecord((Species)(int)Attribute(element, "id"),
                new PetCareDays(
                    (int)Attribute(element, "day"),
                    (int)Attribute(element, "meals"),
                    (bool)Attribute(element, "together"),
                    (bool)Attribute(element, "bedtime"),
                    (int)Attribute(element, "rhythm"),
                    (int)Attribute(element, "best-rhythm"),
                    (int)Attribute(element, "last-full-day"),
                    (int)Attribute(element, "full-days")),
                (double)Attribute(element, "bond"),
                (bool)Attribute(element, "grown"));

        // No kind attribute means a baby: every manifest written before pets.
        private static CompanionKind Kind(XElement element)
        {
            var kind = element.Attribute("kind");
            if (kind == null)
                return CompanionKind.Baby;

            var value = (CompanionKind)(int)kind;
            if (!Enum.IsDefined(typeof(CompanionKind), value))
                throw new InvalidDataException($"The household manifest names undefined companion kind {(int)value}.");
            return value;
        }

        private static DateTimeOffset Instant(XAttribute ticks) => new DateTimeOffset((long)ticks, TimeSpan.Zero);

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The household manifest is missing {element.Name} {name}.");

        private static XElement Field(XElement root, string name) =>
            root.Element(name) ?? throw new InvalidDataException($"The household manifest is missing {name}.");
    }
}
