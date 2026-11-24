using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // The motor skills that readiness unlocks, in order. Saved as numbers: append, never renumber.
    // A cat's milestones follow the baby's: pounce, climb, high jump. A hamster's: wheel, tunnel, hand-tame. A dog's
    // (P3): fetch, leash walk, agility.
    public enum MotorSkill
    {
        None = 0, Sitting = 1, Crawling = 2, Walking = 3, Pounce = 4, Climb = 5, HighJump = 6, Wheel = 7, Tunnel = 8, HandTame = 9,
        Fetch = 10, LeashWalk = 11, Agility = 12,
    }

    // The four development tracks (docs/care-and-growth.md). Saved as numbers: append, never renumber.
    public enum DevelopmentTrack { Motor = 0, Learning = 1, Social = 2, Physical = 3 }

    // What a care action counts as for engagement. Saved as numbers: append, never renumber.
    public enum EngagementKind
    {
        PlayRattle = 0,
        PlayPlush = 1,
        Story = 2,
        Blocks = 3,
        Soothe = 4,
        Meal = 5,
        Hygiene = 6,
        ComfortSound = 7,
        ExploreSound = 8,
        // A cat's wand play (Motor). Petting counts as Soothe and brushing as Hygiene.
        PlayWand = 9,
        // Talk time, the day's first talk once First words is reached (Learning and Social).
        Talk = 10,
        // A dog's game of fetch (P3): running after the ball (Motor) and learning its tricks (Learning). A walk counts as
        // play (PlayWand, Motor), as a hamster's wheel does.
        Fetch = 11,
    }

    // How a quality sample paces readiness: with its own qualities, held by Away mode at the nominal pace, or by a
    // helper past the absence ceiling. Saved as numbers: append, never renumber.
    public enum SampleMode { Normal = 0, Away = 1, Helper = 2 }

    // The kinds of care in one care day; four or more add a little Bond. Saved as flags: append, never renumber.
    [Flags]
    public enum CareCategories { None = 0, Meal = 1, Comfort = 2, Play = 4, Story = 8, Hygiene = 16, Rest = 32 }

    // One engagement entry: what was done, when (UTC), and the points multiplier it earned.
    public readonly struct EngagementEntry : IEquatable<EngagementEntry>
    {
        public EngagementKind Kind { get; }
        public long At { get; }
        public double Factor { get; }

        public EngagementEntry(EngagementKind kind, long at, double factor)
        {
            Kind = kind;
            At = at;
            Factor = factor;
        }

        public bool Equals(EngagementEntry other) => Kind == other.Kind && At == other.At && Factor.Equals(other.Factor);
        public override bool Equals(object obj) => obj is EngagementEntry other && Equals(other);
        public override int GetHashCode() => (int)Kind ^ At.GetHashCode() ^ Factor.GetHashCode();
    }

    // Wellbeing W and the four track qualities Q at one instant, each 0 to 1.
    public readonly struct TrackSample : IEquatable<TrackSample>
    {
        public SampleMode Mode { get; }
        public double Wellbeing { get; }
        public double Motor { get; }
        public double Learning { get; }
        public double Social { get; }
        public double Physical { get; }

        public TrackSample(SampleMode mode, double wellbeing, double motor, double learning, double social, double physical)
        {
            Mode = mode;
            Wellbeing = wellbeing;
            Motor = motor;
            Learning = learning;
            Social = social;
            Physical = physical;
        }

        public double Quality(DevelopmentTrack track)
        {
            switch (track)
            {
                case DevelopmentTrack.Motor: return Motor;
                case DevelopmentTrack.Learning: return Learning;
                case DevelopmentTrack.Social: return Social;
                case DevelopmentTrack.Physical: return Physical;
                default: throw new ArgumentOutOfRangeException(nameof(track));
            }
        }

        public TrackSample WithMode(SampleMode mode) => new TrackSample(mode, Wellbeing, Motor, Learning, Social, Physical);

        public bool Equals(TrackSample other) =>
            Mode == other.Mode && Wellbeing.Equals(other.Wellbeing) && Motor.Equals(other.Motor) &&
            Learning.Equals(other.Learning) && Social.Equals(other.Social) && Physical.Equals(other.Physical);

        public override bool Equals(object obj) => obj is TrackSample other && Equals(other);
        public override int GetHashCode() => Mode.GetHashCode() ^ Wellbeing.GetHashCode() ^ Motor.GetHashCode();
    }

    // Development state: readiness towards the next motor skill, the last quality sample, recent engagement, the day's
    // variety and the setback; for a baby, readiness towards First words (the Learning track's milestone) and Talk time.
    // Immutable; DevelopmentRules and BabyGrowth return a new record for every change.
    public sealed class DevelopmentRecord : IEquatable<DevelopmentRecord>
    {
        // Engagement entries a record keeps; older ones fall out of the engagement window anyway.
        public const int MaxEntries = 96;
        private static readonly long LastCareDay = DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay;
        private readonly EngagementEntry[] engagement;

        // Progress towards the next motor skill, 0 to 1 (0 once every skill is reached).
        public double Readiness { get; }
        // The latest quality sample; it paces readiness until the next sample instant.
        public TrackSample Sample { get; }
        // Counted time with Wellbeing below the setback threshold, since it was last at or above it.
        public TimeSpan LowWellbeingTime { get; }
        public IReadOnlyList<EngagementEntry> Engagement => engagement;
        // Toys (bit 0 rattle, 1 plush, 2 blocks) already used in the current motor stage.
        public int StageToys { get; }
        // The care day whose kinds of care are counted, those kinds, and whether its variety bonus was given.
        public long CategoryDay { get; }
        public CareCategories Categories { get; }
        public bool VarietyGiven { get; }
        // The skill that went shy in a setback (None when there is no setback) and when the latest setback began.
        public MotorSkill Shy { get; }
        public DateTimeOffset SetbackSince { get; }
        // The good day that ends a setback: its care day, on-time meals and comforting actions so far.
        public long RecoveryDay { get; }
        public int RecoveryMeals { get; }
        public int RecoverySoothes { get; }
        // The Learning track's First words (a baby only). Its stage starts at LearningStart days (BabyGrowth); until then
        // nothing is counted and the record saves as before. Words is readiness towards it (0 once reached).
        public bool LearningStarted { get; }
        public double Words { get; }
        public bool FirstWords { get; }
        // Talk time: talks so far (they vary the child's replies), and the care day whose first talk was counted.
        // TalkCounted is cleared by a Next day preview, as Bond's daily caps are.
        public int Talks { get; }
        public long TalkDay { get; }
        public bool TalkCounted { get; }

        public bool HasSetback => Shy != MotorSkill.None;

        private DevelopmentRecord(Builder source)
        {
            Readiness = source.Readiness;
            Sample = source.Sample;
            LowWellbeingTime = source.LowWellbeingTime;
            engagement = source.Engagement.ToArray();
            StageToys = source.StageToys;
            CategoryDay = source.CategoryDay;
            Categories = source.Categories;
            VarietyGiven = source.VarietyGiven;
            Shy = source.Shy;
            SetbackSince = source.SetbackSince.ToUniversalTime();
            RecoveryDay = source.RecoveryDay;
            RecoveryMeals = source.RecoveryMeals;
            RecoverySoothes = source.RecoverySoothes;
            LearningStarted = source.LearningStarted;
            Words = source.Words;
            FirstWords = source.FirstWords;
            Talks = source.Talks;
            TalkDay = source.TalkDay;
            TalkCounted = source.TalkCounted;
        }

        public Builder ToBuilder() => new Builder
        {
            Readiness = Readiness,
            Sample = Sample,
            LowWellbeingTime = LowWellbeingTime,
            Engagement = new List<EngagementEntry>(engagement),
            StageToys = StageToys,
            CategoryDay = CategoryDay,
            Categories = Categories,
            VarietyGiven = VarietyGiven,
            Shy = Shy,
            SetbackSince = SetbackSince,
            RecoveryDay = RecoveryDay,
            RecoveryMeals = RecoveryMeals,
            RecoverySoothes = RecoverySoothes,
            LearningStarted = LearningStarted,
            Words = Words,
            FirstWords = FirstWords,
            Talks = Talks,
            TalkDay = TalkDay,
            TalkCounted = TalkCounted,
        };

        public bool Equals(DevelopmentRecord other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (engagement.Length != other.engagement.Length)
                return false;
            for (var i = 0; i < engagement.Length; i++)
            {
                if (!engagement[i].Equals(other.engagement[i]))
                    return false;
            }

            return Readiness.Equals(other.Readiness) && Sample.Equals(other.Sample) &&
                LowWellbeingTime == other.LowWellbeingTime && StageToys == other.StageToys &&
                CategoryDay == other.CategoryDay && Categories == other.Categories && VarietyGiven == other.VarietyGiven &&
                Shy == other.Shy && SetbackSince.UtcTicks == other.SetbackSince.UtcTicks &&
                RecoveryDay == other.RecoveryDay && RecoveryMeals == other.RecoveryMeals &&
                RecoverySoothes == other.RecoverySoothes && LearningStarted == other.LearningStarted &&
                Words.Equals(other.Words) && FirstWords == other.FirstWords && Talks == other.Talks &&
                TalkDay == other.TalkDay && TalkCounted == other.TalkCounted;
        }

        public override bool Equals(object obj) => Equals(obj as DevelopmentRecord);
        public override int GetHashCode() => Readiness.GetHashCode() ^ Sample.GetHashCode() ^ engagement.Length;

        // A mutable draft used by the growth rules and save codecs. Build() validates every field.
        public sealed class Builder
        {
            public double Readiness { get; set; }
            public TrackSample Sample { get; set; }
            public TimeSpan LowWellbeingTime { get; set; }
            public List<EngagementEntry> Engagement { get; set; } = new List<EngagementEntry>();
            public int StageToys { get; set; }
            public long CategoryDay { get; set; }
            public CareCategories Categories { get; set; }
            public bool VarietyGiven { get; set; }
            public MotorSkill Shy { get; set; }
            public DateTimeOffset SetbackSince { get; set; }
            public long RecoveryDay { get; set; }
            public int RecoveryMeals { get; set; }
            public int RecoverySoothes { get; set; }
            public bool LearningStarted { get; set; }
            public double Words { get; set; }
            public bool FirstWords { get; set; }
            public int Talks { get; set; }
            public long TalkDay { get; set; }
            public bool TalkCounted { get; set; }

            public DevelopmentRecord Build()
            {
                ValidateFraction(Readiness, nameof(Readiness));
                if (!Enum.IsDefined(typeof(SampleMode), Sample.Mode))
                    throw new ArgumentOutOfRangeException(nameof(Sample));
                ValidateFraction(Sample.Wellbeing, nameof(Sample));
                ValidateFraction(Sample.Motor, nameof(Sample));
                ValidateFraction(Sample.Learning, nameof(Sample));
                ValidateFraction(Sample.Social, nameof(Sample));
                ValidateFraction(Sample.Physical, nameof(Sample));
                if (LowWellbeingTime < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(LowWellbeingTime));
                if (Engagement == null || Engagement.Count > MaxEntries)
                    throw new ArgumentOutOfRangeException(nameof(Engagement));
                for (var i = 0; i < Engagement.Count; i++)
                {
                    var entry = Engagement[i];
                    if (!Enum.IsDefined(typeof(EngagementKind), entry.Kind))
                        throw new ArgumentOutOfRangeException(nameof(Engagement), "Unknown engagement kind.");
                    if (entry.At < 0 || entry.At > DateTimeOffset.MaxValue.UtcTicks)
                        throw new ArgumentOutOfRangeException(nameof(Engagement));
                    if (double.IsNaN(entry.Factor) || entry.Factor < 0 || entry.Factor > 10)
                        throw new ArgumentOutOfRangeException(nameof(Engagement));
                    if (i > 0 && entry.At < Engagement[i - 1].At)
                        throw new ArgumentException("Engagement entries are kept oldest first.", nameof(Engagement));
                }

                if (StageToys < 0 || StageToys > 7)
                    throw new ArgumentOutOfRangeException(nameof(StageToys));
                if (CategoryDay < 0 || CategoryDay > LastCareDay || RecoveryDay < 0 || RecoveryDay > LastCareDay)
                    throw new ArgumentOutOfRangeException(nameof(CategoryDay));
                if (((int)Categories & ~63) != 0)
                    throw new ArgumentOutOfRangeException(nameof(Categories));
                if (!Enum.IsDefined(typeof(MotorSkill), Shy))
                    throw new ArgumentOutOfRangeException(nameof(Shy));
                if (RecoveryMeals < 0 || RecoverySoothes < 0)
                    throw new ArgumentOutOfRangeException(nameof(RecoveryMeals));
                ValidateFraction(Words, nameof(Words));
                if ((FirstWords || Words > 0) && !LearningStarted)
                    throw new ArgumentException("First words come after the Learning stage starts.", nameof(FirstWords));
                if (Talks < 0 || TalkDay < 0 || TalkDay > LastCareDay)
                    throw new ArgumentOutOfRangeException(nameof(Talks));
                if ((Talks > 0 || TalkCounted) && !FirstWords)
                    throw new ArgumentException("Talk time opens with First words.", nameof(Talks));

                return new DevelopmentRecord(this);
            }

            private static void ValidateFraction(double value, string name)
            {
                if (double.IsNaN(value) || value < 0 || value > 1)
                    throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
