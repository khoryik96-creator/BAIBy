using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    public sealed class BabySimulation
    {
        // The baby's care verbs and the routine tags they carry. Stable IDs; append, never rename.
        public static readonly CareProfile BabyCareProfile = new CareProfile("baby", new Dictionary<string, CareTags>
        {
            ["feed"] = CareTags.Meal,
            ["soothe"] = CareTags.Together,
            ["play"] = CareTags.Together,
            ["rest"] = CareTags.Bedtime,
            ["read-story"] = CareTags.Together,
            ["change-diaper"] = CareTags.Hygiene,
            ["comfort-new-sound"] = CareTags.Together,
            ["explore-new-sound"] = CareTags.Together,
            ["bath"] = CareTags.Hygiene,
            ["stack-blocks"] = CareTags.Together,
        });

        private readonly CareTunables careTunables;
        private readonly TimeSpan sittingAge;
        private readonly TimeSpan maxNeedDecay;
        private readonly TimeSpan crawlingAge;
        private readonly TimeSpan walkingAge;
        private const double InitialEnergy = 80;
        private const double SatietyDecay = 10;
        private const double ComfortDecay = 5;
        private const double EnergyDecay = 8;
        private const double CleanlinessDecay = 14;
        private const double RecoveryThreshold = 60;
        private const double LowNeedThreshold = 30;
        private const double RecoveryPerHour = 6;
        private const double HealthLossPerHour = 5;

        public BabySimulation(TimeSpan sittingAge, TimeSpan maxNeedDecay, TimeSpan crawlingAge, TimeSpan walkingAge,
            CareTunables careTunables = null)
        {
            if (sittingAge <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(sittingAge));
            if (maxNeedDecay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxNeedDecay));

            if (crawlingAge <= sittingAge)
                throw new ArgumentOutOfRangeException(nameof(crawlingAge), "Crawling follows sitting.");

            if (walkingAge <= crawlingAge)
                throw new ArgumentOutOfRangeException(nameof(walkingAge), "Walking follows crawling.");

            this.sittingAge = sittingAge;
            this.maxNeedDecay = maxNeedDecay;
            this.crawlingAge = crawlingAge;
            this.walkingAge = walkingAge;
            this.careTunables = careTunables ?? CareTunables.Default;
            this.careTunables.Validate();
        }


        public BabyState Adopt(string name, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the baby.", nameof(name));

            return new BabyState(name.Trim(), now, TimeSpan.Zero, 70, 75, InitialEnergy, 0, 0, false, false, 0, 0, 100, 100, false, NurseryTheme.Sage, false, 0,
                BabyAppearance.Default, CareRules.Start(now, careTunables));
        }

        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) Advance(BabyState state, DateTimeOffset now)
        {
            if (now <= state.TimeCheckpoint)
                return AdvanceElapsed(state, state.TimeCheckpoint, TimeSpan.Zero, state.Care);

            return AdvanceElapsed(state, now, now - state.TimeCheckpoint, CareRules.Advance(state.Care, now, careTunables));
        }

        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvancePreview(BabyState state, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));

            // Preview days pass no real time, so no routine window passes: the ledger is untouched (Covered).
            return AdvanceElapsed(state, state.TimeCheckpoint, elapsed, state.Care);
        }

        // Read-only ledger summary for the Development panel.
        public string DescribeCare(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return CareDiagnostics.Summary(state.Care, careTunables);
        }

        private (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvanceElapsed(
            BabyState state, DateTimeOffset now, TimeSpan elapsed, CareRecord care)
        {
            var age = state.Age + elapsed;
            var canSit = state.CanSitAndReach || age >= sittingAge;
            var canCrawl = state.CanCrawl || age >= crawlingAge;
            var canWalk = state.CanWalk || age >= walkingAge;
            var unlockedSitting = canSit && !state.CanSitAndReach;
            var unlockedCrawling = canCrawl && !state.CanCrawl;
            var unlockedWalking = canWalk && !state.CanWalk;
            if (elapsed == TimeSpan.Zero && !unlockedSitting && !unlockedCrawling && !unlockedWalking)
                return (state, false, false, false);

            var decayHours = Math.Min(elapsed.TotalHours, maxNeedDecay.TotalHours);
            var updated = state.With(timeCheckpoint: now, age: age,
                satiety: Math.Max(0, state.Satiety - decayHours * SatietyDecay),
                comfort: Math.Max(0, state.Comfort - decayHours * ComfortDecay),
                energy: Math.Max(0, state.Energy - decayHours * EnergyDecay),
                canSitAndReach: canSit, canCrawl: canCrawl, canWalk: canWalk,
                cleanliness: Math.Max(0, state.Cleanliness - decayHours * CleanlinessDecay),
                health: HealthAfterDecay(state, decayHours),
                care: care);

            return (updated, unlockedSitting, unlockedCrawling, unlockedWalking);
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
            var blockTowers = state.BlockTowers;
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
                case CareAction.Bath:
                    cleanliness = Math.Min(100, cleanliness + 45);
                    comfort = Math.Min(100, comfort + 30);
                    energy = Math.Max(0, energy - 8);
                    reaction = Reaction.FreshBath;
                    break;
                case CareAction.StackBlocks:
                    if (!state.CanWalk)
                        throw new InvalidOperationException("Stacking blocks is unavailable before the walking milestone.");

                    energy = Math.Max(0, energy - 8);
                    connection += 3;
                    blockTowers = IncrementCount(blockTowers);
                    reaction = Reaction.StackedBlocks;
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

            // Shadow only: the ledger records the routine, and nothing above reads it.
            var care = CareRules.ApplyCare(state.Care, BabyCareProfile.TagsFor(CareVerbFor(action)), state.Satiety, careTunables);
            return (state.With(satiety: satiety, comfort: comfort, energy: energy,
                toyFamiliarity: rattleFamiliarity, plushFamiliarity: plushFamiliarity,
                connection: Math.Min(100, connection), storySessions: storySessions, cleanliness: cleanliness,
                newSoundResolved: newSoundResolved, blockTowers: blockTowers, care: care), reaction);
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
            if (state.CanWalk)
                return Reaction.WalksToToy;
            if (state.CanCrawl)
                return Reaction.CrawlsToToy;

            return state.CanSitAndReach ? Reaction.ReachesForToy : Reaction.TracksToy;
        }

        public static string CareVerbFor(CareAction action)
        {
            switch (action)
            {
                case CareAction.Feed: return "feed";
                case CareAction.Soothe: return "soothe";
                case CareAction.Play: return "play";
                case CareAction.Rest: return "rest";
                case CareAction.ReadStory: return "read-story";
                case CareAction.ChangeDiaper: return "change-diaper";
                case CareAction.ComfortNewSound: return "comfort-new-sound";
                case CareAction.ExploreNewSound: return "explore-new-sound";
                case CareAction.Bath: return "bath";
                case CareAction.StackBlocks: return "stack-blocks";
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static int IncrementCount(int count) => count == int.MaxValue ? count : count + 1;
    }
}
