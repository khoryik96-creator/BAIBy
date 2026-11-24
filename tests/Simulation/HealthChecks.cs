using System;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

// Care slice 3: the health axis of the consequence ladder. Under the weather, Sick, the doctor's visit, medicine and
// Recovering, the Health floor, the save v14 migration and split-advance invariance. See docs/care-and-growth.md.
static class HealthChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly TimeSpan Paris = TimeSpan.FromHours(1);

    public static void Run()
    {
        UnderTheWeatherEntry();
        CareScoreNeedsAFreshMiss();
        OneRungPerAdvanceAndPresence();
        AbsenceNeverPassesUnderTheWeather();
        StayedUnwellAcrossTwoVisits();
        CountedTimeAndVisits();
        GoodCareDayWayBack();
        DoctorVisit();
        SickRefusesGently();
        EnergyCap();
        HealthFloor();
        Medicine();
        RecoveryFallback();
        RecoveryBoost();
        SplitAdvances();
        LongAbsenceNeverHarsher();
        AwayAndPreviewHoldTheHealthAxis();
        Readiness();
        GentleAndValidation();
        SessionAndHousehold();
        SaveAndMigration();
        Console.WriteLine("Health axis checks passed.");
    }

    private static void UnderTheWeatherEntry()
    {
        var game = Game();
        // Fullness 25 costs Health 5 an hour: 52 falls below 50 within the half hour.
        var hungry = Baby(At(2026, 3, 2, 14, 0), satiety: 25, health: 52);
        var low = game.Advance(hungry, At(2026, 3, 2, 14, 30)).State;
        Near(49.5, low.Health, "half an hour with a low need costs Health 2.5");
        Equal(CareCondition.UnderTheWeather, low.Care.Condition, "Health below 50 puts the baby under the weather");
        Equal(CareConditionCause.LowHealth, low.Care.ConditionCause, "the condition names the low Health");
        Equal(At(2026, 3, 2, 14, 30), low.Care.ConditionSince, "the condition starts at the advance");
        Equal(1, low.Care.UnwellSessions, "the visit in which it happens is the first to see it");

        var returned = game.Advance(hungry, At(2026, 3, 2, 20, 0)).State;
        Equal(CareCondition.UnderTheWeather, returned.Care.Condition, "an absence can put the baby under the weather");
        Equal(CareCondition.Well, game.Advance(Baby(At(2026, 3, 2, 14, 0), health: 50), At(2026, 3, 2, 14, 5)).State.Care.Condition,
            "Health 50 is still well");
    }

    // A low CareScore makes the baby unwell only after a meal missed since it was last well, so a way back is
    // never undone at once by the misses that caused it.
    private static void CareScoreNeedsAFreshMiss()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        var neglected = WithCare(Baby(at), draft => draft.Meals = CareRing.Parse("MMMMMMMMM"));
        Near(0.4, CareRules.Score(neglected.Care, Tunables), "nine missed meals leave CareScore 0.4");
        Equal(CareCondition.Well, game.Advance(neglected, at.AddMinutes(5)).State.Care.Condition,
            "a low CareScore without a fresh miss keeps the baby well");
        var missed = WithCare(neglected, draft => draft.MissedSinceWell = true);
        var unwell = game.Advance(missed, at.AddMinutes(5)).State;
        Equal(CareCondition.UnderTheWeather, unwell.Care.Condition, "CareScore below 0.45 after a miss: under the weather");
        Equal(CareConditionCause.LowCareScore, unwell.Care.ConditionCause, "the condition names the CareScore");

        var start = Baby(At(2026, 3, 2, 6, 0));
        Equal(false, start.Care.MissedSinceWell, "a new day has no miss");
        Equal(true, game.Advance(start, At(2026, 3, 2, 12, 30)).State.Care.MissedSinceWell, "a missed breakfast is a fresh miss");
        Equal(CareCondition.Well, game.Advance(start, At(2026, 3, 2, 12, 30)).State.Care.Condition,
            "one miss on a well-kept ledger leaves the baby well");
    }

    private static void OneRungPerAdvanceAndPresence()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        // Health 25 is below both thresholds, yet one advance moves one rung.
        var weak = Baby(at, health: 25);
        var first = game.Advance(weak, at.AddMinutes(20)).State;
        Equal(CareCondition.UnderTheWeather, first.Care.Condition, "one advance raises at most one rung");
        var second = game.Advance(first, at.AddMinutes(25)).State;
        Equal(CareCondition.Sick, second.Care.Condition, "a present advance can raise the next rung");
        Equal(CareConditionCause.LowHealth, second.Care.ConditionCause, "Sick names the low Health");

        // A return starts a visit; Sick waits until the player has been present for 15 minutes.
        // Needs between 30 and 60 neither cost nor restore Health.
        var unwell = Unwell(at, CareCondition.UnderTheWeather, health: 25, satiety: 50, comfort: 50, energy: 50);
        var back = game.Advance(unwell, at.AddHours(2)).State;
        Equal(back.TimeCheckpoint, back.Care.SessionStart, "an advance after more than 30 minutes starts a new visit");
        Equal(CareCondition.UnderTheWeather, back.Care.Condition, "a return never makes the baby sick");
        var soon = game.Advance(back, at.AddHours(2).AddMinutes(10)).State;
        Equal(CareCondition.UnderTheWeather, soon.Care.Condition, "ten minutes into a visit is too soon for Sick");
        var settled = game.Advance(soon, at.AddHours(2).AddMinutes(16)).State;
        Equal(CareCondition.Sick, settled.Care.Condition, "after 15 minutes present, Health below 30 makes the baby sick");
        Equal(true, CareConsequences.FeelsTooPoorly(settled.Care), "a sick baby feels too poorly to play");
    }

    // However the absence is split, returns alone never go past Under the weather.
    private static void AbsenceNeverPassesUnderTheWeather()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        var unwell = Unwell(at, CareCondition.UnderTheWeather, health: 20, satiety: 10, comfort: 10, energy: 10,
            cleanliness: 10, tweak: draft => draft.UnwellSessions = 5);
        Equal(CareCondition.UnderTheWeather, game.Advance(unwell, at.AddDays(14)).State.Care.Condition,
            "two weeks away in one advance stay under the weather");
        var state = unwell;
        for (var visit = 1; visit <= 40; visit++)
        {
            state = game.Advance(state, at.AddHours(visit * 2)).State;
            Equal(CareCondition.UnderTheWeather, state.Care.Condition, $"return {visit} stays under the weather");
        }

        Near(CareConsequences.HealthFloor, state.Health, "Health rests at the floor");
        var well = Baby(at, satiety: 10, comfort: 10, energy: 10, health: 35);
        for (var visit = 1; visit <= 20; visit++)
        {
            well = game.Advance(well, at.AddHours(visit * 3)).State;
            Equal(true, well.Care.Condition != CareCondition.Sick, $"absence {visit} never makes a well baby sick");
        }
    }

    // Under the weather for 18 counted hours, seen in two visits, makes the baby sick while the player is present.
    private static void StayedUnwellAcrossTwoVisits()
    {
        var game = Game();
        var morning = At(2026, 3, 2, 9, 0);
        var state = Unwell(morning, CareCondition.UnderTheWeather, health: 80, satiety: 100, comfort: 100, energy: 100,
            cleanliness: 100, tweak: draft => draft.ConditionCause = CareConditionCause.LowCareScore);
        for (var at = morning.AddMinutes(10); at <= morning.AddHours(4); at = at.AddMinutes(10))
            state = game.Advance(state, at).State;
        Equal(1, state.Care.UnwellSessions, "one visit so far");
        Near(4, state.Care.UnwellTime.TotalHours, "four counted hours");

        var back = game.Advance(state, At(2026, 3, 3, 3, 0)).State;
        Equal(2, back.Care.UnwellSessions, "returning while under the weather is a second visit");
        Near(18, back.Care.UnwellTime.TotalHours, "eighteen counted hours");
        Equal(CareCondition.UnderTheWeather, back.Care.Condition, "the return itself never makes the baby sick");
        var early = game.Advance(back, At(2026, 3, 3, 3, 10)).State;
        Equal(CareCondition.UnderTheWeather, early.Care.Condition, "Sick waits for 15 minutes of presence");
        var sick = game.Advance(early, At(2026, 3, 3, 3, 20)).State;
        Equal(CareCondition.Sick, sick.Care.Condition, "18 hours across two visits makes the baby sick");
        Equal(CareConditionCause.StayedUnwell, sick.Care.ConditionCause, "Sick names the long spell");
        Equal(true, sick.Health >= 30, "Health was not the cause");

        var oneVisit = Unwell(morning, CareCondition.UnderTheWeather, health: 80,
            tweak: draft => { draft.UnwellTime = TimeSpan.FromHours(30); draft.UnwellSessions = 1; });
        Equal(CareCondition.UnderTheWeather, game.Advance(oneVisit, morning.AddMinutes(20)).State.Care.Condition,
            "a long spell seen in one visit alone stays under the weather");
        var nearly = Unwell(morning, CareCondition.UnderTheWeather, health: 80,
            tweak: draft => { draft.UnwellTime = TimeSpan.FromHours(18) - TimeSpan.FromMinutes(25); draft.UnwellSessions = 2; });
        Equal(CareCondition.UnderTheWeather, game.Advance(nearly, morning.AddMinutes(20)).State.Care.Condition,
            "17 hours 55 minutes is not yet 18");
        Equal(CareCondition.Sick, game.Advance(nearly, morning.AddMinutes(25)).State.Care.Condition, "18 hours is");
    }

    private static void CountedTimeAndVisits()
    {
        var game = Game();
        var at = At(2026, 3, 2, 9, 0);
        var unwell = Unwell(at, CareCondition.UnderTheWeather, health: 80);
        // The last care was at 09:00, so only 36 hours of a 3-day gap count.
        var long_ = game.Advance(unwell, at.AddDays(3)).State;
        Near(36, long_.Care.UnwellTime.TotalHours, "time past the absence ceiling never counts");
        var leaving = game.StartAway(unwell, TimeSpan.FromHours(10));
        var during = game.Advance(leaving, at.AddHours(8)).State;
        Equal(TimeSpan.Zero, during.Care.UnwellTime, "time in Away mode never counts");
        Equal(1, during.Care.UnwellSessions, "a visit during Away mode is not counted");
        var after = game.Advance(leaving, at.AddHours(12)).State;
        Near(2, after.Care.UnwellTime.TotalHours, "only the time after Away mode counts");
        Equal(2, after.Care.UnwellSessions, "a return after Away mode is a visit");
        var present = unwell;
        for (var step = at.AddMinutes(15); step <= at.AddHours(3); step = step.AddMinutes(15))
            present = game.Advance(present, step).State;
        Equal(1, present.Care.UnwellSessions, "staying present is one visit");
    }

    // One good care day (two on-time meals, nothing missed, together time) brings an unwell baby back.
    private static void GoodCareDayWayBack()
    {
        var game = Game();
        var start = At(2026, 3, 2, 6, 0);
        var state = Unwell(start, CareCondition.UnderTheWeather, health: 80, satiety: 70, comfort: 90, energy: 90,
            cleanliness: 90, tweak: draft => draft.ConditionCause = CareConditionCause.LowCareScore);
        state = Care(game, state, At(2026, 3, 2, 8, 0), CareAction.Feed);
        state = Care(game, state, At(2026, 3, 2, 8, 5), CareAction.Play);
        state = Care(game, state, At(2026, 3, 2, 8, 10), CareAction.ChangeDiaper);
        state = Care(game, state, At(2026, 3, 2, 12, 0), CareAction.Rest);
        state = Care(game, state, At(2026, 3, 2, 15, 0), CareAction.Feed);
        state = Care(game, state, At(2026, 3, 2, 15, 5), CareAction.Soothe);
        state = Care(game, state, At(2026, 3, 2, 15, 10), CareAction.ChangeDiaper);
        state = Care(game, state, At(2026, 3, 2, 18, 0), CareAction.Rest);
        state = Care(game, state, At(2026, 3, 2, 19, 0), CareAction.Feed);
        state = Care(game, state, At(2026, 3, 2, 20, 0), CareAction.Rest);
        state = Care(game, state, At(2026, 3, 2, 20, 5), CareAction.ChangeDiaper);
        Equal(CareCondition.UnderTheWeather, game.Advance(state, At(2026, 3, 2, 23, 0)).State.Care.Condition,
            "the good day counts once it closes");
        var morning = game.Advance(state, At(2026, 3, 3, 4, 10)).State;
        Equal(CareCondition.Well, morning.Care.Condition, "one good care day brings the baby back to well");
        Equal(CareConditionCause.GoodCareDay, morning.Care.ConditionCause, "the condition names the good day");
        Equal(RoutineCalendar.CareDayOf(At(2026, 3, 2, 12, 0).UtcTicks, Paris, Tunables.DayStart), morning.Care.GoodDay,
            "the good day is recorded");
        Equal(false, morning.Care.MissedSinceWell, "a fresh start: older misses no longer count");

        var lateDinner = Care(game, state, At(2026, 3, 2, 21, 0), CareAction.Feed);
        var noPlay = Unwell(start, CareCondition.UnderTheWeather, health: 80, satiety: 70);
        noPlay = Care(game, noPlay, At(2026, 3, 2, 8, 0), CareAction.Feed);
        noPlay = Care(game, noPlay, At(2026, 3, 2, 15, 0), CareAction.Feed);
        noPlay = Care(game, noPlay, At(2026, 3, 2, 19, 0), CareAction.Feed);
        noPlay = Care(game, noPlay, At(2026, 3, 2, 20, 0), CareAction.Rest);
        Equal(CareCondition.UnderTheWeather, game.Advance(noPlay, At(2026, 3, 3, 4, 10)).State.Care.Condition,
            "a day without together time is not a good day");
        Equal(CareCondition.Well, game.Advance(lateDinner, At(2026, 3, 3, 4, 10)).State.Care.Condition,
            "an extra meal after a good day changes nothing");
        var lowHealth = WithHealth(state, 40);
        Equal(CareCondition.UnderTheWeather, game.Advance(lowHealth, At(2026, 3, 3, 4, 30)).State.Care.Condition,
            "a good day needs Health back at 50 too");
    }

    private static void DoctorVisit()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        var mild = Unwell(at, CareCondition.UnderTheWeather, health: 60,
            tweak: draft => { draft.ConditionCause = CareConditionCause.LowCareScore; draft.MissedSinceWell = true; });
        Equal(true, BabySimulation.CanVisitDoctor(mild), "the doctor is offered under the weather");
        var home = game.VisitDoctor(mild);
        Equal(Reaction.DoctorAllClear, home.Reaction, "a mild case goes home well");
        Equal(CareCondition.Well, home.State.Care.Condition, "the visit is a way back from under the weather");
        Equal(CareConditionCause.DoctorVisit, home.State.Care.ConditionCause, "the condition names the visit");
        Near(75, home.State.Health, "the visit restores Health 15");
        Equal(CareReason.DoctorVisited, home.State.Care.LastReason, "the diagnostic names the visit");
        Equal(at, home.State.Care.LastCareAt, "the visit is hands-on care");
        Equal(CareCondition.Well, game.Advance(home.State, at.AddMinutes(5)).State.Care.Condition,
            "old misses do not make the baby unwell again at once");

        var weak = Unwell(at, CareCondition.UnderTheWeather, health: 30);
        var medicine = game.VisitDoctor(weak);
        Equal(Reaction.DoctorVisited, medicine.Reaction, "a weaker baby needs medicine");
        Equal(CareCondition.Recovering, medicine.State.Care.Condition, "Health still below 50 after the visit: recovering");
        Near(45, medicine.State.Health, "Health 30 + 15");

        var sick = Unwell(at, CareCondition.Sick, health: 12);
        var visited = game.VisitDoctor(sick);
        Equal(CareCondition.Recovering, visited.State.Care.Condition, "the doctor turns Sick into Recovering");
        Near(27, visited.State.Health, "Health +15 at once");
        Equal(0, visited.State.Care.MedicineDoses, "no medicine yet");
        Equal(at, visited.State.Care.ConditionSince, "recovery starts at the visit");
        Equal(CareCondition.Recovering, game.Advance(visited.State, at.AddMinutes(20)).State.Care.Condition,
            "a recovering baby never falls back to Sick by itself");

        var well = Baby(at);
        Equal(false, BabySimulation.CanVisitDoctor(well), "a well baby has no doctor's visit");
        var nothing = game.VisitDoctor(well);
        Equal(true, ReferenceEquals(well, nothing.State), "a visit for a well baby changes nothing");
        Equal(false, BabySimulation.CanVisitDoctor(visited.State), "a recovering baby needs medicine, not another visit");

        var awayUnwell = game.StartAway(mild, TimeSpan.FromDays(2));
        Equal(false, game.VisitDoctor(awayUnwell).State.Care.IsAway, "the doctor's visit ends Away mode");

        var upset = WithCare(Unwell(at, CareCondition.Sick, health: 20), draft =>
        {
            draft.Mood = CareMood.Upset;
            draft.MoodSince = at;
        });
        Equal(CareMood.Upset, game.VisitDoctor(upset).State.Care.Mood, "the visit is not a way to calm crying");
    }

    private static void SickRefusesGently()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        var sick = Unwell(at, CareCondition.Sick, health: 25, energy: 40, walker: true, newSoundResolved: false);
        foreach (var action in new[] { CareAction.Play, CareAction.ReadStory, CareAction.StackBlocks, CareAction.Bath,
            CareAction.ExploreNewSound })
        {
            var refused = game.ApplyCare(sick, action);
            Equal(Reaction.TooPoorly, refused.Reaction, $"a sick baby gently refuses {action}");
            Near(sick.Energy, refused.State.Energy, $"refused {action} costs nothing");
            Near(sick.Cleanliness, refused.State.Cleanliness, $"refused {action} changes nothing");
            Near(sick.Connection, refused.State.Connection, $"refused {action} adds no connection");
            Equal(sick.StorySessions + sick.BlockTowers + sick.ToyFamiliarity,
                refused.State.StorySessions + refused.State.BlockTowers + refused.State.ToyFamiliarity, $"refused {action} counts nothing");
            Equal(false, refused.State.Care.TogetherToday, $"refused {action} is not together time");
            Equal(false, refused.State.NewSoundResolved, $"refused {action} leaves the new sound waiting");
            Equal(CareCondition.Sick, refused.State.Care.Condition, $"refusing {action} buys nothing off");
        }

        Equal(Reaction.Satisfied, game.ApplyCare(sick, CareAction.Feed).Reaction, "a sick baby still eats");
        Equal(Reaction.Calmed, game.ApplyCare(sick, CareAction.Soothe).Reaction, "a sick baby is comforted");
        Equal(Reaction.Rested, game.ApplyCare(sick, CareAction.Rest).Reaction, "a sick baby rests");
        Equal(Reaction.FreshDiaper, game.ApplyCare(sick, CareAction.ChangeDiaper).Reaction, "a sick baby is changed");
        Equal(Reaction.NewSoundComforted, game.ApplyCare(sick, CareAction.ComfortNewSound).Reaction,
            "a sick baby is comforted through a new sound");
        var unwell = Unwell(at, CareCondition.UnderTheWeather, health: 45);
        Equal(Reaction.ReachesForToy, game.ApplyCare(unwell, CareAction.Play).Reaction, "under the weather still plays");
    }

    private static void EnergyCap()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        Near(70, game.ApplyCare(Unwell(at, CareCondition.UnderTheWeather, energy: 50), CareAction.Rest).State.Energy,
            "under the weather, rest stops at Energy 70");
        Near(80, game.ApplyCare(Unwell(at, CareCondition.UnderTheWeather, energy: 80), CareAction.Rest).State.Energy,
            "Energy already above 70 is kept, not taken away");
        Near(70, game.ApplyCare(Unwell(at, CareCondition.Sick, energy: 68), CareAction.Feed).State.Energy,
            "milk's energy stops at 70 too");
        Near(85, game.ApplyCare(Baby(at, energy: 50), CareAction.Rest).State.Energy, "a well baby rests to 85");
        Near(85, game.ApplyCare(Unwell(at, CareCondition.Recovering, energy: 50), CareAction.Rest).State.Energy,
            "a recovering baby rests fully");

        var evening = At(2026, 3, 2, 21, 0);
        var tucked = game.ApplyCare(Unwell(evening, CareCondition.UnderTheWeather, health: 80, energy: 30), CareAction.Rest).State;
        Equal(MomentStatus.OnTime, tucked.Care.BedtimeRest, "put to bed on time");
        Near(70, game.Advance(tucked, At(2026, 3, 3, 6, 0)).State.Energy, "under the weather, a night's sleep stops at 70");
    }

    private static void HealthFloor()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        var empty = Baby(at, satiety: 0, comfort: 0, energy: 0, health: 30);
        var state = empty;
        for (var step = 1; step <= 60; step++)
        {
            state = game.Advance(state, at.AddMinutes(10 * step)).State;
            Equal(true, state.Health >= CareConsequences.HealthFloor, $"Health stays at or above 10 at step {step}");
        }

        Near(CareConsequences.HealthFloor, state.Health, "Health settles at the floor");
        Near(CareConsequences.HealthFloor, game.Advance(empty, at.AddDays(5)).State.Health, "a long absence stops at the floor");
        Near(CareConsequences.HealthFloor, game.AdvancePreview(empty, TimeSpan.FromDays(1)).State.Health,
            "a preview day stops at the floor");
    }

    private static void Medicine()
    {
        var game = Game();
        var visit = At(2026, 3, 2, 6, 0);
        var recovering = Unwell(visit, CareCondition.Recovering, health: 40);
        var early = game.Advance(recovering, At(2026, 3, 2, 6, 30)).State;
        Equal(MedicineState.WaitForMeal, BabySimulation.Medicine(early), "medicine waits for a meal window");
        var tooEarly = game.ApplyCare(early, CareAction.Medicine);
        Equal(Reaction.MedicineNotNow, tooEarly.Reaction, "outside a meal window the dose does not count");
        Equal(0, tooEarly.State.Care.MedicineDoses, "no dose outside a meal window");

        var breakfast = game.Advance(early, At(2026, 3, 2, 8, 0)).State;
        Equal(MedicineState.Ready, BabySimulation.Medicine(breakfast), "breakfast is a meal window");
        var first = game.ApplyCare(breakfast, CareAction.Medicine);
        Equal(Reaction.MedicineTaken, first.Reaction, "the first dose");
        Equal(1, first.State.Care.MedicineDoses, "one dose");
        Equal(CareReason.MedicineDose, first.State.Care.LastReason, "the diagnostic names the dose");
        Equal(CareCondition.Recovering, first.State.Care.Condition, "one dose is not enough");
        Equal(At(2026, 3, 2, 8, 0), first.State.Care.LastCareAt, "medicine is hands-on care");

        var again = game.ApplyCare(first.State, CareAction.Medicine);
        Equal(Reaction.MedicineNotNow, again.Reaction, "a second dose in the same meal is not counted");
        Equal(1, again.State.Care.MedicineDoses, "two doses in one meal window count once");
        var lateBreakfast = game.Advance(first.State, At(2026, 3, 2, 11, 30)).State;
        Equal(MedicineState.GivenThisMeal, BabySimulation.Medicine(lateBreakfast), "the late part of breakfast is the same window");
        Equal(1, game.ApplyCare(lateBreakfast, CareAction.Medicine).State.Care.MedicineDoses, "still one dose");

        var snack = game.Advance(lateBreakfast, At(2026, 3, 2, 15, 0)).State;
        var second = game.ApplyCare(snack, CareAction.Medicine);
        Equal(Reaction.MedicineDone, second.Reaction, "the second dose in another meal finishes the course");
        Equal(CareCondition.Well, second.State.Care.Condition, "two doses in two meal windows: well again");
        Equal(CareConditionCause.MedicineCourse, second.State.Care.ConditionCause, "the condition names the medicine");
        Equal(0, second.State.Care.MedicineDoses, "the course is cleared");
        Equal(MedicineState.NotNeeded, BabySimulation.Medicine(second.State), "a well baby needs no medicine");
        Equal(Reaction.MedicineNotNow, game.ApplyCare(second.State, CareAction.Medicine).Reaction, "medicine does nothing when well");

        // The same moment on the next care day is a different meal window.
        var nextMorning = game.Advance(first.State, At(2026, 3, 3, 8, 0)).State;
        Equal(CareCondition.Well, game.ApplyCare(nextMorning, CareAction.Medicine).State.Care.Condition,
            "breakfast today and breakfast tomorrow are two meal windows");
        Equal(CareTags.Medicine, BabySimulation.BabyCareProfile.TagsFor(BabySimulation.CareVerbFor(CareAction.Medicine)),
            "medicine carries the Medicine tag");
    }

    private static void RecoveryFallback()
    {
        var game = Game();
        var at = At(2026, 3, 3, 9, 0);
        var since = at.AddHours(-36);
        var healthy = Unwell(at, CareCondition.Recovering, health: 65, tweak: draft => draft.ConditionSince = since);
        Equal(CareCondition.Recovering, game.Advance(Unwell(at, CareCondition.Recovering, health: 65,
            tweak: draft => draft.ConditionSince = since.AddMinutes(10)), at.AddMinutes(5)).State.Care.Condition,
            "before 36 hours recovering continues");
        var done = game.Advance(healthy, at.AddMinutes(5)).State;
        Equal(CareCondition.Well, done.Care.Condition, "after 36 hours with Health 60 recovering ends anyway");
        Equal(CareConditionCause.RecoveredWithTime, done.Care.ConditionCause, "the condition names the time");
        var weak = Unwell(at, CareCondition.Recovering, health: 50, tweak: draft => draft.ConditionSince = since);
        Equal(CareCondition.Recovering, game.Advance(weak, at.AddMinutes(5)).State.Care.Condition,
            "after 36 hours with Health below 60 recovering continues until the medicine or Health");
    }

    private static void RecoveryBoost()
    {
        var game = Game();
        var at = At(2026, 3, 2, 9, 0);
        Near(40 + 12, game.Advance(Unwell(at, CareCondition.Recovering, health: 40, satiety: 100, comfort: 100,
            energy: 100, cleanliness: 100), at.AddHours(1)).State.Health, "recovering, Health recovers twice as fast");
        Near(40 + 6, game.Advance(Baby(at, satiety: 100, comfort: 100, energy: 100, health: 40, cleanliness: 100),
            at.AddHours(1)).State.Health, "well, Health recovers 6 an hour");
        var late = Unwell(at, CareCondition.Recovering, health: 40, satiety: 100, comfort: 100, energy: 100, cleanliness: 100,
            tweak: draft => draft.ConditionSince = at.AddHours(-35.5));
        Near(40 + 6 + 3, game.Advance(late, at.AddHours(1)).State.Health, "the boost ends 36 hours after the visit");
        var previewed = game.AdvancePreview(Unwell(at, CareCondition.Recovering, health: 40, satiety: 100, comfort: 100,
            energy: 100, cleanliness: 100), TimeSpan.FromHours(1)).State;
        Near(52, previewed.Health, "a preview hour keeps the boost");
    }

    // Within a session, split advances equal one advance on every rung; across a rung change the long advance never
    // lands harsher.
    private static void SplitAdvances()
    {
        var game = Game();
        // Under the weather, put to bed at 21:00: one long night or many short advances.
        var evening = At(2026, 3, 2, 21, 0);
        var tucked = game.ApplyCare(Unwell(evening, CareCondition.UnderTheWeather, health: 80, energy: 30), CareAction.Rest).State;
        var morning = At(2026, 3, 3, 5, 0);
        var whole = game.Advance(tucked, morning).State;
        var split = Stepped(game, tucked, morning, 20);
        SameNeeds(whole, split, "under the weather overnight");
        Equal(whole.Care.UnwellTime, split.Care.UnwellTime, "counted time under the weather is the same split or whole");
        Equal(whole.Care.Condition, split.Care.Condition, "the night ends on the same rung");

        // Sick through an afternoon.
        var afternoon = At(2026, 3, 2, 14, 0);
        var sick = Unwell(afternoon, CareCondition.Sick, health: 40, energy: 50, satiety: 40);
        var sickWhole = game.Advance(sick, afternoon.AddHours(5)).State;
        var sickSplit = Stepped(game, sick, afternoon.AddHours(5), 10);
        SameNeeds(sickWhole, sickSplit, "while sick");
        Equal(CareCondition.Sick, sickSplit.Care.Condition, "sick waits for the doctor");

        // Recovering across the end of the boost window and the 36-hour end.
        var start = At(2026, 3, 3, 8, 0);
        var recovering = Unwell(start, CareCondition.Recovering, health: 30, satiety: 100, comfort: 100, energy: 100,
            cleanliness: 100, tweak: draft => draft.ConditionSince = start.AddHours(-34));
        var recWhole = game.Advance(recovering, start.AddHours(4)).State;
        var recSplit = Stepped(game, recovering, start.AddHours(4), 20);
        SameNeeds(recWhole, recSplit, "across the end of recovery");
        Near(30 + 2 * 12 + 2 * 6, recWhole.Health, "two boosted hours, then two ordinary ones");
        Equal(CareCondition.Well, recWhole.Care.Condition, "the long advance ends recovering");
        Equal(CareCondition.Well, recSplit.Care.Condition, "so do the short ones");

        // Under the weather across two days in one advance, or refreshed every half hour and a bit (each a return).
        var unwell = Unwell(afternoon, CareCondition.UnderTheWeather, health: 80, satiety: 100, comfort: 100, energy: 100,
            cleanliness: 100);
        var countedWhole = game.Advance(unwell, afternoon.AddHours(10)).State;
        var countedSplit = Stepped(game, unwell, afternoon.AddHours(10), 40);
        Equal(countedWhole.Care.UnwellTime, countedSplit.Care.UnwellTime, "counted hours add up the same across returns");
    }

    // A well baby whose Health slides: refreshed often it can reach Sick, but one long advance only reaches Under the
    // weather, with the same needs and Health.
    private static void LongAbsenceNeverHarsher()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        var sliding = Baby(at, satiety: 20, health: 55);
        var whole = game.Advance(sliding, at.AddHours(6)).State;
        var split = Stepped(game, sliding, at.AddHours(6), 10);
        SameNeeds(whole, split, "while Health slides");
        Equal(CareCondition.UnderTheWeather, whole.Care.Condition, "one long advance moves one rung");
        Equal(CareCondition.Sick, split.Care.Condition, "refreshed often, the slide is seen and can reach Sick");
        Equal(true, whole.Care.Condition <= split.Care.Condition, "the long advance is never harsher");
    }

    private static void AwayAndPreviewHoldTheHealthAxis()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        var away = game.StartAway(Baby(at, health: 45), TimeSpan.FromDays(2));
        var inside = game.Advance(away, at.AddDays(1)).State;
        Equal(CareCondition.Well, inside.Care.Condition, "Away mode holds the health axis");
        var unwellAway = game.StartAway(Unwell(at, CareCondition.UnderTheWeather, health: 20), TimeSpan.FromDays(2));
        var stillAway = Stepped(game, unwellAway, at.AddHours(3), 10);
        Equal(CareCondition.UnderTheWeather, stillAway.Care.Condition, "nothing gets worse while away");
        Equal(TimeSpan.Zero, stillAway.Care.UnwellTime, "time away never counts");

        var unwell = Unwell(at, CareCondition.UnderTheWeather, health: 20);
        Equal(CareCondition.UnderTheWeather, game.AdvancePreview(unwell, TimeSpan.FromDays(1)).State.Care.Condition,
            "a preview day never moves the health axis");
        var slipping = game.AdvancePreview(Baby(at, satiety: 10, health: 55), TimeSpan.FromDays(1)).State;
        Equal(CareCondition.Well, slipping.Care.Condition, "a preview day leaves the rung to the next real refresh");
        Equal(CareCondition.UnderTheWeather, game.Advance(slipping, at.AddMinutes(1)).State.Care.Condition,
            "the next refresh sees the low Health");
    }

    private static void Readiness()
    {
        var at = At(2026, 3, 2, 14, 0);
        Near(1, CareConsequences.Readiness(Baby(at).Care), "well: full readiness");
        Near(0.75, CareConsequences.Readiness(Unwell(at, CareCondition.UnderTheWeather).Care), "under the weather: 0.75");
        Near(0.5, CareConsequences.Readiness(Unwell(at, CareCondition.Recovering).Care), "recovering: 0.5");
        Near(0, CareConsequences.Readiness(Unwell(at, CareCondition.Sick).Care), "sick before the doctor: paused");
        var upset = WithCare(Baby(at), draft => draft.Mood = CareMood.Upset);
        Near(0.5, CareConsequences.Readiness(upset.Care), "upset: 0.5");
        var upsetUnwell = WithCare(Unwell(at, CareCondition.UnderTheWeather), draft => draft.Mood = CareMood.Upset);
        Near(0.5, CareConsequences.Readiness(upsetUnwell.Care), "the lower factor applies");
    }

    private static void GentleAndValidation()
    {
        var game = Game();
        var at = At(2026, 3, 2, 14, 0);
        Equal(true, Tunables.Gentle.IsNoHarsherThan(Tunables.Standard), "Gentle is no harsher on the health axis");
        var gentle = game.ChangeCareStyle(Baby(at, health: 45), CareStyle.Gentle);
        Equal(CareCondition.Well, game.Advance(gentle, at.AddMinutes(5)).State.Care.Condition, "Gentle waits until Health 40");
        var gentleUnwell = game.ChangeCareStyle(Unwell(at, CareCondition.UnderTheWeather, health: 25), CareStyle.Gentle);
        Equal(CareCondition.UnderTheWeather, game.Advance(gentleUnwell, at.AddMinutes(20)).State.Care.Condition,
            "Gentle is sick only below Health 20, after 30 minutes present");
        ExpectArgument(() => new CareTunables(gentle: new CareStyleRules(unwellHealth: 60)).Validate(),
            "a Gentle health threshold harsher than Standard is rejected");
        ExpectArgument(() => new CareTunables(standard: new CareStyleRules(sickHealth: 50)).Validate(),
            "Sick needs a lower Health threshold than Under the weather");
        ExpectArgument(() => new CareTunables(standard: new CareStyleRules(sickHealth: 10),
            gentle: new CareStyleRules(sickHealth: 10)).Validate(), "Sick must sit above the Health floor");
        ExpectArgument(() => new CareTunables(recoveryBoost: 0.5).Validate(), "recovering never heals slower");
        ExpectArgument(() => new CareTunables(recoveryDoses: 0).Validate(), "a course needs a dose");
    }

    private static void SessionAndHousehold()
    {
        var game = Game();
        var at = At(2026, 3, 2, 12, 30);
        var sick = Unwell(at, CareCondition.Sick, health: 40);
        Equal(true, HouseholdRules.NeedsAttention(sick), "a sick baby lights the Home dot");
        Equal(false, HouseholdRules.NeedsAttention(Unwell(at, CareCondition.Recovering, health: 80)),
            "a recovering baby with good needs does not");
        Equal(true, CareDiagnostics.Summary(sick.Care, Tunables).Contains("Sick"), "the read-out names the condition");

        var clock = at;
        var saves = new MemorySaveStore { Primary = BabySaveCodec.Encode(sick) };
        var session = new BabySession(game, saves, () => clock);
        Equal(true, session.CanVisitDoctor, "the session offers the doctor");
        clock = at.AddMinutes(5);
        var visit = session.VisitDoctor();
        Equal(Reaction.DoctorVisited, visit.Reaction, "the session visits the doctor");
        Equal(CareCondition.Recovering, new BabySession(game, saves, () => clock).State.Care.Condition,
            "the visit is saved");
        Equal(false, session.CanVisitDoctor, "no second visit");
        Equal(MedicineState.WaitForMeal, session.Medicine, "12:35 is between meals");
        clock = At(2026, 3, 2, 15, 0);
        session.Refresh();
        Equal(MedicineState.Ready, session.Medicine, "the snack window is open");
        Equal(Reaction.MedicineTaken, session.ApplyCare(CareAction.Medicine).Reaction, "the session gives medicine");
        Equal(1, new BabySession(game, saves, () => clock).State.Care.MedicineDoses, "the dose is saved");
    }

    private static void SaveAndMigration()
    {
        var at = At(2026, 3, 2, 14, 0);
        var full = Unwell(at, CareCondition.Recovering, health: 44, tweak: draft =>
        {
            draft.ConditionCause = CareConditionCause.DoctorVisit;
            draft.ConditionSince = at.AddHours(-3);
            draft.UnwellTime = TimeSpan.FromHours(7.25);
            draft.UnwellSessions = 3;
            draft.SessionStart = at.AddMinutes(-40);
            draft.MissedSinceWell = true;
            draft.MedicineDoses = 1;
            draft.LastDoseWindow = CareRules.WindowKey(draft.CareDay, CareMoment.Morning);
            draft.GoodDay = draft.CareDay - 2;
        });
        var decoded = BabySaveCodec.Decode(BabySaveCodec.Encode(full));
        Equal(full.Care, decoded.Care, "every health field round-trips");
        Equal(true, BabySaveCodec.Encode(full).Contains("<care v=\"3\""), "the care record is v3");
        Equal(true, BabySaveCodec.Encode(full).StartsWith("<baby version=\"14\""), "the baby save is v14");

        // A v13 save (care v2) from before the health axis.
        var v13 = XElement.Parse(BabySaveCodec.Encode(Baby(at, health: 35)));
        v13.SetAttributeValue("version", 13);
        var care = v13.Element("care");
        care.SetAttributeValue("v", 2);
        foreach (var name in new[] { "condition", "condition-cause", "condition-since", "unwell-time", "unwell-sessions",
            "session-start", "missed-since-well", "doses", "dose-window", "good-day" })
            care.Attribute(name).Remove();
        var saves = new MemorySaveStore { Primary = v13.ToString(SaveOptions.DisableFormatting) };
        var clock = at;
        var session = new BabySession(Game(), saves, () => clock);
        Equal(CareCondition.Well, session.State.Care.Condition, "a v13 baby decodes well");
        Equal(at, session.State.Care.SessionStart, "its visit starts at the checkpoint");
        Equal(TimeSpan.Zero, session.State.Care.UnwellTime, "nothing is counted");
        Equal(0, session.State.Care.MedicineDoses, "no medicine");
        clock = at.AddMinutes(5);
        session.Refresh();
        Equal(true, saves.Primary.Contains("version=\"14\"") && saves.Primary.Contains("<care v=\"3\""), "the next write is v14");
        Equal(CareCondition.UnderTheWeather, session.State.Care.Condition,
            "an anchored v13 baby with low Health follows the ladder from its next refresh");

        // Older saves (no care at all) migrate as before: the first advance excuses the gap and moves no rung.
        var legacy = BabyState.Restore("Sam", at, TimeSpan.FromDays(3), 20, 30, 40, 2, 3, true, false, 34, 8, 51, 20, true);
        var anchored = Game().Advance(legacy, at.AddHours(30)).State;
        Equal(CareCondition.Well, anchored.Care.Condition, "the first advance after migration moves no rung on the health axis");

        foreach (var (name, value) in new[] { ("condition", "9"), ("condition-cause", "99"), ("doses", "-1"), ("unwell-time", "-5"),
            ("session-start", long.MaxValue.ToString()), ("good-day", "-3") })
        {
            var bad = XElement.Parse(BabySaveCodec.Encode(full));
            bad.Element("care").SetAttributeValue(name, value);
            ExpectInvalid(bad.ToString(SaveOptions.DisableFormatting), $"care {name}=\"{value}\" is rejected as corrupt");
        }

        var missing = XElement.Parse(BabySaveCodec.Encode(full));
        missing.Element("care").Attribute("doses").Remove();
        ExpectInvalid(missing.ToString(SaveOptions.DisableFormatting), "a v3 care record missing a health field is corrupt");
        var future = XElement.Parse(BabySaveCodec.Encode(full));
        future.SetAttributeValue("version", 15);
        try
        {
            BabySaveCodec.Decode(future.ToString(SaveOptions.DisableFormatting));
            throw new InvalidOperationException("A v15 baby save should be unsupported.");
        }
        catch (NotSupportedException)
        {
        }
    }

    private static void ExpectInvalid(string payload, string description)
    {
        try
        {
            BabySaveCodec.Decode(payload);
        }
        catch (System.IO.InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected InvalidDataException");
    }

    private static BabyState Care(BabySimulation game, BabyState state, DateTimeOffset at, CareAction action) =>
        game.ApplyCare(game.Advance(state, at).State, action).State;

    private static BabyState Stepped(BabySimulation game, BabyState state, DateTimeOffset until, int minutes)
    {
        for (var at = state.TimeCheckpoint.AddMinutes(minutes); at < until; at = at.AddMinutes(minutes))
            state = game.Advance(state, at).State;
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

    private static BabySimulation Game() =>
        new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay, TimeSpan.FromDays(7), TimeSpan.FromDays(14));

    // A sitter whose windows count from `at` (past the first-day grace), visiting since `at`.
    private static BabyState Baby(DateTimeOffset at, double satiety = 80, double comfort = 80, double energy = 80,
        double health = 90, double cleanliness = 90) =>
        BabyState.Restore("Mia", at, TimeSpan.FromDays(4), satiety, comfort, energy, 3, 0, true, false, 10, 1, cleanliness,
            health, true, care: Anchored(at));

    // A baby on a given rung since `at`.
    private static BabyState Unwell(DateTimeOffset at, CareCondition condition, double health = 45, double satiety = 80,
        double comfort = 80, double energy = 80, double cleanliness = 90, bool walker = false, bool newSoundResolved = true,
        Action<CareRecord.Builder> tweak = null)
    {
        var draft = Anchored(at).ToBuilder();
        draft.Condition = condition;
        draft.ConditionCause = condition == CareCondition.Recovering ? CareConditionCause.DoctorVisit : CareConditionCause.LowHealth;
        draft.ConditionSince = at;
        draft.UnwellSessions = condition == CareCondition.Well ? 0 : 1;
        tweak?.Invoke(draft);
        return BabyState.Restore("Mia", at, TimeSpan.FromDays(walker ? 15 : 4), satiety, comfort, energy, 3, 0, true, walker,
            10, 1, cleanliness, health, newSoundResolved, canWalk: walker, care: draft.Build());
    }

    private static BabyState WithCare(BabyState state, Action<CareRecord.Builder> change)
    {
        var draft = state.Care.ToBuilder();
        change(draft);
        return Restore(state, state.Health, draft.Build());
    }

    private static BabyState WithHealth(BabyState state, double health) => Restore(state, health, state.Care);

    private static BabyState Restore(BabyState state, double health, CareRecord care) =>
        BabyState.Restore(state.Name, state.TimeCheckpoint, state.Age, state.Satiety, state.Comfort, state.Energy,
            state.ToyFamiliarity, state.PlushFamiliarity, state.CanSitAndReach, state.CanCrawl, state.Connection,
            state.StorySessions, state.Cleanliness, health, state.NewSoundResolved, state.NurseryTheme, state.CanWalk,
            state.BlockTowers, state.Appearance, care);

    private static CareRecord Anchored(DateTimeOffset at)
    {
        var draft = CareRules.Start(at, Tunables).ToBuilder();
        draft.ExcusedUntil = at;
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
