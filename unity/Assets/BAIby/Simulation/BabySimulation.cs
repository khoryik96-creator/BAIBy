using System;

namespace BAIby.Simulation
{
    public sealed class BabySimulation
    {
        private readonly TimeSpan sittingAge;
        private readonly TimeSpan maxNeedDecay;
        internal const int MaxFamiliarity = 3;

        public BabySimulation(TimeSpan sittingAge, TimeSpan maxNeedDecay)
        {
            if (sittingAge <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(sittingAge));
            if (maxNeedDecay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxNeedDecay));

            this.sittingAge = sittingAge;
            this.maxNeedDecay = maxNeedDecay;
        }

        public BabyState Adopt(string name, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the baby.", nameof(name));

            return new BabyState(name.Trim(), now, TimeSpan.Zero, 70, 75, 0, false);
        }

        public (BabyState State, bool UnlockedSitting) Advance(BabyState state, DateTimeOffset now)
        {
            if (now <= state.TimeCheckpoint)
                return (state, false);

            return AdvanceElapsed(state, now, now - state.TimeCheckpoint);
        }

        public (BabyState State, bool UnlockedSitting) AdvancePreview(BabyState state, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));

            return AdvanceElapsed(state, state.TimeCheckpoint, elapsed);
        }

        private (BabyState State, bool UnlockedSitting) AdvanceElapsed(
            BabyState state, DateTimeOffset now, TimeSpan elapsed)
        {
            var decayHours = Math.Min(elapsed.TotalHours, maxNeedDecay.TotalHours);
            var age = state.Age + elapsed;
            var canSit = state.CanSitAndReach || age >= sittingAge;
            var updated = new BabyState(state.Name, now, age,
                Math.Max(0, state.Satiety - decayHours * 10),
                Math.Max(0, state.Comfort - decayHours * 5),
                state.ToyFamiliarity, canSit);

            return (updated, canSit && !state.CanSitAndReach);
        }

        public (BabyState State, Reaction Reaction) ApplyCare(BabyState state, CareAction action)
        {
            var satiety = state.Satiety;
            var comfort = state.Comfort;
            var familiarity = state.ToyFamiliarity;
            Reaction reaction;

            switch (action)
            {
                case CareAction.Feed:
                    satiety = Math.Min(100, satiety + 25);
                    reaction = Reaction.Satisfied;
                    break;
                case CareAction.Soothe:
                    comfort = Math.Min(100, comfort + 20);
                    reaction = Reaction.Calmed;
                    break;
                case CareAction.Play:
                    familiarity = Math.Min(MaxFamiliarity, familiarity + 1);
                    reaction = state.CanSitAndReach && familiarity >= 2
                        ? Reaction.ReachesForToy : Reaction.TracksToy;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }

            return (new BabyState(state.Name, state.TimeCheckpoint, state.Age,
                satiety, comfort, familiarity, state.CanSitAndReach), reaction);
        }
    }
}
