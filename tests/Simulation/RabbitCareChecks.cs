using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// P5 Rabbit: the fifth species on the care profile mechanism (care slice 7), and a crepuscular one. Greens at breakfast and
// dinner, floor time after school (the together verb: sitting on the floor builds Bond faster than any other care), a dawn
// floor time that closes with grace when school gets in the way, a midday doze through school hours that the rabbit keeps by
// itself, a hay rack to keep filled (Hay plays the Energy role: it empties through the waking hours, holds through the doze
// and only a hands-on refill tops it up), grooming as together time that comforts, and a daily litter tidy whose miss leaves
// scattered hay; twitchy and thumping moods; overfeeding; Away mode; the vet and medicine (a poorly rabbit will not come out
// for floor time or be groomed); the milestones First binky, Hop circuit and Flop of trust; the setback, Bond, Trust, the
// care rhythm and the journal; binkies, the signature moment; helpers (a pellet feeder, a willow tunnel and a litter tray
// liner, none of them floor time or hay); earned and premium breeds; adoption, saves (a rabbit saved before P5 migrates) and
// split advances. See docs/pet-mode.md and docs/care-and-growth.md.
static class RabbitCareChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly PetCatalog Catalog = PetCatalog.Default;
    private static readonly PetAppearance Dutch = PetAppearance.Default(Catalog, PetBreed.Dutch);
    // A premium breed drawn on its own larger base, with tendencies of its own.
    private static readonly PetAppearance Flemish = PetAppearance.Default(Catalog, PetBreed.FlemishGiant);

    public static void Run()
    {
        Profile();
        RoutineWindows();
        FloorTime();
        HayRack();
        TwoGreensTuning();
        Moods();
        Overfeeding();
        ScatteredHay();
        Away();
        HealthAndVet();
        MilestoneTiming();
        Setback();
        BondTrustRhythmJournal();
        Binkies();
        Helpers();
        PreviewDay();
        SplitAdvances();
        Saves();
        Breeds();
        Household();
        Console.WriteLine("Rabbit care checks passed.");
    }

    private static void Profile()
    {
        var rabbit = PetCareProfile.Rabbit;
        Equal(rabbit, PetCareProfile.For(Species.Rabbit), "the rabbit has a care profile");
        Equal(CareRoutine.Rabbit, rabbit.Routine, "and its own routine");
        Equal(CareTags.None, rabbit.TagsFor(PetVerb.Hay), "filling the hay rack answers no window");
        Equal(CareTags.Meal, rabbit.TagsFor(PetVerb.Greens), "greens are the meal");
        Equal(CareTags.Together | CareTags.Comfort, rabbit.TagsFor(PetVerb.Groom), "grooming is together time that comforts");
        Equal(CareTags.Together | CareTags.Play | CareTags.Comfort, rabbit.TagsFor(PetVerb.FloorTime),
            "floor time is together time, play and comfort");
        Equal(CareTags.Hygiene, rabbit.TagsFor(PetVerb.Litter), "the litter tidy is the daily wash");
        Equal(CareTags.Medicine, rabbit.Profile.TagsFor("medicine"), "medicine is a care verb of the profile");
        foreach (var verb in Catalog.Get(Species.Rabbit).Verbs)
            Equal(true, rabbit.Offers(verb.Verb), $"the profile maps the rabbit's {verb.Verb}");
        Equal(PetMeter.Fullness, rabbit.FullnessMeter, "greens fill Fullness");
        Equal(PetMeter.Comfort, rabbit.ComfortMeter, "Comfort is Comfort");
        Equal(PetMeter.Hay, rabbit.EnergyMeter, "Hay plays the Energy role");
        Equal(false, rabbit.RestMeter, "and is no rest meter");
        Equal(true, PetCareProfile.Cat.RestMeter && PetCareProfile.Fish.RestMeter, "every other species rests its Energy");
        Equal(PetMeter.Litter, rabbit.CleanlinessMeter, "Litter plays the cleanliness role");
        Equal(true, rabbit.RestsAlone, "a rabbit dozes by itself");
        Equal(0.0, rabbit.NightEnergy, "and its hay holds through the doze");
        Equal(false, rabbit.IgnoresExtraFood, "a full rabbit is gently overfed like a cat");
        Equal(2, rabbit.MealsPerDay, "two greens a day");
        Equal(true, rabbit.NapsByDay, "its rest is a day nap (the midday doze)");
        Equal(true, rabbit.RefusedWhenPoorly(PetVerb.FloorTime) && rabbit.RefusedWhenPoorly(PetVerb.Groom),
            "a poorly rabbit will not come out for floor time or be groomed");
        Equal(false, rabbit.RefusedWhenPoorly(PetVerb.Litter) || rabbit.RefusedWhenPoorly(PetVerb.Hay) ||
            rabbit.RefusedWhenPoorly(PetVerb.Greens), "but takes hay, greens and a fresh tray");
        Equal(true, PetCareProfile.Fish.RefusedWhenPoorly(PetVerb.WipeGlass) && !PetCareProfile.Fish.RefusedWhenPoorly(PetVerb.WaterChange),
            "other species keep play and the wash");
        Equal<EngagementKind?>(EngagementKind.PlayWand, rabbit.KindOf(PetVerb.FloorTime, false), "floor time feeds Motor like play");
        Equal<EngagementKind?>(EngagementKind.Soothe, rabbit.KindOf(PetVerb.Groom, false), "grooming is soothing");
        Equal<EngagementKind?>(EngagementKind.Hygiene, rabbit.KindOf(PetVerb.Litter, false), "the litter tidy is tidying");
        Equal<EngagementKind?>(EngagementKind.Meal, rabbit.KindOf(PetVerb.Greens, true), "on-time greens are a meal");
        Equal<EngagementKind?>(null, rabbit.KindOf(PetVerb.Hay, false), "hay is no engagement");
        Equal(CareCategories.Play, rabbit.CategoryOf(PetVerb.FloorTime), "floor time is play for the day's variety");
        Equal(CareCategories.Comfort, rabbit.CategoryOf(PetVerb.Groom), "grooming comforts");
        Equal<PetVerb?>(null, rabbit.TrickVerb, "a rabbit learns no tricks");
        Equal("FirstBinky,HopCircuit,FlopOfTrust", string.Join(",", rabbit.Skills), "the rabbit's milestones");
        Equal("2,3.5,5", string.Join(",", rabbit.SkillAges.Select(age => age.TotalDays)), "at nominal days 2, 3.5 and 5");
        Equal(2, rabbit.ReachedBy(TimeSpan.FromDays(4)), "milestones by age");
        Equal(TimeSpan.FromDays(7), Catalog.Get(Species.Rabbit).GrownAge, "a rabbit grows up on day 7");
        Equal("Hay,Fullness,Litter,Comfort", string.Join(",", Catalog.Get(Species.Rabbit).Meters.Select(meter => meter.Meter)),
            "the room shows Hay, Fullness, Litter and Comfort");
        var bonds = Catalog.AllSpecies.SelectMany(species => species.Verbs).Where(verb => verb.Verb != PetVerb.FloorTime)
            .Max(verb => verb.Bond);
        Equal(true, Catalog.Get(Species.Rabbit).FindVerb(PetVerb.FloorTime).Bond > bonds,
            "sitting on the floor builds more Bond than any other pet care");
    }

    private static void RoutineWindows()
    {
        var school = RoutineCalendar.Windows(CareRoutine.Rabbit, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal("Meal 07:00-10:00-12:00,Play 15:00-19:00-20:00,Meal 17:30-20:30-21:30,Doze 10:00-15:00-15:30,Grace play 07:00-09:00-10:00",
            Describe(school), "the rabbit's school day: breakfast, floor time, dinner, the midday doze and the dawn floor time");
        var home = RoutineCalendar.Windows(CareRoutine.Rabbit, RoutinePreset.Home, RoutineCalendar.DefaultWakeTime);
        Equal("Meal 07:00-09:30-11:00,Play 14:30-18:00-19:00,Meal 17:00-19:30-20:30,Doze 11:00-14:30-15:00,Grace play 07:00-09:30-10:30",
            Describe(home), "the Home preset");
        var late = RoutineCalendar.Windows(CareRoutine.Rabbit, RoutinePreset.SchoolDay, TimeSpan.FromHours(8));
        Equal("Meal 08:00-11:00-13:00,Play 15:00-19:00-20:00,Meal 17:30-20:30-21:30,Doze 10:00-15:00-15:30,Grace play 08:00-10:00-11:00",
            Describe(late), "the wake time moves breakfast and the dawn floor time, never the doze");
        Equal(5, RoutineCalendar.MomentsOf(CareRoutine.Rabbit), "a rabbit's day has five moments, like a dog's");
        Equal((TimeSpan.FromHours(10), TimeSpan.FromHours(15)), RoutineCalendar.Night(CareRoutine.Rabbit, RoutinePreset.SchoolDay,
            RoutineCalendar.DefaultWakeTime), "its quiet hours are the midday doze");
        Equal((TimeSpan.FromHours(7), TimeSpan.FromHours(16)), RoutineCalendar.Night(CareRoutine.Hamster, RoutinePreset.SchoolDay,
            RoutineCalendar.DefaultWakeTime), "a hamster still naps from the morning");
        Equal(true, RoutineCalendar.DozesMidday(CareRoutine.Rabbit), "a rabbit dozes at midday");
        Equal(false, RoutineCalendar.DozesMidday(CareRoutine.Hamster), "a hamster naps all day");
        Equal(false, RoutineCalendar.DozesMidday(CareRoutine.Cat), "a cat naps at bedtime");
        Equal(2, RoutineCalendar.MealWindows(CareRoutine.Rabbit), "two meal windows");
        Equal(2, RoutineCalendar.GoodDayMeals(CareRoutine.Rabbit), "a good day asks for both");
        Equal(true, RoutineCalendar.HasPlayWindow(CareRoutine.Rabbit), "floor time keeps the together ring");
        Equal(false, RoutineCalendar.HasMorningHello(CareRoutine.Rabbit), "its morning is breakfast");
        Equal(false, RoutineCalendar.HasWalks(CareRoutine.Rabbit), "a rabbit is never walked");
        var wash = RoutineCalendar.WashWindow(CareRoutine.Rabbit, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal((TimeSpan.FromHours(7), TimeSpan.FromHours(20.5), TimeSpan.FromHours(21.5)), wash,
            "the litter tidy: any time from morning until dinner");
        Tunables.Validate();

        var sim = Sim();
        var rabbit = Rabbit(At(3, 7, 30));
        Equal("Hello:Due,MorningMeal:Due,Nap:Later,Play:Later,EveningMeal:Later,Wash:Due", Strip(sim, rabbit),
            "the strip: the dawn floor time, breakfast, the doze, floor time, dinner and the litter");
        Equal("Hello:Excused,MorningMeal:Missed,Nap:Napping,Play:Later,EveningMeal:Later,Wash:Due",
            Strip(sim, sim.Step(rabbit, At(3, 12, 30)).State), "at noon it dozes");
        Equal(true, PetSimulation.IsNapping(sim.Step(rabbit, At(3, 12, 30)).State), "and is napping");
        Equal(false, PetSimulation.IsNapping(sim.Step(rabbit, At(3, 15, 30)).State), "awake again after school");
        Equal(false, PetSimulation.IsNapping(sim.Step(rabbit, At(3, 23, 30)).State), "and through the night");
        var fed = sim.Care(rabbit, PetVerb.Greens).State;
        Equal(CareReason.MealOnTime, fed.Care.LastReason, "greens before school are breakfast on time");
        Equal(MomentStatus.OnTime, fed.Care.StatusOf(CareMoment.Morning), "breakfast is done");
        var dinner = sim.Care(Rabbit(At(3, 18, 0), fullness: 50), PetVerb.Greens).State;
        Equal(MomentStatus.OnTime, dinner.Care.StatusOf(CareMoment.Evening), "greens at 18:00 are dinner");
        var lateDinner = sim.Care(Rabbit(At(3, 21, 0), fullness: 50), PetVerb.Greens).State;
        Equal(CareReason.MealLate, lateDinner.Care.LastReason, "after 20:30 dinner is late");
        var missed = sim.Step(Rabbit(At(3, 16, 0)), At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, missed.Care.StatusOf(CareMoment.Evening), "no greens by 21:30 misses dinner");
        Equal(CareTags.Meal, missed.Care.Owed & CareTags.Meal, "a missed meal is owed");
        var doze = sim.Step(Rabbit(At(3, 7, 30)), At(3, 16, 0)).State;
        Equal(MomentStatus.Excused, doze.Care.StatusOf(CareMoment.Bedtime), "the doze asks nothing and closes excused");
        Equal(CareTags.None, doze.Care.Owed & CareTags.Bedtime, "and is never owed");
        var adopted = sim.Adopt("Clover", Dutch, At(3, 16, 0));
        var first = sim.Step(adopted, At(3, 23, 0)).State;
        Equal(MomentStatus.Excused, first.Care.StatusOf(CareMoment.Evening), "the first day's windows are excused");
        Equal(5, first.Care.MomentCode.Length, "a rabbit's ledger keeps five moments");
    }

    // A dawn floor time that closes with grace when school gets in the way, floor time after school that keeps the together
    // ring, and the doze, through which a content rabbit sleeps on.
    private static void FloorTime()
    {
        var sim = Sim();
        var morning = Rabbit(At(3, 7, 15), comfort: 60, bond: 30);
        var dawn = sim.Care(morning, PetVerb.FloorTime).State;
        Equal(CareReason.PlayOnTime, dawn.Care.LastReason, "floor time at 7:15 is the dawn floor time, on time");
        Equal(MomentStatus.OnTime, dawn.Care.StatusOf(CareMoment.Dawn), "the dawn floor time is done");
        Equal(MomentStatus.Pending, dawn.Care.StatusOf(CareMoment.Afternoon), "floor time after school still waits");
        Equal("O", dawn.Care.TogetherDays.ToCode(), "it keeps the together ring");
        Near(60 + 15, dawn.Meter(PetMeter.Comfort), "floor time gives Comfort +15");
        Near(30 + 4, dawn.Bond, "and Bond +4");

        // School gets in the way: breakfast but no dawn floor time, and nothing is missed, owed or lost.
        var noon = sim.Step(sim.Care(morning, PetVerb.Greens).State, At(3, 11, 30)).State;
        Equal(MomentStatus.Excused, noon.Care.StatusOf(CareMoment.Dawn), "the dawn floor time closes with grace at 10:00");
        Equal("", noon.Care.TogetherDays.ToCode(), "it never enters the together ring");
        Equal(CareTags.None, noon.Care.Owed, "and is never owed");
        Equal(CareMood.Content, noon.Care.Mood, "the rabbit is content");
        Equal(true, noon.Bond >= morning.Bond, "and it costs no Bond");
        var dozing = sim.Care(noon, PetVerb.FloorTime);
        Equal(PetCue.Napping, dozing.Cue, "before lunch a content rabbit dozes on");
        Equal(noon.Meter(PetMeter.Comfort), dozing.State.Meter(PetMeter.Comfort), "floor time waits for the afternoon");
        Equal(false, dozing.State.Care.TogetherToday, "and is not counted");
        var hay = sim.Care(noon, PetVerb.Hay);
        Equal(PetCue.Content, hay.Cue, "the hay rack can be filled round a dozing rabbit");

        var after = sim.Care(sim.Step(noon, At(3, 16, 0)).State, PetVerb.FloorTime).State;
        Equal(CareReason.PlayOnTime, after.Care.LastReason, "floor time at 16:00 is on time");
        Equal(MomentStatus.OnTime, after.Care.StatusOf(CareMoment.Afternoon), "floor time is done");
        var noFloor = sim.Step(noon, At(3, 20, 15)).State;
        Equal(MomentStatus.Missed, noFloor.Care.StatusOf(CareMoment.Afternoon), "no floor time by 20:00 misses it");
        Equal("M", noFloor.Care.TogetherDays.ToCode(), "a missed together day");
        Equal(CareTags.None, noFloor.Care.Owed & ~CareTags.Meal, "floor time is never owed");
        var groomed = sim.Care(sim.Step(noon, At(3, 21, 0)).State, PetVerb.Groom).State;
        Equal(true, groomed.Care.TogetherToday, "a groom still counts as the day's together time");

        // A school day: breakfast before school, floor time, a groom, the litter and dinner after school.
        var day = Rabbit(At(3, 7, 30), fullness: 60);
        day = sim.Care(day, PetVerb.Greens).State;
        day = sim.Care(day, PetVerb.Hay).State;
        day = sim.Step(day, At(3, 17, 45)).State;
        foreach (var verb in new[] { PetVerb.FloorTime, PetVerb.Groom, PetVerb.Litter, PetVerb.Greens, PetVerb.Hay })
            day = sim.Care(day, verb).State;
        var closed = sim.Step(day, At(4, 7, 0)).State;
        Equal(closed.Care.CareDay - 1, closed.Care.GoodDay, "a school day is a good day: the dawn grace and the doze excuse nothing");
        Equal(1, sim.Bonding(closed).Rhythm, "and it counts for the care rhythm");
    }

    // The hay rack: Hay plays the Energy role. It empties through the waking hours (evening and night too), holds through
    // the doze, is filled by hand (no window, no overfeeding) and an empty rack makes the rabbit twitchy.
    private static void HayRack()
    {
        var sim = Sim();
        // A Dutch rabbit grazes its hay 15% faster (its tendency, traded for slower Fullness).
        var rate = Catalog.Get(Species.Rabbit).Meters.Single(meter => meter.Meter == PetMeter.Hay).DecayPerHour *
            Catalog.Get(PetBreed.Dutch).DecayFactor(PetMeter.Hay);
        var morning = Rabbit(At(3, 8, 0), hay: 80);
        Near(80 - 2 * rate, sim.Step(morning, At(3, 10, 0)).State.Meter(PetMeter.Hay), "the rack empties while the rabbit is awake");
        var doze = sim.Step(morning, At(3, 15, 0)).State;
        Near(80 - 2 * rate, doze.Meter(PetMeter.Hay), "and holds through the doze");
        var evening = Rabbit(At(3, 20, 0), hay: 80);
        Near(80 - 4 * rate, sim.Step(evening, At(4, 0, 0)).State.Meter(PetMeter.Hay), "a rabbit grazes through the night");
        var filled = sim.Care(evening, PetVerb.Hay);
        Near(100, filled.State.Meter(PetMeter.Hay), "a refill gives Hay +50 (to the top)");
        Equal(PetCue.Content, filled.Cue, "hay is never too much");
        Equal(CareReason.NoRoutineTag, filled.State.Care.LastReason, "and answers no window");
        Equal(evening.Bond, filled.State.Bond, "it is upkeep, not Bond");
        var full = sim.Care(filled.State, PetVerb.Hay);
        Equal(PetCue.Content, full.Cue, "a full rack takes more hay without fuss");
        Near(100, full.State.Meter(PetMeter.Hay), "and stays full");
        Near(filled.State.Meter(PetMeter.Comfort), full.State.Meter(PetMeter.Comfort), "with no overfeeding");

        var empty = sim.Step(Rabbit(At(3, 18, 0), hay: 30, bond: 30), At(3, 18, 5)).State;
        Equal(CareMood.Fussy, empty.Care.Mood, "an empty hay rack makes the rabbit twitchy");
        var refilled = sim.Care(empty, PetVerb.Hay);
        Equal(CareMood.Content, refilled.State.Care.Mood, "a refill settles it");
        Near(empty.Bond + 5, refilled.State.Bond, "answering within half an hour builds Bond +5");

        // A poorly rabbit's rack can always be filled: Hay is no rest meter, so the unwell cap does not apply.
        var poorly = sim.Step(Rabbit(At(3, 18, 0), hay: 30, health: 40), At(3, 18, 30)).State;
        Equal(true, poorly.Care.IsUnwell, "under the weather");
        Near(poorly.Meter(PetMeter.Hay) + 50, sim.Care(poorly, PetVerb.Hay).State.Meter(PetMeter.Hay), "the rack fills past the unwell cap");
        Equal(true, poorly.Meter(PetMeter.Hay) + 50 > Tunables.UnwellEnergyCap, "(which it passes here)");
        var tiredCat = CatAt(At(3, 18, 0), energy: 40, health: 40);
        tiredCat = sim.Step(tiredCat, At(3, 18, 30)).State;
        Near(Tunables.UnwellEnergyCap, sim.Care(tiredCat, PetVerb.Nap).State.Meter(PetMeter.Energy),
            "a poorly cat's nap still stops at the unwell cap");
    }

    // Two greens a day and a hay rack: an attentive player and a busy school-day player both keep a content rabbit, and
    // greens never overfeed at normal play.
    private static void TwoGreensTuning()
    {
        var species = Catalog.Get(Species.Rabbit);
        var rate = species.Meters.ToDictionary(meter => meter.Meter, meter => meter.DecayPerHour);
        var profile = PetCareProfile.Rabbit;
        // Awake 15:00 to 10:00 the next day (19 h) at the awake rates, the doze 10:00 to 15:00 (5 h) at the night rates.
        var fullness = 19 * rate[PetMeter.Fullness] + 5 * profile.NightFullness;
        var hay = 19 * rate[PetMeter.Hay];
        var comfort = 19 * rate[PetMeter.Comfort] + 5 * profile.NightComfort;
        var litter = 19 * rate[PetMeter.Litter] + 5 * profile.NightCleanliness;
        double Gain(PetVerb verb, PetMeter meter) => species.FindVerb(verb).Effects.Single(effect => effect.Meter == meter).Delta;
        var greens = Gain(PetVerb.Greens, PetMeter.Fullness);
        Near(64.5, fullness, "a day costs 64.5 Fullness");
        Equal(true, fullness < 2 * greens, "two greens (+35) cover a day");
        Near(76, hay, "the rack empties 76 a day");
        Equal(true, hay < 2 * Gain(PetVerb.Hay, PetMeter.Hay), "two refills (+50) cover a day");
        Near(31, comfort, "a day costs 31 Comfort");
        var floor = Gain(PetVerb.FloorTime, PetMeter.Comfort);
        var groom = Gain(PetVerb.Groom, PetMeter.Comfort);
        Equal(true, comfort < 2 * floor + groom, "the dawn floor time, floor time and a groom cover it");
        Near(31, litter, "a day costs 31 Litter");
        Equal(true, litter < Gain(PetVerb.Litter, PetMeter.Litter), "one litter tidy (+40) covers it");
        // The slowest-eating breed after the shortest gap between two greens of normal play (a late breakfast at 11:00 and
        // dinner when it opens, 17:30; the doze between) still finds room for dinner.
        var slowest = Catalog.BreedsOf(Species.Rabbit).Min(breed => breed.DecayFactor(PetMeter.Fullness));
        Equal(true, 100 - (4 * profile.NightFullness + 2.5 * rate[PetMeter.Fullness] * slowest) < Tunables.FullMealThreshold,
            "a late breakfast and an early dinner never overfeed");

        var sim = Sim();
        var lowest = 100.0;
        var lowestMeal = 100.0;
        var highestMeal = 0.0;
        foreach (var (name, plan) in new[] { ("attentive", Attentive), ("busy", Busy) })
        {
            var rabbit = sim.Adopt("Clover", Dutch, At(2, 18, 0));
            var personaLowest = 100.0;
            for (var day = 3; day <= 23; day++)
            {
                foreach (var (hour, minute, verbs) in plan)
                {
                    rabbit = sim.Step(rabbit, At(day, hour, minute)).State;
                    if (day > 3)
                        personaLowest = Math.Min(personaLowest, Lowest(rabbit));
                    foreach (var verb in verbs)
                    {
                        if (verb == PetVerb.Greens && day > 3)
                        {
                            lowestMeal = Math.Min(lowestMeal, rabbit.Meter(PetMeter.Fullness));
                            highestMeal = Math.Max(highestMeal, rabbit.Meter(PetMeter.Fullness));
                        }

                        var step = sim.Care(rabbit, verb);
                        Equal(true, step.Cue != PetCue.TooFull, $"{name} day {day}: greens are never overfeeding");
                        rabbit = step.State;
                    }
                }
            }

            Equal(true, personaLowest >= 45, $"{name}: no need falls below 45 (lowest {personaLowest:0.0})");
            Equal(CareMood.Content, rabbit.Care.Mood, $"{name}: the rabbit stays content");
            Equal(true, sim.Bonding(rabbit).Rhythm >= 14, $"{name}: every day counts for the rhythm ({sim.Bonding(rabbit).Rhythm})");
            lowest = Math.Min(lowest, personaLowest);
            Console.WriteLine($"  rabbit {name} player: lowest need {personaLowest:0.0}, Bond {rabbit.Bond:0}");
        }

        Equal(true, highestMeal < Tunables.FullMealThreshold, $"greens find it peckish but never full ({lowestMeal:0.0} to {highestMeal:0.0})");

        // Every breed is the same: the busy player keeps each one content.
        var breedLowest = 100.0;
        foreach (var breed in Catalog.BreedsOf(Species.Rabbit))
        {
            var rabbit = sim.Adopt("Clover", PetAppearance.Default(Catalog, breed.Breed), At(2, 18, 0));
            var low = 100.0;
            for (var day = 3; day <= 16; day++)
            {
                foreach (var (hour, minute, verbs) in Busy)
                {
                    rabbit = sim.Step(rabbit, At(day, hour, minute)).State;
                    if (day > 3)
                        low = Math.Min(low, Lowest(rabbit));
                    foreach (var verb in verbs)
                    {
                        var step = sim.Care(rabbit, verb);
                        Equal(true, step.Cue != PetCue.TooFull, $"{breed.Breed} day {day}: greens are never overfeeding");
                        rabbit = step.State;
                    }
                }
            }

            Equal(CareMood.Content, rabbit.Care.Mood, $"the busy player keeps a {breed.Breed} content");
            Equal(true, low >= 40, $"{breed.Breed} never twitchy ({low:0.0})");
            breedLowest = Math.Min(breedLowest, low);
        }

        // No hay at all: the rack runs dry and the rabbit is twitchy within about a day.
        var noHay = sim.Adopt("Clover", Dutch, At(2, 18, 0));
        var twitchyOn = 0.0;
        for (var hours = 1; hours <= 24 * 4 && twitchyOn == 0; hours++)
        {
            noHay = Run(sim, noHay, NoHay, At(2, 18, 0).AddHours(hours - 1), At(2, 18, 0).AddHours(hours));
            if (noHay.Care.Mood != CareMood.Content)
                twitchyOn = hours / 24.0;
        }

        Equal(true, twitchyOn > 0 && noHay.Meter(PetMeter.Hay) < 35, $"with no hay the rabbit turns twitchy (day {twitchyOn:0.0})");
        Console.WriteLine($"  rabbit two-greens tuning: a day costs Fullness {fullness:0.0} (greens +35 x2), Hay {hay:0} " +
            $"(refill +50), Comfort {comfort:0.0} (floor time +15, groom +12), Litter {litter:0.0} (tidy +40); lowest need " +
            $"{lowest:0.0} (busy, every breed {breedLowest:0.0}), greens at Fullness {lowestMeal:0.0} to {highestMeal:0.0}; " +
            $"no hay: twitchy after {twitchyOn:0.0} days");
    }

    private static void Moods()
    {
        var sim = Sim();
        var at = At(3, 17, 0);
        var lonely = Rabbit(at, comfort: 30, bond: 30);
        var twitchy = sim.Step(lonely, at.AddMinutes(5)).State;
        Equal(CareMood.Fussy, twitchy.Care.Mood, "Comfort below 35 makes the rabbit twitchy");
        Equal(CareMoodCause.LowNeed, twitchy.Care.MoodCause, "because a need is low");
        var answered = sim.Care(sim.Step(twitchy, at.AddMinutes(20)).State, PetVerb.FloorTime);
        Equal(CareMood.Content, answered.State.Care.Mood, "floor time settles it");
        Near(twitchy.Bond + 4 + 5, answered.State.Bond, "floor time +4 and answering within half an hour +5");

        var hungry = sim.Step(Rabbit(at, fullness: 25, comfort: 60, bond: 30), at.AddMinutes(5)).State;
        var thumping = hungry;
        for (var minutes = 30; thumping.Care.Mood != CareMood.Upset && minutes <= 300; minutes += 20)
            thumping = sim.Step(thumping, at.AddMinutes(minutes)).State;
        Equal(CareMood.Upset, thumping.Care.Mood, "twitchiness left unanswered turns to thumping");
        var refused = sim.Care(thumping, PetVerb.FloorTime);
        Equal(PetCue.PushesToyAway, refused.Cue, "a thumping rabbit will not come out for floor time");
        Equal(thumping.Meter(PetMeter.Comfort), refused.State.Meter(PetMeter.Comfort), "and floor time gives nothing");
        var fed = sim.Care(thumping, PetVerb.Greens).State;
        Equal(CareMood.Upset, fed.Care.Mood, "greens alone do not settle a thumping rabbit");
        var groomed = sim.Care(fed, PetVerb.Groom).State;
        Equal(true, groomed.Care.Mood < CareMood.Upset, "a gentle groom once fed settles it");
    }

    private static void Overfeeding()
    {
        var sim = Sim();
        var full = Rabbit(At(3, 18, 0), fullness: 95, comfort: 70, litter: 80);
        Equal(true, sim.IsFull(full), "IsFull warns before it happens");
        Equal(false, sim.IsFull(Rabbit(At(3, 18, 0), fullness: 89)), "89 is not full");
        var over = sim.Care(full, PetVerb.Greens);
        Equal(PetCue.TooFull, over.Cue, "a full rabbit nibbles a leaf and flops");
        Equal(CareReason.MealTooFull, over.State.Care.LastReason, "it is not its dinner");
        Equal(MomentStatus.Pending, over.State.Care.StatusOf(CareMoment.Evening), "dinner still waits");
        Near(70 - Tunables.Standard.OverfeedComfort, over.State.Meter(PetMeter.Comfort), "a little less comfortable");
        Near(80 - Tunables.Standard.OverfeedCleanliness, over.State.Meter(PetMeter.Litter), "and a messier tray");
        Equal(full.Bond, over.State.Bond, "overfeeding builds no Bond");
        Equal(100.0, over.State.Health, "and never harms Health");
    }

    // A missed litter tidy leaves scattered hay (the mess); the next tidy clears it.
    private static void ScatteredHay()
    {
        var sim = Sim();
        var rabbit = Rabbit(At(3, 8, 0));
        var untidy = sim.Step(rabbit, At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, untidy.Care.Wash, "no tidy by 21:30 misses the day's litter");
        Equal(1, untidy.Care.Mess, "and leaves scattered hay");
        var tidied = sim.Care(sim.Step(untidy, At(4, 8, 0)).State, PetVerb.Litter);
        Equal(0, tidied.State.Care.Mess, "a litter tidy clears it");
        Equal(CareReason.WashOnTime, tidied.State.Care.LastReason, "and is the day's tidy");
        var groomed = sim.Care(sim.Step(untidy, At(4, 8, 0)).State, PetVerb.Groom).State;
        Equal(1, groomed.Care.Mess, "a groom leaves the hay where it lies");
        var cut = Rabbit(At(3, 8, 0), comfort: 50, mess: 2);
        Near(50 + 15 * (1 - 2 * Tunables.Standard.MessComfortCut), sim.Care(cut, PetVerb.FloorTime).State.Meter(PetMeter.Comfort),
            "scattered hay cuts comfort gains a little");
        var three = rabbit;
        for (var day = 3; day <= 6; day++)
        {
            three = sim.Care(sim.Step(three, At(day, 18, 0)).State, PetVerb.Greens).State;
            three = sim.Step(three, At(day, 22, 0)).State;
        }

        Equal(CareRecord.MaxMess, three.Care.Mess, "scattered hay stops at three missed tidies");
    }

    // Away mode: a neighbour pops in with greens and hay. Nothing is missed, and a happy rabbit binkies when floor time
    // comes back.
    private static void Away()
    {
        var sim = Sim();
        var rabbit = sim.Care(Rabbit(At(3, 16, 0), bond: 40, milestones: 1), PetVerb.FloorTime).State;
        var away = sim.StartAway(rabbit, TimeSpan.FromDays(3));
        Equal(true, away.Care.IsAway, "Away mode starts");
        var later = sim.Step(away, At(5, 22, 0)).State;
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Evening), "the neighbour's greens are covered");
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Afternoon), "and floor time");
        Equal(CareMood.Content, later.Care.Mood, "the rabbit stays content");
        Equal(true, later.Meter(PetMeter.Hay) >= 60 - 1e-9 && later.Meter(PetMeter.Fullness) >= 60 - 1e-9, "needs are held at 60");
        Equal(true, later.Bond >= rabbit.Bond, "Away costs no Bond");
        Equal(SampleMode.Away, later.Development.Sample.Mode, "development keeps the nominal pace");
        var back = sim.Care(later, PetVerb.FloorTime).State;
        Equal(false, back.Care.IsAway, "any care ends Away mode");
        Equal(true, PetReunion.Binkies(back, Catalog) > 0, "and floor time with a happy rabbit ends in binkies");
    }

    private static void HealthAndVet()
    {
        var sim = Sim();
        var at = At(3, 17, 0);
        var poorly = Rabbit(at, fullness: 10, health: 35);
        var unwell = sim.Step(poorly, at.AddMinutes(30)).State;
        Equal(CareCondition.UnderTheWeather, unwell.Care.Condition, "Health below 50 puts the rabbit under the weather");
        Equal(true, PetSimulation.CanVisitVet(unwell), "the vet is offered");
        var sick = unwell;
        for (var minutes = 40; sick.Care.Condition != CareCondition.Sick && minutes <= 90; minutes += 5)
            sick = sim.Step(sick, at.AddMinutes(minutes)).State;
        Equal(CareCondition.Sick, sick.Care.Condition, "while present, Health below 30 makes it sick");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.FloorTime).Cue, "a sick rabbit will not come out for floor time");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.Groom).Cue, "or be groomed");
        Equal(sick.Bond, sim.Care(sick, PetVerb.FloorTime).State.Bond, "and refused care builds nothing");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.Greens).Cue, "but it eats its greens");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.Hay).Cue, "nibbles its hay");
        var tray = sim.Care(sick, PetVerb.Litter);
        Equal(PetCue.Content, tray.Cue, "and a fresh tray always helps");
        Equal(true, tray.State.Meter(PetMeter.Litter) > sick.Meter(PetMeter.Litter), "the tray is fresher");

        var visit = sim.VisitVet(sick);
        Equal(PetCue.VetVisited, visit.Cue, "Dr. Rosa's visit");
        Equal(CareCondition.Recovering, visit.State.Care.Condition, "a sick rabbit starts recovering");
        Near(sick.Health + 15, visit.State.Health, "the vet restores Health +15 at once");
        Equal(MedicineState.Ready, PetSimulation.Medicine(visit.State), "dinner is a medicine window");
        var first = sim.GiveMedicine(visit.State);
        Equal(PetCue.MedicineTaken, first.Cue, "the first dose, with dinner");
        Equal(MedicineState.GivenThisMeal, PetSimulation.Medicine(first.State), "one dose per meal");
        var tomorrow = sim.Step(first.State, At(4, 8, 0)).State;
        Equal(MedicineState.Ready, PetSimulation.Medicine(tomorrow), "the next dose goes with breakfast");
        var second = sim.GiveMedicine(tomorrow);
        Equal(PetCue.MedicineDone, second.Cue, "two doses end Recovering");
        Equal(CareCondition.Well, second.State.Care.Condition, "the rabbit is well again");
        Equal(true, second.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Poorly),
            "the journal keeps a hard day we got through");
        Equal(PetCue.VetAllClear, sim.VisitVet(Rabbit(at)).Cue, "a well rabbit needs no vet");

        var left = sim.Step(poorly, At(5, 18, 0)).State;
        Equal(true, left.Care.Condition != CareCondition.Sick, "an absence never passes under the weather");
        Equal(true, left.Health >= CareConsequences.HealthFloor, "Health stays at the floor or above");
    }

    private static void MilestoneTiming()
    {
        var profile = PetCareProfile.Rabbit;
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

        Equal(true, previous[2].TotalDays < 7, "excellent care brings the flop of trust before the rabbit grows up");
        Equal(true, 1.4 * profile.SkillAges[2].TotalDays <= Catalog.Get(Species.Rabbit).GrownAge.TotalDays + 1e-9,
            "and every care style by day 7, when it grows up");
        var attentive = Play(Attentive, 9);
        var busy = Play(Busy, 9);
        var neglected = Play(Neglect, 9);
        for (var i = 0; i < 3; i++)
        {
            var nominal = profile.SkillAges[i].TotalDays;
            foreach (var (name, ages) in new[] { ("attentive", attentive), ("busy", busy), ("neglected", neglected) })
                Equal(true, ages[i] >= 0.8 * nominal - 0.05 && ages[i] <= 1.4 * nominal + 0.05,
                    $"{name} {profile.Skills[i]} within bounds ({ages[i]:0.00})");
            Equal(true, attentive[i] <= busy[i] + 0.05 && busy[i] <= neglected[i] + 0.05, $"{profile.Skills[i]} follows the play style");
        }

        Equal(true, attentive[2] < neglected[2], "attentive care brings the flop of trust sooner than neglect");
        Console.WriteLine("  rabbit milestones (first binky, hop circuit, flop of trust): steady " + string.Join(", ", rows) +
            $"; scripted attentive {Days(attentive)}, busy {Days(busy)}, neglect {Days(neglected)}");
    }

    private static void Setback()
    {
        var sim = Sim();
        // A young rabbit that has done its first binky. Floor time keeps the player around, and greens given late each night
        // (after dinner's window has closed) keep it fed while three meals in a row are missed: dinner on day 3, breakfast
        // and dinner on day 4.
        var rabbit = Rabbit(At(3, 16, 0), fullness: 70, milestones: 1);
        Equal(true, rabbit.HasReached(MotorSkill.FirstBinky), "the young rabbit has done its first binky");
        rabbit = sim.Care(rabbit, PetVerb.FloorTime).State;
        rabbit = sim.Care(sim.Step(rabbit, At(3, 22, 0)).State, PetVerb.Greens).State;
        rabbit = sim.Care(sim.Step(rabbit, At(4, 16, 0)).State, PetVerb.FloorTime).State;
        var before = sim.Step(rabbit, At(4, 21, 35)).State;
        Equal("MMM", before.Care.Meals.ToCode(), "three meals missed");
        rabbit = sim.Step(before, At(4, 21, 55)).State;
        Equal(rabbit.NewestSkill, rabbit.ShySkill, "three missed meals while present: the newest skill goes shy");
        Equal(true, rabbit.ShySkill != MotorSkill.None, "a skill is shy");
        Equal(true, rabbit.HasReached(rabbit.ShySkill), "a shy skill stays reached");
        Equal(false, rabbit.Shows(rabbit.ShySkill), "but does not show");
        Equal(0, PetReunion.Binkies(rabbit, Catalog), "a shy first binky means no binkies for now");

        var shy = rabbit.ShySkill;
        var morning = sim.Step(rabbit, At(5, 7, 30)).State;
        Equal(CareMood.Upset, morning.Care.Mood, "after three missed meals the rabbit is thumping");
        var step = sim.Care(morning, PetVerb.Greens);
        step = sim.Care(step.State, PetVerb.Hay);
        step = sim.Care(step.State, PetVerb.Groom);
        Equal(true, step.State.Care.Mood < CareMood.Upset, "greens and hay, then a groom, settle it");
        Equal(shy, step.State.ShySkill, "still shy before the good day is complete");
        step = sim.Care(sim.Step(step.State, At(5, 16, 0)).State, PetVerb.FloorTime);
        step = sim.Care(sim.Step(step.State, At(5, 18, 0)).State, PetVerb.Greens);
        Equal(PetCue.SkillBack, step.Cue, "both greens on time and two calm moments (a groom, floor time) bring it back");
        Equal(MotorSkill.None, step.State.ShySkill, "the skill shows again");
        Equal(true, step.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Shy &&
            entry.Skill == shy), "the journal remembers the shy spell");
    }

    private static void BondTrustRhythmJournal()
    {
        var sim = Sim();
        var rabbit = Rabbit(At(3, 18, 0), fullness: 50, bond: 20);
        var fed = sim.Care(rabbit, PetVerb.Greens).State;
        Near(21, fed.Bond, "on-time greens +1");
        Near(25, sim.Care(fed, PetVerb.FloorTime).State.Bond, "floor time +4");
        Near(23, sim.Care(fed, PetVerb.Groom).State.Bond, "a groom +2");
        Near(21, sim.Care(fed, PetVerb.Litter).State.Bond, "a litter tidy is upkeep");
        Near(21, sim.Care(fed, PetVerb.Hay).State.Bond, "and so is hay");

        // Sitting on the floor builds Bond faster: three floor times reach the day's cap, where grooming takes six.
        var floor = fed;
        var grooms = fed;
        for (var i = 0; i < 3; i++)
        {
            floor = sim.Care(floor, PetVerb.FloorTime).State;
            grooms = sim.Care(grooms, PetVerb.Groom).State;
        }

        Near(32, floor.Bond, "three floor times: Bond +11, the rest of the day's cap of 12");
        Near(27, grooms.Bond, "three grooms: Bond +6");
        Equal(true, floor.Bond > grooms.Bond, "the floor is the fast way to a rabbit's heart");
        for (var i = 0; i < 3; i++)
            grooms = sim.Care(grooms, PetVerb.Groom).State;
        Near(32, grooms.Bond, "grooming gets there too, in twice the visits");
        var capped = floor;
        for (var i = 0; i < 5; i++)
            capped = sim.Care(capped, PetVerb.FloorTime).State;
        Near(32, capped.Bond, "gains stop at the daily cap of 12");
        Equal(capped.Bond, capped.HighestBond, "HighestBond follows Bond for the earned-breed goal");

        var missed = sim.Step(Rabbit(At(3, 21, 0), bond: 50), At(3, 21, 45)).State;
        Equal(true, missed.Bond < 50, "missed greens cost Bond");
        var lowFloor = sim.Step(Rabbit(At(3, 18, 0), bond: 50), At(20, 18, 0)).State;
        Equal(true, lowFloor.Bond >= 0.6 * 50 - 1e-9, "Bond never falls below 60% of its high-water mark");

        var attentive = Days(sim, Attentive, 12);
        var busy = Days(sim, Busy, 12);
        var neglected = Days(sim, Neglect, 12);
        var bonding = sim.Bonding(attentive);
        Equal(true, bonding.Rhythm >= 8, $"a care day counts with both greens and together time (rhythm {bonding.Rhythm})");
        Equal(true, sim.Bonding(busy).Rhythm >= 8, $"a busy school day counts too (rhythm {sim.Bonding(busy).Rhythm})");
        Equal(true, sim.Trust(attentive) != SecurityPhase.Clingy, "attentive care builds Trust");
        Equal(true, bonding.Security > sim.Bonding(neglected).Security, "missed meals cost Trust");
        Equal(true, attentive.Bond > neglected.Bond, "attentive care builds more Bond");
        Equal(true, attentive.Bond >= busy.Bond, "and a little more than a busy day");
        Equal(0, sim.Bonding(neglected).Rhythm, "neglect has no rhythm");
        var journal = sim.Journal(attentive).Entries;
        Equal(JournalKind.Arrival, journal[0].Kind, "the journal starts with the arrival");
        Equal("FirstBinky,HopCircuit,FlopOfTrust",
            string.Join(",", journal.Where(entry => entry.Kind == JournalKind.Milestone).Select(entry => entry.Skill)),
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
        Equal(MotorSkill.FlopOfTrust, shot.Skill, "a photo keeps the stage shown");
    }

    // The signature moment: floor time with a happy, well-cared rabbit ends in binkies (and a hop circuit once learned).
    // Display only.
    private static void Binkies()
    {
        var sim = Sim();
        var young = sim.Care(Rabbit(At(3, 16, 0), milestones: 0), PetVerb.FloorTime).State;
        Equal(0, PetReunion.Binkies(young, Catalog), "no binkies before its first");
        var happy = sim.Care(Rabbit(At(3, 16, 0), milestones: 1), PetVerb.FloorTime).State;
        Equal(1, PetReunion.Binkies(happy, Catalog), "a happy rabbit binkies once");
        Equal(false, PetReunion.HopCircuit(happy, Catalog), "with no hop circuit yet");
        var trusting = sim.Care(Rabbit(At(3, 16, 0), bond: 45, milestones: 2), PetVerb.FloorTime).State;
        Equal(2, PetReunion.Binkies(trusting, Catalog), "twice once it trusts you");
        Equal(true, PetReunion.HopCircuit(trusting, Catalog), "after a hop circuit round the room");
        var friends = sim.Care(Rabbit(At(3, 16, 0), bond: 80, milestones: 3), PetVerb.FloorTime).State;
        Equal(3, PetReunion.Binkies(friends, Catalog), "three in a row for best friends");
        var hungry = sim.Care(Rabbit(At(3, 16, 0), fullness: 40, milestones: 1), PetVerb.FloorTime).State;
        Equal(0, PetReunion.Binkies(hungry, Catalog), "a hungry rabbit enjoys the company but does not binky");
        var noHay = sim.Care(Rabbit(At(3, 16, 0), hay: 45, milestones: 1), PetVerb.FloorTime).State;
        Equal(0, PetReunion.Binkies(noHay, Catalog), "nor one whose hay rack is low");
        var unwell = sim.Step(Rabbit(At(3, 16, 0), health: 40, milestones: 1), At(3, 16, 30)).State;
        Equal(0, PetReunion.Binkies(sim.Care(unwell, PetVerb.FloorTime).State, Catalog), "nor a poorly one");
        Equal(0, PetReunion.Binkies(sim.Step(Rabbit(At(3, 9, 0), milestones: 1), At(3, 12, 0)).State, Catalog),
            "nor one dozing at noon");
        var dog = sim.Care(DogAt(At(3, 16, 0)), PetVerb.Fetch).State;
        Equal(0, PetReunion.Binkies(dog, Catalog), "binkies are the rabbit's signature moment");
        Equal(happy.Bond, sim.Care(Rabbit(At(3, 16, 0), milestones: 0), PetVerb.FloorTime).State.Bond,
            "binkies are display only: they add no Bond");
        var line = PetReunion.BinkyLine("Clover", 3, true);
        Equal(true, line.Contains("Clover") && line.Contains("binkies"), "the line names the rabbit");
        Equal(null, PetReunion.BinkyLine("Clover", 0, false), "no binkies, no line");
        foreach (var word in new[] { "sorry", "lonely", "miss", "forgot", "sad", "waited", "finally", "alone", "hungry" })
            Equal(false, line.ToLowerInvariant().Contains(word) || PetReunion.BinkyLine("Clover", 1, false).ToLowerInvariant().Contains(word),
                $"the binky line never says \"{word}\"");
    }

    // The pellet feeder, the willow tunnel and the litter tray liner: the cat's helpers as a rabbit's objects. None of them
    // is floor time, and none fills the hay rack.
    private static void Helpers()
    {
        var sim = Sim();
        var rabbit = sim.StockFeeder(Rabbit(At(3, 17, 30), fullness: 48, comfort: 42, litter: 42, hay: 90));
        Equal(6, rabbit.Care.Helpers.Stock, "the pellet feeder is stocked");
        var events = new List<HelperEvent>();
        var helped = sim.Step(rabbit, At(4, 1, 0), HelperSettings.All, events).State;
        Equal(true, events.Any(e => e.Kind == HelperKind.Feeder), "the pellet feeder serves dinner");
        Equal(true, events.Any(e => e.Kind == HelperKind.Play), "the willow tunnel keeps the rabbit happy");
        Equal(true, events.Any(e => e.Kind == HelperKind.Tidy), "the tray liner keeps the litter fresh");
        Equal(MomentStatus.Helped, helped.Care.StatusOf(CareMoment.Evening), "dinner from the feeder is Helped");
        Equal(MomentStatus.Missed, helped.Care.StatusOf(CareMoment.Afternoon), "but no helper is floor time");
        Equal(true, helped.Meter(PetMeter.Comfort) >= HelperRules.Default.PlayFloor - 1e-9, "Comfort is held at the tunnel's floor");
        Equal(true, helped.Meter(PetMeter.Litter) >= HelperRules.Default.TidyFloor - 1e-9, "Litter is held at the liner's floor");
        var plain = sim.Step(rabbit, At(4, 1, 0)).State;
        Near(plain.Meter(PetMeter.Hay), helped.Meter(PetMeter.Hay), "no helper fills the hay rack");
        Near(plain.Bond, helped.Bond, "helpers never build Bond");
        Equal(plain.Milestones, helped.Milestones, "or milestones");
        var payload = PetSaveCodec.Encode(helped);
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload, Catalog)), "the rabbit's helpers round-trip");
    }

    private static void PreviewDay()
    {
        var sim = Sim();
        var rabbit = sim.Adopt("Clover", Dutch, At(3, 16, 0));
        foreach (var verb in new[] { PetVerb.Greens, PetVerb.FloorTime, PetVerb.Hay })
            rabbit = sim.Care(rabbit, verb).State;
        rabbit = sim.Care(sim.Step(rabbit, At(3, 23, 0)).State, PetVerb.Greens).State;
        var next = sim.PreviewStep(rabbit, TimeSpan.FromDays(1)).State;
        Equal(rabbit.TimeCheckpoint, next.TimeCheckpoint, "a preview day passes no real time");
        Equal(rabbit.Care, next.Care, "the ledger holds");
        Equal(1, sim.Bonding(next).Rhythm, "two greens and floor time count a preview day (the doze asks nothing)");
        Equal(rabbit.Age + TimeSpan.FromDays(1), next.Age, "a day older");
        var later = next;
        for (var i = 0; i < 2; i++)
            later = sim.PreviewStep(later, TimeSpan.FromDays(1)).State;
        Equal(true, later.HasReached(MotorSkill.FirstBinky), "three preview days bring the first binky");
        var grown = later;
        for (var i = 0; i < 4; i++)
            grown = sim.PreviewStep(grown, TimeSpan.FromDays(1)).State;
        Equal(PetStage.Grown, grown.Stage, "seven preview days and the rabbit is grown");
    }

    private static void SplitAdvances()
    {
        var sim = Sim();
        var rabbit = sim.Adopt("Clover", Flemish, At(2, 18, 0));
        rabbit = Run(sim, rabbit, Attentive, At(2, 18, 0), At(4, 6, 30));
        // Through the school day and its doze, the evening and into the night; each gap stays under the 8-hour cap, which
        // is path dependent on purpose.
        foreach (var (from, to, steps) in new[]
                 {
                     (At(4, 7, 20), At(4, 15, 10), 14), (At(4, 18, 35), At(5, 2, 0), 15), (At(5, 2, 0), At(5, 7, 10), 12),
                 })
        {
            var start = Run(sim, rabbit, Attentive, At(4, 6, 30), from);
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

        // The same with breakfast but the dawn floor time skipped (its grace closes inside the gap, before the doze).
        var skipped = sim.Care(sim.Step(rabbit, At(4, 7, 30)).State, PetVerb.Greens).State;
        var wholeSkip = sim.Step(skipped, At(4, 13, 0)).State;
        var splitSkip = skipped;
        for (var i = 1; i <= 11; i++)
            splitSkip = sim.Step(splitSkip, At(4, 7, 30) + TimeSpan.FromTicks((At(4, 13, 0) - At(4, 7, 30)).Ticks * i / 11)).State;
        Equal(Ledger(wholeSkip.Care), Ledger(splitSkip.Care), "the dawn floor time closes the same split or whole");
        Equal(MomentStatus.Excused, wholeSkip.Care.StatusOf(CareMoment.Dawn), "excused either way");

        // Determinism: the same plan twice gives the same record.
        var again = Run(sim, sim.Adopt("Clover", Flemish, At(2, 18, 0)), Attentive, At(2, 18, 0), At(4, 6, 30));
        Equal(PetSaveCodec.Encode(rabbit), PetSaveCodec.Encode(again), "the same care gives the same rabbit");
    }

    // A rabbit saves as a v2 record from adoption and round-trips byte for byte; a rabbit record written with the simple
    // rules (v1, or v2 before P5) is seeded on first use like any v1 pet.
    private static void Saves()
    {
        var sim = Sim();
        var built = sim.Care(sim.Step(sim.Adopt("Clover", Flemish, At(2, 18, 0)), At(4, 16, 15)).State, PetVerb.FloorTime).State;
        var payload = PetSaveCodec.Encode(built);
        Equal(true, payload.StartsWith("<pet version=\"2\"><name>Clover</name><species>3</species><breed>406</breed>", StringComparison.Ordinal),
            "a Flemish giant saves as a v2 pet record");
        Equal(true, payload.Contains("<meter id=\"9\"") && payload.Contains("<meter id=\"10\""), "with its Hay and Litter");
        Equal(false, payload.Contains("<meter id=\"1\""), "and no Energy meter: Hay plays the role");
        Equal(true, System.Text.RegularExpressions.Regex.IsMatch(payload, " moments=\"[A-Z]{5}\""), "and five moments in its ledger");
        Equal(false, payload.Contains("tricks="), "a rabbit learns no tricks");
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload)), "and round-trips byte for byte");
        var decoded = PetSaveCodec.Decode(payload);
        Equal(CareRoutine.Rabbit, decoded.Care.Routine, "the ledger follows the rabbit's routine");
        Equal(MomentStatus.OnTime, decoded.Care.StatusOf(CareMoment.Afternoon), "floor time is kept");
        Equal(built.Milestones, decoded.Milestones, "milestones are kept");
        ExpectInvalid(payload.Replace(" milestones=\"" + built.Milestones + "\"", " milestones=\"4\""), "more milestones than a rabbit has");
        ExpectInvalid(payload.Replace("<breed>406</breed>", "<breed>204</breed>"), "a dog breed is not a rabbit");
        ExpectInvalid(payload.Replace(" milestones=\"" + built.Milestones + "\"", " milestones=\"" + built.Milestones + "\" tricks=\"2\""),
            "a rabbit learns no tricks");

        // A Dutch rabbit saved with the simple rules: its four meters and no care records.
        const string v1 =
            "<pet version=\"1\"><name>Clover</name><species>3</species><breed>400</breed>" +
            "<appearance coat=\"3\" accessory=\"1\" accent=\"2\" />" +
            "<checkpoint>639028224000000000</checkpoint><age>3456000000000</age>" +
            "<meters><meter id=\"9\" value=\"41.5\" /><meter id=\"0\" value=\"56\" /><meter id=\"10\" value=\"72\" />" +
            "<meter id=\"2\" value=\"63.2\" /><meter id=\"4\" value=\"97\" /></meters>" +
            "<bond value=\"18\" highest=\"21\" /><personality lively=\"6\" bold=\"7\" /><unlocks grown=\"false\" /></pet>";
        var old = PetSaveCodec.Decode(v1);
        Equal(2, old.Milestones, "a 4-day rabbit saved with the simple rules has its first two milestones");
        Near(41.5, old.Meter(PetMeter.Hay), "its Hay is kept");
        Near(72, old.Meter(PetMeter.Litter), "and its Litter");
        Near(63.2, old.Meter(PetMeter.Comfort), "and its Comfort");
        Equal(PetAccessory.Collar, old.Appearance.Accessory, "and its collar");
        Equal(true, PetSaveCodec.Encode(old).Contains("awaiting-anchor=\"true\""), "and a ledger waiting for its anchor");
        var anchored = sim.Step(old, new DateTimeOffset(639028224000000000, TimeSpan.Zero).AddDays(3)).State;
        Equal("", anchored.Care.Meals.ToCode(), "its first advance excuses the gap");
        Equal(18.0, anchored.Bond, "and costs no Bond");
        Equal(5, anchored.Care.MomentCode.Length, "its ledger keeps the rabbit's five moments");
        var v2Simple = PetSaveCodec.Decode(v1.Replace("version=\"1\"", "version=\"2\""));
        Equal(2, v2Simple.Milestones, "a v2 rabbit saved before P5 (no care element) migrates the same way");
        var grown = PetSaveCodec.Decode(v1.Replace("<unlocks grown=\"false\" />", "<unlocks grown=\"true\" />"));
        Equal(3, grown.Milestones, "a grown rabbit has all three");
        var migrated = PetSaveCodec.Encode(anchored);
        Equal(migrated, PetSaveCodec.Encode(PetSaveCodec.Decode(migrated)), "and saves as a rabbit on its care profile");
        ExpectInvalid(v1.Replace("<meter id=\"9\" value=\"41.5\" />", ""), "a rabbit still needs its Hay");

        // Cats, hamsters, dogs and fish are unchanged: their records still decode with their own routines.
        var cat = sim.Adopt("Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), At(3, 8, 0));
        Equal(CareRoutine.Cat, PetSaveCodec.Decode(PetSaveCodec.Encode(cat)).Care.Routine, "a cat keeps the cat's routine");
        var dog = sim.Adopt("Rex", PetAppearance.Default(Catalog, PetBreed.MixedBreed), At(3, 8, 0));
        Equal(5, PetSaveCodec.Decode(PetSaveCodec.Encode(dog)).Care.MomentCode.Length, "a dog keeps its five moments");
        var fish = sim.Adopt("Bubbles", PetAppearance.Default(Catalog, PetBreed.Goldfish), At(3, 8, 0));
        Equal(CareRoutine.Fish, PetSaveCodec.Decode(PetSaveCodec.Encode(fish)).Care.Routine, "a fish keeps the fish's routine");
    }

    private static void Breeds()
    {
        var breeds = Catalog.BreedsOf(Species.Rabbit);
        Equal("Dutch:Free,NetherlandDwarf:Earned,HollandLop:Earned,Lionhead:Premium,Angora:Premium,Rex:Premium,FlemishGiant:Premium,Harlequin:Premium",
            string.Join(",", breeds.Select(breed => breed.Breed + ":" + breed.Unlock)), "the rabbit's breed ladder");
        Equal("", string.Join(",", breeds.Where(breed => !breed.AvailableNow).Select(breed => breed.Breed)),
            "every rabbit breed ships with P5, the Lionhead and the Flemish giant too");
        Equal("400,401,402,403,404,405,406,407", string.Join(",", breeds.Select(breed => (int)breed.Breed)), "in the rabbit block");
        Equal(EarnedGoal.FirstFullCareDay, Catalog.Get(PetBreed.NetherlandDwarf).Goal, "Netherland dwarf: a first full care day");
        Equal(EarnedGoal.Bond40, Catalog.Get(PetBreed.HollandLop).Goal, "Holland lop: Bond 40");
        Equal("breed.rabbit.lionhead,breed.rabbit.angora,breed.rabbit.rex,breed.rabbit.flemish_giant,breed.rabbit.harlequin",
            string.Join(",", breeds.Where(breed => breed.Unlock == BreedUnlock.Premium).Select(breed => breed.ProductId)),
            "premium product IDs");
        Equal(true, Catalog.GrantsBreed("breedpack.rabbit", PetBreed.Harlequin), "the rabbit breed pack");
        Equal(false, Catalog.GrantsBreed("breedpack.fish", PetBreed.Harlequin), "not the fish's");
        Equal(true, Catalog.GrantsBreed(PetIds.AllBreedsPackProductId, PetBreed.Lionhead), "and the every-breed pack");
        foreach (var product in breeds.Where(breed => breed.ProductId != null).Select(breed => breed.ProductId)
                     .Concat(new[] { "breedpack.rabbit" }))
            Equal(true, DeveloperUnlocks.Products(Catalog).Contains(product), $"developer mode grants {product}");
        var species = Catalog.Get(Species.Rabbit);
        foreach (var breed in breeds)
            Near(0, breed.NetDecayChangePerHour(species), $"{breed.Breed} is no harder or easier", 1e-9);
        Catalog.Validate();

        var none = SpeciesGoalProgress.None;
        Equal("Dutch", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Rabbit, none, new string[0])),
            "a new household adopts a Dutch rabbit");
        var earned = new SpeciesGoalProgress(1, 40, true, 0);
        Equal("Dutch,NetherlandDwarf,HollandLop", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Rabbit, earned, new string[0])),
            "care earns the Netherland dwarf and the Holland lop");
        Equal("Dutch,Rex", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Rabbit, none, new[] { "breed.rabbit.rex" })),
            "buying the Rex opens the Rex");
        Equal("Dutch,Lionhead,Angora,Rex,FlemishGiant,Harlequin",
            string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Rabbit, none, new[] { "breedpack.rabbit" })),
            "the pack opens every premium rabbit");
        Equal((2, false), PetCareDays.RuleFor(Species.Rabbit), "a rabbit's care day: two meals (hay or greens) and together time");
        Equal(true, species.Offers(PetAccessory.Bow), "a rabbit can wear a bow");
    }

    private static void Household()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8), TimeSpan.FromDays(7), TimeSpan.FromDays(14));
        var pets = Sim();
        var now = At(3, 7, 0);
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        Equal(true, household.CanAdoptBreed(PetBreed.Dutch), "a Dutch rabbit is adoptable at once");
        Equal(false, household.CanAdoptBreed(PetBreed.NetherlandDwarf), "the Netherland dwarf is earned");
        Equal(false, household.CanAdoptBreed(PetBreed.Lionhead), "the Lionhead is premium");
        household.AdoptPet("Clover", Dutch);
        now = At(4, 17, 45);
        household.AdvanceAll();
        var greens = household.ApplyPetCare(2, PetVerb.Greens);
        Equal(PetCue.Content, greens.Cue, "dinner in the household");
        Equal(false, greens.CompletedCareDay, "not yet a full day");
        household.ApplyPetCare(2, PetVerb.Hay);
        var floor = household.ApplyPetCare(2, PetVerb.FloorTime);
        Equal(true, floor.CompletedCareDay, "greens, hay and floor time complete a rabbit's care day");
        Equal("NetherlandDwarf", string.Join(",", floor.UnlockedBreeds), "and earn the Netherland dwarf");
        Equal(true, household.CanAdoptBreed(PetBreed.NetherlandDwarf), "which can now be adopted");
        Equal(PetCue.VetAllClear, household.VisitPetVet(2).Cue, "the vet is there for a rabbit too");

        // A young rabbit's milestones wait as celebrations in their own manifest element.
        now = At(12, 18, 0);
        household.AdvanceAll(activeOnScreen: false);
        var manifest = System.Xml.Linq.XElement.Parse(store.ManifestPrimary);
        Equal(true, manifest.Element("rabbit-celebrations").Elements().Count() >= 1, "milestones wait in rabbit-celebrations");
        Equal(null, manifest.Element("pet-celebrations"), "never where an older build reads a cat's");
        Equal(null, manifest.Element("fish-celebrations"), "or a fish's");
        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        var played = reopened.TakeCelebrations(2).ToList();
        Equal(true, played.Contains(CompanionMilestone.FirstBinky) && played.Contains(CompanionMilestone.Grown),
            "they survive a reload: the first binky and growing up");
        Equal(true, played.IndexOf(CompanionMilestone.FirstBinky) < played.IndexOf(CompanionMilestone.Grown),
            "milestones before growing up");
    }

    // Scripted players: (hour, minute, verbs) visits each day. An attentive player sits on the floor at dawn with breakfast
    // and hay, comes back after school for floor time, a groom and the litter, and gives dinner and hay in the evening; a
    // busy school-day player gives breakfast and hay before school and does everything else in one visit when dinner
    // opens; neglect is late greens and hay at night and nothing else.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Attentive =
    {
        (7, 15, new[] { PetVerb.FloorTime, PetVerb.Greens, PetVerb.Hay }),
        (16, 0, new[] { PetVerb.FloorTime, PetVerb.Groom, PetVerb.Litter }),
        (18, 30, new[] { PetVerb.Greens, PetVerb.Hay }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Busy =
    {
        (7, 30, new[] { PetVerb.Greens, PetVerb.Hay }),
        (17, 30, new[] { PetVerb.FloorTime, PetVerb.Groom, PetVerb.Litter, PetVerb.Greens, PetVerb.Hay }),
    };

    // The attentive player who never fills the hay rack.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] NoHay =
    {
        (7, 15, new[] { PetVerb.FloorTime, PetVerb.Greens }),
        (16, 0, new[] { PetVerb.FloorTime, PetVerb.Groom, PetVerb.Litter }),
        (18, 30, new[] { PetVerb.Greens }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Neglect =
    {
        (21, 0, new[] { PetVerb.Greens, PetVerb.Hay }),
    };

    private static double Lowest(PetState state) =>
        new[] { PetMeter.Hay, PetMeter.Fullness, PetMeter.Litter, PetMeter.Comfort }.Min(state.Meter);

    private static double[] Play((int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var sim = Sim();
        var adopted = At(2, 7, 0);
        var rabbit = Run(sim, sim.Adopt("Clover", Dutch, adopted), plan, adopted, adopted.AddDays(days));
        var ages = new double[3];
        foreach (var entry in sim.Journal(rabbit).Entries.Where(entry => entry.Kind == JournalKind.Milestone))
            ages[PetCareProfile.Rabbit.IndexOf(entry.Skill)] = (entry.At - adopted).TotalDays;
        return ages;
    }

    private static PetState Days(PetSimulation sim, (int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var adopted = At(2, 7, 0);
        return Run(sim, sim.Adopt("Clover", Dutch, adopted), plan, adopted, adopted.AddDays(days));
    }

    private static PetState Run(PetSimulation sim, PetState rabbit, (int Hour, int Minute, PetVerb[] Verbs)[] plan,
        DateTimeOffset from, DateTimeOffset until)
    {
        for (var day = from.Date; day <= until.Date; day = day.AddDays(1))
        {
            foreach (var (hour, minute, verbs) in plan)
            {
                var at = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, from.Offset);
                if (at <= from || at > until)
                    continue;
                rabbit = sim.Step(rabbit, at).State;
                foreach (var verb in verbs)
                    rabbit = sim.Care(rabbit, verb).State;
            }
        }

        return sim.Step(rabbit, until).State;
    }

    private static string Strip(PetSimulation sim, PetState rabbit) =>
        string.Join(",", sim.Routine(rabbit).Select(slot => slot.Kind + ":" + slot.State));

    private static string Ledger(CareRecord care)
    {
        var element = CareRecordCodec.Encode(care);
        element.Attribute("session-start").Remove();
        element.Attribute("mood-since").Remove();
        return element.ToString();
    }

    private static string Days(double[] ages) => $"{ages[0]:0.0} / {ages[1]:0.0} / {ages[2]:0.0}";

    private static PetSimulation Sim() => new PetSimulation();

    // A rabbit on an anchored ledger whose windows all count, at the given needs.
    private static PetState Rabbit(DateTimeOffset at, double fullness = 80, double comfort = 80, double hay = 80,
        double litter = 80, double health = 100, double bond = 20, int milestones = 0, int mess = 0)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Rabbit).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        draft.Mess = mess;
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Hay] = hay, [PetMeter.Fullness] = fullness, [PetMeter.Litter] = litter,
            [PetMeter.Comfort] = comfort, [PetMeter.Health] = health,
        };
        return PetState.Restore(Catalog, "Clover", Dutch, at, TimeSpan.FromDays(1 + 1.5 * milestones), meters, bond, bond, 0, 0,
            false, draft.Build(), null, null, null, milestones);
    }

    private static PetState CatAt(DateTimeOffset at, double energy, double health)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Cat).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = 80, [PetMeter.Energy] = energy, [PetMeter.Comfort] = 80, [PetMeter.Cleanliness] = 80,
            [PetMeter.Health] = health,
        };
        return PetState.Restore(Catalog, "Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), at, TimeSpan.FromDays(1), meters,
            20, 20, 0, 0, false, draft.Build(), null, null, null, 0);
    }

    private static PetState DogAt(DateTimeOffset at)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Dog).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = 80, [PetMeter.Energy] = 80, [PetMeter.Exercise] = 80, [PetMeter.Cleanliness] = 80,
            [PetMeter.Health] = 100,
        };
        return PetState.Restore(Catalog, "Rex", PetAppearance.Default(Catalog, PetBreed.MixedBreed), at, TimeSpan.FromDays(4),
            meters, 50, 50, 0, 0, false, draft.Build(), null, null, null, 1);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new DateTimeOffset(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

    private static string Describe(IReadOnlyList<CareWindow> windows) =>
        string.Join(",", windows.Select(window =>
            $"{(window.Grace ? "Grace play" : window.Nap ? "Doze" : window.Core.ToString())} {window.Open:hh\\:mm}-{window.Due:hh\\:mm}-{window.LateUntil:hh\\:mm}"));

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
