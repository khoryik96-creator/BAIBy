using System;

namespace BAIby.Simulation.Pets
{
    // A pet's chosen look. Cosmetic only: tendencies come from the breed, never from the coat.
    public sealed class PetAppearance : IEquatable<PetAppearance>
    {
        public PetBreed Breed { get; }
        public PetCoat Coat { get; }
        // Optional named pattern such as coat.cat.tortoiseshell; null when the breed coat is shown.
        public string SpecialCoatId { get; }
        public PetAccessory Accessory { get; }
        // Accessories reuse the baby outfit palette, so a collar can match a baby's outfit.
        public Outfit AccessoryAccent { get; }

        public Species Species => PetIds.SpeciesOf(Breed);

        // Checks IDs only. Whether the breed offers the coat is a catalog question: see PetCatalog.ValidateChoice.
        public PetAppearance(PetBreed breed, PetCoat coat, string specialCoatId = null,
            PetAccessory accessory = PetAccessory.None, Outfit accessoryAccent = Outfit.Sage)
        {
            if (!PetIds.IsDefined(breed))
                throw new ArgumentOutOfRangeException(nameof(breed));
            if (!Enum.IsDefined(typeof(PetCoat), coat))
                throw new ArgumentOutOfRangeException(nameof(coat));
            if (specialCoatId != null && !PetIds.IsSpecialCoatId(specialCoatId, PetIds.SpeciesOf(breed)))
                throw new ArgumentException($"A special coat for {PetIds.SpeciesOf(breed)} looks like coat.{PetIds.Key(PetIds.SpeciesOf(breed))}.<name>.",
                    nameof(specialCoatId));
            if (!Enum.IsDefined(typeof(PetAccessory), accessory))
                throw new ArgumentOutOfRangeException(nameof(accessory));
            if (!Enum.IsDefined(typeof(Outfit), accessoryAccent))
                throw new ArgumentOutOfRangeException(nameof(accessoryAccent));

            Breed = breed;
            Coat = coat;
            SpecialCoatId = specialCoatId;
            Accessory = accessory;
            AccessoryAccent = accessoryAccent;
        }

        public static PetAppearance Default(PetCatalog catalog, PetBreed breed) =>
            new PetAppearance(breed, (catalog ?? throw new ArgumentNullException(nameof(catalog))).Get(breed).DefaultCoat);

        public PetAppearance WithCoat(PetCoat value) => new PetAppearance(Breed, value, SpecialCoatId, Accessory, AccessoryAccent);
        public PetAppearance WithSpecialCoat(string value) => new PetAppearance(Breed, Coat, value, Accessory, AccessoryAccent);
        public PetAppearance WithAccessory(PetAccessory value) => new PetAppearance(Breed, Coat, SpecialCoatId, value, AccessoryAccent);
        public PetAppearance WithAccessoryAccent(Outfit value) => new PetAppearance(Breed, Coat, SpecialCoatId, Accessory, value);

        public bool Equals(PetAppearance other) =>
            other != null && Breed == other.Breed && Coat == other.Coat &&
            string.Equals(SpecialCoatId, other.SpecialCoatId, StringComparison.Ordinal) &&
            Accessory == other.Accessory && AccessoryAccent == other.AccessoryAccent;

        public override bool Equals(object obj) => Equals(obj as PetAppearance);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Breed;
                hash = hash * 31 + (int)Coat;
                hash = hash * 31 + (SpecialCoatId == null ? 0 : StringComparer.Ordinal.GetHashCode(SpecialCoatId));
                hash = hash * 31 + (int)Accessory;
                return hash * 31 + (int)AccessoryAccent;
            }
        }

        public override string ToString() =>
            $"{Breed}/{Coat}{(SpecialCoatId == null ? string.Empty : "/" + SpecialCoatId)}/{Accessory}/{AccessoryAccent}";
    }
}
