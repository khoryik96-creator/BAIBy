using System;
using System.Collections.Generic;
using BAIby.Simulation;

namespace BAIby.Application
{
    public sealed class HouseholdEntry
    {
        public int Slot { get; }
        public DateTimeOffset AdoptedAt { get; }

        // Set while a goodbye is in progress: the keepsake is saved and the record is about to be
        // deleted. An interrupted goodbye finishes on the next launch.
        public bool Leaving { get; }

        public HouseholdEntry(int slot, DateTimeOffset adoptedAt, bool leaving = false)
        {
            HouseholdRules.ValidateSlot(slot, nameof(slot));
            Slot = slot;
            AdoptedAt = adoptedAt;
            Leaving = leaving;
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

    // What stays behind after "Say goodbye": the name, dates and look for a portrait card.
    public sealed class Keepsake
    {
        public string Name { get; }
        public DateTimeOffset AdoptedAt { get; }
        public DateTimeOffset FarewellAt { get; }
        public TimeSpan Age { get; }
        public BabyStage Stage { get; }
        public BabyAppearance Appearance { get; }

        public Keepsake(string name, DateTimeOffset adoptedAt, DateTimeOffset farewellAt, TimeSpan age,
            BabyStage stage, BabyAppearance appearance)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A keepsake needs a name.", nameof(name));
            if (age < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(age));
            if (!Enum.IsDefined(typeof(BabyStage), stage))
                throw new ArgumentOutOfRangeException(nameof(stage));

            Name = name;
            AdoptedAt = adoptedAt;
            FarewellAt = farewellAt;
            Age = age;
            Stage = stage;
            Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        }
    }

    // The household index. Slots are the source of truth; the manifest adds order, the active
    // companion, pending celebrations and keepsakes, and can be rebuilt by probing the slots.
    public sealed class HouseholdManifest
    {
        public static readonly HouseholdManifest Empty =
            new HouseholdManifest(new HouseholdEntry[0], 0, new PendingCelebration[0], new Keepsake[0]);

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
