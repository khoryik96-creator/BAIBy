using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Infrastructure;
using BAIby.Simulation;

// Care slice 1, the shadow ledger: calendar, clock and save checks. See docs/care-and-growth.md.
static class CareChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly TimeSpan Paris = TimeSpan.FromHours(1);


    public static void Run()
    {
        TunablesAndProfile();
        CalendarWindows();
        OnTimeLateAndMissed();
        FirstDayIsExcused();
        DaylightSavingShifts();
        Travel();
        ClockFollowsZoneChanges();
        BackwardsClock();
        LongAbsence();
        MigrationWithoutSurprise();
        RoundTrip();
        InvalidCareIsRejected();
        ShadowGuarantee();
        Console.WriteLine("Care ledger checks passed.");
    }

    private static void TunablesAndProfile()
    {
        Tunables.Validate();
        ExpectArgument(() => new CareTunables(mealWeight: 0.7).Validate(), "CareScore weights must add up to 1");
        ExpectArgument(() => new CareTunables(absenceCeiling: TimeSpan.Zero).Validate(), "absence ceiling must be positive");
        ExpectArgument(() => new CareTunables(dayStart: TimeSpan.FromHours(6)).Validate(), "care days start before waking");
        ExpectArgument(() => new CareTunables(mealRing: CareRing.MaxLength + 1).Validate(), "rings fit the saved maximum");
        ExpectArgument(() => new CareTunables(lateCredit: double.NaN).Validate(), "late credit must be a number");
        ExpectArgument(() => new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7),
            TimeSpan.FromDays(14), new CareTunables(fullMealThreshold: 0)), "the simulation validates care tunables");

        foreach (CareAction action in Enum.GetValues(typeof(CareAction)))
            BabySimulation.BabyCareProfile.TagsFor(BabySimulation.CareVerbFor(action));
        Equal(CareTags.Meal, Tags(CareAction.Feed), "feeding is a meal");
        Equal(CareTags.Bedtime, Tags(CareAction.Rest), "rest is the bedtime core");
        Equal(CareTags.Together | CareTags.Comfort, Tags(CareAction.Soothe), "soothing is together time that comforts");
        Equal(CareTags.Together, Tags(CareAction.ReadStory), "a story is together time");
        Equal(CareTags.Hygiene, Tags(CareAction.Bath), "a bath is hygiene");
        ExpectArgument(() => BabySimulation.BabyCareProfile.TagsFor("juggle"), "unknown verbs are rejected");
        ExpectArgument(() => new CareProfile("cat", new[]
        {
            new KeyValuePair<string, CareTags>("feed", CareTags.Meal), new KeyValuePair<string, CareTags>("feed", CareTags.Meal),
        }), "a profile cannot list a verb twice");
        ExpectArgument(() => new CareProfile("cat", new[] { new KeyValuePair<string, CareTags>("feed", (CareTags)128) }),
            "a profile cannot use unknown tags");
    }

    private static CareTags Tags(CareAction action) =>
        BabySimulation.BabyCareProfile.TagsFor(BabySimulation.CareVerbFor(action));

    private static void CalendarWindows()
    {
        var school = RoutineCalendar.Windows(RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        ExpectWindow(school[0], CareMoment.Morning, CareTags.Meal, "07:00", "10:00", "12:00");
        ExpectWindow(school[1], CareMoment.Afternoon, CareTags.Meal, "14:00", "17:00", "18:00");
        ExpectWindow(school[2], CareMoment.Evening, CareTags.Meal, "18:00", "20:30", "21:30");
        ExpectWindow(school[3], CareMoment.Bedtime, CareTags.Bedtime, "19:30", "21:30", "22:30");
        var home = RoutineCalendar.Windows(RoutinePreset.Home, RoutineCalendar.DefaultWakeTime);
        ExpectWindow(home[0], CareMoment.Morning, CareTags.Meal, "07:00", "09:30", "11:00");
        ExpectWindow(home[1], CareMoment.Afternoon, CareTags.Meal, "12:00", "14:00", "15:00");
        ExpectWindow(home[2], CareMoment.Evening, CareTags.Meal, "17:30", "19:30", "20:30");
        ExpectWindow(home[3], CareMoment.Bedtime, CareTags.Bedtime, "19:00", "20:30", "21:30");
        var lateRiser = RoutineCalendar.Windows(RoutinePreset.SchoolDay, TimeSpan.FromHours(9));
        ExpectWindow(lateRiser[0], CareMoment.Morning, CareTags.Meal, "09:00", "12:00", "14:00");
        ExpectWindow(lateRiser[1], CareMoment.Afternoon, CareTags.Meal, "14:00", "17:00", "18:00");
        ExpectArgument(() => RoutineCalendar.Windows(RoutinePreset.SchoolDay, TimeSpan.FromHours(4.5)), "wake time from 05:00");
        ExpectArgument(() => RoutineCalendar.Windows(RoutinePreset.SchoolDay, TimeSpan.FromMinutes(601)), "wake time until 10:00");
        ExpectArgument(() => RoutineCalendar.Windows(RoutinePreset.SchoolDay, new TimeSpan(7, 0, 30)), "wake time in whole minutes");
        ExpectArgument(() => RoutineCalendar.Windows((RoutinePreset)2, RoutineCalendar.DefaultWakeTime), "unknown presets");

        Equal(new DateTime(2026, 3, 1), DateOf(At(2026, 3, 2, 3, 59, Paris)), "03:59 belongs to the previous care day");
        Equal(new DateTime(2026, 3, 2), DateOf(At(2026, 3, 2, 4, 0, Paris)), "care days start at 04:00 local");
        Equal(new DateTime(2026, 3, 2), DateOf(At(2026, 3, 3, 3, 0, Paris)), "the night belongs to the day before");
        Equal(new DateTimeOffset(2026, 3, 2, 3, 0, 0, TimeSpan.Zero).UtcTicks,
            RoutineCalendar.CareDayStart(Day(2026, 3, 2), Paris, Tunables.DayStart), "the local offset places the day start");

        var placed = CareRules.WindowOf(Anchored(At(2026, 3, 2, 6, 0, Paris)), CareMoment.Morning);
        Equal(new DateTimeOffset(2026, 3, 2, 6, 0, 0, TimeSpan.Zero), placed.Open, "the morning opens at 07:00 local");
        Equal(new DateTimeOffset(2026, 3, 2, 11, 0, 0, TimeSpan.Zero), placed.LateUntil, "late morning ends at 12:00 local");
    }

    private static void OnTimeLateAndMissed()
    {
        var care = Anchored(At(2026, 3, 2, 6, 0, Paris));
        care = Care(care, At(2026, 3, 2, 6, 59, Paris), CareTags.Meal);
        Equal(CareReason.MealOutsideWindow, care.LastReason, "a meal before the window does not count");
        Equal(MomentStatus.Pending, care.StatusOf(CareMoment.Morning), "an early meal leaves the morning open");
        care = Care(care, At(2026, 3, 2, 7, 0, Paris), CareTags.Meal);
        Equal(CareReason.MealOnTime, care.LastReason, "a meal at the opening minute is on time");
        Equal("O", care.Meals.ToCode(), "an on-time meal enters the ring");
        care = Care(care, At(2026, 3, 2, 9, 0, Paris), CareTags.Meal);
        Equal(CareReason.MealOutsideWindow, care.LastReason, "a second morning meal finds no open window");
        Equal("O", care.Meals.ToCode(), "a second meal is not counted twice");
        care = Care(care, At(2026, 3, 2, 11, 0, Paris), CareTags.Together);
        Equal(CareReason.TogetherCounted, care.LastReason, "together time counts once a day");
        care = Care(care, At(2026, 3, 2, 12, 0, Paris), CareTags.Together);
        Equal(CareReason.TogetherAlreadyCounted, care.LastReason, "more together time the same day is not a new entry");
        Equal("O", care.TogetherDays.ToCode(), "one together entry for the day");
        care = Care(care, At(2026, 3, 2, 17, 0, Paris), CareTags.Meal);
        Equal(CareReason.MealLate, care.LastReason, "a meal at the due minute is late");
        Equal(MomentStatus.Late, care.StatusOf(CareMoment.Afternoon), "the afternoon is late");
        care = Care(care, At(2026, 3, 2, 18, 30, Paris), CareTags.Meal, 95);
        Equal(CareReason.MealTooFull, care.LastReason, "feeding at Fullness 90 or more is not a meal");
        Equal(MomentStatus.Pending, care.StatusOf(CareMoment.Evening), "an overfull feed leaves the evening open");
        care = Care(care, At(2026, 3, 2, 21, 0, Paris), CareTags.Bedtime);
        Equal(CareReason.BedtimeOnTime, care.LastReason, "rest in the bedtime window is on time");
        care = CareRules.Advance(care, At(2026, 3, 2, 21, 29, Paris), Tunables);
        Equal(MomentStatus.Pending, care.StatusOf(CareMoment.Evening), "the evening stays open until its late limit");
        care = CareRules.Advance(care, At(2026, 3, 2, 21, 30, Paris), Tunables);
        Equal(MomentStatus.Missed, care.StatusOf(CareMoment.Evening), "the evening is missed at its late limit");
        Equal(CareReason.WindowMissed, care.LastReason, "the diagnostic names the miss");
        Equal("OLM", care.Meals.ToCode(), "on time, late and missed meals in order");
        Equal("O", care.Bedtimes.ToCode(), "one bedtime entry");

        care = CareRules.Advance(care, At(2026, 3, 3, 5, 0, Paris), Tunables);
        Equal(Day(2026, 3, 3), care.CareDay, "the care day rolls over at 04:00");
        Equal("PPPP", care.MomentCode, "a new day starts with every moment pending");
        Equal(false, care.TogetherToday, "together time resets each day");
        care = Care(care, At(2026, 3, 3, 15, 0, Paris), CareTags.Meal);
        care = CareRules.Advance(care, At(2026, 3, 4, 4, 30, Paris), Tunables);
        Equal("OLMMOM", care.Meals.ToCode(), "a skipped day records its missed meals");
        Equal("OM", care.Bedtimes.ToCode(), "a skipped bedtime is missed");
        Equal("OM", care.TogetherDays.ToCode(), "a day without together time is missed");
        Equal(0.6 * (5.5 / 9) + 0.25 * (2.0 / 3) + 0.15 * (2.0 / 3), CareRules.Score(care, Tunables),
            "CareScore weighs the three rings, unfilled slots counting as on time");
        Equal(1.0, CareRules.Score(Anchored(At(2026, 3, 2, 6, 0, Paris)), Tunables), "empty rings count as 1");

        var bedtime = Anchored(At(2026, 3, 2, 6, 0, Paris));
        Equal(CareReason.BedtimeOutsideWindow, Care(bedtime, At(2026, 3, 2, 15, 0, Paris), CareTags.Bedtime).LastReason,
            "an afternoon nap is not bedtime");
        var lateBed = Care(bedtime, At(2026, 3, 2, 22, 0, Paris), CareTags.Bedtime);
        Equal(CareReason.BedtimeLate, lateBed.LastReason, "bedtime within 60 minutes of the window is late");
        Equal("L", lateBed.Bedtimes.ToCode(), "a late bedtime enters the ring");
        Equal(CareReason.WashOnTime, Care(bedtime, At(2026, 3, 2, 8, 0, Paris), CareTags.Hygiene).LastReason,
            "a morning wash is on time");
        Equal(CareReason.WashOutsideWindow, Care(bedtime, At(2026, 3, 2, 6, 30, Paris), CareTags.Hygiene).LastReason,
            "a wash before waking is outside the window");
        Equal(CareReason.NoRoutineTag, Care(bedtime, At(2026, 3, 2, 8, 0, Paris), CareTags.Medicine).LastReason,
            "medicine has no window in this slice");

        var home = CareRules.ChangeRoutine(Anchored(At(2026, 3, 2, 6, 0, Paris)), RoutinePreset.Home, TimeSpan.FromMinutes(510));
        Equal(RoutinePreset.SchoolDay, home.Preset, "a routine change waits for the next care day");
        Equal(CareReason.MealOnTime, Care(home, At(2026, 3, 2, 9, 45, Paris), CareTags.Meal).LastReason,
            "today keeps the school-day morning");
        home = CareRules.Advance(home, At(2026, 3, 3, 6, 0, Paris), Tunables);
        Equal(RoutinePreset.Home, home.Preset, "the home preset applies from the next care day");
        Equal(TimeSpan.FromMinutes(510), home.WakeTime, "the wake time applies from the next care day");
        Equal(At(2026, 3, 3, 8, 30, Paris).UtcTicks, CareRules.WindowOf(home, CareMoment.Morning).Open.UtcTicks,
            "the morning opens at the wake time");
        Equal(CareReason.MealLate, Care(home, At(2026, 3, 3, 11, 30, Paris), CareTags.Meal).LastReason,
            "the home morning is due 2.5 hours after waking");
        Equal(CareReason.MealOnTime, Care(home, At(2026, 3, 3, 12, 30, Paris), CareTags.Meal).LastReason,
            "the home lunch is on time at midday");
        ExpectArgument(() => CareRules.ChangeRoutine(home, RoutinePreset.Home, TimeSpan.FromHours(11)), "routine wake times are validated");
    }

    private static void FirstDayIsExcused()
    {
        var game = Game();
        var baby = game.Adopt("Mia", At(2026, 3, 1, 3, 0, Paris));
        Equal(CareReason.Started, baby.Care.LastReason, "adoption starts the ledger");
        Equal(Day(2026, 2, 28), baby.Care.CareDay, "03:00 adoption belongs to the previous care day");
        baby = game.ApplyCare(game.Advance(baby, At(2026, 3, 1, 8, 0, Paris)).State, CareAction.Feed).State;
        Equal(CareReason.MealExcused, baby.Care.LastReason, "the first 24 hours are excused");
        baby = game.Advance(baby, At(2026, 3, 2, 8, 0, Paris)).State;
        Equal("", baby.Care.Meals.ToCode(), "excused windows never enter the rings");
        Equal("", baby.Care.TogetherDays.ToCode(), "an excused day without together time is not missed");
        Equal(true, baby.Care.ExcusedCount >= 8, "the first care days are counted as excused");
        baby = game.ApplyCare(baby, CareAction.Feed).State;
        Equal(CareReason.MealOnTime, baby.Care.LastReason, "windows after the grace period count");

        var session = new BabySession(game, new MemorySaveStore(), () => At(2026, 3, 1, 3, 0, Paris));
        Equal("", session.CareSummary, "no read-out before adoption");
        session.Adopt("Mia");
        Equal(true, session.CareSummary.StartsWith("Care day 2026-02-28 UTC+01:00 | today EEEE"), "the read-out names the care day");
        Equal(true, session.CareSummary.EndsWith("ledger started, first day excused"), "the read-out names the last reason");
    }

    private static void DaylightSavingShifts()
    {
        // Central Europe springs forward at 02:00 local on 29 March 2026.
        var spring = CareRules.ApplyCare(Anchored(At(2026, 3, 28, 22, 40, Paris)), CareTags.Together, 50, Tunables);
        var summer = TimeSpan.FromHours(2);
        spring = CareRules.Advance(spring, At(2026, 3, 29, 3, 30, summer), Tunables);
        Equal(CareReason.OffsetShiftScheduled, spring.LastReason, "a one-hour shift is not travel");
        Equal(Paris, spring.Offset, "the current care day keeps its offset");
        Equal(summer, spring.NextOffset, "the new offset waits for the next care day");
        Equal(false, spring.DayExcused, "daylight saving excuses nothing");
        var excusedBefore = spring.ExcusedCount;
        spring = CareRules.Advance(spring, At(2026, 3, 29, 10, 30, summer), Tunables);
        Equal(Day(2026, 3, 29), spring.CareDay, "the next care day starts at 04:00 summer time");
        Equal(summer, spring.Offset, "summer time places the new day");
        Equal(excusedBefore, spring.ExcusedCount, "the shift itself excuses no window");
        spring = CareRules.ApplyCare(spring, CareTags.Meal, 50, Tunables);
        Equal(CareReason.MealLate, spring.LastReason, "10:30 summer time is past the 10:00 due time");

        // And falls back at 03:00 local on 25 October 2026.
        var autumn = Anchored(At(2026, 10, 24, 22, 40, summer));
        autumn = CareRules.Advance(autumn, At(2026, 10, 25, 2, 30, Paris), Tunables);
        Equal(CareReason.OffsetShiftScheduled, autumn.LastReason, "falling back is scheduled too");
        Equal(Day(2026, 10, 24), autumn.CareDay, "the extra hour stays in the old care day");
        autumn = CareRules.Advance(autumn, At(2026, 10, 25, 6, 30, Paris), Tunables);
        Equal(Day(2026, 10, 25), autumn.CareDay, "the next care day begins");
        Equal(CareReason.MealOutsideWindow, CareRules.ApplyCare(autumn, CareTags.Meal, 50, Tunables).LastReason,
            "06:30 winter time is before the morning window");
        Equal(CareReason.MealOnTime, Care(autumn, At(2026, 10, 25, 7, 15, Paris), CareTags.Meal).LastReason,
            "07:15 winter time is on time");
    }

    private static void Travel()
    {
        var newYork = TimeSpan.FromHours(-5);
        var trip = Care(Anchored(At(2026, 6, 10, 6, 0, TimeSpan.Zero)), At(2026, 6, 10, 8, 0, TimeSpan.Zero), CareTags.Meal);
        trip = CareRules.Advance(trip, At(2026, 6, 10, 14, 0, newYork), Tunables);
        Equal(CareReason.TravelDayExcused, trip.LastReason, "a five-hour offset change is travel");
        Equal(true, trip.DayExcused, "the travel day is excused");
        Equal(MomentStatus.Excused, trip.StatusOf(CareMoment.Afternoon), "windows that closed in flight are excused");
        trip = CareRules.ApplyCare(trip, CareTags.Meal, 50, Tunables);
        Equal(CareReason.MealExcused, trip.LastReason, "care on the travel day is excused too");
        trip = Care(trip, At(2026, 6, 11, 8, 0, newYork), CareTags.Meal);
        Equal(Day(2026, 6, 11), trip.CareDay, "the next care day follows the local date");
        Equal(newYork, trip.Offset, "the new offset applies from the next care day");
        Equal(false, trip.DayExcused, "only the travel day is excused");
        Equal(CareReason.MealOnTime, trip.LastReason, "the routine resumes on local time");
        Equal("OO", trip.Meals.ToCode(), "travel adds no misses");
        Equal("", trip.TogetherDays.ToCode(), "the travel day's together time is excused");

        // Flying west over the date line repeats a calendar date; the care day never decreases.
        var tokyo = TimeSpan.FromHours(9);
        var honolulu = TimeSpan.FromHours(-10);
        var west = Anchored(At(2026, 6, 10, 10, 0, tokyo));
        west = CareRules.Advance(west, At(2026, 6, 9, 22, 0, honolulu), Tunables);
        Equal(Day(2026, 6, 10), west.CareDay, "arriving on an earlier local date keeps the care day");
        west = CareRules.Advance(west, At(2026, 6, 10, 20, 0, honolulu), Tunables);
        Equal(Day(2026, 6, 10), west.CareDay, "the repeated date is the same excused care day");
        Equal(true, west.DayExcused, "the whole repeated date is excused");
        west = CareRules.Advance(west, At(2026, 6, 11, 8, 0, honolulu), Tunables);
        Equal(Day(2026, 6, 11), west.CareDay, "the next local date is the next care day");
        Equal(false, west.DayExcused, "the day after travel counts");
        Equal("", west.Meals.ToCode(), "flying west adds no misses");

        // Flying east can skip a whole local day. The ordinal jumps forward and only the arrival day is
        // excused; the day spent in flight counts like any absence of the same length.
        var east = Anchored(At(2026, 6, 10, 18, 0, newYork));
        east = CareRules.Advance(east, At(2026, 6, 12, 8, 0, tokyo), Tunables);
        Equal(Day(2026, 6, 12), east.CareDay, "arriving on a later date jumps to it");
        Equal(true, east.DayExcused, "the arrival day is excused");
        Equal(CareReason.TravelDayExcused, east.LastReason, "the diagnostic names the travel day");
        Equal("MMM/MM/", east.Meals.ToCode() + "/" + east.Bedtimes.ToCode() + "/" + east.TogetherDays.ToCode(),
            "the day spent in flight counts like any absence");
        east = Care(east, At(2026, 6, 13, 8, 0, tokyo), CareTags.Meal);
        Equal(CareReason.MealOnTime, east.LastReason, "the routine resumes on Tokyo time");
        Equal(false, east.DayExcused, "the day after arrival counts");

        // A zone change seen only on return excuses the day it lands on, never the absence before it.
        var moscow = TimeSpan.FromHours(3);
        var fed = Care(Anchored(At(2026, 3, 2, 6, 0, Paris)), At(2026, 3, 2, 8, 0, Paris), CareTags.Meal);
        var home = CareRules.Advance(fed, At(2026, 3, 3, 19, 0, Paris), Tunables);
        Equal("OMMMM/M/", Rings(home), "staying home misses the absence");
        var moved = CareRules.Advance(fed, At(2026, 3, 3, 19, 0, Paris).ToOffset(moscow), Tunables);
        Equal("OMM/M/", Rings(moved), "changing the zone before reopening keeps the earlier misses");
        Equal(true, moved.DayExcused, "the day the change lands on is excused");

        // Split advances across a travel jump equal one advance when the jump lands on a later care day.
        var single = CareRules.Advance(fed, At(2026, 3, 3, 21, 0, moscow), Tunables);
        var split = CareRules.Advance(CareRules.Advance(fed, At(2026, 3, 2, 12, 0, Paris), Tunables),
            At(2026, 3, 3, 21, 0, moscow), Tunables);
        Equal(single, split, "a refresh before the jump changes nothing");
    }

    private static string Rings(CareRecord care) =>
        care.Meals.ToCode() + "/" + care.Bedtimes.ToCode() + "/" + care.TogetherDays.ToCode();

    private static void ClockFollowsZoneChanges()
    {
        // The process caches the local zone, so a flight with the app in the background must still show up.
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var previous = Environment.GetEnvironmentVariable("TZ");
            try
            {
                var clock = new LocalClock(TimeSpan.Zero);
                Environment.SetEnvironmentVariable("TZ", "Asia/Tokyo");
                Equal(TimeSpan.FromHours(9), clock.Now().Offset, "the clock reads the local zone");
                Environment.SetEnvironmentVariable("TZ", "Asia/Kolkata");
                var later = clock.Now();
                Equal(TimeSpan.FromHours(5.5), later.Offset, "a zone change between two reads changes the offset");
                Equal(true, Math.Abs((later.UtcDateTime - DateTime.UtcNow).TotalMinutes) < 1, "the instant stays the UTC instant");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TZ", previous);
                TimeZoneInfo.ClearCachedData();
            }
        }

        // Dropping the cached zone is throttled by running time, but a wall-clock jump (a resume after
        // sleep) drops it at once.
        var utc = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var running = TimeSpan.Zero;
        var throttled = new LocalClock(TimeSpan.FromSeconds(10), () => utc, () => running);
        throttled.Now();
        Equal(1, throttled.Refreshes, "the first read drops the cached zone");
        utc = utc.AddSeconds(5);
        running += TimeSpan.FromSeconds(5);
        throttled.Now();
        Equal(1, throttled.Refreshes, "frequent reads reuse the zone");
        utc = utc.AddSeconds(5);
        running += TimeSpan.FromSeconds(5);
        throttled.Now();
        Equal(2, throttled.Refreshes, "the zone is read again after the interval");
        utc = utc.AddHours(9);
        running += TimeSpan.FromSeconds(1);
        Equal(utc, throttled.Now().UtcDateTime, "the clock returns the UTC instant");
        Equal(3, throttled.Refreshes, "a resume after a long sleep reads the zone again");
        ExpectArgument(() => new LocalClock(TimeSpan.FromSeconds(-1)), "a negative refresh interval");
    }

    private static void BackwardsClock()
    {
        var care = CareRules.Advance(Anchored(At(2026, 3, 2, 6, 30, Paris)), At(2026, 3, 2, 8, 0, Paris), Tunables);
        Equal(true, ReferenceEquals(care, CareRules.Advance(care, At(2026, 3, 2, 3, 0, Paris), Tunables)),
            "a backwards clock changes nothing");
        Equal(true, ReferenceEquals(care, CareRules.Advance(care, At(2026, 3, 1, 8, 0, Paris), Tunables)),
            "a clock set a day back keeps the care day");
        Equal(true, ReferenceEquals(care, CareRules.Advance(care, care.Checkpoint.ToOffset(TimeSpan.FromHours(8)), Tunables)),
            "the same instant in a new offset changes nothing");

        var game = Game();
        var baby = BabyState.Restore("Mia", At(2026, 3, 2, 8, 0, Paris), TimeSpan.FromDays(2), 50, 60, 70, 0, 0, false, false,
            0, 0, 100, 100, false, care: care);
        var rewound = game.Advance(baby, At(2026, 3, 2, 5, 0, Paris));
        Equal(true, ReferenceEquals(baby, rewound.State), "a rewound clock leaves the baby untouched");
        var fed = game.ApplyCare(rewound.State, CareAction.Feed).State;
        Equal(CareReason.MealOnTime, fed.Care.LastReason, "care after a rewind uses the latest instant");
        fed = game.Advance(fed, At(2026, 3, 2, 9, 0, Paris)).State;
        Equal("O", fed.Care.Meals.ToCode(), "moving forward again counts nothing twice");
    }

    private static void LongAbsence()
    {
        var day = Anchored(At(2026, 3, 2, 6, 0, Paris));
        day = Care(day, At(2026, 3, 2, 8, 0, Paris), CareTags.Meal);
        day = Care(day, At(2026, 3, 2, 11, 0, Paris), CareTags.Together);
        day = Care(day, At(2026, 3, 2, 15, 0, Paris), CareTags.Meal);
        day = Care(day, At(2026, 3, 2, 18, 30, Paris), CareTags.Meal);
        day = Care(day, At(2026, 3, 2, 21, 0, Paris), CareTags.Bedtime);

        var thirtySeven = CareRules.Advance(day, day.LastCareAt.ToOffset(Paris).AddHours(37), Tunables);
        Equal("OOOMMM", thirtySeven.Meals.ToCode(), "the first 36 hours of absence use the full rules");
        Equal("OM", thirtySeven.Bedtimes.ToCode(), "a bedtime inside the ceiling is missed");
        Equal("OM", thirtySeven.TogetherDays.ToCode(), "a day inside the ceiling without together time is missed");

        var yearLater = At(2027, 3, 2, 10, 0, Paris);
        var year = CareRules.Advance(day, yearLater, Tunables);
        Equal(thirtySeven.Meals, year.Meals, "a one-year absence counts like 37 hours");
        Equal(thirtySeven.Bedtimes, year.Bedtimes, "a long absence adds no further missed bedtimes");
        Equal(thirtySeven.TogetherDays, year.TogetherDays, "a long absence adds no further missed together days");
        Equal(CareReason.AbsenceCovered, year.LastReason, "the diagnostic names the covered absence");
        Equal(Day(2027, 3, 2), year.CareDay, "the care day follows the clock after an absence");
        Equal(true, year.CoveredCount > 365 * 4, "every window past the ceiling is covered");
        Equal(true, year.Equals(CareRules.Advance(thirtySeven, yearLater, Tunables)),
            "split advances equal one advance");

        var farFuture = CareRules.Advance(day, new DateTimeOffset(9999, 12, 30, 12, 0, 0, Paris), Tunables);
        Equal("OOOMMM", farFuture.Meals.ToCode(), "a clock jump to the far future is only an absence");
        Equal("PPPP", CareRules.Advance(day, yearLater.AddHours(-4), Tunables).MomentCode,
            "the returning day's windows are still open");

        var game = Game();
        var baby = BabyState.Restore("Mia", day.Checkpoint, TimeSpan.FromDays(2), 50, 60, 70, 0, 0, false, false,
            0, 0, 100, 100, false, care: day);
        var returned = game.Advance(baby, yearLater).State;
        var ledgerOnly = returned.Care.ToBuilder();
        ledgerOnly.Mood = year.Mood;
        ledgerOnly.MoodCause = year.MoodCause;
        ledgerOnly.MoodSince = year.MoodSince;
        Equal(year, ledgerOnly.Build(), "the simulation advances the ledger with the clock");
        Equal(CareMood.Fussy, returned.Care.Mood, "a return moves the baby one rung, to Fussy");
        Equal(true, ReferenceEquals(returned.Care, game.AdvancePreview(returned, TimeSpan.FromDays(3)).State.Care),
            "preview days pass no real time and leave the ledger alone");
    }

    private static void MigrationWithoutSurprise()
    {
        var game = Game();
        var checkpoint = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        var v12 = XElement.Parse(BabySaveCodec.Encode(BabyState.Restore("Sam", checkpoint, TimeSpan.FromDays(5), 41, 52, 63,
            2, 3, true, false, 34, 8, 51, 74, true, NurseryTheme.Peach)));
        v12.Element("care").Remove();
        v12.SetAttributeValue("version", 11);
        var v11 = v12.ToString(SaveOptions.DisableFormatting);
        var decoded = BabySaveCodec.Decode(v11);
        Equal(true, decoded.Care.AwaitingAnchor, "a v11 baby gets an unanchored ledger");
        Equal(checkpoint.UtcTicks, decoded.Care.Checkpoint.UtcTicks, "the ledger shares the baby's checkpoint");

        const string v1 = "<baby version=\"1\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
            "<age>2592000000000</age><satiety>41</satiety><comfort>52</comfort>" +
            "<toy-familiarity>2</toy-familiarity><can-sit-and-reach>true</can-sit-and-reach></baby>";
        Equal(true, BabySaveCodec.Decode(v1).Care.AwaitingAnchor, "a v1 baby gets an unanchored ledger");

        var summer = TimeSpan.FromHours(2);
        var clock = At(2026, 3, 5, 11, 0, summer);
        var saves = new MemorySaveStore { Primary = v11 };
        var session = new BabySession(game, saves, () => clock);
        session.Refresh();
        var care = session.State.Care;
        Equal(false, care.AwaitingAnchor, "the first advance anchors the ledger");
        Equal(CareReason.GapExcused, care.LastReason, "the first advance excuses the whole gap");
        Equal("", care.Meals.ToCode() + care.TogetherDays.ToCode() + care.Bedtimes.ToCode(), "no migrated baby starts with misses");
        Equal(1.0, CareRules.Score(care, Tunables), "a migrated baby starts with a full CareScore");
        Equal(Day(2026, 3, 5), care.CareDay, "the care day follows the player's local date");
        Equal(summer, care.Offset, "the ledger takes the player's offset");
        Equal(true, saves.Primary.Contains("version=\"13\"") && saves.Primary.Contains("<care v=\"2\""), "the migration saves v13");
        Equal(care, new BabySession(game, saves, () => clock).State.Care, "the anchored ledger survives a reload");

        session.ApplyCare(CareAction.Feed);
        Equal(CareReason.MealExcused, session.State.Care.LastReason, "a window already late at migration is excused, not late");
        clock = At(2026, 3, 5, 15, 0, summer);
        session.ApplyCare(CareAction.Feed);
        Equal(CareReason.MealOnTime, session.State.Care.LastReason, "windows after the migration count");
    }

    private static void RoundTrip()
    {
        var care = Anchored(At(2026, 3, 2, 6, 0, Paris));
        care = Care(care, At(2026, 3, 2, 11, 0, Paris), CareTags.Meal);
        care = Care(care, At(2026, 3, 2, 15, 0, Paris), CareTags.Together);
        care = CareRules.ChangeRoutine(care, RoutinePreset.Home, TimeSpan.FromMinutes(345));
        care = CareRules.Advance(care, At(2026, 3, 2, 21, 0, TimeSpan.FromHours(5.5)), Tunables);
        care = CareRules.ApplyCare(care, CareTags.Meal, 50, Tunables);
        Equal(true, care.DayExcused && care.NextOffset != care.Offset && care.Meals.Count == 1 && care.ExcusedCount > 0,
            "the round-trip fixture uses every kind of field");

        var baby = BabyState.Restore("Mia", care.Checkpoint, TimeSpan.FromDays(4), 41, 52, 63, 2, 3, true, false, 34, 8, 51,
            74, true, NurseryTheme.Peach, care: care);
        var payload = BabySaveCodec.Encode(baby);
        var decoded = BabySaveCodec.Decode(payload);
        Equal(care, decoded.Care, "the care record survives a save round trip");
        Equal(payload, BabySaveCodec.Encode(decoded), "re-encoding is stable");
        Equal(CareReason.MealExcused, decoded.Care.LastReason, "the diagnostic reason round-trips");

        var unanchored = BabySaveCodec.Decode(BabySaveCodec.Encode(BabyState.Restore("Mia", care.Checkpoint, TimeSpan.Zero,
            70, 75, 80, 0, 0, false, false, 0, 0, 100, 100, false)));
        Equal(true, unanchored.Care.AwaitingAnchor, "an unanchored ledger round-trips");

        var saves = new MemorySaveStore();
        var clock = At(2026, 3, 2, 6, 0, Paris);
        var session = new BabySession(Game(), saves, () => clock);
        session.Adopt("Mia");
        clock = At(2026, 3, 3, 8, 0, Paris);
        session.ApplyCare(CareAction.Feed);
        Equal(session.State.Care, new BabySession(Game(), saves, () => clock).State.Care, "the session saves the ledger");

        ExpectArgument(() => BabyState.Restore("Mia", care.Checkpoint.AddTicks(1), TimeSpan.Zero, 70, 75, 80, 0, 0, false, false,
            0, 0, 100, 100, false, care: care), "the ledger must share the baby's checkpoint");
    }

    private static void InvalidCareIsRejected()
    {
        var care = Care(Anchored(At(2026, 3, 2, 6, 0, Paris)), At(2026, 3, 2, 8, 0, Paris), CareTags.Meal);
        var valid = XElement.Parse(BabySaveCodec.Encode(BabyState.Restore("Mia", care.Checkpoint, TimeSpan.FromDays(4),
            41, 52, 63, 2, 3, true, false, 34, 8, 51, 74, true, care: care)));
        BabySaveCodec.Decode(valid.ToString(SaveOptions.DisableFormatting));

        var missing = new XElement(valid);
        missing.Element("care").Remove();
        ExpectInvalid(missing, "a v12 save without a care record");
        foreach (var attribute in valid.Element("care").Attributes())
        {
            var without = new XElement(valid);
            without.Element("care").Attribute(attribute.Name).Remove();
            ExpectInvalid(without, $"a care record without {attribute.Name}");
        }

        var dayValue = (long)valid.Element("care").Attribute("day");
        var checkpoint = (long)valid.Element("care").Attribute("checkpoint");
        foreach (var (attribute, value) in new[]
        {
            ("v", "0"), ("v", "one"), ("checkpoint", (checkpoint + 1).ToString()), ("checkpoint", "abc"),
            ("checkpoint", "99999999999999999999"), ("offset", "841"), ("next-offset", "-900"), ("day", "-1"),
            ("day", (dayValue + 10).ToString()), ("preset", "2"), ("next-preset", "-1"), ("wake", "299"),
            ("next-wake", "601"), ("moments", "PPP"), ("moments", "PPPX"), ("together-today", "maybe"),
            ("excused-until", "-5"), ("last-care", (checkpoint + 1).ToString()), ("meals", "OOX"),
            ("meals", new string('O', CareRing.MaxLength + 1)), ("together", "o"), ("bedtimes", "P"),
            ("excused", "-1"), ("covered", "-5"), ("reason", "999"), ("reason", "-1"),
            ("wash", "6"), ("mess", "4"), ("mess", "-1"), ("mood", "3"), ("mood-cause", "99"),
            ("mood-since", (checkpoint + 1).ToString()), ("owed", "4"), ("style", "3"),
            ("away-from", (checkpoint + 1).ToString()), ("rest", "3"), ("rest", "-1"), ("rest-day", "-1"),
            ("away-until", (checkpoint + TimeSpan.FromDays(15).Ticks).ToString()),
        })
        {
            var corrupt = new XElement(valid);
            corrupt.Element("care").SetAttributeValue(attribute, value);
            ExpectInvalid(corrupt, $"care {attribute}=\"{value}\"");
        }

        var renamed = new XElement(valid);
        renamed.Element("care").Name = "caring";
        ExpectInvalid(renamed, "a renamed care element");
        try
        {
            CareRecordCodec.Decode(null);
            throw new InvalidOperationException("A missing care element should be rejected.");
        }
        catch (InvalidDataException)
        {
        }

        var saves = new MemorySaveStore();
        var session = new BabySession(Game(), saves, () => care.Checkpoint);
        session.Adopt("Mia");
        session.ApplyCare(CareAction.Feed);
        var corruptCare = XElement.Parse(saves.Primary);
        corruptCare.Element("care").SetAttributeValue("moments", "ZZZZ");
        saves.Primary = corruptCare.ToString(SaveOptions.DisableFormatting);
        var recovered = new BabySession(Game(), saves, () => care.Checkpoint);
        Equal("Mia", recovered.State.Name, "invalid care data falls back to the backup save");

        // A newer build may write a newer care record inside a baby save this build reads.
        var future = new XElement(valid);
        future.Element("care").SetAttributeValue("v", "3");
        try
        {
            BabySaveCodec.Decode(future.ToString(SaveOptions.DisableFormatting));
            throw new InvalidOperationException("A newer care record should be unsupported, not corrupt.");
        }
        catch (NotSupportedException)
        {
        }

        session = new BabySession(Game(), saves, () => care.Checkpoint);
        session.ApplyCare(CareAction.Feed);
        var newer = XElement.Parse(saves.Primary);
        newer.Element("care").SetAttributeValue("v", "3");
        saves.Primary = newer.ToString(SaveOptions.DisableFormatting);
        var backupBefore = saves.Backup;
        try
        {
            new BabySession(Game(), saves, () => care.Checkpoint.AddHours(1));
            throw new InvalidOperationException("A save with a newer care record should not open.");
        }
        catch (NotSupportedException)
        {
        }

        Equal(newer.ToString(SaveOptions.DisableFormatting), saves.Primary, "a save with a newer care record is left untouched");
        Equal(backupBefore, saves.Backup, "its backup is left untouched too");
    }

    // Slice 2 retuned the needs, so the slice 1 bit-for-bit match with the pre-care simulation is gone. What
    // stays: on a well-cared day, whether windows count (anchored) or wait for an anchor (migrated) changes no
    // needs and no growth, the day ends content, and an excused travel day records no misses.
    private static void ShadowGuarantee()
    {
        var game = Game();
        var day = At(2026, 3, 2, 6, 0, Paris);
        var unanchored = BabyState.Restore("Mia", day, TimeSpan.FromDays(6.5), 55, 60, 70, 1, 0, true, false, 12, 2, 80, 90, false);
        var counted = unanchored.With(care: Anchored(day));
        var countedFingerprint = ShadowFingerprint(game, counted, day, out var ledger);
        Equal("OOOO", ledger.Meals.ToCode(), "the scripted day's meals are on time");
        Equal("O", ledger.Bedtimes.ToCode(), "the scripted bedtime is on time");
        Equal("OO", ledger.TogetherDays.ToCode(), "both scripted days have together time");
        Equal(CareMood.Content, ledger.Mood, "a well-cared day ends content");
        Equal(0, ledger.Mess, "a washed day leaves no mess");
        Equal(countedFingerprint, ShadowFingerprint(game, unanchored, day, out _),
            "a migrated baby's needs and growth match a counted day's");

        var travelling = unanchored.With(care: CareRules.Advance(Anchored(day.AddHours(-1)), day.ToOffset(TimeSpan.FromHours(-6)), Tunables));
        Equal(true, travelling.Care.DayExcused, "the third run starts on an excused travel day");
        // Night follows the local clock, so a traveller's needs differ; the excused day records no misses.
        ShadowFingerprint(game, travelling, day, out var travelLedger);
        Equal(false, (travelLedger.Meals.ToCode() + travelLedger.Bedtimes.ToCode()).Contains("M"),
            "an excused travel day records no misses");
    }

    // Everything except the care ledger, hashed after every step of a scripted day of care.
    private static string ShadowFingerprint(BabySimulation game, BabyState start, DateTimeOffset day, out CareRecord ledger)
    {
        var steps = new (double Hour, CareAction? Action)[]
        {
            (1.5, CareAction.Feed), (2, CareAction.ChangeDiaper), (3, CareAction.Play), (5, CareAction.Soothe),
            (7, CareAction.Rest), (9, CareAction.Feed), (10, CareAction.ReadStory), (11, CareAction.ChangeDiaper),
            (11.5, CareAction.Feed),
            (12.5, CareAction.Feed), (13.75, CareAction.Bath), (14, CareAction.Rest), (25, CareAction.Feed),
            (26, CareAction.Play),
        };
        var trace = new StringBuilder();
        var state = start;
        foreach (var (hour, action) in steps)
        {
            var advanced = game.Advance(state, day.AddHours(hour));
            state = advanced.State;
            trace.Append(advanced.UnlockedSitting).Append(advanced.UnlockedCrawling).Append(advanced.UnlockedWalking);
            if (action.HasValue)
            {
                var cared = game.ApplyCare(state, action.Value);
                state = cared.State;
                trace.Append(cared.Reaction);
            }

            foreach (var value in new[] { state.Satiety, state.Comfort, state.Energy, state.Cleanliness, state.Health, state.Connection })
                trace.Append(BitConverter.DoubleToInt64Bits(value).ToString("X16")).Append(',');

            trace.Append(state.TimeCheckpoint.UtcTicks).Append(',').Append(state.Age.Ticks).Append(',')
                .Append(state.ToyFamiliarity).Append(',').Append(state.PlushFamiliarity).Append(',')
                .Append(state.StorySessions).Append(',').Append(state.BlockTowers).Append(',')
                .Append(state.CanSitAndReach).Append(state.CanCrawl).Append(state.CanWalk).Append(state.NewSoundResolved)
                .Append(state.NurseryTheme).Append(';');
        }

        ledger = state.Care;
        using (var sha = SHA256.Create())
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(trace.ToString())));
    }

    private static BabySimulation Game() =>
        new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7), TimeSpan.FromDays(14));

    // A ledger past its first-day grace, so windows from `at` onwards count.
    private static CareRecord Anchored(DateTimeOffset at)
    {
        var draft = CareRules.Start(at, Tunables).ToBuilder();
        draft.ExcusedUntil = at;
        return draft.Build();
    }

    private static CareRecord Care(CareRecord care, DateTimeOffset at, CareTags tags, double fullness = 50) =>
        CareRules.ApplyCare(CareRules.Advance(care, at, Tunables), tags, fullness, Tunables);

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute, TimeSpan offset) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, offset);

    private static long Day(int year, int month, int day) => new DateTime(year, month, day).Ticks / TimeSpan.TicksPerDay;

    private static DateTime DateOf(DateTimeOffset at) =>
        RoutineCalendar.CalendarDate(RoutineCalendar.CareDayOf(at.UtcTicks, at.Offset, Tunables.DayStart));

    private static void ExpectWindow(CareWindow window, CareMoment moment, CareTags core, string open, string due, string lateUntil)
    {
        Equal(moment, window.Moment, $"{moment} window order");
        Equal(core, window.Core, $"{moment} core tag");
        Equal(TimeSpan.Parse(open), window.Open, $"{moment} opens at {open}");
        Equal(TimeSpan.Parse(due), window.Due, $"{moment} is due at {due}");
        Equal(TimeSpan.Parse(lateUntil), window.LateUntil, $"{moment} is late until {lateUntil}");
    }

    private static void ExpectInvalid(XElement payload, string description)
    {
        try
        {
            BabySaveCodec.Decode(payload.ToString(SaveOptions.DisableFormatting));
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected InvalidDataException");
    }

    private static void ExpectArgument(Action attempt, string description)
    {
        try
        {
            attempt();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected ArgumentException");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }
}
