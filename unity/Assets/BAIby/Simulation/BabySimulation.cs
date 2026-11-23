using System;

namespace BAIby.Simulation
{
    public sealed class BabySimulation
    {
        private readonly TimeSpan sittingAge;
        private readonly TimeSpan maxNeedDecay;
        private readonly TimeSpan crawlingAge;
        private const double InitialEnergy = 80;
        private const double SatietyDecay = 10;
        private const double ComfortDecay = 5;
        private const double EnergyDecay = 8;
        private const double CleanlinessDecay = 14;
        private const double RecoveryThreshold = 60;
        private const double LowNeedThreshold = 30;
        private const double RecoveryPerHour = 6;
        private const double HealthLossPerHour = 5;

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

            return new BabyState(name.Trim(), now, TimeSpan.Zero, 70, 75, InitialEnergy, 0, 0, false, false, 0, 0, 100, 100, false);
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
                Math.Max(0, state.Satiety - decayHours * SatietyDecay),
                Math.Max(0, state.Comfort - decayHours * ComfortDecay),
                Math.Max(0, state.Energy - decayHours * EnergyDecay),
                state.ToyFamiliarity, state.PlushFamiliarity, canSit, canCrawl, state.Connection, state.StorySessions,
                Math.Max(0, state.Cleanliness - decayHours * CleanlinessDecay), HealthAfterDecay(state, decayHours), state.NewSoundResolved);

            return (updated, unlockedSitting, unlockedCrawling);
        }

        public static bool CanFeed(BabyState state, Food food) =>
            food == Food.Milk || (state.CanCrawl && (food == Food.Banana || food == Food.Oats));

        public (BabyState State, Reaction Reaction) ApplyCare(BabyState state, CareAction action,
            Toy toy = Toy.Rattle, Food food = Food.Milk)
        {
            var satiety = state.Satiety;
            var comfort = state.Comfort;
            var energy = state.Energy;
            var cleanliness = state.Cleanliness;
            var connection = state.Connection;
            var storySessions = state.StorySessions;
            var newSoundResolved = state.NewSoundResolved;
            var rattleFamiliarity = state.ToyFamiliarity;
            var plushFamiliarity = state.PlushFamiliarity;
            Reaction reaction;

            switch (action)
            {
                case CareAction.Feed:
                    if (!CanFeed(state, food))
                        throw new InvalidOperationException("This food is unavailable for the baby.");

                    switch (food)
                    {
                        case Food.Milk:
                            satiety = Math.Min(100, satiety + 25);
                            energy = Math.Min(100, energy + 5);
                            break;
                        case Food.Banana:
                            satiety = Math.Min(100, satiety + 20);
                            comfort = Math.Min(100, comfort + 8);
                            break;
                        case Food.Oats:
                            satiety = Math.Min(100, satiety + 34);
                            break;
                    }

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
                case CareAction.ChangeDiaper:
                    cleanliness = 100;
                    reaction = Reaction.FreshDiaper;
                    break;
                case CareAction.ReadStory:
                    energy = Math.Max(0, energy - 6);
                    connection += 2;
                    storySessions = IncrementCount(storySessions);
                    reaction = Reaction.EnjoysStory;
                    break;
                case CareAction.ComfortNewSound:
                case CareAction.ExploreNewSound:
                    if (!state.HasPendingNewSound)
                        throw new InvalidOperationException("The new sound event is unavailable.");

                    newSoundResolved = true;
                    if (action == CareAction.ComfortNewSound)
                    {
                        comfort = Math.Min(100, comfort + 16);
                        connection += 2;
                        reaction = Reaction.NewSoundComforted;
                    }
                    else
                    {
                        energy = Math.Max(0, energy - 6);
                        connection += 5;
                        reaction = Reaction.NewSoundExplored;
                    }
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
                state.CanCrawl, Math.Min(100, connection), storySessions, cleanliness, state.Health, newSoundResolved), reaction);
        }

        private static double HealthAfterDecay(BabyState state, double hours)
        {
            // In an unattended interval, needs only fall. Recovery can precede a
            // neutral period and then deterioration. Use the actual time on each side.
            var recovering = Math.Min(hours, HoursUntilFirstLowNeed(state, RecoveryThreshold));
            var deteriorating = Math.Max(0, hours - HoursUntilFirstLowNeed(state, LowNeedThreshold));
            var recovered = Math.Min(100, state.Health + recovering * RecoveryPerHour);
            return Math.Max(0, recovered - deteriorating * HealthLossPerHour);
        }

        private static double HoursUntilFirstLowNeed(BabyState state, double threshold)
        {
            var satiety = (state.Satiety - threshold) / SatietyDecay;
            var comfort = (state.Comfort - threshold) / ComfortDecay;
            var energy = (state.Energy - threshold) / EnergyDecay;
            var cleanliness = (state.Cleanliness - threshold) / CleanlinessDecay;
            return Math.Max(0, Math.Min(Math.Min(satiety, comfort), Math.Min(energy, cleanliness)));
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
