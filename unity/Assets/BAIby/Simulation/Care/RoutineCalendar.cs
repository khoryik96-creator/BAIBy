using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // Saved values are stable IDs. Append new options; never renumber.
    public enum RoutinePreset { SchoolDay = 0, Home = 1 }
    public enum CareMoment { Morning = 0, Afternoon = 1, Evening = 2, Bedtime = 3 }

    // One moment of the routine as local times of day. On time is [Open, Due), late is [Due, LateUntil).
    public sealed class CareWindow
    {
        public CareMoment Moment { get; }
        public CareTags Core { get; }
        public TimeSpan Open { get; }
        public TimeSpan Due { get; }
        public TimeSpan LateUntil { get; }

        public CareWindow(CareMoment moment, CareTags core, TimeSpan open, TimeSpan due, TimeSpan lateUntil)
        {
            if (!Enum.IsDefined(typeof(CareMoment), moment))
                throw new ArgumentOutOfRangeException(nameof(moment));
            if (core != CareTags.Meal && core != CareTags.Bedtime)
                throw new ArgumentOutOfRangeException(nameof(core), "A window's core is a meal or a bedtime.");
            if (open < TimeSpan.Zero || due <= open || lateUntil <= due)
                throw new ArgumentException("A care window needs Open < Due < LateUntil.");

            Moment = moment;
            Core = core;
            Open = open;
            Due = due;
            LateUntil = lateUntil;
        }

        internal CareWindow Shifted(TimeSpan open) =>
            new CareWindow(Moment, Core, open, open + (Due - Open), open + (LateUntil - Open));
    }

    // Places the routine on the real clock. All instants are UTC ticks; the offset only places windows.
    public static class RoutineCalendar
    {
        public const int MomentCount = 4;
        public static readonly TimeSpan DefaultWakeTime = TimeSpan.FromHours(7);
        public static readonly TimeSpan EarliestWakeTime = TimeSpan.FromHours(5);
        public static readonly TimeSpan LatestWakeTime = TimeSpan.FromHours(10);

        // School-friendly default: nothing during school hours, three meals and a bedtime.
        private static readonly CareWindow[] SchoolDay =
        {
            Window(CareMoment.Morning, CareTags.Meal, 7, 0, 10, 0, 12, 0),
            Window(CareMoment.Afternoon, CareTags.Meal, 14, 0, 17, 0, 18, 0),
            Window(CareMoment.Evening, CareTags.Meal, 18, 0, 20, 30, 21, 30),
            Window(CareMoment.Bedtime, CareTags.Bedtime, 19, 30, 21, 30, 22, 30),
        };

        // Home preset for a player who is around all day: a lunch replaces the after-school meal.
        private static readonly CareWindow[] Home =
        {
            Window(CareMoment.Morning, CareTags.Meal, 7, 0, 9, 30, 11, 0),
            Window(CareMoment.Afternoon, CareTags.Meal, 12, 0, 14, 0, 15, 0),
            Window(CareMoment.Evening, CareTags.Meal, 17, 30, 19, 30, 20, 30),
            Window(CareMoment.Bedtime, CareTags.Bedtime, 19, 0, 20, 30, 21, 30),
        };

        // The wake time moves only the morning window; the rest of the day stays school-friendly.
        public static IReadOnlyList<CareWindow> Windows(RoutinePreset preset, TimeSpan wakeTime)
        {
            ValidateRoutine(preset, wakeTime);
            var template = preset == RoutinePreset.Home ? Home : SchoolDay;
            var windows = (CareWindow[])template.Clone();
            if (wakeTime != DefaultWakeTime)
                windows[(int)CareMoment.Morning] = template[(int)CareMoment.Morning].Shifted(wakeTime);

            return Array.AsReadOnly(windows);
        }

        public static void ValidateRoutine(RoutinePreset preset, TimeSpan wakeTime)
        {
            if (!Enum.IsDefined(typeof(RoutinePreset), preset))
                throw new ArgumentOutOfRangeException(nameof(preset));
            if (wakeTime < EarliestWakeTime || wakeTime > LatestWakeTime || wakeTime.Ticks % TimeSpan.TicksPerMinute != 0)
                throw new ArgumentOutOfRangeException(nameof(wakeTime), "Wake time is a whole minute from 05:00 to 10:00.");
        }

        // The care day that contains a UTC instant at a local offset, as days since 0001-01-01.
        public static long CareDayOf(long utcTicks, TimeSpan offset, TimeSpan dayStart) =>
            FloorDiv(utcTicks + offset.Ticks - dayStart.Ticks, TimeSpan.TicksPerDay);

        // The UTC instant at which a care day starts under an offset.
        public static long CareDayStart(long careDay, TimeSpan offset, TimeSpan dayStart) =>
            Place(careDay, offset, dayStart);

        // The UTC instant of a local time of day on the calendar date of a care day.
        public static long Place(long careDay, TimeSpan offset, TimeSpan localTimeOfDay) =>
            careDay * TimeSpan.TicksPerDay + localTimeOfDay.Ticks - offset.Ticks;

        public static DateTime CalendarDate(long careDay) => new DateTime(careDay * TimeSpan.TicksPerDay);

        private static long FloorDiv(long value, long divisor)
        {
            var quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        private static CareWindow Window(CareMoment moment, CareTags core, int openHour, int openMinute,
            int dueHour, int dueMinute, int lateHour, int lateMinute) =>
            new CareWindow(moment, core, new TimeSpan(openHour, openMinute, 0), new TimeSpan(dueHour, dueMinute, 0),
                new TimeSpan(lateHour, lateMinute, 0));
    }
}
