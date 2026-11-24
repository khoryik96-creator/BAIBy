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
        // An advance over a gap longer than this is a return: it starts a new session and is not "present".
        public TimeSpan PresenceGap { get; }
        // Under the weather or sick, Energy cannot rise past this.
        public double UnwellEnergyCap { get; }
        // Recovering: Health recovers this many times faster for the first RecoveryWindow after the doctor's visit.
        public double RecoveryBoost { get; }
        // Recovering ends anyway this long after the visit once Health is at least RecoveredHealth.
        public TimeSpan RecoveryWindow { get; }
        public double RecoveredHealth { get; }
        // Recovering ends after this many medicine doses, each in a different meal window.
        public int RecoveryDoses { get; }
        // The doctor's visit restores this much Health at once.
        public double DoctorHealth { get; }
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
            double lateCredit = 0.5, double awayHoldLevel = 60, CareStyleRules standard = null, CareStyleRules gentle = null,
            TimeSpan? presenceGap = null, double unwellEnergyCap = 70, double recoveryBoost = 2, TimeSpan? recoveryWindow = null,
            double recoveredHealth = 60, int recoveryDoses = 2, double doctorHealth = 15)
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
            PresenceGap = presenceGap ?? TimeSpan.FromMinutes(30);
            UnwellEnergyCap = unwellEnergyCap;
            RecoveryBoost = recoveryBoost;
            RecoveryWindow = recoveryWindow ?? TimeSpan.FromHours(36);
            RecoveredHealth = recoveredHealth;
            RecoveryDoses = recoveryDoses;
            DoctorHealth = doctorHealth;
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
            if (PresenceGap <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(PresenceGap));
            if (double.IsNaN(UnwellEnergyCap) || UnwellEnergyCap <= 0 || UnwellEnergyCap > 100)
                throw new ArgumentOutOfRangeException(nameof(UnwellEnergyCap));
            if (double.IsNaN(RecoveryBoost) || RecoveryBoost < 1 || RecoveryBoost > 10)
                throw new ArgumentOutOfRangeException(nameof(RecoveryBoost), "Recovering never heals slower than usual.");
            if (RecoveryWindow <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(RecoveryWindow));
            if (double.IsNaN(RecoveredHealth) || RecoveredHealth < CareConsequences.HealthFloor || RecoveredHealth > 100)
                throw new ArgumentOutOfRangeException(nameof(RecoveredHealth));
            if (RecoveryDoses < 1 || RecoveryDoses > CareRecord.MaxDoses)
                throw new ArgumentOutOfRangeException(nameof(RecoveryDoses));
            if (double.IsNaN(DoctorHealth) || DoctorHealth < 0 || DoctorHealth > 100)
                throw new ArgumentOutOfRangeException(nameof(DoctorHealth));
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
            overfeedCleanliness: 5, messComfortCut: 0.03, unwellHealth: 40, unwellCareScore: 0.35, sickHealth: 20,
            sickUnwellTime: TimeSpan.FromHours(24), sickSessions: 3, sickDwell: TimeSpan.FromMinutes(30));

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
        // The health axis. Well becomes Under the weather when Health falls below UnwellHealth, or when the
        // CareScore is below UnwellCareScore after a missed meal.
        public double UnwellHealth { get; }
        public double UnwellCareScore { get; }
        // Under the weather becomes Sick, only while the player is present and has been for SickDwell, when Health
        // is below SickHealth, or after SickUnwellTime of counted time under the weather seen in SickSessions visits.
        public double SickHealth { get; }
        public TimeSpan SickUnwellTime { get; }
        public int SickSessions { get; }
        public TimeSpan SickDwell { get; }

        public CareStyleRules(double fussyNeed = 35, double upsetNeed = 15, TimeSpan? upsetDwell = null,
            TimeSpan? escalation = null, int missedMealsForUpset = 2, double upsetCareScore = 0.6,
            bool overdueMealIsFussy = true, double calmFullness = 50, double calmComfort = 40,
            double overfeedComfort = 10, double overfeedCleanliness = 15, double messComfortCut = 0.05,
            double unwellHealth = 50, double unwellCareScore = 0.45, double sickHealth = 30, TimeSpan? sickUnwellTime = null,
            int sickSessions = 2, TimeSpan? sickDwell = null)
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
            UnwellHealth = unwellHealth;
            UnwellCareScore = unwellCareScore;
            SickHealth = sickHealth;
            SickUnwellTime = sickUnwellTime ?? TimeSpan.FromHours(18);
            SickSessions = sickSessions;
            SickDwell = sickDwell ?? TimeSpan.FromMinutes(15);
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
            if (!IsNeed(UnwellHealth) || !IsNeed(SickHealth) || SickHealth >= UnwellHealth)
                throw new ArgumentOutOfRangeException(name, "Sick needs a lower Health threshold than Under the weather.");
            if (SickHealth <= CareConsequences.HealthFloor)
                throw new ArgumentOutOfRangeException(name, "Health never falls below the floor, so Sick must sit above it.");
            if (double.IsNaN(UnwellCareScore) || UnwellCareScore < 0 || UnwellCareScore > 1)
                throw new ArgumentOutOfRangeException(name);
            if (SickUnwellTime <= TimeSpan.Zero || SickSessions < 1 || SickDwell < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(name);
        }

        internal bool IsNoHarsherThan(CareStyleRules other) =>
            FussyNeed <= other.FussyNeed && UpsetNeed <= other.UpsetNeed && UpsetDwell >= other.UpsetDwell &&
            Escalation >= other.Escalation && MissedMealsForUpset >= other.MissedMealsForUpset &&
            UpsetCareScore <= other.UpsetCareScore && (!OverdueMealIsFussy || other.OverdueMealIsFussy) &&
            CalmFullness <= other.CalmFullness && CalmComfort <= other.CalmComfort &&
            OverfeedComfort <= other.OverfeedComfort && OverfeedCleanliness <= other.OverfeedCleanliness &&
            MessComfortCut <= other.MessComfortCut && UnwellHealth <= other.UnwellHealth &&
            UnwellCareScore <= other.UnwellCareScore && SickHealth <= other.SickHealth &&
            SickUnwellTime >= other.SickUnwellTime && SickSessions >= other.SickSessions && SickDwell >= other.SickDwell;

        private static bool IsNeed(double value) => !double.IsNaN(value) && value >= 0 && value <= 100;
    }
}
