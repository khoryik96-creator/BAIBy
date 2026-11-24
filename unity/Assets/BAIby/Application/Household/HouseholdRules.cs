using System;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    // Saved values are stable content IDs. Append new options; never renumber.
    public enum BabyStage { Newborn = 0, Sitter = 1, Crawler = 2, Walker = 3 }
    // Grown is a pet growing from Young to Grown.
    // A cat's milestones follow the baby's: pounce, climb and the high jump.
    public enum CompanionMilestone { Sitting = 0, Crawling = 1, Walking = 2, Grown = 3, Pounce = 4, Climb = 5, HighJump = 6 }

    public static class HouseholdRules
    {
        // The single tunable for how many companions a household holds without a paid unlock.
        public const int FreeCompanions = 2;

        // Slot keys that exist on disk. Raising the free limit or adding a paid pack up to this
        // number needs no save migration.
        public const int PhysicalSlots = 8;

        // Rooms added by the "More rooms" pack (ProductIds.HomeRooms2).
        public const int RoomsPerPack = 2;

        // A need below this shows as a dot on the Home chip. Matches the simulation's low-need threshold.
        public const double LowNeedThreshold = 30;

        // How many companions can be awake: the free rooms plus any owned pack, never beyond the
        // physical slots. Companions beyond this rest; the count is computed at runtime and never saved.
        public static int CapacityFor(IEntitlements entitlements)
        {
            var capacity = FreeCompanions;
            if (entitlements != null && entitlements.Owns(ProductIds.HomeRooms2))
                capacity += RoomsPerPack;

            return Math.Min(capacity, PhysicalSlots);
        }

        // Capacity for a household of this many companions. While ownership is unknown (a damaged entitlement
        // cache with no store answer yet) every existing companion keeps a room: only a definitive refund makes
        // a companion rest. It never adds a room for a new adoption.
        public static int CapacityFor(IEntitlements entitlements, int companions)
        {
            var capacity = CapacityFor(entitlements);
            if (entitlements != null && entitlements.OwnershipUnknown)
                capacity = Math.Max(capacity, Math.Min(companions, PhysicalSlots));

            return capacity;
        }

        public static BabyStage StageOf(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.CanWalk)
                return BabyStage.Walker;
            if (state.CanCrawl)
                return BabyStage.Crawler;

            return state.CanSitAndReach ? BabyStage.Sitter : BabyStage.Newborn;
        }

        public static bool NeedsAttention(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return state.Satiety < LowNeedThreshold || state.Comfort < LowNeedThreshold ||
                state.Energy < LowNeedThreshold || state.Cleanliness < LowNeedThreshold ||
                state.Health < LowNeedThreshold || state.Care.Condition == CareCondition.Sick;
        }

        // A pet needs attention when Health or any meter its room shows is below the threshold, or (with a care
        // profile) when a missed meal is still unanswered, it is grumpy or it is sick.
        public static bool NeedsAttention(PetState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.Care != null && ((state.Care.Owed & CareTags.Meal) != 0 || state.Care.Mood == CareMood.Upset ||
                state.Care.Condition == CareCondition.Sick))
                return true;

            foreach (var meter in state.Meters)
            {
                if (meter.Value < LowNeedThreshold)
                    return true;
            }

            return false;
        }

        internal static void ValidateSlot(int slot, string parameterName)
        {
            if (slot < 1 || slot > PhysicalSlots)
                throw new ArgumentOutOfRangeException(parameterName, $"Household slots run from 1 to {PhysicalSlots}.");
        }
    }
}
