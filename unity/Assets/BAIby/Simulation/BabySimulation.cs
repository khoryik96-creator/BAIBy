using System;

namespace BAIby.Simulation
{
    public sealed class BabySimulation
    {
        private readonly TimeSpan sittingAge;
        private readonly TimeSpan maxNeedDecay;
        private readonly TimeSpan crawlingAge;
        private const double InitialEnergy = 80;

        public BabySimulation(TimeSpan sittingAge, TimeSpan maxNeedDecay, TimeSpan crawlingAge)
        {
            if (sittingAge <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(sittingAge));
            if (maxNeedDecay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxNeedDecay));

            if (crawlingAge <= sittingAge)
                throw new ArgumentOutOfRangeException(nameof(crawlingAge), "Crawling follows sitting.");

            this.sittingAge = sittingAge;
            this.maxNeedDecay = maxNeedDecay;
            this.crawlingAge = crawlingAge;
        }

        public BabyState Adopt(string name, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the baby.", nameof(name));

            return new BabyState(name.Trim(), now, TimeSpan.Zero, 70, 75, InitialEnergy, 0, 0, false, false, 0, 0);
        }

        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling) Advance(BabyState state, DateTimeOffset now)
        {
            if (now <= state.TimeCheckpoint)
                return AdvanceElapsed(state, state.TimeCheckpoint, TimeSpan.Zero);

            return AdvanceElapsed(state, now, now - state.TimeCheckpoint);
        }

        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling) AdvancePreview(BabyState state, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));

            return AdvanceElapsed(state, state.TimeCheckpoint, elapsed);
        }

        private (BabyState State, bool UnlockedSitting, bool UnlockedCrawling) AdvanceElapsed(
            BabyState state, DateTimeOffset now, TimeSpan elapsed)
        {
            var age = state.Age + elapsed;
            var canSit = state.CanSitAndReach || age >= sittingAge;
            var canCrawl = state.CanCrawl || age >= crawlingAge;
            var unlockedSitting = canSit && !state.CanSitAndReach;
            var unlockedCrawling = canCrawl && !state.CanCrawl;
            if (elapsed == TimeSpan.Zero && !unlockedSitting && !unlockedCrawling)
                return (state, false, false);

            var decayHours = Math.Min(elapsed.TotalHours, maxNeedDecay.TotalHours);
            var updated = new BabyState(state.Name, now, age,
                Math.Max(0, state.Satiety - decayHours * 10),
                Math.Max(0, state.Comfort - decayHours * 5),
                Math.Max(0, state.Energy - decayHours * 8),
                state.ToyFamiliarity, state.PlushFamiliarity, canSit, canCrawl, state.Connection, state.StorySessions);

            return (updated, unlockedSitting, unlockedCrawling);
        }

        public (BabyState State, Reaction Reaction) ApplyCare(BabyState state, CareAction action, Toy toy = Toy.Rattle)
        {
            var satiety = state.Satiety;
            var comfort = state.Comfort;
            var energy = state.Energy;
            var connection = state.Connection;
            var storySessions = state.StorySessions;
            var rattleFamiliarity = state.ToyFamiliarity;
            var plushFamiliarity = state.PlushFamiliarity;
            Reaction reaction;

            switch (action)
            {
                case CareAction.Feed:
                    satiety = Math.Min(100, satiety + 25);
                    reaction = Reaction.Satisfied;
                    break;
                case CareAction.Soothe:
                    comfort = Math.Min(100, comfort + 20);
                    connection += 3;
                    reaction = Reaction.Calmed;
                    break;
                case CareAction.Rest:
                    energy = Math.Min(100, energy + 35);
                    reaction = Reaction.Rested;
                    break;
                case CareAction.ReadStory:
                    energy = Math.Max(0, energy - 6);
                    connection += 2;
                    storySessions = IncrementCount(storySessions);
                    reaction = Reaction.EnjoysStory;
                    break;
                case CareAction.Play:
                    connection += 1;
                    energy = Math.Max(0, energy - 12);
                    switch (toy)
                    {
                        case Toy.Rattle:
                            rattleFamiliarity = IncrementCount(rattleFamiliarity);
                            break;
                        case Toy.Plush:
                            plushFamiliarity = IncrementCount(plushFamiliarity);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(toy));
                    }

                    var familiarity = toy == Toy.Rattle ? rattleFamiliarity : plushFamiliarity;
                    reaction = ToyReaction(state, familiarity);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }

            return (new BabyState(state.Name, state.TimeCheckpoint, state.Age,
                satiety, comfort, energy, rattleFamiliarity, plushFamiliarity, state.CanSitAndReach,
                state.CanCrawl, Math.Min(100, connection), storySessions), reaction);
        }

        private static Reaction ToyReaction(BabyState state, int familiarity)
        {
            if (familiarity < 2)
                return Reaction.TracksToy;
            if (state.CanCrawl)
                return Reaction.CrawlsToToy;

            return state.CanSitAndReach ? Reaction.ReachesForToy : Reaction.TracksToy;
        }

        private static int IncrementCount(int count) => count == int.MaxValue ? count : count + 1;
    }
}
