using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// P3 Dog: the third species on the care profile mechanism (care slice 7). Two meals and two walks a day that never punish
// school hours (an early walk that closes with grace when school gets in the way, an evening walk after school), need
// rates tuned for them, restless and grumpy moods, overfeeding, Away mode (a neighbour walks the dog), the vet and
// medicine, the milestones Fetch, Leash walk and Agility, the setback, Bond, Trust, the care rhythm and the journal,
// tricks learned through familiarity, the reunion greeting scaled to time away, helpers that never walk the dog, earned and
// premium breeds (the Corgi is premium), adoption, saves and split advances. See docs/pet-mode.md and
// docs/care-and-growth.md.
static class DogCareChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly PetCatalog Catalog = PetCatalog.Default;
    private static readonly PetAppearance Mixed = PetAppearance.Default(Catalog, PetBreed.MixedBreed);
    // A premium breed with tendencies of its own; the species' rates are checked on the catalog.
    private static readonly PetAppearance Corgi = PetAppearance.Default(Catalog, PetBreed.Corgi);

    public static void Run()
    {
        Profile();
        RoutineWindows();
        Walks();
        TwoMealsTwoWalksTuning();
        Moods();
        Overfeeding();
        Away();
        HealthAndVet();
        MilestoneTiming();
        Setback();
        BondTrustRhythmJournal();
        Tricks();
        Reunion();
        Helpers();
        PreviewDay();
        SplitAdvances();
        Saves();
        Breeds();
        Household();
        Console.WriteLine("Dog care checks passed.");
    }

    private static void Profile()
    {
        var dog = PetCareProfile.Dog;
        Equal(dog, PetCareProfile.For(Species.Dog), "the dog has a care profile");
        Equal(CareRoutine.Dog, dog.Routine, "and its own routine");
        Equal(CareTags.Meal, dog.TagsFor(PetVerb.Feed), "feeding is the meal");
        Equal(CareTags.Together | CareTags.Walk | CareTags.Comfort, dog.TagsFor(PetVerb.Walk),
            "a walk answers the walk windows and comforts");
        Equal(CareTags.Together | CareTags.Play, dog.TagsFor(PetVerb.Fetch), "fetch is play");
        Equal(CareTags.Hygiene, dog.TagsFor(PetVerb.Brush), "the brush is the daily wash");
        Equal(CareTags.Bedtime, dog.TagsFor(PetVerb.Rest), "rest is the bedtime");
        Equal(CareTags.Medicine, dog.Profile.TagsFor("medicine"), "medicine is a care verb of the profile");
        foreach (var verb in Catalog.Get(Species.Dog).Verbs)
            Equal(true, dog.Offers(verb.Verb), $"the profile maps the dog's {verb.Verb}");
        Equal(PetMeter.Exercise, dog.ComfortMeter, "Exercise plays the comfort role");
        Equal(PetMeter.Cleanliness, dog.CleanlinessMeter, "Cleanliness is its own");
        Equal(false, dog.RestsAlone, "a dog sleeps at night");
        Equal(2, dog.MealsPerDay, "two meals a day");
        Equal(false, dog.NapsByDay, "no day nap");
        Equal<EngagementKind?>(EngagementKind.PlayWand, dog.KindOf(PetVerb.Walk, false), "a walk feeds Motor like play");
        Equal<EngagementKind?>(EngagementKind.Fetch, dog.KindOf(PetVerb.Fetch, false), "fetch feeds Motor and Learning");
        Equal<EngagementKind?>(EngagementKind.Hygiene, dog.KindOf(PetVerb.Brush, false), "brushing is grooming");
        Equal<EngagementKind?>(EngagementKind.Meal, dog.KindOf(PetVerb.Feed, true), "an on-time meal is a meal");
        Equal(CareCategories.Play, dog.CategoryOf(PetVerb.Walk), "a walk is play for the day's variety");
        Equal(CareCategories.Play, dog.CategoryOf(PetVerb.Fetch), "and so is fetch");
        Equal(3.0, DevelopmentRules.BasePoints(EngagementKind.Fetch, DevelopmentTrack.Motor), "fetch: Motor 3");
        Equal(3.0, DevelopmentRules.BasePoints(EngagementKind.Fetch, DevelopmentTrack.Learning), "and Learning 3 (tricks)");
        Equal<PetVerb?>(PetVerb.Fetch, dog.TrickVerb, "fetch teaches tricks");
        Equal<PetVerb?>(null, PetCareProfile.Cat.TrickVerb, "a cat learns none");
        Equal(EngagementKind.PlayWand, PetCareProfile.Cat.PlayKind, "a cat's wand is unchanged");
        Equal("Fetch,LeashWalk,Agility", string.Join(",", dog.Skills), "the dog's milestones");
        Equal("3,6,9", string.Join(",", dog.SkillAges.Select(age => age.TotalDays)), "at nominal days 3, 6 and 9");
        Equal(2, dog.ReachedBy(TimeSpan.FromDays(7)), "milestones by age");
        Equal(TimeSpan.FromDays(10), Catalog.Get(Species.Dog).GrownAge, "a dog grows up on day 10");
    }

    private static void RoutineWindows()
    {
        var school = RoutineCalendar.Windows(CareRoutine.Dog, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal("Meal 07:00-10:00-12:00,Walk 15:30-19:00-20:00,Meal 18:00-20:30-21:30,Bedtime 19:30-21:30-22:30,Grace walk 07:00-09:00-10:00",
            Describe(school), "the dog's school day: breakfast, the evening walk, dinner, bed and the early walk");
        var home = RoutineCalendar.Windows(CareRoutine.Dog, RoutinePreset.Home, RoutineCalendar.DefaultWakeTime);
        Equal("Meal 07:00-09:30-11:00,Walk 15:00-18:00-19:00,Meal 17:30-19:30-20:30,Bedtime 19:00-20:30-21:30,Grace walk 07:00-09:30-10:30",
            Describe(home), "the Home preset");
        var late = RoutineCalendar.Windows(CareRoutine.Dog, RoutinePreset.SchoolDay, TimeSpan.FromHours(6));
        Equal("Meal 06:00-09:00-11:00,Walk 15:30-19:00-20:00,Meal 18:00-20:30-21:30,Bedtime 19:30-21:30-22:30,Grace walk 06:00-08:00-09:00",
            Describe(late), "the wake time moves breakfast and the early walk together");
        Equal(5, RoutineCalendar.MomentsOf(CareRoutine.Dog), "a dog's day has five moments");
        Equal(4, RoutineCalendar.MomentsOf(CareRoutine.Cat), "a cat's keeps four");
        Equal(4, RoutineCalendar.MomentsOf(CareRoutine.Baby), "and a baby's");
        Equal(4, RoutineCalendar.MomentsOf(CareRoutine.Hamster), "and a hamster's");
        Equal((TimeSpan.FromHours(21.5), TimeSpan.FromHours(7)), RoutineCalendar.Night(CareRoutine.Dog, RoutinePreset.SchoolDay,
            RoutineCalendar.DefaultWakeTime), "a dog sleeps from 21:30 to the wake time");
        Equal(2, RoutineCalendar.MealWindows(CareRoutine.Dog), "two meal windows");
        Equal(2, RoutineCalendar.GoodDayMeals(CareRoutine.Dog), "a good day asks for both");
        Equal(true, RoutineCalendar.HasPlayWindow(CareRoutine.Dog), "the walk windows keep the together ring");
        Equal(true, RoutineCalendar.HasWalks(CareRoutine.Dog), "a dog walks");
        Equal(false, RoutineCalendar.HasWalks(CareRoutine.Cat), "a cat does not");
        var wash = RoutineCalendar.WashWindow(CareRoutine.Dog, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal((TimeSpan.FromHours(7), TimeSpan.FromHours(20.5), TimeSpan.FromHours(21.5)), wash, "the brush: any time from morning until dinner");
        ExpectThrows<ArgumentOutOfRangeException>(() => CareRules.WindowKey(1, CareMoment.Dawn), "the early walk is never a keyed meal");
        ExpectThrows<ArgumentOutOfRangeException>(() => new CareWindow(CareMoment.Morning, CareTags.Meal, TimeSpan.FromHours(7),
            TimeSpan.FromHours(8), TimeSpan.FromHours(9), false, true), "a meal never closes with grace");
        Tunables.Validate();

        var sim = Sim();
        var dog = Dog(At(3, 7, 30));
        Equal("Walk:Due,MorningMeal:Due,Walk:Later,EveningMeal:Later,Bedtime:Later,Wash:Due",
            Strip(sim, dog), "the strip in time order: early walk, breakfast, evening walk, dinner, bed, brush");
        var fed = sim.Care(dog, PetVerb.Feed).State;
        Equal(CareReason.MealOnTime, fed.Care.LastReason, "breakfast at 7:30 is on time");
        var missed = sim.Step(Dog(At(3, 18, 0)), At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, missed.Care.StatusOf(CareMoment.Evening), "dinner is missed at 21:30");
        Equal(CareTags.Meal, missed.Care.Owed & CareTags.Meal, "a missed dinner is owed");
        Equal(CareMood.Fussy, missed.Care.Mood, "and the dog is restless");
        var adopted = sim.Adopt("Biscuit", Mixed, At(3, 16, 0));
        var first = sim.Step(adopted, At(3, 23, 0)).State;
        Equal(MomentStatus.Excused, first.Care.StatusOf(CareMoment.Evening), "the first day's windows are excused");
        Equal(MomentStatus.Excused, first.Care.StatusOf(CareMoment.Afternoon), "the evening walk too");
        Equal(5, first.Care.MomentCode.Length, "a dog's ledger keeps five moments");
    }

    // Two walks: an early one that closes with grace when school gets in the way, and an evening one that keeps the
    // together ring. Fetch is play, never a walk, and no helper ever walks the dog.
    private static void Walks()
    {
        var sim = Sim();
        var morning = Dog(At(3, 7, 15), bond: 30);
        var early = sim.Care(morning, PetVerb.Walk).State;
        Equal(CareReason.WalkOnTime, early.Care.LastReason, "the early walk at 7:15 is on time");
        Equal(MomentStatus.OnTime, early.Care.StatusOf(CareMoment.Dawn), "the early walk is done");
        Equal("O", early.Care.TogetherDays.ToCode(), "it keeps the together ring");
        Near(100, early.Meter(PetMeter.Exercise), "a walk gives Exercise +40 (capped)");
        Near(72, early.Meter(PetMeter.Energy), "costs Energy 8");
        Near(77, early.Meter(PetMeter.Cleanliness), "and muddy paws cost Cleanliness 3");
        var lateWalk = sim.Care(sim.Step(morning, At(3, 9, 30)).State, PetVerb.Walk).State;
        Equal(CareReason.WalkLate, lateWalk.Care.LastReason, "a walk after 9:00 is late");

        // School gets in the way: no early walk, and nothing is missed, owed or lost.
        var school = sim.Care(morning, PetVerb.Feed).State;
        var noon = sim.Step(school, At(3, 15, 0)).State;
        Equal(MomentStatus.Excused, noon.Care.StatusOf(CareMoment.Dawn), "the early walk closes with grace at 10:00");
        Equal("", noon.Care.TogetherDays.ToCode(), "it never enters the together ring");
        Equal(CareTags.None, noon.Care.Owed, "and is never owed");
        Equal(CareMood.Content, noon.Care.Mood, "the dog is content");
        Equal(true, noon.Bond >= school.Bond, "and it costs no Bond");

        var fetched = sim.Care(noon, PetVerb.Fetch).State;
        Equal(MomentStatus.Pending, fetched.Care.StatusOf(CareMoment.Afternoon), "fetch is a game, not a walk");
        Equal(false, noon.Care.TogetherToday, "nothing together yet before fetch");
        Equal(true, fetched.Care.TogetherToday, "but fetch counts as together time for the day");
        var outside = sim.Care(noon, PetVerb.Walk).State;
        Equal(CareReason.WalkOutsideWindow, outside.Care.LastReason, "a walk at 15:00 is outside both windows");
        Equal(true, outside.Meter(PetMeter.Exercise) > noon.Meter(PetMeter.Exercise), "but still a good run");

        var evening = sim.Care(sim.Step(noon, At(3, 16, 0)).State, PetVerb.Walk).State;
        Equal(CareReason.WalkOnTime, evening.Care.LastReason, "the evening walk at 16:00 is on time");
        Equal(MomentStatus.OnTime, evening.Care.StatusOf(CareMoment.Afternoon), "the evening walk is done");
        Equal("O", evening.Care.TogetherDays.ToCode(), "and keeps the ring");
        var noWalk = sim.Step(sim.Care(noon, PetVerb.Fetch).State, At(3, 20, 15)).State;
        Equal(MomentStatus.Missed, noWalk.Care.StatusOf(CareMoment.Afternoon), "no evening walk by 20:00 misses it");
        Equal("M", noWalk.Care.TogetherDays.ToCode(), "a missed together day");
        Equal(CareTags.None, noWalk.Care.Owed & ~CareTags.Meal, "a walk is never owed");

        // A day with breakfast, the evening walk, dinner and bed is a good day without the early walk.
        var day = sim.Care(Dog(At(3, 7, 30)), PetVerb.Feed).State;
        day = sim.Care(sim.Step(day, At(3, 16, 30)).State, PetVerb.Walk).State;
        day = sim.Care(sim.Step(day, At(3, 18, 30)).State, PetVerb.Feed).State;
        day = sim.Care(sim.Step(day, At(3, 20, 30)).State, PetVerb.Rest).State;
        var closed = sim.Step(day, At(4, 7, 0)).State;
        Equal(CareRules.WindowKey(0, CareMoment.Morning) > 0, true, "windows keep their keys");
        Equal(closed.Care.CareDay - 1, closed.Care.GoodDay, "a good day: the grace walk excuses nothing");
        Equal(1, sim.Bonding(closed).Rhythm, "and it counts for the care rhythm");

        // The early walk's grace never excuses a day away from the rhythm, and a whole day can be walked twice.
        var twice = sim.Care(Dog(At(3, 7, 15)), PetVerb.Walk).State;
        twice = sim.Care(sim.Step(twice, At(3, 16, 15)).State, PetVerb.Walk).State;
        Equal("OO", twice.Care.TogetherDays.ToCode(), "two walks, two marks");
    }

    // Two meals and two walks: the needs stay comfortable for a player who walks before school and after it.
    private static void TwoMealsTwoWalksTuning()
    {
        var species = Catalog.Get(Species.Dog);
        var rate = species.Meters.ToDictionary(meter => meter.Meter, meter => meter.DecayPerHour);
        var profile = PetCareProfile.Dog;
        // Awake 07:00 to 21:30 (14.5 h) at the awake rates, asleep 21:30 to 07:00 (9.5 h) at the night rates.
        var fullness = 14.5 * rate[PetMeter.Fullness] + 9.5 * profile.NightFullness;
        var exercise = 14.5 * rate[PetMeter.Exercise] + 9.5 * profile.NightComfort;
        var cleanliness = 14.5 * rate[PetMeter.Cleanliness] + 9.5 * profile.NightCleanliness + 2 * 3;
        var energy = 14.5 * rate[PetMeter.Energy] + 2 * 8 + 10;
        Near(72.25, fullness, "a day costs 72.25 Fullness");
        Equal(true, fullness <= 2 * species.FindVerb(PetVerb.Feed).Effects.Single().Delta, "two meals (40 each) cover a day");
        Near(48.25, exercise, "a day costs 48.25 Exercise");
        Equal(true, exercise <= 40 + 40, "two walks cover it");
        Equal(true, exercise <= 40 + 15, "and so do the evening walk and one game of fetch");
        Equal(true, exercise > 40, "the evening walk alone leaves the dog a little restless over days");
        Near(32.5, cleanliness, "a day costs 32.5 Cleanliness with two muddy walks");
        Equal(true, cleanliness <= 35, "one brush covers it");
        Near(55, energy, "a busy day spends 55 Energy before bed, and the night brings it back");

        var sim = Sim();
        var dog = sim.Adopt("Biscuit", Mixed, At(2, 18, 0));
        var lowest = 100.0;
        var lowestMeal = 100.0;
        var highestMeal = 0.0;
        for (var day = 3; day <= 16; day++)
        {
            foreach (var (hour, minute, verbs) in Attentive)
            {
                dog = sim.Step(dog, At(day, hour, minute)).State;
                if (day > 3)
                    lowest = Math.Min(lowest, Lowest(dog));
                foreach (var verb in verbs)
                {
                    if (verb == PetVerb.Feed && day > 3)
                    {
                        lowestMeal = Math.Min(lowestMeal, dog.Meter(PetMeter.Fullness));
                        highestMeal = Math.Max(highestMeal, dog.Meter(PetMeter.Fullness));
                    }

                    var step = sim.Care(dog, verb);
                    Equal(true, step.Cue != PetCue.TooFull, $"day {day}: a meal is never overfeeding");
                    dog = step.State;
                }
            }
        }

        Equal(true, lowest >= 40, $"no need falls below 40 for an attentive player (lowest {lowest:0.0})");
        Equal(CareMood.Content, dog.Care.Mood, "and the dog stays content");
        Equal(true, lowestMeal >= 45 && highestMeal < Tunables.FullMealThreshold,
            $"meals find it hungry but never full ({lowestMeal:0.0} to {highestMeal:0.0})");

        // A school-day player who only manages the evening walk keeps a content dog with a game of fetch.
        var evenings = Days(sim, SchoolDays, 10);
        Equal(CareMood.Content, evenings.Care.Mood, "the evening walk and fetch keep the dog content");
        var lowestEvening = Lowest(sim.Step(evenings, At(12, 16, 0)).State);
        Equal(true, lowestEvening >= 35, $"never restless before the evening walk ({lowestEvening:0.0})");

        // No walks at all (meals, the brush and bed on time): the dog grows restless, nose at the door, within days.
        var noWalks = sim.Adopt("Biscuit", Mixed, At(2, 7, 0));
        var restlessAt = 0.0;
        for (var hours = 24; hours <= 24 * 6 && restlessAt == 0; hours++)
        {
            noWalks = Run(sim, noWalks, NoWalks, At(2, 7, 0).AddHours(hours - 1), At(2, 7, 0).AddHours(hours));
            if (noWalks.Care.Mood != CareMood.Content)
                restlessAt = hours / 24.0;
        }

        Equal(CareMood.Fussy, noWalks.Care.Mood, "without walks the dog grows restless");
        Equal(CareMoodCause.LowNeed, noWalks.Care.MoodCause, "because Exercise is low");
        Equal(true, noWalks.Meter(PetMeter.Exercise) < 35, "Exercise is the low need");
        Console.WriteLine($"  dog two-meal two-walk tuning: a day costs Fullness {fullness:0.00} (meals +40 x2), Exercise " +
            $"{exercise:0.00} (walks +40 x2, fetch +15), Cleanliness {cleanliness:0.0} (brush +35), Energy {energy:0} by bedtime; " +
            $"attentive lowest need {lowest:0.0}, meals at Fullness {lowestMeal:0.0} to {highestMeal:0.0}; evening-only player " +
            $"lowest before the walk {lowestEvening:0.0}; no walks: restless on day {restlessAt:0.0}");
    }

    private static void Moods()
    {
        var sim = Sim();
        var at = At(3, 16, 0);
        var stiff = Dog(at, exercise: 30, bond: 30);
        var restless = sim.Step(stiff, at.AddMinutes(5)).State;
        Equal(CareMood.Fussy, restless.Care.Mood, "Exercise below 35 makes the dog restless, nose at the door");
        Equal(CareMoodCause.LowNeed, restless.Care.MoodCause, "because a need is low");
        var answered = sim.Care(sim.Step(restless, at.AddMinutes(20)).State, PetVerb.Walk);
        Equal(CareMood.Content, answered.State.Care.Mood, "a walk settles it");
        Near(restless.Bond + 3 + 5, answered.State.Bond, "a walk +3 and answering within half an hour +5");

        var hungry = sim.Step(Dog(at, fullness: 20, exercise: 60, bond: 30), at.AddMinutes(5)).State;
        var grumpy = hungry;
        for (var minutes = 30; grumpy.Care.Mood != CareMood.Upset && minutes <= 300; minutes += 20)
            grumpy = sim.Step(grumpy, at.AddMinutes(minutes)).State;
        Equal(CareMood.Upset, grumpy.Care.Mood, "restlessness left unanswered turns grumpy");
        var refused = sim.Care(grumpy, PetVerb.Fetch);
        Equal(PetCue.PushesToyAway, refused.Cue, "a grumpy dog turns away from the ball");
        Equal(grumpy.Meter(PetMeter.Energy), refused.State.Meter(PetMeter.Energy), "and fetch gives nothing");
        var fed = sim.Care(grumpy, PetVerb.Feed).State;
        Equal(CareMood.Upset, fed.Care.Mood, "food alone does not calm a grumpy dog");
        var walked = sim.Care(fed, PetVerb.Walk).State;
        Equal(true, walked.Care.Mood < CareMood.Upset, "a walk once fed calms it");
    }

    private static void Overfeeding()
    {
        var sim = Sim();
        var full = Dog(At(3, 18, 0), fullness: 95, exercise: 70, cleanliness: 80);
        var over = sim.Care(full, PetVerb.Feed);
        Equal(PetCue.TooFull, over.Cue, "feeding a full dog is overfeeding");
        Equal(CareReason.MealTooFull, over.State.Care.LastReason, "and is not dinner");
        Equal(MomentStatus.Pending, over.State.Care.StatusOf(CareMoment.Evening), "dinner still waits");
        Near(60, over.State.Meter(PetMeter.Exercise), "it flops down, too full to play (Exercise -10)");
        Near(65, over.State.Meter(PetMeter.Cleanliness), "and gets messy (Cleanliness -15)");
        Equal(full.Bond, over.State.Bond, "overfeeding builds no Bond");
        Equal(true, sim.IsFull(full), "IsFull warns before it happens");
        Equal(false, sim.IsFull(Dog(At(3, 18, 0), fullness: 89)), "89 is not full");
    }

    // Away mode: a neighbour pops in to walk the dog. Both walks and both meals are covered, nothing is missed, and the
    // dog gives the biggest welcome when the player is back.
    private static void Away()
    {
        var sim = Sim();
        var dog = sim.Care(Dog(At(3, 7, 30), bond: 40), PetVerb.Walk).State;
        var away = sim.StartAway(dog, TimeSpan.FromDays(3));
        Equal(true, away.Care.IsAway, "Away mode starts");
        var later = sim.Step(away, At(5, 22, 0)).State;
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Afternoon), "the neighbour's evening walk is covered");
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Dawn), "and the early walk");
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Evening), "and dinner");
        Equal("O", later.Care.TogetherDays.ToCode(), "no walk is missed while away");
        Equal(CareMood.Content, later.Care.Mood, "the dog stays content");
        Equal(true, later.Meter(PetMeter.Fullness) >= 60 - 1e-9, "needs are held at 60");
        Equal(true, later.Meter(PetMeter.Exercise) >= 60 - 1e-9, "Exercise too: the neighbour walks it");
        Equal(true, later.Bond >= dog.Bond, "Away costs no Bond");
        Equal(SampleMode.Away, later.Development.Sample.Mode, "development keeps the nominal pace");
        Equal(ReunionScale.BiggestWelcome, sim.Reunion(later), "and the welcome is the biggest");
        var back = sim.Care(later, PetVerb.Walk).State;
        Equal(false, back.Care.IsAway, "any care ends Away mode");
        Equal(ReunionScale.None, sim.Reunion(back), "and the greeting is done");
    }

    private static void HealthAndVet()
    {
        var sim = Sim();
        var at = At(3, 8, 0);
        var poorly = Dog(at, fullness: 10, health: 35);
        var unwell = sim.Step(poorly, at.AddMinutes(30)).State;
        Equal(CareCondition.UnderTheWeather, unwell.Care.Condition, "Health below 50 puts the dog under the weather");
        Equal(true, PetSimulation.CanVisitVet(unwell), "the vet is offered");
        var sick = unwell;
        for (var minutes = 40; sick.Care.Condition != CareCondition.Sick && minutes <= 90; minutes += 5)
            sick = sim.Step(sick, at.AddMinutes(minutes)).State;
        Equal(CareCondition.Sick, sick.Care.Condition, "while present, Health below 30 makes it sick");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.Fetch).Cue, "a sick dog is too poorly for fetch");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.Brush).Cue, "or a brush");
        var sniff = sim.Care(sick, PetVerb.Walk);
        Equal(PetCue.Content, sniff.Cue, "but manages a slow sniff round the garden");
        Equal(true, sniff.State.Meter(PetMeter.Exercise) > sick.Meter(PetMeter.Exercise), "which still counts as a walk");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.Feed).Cue, "and eats");

        var visit = sim.VisitVet(sick);
        Equal(PetCue.VetVisited, visit.Cue, "Dr. Rosa's visit");
        Equal(CareCondition.Recovering, visit.State.Care.Condition, "a sick dog starts recovering");
        Near(sick.Health + 15, visit.State.Health, "the vet restores Health +15 at once");
        Equal(MedicineState.Ready, PetSimulation.Medicine(visit.State), "breakfast is a medicine window");
        var first = sim.GiveMedicine(visit.State);
        Equal(PetCue.MedicineTaken, first.Cue, "the first dose, with breakfast");
        Equal(MedicineState.GivenThisMeal, PetSimulation.Medicine(first.State), "one dose per meal");
        var afternoon = sim.Step(first.State, At(3, 15, 0)).State;
        Equal(MedicineState.WaitForMeal, PetSimulation.Medicine(afternoon), "between meals the next dose waits for dinner");
        var dinner = sim.Step(afternoon, At(3, 18, 30)).State;
        var second = sim.GiveMedicine(dinner);
        Equal(PetCue.MedicineDone, second.Cue, "a dose with breakfast and one with dinner end Recovering");
        Equal(CareCondition.Well, second.State.Care.Condition, "the dog is well again");
        Equal(true, second.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Poorly),
            "the journal keeps a hard day we got through");
        Equal(PetCue.VetAllClear, sim.VisitVet(Dog(at)).Cue, "a well dog needs no vet");

        var left = sim.Step(poorly, At(5, 18, 0)).State;
        Equal(true, left.Care.Condition != CareCondition.Sick, "an absence never passes under the weather");
        Equal(true, left.Health >= CareConsequences.HealthFloor, "Health stays at the floor or above");
    }

    private static void MilestoneTiming()
    {
        var profile = PetCareProfile.Dog;
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

        Equal(true, previous[2].TotalDays < 10, "excellent care brings agility before the dog grows up");
        var attentive = Play(Attentive, 14);
        var busy = Play(Busy, 14);
        var neglected = Play(Neglect, 14);
        for (var i = 0; i < 3; i++)
        {
            var nominal = profile.SkillAges[i].TotalDays;
            foreach (var (name, ages) in new[] { ("attentive", attentive), ("busy", busy), ("neglected", neglected) })
                Equal(true, ages[i] >= 0.8 * nominal - 0.05 && ages[i] <= 1.4 * nominal + 0.05,
                    $"{name} {profile.Skills[i]} within bounds ({ages[i]:0.00})");
            Equal(true, attentive[i] <= busy[i] && busy[i] <= neglected[i] + (i == 0 ? 0.05 : 0), $"{profile.Skills[i]} follows the play style");
        }

        Equal(true, attentive[0] < neglected[0], "attentive care brings fetch sooner than neglect");
        Console.WriteLine("  dog milestones (fetch, leash walk, agility): steady " + string.Join(", ", rows) +
            $"; scripted attentive {Days(attentive)}, busy {Days(busy)}, neglect {Days(neglected)}");
    }

    private static void Setback()
    {
        var sim = Sim();
        var dog = sim.Adopt("Biscuit", Mixed, At(2, 8, 0));
        dog = sim.PreviewStep(dog, TimeSpan.FromDays(4)).State;
        Equal(true, dog.HasReached(MotorSkill.Fetch), "the young dog fetches by day four");
        // Walks (not meals) keep the player around while three meals are missed: day 3's dinner, day 4's breakfast and
        // day 4's dinner (day 3's breakfast falls in the excused first day).
        foreach (var day in new[] { 3, 4 })
            dog = sim.Care(sim.Step(dog, At(day, 16, 0)).State, PetVerb.Walk).State;
        var before = sim.Step(dog, At(4, 21, 35)).State;
        dog = sim.Step(before, At(4, 21, 55)).State;
        Equal(dog.NewestSkill, dog.ShySkill, "three missed meals while present: the newest skill goes shy");
        Equal(true, dog.HasReached(dog.ShySkill), "a shy skill stays reached");
        Equal(false, dog.Shows(dog.ShySkill), "but does not show");

        var shy = dog.ShySkill;
        var morning = sim.Step(dog, At(5, 8, 0)).State;
        var step = sim.Care(morning, PetVerb.Feed);
        step = sim.Care(step.State, PetVerb.Walk);
        Equal(shy, step.State.ShySkill, "still shy before the good day is complete");
        var evening = sim.Step(step.State, At(5, 16, 30)).State;
        step = sim.Care(evening, PetVerb.Walk);
        step = sim.Care(sim.Step(step.State, At(5, 18, 30)).State, PetVerb.Feed);
        Equal(PetCue.SkillBack, step.Cue, "two on-time meals and two walks in a day bring it back");
        Equal(MotorSkill.None, step.State.ShySkill, "the skill shows again");
        Equal(true, step.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Shy &&
            entry.Skill == shy), "the journal remembers the shy spell");
    }

    private static void BondTrustRhythmJournal()
    {
        var sim = Sim();
        var dog = Dog(At(3, 7, 30), bond: 20);
        var fed = sim.Care(dog, PetVerb.Feed).State;
        Near(21, fed.Bond, "an on-time meal +1");
        Near(24, sim.Care(fed, PetVerb.Walk).State.Bond, "a walk +3");
        Near(23, sim.Care(fed, PetVerb.Fetch).State.Bond, "fetch +2");
        Near(23, sim.Care(fed, PetVerb.Brush).State.Bond, "a brush +2");
        var capped = fed;
        for (var i = 0; i < 10; i++)
            capped = sim.Care(capped, PetVerb.Walk).State;
        Near(32, capped.Bond, "gains stop at the daily cap of 12");
        Equal(capped.Bond, capped.HighestBond, "HighestBond follows Bond for the earned-breed goal");

        var missed = sim.Step(Dog(At(3, 21, 0), bond: 50), At(3, 21, 45)).State;
        Equal(true, missed.Bond < 50, "a missed dinner costs Bond");
        var floor = sim.Step(Dog(At(3, 18, 0), bond: 50), At(20, 18, 0)).State;
        Equal(true, floor.Bond >= 0.6 * 50 - 1e-9, "Bond never falls below 60% of its high-water mark");

        var attentive = Days(sim, Attentive, 12);
        var neglected = Days(sim, Neglect, 12);
        var bonding = sim.Bonding(attentive);
        Equal(true, bonding.Rhythm >= 8, $"a care day counts with two meals, together time and bed (rhythm {bonding.Rhythm})");
        Equal(true, sim.Trust(attentive) != SecurityPhase.Clingy, "attentive care builds Trust");
        Equal(true, bonding.Security > sim.Bonding(neglected).Security, "missed meals cost Trust");
        Equal(true, attentive.Bond > neglected.Bond, "attentive care builds more Bond");
        Equal(0, sim.Bonding(neglected).Rhythm, "neglect has no rhythm");
        var journal = sim.Journal(attentive).Entries;
        Equal(JournalKind.Arrival, journal[0].Kind, "the journal starts with the arrival");
        Equal("Fetch,LeashWalk,Agility", string.Join(",", journal.Where(entry => entry.Kind == JournalKind.Milestone).Select(entry => entry.Skill)),
            "each milestone is in the journal");
        Equal(true, journal.Any(entry => entry.Kind == JournalKind.Sticker && entry.Count == 3), "a 3-day rhythm sticker");
        Equal(true, journal.Any(entry => entry.Kind == JournalKind.Sticker && entry.Count == 7), "and a 7-day one");
        Equal(true, journal.Any(entry => entry.Kind == JournalKind.Week && entry.Count == 1), "a page for the first week");

        var photos = attentive;
        var taken = 0;
        for (var i = 0; i < 4; i++)
        {
            var photo = sim.TakePhoto(photos, PhotoMoment.Playing);
            taken += photo.Taken ? 1 : 0;
            photos = photo.State;
        }

        Equal(3, taken, "three photos a day");
        var shot = sim.Journal(photos).Entries.Last(entry => entry.Kind == JournalKind.Photo);
        Equal(MotorSkill.Agility, shot.Skill, "a photo keeps the stage shown");
        Equal(PhotoMoment.Playing, shot.Moment, "and the moment");
    }

    // Tricks learned through familiarity counts: each game of fetch that is not one too many.
    private static void Tricks()
    {
        Equal(0, DogTricks.Learned(1), "no trick after one game");
        Equal("Sit", DogTricks.Newest(2), "Sit after two");
        Equal("Sit,Paw,Spin,Roll over,High five", string.Join(",", DogTricks.Names(99)), "five tricks in order");
        Equal(3, DogTricks.GamesToNext(2), "three more games to Paw");
        Equal(0, DogTricks.GamesToNext(20), "nothing left after High five");
        Equal(DogTricks.MaxFamiliarity, DogTricks.Next(DogTricks.MaxFamiliarity), "familiarity saturates");

        var sim = Sim();
        var dog = Dog(At(3, 16, 0));
        var first = sim.Care(dog, PetVerb.Fetch);
        Equal(1, first.State.Tricks, "a game of fetch is familiarity");
        Equal(PetCue.Content, first.Cue, "no trick yet");
        var second = sim.Care(first.State, PetVerb.Fetch);
        Equal(PetCue.NewTrick, second.Cue, "the second game teaches Sit");
        Equal("Sit", DogTricks.Newest(second.State.Tricks), "Sit is learned");
        var third = sim.Care(second.State, PetVerb.Fetch);
        var fourth = sim.Care(third.State, PetVerb.Fetch);
        Equal(PetCue.Bored, fourth.Cue, "a fourth game in a row is one too many");
        Equal(third.State.Tricks, fourth.State.Tricks, "and teaches nothing");
        Equal(0, sim.Care(dog, PetVerb.Walk).State.Tricks, "a walk is not fetch");
        var sick = Dog(At(3, 16, 0), health: 20);
        sick = sim.Step(sick, At(3, 16, 20)).State;
        sick = sim.Step(sick, At(3, 16, 40)).State;
        if (sick.Care.Condition == CareCondition.Sick)
            Equal(0, sim.Care(sick, PetVerb.Fetch).State.Tricks, "a game the dog was too poorly for teaches nothing");

        var payload = PetSaveCodec.Encode(second.State);
        Equal(true, payload.Contains(" tricks=\"2\""), "tricks are saved with the unlocks");
        Equal(2, PetSaveCodec.Decode(payload).Tricks, "and round-trip");
        Equal(false, PetSaveCodec.Encode(dog).Contains("tricks="), "a dog that has not played saves no tricks attribute");
        var cat = Sim().Adopt("Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), At(3, 8, 0));
        var catSave = PetSaveCodec.Encode(cat);
        ExpectInvalid(catSave.Replace(" milestones=\"0\"", " milestones=\"0\" tricks=\"3\""), "a cat learns no tricks");
        ExpectInvalid(payload.Replace(" tricks=\"2\"", " tricks=\"-1\""), "negative familiarity is corrupt");
    }

    // The reunion greeting, scaled to time away: a wag after a school day, zoomies after about a day, the biggest welcome
    // after two days or more. Always joy, never guilt.
    private static void Reunion()
    {
        Equal(ReunionScale.None, PetReunion.ScaleFor(TimeSpan.FromMinutes(90)), "a quick trip out gets no fuss");
        Equal(ReunionScale.TailWag, PetReunion.ScaleFor(TimeSpan.FromHours(2)), "two hours: a wagging tail");
        Equal(ReunionScale.TailWag, PetReunion.ScaleFor(TimeSpan.FromHours(10)), "a night or a school day: a wagging tail");
        Equal(ReunionScale.Zoomies, PetReunion.ScaleFor(TimeSpan.FromHours(16)), "most of a day: zoomies");
        Equal(ReunionScale.Zoomies, PetReunion.ScaleFor(TimeSpan.FromHours(47)), "a day and more: zoomies");
        Equal(ReunionScale.BiggestWelcome, PetReunion.ScaleFor(TimeSpan.FromDays(2)), "two days: the biggest welcome");
        Equal(ReunionScale.BiggestWelcome, PetReunion.ScaleFor(TimeSpan.FromDays(30)), "and no bigger after a month");
        var heights = new[] { ReunionScale.None, ReunionScale.TailWag, ReunionScale.Zoomies, ReunionScale.BiggestWelcome };
        for (var i = 1; i < heights.Length; i++)
        {
            Equal(true, PetReunion.Bounces(heights[i]) > PetReunion.Bounces(heights[i - 1]), $"{heights[i]} has more bounces");
            Equal(true, PetReunion.BounceHeight(heights[i]) > PetReunion.BounceHeight(heights[i - 1]), $"{heights[i]} bounces higher");
        }

        var banned = new[] { "sorry", "lonely", "miss", "forgot", "sad", "waited", "waiting", "where were you", "finally", "left",
            "alone", "cried", "hungry", "long time", "days", "hours" };
        foreach (var scale in heights.Skip(1))
        {
            var line = PetReunion.Line(scale, "Biscuit");
            Equal(true, line.Contains("Biscuit"), $"{scale} names the dog");
            foreach (var word in banned)
                Equal(false, line.ToLowerInvariant().Contains(word), $"{scale} never says \"{word}\": joy, not guilt");
        }

        Equal<string>(null, PetReunion.Line(ReunionScale.None, "Biscuit"), "no line without a welcome");

        var sim = Sim();
        var dog = sim.Care(Dog(At(3, 16, 0)), PetVerb.Walk).State;
        Equal(ReunionScale.None, sim.Reunion(dog), "no greeting right after a walk");
        Equal(ReunionScale.None, sim.Reunion(sim.Step(dog, At(3, 17, 0)).State), "an hour later, none");
        var night = sim.Step(dog, At(4, 7, 0)).State;
        Equal(ReunionScale.TailWag, sim.Reunion(night), "the next morning, a wagging tail");
        Equal(ReunionScale.Zoomies, sim.Reunion(sim.Step(dog, At(4, 12, 0)).State), "the next lunchtime, zoomies");
        Equal(ReunionScale.BiggestWelcome, sim.Reunion(sim.Step(dog, At(6, 9, 0)).State), "after a long weekend, the biggest welcome");
        Equal(ReunionScale.None, sim.Reunion(sim.Care(night, PetVerb.Feed).State), "any care ends the greeting");
        var cat = sim.Step(sim.Adopt("Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), At(3, 8, 0)), At(6, 9, 0)).State;
        Equal(ReunionScale.None, sim.Reunion(cat), "the reunion greeting is the dog's signature moment");
        var bigger = sim.Step(dog, At(6, 9, 0)).State;
        Equal(night.Bond, sim.Step(dog, At(4, 7, 0)).State.Bond, "the greeting is display only");
        Equal(true, bigger.Bond <= dog.Bond, "time away never adds Bond by itself");
    }

    // The auto-feeder, the ball launcher and the doggy door: the cat's helpers as a dog's objects. None of them walks
    // the dog: the walk windows still ask for a walk.
    private static void Helpers()
    {
        var sim = Sim();
        var dog = sim.StockFeeder(Dog(At(3, 18, 0), fullness: 50, exercise: 42, cleanliness: 42));
        Equal(6, dog.Care.Helpers.Stock, "the auto-feeder is stocked");
        var events = new List<HelperEvent>();
        var helped = sim.Step(dog, At(4, 1, 0), HelperSettings.All, events).State;
        Equal(true, events.Any(e => e.Kind == HelperKind.Feeder), "the auto-feeder serves dinner");
        Equal(true, events.Any(e => e.Kind == HelperKind.Play), "the ball launcher keeps it busy");
        Equal(true, events.Any(e => e.Kind == HelperKind.Tidy), "the doggy door's paw mat keeps it clean");
        Equal(MomentStatus.Helped, helped.Care.StatusOf(CareMoment.Evening), "dinner from the feeder is Helped");
        Equal(MomentStatus.Missed, helped.Care.StatusOf(CareMoment.Afternoon), "but no helper ever walks the dog");
        Equal("M", helped.Care.TogetherDays.ToCode(), "the evening walk is missed for the ring");
        Equal(true, helped.Meter(PetMeter.Exercise) >= HelperRules.Default.PlayFloor - 1e-9, "Exercise is held at the play floor");
        Equal(true, helped.Meter(PetMeter.Exercise) < HelperRules.WellKept, "and never to well kept: walks look better");
        Equal(true, helped.Meter(PetMeter.Cleanliness) >= HelperRules.Default.TidyFloor - 1e-9, "Cleanliness is held at the tidy floor");
        var plain = sim.Step(dog, At(4, 1, 0)).State;
        Near(plain.Bond, helped.Bond, "helpers never build Bond");
        Equal(plain.Milestones, helped.Milestones, "or milestones");
        Equal(plain.Care.StatusOf(CareMoment.Afternoon), helped.Care.StatusOf(CareMoment.Afternoon), "or walk the dog");
        var payload = PetSaveCodec.Encode(helped);
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload, Catalog)), "the dog's helpers round-trip");
    }

    private static void PreviewDay()
    {
        var sim = Sim();
        var dog = sim.Adopt("Biscuit", Mixed, At(3, 16, 0));
        foreach (var verb in new[] { PetVerb.Feed, PetVerb.Walk, PetVerb.Rest })
            dog = sim.Care(dog, verb).State;
        dog = sim.Care(sim.Step(dog, At(3, 22, 0)).State, PetVerb.Feed).State;
        var next = sim.PreviewStep(dog, TimeSpan.FromDays(1)).State;
        Equal(dog.TimeCheckpoint, next.TimeCheckpoint, "a preview day passes no real time");
        Equal(dog.Care, next.Care, "the ledger holds");
        Equal(1, sim.Bonding(next).Rhythm, "two meals, a walk and a rest count a preview day");
        Equal(dog.Age + TimeSpan.FromDays(1), next.Age, "a day older");
        var later = next;
        for (var i = 0; i < 3; i++)
            later = sim.PreviewStep(later, TimeSpan.FromDays(1)).State;
        Equal(true, later.HasReached(MotorSkill.Fetch), "four preview days bring fetch");
        var grown = later;
        for (var i = 0; i < 6; i++)
            grown = sim.PreviewStep(grown, TimeSpan.FromDays(1)).State;
        Equal(PetStage.Grown, grown.Stage, "ten preview days and the dog is grown");
    }

    private static void SplitAdvances()
    {
        var sim = Sim();
        var dog = sim.Adopt("Biscuit", Corgi, At(2, 18, 0));
        dog = Run(sim, dog, Attentive, At(2, 18, 0), At(4, 6, 30));
        // Through the school day, the evening walk window and into the night; each gap stays
        // under the 8-hour cap, which is path dependent on purpose.
        foreach (var (from, to, steps) in new[]
                 {
                     (At(4, 7, 20), At(4, 15, 0), 14), (At(4, 20, 35), At(5, 3, 0), 15), (At(5, 3, 0), At(5, 7, 10), 12),
                 })
        {
            var start = Run(sim, dog, Attentive, At(4, 6, 30), from);
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
            Equal(sim.Reunion(whole), sim.Reunion(split), $"{name}: the welcome");
        }

        // The same with the early walk skipped (its grace closes inside the gap).
        var skipped = sim.Care(sim.Step(dog, At(4, 7, 30)).State, PetVerb.Feed).State;
        var wholeSkip = sim.Step(skipped, At(4, 13, 0)).State;
        var splitSkip = skipped;
        for (var i = 1; i <= 11; i++)
            splitSkip = sim.Step(splitSkip, At(4, 7, 30) + TimeSpan.FromTicks((At(4, 13, 0) - At(4, 7, 30)).Ticks * i / 11)).State;
        Equal(Ledger(wholeSkip.Care), Ledger(splitSkip.Care), "the grace walk closes the same split or whole");
        Equal(MomentStatus.Excused, wholeSkip.Care.StatusOf(CareMoment.Dawn), "excused either way");

        // Determinism: the same plan twice gives the same record.
        var again = Run(sim, sim.Adopt("Biscuit", Corgi, At(2, 18, 0)), Attentive, At(2, 18, 0), At(4, 6, 30));
        Equal(PetSaveCodec.Encode(dog), PetSaveCodec.Encode(again), "the same care gives the same dog");
    }

    // A dog saves as a v2 record from adoption with five moments in its ledger and round-trips byte for byte; a dog
    // record written with the simple rules (v1, or v2 before P3) is seeded on first use like any v1 pet.
    private static void Saves()
    {
        var sim = Sim();
        var built = sim.Care(sim.Step(sim.Adopt("Biscuit", Corgi, At(2, 18, 0)), At(4, 7, 15)).State, PetVerb.Walk).State;
        var payload = PetSaveCodec.Encode(built);
        Equal(true, payload.StartsWith("<pet version=\"2\"><name>Biscuit</name><species>1</species><breed>204</breed>", StringComparison.Ordinal),
            "a Corgi saves as a v2 pet record");
        Equal(true, payload.Contains("<meter id=\"5\""), "with its Exercise");
        Equal(true, payload.Contains(" moments=\"") && System.Text.RegularExpressions.Regex.IsMatch(payload, " moments=\"[A-Z]{5}\""),
            "and five moments in its ledger");
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload)), "and round-trips byte for byte");
        var decoded = PetSaveCodec.Decode(payload);
        Equal(CareRoutine.Dog, decoded.Care.Routine, "the ledger follows the dog's routine");
        Equal(MomentStatus.OnTime, decoded.Care.StatusOf(CareMoment.Dawn), "the early walk is kept");
        Equal(built.Milestones, decoded.Milestones, "milestones are kept");
        ExpectInvalid(payload.Replace(" milestones=\"" + built.Milestones + "\"", " milestones=\"4\""), "more milestones than a dog has");
        ExpectInvalid(payload.Replace("<breed>204</breed>", "<breed>101</breed>"), "a cat breed is not a dog");
        var four = System.Text.RegularExpressions.Regex.Replace(payload, " moments=\"([A-Z]{4})[A-Z]\"", " moments=\"$1\"");
        ExpectInvalid(four, "a dog's ledger needs five moments");

        const string v1 =
            "<pet version=\"1\"><name>Rex</name><species>1</species><breed>200</breed>" +
            "<appearance coat=\"13\" accessory=\"0\" accent=\"0\" />" +
            "<checkpoint>639028224000000000</checkpoint><age>5616000000000</age>" +
            "<meters><meter id=\"0\" value=\"56.5\" /><meter id=\"1\" value=\"66.2\" /><meter id=\"3\" value=\"84\" />" +
            "<meter id=\"5\" value=\"63.2\" /><meter id=\"4\" value=\"97\" /></meters>" +
            "<bond value=\"12\" highest=\"15\" /><personality lively=\"12\" bold=\"-3.5\" /><unlocks grown=\"false\" /></pet>";
        var old = PetSaveCodec.Decode(v1);
        Equal(2, old.Milestones, "a 6.5-day dog saved with the simple rules has its first two milestones");
        Equal(true, PetSaveCodec.Encode(old).Contains("awaiting-anchor=\"true\""), "and a ledger waiting for its anchor");
        var anchored = sim.Step(old, new DateTimeOffset(639028224000000000, TimeSpan.Zero).AddDays(3)).State;
        Equal("", anchored.Care.Meals.ToCode(), "its first advance excuses the gap");
        Equal(12.0, anchored.Bond, "and costs no Bond");
        var v2Simple = PetSaveCodec.Decode(v1.Replace("version=\"1\"", "version=\"2\""));
        Equal(2, v2Simple.Milestones, "a v2 dog saved before P3 (no care element) migrates the same way");

        // Cats and hamsters are unchanged: their records still decode with their own routines.
        var cat = sim.Adopt("Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), At(3, 8, 0));
        Equal(CareRoutine.Cat, PetSaveCodec.Decode(PetSaveCodec.Encode(cat)).Care.Routine, "a cat keeps the cat's routine");
        Equal(4, PetSaveCodec.Decode(PetSaveCodec.Encode(cat)).Care.MomentCode.Length, "and four moments");
        var catSave = PetSaveCodec.Encode(cat);
        ExpectInvalid(catSave.Substring(0, catSave.IndexOf("<care ", StringComparison.Ordinal)) + "</pet>", "a v2 cat still keeps its ledger");
    }

    private static void Breeds()
    {
        var breeds = Catalog.BreedsOf(Species.Dog);
        Equal("MixedBreed:Free,Golden:Earned,Beagle:Earned,Shiba:Earned,Corgi:Premium,Poodle:Premium,Dachshund:Premium,Husky:Premium," +
              "Samoyed:Premium,Pomeranian:Premium,Dalmatian:Premium,BorderCollie:Premium",
            string.Join(",", breeds.Select(breed => breed.Breed + ":" + breed.Unlock)), "the dog's breed ladder");
        Equal("Dachshund", string.Join(",", breeds.Where(breed => !breed.AvailableNow).Select(breed => breed.Breed)),
            "every dog breed ships with P3 except the Dachshund, whose long body needs a new silhouette");
        Equal(true, Catalog.Get(PetBreed.Corgi).AvailableNow, "the Corgi ships");
        Equal(BreedUnlock.Premium, Catalog.Get(PetBreed.Corgi).Unlock, "and the Corgi is premium");
        Equal("breed.dog.corgi", Catalog.Get(PetBreed.Corgi).ProductId, "sold as breed.dog.corgi");
        Equal("200,201,202,203,204,205,206,207,208,209,210,211", string.Join(",", breeds.Select(breed => (int)breed.Breed)),
            "in the dog block");
        Equal(EarnedGoal.FirstFullCareDay, Catalog.Get(PetBreed.Golden).Goal, "Golden: a first full care day");
        Equal(EarnedGoal.Bond40, Catalog.Get(PetBreed.Beagle).Goal, "Beagle: Bond 40");
        Equal(EarnedGoal.RaisedToGrown, Catalog.Get(PetBreed.Shiba).Goal, "Shiba: raised to Grown");
        Equal("breed.dog.corgi,breed.dog.poodle,breed.dog.dachshund,breed.dog.husky,breed.dog.samoyed,breed.dog.pomeranian," +
              "breed.dog.dalmatian,breed.dog.border_collie",
            string.Join(",", breeds.Where(breed => breed.Unlock == BreedUnlock.Premium).Select(breed => breed.ProductId)),
            "premium product IDs");
        Equal(true, Catalog.GrantsBreed("breedpack.dog", PetBreed.Corgi), "the dog breed pack");
        Equal(false, Catalog.GrantsBreed("breedpack.cat", PetBreed.Corgi), "not the cat's");
        Equal(true, Catalog.GrantsBreed(PetIds.AllBreedsPackProductId, PetBreed.Husky), "and the every-breed pack");
        foreach (var product in breeds.Where(breed => breed.ProductId != null).Select(breed => breed.ProductId)
                     .Concat(new[] { "breedpack.dog" }))
            Equal(true, DeveloperUnlocks.Products(Catalog).Contains(product), $"developer mode grants {product}");
        var species = Catalog.Get(Species.Dog);
        foreach (var breed in breeds)
            Near(0, breed.NetDecayChangePerHour(species), $"{breed.Breed} is no harder or easier", 1e-9);
        Catalog.Validate();

        var none = SpeciesGoalProgress.None;
        Equal("MixedBreed", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Dog, none, new string[0])),
            "a new household adopts a mixed breed");
        var earned = new SpeciesGoalProgress(1, 40, true, 0);
        Equal("MixedBreed,Golden,Beagle,Shiba", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Dog, earned, new string[0])),
            "care earns the Golden, the Beagle and the Shiba");
        Equal("MixedBreed,Corgi", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Dog, none, new[] { "breed.dog.corgi" })),
            "buying the Corgi opens the Corgi");
        Equal("MixedBreed,Corgi,Poodle,Husky,Samoyed,Pomeranian,Dalmatian,BorderCollie",
            string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Dog, none, new[] { "breedpack.dog" })),
            "the pack opens every premium dog that has shipped; the Dachshund waits for its art");
        Equal(true, PetUnlocks.IsUnlocked(Catalog, PetBreed.Dachshund, none, new[] { "breedpack.dog" }),
            "a pack owner keeps the Dachshund for when it arrives");
        Equal((2, true), PetCareDays.RuleFor(Species.Dog), "a dog's care day: two meals, together time and bed");
    }

    private static void Household()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8), TimeSpan.FromDays(7), TimeSpan.FromDays(14));
        var pets = Sim();
        var now = At(3, 7, 0);
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        Equal(true, household.CanAdoptBreed(PetBreed.MixedBreed), "a mixed breed is adoptable at once");
        Equal(false, household.CanAdoptBreed(PetBreed.Golden), "the Golden is earned");
        Equal(false, household.CanAdoptBreed(PetBreed.Corgi), "the Corgi is premium");
        household.AdoptPet("Biscuit", Mixed);
        now = At(4, 7, 15);
        household.AdvanceAll();
        household.ApplyPetCare(2, PetVerb.Walk);
        var breakfast = household.ApplyPetCare(2, PetVerb.Feed);
        Equal(PetCue.Content, breakfast.Cue, "breakfast in the household");
        Equal(false, breakfast.CompletedCareDay, "not yet a full day");
        now = At(4, 18, 30);
        household.AdvanceAll();
        household.ApplyPetCare(2, PetVerb.Walk);
        household.ApplyPetCare(2, PetVerb.Feed);
        var rest = household.ApplyPetCare(2, PetVerb.Rest);
        Equal(true, rest.CompletedCareDay, "two meals, a walk and a rest complete a dog's care day");
        Equal("Golden", string.Join(",", rest.UnlockedBreeds), "and earn the Golden");
        Equal(true, household.CanAdoptBreed(PetBreed.Golden), "which can now be adopted");
        Equal(PetCue.VetAllClear, household.VisitPetVet(2).Cue, "the vet is there for a dog too");

        // A young dog's milestones wait as celebrations in their own manifest element.
        now = At(14, 18, 0);
        household.AdvanceAll(activeOnScreen: false);
        var manifest = System.Xml.Linq.XElement.Parse(store.ManifestPrimary);
        Equal(true, manifest.Element("dog-celebrations").Elements().Count() >= 1, "milestones wait in dog-celebrations");
        Equal(null, manifest.Element("pet-celebrations"), "never where an older build reads a cat's");
        Equal(null, manifest.Element("hamster-celebrations"), "or a hamster's");
        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        var played = reopened.TakeCelebrations(2).ToList();
        Equal(true, played.Contains(CompanionMilestone.Fetch) && played.Contains(CompanionMilestone.Grown),
            "they survive a reload: fetch and growing up");
        Equal(true, played.IndexOf(CompanionMilestone.Fetch) < played.IndexOf(CompanionMilestone.Grown), "milestones before growing up");
    }

    // Scripted players: (hour, minute, verbs) visits each day. An attentive player walks before school and after it; a
    // school-day player feeds before school and walks once, after it, with a game of fetch; a busy one feeds early and
    // late; neglect is a lunchtime meal.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Attentive =
    {
        (7, 15, new[] { PetVerb.Walk, PetVerb.Feed }),
        (16, 0, new[] { PetVerb.Walk, PetVerb.Fetch }),
        (18, 30, new[] { PetVerb.Feed, PetVerb.Brush }),
        (20, 30, new[] { PetVerb.Rest }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] SchoolDays =
    {
        (7, 45, new[] { PetVerb.Feed }),
        (16, 30, new[] { PetVerb.Walk, PetVerb.Fetch }),
        (18, 30, new[] { PetVerb.Feed, PetVerb.Brush }),
        (20, 0, new[] { PetVerb.Rest }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] NoWalks =
    {
        (7, 45, new[] { PetVerb.Feed }),
        (18, 30, new[] { PetVerb.Feed, PetVerb.Brush }),
        (20, 0, new[] { PetVerb.Rest }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Busy =
    {
        (7, 30, new[] { PetVerb.Feed }),
        (19, 45, new[] { PetVerb.Walk, PetVerb.Feed, PetVerb.Rest }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Neglect =
    {
        (12, 30, new[] { PetVerb.Feed }),
    };

    private static double Lowest(PetState state) =>
        new[] { PetMeter.Fullness, PetMeter.Exercise, PetMeter.Energy, PetMeter.Cleanliness }.Min(state.Meter);

    private static double[] Play((int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var sim = Sim();
        var adopted = At(2, 7, 0);
        var dog = Run(sim, sim.Adopt("Biscuit", Mixed, adopted), plan, adopted, adopted.AddDays(days));
        var ages = new double[3];
        foreach (var entry in sim.Journal(dog).Entries.Where(entry => entry.Kind == JournalKind.Milestone))
            ages[PetCareProfile.Dog.IndexOf(entry.Skill)] = (entry.At - adopted).TotalDays;
        return ages;
    }

    private static PetState Days(PetSimulation sim, (int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var adopted = At(2, 7, 0);
        return Run(sim, sim.Adopt("Biscuit", Mixed, adopted), plan, adopted, adopted.AddDays(days));
    }

    private static PetState Run(PetSimulation sim, PetState dog, (int Hour, int Minute, PetVerb[] Verbs)[] plan,
        DateTimeOffset from, DateTimeOffset until)
    {
        for (var day = from.Date; day <= until.Date; day = day.AddDays(1))
        {
            foreach (var (hour, minute, verbs) in plan)
            {
                var at = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, from.Offset);
                if (at <= from || at > until)
                    continue;
                dog = sim.Step(dog, at).State;
                foreach (var verb in verbs)
                    dog = sim.Care(dog, verb).State;
            }
        }

        return sim.Step(dog, until).State;
    }

    private static string Strip(PetSimulation sim, PetState dog) =>
        string.Join(",", sim.Routine(dog).Select(slot => slot.Kind + ":" + slot.State));

    private static string Ledger(CareRecord care)
    {
        var element = CareRecordCodec.Encode(care);
        element.Attribute("session-start").Remove();
        element.Attribute("mood-since").Remove();
        return element.ToString();
    }

    private static string Days(double[] ages) => $"{ages[0]:0.0} / {ages[1]:0.0} / {ages[2]:0.0}";

    private static PetSimulation Sim() => new PetSimulation();

    // A dog on an anchored ledger whose windows all count, at the given needs.
    private static PetState Dog(DateTimeOffset at, double fullness = 80, double exercise = 80, double energy = 80,
        double cleanliness = 80, double health = 100, double bond = 20)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Dog).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = fullness, [PetMeter.Energy] = energy, [PetMeter.Exercise] = exercise,
            [PetMeter.Cleanliness] = cleanliness, [PetMeter.Health] = health,
        };
        return PetState.Restore(Catalog, "Biscuit", Mixed, at, TimeSpan.FromDays(1), meters, bond, bond, 0, 0, false,
            draft.Build(), null, null, null, 0);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new DateTimeOffset(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

    private static string Describe(IReadOnlyList<CareWindow> windows) =>
        string.Join(",", windows.Select(window =>
            $"{(window.Grace ? "Grace walk" : window.Core.ToString())} {window.Open:hh\\:mm}-{window.Due:hh\\:mm}-{window.LateUntil:hh\\:mm}"));

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

    private static void ExpectThrows<TException>(Action action, string description) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected {typeof(TException).Name}");
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
