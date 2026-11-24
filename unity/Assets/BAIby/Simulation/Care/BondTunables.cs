using System;

namespace BAIby.Simulation
{
    // Bond, temperament and the care rhythm (docs/care-and-growth.md, Bond and temperament, Return loops). Starting
    // values for playtests; call Validate() after changing them.
    public sealed class BondTunables
    {
        public static readonly BondTunables Default = new BondTunables();

        // Gains: an on-time meal, a soothe, a story, play, and answering fussing or upset within AnswerWindow of its
        // start. Blocks, the new sound and the day's variety keep their earlier gains.
        public double MealGain { get; }
        public double SootheGain { get; }
        public double StoryGain { get; }
        public double PlayGain { get; }
        public double AnswerGain { get; }
        public TimeSpan AnswerWindow { get; }
        // Losses: a missed meal; every FussyEvery of fussing and every UpsetEvery of upset (counted time only); each
        // day a helper covers.
        public double MissedMealLoss { get; }
        public double FussyLoss { get; }
        public TimeSpan FussyEvery { get; }
        public double UpsetLoss { get; }
        public TimeSpan UpsetEvery { get; }
        public double HelperLoss { get; }
        // Daily caps on raw gains and on losses (a smaller loss cap while a helper covers).
        public double DailyGain { get; }
        public double DailyLoss { get; }
        public double HelperDailyLoss { get; }
        // Bond never falls below this share of its high-water mark, and gains below the mark are multiplied.
        public double Floor { get; }
        public double BelowHighWater { get; }
        // Temperament: each closed care day moves an axis this share of the way to the day's target.
        public double Drift { get; }
        // Security's target: a base, more for each fussing answered in time (up to AnsweredCap), less for each missed
        // meal. A helper day takes HelperSecurity directly.
        public double SecurityBase { get; }
        public double SecurityPerAnswer { get; }
        public int AnsweredCap { get; }
        public double SecurityPerMiss { get; }
        public double HelperSecurity { get; }
        // Curiosity's target: a base plus a step for each different kind of play-like care in the day.
        public double CuriosityBase { get; }
        public double CuriosityPerKind { get; }
        // Phases: clingy at or below Clingy (Comfort falls ClingyComfort times faster), settled at or above Settled;
        // cautious or explorer the same way on Curiosity.
        public double Clingy { get; }
        public double Settled { get; }
        public double Cautious { get; }
        public double Explorer { get; }
        public double ClingyComfort { get; }
        // A slight engagement modifier: an explorer enjoys fresh kinds of play more, a cautious baby familiar ones.
        public double TemperamentEngagement { get; }
        // Care rhythm: one automatic grace day per GraceEvery care days; engagement x RhythmBonus at RhythmBonusDays
        // and x RhythmBonusHigh at RhythmBonusHighDays.
        public int GraceEvery { get; }
        public int RhythmBonusDays { get; }
        public double RhythmBonus { get; }
        public int RhythmBonusHighDays { get; }
        public double RhythmBonusHigh { get; }

        public BondTunables(double mealGain = 1, double sootheGain = 3, double storyGain = 2, double playGain = 1,
            double answerGain = 5, TimeSpan? answerWindow = null, double missedMealLoss = 2, double fussyLoss = 2,
            TimeSpan? fussyEvery = null, double upsetLoss = 1, TimeSpan? upsetEvery = null, double helperLoss = 2,
            double dailyGain = 12, double dailyLoss = 8, double helperDailyLoss = 4, double floor = 0.6,
            double belowHighWater = 2, double drift = 0.2, double securityBase = 45, double securityPerAnswer = 20,
            int answeredCap = 2, double securityPerMiss = 40, double helperSecurity = 1, double curiosityBase = -40,
            double curiosityPerKind = 30, double clingy = -25, double settled = 40, double cautious = -25,
            double explorer = 40, double clingyComfort = 1.25, double temperamentEngagement = 1.05, int graceEvery = 7,
            int rhythmBonusDays = 3, double rhythmBonus = 1.05, int rhythmBonusHighDays = 7, double rhythmBonusHigh = 1.10)
        {
            MealGain = mealGain;
            SootheGain = sootheGain;
            StoryGain = storyGain;
            PlayGain = playGain;
            AnswerGain = answerGain;
            AnswerWindow = answerWindow ?? TimeSpan.FromMinutes(30);
            MissedMealLoss = missedMealLoss;
            FussyLoss = fussyLoss;
            FussyEvery = fussyEvery ?? TimeSpan.FromHours(2);
            UpsetLoss = upsetLoss;
            UpsetEvery = upsetEvery ?? TimeSpan.FromHours(3);
            HelperLoss = helperLoss;
            DailyGain = dailyGain;
            DailyLoss = dailyLoss;
            HelperDailyLoss = helperDailyLoss;
            Floor = floor;
            BelowHighWater = belowHighWater;
            Drift = drift;
            SecurityBase = securityBase;
            SecurityPerAnswer = securityPerAnswer;
            AnsweredCap = answeredCap;
            SecurityPerMiss = securityPerMiss;
            HelperSecurity = helperSecurity;
            CuriosityBase = curiosityBase;
            CuriosityPerKind = curiosityPerKind;
            Clingy = clingy;
            Settled = settled;
            Cautious = cautious;
            Explorer = explorer;
            ClingyComfort = clingyComfort;
            TemperamentEngagement = temperamentEngagement;
            GraceEvery = graceEvery;
            RhythmBonusDays = rhythmBonusDays;
            RhythmBonus = rhythmBonus;
            RhythmBonusHighDays = rhythmBonusHighDays;
            RhythmBonusHigh = rhythmBonusHigh;
        }

        public void Validate()
        {
            foreach (var (value, name) in new[]
            {
                (MealGain, nameof(MealGain)), (SootheGain, nameof(SootheGain)), (StoryGain, nameof(StoryGain)),
                (PlayGain, nameof(PlayGain)), (AnswerGain, nameof(AnswerGain)), (MissedMealLoss, nameof(MissedMealLoss)),
                (FussyLoss, nameof(FussyLoss)), (UpsetLoss, nameof(UpsetLoss)), (HelperLoss, nameof(HelperLoss)),
                (SecurityPerAnswer, nameof(SecurityPerAnswer)), (SecurityPerMiss, nameof(SecurityPerMiss)),
                (HelperSecurity, nameof(HelperSecurity)), (CuriosityPerKind, nameof(CuriosityPerKind)),
            })
            {
                if (double.IsNaN(value) || value < 0 || value > 100)
                    throw new ArgumentOutOfRangeException(name);
            }

            if (AnswerWindow <= TimeSpan.Zero || FussyEvery < TimeSpan.FromMinutes(15) || UpsetEvery < TimeSpan.FromMinutes(15))
                throw new ArgumentOutOfRangeException(nameof(FussyEvery), "Losses come at most every 15 minutes.");
            if (double.IsNaN(DailyGain) || DailyGain <= 0 || DailyGain > 100)
                throw new ArgumentOutOfRangeException(nameof(DailyGain));
            if (double.IsNaN(DailyLoss) || DailyLoss <= 0 || DailyLoss > 100)
                throw new ArgumentOutOfRangeException(nameof(DailyLoss));
            if (double.IsNaN(HelperDailyLoss) || HelperDailyLoss < HelperLoss || HelperDailyLoss > DailyLoss)
                throw new ArgumentOutOfRangeException(nameof(HelperDailyLoss), "A helper day costs no more than any day.");
            if (double.IsNaN(Floor) || Floor < 0 || Floor > 1)
                throw new ArgumentOutOfRangeException(nameof(Floor));
            if (double.IsNaN(BelowHighWater) || BelowHighWater < 1 || BelowHighWater > 4)
                throw new ArgumentOutOfRangeException(nameof(BelowHighWater));
            if (double.IsNaN(Drift) || Drift <= 0 || Drift > 1)
                throw new ArgumentOutOfRangeException(nameof(Drift));
            if (AnsweredCap < 0 || GraceEvery < 1)
                throw new ArgumentOutOfRangeException(nameof(AnsweredCap));
            foreach (var (value, name) in new[]
            {
                (SecurityBase, nameof(SecurityBase)), (CuriosityBase, nameof(CuriosityBase)), (Clingy, nameof(Clingy)),
                (Settled, nameof(Settled)), (Cautious, nameof(Cautious)), (Explorer, nameof(Explorer)),
            })
            {
                if (double.IsNaN(value) || value < BondRecord.AxisMin || value > BondRecord.AxisMax)
                    throw new ArgumentOutOfRangeException(name);
            }

            if (Clingy >= Settled || Cautious >= Explorer)
                throw new ArgumentException("Each axis needs its low phase below its high phase.");
            if (double.IsNaN(ClingyComfort) || ClingyComfort < 1 || ClingyComfort > 2)
                throw new ArgumentOutOfRangeException(nameof(ClingyComfort));
            if (double.IsNaN(TemperamentEngagement) || TemperamentEngagement < 1 || TemperamentEngagement > 1.5)
                throw new ArgumentOutOfRangeException(nameof(TemperamentEngagement));
            if (RhythmBonusDays < 1 || RhythmBonusHighDays < RhythmBonusDays || double.IsNaN(RhythmBonus) ||
                RhythmBonus < 1 || double.IsNaN(RhythmBonusHigh) || RhythmBonusHigh < RhythmBonus || RhythmBonusHigh > 2)
                throw new ArgumentOutOfRangeException(nameof(RhythmBonus), "A longer rhythm never pays less.");
        }
    }
}
