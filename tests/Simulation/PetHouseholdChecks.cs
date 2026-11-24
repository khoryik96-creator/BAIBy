using System;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// PetSession and a household of babies and pets together (docs/pet-mode.md, P1 Cat).
static class PetHouseholdChecks
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly PetAppearance Ginger = new PetAppearance(PetBreed.DomesticShorthair, PetCoat.Ginger, null,
        PetAccessory.Collar, Outfit.Sky);

    public static void Run()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7), TimeSpan.FromDays(12));
        var pets = new PetSimulation();
        CheckPetSession(pets);
        CheckCareDays();
        CheckMixedAdoptionAndCapacity(game, pets);
        CheckBreedUnlocks(game, pets);
        CheckTimeAndGrowth(game, pets);
        CheckPetGoodbye(game, pets);
        CheckManifestKinds(game, pets);
        CheckUnreadablePets(game, pets);
        CheckRestingCat(game, pets);
        Console.WriteLine("Pet household checks passed.");
    }

    private static void CheckPetSession(PetSimulation pets)
    {
        var now = Start;
        var saves = new MemorySaveStore();
        var session = new PetSession(pets, saves, () => now);
        Equal<PetState>(null, session.State, "a new pet session has no pet");
        Equal(false, session.Refresh(), "refresh before adoption does nothing");
        Equal(0, saves.Writes, "an empty session writes nothing");
        session.Adopt("Pip & Co", Ginger);
        Equal(1, saves.Writes, "adoption writes the record once");
        Equal(true, saves.Primary.StartsWith("<pet version=\"2\"", StringComparison.Ordinal), "a pet record is <pet version=\"2\">");
        ExpectThrows<InvalidOperationException>(() => session.Adopt("Again", Ginger), "a session adopts once");

        Equal(true, session.CanUse(PetVerb.Wand), "a cat can play with the wand");
        Equal(false, session.CanUse(PetVerb.Walk), "a cat is not walked");
        ExpectThrows<InvalidOperationException>(() => session.ApplyCare(PetVerb.Walk), "a verb the species lacks is refused");
        Equal(1, saves.Writes, "a refused verb writes nothing");

        now = now.AddHours(6);
        var fed = session.ApplyCare(PetVerb.Feed);
        Equal(PetCue.Content, fed.Cue, "feeding is a content cue");
        Equal(84.0, session.State.Meter(PetMeter.Fullness), "six hours' decay and a meal commit together");
        Equal(2, saves.Writes, "care writes once");

        var reopened = new PetSession(pets, saves, () => now);
        Equal("Pip & Co", reopened.State.Name, "the name survives the record");
        Equal(Ginger, reopened.State.Appearance, "the look survives the record");
        Equal(84.0, reopened.State.Meter(PetMeter.Fullness), "meters survive the record");
        Equal(false, reopened.Refresh(), "a same-time refresh reports nothing");
        Equal(2, saves.Writes, "a same-time refresh writes nothing");

        var beforeCorruption = saves.Primary;
        reopened.ApplyCare(PetVerb.Pet);
        saves.Primary = "<pet";
        var recovered = new PetSession(pets, saves, () => now);
        Equal(beforeCorruption, PetSaveCodecOf(recovered.State), "a corrupt record falls back to its backup");

        var corruptOnly = new MemorySaveStore { Primary = "<pet" };
        ExpectThrows<InvalidDataException>(() => new PetSession(pets, corruptOnly, () => now),
            "a corrupt record without a backup is reported");

        var newer = new MemorySaveStore { Primary = "<pet version=\"9\" />" };
        ExpectThrows<NotSupportedException>(() => new PetSession(pets, newer, () => now), "a newer record is not opened");
        Equal("<pet version=\"9\" />", newer.Primary, "a newer record keeps its bytes");

        var backupOnly = new MemorySaveStore();
        backupOnly.Write(beforeCorruption, null);
        backupOnly.Write("<pet", beforeCorruption);
        backupOnly.Primary = null;
        Equal("Pip & Co", new PetSession(pets, backupOnly, () => now).State.Name, "a record with only a backup restores");

        var growing = new PetSession(pets, new MemorySaveStore(), () => Start);
        growing.Adopt("Kit", Ginger);
        Equal(false, growing.AdvancePreview(TimeSpan.FromDays(6)), "a kitten is not grown at six days");
        Equal(true, growing.AdvancePreview(TimeSpan.FromDays(1)), "a cat is grown at seven days");
        Equal(PetStage.Grown, growing.State.Stage, "the grown stage is saved");
        Equal(false, growing.AdvancePreview(TimeSpan.FromDays(1)), "growing up is reported once");
    }

    private static void CheckCareDays()
    {
        var cat = PetCatalog.Default.Get(Species.Cat);
        Equal(PetCareKind.Meal, PetCareDays.KindOf(cat, PetVerb.Feed), "feeding is a meal");
        Equal(PetCareKind.Together, PetCareDays.KindOf(cat, PetVerb.Pet), "petting is together time");
        Equal(PetCareKind.Together, PetCareDays.KindOf(cat, PetVerb.Wand), "wand play is together time");
        Equal(PetCareKind.Bedtime, PetCareDays.KindOf(cat, PetVerb.Nap), "a nap is bedtime");
        Equal(PetCareKind.None, PetCareDays.KindOf(cat, PetVerb.Walk), "a verb the species lacks counts for nothing");

        var day = PetCareDays.DayNumber(Start);
        var days = PetCareDays.None;
        days = days.Record(PetCareKind.Meal, day).Days;
        days = days.Record(PetCareKind.Together, day).Days;
        var bedtime = days.Record(PetCareKind.Bedtime, day);
        Equal(false, bedtime.Completed, "one meal is not a full day");
        var second = bedtime.Days.Record(PetCareKind.Meal, day);
        Equal(true, second.Completed, "two meals, together time and a bedtime make a full day");
        Equal(1, second.Days.Rhythm, "a first full day starts the rhythm");
        Equal(false, second.Days.Record(PetCareKind.Meal, day).Completed, "a day counts once");

        days = second.Days;
        for (var next = day + 1; next <= day + 4; next++)
            days = FullDay(days, next);
        Equal(5, days.Rhythm, "consecutive full days build the rhythm");
        days = FullDay(days, day + 7);
        Equal(2, days.Rhythm, "two missed days halve the rhythm twice, then the day counts");
        Equal(5, days.BestRhythm, "the best rhythm is kept");
        Equal(6, days.FullDays, "every full day is counted");
        Equal(days, days.Record(PetCareKind.None, day + 7).Days, "a verb that counts for nothing changes nothing");
        Equal(day + 7, days.Record(PetCareKind.Meal, day).Days.Day, "a clock set back counts on the tracked day");
    }

    private static PetCareDays FullDay(PetCareDays days, int day)
    {
        foreach (var kind in new[] { PetCareKind.Meal, PetCareKind.Meal, PetCareKind.Together, PetCareKind.Bedtime })
            days = days.Record(kind, day).Days;
        return days;
    }

    private static void CheckMixedAdoptionAndCapacity(BabySimulation game, PetSimulation pets)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        Equal(true, household.CanAdoptPet, "an empty household can adopt a pet");
        ExpectThrows<ArgumentException>(() => household.AdoptPet(" ", Ginger), "a pet needs a name");

        var pip = household.AdoptPet("Pip", Ginger);
        Equal(2, pip.Slot, "a pet never takes slot 1");
        Equal<string>(null, store.Slot1.Primary, "slot 1 stays free for a baby");
        Equal(true, pip.IsPet, "the companion is a pet");
        Equal(CompanionKind.Pet, pip.Kind, "the companion kind is Pet");
        Equal<BabyState>(null, pip.State, "a pet has no baby state");
        Equal("Pip", pip.Name, "the companion is named");
        Equal(2, household.Active.Slot, "a new pet becomes active");
        Equal(true, store.ManifestPrimary.StartsWith("<household version=\"2\"", StringComparison.Ordinal),
            "a household with a pet writes manifest v2");
        Equal(true, store.ManifestPrimary.Contains("<companion slot=\"2\" adopted=\"" + Start.UtcDateTime.Ticks + "\" kind=\"1\" />"),
            "the manifest records the pet's kind");

        now = now.AddMinutes(3);
        var mia = household.Adopt("Mia");
        Equal(1, mia.Slot, "a baby still takes slot 1");
        Equal(true, household.IsFull, "a baby and a cat fill the two free rooms");
        Equal(false, household.CanAdoptPet, "a full household cannot adopt a pet");
        ExpectThrows<InvalidOperationException>(() => household.AdoptPet("Tig", Ginger), "capacity 2 counts babies and pets");
        Equal<string>(null, store.SlotStore(3).Primary, "a refused pet writes no record");

        household.Switch(2);
        Equal(2, household.Active.Slot, "a switch opens the cat");
        var fed = household.ApplyPetCare(2, PetVerb.Feed);
        Equal(PetCue.Content, fed.Cue, "the cat eats");
        ExpectThrows<InvalidOperationException>(() => household.ApplyPetCare(1, PetVerb.Feed), "a baby is not cared for as a pet");

        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        Equal("Pip,Mia", string.Join(",", reopened.Companions.Select(companion => companion.Name)), "order persists for mixed kinds");
        Equal(CompanionKind.Pet, reopened.Companions[0].Kind, "the cat reopens as a pet");
        Equal(CompanionKind.Baby, reopened.Companions[1].Kind, "the baby reopens as a baby");
        Equal(2, reopened.Active.Slot, "the active cat persists");
        Equal(Start, reopened.Companions[0].AdoptedAt, "the cat keeps its adoption date");
        Equal(Ginger, reopened.Companions[0].PetState.Appearance, "the cat keeps its look");
    }

    private static void CheckBreedUnlocks(BabySimulation game, PetSimulation pets)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        Equal(true, household.CanAdoptBreed(PetBreed.DomesticShorthair), "the free starter breed is always adoptable");
        Equal(false, household.CanAdoptBreed(PetBreed.Tabby), "an earned breed waits for its goal");
        Equal(false, household.CanAdoptBreed(PetBreed.Bengal), "a premium breed needs its product");
        ExpectThrows<InvalidOperationException>(() => household.AdoptPet("Tig", new PetAppearance(PetBreed.Tabby, PetCoat.Brown)),
            "a locked earned breed cannot be adopted");
        ExpectThrows<InvalidOperationException>(() => household.AdoptPet("Bee", new PetAppearance(PetBreed.Bengal, PetCoat.Golden)),
            "a premium breed cannot be adopted unowned");
        ExpectThrows<ArgumentException>(() => household.AdoptPet("Pip", new PetAppearance(PetBreed.DomesticShorthair, PetCoat.Seal)),
            "a coat the breed does not offer is refused");
        Equal(0, store.RecordWrites, "refused adoptions write nothing");

        var owner = new HouseholdSession(game, pets, new MemoryHouseholdStore(), () => now,
            new FakeEntitlements("breed.cat.bengal", "breed.cat.ragdoll"));
        Equal(true, owner.CanAdoptBreed(PetBreed.Bengal), "an owned premium recolor breed is adoptable");
        Equal(true, owner.IsBreedUnlocked(PetBreed.Ragdoll), "an owned overlay breed is unlocked");
        Equal(false, owner.CanAdoptBreed(PetBreed.Ragdoll), "an owned breed waits for its art");
        Equal(PetBreed.Bengal, owner.AdoptPet("Bee", new PetAppearance(PetBreed.Bengal, PetCoat.Golden)).PetState.Breed,
            "an owned premium breed is adopted");

        household.AdoptPet("Pip", Ginger);
        var first = household.ApplyPetCare(2, PetVerb.Feed);
        Equal(0, first.UnlockedBreeds.Count, "one meal unlocks nothing");
        household.ApplyPetCare(2, PetVerb.Pet);
        household.ApplyPetCare(2, PetVerb.Nap);
        var full = household.ApplyPetCare(2, PetVerb.Feed);
        Equal(true, full.CompletedCareDay, "a full care day completes on the second meal");
        Equal("Tabby", string.Join(",", full.UnlockedBreeds), "the first full care day unlocks the Tabby");
        Equal(0, household.ApplyPetCare(2, PetVerb.Feed).UnlockedBreeds.Count, "an unlock is announced once");
        Equal(true, household.CanAdoptBreed(PetBreed.Tabby), "the Tabby is adoptable");
        Equal(false, household.CanAdoptBreed(PetBreed.Tuxedo), "the Tuxedo still waits for Bond 40");

        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        Equal(true, reopened.CanAdoptBreed(PetBreed.Tabby), "an earned breed stays unlocked after reload");
        Equal(1, reopened.ProgressFor(Species.Cat).FullCareDays, "the care day is saved in the manifest");

        // A cat's Bond follows the baby's rules (slice 7): gains are capped each day, so Bond 40 takes a few days of
        // meals, a nap and petting.
        PetCareOutcome last = default;
        var announced = false;
        for (var day = 1; day <= 10 && !announced; day++)
        {
            foreach (var (hour, verbs) in new[]
            {
                (0, new[] { PetVerb.Feed, PetVerb.Nap, PetVerb.Pet, PetVerb.Pet }),
                (10, new[] { PetVerb.Feed, PetVerb.Pet, PetVerb.Pet }),
            })
            {
                now = Start.AddDays(day).AddHours(hour);
                foreach (var verb in verbs)
                {
                    last = reopened.ApplyPetCare(2, verb);
                    announced |= last.UnlockedBreeds.Contains(PetBreed.Tuxedo);
                }
            }
        }

        Equal(true, announced, "reaching Bond 40 announces the Tuxedo");
        Equal(true, reopened.CanAdoptBreed(PetBreed.Tuxedo), "Bond 40 unlocks the Tuxedo");
        var grown = reopened.AdvancePetPreview(2, TimeSpan.FromDays(7));
        Equal(true, grown.BecameGrown, "a week's preview grows the cat");
        Equal("Siamese", string.Join(",", grown.UnlockedBreeds), "raising a cat to Grown unlocks the Siamese");
        Equal(true, reopened.HasCelebrations(2), "growing up waits as a celebration");
        Equal("Pounce,Climb,HighJump,Grown", string.Join(",", reopened.TakeCelebrations(2)),
            "the kitten's milestones and growing up are each played once, in order");
        Equal(false, reopened.HasCelebrations(2), "nothing is played twice");
    }

    private static void CheckTimeAndGrowth(BabySimulation game, PetSimulation pets)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        household.Adopt("Mia");
        household.AdoptPet("Pip", Ginger);
        Equal(2, household.Active.Slot, "the cat is on screen");

        now = Start.AddHours(2);
        household.Switch(1);
        Equal(Start, household.Companions[1].PetState.TimeCheckpoint, "a switch does not advance the cat");
        var preview = household.PreviewPetState(2);
        Equal(63.0, preview.Meter(PetMeter.Fullness), "Home previews the cat's decay");
        Equal(Start, household.Companions[1].PetState.TimeCheckpoint, "a preview writes nothing");

        household.AdvanceAll();
        Equal(now, household.Companions[0].State.TimeCheckpoint, "the baby advances on resume");
        Equal(now, household.Companions[1].PetState.TimeCheckpoint, "the cat advances on resume too");
        Equal(63.0, household.Companions[1].PetState.Meter(PetMeter.Fullness), "the cat decays with time");
        Equal(true, household.Companions[1].PetState.Meter(PetMeter.Fullness) < 70, "the cat's need is lower after two hours");

        now = Start.AddDays(7);
        household.AdvanceAll();
        Equal(PetStage.Grown, household.Companions[1].PetState.Stage, "the cat grows up after seven days");
        Equal(true, household.HasCelebrations(2), "growing up off screen waits as a celebration");
        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        Equal("Pounce,Climb,HighJump,Grown", string.Join(",", reopened.TakeCelebrations(2)),
            "the kitten's milestones and growing up survive reload");
        // A cat's needs fall at half the earlier rates (two meals a day) and at most eight hours count, so a week alone
        // leaves it peckish rather than below the Home dot's threshold; breakfast is due.
        Equal(true, reopened.Companions[1].PetState.Meter(PetMeter.Fullness) < 45, "a week alone leaves the cat peckish");
        Equal(true, reopened.Companions[1].PetSession.Routine.Any(slot => slot.Kind == RoutineSlotKind.MorningMeal &&
            slot.State == RoutineSlotState.Due), "and breakfast is due");
        Equal(true, reopened.ProgressFor(Species.Cat).RaisedToGrown, "a grown cat counts toward the species goal");
    }

    private static void CheckPetGoodbye(BabySimulation game, PetSimulation pets)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        household.Adopt("Mia");
        household.AdoptPet("Pip", Ginger);
        // Bond gains are capped each day (slice 7), so meals, a nap and petting over a few days reach Bond 40.
        for (var day = 0; day < 8 && !household.CanAdoptBreed(PetBreed.Tuxedo); day++)
        {
            now = Start.AddDays(day);
            foreach (var verb in new[] { PetVerb.Feed, PetVerb.Nap, PetVerb.Pet, PetVerb.Pet, PetVerb.Pet, PetVerb.Pet })
                household.ApplyPetCare(2, verb);
            now = Start.AddDays(day).AddHours(10);
            household.ApplyPetCare(2, PetVerb.Feed);
        }

        Equal(true, household.CanAdoptBreed(PetBreed.Tuxedo), "the cat's Bond unlocks the Tuxedo");

        now = now.AddDays(2);
        household.SayGoodbye(2);
        Equal<string>(null, store.SlotStore(2).Primary, "goodbye deletes the cat's record");
        var keepsake = household.Keepsakes.Single();
        Equal(CompanionKind.Pet, keepsake.Kind, "the keepsake is a pet's");
        Equal<Species?>(Species.Cat, keepsake.Species, "the keepsake records the species");
        Equal(PetBreed.DomesticShorthair, keepsake.PetAppearance.Breed, "the keepsake records the breed");
        Equal(PetStage.Young, keepsake.PetStage, "the keepsake records the stage");
        Equal("Pip", keepsake.Name, "the keepsake records the name");
        Equal<BabyAppearance>(null, keepsake.Appearance, "a pet keepsake has no baby look");
        Equal(true, household.CanAdoptBreed(PetBreed.Tuxedo), "an earned breed outlives the goodbye");

        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        var kept = reopened.Keepsakes.Single();
        Equal<Species?>(Species.Cat, kept.Species, "the species survives reload");
        Equal(Ginger, kept.PetAppearance, "the look survives reload");
        Equal(now, kept.FarewellAt, "the farewell date survives reload");
        Equal(true, reopened.CanAdoptBreed(PetBreed.Tuxedo), "earned breeds survive reload after a goodbye");
        Equal(true, reopened.CanAdoptPet, "the freed room takes a pet again");
        Equal(2, reopened.AdoptPet("Tig", new PetAppearance(PetBreed.Tuxedo, PetCoat.Black)).Slot, "the freed slot is reused");

        var interrupted = new MemoryHouseholdStore();
        var parting = new HouseholdSession(game, pets, interrupted, () => now, null);
        parting.AdoptPet("Pip", Ginger);
        interrupted.FailDeletes = true;
        ExpectThrows<IOException>(() => parting.SayGoodbye(2), "a failed delete is surfaced");
        interrupted.FailDeletes = false;
        var finished = new HouseholdSession(game, pets, interrupted, () => now, null);
        Equal(0, finished.Companions.Count, "an interrupted pet goodbye finishes on launch");
        Equal<Species?>(Species.Cat, finished.Keepsakes.Single().Species, "exactly one pet keepsake stays");
    }

    private static void CheckManifestKinds(BabySimulation game, PetSimulation pets)
    {
        const string legacy = "<household version=\"1\" active=\"2\"><companions>" +
            "<companion slot=\"1\" adopted=\"639028224000000000\" /><companion slot=\"2\" adopted=\"639028224000000000\" />" +
            "</companions><celebrations /><keepsakes /></household>";
        var manifest = HouseholdManifestCodec.Decode(legacy);
        Equal(true, manifest.Companions.All(entry => entry.Kind == CompanionKind.Baby), "a manifest without kinds lists babies");
        Equal(false, manifest.HasPetData, "a baby manifest has no pet data");
        Equal(legacy, HouseholdManifestCodec.Encode(manifest), "a baby manifest still encodes as v1");
        ExpectThrows<InvalidDataException>(() => HouseholdManifestCodec.Decode(legacy.Replace("slot=\"1\" adopted=\"639028224000000000\"",
            "slot=\"1\" adopted=\"639028224000000000\" kind=\"1\"")), "slot 1 is never a pet");
        ExpectThrows<InvalidDataException>(() => HouseholdManifestCodec.Decode(legacy.Replace("slot=\"2\" adopted=\"639028224000000000\"",
            "slot=\"2\" adopted=\"639028224000000000\" kind=\"7\"")), "an undefined kind is rejected");

        var golden = "<household version=\"2\" active=\"2\"><companions>" +
            "<companion slot=\"2\" adopted=\"639028224000000000\" kind=\"1\" /></companions><celebrations>" +
            "<celebration slot=\"2\" milestone=\"3\" /></celebrations><keepsakes>" +
            "<keepsake kind=\"1\" name=\"Tig\" adopted=\"639028224000000000\" farewell=\"639029088000000000\" age=\"864000000000\" " +
            "stage=\"1\" species=\"0\" breed=\"101\" coat=\"5\" accessory=\"1\" accent=\"2\" /></keepsakes><pets>" +
            "<species id=\"0\" bond=\"42\" grown=\"true\" day=\"739617\" meals=\"2\" together=\"true\" bedtime=\"true\" " +
            "rhythm=\"1\" best-rhythm=\"1\" last-full-day=\"739617\" full-days=\"1\" /></pets></household>";
        var pets2 = HouseholdManifestCodec.Decode(golden);
        Equal(CompanionKind.Pet, pets2.Companions[0].Kind, "golden v2 keeps the pet kind");
        Equal(CompanionMilestone.Grown, pets2.Celebrations[0].Milestone, "golden v2 keeps a Grown celebration");
        Equal<Species?>(Species.Cat, pets2.Keepsakes[0].Species, "golden v2 keeps the keepsake species");
        Equal(PetStage.Grown, pets2.Keepsakes[0].PetStage, "golden v2 keeps the keepsake stage");
        Equal(new PetAppearance(PetBreed.Tabby, PetCoat.Brown, null, PetAccessory.Collar, Outfit.Sky), pets2.Keepsakes[0].PetAppearance,
            "golden v2 keeps the keepsake look");
        Equal(42.0, pets2.SpeciesRecords[0].HighestBond, "golden v2 keeps the species Bond");
        Equal(1, pets2.SpeciesRecords[0].Progress.FullCareDays, "golden v2 keeps the care days");
        Equal(golden, HouseholdManifestCodec.Encode(pets2), "manifest v2 encoding is stable");
        ExpectThrows<InvalidDataException>(() => HouseholdManifestCodec.Decode(golden.Replace("breed=\"101\"", "breed=\"201\"")),
            "a keepsake breed of another species is rejected");
        ExpectThrows<InvalidDataException>(() => HouseholdManifestCodec.Decode(golden.Replace("breed=\"101\"", "breed=\"199\"")),
            "an undefined keepsake breed is rejected");

        // An older household build rewrites the manifest without kinds; the record still says it is a pet.
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, null);
        household.Adopt("Mia");
        household.AdoptPet("Pip", Ginger);
        store.ManifestPrimary = legacy;
        store.ManifestBackup = null;
        var reopened = new HouseholdSession(game, pets, store, () => now, null);
        Equal(CompanionKind.Pet, reopened.Companions.Single(companion => companion.Slot == 2).Kind,
            "a pet opens as a pet when the manifest has no kind");
        Equal("Pip", reopened.Companions.Single(companion => companion.Slot == 2).Name, "the pet's record is read");
        reopened.Switch(1);
        Equal(true, store.ManifestPrimary.Contains("kind=\"1\""), "the next write records the kind again");
    }

    private static void CheckUnreadablePets(BabySimulation game, PetSimulation pets)
    {
        var store = new MemoryHouseholdStore();
        store.SlotStore(2).Primary = "<pet";
        store.SlotStore(3).Primary = "<pet version=\"9\" />";
        var household = new HouseholdSession(game, pets, store, () => Start, null);
        Equal(2, household.Companions.Count, "unreadable pet slots are listed");
        Equal(true, household.Companions.All(companion => companion.Kind == CompanionKind.Pet && !companion.IsReadable),
            "an unreadable pet record is still known to be a pet");
        Equal(true, household.IsFull, "unreadable pets count toward capacity");
        Equal(false, household.CanAdoptPet, "an unreadable slot is never treated as free");
        Equal("<pet", store.SlotStore(2).Primary, "unreadable pet bytes are kept");
        Equal("<pet version=\"9\" />", store.SlotStore(3).Primary, "a newer pet record keeps its bytes");
        ExpectThrows<InvalidOperationException>(() => household.SayGoodbye(2), "an unreadable pet is not deleted");

        // A build from before pets opens every slot as a baby: a pet record is an unreadable save there,
        // which the household keeps without writing.
        var petRecord = new MemorySaveStore();
        new PetSession(pets, petRecord, () => Start).Adopt("Pip", Ginger);
        ExpectThrows<InvalidDataException>(() => new BabySession(game, petRecord, () => Start),
            "an older build cannot read a pet record as a baby");
    }

    private static void CheckRestingCat(BabySimulation game, PetSimulation pets)
    {
        var now = Start;
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, pets, store, () => now, rooms);
        household.Adopt("Mia");
        household.AdoptPet("Pip", Ginger);
        var tig = household.AdoptPet("Tig", new PetAppearance(PetBreed.DomesticShorthair, PetCoat.Gray));
        Equal(3, tig.Slot, "a second cat takes the next free slot");
        Equal(4, household.Capacity, "the rooms pack makes four rooms");

        rooms.Revoke(ProductIds.HomeRooms2);
        Equal(2, household.Capacity, "a refund leaves two rooms");
        Equal(true, household.IsResting(3), "the cat beyond the free rooms rests");
        Equal(false, household.IsResting(2), "the first cat stays awake");
        Equal(false, household.CanAdoptPet, "nobody new is adopted while someone rests");
        ExpectThrows<InvalidOperationException>(() => household.ApplyPetCare(3, PetVerb.Feed), "a resting cat is read-only");

        var restingSave = store.SlotStore(3).Primary;
        var restingWrites = store.SlotStore(3).Writes;
        now = Start.AddHours(6);
        household.AdvanceAll();
        Equal(restingWrites, store.SlotStore(3).Writes, "a resting cat makes no writes");
        Equal(restingSave, store.SlotStore(3).Primary, "a resting cat's record is untouched");
        Equal(Start, household.PreviewPetState(3).TimeCheckpoint, "a resting cat is previewed as it rests");
        Equal(70.0, household.Companions[2].PetState.Meter(PetMeter.Fullness), "a resting cat does not decay");
        Equal(now, household.Companions[1].PetState.TimeCheckpoint, "the awake cat advances");

        household.SwapResting(3, 1);
        Equal(true, household.IsResting(1), "the baby rests instead");
        Equal(false, household.IsResting(3), "the cat wakes");
        Equal(now, household.Companions.Single(companion => companion.Slot == 3).PetState.TimeCheckpoint,
            "the woken cat gets its one capped advance");
        Equal(PetCue.Content, household.ApplyPetCare(3, PetVerb.Feed).Cue, "the woken cat can be cared for");

        rooms.Grant(ProductIds.HomeRooms2);
        Equal(false, household.HasResting, "a restore wakes everyone");
    }

    // The record a session would write now: the state encoded.
    private static string PetSaveCodecOf(PetState state) => PetSaveCodec.Encode(state);

    private static void ExpectThrows<TException>(Action attempt, string description) where TException : Exception
    {
        try
        {
            attempt();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected {typeof(TException).Name}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }
}
