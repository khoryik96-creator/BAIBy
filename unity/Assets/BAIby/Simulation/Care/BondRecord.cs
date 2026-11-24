using System;

namespace BAIby.Simulation
{
    // Where a temperament axis stands right now. Derived, never saved; the copy is temporary and warm.
    public enum SecurityPhase { Steady, Clingy, Settled }
    public enum CuriosityPhase { Steady, Cautious, Explorer }

    // The relationship beside the Bond value itself (docs/care-and-growth.md, Bond and temperament, Return loops):
    // Bond's high-water mark and the day's caps, the two hidden temperament axes with the day's tallies that set
    // their targets, and the care rhythm with its lifetime records. Immutable; BondRules returns a new record for
    // every change.
    public sealed class BondRecord : IEquatable<BondRecord>
    {
        public const double AxisMin = -60;
        public const double AxisMax = 100;
        // Play-like engagement kinds a day can count towards Curiosity (bits of EngagementKind).
        public const int KindMask = (1 << 9) - 1;
        private static readonly long LastCareDay = DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay;

        // The highest Bond ever reached. Bond never falls below a share of it, and gains below it count double.
        public double HighWater { get; }
        // The care day whose caps are being counted, the raw gains and the losses counted on it, and whether a
        // helper covered it (a smaller loss cap).
        public long CapDay { get; }
        public double Gained { get; }
        public double Lost { get; }
        public bool HelperDay { get; }
        // Hidden axes from -60 to +100. Security follows answered fussing and missed meals, Curiosity the variety of
        // play. Each moves a fifth of the way to the day's target when a care day closes.
        public double Security { get; }
        public double Curiosity { get; }
        // The care day of the tallies below: meals given, together time, bedtime rest, fussing answered in time and
        // the kinds of play-like care (bits of EngagementKind).
        public long TallyDay { get; }
        public int Meals { get; }
        public bool Together { get; }
        public bool Rested { get; }
        public int Answered { get; }
        public int Kinds { get; }
        // Care days in a row (a missed day halves it; covered, away and excused days freeze it), the care day of the
        // last automatic grace day (0 for none), and the records kept forever.
        public int Rhythm { get; }
        public long GraceDay { get; }
        public int TotalCareDays { get; }
        public int LongestRhythm { get; }

        private BondRecord(Builder source)
        {
            HighWater = source.HighWater;
            CapDay = source.CapDay;
            Gained = source.Gained;
            Lost = source.Lost;
            HelperDay = source.HelperDay;
            Security = source.Security;
            Curiosity = source.Curiosity;
            TallyDay = source.TallyDay;
            Meals = source.Meals;
            Together = source.Together;
            Rested = source.Rested;
            Answered = source.Answered;
            Kinds = source.Kinds;
            Rhythm = source.Rhythm;
            GraceDay = source.GraceDay;
            TotalCareDays = source.TotalCareDays;
            LongestRhythm = source.LongestRhythm;
        }

        public Builder ToBuilder() => new Builder
        {
            HighWater = HighWater,
            CapDay = CapDay,
            Gained = Gained,
            Lost = Lost,
            HelperDay = HelperDay,
            Security = Security,
            Curiosity = Curiosity,
            TallyDay = TallyDay,
            Meals = Meals,
            Together = Together,
            Rested = Rested,
            Answered = Answered,
            Kinds = Kinds,
            Rhythm = Rhythm,
            GraceDay = GraceDay,
            TotalCareDays = TotalCareDays,
            LongestRhythm = LongestRhythm,
        };

        public bool Equals(BondRecord other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return HighWater.Equals(other.HighWater) && CapDay == other.CapDay && Gained.Equals(other.Gained) &&
                Lost.Equals(other.Lost) && HelperDay == other.HelperDay && Security.Equals(other.Security) &&
                Curiosity.Equals(other.Curiosity) && TallyDay == other.TallyDay && Meals == other.Meals &&
                Together == other.Together && Rested == other.Rested && Answered == other.Answered && Kinds == other.Kinds &&
                Rhythm == other.Rhythm && GraceDay == other.GraceDay && TotalCareDays == other.TotalCareDays &&
                LongestRhythm == other.LongestRhythm;
        }

        public override bool Equals(object obj) => Equals(obj as BondRecord);
        public override int GetHashCode() => HighWater.GetHashCode() ^ Security.GetHashCode() ^ Rhythm ^ TotalCareDays << 8;

        // A mutable draft used by BondRules and save codecs. Build() validates every field.
        public sealed class Builder
        {
            public double HighWater { get; set; }
            public long CapDay { get; set; }
            public double Gained { get; set; }
            public double Lost { get; set; }
            public bool HelperDay { get; set; }
            public double Security { get; set; }
            public double Curiosity { get; set; }
            public long TallyDay { get; set; }
            public int Meals { get; set; }
            public bool Together { get; set; }
            public bool Rested { get; set; }
            public int Answered { get; set; }
            public int Kinds { get; set; }
            public int Rhythm { get; set; }
            public long GraceDay { get; set; }
            public int TotalCareDays { get; set; }
            public int LongestRhythm { get; set; }

            public BondRecord Build()
            {
                if (double.IsNaN(HighWater) || HighWater < 0 || HighWater > 100)
                    throw new ArgumentOutOfRangeException(nameof(HighWater));
                if (CapDay < 0 || CapDay > LastCareDay || TallyDay < 0 || TallyDay > LastCareDay ||
                    GraceDay < 0 || GraceDay > LastCareDay)
                    throw new ArgumentOutOfRangeException(nameof(CapDay));
                if (double.IsNaN(Gained) || Gained < 0 || Gained > 1000 || double.IsNaN(Lost) || Lost < 0 || Lost > 1000)
                    throw new ArgumentOutOfRangeException(nameof(Gained));
                ValidateAxis(Security, nameof(Security));
                ValidateAxis(Curiosity, nameof(Curiosity));
                if (Meals < 0 || Answered < 0)
                    throw new ArgumentOutOfRangeException(nameof(Meals));
                if ((Kinds & ~KindMask) != 0)
                    throw new ArgumentOutOfRangeException(nameof(Kinds));
                if (Rhythm < 0 || TotalCareDays < 0 || LongestRhythm < Rhythm)
                    throw new ArgumentOutOfRangeException(nameof(Rhythm), "The longest rhythm is at least the current one.");

                return new BondRecord(this);
            }

            private static void ValidateAxis(double value, string name)
            {
                if (double.IsNaN(value) || value < AxisMin || value > AxisMax)
                    throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
