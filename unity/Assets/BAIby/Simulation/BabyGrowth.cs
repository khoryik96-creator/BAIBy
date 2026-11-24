using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // How a projected milestone compares with its nominal age. Not saved.
    public enum GrowthPace { OnTrack, Ahead, TakingTime }

    // The growth chart band from the Physical track: never weights or body labels. Not saved.
    public enum GrowthBand { Thriving, Steady, NeedsRegularMeals }

    // A read-out of a companion's development for the Growth panel and the report.
    public sealed class GrowthReport
    {
        // The latest track qualities and Wellbeing.
        public TrackSample Sample { get; }
        public GrowthBand Band { get; }
        // The next motor skill (None once the last is reached), readiness towards it, and when it is likely at the
        // current pace (at most its guarantee).
        public MotorSkill Next { get; }
        public double Readiness { get; }
        public TimeSpan ExpectedIn { get; }
        public GrowthPace Pace { get; }
        // Readiness is paused: the little one is sick and waits for the doctor or the vet.
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

        // First words for a baby (null for a species without them).
        public WordsReport Words { get; }

        internal GrowthReport(TrackSample sample, GrowthBand band, MotorSkill next, double readiness, TimeSpan expectedIn,
            GrowthPace pace, bool paused, MotorSkill shy, int recoveryMeals, int recoverySoothes, WordsReport words)
            : this(sample, band, next, readiness, expectedIn, pace, paused, shy, recoveryMeals, recoverySoothes)
        {
            Words = words;
        }
    }

    // First words for the Growth panel: reached, or readiness and when they are likely at the current pace.
    public sealed class WordsReport
    {
        public bool Reached { get; }
        public double Readiness { get; }
        public TimeSpan ExpectedIn { get; }
        public GrowthPace Pace { get; }

        internal WordsReport(bool reached, double readiness, TimeSpan expectedIn, GrowthPace pace)
        {
            Reached = reached;
            Readiness = readiness;
            ExpectedIn = expectedIn;
            Pace = pace;
        }
    }

    // Development (docs/care-and-growth.md, Development tracks, Milestone timing and the Setback rung), for the baby and,
    // through Subject, for any species with three milestones (a cat's pounce, climb and high jump).
    //
    // - Readiness p towards the next motor skill grows at cond / (D x (1.30 - 0.50 Q)) a day, D being the stage's
    //   nominal length, Q the Motor quality and cond CareConsequences.Readiness at the start of the gap. Past the
    //   skill's nominal age with Wellbeing at 0.6 or more it grows x1.25. The skill unlocks at p 1 and always by
    //   1.4x its nominal age; then p starts again for the next stage. Skills never re-lock.
    // - Qualities are sampled on a fixed grid of UTC instants (every hour) and after every care action. A sample
    //   paces readiness until the next one, and each sample is computed from the state at the start of the gap,
    //   so within a session one long advance equals many short ones. Away mode holds the nominal pace; past the
    //   absence ceiling a helper holds Q 0.3.
    // - A companion saved before tracks existed (no record) is seeded with readiness from its age, and its first advance
    //   runs at the nominal pace, so migration never moves a milestone.
    // - Setback, only while the player is present, never re-locks: the newest skill goes shy and readiness drops by
    //   0.15. One good day (three on-time meals, or every meal of a routine with fewer, and two comforting actions)
    //   brings it back.
    // - A baby's Learning track has one milestone in the game, First words (nominal day 16). Babbles (4) and Favourite
    //   story (9) have no milestone yet, so First words' stage starts at day 9 (D = 7 days) and its readiness follows
    //   the same rules with the Learning quality: cond, catch-up, at most 1.4x its nominal age, never re-locking, paused
    //   while sick. Before day 9 nothing is counted or saved. A record from before First words (LearningStarted false)
    //   past day 9 is seeded from age at the nominal pace, as slice 4 seeded Motor readiness.
    internal sealed class BabyGrowth
    {
        private const int Stages = 3;
        private readonly bool hasWords;
        private readonly long wordsStart;
        private readonly long wordsNominal;
        private readonly long wordsLength;
        private readonly long wordsGuarantee;
        private readonly long[] nominal = new long[Stages];
        private readonly long[] length = new long[Stages];
        private readonly long[] guarantee = new long[Stages];
        private readonly MotorSkill[] skills;
        private readonly BabyNeeds.NeedRates rates;
        private readonly CareTunables care;
        private readonly GrowthTunables tunables;

        public BabyGrowth(TimeSpan sittingAge, TimeSpan crawlingAge, TimeSpan walkingAge, CareTunables care,
            GrowthTunables tunables)
            : this(new[] { sittingAge, crawlingAge, walkingAge },
                new[] { MotorSkill.Sitting, MotorSkill.Crawling, MotorSkill.Walking }, care, tunables, BabyNeeds.NeedRates.Baby,
                (LearningStart, FirstWordsAge))
        {
        }

        // First words' nominal age, and the start of its stage (Favourite story's nominal day, not a milestone yet).
        public static readonly TimeSpan FirstWordsAge = TimeSpan.FromDays(16);
        public static readonly TimeSpan LearningStart = TimeSpan.FromDays(9);

        // Three milestones at their nominal ages, in order, and the needs' rates for preview days. `words` gives a
        // Learning milestone's stage start and nominal age (the baby's First words); null for none.
        public BabyGrowth(TimeSpan[] ages, MotorSkill[] skills, CareTunables care, GrowthTunables tunables,
            BabyNeeds.NeedRates rates, (TimeSpan Start, TimeSpan Nominal)? words = null)
        {
            if (words.HasValue)
            {
                if (words.Value.Start < TimeSpan.Zero || words.Value.Nominal <= words.Value.Start)
                    throw new ArgumentOutOfRangeException(nameof(words), "First words follow the start of their stage.");
                hasWords = true;
                wordsStart = words.Value.Start.Ticks;
                wordsNominal = words.Value.Nominal.Ticks;
                wordsLength = wordsNominal - wordsStart;
                wordsGuarantee = (long)Math.Ceiling(wordsNominal * tunables.Guarantee);
            }

            if (ages == null || ages.Length != Stages || skills == null || skills.Length != Stages)
                throw new ArgumentException("Growth needs three milestones.", nameof(ages));
            this.care = care;
            this.tunables = tunables;
            this.skills = (MotorSkill[])skills.Clone();
            this.rates = rates;
            for (var i = 0; i < Stages; i++)
            {
                nominal[i] = ages[i].Ticks;
                if (nominal[i] <= (i == 0 ? 0 : nominal[i - 1]))
                    throw new ArgumentOutOfRangeException(nameof(ages), "Each milestone follows the one before.");
                length[i] = nominal[i] - (i == 0 ? 0 : nominal[i - 1]);
                guarantee[i] = (long)Math.Ceiling(nominal[i] * tunables.Guarantee);
            }
        }

        public GrowthTunables Tunables => tunables;

        // The milestones, in order.
        public IReadOnlyList<MotorSkill> Skills => skills;

        // The ledger and needs a pure advance from the state would reach at an instant (UTC ticks).
        public delegate (CareRecord Care, BabyNeeds.Values Needs, double Bond) Projection(long at);

        // What growth reads from a companion: its age, how many milestones it has reached, its development record (null
        // before it is seeded), its ledger, needs and Bond, and its checkpoint.
        internal readonly struct Subject
        {
            public readonly long Age;
            public readonly int Stage;
            public readonly DevelopmentRecord Development;
            public readonly CareRecord Care;
            public readonly BabyNeeds.Values Needs;
            public readonly double Bond;
            public readonly DateTimeOffset Checkpoint;

            public Subject(long age, int stage, DevelopmentRecord development, CareRecord care, BabyNeeds.Values needs,
                double bond, DateTimeOffset checkpoint)
            {
                Age = age;
                Stage = stage;
                Development = development;
                Care = care;
                Needs = needs;
                Bond = bond;
                Checkpoint = checkpoint;
            }
        }

        // What one care action counts as: its engagement kind (null for none, or when refused), its kind of care for the
        // day's variety, whether it was refused, a meal (an on-time one counts towards a setback's good day) or comforting.
        internal readonly struct CareKind
        {
            public readonly EngagementKind? Kind;
            public readonly CareCategories Category;
            public readonly bool Refused;
            public readonly bool Meal;
            public readonly bool Comforting;

            public CareKind(EngagementKind? kind, CareCategories category, bool refused, bool meal, bool comforting)
            {
                Kind = refused ? null : kind;
                Category = refused ? CareCategories.None : category;
                Refused = refused;
                Meal = meal;
                Comforting = comforting;
            }
        }

        public static Subject Of(BabyState state) =>
            new Subject(state.Age.Ticks, StageOf(state), state.Development, state.Care, BabyNeeds.From(state), state.Bond,
                state.TimeCheckpoint);

        // Readiness from age at the nominal pace, and a sample of the state as it is.
        public DevelopmentRecord Seed(BabyState state) => Seed(Of(state));

        public DevelopmentRecord Seed(Subject state)
        {
            var stage = state.Stage;
            var readiness = 0.0;
            if (stage < Stages)
            {
                var start = stage == 0 ? 0 : nominal[stage - 1];
                readiness = DevelopmentRules.Clamp01((double)(state.Age - start) / length[stage]);
            }

            var empty = new List<EngagementEntry>();
            var draft = new DevelopmentRecord.Builder
            {
                Readiness = readiness,
                Sample = SampleOf(SampleMode.Normal, state.Needs, state.Care, state.Bond, empty, state.Checkpoint.UtcTicks),
                Engagement = empty,
                CategoryDay = state.Care.CareDay,
                RecoveryDay = state.Care.CareDay,
            };
            SeedWords(draft, state.Age);
            return draft.Build();
        }

        // Whether this species has First words (the baby).
        public bool HasWords => hasWords;

        // First words' readiness from age at the nominal pace, once its stage has started and when not already counted.
        private void SeedWords(DevelopmentRecord.Builder draft, long age)
        {
            if (!hasWords || draft.LearningStarted || draft.FirstWords || age < wordsStart)
                return;
            draft.LearningStarted = true;
            draft.Words = DevelopmentRules.Clamp01((double)(age - wordsStart) / wordsLength);
        }

        // Unlocks that are already due without time passing (readiness 1, or the guarantee age).
        public Step Settle(BabyState state) => Settle(Of(state));

        public Step Settle(Subject state)
        {
            var dev = state.Development ?? Seed(state);
            var run = new Run(this, state, dev);
            run.Check();
            return run.Finish();
        }

        // Real time from the state's checkpoint to `now`. The ledger and needs at each sample instant come from
        // `project`, which advances the state's own ledger and needs without settling the ladder.
        public Step Real(BabyState state, DateTimeOffset now, Projection project) => Real(Of(state), now, project);

        public Step Real(Subject state, DateTimeOffset now, Projection project)
        {
            var seeded = state.Development == null;
            var dev = state.Development ?? Seed(state);
            var run = new Run(this, state, dev);
            var from = state.Checkpoint.UtcTicks;
            var to = now.UtcTicks;
            run.Check();
            if (seeded || state.Care.AwaitingAnchor)
            {
                // Migration excuses the whole gap at the nominal pace; the caller samples the end.
                run.AdvanceWords(to - from, DevelopmentRules.NominalQuality, 0, 1, false, pace: 1);
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
                    sample = SampleAt(state, dev, project, next, absenceEnd);
                    sampledAt = next;
                    if (sample.Mode == SampleMode.Normal && sample.Wellbeing >= tunables.SetbackWellbeing)
                        run.Draft.LowWellbeingTime = TimeSpan.Zero;
                }

                t = end;
            }

            // A held sample is refreshed at the last grid instant, as short advances would have.
            var last = FloorGrid(to, interval);
            if (sample.Mode != SampleMode.Normal && last > from && last > sampledAt)
                sample = SampleAt(state, dev, project, last, absenceEnd);
            run.Draft.Sample = sample;
            return run.Finish();
        }

        // A preview day: no real time passes, so the ledger and engagement hold and the hours count as awake hours.
        public Step Preview(BabyState state, TimeSpan elapsed, TimeSpan cap, double recoveryBoost, double comfortFactor = 1) =>
            Preview(Of(state), elapsed, cap, recoveryBoost, comfortFactor);

        public Step Preview(Subject state, TimeSpan elapsed, TimeSpan cap, double recoveryBoost, double comfortFactor = 1)
        {
            var dev = state.Development ?? Seed(state);
            var run = new Run(this, state, dev);
            run.Check();
            var cond = CareConsequences.Readiness(state.Care);
            var mode = state.Care.IsAway ? SampleMode.Away : SampleMode.Normal;
            var start = state.Needs;
            var at = state.Checkpoint.UtcTicks;
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
                    var needs = BabyNeeds.Awake(start, TimeSpan.FromTicks(next), cap, rates, recoveryBoost, comfortFactor);
                    sample = SampleOf(mode, needs, state.Care, state.Bond, dev.Engagement, at);
                }

                hour = end;
            }

            run.Draft.Sample = sample;
            return run.Finish();
        }

        // Samples the state as it is now (after care, or at the end of a migrated advance).
        public TrackSample SampleNow(BabyNeeds.Values needs, CareRecord record, double bond,
            IReadOnlyList<EngagementEntry> engagement, long at) =>
            SampleOf(record.IsAwayAt(new DateTimeOffset(at, TimeSpan.Zero)) ? SampleMode.Away : SampleMode.Normal,
                needs, record, bond, engagement, at);

        // The setback rung, after an advance: only while the player is present (an advance within the presence gap,
        // at least the sick dwell into the visit), never while away, and never in Gentle care.
        public DevelopmentRecord Setback(DevelopmentRecord dev, BabyState state, CareRecord before, CareRecord after) =>
            Setback(dev, StageOf(state), before, after);

        // `stage` is the number of milestones reached after the advance.
        public DevelopmentRecord Setback(DevelopmentRecord dev, int stage, CareRecord before, CareRecord after)
        {
            if (dev.HasSetback || after.IsAway || after.AwaitingAnchor || before.AwaitingAnchor)
                return dev;
            var rules = care.RulesFor(after.Style);
            if (!rules.Setbacks)
                return dev;
            var newest = NewestOf(stage);
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
        // after it (its LastReason says whether a meal was on time); `needs` the values after it. `scale` multiplies the
        // action's engagement points (the care rhythm's bonus), and a fresh or familiar kind of play pays
        // `temperament` times more for an explorer or a cautious little one. `settleBond` takes the day's variety bonus
        // (0 when none) and returns Bond after the action, which the fresh sample uses.
        public CareOutcome AfterCare(BabyState before, CareAction action, Toy toy, Reaction reaction, CareRecord record,
            BabyNeeds.Values needs, double scale, CuriosityPhase curiosity, double temperament, Func<double, double> settleBond)
        {
            var refused = reaction == Reaction.PushesToyAway || reaction == Reaction.TooPoorly ||
                reaction == Reaction.MedicineNotNow;
            var kind = new CareKind(KindOf(action, toy, record.LastReason), CategoryOf(action), refused, action == CareAction.Feed,
                action == CareAction.Soothe || action == CareAction.ComfortNewSound);
            return AfterCare(Of(before), kind, record, needs, scale, curiosity, temperament, settleBond);
        }

        public CareOutcome AfterCare(Subject before, CareKind action, CareRecord record, BabyNeeds.Values needs, double scale,
            CuriosityPhase curiosity, double temperament, Func<double, double> settleBond)
        {
            var dev = before.Development ?? Seed(before);
            var draft = dev.ToBuilder();
            var at = record.Checkpoint.UtcTicks;
            var refused = action.Refused;
            var bored = false;
            var bonus = 0.0;
            var kind = action.Kind;
            if (kind.HasValue)
            {
                var wellbeing = Wellbeing(before.Needs);
                var repeats = DevelopmentRules.Repeats(dev.Engagement, kind.Value, at, tunables);
                var toyBit = ToyBit(kind.Value);
                var novel = toyBit != 0 && (dev.StageToys & toyBit) == 0;
                draft.StageToys |= toyBit;
                var log = DevelopmentRules.Prune(dev.Engagement, at, tunables);
                // Kept oldest first: an entry never predates one already kept.
                var stamp = log.Count > 0 ? Math.Max(at, log[log.Count - 1].At) : at;
                var leaning = (curiosity == CuriosityPhase.Explorer && repeats == 0) ||
                    (curiosity == CuriosityPhase.Cautious && repeats > 0) ? temperament : 1;
                var factor = DevelopmentRules.Factor(repeats, wellbeing, novel, tunables) * scale * leaning;
                log.Add(new EngagementEntry(kind.Value, stamp, Math.Min(10, factor)));
                draft.Engagement = DevelopmentRules.Prune(log, at, tunables);
                bored = IsPlay(kind.Value) && repeats >= tunables.BoredRepeats;
            }

            var category = action.Category;
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
                bonus = tunables.VarietyBond;
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

                if (action.Meal && record.LastReason == CareReason.MealOnTime)
                    draft.RecoveryMeals++;
                if (action.Comforting)
                    draft.RecoverySoothes++;
                // A routine with fewer meal windows (a cat eats twice) asks for all of them on time.
                var meals = Math.Min(tunables.RecoveryMeals, RoutineCalendar.MealWindows(record.Routine));
                if (draft.RecoveryMeals >= meals && draft.RecoverySoothes >= tunables.RecoverySoothes)
                {
                    draft.Shy = MotorSkill.None;
                    recovered = true;
                }
            }

            var bond = settleBond(bonus);
            draft.Sample = SampleNow(needs, record, bond, draft.Engagement, at);
            if (draft.Sample.Wellbeing >= tunables.SetbackWellbeing)
                draft.LowWellbeingTime = TimeSpan.Zero;
            return new CareOutcome(draft.Build(), bond, bored, recovered, kind);
        }

        public GrowthReport Report(BabyState state) => Report(Of(state));

        public GrowthReport Report(Subject state)
        {
            var dev = state.Development ?? Seed(state);
            var sample = dev.Sample;
            var physical = sample.Physical;
            var band = physical >= 0.75 ? GrowthBand.Thriving : physical >= 0.5 ? GrowthBand.Steady : GrowthBand.NeedsRegularMeals;
            var cond = CareConsequences.Readiness(state.Care);
            var words = WordsReport(state, dev, sample, cond);
            var run = new Run(this, state, dev);
            run.Check();
            var stage = run.Stage;
            if (stage >= Stages)
                return new GrowthReport(sample, band, MotorSkill.None, 0, TimeSpan.Zero, GrowthPace.OnTrack, false, dev.Shy,
                    dev.RecoveryMeals, dev.RecoverySoothes, words);

            // The current pace held until the skill unlocks, never past its guarantee.
            var readiness = run.Draft.Readiness;
            run.UnlockAge = null;
            run.Cell(guarantee[stage] - run.Age + 1, sample, cond, 0, stopAtUnlock: true);
            var unlockAge = run.UnlockAge ?? guarantee[stage];
            var ratio = (double)unlockAge / nominal[stage];
            var pace = ratio < 0.95 ? GrowthPace.Ahead : ratio <= 1.05 ? GrowthPace.OnTrack : GrowthPace.TakingTime;
            return new GrowthReport(sample, band, skills[stage], readiness,
                TimeSpan.FromTicks(Math.Max(0, unlockAge - state.Age)), pace, cond <= 0, dev.Shy,
                dev.RecoveryMeals, dev.RecoverySoothes, words);
        }

        // First words for the report: reached, or readiness and when they are likely at the current pace (null for a
        // species without them).
        private WordsReport WordsReport(Subject state, DevelopmentRecord dev, TrackSample sample, double cond)
        {
            if (!hasWords)
                return null;
            var run = new Run(this, state, dev);
            run.Check();
            if (run.Draft.FirstWords)
                return new WordsReport(true, 0, TimeSpan.Zero, GrowthPace.OnTrack);
            var readiness = run.Draft.Words;
            var unlockAge = run.ProjectWords(sample, cond, wordsGuarantee - run.WordsAge + 1) ?? wordsGuarantee;
            var ratio = (double)unlockAge / wordsNominal;
            var pace = ratio < 0.95 ? GrowthPace.Ahead : ratio <= 1.05 ? GrowthPace.OnTrack : GrowthPace.TakingTime;
            return new WordsReport(false, readiness, TimeSpan.FromTicks(Math.Max(0, unlockAge - state.Age)), pace);
        }

        // The age at which each skill unlocks from birth at a steady sample and condition, advanced in steps of `step`
        // (for checks and tuning: the same integration as real time).
        public TimeSpan[] Schedule(TrackSample sample, double cond, TimeSpan step)
        {
            var run = new Run(this, 0, 0, new DevelopmentRecord.Builder { Sample = sample });
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

        // The age at which First words arrive from birth at a steady sample and condition, in steps of `step`.
        public TimeSpan WordsSchedule(TrackSample sample, double cond, TimeSpan step)
        {
            if (!hasWords)
                throw new InvalidOperationException("This species has no First words.");
            var run = new Run(this, 0, 0, new DevelopmentRecord.Builder { Sample = sample });
            while (!run.Draft.FirstWords)
                run.Cell(step.Ticks, sample, cond, 0);
            return TimeSpan.FromTicks(run.WordsUnlockAge);
        }

        // The skill a setback makes shy: the newest one reached.
        public static MotorSkill Newest(BabyState state) =>
            state.CanWalk ? MotorSkill.Walking : state.CanCrawl ? MotorSkill.Crawling :
            state.CanSitAndReach ? MotorSkill.Sitting : MotorSkill.None;

        // The newest milestone among `stage` reached (None before the first).
        public MotorSkill NewestOf(int stage) => stage <= 0 ? MotorSkill.None : skills[Math.Min(stage, Stages) - 1];

        public static double Wellbeing(BabyNeeds.Values needs) =>
            DevelopmentRules.Wellbeing(needs.Satiety, needs.Comfort, needs.Energy, needs.Cleanliness);

        private TrackSample SampleAt(Subject state, DevelopmentRecord dev, Projection project, long at, long absenceEnd)
        {
            var mode = state.Care.IsAwayAt(new DateTimeOffset(at, TimeSpan.Zero)) ? SampleMode.Away
                : at >= absenceEnd ? SampleMode.Helper : SampleMode.Normal;
            var projected = project(at);
            return SampleOf(mode, projected.Needs, projected.Care, projected.Bond, dev.Engagement, at);
        }

        private TrackSample SampleOf(SampleMode mode, BabyNeeds.Values needs, CareRecord record, double bond,
            IReadOnlyList<EngagementEntry> engagement, long at) =>
            DevelopmentRules.Sample(mode, Wellbeing(needs), record.Meals.Credit(care.MealRing, care.LateCredit),
                bond / 100, needs.Health / 100, engagement, at, tunables);

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
            kind == EngagementKind.Blocks || kind == EngagementKind.ExploreSound || kind == EngagementKind.PlayWand;

        // A cat's wand is its first toy (the rattle's bit).
        private static int ToyBit(EngagementKind kind)
        {
            switch (kind)
            {
                case EngagementKind.PlayRattle:
                case EngagementKind.PlayWand: return 1;
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

        // The result of one development step. Milestones are numbered 0 to 2 (for the baby: sitting, crawling, walking).
        internal sealed class Step
        {
            private readonly bool[] can;
            private readonly bool[] unlocked;

            public DevelopmentRecord.Builder Draft { get; }
            public bool CanSitAndReach => can[0];
            public bool CanCrawl => can[1];
            public bool CanWalk => can[2];
            public bool UnlockedSitting => unlocked[0];
            public bool UnlockedCrawling => unlocked[1];
            public bool UnlockedWalking => unlocked[2];
            // The caller samples the advanced state at the end (a migrated advance).
            public bool ResampleAtEnd { get; set; }

            public bool AnyUnlocked => UnlockedSitting || UnlockedCrawling || UnlockedWalking || UnlockedWords;

            // Milestones reached after the step.
            public int Stage => can[2] ? 3 : can[1] ? 2 : can[0] ? 1 : 0;

            // The age (ticks) at which each stage unlocked during the step; meaningful where Unlocked* is true.
            public long[] UnlockAges { get; }

            public bool Unlocked(int stage) => unlocked[stage];

            // First words were reached during the step, at this age (ticks).
            public bool UnlockedWords { get; }
            public long WordsAge { get; }

            public Step(DevelopmentRecord.Builder draft, bool[] can, bool[] unlocked, long[] unlockAges,
                bool unlockedWords = false, long wordsAge = 0)
            {
                Draft = draft;
                UnlockedWords = unlockedWords;
                WordsAge = wordsAge;
                UnlockAges = (long[])unlockAges.Clone();
                this.can = (bool[])can.Clone();
                this.unlocked = (bool[])unlocked.Clone();
            }
        }

        internal readonly struct CareOutcome
        {
            public readonly DevelopmentRecord Record;
            public readonly double Bond;
            public readonly bool Bored;
            public readonly bool Recovered;
            // What the action counted as for engagement, if anything.
            public readonly EngagementKind? Kind;

            public CareOutcome(DevelopmentRecord record, double bond, bool bored, bool recovered, EngagementKind? kind)
            {
                Record = record;
                Bond = bond;
                Bored = bored;
                Recovered = recovered;
                Kind = kind;
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
            // First words' own clock (the same age, advanced before the motor stages), and when they were reached.
            public long WordsAge { get; private set; }
            public bool UnlockedWords { get; private set; }
            public long WordsUnlockAge { get; private set; }

            public Run(BabyGrowth growth, Subject state, DevelopmentRecord dev)
                : this(growth, state.Age, state.Stage, dev.ToBuilder())
            {
            }

            public Run(BabyGrowth growth, long age, int stage, DevelopmentRecord.Builder draft)
            {
                this.growth = growth;
                Draft = draft;
                Age = age;
                WordsAge = age;
                Stage = Math.Max(0, Math.Min(Stages, stage));
                for (var i = 0; i < Stage; i++)
                    can[i] = true;
                // A record from before First words is seeded from age once its stage has started.
                growth.SeedWords(draft, age);
            }

            // One stretch of `ticks` paced by `sample`; `low` ticks of it count towards low Wellbeing.
            public void Cell(long ticks, TrackSample sample, double cond, long low, bool stopAtUnlock = false)
            {
                var tunables = growth.tunables;
                switch (sample.Mode)
                {
                    case SampleMode.Away:
                        AdvanceWords(ticks, tunables.AwayQuality, sample.Wellbeing, 1, false);
                        Advance(ticks, tunables.AwayQuality, sample.Wellbeing, 1, false, stopAtUnlock);
                        break;
                    case SampleMode.Helper:
                        AdvanceWords(ticks, tunables.HelperQuality, sample.Wellbeing, cond, false);
                        Advance(ticks, tunables.HelperQuality, sample.Wellbeing, cond, false, stopAtUnlock);
                        break;
                    default:
                        AdvanceWords(ticks, sample.Learning, sample.Wellbeing, cond, true);
                        Advance(ticks, sample.Motor, sample.Wellbeing, cond, true, stopAtUnlock);
                        break;
                }

                if (low > 0)
                    Draft.LowWellbeingTime += TimeSpan.FromTicks(low);
            }

            // First words' readiness over `ticks` at the Learning quality, as Advance does for the motor stages: nothing
            // before the stage starts, x1.25 past the nominal age with Wellbeing 0.6 or more, reached at readiness 1
            // and always by the guarantee.
            public void AdvanceWords(long ticks, double quality, double wellbeing, double cond, bool catchUp,
                double? pace = null)
            {
                if (!growth.hasWords || ticks <= 0)
                    return;

                var tunables = growth.tunables;
                var boosted = catchUp && wellbeing >= tunables.CatchUpWellbeing;
                while (ticks > 0 && !Draft.FirstWords)
                {
                    if (CheckWords())
                        break;
                    if (WordsAge < growth.wordsStart)
                    {
                        var wait = Math.Min(ticks, growth.wordsStart - WordsAge);
                        WordsAge += wait;
                        ticks -= wait;
                        continue;
                    }

                    var segment = Math.Min(ticks, growth.wordsGuarantee - WordsAge);
                    if (boosted && WordsAge < growth.wordsNominal)
                        segment = Math.Min(segment, growth.wordsNominal - WordsAge);
                    var rate = Math.Max(0, cond) / (growth.wordsLength * (pace ?? DevelopmentRules.Pace(quality)));
                    if (boosted && WordsAge >= growth.wordsNominal)
                        rate *= tunables.CatchUp;

                    if (rate > 0)
                    {
                        var need = (1 - Draft.Words) / rate;
                        if (need <= segment)
                        {
                            // A thousandth of a tick absorbs rounding, as for the motor stages.
                            var step = Math.Min(segment, Math.Max(0, (long)Math.Ceiling(need - 0.001)));
                            WordsAge += step;
                            ticks -= step;
                            UnlockWords();
                            break;
                        }

                        Draft.Words = Math.Min(1, Draft.Words + rate * segment);
                    }

                    WordsAge += segment;
                    ticks -= segment;
                }

                if (ticks > 0)
                    WordsAge += ticks;
                CheckWords();
            }

            // The age First words would arrive at the current pace within `limit` ticks, or null.
            public long? ProjectWords(TrackSample sample, double cond, long limit)
            {
                var tunables = growth.tunables;
                switch (sample.Mode)
                {
                    case SampleMode.Away: AdvanceWords(limit, tunables.AwayQuality, sample.Wellbeing, 1, false); break;
                    case SampleMode.Helper: AdvanceWords(limit, tunables.HelperQuality, sample.Wellbeing, cond, false); break;
                    default: AdvanceWords(limit, sample.Learning, sample.Wellbeing, cond, true); break;
                }

                return UnlockedWords ? WordsUnlockAge : (long?)null;
            }

            // Marks the stage started once its age is reached, and reaches First words when they are due (readiness 1,
            // or the guarantee age). Returns whether they were reached now.
            private bool CheckWords()
            {
                if (!growth.hasWords || Draft.FirstWords || WordsAge < growth.wordsStart)
                    return false;
                Draft.LearningStarted = true;
                if (Draft.Words < 1 && WordsAge < growth.wordsGuarantee)
                    return false;
                UnlockWords();
                return true;
            }

            private void UnlockWords()
            {
                Draft.LearningStarted = true;
                Draft.FirstWords = true;
                Draft.Words = 0;
                UnlockedWords = true;
                WordsUnlockAge = WordsAge;
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

            // Unlocks every skill already due (and First words). Returns whether any motor skill unlocked.
            public bool Check()
            {
                CheckWords();
                var any = false;
                while (Stage < Stages && (Draft.Readiness >= 1 || Age >= growth.guarantee[Stage]))
                {
                    Unlock();
                    any = true;
                }

                return any;
            }

            public Step Finish() => new Step(Draft, can, unlocked, UnlockAges, UnlockedWords, WordsUnlockAge);

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
