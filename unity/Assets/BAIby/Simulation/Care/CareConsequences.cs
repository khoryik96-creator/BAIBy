using System;

namespace BAIby.Simulation
{
    // The needs a species maps into the needs axis: its lowest need, and the two that calm Upset.
    public readonly struct NeedSnapshot
    {
        public double Lowest { get; }
        public double Fullness { get; }
        public double Comfort { get; }

        public NeedSnapshot(double lowest, double fullness, double comfort)
        {
            Lowest = lowest;
            Fullness = fullness;
            Comfort = comfort;
        }
    }

    // Whether a recovering companion can take a medicine dose now. Not saved.
    public enum MedicineState { NotNeeded, Ready, WaitForMeal, GivenThisMeal }

    // The reversible needs axis of the consequence ladder (docs/care-and-growth.md): Content, Fussy, Upset.
    //
    // - Time moves the companion at most one rung per advance, never while Away or before the ledger is
    //   anchored, and never during a preview day.
    // - Fussy: a need below the style's Fussy threshold, a missed meal or bedtime nobody has answered, or (in
    //   Standard) a meal past its due time. It calms by itself once every cause is gone.
    // - Upset, after at least UpsetDwell of fussing: a need below the Upset threshold, fussing unanswered for
    //   Escalation, or, while a missed meal is unanswered, too many recent misses or a low CareScore.
    // - Only hands-on care calms Upset: Fullness and Comfort back up, then a comforting action.
    //
    // The health axis (Well, Under the weather, Sick, Recovering) is a second track; see SettleHealth.
    // No randomness: the same inputs always give the same rung.
    public static class CareConsequences
    {
        private const int RecentMeals = 3;
        // Health never falls below this, whatever happens (docs/care-and-growth.md, ladder rules).
        public const double HealthFloor = 10;

        // Moves the mood after time has passed. `record` is the ledger already advanced to the new instant.
        public static CareRecord Settle(CareRecord record, NeedSnapshot needs, CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));
            if (record.IsAway || record.AwaitingAnchor)
                return record;

            var rules = tunables.RulesFor(record.Style);
            var fussy = FussyCause(record, needs, rules);
            switch (record.Mood)
            {
                case CareMood.Content:
                    return fussy == CareMoodCause.None ? record : Become(record, CareMood.Fussy, fussy);
                case CareMood.Fussy:
                    // Whatever made the companion fussy may have gone while nobody was looking (energy comes
                    // back overnight). Calm first, so fussing that has already ended never escalates.
                    if (fussy == CareMoodCause.None)
                        return Become(record, CareMood.Content, CareMoodCause.None);
                    var upset = UpsetCause(record, needs, rules, tunables);
                    if (upset != CareMoodCause.None && record.Checkpoint - record.MoodSince >= rules.UpsetDwell)
                        return Become(record, CareMood.Upset, upset);
                    return record;
                default:
                    return record;
            }
        }

        // Calms the mood after hands-on care. `record` already holds the care's ledger changes.
        public static CareRecord AfterCare(CareRecord record, CareTags tags, NeedSnapshot needs, CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));

            var rules = tunables.RulesFor(record.Style);
            var fussy = FussyCause(record, needs, rules);
            switch (record.Mood)
            {
                case CareMood.Fussy:
                    return fussy == CareMoodCause.None ? Become(record, CareMood.Content, CareMoodCause.None) : record;
                case CareMood.Upset:
                    if ((tags & CareTags.Comfort) == 0 || needs.Fullness < rules.CalmFullness || needs.Comfort < rules.CalmComfort)
                        return record;
                    return fussy == CareMoodCause.None
                        ? Become(record, CareMood.Content, CareMoodCause.None)
                        : Become(record, CareMood.Fussy, fussy);
                default:
                    return record;
            }
        }

        // An upset companion pushes toys away: play, stories and blocks give nothing.
        public static bool RefusesPlay(CareRecord record) =>
            record != null && record.Mood == CareMood.Upset;

        // A sick companion stays in bed and accepts only food and comfort (and rest, changes and the doctor):
        // play, stories, blocks, baths and exploring are gently refused and give nothing.
        public static bool FeelsTooPoorly(CareRecord record) =>
            record != null && record.Condition == CareCondition.Sick;

        // What the companion's state does to development readiness (docs/care-and-growth.md, Milestone timing):
        // 1 when well, 0.75 under the weather, 0.5 upset or recovering, and 0 while sick before the doctor. The
        // lower of the mood's and the condition's factor applies. Slice 4's readiness timing multiplies by it.
        public static double Readiness(CareRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            double condition;
            switch (record.Condition)
            {
                case CareCondition.Sick: condition = 0; break;
                case CareCondition.Recovering: condition = 0.5; break;
                case CareCondition.UnderTheWeather: condition = 0.75; break;
                default: condition = 1; break;
            }

            return Math.Min(condition, record.Mood == CareMood.Upset ? 0.5 : 1);
        }

        // Moves the health axis after time has passed. `before` is the record at the start of the gap and `record`
        // the ledger already advanced (and its mood settled) to the new instant; `health` is Health there.
        //
        // - Time under the weather counts only up to the absence ceiling and outside Away mode, so it is the same in
        //   one advance or many. Each visit that starts while under the weather is one more session seen.
        // - Ways back come first: a good care day closed since the companion became unwell, with Health back at
        //   the Under the weather threshold, makes it well; Recovering ends once RecoveryWindow has passed with
        //   Health at RecoveredHealth. Sick waits for the doctor.
        // - Then at most one rung up, never while away or before the anchor: Well to Under the weather (an absence
        //   can do this), and Under the weather to Sick only while the player is present (an advance within the
        //   presence gap) and has been for SickDwell, so an absence alone never goes past Under the weather.
        public static CareRecord SettleHealth(CareRecord before, CareRecord record, double health, CareTunables tunables)
        {
            if (before == null)
                throw new ArgumentNullException(nameof(before));
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));
            if (before.AwaitingAnchor || record.AwaitingAnchor)
                return record;

            var started = CareRules.StartedSession(before, record);
            var draft = record.ToBuilder();
            if (record.Condition == CareCondition.UnderTheWeather)
            {
                draft.UnwellTime += CountedTime(before.Checkpoint.UtcTicks, record, tunables);
                if (started && !record.IsAway && draft.UnwellSessions < int.MaxValue)
                    draft.UnwellSessions++;
            }

            if (record.IsAway)
                return draft.Build();

            var rules = tunables.RulesFor(record.Style);
            var at = record.Checkpoint;
            switch (record.Condition)
            {
                case CareCondition.Well:
                    if (health < rules.UnwellHealth)
                        BecomeUnwell(draft, at, CareConditionCause.LowHealth);
                    else if (record.MissedSinceWell && CareRules.Score(record, tunables) < rules.UnwellCareScore)
                        BecomeUnwell(draft, at, CareConditionCause.LowCareScore);
                    break;
                case CareCondition.UnderTheWeather:
                    var unwellDay = RoutineCalendar.CareDayOf(record.ConditionSince.UtcTicks, record.Offset, tunables.DayStart);
                    if (record.GoodDay > 0 && record.GoodDay >= unwellDay && health >= rules.UnwellHealth)
                    {
                        BecomeWell(draft, at, CareConditionCause.GoodCareDay);
                        break;
                    }

                    var present = !started && at - record.SessionStart >= rules.SickDwell;
                    if (!present)
                        break;
                    if (health < rules.SickHealth)
                        BecomeSick(draft, at, CareConditionCause.LowHealth);
                    else if (draft.UnwellTime >= rules.SickUnwellTime && draft.UnwellSessions >= rules.SickSessions)
                        BecomeSick(draft, at, CareConditionCause.StayedUnwell);
                    break;
                case CareCondition.Recovering:
                    if (at - record.ConditionSince >= tunables.RecoveryWindow && health >= tunables.RecoveredHealth)
                        BecomeWell(draft, at, CareConditionCause.RecoveredWithTime);
                    break;
            }

            return draft.Build();
        }

        // The free doctor's visit, for a companion under the weather or sick. It counts as hands-on care (it ends
        // Away mode) and restores DoctorHealth at once. A sick companion, or one still below the Under the weather
        // threshold after the visit, starts Recovering with medicine to take; otherwise the doctor sends it home
        // well. Returns the record and Health after the visit; nothing changes for a companion that is not unwell.
        public static (CareRecord Record, double Health) VisitDoctor(CareRecord record, double health, NeedSnapshot needs,
            CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));
            if (!record.IsUnwell)
                return (record, health);

            var healed = Math.Min(100, health + tunables.DoctorHealth);
            var draft = CareRules.ApplyCare(record, CareTags.None, double.NaN, tunables).ToBuilder();
            if (record.Condition == CareCondition.Sick || healed < tunables.RulesFor(record.Style).UnwellHealth)
            {
                draft.Condition = CareCondition.Recovering;
                draft.ConditionCause = CareConditionCause.DoctorVisit;
                draft.ConditionSince = record.Checkpoint;
                draft.MedicineDoses = 0;
                draft.LastDoseWindow = 0;
            }
            else
            {
                BecomeWell(draft, record.Checkpoint, CareConditionCause.DoctorVisit);
            }

            draft.LastReason = CareReason.DoctorVisited;
            return (AfterCare(draft.Build(), CareTags.None, needs, tunables), healed);
        }

        // Medicine goes with meals: one dose counts per meal window, and only while recovering.
        public static MedicineState Medicine(CareRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (record.Condition != CareCondition.Recovering)
                return MedicineState.NotNeeded;

            var window = CareRules.MealWindowAt(record);
            if (window == null)
                return MedicineState.WaitForMeal;

            return CareRules.WindowKey(record.CareDay, window.Value) == record.LastDoseWindow
                ? MedicineState.GivenThisMeal
                : MedicineState.Ready;
        }

        // Counts a medicine dose; RecoveryDoses doses in different meal windows end Recovering. `record` already
        // holds the care's ledger changes. Does nothing unless Medicine(record) is Ready.
        public static CareRecord GiveMedicine(CareRecord record, CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));
            if (Medicine(record) != MedicineState.Ready)
                return record;

            var draft = record.ToBuilder();
            draft.MedicineDoses = Math.Min(CareRecord.MaxDoses, record.MedicineDoses + 1);
            draft.LastDoseWindow = CareRules.WindowKey(record.CareDay, CareRules.MealWindowAt(record).Value);
            draft.LastReason = CareReason.MedicineDose;
            if (draft.MedicineDoses >= tunables.RecoveryDoses)
                BecomeWell(draft, record.Checkpoint, CareConditionCause.MedicineCourse);
            return draft.Build();
        }

        // Time from `from` to the record's checkpoint that counts: up to the absence ceiling, outside Away mode.
        private static TimeSpan CountedTime(long from, CareRecord record, CareTunables tunables)
        {
            var to = Math.Min(record.Checkpoint.UtcTicks, CareRules.AbsenceEnd(record, tunables));
            if (to <= from)
                return TimeSpan.Zero;

            var away = Math.Max(0, Math.Min(to, record.AwayUntil.UtcTicks) - Math.Max(from, record.AwayFrom.UtcTicks));
            return TimeSpan.FromTicks(to - from - away);
        }

        private static void BecomeUnwell(CareRecord.Builder draft, DateTimeOffset at, CareConditionCause cause)
        {
            draft.Condition = CareCondition.UnderTheWeather;
            draft.ConditionCause = cause;
            draft.ConditionSince = at;
            draft.UnwellTime = TimeSpan.Zero;
            // The visit in which it happens is the first one to see it.
            draft.UnwellSessions = 1;
        }

        private static void BecomeSick(CareRecord.Builder draft, DateTimeOffset at, CareConditionCause cause)
        {
            draft.Condition = CareCondition.Sick;
            draft.ConditionCause = cause;
            draft.ConditionSince = at;
        }

        private static void BecomeWell(CareRecord.Builder draft, DateTimeOffset at, CareConditionCause cause)
        {
            draft.Condition = CareCondition.Well;
            draft.ConditionCause = cause;
            draft.ConditionSince = at;
            draft.UnwellTime = TimeSpan.Zero;
            draft.UnwellSessions = 0;
            draft.MissedSinceWell = false;
            draft.MedicineDoses = 0;
            draft.LastDoseWindow = 0;
        }

        public static CareMoodCause FussyCause(CareRecord record, NeedSnapshot needs, CareStyleRules rules)
        {
            if (needs.Lowest < rules.FussyNeed)
                return CareMoodCause.LowNeed;
            if (record.Owed != CareTags.None)
                return CareMoodCause.MissedWindow;
            if (rules.OverdueMealIsFussy && CareRules.HasOverdueMeal(record))
                return CareMoodCause.MealOverdue;

            return CareMoodCause.None;
        }

        private static CareMoodCause UpsetCause(CareRecord record, NeedSnapshot needs, CareStyleRules rules,
            CareTunables tunables)
        {
            if (needs.Lowest < rules.UpsetNeed)
                return CareMoodCause.LowNeed;
            if ((record.Owed & CareTags.Meal) != 0)
            {
                if (RecentMisses(record.Meals) >= rules.MissedMealsForUpset)
                    return CareMoodCause.MissedMeals;
                if (CareRules.Score(record, tunables) < rules.UpsetCareScore)
                    return CareMoodCause.LowCareScore;
            }

            if (record.Checkpoint - record.MoodSince >= rules.Escalation)
                return CareMoodCause.Unanswered;

            return CareMoodCause.None;
        }

        private static int RecentMisses(CareRing meals)
        {
            var misses = 0;
            for (var i = Math.Max(0, meals.Count - RecentMeals); i < meals.Count; i++)
            {
                if (meals[i] == CareMark.Missed)
                    misses++;
            }

            return misses;
        }

        private static CareRecord Become(CareRecord record, CareMood mood, CareMoodCause cause)
        {
            var draft = record.ToBuilder();
            draft.Mood = mood;
            draft.MoodCause = cause;
            draft.MoodSince = record.Checkpoint;
            return draft.Build();
        }
    }
}
