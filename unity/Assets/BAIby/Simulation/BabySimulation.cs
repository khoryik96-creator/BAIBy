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
            ["soothe"] = CareTags.Together | CareTags.Comfort,
            ["play"] = CareTags.Together,
            ["rest"] = CareTags.Bedtime,
            ["read-story"] = CareTags.Together,
            ["change-diaper"] = CareTags.Hygiene,
            ["comfort-new-sound"] = CareTags.Together | CareTags.Comfort,
            ["explore-new-sound"] = CareTags.Together,
            ["bath"] = CareTags.Hygiene,
            ["stack-blocks"] = CareTags.Together,
            ["medicine"] = CareTags.Medicine,
        });

        // The offline decay cap from the care retune: a missed day is felt without needs collapsing.
        public static readonly TimeSpan DefaultMaxNeedDecay = TimeSpan.FromHours(8);
        private readonly CareTunables careTunables;
        private readonly TimeSpan sittingAge;
        private readonly TimeSpan maxNeedDecay;
        private readonly TimeSpan crawlingAge;
        private readonly TimeSpan walkingAge;
        private const double InitialEnergy = 80;
        private const double MilkFullness = 35;
        private const double BananaFullness = 28;
        private const double OatsFullness = 40;

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

        // Real time: needs follow the local day and night, the ledger evaluates windows, and the needs axis and
        // the health axis each move at most one rung. Development keeps its nominal pace, Away mode included
        // (CareConsequences.Readiness is the factor slice 4's readiness timing will apply).
        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) Advance(BabyState state, DateTimeOffset now)
        {
            if (now <= state.TimeCheckpoint)
                return AdvanceElapsed(state, state.TimeCheckpoint, TimeSpan.Zero, BabyNeeds.From(state), state.Care);

            var care = CareRules.Advance(state.Care, now, careTunables);
            var needs = BabyNeeds.Real(BabyNeeds.From(state), state.Care, care, maxNeedDecay, careTunables.AwayHoldLevel,
                careTunables.DayStart, HealthEffects(state.Care));
            // The first advance of a migrated ledger excuses the whole gap and moves nobody on the ladder.
            if (!state.Care.AwaitingAnchor)
            {
                care = CareConsequences.Settle(care, Snapshot(needs), careTunables);
                care = CareConsequences.SettleHealth(state.Care, care, needs.Health, careTunables);
            }

            return AdvanceElapsed(state, now, now - state.TimeCheckpoint, needs, care);
        }

        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvancePreview(BabyState state, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));

            // Preview days pass no real time, so no routine window passes: the ledger and the mood are
            // untouched (Covered), and the hours count as awake hours.
            // A recovering baby keeps its faster recovery while the visit's window is open at the checkpoint.
            var effects = HealthEffects(state.Care);
            var boost = state.TimeCheckpoint.UtcTicks < effects.BoostUntil ? effects.RecoveryBoost : 1;
            return AdvanceElapsed(state, state.TimeCheckpoint, elapsed,
                BabyNeeds.Awake(BabyNeeds.From(state), elapsed, maxNeedDecay, boost), state.Care);
        }

        // Whether the doctor's visit is offered: the baby is under the weather or sick.
        public static bool CanVisitDoctor(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return state.Care.IsUnwell;
        }

        // The free doctor's visit (Dr. Lee): Health +15 at once, then Recovering with medicine to take, or home
        // well when the baby was only a little under the weather. Advance to the current instant first.
        public (BabyState State, Reaction Reaction) VisitDoctor(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (!CanVisitDoctor(state))
                return (state, Reaction.DoctorAllClear);

            var visit = CareConsequences.VisitDoctor(state.Care, state.Health, Snapshot(BabyNeeds.From(state)), careTunables);
            var reaction = visit.Record.Condition == CareCondition.Recovering ? Reaction.DoctorVisited : Reaction.DoctorAllClear;
            return (state.With(health: visit.Health, care: visit.Record), reaction);
        }

        // Whether a medicine dose would count now: only while recovering, one per meal window.
        public static MedicineState Medicine(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return CareConsequences.Medicine(state.Care);
        }

        // Read-only ledger summary for the Development panel.
        public string DescribeCare(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return CareDiagnostics.Summary(state.Care, careTunables);
        }

        // Today's routine windows at the state's checkpoint.
        public static IReadOnlyList<RoutineSlot> Routine(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return RoutineStrip.Today(state.Care);
        }

        // A planned absence of up to 14 days, free: windows are Covered, needs held, development at its nominal
        // pace. Advance to the current instant first; it starts at the state's checkpoint.
        public BabyState StartAway(BabyState state, TimeSpan duration)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return state.With(care: CareRules.StartAway(state.Care, duration));
        }

        public BabyState EndAway(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var care = CareRules.EndAway(state.Care);
            return ReferenceEquals(care, state.Care) ? state : state.With(care: care);
        }

        public BabyState ChangeCareStyle(BabyState state, CareStyle style)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var care = CareRules.ChangeStyle(state.Care, style);
            return ReferenceEquals(care, state.Care) ? state : state.With(care: care);
        }

        // Whether feeding now would overfeed: a mild Comfort and Cleanliness drop, never harm.
        public bool IsFull(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return state.Satiety >= careTunables.FullMealThreshold;
        }

        private (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvanceElapsed(
            BabyState state, DateTimeOffset now, TimeSpan elapsed, BabyNeeds.Values needs, CareRecord care)
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

            var updated = state.With(timeCheckpoint: now, age: age,
                satiety: needs.Satiety, comfort: needs.Comfort, energy: needs.Energy,
                canSitAndReach: canSit, canCrawl: canCrawl, canWalk: canWalk,
                cleanliness: needs.Cleanliness, health: needs.Health, care: care);

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
            var rules = careTunables.RulesFor(state.Care.Style);
            // A messy room cuts every comfort gain a little.
            var comfortGain = 1 - rules.MessComfortCut * state.Care.Mess;
            // An upset baby pushes toys away: play, stories and blocks give nothing.
            var refusesPlay = CareConsequences.RefusesPlay(state.Care);
            // A sick baby stays in bed: only food, comfort, rest and changes are accepted.
            var tooPoorly = CareConsequences.FeelsTooPoorly(state.Care);
            // Under the weather or sick, Energy cannot rise past the unwell cap.
            var energyCeiling = state.Care.IsUnwell ? careTunables.UnwellEnergyCap : 100;
            var medicine = CareConsequences.Medicine(state.Care);
            var tags = BabyCareProfile.TagsFor(CareVerbFor(action));
            Reaction reaction;

            switch (action)
            {
                case CareAction.Feed:
                    if (!CanFeed(state, food))
                        throw new InvalidOperationException("This food is unavailable for the baby.");

                    switch (food)
                    {
                        case Food.Milk:
                            satiety = Math.Min(100, satiety + MilkFullness);
                            energy = Gain(energy, 5, energyCeiling);
                            break;
                        case Food.Banana:
                            satiety = Math.Min(100, satiety + BananaFullness);
                            comfort = Math.Min(100, comfort + 8 * comfortGain);
                            break;
                        case Food.Oats:
                            satiety = Math.Min(100, satiety + OatsFullness);
                            break;
                    }

                    reaction = Reaction.Satisfied;
                    if (IsFull(state))
                    {
                        comfort = Math.Max(0, comfort - rules.OverfeedComfort);
                        cleanliness = Math.Max(0, cleanliness - rules.OverfeedCleanliness);
                        reaction = Reaction.TooFull;
                    }
                    break;
                case CareAction.Soothe:
                    comfort = Math.Min(100, comfort + 20 * comfortGain);
                    connection += 3;
                    reaction = Reaction.Calmed;
                    break;
                case CareAction.Rest:
                    energy = Gain(energy, 35, energyCeiling);
                    reaction = Reaction.Rested;
                    break;
                case CareAction.ChangeDiaper:
                    cleanliness = 100;
                    reaction = Reaction.FreshDiaper;
                    break;
                case CareAction.Bath:
                    if (tooPoorly)
                    {
                        reaction = Reaction.TooPoorly;
                        tags = CareTags.None;
                        break;
                    }

                    cleanliness = Math.Min(100, cleanliness + 45);
                    comfort = Math.Min(100, comfort + 30 * comfortGain);
                    energy = Math.Max(0, energy - 8);
                    reaction = Reaction.FreshBath;
                    break;
                case CareAction.StackBlocks:
                    if (!state.CanWalk)
                        throw new InvalidOperationException("Stacking blocks is unavailable before the walking milestone.");
                    if (tooPoorly)
                    {
                        reaction = Reaction.TooPoorly;
                        tags = CareTags.None;
                        break;
                    }

                    if (refusesPlay)
                    {
                        reaction = Reaction.PushesToyAway;
                        tags = CareTags.None;
                        break;
                    }

                    energy = Math.Max(0, energy - 8);
                    connection += 3;
                    blockTowers = IncrementCount(blockTowers);
                    reaction = Reaction.StackedBlocks;
                    break;
                case CareAction.ReadStory:
                    if (tooPoorly)
                    {
                        reaction = Reaction.TooPoorly;
                        tags = CareTags.None;
                        break;
                    }

                    if (refusesPlay)
                    {
                        reaction = Reaction.PushesToyAway;
                        tags = CareTags.None;
                        break;
                    }

                    energy = Math.Max(0, energy - 6);
                    connection += 2;
                    storySessions = IncrementCount(storySessions);
                    reaction = Reaction.EnjoysStory;
                    break;
                case CareAction.ComfortNewSound:
                case CareAction.ExploreNewSound:
                    if (!state.HasPendingNewSound)
                        throw new InvalidOperationException("The new sound event is unavailable.");
                    if (action == CareAction.ExploreNewSound && tooPoorly)
                    {
                        reaction = Reaction.TooPoorly;
                        tags = CareTags.None;
                        break;
                    }

                    newSoundResolved = true;
                    if (action == CareAction.ComfortNewSound)
                    {
                        comfort = Math.Min(100, comfort + 16 * comfortGain);
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
                    if (toy != Toy.Rattle && toy != Toy.Plush)
                        throw new ArgumentOutOfRangeException(nameof(toy));
                    if (tooPoorly)
                    {
                        reaction = Reaction.TooPoorly;
                        tags = CareTags.None;
                        break;
                    }

                    if (refusesPlay)
                    {
                        reaction = Reaction.PushesToyAway;
                        tags = CareTags.None;
                        break;
                    }

                    connection += 1;
                    energy = Math.Max(0, energy - 12);
                    if (toy == Toy.Rattle)
                        rattleFamiliarity = IncrementCount(rattleFamiliarity);
                    else
                        plushFamiliarity = IncrementCount(plushFamiliarity);

                    var familiarity = toy == Toy.Rattle ? rattleFamiliarity : plushFamiliarity;
                    reaction = ToyReaction(state, familiarity);
                    break;
                case CareAction.Medicine:
                    // One dose per meal window, only while recovering; otherwise nothing happens.
                    if (medicine != MedicineState.Ready)
                    {
                        reaction = Reaction.MedicineNotNow;
                        tags = CareTags.None;
                        break;
                    }

                    reaction = Reaction.MedicineTaken;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }

            // The ledger records the routine (and ends Away mode); then the right care calms the needs axis.
            var care = CareRules.ApplyCare(state.Care, tags, state.Satiety, careTunables);
            var cared = new BabyNeeds.Values { Satiety = satiety, Comfort = comfort, Energy = energy, Cleanliness = cleanliness };
            care = CareConsequences.AfterCare(care, tags, Snapshot(cared), careTunables);
            if (action == CareAction.Medicine && medicine == MedicineState.Ready)
            {
                care = CareConsequences.GiveMedicine(care, careTunables);
                if (care.Condition == CareCondition.Well)
                    reaction = Reaction.MedicineDone;
            }

            return (state.With(satiety: satiety, comfort: comfort, energy: energy,
                toyFamiliarity: rattleFamiliarity, plushFamiliarity: plushFamiliarity,
                connection: Math.Min(100, connection), storySessions: storySessions, cleanliness: cleanliness,
                newSoundResolved: newSoundResolved, blockTowers: blockTowers, care: care), reaction);
        }

        // Gains stop at the ceiling; a value already above it is kept.
        private static double Gain(double value, double amount, double ceiling) =>
            Math.Max(value, Math.Min(ceiling, value + amount));

        private BabyNeeds.Effects HealthEffects(CareRecord care)
        {
            var ceiling = care.IsUnwell ? careTunables.UnwellEnergyCap : 100;
            if (care.Condition != CareCondition.Recovering)
                return new BabyNeeds.Effects(ceiling, 1, long.MinValue);

            var since = care.ConditionSince.UtcTicks;
            var window = careTunables.RecoveryWindow.Ticks;
            var until = since > long.MaxValue - window ? long.MaxValue : since + window;
            return new BabyNeeds.Effects(ceiling, careTunables.RecoveryBoost, until);
        }

        private static NeedSnapshot Snapshot(BabyNeeds.Values needs) =>
            new NeedSnapshot(needs.Lowest, needs.Satiety, needs.Comfort);

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
                case CareAction.Medicine: return "medicine";
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static int IncrementCount(int count) => count == int.MaxValue ? count : count + 1;
    }
}
