using System;
using System.Collections.Generic;

namespace BAIby.Simulation.Pets
{
    // Plain data for the pet catalog. Authoring tools can produce these later; the rules only read them.
    public sealed class MeterDefinition
    {
        public PetMeter Meter { get; }
        public double DecayPerHour { get; }
        public double Initial { get; }

        public MeterDefinition(PetMeter meter, double decayPerHour, double initial)
        {
            Meter = meter;
            DecayPerHour = decayPerHour;
            Initial = initial;
        }
    }

    public readonly struct MeterEffect
    {
        public PetMeter Meter { get; }
        public double Delta { get; }

        public MeterEffect(PetMeter meter, double delta)
        {
            Meter = meter;
            Delta = delta;
        }
    }

    public sealed class VerbDefinition
    {
        public PetVerb Verb { get; }
        public IReadOnlyList<MeterEffect> Effects { get; }
        public double Bond { get; }
        // Temperament nudges, applied only while Young (Mellow -/Lively +, Shy -/Bold +).
        public double Lively { get; }
        public double Bold { get; }

        public VerbDefinition(PetVerb verb, double bond, double lively, double bold, params MeterEffect[] effects)
        {
            Verb = verb;
            Bond = bond;
            Lively = lively;
            Bold = bold;
            Effects = Array.AsReadOnly(effects ?? Array.Empty<MeterEffect>());
        }
    }

    public sealed class SpeciesDefinition
    {
        public Species Species { get; }
        // Visible meters in room order, at most four. Health is an implied quiet background meter.
        public IReadOnlyList<MeterDefinition> Meters { get; }
        public IReadOnlyList<VerbDefinition> Verbs { get; }
        public TimeSpan GrownAge { get; }
        public IReadOnlyList<PetMoment> Moments { get; }
        public IReadOnlyList<PetAccessory> Accessories { get; }

        public SpeciesDefinition(Species species, TimeSpan grownAge, MeterDefinition[] meters, VerbDefinition[] verbs,
            PetMoment[] moments, PetAccessory[] accessories)
        {
            Species = species;
            GrownAge = grownAge;
            Meters = Array.AsReadOnly(meters ?? Array.Empty<MeterDefinition>());
            Verbs = Array.AsReadOnly(verbs ?? Array.Empty<VerbDefinition>());
            Moments = Array.AsReadOnly(moments ?? Array.Empty<PetMoment>());
            Accessories = Array.AsReadOnly(accessories ?? Array.Empty<PetAccessory>());
        }

        public bool HasMeter(PetMeter meter)
        {
            if (meter == PetMeter.Health)
                return true;

            foreach (var definition in Meters)
            {
                if (definition.Meter == meter)
                    return true;
            }

            return false;
        }

        public VerbDefinition FindVerb(PetVerb verb)
        {
            foreach (var definition in Verbs)
            {
                if (definition.Verb == verb)
                    return definition;
            }

            return null;
        }

        public bool Offers(PetAccessory accessory)
        {
            foreach (var offered in Accessories)
            {
                if (offered == accessory)
                    return true;
            }

            return false;
        }
    }

    // A gentle breed tendency: the meter decays this many percent faster (positive) or slower (negative).
    public readonly struct BreedTendency
    {
        public PetMeter Meter { get; }
        public int Percent { get; }

        public BreedTendency(PetMeter meter, int percent)
        {
            Meter = meter;
            Percent = percent;
        }
    }

    public sealed class BreedDefinition
    {
        public PetBreed Breed { get; }
        public Species Species { get; }
        public BreedUnlock Unlock { get; }
        // Set only for earned breeds.
        public EarnedGoal? Goal { get; }
        // Set only for premium breeds: breed.<species>.<breed>.
        public string ProductId { get; }
        public BreedArtTier Art { get; }
        // True when the breed's art ships with its species slice. Free and earned breeds always do.
        public bool AvailableNow { get; }
        // Breed-limited coats; the first is the default look.
        public IReadOnlyList<PetCoat> Coats { get; }
        public IReadOnlyList<BreedTendency> Tendencies { get; }
        // Starting temperament bias on the hidden Mellow/Lively and Shy/Bold axes.
        public int LivelyBias { get; }
        public int BoldBias { get; }

        public PetCoat DefaultCoat => Coats.Count > 0 ? Coats[0] : PetCoat.Cream;

        public BreedDefinition(PetBreed breed, Species species, BreedUnlock unlock, EarnedGoal? goal, string productId,
            BreedArtTier art, bool availableNow, PetCoat[] coats, BreedTendency[] tendencies, int livelyBias, int boldBias)
        {
            Breed = breed;
            Species = species;
            Unlock = unlock;
            Goal = goal;
            ProductId = productId;
            Art = art;
            AvailableNow = availableNow;
            Coats = Array.AsReadOnly(coats ?? Array.Empty<PetCoat>());
            Tendencies = Array.AsReadOnly(tendencies ?? Array.Empty<BreedTendency>());
            LivelyBias = livelyBias;
            BoldBias = boldBias;
        }

        public bool OffersCoat(PetCoat coat)
        {
            foreach (var offered in Coats)
            {
                if (offered == coat)
                    return true;
            }

            return false;
        }

        // Decay multiplier for one meter: 1.15 for a +15 percent tendency.
        public double DecayFactor(PetMeter meter)
        {
            foreach (var tendency in Tendencies)
            {
                if (tendency.Meter == meter)
                    return 1 + tendency.Percent / 100.0;
            }

            return 1;
        }

        // Change in need lost per hour across the species' meters, against a breed with no
        // tendencies. Meters decay at different base rates, so balanced percentages are not enough:
        // a balanced breed has a net change of zero.
        public double NetDecayChangePerHour(SpeciesDefinition species)
        {
            if (species == null)
                throw new ArgumentNullException(nameof(species));

            var net = 0.0;
            foreach (var meter in species.Meters)
            {
                if (meter != null)
                    net += meter.DecayPerHour * (DecayFactor(meter.Meter) - 1);
            }

            return net;
        }

        // Total size of the breed's tendencies, used to keep premium breeds no stronger than free ones.
        public int TendencyMagnitude
        {
            get
            {
                var total = 0;
                foreach (var tendency in Tendencies)
                    total += Math.Abs(tendency.Percent);

                return total;
            }
        }
    }

    // A breed pack product (breedpack.<species> or breedpack.all). It sells the premium breeds that
    // were current when the pack was published, listed explicitly. The list never grows: a breed
    // added to the catalog later is not part of a pack players already own.
    public sealed class BreedPackDefinition
    {
        public string ProductId { get; }
        // The species the pack covers, or null for the every-species pack.
        public Species? Species { get; }
        public IReadOnlyList<PetBreed> Breeds { get; }

        public BreedPackDefinition(string productId, Species? species, PetBreed[] breeds)
        {
            ProductId = productId;
            Species = species;
            Breeds = Array.AsReadOnly(breeds ?? Array.Empty<PetBreed>());
        }

        public bool Includes(PetBreed breed)
        {
            foreach (var included in Breeds)
            {
                if (included == breed)
                    return true;
            }

            return false;
        }
    }
}
