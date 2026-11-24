using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    // One <pet version="2"> record per pet slot. IDs are the stable content IDs from PetIds.
    //
    // v2: a species with a care profile (the cat) also saves its care systems: the milestones it has reached (an attribute
    // of <unlocks>), a <care> element (its routine comes from the species), and, once seeded, <development>, <bonding>
    // and <journal> elements, the elements a baby's save uses (its <bond> is <bonding> here, beside the pet's own <bond>). A v1 pet decodes as before: its stage, Bond, look and
    // meters are kept, the milestones its age passed are reached (all of them once grown), and its records are seeded
    // on first use, the ledger excusing the whole gap on its first advance (as the baby's v1 migration did).
    internal static class PetSaveCodec
    {
        public const int Version = 2;
        private const int VersionBeforeCare = 1;

        public static string Encode(PetState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var appearance = new XElement("appearance",
                new XAttribute("coat", (int)state.Appearance.Coat),
                new XAttribute("accessory", (int)state.Appearance.Accessory),
                new XAttribute("accent", (int)state.Appearance.AccessoryAccent));
            if (state.Appearance.SpecialCoatId != null)
                appearance.Add(new XAttribute("special-coat", state.Appearance.SpecialCoatId));

            var meters = new XElement("meters");
            foreach (var meter in OrderedMeters(state))
            {
                meters.Add(new XElement("meter",
                    new XAttribute("id", (int)meter),
                    new XAttribute("value", state.Meter(meter))));
            }

            var unlocks = new XElement("unlocks", new XAttribute("grown", state.IsGrown));
            var profile = PetCareProfile.For(state.Species);
            var cared = profile != null;
            // A pet restored without its ledger saves the ledger its first advance would start from.
            var care = state.Care ?? (cared ? CareRules.Unanchored(state.TimeCheckpoint, profile.Routine) : null);
            if (cared)
                unlocks.Add(new XAttribute("milestones", state.Milestones));
            return new XElement("pet",
                    new XAttribute("version", Version),
                    new XElement("name", state.Name),
                    new XElement("species", (int)state.Species),
                    new XElement("breed", (int)state.Breed),
                    appearance,
                    new XElement("checkpoint", state.TimeCheckpoint.UtcDateTime.Ticks),
                    new XElement("age", state.Age.Ticks),
                    meters,
                    new XElement("bond", new XAttribute("value", state.Bond), new XAttribute("highest", state.HighestBond)),
                    new XElement("personality", new XAttribute("lively", state.Liveliness), new XAttribute("bold", state.Boldness)),
                    unlocks,
                    cared ? CareRecordCodec.Encode(care) : null,
                    cared && state.Development != null ? DevelopmentRecordCodec.Encode(state.Development) : null,
                    cared && state.Bonding != null ? Bonding(state.Bonding) : null,
                    cared && state.Journal != null ? JournalRecordCodec.Encode(state.Journal) : null)
                .ToString(SaveOptions.DisableFormatting);
        }

        public static PetState Decode(string payload) => Decode(payload, PetCatalog.Default);

        public static PetState Decode(string payload, PetCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            try
            {
                var root = XElement.Parse(payload);
                if (root.Name != "pet" || root.Attribute("version") == null)
                    throw new InvalidDataException("The pet save header is invalid.");

                var version = (int)root.Attribute("version");
                if (version < 1 || version > Version)
                    throw new NotSupportedException($"Pet save version {version} is unsupported.");

                var species = (Species)(int)Field(root, "species");
                if (!PetIds.IsDefined(species))
                    throw new InvalidDataException($"The pet save names undefined species {(int)species}.");

                var breed = (PetBreed)(int)Field(root, "breed");
                if (!PetIds.IsDefined(breed) || !catalog.TryGet(breed, out _))
                    throw new InvalidDataException($"The pet save names undefined breed {(int)breed}.");
                if (PetIds.SpeciesOf(breed) != species)
                    throw new InvalidDataException($"Breed {breed} is not a {species}.");

                var appearanceElement = Field(root, "appearance");
                var appearance = new PetAppearance(breed,
                    (PetCoat)(int)Attribute(appearanceElement, "coat"),
                    (string)appearanceElement.Attribute("special-coat"),
                    (PetAccessory)(int)Attribute(appearanceElement, "accessory"),
                    (Outfit)(int)Attribute(appearanceElement, "accent"));

                var meters = new Dictionary<PetMeter, double>();
                foreach (var meter in Field(root, "meters").Elements("meter"))
                {
                    var id = (PetMeter)(int)Attribute(meter, "id");
                    if (!Enum.IsDefined(typeof(PetMeter), id))
                        throw new InvalidDataException($"The pet save names undefined meter {(int)id}.");
                    if (meters.ContainsKey(id))
                        throw new InvalidDataException($"The pet save lists {id} twice.");

                    meters.Add(id, (double)Attribute(meter, "value"));
                }

                var bond = Field(root, "bond");
                var personality = Field(root, "personality");
                var unlocks = Field(root, "unlocks");
                var checkpoint = new DateTimeOffset((long)Field(root, "checkpoint"), TimeSpan.Zero);
                var age = TimeSpan.FromTicks((long)Field(root, "age"));
                var profile = PetCareProfile.For(species);
                // A v1 pet, or a species without care records yet: the simple record.
                if (version == VersionBeforeCare || profile == null)
                {
                    if (profile == null && (root.Element(CareRecordCodec.ElementName) != null || unlocks.Attribute("milestones") != null))
                        throw new InvalidDataException($"A {species} save cannot hold care records yet.");
                    return PetState.Restore(catalog, (string)Field(root, "name"), appearance, checkpoint, age, meters,
                        (double)Attribute(bond, "value"), (double)Attribute(bond, "highest"),
                        (double)Attribute(personality, "lively"), (double)Attribute(personality, "bold"),
                        (bool)Attribute(unlocks, "grown"));
                }

                var development = root.Element(DevelopmentRecordCodec.ElementName);
                var bonding = root.Element(BondingElement);
                var journal = root.Element(JournalRecordCodec.ElementName);
                return PetState.Restore(catalog, (string)Field(root, "name"), appearance, checkpoint, age, meters,
                    (double)Attribute(bond, "value"), (double)Attribute(bond, "highest"),
                    (double)Attribute(personality, "lively"), (double)Attribute(personality, "bold"),
                    (bool)Attribute(unlocks, "grown"),
                    CareRecordCodec.Decode(Field(root, CareRecordCodec.ElementName), profile.Routine),
                    development == null ? null : DevelopmentRecordCodec.Decode(development),
                    bonding == null ? null : BondRecordCodec.Decode(new XElement(bonding) { Name = BondRecordCodec.ElementName }),
                    journal == null ? null : JournalRecordCodec.Decode(journal),
                    (int)Attribute(unlocks, "milestones"));
            }
            catch (Exception exception) when (exception is XmlException || exception is FormatException ||
                exception is ArgumentException || exception is OverflowException)
            {
                throw new InvalidDataException("The pet save contains invalid data.", exception);
            }
        }

        // The pet record already has a <bond> element (Bond and its highest value), so Bond's record is <bonding>.
        private const string BondingElement = "bonding";

        private static XElement Bonding(BondRecord record)
        {
            var element = BondRecordCodec.Encode(record);
            element.Name = BondingElement;
            return element;
        }

        // Meter ID order, so a record encodes the same way every time.
        private static IEnumerable<PetMeter> OrderedMeters(PetState state)
        {
            var ordered = new List<PetMeter>();
            foreach (PetMeter meter in Enum.GetValues(typeof(PetMeter)))
            {
                if (state.HasMeter(meter))
                    ordered.Add(meter);
            }

            return ordered;
        }

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The pet save is missing {element.Name} {name}.");

        private static XElement Field(XElement root, string name) =>
            root.Element(name) ?? throw new InvalidDataException($"The pet save is missing {name}.");
    }
}
