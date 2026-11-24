using System;
using System.Collections.Generic;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    public sealed class HouseholdEntry
    {
        public int Slot { get; }
        public DateTimeOffset AdoptedAt { get; }

        // Set while a goodbye is in progress: the keepsake is saved and the record is about to be
        // deleted. An interrupted goodbye finishes on the next launch.
        public bool Leaving { get; }

        // What the slot holds. The record itself is the source of truth; the manifest copy names an
        // unreadable slot's kind. Manifests written before pets have no kind: every entry is a baby.
        public CompanionKind Kind { get; }

        public HouseholdEntry(int slot, DateTimeOffset adoptedAt, bool leaving = false, CompanionKind kind = CompanionKind.Baby)
        {
            HouseholdRules.ValidateSlot(slot, nameof(slot));
            if (!Enum.IsDefined(typeof(CompanionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (slot == 1 && kind != CompanionKind.Baby)
                throw new ArgumentException("Slot 1 only ever holds a baby.", nameof(kind));

            Slot = slot;
            AdoptedAt = adoptedAt;
            Leaving = leaving;
            Kind = kind;
        }
    }

    // A milestone a companion reached while it was off screen. It is shown once, the next time
    // that companion's room opens.
    public sealed class PendingCelebration
    {
        public int Slot { get; }
        public CompanionMilestone Milestone { get; }

        public PendingCelebration(int slot, CompanionMilestone milestone)
        {
            HouseholdRules.ValidateSlot(slot, nameof(slot));
            if (!Enum.IsDefined(typeof(CompanionMilestone), milestone))
                throw new ArgumentOutOfRangeException(nameof(milestone));

            Slot = slot;
            Milestone = milestone;
        }
    }

    // What stays behind after "Say goodbye": the name, dates and look for a portrait card. A baby's
    // keepsake keeps its stage and look; a pet's keeps its species, stage and look.
    public sealed class Keepsake
    {
        public CompanionKind Kind { get; }
        public string Name { get; }
        public DateTimeOffset AdoptedAt { get; }
        public DateTimeOffset FarewellAt { get; }
        public TimeSpan Age { get; }
        // The baby's stage; Newborn for a pet.
        public BabyStage Stage { get; }
        // Null for a pet.
        public BabyAppearance Appearance { get; }
        // Null for a baby.
        public Species? Species => PetAppearance?.Species;
        public PetStage PetStage { get; }
        public PetAppearance PetAppearance { get; }

        public Keepsake(string name, DateTimeOffset adoptedAt, DateTimeOffset farewellAt, TimeSpan age,
            BabyStage stage, BabyAppearance appearance)
            : this(CompanionKind.Baby, name, adoptedAt, farewellAt, age)
        {
            if (!Enum.IsDefined(typeof(BabyStage), stage))
                throw new ArgumentOutOfRangeException(nameof(stage));

            Stage = stage;
            Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        }

        public Keepsake(string name, DateTimeOffset adoptedAt, DateTimeOffset farewellAt, TimeSpan age,
            PetStage stage, PetAppearance appearance)
            : this(CompanionKind.Pet, name, adoptedAt, farewellAt, age)
        {
            if (!Enum.IsDefined(typeof(PetStage), stage))
                throw new ArgumentOutOfRangeException(nameof(stage));

            PetStage = stage;
            PetAppearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        }

        private Keepsake(CompanionKind kind, string name, DateTimeOffset adoptedAt, DateTimeOffset farewellAt, TimeSpan age)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A keepsake needs a name.", nameof(name));
            if (age < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(age));

            Kind = kind;
            Name = name;
            AdoptedAt = adoptedAt;
            FarewellAt = farewellAt;
            Age = age;
        }
    }

    // Household-wide pet progress for one species: the care days and the best Bond and growth any of
    // its pets has reached. It outlives the pets, so earned breeds stay unlocked after a goodbye.
    public sealed class SpeciesRecord
    {
        public Species Species { get; }
        public PetCareDays CareDays { get; }
        public double HighestBond { get; }
        public bool RaisedToGrown { get; }

        public SpeciesRecord(Species species, PetCareDays careDays, double highestBond, bool raisedToGrown)
        {
            if (!PetIds.IsDefined(species))
                throw new ArgumentOutOfRangeException(nameof(species));
            if (double.IsNaN(highestBond) || highestBond < 0 || highestBond > 100)
                throw new ArgumentOutOfRangeException(nameof(highestBond));

            Species = species;
            CareDays = careDays ?? throw new ArgumentNullException(nameof(careDays));
            HighestBond = highestBond;
            RaisedToGrown = raisedToGrown;
        }

        public static SpeciesRecord Empty(Species species) => new SpeciesRecord(species, PetCareDays.None, 0, false);

        public SpeciesGoalProgress Progress =>
            new SpeciesGoalProgress(CareDays.FullDays, HighestBond, RaisedToGrown, CareDays.BestRhythm);

        // Keeps the best of this record and what the pet's own record shows.
        public SpeciesRecord WithPet(PetState pet) =>
            pet == null || pet.Species != Species
                ? this
                : new SpeciesRecord(Species, CareDays, Math.Max(HighestBond, pet.HighestBond), RaisedToGrown || pet.IsGrown);

        public SpeciesRecord WithCareDays(PetCareDays careDays) =>
            new SpeciesRecord(Species, careDays, HighestBond, RaisedToGrown);
    }

    // The household index. Slots are the source of truth; the manifest adds order, the active
    // companion, pending celebrations and keepsakes, and can be rebuilt by probing the slots.
    public sealed class HouseholdManifest
    {
        public static readonly HouseholdManifest Empty =
            new HouseholdManifest(new HouseholdEntry[0], 0, new PendingCelebration[0], new Keepsake[0]);

        // Pet progress per species, at most one record each.
        public IReadOnlyList<SpeciesRecord> SpeciesRecords { get; private set; } = Array.Empty<SpeciesRecord>();

        // Pet data (a pet companion, a pet keepsake or species progress) needs a build that knows pets.
        public bool HasPetData
        {
            get
            {
                if (SpeciesRecords.Count > 0)
                    return true;
                foreach (var entry in Companions)
                {
                    if (entry.Kind != CompanionKind.Baby)
                        return true;
                }

                foreach (var keepsake in Keepsakes)
                {
                    if (keepsake.Kind != CompanionKind.Baby)
                        return true;
                }

                return false;
            }
        }

        public HouseholdManifest(IEnumerable<HouseholdEntry> companions, int activeSlot,
            IEnumerable<PendingCelebration> celebrations, IEnumerable<Keepsake> keepsakes, IEnumerable<SpeciesRecord> species)
            : this(companions, activeSlot, celebrations, keepsakes)
        {
            if (species == null)
                throw new ArgumentNullException(nameof(species));

            var records = new List<SpeciesRecord>();
            var seen = new HashSet<Species>();
            foreach (var record in species)
            {
                if (record == null)
                    throw new ArgumentException("A species record is missing.", nameof(species));
                if (!seen.Add(record.Species))
                    throw new ArgumentException($"{record.Species} is listed twice.", nameof(species));
                records.Add(record);
            }

            SpeciesRecords = records.AsReadOnly();
        }

        public IReadOnlyList<HouseholdEntry> Companions { get; }
        // 0 when no companion is active.
        public int ActiveSlot { get; }
        public IReadOnlyList<PendingCelebration> Celebrations { get; }
        public IReadOnlyList<Keepsake> Keepsakes { get; }

        public HouseholdManifest(IEnumerable<HouseholdEntry> companions, int activeSlot,
            IEnumerable<PendingCelebration> celebrations, IEnumerable<Keepsake> keepsakes)
        {
            if (companions == null)
                throw new ArgumentNullException(nameof(companions));
            if (celebrations == null)
                throw new ArgumentNullException(nameof(celebrations));
            if (keepsakes == null)
                throw new ArgumentNullException(nameof(keepsakes));

            var entries = new List<HouseholdEntry>();
            var slots = new HashSet<int>();
            foreach (var entry in companions)
            {
                if (entry == null)
                    throw new ArgumentException("A household entry is missing.", nameof(companions));
                if (!slots.Add(entry.Slot))
                    throw new ArgumentException($"Slot {entry.Slot} is listed twice.", nameof(companions));
                entries.Add(entry);
            }

            if (activeSlot != 0 && !slots.Contains(activeSlot))
                throw new ArgumentException("The active slot must be a listed companion.", nameof(activeSlot));

            var pending = new List<PendingCelebration>();
            foreach (var celebration in celebrations)
            {
                if (celebration == null)
                    throw new ArgumentException("A pending celebration is missing.", nameof(celebrations));
                if (!slots.Contains(celebration.Slot))
                    throw new ArgumentException("A celebration must belong to a listed companion.", nameof(celebrations));
                foreach (var existing in pending)
                {
                    if (existing.Slot == celebration.Slot && existing.Milestone == celebration.Milestone)
                        throw new ArgumentException("A celebration is listed twice.", nameof(celebrations));
                }
                pending.Add(celebration);
            }

            var kept = new List<Keepsake>();
            foreach (var keepsake in keepsakes)
                kept.Add(keepsake ?? throw new ArgumentException("A keepsake is missing.", nameof(keepsakes)));

            Companions = entries.AsReadOnly();
            ActiveSlot = activeSlot;
            Celebrations = pending.AsReadOnly();
            Keepsakes = kept.AsReadOnly();
        }
    }
}
