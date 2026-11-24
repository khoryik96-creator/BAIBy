using System;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

// Care slice 2: need retune and offline cap, Fussy and Upset, mess, overfeeding, Away mode, care styles,
// the routine strip and the save v13 migration. See docs/care-and-growth.md.
static class ConsequenceChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly TimeSpan Paris = TimeSpan.FromHours(1);

    public static void Run()
    {
        NeedRetuneAndCap();
        NightAndBedtime();
        ClockAndRoutineChangesAtTheDayBoundary();
        OnTimeLateAndMissed();
        FussyAndUpset();
        OneRungPerAdvance();
        Mess();
        Overfeeding();
        AwayMode();
        AwayHoldsTheLadder();
        MissedMealsUpset();
        GentleIsGentler();
        RoutineStripStates();
        Migration();
        ReviewFixes();
        Console.WriteLine("Care consequence checks passed.");
    }

    private static void NeedRetuneAndCap()
    {
        var game = Game();
        Equal(TimeSpan.FromHours(8), BabySimulation.DefaultMaxNeedDecay, "the offline decay cap is 8 hours");
        var morning = At(2026, 3, 2, 8, 0);
        var baby = game.Adopt("Mia", morning);
        var hour = game.Advance(baby, morning.AddHours(1)).State;
        Near(baby.Satiety - 7, hour.Satiety, "Fullness falls 7 an awake hour");
        Near(baby.Comfort - 4, hour.Comfort, "Comfort falls 4 an awake hour");
        Near(baby.Energy - 6, hour.Energy, "Energy falls 6 an awake hour");
        Near(baby.Cleanliness - 9, hour.Cleanliness, "Cleanliness falls 9 an awake hour");

        var capped = game.Advance(baby, morning.AddHours(8)).State;
        var days = game.Advance(baby, morning.AddDays(3)).State;
        Near(capped.Satiety, days.Satiety, "three days away decay Fullness like 8 hours");
        Near(capped.Comfort, days.Comfort, "three days away decay Comfort like 8 hours");
        Near(capped.Energy, days.Energy, "three days away decay Energy like 8 hours");
        Near(capped.Cleanliness, days.Cleanliness, "three days away decay Cleanliness like 8 hours");
        Near(capped.Health, days.Health, "three days away change Health like 8 hours");
        Equal(TimeSpan.FromDays(3), days.Age, "development keeps its nominal pace through an absence");

        var fed = game.ApplyCare(baby, CareAction.Feed, food: Food.Milk).State;
        Near(Math.Min(100, baby.Satiety + 35), fed.Satiety, "milk gives Fullness 35");
    }

    private static void NightAndBedtime()
    {
        var game = Game();
        var evening = At(2026, 3, 2, 21, 0);
        // A late dinner at 21:00 (Fullness 95, Energy 45), so only the bedtime is in question.
        var tired = game.ApplyCare(Baby(evening, satiety: 60, energy: 40, countFrom: At(2026, 3, 2, 6, 0)), CareAction.Feed).State;
        Equal(MomentStatus.Late, tired.Care.StatusOf(CareMoment.Evening), "dinner at 21:00 is late");
        var bedtime = game.ApplyCare(tired, CareAction.Rest).State;
        Equal(MomentStatus.OnTime, bedtime.Care.StatusOf(CareMoment.Bedtime), "rest at 21:00 is an on-time bedtime");
        var rested = game.Advance(bedtime, At(2026, 3, 3, 5, 0)).State;
        Near(100, rested.Energy, "an on-time bedtime restores Energy to 100 overnight");
        // 30 awake minutes, then 7.5 quiet hours at 2.5 an hour.
        Near(95 - 3.5 - 7.5 * 2.5, rested.Satiety, "Fullness falls slowly at night");
        Equal(CareMood.Content, rested.Care.Mood, "a baby put to bed wakes content");
        var split = bedtime;
        for (var at = evening.AddMinutes(20); at <= At(2026, 3, 3, 5, 0); at = at.AddMinutes(20))
            split = game.Advance(split, at).State;
        Near(rested.Energy, split.Energy, "a night split into many advances rests like one advance");
        Near(rested.Satiety, split.Satiety, "a split night decays Fullness like one advance");
        Near(rested.Health, split.Health, "a split night changes Health like one advance");
        var lateBed = game.ApplyCare(game.Advance(tired, At(2026, 3, 2, 22, 0)).State, CareAction.Rest).State;
        Near(85, game.Advance(lateBed, At(2026, 3, 3, 5, 59)).State.Energy, "a late bedtime restores Energy to 85");

        // On the first day the bedtime window is excused, yet a baby put to bed still sleeps well.
        var firstNight = BabyState.Restore("Mia", evening, TimeSpan.Zero, 70, 80, 30, 0, 0, false, false, 0, 0, 100, 100, false,
            care: CareRules.Start(evening, Tunables));
        var tucked = game.ApplyCare(firstNight, CareAction.Rest).State;
        Equal(MomentStatus.Excused, tucked.Care.StatusOf(CareMoment.Bedtime), "the first bedtime is excused");
        Near(100, game.Advance(tucked, At(2026, 3, 3, 6, 0)).State.Energy, "an excused bedtime still restores Energy");

        var dozed = game.Advance(tired, At(2026, 3, 3, 5, 0)).State;
        Near(65, dozed.Energy, "without a bedtime the baby dozes on the mat to Energy 65");
        Equal(CareMood.Fussy, dozed.Care.Mood, "without a bedtime the baby wakes fussy");
        Equal(CareTags.Bedtime, dozed.Care.Owed, "the missed bedtime is owed");
        var napped = game.ApplyCare(dozed, CareAction.Rest).State;
        Equal(CareMood.Content, napped.Care.Mood, "a nap answers the missed bedtime");
        Equal(CareTags.None, napped.Care.Owed, "nothing is owed after the nap");
    }

    private static void OnTimeLateAndMissed()
    {
        var game = Game();
        var start = Baby(At(2026, 3, 2, 6, 0));

        var onTime = game.ApplyCare(game.Advance(start, At(2026, 3, 2, 8, 0)).State, CareAction.Feed).State;
        Equal(CareReason.MealOnTime, onTime.Care.LastReason, "an on-time meal");
        Equal(CareMood.Content, game.Advance(onTime, At(2026, 3, 2, 11, 0)).State.Care.Mood, "an on-time meal keeps the baby content");

        var overdue = game.Advance(start, At(2026, 3, 2, 10, 30)).State;
        Equal(CareMood.Fussy, overdue.Care.Mood, "a meal past its due time makes the baby fussy");
        Equal(CareMoodCause.MealOverdue, overdue.Care.MoodCause, "the fussing names the overdue meal");
        var late = game.ApplyCare(overdue, CareAction.Feed).State;
        Equal(CareReason.MealLate, late.Care.LastReason, "the meal counts as late");
        Equal(CareMood.Content, late.Care.Mood, "a late meal calms the fussing");
        Equal("L", late.Care.Meals.ToCode(), "a late meal enters the ring");

        var missed = game.Advance(start, At(2026, 3, 2, 12, 30)).State;
        Equal("M", missed.Care.Meals.ToCode(), "the morning meal is missed at noon");
        Equal(CareMood.Fussy, missed.Care.Mood, "a missed meal makes the baby fussy");
        Equal(CareMoodCause.MissedWindow, missed.Care.MoodCause, "the fussing names the missed window");
        Equal(CareTags.Meal, missed.Care.Owed, "the missed meal is owed");
        var answered = game.ApplyCare(missed, CareAction.Feed).State;
        Equal(CareMood.Content, answered.Care.Mood, "any meal answers a missed one");
        Equal(CareTags.None, answered.Care.Owed, "the meal is no longer owed");
        Equal("M", answered.Care.Meals.ToCode(), "answering never erases the miss");

        var ignored = game.Advance(missed, At(2026, 3, 2, 12, 50)).State;
        Near(0.6 * (8.0 / 9) + 0.25 + 0.15, CareRules.Score(ignored.Care, Tunables), "one miss weighs one ninth of the meals");
        Equal(CareMood.Fussy, ignored.Care.Mood, "a young ledger's first miss never upsets the baby through CareScore");
    }

    private static void FussyAndUpset()
    {
        var game = Game();
        var hungry = Baby(At(2026, 3, 2, 14, 0), satiety: 16);
        var fussy = game.Advance(hungry, At(2026, 3, 2, 14, 5)).State;
        Equal(CareMood.Fussy, fussy.Care.Mood, "a need below 35 makes the baby fussy");
        Equal(CareMoodCause.LowNeed, fussy.Care.MoodCause, "the fussing names the low need");
        Equal(CareMood.Fussy, game.Advance(fussy, At(2026, 3, 2, 14, 15)).State.Care.Mood,
            "fussing lasts at least 15 minutes before it can become upset");
        var upset = game.Advance(fussy, At(2026, 3, 2, 14, 25)).State;
        Equal(CareMood.Upset, upset.Care.Mood, "a need below 15 upsets a fussing baby");
        Equal(true, CareConsequences.RefusesPlay(upset.Care), "an upset baby refuses play");

        var pushed = game.ApplyCare(upset, CareAction.Play);
        Equal(Reaction.PushesToyAway, pushed.Reaction, "an upset baby pushes toys away");
        Equal(upset.ToyFamiliarity, pushed.State.ToyFamiliarity, "refused play gives nothing");
        Equal(upset.Connection, pushed.State.Connection, "refused play adds no connection");
        Equal(upset.Energy, pushed.State.Energy, "refused play costs no energy");
        Equal(false, pushed.State.Care.TogetherToday, "refused play is not together time");
        Equal(Reaction.PushesToyAway, game.ApplyCare(upset, CareAction.ReadStory).Reaction, "an upset baby refuses a story");

        var soothedHungry = game.ApplyCare(upset, CareAction.Soothe).State;
        Equal(CareMood.Upset, soothedHungry.Care.Mood, "soothing a hungry upset baby is not enough");
        var fed = game.ApplyCare(game.ApplyCare(upset, CareAction.Feed).State, CareAction.Feed).State;
        Equal(CareMood.Upset, fed.Care.Mood, "feeding alone does not calm an upset baby");
        var calmed = game.ApplyCare(fed, CareAction.Soothe).State;
        Equal(CareMood.Content, calmed.Care.Mood, "Fullness 50 and Comfort 40, then soothing, calms an upset baby");
        Equal(Reaction.ReachesForToy, game.ApplyCare(calmed, CareAction.Play).Reaction, "a calm baby plays again");

        var unsettled = Baby(At(2026, 3, 2, 14, 0), comfort: 30);
        var fussing = game.Advance(unsettled, At(2026, 3, 2, 14, 5)).State;
        Equal(CareMood.Fussy, fussing.Care.Mood, "low comfort makes the baby fussy");
        var unanswered = game.Advance(fussing, At(2026, 3, 2, 16, 10)).State;
        Equal(CareMood.Upset, unanswered.Care.Mood, "fussing unanswered for two hours becomes upset");
        Equal(CareMoodCause.Unanswered, unanswered.Care.MoodCause, "the upset names the unanswered fussing");
        var answered = game.ApplyCare(game.Advance(fussing, At(2026, 3, 2, 15, 0)).State, CareAction.Soothe).State;
        Equal(CareMood.Content, answered.Care.Mood, "soothing answers fussing from low comfort");
        Equal(CareMood.Content, game.Advance(answered, At(2026, 3, 2, 16, 55)).State.Care.Mood,
            "answered fussing does not escalate");
        Near(90 - 5, answered.Health, "an hour with Comfort below 30 costs Health 5");
    }

    private static void OneRungPerAdvance()
    {
        var game = Game();
        var starving = Baby(At(2026, 3, 2, 14, 0), satiety: 5, comfort: 5);
        var first = game.Advance(starving, At(2026, 3, 2, 18, 0)).State;
        Equal(CareMood.Fussy, first.Care.Mood, "one advance moves at most one rung");
        var second = game.Advance(first, At(2026, 3, 2, 18, 20)).State;
        Equal(CareMood.Upset, second.Care.Mood, "the next advance can move the next rung");
        Near(90 - 5 * (4 + 20 / 60.0), second.Health, "Health falls 5 an hour while a need is below 30");
        var preview = game.AdvancePreview(first, TimeSpan.FromDays(1)).State;
        Equal(CareMood.Fussy, preview.Care.Mood, "a preview day never moves the mood");
    }

    private static void Mess()
    {
        var game = Game();
        var start = Baby(At(2026, 3, 2, 6, 0));
        var unwashed = game.Advance(start, At(2026, 3, 2, 22, 0)).State;
        Equal(MomentStatus.Missed, unwashed.Care.Wash, "a day without a wash misses the wash");
        Equal(1, unwashed.Care.Mess, "a missed wash leaves one mess item");
        Equal(true, unwashed.Care.IsMessy, "the room is messy");

        var soothed = game.ApplyCare(unwashed.With(comfort: 50), CareAction.Soothe).State;
        Near(50 + 20 * (1 - 0.05), soothed.Comfort, "each mess item cuts comfort gains by 5%");

        var nextDay = game.ApplyCare(game.Advance(unwashed, At(2026, 3, 3, 15, 0)).State, CareAction.Feed).State;
        var threeDays = game.Advance(game.ApplyCare(nextDay, CareAction.Soothe).State, At(2026, 3, 4, 23, 0)).State;
        Equal(CareRecord.MaxMess, threeDays.Care.Mess, "mess grows by one per missed wash up to three");
        var washed = game.ApplyCare(threeDays, CareAction.Bath).State;
        Equal(0, washed.Care.Mess, "washing up clears the mess");
        Equal(0, game.ApplyCare(unwashed, CareAction.ChangeDiaper).State.Care.Mess, "a diaper change also tidies up");

        var onTime = game.ApplyCare(game.Advance(start, At(2026, 3, 2, 9, 0)).State, CareAction.Bath).State;
        Equal(MomentStatus.OnTime, onTime.Care.Wash, "a morning bath is an on-time wash");
        Equal(0, game.Advance(onTime, At(2026, 3, 2, 22, 0)).State.Care.Mess, "a washed day leaves no mess");

        var longAway = game.Advance(unwashed, At(2026, 3, 20, 10, 0)).State;
        Equal(1, longAway.Care.Mess, "a long absence adds no mess past the 36-hour ceiling");
    }

    private static void Overfeeding()
    {
        var game = Game();
        var full = Baby(At(2026, 3, 2, 8, 0), satiety: 92);
        Equal(true, game.IsFull(full), "Fullness 90 or more is full");
        var overfed = game.ApplyCare(full, CareAction.Feed);
        Equal(Reaction.TooFull, overfed.Reaction, "feeding a full baby shows the too-full cue");
        Near(full.Comfort - 10, overfed.State.Comfort, "overfeeding costs Comfort 10");
        Near(full.Cleanliness - 15, overfed.State.Cleanliness, "overfeeding spits up, Cleanliness -15");
        Near(full.Health, overfed.State.Health, "overfeeding never harms health");
        Equal(CareReason.MealTooFull, overfed.State.Care.LastReason, "an overfull feed never counts as a meal");
        Equal(MomentStatus.Pending, overfed.State.Care.StatusOf(CareMoment.Morning), "the morning stays open");

        var hungry = Baby(At(2026, 3, 2, 8, 0), satiety: 89);
        Equal(false, game.IsFull(hungry), "Fullness 89 is not full");
        var fed = game.ApplyCare(hungry, CareAction.Feed);
        Equal(Reaction.Satisfied, fed.Reaction, "a normal feed is satisfying");
        Near(hungry.Comfort, fed.State.Comfort, "a normal feed costs no Comfort");

        var repeated = full;
        for (var i = 0; i < 20; i++)
            repeated = game.ApplyCare(repeated, CareAction.Feed).State;
        Equal(true, repeated.Comfort >= 0 && repeated.Cleanliness >= 0, "repeated overfeeding stays in range");
        Near(full.Health, repeated.Health, "repeated overfeeding never harms health");
    }

    private static void AwayMode()
    {
        var game = Game();
        var start = Baby(At(2026, 3, 2, 6, 0), satiety: 80, comfort: 50, energy: 90);
        var away = game.StartAway(start, TimeSpan.FromDays(3));
        Equal(true, away.Care.IsAway, "Away mode starts at once");
        Equal(CareReason.AwayStarted, away.Care.LastReason, "the diagnostic names the start");
        ExpectArgument(() => game.StartAway(start, TimeSpan.FromDays(14.01)), "Away mode lasts at most 14 days");
        ExpectArgument(() => game.StartAway(start, TimeSpan.Zero), "Away mode needs a duration");
        game.StartAway(start, TimeSpan.FromDays(14));
        try
        {
            game.StartAway(away, TimeSpan.FromDays(1));
            throw new InvalidOperationException("Away mode cannot start twice.");
        }
        catch (InvalidOperationException error) when (error.Message != "Away mode cannot start twice.")
        {
        }

        var twoDays = game.Advance(away, At(2026, 3, 4, 6, 0)).State;
        Near(60, twoDays.Satiety, "needs above the hold level settle at 60");
        Near(50, twoDays.Comfort, "needs below the hold level never fall");
        Near(60, twoDays.Energy, "Energy is held too");
        Near(start.Health, twoDays.Health, "health is held");
        Equal(TimeSpan.FromDays(2), twoDays.Age - start.Age, "development keeps a neutral pace while away");
        Equal("", twoDays.Care.Meals.ToCode() + twoDays.Care.Bedtimes.ToCode() + twoDays.Care.TogetherDays.ToCode(),
            "windows while away never enter a ring");
        Equal(true, twoDays.Care.CoveredCount >= 8, "windows while away are covered");
        Equal(CareMood.Content, twoDays.Care.Mood, "the baby stays content while away");
        Equal(0, twoDays.Care.Mess, "no mess builds while away");
        Equal(true, twoDays.Care.IsAway, "still away");
        foreach (var slot in BabySimulation.Routine(twoDays))
            Equal(RoutineSlotState.Away, slot.State, $"the strip shows {slot.Kind} as away");

        var back = game.EndAway(twoDays);
        Equal(false, back.Care.IsAway, "ending Away mode returns to normal");
        Equal(CareReason.AwayEnded, back.Care.LastReason, "the diagnostic names the end");
        Equal(true, ReferenceEquals(back, game.EndAway(back)), "ending twice changes nothing");
        var afterwards = game.Advance(back, At(2026, 3, 4, 13, 0)).State;
        Equal("M", afterwards.Care.Meals.ToCode(), "windows after Away mode count again");

        var cared = game.ApplyCare(game.Advance(twoDays, At(2026, 3, 4, 8, 0)).State, CareAction.Feed).State;
        Equal(false, cared.Care.IsAway, "hands-on care ends Away mode");
        Equal(CareReason.MealOnTime, cared.Care.LastReason, "and the care counts");

        var overdue = game.Advance(away, At(2026, 3, 5, 18, 0)).State;
        Equal(false, overdue.Care.IsAway, "Away mode ends on its own at the planned time");
        Equal(true, overdue.Care.Meals.ToCode().Contains("M"), "windows after the planned end count like any absence");
        Equal(CareMood.Fussy, overdue.Care.Mood, "an overdue return moves the baby one rung");

        var clock = At(2026, 3, 2, 6, 0);
        var saves = new MemorySaveStore();
        var session = new BabySession(Game(), saves, () => clock);
        session.Adopt("Mia");
        clock = At(2026, 3, 2, 7, 0);
        session.StartAway(TimeSpan.FromDays(7));
        Equal(true, new BabySession(Game(), saves, () => clock).State.Care.IsAway, "Away mode survives a reload");
        clock = At(2026, 3, 3, 7, 0);
        session.EndAway();
        Equal(false, session.State.Care.IsAway, "the session ends Away mode");
        Equal(false, new BabySession(Game(), saves, () => clock).State.Care.IsAway, "the end survives a reload");
    }

    // A new offset or routine applies from the next care day, so time before the boundary keeps the old clock and
    // routine whether it passes in one advance or in many short ones.
    private static void ClockAndRoutineChangesAtTheDayBoundary()
    {
        var game = Game();
        // Paris springs forward on 2026-03-29 at 02:00 (01:00 UTC).
        var springForward = new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero);
        Func<DateTimeOffset, DateTimeOffset> paris = utc =>
            utc.ToOffset(utc < springForward ? TimeSpan.FromHours(1) : TimeSpan.FromHours(2));
        var evening = Baby(At(2026, 3, 28, 21, 0), satiety: 80);
        var morning = new DateTimeOffset(2026, 3, 29, 4, 30, 0, TimeSpan.FromHours(2));
        SameNeeds(Whole(game, evening, morning), Stepped(game, evening, morning, paris), "across a DST change");

        var start = At(2026, 3, 2, 20, 30);
        var home = Baby(start);
        home = BabyState.Restore("Mia", start, home.Age, 80, 80, 80, 3, 0, true, false, 10, 1, 90, 90, true,
            care: CareRules.ChangeRoutine(home.Care, RoutinePreset.Home, RoutineCalendar.DefaultWakeTime));
        var nextDay = At(2026, 3, 3, 4, 15);
        var whole = Whole(game, home, nextDay);
        Equal(RoutinePreset.Home, whole.Care.Preset, "the new routine applies from the next care day");
        SameNeeds(whole, Stepped(game, home, nextDay, at => at), "across a routine change");
        var split = game.Advance(game.Advance(home, At(2026, 3, 2, 21, 15)).State, nextDay).State;
        SameNeeds(whole, split, "across a routine change split once");
    }

    private static BabyState Whole(BabySimulation game, BabyState state, DateTimeOffset until) =>
        game.Advance(state, until).State;

    private static BabyState Stepped(BabySimulation game, BabyState state, DateTimeOffset until,
        Func<DateTimeOffset, DateTimeOffset> local)
    {
        for (var at = state.TimeCheckpoint.AddMinutes(5); at < until; at = at.AddMinutes(5))
            state = game.Advance(state, local(at)).State;
        return game.Advance(state, until).State;
    }

    private static void SameNeeds(BabyState expected, BabyState actual, string description)
    {
        Near(expected.Satiety, actual.Satiety, $"Fullness is the same in one advance or many {description}");
        Near(expected.Comfort, actual.Comfort, $"Comfort is the same in one advance or many {description}");
        Near(expected.Energy, actual.Energy, $"Energy is the same in one advance or many {description}");
        Near(expected.Cleanliness, actual.Cleanliness, $"Cleanliness is the same in one advance or many {description}");
        Near(expected.Health, actual.Health, $"Health is the same in one advance or many {description}");
    }

    // Away mode freezes the ladder: nothing moves it while away, and time away never counts towards escalation.
    private static void AwayHoldsTheLadder()
    {
        var game = Game();
        var low = Baby(At(2026, 3, 2, 14, 0), comfort: 30);
        var owing = low.Care.ToBuilder();
        owing.Owed = CareTags.Meal;
        var away = game.StartAway(BabyState.Restore("Mia", low.TimeCheckpoint, low.Age, 80, 30, 80, 3, 0, true, false, 10, 1,
            90, 90, true, care: owing.Build()), TimeSpan.FromHours(3));
        var inside = game.Advance(away, At(2026, 3, 2, 16, 0)).State;
        Equal(true, inside.Care.IsAway, "still away at 16:00");
        Equal(CareMood.Content, inside.Care.Mood, "a low need or an owed meal never moves the ladder while away");

        var fussy = game.Advance(low, At(2026, 3, 2, 14, 5)).State;
        Equal(CareMood.Fussy, fussy.Care.Mood, "Comfort 30 makes the baby fussy");
        var leaving = game.StartAway(fussy, TimeSpan.FromHours(3));
        var back = game.Advance(leaving, At(2026, 3, 2, 17, 20)).State;
        Equal(CareMood.Fussy, back.Care.Mood, "fussing before Away mode does not escalate while away");
        Equal(leaving.Care.AwayUntil, back.Care.MoodSince, "the fussing counts again from the planned end");
        var nearly = game.Advance(back, At(2026, 3, 2, 19, 0)).State;
        Equal(CareMood.Fussy, nearly.Care.Mood, "no escalation before two hours of fussing outside Away mode");
        var escalated = game.Advance(nearly, At(2026, 3, 2, 19, 10)).State;
        Equal(CareMood.Upset, escalated.Care.Mood, "two hours of unanswered fussing after the return upsets the baby");
        Equal(CareMoodCause.Unanswered, escalated.Care.MoodCause, "the upset names the unanswered fussing");

        var morningAway = game.StartAway(Baby(At(2026, 3, 2, 6, 0)), TimeSpan.FromHours(3));
        var afterBreakfast = game.Advance(morningAway, At(2026, 3, 2, 10, 30)).State;
        Equal(false, CareRules.HasOverdueMeal(afterBreakfast.Care), "a meal window that overlaps Away mode is never overdue");
        Equal(CareMood.Content, afterBreakfast.Care.Mood, "a window covered by Away mode never makes the baby fussy");
    }

    // Two of the last three meals missed, with a miss unanswered, upsets a fussing baby.
    private static void MissedMealsUpset()
    {
        var game = Game();
        Equal(CareMood.Upset, Owing("OMM").Care.Mood, "two of the last three meals missed upsets the baby");
        Equal(CareMoodCause.MissedMeals, Owing("OMM").Care.MoodCause, "the upset names the missed meals");
        Equal(CareMood.Upset, Owing("MOM").Care.Mood, "two misses among the last three meals count however they fall");
        Equal(CareMoodCause.MissedMeals, Owing("MOM").Care.MoodCause, "misses need not be consecutive");
        Equal(CareMood.Fussy, Owing("OOM").Care.Mood, "one recent miss alone keeps the baby fussy");

        BabyState Owing(string meals)
        {
            var at = At(2026, 3, 2, 14, 0);
            var draft = Anchored(at).ToBuilder();
            draft.Meals = CareRing.Parse(meals);
            draft.Owed = CareTags.Meal;
            draft.Mood = CareMood.Fussy;
            draft.MoodCause = CareMoodCause.MissedWindow;
            draft.MoodSince = at.AddMinutes(-30);
            var baby = BabyState.Restore("Mia", at, TimeSpan.FromDays(4), 80, 80, 80, 3, 0, true, false, 10, 1, 90, 90, true,
                care: draft.Build());
            return game.Advance(baby, At(2026, 3, 2, 14, 5)).State;
        }
    }

    private static void GentleIsGentler()
    {
        Equal(true, Tunables.Gentle.IsNoHarsherThan(Tunables.Standard), "Gentle is no harsher than Standard on any rule");
        ExpectArgument(() => new CareTunables(gentle: new CareStyleRules(fussyNeed: 40)).Validate(),
            "a Gentle style harsher than Standard is rejected");
        var game = Game();

        var standard = Baby(At(2026, 3, 2, 6, 0));
        var gentle = game.ChangeCareStyle(standard, CareStyle.Gentle);
        Equal(CareStyle.Gentle, gentle.Care.Style, "the care style is stored");
        Equal(CareStyle.Standard, game.Adopt("Mia", At(2026, 3, 2, 6, 0)).Care.Style, "Standard is the default");
        Equal(CareMood.Fussy, game.Advance(standard, At(2026, 3, 2, 10, 30)).State.Care.Mood, "Standard fusses at an overdue meal");
        Equal(CareMood.Content, game.Advance(gentle, At(2026, 3, 2, 10, 30)).State.Care.Mood, "Gentle waits for the miss");

        var lowComfort = Baby(At(2026, 3, 2, 14, 0), comfort: 30);
        Equal(CareMood.Fussy, game.Advance(lowComfort, At(2026, 3, 2, 14, 5)).State.Care.Mood, "Standard fusses below 35");
        var gentleLow = game.ChangeCareStyle(lowComfort, CareStyle.Gentle);
        Equal(CareMood.Content, game.Advance(gentleLow, At(2026, 3, 2, 14, 5)).State.Care.Mood, "Gentle fusses only below 25");

        var veryLow = Baby(At(2026, 3, 2, 14, 0), comfort: 20);
        var standardFussy = game.Advance(veryLow, At(2026, 3, 2, 14, 5)).State;
        var gentleFussy = game.Advance(game.ChangeCareStyle(veryLow, CareStyle.Gentle), At(2026, 3, 2, 14, 5)).State;
        Equal(CareMood.Upset, game.Advance(standardFussy, At(2026, 3, 2, 16, 10)).State.Care.Mood,
            "Standard escalates after two hours");
        Equal(CareMood.Fussy, game.Advance(gentleFussy, At(2026, 3, 2, 16, 10)).State.Care.Mood,
            "Gentle gives four hours before escalating");

        var full = Baby(At(2026, 3, 2, 8, 0), satiety: 95);
        var standardOverfed = game.ApplyCare(full, CareAction.Feed).State;
        var gentleOverfed = game.ApplyCare(game.ChangeCareStyle(full, CareStyle.Gentle), CareAction.Feed).State;
        Equal(true, gentleOverfed.Comfort > standardOverfed.Comfort, "Gentle overfeeding costs less comfort");
        Equal(true, gentleOverfed.Cleanliness > standardOverfed.Cleanliness, "Gentle overfeeding is less messy");

        var realistic = game.ChangeCareStyle(standard, CareStyle.Realistic);
        Equal(CareMood.Fussy, game.Advance(realistic, At(2026, 3, 2, 10, 30)).State.Care.Mood,
            "Realistic behaves as Standard in this slice");
        Equal(true, ReferenceEquals(standard, game.ChangeCareStyle(standard, CareStyle.Standard)), "the same style changes nothing");
        ExpectArgument(() => game.ChangeCareStyle(standard, (CareStyle)9), "unknown styles are rejected");
    }

    private static void RoutineStripStates()
    {
        var game = Game();
        var start = Baby(At(2026, 3, 2, 6, 0));
        var strip = BabySimulation.Routine(start);
        Equal(6, strip.Count, "the strip shows six moments");
        Equal("MorningMeal,AfternoonMeal,EveningMeal,Play,Bedtime,Wash", string.Join(",", Kinds(strip)), "the strip reads through the day");
        Equal(RoutineSlotState.Later, strip[0].State, "before 07:00 breakfast is later");
        Equal(new DateTimeOffset(2026, 3, 2, 7, 0, 0, Paris), strip[0].Open, "the strip places windows on the local clock");

        var morning = game.Advance(start, At(2026, 3, 2, 8, 0)).State;
        Equal(RoutineSlotState.Due, BabySimulation.Routine(morning)[0].State, "at 08:00 breakfast is due");
        Equal(RoutineSlotState.Due, BabySimulation.Routine(morning)[3].State, "play is due all day");
        Equal(RoutineSlotState.Due, BabySimulation.Routine(morning)[5].State, "the wash is due from the morning");
        var played = game.ApplyCare(game.ApplyCare(morning, CareAction.Feed).State, CareAction.Play).State;
        Equal(RoutineSlotState.OnTime, BabySimulation.Routine(played)[0].State, "an on-time breakfast");
        Equal(RoutineSlotState.OnTime, BabySimulation.Routine(played)[3].State, "play done today");

        var overdue = game.Advance(start, At(2026, 3, 2, 11, 0)).State;
        Equal(RoutineSlotState.Overdue, BabySimulation.Routine(overdue)[0].State, "at 11:00 breakfast is late");
        Equal(RoutineSlotState.DoneLate, BabySimulation.Routine(game.ApplyCare(overdue, CareAction.Feed).State)[0].State,
            "a late breakfast is done late");
        Equal(RoutineSlotState.Missed, BabySimulation.Routine(game.Advance(start, At(2026, 3, 2, 12, 30)).State)[0].State,
            "at 12:30 breakfast is missed");

        var newborn = game.Adopt("Mia", At(2026, 3, 2, 6, 0));
        Equal(RoutineSlotState.Excused, BabySimulation.Routine(game.Advance(newborn, At(2026, 3, 2, 8, 0)).State)[0].State,
            "the first day's windows are free");
    }

    private static void Migration()
    {
        var game = Game();
        var checkpoint = At(2026, 3, 2, 9, 0);
        var shadow = CareRules.Start(checkpoint.AddDays(-3), Tunables);
        shadow = CareRules.Advance(shadow, checkpoint, Tunables);
        Equal(true, shadow.Meals.ToCode().Contains("M"), "the v12 shadow ledger holds misses nobody saw");
        var baby = BabyState.Restore("Sam", checkpoint, TimeSpan.FromDays(3), 20, 30, 40, 2, 3, true, false, 34, 8, 51, 74,
            true, care: shadow);
        var v12 = XElement.Parse(BabySaveCodec.Encode(baby));
        v12.SetAttributeValue("version", 12);
        var care = v12.Element("care");
        care.SetAttributeValue("v", 1);
        foreach (var name in new[] { "wash", "mess", "mood", "mood-since", "mood-cause", "owed", "style", "away-from", "away-until" })
            care.Attribute(name).Remove();

        var saves = new MemorySaveStore { Primary = v12.ToString(SaveOptions.DisableFormatting) };
        var clock = checkpoint.AddHours(30);
        var session = new BabySession(game, saves, () => clock);
        var decoded = session.State.Care;
        Equal(true, decoded.AwaitingAnchor, "a v12 ledger waits for an anchor again");
        Equal("", decoded.Meals.ToCode() + decoded.TogetherDays.ToCode() + decoded.Bedtimes.ToCode(), "the unseen misses are cleared");
        Equal(CareStyle.Standard, decoded.Style, "a migrated baby uses the Standard style");
        Equal(false, decoded.IsAway, "a migrated baby is not away");

        session.Refresh();
        var migrated = session.State.Care;
        Equal(CareMood.Content, migrated.Mood, "the first advance after migration moves nobody on the ladder");
        Equal(CareReason.GapExcused, migrated.LastReason, "the first advance excuses the whole gap");
        Equal(0, migrated.Mess, "a migrated baby starts with a tidy room");
        Equal(CareTags.None, migrated.Owed, "a migrated baby owes nothing");
        Equal(true, saves.Primary.Contains("version=\"15\"") && saves.Primary.Contains("<care v=\"3\""), "the migration saves v15");
        Equal(migrated, new BabySession(game, saves, () => clock).State.Care, "the migrated ledger survives a reload");

        var round = BabyState.Restore("Sam", checkpoint, TimeSpan.FromDays(3), 20, 30, 40, 2, 3, true, false, 34, 8, 51, 74, true,
            care: Consequential(checkpoint));
        Equal(round.Care, BabySaveCodec.Decode(BabySaveCodec.Encode(round)).Care, "every consequence field round-trips");
    }

    // A record that uses every slice 2 field.
    private static CareRecord Consequential(DateTimeOffset at)
    {
        var draft = Anchored(at).ToBuilder();
        draft.Wash = MomentStatus.Late;
        draft.Mess = 2;
        draft.Mood = CareMood.Upset;
        draft.MoodSince = at.AddMinutes(-20);
        draft.MoodCause = CareMoodCause.Unanswered;
        draft.Owed = CareTags.Meal | CareTags.Bedtime;
        draft.Style = CareStyle.Gentle;
        draft.AwayFrom = at.AddDays(-2);
        draft.AwayUntil = at.AddDays(-1);
        draft.BedtimeRest = MomentStatus.Late;
        draft.BedtimeRestDay = draft.CareDay - 1;
        return draft.Build();
    }

    private static string[] Kinds(System.Collections.Generic.IReadOnlyList<RoutineSlot> strip)
    {
        var kinds = new string[strip.Count];
        for (var i = 0; i < strip.Count; i++)
            kinds[i] = strip[i].Kind.ToString();
        return kinds;
    }

    // Codex review of PR #15: a long absence must land where frequent refreshes would.
    private static void ReviewFixes()
    {
        var game = Game();

        // Fussing whose only cause (low Energy) recovers overnight calms; it never escalates on return.
        var sleepy = Baby(At(2026, 3, 2, 22, 0), satiety: 90, comfort: 90, energy: 25, countFrom: At(2026, 3, 2, 22, 0));
        var fussing = game.Advance(sleepy, At(2026, 3, 2, 22, 5)).State;
        Equal(CareMood.Fussy, fussing.Care.Mood, "Energy 25 makes the baby fussy");
        var morning = game.Advance(fussing, At(2026, 3, 3, 1, 0)).State;
        Equal(true, morning.Energy >= 35, "Energy recovers overnight");
        Equal(CareMood.Content, morning.Care.Mood, "fussing that ended overnight calms instead of escalating");

        // Health lost early in the night is won back later, as it would be with a refresh partway through.
        var evening = At(2026, 3, 2, 21, 30);
        var tired = BabyState.Restore("Mia", evening, TimeSpan.FromDays(4), 100, 100, 20, 3, 0, true, false, 10, 1, 100, 100, true,
            care: Anchored(evening));
        var once = game.Advance(tired, evening.AddHours(6)).State;
        var twice = game.Advance(game.Advance(tired, evening.AddHours(2)).State, evening.AddHours(6)).State;
        Near(twice.Health, once.Health, "one long night changes Health like a night with a refresh");
        Near(100, once.Health, "early night-time Health loss is recovered later that night");
    }

    private static BabySimulation Game() =>
        new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay, TimeSpan.FromDays(7), TimeSpan.FromDays(14));

    // A sitter whose windows count from `at` (past the first-day grace).
    private static BabyState Baby(DateTimeOffset at, double satiety = 80, double comfort = 80, double energy = 80,
        DateTimeOffset? countFrom = null) =>
        BabyState.Restore("Mia", at, TimeSpan.FromDays(4), satiety, comfort, energy, 3, 0, true, false, 10, 1, 90, 90, true,
            care: Anchored(at, countFrom));

    // Windows that open from `countFrom` (default `at`) count; earlier ones that already closed were excused.
    private static CareRecord Anchored(DateTimeOffset at, DateTimeOffset? countFrom = null)
    {
        var draft = CareRules.Start(at, Tunables).ToBuilder();
        draft.ExcusedUntil = countFrom ?? at;
        return draft.Build();
    }

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, Paris);

    private static void Near(double expected, double actual, string description)
    {
        if (Math.Abs(expected - actual) > 1e-9)
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
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
}
