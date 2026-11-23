using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // Pure routine rules. They evaluate windows and keep the ledger; they never touch needs or growth.
    //
    // Time rules: elapsed time uses the UTC instant and the offset only places windows. An offset change
    // applies from the next care day. A change over TravelJump excuses only the care day it lands on; the
    // days it spans count like any absence. The care day never decreases, a backwards clock changes nothing,
    // and a forward jump is absence.
    public static class CareRules
    {
        private static readonly long MaxTicks = DateTimeOffset.MaxValue.UtcTicks;

        public static CareRecord Start(DateTimeOffset now, CareTunables tunables)
        {
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));

            var instant = now.UtcTicks;
            var draft = new CareRecord.Builder
            {
                Checkpoint = now,
                Offset = now.Offset,
                NextOffset = now.Offset,
                CareDay = RoutineCalendar.CareDayOf(instant, now.Offset, tunables.DayStart),
                ExcusedUntil = AsInstant(Add(instant, tunables.FirstDayGrace.Ticks)),
                LastCareAt = now,
                LastReason = CareReason.Started,
            };
            CloseWindows(draft, instant, false, tunables, new GapSummary());
            return draft.Build();
        }

        // A ledger for a baby saved before care existed. Its first Advance excuses the whole gap.
        public static CareRecord Unanchored(DateTimeOffset checkpoint) => new CareRecord.Builder
        {
            Checkpoint = checkpoint,
            ExcusedUntil = checkpoint,
            LastCareAt = checkpoint,
            AwaitingAnchor = true,
            LastReason = CareReason.AwaitingAnchor,
        }.Build();

        public static CareRecord Advance(CareRecord record, DateTimeOffset now, CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));

            var instant = now.UtcTicks;
            var checkpoint = record.Checkpoint.UtcTicks;
            if (instant <= checkpoint)
                return record;

            var draft = record.ToBuilder();
            if (draft.AwaitingAnchor)
                return Anchor(draft, now, tunables);

            var gap = new GapSummary();
            var travel = false;
            if (now.Offset != draft.NextOffset)
            {
                draft.NextOffset = now.Offset;
                travel = (now.Offset - draft.Offset).Duration() > tunables.TravelJump;
                gap.Shifted = !travel;
            }

            while (true)
            {
                var dayEnd = RoutineCalendar.CareDayStart(draft.CareDay + 1, draft.NextOffset, tunables.DayStart);
                if (dayEnd > instant)
                    break;

                CloseDay(draft, Math.Max(dayEnd, checkpoint), tunables, gap);
                draft.CareDay++;
                BeginDay(draft);
                gap.NewDay = true;
                SkipCoveredDays(draft, instant, tunables, gap);
            }

            // Only the care day the jump lands on is excused, so changing the zone before a refresh cannot
            // excuse the days an absence already spans.
            if (travel)
                draft.DayExcused = true;

            CloseWindows(draft, instant, false, tunables, gap);
            draft.Checkpoint = now;
            draft.LastReason = gap.Reason(travel, draft.LastReason);
            return draft.Build();
        }

        public static CareRecord ApplyCare(CareRecord record, CareTags tags, double fullnessBefore, CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));

            var draft = record.ToBuilder();
            var at = record.Checkpoint.UtcTicks;
            draft.LastCareAt = record.Checkpoint;
            if (draft.AwaitingAnchor)
            {
                draft.LastReason = CareReason.AwaitingAnchor;
                return draft.Build();
            }

            var reason = CareReason.NoRoutineTag;
            if ((tags & CareTags.Together) != 0)
                reason = MarkTogether(draft, at, tunables);
            if ((tags & CareTags.Bedtime) != 0)
                reason = MarkMoment(draft, CareTags.Bedtime, at, CareReason.BedtimeOnTime, CareReason.BedtimeLate,
                    CareReason.BedtimeExcused, CareReason.BedtimeOutsideWindow, tunables);
            if ((tags & CareTags.Meal) != 0)
                reason = !double.IsNaN(fullnessBefore) && fullnessBefore >= tunables.FullMealThreshold
                    ? CareReason.MealTooFull
                    : MarkMoment(draft, CareTags.Meal, at, CareReason.MealOnTime, CareReason.MealLate,
                        CareReason.MealExcused, CareReason.MealOutsideWindow, tunables);

            draft.LastReason = reason;
            return draft.Build();
        }

        // The new routine applies from the next care day.
        public static CareRecord ChangeRoutine(CareRecord record, RoutinePreset preset, TimeSpan wakeTime)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            RoutineCalendar.ValidateRoutine(preset, wakeTime);
            var draft = record.ToBuilder();
            draft.NextPreset = preset;
            draft.NextWakeTime = wakeTime;
            draft.LastReason = CareReason.RoutineScheduled;
            return draft.Build();
        }

        // 0 to 1. Excused and Covered entries never count; empty rings count as 1.
        public static double Score(CareRecord record, CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));

            return tunables.MealWeight * record.Meals.Credit(tunables.MealRing, tunables.LateCredit) +
                tunables.TogetherWeight * record.TogetherDays.Credit(tunables.TogetherRing, tunables.LateCredit) +
                tunables.BedtimeWeight * record.Bedtimes.Credit(tunables.BedtimeRing, tunables.LateCredit);
        }

        // UTC instants of a moment's window on the current care day.
        public static (DateTimeOffset Open, DateTimeOffset Due, DateTimeOffset LateUntil) WindowOf(CareRecord record, CareMoment moment)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var window = RoutineCalendar.Windows(record.Preset, record.WakeTime)[(int)moment];
            return (AsInstant(Place(record.CareDay, record.Offset, window.Open)),
                AsInstant(Place(record.CareDay, record.Offset, window.Due)),
                AsInstant(Place(record.CareDay, record.Offset, window.LateUntil)));
        }

        private static CareRecord Anchor(CareRecord.Builder draft, DateTimeOffset now, CareTunables tunables)
        {
            var instant = now.UtcTicks;
            draft.Offset = now.Offset;
            draft.NextOffset = now.Offset;
            draft.CareDay = Math.Max(draft.CareDay, RoutineCalendar.CareDayOf(instant, now.Offset, tunables.DayStart));
            BeginDay(draft);
            draft.ExcusedUntil = AsInstant(Math.Max(draft.ExcusedUntil.UtcTicks, instant));
            draft.LastCareAt = now;
            draft.AwaitingAnchor = false;
            CloseWindows(draft, instant, false, tunables, new GapSummary());
            draft.Checkpoint = now;
            draft.LastReason = CareReason.GapExcused;
            return draft.Build();
        }

        private static void BeginDay(CareRecord.Builder draft)
        {
            draft.Offset = draft.NextOffset;
            draft.Preset = draft.NextPreset;
            draft.WakeTime = draft.NextWakeTime;
            draft.Moments = new MomentStatus[RoutineCalendar.MomentCount];
            draft.TogetherToday = false;
            draft.DayExcused = false;
        }

        // A day that starts after the absence ceiling is Covered whole, so long gaps cost no loop.
        private static void SkipCoveredDays(CareRecord.Builder draft, long instant, CareTunables tunables, GapSummary gap)
        {
            var dayStart = RoutineCalendar.CareDayStart(draft.CareDay, draft.Offset, tunables.DayStart);
            if (dayStart < AbsenceEnd(draft, tunables))
                return;

            var target = RoutineCalendar.CareDayOf(instant, draft.Offset, tunables.DayStart);
            if (target <= draft.CareDay)
                return;

            var skipped = target - draft.CareDay;
            draft.CoveredCount = Add(draft.CoveredCount, Multiply(skipped, RoutineCalendar.MomentCount + 1));
            draft.CareDay = target;
            gap.Covered = true;
        }

        private static void CloseDay(CareRecord.Builder draft, long dayEnd, CareTunables tunables, GapSummary gap)
        {
            CloseWindows(draft, dayEnd, true, tunables, gap);
            if (draft.TogetherToday)
                return;

            var dayStart = RoutineCalendar.CareDayStart(draft.CareDay, draft.Offset, tunables.DayStart);
            Resolve(draft, dayStart, dayEnd, tunables, gap,
                () => draft.TogetherDays = draft.TogetherDays.Push(CareMark.Missed, tunables.TogetherRing));
        }

        // Resolves pending windows that closed by `through`. At the end of a day every pending window
        // resolves, and the day end cuts a later window short.
        private static void CloseWindows(CareRecord.Builder draft, long through, bool endOfDay, CareTunables tunables,
            GapSummary gap)
        {
            foreach (var window in Windows(draft))
            {
                var index = (int)window.Moment;
                if (draft.Moments[index] != MomentStatus.Pending)
                    continue;

                var lateUntil = Place(draft.CareDay, draft.Offset, window.LateUntil);
                if (!endOfDay && lateUntil > through)
                    continue;

                var open = Place(draft.CareDay, draft.Offset, window.Open);
                var closeAt = Math.Min(lateUntil, through);
                var core = window.Core;
                draft.Moments[index] = Resolve(draft, open, closeAt, tunables, gap, () => PushMissed(draft, core, tunables));
            }
        }

        private static MomentStatus Resolve(CareRecord.Builder draft, long open, long closeAt, CareTunables tunables,
            GapSummary gap, Action pushMissed)
        {
            if (IsExcused(draft, open))
            {
                draft.ExcusedCount = Add(draft.ExcusedCount, 1);
                return MomentStatus.Excused;
            }

            if (closeAt > AbsenceEnd(draft, tunables))
            {
                draft.CoveredCount = Add(draft.CoveredCount, 1);
                gap.Covered = true;
                return MomentStatus.Covered;
            }

            pushMissed();
            gap.Missed = true;
            return MomentStatus.Missed;
        }

        private static void PushMissed(CareRecord.Builder draft, CareTags core, CareTunables tunables)
        {
            if (core == CareTags.Meal)
                draft.Meals = draft.Meals.Push(CareMark.Missed, tunables.MealRing);
            else
                draft.Bedtimes = draft.Bedtimes.Push(CareMark.Missed, tunables.BedtimeRing);
        }

        private static CareReason MarkMoment(CareRecord.Builder draft, CareTags core, long at, CareReason onTime,
            CareReason late, CareReason excused, CareReason outside, CareTunables tunables)
        {
            var windows = Windows(draft);
            // An on-time window wins over a late one; among equals the earliest moment wins.
            foreach (var lateWindow in new[] { false, true })
            {
                foreach (var window in windows)
                {
                    var index = (int)window.Moment;
                    if (window.Core != core || draft.Moments[index] != MomentStatus.Pending)
                        continue;

                    var open = Place(draft.CareDay, draft.Offset, window.Open);
                    var due = Place(draft.CareDay, draft.Offset, window.Due);
                    var lateUntil = Place(draft.CareDay, draft.Offset, window.LateUntil);
                    var inWindow = lateWindow ? at >= due && at < lateUntil : at >= open && at < due;
                    if (!inWindow)
                        continue;

                    if (IsExcused(draft, open))
                    {
                        draft.Moments[index] = MomentStatus.Excused;
                        draft.ExcusedCount = Add(draft.ExcusedCount, 1);
                        return excused;
                    }

                    var mark = lateWindow ? CareMark.Late : CareMark.OnTime;
                    draft.Moments[index] = lateWindow ? MomentStatus.Late : MomentStatus.OnTime;
                    if (core == CareTags.Meal)
                        draft.Meals = draft.Meals.Push(mark, tunables.MealRing);
                    else
                        draft.Bedtimes = draft.Bedtimes.Push(mark, tunables.BedtimeRing);

                    return lateWindow ? late : onTime;
                }
            }

            return outside;
        }

        private static CareReason MarkTogether(CareRecord.Builder draft, long at, CareTunables tunables)
        {
            if (draft.TogetherToday)
                return CareReason.TogetherAlreadyCounted;

            // Together time done after the grace period counts; a grace day without it is excused at day end.
            draft.TogetherToday = true;
            if (IsExcused(draft, at))
            {
                draft.ExcusedCount = Add(draft.ExcusedCount, 1);
                return CareReason.TogetherExcused;
            }

            draft.TogetherDays = draft.TogetherDays.Push(CareMark.OnTime, tunables.TogetherRing);
            return CareReason.TogetherCounted;
        }

        private static bool IsExcused(CareRecord.Builder draft, long open) =>
            draft.DayExcused || open < draft.ExcusedUntil.UtcTicks;

        private static long AbsenceEnd(CareRecord.Builder draft, CareTunables tunables) =>
            Add(draft.LastCareAt.UtcTicks, tunables.AbsenceCeiling.Ticks);

        private static IReadOnlyList<CareWindow> Windows(CareRecord.Builder draft) =>
            RoutineCalendar.Windows(draft.Preset, draft.WakeTime);

        private static long Place(long careDay, TimeSpan offset, TimeSpan localTime) =>
            RoutineCalendar.Place(careDay, offset, localTime);

        private static DateTimeOffset AsInstant(long utcTicks) =>
            new DateTimeOffset(Math.Max(0, Math.Min(MaxTicks, utcTicks)), TimeSpan.Zero);

        private static long Add(long value, long amount) =>
            amount > 0 && value > long.MaxValue - amount ? long.MaxValue : value + amount;

        private static long Multiply(long value, long factor) =>
            value > long.MaxValue / factor ? long.MaxValue : value * factor;

        // What happened during one Advance, reduced to a single diagnostic reason.
        private sealed class GapSummary
        {
            public bool NewDay;
            public bool Shifted;
            public bool Missed;
            public bool Covered;

            public CareReason Reason(bool travel, CareReason previous)
            {
                if (Covered)
                    return CareReason.AbsenceCovered;
                if (travel)
                    return CareReason.TravelDayExcused;
                if (Missed)
                    return CareReason.WindowMissed;
                if (Shifted)
                    return CareReason.OffsetShiftScheduled;

                return NewDay ? CareReason.NewCareDay : previous;
            }
        }
    }
}
