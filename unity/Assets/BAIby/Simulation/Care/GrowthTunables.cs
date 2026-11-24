using System;

namespace BAIby.Simulation
{
    // Development tracks and readiness timing (docs/care-and-growth.md, Development tracks and Milestone timing).
    // Starting values for playtests; call Validate() after changing them. The readiness formula itself
    // (stage length x (1.30 - 0.50 Q)) is fixed in DevelopmentRules, so every milestone lands between 0.8x and 1.3x
    // of its nominal age at a steady quality, and the guarantee below always holds.
    public sealed class GrowthTunables
    {
        public static readonly GrowthTunables Default = new GrowthTunables();

        // Every milestone unlocks by this multiple of its nominal age, whatever happens.
        public double Guarantee { get; }
        // Past a milestone's nominal age with Wellbeing at CatchUpWellbeing or more, readiness grows this much faster.
        public double CatchUp { get; }
        public double CatchUpWellbeing { get; }
        // Track quality while Away mode holds the baby (0.6 is the nominal pace) and while a helper covers an
        // absence past the ceiling.
        public double AwayQuality { get; }
        public double HelperQuality { get; }
        // Qualities are sampled on this grid of instants (UTC), and after every care action.
        public TimeSpan SampleInterval { get; }
        // Engagement: points earned in this window, divided by EngagementTarget, capped at 1. One kind of action
        // supplies at most KindCap points to a track; repeats of a kind within RepeatWindow pay less.
        public TimeSpan EngagementWindow { get; }
        public TimeSpan RepeatWindow { get; }
        public double EngagementTarget { get; }
        public double KindCap { get; }
        // A toy's first use in a stage pays this much more.
        public double NoveltyBonus { get; }
        // This many repeats of a play action within RepeatWindow show a Bored reaction.
        public int BoredRepeats { get; }
        // This many kinds of care in one care day add VarietyBond to Bond (Connection), once a day.
        public int VarietyCategories { get; }
        public double VarietyBond { get; }
        // Setback, while the player is present: Wellbeing below SetbackWellbeing for SetbackLowTime of counted time,
        // the last SetbackMissedMeals meals missed and a meal still owed, or sick for SetbackSickTime without the
        // doctor. The newest skill goes shy and readiness drops by SetbackReadiness (never below 0, never re-locking).
        public double SetbackWellbeing { get; }
        public TimeSpan SetbackLowTime { get; }
        public int SetbackMissedMeals { get; }
        public TimeSpan SetbackSickTime { get; }
        public double SetbackReadiness { get; }
        // One good day ends it: this many on-time meals and comforting actions in one care day.
        public int RecoveryMeals { get; }
        public int RecoverySoothes { get; }

        public GrowthTunables(double guarantee = 1.4, double catchUp = 1.25, double catchUpWellbeing = 0.6,
            double awayQuality = 0.6, double helperQuality = 0.3, TimeSpan? sampleInterval = null,
            TimeSpan? engagementWindow = null, TimeSpan? repeatWindow = null, double engagementTarget = 10,
            double kindCap = 6, double noveltyBonus = 1.5, int boredRepeats = 3, int varietyCategories = 4,
            double varietyBond = 2, double setbackWellbeing = 0.3, TimeSpan? setbackLowTime = null,
            int setbackMissedMeals = 3, TimeSpan? setbackSickTime = null, double setbackReadiness = 0.15,
            int recoveryMeals = 3, int recoverySoothes = 2)
        {
            Guarantee = guarantee;
            CatchUp = catchUp;
            CatchUpWellbeing = catchUpWellbeing;
            AwayQuality = awayQuality;
            HelperQuality = helperQuality;
            SampleInterval = sampleInterval ?? TimeSpan.FromHours(1);
            EngagementWindow = engagementWindow ?? TimeSpan.FromHours(24);
            RepeatWindow = repeatWindow ?? TimeSpan.FromHours(6);
            EngagementTarget = engagementTarget;
            KindCap = kindCap;
            NoveltyBonus = noveltyBonus;
            BoredRepeats = boredRepeats;
            VarietyCategories = varietyCategories;
            VarietyBond = varietyBond;
            SetbackWellbeing = setbackWellbeing;
            SetbackLowTime = setbackLowTime ?? TimeSpan.FromDays(2);
            SetbackMissedMeals = setbackMissedMeals;
            SetbackSickTime = setbackSickTime ?? TimeSpan.FromHours(24);
            SetbackReadiness = setbackReadiness;
            RecoveryMeals = recoveryMeals;
            RecoverySoothes = recoverySoothes;
        }

        public void Validate()
        {
            // The slowest steady pace is 1.3x; the guarantee must come after it, so care always matters.
            if (double.IsNaN(Guarantee) || Guarantee < DevelopmentRules.SlowestPace || Guarantee > 3)
                throw new ArgumentOutOfRangeException(nameof(Guarantee), "The guarantee follows the slowest steady pace.");
            if (double.IsNaN(CatchUp) || CatchUp < 1 || CatchUp > 2)
                throw new ArgumentOutOfRangeException(nameof(CatchUp));
            ValidateFraction(CatchUpWellbeing, nameof(CatchUpWellbeing));
            ValidateFraction(AwayQuality, nameof(AwayQuality));
            ValidateFraction(HelperQuality, nameof(HelperQuality));
            if (SampleInterval < TimeSpan.FromMinutes(1) || SampleInterval > TimeSpan.FromHours(6) ||
                TimeSpan.TicksPerDay % SampleInterval.Ticks != 0)
                throw new ArgumentOutOfRangeException(nameof(SampleInterval), "Samples divide the day evenly.");
            if (EngagementWindow <= TimeSpan.Zero || RepeatWindow <= TimeSpan.Zero || RepeatWindow > EngagementWindow)
                throw new ArgumentOutOfRangeException(nameof(RepeatWindow), "Repeats count within the engagement window.");
            if (double.IsNaN(EngagementTarget) || EngagementTarget <= 0)
                throw new ArgumentOutOfRangeException(nameof(EngagementTarget));
            if (double.IsNaN(KindCap) || KindCap <= 0 || KindCap >= EngagementTarget)
                throw new ArgumentOutOfRangeException(nameof(KindCap), "One kind of action never fills engagement alone.");
            if (double.IsNaN(NoveltyBonus) || NoveltyBonus < 1 || NoveltyBonus > 3)
                throw new ArgumentOutOfRangeException(nameof(NoveltyBonus));
            if (BoredRepeats < 1 || VarietyCategories < 1 || RecoveryMeals < 1 || RecoverySoothes < 0)
                throw new ArgumentOutOfRangeException(nameof(BoredRepeats));
            if (double.IsNaN(VarietyBond) || VarietyBond < 0 || VarietyBond > 10)
                throw new ArgumentOutOfRangeException(nameof(VarietyBond));
            ValidateFraction(SetbackWellbeing, nameof(SetbackWellbeing));
            ValidateFraction(SetbackReadiness, nameof(SetbackReadiness));
            if (SetbackLowTime <= TimeSpan.Zero || SetbackSickTime <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(SetbackLowTime));
            if (SetbackMissedMeals < 1 || SetbackMissedMeals > CareRing.MaxLength)
                throw new ArgumentOutOfRangeException(nameof(SetbackMissedMeals));
        }

        private static void ValidateFraction(double value, string name)
        {
            if (double.IsNaN(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
