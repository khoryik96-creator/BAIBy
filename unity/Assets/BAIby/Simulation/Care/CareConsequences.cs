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

    // The reversible needs axis of the consequence ladder (docs/care-and-growth.md): Content, Fussy, Upset.
    //
    // - Time moves the companion at most one rung per advance, never while Away or before the ledger is
    //   anchored, and never during a preview day.
    // - Fussy: a need below the style's Fussy threshold, a missed meal or bedtime nobody has answered, or (in
    //   Standard) a meal past its due time. It calms by itself once every cause is gone.
    // - Upset, after at least UpsetDwell of fussing: a need below the Upset threshold, fussing unanswered for
    //   Escalation, or, while a missed meal is unanswered, too many recent misses or a low CareScore.
    // - Only hands-on care calms Upset: Fullness and Comfort back up, then a comforting action.
    // No randomness: the same inputs always give the same rung.
    public static class CareConsequences
    {
        private const int RecentMeals = 3;

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
                    var upset = UpsetCause(record, needs, rules, tunables);
                    if (upset != CareMoodCause.None && record.Checkpoint - record.MoodSince >= rules.UpsetDwell)
                        return Become(record, CareMood.Upset, upset);
                    return fussy == CareMoodCause.None ? Become(record, CareMood.Content, CareMoodCause.None) : record;
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
