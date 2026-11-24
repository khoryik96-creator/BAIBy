using System;
using System.Collections.Generic;
using System.Linq;

namespace BAIby.Simulation.Pets
{
    // One pet companion. Kept separate from BabyState; the time model matches the baby's. A species with a care
    // profile (PetCareProfile, the cat first) also keeps the baby's care systems: its ledger and consequence ladder
    // (Care), development (Development), Bond's record, temperament and rhythm (Bonding), its journal and the
    // milestones it has reached. A pet saved before them (or restored without them) is migrated on first use.
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
        // The care ledger and the consequence ladder; null for a species without a care profile, or for a pet saved
        // before slice 7 until its first advance.
        public CareRecord Care { get; }
        // Development tracks, readiness and the setback; null until seeded.
        public DevelopmentRecord Development { get; }
        // Bond's high-water mark, temperament (Trust and Curiosity) and the care rhythm; null until seeded.
        public BondRecord Bonding { get; }
        public JournalRecord Journal { get; }
        // How many of the species' milestones (PetCareProfile.Skills) the pet has reached, 0 to 3.
        public int Milestones { get; }

        public Species Species => Appearance.Species;
        // The milestone that went shy in a setback, if any.
        public MotorSkill ShySkill => Development?.Shy ?? MotorSkill.None;
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

            // Saved before care profiles: the milestones its age has passed stay reached (all of them once grown).
            var profile = PetCareProfile.For(appearance.Species);
            var milestones = profile == null ? 0 : isGrown ? profile.Skills.Count : profile.ReachedBy(age);
            return new PetState(name, appearance, timeCheckpoint, age, meters.ToDictionary(pair => pair.Key, pair => pair.Value),
                bond, highestBond, liveliness, boldness, isGrown, null, null, null, null, milestones);
        }

        // A pet with its care records (a save from slice 7 on). Records a species without a care profile cannot have
        // are refused; the development, Bond and journal records may be missing (seeded on first use).
        public static PetState Restore(PetCatalog catalog, string name, PetAppearance appearance, DateTimeOffset timeCheckpoint,
            TimeSpan age, IReadOnlyDictionary<PetMeter, double> meters, double bond, double highestBond,
            double liveliness, double boldness, bool isGrown, CareRecord care, DevelopmentRecord development,
            BondRecord bonding, JournalRecord journal, int milestones)
        {
            var restored = Restore(catalog, name, appearance, timeCheckpoint, age, meters, bond, highestBond, liveliness,
                boldness, isGrown);
            var profile = PetCareProfile.For(appearance.Species);
            if (profile == null)
            {
                if (care != null || development != null || bonding != null || journal != null || milestones != 0)
                    throw new ArgumentException($"A {appearance.Species} has no care records yet.", nameof(care));
                return restored;
            }

            if (care == null)
                throw new ArgumentNullException(nameof(care));
            if (care.Routine != profile.Routine)
                throw new ArgumentException("The care record follows another routine.", nameof(care));
            if (care.Checkpoint.UtcTicks != timeCheckpoint.UtcTicks)
                throw new ArgumentException("The care record must share the pet's checkpoint.", nameof(care));
            if (milestones < 0 || milestones > profile.Skills.Count)
                throw new ArgumentOutOfRangeException(nameof(milestones));
            if (development != null && development.Shy != MotorSkill.None &&
                (milestones == 0 || !profile.Skills.Take(milestones).Contains(development.Shy)))
                throw new ArgumentException("Only a milestone the pet has reached can go shy.", nameof(development));

            return restored.With(care: care, development: development, bonding: bonding, journal: journal,
                milestones: milestones);
        }

        internal PetState(string name, PetAppearance appearance, DateTimeOffset timeCheckpoint, TimeSpan age,
            Dictionary<PetMeter, double> meters, double bond, double highestBond, double liveliness, double boldness, bool isGrown)
            : this(name, appearance, timeCheckpoint, age, meters, bond, highestBond, liveliness, boldness, isGrown, null, null,
                null, null, 0)
        {
        }

        internal PetState(string name, PetAppearance appearance, DateTimeOffset timeCheckpoint, TimeSpan age,
            Dictionary<PetMeter, double> meters, double bond, double highestBond, double liveliness, double boldness, bool isGrown,
            CareRecord care, DevelopmentRecord development, BondRecord bonding, JournalRecord journal, int milestones)
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
            Care = care;
            Development = development;
            Bonding = bonding;
            Journal = journal;
            Milestones = milestones;
        }

        // A copy with some fields replaced; the care fields keep their value when not given.
        internal PetState With(DateTimeOffset? timeCheckpoint = null, TimeSpan? age = null,
            Dictionary<PetMeter, double> meters = null, double? bond = null, double? highestBond = null,
            double? liveliness = null, double? boldness = null, bool? isGrown = null, CareRecord care = null,
            DevelopmentRecord development = null, BondRecord bonding = null, JournalRecord journal = null,
            int? milestones = null) =>
            new PetState(Name, Appearance, timeCheckpoint ?? TimeCheckpoint, age ?? Age, meters ?? this.meters,
                bond ?? Bond, highestBond ?? HighestBond, liveliness ?? Liveliness, boldness ?? Boldness, isGrown ?? IsGrown,
                care ?? Care, development ?? Development, bonding ?? Bonding, journal ?? Journal, milestones ?? Milestones);

        // Whether the milestone has been reached (a shy one is still reached), and whether it shows.
        public bool HasReached(MotorSkill skill)
        {
            var profile = PetCareProfile.For(Species);
            if (profile == null)
                return false;
            for (var i = 0; i < Milestones && i < profile.Skills.Count; i++)
            {
                if (profile.Skills[i] == skill)
                    return true;
            }

            return false;
        }

        public bool Shows(MotorSkill skill) => HasReached(skill) && ShySkill != skill;

        // The newest milestone reached (None before the first).
        public MotorSkill NewestSkill
        {
            get
            {
                var profile = PetCareProfile.For(Species);
                return profile == null || Milestones == 0 ? MotorSkill.None : profile.Skills[Milestones - 1];
            }
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

            return new PetState(Name, appearance, TimeCheckpoint, Age, meters, Bond, HighestBond, Liveliness, Boldness, IsGrown,
                Care, Development, Bonding, Journal, Milestones);
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
