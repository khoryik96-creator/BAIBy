using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // Display-only; never saved.
    public enum RoutineSlotKind { MorningMeal, AfternoonMeal, EveningMeal, Play, Bedtime, Wash }

    // Later: the window has not opened. Due: open now. Overdue: past due, still waiting (late if done now).
    public enum RoutineSlotState { Later, Due, Overdue, OnTime, DoneLate, Missed, Excused, Away }

    public sealed class RoutineSlot
    {
        public RoutineSlotKind Kind { get; }
        public RoutineSlotState State { get; }
        // Local times of the window on the current care day (the record's offset).
        public DateTimeOffset Open { get; }
        public DateTimeOffset Due { get; }
        public DateTimeOffset LateUntil { get; }

        public RoutineSlot(RoutineSlotKind kind, RoutineSlotState state, DateTimeOffset open, DateTimeOffset due,
            DateTimeOffset lateUntil)
        {
            Kind = kind;
            State = state;
            Open = open;
            Due = due;
            LateUntil = lateUntil;
        }
    }

    // Today's routine as the nursery's strip shows it, read from the ledger at its checkpoint.
    public static class RoutineStrip
    {
        public static IReadOnlyList<RoutineSlot> Today(CareRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var windows = RoutineCalendar.Windows(record.Preset, record.WakeTime);
            var slots = new List<RoutineSlot>(6);
            foreach (var window in windows)
            {
                var kind = window.Core == CareTags.Bedtime ? RoutineSlotKind.Bedtime : (RoutineSlotKind)(int)window.Moment;
                slots.Add(Slot(record, kind, record.StatusOf(window.Moment), window.Open, window.Due, window.LateUntil));
            }

            // Bedtime reads best last: Breakfast, Lunch, Dinner, Play, Bed, Wash.
            var bedtime = slots[(int)CareMoment.Bedtime];
            slots.RemoveAt((int)CareMoment.Bedtime);
            var morning = windows[(int)CareMoment.Morning];
            var together = record.TogetherToday ? MomentStatus.OnTime : MomentStatus.Pending;
            // Together time is due all day and counts whenever it happens.
            slots.Add(Slot(record, RoutineSlotKind.Play, together, morning.Open, windows[(int)CareMoment.Bedtime].LateUntil,
                windows[(int)CareMoment.Bedtime].LateUntil, anytime: true));
            slots.Add(bedtime);
            var wash = RoutineCalendar.WashWindow(record.Preset, record.WakeTime);
            slots.Add(Slot(record, RoutineSlotKind.Wash, record.Wash, wash.Open, wash.Due, wash.LateUntil));
            return slots.AsReadOnly();
        }

        private static RoutineSlot Slot(CareRecord record, RoutineSlotKind kind, MomentStatus status, TimeSpan open,
            TimeSpan due, TimeSpan lateUntil, bool anytime = false)
        {
            var openAt = Local(record, open);
            var dueAt = Local(record, due);
            var lateAt = Local(record, lateUntil);
            return new RoutineSlot(kind, StateOf(record, status, openAt, dueAt, lateAt, anytime), openAt, dueAt, lateAt);
        }

        private static RoutineSlotState StateOf(CareRecord record, MomentStatus status, DateTimeOffset open,
            DateTimeOffset due, DateTimeOffset lateUntil, bool anytime)
        {
            var away = record.AwayUntil > record.AwayFrom && open < record.AwayUntil && lateUntil > record.AwayFrom;
            switch (status)
            {
                case MomentStatus.OnTime: return RoutineSlotState.OnTime;
                case MomentStatus.Late: return RoutineSlotState.DoneLate;
                case MomentStatus.Missed: return RoutineSlotState.Missed;
                case MomentStatus.Excused: return RoutineSlotState.Excused;
                case MomentStatus.Covered: return away ? RoutineSlotState.Away : RoutineSlotState.Excused;
            }

            var at = record.Checkpoint;
            if (record.IsAway)
                return RoutineSlotState.Away;
            if (record.AwaitingAnchor || record.DayExcused || (anytime ? at : open) < record.ExcusedUntil)
                return RoutineSlotState.Excused;
            if (anytime)
                return RoutineSlotState.Due;
            if (at < open)
                return RoutineSlotState.Later;
            if (at < due)
                return RoutineSlotState.Due;

            return at < lateUntil ? RoutineSlotState.Overdue : RoutineSlotState.Missed;
        }

        private static DateTimeOffset Local(CareRecord record, TimeSpan timeOfDay) =>
            new DateTimeOffset(RoutineCalendar.Place(record.CareDay, record.Offset, timeOfDay), TimeSpan.Zero)
                .ToOffset(record.Offset);
    }
}
