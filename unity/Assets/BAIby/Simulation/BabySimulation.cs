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
        private readonly BondTunables bondTunables;
        private readonly BabyGrowth growth;
        private readonly TimeSpan sittingAge;
        private readonly TimeSpan crawlingAge;
        private readonly TimeSpan walkingAge;
        private readonly TimeSpan maxNeedDecay;
        private const double InitialEnergy = 80;
        private const double MilkFullness = 35;
        private const double BananaFullness = 28;
        private const double OatsFullness = 40;

        // The three ages are each milestone's nominal age: readiness moves it to between 0.8x and 1.3x of it with care,
        // and always unlocks it by GrowthTunables.Guarantee (1.4x).
        public BabySimulation(TimeSpan sittingAge, TimeSpan maxNeedDecay, TimeSpan crawlingAge, TimeSpan walkingAge,
            CareTunables careTunables = null, GrowthTunables growthTunables = null, BondTunables bondTunables = null)
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
            this.bondTunables = bondTunables ?? BondTunables.Default;
            this.bondTunables.Validate();
            this.sittingAge = sittingAge;
            this.crawlingAge = crawlingAge;
            this.walkingAge = walkingAge;
        }

        public BondTunables BondTunables => bondTunables;

        public BabyState Adopt(string name, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the baby.", nameof(name));

            var care = CareRules.Start(now, careTunables);
            var baby = new BabyState(name.Trim(), now, TimeSpan.Zero, 70, 75, InitialEnergy, 0, 0, false, false, 0, 0, 100, 100, false, NurseryTheme.Sage, false, 0,
                BabyAppearance.Default, care, null, BondRules.Seed(0, care), JournalRules.Start(now));
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
                state = Seeded(state);
                var settled = Develop(state, state.TimeCheckpoint, TimeSpan.Zero, BabyNeeds.From(state), state.Care, due,
                    due.Draft.Build());
                return (settled.State.With(journal: Chronicle(state, settled.State, due, null, false)),
                    settled.UnlockedSitting, settled.UnlockedCrawling, settled.UnlockedWalking);
            }

            state = Seeded(state);
            // What the ledger resolved over the gap drives Bond, temperament and the rhythm, in time order.
            var ledger = new List<LedgerEvent>();
            var care = CareRules.Advance(state.Care, now, careTunables, ledger);
            var bond = BondRules.Advance(state.Bond, state.Bonding, state.Care, ledger, now.UtcTicks, bondTunables, careTunables);
            var start = BabyNeeds.From(state);
            var effects = HealthEffects(state.Care, bond);
            var needs = BabyNeeds.Real(start, state.Care, care, maxNeedDecay, careTunables.AwayHoldLevel,
                careTunables.DayStart, effects);
            // Each sample sees the ledger, needs and Bond a pure advance from this state would reach at its instant.
            var step = growth.Real(state, now, at =>
            {
                if (at == now.UtcTicks)
                    return (care, needs, bond.Bond);
                var ledgerAt = CareRules.Advance(state.Care, new DateTimeOffset(at, TimeSpan.Zero).ToOffset(now.Offset), careTunables);
                var bondAt = BondRules.Advance(state.Bond, state.Bonding, state.Care, ledger, at, bondTunables, careTunables).Bond;
                return (ledgerAt, BabyNeeds.Real(start, state.Care, ledgerAt, maxNeedDecay, careTunables.AwayHoldLevel,
                    careTunables.DayStart, effects), bondAt);
            });
            // The first advance of a migrated ledger excuses the whole gap and moves nobody on the ladder.
            if (!state.Care.AwaitingAnchor)
            {
                care = CareConsequences.Settle(care, Snapshot(needs), careTunables);
                care = CareConsequences.SettleHealth(state.Care, care, needs.Health, careTunables);
            }

            if (step.ResampleAtEnd)
                step.Draft.Sample = growth.SampleNow(needs, care, bond.Bond, step.Draft.Engagement, now.UtcTicks);
            var development = step.Draft.Build();
            var advanced = Develop(state, now, now - state.TimeCheckpoint, needs, care, step, development);
            var setback = growth.Setback(development, advanced.State, state.Care, care);
            var result = advanced.State.With(bond: bond.Bond, bonding: bond.Record, development: setback);
            return (result.With(journal: Chronicle(state, result, step, bond, true)), advanced.UnlockedSitting,
                advanced.UnlockedCrawling, advanced.UnlockedWalking);
        }

        public (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvancePreview(BabyState state, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));

            // Preview days pass no real time, so no routine window passes: the ledger and the mood are
            // untouched (Covered), and the hours count as awake hours.
            // A recovering baby keeps its faster recovery while the visit's window is open at the checkpoint.
            // Readiness follows the needs as they fall through the preview hours; engagement and the ledger hold.
            // The care given since the last day mark first closes as a day for the rhythm and temperament.
            state = Seeded(state);
            var bond = BondRules.PreviewDay(state.Bond, state.Bonding, state.TimeCheckpoint.UtcTicks, bondTunables);
            var comfort = BondRules.SecurityOf(bond.Record, bondTunables) == SecurityPhase.Clingy ? bondTunables.ClingyComfort : 1;
            var effects = HealthEffects(state.Care, null);
            var boost = state.TimeCheckpoint.UtcTicks < effects.BoostUntil ? effects.RecoveryBoost : 1;
            var step = growth.Preview(state, elapsed, maxNeedDecay, boost, comfort);
            var advanced = Develop(state, state.TimeCheckpoint, elapsed,
                BabyNeeds.Awake(BabyNeeds.From(state), elapsed, maxNeedDecay, boost, comfort), state.Care, step, step.Draft.Build());
            var result = advanced.State.With(bonding: bond.Record);
            return (result.With(journal: Chronicle(state, result, step, bond, false)), advanced.UnlockedSitting,
                advanced.UnlockedCrawling, advanced.UnlockedWalking);
        }

        // The relationship: Bond's high-water mark, temperament and the care rhythm (seeded for an older save).
        public BondRecord Bonding(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return state.Bonding ?? BondRules.Seed(state.Bond, state.Care);
        }

        // The journal (seeded with the milestones already reached for an older save).
        public JournalRecord Journal(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return state.Journal ?? JournalRules.Seed(state.TimeCheckpoint, state.Age, state.CanSitAndReach, state.CanCrawl,
                state.CanWalk, sittingAge, crawlingAge, walkingAge, state.Care);
        }

        public SecurityPhase Security(BabyState state) => BondRules.SecurityOf(Bonding(state), bondTunables);

        public CuriosityPhase Curiosity(BabyState state) => BondRules.CuriosityOf(Bonding(state), bondTunables);

        // Photos the baby can still have taken today (three a day).
        public int PhotosLeft(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return JournalRules.PhotosLeft(Journal(state), state.Age);
        }

        // A photo for the journal: the moment, the stage shown and today's look, drawn from the pose art when the
        // journal is read. At most three a day; nothing changes past that. Advance to the current instant first.
        public (BabyState State, bool Taken) TakePhoto(BabyState state, PhotoMoment moment)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (!Enum.IsDefined(typeof(PhotoMoment), moment))
                throw new ArgumentOutOfRangeException(nameof(moment));

            var journal = Journal(state);
            var stage = state.ShowsWalking ? MotorSkill.Walking : state.ShowsCrawling ? MotorSkill.Crawling :
                state.ShowsSitting ? MotorSkill.Sitting : MotorSkill.None;
            var photo = JournalRules.Photo(journal, state.TimeCheckpoint, state.Age, moment, stage, state.Appearance);
            if (ReferenceEquals(photo, journal))
                return (state, false);

            return (Seeded(state).With(journal: photo), true);
        }

        // Whether the look can be worn: earned looks need the care rhythm that earns them (or are already worn).
        public bool CanWear(BabyState state, BabyAppearance look, bool unlockEarned = false)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (look == null)
                throw new ArgumentNullException(nameof(look));

            return unlockEarned || EarnedLooks.CanWear(state.Appearance, look, Bonding(state).LongestRhythm);
        }

        // A baby saved before Bond's record or the journal existed gets them on first use.
        private BabyState Seeded(BabyState state) =>
            state.Bonding != null && state.Journal != null ? state
                : state.With(bonding: Bonding(state), journal: Journal(state));

        // Journal entries for one step: milestones reached (on the day and at the instant they were reached), rhythm
        // stickers, a hard spell that ended, and week pages. `before` is seeded; `bond` is null when no day closed.
        private JournalRecord Chronicle(BabyState before, BabyState after, BabyGrowth.Step step, BondStep bond, bool real)
        {
            var milestones = new List<(MotorSkill, long)>();
            for (var stage = 0; stage < 3; stage++)
            {
                if (step.Unlocked(stage))
                    milestones.Add(((MotorSkill)(stage + 1), step.UnlockAges[stage]));
            }

            return JournalRules.Chronicle(before.Journal, before.TimeCheckpoint, before.Age, before.Care.Condition,
                after.TimeCheckpoint, after.Age, after.Care.Condition, milestones, bond, real, before.Bonding.TotalCareDays);
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
            state = Seeded(state);
            var journal = JournalRules.AfterCondition(state.Journal, state.Care.Condition, visit.Record.Condition,
                state.TimeCheckpoint, state.Age);
            return (state.With(health: visit.Health, care: visit.Record, journal: journal), reaction);
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
            var bonding = Bonding(state);
            return CareDiagnostics.Summary(state.Care, careTunables) + string.Format(CultureInfo.InvariantCulture,
                " | growth {0} {1:0.00} in {2:0.0}d | W {3:0.00} Q motor {4:0.00} learning {5:0.00} social {6:0.00} physical {7:0.00}{8}{9}",
                report.Next, report.Readiness, report.ExpectedIn.TotalDays, sample.Wellbeing, sample.Motor, sample.Learning,
                sample.Social, sample.Physical, sample.Mode == SampleMode.Normal ? "" : " " + sample.Mode,
                report.Shy == MotorSkill.None ? "" : " | shy " + report.Shy) + string.Format(CultureInfo.InvariantCulture,
                " | bond {0:0.0} high {1:0.0} today +{2:0}/-{3:0} | security {4:0} curiosity {5:0} | rhythm {6} best {7} days {8}",
                state.Bond, bonding.HighWater, bonding.Gained, bonding.Lost, bonding.Security, bonding.Curiosity, bonding.Rhythm,
                bonding.LongestRhythm, bonding.TotalCareDays);
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
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            state = Seeded(state);
            var satiety = state.Satiety;
            var comfort = state.Comfort;
            var energy = state.Energy;
            var cleanliness = state.Cleanliness;
            // Bond's raw gain from this action, before the day's cap and the doubling below the high-water mark.
            var gain = 0.0;
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
                    gain += bondTunables.SootheGain;
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
                    gain += 3;
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
                    gain += bondTunables.StoryGain;
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
                        gain += 2;
                        reaction = Reaction.NewSoundComforted;
                    }
                    else
                    {
                        energy = Math.Max(0, energy - 6);
                        gain += 5;
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

                    gain += bondTunables.PlayGain;
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

            // Bond: an on-time meal, and fussing or upset answered within half an hour of it starting.
            if (action == CareAction.Feed && care.LastReason == CareReason.MealOnTime)
                gain += bondTunables.MealGain;
            var answered = state.Care.Mood != CareMood.Content && care.Mood < state.Care.Mood &&
                state.Care.Checkpoint - state.Care.MoodSince <= bondTunables.AnswerWindow;
            if (answered)
                gain += bondTunables.AnswerGain;
            var careDay = care.CareDay;
            var bonded = BondRules.Gain(state.Bond, state.Bonding, careDay, gain, bondTunables);

            // Engagement (with the rhythm's bonus and a temperament's leaning), the day's variety, the setback's good
            // day and a fresh quality sample with the new Bond.
            var developed = growth.AfterCare(state, action, toy, reaction, care, cared,
                BondRules.RhythmBonus(state.Bonding, bondTunables), BondRules.CuriosityOf(state.Bonding, bondTunables),
                bondTunables.TemperamentEngagement, bonus =>
                {
                    if (bonus > 0)
                        bonded = BondRules.Gain(bonded.Bond, bonded.Record, careDay, bonus, bondTunables);
                    return bonded.Bond;
                });
            var journal = state.Journal;
            if (developed.Recovered)
            {
                reaction = Reaction.SkillBack;
                journal = JournalRules.SkillBack(journal, state.TimeCheckpoint, state.Age, Newest(state),
                    state.Development?.SetbackSince ?? state.TimeCheckpoint);
            }
            else if (developed.Bored)
            {
                reaction = Reaction.Bored;
            }

            journal = JournalRules.AfterCondition(journal, state.Care.Condition, care.Condition, state.TimeCheckpoint, state.Age);
            var meal = (tags & CareTags.Meal) != 0 && care.LastReason != CareReason.MealTooFull;
            var playKind = developed.Kind.HasValue && developed.Kind != EngagementKind.Meal && developed.Kind != EngagementKind.Hygiene
                ? developed.Kind : null;
            var bonding = BondRules.Tally(bonded.Record, careDay, meal, (tags & CareTags.Together) != 0,
                action == CareAction.Rest, answered, playKind);

            return (state.With(satiety: satiety, comfort: comfort, energy: energy,
                toyFamiliarity: rattleFamiliarity, plushFamiliarity: plushFamiliarity,
                bond: bonded.Bond, storySessions: storySessions, cleanliness: cleanliness,
                newSoundResolved: newSoundResolved, blockTowers: blockTowers, care: care,
                development: developed.Record, bonding: bonding, journal: journal), reaction);
        }

        // The skill a setback makes shy and a good day brings back: the newest one reached.
        private static MotorSkill Newest(BabyState state) =>
            state.CanWalk ? MotorSkill.Walking : state.CanCrawl ? MotorSkill.Crawling :
            state.CanSitAndReach ? MotorSkill.Sitting : MotorSkill.None;

        // Gains stop at the ceiling; a value already above it is kept.
        private static double Gain(double value, double amount, double ceiling) =>
            Math.Max(value, Math.Min(ceiling, value + amount));

        // What the condition at the start of a gap, and a clingy phase (which can begin or end when a care day closes
        // inside the gap), do to the needs over it.
        private BabyNeeds.Effects HealthEffects(CareRecord care, BondStep bond) =>
            BabyNeeds.EffectsFor(care, bond, careTunables, bondTunables);

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
