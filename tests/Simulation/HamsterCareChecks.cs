using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// P2 Hamster: the second species on the care profile mechanism (care slice 7). A nocturnal routine that never punishes
// school hours (two nap blocks that excuse themselves, one dinner, evening wheel time and the daily bedding tidy), need
// rates tuned for one meal a day, fidgety and grumpy moods, overfeeding, Away mode, the vet and medicine, the milestones
// Wheel, Tunnel and Hand-tame, the setback, Bond, Trust, the care rhythm and the journal, helpers, earned and premium
// breeds, adoption, saves (a new species: no migration) and split advances. See docs/pet-mode.md and
// docs/care-and-growth.md.
static class HamsterCareChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly PetCatalog Catalog = PetCatalog.Default;
    private static readonly PetAppearance Syrian = PetAppearance.Default(Catalog, PetBreed.SyrianGolden);
    // A breed with tendencies of its own; the species' rates are checked on the catalog.
    private static readonly PetAppearance Panda = PetAppearance.Default(Catalog, PetBreed.Panda);

    public static void Run()
    {
        Profile();
        RoutineWindows();
        DayNap();
        OneMealTuning();
        WheelAndBedding();
        Moods();
        Overfeeding();
        Away();
        HealthAndVet();
        MilestoneTiming();
        Setback();
        BondTrustRhythmJournal();
        Helpers();
        PreviewDay();
        SplitAdvances();
        Saves();
        Breeds();
        Household();
        Console.WriteLine("Hamster care checks passed.");
    }

    private static void Profile()
    {
        var hamster = PetCareProfile.Hamster;
        Equal(hamster, PetCareProfile.For(Species.Hamster), "the hamster has a care profile");
        Equal(CareRoutine.Hamster, hamster.Routine, "and its own routine");
        Equal(CareTags.Meal, hamster.TagsFor(PetVerb.Bowl), "the bowl is the meal");
        Equal(CareTags.Together | CareTags.Play, hamster.TagsFor(PetVerb.Wheel), "the wheel answers the play window");
        Equal(CareTags.Hygiene, hamster.TagsFor(PetVerb.FluffBedding), "fluffing the bedding is the daily tidy");
        Equal(CareTags.Together | CareTags.Comfort, hamster.TagsFor(PetVerb.GentleHold), "a gentle hold is together time that comforts");
        Equal(CareTags.Bedtime, hamster.TagsFor(PetVerb.Nap), "the nap is a rest");
        Equal(CareTags.Medicine, hamster.Profile.TagsFor("medicine"), "medicine is a care verb of the profile");
        foreach (var verb in Catalog.Get(Species.Hamster).Verbs)
            Equal(true, hamster.Offers(verb.Verb), $"the profile maps the hamster's {verb.Verb}");
        Equal(PetMeter.Exercise, hamster.ComfortMeter, "Exercise plays the comfort role");
        Equal(PetMeter.Bedding, hamster.CleanlinessMeter, "Bedding plays the cleanliness role");
        Equal<PetMeter?>(PetMeter.Comfort, hamster.RoleOf(PetMeter.Exercise), "the role of Exercise");
        Equal<PetMeter?>(null, PetCareProfile.Cat.RoleOf(PetMeter.Exercise), "a cat has no Exercise");
        Equal(true, hamster.RestsAlone, "a hamster naps on its own");
        Equal(1, hamster.MealsPerDay, "one meal a day");
        Equal(true, hamster.NapsByDay, "it naps by day");
        Equal(false, PetCareProfile.Cat.NapsByDay, "a cat does not");
        Equal<EngagementKind?>(EngagementKind.PlayWand, hamster.KindOf(PetVerb.Wheel, false), "the wheel feeds Motor like play");
        Equal<EngagementKind?>(EngagementKind.Soothe, hamster.KindOf(PetVerb.GentleHold, false), "a hold feeds Social");
        Equal<EngagementKind?>(EngagementKind.Hygiene, hamster.KindOf(PetVerb.FluffBedding, false), "bedding is grooming");
        Equal<EngagementKind?>(EngagementKind.Meal, hamster.KindOf(PetVerb.Bowl, true), "an on-time dinner is a meal");
        Equal("Wheel,Tunnel,HandTame", string.Join(",", hamster.Skills), "the hamster's milestones");
        Equal("2,4,6", string.Join(",", hamster.SkillAges.Select(age => age.TotalDays)), "at nominal days 2, 4 and 6");
        Equal(2, hamster.ReachedBy(TimeSpan.FromDays(5)), "milestones by age");
        Equal(TimeSpan.FromDays(5), Catalog.Get(Species.Hamster).GrownAge, "a hamster grows up on day 5");
    }

    private static void RoutineWindows()
    {
        var school = RoutineCalendar.Windows(CareRoutine.Hamster, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal("Nap 07:00-11:30-12:00,Play 17:00-20:00-21:00,Meal 17:00-20:30-21:30,Nap 12:00-16:00-16:30",
            Describe(school), "the hamster's school day: a day nap in two blocks, wheel time and dinner in the evening");
        var home = RoutineCalendar.Windows(CareRoutine.Hamster, RoutinePreset.Home, TimeSpan.FromHours(9));
        Equal("Nap 07:00-11:00-11:30,Play 16:00-19:00-20:00,Meal 16:30-19:30-20:30,Nap 11:30-15:00-15:30",
            Describe(home), "the Home preset; the wake time does not move the nap");
        Equal((TimeSpan.FromHours(7), TimeSpan.FromHours(16)), RoutineCalendar.Night(CareRoutine.Hamster, RoutinePreset.SchoolDay,
            RoutineCalendar.DefaultWakeTime), "quiet hours are the day nap");
        Equal((TimeSpan.FromHours(21.5), TimeSpan.FromHours(7)), RoutineCalendar.Night(CareRoutine.Cat, RoutinePreset.SchoolDay,
            RoutineCalendar.DefaultWakeTime), "a cat's quiet hours are unchanged");
        Equal(1, RoutineCalendar.MealWindows(CareRoutine.Hamster), "one meal window");
        Equal(1, RoutineCalendar.GoodDayMeals(CareRoutine.Hamster), "a good day asks for the dinner");
        Equal(2, RoutineCalendar.GoodDayMeals(CareRoutine.Cat), "and two meals of a cat");
        Equal(2, RoutineCalendar.GoodDayMeals(CareRoutine.Baby), "and of a baby");
        Equal(true, RoutineCalendar.HasPlayWindow(CareRoutine.Hamster), "the wheel window keeps the together ring");
        var wash = RoutineCalendar.WashWindow(CareRoutine.Hamster, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal((TimeSpan.FromHours(7), TimeSpan.FromHours(20.5), TimeSpan.FromHours(21.5)), wash,
            "the bedding tidy: any time from morning until dinner is due");
        Tunables.Validate();

        var sim = Sim();
        var hamster = Hamster(At(3, 17, 30));
        var fed = sim.Care(hamster, PetVerb.Bowl).State;
        Equal(CareReason.MealOnTime, fed.Care.LastReason, "dinner at 17:30 is on time");
        Equal(MomentStatus.OnTime, fed.Care.StatusOf(CareMoment.Evening), "dinner is done");
        Equal("Nap:Excused,Play:Due,EveningMeal:OnTime,Wash:Due",
            string.Join(",", sim.Routine(fed).Select(slot => slot.Kind + ":" + slot.State)), "the strip: nap, wheel, dinner, bedding");
        var late = sim.Care(sim.Step(hamster, At(3, 21, 0)).State, PetVerb.Bowl).State;
        Equal(CareReason.MealLate, late.Care.LastReason, "dinner after 20:30 is late");
        Equal("L", late.Care.Meals.ToCode(), "a late dinner counts half");

        var missed = sim.Step(hamster, At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, missed.Care.StatusOf(CareMoment.Evening), "dinner is missed at 21:30");
        Equal(CareTags.Meal, missed.Care.Owed, "a missed dinner is owed");
        Equal(CareMood.Fussy, missed.Care.Mood, "and the hamster is fidgety");
        var answered = sim.Care(missed, PetVerb.Bowl).State;
        Equal(CareTags.None, answered.Care.Owed, "a meal answers it");
        Equal(CareMood.Content, answered.Care.Mood, "and the hamster settles");

        var adopted = sim.Adopt("Pip", Syrian, At(3, 18, 0));
        Equal(MomentStatus.Excused, sim.Step(adopted, At(3, 23, 0)).State.Care.StatusOf(CareMoment.Evening),
            "the first day's windows are excused");
    }

    // The day nap through school hours: the hamster sleeps, nothing is asked, nothing is missed or owed, and a player who
    // is at school all day loses nothing.
    private static void DayNap()
    {
        var sim = Sim();
        var morning = Hamster(At(3, 6, 0), bond: 30);
        Equal(false, PetSimulation.IsNapping(morning), "awake before the nap");
        var napping = sim.Step(morning, At(3, 10, 0)).State;
        Equal(true, PetSimulation.IsNapping(napping), "napping at 10:00");
        Equal("Nap:Napping,Play:Later,EveningMeal:Later,Wash:Due",
            string.Join(",", sim.Routine(napping).Select(slot => slot.Kind + ":" + slot.State)), "the strip shows the nap");
        var afternoon = sim.Step(napping, At(3, 16, 45)).State;
        Equal(false, PetSimulation.IsNapping(afternoon), "awake from 16:00");
        Equal(MomentStatus.Excused, afternoon.Care.StatusOf(CareMoment.Morning), "the morning nap closes excused");
        Equal(MomentStatus.Excused, afternoon.Care.StatusOf(CareMoment.Bedtime), "and so does the afternoon nap");
        Equal(CareTags.None, afternoon.Care.Owed, "a nap is never owed");
        Equal(CareMood.Content, afternoon.Care.Mood, "a sleeping day is never a failure");
        Equal(30.0, afternoon.Bond, "and costs no Bond");
        Equal("", afternoon.Care.Bedtimes.ToCode(), "naps never enter the bedtime ring");
        Equal(1.0, CareRules.Score(afternoon.Care, Tunables), "the CareScore is untouched");
        Equal(true, afternoon.Meter(PetMeter.Energy) > morning.Meter(PetMeter.Energy), "the nap brings Energy back");
        Near(100 - 0.75 * 3, afternoon.Meter(PetMeter.Energy), "all the way to full by 16:00, with no bedtime asked");
        Near(morning.Meter(PetMeter.Fullness) - 2.4 - 9 * 0.8 - 0.75 * 2.4, afternoon.Meter(PetMeter.Fullness),
            "needs fall slowly while it naps (0.8/h) and at the awake rate either side (2.4/h)");

        var wheel = sim.Care(napping, PetVerb.Wheel);
        Equal(PetCue.Napping, wheel.Cue, "the wheel waits for the evening while it naps");
        Equal(napping.Meter(PetMeter.Exercise), wheel.State.Meter(PetMeter.Exercise), "and gives nothing");
        Equal(MomentStatus.Pending, wheel.State.Care.StatusOf(CareMoment.Afternoon), "a napping hamster misses no wheel time");
        Equal(napping.Bond, wheel.State.Bond, "and costs no Bond");
        var peek = sim.Care(napping, PetVerb.GentleHold);
        Equal(PetCue.Content, peek.Cue, "a gentle peek is welcome");
        Equal(true, peek.State.Bond > napping.Bond, "and builds Bond");
        var snack = sim.Care(napping, PetVerb.Bowl);
        Equal(CareReason.MealOutsideWindow, snack.State.Care.LastReason, "food in the day is stashed, not dinner");
        Equal(true, snack.State.Meter(PetMeter.Fullness) > napping.Meter(PetMeter.Fullness), "but it still fills the cheeks");
        Equal(MomentStatus.Pending, snack.State.Care.StatusOf(CareMoment.Evening), "dinner still waits");
        var tucked = sim.Care(napping, PetVerb.Nap);
        Equal(CareReason.BedtimeOutsideWindow, tucked.State.Care.LastReason, "tucking in during a nap block marks nothing");
        Equal(MomentStatus.Pending, tucked.State.Care.StatusOf(CareMoment.Morning), "the nap is its own");
        Equal(CareReason.WashOnTime, sim.Care(napping, PetVerb.FluffBedding).State.Care.LastReason,
            "the bedding can be tidied while it sleeps");

        // Past the 8-hour cap nothing decays, but the naps it slept through still rest it.
        var evening = Hamster(At(3, 18, 0), energy: 40);
        var nextAfternoon = sim.Step(evening, At(4, 15, 0)).State;
        Near(40 - 8 * 3 + 8 * 9, nextAfternoon.Meter(PetMeter.Energy), "8 counted awake hours, then the uncounted nap rests it");
        Near(evening.Meter(PetMeter.Fullness) - 8 * 2.4, nextAfternoon.Meter(PetMeter.Fullness), "while nothing else moves past the cap");

        // A fidgety hamster is awake anyway, so it may run.
        var fidgety = sim.Step(Hamster(At(3, 9, 0), exercise: 25), At(3, 9, 5)).State;
        Equal(CareMood.Fussy, fidgety.Care.Mood, "low Exercise makes it fidgety");
        Equal(PetCue.Content, sim.Care(fidgety, PetVerb.Wheel).Cue, "a fidgety hamster runs on the wheel, even by day");

        // A week of school days: the player only visits in the evening and before school.
        var week = sim.Adopt("Pip", Syrian, At(2, 18, 0));
        week = Run(sim, week, Attentive, At(2, 18, 0), At(10, 22, 0));
        Equal(true, week.Care.Meals.ToCode().All(mark => mark == 'O'), "an evening player keeps every dinner on time");
        Equal("", week.Care.Bedtimes.ToCode(), "and is never asked for a bedtime");
    }

    // One meal a day: the needs stay comfortable for a player who visits before school and in the evening.
    private static void OneMealTuning()
    {
        var species = Catalog.Get(Species.Hamster);
        var rate = species.Meters.ToDictionary(meter => meter.Meter, meter => meter.DecayPerHour);
        var profile = PetCareProfile.Hamster;
        // Awake 16:00 to 07:00 (15 h) at the awake rates, napping 07:00 to 16:00 (9 h) at the night rates.
        var fullness = 15 * rate[PetMeter.Fullness] + 9 * profile.NightFullness;
        var exercise = 15 * rate[PetMeter.Exercise] + 9 * profile.NightComfort;
        var bedding = 15 * rate[PetMeter.Bedding] + 9 * profile.NightCleanliness;
        Near(43.2, fullness, "a day costs 43.2 Fullness");
        Equal(true, fullness <= species.FindVerb(PetVerb.Bowl).Effects.Single().Delta, "one dinner (45) covers a day");
        Near(40.5, exercise, "a day costs 40.5 Exercise");
        Equal(true, exercise <= 30 + 12, "one wheel spin and one hold cover it");
        Near(37.2, bedding, "a day costs 37.2 Bedding");
        Equal(true, bedding <= 40, "one fluff covers it");

        var sim = Sim();
        var hamster = sim.Adopt("Pip", Syrian, At(2, 18, 0));
        var lowest = 100.0;
        var lowestDinner = 100.0;
        var highestDinner = 0.0;
        var at = At(2, 18, 0);
        for (var day = 3; day <= 16; day++)
        {
            foreach (var (hour, minute, verbs) in Attentive)
            {
                at = At(day, hour, minute);
                hamster = sim.Step(hamster, at).State;
                if (day > 3)
                    lowest = Math.Min(lowest, Lowest(hamster));
                foreach (var verb in verbs)
                {
                    if (verb == PetVerb.Bowl && day > 3)
                    {
                        lowestDinner = Math.Min(lowestDinner, hamster.Meter(PetMeter.Fullness));
                        highestDinner = Math.Max(highestDinner, hamster.Meter(PetMeter.Fullness));
                    }

                    var step = sim.Care(hamster, verb);
                    Equal(true, step.Cue != PetCue.TooFull, $"day {day}: dinner is never overfeeding");
                    hamster = step.State;
                }
            }
        }

        Equal(true, lowest >= 40, $"no need falls below 40 for an attentive player (lowest {lowest:0.0})");
        Equal(CareMood.Content, hamster.Care.Mood, "and the hamster stays content");
        Equal(true, lowestDinner >= 45 && highestDinner < Tunables.FullMealThreshold,
            $"dinner finds it hungry but never full ({lowestDinner:0.0} to {highestDinner:0.0})");

        // A missed dinner: fidgety and hungry, never grumpy by the next evening.
        var skipped = Hamster(At(3, 18, 0), fullness: 55);
        skipped = sim.Care(skipped, PetVerb.GentleHold).State;
        skipped = sim.Step(skipped, At(3, 21, 45)).State;
        Equal(CareMood.Fussy, skipped.Care.Mood, "a missed dinner makes it fidgety");
        for (var hour = 22; hour <= 40; hour += 2)
            skipped = sim.Step(skipped, At(3, 0, 0).AddHours(hour)).State;
        Equal(true, skipped.Meter(PetMeter.Fullness) >= 15, $"but never starving within a day ({skipped.Meter(PetMeter.Fullness):0.0})");
        Console.WriteLine($"  hamster one-meal tuning: a day costs Fullness {fullness:0.0} (dinner +45), Exercise {exercise:0.0} " +
            $"(wheel +30, hold +12), Bedding {bedding:0.0} (fluff +40); attentive player lowest need {lowest:0.0}, " +
            $"dinner at Fullness {lowestDinner:0.0} to {highestDinner:0.0}; a missed dinner leaves {skipped.Meter(PetMeter.Fullness):0.0} " +
            "by the next afternoon");
    }

    private static void WheelAndBedding()
    {
        var sim = Sim();
        var hamster = Hamster(At(3, 17, 30));
        var ran = sim.Care(hamster, PetVerb.Wheel).State;
        Equal(CareReason.PlayOnTime, ran.Care.LastReason, "wheel time at 17:30 is on time");
        Equal(MomentStatus.OnTime, ran.Care.StatusOf(CareMoment.Afternoon), "the wheel window is done");
        Equal("O", ran.Care.TogetherDays.ToCode(), "it keeps the together ring");
        Near(100, ran.Meter(PetMeter.Exercise), "the wheel gives Exercise +30 (capped)");
        Near(70, ran.Meter(PetMeter.Energy), "and costs Energy 10");
        var held = sim.Care(hamster, PetVerb.GentleHold).State;
        Equal(MomentStatus.Pending, held.Care.StatusOf(CareMoment.Afternoon), "a hold is together time, not the wheel");
        Equal(true, held.Care.TogetherToday, "but counts for the day");
        Near(92, held.Meter(PetMeter.Exercise), "a hold lets it scramble hand to hand (+12)");
        var missed = sim.Step(held, At(3, 21, 15)).State;
        Equal(MomentStatus.Missed, missed.Care.StatusOf(CareMoment.Afternoon), "no wheel by 21:00 misses it");
        Equal(CareTags.None, missed.Care.Owed & ~CareTags.Meal, "wheel time is never owed");

        var tidy = sim.Care(Hamster(At(3, 8, 0)), PetVerb.FluffBedding).State;
        Equal(CareReason.WashOnTime, tidy.Care.LastReason, "fluffing the bedding is the daily tidy");
        Near(100, tidy.Meter(PetMeter.Bedding), "fresh bedding (+40, capped)");
        var messy = sim.Step(sim.Care(Hamster(At(3, 17, 0)), PetVerb.Bowl).State, At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, messy.Care.Wash, "no tidy by the evening misses it");
        Equal(1, messy.Care.Mess, "and leaves messy bedding");
        var exerciseBefore = messy.Meter(PetMeter.Exercise);
        Near(Math.Min(100, exerciseBefore + 12 * 0.95), sim.Care(messy, PetVerb.GentleHold).State.Meter(PetMeter.Exercise),
            "messy bedding cuts the hold's gain by 5%");
        Equal(0, sim.Care(messy, PetVerb.FluffBedding).State.Care.Mess, "fluffing tidies it");
    }

    private static void Moods()
    {
        var sim = Sim();
        var at = At(3, 18, 0);
        var stiff = Hamster(at, exercise: 30, bond: 30);
        var fidgety = sim.Step(stiff, at.AddMinutes(5)).State;
        Equal(CareMood.Fussy, fidgety.Care.Mood, "Exercise below 35 makes the hamster fidgety (Restless)");
        Equal(CareMoodCause.LowNeed, fidgety.Care.MoodCause, "because a need is low");
        var answered = sim.Care(sim.Step(fidgety, at.AddMinutes(20)).State, PetVerb.Wheel);
        Equal(CareMood.Content, answered.State.Care.Mood, "the wheel settles it");
        Near(fidgety.Bond + 2 + 5, answered.State.Bond, "wheel time +2 and answering within half an hour +5");

        var hungry = sim.Step(Hamster(at, fullness: 20, exercise: 60, bond: 30), at.AddMinutes(5)).State;
        var grumpy = hungry;
        for (var minutes = 30; grumpy.Care.Mood != CareMood.Upset && minutes <= 300; minutes += 20)
            grumpy = sim.Step(grumpy, at.AddMinutes(minutes)).State;
        Equal(CareMood.Upset, grumpy.Care.Mood, "fidgeting left unanswered turns grumpy");
        var refused = sim.Care(grumpy, PetVerb.Wheel);
        Equal(PetCue.PushesToyAway, refused.Cue, "a grumpy hamster ignores the wheel");
        Equal(grumpy.Meter(PetMeter.Energy), refused.State.Meter(PetMeter.Energy), "and the wheel gives nothing");
        var fed = sim.Care(grumpy, PetVerb.Bowl).State;
        Equal(CareMood.Upset, fed.Care.Mood, "dinner alone does not calm a grumpy hamster");
        var held = sim.Care(fed, PetVerb.GentleHold).State;
        Equal(true, held.Care.Mood < CareMood.Upset, "a gentle hold once fed calms it");
    }

    private static void Overfeeding()
    {
        var sim = Sim();
        var full = Hamster(At(3, 18, 0), fullness: 95, exercise: 70, bedding: 80);
        var over = sim.Care(full, PetVerb.Bowl);
        Equal(PetCue.TooFull, over.Cue, "filling a full bowl is overfeeding");
        Equal(CareReason.MealTooFull, over.State.Care.LastReason, "and is not dinner");
        Equal(MomentStatus.Pending, over.State.Care.StatusOf(CareMoment.Evening), "dinner still waits");
        Near(60, over.State.Meter(PetMeter.Exercise), "it feels sluggish (Exercise -10)");
        Near(65, over.State.Meter(PetMeter.Bedding), "and stashes the extra in its bedding (Bedding -15)");
        Equal(full.Bond, over.State.Bond, "overfeeding builds no Bond");
        Equal(true, sim.IsFull(full), "IsFull warns before it happens");
        Equal(false, sim.IsFull(Hamster(At(3, 18, 0), fullness: 89)), "89 is not full");
    }

    private static void Away()
    {
        var sim = Sim();
        var hamster = sim.Care(Hamster(At(3, 18, 0), bond: 40), PetVerb.Bowl).State;
        var away = sim.StartAway(hamster, TimeSpan.FromDays(3));
        Equal(true, away.Care.IsAway, "Away mode starts");
        var later = sim.Step(away, At(5, 22, 0)).State;
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Evening), "dinner is covered while away");
        Equal("O", later.Care.Meals.ToCode(), "no dinner is missed while away");
        Equal(CareMood.Content, later.Care.Mood, "the hamster stays content");
        Equal(true, later.Meter(PetMeter.Fullness) >= 60 - 1e-9, "needs are held at 60");
        Equal(true, later.Meter(PetMeter.Exercise) >= 60 - 1e-9, "Exercise too");
        Equal(true, later.Bond >= hamster.Bond, "Away costs no Bond");
        Equal(SampleMode.Away, later.Development.Sample.Mode, "development keeps the nominal pace");
        var back = sim.Care(later, PetVerb.GentleHold).State;
        Equal(false, back.Care.IsAway, "any care ends Away mode");
    }

    private static void HealthAndVet()
    {
        var sim = Sim();
        var at = At(3, 18, 0);
        var poorly = Hamster(at, fullness: 10, health: 35);
        var unwell = sim.Step(poorly, at.AddMinutes(30)).State;
        Equal(CareCondition.UnderTheWeather, unwell.Care.Condition, "Health below 50 puts the hamster under the weather");
        Equal(true, PetSimulation.CanVisitVet(unwell), "the vet is offered");
        var sick = unwell;
        for (var minutes = 40; sick.Care.Condition != CareCondition.Sick && minutes <= 90; minutes += 5)
            sick = sim.Step(sick, at.AddMinutes(minutes)).State;
        Equal(CareCondition.Sick, sick.Care.Condition, "while present, Health below 30 makes it sick");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.Wheel).Cue, "a sick hamster is too poorly for the wheel");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.FluffBedding).Cue, "or a bedding change around it");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.GentleHold).Cue, "but likes a gentle hold");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.Bowl).Cue, "and eats");

        var visit = sim.VisitVet(sick);
        Equal(PetCue.VetVisited, visit.Cue, "Dr. Rosa's visit");
        Equal(CareCondition.Recovering, visit.State.Care.Condition, "a sick hamster starts recovering");
        Near(sick.Health + 15, visit.State.Health, "the vet restores Health +15 at once");
        Equal(MedicineState.Ready, PetSimulation.Medicine(visit.State), "dinner time is a medicine window");
        var first = sim.GiveMedicine(visit.State);
        Equal(PetCue.MedicineTaken, first.Cue, "the first dose, with dinner");
        Equal(MedicineState.GivenThisMeal, PetSimulation.Medicine(first.State), "one dose per meal");
        var morning = sim.Step(first.State, At(4, 9, 0)).State;
        Equal(MedicineState.WaitForMeal, PetSimulation.Medicine(morning), "a hamster has no breakfast: the next dose waits for dinner");
        var dinner = sim.Step(morning, At(4, 18, 0)).State;
        var second = sim.GiveMedicine(dinner);
        Equal(PetCue.MedicineDone, second.Cue, "two doses on two evenings end Recovering");
        Equal(CareCondition.Well, second.State.Care.Condition, "the hamster is well again");
        Equal(true, second.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Poorly),
            "the journal keeps a hard day we got through");
        Equal(PetCue.VetAllClear, sim.VisitVet(Hamster(at)).Cue, "a well hamster needs no vet");

        var left = sim.Step(poorly, At(5, 18, 0)).State;
        Equal(true, left.Care.Condition != CareCondition.Sick, "an absence never passes under the weather");
        Equal(true, left.Health >= CareConsequences.HealthFloor, "Health stays at the floor or above");
    }

    private static void MilestoneTiming()
    {
        var profile = PetCareProfile.Hamster;
        var growth = new BabyGrowth(profile.SkillAges.ToArray(), profile.Skills.ToArray(), Tunables, GrowthTunables.Default,
            BabyNeeds.NeedRates.Baby);
        var rows = new List<string>();
        var previous = new TimeSpan[3];
        foreach (var (name, quality) in new[] { ("neglect", 0.10), ("average", 0.57), ("excellent", 0.94) })
        {
            var sample = new TrackSample(SampleMode.Normal, 0.5, quality, quality, quality, quality);
            var ages = growth.Schedule(sample, 1, TimeSpan.FromHours(1));
            for (var i = 0; i < 3; i++)
            {
                var nominal = profile.SkillAges[i].TotalDays;
                Equal(true, ages[i].TotalDays >= 0.8 * nominal - 0.05 && ages[i].TotalDays <= 1.4 * nominal + 0.05,
                    $"{name} {profile.Skills[i]} lands within 0.8x and 1.4x");
                if (previous[i] != TimeSpan.Zero)
                    Equal(true, ages[i] < previous[i], $"better care brings {profile.Skills[i]} sooner");
            }

            previous = ages;
            rows.Add($"{name} {ages[0].TotalDays:0.0} / {ages[1].TotalDays:0.0} / {ages[2].TotalDays:0.0}");
        }

        var attentive = Play(Attentive, 11);
        var busy = Play(Busy, 11);
        var neglected = Play(Neglect, 11);
        for (var i = 0; i < 3; i++)
        {
            var nominal = profile.SkillAges[i].TotalDays;
            foreach (var (name, ages) in new[] { ("attentive", attentive), ("busy", busy), ("neglected", neglected) })
                Equal(true, ages[i] >= 0.8 * nominal - 0.05 && ages[i] <= 1.4 * nominal + 0.05, $"{name} {profile.Skills[i]} within bounds");
            // The first milestone falls mostly in the excused first day, so a late dinner and a lunchtime snack are within
            // an hour of each other there.
            Equal(true, attentive[i] <= busy[i] && busy[i] <= neglected[i] + (i == 0 ? 0.05 : 0), $"{profile.Skills[i]} follows the play style");
        }

        Equal(true, attentive[0] < neglected[0], "attentive care brings wheel running sooner than neglect");
        Console.WriteLine("  hamster milestones (wheel, tunnel, hand-tame): steady " + string.Join(", ", rows) +
            $"; scripted attentive {Days(attentive)}, busy {Days(busy)}, neglect {Days(neglected)}");
    }

    private static void Setback()
    {
        var sim = Sim();
        var hamster = sim.Adopt("Pip", Syrian, At(2, 18, 0));
        hamster = sim.PreviewStep(hamster, TimeSpan.FromDays(3)).State;
        Equal(true, hamster.HasReached(MotorSkill.Wheel), "the young hamster runs its wheel by day three");
        // Gentle holds (not dinner) keep the player around while three dinners are missed.
        // Day 3's dinner falls in the excused first day; days 4, 5 and 6 are missed.
        foreach (var day in new[] { 3, 4, 5, 6 })
            hamster = sim.Care(sim.Step(hamster, At(day, 19, 0)).State, PetVerb.GentleHold).State;
        var before = sim.Step(hamster, At(6, 21, 35)).State;
        hamster = sim.Step(before, At(6, 21, 55)).State;
        Equal(hamster.NewestSkill, hamster.ShySkill, "three dinners missed in a row while present: the newest skill goes shy");
        Equal(true, hamster.HasReached(hamster.ShySkill), "a shy skill stays reached");
        Equal(false, hamster.Shows(hamster.ShySkill), "but does not show");

        var shy = hamster.ShySkill;
        var evening = sim.Step(hamster, At(7, 18, 0)).State;
        var step = sim.Care(evening, PetVerb.GentleHold);
        step = sim.Care(step.State, PetVerb.GentleHold);
        Equal(shy, step.State.ShySkill, "still shy before the good day is complete");
        var back = sim.Care(step.State, PetVerb.Bowl);
        Equal(PetCue.SkillBack, back.Cue, "an on-time dinner and two holds bring it back");
        Equal(MotorSkill.None, back.State.ShySkill, "the skill shows again");
        Equal(true, back.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Shy &&
            entry.Skill == shy), "the journal remembers the shy spell");
    }

    private static void BondTrustRhythmJournal()
    {
        var sim = Sim();
        var hamster = Hamster(At(3, 18, 0), bond: 20);
        var fed = sim.Care(hamster, PetVerb.Bowl).State;
        Near(21, fed.Bond, "an on-time dinner +1");
        Near(24, sim.Care(fed, PetVerb.GentleHold).State.Bond, "a gentle hold +3");
        Near(23, sim.Care(fed, PetVerb.Wheel).State.Bond, "wheel time +2");
        var capped = fed;
        for (var i = 0; i < 10; i++)
            capped = sim.Care(capped, PetVerb.GentleHold).State;
        Near(32, capped.Bond, "gains stop at the daily cap of 12");
        Equal(capped.Bond, capped.HighestBond, "HighestBond follows Bond for the earned-breed goal");

        var missed = sim.Step(Hamster(At(3, 21, 0), bond: 50), At(3, 21, 45)).State;
        Equal(true, missed.Bond < 50, "a missed dinner costs Bond");
        var floor = sim.Step(Hamster(At(3, 18, 0), bond: 50), At(20, 18, 0)).State;
        Equal(true, floor.Bond >= 0.6 * 50 - 1e-9, "Bond never falls below 60% of its high-water mark");

        var attentive = Days(sim, Attentive, 9);
        var neglected = Days(sim, Neglect, 9);
        var bonding = sim.Bonding(attentive);
        Equal(true, bonding.Rhythm >= 5, $"a care day counts with dinner and together time; the nap is its own (rhythm {bonding.Rhythm})");
        Equal(true, sim.Trust(attentive) != SecurityPhase.Clingy, "attentive care builds Trust");
        Equal(true, bonding.Security > sim.Bonding(neglected).Security, "missed dinners cost Trust");
        Equal(true, attentive.Bond > neglected.Bond, "attentive care builds more Bond");
        Equal(0, sim.Bonding(neglected).Rhythm, "neglect has no rhythm");
        var journal = sim.Journal(attentive).Entries;
        Equal(JournalKind.Arrival, journal[0].Kind, "the journal starts with the arrival");
        Equal("Wheel,Tunnel,HandTame", string.Join(",", journal.Where(entry => entry.Kind == JournalKind.Milestone).Select(entry => entry.Skill)),
            "each milestone is in the journal");
        Equal(true, journal.Any(entry => entry.Kind == JournalKind.Sticker && entry.Count == 3), "a 3-day rhythm sticker");
        Equal(true, journal.Any(entry => entry.Kind == JournalKind.Sticker && entry.Count == 7), "and a 7-day one");
        Equal(true, journal.Any(entry => entry.Kind == JournalKind.Week && entry.Count == 1), "a page for the first week");

        var photos = attentive;
        var taken = 0;
        for (var i = 0; i < 4; i++)
        {
            var photo = sim.TakePhoto(photos, PhotoMoment.Napping);
            taken += photo.Taken ? 1 : 0;
            photos = photo.State;
        }

        Equal(3, taken, "three photos a day");
        var shot = sim.Journal(photos).Entries.Last(entry => entry.Kind == JournalKind.Photo);
        Equal(MotorSkill.HandTame, shot.Skill, "a photo keeps the stage shown");
        Equal(PhotoMoment.Napping, shot.Moment, "and the moment: a cosy day nap");
    }

    // The seed dispenser, the spare wheel and the self-cleaning litter corner: the cat's helpers in the hamster's roles.
    private static void Helpers()
    {
        var sim = Sim();
        var hamster = sim.StockFeeder(Hamster(At(3, 18, 0), fullness: 50, exercise: 42, bedding: 42));
        Equal(6, hamster.Care.Helpers.Stock, "the seed dispenser is stocked");
        var events = new List<HelperEvent>();
        var helped = sim.Step(hamster, At(4, 1, 0), HelperSettings.All, events).State;
        Equal(true, events.Any(e => e.Kind == HelperKind.Feeder), "the seed dispenser serves dinner");
        Equal(true, events.Any(e => e.Kind == HelperKind.Play), "the spare wheel keeps it running");
        Equal(true, events.Any(e => e.Kind == HelperKind.Tidy), "the litter corner keeps the bedding fresh");
        Equal(MomentStatus.Helped, helped.Care.StatusOf(CareMoment.Evening), "dinner from the dispenser is Helped");
        Equal(true, helped.Meter(PetMeter.Exercise) >= HelperRules.Default.PlayFloor - 1e-9, "Exercise is held at the play floor");
        Equal(true, helped.Meter(PetMeter.Bedding) >= HelperRules.Default.TidyFloor - 1e-9, "Bedding is held at the tidy floor");
        var plain = sim.Step(hamster, At(4, 1, 0)).State;
        Near(plain.Bond, helped.Bond, "helpers never build Bond");
        Equal(plain.Milestones, helped.Milestones, "or milestones");
        var payload = PetSaveCodec.Encode(helped);
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload, Catalog)), "the hamster's helpers round-trip");
        // Helpers never act in the nap: a day of school hours stays the hamster's own.
        events.Clear();
        sim.Step(Hamster(At(3, 8, 0), fullness: 80, exercise: 80, bedding: 80), At(3, 15, 0), HelperSettings.All, events);
        Equal(0, events.Count, "nothing to do while it naps with its needs met");
    }

    private static void PreviewDay()
    {
        var sim = Sim();
        var hamster = sim.Adopt("Pip", Syrian, At(3, 18, 0));
        foreach (var verb in new[] { PetVerb.Bowl, PetVerb.Wheel, PetVerb.FluffBedding })
            hamster = sim.Care(hamster, verb).State;
        var next = sim.PreviewStep(hamster, TimeSpan.FromDays(1)).State;
        Equal(hamster.TimeCheckpoint, next.TimeCheckpoint, "a preview day passes no real time");
        Equal(hamster.Care, next.Care, "the ledger holds");
        Equal(1, sim.Bonding(next).Rhythm, "dinner and wheel time count a preview day, with no nap asked");
        Equal(hamster.Age + TimeSpan.FromDays(1), next.Age, "a day older");
        var later = next;
        for (var i = 0; i < 2; i++)
            later = sim.PreviewStep(later, TimeSpan.FromDays(1)).State;
        Equal(true, later.HasReached(MotorSkill.Wheel), "three preview days bring wheel running");
    }

    private static void SplitAdvances()
    {
        var sim = Sim();
        var hamster = sim.Adopt("Pip", Panda, At(2, 18, 0));
        hamster = Run(sim, hamster, Attentive, At(2, 18, 0), At(4, 6, 30));
        // Into the day nap (quiet hours inside one day), out of it through the evening windows, and into the night; each
        // gap stays under the 8-hour cap, which is path dependent on purpose.
        foreach (var (from, to, steps) in new[]
                 {
                     (At(4, 6, 30), At(4, 14, 0), 14), (At(4, 17, 30), At(5, 1, 0), 15), (At(5, 1, 0), At(5, 8, 30), 12),
                 })
        {
            var start = from == At(4, 6, 30) ? hamster : Run(sim, hamster, Attentive, At(4, 6, 30), from);
            var whole = sim.Step(start, to).State;
            var split = start;
            for (var i = 1; i <= steps; i++)
                split = sim.Step(split, from + TimeSpan.FromTicks((to - from).Ticks * i / steps)).State;
            var name = $"{from:HH:mm} to {to:HH:mm}";
            foreach (var pair in whole.Meters)
                Near(pair.Value, split.Meter(pair.Key), $"{name}: {pair.Key} is the same split or whole", 1e-9);
            Equal(Ledger(whole.Care), Ledger(split.Care), $"{name}: the ledger is the same");
            Near(whole.Development.Readiness, split.Development.Readiness, $"{name}: readiness", 1e-9);
            Equal(whole.Milestones, split.Milestones, $"{name}: milestones");
            Near(whole.Bond, split.Bond, $"{name}: Bond", 1e-9);
            Equal(whole.Journal.Entries.Count, split.Journal.Entries.Count, $"{name}: the journal");
        }

        // Determinism: the same plan twice gives the same record.
        var again = Run(sim, sim.Adopt("Pip", Panda, At(2, 18, 0)), Attentive, At(2, 18, 0), At(4, 6, 30));
        Equal(PetSaveCodec.Encode(hamster), PetSaveCodec.Encode(again), "the same care gives the same hamster");
    }

    // A new species, not a migration: a hamster saves as a v2 record from adoption, round-trips byte for byte, keeps the
    // hamster's routine, and a hamster record written before its care profile (v1, the simple rules) is seeded on first
    // use like any v1 pet.
    private static void Saves()
    {
        var sim = Sim();
        var built = sim.Care(sim.Step(sim.Adopt("Pip", Syrian, At(2, 18, 0)), At(4, 18, 30)).State, PetVerb.Bowl).State;
        var payload = PetSaveCodec.Encode(built);
        Equal(true, payload.StartsWith("<pet version=\"2\"><name>Pip</name><species>4</species><breed>500</breed>", StringComparison.Ordinal),
            "a hamster saves as a v2 pet record");
        Equal(true, payload.Contains("<meter id=\"5\"") && payload.Contains("<meter id=\"6\""), "with its Exercise and Bedding");
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload)), "and round-trips byte for byte");
        var decoded = PetSaveCodec.Decode(payload);
        Equal(CareRoutine.Hamster, decoded.Care.Routine, "the ledger follows the hamster's routine");
        Equal(built.Milestones, decoded.Milestones, "milestones are kept");
        ExpectInvalid(payload.Replace(" milestones=\"" + built.Milestones + "\"", " milestones=\"4\""), "more milestones than a hamster has");
        ExpectInvalid(payload.Replace("<breed>500</breed>", "<breed>101</breed>"), "a cat breed is not a hamster");

        const string v1 =
            "<pet version=\"1\"><name>Nib</name><species>4</species><breed>500</breed>" +
            "<appearance coat=\"6\" accessory=\"0\" accent=\"0\" />" +
            "<checkpoint>639028224000000000</checkpoint><age>3888000000000</age>" +
            "<meters><meter id=\"0\" value=\"56.5\" /><meter id=\"1\" value=\"66.2\" /><meter id=\"5\" value=\"63.2\" />" +
            "<meter id=\"6\" value=\"84\" /><meter id=\"4\" value=\"97\" /></meters>" +
            "<bond value=\"12\" highest=\"15\" /><personality lively=\"12\" bold=\"-3.5\" /><unlocks grown=\"false\" /></pet>";
        var old = PetSaveCodec.Decode(v1);
        Equal(2, old.Milestones, "a 4.5-day hamster saved with the simple rules has its first two milestones");
        Equal(true, PetSaveCodec.Encode(old).Contains("awaiting-anchor=\"true\""), "and a ledger waiting for its anchor");
        var anchored = sim.Step(old, new DateTimeOffset(639028224000000000, TimeSpan.Zero).AddDays(3)).State;
        Equal("", anchored.Care.Meals.ToCode(), "its first advance excuses the gap");
        Equal(12.0, anchored.Bond, "and costs no Bond");

        // Cats are unchanged: the cat's golden record still decodes as a cat.
        var cat = sim.Adopt("Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), At(3, 8, 0));
        Equal(CareRoutine.Cat, PetSaveCodec.Decode(PetSaveCodec.Encode(cat)).Care.Routine, "a cat keeps the cat's routine");
    }

    private static void Breeds()
    {
        var breeds = Catalog.BreedsOf(Species.Hamster);
        Equal("SyrianGolden:Free,WinterWhite:Earned,Roborovski:Earned,TeddyBear:Premium,Panda:Premium,Sapphire:Premium,Campbells:Premium",
            string.Join(",", breeds.Select(breed => breed.Breed + ":" + breed.Unlock)), "the hamster's breed ladder");
        Equal(true, breeds.All(breed => breed.AvailableNow), "every hamster breed's art ships with P2");
        Equal("500,501,502,503,504,505,506", string.Join(",", breeds.Select(breed => (int)breed.Breed)), "in the hamster block");
        Equal(EarnedGoal.FirstFullCareDay, Catalog.Get(PetBreed.WinterWhite).Goal, "Winter white: a first full care day");
        Equal(EarnedGoal.Bond40, Catalog.Get(PetBreed.Roborovski).Goal, "Roborovski: Bond 40");
        Equal("breed.hamster.teddy_bear,breed.hamster.panda,breed.hamster.sapphire,breed.hamster.campbells",
            string.Join(",", breeds.Where(breed => breed.Unlock == BreedUnlock.Premium).Select(breed => breed.ProductId)),
            "premium product IDs");
        Equal(true, Catalog.GrantsBreed("breedpack.hamster", PetBreed.Sapphire), "the hamster breed pack");
        Equal(false, Catalog.GrantsBreed("breedpack.cat", PetBreed.Sapphire), "not the cat's");
        Equal(true, Catalog.GrantsBreed(PetIds.AllBreedsPackProductId, PetBreed.TeddyBear), "and the every-breed pack");
        foreach (var product in new[] { "breed.hamster.teddy_bear", "breed.hamster.panda", "breed.hamster.sapphire",
                     "breed.hamster.campbells", "breedpack.hamster" })
            Equal(true, DeveloperUnlocks.Products(Catalog).Contains(product), $"developer mode grants {product}");
        var species = Catalog.Get(Species.Hamster);
        foreach (var breed in breeds)
            Near(0, breed.NetDecayChangePerHour(species), $"{breed.Breed} is no harder or easier", 1e-9);
        Catalog.Validate();

        var none = SpeciesGoalProgress.None;
        Equal("SyrianGolden", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Hamster, none, new string[0])),
            "a new household adopts the Syrian golden");
        var earned = new SpeciesGoalProgress(1, 40, false, 0);
        Equal("SyrianGolden,WinterWhite,Roborovski", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Hamster, earned, new string[0])),
            "care earns the Winter white and the Roborovski");
        Equal("SyrianGolden,TeddyBear,Panda,Sapphire,Campbells", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Hamster, none,
            new[] { "breedpack.hamster" })), "the pack opens the premium hamsters, the Teddy bear included");

        var days = PetCareDays.None;
        Equal((1, false), PetCareDays.RuleFor(Species.Hamster), "a hamster's care day: one dinner, no bedtime");
        Equal((2, true), PetCareDays.RuleFor(Species.Cat), "a cat's is unchanged");
        Equal((2, true), PetCareDays.RuleFor(Species.Dog), "and so is every other species'");
        var day = PetCareDays.DayNumber(At(3, 18, 0));
        days = days.Record(PetCareKind.Meal, day, 1, false).Days;
        var full = days.Record(PetCareKind.Together, day, 1, false);
        Equal(true, full.Completed, "dinner and together time make a hamster's full care day");
        Equal(1, full.Days.FullDays, "counted once");
    }

    private static void Household()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8), TimeSpan.FromDays(7), TimeSpan.FromDays(14));
        var pets = Sim();
        var now = At(3, 17, 0);
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        Equal(true, household.CanAdoptBreed(PetBreed.SyrianGolden), "the Syrian golden is adoptable at once");
        Equal(false, household.CanAdoptBreed(PetBreed.WinterWhite), "the Winter white is earned");
        Equal(false, household.CanAdoptBreed(PetBreed.TeddyBear), "the Teddy bear is premium");
        household.AdoptPet("Pip", Syrian);
        now = At(4, 17, 30);
        var dinner = household.ApplyPetCare(2, PetVerb.Bowl);
        Equal(PetCue.Content, dinner.Cue, "dinner in the household");
        Equal(false, dinner.CompletedCareDay, "not yet a full day");
        var hold = household.ApplyPetCare(2, PetVerb.GentleHold);
        Equal(true, hold.CompletedCareDay, "dinner and a gentle hold complete a hamster's care day");
        Equal("WinterWhite", string.Join(",", hold.UnlockedBreeds), "and earn the Winter white");
        Equal(true, household.CanAdoptBreed(PetBreed.WinterWhite), "which can now be adopted");
        household.ApplyPetCare(2, PetVerb.Wheel);
        household.ApplyPetCare(2, PetVerb.FluffBedding);

        now = At(5, 10, 0);
        household.AdvanceAll();
        var napping = household.ApplyPetCare(2, PetVerb.Wheel);
        Equal(PetCue.Napping, napping.Cue, "the household lets a napping hamster sleep");
        Equal(false, napping.CompletedCareDay, "a wheel it slept through counts for nothing");
        Equal(PetCue.VetAllClear, household.VisitPetVet(2).Cue, "the vet is there for a hamster too");

        // A young hamster's milestones wait as celebrations in their own manifest element.
        now = At(12, 18, 0);
        household.AdvanceAll(activeOnScreen: false);
        var manifest = System.Xml.Linq.XElement.Parse(store.ManifestPrimary);
        Equal(true, manifest.Element("hamster-celebrations").Elements().Count() >= 1, "milestones wait in hamster-celebrations");
        Equal(null, manifest.Element("pet-celebrations"), "never where an older build reads a cat's");
        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        var played = reopened.TakeCelebrations(2);
        Equal(true, played.Contains(CompanionMilestone.Wheel) && played.Contains(CompanionMilestone.Grown),
            "they survive a reload: wheel running and growing up");
        Equal(true, played.ToList().IndexOf(CompanionMilestone.Wheel) < played.ToList().IndexOf(CompanionMilestone.Grown), "milestones before growing up");
    }

    // Scripted players: (hour, minute, verbs) visits each day. An attentive player looks in before school and cares in
    // the evening; a busy one only manages a late dinner; neglect is a lunchtime snack.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Attentive =
    {
        (7, 30, new[] { PetVerb.GentleHold }),
        (17, 30, new[] { PetVerb.Bowl, PetVerb.Wheel, PetVerb.FluffBedding }),
        (19, 30, new[] { PetVerb.GentleHold }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Busy =
    {
        (20, 45, new[] { PetVerb.Bowl, PetVerb.GentleHold }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Neglect =
    {
        (12, 30, new[] { PetVerb.Bowl }),
    };

    private static double Lowest(PetState state) =>
        new[] { PetMeter.Fullness, PetMeter.Exercise, PetMeter.Energy, PetMeter.Bedding }.Min(state.Meter);

    private static double[] Play((int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var sim = Sim();
        var adopted = At(2, 17, 15);
        var hamster = Run(sim, sim.Adopt("Pip", Syrian, adopted), plan, adopted, adopted.AddDays(days));
        var ages = new double[3];
        foreach (var entry in sim.Journal(hamster).Entries.Where(entry => entry.Kind == JournalKind.Milestone))
            ages[PetCareProfile.Hamster.IndexOf(entry.Skill)] = (entry.At - adopted).TotalDays;
        return ages;
    }

    private static PetState Days(PetSimulation sim, (int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var adopted = At(2, 17, 15);
        return Run(sim, sim.Adopt("Pip", Syrian, adopted), plan, adopted, adopted.AddDays(days));
    }

    private static PetState Run(PetSimulation sim, PetState hamster, (int Hour, int Minute, PetVerb[] Verbs)[] plan,
        DateTimeOffset from, DateTimeOffset until)
    {
        for (var day = from.Date; day <= until.Date; day = day.AddDays(1))
        {
            foreach (var (hour, minute, verbs) in plan)
            {
                var at = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, from.Offset);
                if (at <= from || at > until)
                    continue;
                hamster = sim.Step(hamster, at).State;
                foreach (var verb in verbs)
                    hamster = sim.Care(hamster, verb).State;
            }
        }

        return sim.Step(hamster, until).State;
    }

    private static string Ledger(CareRecord care)
    {
        var element = CareRecordCodec.Encode(care);
        element.Attribute("session-start").Remove();
        element.Attribute("mood-since").Remove();
        return element.ToString();
    }

    private static string Days(double[] ages) => $"{ages[0]:0.0} / {ages[1]:0.0} / {ages[2]:0.0}";

    private static PetSimulation Sim() => new PetSimulation();

    // A hamster on an anchored ledger whose windows all count, at the given needs.
    private static PetState Hamster(DateTimeOffset at, double fullness = 80, double exercise = 80, double energy = 80,
        double bedding = 80, double health = 100, double bond = 20)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Hamster).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = fullness, [PetMeter.Energy] = energy, [PetMeter.Exercise] = exercise,
            [PetMeter.Bedding] = bedding, [PetMeter.Health] = health,
        };
        return PetState.Restore(Catalog, "Pip", Syrian, at, TimeSpan.FromDays(1), meters, bond, bond, 0, 0, false,
            draft.Build(), null, null, null, 0);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new DateTimeOffset(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

    private static string Describe(IReadOnlyList<CareWindow> windows) =>
        string.Join(",", windows.Select(window =>
            $"{(window.Nap ? "Nap" : window.Core.ToString())} {window.Open:hh\\:mm}-{window.Due:hh\\:mm}-{window.LateUntil:hh\\:mm}"));

    private static void ExpectInvalid(string payload, string description)
    {
        try
        {
            PetSaveCodec.Decode(payload);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected InvalidDataException");
    }

    private static void Near(double expected, double actual, string description, double tolerance = 1e-9)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }
}
