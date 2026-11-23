using System;
using System.Collections.Generic;
using System.Linq;

namespace BAIby.Simulation.Pets
{
    // One pet companion. Kept separate from BabyState; the time model matches the baby's.
    public sealed class PetState
    {
        public const double TrustingBond = 40;
        public const double BestFriendsBond = 75;
        public const double PersonalityLimit = 100;
        // About 10,000 years, the whole DateTimeOffset range, so no clock can age a pet further.
        // PetSimulation saturates here; a larger saved age is corrupt.
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(3_652_500);

        private readonly Dictionary<PetMeter, double> meters;

        public string Name { get; }
        public PetAppearance Appearance { get; }
        public DateTimeOffset TimeCheckpoint { get; }
        public TimeSpan Age { get; }
        public double Bond { get; }
        // Bond high-water mark: earned goals and the care ladder's Bond floor read it.
        public double HighestBond { get; }
        // Hidden temperament axes: Mellow (-) to Lively (+) and Shy (-) to Bold (+).
        public double Liveliness { get; }
        public double Boldness { get; }
        public bool IsGrown { get; }

        public Species Species => Appearance.Species;
        public PetBreed Breed => Appearance.Breed;
        public PetStage Stage => IsGrown ? PetStage.Grown : PetStage.Young;
        public double Health => meters[PetMeter.Health];
        public IReadOnlyDictionary<PetMeter, double> Meters => meters;

        public BondTier BondTier =>
            Bond >= BestFriendsBond ? BondTier.BestFriends : Bond >= TrustingBond ? BondTier.Trusting : BondTier.Acquainted;

        public static PetState Restore(PetCatalog catalog, string name, PetAppearance appearance, DateTimeOffset timeCheckpoint,
            TimeSpan age, IReadOnlyDictionary<PetMeter, double> meters, double bond, double highestBond,
            double liveliness, double boldness, bool isGrown)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The saved pet needs a name.", nameof(name));
            if (!IsSavableName(name))
                throw new ArgumentException("The saved pet's name has characters a save cannot hold.", nameof(name));
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));
            if (age < TimeSpan.Zero || age > MaxAge)
                throw new ArgumentOutOfRangeException(nameof(age));
            if (meters == null)
                throw new ArgumentNullException(nameof(meters));

            var species = catalog.Get(appearance.Species);
            _ = catalog.Get(appearance.Breed);
            var expected = species.Meters.Select(meter => meter.Meter).Concat(new[] { PetMeter.Health }).ToArray();
            if (meters.Count != expected.Length)
                throw new ArgumentException($"A {appearance.Species} has exactly {expected.Length} meters.", nameof(meters));

            foreach (var meter in expected)
            {
                if (!meters.TryGetValue(meter, out var value))
                    throw new ArgumentException($"A {appearance.Species} needs a {meter} value.", nameof(meters));
                if (!IsPercent(value))
                    throw new ArgumentOutOfRangeException(nameof(meters), $"{meter} must be from 0 to 100.");
            }

            if (!IsPercent(bond))
                throw new ArgumentOutOfRangeException(nameof(bond));
            if (!IsPercent(highestBond) || highestBond < bond)
                throw new ArgumentOutOfRangeException(nameof(highestBond));
            if (!IsPersonality(liveliness))
                throw new ArgumentOutOfRangeException(nameof(liveliness));
            if (!IsPersonality(boldness))
                throw new ArgumentOutOfRangeException(nameof(boldness));

            return new PetState(name, appearance, timeCheckpoint, age, meters.ToDictionary(pair => pair.Key, pair => pair.Value),
                bond, highestBond, liveliness, boldness, isGrown);
        }

        internal PetState(string name, PetAppearance appearance, DateTimeOffset timeCheckpoint, TimeSpan age,
            Dictionary<PetMeter, double> meters, double bond, double highestBond, double liveliness, double boldness, bool isGrown)
        {
            Name = name;
            Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            TimeCheckpoint = timeCheckpoint;
            Age = age;
            this.meters = meters ?? throw new ArgumentNullException(nameof(meters));
            Bond = bond;
            HighestBond = highestBond;
            Liveliness = liveliness;
            Boldness = boldness;
            IsGrown = isGrown;
        }

        public bool HasMeter(PetMeter meter) => meters.ContainsKey(meter);

        public double Meter(PetMeter meter) =>
            meters.TryGetValue(meter, out var value)
                ? value
                : throw new ArgumentException($"A {Species} has no {meter} meter.", nameof(meter));

        // Cosmetic change only; the breed, and so the species, cannot change after adoption.
        public PetState WithAppearance(PetAppearance appearance)
        {
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));
            if (appearance.Breed != Breed)
                throw new ArgumentException("A pet keeps its breed.", nameof(appearance));
            if (Appearance.Equals(appearance))
                return this;

            return new PetState(Name, appearance, TimeCheckpoint, Age, meters, Bond, HighestBond, Liveliness, Boldness, IsGrown);
        }

        // True when every character can be written to an XML save and read back unchanged: no
        // control characters (line breaks included, which XML would normalize), no noncharacters
        // U+FFFE and U+FFFF, and surrogates only as complete pairs, so a name cut in the middle of
        // an emoji is refused.
        public static bool IsSavableName(string name)
        {
            if (name == null)
                return false;

            for (var index = 0; index < name.Length; index++)
            {
                var character = name[index];
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= name.Length || !char.IsLowSurrogate(name[index + 1]))
                        return false;

                    index++;
                    continue;
                }

                if (char.IsLowSurrogate(character))
                    return false;
                if (character < 0x20)
                    return false;
                if (character == '\uFFFE' || character == '\uFFFF')
                    return false;
            }

            return true;
        }

        private static bool IsPercent(double value) => !double.IsNaN(value) && value >= 0 && value <= 100;

        private static bool IsPersonality(double value) =>
            !double.IsNaN(value) && value >= -PersonalityLimit && value <= PersonalityLimit;
    }
}
