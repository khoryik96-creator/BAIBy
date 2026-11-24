using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BAIby.Simulation.Pets
{
    // What one step did to a pet: the new state, whether it grew up, the milestones it reached (in order) and how it
    // answered the care, if any.
    public sealed class PetStep
    {
        private static readonly MotorSkill[] None = new MotorSkill[0];

        public PetState State { get; }
        public bool BecameGrown { get; }
        public IReadOnlyList<MotorSkill> Reached { get; }
        public PetCue Cue { get; }

        internal PetStep(PetState state, bool becameGrown, IReadOnlyList<MotorSkill> reached = null, PetCue cue = PetCue.Content)
        {
            State = state;
            BecameGrown = becameGrown;
            Reached = reached ?? None;
            Cue = cue;
        }
    }

    // The baby's care systems for a pet whose species has a care profile (PetCareProfile; the cat first). It is the
    // pet counterpart of BabySimulation's orchestration and reuses the same species-agnostic rules: CareRules for the
    // routine windows, mess and Away mode, CareConsequences for both axes of the ladder (the vet is the doctor's
    // visit), BabyNeeds for the needs over the local day and night (at the species' and breed's rates), BabyGrowth for
    // readiness, milestones and the setback, BondRules for Bond, Trust (temperament's Security), Curiosity and the care
    // rhythm, and JournalRules for the journal. No randomness; within a session one long advance equals many short ones.
    internal sealed class PetCare
    {
        private readonly PetCatalog catalog;
        private readonly CareTunables care;
        private readonly GrowthTunables growthTunables;
        private readonly BondTunables bond;
        private readonly TimeSpan maxNeedDecay;

        public PetCare(PetCatalog catalog, TimeSpan maxNeedDecay, CareTunables care, GrowthTunables growth, BondTunables bond)
        {
            this.catalog = catalog;
            this.maxNeedDecay = maxNeedDecay;
            this.care = care;
            growthTunables = growth;
            this.bond = bond;
        }

        public CareTunables CareTunables => care;
        public BondTunables BondTunables => bond;

        // A new pet's records: the ledger starts (its first day excused), Bond's record, the journal's arrival and a
        // first development sample.
        public PetState Adopt(PetState fresh)
        {
            var profile = Profile(fresh);
            var record = CareRules.Start(fresh.TimeCheckpoint, care, profile.Routine);
            var adopted = fresh.With(care: record, bonding: BondRules.Seed(fresh.Bond, record),
                journal: JournalRules.Start(fresh.TimeCheckpoint), milestones: 0);
            return adopted.With(development: Growth(adopted).Seed(Subject(adopted)));
        }

        // A pet saved before care profiles (or restored without its records) gets them on first use: a ledger whose first
        // advance excuses the whole gap, Bond's record with its high-water mark, and a journal with the arrival and the
        // milestones its age passed. Development is seeded by the first advance, at the nominal pace.
        public PetState Seeded(PetState state)
        {
            if (state.Care != null && state.Bonding != null && state.Journal != null)
                return state;

            var profile = Profile(state);
            var record = state.Care ?? CareRules.Unanchored(state.TimeCheckpoint, profile.Routine);
            return state.With(care: record, bonding: state.Bonding ?? BondRules.Seed(state.HighestBond, record),
                journal: state.Journal ?? JournalRules.Seed(state.TimeCheckpoint, state.Age, state.Milestones, profile.Skills,
                    profile.SkillAges, record));
        }

        public BondRecord Bonding(PetState state) => Seeded(state).Bonding;

        public JournalRecord Journal(PetState state) => Seeded(state).Journal;

        public PetStep Advance(PetState state, DateTimeOffset now)
        {
            var species = catalog.Get(state.Species);
            var growth = Growth(state);
            if (now <= state.TimeCheckpoint)
            {
                // No time passes: only what is already due (readiness 1 or the guarantee age, or the grown age).
                var due = state.Care != null && state.Development != null ? growth.Settle(Subject(state)) : null;
                var grownNow = !state.IsGrown && state.Age >= species.GrownAge;
                if ((due == null || !due.AnyUnlocked) && !grownNow)
                    return new PetStep(state, false);

                state = Seeded(state);
                due = due ?? growth.Settle(Subject(state));
                var settled = state.With(isGrown: state.IsGrown || grownNow, development: due.Draft.Build(), milestones: due.Stage);
                return new PetStep(settled.With(journal: Chronicle(state, settled, due, null, false)), grownNow,
                    Reached(state, due));
            }

            state = Seeded(state);
            var rates = Rates(state);
            var ledger = new List<LedgerEvent>();
            var advanced = CareRules.Advance(state.Care, now, care, ledger);
            var bonded = BondRules.Advance(state.Bond, state.Bonding, state.Care, ledger, now.UtcTicks, bond, care);
            var start = Values(state);
            var effects = BabyNeeds.EffectsFor(state.Care, bonded, care, bond);
            var needs = BabyNeeds.Real(start, state.Care, advanced, maxNeedDecay, care.AwayHoldLevel, care.DayStart, effects,
                rates);
            var from = state.Care;
            var step = growth.Real(Subject(state), now, at =>
            {
                if (at == now.UtcTicks)
                    return (advanced, needs, bonded.Bond);
                var ledgerAt = CareRules.Advance(from, new DateTimeOffset(at, TimeSpan.Zero).ToOffset(now.Offset), care);
                var bondAt = BondRules.Advance(state.Bond, state.Bonding, from, ledger, at, bond, care).Bond;
                return (ledgerAt, BabyNeeds.Real(start, from, ledgerAt, maxNeedDecay, care.AwayHoldLevel, care.DayStart,
                    effects, rates), bondAt);
            });

            // The first advance of a migrated ledger excuses the whole gap and moves nobody on the ladder.
            var record = advanced;
            if (!from.AwaitingAnchor)
            {
                record = CareConsequences.Settle(record, Snapshot(needs), care);
                record = CareConsequences.SettleHealth(from, record, needs.Health, care);
            }

            if (step.ResampleAtEnd)
                step.Draft.Sample = growth.SampleNow(needs, record, bonded.Bond, step.Draft.Engagement, now.UtcTicks);
            var development = growth.Setback(step.Draft.Build(), step.Stage, from, record);
            var elapsed = now - state.TimeCheckpoint;
            var age = elapsed >= PetState.MaxAge - state.Age ? PetState.MaxAge : state.Age + elapsed;
            var isGrown = state.IsGrown || age >= species.GrownAge;
            var after = state.With(timeCheckpoint: now, age: age, meters: Meters(state, needs), bond: bonded.Bond,
                highestBond: Math.Max(state.HighestBond, bonded.Bond), isGrown: isGrown, care: record,
                development: development, bonding: bonded.Record, milestones: step.Stage);
            return new PetStep(after.With(journal: Chronicle(state, after, step, bonded, true)), isGrown && !state.IsGrown,
                Reached(state, step));
        }

        // A preview day passes no real time, so no routine window passes: the ledger and the mood hold, the hours count
        // as awake hours, the care since the last day mark closes a day for the rhythm and temperament, and readiness
        // follows the needs as they fall.
        public PetStep Preview(PetState state, TimeSpan elapsed)
        {
            var species = catalog.Get(state.Species);
            state = Seeded(state);
            var growth = Growth(state);
            var bonded = BondRules.PreviewDay(state.Bond, state.Bonding, state.TimeCheckpoint.UtcTicks, bond);
            var comfort = BondRules.SecurityOf(bonded.Record, bond) == SecurityPhase.Clingy ? bond.ClingyComfort : 1;
            var effects = BabyNeeds.EffectsFor(state.Care, null, care, bond);
            var boost = state.TimeCheckpoint.UtcTicks < effects.BoostUntil ? effects.RecoveryBoost : 1;
            var counted = elapsed > PetState.MaxAge - state.Age ? PetState.MaxAge - state.Age : elapsed;
            var step = growth.Preview(Subject(state), counted, maxNeedDecay, boost, comfort);
            var needs = BabyNeeds.Awake(Values(state), counted, maxNeedDecay, Rates(state), boost, comfort);
            var age = state.Age + counted;
            var isGrown = state.IsGrown || age >= species.GrownAge;
            var after = state.With(age: age, meters: Meters(state, needs), isGrown: isGrown, development: step.Draft.Build(),
                bonding: bonded.Record, milestones: step.Stage);
            return new PetStep(after.With(journal: Chronicle(state, after, step, bonded, false)), isGrown && !state.IsGrown,
                Reached(state, step));
        }

        // One care verb, or a medicine dose when `verb` is null. Advance to the current instant first.
        public PetStep ApplyCare(PetState state, PetVerb? verb)
        {
            state = Seeded(state);
            var profile = Profile(state);
            VerbDefinition definition = null;
            CareTags tags;
            if (verb.HasValue)
            {
                definition = catalog.Get(state.Species).FindVerb(verb.Value);
                if (definition == null || !profile.Offers(verb.Value))
                    throw new InvalidOperationException($"A {state.Species} has no {verb} care.");
                tags = profile.TagsFor(verb.Value);
            }
            else
            {
                tags = profile.Profile.TagsFor("medicine");
            }

            var values = Values(state);
            var rules = care.RulesFor(state.Care.Style);
            // A scratched rug cuts every comfort gain a little.
            var comfortGain = 1 - rules.MessComfortCut * state.Care.Mess;
            // Under the weather or sick, Energy cannot rise past the unwell cap.
            var ceiling = state.Care.IsUnwell ? care.UnwellEnergyCap : 100;
            var medicine = CareConsequences.Medicine(state.Care);
            var liveliness = state.Liveliness;
            var boldness = state.Boldness;
            var gain = 0.0;
            var cue = PetCue.Content;
            var refused = false;
            if (!verb.HasValue)
            {
                // One dose per meal window, only while recovering; otherwise nothing happens.
                refused = medicine != MedicineState.Ready;
                cue = refused ? PetCue.MedicineNotNow : PetCue.MedicineTaken;
            }
            else if (CareConsequences.FeelsTooPoorly(state.Care) && (tags & (CareTags.Play | CareTags.Hygiene)) != 0)
            {
                // A sick pet stays curled up: only food, petting, naps and the vet.
                refused = true;
                cue = PetCue.TooPoorly;
            }
            else if (CareConsequences.RefusesPlay(state.Care) && (tags & CareTags.Play) != 0)
            {
                // A grumpy (upset) pet swats the toy away.
                refused = true;
                cue = PetCue.PushesToyAway;
            }
            else
            {
                foreach (var effect in definition.Effects)
                    values = Apply(values, effect, comfortGain, ceiling);
                if ((tags & CareTags.Meal) != 0 && IsFull(state))
                {
                    values.Comfort = Math.Max(0, values.Comfort - rules.OverfeedComfort);
                    values.Cleanliness = Math.Max(0, values.Cleanliness - rules.OverfeedCleanliness);
                    cue = PetCue.TooFull;
                }

                // Meals build Bond only when on time (below); other care builds its species' Bond.
                if ((tags & CareTags.Meal) == 0)
                    gain += definition.Bond;
                // Temperament drifts with care only during the Young stage.
                if (!state.IsGrown)
                {
                    liveliness = Personality(liveliness + definition.Lively);
                    boldness = Personality(boldness + definition.Bold);
                }
            }

            if (refused)
                tags = CareTags.None;

            // The ledger records the routine (and ends Away mode); then the right care calms the needs axis.
            var record = CareRules.ApplyCare(state.Care, tags, state.Meter(PetMeter.Fullness), care);
            record = CareConsequences.AfterCare(record, tags, Snapshot(values), care);
            if (!verb.HasValue && medicine == MedicineState.Ready)
            {
                record = CareConsequences.GiveMedicine(record, care);
                if (record.Condition == CareCondition.Well)
                    cue = PetCue.MedicineDone;
            }

            // Bond: an on-time meal, and fussing (restless) or upset (grumpy) answered within half an hour of it starting.
            var meal = (tags & CareTags.Meal) != 0;
            var onTimeMeal = meal && record.LastReason == CareReason.MealOnTime;
            if (onTimeMeal)
                gain += bond.MealGain;
            var answered = state.Care.Mood != CareMood.Content && record.Mood < state.Care.Mood &&
                state.Care.Checkpoint - state.Care.MoodSince <= bond.AnswerWindow;
            if (answered)
                gain += bond.AnswerGain;
            var careDay = record.CareDay;
            var bonded = BondRules.Gain(state.Bond, state.Bonding, careDay, gain, bond);

            var kind = verb.HasValue ? profile.KindOf(verb.Value, onTimeMeal) : null;
            var category = verb.HasValue ? profile.CategoryOf(verb.Value) : CareCategories.None;
            var developed = Growth(state).AfterCare(Subject(state),
                new BabyGrowth.CareKind(kind, category, refused, meal, (tags & CareTags.Comfort) != 0), record, values,
                BondRules.RhythmBonus(state.Bonding, bond), BondRules.CuriosityOf(state.Bonding, bond),
                bond.TemperamentEngagement, bonus =>
                {
                    if (bonus > 0)
                        bonded = BondRules.Gain(bonded.Bond, bonded.Record, careDay, bonus, bond);
                    return bonded.Bond;
                });

            var journal = state.Journal;
            if (developed.Recovered)
            {
                cue = PetCue.SkillBack;
                journal = JournalRules.SkillBack(journal, state.TimeCheckpoint, state.Age, state.ShySkill,
                    state.Development?.SetbackSince ?? state.TimeCheckpoint);
            }
            else if (developed.Bored)
            {
                cue = PetCue.Bored;
            }

            journal = JournalRules.AfterCondition(journal, state.Care.Condition, record.Condition, state.TimeCheckpoint, state.Age);
            var playKind = developed.Kind.HasValue && developed.Kind != EngagementKind.Meal && developed.Kind != EngagementKind.Hygiene
                ? developed.Kind : null;
            var bonding = BondRules.Tally(bonded.Record, careDay, meal && record.LastReason != CareReason.MealTooFull,
                (tags & CareTags.Together) != 0, (tags & CareTags.Bedtime) != 0, answered, playKind);
            var updated = state.With(meters: Meters(state, values), bond: bonded.Bond,
                highestBond: Math.Max(state.HighestBond, bonded.Bond), liveliness: liveliness, boldness: boldness, care: record,
                development: developed.Record, bonding: bonding, journal: journal);
            return new PetStep(updated, false, null, cue);
        }

        // The free vet's visit, for a pet under the weather or sick: Health +15 at once, then Recovering with medicine
        // to take, or home well when only a little under the weather. Advance to the current instant first.
        public PetStep VisitVet(PetState state)
        {
            state = Seeded(state);
            if (!state.Care.IsUnwell)
                return new PetStep(state, false, null, PetCue.VetAllClear);

            var visit = CareConsequences.VisitDoctor(state.Care, state.Health, Snapshot(Values(state)), care);
            var cue = visit.Record.Condition == CareCondition.Recovering ? PetCue.VetVisited : PetCue.VetAllClear;
            var journal = JournalRules.AfterCondition(state.Journal, state.Care.Condition, visit.Record.Condition,
                state.TimeCheckpoint, state.Age);
            var meters = Copy(state);
            meters[PetMeter.Health] = visit.Health;
            return new PetStep(state.With(meters: meters, care: visit.Record, journal: journal), false, null, cue);
        }

        // A journal photo of the moment: at most three a day. The stage shown is the newest milestone that shows.
        public (PetState State, bool Taken) TakePhoto(PetState state, PhotoMoment moment)
        {
            if (!Enum.IsDefined(typeof(PhotoMoment), moment))
                throw new ArgumentOutOfRangeException(nameof(moment));

            var seeded = Seeded(state);
            var profile = Profile(state);
            var shown = MotorSkill.None;
            foreach (var skill in profile.Skills)
            {
                if (state.Shows(skill))
                    shown = skill;
            }

            var photo = JournalRules.Photo(seeded.Journal, state.TimeCheckpoint, state.Age, moment, shown, BabyAppearance.Default);
            return ReferenceEquals(photo, seeded.Journal) ? (state, false) : (seeded.With(journal: photo), true);
        }

        public int PhotosLeft(PetState state) => JournalRules.PhotosLeft(Journal(state), state.Age);

        public GrowthReport Report(PetState state)
        {
            var seeded = Seeded(state);
            return Growth(seeded).Report(Subject(seeded));
        }

        public PetState StartAway(PetState state, TimeSpan duration)
        {
            state = Seeded(state);
            return state.With(care: CareRules.StartAway(state.Care, duration));
        }

        public PetState EndAway(PetState state)
        {
            state = Seeded(state);
            var record = CareRules.EndAway(state.Care);
            return ReferenceEquals(record, state.Care) ? state : state.With(care: record);
        }

        public PetState ChangeStyle(PetState state, CareStyle style)
        {
            state = Seeded(state);
            var record = CareRules.ChangeStyle(state.Care, style);
            return ReferenceEquals(record, state.Care) ? state : state.With(care: record);
        }

        public PetState ChangeRoutine(PetState state, RoutinePreset preset, TimeSpan wakeTime)
        {
            state = Seeded(state);
            return state.With(care: CareRules.ChangeRoutine(state.Care, preset, wakeTime));
        }

        // Whether feeding now would overfeed: a mild Comfort and Cleanliness drop, never harm.
        public bool IsFull(PetState state) => state.Meter(PetMeter.Fullness) >= care.FullMealThreshold;

        // Developer read-out of the ledger, growth and Bond, as for the baby.
        public string Describe(PetState state)
        {
            var seeded = Seeded(state);
            var report = Report(seeded);
            var sample = report.Sample;
            var bonding = seeded.Bonding;
            return CareDiagnostics.Summary(seeded.Care, care) + string.Format(CultureInfo.InvariantCulture,
                " | growth {0} {1:0.00} in {2:0.0}d | W {3:0.00} Q motor {4:0.00} social {5:0.00} physical {6:0.00}{7}{8}",
                report.Next, report.Readiness, report.ExpectedIn.TotalDays, sample.Wellbeing, sample.Motor, sample.Social,
                sample.Physical, sample.Mode == SampleMode.Normal ? "" : " " + sample.Mode,
                report.Shy == MotorSkill.None ? "" : " | shy " + report.Shy) + string.Format(CultureInfo.InvariantCulture,
                " | bond {0:0.0} high {1:0.0} today +{2:0}/-{3:0} | trust {4:0} curiosity {5:0} | rhythm {6} best {7} days {8}",
                seeded.Bond, bonding.HighWater, bonding.Gained, bonding.Lost, bonding.Security, bonding.Curiosity,
                bonding.Rhythm, bonding.LongestRhythm, bonding.TotalCareDays);
        }

        private PetCareProfile Profile(PetState state) =>
            PetCareProfile.For(state.Species) ?? throw new InvalidOperationException($"A {state.Species} has no care profile.");

        private BabyGrowth Growth(PetState state)
        {
            var profile = Profile(state);
            return new BabyGrowth(profile.SkillAges.ToArray(), profile.Skills.ToArray(), care, growthTunables, Rates(state));
        }

        // The pet's needs fall at its species' rates, adjusted by its breed's gentle tendencies while awake. Quiet hours
        // are the same for every breed, so no breed is harder to keep overnight either.
        private BabyNeeds.NeedRates Rates(PetState state)
        {
            var profile = Profile(state);
            var species = catalog.Get(state.Species);
            var breed = catalog.Get(state.Breed);
            double Awake(PetMeter meter)
            {
                foreach (var definition in species.Meters)
                {
                    if (definition.Meter == meter)
                        return definition.DecayPerHour * breed.DecayFactor(meter);
                }

                throw new InvalidOperationException($"A {state.Species} needs a {meter} meter for its care profile.");
            }

            return new BabyNeeds.NeedRates(Awake(PetMeter.Fullness), Awake(PetMeter.Comfort), Awake(PetMeter.Energy),
                Awake(PetMeter.Cleanliness), profile.NightFullness, profile.NightComfort, profile.NightEnergy,
                profile.NightCleanliness);
        }

        private static BabyGrowth.Subject Subject(PetState state) =>
            new BabyGrowth.Subject(state.Age.Ticks, state.Milestones, state.Development, state.Care, Values(state), state.Bond,
                state.TimeCheckpoint);

        private static BabyNeeds.Values Values(PetState state) => new BabyNeeds.Values
        {
            Satiety = state.Meter(PetMeter.Fullness),
            Comfort = state.Meter(PetMeter.Comfort),
            Energy = state.Meter(PetMeter.Energy),
            Cleanliness = state.Meter(PetMeter.Cleanliness),
            Health = state.Health,
        };

        private static Dictionary<PetMeter, double> Copy(PetState state)
        {
            var meters = new Dictionary<PetMeter, double>();
            foreach (var pair in state.Meters)
                meters[pair.Key] = pair.Value;
            return meters;
        }

        private static Dictionary<PetMeter, double> Meters(PetState state, BabyNeeds.Values values)
        {
            var meters = Copy(state);
            meters[PetMeter.Fullness] = values.Satiety;
            meters[PetMeter.Comfort] = values.Comfort;
            meters[PetMeter.Energy] = values.Energy;
            meters[PetMeter.Cleanliness] = values.Cleanliness;
            meters[PetMeter.Health] = values.Health;
            return meters;
        }

        // One verb effect on the needs: comfort gains are cut by mess, and Energy gains stop at the unwell cap (higher
        // Energy is kept).
        private static BabyNeeds.Values Apply(BabyNeeds.Values values, MeterEffect effect, double comfortGain, double ceiling)
        {
            var delta = effect.Delta;
            switch (effect.Meter)
            {
                case PetMeter.Fullness:
                    values.Satiety = Clamp(values.Satiety + delta);
                    break;
                case PetMeter.Comfort:
                    values.Comfort = Clamp(values.Comfort + (delta > 0 ? delta * comfortGain : delta));
                    break;
                case PetMeter.Energy:
                    values.Energy = delta > 0
                        ? Math.Max(values.Energy, Math.Min(ceiling, values.Energy + delta))
                        : Clamp(values.Energy + delta);
                    break;
                case PetMeter.Cleanliness:
                    values.Cleanliness = Clamp(values.Cleanliness + delta);
                    break;
            }

            return values;
        }

        private JournalRecord Chronicle(PetState before, PetState after, BabyGrowth.Step step, BondStep bonded, bool real)
        {
            var profile = Profile(before);
            var milestones = new List<(MotorSkill, long)>();
            for (var stage = 0; stage < profile.Skills.Count; stage++)
            {
                if (step.Unlocked(stage))
                    milestones.Add((profile.Skills[stage], step.UnlockAges[stage]));
            }

            return JournalRules.Chronicle(before.Journal, before.TimeCheckpoint, before.Age, before.Care.Condition,
                after.TimeCheckpoint, after.Age, after.Care.Condition, milestones, bonded, real, before.Bonding.TotalCareDays);
        }

        private static IReadOnlyList<MotorSkill> Reached(PetState state, BabyGrowth.Step step)
        {
            var profile = PetCareProfile.For(state.Species);
            var reached = new List<MotorSkill>();
            for (var stage = 0; stage < profile.Skills.Count; stage++)
            {
                if (step.Unlocked(stage))
                    reached.Add(profile.Skills[stage]);
            }

            return reached;
        }

        private static NeedSnapshot Snapshot(BabyNeeds.Values needs) =>
            new NeedSnapshot(needs.Lowest, needs.Satiety, needs.Comfort);

        private static double Clamp(double value) => Math.Max(0, Math.Min(100, value));

        private static double Personality(double value) =>
            Math.Max(-PetState.PersonalityLimit, Math.Min(PetState.PersonalityLimit, value));
    }
}
