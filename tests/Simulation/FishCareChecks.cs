using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// P4 Fish: the fourth species on the care profile mechanism (care slice 7), and the lowest-effort one. One sprinkle of food
// a day any time from morning to evening, glass time after school (follow-finger: together time that comforts), lights
// off at bedtime and a daily wipe of the glass; a morning hello at the glass that closes with grace when school gets in the
// way; Water that drifts down slowly over days and a water change about once a week, so a missed one is a gentle
// consequence; extra flakes that a full fish ignores and that cloud the water slightly; skittish and sulky moods; Away
// mode; the vet and medicine; the milestones Darts to glass, Follows finger and Hoop swim; the setback, Bond, Trust, the
// care rhythm and the journal; swimming to the glass to say hello; helpers (an auto feeder, a bubbler and a filter);
// earned and premium breeds; adoption, saves (a fish saved before P4 migrates) and split advances. See docs/pet-mode.md
// and docs/care-and-growth.md.
static class FishCareChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly PetCatalog Catalog = PetCatalog.Default;
    private static readonly PetAppearance Goldfish = PetAppearance.Default(Catalog, PetBreed.Goldfish);
    // A premium breed with tendencies of its own; the species' rates are checked on the catalog.
    private static readonly PetAppearance Discus = PetAppearance.Default(Catalog, PetBreed.Discus);

    public static void Run()
    {
        Profile();
        RoutineWindows();
        GlassTime();
        OneSprinkleTuning();
        WaterChangeCadence();
        Moods();
        Overfeeding();
        CloudyWater();
        Away();
        HealthAndVet();
        MilestoneTiming();
        Setback();
        BondTrustRhythmJournal();
        SwimsToGlass();
        Helpers();
        PreviewDay();
        SplitAdvances();
        Saves();
        Breeds();
        Household();
        Console.WriteLine("Fish care checks passed.");
    }

    private static void Profile()
    {
        var fish = PetCareProfile.Fish;
        Equal(fish, PetCareProfile.For(Species.Fish), "the fish has a care profile");
        Equal(CareRoutine.Fish, fish.Routine, "and its own routine");
        Equal(CareTags.Meal, fish.TagsFor(PetVerb.SprinkleFood), "a sprinkle of food is the meal");
        Equal(CareTags.Together | CareTags.Play | CareTags.Comfort, fish.TagsFor(PetVerb.FollowFinger),
            "follow-finger is glass time: together, play and a calm moment");
        Equal(CareTags.Hygiene, fish.TagsFor(PetVerb.WipeGlass), "wiping the glass is the daily wash");
        Equal(CareTags.Tidy | CareTags.Comfort, fish.TagsFor(PetVerb.WaterChange), "a water change tidies and settles");
        Equal(CareTags.Bedtime, fish.TagsFor(PetVerb.Rest), "lights off is the bedtime");
        Equal(CareTags.Medicine, fish.Profile.TagsFor("medicine"), "medicine is a care verb of the profile");
        foreach (var verb in Catalog.Get(Species.Fish).Verbs)
            Equal(true, fish.Offers(verb.Verb), $"the profile maps the fish's {verb.Verb}");
        Equal(PetMeter.Calm, fish.ComfortMeter, "Calm plays the comfort role");
        Equal(PetMeter.Water, fish.CleanlinessMeter, "Water plays the cleanliness role");
        Equal(PetMeter.Energy, fish.EnergyMeter, "Energy is the rest meter");
        Equal(false, fish.RestsAlone, "a fish rests when the lights go off");
        Equal(true, fish.IgnoresExtraFood, "a full fish ignores extra flakes");
        Equal(false, PetCareProfile.Dog.IgnoresExtraFood, "a dog does not");
        Equal(1, fish.MealsPerDay, "one meal a day");
        Equal(false, fish.NapsByDay, "no day nap");
        Equal<EngagementKind?>(EngagementKind.PlayWand, fish.KindOf(PetVerb.FollowFinger, false), "follow-finger feeds Motor like play");
        Equal<EngagementKind?>(EngagementKind.Soothe, fish.KindOf(PetVerb.WaterChange, false), "a water change is soothing");
        Equal<EngagementKind?>(EngagementKind.Hygiene, fish.KindOf(PetVerb.WipeGlass, false), "wiping is tidying");
        Equal<EngagementKind?>(EngagementKind.Meal, fish.KindOf(PetVerb.SprinkleFood, true), "an on-time meal is a meal");
        Equal(CareCategories.Play, fish.CategoryOf(PetVerb.FollowFinger), "follow-finger is play for the day's variety");
        Equal(CareCategories.Comfort, fish.CategoryOf(PetVerb.WaterChange), "a water change comforts");
        Equal<PetVerb?>(null, fish.TrickVerb, "a fish learns no tricks");
        Equal("DartsToGlass,FollowsFinger,HoopSwim", string.Join(",", fish.Skills), "the fish's milestones");
        Equal("1.5,3,4.5", string.Join(",", fish.SkillAges.Select(age => age.TotalDays)), "at nominal days 1.5, 3 and 4.5");
        Equal(2, fish.ReachedBy(TimeSpan.FromDays(4)), "milestones by age");
        Equal(TimeSpan.FromDays(5), Catalog.Get(Species.Fish).GrownAge, "a fish grows up on day 5");
        Equal("Fullness,Energy,Calm,Water", string.Join(",", Catalog.Get(Species.Fish).Meters.Select(meter => meter.Meter)),
            "the room shows Fullness, Energy, Calm and Water");
    }

    private static void RoutineWindows()
    {
        var school = RoutineCalendar.Windows(CareRoutine.Fish, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal("Grace play 07:00-09:00-10:00,Play 15:00-20:00-21:00,Meal 07:00-20:00-21:30,Bedtime 19:30-21:30-22:30",
            Describe(school), "the fish's school day: a morning hello, glass time, one sprinkle and lights off");
        var home = RoutineCalendar.Windows(CareRoutine.Fish, RoutinePreset.Home, RoutineCalendar.DefaultWakeTime);
        Equal("Grace play 07:00-09:30-10:30,Play 12:00-17:00-18:00,Meal 07:00-18:00-19:30,Bedtime 19:00-20:30-21:30",
            Describe(home), "the Home preset");
        var late = RoutineCalendar.Windows(CareRoutine.Fish, RoutinePreset.SchoolDay, TimeSpan.FromHours(9));
        Equal("Grace play 09:00-11:00-12:00,Play 15:00-20:00-21:00,Meal 07:00-20:00-21:30,Bedtime 19:30-21:30-22:30",
            Describe(late), "the wake time moves only the morning hello");
        Equal(4, RoutineCalendar.MomentsOf(CareRoutine.Fish), "a fish's day keeps four moments");
        Equal((TimeSpan.FromHours(21.5), TimeSpan.FromHours(7)), RoutineCalendar.Night(CareRoutine.Fish, RoutinePreset.SchoolDay,
            RoutineCalendar.DefaultWakeTime), "the lights are off from 21:30 to the wake time");
        Equal(1, RoutineCalendar.MealWindows(CareRoutine.Fish), "one meal window");
        Equal(1, RoutineCalendar.GoodDayMeals(CareRoutine.Fish), "a good day asks for it");
        Equal(true, RoutineCalendar.HasPlayWindow(CareRoutine.Fish), "glass time keeps the together ring");
        Equal(true, RoutineCalendar.HasMorningHello(CareRoutine.Fish), "a fish says hello in the morning");
        Equal(false, RoutineCalendar.HasMorningHello(CareRoutine.Cat), "a cat has breakfast instead");
        Equal(false, RoutineCalendar.HasMorningHello(CareRoutine.Dog), "a dog too");
        Equal(false, RoutineCalendar.HasWalks(CareRoutine.Fish), "a fish never walks");
        var wash = RoutineCalendar.WashWindow(CareRoutine.Fish, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal((TimeSpan.FromHours(7), TimeSpan.FromHours(20), TimeSpan.FromHours(21.5)), wash,
            "the wipe: any time from morning until the evening");
        Tunables.Validate();

        var sim = Sim();
        var fish = Fish(At(3, 7, 30));
        Equal("Hello:Due,EveningMeal:Due,Play:Later,Bedtime:Later,Wash:Due", Strip(sim, fish),
            "the strip: the morning hello, the day's sprinkle, glass time, lights off and the wipe");
        var fed = sim.Care(fish, PetVerb.SprinkleFood).State;
        Equal(CareReason.MealOnTime, fed.Care.LastReason, "a sprinkle before school is on time");
        var evening = sim.Care(Fish(At(3, 19, 45)), PetVerb.SprinkleFood).State;
        Equal(CareReason.MealOnTime, evening.Care.LastReason, "and so is one after school");
        var lateMeal = sim.Care(Fish(At(3, 20, 30)), PetVerb.SprinkleFood).State;
        Equal(CareReason.MealLate, lateMeal.Care.LastReason, "after 20:00 it is late");
        var missed = sim.Step(Fish(At(3, 15, 0)), At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, missed.Care.StatusOf(CareMoment.Evening), "no sprinkle by 21:30 misses the meal");
        Equal(CareTags.Meal, missed.Care.Owed & CareTags.Meal, "a missed meal is owed");
        Equal(CareMood.Fussy, missed.Care.Mood, "and the fish is skittish");
        var adopted = sim.Adopt("Bubbles", Goldfish, At(3, 16, 0));
        var first = sim.Step(adopted, At(3, 23, 0)).State;
        Equal(MomentStatus.Excused, first.Care.StatusOf(CareMoment.Evening), "the first day's windows are excused");
        Equal(4, first.Care.MomentCode.Length, "a fish's ledger keeps four moments");
    }

    // A morning hello at the glass that closes with grace when school gets in the way, and glass time after school that
    // keeps the together ring.
    private static void GlassTime()
    {
        var sim = Sim();
        var morning = Fish(At(3, 7, 15), bond: 30);
        var hello = sim.Care(morning, PetVerb.FollowFinger).State;
        Equal(CareReason.PlayOnTime, hello.Care.LastReason, "a hello at 7:15 is on time");
        Equal(MomentStatus.OnTime, hello.Care.StatusOf(CareMoment.Morning), "the morning hello is done");
        Equal(MomentStatus.Pending, hello.Care.StatusOf(CareMoment.Afternoon), "glass time still waits");
        Equal("O", hello.Care.TogetherDays.ToCode(), "it keeps the together ring");
        Near(80 + 12, hello.Meter(PetMeter.Calm), "follow-finger gives Calm +12");
        Near(74, hello.Meter(PetMeter.Energy), "and costs Energy 6");

        // School gets in the way: no morning hello, and nothing is missed, owed or lost.
        var noon = sim.Step(morning, At(3, 14, 0)).State;
        Equal(MomentStatus.Excused, noon.Care.StatusOf(CareMoment.Morning), "the morning hello closes with grace at 10:00");
        Equal("", noon.Care.TogetherDays.ToCode(), "it never enters the together ring");
        Equal(CareTags.None, noon.Care.Owed, "and is never owed");
        Equal(CareMood.Content, noon.Care.Mood, "the fish is content");
        Equal(true, noon.Bond >= morning.Bond, "and it costs no Bond");
        var outside = sim.Care(noon, PetVerb.FollowFinger).State;
        Equal(CareReason.PlayOutsideWindow, outside.Care.LastReason, "a finger at 14:00 is between the windows");
        Equal(true, outside.Care.TogetherToday, "but still together time for the day");

        var after = sim.Care(sim.Step(noon, At(3, 16, 0)).State, PetVerb.FollowFinger).State;
        Equal(CareReason.PlayOnTime, after.Care.LastReason, "glass time at 16:00 is on time");
        Equal(MomentStatus.OnTime, after.Care.StatusOf(CareMoment.Afternoon), "glass time is done");
        var noGlass = sim.Step(noon, At(3, 21, 15)).State;
        Equal(MomentStatus.Missed, noGlass.Care.StatusOf(CareMoment.Afternoon), "no glass time by 21:00 misses it");
        Equal("M", noGlass.Care.TogetherDays.ToCode(), "a missed together day");
        Equal(CareTags.None, noGlass.Care.Owed & ~CareTags.Meal, "glass time is never owed");

        // One visit after school keeps the whole day: sprinkle, follow-finger, wipe and lights off at 19:45.
        var day = Fish(At(3, 19, 45));
        foreach (var verb in new[] { PetVerb.SprinkleFood, PetVerb.FollowFinger, PetVerb.WipeGlass, PetVerb.Rest })
            day = sim.Care(day, verb).State;
        Equal(CareReason.BedtimeOnTime, day.Care.LastReason, "lights off at 19:45 is on time");
        var closed = sim.Step(day, At(4, 7, 0)).State;
        Equal(closed.Care.CareDay - 1, closed.Care.GoodDay, "one evening visit is a good day: the hello's grace excuses nothing");
        Equal(1, sim.Bonding(closed).Rhythm, "and it counts for the care rhythm");
    }

    // One sprinkle a day: the needs stay comfortable for an attentive player and for a busy school-day player who visits
    // once, after school, and changes the water at the weekend.
    private static void OneSprinkleTuning()
    {
        var species = Catalog.Get(Species.Fish);
        var rate = species.Meters.ToDictionary(meter => meter.Meter, meter => meter.DecayPerHour);
        var profile = PetCareProfile.Fish;
        // Awake 07:00 to 21:30 (14.5 h) at the awake rates, lights off 21:30 to 07:00 (9.5 h) at the night rates.
        var fullness = 14.5 * rate[PetMeter.Fullness] + 9.5 * profile.NightFullness;
        var calm = 14.5 * rate[PetMeter.Calm] + 9.5 * profile.NightComfort;
        var water = 14.5 * rate[PetMeter.Water] + 9.5 * profile.NightCleanliness;
        var energy = 14.5 * rate[PetMeter.Energy] + 6;
        var sprinkle = species.FindVerb(PetVerb.SprinkleFood).Effects.Single().Delta;
        Near(42.3, fullness, "a day costs 42.3 Fullness");
        Equal(true, fullness < sprinkle, "one sprinkle (+50) covers a day");
        Near(17.35, calm, "a day costs 17.35 Calm");
        Equal(true, calm < 12 + 4 + 8, "follow-finger, the wipe and lights off cover it");
        Near(9.625, water, "Water drifts 9.625 a day");
        Near(27.75, energy, "a day with one follow-finger spends 27.75 Energy before lights off");
        // The fastest-eating breed and the shortest night between two sprinkles still never overfeed.
        var fastest = Catalog.BreedsOf(Species.Fish).Max(breed => breed.DecayFactor(PetMeter.Fullness));
        Equal(true, 14.5 * rate[PetMeter.Fullness] * fastest + 9.5 * profile.NightFullness < sprinkle,
            "one sprinkle covers the hungriest breed's day");
        Equal(true, 100 - 9.5 * profile.NightFullness < Tunables.FullMealThreshold,
            "a sprinkle at 21:30 and another at 07:00 is never overfeeding");
        // The 8-hour decay cap: a player who visits once a day sees at most 8 hours of need fall between sprinkles, and
        // even 8 hours of night leave room for the next one.
        Equal(true, 100 - 8 * profile.NightFullness < Tunables.FullMealThreshold,
            "a once-a-day sprinkle is never overfeeding, even after 8 counted hours of night");

        var sim = Sim();
        var lowest = 100.0;
        var lowestMeal = 100.0;
        var highestMeal = 0.0;
        foreach (var (name, plan) in new[] { ("attentive", Attentive), ("busy", Busy) })
        {
            var fish = sim.Adopt("Bubbles", Goldfish, At(2, 18, 0));
            var personaLowest = 100.0;
            for (var day = 3; day <= 23; day++)
            {
                foreach (var (hour, minute, verbs) in Visits(plan, day))
                {
                    fish = sim.Step(fish, At(day, hour, minute)).State;
                    if (day > 3)
                        personaLowest = Math.Min(personaLowest, Lowest(fish));
                    foreach (var verb in verbs)
                    {
                        if (verb == PetVerb.SprinkleFood && day > 3)
                        {
                            lowestMeal = Math.Min(lowestMeal, fish.Meter(PetMeter.Fullness));
                            highestMeal = Math.Max(highestMeal, fish.Meter(PetMeter.Fullness));
                        }

                        var step = sim.Care(fish, verb);
                        Equal(true, step.Cue != PetCue.IgnoredFlakes && step.Cue != PetCue.TooFull,
                            $"{name} day {day}: a sprinkle is never overfeeding");
                        fish = step.State;
                    }
                }
            }

            Equal(true, personaLowest >= 40, $"{name}: no need falls below 40 (lowest {personaLowest:0.0})");
            Equal(CareMood.Content, fish.Care.Mood, $"{name}: the fish stays content");
            Equal(true, sim.Bonding(fish).Rhythm >= 14, $"{name}: every day counts for the rhythm ({sim.Bonding(fish).Rhythm})");
            lowest = Math.Min(lowest, personaLowest);
            Console.WriteLine($"  fish {name} player: lowest need {personaLowest:0.0}, Bond {fish.Bond:0}");
        }

        Equal(true, lowestMeal >= 45 && highestMeal < Tunables.FullMealThreshold,
            $"sprinkles find it peckish but never full ({lowestMeal:0.0} to {highestMeal:0.0})");

        // Every breed is the same: the busy player keeps each one content.
        foreach (var breed in Catalog.BreedsOf(Species.Fish))
        {
            var fish = Run(sim, sim.Adopt("Bubbles", PetAppearance.Default(Catalog, breed.Breed), At(2, 18, 0)), Busy,
                At(2, 18, 0), At(23, 19, 0));
            Equal(CareMood.Content, fish.Care.Mood, $"the busy player keeps a {breed.Breed} content");
            Equal(true, Lowest(fish) >= 35, $"{breed.Breed} never skittish ({Lowest(fish):0.0})");
        }

        Console.WriteLine($"  fish one-sprinkle tuning: a day costs Fullness {fullness:0.0} (sprinkle +50), Calm {calm:0.00} " +
            $"(follow-finger +12, wipe +4, lights off +8), Water {water:0.000} (wipe +3, water change +70 weekly), Energy {energy:0.00} " +
            $"by lights off; lowest need {lowest:0.0}, sprinkles at Fullness {lowestMeal:0.0} to {highestMeal:0.0}");
    }

    // Water drifts down over days; the daily wipe slows it and a water change about once a week tops it up. Forgetting the
    // water change makes the fish skittish only after about a week, and one water change fixes it.
    private static void WaterChangeCadence()
    {
        var species = Catalog.Get(Species.Fish);
        var profile = PetCareProfile.Fish;
        var wipe = species.FindVerb(PetVerb.WipeGlass).Effects.Single(effect => effect.Meter == PetMeter.Water).Delta;
        var change = species.FindVerb(PetVerb.WaterChange).Effects.Single(effect => effect.Meter == PetMeter.Water).Delta;
        foreach (var breed in Catalog.BreedsOf(Species.Fish))
        {
            var drift = 14.5 * species.Meters.Single(meter => meter.Meter == PetMeter.Water).DecayPerHour *
                breed.DecayFactor(PetMeter.Water) + 9.5 * profile.NightCleanliness - wipe;
            Equal(true, 100 - 7 * drift > 35, $"{breed.Breed}: with daily wipes, a week of water stays clear ({100 - 7 * drift:0.0})");
        }

        Equal(true, change >= 60, "a water change restores Water strongly");

        var sim = Sim();
        // The attentive player who never changes the water: Water drifts, and the fish turns skittish after about a week.
        // (A player who visits once a day sees at most 8 hours of drift a day, the decay cap, so the water lasts longer.)
        var start = At(2, 18, 0);
        var never = sim.Adopt("Bubbles", Goldfish, start);
        var skittishOn = 0.0;
        for (var hours = 1; hours <= 24 * 20 && skittishOn == 0; hours++)
        {
            never = Run(sim, never, NoWaterChange, start.AddHours(hours - 1), start.AddHours(hours));
            if (never.Care.Mood != CareMood.Content)
                skittishOn = hours / 24.0;
        }

        Equal(CareMood.Fussy, never.Care.Mood, "without a water change the fish turns skittish");
        Equal(CareMoodCause.LowNeed, never.Care.MoodCause, "because the water is low");
        Equal(true, never.Meter(PetMeter.Water) < 35, "Water is the low need");
        Equal(true, skittishOn >= 7, $"but only after a week (day {skittishOn})");
        Equal(CareCondition.Well, never.Care.Condition, "and it is never ill from it at once");
        var changed = sim.Care(never, PetVerb.WaterChange);
        Equal(CareMood.Content, changed.State.Care.Mood, "one water change settles it");
        Equal(true, changed.State.Meter(PetMeter.Water) >= 90, "and the water is fresh again");

        // A water change answers no window: the daily wipe still waits.
        var morning = Fish(At(3, 8, 0), water: 50);
        var fresh = sim.Care(morning, PetVerb.WaterChange).State;
        Equal(MomentStatus.Pending, fresh.Care.Wash, "a water change is not the day's wipe");
        Equal(CareReason.NoRoutineTag, fresh.Care.LastReason, "it answers no window");
        Near(100, fresh.Meter(PetMeter.Water), "Water +70 (capped)");
        Console.WriteLine($"  fish water: with daily wipes and no water change, skittish on day {skittishOn:0.0}");
    }

    private static void Moods()
    {
        var sim = Sim();
        var at = At(3, 16, 0);
        var jumpy = Fish(at, calm: 30, bond: 30);
        var skittish = sim.Step(jumpy, at.AddMinutes(5)).State;
        Equal(CareMood.Fussy, skittish.Care.Mood, "Calm below 35 makes the fish skittish");
        Equal(CareMoodCause.LowNeed, skittish.Care.MoodCause, "because a need is low");
        var answered = sim.Care(sim.Step(skittish, at.AddMinutes(20)).State, PetVerb.FollowFinger);
        Equal(CareMood.Content, answered.State.Care.Mood, "follow-finger settles it");
        Near(skittish.Bond + 3 + 5, answered.State.Bond, "follow-finger +3 and answering within half an hour +5");

        var hungry = sim.Step(Fish(at, fullness: 20, calm: 60, bond: 30), at.AddMinutes(5)).State;
        var sulky = hungry;
        for (var minutes = 30; sulky.Care.Mood != CareMood.Upset && minutes <= 300; minutes += 20)
            sulky = sim.Step(sulky, at.AddMinutes(minutes)).State;
        Equal(CareMood.Upset, sulky.Care.Mood, "skittishness left unanswered turns sulky");
        var refused = sim.Care(sulky, PetVerb.FollowFinger);
        Equal(PetCue.PushesToyAway, refused.Cue, "a sulky fish hides behind the plant and will not follow");
        Equal(sulky.Meter(PetMeter.Calm), refused.State.Meter(PetMeter.Calm), "and follow-finger gives nothing");
        var fed = sim.Care(sulky, PetVerb.SprinkleFood).State;
        Equal(CareMood.Upset, fed.Care.Mood, "food alone does not settle a sulky fish");
        var fresh = sim.Care(fed, PetVerb.WaterChange).State;
        Equal(true, fresh.Care.Mood < CareMood.Upset, "fresh water once fed settles it");
    }

    private static void Overfeeding()
    {
        var sim = Sim();
        var full = Fish(At(3, 18, 0), fullness: 95, calm: 70, water: 80);
        Equal(true, sim.IsFull(full), "IsFull warns before it happens");
        Equal(false, sim.IsFull(Fish(At(3, 18, 0), fullness: 89)), "89 is not full");
        var over = sim.Care(full, PetVerb.SprinkleFood);
        Equal(PetCue.IgnoredFlakes, over.Cue, "a full fish ignores the extra flakes");
        Equal(CareReason.MealTooFull, over.State.Care.LastReason, "and they are not its meal");
        Equal(MomentStatus.Pending, over.State.Care.StatusOf(CareMoment.Evening), "the day's sprinkle still waits");
        Near(95, over.State.Meter(PetMeter.Fullness), "ignored flakes add nothing");
        Near(80 - PetSimulation.CloudedWater, over.State.Meter(PetMeter.Water), "and cloud the water slightly (Water -8)");
        Near(70, over.State.Meter(PetMeter.Calm), "Calm is left alone");
        Equal(full.Bond, over.State.Bond, "overfeeding builds no Bond");
        Equal(100.0, over.State.Health, "and never harms Health");
        var gentle = sim.ChangeCareStyle(full, CareStyle.Gentle);
        Near(80 - Tunables.Gentle.OverfeedCleanliness, sim.Care(gentle, PetVerb.SprinkleFood).State.Meter(PetMeter.Water),
            "Gentle care clouds it less");
        var cat = sim.Care(CatAt(At(3, 18, 0)), PetVerb.Feed);
        Equal(PetCue.TooFull, cat.Cue, "the flake rule is the fish's; a full cat is gently overfed as before");
    }

    // A missed wipe leaves the water a little cloudy (the mess); the next wipe or a water change clears it.
    private static void CloudyWater()
    {
        var sim = Sim();
        var fish = Fish(At(3, 8, 0));
        var unwiped = sim.Step(fish, At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, unwiped.Care.Wash, "no wipe by 21:30 misses the day's wipe");
        Equal(1, unwiped.Care.Mess, "and the water turns a little cloudy");
        var wiped = sim.Care(sim.Step(unwiped, At(4, 8, 0)).State, PetVerb.WipeGlass).State;
        Equal(0, wiped.Care.Mess, "a wipe clears it");
        Equal(CareReason.WashOnTime, wiped.Care.LastReason, "and is the day's wipe");
        var changed = sim.Care(sim.Step(unwiped, At(4, 8, 0)).State, PetVerb.WaterChange).State;
        Equal(0, changed.Care.Mess, "so does a water change");
        Equal(MomentStatus.Pending, changed.Care.Wash, "which still leaves the day's wipe to do");
        // Fed every day (so the player is around), never wiped.
        var three = fish;
        for (var day = 3; day <= 6; day++)
        {
            three = sim.Care(sim.Step(three, At(day, 16, 0)).State, PetVerb.SprinkleFood).State;
            three = sim.Step(three, At(day, 22, 0)).State;
        }

        Equal(CareRecord.MaxMess, three.Care.Mess, "cloudiness stops at three missed wipes");
    }

    // Away mode: a neighbour pops in to sprinkle food and the filter hums along. Nothing is missed and the fish swims to
    // the glass when the player is back.
    private static void Away()
    {
        var sim = Sim();
        var fish = sim.Care(Fish(At(3, 16, 0), bond: 40), PetVerb.FollowFinger).State;
        var away = sim.StartAway(fish, TimeSpan.FromDays(3));
        Equal(true, away.Care.IsAway, "Away mode starts");
        var later = sim.Step(away, At(5, 22, 0)).State;
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Evening), "the neighbour's sprinkle is covered");
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Afternoon), "and glass time");
        Equal(CareMood.Content, later.Care.Mood, "the fish stays content");
        Equal(true, later.Meter(PetMeter.Fullness) >= 60 - 1e-9, "needs are held at 60");
        Equal(true, later.Bond >= fish.Bond, "Away costs no Bond");
        Equal(SampleMode.Away, later.Development.Sample.Mode, "development keeps the nominal pace");
        Equal(true, PetReunion.SwimsToGlass(later, Catalog), "and the fish swims to the glass when the player is back");
        var back = sim.Care(later, PetVerb.SprinkleFood).State;
        Equal(false, back.Care.IsAway, "any care ends Away mode");
    }

    private static void HealthAndVet()
    {
        var sim = Sim();
        var at = At(3, 16, 0);
        var poorly = Fish(at, fullness: 10, health: 35);
        var unwell = sim.Step(poorly, at.AddMinutes(30)).State;
        Equal(CareCondition.UnderTheWeather, unwell.Care.Condition, "Health below 50 puts the fish under the weather");
        Equal(true, PetSimulation.CanVisitVet(unwell), "the vet is offered");
        var sick = unwell;
        for (var minutes = 40; sick.Care.Condition != CareCondition.Sick && minutes <= 90; minutes += 5)
            sick = sim.Step(sick, at.AddMinutes(minutes)).State;
        Equal(CareCondition.Sick, sick.Care.Condition, "while present, Health below 30 makes it sick");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.FollowFinger).Cue, "a sick fish is too poorly to follow a finger");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.WipeGlass).Cue, "or to have the glass wiped round it");
        var fresh = sim.Care(sick, PetVerb.WaterChange);
        Equal(PetCue.Content, fresh.Cue, "but fresh water always helps");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.SprinkleFood).Cue, "and it eats");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.Rest).Cue, "and rests");

        var visit = sim.VisitVet(sick);
        Equal(PetCue.VetVisited, visit.Cue, "Dr. Rosa's visit");
        Equal(CareCondition.Recovering, visit.State.Care.Condition, "a sick fish starts recovering");
        Near(sick.Health + 15, visit.State.Health, "the vet restores Health +15 at once");
        Equal(MedicineState.Ready, PetSimulation.Medicine(visit.State), "the day's meal is a medicine window");
        var first = sim.GiveMedicine(visit.State);
        Equal(PetCue.MedicineTaken, first.Cue, "the first dose, with today's food");
        Equal(MedicineState.GivenThisMeal, PetSimulation.Medicine(first.State), "one dose per meal");
        var tomorrow = sim.Step(first.State, At(4, 16, 0)).State;
        Equal(MedicineState.Ready, PetSimulation.Medicine(tomorrow), "the next dose goes with tomorrow's food");
        var second = sim.GiveMedicine(tomorrow);
        Equal(PetCue.MedicineDone, second.Cue, "a dose on two days ends Recovering");
        Equal(CareCondition.Well, second.State.Care.Condition, "the fish is well again");
        Equal(true, second.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Poorly),
            "the journal keeps a hard day we got through");
        Equal(PetCue.VetAllClear, sim.VisitVet(Fish(at)).Cue, "a well fish needs no vet");

        var left = sim.Step(poorly, At(5, 18, 0)).State;
        Equal(true, left.Care.Condition != CareCondition.Sick, "an absence never passes under the weather");
        Equal(true, left.Health >= CareConsequences.HealthFloor, "Health stays at the floor or above");
    }

    private static void MilestoneTiming()
    {
        var profile = PetCareProfile.Fish;
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

        Equal(true, previous[2].TotalDays < 5, "excellent care brings the hoop before the fish grows up");
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

        Equal(true, attentive[2] < neglected[2], "attentive care brings the hoop sooner than neglect");
        Console.WriteLine("  fish milestones (darts to glass, follows finger, hoop swim): steady " + string.Join(", ", rows) +
            $"; scripted attentive {Days(attentive)}, busy {Days(busy)}, neglect {Days(neglected)}");
    }

    private static void Setback()
    {
        var sim = Sim();
        // A young fish that already darts to the glass. Glass time keeps the player around, and food sprinkled late each
        // night (after the window has closed) keeps it fed while three meals in a row are missed: days 3, 4 and 5.
        var fish = Fish(At(3, 15, 0), fullness: 70, milestones: 1);
        Equal(true, fish.HasReached(MotorSkill.DartsToGlass), "the young fish darts to the glass");
        foreach (var day in new[] { 3, 4, 5 })
        {
            fish = sim.Care(sim.Step(fish, At(day, 16, 0)).State, PetVerb.FollowFinger).State;
            if (day < 5)
            {
                fish = sim.Care(sim.Step(fish, At(day, 22, 0)).State, PetVerb.SprinkleFood).State;
                fish = sim.Care(fish, PetVerb.Rest).State;
            }
        }

        var before = sim.Step(fish, At(5, 21, 35)).State;
        Equal("MMM", before.Care.Meals.ToCode(), "three meals missed");
        fish = sim.Step(before, At(5, 21, 55)).State;
        Equal(fish.NewestSkill, fish.ShySkill, "three missed meals while present: the newest skill goes shy");
        Equal(true, fish.ShySkill != MotorSkill.None, "a skill is shy");
        Equal(true, fish.HasReached(fish.ShySkill), "a shy skill stays reached");
        Equal(false, fish.Shows(fish.ShySkill), "but does not show");

        var shy = fish.ShySkill;
        var morning = sim.Step(sim.Care(fish, PetVerb.Rest).State, At(6, 8, 0)).State;
        Equal(CareMood.Upset, morning.Care.Mood, "after three missed meals the fish is sulky");
        var step = sim.Care(morning, PetVerb.SprinkleFood);
        step = sim.Care(step.State, PetVerb.WaterChange);
        Equal(true, step.State.Care.Mood < CareMood.Upset, "a sprinkle, then fresh water, settles it");
        Equal(shy, step.State.ShySkill, "still shy before the good day is complete");
        step = sim.Care(sim.Step(step.State, At(6, 16, 30)).State, PetVerb.FollowFinger);
        Equal(PetCue.SkillBack, step.Cue, "the day's sprinkle on time and two calm moments (fresh water, follow-finger) bring it back");
        Equal(MotorSkill.None, step.State.ShySkill, "the skill shows again");
        Equal(true, step.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Shy &&
            entry.Skill == shy), "the journal remembers the shy spell");
    }

    private static void BondTrustRhythmJournal()
    {
        var sim = Sim();
        var fish = Fish(At(3, 16, 0), bond: 20);
        var fed = sim.Care(fish, PetVerb.SprinkleFood).State;
        Near(21, fed.Bond, "an on-time meal +1");
        Near(24, sim.Care(fed, PetVerb.FollowFinger).State.Bond, "follow-finger +3");
        Near(22, sim.Care(fed, PetVerb.WipeGlass).State.Bond, "a wipe +1");
        Near(22, sim.Care(fed, PetVerb.WaterChange).State.Bond, "a water change +1");
        var capped = fed;
        for (var i = 0; i < 10; i++)
            capped = sim.Care(capped, PetVerb.FollowFinger).State;
        Near(32, capped.Bond, "gains stop at the daily cap of 12");
        Equal(capped.Bond, capped.HighestBond, "HighestBond follows Bond for the earned-breed goal");

        var missed = sim.Step(Fish(At(3, 21, 0), bond: 50), At(3, 21, 45)).State;
        Equal(true, missed.Bond < 50, "a missed sprinkle costs Bond");
        var floor = sim.Step(Fish(At(3, 18, 0), bond: 50), At(20, 18, 0)).State;
        Equal(true, floor.Bond >= 0.6 * 50 - 1e-9, "Bond never falls below 60% of its high-water mark");

        var attentive = Days(sim, Attentive, 12);
        var busy = Days(sim, Busy, 12);
        var neglected = Days(sim, Neglect, 12);
        var bonding = sim.Bonding(attentive);
        Equal(true, bonding.Rhythm >= 8, $"a care day counts with the sprinkle, together time and lights off (rhythm {bonding.Rhythm})");
        Equal(true, sim.Bonding(busy).Rhythm >= 8, $"one evening visit counts too (rhythm {sim.Bonding(busy).Rhythm})");
        Equal(true, sim.Trust(attentive) != SecurityPhase.Clingy, "attentive care builds Trust");
        Equal(true, bonding.Security > sim.Bonding(neglected).Security, "missed meals cost Trust");
        Equal(true, attentive.Bond > neglected.Bond, "attentive care builds more Bond");
        Equal(true, attentive.Bond >= busy.Bond, "and a little more than one visit a day");
        Equal(0, sim.Bonding(neglected).Rhythm, "neglect has no rhythm");
        var journal = sim.Journal(attentive).Entries;
        Equal(JournalKind.Arrival, journal[0].Kind, "the journal starts with the arrival");
        Equal("DartsToGlass,FollowsFinger,HoopSwim",
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
        Equal(MotorSkill.HoopSwim, shot.Skill, "a photo keeps the stage shown");
        Equal(PhotoMoment.Playing, shot.Moment, "and the moment");
    }

    // The signature moment: back after a while, the fish swims straight to the glass to say hello. Display only.
    private static void SwimsToGlass()
    {
        var sim = Sim();
        var fish = sim.Care(Fish(At(3, 16, 0)), PetVerb.FollowFinger).State;
        Equal(false, PetReunion.SwimsToGlass(fish, Catalog), "not right after glass time");
        Equal(false, PetReunion.SwimsToGlass(sim.Step(fish, At(3, 17, 0)).State, Catalog), "or an hour later");
        var night = sim.Step(fish, At(4, 7, 0)).State;
        Equal(true, PetReunion.SwimsToGlass(night, Catalog), "the next morning it swims to the glass");
        Equal(false, PetReunion.SwimsToGlass(sim.Care(night, PetVerb.SprinkleFood).State, Catalog), "any care ends the hello");
        Equal(ReunionScale.None, sim.Reunion(night), "a fish does not give a dog's welcome");
        var dog = sim.Step(sim.Adopt("Biscuit", PetAppearance.Default(Catalog, PetBreed.MixedBreed), At(3, 8, 0)), At(6, 9, 0)).State;
        Equal(false, PetReunion.SwimsToGlass(dog, Catalog), "swimming to the glass is the fish's signature moment");
        var line = PetReunion.GlassLine("Bubbles");
        Equal(true, line.Contains("Bubbles") && line.Contains("glass"), "the hello names the fish");
        foreach (var word in new[] { "sorry", "lonely", "miss", "forgot", "sad", "waited", "finally", "alone", "hungry" })
            Equal(false, line.ToLowerInvariant().Contains(word), $"the hello never says \"{word}\"");
        Equal(night.Bond, sim.Step(fish, At(4, 7, 0)).State.Bond, "the hello is display only");
    }

    // The auto feeder, the bubbler and the filter: the cat's helpers as a fish's objects. None of them is glass time, and
    // none reaches a full water change.
    private static void Helpers()
    {
        var sim = Sim();
        var fish = sim.StockFeeder(Fish(At(3, 15, 0), fullness: 48, calm: 42, water: 42));
        Equal(6, fish.Care.Helpers.Stock, "the auto feeder is stocked");
        var events = new List<HelperEvent>();
        var helped = sim.Step(fish, At(4, 1, 0), HelperSettings.All, events).State;
        Equal(true, events.Any(e => e.Kind == HelperKind.Feeder), "the auto feeder sprinkles the day's food");
        Equal(true, events.Any(e => e.Kind == HelperKind.Play), "the bubbler keeps the fish calm");
        Equal(true, events.Any(e => e.Kind == HelperKind.Tidy), "the filter keeps the water clear");
        Equal(MomentStatus.Helped, helped.Care.StatusOf(CareMoment.Evening), "food from the feeder is Helped");
        Equal(MomentStatus.Missed, helped.Care.StatusOf(CareMoment.Afternoon), "but no helper is glass time");
        Equal(true, helped.Meter(PetMeter.Calm) >= HelperRules.Default.PlayFloor - 1e-9, "Calm is held at the bubbler's floor");
        Equal(true, helped.Meter(PetMeter.Water) >= HelperRules.Default.TidyFloor - 1e-9, "Water is held at the filter's floor");
        Equal(true, helped.Meter(PetMeter.Water) < 100, "and never fresh: a water change stays hands-on");
        var plain = sim.Step(fish, At(4, 1, 0)).State;
        Near(plain.Bond, helped.Bond, "helpers never build Bond");
        Equal(plain.Milestones, helped.Milestones, "or milestones");
        var payload = PetSaveCodec.Encode(helped);
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload, Catalog)), "the fish's helpers round-trip");
    }

    private static void PreviewDay()
    {
        var sim = Sim();
        var fish = sim.Adopt("Bubbles", Goldfish, At(3, 16, 0));
        foreach (var verb in new[] { PetVerb.SprinkleFood, PetVerb.FollowFinger, PetVerb.Rest })
            fish = sim.Care(fish, verb).State;
        var next = sim.PreviewStep(fish, TimeSpan.FromDays(1)).State;
        Equal(fish.TimeCheckpoint, next.TimeCheckpoint, "a preview day passes no real time");
        Equal(fish.Care, next.Care, "the ledger holds");
        Equal(1, sim.Bonding(next).Rhythm, "a sprinkle, glass time and lights off count a preview day");
        Equal(fish.Age + TimeSpan.FromDays(1), next.Age, "a day older");
        var later = next;
        for (var i = 0; i < 2; i++)
            later = sim.PreviewStep(later, TimeSpan.FromDays(1)).State;
        Equal(true, later.HasReached(MotorSkill.DartsToGlass), "three preview days bring darting to the glass");
        var grown = later;
        for (var i = 0; i < 2; i++)
            grown = sim.PreviewStep(grown, TimeSpan.FromDays(1)).State;
        Equal(PetStage.Grown, grown.Stage, "five preview days and the fish is grown");
    }

    private static void SplitAdvances()
    {
        var sim = Sim();
        var fish = sim.Adopt("Bubbles", Discus, At(2, 18, 0));
        fish = Run(sim, fish, Attentive, At(2, 18, 0), At(4, 6, 30));
        // Through the school day, glass time and into the night; each gap stays under the 8-hour cap, which is path
        // dependent on purpose.
        foreach (var (from, to, steps) in new[]
                 {
                     (At(4, 7, 20), At(4, 15, 0), 14), (At(4, 20, 35), At(5, 3, 0), 15), (At(5, 3, 0), At(5, 7, 10), 12),
                 })
        {
            var start = Run(sim, fish, Attentive, At(4, 6, 30), from);
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

        // The same with the morning hello skipped (its grace closes inside the gap).
        var skipped = sim.Step(fish, At(4, 7, 30)).State;
        var wholeSkip = sim.Step(skipped, At(4, 13, 0)).State;
        var splitSkip = skipped;
        for (var i = 1; i <= 11; i++)
            splitSkip = sim.Step(splitSkip, At(4, 7, 30) + TimeSpan.FromTicks((At(4, 13, 0) - At(4, 7, 30)).Ticks * i / 11)).State;
        Equal(Ledger(wholeSkip.Care), Ledger(splitSkip.Care), "the morning hello closes the same split or whole");
        Equal(MomentStatus.Excused, wholeSkip.Care.StatusOf(CareMoment.Morning), "excused either way");

        // Determinism: the same plan twice gives the same record.
        var again = Run(sim, sim.Adopt("Bubbles", Discus, At(2, 18, 0)), Attentive, At(2, 18, 0), At(4, 6, 30));
        Equal(PetSaveCodec.Encode(fish), PetSaveCodec.Encode(again), "the same care gives the same fish");
    }

    // A fish saves as a v2 record from adoption and round-trips byte for byte; a fish record written with the simple rules
    // (v1, or v2 before P4, with no Energy meter) is seeded on first use like any v1 pet.
    private static void Saves()
    {
        var sim = Sim();
        var built = sim.Care(sim.Step(sim.Adopt("Bubbles", Discus, At(2, 18, 0)), At(4, 16, 15)).State, PetVerb.FollowFinger).State;
        var payload = PetSaveCodec.Encode(built);
        Equal(true, payload.StartsWith("<pet version=\"2\"><name>Bubbles</name><species>2</species><breed>307</breed>", StringComparison.Ordinal),
            "a Discus saves as a v2 pet record");
        Equal(true, payload.Contains("<meter id=\"7\"") && payload.Contains("<meter id=\"8\""), "with its Water and Calm");
        Equal(true, payload.Contains("<meter id=\"1\""), "and its Energy");
        Equal(true, System.Text.RegularExpressions.Regex.IsMatch(payload, " moments=\"[A-Z]{4}\""), "and four moments in its ledger");
        Equal(false, payload.Contains("tricks="), "a fish learns no tricks");
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload)), "and round-trips byte for byte");
        var decoded = PetSaveCodec.Decode(payload);
        Equal(CareRoutine.Fish, decoded.Care.Routine, "the ledger follows the fish's routine");
        Equal(MomentStatus.OnTime, decoded.Care.StatusOf(CareMoment.Afternoon), "glass time is kept");
        Equal(built.Milestones, decoded.Milestones, "milestones are kept");
        ExpectInvalid(payload.Replace(" milestones=\"" + built.Milestones + "\"", " milestones=\"4\""), "more milestones than a fish has");
        ExpectInvalid(payload.Replace("<breed>307</breed>", "<breed>204</breed>"), "a dog breed is not a fish");
        ExpectInvalid(payload.Replace(" milestones=\"" + built.Milestones + "\"", " milestones=\"" + built.Milestones + "\" tricks=\"2\""),
            "a fish learns no tricks");

        // A goldfish saved with the simple rules: three meters (no Energy) and no care records.
        const string v1 =
            "<pet version=\"1\"><name>Bubbles</name><species>2</species><breed>300</breed>" +
            "<appearance coat=\"15\" accessory=\"4\" accent=\"0\" />" +
            "<checkpoint>639028224000000000</checkpoint><age>2592000000000</age>" +
            "<meters><meter id=\"0\" value=\"56.5\" /><meter id=\"7\" value=\"84\" /><meter id=\"8\" value=\"63.2\" />" +
            "<meter id=\"4\" value=\"97\" /></meters>" +
            "<bond value=\"12\" highest=\"15\" /><personality lively=\"2\" bold=\"5\" /><unlocks grown=\"false\" /></pet>";
        var old = PetSaveCodec.Decode(v1);
        Equal(2, old.Milestones, "a 3-day fish saved with the simple rules has its first two milestones");
        Near(80, old.Meter(PetMeter.Energy), "its Energy starts at the species' initial value");
        Near(84, old.Meter(PetMeter.Water), "its Water is kept");
        Near(63.2, old.Meter(PetMeter.Calm), "and its Calm");
        Equal(PetAccessory.TankCastle, old.Appearance.Accessory, "and its tank castle");
        Equal(true, PetSaveCodec.Encode(old).Contains("awaiting-anchor=\"true\""), "and a ledger waiting for its anchor");
        var anchored = sim.Step(old, new DateTimeOffset(639028224000000000, TimeSpan.Zero).AddDays(3)).State;
        Equal("", anchored.Care.Meals.ToCode(), "its first advance excuses the gap");
        Equal(12.0, anchored.Bond, "and costs no Bond");
        var v2Simple = PetSaveCodec.Decode(v1.Replace("version=\"1\"", "version=\"2\""));
        Equal(2, v2Simple.Milestones, "a v2 fish saved before P4 (no care element) migrates the same way");
        var migrated = PetSaveCodec.Encode(anchored);
        Equal(migrated, PetSaveCodec.Encode(PetSaveCodec.Decode(migrated)), "and saves as a fish on its care profile");
        ExpectInvalid(v1.Replace("<meter id=\"7\" value=\"84\" />", ""), "a fish still needs its Water");

        // Cats, hamsters and dogs are unchanged: their records still decode with their own routines.
        var cat = sim.Adopt("Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), At(3, 8, 0));
        Equal(CareRoutine.Cat, PetSaveCodec.Decode(PetSaveCodec.Encode(cat)).Care.Routine, "a cat keeps the cat's routine");
        var dog = sim.Adopt("Rex", PetAppearance.Default(Catalog, PetBreed.MixedBreed), At(3, 8, 0));
        Equal(5, PetSaveCodec.Decode(PetSaveCodec.Encode(dog)).Care.MomentCode.Length, "a dog keeps its five moments");
    }

    private static void Breeds()
    {
        var breeds = Catalog.BreedsOf(Species.Fish);
        Equal("Goldfish:Free,Guppy:Earned,Betta:Earned,NeonTetraSchool:Premium,Angelfish:Premium,Koi:Premium,Clownfish:Premium,Discus:Premium",
            string.Join(",", breeds.Select(breed => breed.Breed + ":" + breed.Unlock)), "the fish's breed ladder");
        Equal("", string.Join(",", breeds.Where(breed => !breed.AvailableNow).Select(breed => breed.Breed)),
            "every fish breed ships with P4, the Neon tetra school too");
        Equal("300,301,302,303,304,305,306,307", string.Join(",", breeds.Select(breed => (int)breed.Breed)), "in the fish block");
        Equal(EarnedGoal.FirstFullCareDay, Catalog.Get(PetBreed.Guppy).Goal, "Guppy: a first full care day");
        Equal(EarnedGoal.Bond40, Catalog.Get(PetBreed.Betta).Goal, "Betta: Bond 40");
        Equal("breed.fish.neon_tetra_school,breed.fish.angelfish,breed.fish.koi,breed.fish.clownfish,breed.fish.discus",
            string.Join(",", breeds.Where(breed => breed.Unlock == BreedUnlock.Premium).Select(breed => breed.ProductId)),
            "premium product IDs");
        Equal(true, Catalog.GrantsBreed("breedpack.fish", PetBreed.Koi), "the fish breed pack");
        Equal(false, Catalog.GrantsBreed("breedpack.dog", PetBreed.Koi), "not the dog's");
        Equal(true, Catalog.GrantsBreed(PetIds.AllBreedsPackProductId, PetBreed.Discus), "and the every-breed pack");
        foreach (var product in breeds.Where(breed => breed.ProductId != null).Select(breed => breed.ProductId)
                     .Concat(new[] { "breedpack.fish" }))
            Equal(true, DeveloperUnlocks.Products(Catalog).Contains(product), $"developer mode grants {product}");
        var species = Catalog.Get(Species.Fish);
        foreach (var breed in breeds)
            Near(0, breed.NetDecayChangePerHour(species), $"{breed.Breed} is no harder or easier", 1e-9);
        Catalog.Validate();

        var none = SpeciesGoalProgress.None;
        Equal("Goldfish", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Fish, none, new string[0])),
            "a new household adopts a goldfish");
        var earned = new SpeciesGoalProgress(1, 40, true, 0);
        Equal("Goldfish,Guppy,Betta", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Fish, earned, new string[0])),
            "care earns the Guppy and the Betta");
        Equal("Goldfish,Koi", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Fish, none, new[] { "breed.fish.koi" })),
            "buying the Koi opens the Koi");
        Equal("Goldfish,NeonTetraSchool,Angelfish,Koi,Clownfish,Discus",
            string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Fish, none, new[] { "breedpack.fish" })),
            "the pack opens every premium fish");
        Equal((1, true), PetCareDays.RuleFor(Species.Fish), "a fish's care day: one sprinkle, together time and lights off");
        Equal(true, species.Offers(PetAccessory.TankCastle), "a fish can have a tank castle");
    }

    private static void Household()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8), TimeSpan.FromDays(7), TimeSpan.FromDays(14));
        var pets = Sim();
        var now = At(3, 7, 0);
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        Equal(true, household.CanAdoptBreed(PetBreed.Goldfish), "a goldfish is adoptable at once");
        Equal(false, household.CanAdoptBreed(PetBreed.Guppy), "the Guppy is earned");
        Equal(false, household.CanAdoptBreed(PetBreed.NeonTetraSchool), "the Neon tetra school is premium");
        household.AdoptPet("Bubbles", Goldfish);
        now = At(4, 19, 30);
        household.AdvanceAll();
        var food = household.ApplyPetCare(2, PetVerb.SprinkleFood);
        Equal(PetCue.Content, food.Cue, "the day's sprinkle in the household");
        Equal(false, food.CompletedCareDay, "not yet a full day");
        household.ApplyPetCare(2, PetVerb.FollowFinger);
        household.ApplyPetCare(2, PetVerb.WipeGlass);
        var lights = household.ApplyPetCare(2, PetVerb.Rest);
        Equal(true, lights.CompletedCareDay, "a sprinkle, follow-finger and lights off complete a fish's care day");
        Equal("Guppy", string.Join(",", lights.UnlockedBreeds), "and earn the Guppy");
        Equal(true, household.CanAdoptBreed(PetBreed.Guppy), "which can now be adopted");
        Equal(PetCue.VetAllClear, household.VisitPetVet(2).Cue, "the vet is there for a fish too");

        // A young fish's milestones wait as celebrations in their own manifest element.
        now = At(10, 18, 0);
        household.AdvanceAll(activeOnScreen: false);
        var manifest = System.Xml.Linq.XElement.Parse(store.ManifestPrimary);
        Equal(true, manifest.Element("fish-celebrations").Elements().Count() >= 1, "milestones wait in fish-celebrations");
        Equal(null, manifest.Element("pet-celebrations"), "never where an older build reads a cat's");
        Equal(null, manifest.Element("dog-celebrations"), "or a dog's");
        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        var played = reopened.TakeCelebrations(2).ToList();
        Equal(true, played.Contains(CompanionMilestone.DartsToGlass) && played.Contains(CompanionMilestone.Grown),
            "they survive a reload: darting to the glass and growing up");
        Equal(true, played.IndexOf(CompanionMilestone.DartsToGlass) < played.IndexOf(CompanionMilestone.Grown),
            "milestones before growing up");
    }

    // Scripted players: (hour, minute, verbs) visits each day, and a water change every seventh day. An attentive player
    // says hello before school, comes back after school and turns the lights off at bedtime; a busy school-day player visits
    // once, in the evening; neglect is a late-night sprinkle and nothing else.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Attentive =
    {
        (7, 15, new[] { PetVerb.FollowFinger }),
        (16, 0, new[] { PetVerb.SprinkleFood, PetVerb.FollowFinger, PetVerb.WipeGlass }),
        (20, 30, new[] { PetVerb.Rest }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Busy =
    {
        (19, 30, new[] { PetVerb.SprinkleFood, PetVerb.FollowFinger, PetVerb.WipeGlass, PetVerb.Rest }),
    };

    // Never a water change: the attentive player who forgets it.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] NoWaterChange =
    {
        (7, 15, new[] { PetVerb.FollowFinger }),
        (16, 0, new[] { PetVerb.SprinkleFood, PetVerb.FollowFinger, PetVerb.WipeGlass }),
        (20, 30, new[] { PetVerb.Rest }),
    };

    // A sprinkle any time of the day is on time for a fish, so neglect is food only late at night, after the window.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Neglect =
    {
        (22, 0, new[] { PetVerb.SprinkleFood }),
    };

    // A plan's visits on a day: every seventh day (a weekend) the first visit of a plan that has an evening visit also
    // changes the water. Plans other than Attentive and Busy never change it.
    private static IEnumerable<(int Hour, int Minute, PetVerb[] Verbs)> Visits((int Hour, int Minute, PetVerb[] Verbs)[] plan, int day)
    {
        var weekly = (ReferenceEquals(plan, Attentive) || ReferenceEquals(plan, Busy)) && day % 7 == 0;
        foreach (var (hour, minute, verbs) in plan)
        {
            var change = weekly && verbs.Contains(PetVerb.SprinkleFood);
            yield return (hour, minute, change ? verbs.Concat(new[] { PetVerb.WaterChange }).ToArray() : verbs);
        }
    }

    private static double Lowest(PetState state) =>
        new[] { PetMeter.Fullness, PetMeter.Calm, PetMeter.Energy, PetMeter.Water }.Min(state.Meter);

    private static double[] Play((int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var sim = Sim();
        var adopted = At(2, 7, 0);
        var fish = Run(sim, sim.Adopt("Bubbles", Goldfish, adopted), plan, adopted, adopted.AddDays(days));
        var ages = new double[3];
        foreach (var entry in sim.Journal(fish).Entries.Where(entry => entry.Kind == JournalKind.Milestone))
            ages[PetCareProfile.Fish.IndexOf(entry.Skill)] = (entry.At - adopted).TotalDays;
        return ages;
    }

    private static PetState Days(PetSimulation sim, (int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var adopted = At(2, 7, 0);
        return Run(sim, sim.Adopt("Bubbles", Goldfish, adopted), plan, adopted, adopted.AddDays(days));
    }

    private static PetState Run(PetSimulation sim, PetState fish, (int Hour, int Minute, PetVerb[] Verbs)[] plan,
        DateTimeOffset from, DateTimeOffset until)
    {
        for (var day = from.Date; day <= until.Date; day = day.AddDays(1))
        {
            foreach (var (hour, minute, verbs) in Visits(plan, day.Day))
            {
                var at = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, from.Offset);
                if (at <= from || at > until)
                    continue;
                fish = sim.Step(fish, at).State;
                foreach (var verb in verbs)
                    fish = sim.Care(fish, verb).State;
            }
        }

        return sim.Step(fish, until).State;
    }

    private static string Strip(PetSimulation sim, PetState fish) =>
        string.Join(",", sim.Routine(fish).Select(slot => slot.Kind + ":" + slot.State));

    private static string Ledger(CareRecord care)
    {
        var element = CareRecordCodec.Encode(care);
        element.Attribute("session-start").Remove();
        element.Attribute("mood-since").Remove();
        return element.ToString();
    }

    private static string Days(double[] ages) => $"{ages[0]:0.0} / {ages[1]:0.0} / {ages[2]:0.0}";

    private static PetSimulation Sim() => new PetSimulation();

    // A fish on an anchored ledger whose windows all count, at the given needs.
    private static PetState Fish(DateTimeOffset at, double fullness = 80, double calm = 80, double energy = 80,
        double water = 80, double health = 100, double bond = 20, int milestones = 0)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Fish).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = fullness, [PetMeter.Energy] = energy, [PetMeter.Calm] = calm,
            [PetMeter.Water] = water, [PetMeter.Health] = health,
        };
        return PetState.Restore(Catalog, "Bubbles", Goldfish, at, TimeSpan.FromDays(1 + milestones), meters, bond, bond, 0, 0,
            false, draft.Build(), null, null, null, milestones);
    }

    // A full cat on an anchored ledger, to show the flake rule is the fish's.
    private static PetState CatAt(DateTimeOffset at)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Cat).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = 95, [PetMeter.Energy] = 80, [PetMeter.Comfort] = 80, [PetMeter.Cleanliness] = 80,
            [PetMeter.Health] = 100,
        };
        return PetState.Restore(Catalog, "Kit", PetAppearance.Default(Catalog, PetBreed.Tabby), at, TimeSpan.FromDays(1), meters,
            20, 20, 0, 0, false, draft.Build(), null, null, null, 0);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new DateTimeOffset(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

    private static string Describe(IReadOnlyList<CareWindow> windows) =>
        string.Join(",", windows.Select(window =>
            $"{(window.Grace ? "Grace play" : window.Core.ToString())} {window.Open:hh\\:mm}-{window.Due:hh\\:mm}-{window.LateUntil:hh\\:mm}"));

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
