using System;
using System.Collections.Generic;
using System.Globalization;

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
        private readonly BabyGrowth growth;
        private readonly TimeSpan maxNeedDecay;
        private const double InitialEnergy = 80;
        private const double MilkFullness = 35;
        private const double BananaFullness = 28;
        private const double OatsFullness = 40;

        // The three ages are each milestone's nominal age: readiness moves it to between 0.8x and 1.3x of it with care,
        // and always unlocks it by GrowthTunables.Guarantee (1.4x).
        public BabySimulation(TimeSpan sittingAge, TimeSpan maxNeedDecay, TimeSpan crawlingAge, TimeSpan walkingAge,
            CareTunables careTunables = null, GrowthTunables growthTunables = null)
        {
            if (sittingAge <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(sittingAge));
            if (maxNeedDecay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxNeedDecay));

            if (crawlingAge <= sittingAge)
                throw new ArgumentOutOfRangeException(nameof(crawlingAge), "Crawling follows sitting.");

            if (walkingAge <= crawlingAge)
                throw new ArgumentOutOfRangeException(nameof(walkingAge), "Walking follows crawling.");

            this.maxNeedDecay = maxNeedDecay;
            this.careTunables = careTunables ?? CareTunables.Default;
            this.careTunables.Validate();
            var growthRules = growthTunables ?? GrowthTunables.Default;
            growthRules.Validate();
            growth = new BabyGrowth(sittingAge, crawlingAge, walkingAge, this.careTunables, growthRules);
        }

        public BabyState Adopt(string name, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the baby.", nameof(name));

            var baby = new BabyState(name.Trim(), now, TimeSpan.Zero, 70, 75, InitialEnergy, 0, 0, false, false, 0, 0, 100, 100, false, NurseryTheme.Sage, false, 0,
                BabyAppearance.Default, CareRules.Start(now, careTunables));
            return baby.With(development: growth.Seed(baby));
        }

        // Real time: needs follow the local day and night, the ledger evaluates windows, and the needs axis and
        // the health axis each move at most one rung. Readiness grows with the Motor track's quality, sampled every
        // hour, and the condition at the start of the gap (BabyGrowth); a skill unlocks when it is ready, and always
        // by 1.4x its nominal age. While the player is present, neglect can make the newest skill shy (a setback).
        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) Advance(BabyState state, DateTimeOffset now)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (now <= state.TimeCheckpoint)
            {
                var due = growth.Settle(state);
                if (!due.AnyUnlocked)
                    return (state, false, false, false);
                return Develop(state, state.TimeCheckpoint, TimeSpan.Zero, BabyNeeds.From(state), state.Care, due, due.Draft.Build());
            }

            var care = CareRules.Advance(state.Care, now, careTunables);
            var start = BabyNeeds.From(state);
            var effects = HealthEffects(state.Care);
            var needs = BabyNeeds.Real(start, state.Care, care, maxNeedDecay, careTunables.AwayHoldLevel,
                careTunables.DayStart, effects);
            // Each sample sees the ledger and needs a pure advance from this state would reach at its instant.
            var step = growth.Real(state, now, at =>
            {
                if (at == now.UtcTicks)
                    return (care, needs);
                var ledger = CareRules.Advance(state.Care, new DateTimeOffset(at, TimeSpan.Zero).ToOffset(now.Offset), careTunables);
                return (ledger, BabyNeeds.Real(start, state.Care, ledger, maxNeedDecay, careTunables.AwayHoldLevel,
                    careTunables.DayStart, effects));
            });
            // The first advance of a migrated ledger excuses the whole gap and moves nobody on the ladder.
            if (!state.Care.AwaitingAnchor)
            {
                care = CareConsequences.Settle(care, Snapshot(needs), careTunables);
                care = CareConsequences.SettleHealth(state.Care, care, needs.Health, careTunables);
            }

            if (step.ResampleAtEnd)
                step.Draft.Sample = growth.SampleNow(needs, care, state.Connection, step.Draft.Engagement, now.UtcTicks);
            var development = step.Draft.Build();
            var advanced = Develop(state, now, now - state.TimeCheckpoint, needs, care, step, development);
            var setback = growth.Setback(development, advanced.State, state.Care, care);
            return ReferenceEquals(setback, development)
                ? advanced
                : (advanced.State.With(development: setback), advanced.UnlockedSitting, advanced.UnlockedCrawling,
                    advanced.UnlockedWalking);
        }

        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvancePreview(BabyState state, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));

            // Preview days pass no real time, so no routine window passes: the ledger and the mood are
            // untouched (Covered), and the hours count as awake hours.
            // A recovering baby keeps its faster recovery while the visit's window is open at the checkpoint.
            // Readiness follows the needs as they fall through the preview hours; engagement and the ledger hold.
            var effects = HealthEffects(state.Care);
            var boost = state.TimeCheckpoint.UtcTicks < effects.BoostUntil ? effects.RecoveryBoost : 1;
            var step = growth.Preview(state, elapsed, maxNeedDecay, boost);
            return Develop(state, state.TimeCheckpoint, elapsed,
                BabyNeeds.Awake(BabyNeeds.From(state), elapsed, maxNeedDecay, boost), state.Care, step, step.Draft.Build());
        }

        // The baby's development for the Growth panel and the report: track qualities, the growth chart band, and
        // when the next skill is likely at the current pace.
        public GrowthReport Growth(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return growth.Report(state);
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

        // Medicine doses that end Recovering.
        public int RecoveryDoses => careTunables.RecoveryDoses;

        // Read-only ledger summary for the Development panel.
        public string DescribeCare(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var report = growth.Report(state);
            var sample = report.Sample;
            return CareDiagnostics.Summary(state.Care, careTunables) + string.Format(CultureInfo.InvariantCulture,
                " | growth {0} {1:0.00} in {2:0.0}d | W {3:0.00} Q motor {4:0.00} learning {5:0.00} social {6:0.00} physical {7:0.00}{8}{9}",
                report.Next, report.Readiness, report.ExpectedIn.TotalDays, sample.Wellbeing, sample.Motor, sample.Learning,
                sample.Social, sample.Physical, sample.Mode == SampleMode.Normal ? "" : " " + sample.Mode,
                report.Shy == MotorSkill.None ? "" : " | shy " + report.Shy);
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

        private static (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) Develop(
            BabyState state, DateTimeOffset now, TimeSpan elapsed, BabyNeeds.Values needs, CareRecord care,
            BabyGrowth.Step step, DevelopmentRecord development)
        {
            var updated = state.With(timeCheckpoint: now, age: state.Age + elapsed,
                satiety: needs.Satiety, comfort: needs.Comfort, energy: needs.Energy,
                canSitAndReach: step.CanSitAndReach, canCrawl: step.CanCrawl, canWalk: step.CanWalk,
                cleanliness: needs.Cleanliness, health: needs.Health, care: care, development: development);

            return (updated, step.UnlockedSitting, step.UnlockedCrawling, step.UnlockedWalking);
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
            var cared = new BabyNeeds.Values { Satiety = satiety, Comfort = comfort, Energy = energy, Cleanliness = cleanliness,
                Health = state.Health };
            care = CareConsequences.AfterCare(care, tags, Snapshot(cared), careTunables);
            if (action == CareAction.Medicine && medicine == MedicineState.Ready)
            {
                care = CareConsequences.GiveMedicine(care, careTunables);
                if (care.Condition == CareCondition.Well)
                    reaction = Reaction.MedicineDone;
            }

            // Engagement, the day's variety, the setback's good day and a fresh quality sample.
            var developed = growth.AfterCare(state, action, toy, reaction, care, cared, Math.Min(100, connection));
            if (developed.Recovered)
                reaction = Reaction.SkillBack;
            else if (developed.Bored)
                reaction = Reaction.Bored;

            return (state.With(satiety: satiety, comfort: comfort, energy: energy,
                toyFamiliarity: rattleFamiliarity, plushFamiliarity: plushFamiliarity,
                connection: developed.Connection, storySessions: storySessions, cleanliness: cleanliness,
                newSoundResolved: newSoundResolved, blockTowers: blockTowers, care: care,
                development: developed.Record), reaction);
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
            // A shy skill shows the stage before it: the walker crawls to the toy.
            if (familiarity < 2)
                return Reaction.TracksToy;
            if (state.ShowsWalking)
                return Reaction.WalksToToy;
            if (state.ShowsCrawling)
                return Reaction.CrawlsToToy;

            return state.ShowsSitting ? Reaction.ReachesForToy : Reaction.TracksToy;
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
