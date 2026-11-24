using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // Saved values are stable IDs. Append new options; never renumber.
    public enum RoutinePreset { SchoolDay = 0, Home = 1 }
    // Dawn (P3) is the fifth moment of a routine that has one: a dog's morning walk. Other routines keep four moments,
    // so their ledgers, saves and window keys never change.
    public enum CareMoment { Morning = 0, Afternoon = 1, Evening = 2, Bedtime = 3, Dawn = 4 }
    // Whose routine a ledger keeps: a species' windows (docs/care-and-growth.md, Pets and household). Not saved: a
    // baby's ledger is always Baby, and a pet's codec names the routine from its species.
    public enum CareRoutine { Baby, Cat, Hamster, Dog }

    // One moment of the routine as local times of day. On time is [Open, Due), late is [Due, LateUntil).
    //
    // A nap block (a hamster's day nap) is a Bedtime moment the companion keeps by itself: nothing is asked of the
    // player, so it never counts, is never missed and is never owed. It closes Excused, and care during it is gentle.
    //
    // A grace window (a dog's morning walk, P3) can be done on time or late like any window, but when it closes undone
    // it closes Excused, never missed, owed or counted, and it never excuses the day: school mornings are busy, and the
    // evening walk keeps the day.
    public sealed class CareWindow
    {
        public CareMoment Moment { get; }
        public CareTags Core { get; }
        public TimeSpan Open { get; }
        public TimeSpan Due { get; }
        public TimeSpan LateUntil { get; }
        public bool Nap { get; }
        public bool Grace { get; }

        public CareWindow(CareMoment moment, CareTags core, TimeSpan open, TimeSpan due, TimeSpan lateUntil)
            : this(moment, core, open, due, lateUntil, false)
        {
        }

        public CareWindow(CareMoment moment, CareTags core, TimeSpan open, TimeSpan due, TimeSpan lateUntil, bool nap)
            : this(moment, core, open, due, lateUntil, nap, false)
        {
        }

        public CareWindow(CareMoment moment, CareTags core, TimeSpan open, TimeSpan due, TimeSpan lateUntil, bool nap,
            bool grace)
        {
            if (nap && core != CareTags.Bedtime)
                throw new ArgumentOutOfRangeException(nameof(core), "A nap block is a bedtime moment.");
            if (grace && (nap || core == CareTags.Meal || core == CareTags.Bedtime))
                throw new ArgumentOutOfRangeException(nameof(grace), "Only a play or walk window can close with grace.");
            if (!Enum.IsDefined(typeof(CareMoment), moment))
                throw new ArgumentOutOfRangeException(nameof(moment));
            if (core != CareTags.Meal && core != CareTags.Bedtime && core != CareTags.Play && core != CareTags.Walk)
                throw new ArgumentOutOfRangeException(nameof(core), "A window's core is a meal, play, a walk or a bedtime.");
            if (open < TimeSpan.Zero || due <= open || lateUntil <= due)
                throw new ArgumentException("A care window needs Open < Due < LateUntil.");

            Moment = moment;
            Core = core;
            Open = open;
            Due = due;
            LateUntil = lateUntil;
            Nap = nap;
            Grace = grace;
        }

        internal CareWindow Shifted(TimeSpan open) =>
            new CareWindow(Moment, Core, open, open + (Due - Open), open + (LateUntil - Open), Nap, Grace);
    }

    // Places the routine on the real clock. All instants are UTC ticks; the offset only places windows.
    public static class RoutineCalendar
    {
        // The moments of a four-moment routine, which also key meal windows (WindowKey). A routine may have one more
        // (MaxMoments: a dog's Dawn walk), which is never a meal and so never keyed.
        public const int MomentCount = 4;
        public const int MaxMoments = 5;
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

        // A cat eats twice (Morning and Evening) and has a play window in the Afternoon slot instead of a meal; its
        // bedtime is the evening nap. The same four moments, so the ledger, the strip and the saves keep four letters.
        private static readonly CareWindow[] CatSchoolDay =
        {
            Window(CareMoment.Morning, CareTags.Meal, 7, 0, 10, 0, 12, 0),
            Window(CareMoment.Afternoon, CareTags.Play, 15, 0, 18, 0, 19, 0),
            Window(CareMoment.Evening, CareTags.Meal, 18, 0, 20, 30, 21, 30),
            Window(CareMoment.Bedtime, CareTags.Bedtime, 19, 30, 21, 30, 22, 30),
        };

        private static readonly CareWindow[] CatHome =
        {
            Window(CareMoment.Morning, CareTags.Meal, 7, 0, 9, 30, 11, 0),
            Window(CareMoment.Afternoon, CareTags.Play, 12, 0, 15, 0, 16, 0),
            Window(CareMoment.Evening, CareTags.Meal, 17, 30, 19, 30, 20, 30),
            Window(CareMoment.Bedtime, CareTags.Bedtime, 19, 0, 20, 30, 21, 30),
        };

        // A hamster (docs/care-and-growth.md, Pets and household) is nocturnal: it naps through the day and is busiest in
        // the evening, so its one meal is dinner and its wheel time is the evening play window. Its day nap fills the
        // Morning and Bedtime moments as two nap blocks (morning and afternoon), so the ledger, the saves and the
        // window keys keep their four moments; nothing is asked during school hours. The wake time does not move it.
        private static readonly CareWindow[] HamsterSchoolDay =
        {
            NapBlock(CareMoment.Morning, 7, 0, 11, 30, 12, 0),
            Window(CareMoment.Afternoon, CareTags.Play, 17, 0, 20, 0, 21, 0),
            Window(CareMoment.Evening, CareTags.Meal, 17, 0, 20, 30, 21, 30),
            NapBlock(CareMoment.Bedtime, 12, 0, 16, 0, 16, 30),
        };

        private static readonly CareWindow[] HamsterHome =
        {
            NapBlock(CareMoment.Morning, 7, 0, 11, 0, 11, 30),
            Window(CareMoment.Afternoon, CareTags.Play, 16, 0, 19, 0, 20, 0),
            Window(CareMoment.Evening, CareTags.Meal, 16, 30, 19, 30, 20, 30),
            NapBlock(CareMoment.Bedtime, 11, 30, 15, 0, 15, 30),
        };

        // A dog (P3, docs/care-and-growth.md, Pets and household) eats twice like a cat and goes for two walks: an early
        // walk before school (the Dawn moment, a grace window: if school gets in the way it closes excused) and an
        // evening walk after school in the Afternoon slot, where a cat plays. Bedtime is the evening settle. In time
        // order: morning walk, breakfast, evening walk, dinner, bed. The wake time moves the morning walk and breakfast.
        private static readonly CareWindow[] DogSchoolDay =
        {
            Window(CareMoment.Morning, CareTags.Meal, 7, 0, 10, 0, 12, 0),
            Window(CareMoment.Afternoon, CareTags.Walk, 15, 30, 19, 0, 20, 0),
            Window(CareMoment.Evening, CareTags.Meal, 18, 0, 20, 30, 21, 30),
            Window(CareMoment.Bedtime, CareTags.Bedtime, 19, 30, 21, 30, 22, 30),
            GraceWalk(CareMoment.Dawn, 7, 0, 9, 0, 10, 0),
        };

        private static readonly CareWindow[] DogHome =
        {
            Window(CareMoment.Morning, CareTags.Meal, 7, 0, 9, 30, 11, 0),
            Window(CareMoment.Afternoon, CareTags.Walk, 15, 0, 18, 0, 19, 0),
            Window(CareMoment.Evening, CareTags.Meal, 17, 30, 19, 30, 20, 30),
            Window(CareMoment.Bedtime, CareTags.Bedtime, 19, 0, 20, 30, 21, 30),
            GraceWalk(CareMoment.Dawn, 7, 0, 9, 30, 10, 30),
        };

        // The wake time moves only the morning window (and a dog's morning walk); the rest of the day stays
        // school-friendly.
        public static IReadOnlyList<CareWindow> Windows(RoutinePreset preset, TimeSpan wakeTime) =>
            Windows(CareRoutine.Baby, preset, wakeTime);

        public static IReadOnlyList<CareWindow> Windows(CareRoutine routine, RoutinePreset preset, TimeSpan wakeTime)
        {
            ValidateRoutine(preset, wakeTime);
            if (!Enum.IsDefined(typeof(CareRoutine), routine))
                throw new ArgumentOutOfRangeException(nameof(routine));
            var template = Template(routine, preset);
            var windows = (CareWindow[])template.Clone();
            // A day nap is the companion's own: the player's wake time does not move it.
            if (wakeTime != DefaultWakeTime && !template[(int)CareMoment.Morning].Nap)
                windows[(int)CareMoment.Morning] = template[(int)CareMoment.Morning].Shifted(wakeTime);
            if (wakeTime != DefaultWakeTime && template.Length > (int)CareMoment.Dawn)
                windows[(int)CareMoment.Dawn] = template[(int)CareMoment.Dawn].Shifted(wakeTime);

            return Array.AsReadOnly(windows);
        }

        private static CareWindow[] Template(CareRoutine routine, RoutinePreset preset)
        {
            var home = preset == RoutinePreset.Home;
            switch (routine)
            {
                case CareRoutine.Cat: return home ? CatHome : CatSchoolDay;
                case CareRoutine.Hamster: return home ? HamsterHome : HamsterSchoolDay;
                case CareRoutine.Dog: return home ? DogHome : DogSchoolDay;
                default: return home ? Home : SchoolDay;
            }
        }

        // How many moments a care day of the routine has: four, or five with a dog's Dawn walk.
        public static int MomentsOf(CareRoutine routine)
        {
            if (!Enum.IsDefined(typeof(CareRoutine), routine))
                throw new ArgumentOutOfRangeException(nameof(routine));
            return Template(routine, RoutinePreset.SchoolDay).Length;
        }

        // The daily wash: on time from the morning opening until the evening meal is due, late until the
        // evening meal's late limit. A missed wash leaves the room messier.
        // A cat's daily wash is its brush.
        public static (TimeSpan Open, TimeSpan Due, TimeSpan LateUntil) WashWindow(RoutinePreset preset, TimeSpan wakeTime) =>
            WashWindow(CareRoutine.Baby, preset, wakeTime);

        public static (TimeSpan Open, TimeSpan Due, TimeSpan LateUntil) WashWindow(CareRoutine routine, RoutinePreset preset,
            TimeSpan wakeTime)
        {
            var windows = Windows(routine, preset, wakeTime);
            var evening = windows[(int)CareMoment.Evening];
            return (windows[(int)CareMoment.Morning].Open, evening.Due, evening.LateUntil);
        }

        // Quiet hours run from the bedtime window's due time until the wake time. A routine with a day nap rests by day
        // instead: its quiet hours run from the morning nap's opening until the afternoon nap's due time (Start before End,
        // within one day).
        public static (TimeSpan Start, TimeSpan End) Night(RoutinePreset preset, TimeSpan wakeTime) =>
            Night(CareRoutine.Baby, preset, wakeTime);

        public static (TimeSpan Start, TimeSpan End) Night(CareRoutine routine, RoutinePreset preset, TimeSpan wakeTime)
        {
            var windows = Windows(routine, preset, wakeTime);
            var bedtime = windows[(int)CareMoment.Bedtime];
            return bedtime.Nap ? (windows[(int)CareMoment.Morning].Open, bedtime.Due) : (bedtime.Due, wakeTime);
        }

        // Whether the routine's rest is a day nap the companion keeps by itself (a hamster's).
        public static bool NapsByDay(CareRoutine routine) =>
            Windows(routine, RoutinePreset.SchoolDay, DefaultWakeTime)[(int)CareMoment.Bedtime].Nap;

        // Whether a local time of day falls in the routine's day nap (its quiet hours).
        public static bool IsNapTime(CareRoutine routine, RoutinePreset preset, TimeSpan wakeTime, TimeSpan timeOfDay)
        {
            if (!NapsByDay(routine))
                return false;
            var night = Night(routine, preset, wakeTime);
            return timeOfDay >= night.Start && timeOfDay < night.End;
        }

        // How many meal windows a care day of the routine has (three for a baby, two for a cat, one for a hamster).
        public static int MealWindows(CareRoutine routine)
        {
            var meals = 0;
            foreach (var window in Windows(routine, RoutinePreset.SchoolDay, DefaultWakeTime))
            {
                if (window.Core == CareTags.Meal)
                    meals++;
            }

            return meals;
        }

        // Whether the routine has a timed play window (or a dog's walk windows). Its together ring then follows those
        // windows, not the whole day.
        public static bool HasPlayWindow(CareRoutine routine) =>
            routine == CareRoutine.Cat || routine == CareRoutine.Hamster || routine == CareRoutine.Dog;

        // Whether the routine has walk windows (a dog's two walks).
        public static bool HasWalks(CareRoutine routine) => routine == CareRoutine.Dog;

        // How many on-time meals a good care day asks for: two, or every meal of a routine with fewer.
        public static int GoodDayMeals(CareRoutine routine) => Math.Min(2, MealWindows(routine));

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

        private static CareWindow NapBlock(CareMoment moment, int openHour, int openMinute, int dueHour, int dueMinute,
            int lateHour, int lateMinute) =>
            new CareWindow(moment, CareTags.Bedtime, new TimeSpan(openHour, openMinute, 0), new TimeSpan(dueHour, dueMinute, 0),
                new TimeSpan(lateHour, lateMinute, 0), true);

        private static CareWindow GraceWalk(CareMoment moment, int openHour, int openMinute, int dueHour, int dueMinute,
            int lateHour, int lateMinute) =>
            new CareWindow(moment, CareTags.Walk, new TimeSpan(openHour, openMinute, 0), new TimeSpan(dueHour, dueMinute, 0),
                new TimeSpan(lateHour, lateMinute, 0), false, true);

        private static CareWindow Window(CareMoment moment, CareTags core, int openHour, int openMinute,
            int dueHour, int dueMinute, int lateHour, int lateMinute) =>
            new CareWindow(moment, core, new TimeSpan(openHour, openMinute, 0), new TimeSpan(dueHour, dueMinute, 0),
                new TimeSpan(lateHour, lateMinute, 0));
    }
}
