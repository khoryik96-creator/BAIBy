using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // Display-only; never saved. Hello is a fish's morning hello at the glass (P4), a grace play window.
    public enum RoutineSlotKind { MorningMeal, AfternoonMeal, EveningMeal, Play, Bedtime, Wash, Nap, Walk, Hello }

    // Later: the window has not opened. Due: open now. Overdue: past due, still waiting (late if done now). Helped: a
    // helper served the meal (it still waits for a hands-on meal while the window is open). Napping: a day nap is under
    // way (a hamster's; it asks nothing of the player).
    public enum RoutineSlotState { Later, Due, Overdue, OnTime, DoneLate, Missed, Excused, Away, Helped, Napping }

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

            var windows = RoutineCalendar.Windows(record.Routine, record.Preset, record.WakeTime);
            if (RoutineCalendar.NapsByDay(record.Routine))
                return NapDay(record, windows);
            if (RoutineCalendar.HasWalks(record.Routine))
                return WalkDay(record, windows);
            if (RoutineCalendar.HasMorningHello(record.Routine))
                return GlassDay(record, windows);

            var slots = new List<RoutineSlot>(6);
            foreach (var window in windows)
            {
                var kind = window.Core == CareTags.Bedtime ? RoutineSlotKind.Bedtime
                    : window.Core == CareTags.Play ? RoutineSlotKind.Play : (RoutineSlotKind)(int)window.Moment;
                slots.Add(Slot(record, kind, record.StatusOf(window.Moment), window.Open, window.Due, window.LateUntil));
            }

            // Bedtime reads best last: Breakfast, Lunch, Dinner, Play, Bed, Wash. A cat's play window keeps its place
            // between the meals: Breakfast, Play, Dinner, Bed, Brush.
            var bedtime = slots[(int)CareMoment.Bedtime];
            slots.RemoveAt((int)CareMoment.Bedtime);
            if (!RoutineCalendar.HasPlayWindow(record.Routine))
            {
                var morning = windows[(int)CareMoment.Morning];
                var together = record.TogetherToday ? MomentStatus.OnTime : MomentStatus.Pending;
                // Together time is due all day and counts whenever it happens.
                slots.Add(Slot(record, RoutineSlotKind.Play, together, morning.Open, windows[(int)CareMoment.Bedtime].LateUntil,
                    windows[(int)CareMoment.Bedtime].LateUntil, anytime: true));
            }

            slots.Add(bedtime);
            var wash = RoutineCalendar.WashWindow(record.Routine, record.Preset, record.WakeTime);
            slots.Add(Slot(record, RoutineSlotKind.Wash, record.Wash, wash.Open, wash.Due, wash.LateUntil));
            return slots.AsReadOnly();
        }

        // A hamster's day: its nap through school hours (one slot for both nap blocks), the wheel, dinner and the bedding.
        private static IReadOnlyList<RoutineSlot> NapDay(CareRecord record, IReadOnlyList<CareWindow> windows)
        {
            var morning = windows[(int)CareMoment.Morning];
            var afternoon = windows[(int)CareMoment.Bedtime];
            var napOpen = Local(record, morning.Open);
            var napDue = Local(record, afternoon.Due);
            var napEnd = Local(record, afternoon.LateUntil);
            var at = record.Checkpoint;
            var napState = record.IsAway ? RoutineSlotState.Away
                : at < napOpen ? RoutineSlotState.Later
                : at < napDue ? RoutineSlotState.Napping : RoutineSlotState.Excused;
            var slots = new List<RoutineSlot>(4) { new RoutineSlot(RoutineSlotKind.Nap, napState, napOpen, napDue, napEnd) };
            foreach (var moment in new[] { CareMoment.Afternoon, CareMoment.Evening })
            {
                var window = windows[(int)moment];
                var kind = window.Core == CareTags.Play ? RoutineSlotKind.Play : (RoutineSlotKind)(int)window.Moment;
                slots.Add(Slot(record, kind, record.StatusOf(moment), window.Open, window.Due, window.LateUntil));
            }

            var wash = RoutineCalendar.WashWindow(record.Routine, record.Preset, record.WakeTime);
            slots.Add(Slot(record, RoutineSlotKind.Wash, record.Wash, wash.Open, wash.Due, wash.LateUntil));
            return slots.AsReadOnly();
        }

        // A dog's day in time order: the morning walk, breakfast, the evening walk, dinner, bed and the brush.
        private static IReadOnlyList<RoutineSlot> WalkDay(CareRecord record, IReadOnlyList<CareWindow> windows)
        {
            var slots = new List<RoutineSlot>(6);
            foreach (var moment in new[] { CareMoment.Dawn, CareMoment.Morning, CareMoment.Afternoon, CareMoment.Evening,
                CareMoment.Bedtime })
            {
                var window = windows[(int)moment];
                var kind = window.Core == CareTags.Walk ? RoutineSlotKind.Walk
                    : window.Core == CareTags.Bedtime ? RoutineSlotKind.Bedtime : (RoutineSlotKind)(int)window.Moment;
                slots.Add(Slot(record, kind, record.StatusOf(moment), window.Open, window.Due, window.LateUntil));
            }

            var wash = RoutineCalendar.WashWindow(record.Routine, record.Preset, record.WakeTime);
            slots.Add(Slot(record, RoutineSlotKind.Wash, record.Wash, wash.Open, wash.Due, wash.LateUntil));
            return slots.AsReadOnly();
        }

        // A fish's day: the morning hello, the day's one sprinkle of food, glass time, lights off and the wipe of the glass.
        private static IReadOnlyList<RoutineSlot> GlassDay(CareRecord record, IReadOnlyList<CareWindow> windows)
        {
            var slots = new List<RoutineSlot>(5);
            foreach (var moment in new[] { CareMoment.Morning, CareMoment.Evening, CareMoment.Afternoon, CareMoment.Bedtime })
            {
                var window = windows[(int)moment];
                var kind = window.Grace ? RoutineSlotKind.Hello
                    : window.Core == CareTags.Play ? RoutineSlotKind.Play
                    : window.Core == CareTags.Bedtime ? RoutineSlotKind.Bedtime : RoutineSlotKind.EveningMeal;
                slots.Add(Slot(record, kind, record.StatusOf(moment), window.Open, window.Due, window.LateUntil));
            }

            var wash = RoutineCalendar.WashWindow(record.Routine, record.Preset, record.WakeTime);
            slots.Add(Slot(record, RoutineSlotKind.Wash, record.Wash, wash.Open, wash.Due, wash.LateUntil));
            return slots.AsReadOnly();
        }

        private static RoutineSlot Slot(CareRecord record, RoutineSlotKind kind, MomentStatus status, TimeSpan open,
            TimeSpan due, TimeSpan lateUntil, bool anytime = false)
        {
            var openAt = Local(record, open);
            var dueAt = Local(record, due);
            var lateAt = Local(record, lateUntil);
            var meal = kind == RoutineSlotKind.MorningMeal || kind == RoutineSlotKind.AfternoonMeal ||
                kind == RoutineSlotKind.EveningMeal;
            return new RoutineSlot(kind, StateOf(record, status, openAt, dueAt, lateAt, anytime, meal), openAt, dueAt, lateAt);
        }

        private static RoutineSlotState StateOf(CareRecord record, MomentStatus status, DateTimeOffset open,
            DateTimeOffset due, DateTimeOffset lateUntil, bool anytime, bool meal)
        {
            var away = record.AwayUntil > record.AwayFrom && open < record.AwayUntil && lateUntil > record.AwayFrom;
            switch (status)
            {
                case MomentStatus.OnTime: return RoutineSlotState.OnTime;
                case MomentStatus.Late: return RoutineSlotState.DoneLate;
                case MomentStatus.Missed: return RoutineSlotState.Missed;
                case MomentStatus.Excused: return RoutineSlotState.Excused;
                case MomentStatus.Covered: return away ? RoutineSlotState.Away : RoutineSlotState.Excused;
                case MomentStatus.Helped: return RoutineSlotState.Helped;
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
            var served = record.Helpers.LastServe;
            if (meal && served > 0 && served >= open.UtcTicks && served <= at.UtcTicks && at < lateUntil)
                return RoutineSlotState.Helped;
            if (at < due)
                return RoutineSlotState.Due;

            return at < lateUntil ? RoutineSlotState.Overdue : RoutineSlotState.Missed;
        }

        private static DateTimeOffset Local(CareRecord record, TimeSpan timeOfDay) =>
            new DateTimeOffset(RoutineCalendar.Place(record.CareDay, record.Offset, timeOfDay), TimeSpan.Zero)
                .ToOffset(record.Offset);
    }
}
