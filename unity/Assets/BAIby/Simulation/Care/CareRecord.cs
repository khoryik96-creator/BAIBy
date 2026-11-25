using System;
using System.Collections.Generic;
using System.Text;

namespace BAIby.Simulation
{
    // Saved as single letters; never change a letter.
    public enum CareMark { OnTime = 0, Late = 1, Missed = 2 }
    // Helped: a paid helper (the feeder) served during the window and nobody gave the meal by hand. Like Covered it stays
    // out of the rings and is not owed; unlike Covered, Bond, temperament and the care rhythm count it as missed, so a
    // helper never changes the attention axis.
    public enum MomentStatus { Pending = 0, OnTime = 1, Late = 2, Missed = 3, Excused = 4, Covered = 5, Helped = 6 }
    // The needs axis of the consequence ladder. Saved as numbers: append, never renumber.
    public enum CareMood { Content = 0, Fussy = 1, Upset = 2 }
    // Why the companion last moved on the needs axis. Saved as numbers: append, never renumber.
    public enum CareMoodCause
    {
        None = 0,
        LowNeed = 1,
        MealOverdue = 2,
        MissedWindow = 3,
        Unanswered = 4,
        MissedMeals = 5,
        LowCareScore = 6,
    }
    // The health axis of the consequence ladder (docs/care-and-growth.md). Saved as numbers: append, never renumber.
    public enum CareCondition { Well = 0, UnderTheWeather = 1, Sick = 2, Recovering = 3 }
    // Why the companion last moved on the health axis. Saved as numbers: append, never renumber.
    public enum CareConditionCause
    {
        None = 0,
        LowHealth = 1,
        LowCareScore = 2,
        StayedUnwell = 3,
        DoctorVisit = 4,
        GoodCareDay = 5,
        MedicineCourse = 6,
        RecoveredWithTime = 7,
    }
    // How strongly consequences bite. Saved as numbers: append, never renumber. Realistic is stored but
    // behaves as Standard until its own rules exist.
    public enum CareStyle { Standard = 0, Gentle = 1, Realistic = 2 }

    // The most recent counted entries of one kind, oldest first. Excused and Covered never enter a ring,
    // so an absence or a travel day cannot push old misses out.
    public sealed class CareRing : IEquatable<CareRing>
    {
        public const int MaxLength = 32;
        public static readonly CareRing Empty = new CareRing(new CareMark[0]);
        private const string Letters = "OLM";
        private readonly CareMark[] marks;

        private CareRing(CareMark[] marks)
        {
            this.marks = marks;
        }

        public int Count => marks.Length;
        public CareMark this[int index] => marks[index];

        public CareRing Push(CareMark mark, int capacity)
        {
            if (!Enum.IsDefined(typeof(CareMark), mark))
                throw new ArgumentOutOfRangeException(nameof(mark));
            if (capacity < 1 || capacity > MaxLength)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            var kept = Math.Min(marks.Length, capacity - 1);
            var next = new CareMark[kept + 1];
            Array.Copy(marks, marks.Length - kept, next, 0, kept);
            next[kept] = mark;
            return new CareRing(next);
        }

        // Mean credit over the newest `window` slots: on time 1, late lateCredit, missed 0. Slots not filled yet
        // count as on time, so an empty ring counts as 1 and a young ledger's first miss weighs no more than a
        // long-cared one's.
        public double Credit(int window, double lateCredit)
        {
            if (window <= 0)
                return 1;

            var count = Math.Min(window, marks.Length);
            var total = 0.0;
            for (var i = marks.Length - count; i < marks.Length; i++)
                total += marks[i] == CareMark.OnTime ? 1 : marks[i] == CareMark.Late ? lateCredit : 0;

            return (total + (window - count)) / window;
        }

        public string ToCode()
        {
            var code = new StringBuilder(marks.Length);
            foreach (var mark in marks)
                code.Append(Letters[(int)mark]);

            return code.ToString();
        }

        public static CareRing Parse(string code)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));
            if (code.Length > MaxLength)
                throw new FormatException("A care ring is too long.");
            if (code.Length == 0)
                return Empty;

            var marks = new CareMark[code.Length];
            for (var i = 0; i < code.Length; i++)
            {
                var index = Letters.IndexOf(code[i]);
                if (index < 0)
                    throw new FormatException($"Unknown care mark '{code[i]}'.");

                marks[i] = (CareMark)index;
            }

            return new CareRing(marks);
        }

        public bool Equals(CareRing other)
        {
            if (other == null || other.marks.Length != marks.Length)
                return false;

            for (var i = 0; i < marks.Length; i++)
            {
                if (marks[i] != other.marks[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as CareRing);
        public override int GetHashCode() => ToCode().GetHashCode();
        public override string ToString() => ToCode();
    }

    // The care ledger and the companion's consequence state. Immutable; CareRules and CareConsequences
    // return a new record for every change.
    // All instants are UTC; offsets only place windows on the local clock.
    public sealed class CareRecord : IEquatable<CareRecord>
    {
        private const string StatusLetters = "POLMECH";
        private static readonly long LastCareDay = DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay;
        private static readonly TimeSpan MaxOffset = TimeSpan.FromHours(14);
        // A stored care day stays this close to its checkpoint's calendar day, whatever the offsets.
        private const long CareDayTolerance = 3;
        public const int MaxMess = 3;
        // Medicine doses a record can hold; the tunables ask for fewer.
        public const int MaxDoses = 8;
        public static readonly TimeSpan MaxAway = TimeSpan.FromDays(14);
        private const CareTags OwedTags = CareTags.Meal | CareTags.Bedtime;
        private readonly MomentStatus[] moments;

        // The instant the ledger was last evaluated; equal to the owner's time checkpoint.
        public DateTimeOffset Checkpoint { get; }
        // The offset placing the current care day, and the latest observed offset, applied at the next care day.
        public TimeSpan Offset { get; }
        public TimeSpan NextOffset { get; }
        // Days since 0001-01-01 of the current care day. It never decreases.
        public long CareDay { get; }
        public RoutinePreset Preset { get; }
        public TimeSpan WakeTime { get; }
        // A routine change waits for the next care day.
        public RoutinePreset NextPreset { get; }
        public TimeSpan NextWakeTime { get; }
        public bool TogetherToday { get; }
        // A travel day: every unresolved moment of the current care day is excused.
        public bool DayExcused { get; }
        // Set after a migration: the next Advance excuses the whole gap and places the day on the local clock.
        public bool AwaitingAnchor { get; }
        // Windows that open before this instant are excused (first day, migration).
        public DateTimeOffset ExcusedUntil { get; }
        // The last hands-on care; absence is measured from here.
        public DateTimeOffset LastCareAt { get; }
        public CareRing Meals { get; }
        public CareRing TogetherDays { get; }
        public CareRing Bedtimes { get; }
        public long ExcusedCount { get; }
        public long CoveredCount { get; }
        public CareReason LastReason { get; }
        // Today's wash: one hygiene window a day. A missed wash adds one mess item.
        public MomentStatus Wash { get; }
        // Messy-room items, 0 to MaxMess. Hygiene care clears them.
        public int Mess { get; }
        public CareMood Mood { get; }
        // When the current mood began; escalation is measured from here.
        public DateTimeOffset MoodSince { get; }
        public CareMoodCause MoodCause { get; }
        // Routine cores (Meal, Bedtime) whose window was missed and that no care has answered yet.
        public CareTags Owed { get; }
        public CareStyle Style { get; }
        // The latest planned absence. Away while AwayFrom <= instant < AwayUntil; ending early sets AwayUntil.
        public DateTimeOffset AwayFrom { get; }
        public DateTimeOffset AwayUntil { get; }
        // The last time the companion was put to bed: OnTime, Late or Pending (never), and the care day whose
        // night it was. That night's sleep restores Energy to 100 (on time) or 85 (late); other nights to 65.
        public MomentStatus BedtimeRest { get; }
        public long BedtimeRestDay { get; }
        // The health axis: Well, Under the weather, Sick or Recovering, why it last moved, and since when.
        public CareCondition Condition { get; }
        public CareConditionCause ConditionCause { get; }
        public DateTimeOffset ConditionSince { get; }
        // Counted time under the weather in the current spell (not away, not past the absence ceiling), and the
        // sessions the player has seen it in. Both reset when the companion becomes unwell.
        public TimeSpan UnwellTime { get; }
        public int UnwellSessions { get; }
        // When the player's current visit began: an advance over a gap longer than the presence gap starts one.
        public DateTimeOffset SessionStart { get; }
        // A meal was missed since the companion was last well, so a low CareScore can make it unwell again.
        public bool MissedSinceWell { get; }
        // Medicine doses given while recovering, and the meal window of the last one (care day x moments +
        // moment, 0 for none), so two doses in one meal count once.
        public int MedicineDoses { get; }
        public long LastDoseWindow { get; }
        // The last care day that closed as a good care day (0 for none).
        public long GoodDay { get; }
        // Whose windows the ledger keeps (a baby's, or a species' routine). Never saved in the care element.
        public CareRoutine Routine { get; }
        // The household helpers' stock and counts for this companion. Never null; empty until a helper is used.
        public HelperRecord Helpers { get; }
        public bool IsAway => IsAwayAt(Checkpoint);
        public bool IsUnwell => Condition == CareCondition.UnderTheWeather || Condition == CareCondition.Sick;
        public bool IsMessy => Mess > 0;

        public bool IsAwayAt(DateTimeOffset instant) =>
            instant.UtcTicks >= AwayFrom.UtcTicks && instant.UtcTicks < AwayUntil.UtcTicks;

        public MomentStatus StatusOf(CareMoment moment)
        {
            if (!Enum.IsDefined(typeof(CareMoment), moment) || (int)moment >= moments.Length)
                throw new ArgumentOutOfRangeException(nameof(moment));

            return moments[(int)moment];
        }

        public string MomentCode
        {
            get
            {
                var code = new StringBuilder(moments.Length);
                foreach (var status in moments)
                    code.Append(StatusLetters[(int)status]);

                return code.ToString();
            }
        }

        public static MomentStatus[] ParseMoments(string code)
        {
            // Four letters, or five for a routine with a Dawn moment (a dog's); the record checks the routine's count.
            if (code == null || code.Length < RoutineCalendar.MomentCount || code.Length > RoutineCalendar.MaxMoments)
                throw new FormatException("Care moments need one letter per moment.");

            var statuses = new MomentStatus[code.Length];
            for (var i = 0; i < code.Length; i++)
            {
                var index = StatusLetters.IndexOf(code[i]);
                if (index < 0)
                    throw new FormatException($"Unknown moment status '{code[i]}'.");

                statuses[i] = (MomentStatus)index;
            }

            return statuses;
        }

        private CareRecord(Builder source)
        {
            Checkpoint = source.Checkpoint.ToUniversalTime();
            Offset = source.Offset;
            NextOffset = source.NextOffset;
            CareDay = source.CareDay;
            Preset = source.Preset;
            WakeTime = source.WakeTime;
            NextPreset = source.NextPreset;
            NextWakeTime = source.NextWakeTime;
            moments = (MomentStatus[])source.Moments.Clone();
            TogetherToday = source.TogetherToday;
            DayExcused = source.DayExcused;
            AwaitingAnchor = source.AwaitingAnchor;
            ExcusedUntil = source.ExcusedUntil.ToUniversalTime();
            LastCareAt = source.LastCareAt.ToUniversalTime();
            Meals = source.Meals;
            TogetherDays = source.TogetherDays;
            Bedtimes = source.Bedtimes;
            ExcusedCount = source.ExcusedCount;
            CoveredCount = source.CoveredCount;
            LastReason = source.LastReason;
            Wash = source.Wash;
            Mess = source.Mess;
            Mood = source.Mood;
            MoodSince = source.MoodSince.ToUniversalTime();
            MoodCause = source.MoodCause;
            Owed = source.Owed;
            Style = source.Style;
            AwayFrom = source.AwayFrom.ToUniversalTime();
            AwayUntil = source.AwayUntil.ToUniversalTime();
            BedtimeRest = source.BedtimeRest;
            BedtimeRestDay = source.BedtimeRestDay;
            Condition = source.Condition;
            ConditionCause = source.ConditionCause;
            ConditionSince = source.ConditionSince.ToUniversalTime();
            UnwellTime = source.UnwellTime;
            UnwellSessions = source.UnwellSessions;
            SessionStart = source.SessionStart.ToUniversalTime();
            MissedSinceWell = source.MissedSinceWell;
            MedicineDoses = source.MedicineDoses;
            LastDoseWindow = source.LastDoseWindow;
            GoodDay = source.GoodDay;
            Routine = source.Routine;
            Helpers = source.Helpers;
        }

        // The same record with the helpers' stock and counts replaced.
        public CareRecord WithHelpers(HelperRecord helpers)
        {
            if (helpers == null)
                throw new ArgumentNullException(nameof(helpers));
            if (helpers.Equals(Helpers))
                return this;

            var draft = ToBuilder();
            draft.Helpers = helpers;
            return draft.Build();
        }

        public Builder ToBuilder() => new Builder
        {
            Checkpoint = Checkpoint,
            Offset = Offset,
            NextOffset = NextOffset,
            CareDay = CareDay,
            Preset = Preset,
            WakeTime = WakeTime,
            NextPreset = NextPreset,
            NextWakeTime = NextWakeTime,
            Moments = (MomentStatus[])moments.Clone(),
            TogetherToday = TogetherToday,
            DayExcused = DayExcused,
            AwaitingAnchor = AwaitingAnchor,
            ExcusedUntil = ExcusedUntil,
            LastCareAt = LastCareAt,
            Meals = Meals,
            TogetherDays = TogetherDays,
            Bedtimes = Bedtimes,
            ExcusedCount = ExcusedCount,
            CoveredCount = CoveredCount,
            LastReason = LastReason,
            Wash = Wash,
            Mess = Mess,
            Mood = Mood,
            MoodSince = MoodSince,
            MoodCause = MoodCause,
            Owed = Owed,
            Style = Style,
            AwayFrom = AwayFrom,
            AwayUntil = AwayUntil,
            BedtimeRest = BedtimeRest,
            BedtimeRestDay = BedtimeRestDay,
            Condition = Condition,
            ConditionCause = ConditionCause,
            ConditionSince = ConditionSince,
            UnwellTime = UnwellTime,
            UnwellSessions = UnwellSessions,
            SessionStart = SessionStart,
            MissedSinceWell = MissedSinceWell,
            MedicineDoses = MedicineDoses,
            LastDoseWindow = LastDoseWindow,
            GoodDay = GoodDay,
            Routine = Routine,
            Helpers = Helpers,
        };

        public bool Equals(CareRecord other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            if (moments.Length != other.moments.Length)
                return false;
            for (var i = 0; i < moments.Length; i++)
            {
                if (moments[i] != other.moments[i])
                    return false;
            }

            return Checkpoint.UtcTicks == other.Checkpoint.UtcTicks && Offset == other.Offset &&
                NextOffset == other.NextOffset && CareDay == other.CareDay && Preset == other.Preset &&
                WakeTime == other.WakeTime && NextPreset == other.NextPreset && NextWakeTime == other.NextWakeTime &&
                TogetherToday == other.TogetherToday && DayExcused == other.DayExcused &&
                AwaitingAnchor == other.AwaitingAnchor && ExcusedUntil.UtcTicks == other.ExcusedUntil.UtcTicks &&
                LastCareAt.UtcTicks == other.LastCareAt.UtcTicks && Meals.Equals(other.Meals) &&
                TogetherDays.Equals(other.TogetherDays) && Bedtimes.Equals(other.Bedtimes) &&
                ExcusedCount == other.ExcusedCount && CoveredCount == other.CoveredCount && LastReason == other.LastReason &&
                Wash == other.Wash && Mess == other.Mess && Mood == other.Mood &&
                MoodSince.UtcTicks == other.MoodSince.UtcTicks && MoodCause == other.MoodCause && Owed == other.Owed &&
                Style == other.Style && AwayFrom.UtcTicks == other.AwayFrom.UtcTicks &&
                AwayUntil.UtcTicks == other.AwayUntil.UtcTicks && BedtimeRest == other.BedtimeRest &&
                BedtimeRestDay == other.BedtimeRestDay && Condition == other.Condition &&
                ConditionCause == other.ConditionCause && ConditionSince.UtcTicks == other.ConditionSince.UtcTicks &&
                UnwellTime == other.UnwellTime && UnwellSessions == other.UnwellSessions &&
                SessionStart.UtcTicks == other.SessionStart.UtcTicks && MissedSinceWell == other.MissedSinceWell &&
                MedicineDoses == other.MedicineDoses && LastDoseWindow == other.LastDoseWindow && GoodDay == other.GoodDay &&
                Routine == other.Routine && Helpers.Equals(other.Helpers);
        }

        public override bool Equals(object obj) => Equals(obj as CareRecord);

        public override int GetHashCode() =>
            Checkpoint.UtcTicks.GetHashCode() ^ CareDay.GetHashCode() ^ MomentCode.GetHashCode() ^ Meals.GetHashCode();

        // A mutable draft used by CareRules and save codecs. Build() validates every field.
        public sealed class Builder
        {
            public DateTimeOffset Checkpoint { get; set; }
            public TimeSpan Offset { get; set; }
            public TimeSpan NextOffset { get; set; }
            public long CareDay { get; set; }
            public RoutinePreset Preset { get; set; } = RoutinePreset.SchoolDay;
            public TimeSpan WakeTime { get; set; } = RoutineCalendar.DefaultWakeTime;
            public RoutinePreset NextPreset { get; set; } = RoutinePreset.SchoolDay;
            public TimeSpan NextWakeTime { get; set; } = RoutineCalendar.DefaultWakeTime;
            // One status per moment of the routine (CareRules sets five for a dog's).
            public MomentStatus[] Moments { get; set; } = new MomentStatus[RoutineCalendar.MomentCount];
            public bool TogetherToday { get; set; }
            public bool DayExcused { get; set; }
            public bool AwaitingAnchor { get; set; }
            public DateTimeOffset ExcusedUntil { get; set; }
            public DateTimeOffset LastCareAt { get; set; }
            public CareRing Meals { get; set; } = CareRing.Empty;
            public CareRing TogetherDays { get; set; } = CareRing.Empty;
            public CareRing Bedtimes { get; set; } = CareRing.Empty;
            public long ExcusedCount { get; set; }
            public long CoveredCount { get; set; }
            public CareReason LastReason { get; set; }
            public MomentStatus Wash { get; set; }
            public int Mess { get; set; }
            public CareMood Mood { get; set; }
            public DateTimeOffset MoodSince { get; set; }
            public CareMoodCause MoodCause { get; set; }
            public CareTags Owed { get; set; }
            public CareStyle Style { get; set; }
            public DateTimeOffset AwayFrom { get; set; }
            public DateTimeOffset AwayUntil { get; set; }
            public MomentStatus BedtimeRest { get; set; }
            public long BedtimeRestDay { get; set; }
            public CareCondition Condition { get; set; }
            public CareConditionCause ConditionCause { get; set; }
            public DateTimeOffset ConditionSince { get; set; }
            public TimeSpan UnwellTime { get; set; }
            public int UnwellSessions { get; set; }
            public DateTimeOffset SessionStart { get; set; }
            public bool MissedSinceWell { get; set; }
            public int MedicineDoses { get; set; }
            public long LastDoseWindow { get; set; }
            public long GoodDay { get; set; }
            public CareRoutine Routine { get; set; }
            public HelperRecord Helpers { get; set; } = HelperRecord.Empty;

            public CareRecord Build()
            {
                ValidateOffset(Offset, nameof(Offset));
                ValidateOffset(NextOffset, nameof(NextOffset));
                RoutineCalendar.ValidateRoutine(Preset, WakeTime);
                RoutineCalendar.ValidateRoutine(NextPreset, NextWakeTime);
                if (CareDay < 0 || CareDay > LastCareDay)
                    throw new ArgumentOutOfRangeException(nameof(CareDay));
                if (!AwaitingAnchor &&
                    Math.Abs(CareDay - RoutineCalendar.CareDayOf(Checkpoint.UtcTicks, Offset, TimeSpan.Zero)) > CareDayTolerance)
                    throw new ArgumentOutOfRangeException(nameof(CareDay), "The care day does not match the checkpoint.");
                if (!Enum.IsDefined(typeof(CareRoutine), Routine))
                    throw new ArgumentOutOfRangeException(nameof(Routine));
                if (Moments == null || Moments.Length != RoutineCalendar.MomentsOf(Routine))
                    throw new ArgumentException("Care moments need one status per moment.", nameof(Moments));
                foreach (var status in Moments)
                {
                    if (!Enum.IsDefined(typeof(MomentStatus), status))
                        throw new ArgumentOutOfRangeException(nameof(Moments));
                }

                if (LastCareAt > Checkpoint)
                    throw new ArgumentException("The last care cannot follow the checkpoint.", nameof(LastCareAt));
                if (Meals == null || TogetherDays == null || Bedtimes == null)
                    throw new ArgumentNullException(nameof(Meals), "Care rings cannot be null.");
                if (ExcusedCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(ExcusedCount));
                if (CoveredCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(CoveredCount));
                if (!Enum.IsDefined(typeof(CareReason), LastReason))
                    throw new ArgumentOutOfRangeException(nameof(LastReason));
                if (!Enum.IsDefined(typeof(MomentStatus), Wash) || Wash == MomentStatus.Helped)
                    throw new ArgumentOutOfRangeException(nameof(Wash));
                if (Mess < 0 || Mess > MaxMess)
                    throw new ArgumentOutOfRangeException(nameof(Mess));
                if (!Enum.IsDefined(typeof(CareMood), Mood))
                    throw new ArgumentOutOfRangeException(nameof(Mood));
                if (!Enum.IsDefined(typeof(CareMoodCause), MoodCause))
                    throw new ArgumentOutOfRangeException(nameof(MoodCause));
                if (MoodSince > Checkpoint)
                    throw new ArgumentException("A mood cannot begin after the checkpoint.", nameof(MoodSince));
                if ((Owed & ~OwedTags) != 0)
                    throw new ArgumentOutOfRangeException(nameof(Owed), "Only meals and bedtimes can be owed.");
                if (!Enum.IsDefined(typeof(CareStyle), Style))
                    throw new ArgumentOutOfRangeException(nameof(Style));
                if (AwayUntil < AwayFrom || AwayUntil - AwayFrom > MaxAway)
                    throw new ArgumentOutOfRangeException(nameof(AwayUntil), "Away mode lasts at most 14 days.");
                if (AwayFrom > Checkpoint)
                    throw new ArgumentException("Away mode cannot start after the checkpoint.", nameof(AwayFrom));
                if (BedtimeRest != MomentStatus.Pending && BedtimeRest != MomentStatus.OnTime && BedtimeRest != MomentStatus.Late)
                    throw new ArgumentOutOfRangeException(nameof(BedtimeRest));
                if (BedtimeRestDay < 0 || BedtimeRestDay > LastCareDay)
                    throw new ArgumentOutOfRangeException(nameof(BedtimeRestDay));
                if (!Enum.IsDefined(typeof(CareCondition), Condition))
                    throw new ArgumentOutOfRangeException(nameof(Condition));
                if (!Enum.IsDefined(typeof(CareConditionCause), ConditionCause))
                    throw new ArgumentOutOfRangeException(nameof(ConditionCause));
                if (ConditionSince > Checkpoint)
                    throw new ArgumentException("A condition cannot begin after the checkpoint.", nameof(ConditionSince));
                if (UnwellTime < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(UnwellTime));
                if (UnwellSessions < 0)
                    throw new ArgumentOutOfRangeException(nameof(UnwellSessions));
                if (SessionStart > Checkpoint)
                    throw new ArgumentException("A session cannot begin after the checkpoint.", nameof(SessionStart));
                if (MedicineDoses < 0 || MedicineDoses > MaxDoses)
                    throw new ArgumentOutOfRangeException(nameof(MedicineDoses));
                if (LastDoseWindow < 0 || LastDoseWindow / RoutineCalendar.MomentCount > LastCareDay)
                    throw new ArgumentOutOfRangeException(nameof(LastDoseWindow));
                if (GoodDay < 0 || GoodDay > LastCareDay)
                    throw new ArgumentOutOfRangeException(nameof(GoodDay));
                if (!Enum.IsDefined(typeof(CareRoutine), Routine))
                    throw new ArgumentOutOfRangeException(nameof(Routine));
                if (Helpers == null)
                    throw new ArgumentNullException(nameof(Helpers));
                if (Helpers.LastServe > Checkpoint.UtcTicks)
                    throw new ArgumentException("A helper cannot serve after the checkpoint.", nameof(Helpers));

                return new CareRecord(this);
            }

            private static void ValidateOffset(TimeSpan offset, string name)
            {
                if (offset < -MaxOffset || offset > MaxOffset || offset.Ticks % TimeSpan.TicksPerMinute != 0)
                    throw new ArgumentOutOfRangeException(name, "Offsets are whole minutes within 14 hours of UTC.");
            }
        }
    }
}
