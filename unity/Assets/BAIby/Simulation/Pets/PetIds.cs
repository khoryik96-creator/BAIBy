using System;

namespace BAIby.Simulation.Pets
{
    // Saved values are stable content IDs. Append new members; never renumber or remove one.
    // tests/content-ids.test.mjs snapshots every member.
    public enum Species { Cat = 0, Dog = 1, Fish = 2, Rabbit = 3, Hamster = 4 }

    // Breeds use a block of 100 IDs per species (Cat 100+, Dog 200+, Fish 300+, Rabbit 400+,
    // Hamster 500+), so a saved breed always names its species. Later breeds take the next free
    // ID in their species block.
    public enum PetBreed
    {
        DomesticShorthair = 100,
        Tabby = 101,
        Tuxedo = 102,
        Siamese = 103,
        Calico = 104,
        Ragdoll = 105,
        MaineCoon = 106,
        Bengal = 107,
        BritishShorthair = 108,
        RussianBlue = 109,
        Sphynx = 110,

        MixedBreed = 200,
        Golden = 201,
        Beagle = 202,
        Shiba = 203,
        Corgi = 204,
        Poodle = 205,
        Dachshund = 206,
        Husky = 207,
        Samoyed = 208,
        Pomeranian = 209,
        Dalmatian = 210,
        BorderCollie = 211,

        Goldfish = 300,
        Guppy = 301,
        Betta = 302,
        NeonTetraSchool = 303,
        Angelfish = 304,
        Koi = 305,
        Clownfish = 306,
        Discus = 307,

        Dutch = 400,
        NetherlandDwarf = 401,
        HollandLop = 402,
        Lionhead = 403,
        Angora = 404,
        Rex = 405,
        FlemishGiant = 406,
        Harlequin = 407,

        SyrianGolden = 500,
        WinterWhite = 501,
        Roborovski = 502,
        TeddyBear = 503,
        Panda = 504,
        Sapphire = 505,
        Campbells = 506,
    }

    // Every pet meter across all species. A species uses a subset; Health is always present.
    public enum PetMeter
    {
        Fullness = 0, Energy = 1, Comfort = 2, Cleanliness = 3, Health = 4,
        Exercise = 5, Bedding = 6, Water = 7, Calm = 8, Hay = 9, Litter = 10,
    }

    // Care verbs. A verb shared by several species keeps one ID; its effect is per species.
    public enum PetVerb
    {
        Feed = 0, Pet = 1, Wand = 2, Brush = 3, Nap = 4,
        Walk = 5, Fetch = 6, Rest = 7,
        Bowl = 8, Wheel = 9, FluffBedding = 10, GentleHold = 11,
        SprinkleFood = 12, WipeGlass = 13, WaterChange = 14,
        Hay = 15, Greens = 16, Groom = 17, FloorTime = 18, Litter = 19,
    }

    public enum PetMoment
    {
        Kneading = 0, Purring = 1, BringsToy = 2, ReunionGreeting = 3,
        CheekPouches = 4, SwimsToGlass = 5, Binky = 6,
    }

    // Main coat colours offered per breed. A breed that later drops a coat still decodes it
    // and renders its default coat instead.
    public enum PetCoat
    {
        Cream = 0, Ginger = 1, Gray = 2, Black = 3, White = 4, Brown = 5, Golden = 6, Silver = 7,
        Blue = 8, Seal = 9, Lilac = 10, Red = 11, Sable = 12, Tan = 13, Fawn = 14, Orange = 15,
        Yellow = 16, Purple = 17, Chocolate = 18, Tricolor = 19,
    }

    // Placeholders until the pet accessory art lands. The accent uses the baby outfit palette.
    public enum PetAccessory { None = 0, Collar = 1, Bandana = 2, Bow = 3, TankCastle = 4 }

    public enum PetStage { Young = 0, Grown = 1 }
    public enum BondTier { Acquainted = 0, Trusting = 1, BestFriends = 2 }
    public enum BreedUnlock { Free = 0, Earned = 1, Premium = 2 }
    public enum BreedArtTier { Recolor = 0, Overlay = 1, NewSilhouette = 2 }

    // The per-species goal ladder for earned breeds. The n-th earned breed of a species uses goal n.
    public enum EarnedGoal { FirstFullCareDay = 0, Bond40 = 1, RaisedToGrown = 2, SevenOnTimeCareDays = 3 }

    public enum PetCue { Content = 0, IgnoredFlakes = 1 }

    public static class PetIds
    {
        public const int BreedBlockSize = 100;

        public static bool IsDefined(Species species) => Enum.IsDefined(typeof(Species), species);
        public static bool IsDefined(PetBreed breed) => Enum.IsDefined(typeof(PetBreed), breed);

        public static int BreedBlockStart(Species species) => ((int)species + 1) * BreedBlockSize;

        // The species whose block holds this breed ID, whether or not the breed is defined yet.
        public static bool TrySpeciesOfBlock(int breedId, out Species species)
        {
            species = (Species)(breedId / BreedBlockSize - 1);
            return breedId >= BreedBlockSize && IsDefined(species);
        }

        public static Species SpeciesOf(PetBreed breed)
        {
            if (!IsDefined(breed) || !TrySpeciesOfBlock((int)breed, out var species))
                throw new ArgumentOutOfRangeException(nameof(breed));

            return species;
        }

        // Lowercase key used in product and special coat IDs, for example breed.dog.corgi.
        public static string Key(Species species)
        {
            switch (species)
            {
                case Species.Cat: return "cat";
                case Species.Dog: return "dog";
                case Species.Fish: return "fish";
                case Species.Rabbit: return "rabbit";
                case Species.Hamster: return "hamster";
                default: throw new ArgumentOutOfRangeException(nameof(species));
            }
        }

        public static string BreedProductId(Species species, string breedKey) => $"breed.{Key(species)}.{breedKey}";

        // Store product IDs allow lowercase letters, digits, underscores and periods.
        public static bool IsIdSegment(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            foreach (var character in value)
            {
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '_')
                    return false;
            }

            return true;
        }

        // A special coat is a named pattern for one species, for example coat.cat.tortoiseshell.
        public static bool IsSpecialCoatId(string id, Species species)
        {
            var prefix = $"coat.{Key(species)}.";
            return id != null && id.StartsWith(prefix, StringComparison.Ordinal) && IsIdSegment(id.Substring(prefix.Length));
        }
    }
}
