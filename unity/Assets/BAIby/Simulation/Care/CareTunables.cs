using System;

namespace BAIby.Simulation
{
    // Starting values from docs/care-and-growth.md. Tune them through playtests, then call Validate().
    public sealed class CareTunables
    {
        public static readonly CareTunables Default = new CareTunables();

        // Care days start at this local time; earlier hours belong to the previous care day.
        public TimeSpan DayStart { get; }
        // Windows that open within this span after adoption are excused.
        public TimeSpan FirstDayGrace { get; }
        // An offset change larger than this is travel and excuses the care day.
        public TimeSpan TravelJump { get; }
        // Windows that close more than this long after the last hands-on care are Covered, not missed.
        public TimeSpan AbsenceCeiling { get; }
        // Feeding at or above this Fullness never counts as a meal.
        public double FullMealThreshold { get; }
        public int MealRing { get; }
        public int TogetherRing { get; }
        public int BedtimeRing { get; }
        public double MealWeight { get; }
        public double TogetherWeight { get; }
        public double BedtimeWeight { get; }
        public double LateCredit { get; }
        // Away mode holds needs at or above this level, or where they were if lower.
        public double AwayHoldLevel { get; }
        // The needs-axis thresholds of each care style. Realistic uses Standard until its own rules exist.
        public CareStyleRules Standard { get; }
        public CareStyleRules Gentle { get; }

        public CareStyleRules RulesFor(CareStyle style)
        {
            switch (style)
            {
                case CareStyle.Gentle: return Gentle;
                case CareStyle.Standard:
                case CareStyle.Realistic: return Standard;
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
        }

        public CareTunables(TimeSpan? dayStart = null, TimeSpan? firstDayGrace = null, TimeSpan? travelJump = null,
            TimeSpan? absenceCeiling = null, double fullMealThreshold = 90, int mealRing = 9, int togetherRing = 3,
            int bedtimeRing = 3, double mealWeight = 0.6, double togetherWeight = 0.25, double bedtimeWeight = 0.15,
            double lateCredit = 0.5, double awayHoldLevel = 60, CareStyleRules standard = null, CareStyleRules gentle = null)
        {
            DayStart = dayStart ?? TimeSpan.FromHours(4);
            FirstDayGrace = firstDayGrace ?? TimeSpan.FromHours(24);
            TravelJump = travelJump ?? TimeSpan.FromMinutes(60);
            AbsenceCeiling = absenceCeiling ?? TimeSpan.FromHours(36);
            FullMealThreshold = fullMealThreshold;
            MealRing = mealRing;
            TogetherRing = togetherRing;
            BedtimeRing = bedtimeRing;
            MealWeight = mealWeight;
            TogetherWeight = togetherWeight;
            BedtimeWeight = bedtimeWeight;
            LateCredit = lateCredit;
            AwayHoldLevel = awayHoldLevel;
            Standard = standard ?? CareStyleRules.StandardDefault;
            Gentle = gentle ?? CareStyleRules.GentleDefault;
        }

        public void Validate()
        {
            if (DayStart < TimeSpan.Zero || DayStart > RoutineCalendar.EarliestWakeTime)
                throw new ArgumentOutOfRangeException(nameof(DayStart), "Care days must start before the earliest wake time.");
            if (FirstDayGrace < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(FirstDayGrace));
            if (TravelJump < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(TravelJump));
            if (AbsenceCeiling <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(AbsenceCeiling));
            if (double.IsNaN(FullMealThreshold) || FullMealThreshold <= 0 || FullMealThreshold > 100)
                throw new ArgumentOutOfRangeException(nameof(FullMealThreshold));

            ValidateRing(MealRing, nameof(MealRing));
            ValidateRing(TogetherRing, nameof(TogetherRing));
            ValidateRing(BedtimeRing, nameof(BedtimeRing));
            ValidateFraction(MealWeight, nameof(MealWeight));
            ValidateFraction(TogetherWeight, nameof(TogetherWeight));
            ValidateFraction(BedtimeWeight, nameof(BedtimeWeight));
            ValidateFraction(LateCredit, nameof(LateCredit));
            if (Math.Abs(MealWeight + TogetherWeight + BedtimeWeight - 1) > 1e-9)
                throw new ArgumentException("CareScore weights must add up to 1.");
            if (double.IsNaN(AwayHoldLevel) || AwayHoldLevel < 0 || AwayHoldLevel > 100)
                throw new ArgumentOutOfRangeException(nameof(AwayHoldLevel));
            if (Standard == null || Gentle == null)
                throw new ArgumentNullException(nameof(Standard));
            Standard.Validate(nameof(Standard));
            Gentle.Validate(nameof(Gentle));
            if (!Gentle.IsNoHarsherThan(Standard))
                throw new ArgumentException("Gentle care must be at least as gentle as Standard on every rule.");

            // Every preset must place each window inside one care day at both wake-time limits.
            var dayEnd = DayStart + TimeSpan.FromDays(1);
            foreach (RoutinePreset preset in Enum.GetValues(typeof(RoutinePreset)))
            {
                foreach (var wake in new[] { RoutineCalendar.EarliestWakeTime, RoutineCalendar.LatestWakeTime })
                {
                    foreach (var window in RoutineCalendar.Windows(preset, wake))
                    {
                        if (window.Open < DayStart || window.LateUntil > dayEnd)
                            throw new ArgumentException($"The {preset} {window.Moment} window falls outside the care day.");
                    }
                }
            }
        }

        private static void ValidateRing(int length, string name)
        {
            if (length < 1 || length > CareRing.MaxLength)
                throw new ArgumentOutOfRangeException(name);
        }

        private static void ValidateFraction(double value, string name)
        {
            if (double.IsNaN(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    // Needs-axis thresholds for one care style (docs/care-and-growth.md, Consequence ladder).
    public sealed class CareStyleRules
    {
        public static readonly CareStyleRules StandardDefault = new CareStyleRules();
        public static readonly CareStyleRules GentleDefault = new CareStyleRules(fussyNeed: 25, upsetNeed: 8,
            upsetDwell: TimeSpan.FromMinutes(30), escalation: TimeSpan.FromHours(4), missedMealsForUpset: 3,
            upsetCareScore: 0.4, overdueMealIsFussy: false, calmFullness: 40, calmComfort: 30, overfeedComfort: 5,
            overfeedCleanliness: 5, messComfortCut: 0.03);

        // Content becomes Fussy when a need falls below this.
        public double FussyNeed { get; }
        // Fussy becomes Upset when a need falls below this.
        public double UpsetNeed { get; }
        // Fussy lasts at least this long before it can become Upset, so a present player can answer it.
        public TimeSpan UpsetDwell { get; }
        // Fussy left unanswered this long becomes Upset.
        public TimeSpan Escalation { get; }
        // With a missed meal unanswered, this many misses among the last three meals cause Upset.
        public int MissedMealsForUpset { get; }
        // With a missed meal unanswered, a CareScore below this causes Upset.
        public double UpsetCareScore { get; }
        // Whether a meal window past its due time makes the companion fussy.
        public bool OverdueMealIsFussy { get; }
        // Soothing calms Upset once Fullness and Comfort have reached these.
        public double CalmFullness { get; }
        public double CalmComfort { get; }
        // Feeding when already full: a mild Comfort and Cleanliness drop, never harm.
        public double OverfeedComfort { get; }
        public double OverfeedCleanliness { get; }
        // Each mess item cuts Comfort gains by this fraction.
        public double MessComfortCut { get; }

        public CareStyleRules(double fussyNeed = 35, double upsetNeed = 15, TimeSpan? upsetDwell = null,
            TimeSpan? escalation = null, int missedMealsForUpset = 2, double upsetCareScore = 0.6,
            bool overdueMealIsFussy = true, double calmFullness = 50, double calmComfort = 40,
            double overfeedComfort = 10, double overfeedCleanliness = 15, double messComfortCut = 0.05)
        {
            FussyNeed = fussyNeed;
            UpsetNeed = upsetNeed;
            UpsetDwell = upsetDwell ?? TimeSpan.FromMinutes(15);
            Escalation = escalation ?? TimeSpan.FromHours(2);
            MissedMealsForUpset = missedMealsForUpset;
            UpsetCareScore = upsetCareScore;
            OverdueMealIsFussy = overdueMealIsFussy;
            CalmFullness = calmFullness;
            CalmComfort = calmComfort;
            OverfeedComfort = overfeedComfort;
            OverfeedCleanliness = overfeedCleanliness;
            MessComfortCut = messComfortCut;
        }

        internal void Validate(string name)
        {
            if (!IsNeed(FussyNeed) || !IsNeed(UpsetNeed) || UpsetNeed >= FussyNeed)
                throw new ArgumentOutOfRangeException(name, "Upset needs a lower need threshold than Fussy.");
            if (UpsetDwell < TimeSpan.Zero || Escalation < UpsetDwell)
                throw new ArgumentOutOfRangeException(name, "Escalation cannot come before the upset dwell.");
            if (MissedMealsForUpset < 1 || MissedMealsForUpset > 3)
                throw new ArgumentOutOfRangeException(name, "Missed meals for Upset count among the last three.");
            if (double.IsNaN(UpsetCareScore) || UpsetCareScore < 0 || UpsetCareScore > 1)
                throw new ArgumentOutOfRangeException(name);
            if (!IsNeed(CalmFullness) || !IsNeed(CalmComfort) || !IsNeed(OverfeedComfort) || !IsNeed(OverfeedCleanliness))
                throw new ArgumentOutOfRangeException(name);
            if (double.IsNaN(MessComfortCut) || MessComfortCut < 0 || MessComfortCut * CareRecord.MaxMess >= 1)
                throw new ArgumentOutOfRangeException(name, "A messy room cuts comfort gains, never removes them.");
        }

        internal bool IsNoHarsherThan(CareStyleRules other) =>
            FussyNeed <= other.FussyNeed && UpsetNeed <= other.UpsetNeed && UpsetDwell >= other.UpsetDwell &&
            Escalation >= other.Escalation && MissedMealsForUpset >= other.MissedMealsForUpset &&
            UpsetCareScore <= other.UpsetCareScore && (!OverdueMealIsFussy || other.OverdueMealIsFussy) &&
            CalmFullness <= other.CalmFullness && CalmComfort <= other.CalmComfort &&
            OverfeedComfort <= other.OverfeedComfort && OverfeedCleanliness <= other.OverfeedCleanliness &&
            MessComfortCut <= other.MessComfortCut;

        private static bool IsNeed(double value) => !double.IsNaN(value) && value >= 0 && value <= 100;
    }
}
