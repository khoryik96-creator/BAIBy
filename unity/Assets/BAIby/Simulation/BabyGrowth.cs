using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // How a projected milestone compares with its nominal age. Not saved.
    public enum GrowthPace { OnTrack, Ahead, TakingTime }

    // The growth chart band from the Physical track: never weights or body labels. Not saved.
    public enum GrowthBand { Thriving, Steady, NeedsRegularMeals }

    // A read-out of the baby's development for the Growth panel and the Baby report.
    public sealed class GrowthReport
    {
        // The latest track qualities and Wellbeing.
        public TrackSample Sample { get; }
        public GrowthBand Band { get; }
        // The next motor skill (None once walking), readiness towards it, and when it is likely at the current
        // pace (at most its guarantee).
        public MotorSkill Next { get; }
        public double Readiness { get; }
        public TimeSpan ExpectedIn { get; }
        public GrowthPace Pace { get; }
        // Readiness is paused: the baby is sick and waits for the doctor.
        public bool Paused { get; }
        public MotorSkill Shy { get; }
        // Progress through the good day that ends a setback.
        public int RecoveryMeals { get; }
        public int RecoverySoothes { get; }

        internal GrowthReport(TrackSample sample, GrowthBand band, MotorSkill next, double readiness, TimeSpan expectedIn,
            GrowthPace pace, bool paused, MotorSkill shy, int recoveryMeals, int recoverySoothes)
        {
            Sample = sample;
            Band = band;
            Next = next;
            Readiness = readiness;
            ExpectedIn = expectedIn;
            Pace = pace;
            Paused = paused;
            Shy = shy;
            RecoveryMeals = recoveryMeals;
            RecoverySoothes = recoverySoothes;
        }

        public double Quality(DevelopmentTrack track) => Sample.Quality(track);
    }

    // The baby's development (docs/care-and-growth.md, Development tracks, Milestone timing and the Setback rung).
    //
    // - Readiness p towards the next motor skill grows at cond / (D x (1.30 - 0.50 Q)) a day, D being the stage's
    //   nominal length, Q the Motor quality and cond CareConsequences.Readiness at the start of the gap. Past the
    //   skill's nominal age with Wellbeing at 0.6 or more it grows x1.25. The skill unlocks at p 1 and always by
    //   1.4x its nominal age; then p starts again for the next stage. Skills never re-lock.
    // - Qualities are sampled on a fixed grid of UTC instants (every hour) and after every care action. A sample
    //   paces readiness until the next one, and each sample is computed from the state at the start of the gap,
    //   so within a session one long advance equals many short ones. Away mode holds the nominal pace; past the
    //   absence ceiling a helper holds Q 0.3.
    // - A baby saved before tracks existed (no record) is seeded with readiness from its age, and its first advance
    //   runs at the nominal pace, so migration never moves a milestone.
    // - Setback, only while the player is present, never re-locks: the newest skill goes shy and readiness drops by
    //   0.15. One good day (three on-time meals and two comforting actions) brings it back.
    internal sealed class BabyGrowth
    {
        private const int Stages = 3;
        private readonly long[] nominal = new long[Stages];
        private readonly long[] length = new long[Stages];
        private readonly long[] guarantee = new long[Stages];
        private readonly CareTunables care;
        private readonly GrowthTunables tunables;

        public BabyGrowth(TimeSpan sittingAge, TimeSpan crawlingAge, TimeSpan walkingAge, CareTunables care,
            GrowthTunables tunables)
        {
            this.care = care;
            this.tunables = tunables;
            nominal[0] = sittingAge.Ticks;
            nominal[1] = crawlingAge.Ticks;
            nominal[2] = walkingAge.Ticks;
            for (var i = 0; i < Stages; i++)
            {
                length[i] = nominal[i] - (i == 0 ? 0 : nominal[i - 1]);
                guarantee[i] = (long)Math.Ceiling(nominal[i] * tunables.Guarantee);
            }
        }

        public GrowthTunables Tunables => tunables;

        // The ledger and needs a pure advance from the state would reach at an instant (UTC ticks).
        public delegate (CareRecord Care, BabyNeeds.Values Needs) Projection(long at);

        // Readiness from age at the nominal pace, and a sample of the state as it is.
        public DevelopmentRecord Seed(BabyState state)
        {
            var stage = StageOf(state);
            var readiness = 0.0;
            if (stage < Stages)
            {
                var start = stage == 0 ? 0 : nominal[stage - 1];
                readiness = DevelopmentRules.Clamp01((double)(state.Age.Ticks - start) / length[stage]);
            }

            var empty = new List<EngagementEntry>();
            return new DevelopmentRecord.Builder
            {
                Readiness = readiness,
                Sample = SampleOf(SampleMode.Normal, BabyNeeds.From(state), state.Care, state.Connection, empty,
                    state.TimeCheckpoint.UtcTicks),
                Engagement = empty,
                CategoryDay = state.Care.CareDay,
                RecoveryDay = state.Care.CareDay,
            }.Build();
        }

        // Unlocks that are already due without time passing (readiness 1, or the guarantee age).
        public Step Settle(BabyState state)
        {
            var dev = state.Development ?? Seed(state);
            var run = new Run(this, state, dev);
            run.Check();
            return run.Finish();
        }

        // Real time from the state's checkpoint to `now`. The ledger and needs at each sample instant come from
        // `project`, which advances the state's own ledger and needs without settling the ladder.
        public Step Real(BabyState state, DateTimeOffset now, Projection project)
        {
            var seeded = state.Development == null;
            var dev = state.Development ?? Seed(state);
            var run = new Run(this, state, dev);
            var from = state.TimeCheckpoint.UtcTicks;
            var to = now.UtcTicks;
            run.Check();
            if (seeded || state.Care.AwaitingAnchor)
            {
                // Migration excuses the whole gap at the nominal pace; the caller samples the end.
                run.Advance(to - from, DevelopmentRules.NominalQuality, 0, 1, false, pace: 1);
                var step = run.Finish();
                step.ResampleAtEnd = true;
                return step;
            }

            var cond = CareConsequences.Readiness(state.Care);
            var absenceEnd = CareRules.AbsenceEnd(state.Care, care);
            var interval = tunables.SampleInterval.Ticks;
            var sample = dev.Sample;
            var sampledAt = long.MinValue;
            var t = from;
            while (t < to)
            {
                var next = NextGrid(t, interval);
                // Away mode and a helper hold the pace, so the samples in between change nothing.
                if (sample.Mode == SampleMode.Away)
                    next = Math.Max(next, CeilGrid(state.Care.AwayUntil.UtcTicks, interval));
                else if (sample.Mode == SampleMode.Helper)
                    next = long.MaxValue;
                var end = Math.Min(next, to);
                run.Cell(end - t, sample, cond, CountedLow(t, end, absenceEnd, sample));
                if (end == next)
                {
                    sample = SampleAt(state, project, next, absenceEnd);
                    sampledAt = next;
                    if (sample.Mode == SampleMode.Normal && sample.Wellbeing >= tunables.SetbackWellbeing)
                        run.Draft.LowWellbeingTime = TimeSpan.Zero;
                }

                t = end;
            }

            // A held sample is refreshed at the last grid instant, as short advances would have.
            var last = FloorGrid(to, interval);
            if (sample.Mode != SampleMode.Normal && last > from && last > sampledAt)
                sample = SampleAt(state, project, last, absenceEnd);
            run.Draft.Sample = sample;
            return run.Finish();
        }

        // A preview day: no real time passes, so the ledger and engagement hold and the hours count as awake hours.
        public Step Preview(BabyState state, TimeSpan elapsed, TimeSpan cap, double recoveryBoost)
        {
            var dev = state.Development ?? Seed(state);
            var run = new Run(this, state, dev);
            run.Check();
            var cond = CareConsequences.Readiness(state.Care);
            var mode = state.Care.IsAway ? SampleMode.Away : SampleMode.Normal;
            var start = BabyNeeds.From(state);
            var at = state.TimeCheckpoint.UtcTicks;
            var interval = tunables.SampleInterval.Ticks;
            var sample = dev.Sample;
            var hour = 0L;
            while (hour < elapsed.Ticks)
            {
                // Past the decay cap every sample is the same.
                var next = hour >= cap.Ticks ? long.MaxValue : hour + interval;
                var end = Math.Min(next, elapsed.Ticks);
                run.Cell(end - hour, sample, cond, 0);
                if (end == next)
                {
                    var needs = BabyNeeds.Awake(start, TimeSpan.FromTicks(next), cap, recoveryBoost);
                    sample = SampleOf(mode, needs, state.Care, state.Connection, dev.Engagement, at);
                }

                hour = end;
            }

            run.Draft.Sample = sample;
            return run.Finish();
        }

        // Samples the state as it is now (after care, or at the end of a migrated advance).
        public TrackSample SampleNow(BabyNeeds.Values needs, CareRecord record, double connection,
            IReadOnlyList<EngagementEntry> engagement, long at) =>
            SampleOf(record.IsAwayAt(new DateTimeOffset(at, TimeSpan.Zero)) ? SampleMode.Away : SampleMode.Normal,
                needs, record, connection, engagement, at);

        // The setback rung, after an advance: only while the player is present (an advance within the presence gap,
        // at least the sick dwell into the visit), never while away, and never in Gentle care.
        public DevelopmentRecord Setback(DevelopmentRecord dev, BabyState state, CareRecord before, CareRecord after)
        {
            if (dev.HasSetback || after.IsAway || after.AwaitingAnchor || before.AwaitingAnchor)
                return dev;
            var rules = care.RulesFor(after.Style);
            if (!rules.Setbacks)
                return dev;
            var newest = Newest(state);
            if (newest == MotorSkill.None)
                return dev;

            var at = after.Checkpoint;
            var present = !CareRules.StartedSession(before, after) && at - after.SessionStart >= rules.SickDwell;
            if (!present)
                return dev;

            var lowWellbeing = dev.LowWellbeingTime >= tunables.SetbackLowTime &&
                dev.Sample.Mode == SampleMode.Normal && dev.Sample.Wellbeing < tunables.SetbackWellbeing;
            var missedMeals = (after.Owed & CareTags.Meal) != 0 && LastMissed(after.Meals, tunables.SetbackMissedMeals);
            var sickTooLong = after.Condition == CareCondition.Sick && at - after.ConditionSince >= tunables.SetbackSickTime &&
                dev.SetbackSince < after.ConditionSince;
            if (!lowWellbeing && !missedMeals && !sickTooLong)
                return dev;

            var draft = dev.ToBuilder();
            draft.Shy = newest;
            draft.SetbackSince = at;
            draft.Readiness = Math.Max(0, dev.Readiness - tunables.SetbackReadiness);
            draft.LowWellbeingTime = TimeSpan.Zero;
            draft.RecoveryDay = after.CareDay;
            draft.RecoveryMeals = 0;
            draft.RecoverySoothes = 0;
            return draft.Build();
        }

        // What one care action does to development. `before` is the state before the action; `record` the ledger
        // after it (its LastReason says whether a meal was on time); `needs` and `connection` the values after it.
        public CareOutcome AfterCare(BabyState before, CareAction action, Toy toy, Reaction reaction, CareRecord record,
            BabyNeeds.Values needs, double connection)
        {
            var dev = before.Development ?? Seed(before);
            var draft = dev.ToBuilder();
            var at = record.Checkpoint.UtcTicks;
            var refused = reaction == Reaction.PushesToyAway || reaction == Reaction.TooPoorly ||
                reaction == Reaction.MedicineNotNow;
            var bored = false;
            var bond = 0.0;
            var kind = refused ? null : KindOf(action, toy, record.LastReason);
            if (kind.HasValue)
            {
                var wellbeing = Wellbeing(BabyNeeds.From(before));
                var repeats = DevelopmentRules.Repeats(dev.Engagement, kind.Value, at, tunables);
                var toyBit = ToyBit(kind.Value);
                var novel = toyBit != 0 && (dev.StageToys & toyBit) == 0;
                draft.StageToys |= toyBit;
                var log = DevelopmentRules.Prune(dev.Engagement, at, tunables);
                // Kept oldest first: an entry never predates one already kept.
                var stamp = log.Count > 0 ? Math.Max(at, log[log.Count - 1].At) : at;
                log.Add(new EngagementEntry(kind.Value, stamp, DevelopmentRules.Factor(repeats, wellbeing, novel, tunables)));
                draft.Engagement = DevelopmentRules.Prune(log, at, tunables);
                bored = IsPlay(kind.Value) && repeats >= tunables.BoredRepeats;
            }

            var category = refused ? CareCategories.None : CategoryOf(action);
            if (draft.CategoryDay != record.CareDay)
            {
                draft.CategoryDay = record.CareDay;
                draft.Categories = CareCategories.None;
                draft.VarietyGiven = false;
            }

            draft.Categories |= category;
            if (!draft.VarietyGiven && Count(draft.Categories) >= tunables.VarietyCategories)
            {
                draft.VarietyGiven = true;
                bond = tunables.VarietyBond;
            }

            var recovered = false;
            if (dev.HasSetback && !refused)
            {
                if (draft.RecoveryDay != record.CareDay)
                {
                    draft.RecoveryDay = record.CareDay;
                    draft.RecoveryMeals = 0;
                    draft.RecoverySoothes = 0;
                }

                if (action == CareAction.Feed && record.LastReason == CareReason.MealOnTime)
                    draft.RecoveryMeals++;
                if (action == CareAction.Soothe || action == CareAction.ComfortNewSound)
                    draft.RecoverySoothes++;
                if (draft.RecoveryMeals >= tunables.RecoveryMeals && draft.RecoverySoothes >= tunables.RecoverySoothes)
                {
                    draft.Shy = MotorSkill.None;
                    recovered = true;
                }
            }

            var total = Math.Min(100, connection + bond);
            draft.Sample = SampleNow(needs, record, total, draft.Engagement, at);
            if (draft.Sample.Wellbeing >= tunables.SetbackWellbeing)
                draft.LowWellbeingTime = TimeSpan.Zero;
            return new CareOutcome(draft.Build(), total, bored, recovered);
        }

        public GrowthReport Report(BabyState state)
        {
            var dev = state.Development ?? Seed(state);
            var sample = dev.Sample;
            var physical = sample.Physical;
            var band = physical >= 0.75 ? GrowthBand.Thriving : physical >= 0.5 ? GrowthBand.Steady : GrowthBand.NeedsRegularMeals;
            var run = new Run(this, state, dev);
            run.Check();
            var stage = run.Stage;
            var cond = CareConsequences.Readiness(state.Care);
            if (stage >= Stages)
                return new GrowthReport(sample, band, MotorSkill.None, 0, TimeSpan.Zero, GrowthPace.OnTrack, false, dev.Shy,
                    dev.RecoveryMeals, dev.RecoverySoothes);

            // The current pace held until the skill unlocks, never past its guarantee.
            var readiness = run.Draft.Readiness;
            run.UnlockAge = null;
            run.Cell(guarantee[stage] - run.Age + 1, sample, cond, 0, stopAtUnlock: true);
            var unlockAge = run.UnlockAge ?? guarantee[stage];
            var ratio = (double)unlockAge / nominal[stage];
            var pace = ratio < 0.95 ? GrowthPace.Ahead : ratio <= 1.05 ? GrowthPace.OnTrack : GrowthPace.TakingTime;
            return new GrowthReport(sample, band, (MotorSkill)(stage + 1), readiness,
                TimeSpan.FromTicks(Math.Max(0, unlockAge - state.Age.Ticks)), pace, cond <= 0, dev.Shy,
                dev.RecoveryMeals, dev.RecoverySoothes);
        }

        // The age at which each skill unlocks from birth at a steady sample and condition, advanced in steps of `step`
        // (for checks and tuning: the same integration as real time).
        public TimeSpan[] Schedule(TrackSample sample, double cond, TimeSpan step)
        {
            var run = new Run(this, 0, false, false, false, new DevelopmentRecord.Builder { Sample = sample });
            var ages = new TimeSpan[Stages];
            var stage = 0;
            while (stage < Stages)
            {
                run.Cell(step.Ticks, sample, cond, 0);
                for (; stage < run.Stage; stage++)
                    ages[stage] = TimeSpan.FromTicks(run.UnlockAges[stage]);
            }

            return ages;
        }

        // The skill a setback makes shy: the newest one reached.
        public static MotorSkill Newest(BabyState state) =>
            state.CanWalk ? MotorSkill.Walking : state.CanCrawl ? MotorSkill.Crawling :
            state.CanSitAndReach ? MotorSkill.Sitting : MotorSkill.None;

        public static double Wellbeing(BabyNeeds.Values needs) =>
            DevelopmentRules.Wellbeing(needs.Satiety, needs.Comfort, needs.Energy, needs.Cleanliness);

        private TrackSample SampleAt(BabyState state, Projection project, long at, long absenceEnd)
        {
            var mode = state.Care.IsAwayAt(new DateTimeOffset(at, TimeSpan.Zero)) ? SampleMode.Away
                : at >= absenceEnd ? SampleMode.Helper : SampleMode.Normal;
            var projected = project(at);
            var dev = state.Development;
            return SampleOf(mode, projected.Needs, projected.Care, state.Connection, dev.Engagement, at);
        }

        private TrackSample SampleOf(SampleMode mode, BabyNeeds.Values needs, CareRecord record, double connection,
            IReadOnlyList<EngagementEntry> engagement, long at) =>
            DevelopmentRules.Sample(mode, Wellbeing(needs), record.Meals.Credit(care.MealRing, care.LateCredit),
                connection / 100, needs.Health / 100, engagement, at, tunables);

        // Counted time with low Wellbeing in a stretch: only under the normal rules, up to the absence ceiling.
        private long CountedLow(long from, long to, long absenceEnd, TrackSample sample)
        {
            if (sample.Mode != SampleMode.Normal || sample.Wellbeing >= tunables.SetbackWellbeing)
                return 0;

            return Math.Max(0, Math.Min(to, absenceEnd) - from);
        }

        private static bool LastMissed(CareRing meals, int count)
        {
            if (meals.Count < count)
                return false;
            for (var i = meals.Count - count; i < meals.Count; i++)
            {
                if (meals[i] != CareMark.Missed)
                    return false;
            }

            return true;
        }

        private static EngagementKind? KindOf(CareAction action, Toy toy, CareReason reason)
        {
            switch (action)
            {
                case CareAction.Play: return toy == Toy.Plush ? EngagementKind.PlayPlush : EngagementKind.PlayRattle;
                case CareAction.ReadStory: return EngagementKind.Story;
                case CareAction.StackBlocks: return EngagementKind.Blocks;
                case CareAction.Soothe: return EngagementKind.Soothe;
                case CareAction.ComfortNewSound: return EngagementKind.ComfortSound;
                case CareAction.ExploreNewSound: return EngagementKind.ExploreSound;
                case CareAction.ChangeDiaper:
                case CareAction.Bath: return EngagementKind.Hygiene;
                // Only an on-time meal counts as engagement.
                case CareAction.Feed: return reason == CareReason.MealOnTime ? EngagementKind.Meal : (EngagementKind?)null;
                default: return null;
            }
        }

        private static CareCategories CategoryOf(CareAction action)
        {
            switch (action)
            {
                case CareAction.Feed: return CareCategories.Meal;
                case CareAction.Soothe:
                case CareAction.ComfortNewSound: return CareCategories.Comfort;
                case CareAction.Play:
                case CareAction.StackBlocks:
                case CareAction.ExploreNewSound: return CareCategories.Play;
                case CareAction.ReadStory: return CareCategories.Story;
                case CareAction.ChangeDiaper:
                case CareAction.Bath: return CareCategories.Hygiene;
                case CareAction.Rest: return CareCategories.Rest;
                default: return CareCategories.None;
            }
        }

        private static bool IsPlay(EngagementKind kind) =>
            kind == EngagementKind.PlayRattle || kind == EngagementKind.PlayPlush || kind == EngagementKind.Story ||
            kind == EngagementKind.Blocks || kind == EngagementKind.ExploreSound;

        private static int ToyBit(EngagementKind kind)
        {
            switch (kind)
            {
                case EngagementKind.PlayRattle: return 1;
                case EngagementKind.PlayPlush: return 2;
                case EngagementKind.Blocks: return 4;
                default: return 0;
            }
        }

        private static int Count(CareCategories categories)
        {
            var count = 0;
            for (var bits = (int)categories; bits != 0; bits &= bits - 1)
                count++;
            return count;
        }

        private static int StageOf(BabyState state) =>
            state.CanWalk ? 3 : state.CanCrawl ? 2 : state.CanSitAndReach ? 1 : 0;

        // The first grid instant after `at`, and the grid instants at or around one.
        private static long NextGrid(long at, long interval)
        {
            var floor = FloorGrid(at, interval);
            return floor > long.MaxValue - interval ? long.MaxValue : floor + interval;
        }

        private static long FloorGrid(long at, long interval)
        {
            var remainder = at % interval;
            return remainder < 0 ? at - remainder - interval : at - remainder;
        }

        private static long CeilGrid(long at, long interval)
        {
            var floor = FloorGrid(at, interval);
            return floor == at ? at : NextGrid(at, interval);
        }

        // The result of one development step.
        internal sealed class Step
        {
            public DevelopmentRecord.Builder Draft { get; }
            public bool CanSitAndReach { get; }
            public bool CanCrawl { get; }
            public bool CanWalk { get; }
            public bool UnlockedSitting { get; }
            public bool UnlockedCrawling { get; }
            public bool UnlockedWalking { get; }
            // The caller samples the advanced state at the end (a migrated advance).
            public bool ResampleAtEnd { get; set; }

            public bool AnyUnlocked => UnlockedSitting || UnlockedCrawling || UnlockedWalking;

            public Step(DevelopmentRecord.Builder draft, bool[] can, bool[] unlocked)
            {
                Draft = draft;
                CanSitAndReach = can[0];
                CanCrawl = can[1];
                CanWalk = can[2];
                UnlockedSitting = unlocked[0];
                UnlockedCrawling = unlocked[1];
                UnlockedWalking = unlocked[2];
            }
        }

        internal readonly struct CareOutcome
        {
            public readonly DevelopmentRecord Record;
            public readonly double Connection;
            public readonly bool Bored;
            public readonly bool Recovered;

            public CareOutcome(DevelopmentRecord record, double connection, bool bored, bool recovered)
            {
                Record = record;
                Connection = connection;
                Bored = bored;
                Recovered = recovered;
            }
        }

        // Readiness integration over stretches of constant quality, with unlocks inside a stretch.
        private sealed class Run
        {
            private readonly BabyGrowth growth;
            private readonly bool[] can = new bool[Stages];
            private readonly bool[] unlocked = new bool[Stages];

            public DevelopmentRecord.Builder Draft { get; }
            public long Age { get; private set; }
            public int Stage { get; private set; }
            public long? UnlockAge { get; set; }
            public long[] UnlockAges { get; } = new long[Stages];

            public Run(BabyGrowth growth, BabyState state, DevelopmentRecord dev)
                : this(growth, state.Age.Ticks, state.CanSitAndReach, state.CanCrawl, state.CanWalk, dev.ToBuilder())
            {
            }

            public Run(BabyGrowth growth, long age, bool sitting, bool crawling, bool walking, DevelopmentRecord.Builder draft)
            {
                this.growth = growth;
                Draft = draft;
                Age = age;
                can[0] = sitting;
                can[1] = sitting && crawling;
                can[2] = sitting && crawling && walking;
                Stage = can[2] ? 3 : can[1] ? 2 : can[0] ? 1 : 0;
            }

            // One stretch of `ticks` paced by `sample`; `low` ticks of it count towards low Wellbeing.
            public void Cell(long ticks, TrackSample sample, double cond, long low, bool stopAtUnlock = false)
            {
                var tunables = growth.tunables;
                switch (sample.Mode)
                {
                    case SampleMode.Away:
                        Advance(ticks, tunables.AwayQuality, sample.Wellbeing, 1, false, stopAtUnlock);
                        break;
                    case SampleMode.Helper:
                        Advance(ticks, tunables.HelperQuality, sample.Wellbeing, cond, false, stopAtUnlock);
                        break;
                    default:
                        Advance(ticks, sample.Motor, sample.Wellbeing, cond, true, stopAtUnlock);
                        break;
                }

                if (low > 0)
                    Draft.LowWellbeingTime += TimeSpan.FromTicks(low);
            }

            public void Advance(long ticks, double quality, double wellbeing, double cond, bool catchUp,
                bool stopAtUnlock = false, double? pace = null)
            {
                var tunables = growth.tunables;
                var boosted = catchUp && wellbeing >= tunables.CatchUpWellbeing;
                while (ticks > 0 && Stage < Stages)
                {
                    if (Check() && stopAtUnlock)
                        return;
                    if (Stage >= Stages)
                        break;

                    var nominal = growth.nominal[Stage];
                    var segment = Math.Min(ticks, growth.guarantee[Stage] - Age);
                    if (boosted && Age < nominal)
                        segment = Math.Min(segment, nominal - Age);
                    var rate = Math.Max(0, cond) / (growth.length[Stage] * (pace ?? DevelopmentRules.Pace(quality)));
                    if (boosted && Age >= nominal)
                        rate *= tunables.CatchUp;

                    if (rate > 0)
                    {
                        var need = (1 - Draft.Readiness) / rate;
                        if (need <= segment)
                        {
                            // A thousandth of a tick absorbs rounding, so an exact stage lands on its tick.
                            var step = Math.Min(segment, Math.Max(0, (long)Math.Ceiling(need - 0.001)));
                            Age += step;
                            ticks -= step;
                            Unlock();
                            if (stopAtUnlock)
                                return;
                            continue;
                        }

                        Draft.Readiness = Math.Min(1, Draft.Readiness + rate * segment);
                    }

                    Age += segment;
                    ticks -= segment;
                }

                if (ticks > 0)
                    Age += ticks;
                Check();
            }

            // Unlocks every skill already due. Returns whether any unlocked.
            public bool Check()
            {
                var any = false;
                while (Stage < Stages && (Draft.Readiness >= 1 || Age >= growth.guarantee[Stage]))
                {
                    Unlock();
                    any = true;
                }

                return any;
            }

            public Step Finish() => new Step(Draft, can, unlocked);

            private void Unlock()
            {
                can[Stage] = true;
                unlocked[Stage] = true;
                UnlockAge = UnlockAge ?? Age;
                UnlockAges[Stage] = Age;
                Draft.Readiness = 0;
                Draft.StageToys = 0;
                Stage++;
            }
        }
    }
}
