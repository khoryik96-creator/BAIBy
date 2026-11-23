using System;
using System.Collections.Generic;
using System.Text;

namespace BAIby.Simulation
{
    // Saved as single letters; never change a letter.
    public enum CareMark { OnTime = 0, Late = 1, Missed = 2 }
    public enum MomentStatus { Pending = 0, OnTime = 1, Late = 2, Missed = 3, Excused = 4, Covered = 5 }

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

        // Mean credit of the newest entries: on time 1, late lateCredit, missed 0. An empty ring counts as 1.
        public double Credit(int window, double lateCredit)
        {
            var count = Math.Min(window, marks.Length);
            if (count <= 0)
                return 1;

            var total = 0.0;
            for (var i = marks.Length - count; i < marks.Length; i++)
                total += marks[i] == CareMark.OnTime ? 1 : marks[i] == CareMark.Late ? lateCredit : 0;

            return total / count;
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

    // The shadow care ledger. Immutable; CareRules returns a new record for every change.
    // All instants are UTC; offsets only place windows on the local clock.
    public sealed class CareRecord : IEquatable<CareRecord>
    {
        private const string StatusLetters = "POLMEC";
        private static readonly long LastCareDay = DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay;
        private static readonly TimeSpan MaxOffset = TimeSpan.FromHours(14);
        // A stored care day stays this close to its checkpoint's calendar day, whatever the offsets.
        private const long CareDayTolerance = 3;
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

        public MomentStatus StatusOf(CareMoment moment)
        {
            if (!Enum.IsDefined(typeof(CareMoment), moment))
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
            if (code == null || code.Length != RoutineCalendar.MomentCount)
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
        };

        public bool Equals(CareRecord other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

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
                ExcusedCount == other.ExcusedCount && CoveredCount == other.CoveredCount && LastReason == other.LastReason;
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
                if (Moments == null || Moments.Length != RoutineCalendar.MomentCount)
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
