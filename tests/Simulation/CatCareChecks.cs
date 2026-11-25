using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// Care slice 7: pet profiles, cat first. The cat's routine windows, the needs axis (Restless and Grumpy in its copy),
// overfeeding, the scratched rug, Away mode, the health axis with the vet and medicine, readiness-timed milestones
// (pounce, climb, high jump), the setback, Bond, Trust, the care rhythm and the journal, the v1 to v2 pet migration and
// split advances. See docs/care-and-growth.md and docs/pet-mode.md.
static class CatCareChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly PetCatalog Catalog = PetCatalog.Default;
    private static readonly PetAppearance Shorthair = PetAppearance.Default(Catalog, PetBreed.DomesticShorthair);
    // A breed with no tendencies, so needs fall at the species' rates.
    private static readonly PetAppearance Tabby = PetAppearance.Default(Catalog, PetBreed.Tabby);

    public static void Run()
    {
        Profile();
        RoutineWindows();
        PlayWindow();
        WashAndScratchedRug();
        MoodsAndAnswering();
        Overfeeding();
        Away();
        HealthAndVet();
        MilestoneTiming();
        Setback();
        BondTrustRhythmJournal();
        PreviewDay();
        SplitAdvances();
        Migration();
        HouseholdCat();
        Console.WriteLine("Cat care checks passed.");
    }

    private static void Profile()
    {
        var cat = PetCareProfile.Cat;
        Equal(cat, PetCareProfile.For(Species.Cat), "the cat has a care profile");
        // The hamster has its own profile from P2 (HamsterCareChecks), the dog from P3 (DogCareChecks), the fish from P4
        // (FishCareChecks) and the rabbit from P5 (RabbitCareChecks): every species has one now.
        foreach (Species species in Enum.GetValues(typeof(Species)))
            Equal(true, PetCareProfile.For(species) != null, $"{species} has a care profile");
        Equal(CareTags.Meal, cat.TagsFor(PetVerb.Feed), "feeding is the meal");
        Equal(CareTags.Together | CareTags.Comfort, cat.TagsFor(PetVerb.Pet), "petting is together time that comforts");
        Equal(CareTags.Together | CareTags.Play, cat.TagsFor(PetVerb.Wand), "wand play answers the play window");
        Equal(CareTags.Hygiene, cat.TagsFor(PetVerb.Brush), "brushing is the daily wash");
        Equal(CareTags.Bedtime, cat.TagsFor(PetVerb.Nap), "the nap is bedtime");
        Equal(CareTags.Medicine, cat.Profile.TagsFor("medicine"), "medicine is a care verb of the profile");
        foreach (var verb in Catalog.Get(Species.Cat).Verbs)
            Equal(true, cat.Offers(verb.Verb), $"the profile maps the cat's {verb.Verb}");
        Equal<EngagementKind?>(EngagementKind.PlayWand, cat.KindOf(PetVerb.Wand, false), "play feeds Motor");
        Equal<EngagementKind?>(EngagementKind.Soothe, cat.KindOf(PetVerb.Pet, false), "petting feeds Social");
        Equal<EngagementKind?>(EngagementKind.Hygiene, cat.KindOf(PetVerb.Brush, false), "grooming feeds Social");
        Equal<EngagementKind?>(null, cat.KindOf(PetVerb.Feed, false), "a late meal is no engagement");
        Equal<EngagementKind?>(EngagementKind.Meal, cat.KindOf(PetVerb.Feed, true), "an on-time meal is");
        Equal("Pounce,Climb,HighJump", string.Join(",", cat.Skills), "the cat's Motor milestones");
        Equal(2, cat.ReachedBy(TimeSpan.FromDays(3.5)), "milestones by age");
        // The last milestone's guarantee is the grown age, so every kitten has all three by the time it grows up.
        Equal(Catalog.Get(Species.Cat).GrownAge.Ticks, (long)Math.Ceiling(cat.SkillAges[2].Ticks * GrowthTunables.Default.Guarantee),
            "high jump is guaranteed by the grown day");
    }

    private static void RoutineWindows()
    {
        var school = RoutineCalendar.Windows(CareRoutine.Cat, RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime);
        Equal("Meal 07:00-10:00-12:00,Play 15:00-18:00-19:00,Meal 18:00-20:30-21:30,Bedtime 19:30-21:30-22:30",
            Describe(school), "the cat's school-day windows: two meals, play and the evening nap");
        var home = RoutineCalendar.Windows(CareRoutine.Cat, RoutinePreset.Home, TimeSpan.FromHours(8));
        Equal("Meal 08:00-10:30-12:00,Play 12:00-15:00-16:00,Meal 17:30-19:30-20:30,Bedtime 19:00-20:30-21:30",
            Describe(home), "the Home preset, with the wake time moving only the morning");
        Equal("Meal 07:00-10:00-12:00,Meal 14:00-17:00-18:00,Meal 18:00-20:30-21:30,Bedtime 19:30-21:30-22:30",
            Describe(RoutineCalendar.Windows(RoutinePreset.SchoolDay, RoutineCalendar.DefaultWakeTime)), "the baby keeps three meals");
        Tunables.Validate();

        var sim = Sim();
        var cat = Cat(At(3, 7, 30));
        var fed = sim.Care(cat, PetVerb.Feed).State;
        Equal(CareReason.MealOnTime, fed.Care.LastReason, "breakfast at 07:30 is on time");
        Equal(MomentStatus.OnTime, fed.Care.StatusOf(CareMoment.Morning), "the morning meal is done");
        var strip = sim.Routine(fed);
        Equal("MorningMeal:OnTime,Play:Later,EveningMeal:Later,Bedtime:Later,Wash:Due",
            string.Join(",", strip.Select(slot => slot.Kind + ":" + slot.State)), "the cat's strip: breakfast, play, dinner, nap, brush");

        var late = sim.Care(sim.Step(cat, At(3, 11, 0)).State, PetVerb.Feed).State;
        Equal(CareReason.MealLate, late.Care.LastReason, "breakfast after 10:00 is late");
        Equal("L", late.Care.Meals.ToCode(), "a late meal counts half");

        var missed = sim.Step(cat, At(3, 12, 30)).State;
        Equal(MomentStatus.Missed, missed.Care.StatusOf(CareMoment.Morning), "breakfast is missed at 12:00");
        Equal(CareTags.Meal, missed.Care.Owed, "a missed meal is owed");
        Equal(CareMood.Fussy, missed.Care.Mood, "a missed meal makes the cat restless");
        Equal(CareMoodCause.MissedWindow, missed.Care.MoodCause, "because of the missed window");
        var answered = sim.Care(missed, PetVerb.Feed).State;
        Equal(CareTags.None, answered.Care.Owed, "the next meal answers it");
        Equal(CareMood.Content, answered.Care.Mood, "and the cat settles");

        // The first day is excused, as for a baby.
        var adopted = sim.Adopt("Kit", Tabby, At(3, 8, 0));
        Equal(MomentStatus.Excused, sim.Step(adopted, At(3, 23, 0)).State.Care.StatusOf(CareMoment.Evening),
            "the first day's windows are excused");
    }

    private static void PlayWindow()
    {
        var sim = Sim();
        var cat = Cat(At(3, 15, 30));
        var played = sim.Care(cat, PetVerb.Wand).State;
        Equal(CareReason.PlayOnTime, played.Care.LastReason, "wand play in the play window is on time");
        Equal(MomentStatus.OnTime, played.Care.StatusOf(CareMoment.Afternoon), "the play window is done");
        Equal("O", played.Care.TogetherDays.ToCode(), "the play window keeps the together ring");
        Equal(true, played.Care.TogetherToday, "and counts as together time for the day");

        var petted = sim.Care(cat, PetVerb.Pet).State;
        Equal(MomentStatus.Pending, petted.Care.StatusOf(CareMoment.Afternoon), "petting is together time, not play");
        Equal(true, petted.Care.TogetherToday, "petting counts for the day's together time");
        Equal("", petted.Care.TogetherDays.ToCode(), "only the play window fills the ring");

        var late = sim.Care(sim.Step(cat, At(3, 18, 30)).State, PetVerb.Wand).State;
        Equal(CareReason.PlayLate, late.Care.LastReason, "play after 18:00 is late");
        Equal("L", late.Care.TogetherDays.ToCode(), "late play counts half");

        var missed = sim.Step(petted, At(3, 19, 30)).State;
        Equal(MomentStatus.Missed, missed.Care.StatusOf(CareMoment.Afternoon), "no play by 19:00 misses the window");
        Equal("M", missed.Care.TogetherDays.ToCode(), "a missed play window is a missed together day");
        Equal(CareTags.None, missed.Care.Owed & ~CareTags.Meal, "play is never owed");
        var nextDay = sim.Step(missed, At(4, 7, 30)).State;
        Equal("M", nextDay.Care.TogetherDays.ToCode(), "the day's close adds nothing more for together time");
        Equal(true, CareRules.Score(missed.Care, Tunables) < 1, "a missed play window lowers the CareScore");
    }

    private static void WashAndScratchedRug()
    {
        var sim = Sim();
        var cat = Cat(At(3, 8, 0));
        var brushed = sim.Care(cat, PetVerb.Brush).State;
        Equal(CareReason.WashOnTime, brushed.Care.LastReason, "the daily brush is the wash");
        var unbrushed = sim.Step(sim.Care(cat, PetVerb.Feed).State, At(3, 21, 45)).State;
        Equal(MomentStatus.Missed, unbrushed.Care.Wash, "no brush by the evening misses it");
        Equal(1, unbrushed.Care.Mess, "a missed brush leaves a scratched rug");
        var comfortBefore = unbrushed.Meter(PetMeter.Comfort);
        var petted = sim.Care(unbrushed, PetVerb.Pet).State;
        Near(Math.Min(100, comfortBefore + 20 * (1 - 0.05)), petted.Meter(PetMeter.Comfort), "a scratched rug cuts comfort gains by 5%");
        Equal(0, sim.Care(unbrushed, PetVerb.Brush).State.Care.Mess, "a brush tidies the rug");
    }

    private static void MoodsAndAnswering()
    {
        var sim = Sim();
        var at = At(3, 13, 0);
        var hungry = Cat(at, fullness: 30, bond: 30);
        var restless = sim.Step(hungry, at.AddMinutes(5)).State;
        Equal(CareMood.Fussy, restless.Care.Mood, "a need below 35 makes the cat restless");
        var answered = sim.Care(sim.Step(restless, at.AddMinutes(20)).State, PetVerb.Feed);
        Equal(CareMood.Content, answered.State.Care.Mood, "a meal settles a hungry cat");
        Near(restless.Bond + 5, answered.State.Bond, "answering within half an hour builds Bond +5");

        var grumpy = restless;
        for (var minutes = 30; grumpy.Care.Mood != CareMood.Upset && minutes <= 300; minutes += 20)
            grumpy = sim.Step(grumpy, at.AddMinutes(minutes)).State;
        Equal(CareMood.Upset, grumpy.Care.Mood, "restlessness left unanswered turns grumpy");
        var swatted = sim.Care(grumpy, PetVerb.Wand);
        Equal(PetCue.PushesToyAway, swatted.Cue, "a grumpy cat swats the wand away");
        Equal(grumpy.Meter(PetMeter.Energy), swatted.State.Meter(PetMeter.Energy), "and the play gives nothing");
        Equal(MomentStatus.Pending, swatted.State.Care.StatusOf(CareMoment.Afternoon), "a refused play is not play");
        var fed = sim.Care(sim.Care(grumpy, PetVerb.Feed).State, PetVerb.Feed).State;
        Equal(CareMood.Upset, fed.Care.Mood, "meals alone do not calm a grumpy cat");
        var comforted = sim.Care(sim.Care(fed, PetVerb.Pet).State, PetVerb.Pet).State;
        Equal(true, comforted.Care.Mood < CareMood.Upset, "petting once fed and comfortable calms it");
    }

    private static void Overfeeding()
    {
        var sim = Sim();
        var full = Cat(At(3, 8, 0), fullness: 95, comfort: 70, cleanliness: 80);
        var over = sim.Care(full, PetVerb.Feed);
        Equal(PetCue.TooFull, over.Cue, "feeding a full cat is overfeeding");
        Equal(CareReason.MealTooFull, over.State.Care.LastReason, "and is not a meal");
        Equal(MomentStatus.Pending, over.State.Care.StatusOf(CareMoment.Morning), "breakfast still waits");
        Near(60, over.State.Meter(PetMeter.Comfort), "Comfort -10");
        Near(65, over.State.Meter(PetMeter.Cleanliness), "Cleanliness -15");
        Equal(full.Bond, over.State.Bond, "overfeeding builds no Bond");
        Equal(true, sim.IsFull(full), "IsFull warns before it happens");
        Equal(false, sim.IsFull(Cat(At(3, 8, 0), fullness: 89)), "89 is not full");
    }

    private static void Away()
    {
        var sim = Sim();
        var at = At(3, 8, 0);
        var cat = sim.Care(Cat(at, bond: 40), PetVerb.Feed).State;
        var away = sim.StartAway(cat, TimeSpan.FromDays(3));
        Equal(true, away.Care.IsAway, "Away mode starts");
        var later = sim.Step(away, At(5, 20, 0)).State;
        Equal(MomentStatus.Covered, later.Care.StatusOf(CareMoment.Morning), "windows are covered while away");
        Equal("O", later.Care.Meals.ToCode(), "no meal is missed while away");
        Equal(CareMood.Content, later.Care.Mood, "the cat stays content");
        Equal(true, later.Meter(PetMeter.Fullness) >= 60 - 1e-9, "needs are held at 60");
        Equal(true, later.Bond >= cat.Bond, "Away costs no Bond");
        Equal(SampleMode.Away, later.Development.Sample.Mode, "development keeps the nominal pace");
        var back = sim.Care(later, PetVerb.Pet).State;
        Equal(false, back.Care.IsAway, "any care ends Away mode");
        Equal(false, sim.EndAway(back).Care.IsAway, "and ending it again changes nothing");
    }

    private static void HealthAndVet()
    {
        var sim = Sim();
        var at = At(3, 14, 0);
        var poorly = Cat(at, fullness: 10, health: 35);
        var unwell = sim.Step(poorly, at.AddMinutes(30)).State;
        Equal(CareCondition.UnderTheWeather, unwell.Care.Condition, "Health below 50 puts the cat under the weather");
        Equal(true, PetSimulation.CanVisitVet(unwell), "the vet is offered");
        var sick = unwell;
        for (var minutes = 40; sick.Care.Condition != CareCondition.Sick && minutes <= 90; minutes += 5)
            sick = sim.Step(sick, at.AddMinutes(minutes)).State;
        Equal(CareCondition.Sick, sick.Care.Condition, "while present, Health below 30 makes the cat sick");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.Wand).Cue, "a sick cat is too poorly to play");
        Equal(PetCue.TooPoorly, sim.Care(sick, PetVerb.Brush).Cue, "or to be brushed");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.Pet).Cue, "but likes gentle petting");
        Equal(PetCue.Content, sim.Care(sick, PetVerb.Feed).Cue, "and eats");
        Equal(true, sim.Care(sick, PetVerb.Nap).State.Meter(PetMeter.Energy) <= Math.Max(sick.Meter(PetMeter.Energy), 70),
            "a nap cannot lift Energy past 70 while unwell");

        var visit = sim.VisitVet(sick);
        Equal(PetCue.VetVisited, visit.Cue, "the vet's visit");
        Equal(CareCondition.Recovering, visit.State.Care.Condition, "a sick cat starts recovering");
        Near(sick.Health + 15, visit.State.Health, "the vet restores Health +15 at once");
        Equal(MedicineState.WaitForMeal, PetSimulation.Medicine(visit.State), "medicine goes with a meal");
        Equal(PetCue.MedicineNotNow, sim.GiveMedicine(visit.State).Cue, "not between meals");

        var dinner = sim.Step(visit.State, At(3, 18, 10)).State;
        Equal(MedicineState.Ready, PetSimulation.Medicine(dinner), "dinner time is a medicine window");
        var first = sim.GiveMedicine(dinner);
        Equal(PetCue.MedicineTaken, first.Cue, "the first dose");
        Equal(MedicineState.GivenThisMeal, PetSimulation.Medicine(first.State), "one dose per meal");
        var breakfast = sim.Step(first.State, At(4, 8, 0)).State;
        var second = sim.GiveMedicine(breakfast);
        Equal(PetCue.MedicineDone, second.Cue, "two doses in two meal windows end Recovering");
        Equal(CareCondition.Well, second.State.Care.Condition, "the cat is well again");
        Equal(true, second.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Poorly),
            "the journal keeps a hard day we got through");

        var allClear = sim.VisitVet(Cat(at));
        Equal(PetCue.VetAllClear, allClear.Cue, "a well cat needs no vet");
        Equal(false, PetSimulation.CanVisitVet(Cat(at)), "and none is offered");

        // Absence alone never makes a cat sick, and Health never falls below the floor.
        var left = sim.Step(poorly, At(5, 14, 0)).State;
        Equal(true, left.Care.Condition != CareCondition.Sick, "an absence never passes under the weather");
        Equal(true, left.Health >= CareConsequences.HealthFloor, "Health stays at the floor or above");
    }

    private static void MilestoneTiming()
    {
        var profile = PetCareProfile.Cat;
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

        // Scripted players over nine days of real time.
        var attentive = Play(Attentive, 9);
        var busy = Play(Busy, 9);
        var neglected = Play(Neglect, 9);
        for (var i = 0; i < 3; i++)
        {
            var nominal = profile.SkillAges[i].TotalDays;
            foreach (var (name, ages) in new[] { ("attentive", attentive), ("busy", busy), ("neglected", neglected) })
                Equal(true, ages[i] >= 0.8 * nominal - 0.05 && ages[i] <= 1.4 * nominal + 0.05, $"{name} {profile.Skills[i]} within bounds");
            Equal(true, attentive[i] <= busy[i] && busy[i] <= neglected[i], $"{profile.Skills[i]} follows the play style");
        }

        Equal(true, attentive[0] < neglected[0], "attentive care brings the pounce sooner than neglect");
        Console.WriteLine("  cat milestones (pounce, climb, high jump): steady " + string.Join(", ", rows) +
            $"; scripted attentive {Days(attentive)}, busy {Days(busy)}, neglect {Days(neglected)}");
    }

    private static void Setback()
    {
        var sim = Sim();
        var cat = sim.Adopt("Kit", Tabby, At(2, 8, 0));
        cat = sim.PreviewStep(cat, TimeSpan.FromDays(3)).State;
        Equal(true, cat.HasReached(MotorSkill.Pounce), "the kitten pounces by day three");
        // Petting (not feeding) keeps the player around while three meals are missed.
        foreach (var day in new[] { 3, 4 })
            cat = sim.Care(sim.Step(cat, At(day, 13, 0)).State, PetVerb.Pet).State;
        var before = sim.Step(cat, At(4, 21, 35)).State;
        cat = sim.Step(before, At(4, 21, 55)).State;
        Equal(cat.NewestSkill, cat.ShySkill, "three missed meals while present: the newest skill goes shy");
        Equal(true, cat.HasReached(cat.ShySkill), "a shy skill stays reached");
        Equal(false, cat.Shows(cat.ShySkill), "but does not show");
        Equal(true, cat.Development.Readiness < before.Development.Readiness || cat.Development.Readiness == 0, "readiness drops");

        var shy = cat.ShySkill;
        var morning = sim.Step(cat, At(5, 8, 0)).State;
        var step = sim.Care(morning, PetVerb.Feed);
        step = sim.Care(step.State, PetVerb.Pet);
        step = sim.Care(step.State, PetVerb.Pet);
        Equal(shy, step.State.ShySkill, "still shy before the good day is complete");
        var evening = sim.Step(step.State, At(5, 18, 30)).State;
        var back = sim.Care(evening, PetVerb.Feed);
        Equal(PetCue.SkillBack, back.Cue, "two on-time meals and two pets in a day bring it back");
        Equal(MotorSkill.None, back.State.ShySkill, "the skill shows again");
        Equal(true, back.State.Journal.Entries.Any(entry => entry.Kind == JournalKind.HardDay && entry.Spell == HardSpell.Shy &&
            entry.Skill == shy), "the journal remembers the shy spell");
    }

    private static void BondTrustRhythmJournal()
    {
        var sim = Sim();
        var at = At(3, 8, 0);
        var cat = Cat(at, bond: 20);
        var fed = sim.Care(cat, PetVerb.Feed).State;
        Near(21, fed.Bond, "an on-time meal +1");
        Near(24, sim.Care(fed, PetVerb.Pet).State.Bond, "petting +3");
        var capped = fed;
        for (var i = 0; i < 10; i++)
            capped = sim.Care(capped, PetVerb.Pet).State;
        Near(32, capped.Bond, "gains stop at the daily cap of 12");
        Equal(capped.Bond, capped.HighestBond, "HighestBond follows Bond for the earned-breed goal");

        var missed = sim.Step(Cat(At(3, 11, 0), bond: 50), At(3, 12, 30)).State;
        Equal(true, missed.Bond < 50, "a missed meal costs Bond");
        var floor = sim.Step(Cat(At(3, 8, 0), bond: 50), At(20, 8, 0)).State;
        Equal(true, floor.Bond >= 0.6 * 50 - 1e-9, "Bond never falls below 60% of its high-water mark");
        Equal(50.0, floor.HighestBond, "the high-water mark stays for the earned breeds");

        var attentive = Days(sim, Attentive, 8, out var start);
        var neglected = Days(sim, Neglect, 8, out _);
        var bonding = sim.Bonding(attentive);
        Equal(true, bonding.Rhythm >= 3, "a care day counts with two meals, together time and the nap");
        Equal(true, bonding.LongestRhythm >= 3, "the longest rhythm stays");
        Equal(true, sim.Trust(attentive) != SecurityPhase.Clingy, "attentive care builds trust");
        Equal(true, bonding.Security > sim.Bonding(neglected).Security, "missed meals cost Trust");
        Equal(true, attentive.Bond > neglected.Bond, "attentive care builds more Bond");
        Equal(0, sim.Bonding(neglected).Rhythm, "neglect has no rhythm");
        var journal = sim.Journal(attentive).Entries;
        Equal(JournalKind.Arrival, journal[0].Kind, "the journal starts with the arrival");
        Equal("Pounce,Climb,HighJump", string.Join(",", journal.Where(entry => entry.Kind == JournalKind.Milestone).Select(entry => entry.Skill)),
            "each milestone is in the journal");
        Equal(true, journal.Any(entry => entry.Kind == JournalKind.Sticker && entry.Count == 3), "a 3-day rhythm sticker");
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
        Equal(0, sim.PhotosLeft(photos), "and no more today");
        var shot = sim.Journal(photos).Entries.Last(entry => entry.Kind == JournalKind.Photo);
        Equal(MotorSkill.HighJump, shot.Skill, "a photo keeps the stage shown");
        Equal(PhotoMoment.Playing, shot.Moment, "and the moment");
    }

    private static void PreviewDay()
    {
        var sim = Sim();
        var cat = sim.Adopt("Kit", Tabby, At(3, 8, 0));
        foreach (var verb in new[] { PetVerb.Feed, PetVerb.Wand, PetVerb.Nap })
            cat = sim.Care(cat, verb).State;
        cat = sim.Care(sim.Step(cat, At(3, 14, 0)).State, PetVerb.Feed).State;
        var next = sim.PreviewStep(cat, TimeSpan.FromDays(1)).State;
        Equal(cat.TimeCheckpoint, next.TimeCheckpoint, "a preview day passes no real time");
        Equal(cat.Care, next.Care, "the ledger holds");
        Equal(1, sim.Bonding(next).Rhythm, "two feeds, play and a nap count a preview day");
        Equal(cat.Age + TimeSpan.FromDays(1), next.Age, "the cat is a day older");
        var three = sim.PreviewStep(sim.PreviewStep(next, TimeSpan.FromDays(1)).State, TimeSpan.FromDays(1)).State;
        Equal(1, sim.Bonding(three).Rhythm, "a preview day without care freezes the rhythm");
        Equal(true, three.HasReached(MotorSkill.Pounce), "three preview days bring the pounce");
    }

    private static void SplitAdvances()
    {
        var sim = Sim();
        var cat = sim.Adopt("Kit", Tabby, At(2, 8, 0));
        cat = Run(sim, cat, Attentive, At(3, 0, 0), At(3, 8, 5));
        foreach (var (from, to, steps) in new[] { (At(3, 8, 5), At(3, 14, 0), 12), (At(3, 20, 30), At(4, 3, 30), 14) })
        {
            var start = from == At(3, 8, 5) ? cat : Run(sim, cat, Attentive, At(3, 8, 5), from);
            var whole = sim.Step(start, to).State;
            var split = start;
            for (var i = 1; i <= steps; i++)
                split = sim.Step(split, from + TimeSpan.FromTicks((to - from).Ticks * i / steps)).State;
            var name = $"{from:HH:mm} to {to:HH:mm}";
            foreach (var pair in whole.Meters)
                Near(pair.Value, split.Meter(pair.Key), $"{name}: {pair.Key} is the same split or whole", 1e-9);
            // Only the visit and the instant a rung was reached differ: one long gap starts a new visit, and the ladder
            // moves at advances (the rung itself is the same).
            Equal(Ledger(whole.Care), Ledger(split.Care), $"{name}: the ledger is the same");
            Near(whole.Development.Readiness, split.Development.Readiness, $"{name}: readiness", 1e-9);
            Equal(whole.Milestones, split.Milestones, $"{name}: milestones");
            Near(whole.Bond, split.Bond, $"{name}: Bond", 1e-9);
            Equal(whole.Journal.Entries.Count, split.Journal.Entries.Count, $"{name}: the journal");
        }
    }

    // A v1 cat keeps its stage, Bond, look and meters; the milestones its age passed stay reached; its ledger is seeded
    // at the next advance, which excuses the whole gap.
    private static void Migration()
    {
        const string v1 =
            "<pet version=\"1\"><name>Mochi</name><species>0</species><breed>101</breed>" +
            "<appearance coat=\"5\" accessory=\"1\" accent=\"2\" />" +
            "<checkpoint>639028224000000000</checkpoint><age>3024000000000</age>" +
            "<meters><meter id=\"0\" value=\"56.5\" /><meter id=\"1\" value=\"66.2\" /><meter id=\"2\" value=\"63.2\" />" +
            "<meter id=\"3\" value=\"84\" /><meter id=\"4\" value=\"97\" /></meters>" +
            "<bond value=\"12\" highest=\"15\" /><personality lively=\"12\" bold=\"-3.5\" /><unlocks grown=\"false\" /></pet>";
        var sim = Sim();
        var pet = PetSaveCodec.Decode(v1);
        Equal(2, pet.Milestones, "a 3.5-day kitten has pounced and climbed");
        Equal(12.0, pet.Bond, "Bond is kept");
        Equal(15.0, pet.HighestBond, "its high-water mark is kept");
        Equal(false, pet.IsGrown, "the stage is kept");
        var payload = PetSaveCodec.Encode(pet);
        var built = sim.Care(sim.Step(sim.Adopt("Pip", Tabby, At(2, 8, 0)), At(4, 19, 0)).State, PetVerb.Feed).State;
        Equal(GoldenV2, PetSaveCodec.Encode(built), "the encoder writes the golden v2 payload");
        Equal(GoldenV2Migrated, payload, "the migrated cat encodes as v2 with a ledger waiting for its anchor");
        var migrated = PetSaveCodec.Decode(payload);
        Equal(true, migrated.Care.AwaitingAnchor, "the ledger waits for the next advance");
        Equal(CareRoutine.Cat, migrated.Care.Routine, "the ledger keeps the cat's routine");
        Equal("Arrival,Milestone,Milestone", string.Join(",", sim.Journal(migrated).Entries.Select(entry => entry.Kind)),
            "the journal is seeded with the arrival and the milestones reached");
        Equal(15.0, sim.Bonding(migrated).HighWater, "Bond's record starts from the highest Bond");

        var later = new DateTimeOffset(639028224000000000, TimeSpan.Zero).AddDays(3);
        var first = sim.Step(migrated, later);
        var advanced = first.State;
        Equal(false, advanced.Care.AwaitingAnchor, "the first advance anchors the ledger");
        Equal("", advanced.Care.Meals.ToCode(), "the gap is excused: no meal is missed");
        Equal(CareMood.Content, advanced.Care.Mood, "nobody moves on the ladder");
        Equal(12.0, advanced.Bond, "the gap costs no Bond");
        Equal(true, advanced.Milestones >= 2, "milestones reached by age stay reached");
        Equal(true, advanced.Development != null, "development is seeded");
        var saved = PetSaveCodec.Encode(advanced);
        Equal(saved, PetSaveCodec.Encode(PetSaveCodec.Decode(saved)), "a v2 cat round-trips byte for byte");

        var grown = PetSaveCodec.Decode(v1.Replace("grown=\"false\"", "grown=\"true\"").Replace("<age>3024000000000</age>", "<age>6912000000000</age>"));
        Equal(3, grown.Milestones, "a grown v1 cat has every milestone");
        Equal(GoldenV2, PetSaveCodec.Encode(PetSaveCodec.Decode(GoldenV2)), "the golden v2 payload re-encodes byte for byte");
        var golden = PetSaveCodec.Decode(GoldenV2);
        Equal(1, golden.Milestones, "golden milestones");
        Equal(CareRoutine.Cat, golden.Care.Routine, "golden routine");
        Equal(MomentStatus.OnTime, golden.Care.StatusOf(CareMoment.Evening), "golden dinner on time");
        Equal(MomentStatus.Covered, golden.Care.StatusOf(CareMoment.Morning), "golden breakfast covered by the absence");
        Equal(MotorSkill.Pounce, golden.Journal.Entries.Last().Skill, "golden journal keeps the pounce");

        ExpectInvalid(GoldenV2.Replace(" milestones=\"1\"", " milestones=\"4\""), "more milestones than the cat has");
        ExpectInvalid(GoldenV2.Replace(" milestones=\"1\"", string.Empty), "a v2 cat names its milestones");
        ExpectInvalid(GoldenV2.Substring(0, GoldenV2.IndexOf("<care ", StringComparison.Ordinal)) + "</pet>", "a v2 cat keeps its ledger");
        Expect<NotSupportedException>(() => PetSaveCodec.Decode(GoldenV2.Replace("<care v=\"3\"", "<care v=\"9\"")),
            "a newer care element is unsupported, not corrupt");
        // The dog has its own care records from P3 (DogCareChecks) and the rabbit from P5 (RabbitCareChecks): a rabbit's
        // record holds its own five-moment ledger, never a cat's.
        var rabbit = sim.Adopt("Clover", PetAppearance.Default(Catalog, PetBreed.Dutch), At(3, 8, 0));
        var rabbitSave = PetSaveCodec.Encode(rabbit);
        Equal(true, rabbitSave.Contains("<care "), "a rabbit saves its care records");
        Equal(CareRoutine.Rabbit, PetSaveCodec.Decode(rabbitSave).Care.Routine, "with the rabbit's routine");
    }

    private static void HouseholdCat()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8), TimeSpan.FromDays(7), TimeSpan.FromDays(14));
        var pets = Sim();
        var now = At(3, 8, 0);
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        household.AdoptPet("Kit", Shorthair);
        now = At(4, 8, 0);
        household.ApplyPetCare(2, PetVerb.Feed);
        now = At(4, 13, 0);
        household.AdvanceAll();
        Equal(CareMood.Content, household.Companions[0].PetState.Care.Mood, "a fed cat is content");
        now = At(5, 13, 0);
        household.AdvanceAll();
        var refused = household.ApplyPetCare(2, PetVerb.Brush);
        var vet = household.VisitPetVet(2);
        Equal(true, vet.Cue == PetCue.VetAllClear || vet.Cue == PetCue.VetVisited, "the household offers the vet");
        var photo = household.TakePetPhoto(2, PhotoMoment.Calm);
        Equal(PetCue.Content, photo.Cue, "the household takes a photo");
        Equal(1, household.Companions[0].PetSession.Journal.Entries.Count(entry => entry.Kind == JournalKind.Photo), "into the journal");
        var away = household.StartPetAway(2, TimeSpan.FromDays(2));
        Equal(true, household.Companions[0].PetState.Care.IsAway, "Away mode for a pet");
        household.EndPetAway(2);
        Equal(false, household.Companions[0].PetState.Care.IsAway, "and back");
        Equal(PetCue.MedicineNotNow, household.GivePetMedicine(2).Cue, "medicine only while recovering");

        // A kitten's milestones wait as celebrations in their own manifest element, so a build that cannot read them
        // loses only the announcement, never the household.
        now = At(10, 13, 0);
        household.AdvanceAll(activeOnScreen: false);
        var manifest = System.Xml.Linq.XElement.Parse(store.ManifestPrimary);
        Equal("2", (string)manifest.Attribute("version"), "the manifest stays v2");
        Equal(true, manifest.Element("pet-celebrations").Elements().Count() >= 1, "milestones wait in pet-celebrations");
        Equal(true, manifest.Element("celebrations").Elements().All(element => (int)element.Attribute("milestone") <= 3),
            "ordinary celebrations keep the milestones an older build knows");
        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        var played = reopened.TakeCelebrations(2);
        Equal(true, played.Count > 0 && played.Last() == CompanionMilestone.Grown && played.Contains(CompanionMilestone.Pounce),
            "they survive a reload in order, growing up last");
        _ = refused;
        _ = away;
    }

    // Scripted players: (hour, minute, verbs) visits each day.
    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Attentive =
    {
        (8, 0, new[] { PetVerb.Feed, PetVerb.Pet, PetVerb.Brush }),
        (15, 30, new[] { PetVerb.Wand, PetVerb.Pet }),
        (18, 30, new[] { PetVerb.Feed }),
        (20, 0, new[] { PetVerb.Nap }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Busy =
    {
        (7, 30, new[] { PetVerb.Feed }),
        (19, 45, new[] { PetVerb.Feed, PetVerb.Pet, PetVerb.Nap }),
    };

    private static readonly (int Hour, int Minute, PetVerb[] Verbs)[] Neglect =
    {
        (12, 30, new[] { PetVerb.Feed }),
    };

    private static double[] Play((int Hour, int Minute, PetVerb[] Verbs)[] plan, int days)
    {
        var sim = Sim();
        var adopted = At(2, 7, 45);
        var cat = Run(sim, sim.Adopt("Kit", Tabby, adopted), plan, adopted, adopted.AddDays(days));
        var ages = new double[3];
        foreach (var entry in sim.Journal(cat).Entries.Where(entry => entry.Kind == JournalKind.Milestone))
            ages[PetCareProfile.Cat.IndexOf(entry.Skill)] = (entry.At - adopted).TotalDays;
        return ages;
    }

    private static PetState Days(PetSimulation sim, (int Hour, int Minute, PetVerb[] Verbs)[] plan, int days, out DateTimeOffset adopted)
    {
        adopted = At(2, 7, 45);
        return Run(sim, sim.Adopt("Kit", Tabby, adopted), plan, adopted, adopted.AddDays(days));
    }

    // Follows a plan of visits from `from` until `until`, advancing hourly in between (a player who keeps the room open
    // at the visits only: the other hours pass in one advance).
    private static PetState Run(PetSimulation sim, PetState cat, (int Hour, int Minute, PetVerb[] Verbs)[] plan,
        DateTimeOffset from, DateTimeOffset until)
    {
        for (var day = from.Date; day <= until.Date; day = day.AddDays(1))
        {
            foreach (var (hour, minute, verbs) in plan)
            {
                var at = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, from.Offset);
                if (at <= from || at > until)
                    continue;
                cat = sim.Step(cat, at).State;
                foreach (var verb in verbs)
                    cat = sim.Care(cat, verb).State;
            }
        }

        return sim.Step(cat, until).State;
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

    // A cat on an anchored ledger whose windows all count, at the given needs.
    private static PetState Cat(DateTimeOffset at, double fullness = 80, double comfort = 80, double energy = 80,
        double cleanliness = 80, double health = 100, double bond = 20)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Cat).ToBuilder();
        // Windows already open today count too (those that closed before  stay excused).
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = fullness, [PetMeter.Energy] = energy, [PetMeter.Comfort] = comfort,
            [PetMeter.Cleanliness] = cleanliness, [PetMeter.Health] = health,
        };
        return PetState.Restore(Catalog, "Kit", Tabby, at, TimeSpan.FromDays(1), meters, bond, bond, 0, 0, false,
            draft.Build(), null, null, null, 0);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new DateTimeOffset(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

    private static string Describe(IReadOnlyList<CareWindow> windows) =>
        string.Join(",", windows.Select(window => $"{window.Core} {window.Open:hh\\:mm}-{window.Due:hh\\:mm}-{window.LateUntil:hh\\:mm}"));

    // A cat v2 record: a Tabby adopted two and a half days ago, left alone a day and a half, fed on time this evening;
    // it has pounced. Never edit this literal: it is what players' devices hold.
    private const string GoldenV2 =
        "<pet version=\"2\"><name>Pip</name><species>0</species><breed>101</breed>" +
        "<appearance coat=\"5\" accessory=\"0\" accent=\"0\" /><checkpoint>639082476000000000</checkpoint>" +
        "<age>2124000000000</age><meters><meter id=\"0\" value=\"80.36\" /><meter id=\"1\" value=\"52.64\" />" +
        "<meter id=\"2\" value=\"54\" /><meter id=\"3\" value=\"78\" /><meter id=\"4\" value=\"100\" /></meters>" +
        "<bond value=\"1\" highest=\"1\" /><personality lively=\"10\" bold=\"5\" />" +
        "<unlocks grown=\"false\" milestones=\"1\" />" +
        "<care v=\"3\" checkpoint=\"639082476000000000\" offset=\"0\" next-offset=\"0\" day=\"739678\" preset=\"0\" wake=\"420\" next-preset=\"0\" next-wake=\"420\" moments=\"CCOP\" together-today=\"false\" day-excused=\"false\" awaiting-anchor=\"false\" excused-until=\"639081216000000000\" last-care=\"639082476000000000\" meals=\"O\" together=\"M\" bedtimes=\"\" excused=\"7\" covered=\"4\" reason=\"9\" wash=\"0\" mess=\"0\" mood=\"0\" mood-since=\"0\" mood-cause=\"0\" owed=\"0\" style=\"0\" away-from=\"0\" away-until=\"0\" rest=\"0\" rest-day=\"0\" condition=\"0\" condition-cause=\"0\" condition-since=\"639080352000000000\" unwell-time=\"0\" unwell-sessions=\"0\" session-start=\"639082476000000000\" missed-since-well=\"false\" doses=\"0\" dose-window=\"0\" good-day=\"0\" />" +
        "<development v=\"1\" readiness=\"0.17978826817364463\" mode=\"0\" wellbeing=\"0.8662500000000001\" motor=\"0.5475\" learning=\"0.40518750000000003\" social=\"0.3843175000000001\" physical=\"0.959875\" low=\"0\" toys=\"0\" category-day=\"739678\" categories=\"1\" variety=\"false\" shy=\"0\" setback-since=\"0\" recovery-day=\"739676\" recovery-meals=\"0\" recovery-soothes=\"0\">" +
        "<e k=\"5\" t=\"639082476000000000\" f=\"0.8377000000000001\" /></development>" +
        "<bonding v=\"1\" high=\"1\" cap-day=\"739678\" gained=\"1\" lost=\"0\" helper-day=\"false\" security=\"-1\" curiosity=\"0\" tally-day=\"739678\" meals=\"1\" together=\"false\" rested=\"false\" answered=\"0\" kinds=\"0\" rhythm=\"0\" grace-day=\"0\" care-days=\"0\" longest=\"0\" />" +
        "<journal v=\"1\" spell=\"0\" spell-day=\"0\" weeks=\"0\" week-days=\"0\">" +
        "<j k=\"0\" t=\"639080352000000000\" d=\"1\" /><j k=\"1\" t=\"639082208043565114\" d=\"3\" s=\"4\" /></journal>" +
        "</pet>";

    // What the v1 literal in Migration encodes to.
    private const string GoldenV2Migrated =
        "<pet version=\"2\"><name>Mochi</name><species>0</species><breed>101</breed>" +
        "<appearance coat=\"5\" accessory=\"1\" accent=\"2\" /><checkpoint>639028224000000000</checkpoint>" +
        "<age>3024000000000</age><meters><meter id=\"0\" value=\"56.5\" /><meter id=\"1\" value=\"66.2\" />" +
        "<meter id=\"2\" value=\"63.2\" /><meter id=\"3\" value=\"84\" /><meter id=\"4\" value=\"97\" /></meters>" +
        "<bond value=\"12\" highest=\"15\" /><personality lively=\"12\" bold=\"-3.5\" />" +
        "<unlocks grown=\"false\" milestones=\"2\" />" +
        "<care v=\"3\" checkpoint=\"639028224000000000\" offset=\"0\" next-offset=\"0\" day=\"0\" preset=\"0\" wake=\"420\" next-preset=\"0\" next-wake=\"420\" moments=\"PPPP\" together-today=\"false\" day-excused=\"false\" awaiting-anchor=\"true\" excused-until=\"639028224000000000\" last-care=\"639028224000000000\" meals=\"\" together=\"\" bedtimes=\"\" excused=\"0\" covered=\"0\" reason=\"2\" wash=\"0\" mess=\"0\" mood=\"0\" mood-since=\"0\" mood-cause=\"0\" owed=\"0\" style=\"0\" away-from=\"0\" away-until=\"0\" rest=\"0\" rest-day=\"0\" condition=\"0\" condition-cause=\"0\" condition-since=\"639028224000000000\" unwell-time=\"0\" unwell-sessions=\"0\" session-start=\"639028224000000000\" missed-since-well=\"false\" doses=\"0\" dose-window=\"0\" good-day=\"0\" />" +
        "</pet>";

    private static void ExpectInvalid(string payload, string description) =>
        Expect<InvalidDataException>(() => PetSaveCodec.Decode(payload), description);

    private static void Expect<TException>(Action action, string description) where TException : Exception
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
