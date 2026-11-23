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

        public CareTunables(TimeSpan? dayStart = null, TimeSpan? firstDayGrace = null, TimeSpan? travelJump = null,
            TimeSpan? absenceCeiling = null, double fullMealThreshold = 90, int mealRing = 9, int togetherRing = 3,
            int bedtimeRing = 3, double mealWeight = 0.6, double togetherWeight = 0.25, double bedtimeWeight = 0.15,
            double lateCredit = 0.5)
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
}
