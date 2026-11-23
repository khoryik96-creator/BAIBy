using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    // One <pet version="1"> record per pet slot. IDs are the stable content IDs from PetIds.
    internal static class PetSaveCodec
    {
        public const int Version = 1;

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
                    new XElement("unlocks", new XAttribute("grown", state.IsGrown)))
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
                return PetState.Restore(catalog, (string)Field(root, "name"), appearance,
                    new DateTimeOffset((long)Field(root, "checkpoint"), TimeSpan.Zero),
                    TimeSpan.FromTicks((long)Field(root, "age")),
                    meters,
                    (double)Attribute(bond, "value"),
                    (double)Attribute(bond, "highest"),
                    (double)Attribute(personality, "lively"),
                    (double)Attribute(personality, "bold"),
                    (bool)Attribute(Field(root, "unlocks"), "grown"));
            }
            catch (Exception exception) when (exception is XmlException || exception is FormatException ||
                exception is ArgumentException || exception is OverflowException)
            {
                throw new InvalidDataException("The pet save contains invalid data.", exception);
            }
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
