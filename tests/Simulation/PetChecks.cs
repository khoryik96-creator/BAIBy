using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// Pet catalog, unlock, simulation and save checks (docs/pet-mode.md).
static class PetChecks
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly PetCatalog Catalog = PetCatalog.Default;

    public static void Run()
    {
        CatalogValidation();
        SpeciesTable();
        BreedTable();
        UnlockRules();
        PremiumNeverStronger();
        AppearanceRules();
        TimeModel();
        CareVerbs();
        FishOverfeeding();
        SaveRoundTrips();
        GoldenPayload();
        InvalidPayloads();
        Console.WriteLine("Pet checks passed.");
    }

    private static void CatalogValidation()
    {
        Catalog.Validate();
        Equal(0, Catalog.Problems().Count, "default catalog has no problems");
        Equal(Enum.GetValues(typeof(PetBreed)).Length, Catalog.AllBreeds.Count, "every breed ID is catalogued once");
        Equal(Enum.GetValues(typeof(Species)).Length, Catalog.AllSpecies.Count, "every species is catalogued once");

        var tabby = Catalog.Get(PetBreed.Tabby);
        var bengal = Catalog.Get(PetBreed.Bengal);
        ExpectProblem(WithBreeds(Catalog.AllBreeds.Concat(new[] { tabby })), "Tabby is defined twice", "duplicate breed ID");
        ExpectProblem(ReplaceBreed(Clone(tabby, species: Species.Dog)), "outside the Dog ID block", "breed in another species block");
        ExpectProblem(ReplaceBreed(Clone(tabby, clearGoal: true)), "Earned breed Tabby needs a defined goal", "earned breed without goal");
        ExpectProblem(ReplaceBreed(Clone(tabby, goal: EarnedGoal.Bond40)), "should use goal FirstFullCareDay", "earned ladder order");
        ExpectProblem(ReplaceBreed(Clone(bengal, productId: null)), "Premium breed Bengal needs a product ID", "premium without product ID");
        ExpectProblem(ReplaceBreed(Clone(bengal, productId: "breed.dog.bengal")), "Premium breed Bengal needs a product ID", "premium product ID for another species");
        ExpectProblem(ReplaceBreed(Clone(bengal, productId: "breed.cat.Bengal-Cat")), "Premium breed Bengal needs a product ID", "store-invalid product ID");
        ExpectProblem(ReplaceBreed(Clone(Catalog.Get(PetBreed.Husky), productId: "breed.cat.bengal")), "needs a product ID", "product ID species must match");
        ExpectProblem(ReplaceBreed(Clone(Catalog.Get(PetBreed.Ragdoll), productId: bengal.ProductId)), "is used twice", "duplicate product ID");
        ExpectProblem(ReplaceBreed(Clone(tabby, productId: "breed.cat.tabby")), "not premium and cannot have a product ID", "earned breed with product ID");
        ExpectProblem(ReplaceBreed(Clone(tabby, tendencies: Trade(PetMeter.Energy, PetMeter.Fullness, 25))), "outside 10 to 20%", "tendency above 20 percent");
        ExpectProblem(ReplaceBreed(Clone(tabby, tendencies: Trade(PetMeter.Energy, PetMeter.Fullness, 5))), "outside 10 to 20%", "tendency below 10 percent");
        ExpectProblem(ReplaceBreed(Clone(tabby, tendencies: new[] { new BreedTendency(PetMeter.Energy, 15) })), "must balance", "unbalanced tendency");
        // Equal percentages on meters with different base decay do not cancel: Energy 6/h, Fullness 7/h.
        ExpectProblem(ReplaceBreed(Clone(tabby, tendencies: Trade(PetMeter.Energy, PetMeter.Fullness, 15))), "must balance by decay rate",
            "percent-balanced trade that changes total decay");
        ExpectProblem(ReplaceBreed(Clone(Catalog.Get(PetBreed.TeddyBear), tendencies: Trade(PetMeter.Bedding, PetMeter.Exercise, 10))),
            "must balance by decay rate", "premium breed that is net easier");
        ExpectProblem(ReplaceBreed(Clone(tabby, tendencies: Trade(PetMeter.Water, PetMeter.Fullness, 15))), "which Cat does not show", "tendency on another species' meter");
        ExpectProblem(ReplaceBreed(Clone(tabby, tendencies: Trade(PetMeter.Energy, PetMeter.Fullness, 10)
            .Concat(new[] { new BreedTendency(PetMeter.Comfort, 10) }).ToArray())), "more than 2 tendencies", "three tendencies");
        ExpectProblem(ReplaceBreed(Clone(bengal, tendencies: Trade(PetMeter.Energy, PetMeter.Comfort, 20))), "stronger tendencies", "premium stronger than free");
        ExpectProblem(ReplaceBreed(Clone(tabby, art: BreedArtTier.NewSilhouette)), "must ship with the species", "earned breed blocked on new art");
        ExpectProblem(ReplaceBreed(Clone(bengal, available: false)), "Premium recolor breed Bengal ships with its species", "recolor premium ships with species");
        ExpectProblem(ReplaceBreed(Clone(tabby, coats: new PetCoat[0])), "needs at least one coat", "breed without coats");
        ExpectProblem(WithBreeds(Catalog.AllBreeds.Where(breed => breed.Breed != PetBreed.Sphynx)), "Sphynx has no definition", "missing breed");
        ExpectProblem(WithBreeds(Catalog.AllBreeds.Where(breed => breed.Breed != PetBreed.DomesticShorthair)), "exactly one free starter", "species without starter");
        ExpectProblem(new PetCatalog(Catalog.AllSpecies.Concat(new[] { Catalog.Get(Species.Cat) }), Catalog.AllBreeds),
            "Species Cat is defined twice", "duplicate species");

        var cat = Catalog.Get(Species.Cat);
        ExpectProblem(ReplaceSpecies(new SpeciesDefinition(Species.Cat, cat.GrownAge, cat.Meters.ToArray(),
            cat.Verbs.Concat(new[] { new VerbDefinition(PetVerb.Walk, 0, 0, 0) }).ToArray(), cat.Moments.ToArray(), cat.Accessories.ToArray())),
            "Cat Walk changes nothing", "verb without effect");
        ExpectProblem(ReplaceSpecies(new SpeciesDefinition(Species.Cat, cat.GrownAge, cat.Meters.ToArray(),
            cat.Verbs.Concat(new[] { new VerbDefinition(PetVerb.Walk, 1, 0, 0, new MeterEffect(PetMeter.Exercise, 10)) }).ToArray(),
            cat.Moments.ToArray(), cat.Accessories.ToArray())), "which the species does not show", "verb on missing meter");
        ExpectProblem(ReplaceSpecies(new SpeciesDefinition(Species.Cat, cat.GrownAge, cat.Meters.ToArray(),
            cat.Verbs.Concat(new[] { new VerbDefinition(PetVerb.Walk, 1, 0, 0, new MeterEffect(PetMeter.Fullness, 30)) }).ToArray(),
            cat.Moments.ToArray(), cat.Accessories.ToArray())), "same effect as another verb", "verbs must differ");

        ExpectThrows<InvalidOperationException>(
            () => new PetSimulation(ReplaceBreed(Clone(tabby, clearGoal: true)), PetSimulation.DefaultMaxNeedDecay),
            "simulation refuses an invalid catalog");

        var catPack = Catalog.AllPacks.Single(pack => pack.Species == Species.Cat);
        ExpectProblem(ReplacePack(new BreedPackDefinition("breedpack.cat", Species.Cat, catPack.Breeds.Concat(new[] { PetBreed.Tabby }).ToArray())),
            "grants Tabby, which is not premium", "pack grants an earned breed");
        ExpectProblem(ReplacePack(new BreedPackDefinition("breedpack.cat", Species.Cat, catPack.Breeds.Concat(new[] { PetBreed.Husky }).ToArray())),
            "grants Husky, which is not a Cat", "species pack grants another species");
        ExpectProblem(ReplacePack(new BreedPackDefinition("breedpack.cat", Species.Cat, catPack.Breeds.Concat(new[] { (PetBreed)150 }).ToArray())),
            "grants 150, which has no definition", "dangling pack grant");
        ExpectProblem(ReplacePack(new BreedPackDefinition("breedpack.cat", Species.Cat, new[] { PetBreed.Bengal, PetBreed.Bengal })),
            "lists a breed twice", "pack lists a breed twice");
        ExpectProblem(ReplacePack(new BreedPackDefinition("breedpack.cat", Species.Cat, new PetBreed[0])), "grants no breeds", "empty pack");
        ExpectProblem(ReplacePack(new BreedPackDefinition("breedpack.dog", Species.Cat, catPack.Breeds.ToArray())),
            "needs the product ID breedpack.cat", "pack ID names another species");
        ExpectProblem(ReplacePack(new BreedPackDefinition("breedpack.cats", null, catPack.Breeds.ToArray())),
            "needs the product ID breedpack.all", "every-species pack ID");
        ExpectProblem(new PetCatalog(Catalog.AllSpecies, Catalog.AllBreeds, Catalog.AllPacks.Concat(new[] { catPack })),
            "breedpack.cat is defined twice", "duplicate pack");
    }

    private static void SpeciesTable()
    {
        ExpectMeters(Species.Cat, PetMeter.Fullness, PetMeter.Energy, PetMeter.Comfort, PetMeter.Cleanliness);
        ExpectMeters(Species.Dog, PetMeter.Fullness, PetMeter.Energy, PetMeter.Exercise, PetMeter.Cleanliness);
        ExpectMeters(Species.Hamster, PetMeter.Fullness, PetMeter.Energy, PetMeter.Bedding, PetMeter.Exercise);
        ExpectMeters(Species.Fish, PetMeter.Fullness, PetMeter.Water, PetMeter.Calm);
        ExpectMeters(Species.Rabbit, PetMeter.Hay, PetMeter.Fullness, PetMeter.Litter, PetMeter.Comfort);
        ExpectVerbs(Species.Cat, PetVerb.Feed, PetVerb.Pet, PetVerb.Wand, PetVerb.Brush, PetVerb.Nap);
        ExpectVerbs(Species.Dog, PetVerb.Feed, PetVerb.Walk, PetVerb.Fetch, PetVerb.Brush, PetVerb.Rest);
        ExpectVerbs(Species.Hamster, PetVerb.Bowl, PetVerb.Wheel, PetVerb.FluffBedding, PetVerb.GentleHold, PetVerb.Nap);
        ExpectVerbs(Species.Fish, PetVerb.SprinkleFood, PetVerb.WipeGlass, PetVerb.WaterChange, PetVerb.Rest);
        ExpectVerbs(Species.Rabbit, PetVerb.Hay, PetVerb.Greens, PetVerb.Groom, PetVerb.FloorTime, PetVerb.Litter);

        var grownDays = new Dictionary<Species, int>
        {
            [Species.Cat] = 7, [Species.Dog] = 10, [Species.Hamster] = 5, [Species.Fish] = 5, [Species.Rabbit] = 7,
        };
        foreach (var pair in grownDays)
            Equal(TimeSpan.FromDays(pair.Value), Catalog.Get(pair.Key).GrownAge, $"{pair.Key} grows up on day {pair.Value}");

        Equal(true, Catalog.Get(Species.Cat).Moments.Contains(PetMoment.Kneading), "cat kneads");
        Equal(PetMoment.ReunionGreeting, Catalog.Get(Species.Dog).Moments.Single(), "dog reunion greeting");
        Equal(PetMoment.CheekPouches, Catalog.Get(Species.Hamster).Moments.Single(), "hamster cheek pouches");
        Equal(PetMoment.SwimsToGlass, Catalog.Get(Species.Fish).Moments.Single(), "fish swims to glass");
        Equal(PetMoment.Binky, Catalog.Get(Species.Rabbit).Moments.Single(), "rabbit binkies");
        foreach (var species in Catalog.AllSpecies)
            Equal(true, species.Meters.Count <= 4, $"{species.Species} room shows at most 4 meters");

        Equal(0, (int)Species.Cat, "Cat ID");
        Equal(1, (int)Species.Dog, "Dog ID");
        Equal(2, (int)Species.Fish, "Fish ID");
        Equal(3, (int)Species.Rabbit, "Rabbit ID");
        Equal(4, (int)Species.Hamster, "Hamster ID");
        Equal(0, (int)CompanionKind.Baby, "Baby companion ID");
        Equal(1, (int)CompanionKind.Pet, "Pet companion ID");
        foreach (PetBreed breed in Enum.GetValues(typeof(PetBreed)))
        {
            var block = PetIds.BreedBlockStart(PetIds.SpeciesOf(breed));
            Equal(true, (int)breed >= block && (int)breed < block + PetIds.BreedBlockSize, $"{breed} sits in its species block");
        }
    }

    private static void BreedTable()
    {
        var free = new[] { PetBreed.DomesticShorthair, PetBreed.MixedBreed, PetBreed.SyrianGolden, PetBreed.Goldfish, PetBreed.Dutch };
        var earned = new Dictionary<PetBreed, EarnedGoal>
        {
            [PetBreed.Tabby] = EarnedGoal.FirstFullCareDay, [PetBreed.Tuxedo] = EarnedGoal.Bond40,
            [PetBreed.Siamese] = EarnedGoal.RaisedToGrown, [PetBreed.Calico] = EarnedGoal.SevenOnTimeCareDays,
            [PetBreed.Golden] = EarnedGoal.FirstFullCareDay, [PetBreed.Beagle] = EarnedGoal.Bond40, [PetBreed.Shiba] = EarnedGoal.RaisedToGrown,
            [PetBreed.WinterWhite] = EarnedGoal.FirstFullCareDay, [PetBreed.Roborovski] = EarnedGoal.Bond40,
            [PetBreed.Guppy] = EarnedGoal.FirstFullCareDay, [PetBreed.Betta] = EarnedGoal.Bond40,
            [PetBreed.NetherlandDwarf] = EarnedGoal.FirstFullCareDay, [PetBreed.HollandLop] = EarnedGoal.Bond40,
        };
        var premium = new Dictionary<PetBreed, (string ProductId, BreedArtTier Art)>
        {
            [PetBreed.Ragdoll] = ("breed.cat.ragdoll", BreedArtTier.Overlay),
            [PetBreed.MaineCoon] = ("breed.cat.maine_coon", BreedArtTier.Overlay),
            [PetBreed.Bengal] = ("breed.cat.bengal", BreedArtTier.Recolor),
            [PetBreed.BritishShorthair] = ("breed.cat.british_shorthair", BreedArtTier.Recolor),
            [PetBreed.RussianBlue] = ("breed.cat.russian_blue", BreedArtTier.Recolor),
            [PetBreed.Sphynx] = ("breed.cat.sphynx", BreedArtTier.NewSilhouette),
            [PetBreed.Corgi] = ("breed.dog.corgi", BreedArtTier.NewSilhouette),
            [PetBreed.Poodle] = ("breed.dog.poodle", BreedArtTier.Overlay),
            [PetBreed.Dachshund] = ("breed.dog.dachshund", BreedArtTier.NewSilhouette),
            [PetBreed.Husky] = ("breed.dog.husky", BreedArtTier.Recolor),
            [PetBreed.Samoyed] = ("breed.dog.samoyed", BreedArtTier.Overlay),
            [PetBreed.Pomeranian] = ("breed.dog.pomeranian", BreedArtTier.Overlay),
            [PetBreed.Dalmatian] = ("breed.dog.dalmatian", BreedArtTier.Recolor),
            [PetBreed.BorderCollie] = ("breed.dog.border_collie", BreedArtTier.Overlay),
            [PetBreed.TeddyBear] = ("breed.hamster.teddy_bear", BreedArtTier.Overlay),
            [PetBreed.Panda] = ("breed.hamster.panda", BreedArtTier.Recolor),
            [PetBreed.Sapphire] = ("breed.hamster.sapphire", BreedArtTier.Recolor),
            [PetBreed.Campbells] = ("breed.hamster.campbells", BreedArtTier.Recolor),
            [PetBreed.NeonTetraSchool] = ("breed.fish.neon_tetra_school", BreedArtTier.NewSilhouette),
            [PetBreed.Angelfish] = ("breed.fish.angelfish", BreedArtTier.Overlay),
            [PetBreed.Koi] = ("breed.fish.koi", BreedArtTier.Recolor),
            [PetBreed.Clownfish] = ("breed.fish.clownfish", BreedArtTier.Recolor),
            [PetBreed.Discus] = ("breed.fish.discus", BreedArtTier.Overlay),
            [PetBreed.Lionhead] = ("breed.rabbit.lionhead", BreedArtTier.NewSilhouette),
            [PetBreed.Angora] = ("breed.rabbit.angora", BreedArtTier.Overlay),
            [PetBreed.Rex] = ("breed.rabbit.rex", BreedArtTier.Recolor),
            [PetBreed.FlemishGiant] = ("breed.rabbit.flemish_giant", BreedArtTier.NewSilhouette),
            [PetBreed.Harlequin] = ("breed.rabbit.harlequin", BreedArtTier.Recolor),
        };
        Equal(Enum.GetValues(typeof(PetBreed)).Length, free.Length + earned.Count + premium.Count, "the doc's breed table covers every ID");

        foreach (var breed in Catalog.AllBreeds)
        {
            var name = breed.Breed.ToString();
            Equal(PetIds.SpeciesOf(breed.Breed), breed.Species, $"{name} species follows its ID block");
            if (free.Contains(breed.Breed))
            {
                Equal(BreedUnlock.Free, breed.Unlock, $"{name} is the free starter");
                Equal<string>(null, breed.ProductId, $"{name} has no product");
                Equal(true, breed.AvailableNow, $"{name} ships with its species");
            }
            else if (earned.TryGetValue(breed.Breed, out var goal))
            {
                Equal(BreedUnlock.Earned, breed.Unlock, $"{name} is earned");
                Equal<EarnedGoal?>(goal, breed.Goal, $"{name} goal");
                Equal<string>(null, breed.ProductId, $"{name} is never sold");
                Equal(true, breed.AvailableNow, $"{name} ships with its species");
                Equal(true, breed.Art != BreedArtTier.NewSilhouette, $"{name} never waits for new art");
            }
            else
            {
                var expected = premium[breed.Breed];
                Equal(BreedUnlock.Premium, breed.Unlock, $"{name} is premium");
                Equal(expected.ProductId, breed.ProductId, $"{name} product ID");
                Equal(expected.Art, breed.Art, $"{name} art tier");
                Equal(expected.Art == BreedArtTier.Recolor, breed.AvailableNow, $"{name} availability follows its art tier");
                Equal<EarnedGoal?>(null, breed.Goal, $"{name} needs no goal");
            }

            Equal(true, breed.Tendencies.Count <= 2, $"{name} has at most two tendencies");
            foreach (var tendency in breed.Tendencies)
                Equal(true, Math.Abs(tendency.Percent) >= 10 && Math.Abs(tendency.Percent) <= 20, $"{name} tendency is gentle");
            Equal(true, breed.Coats.Count > 0 && breed.OffersCoat(breed.DefaultCoat), $"{name} default coat is offered");
        }

        foreach (var species in Catalog.AllSpecies)
        {
            var count = Catalog.BreedsOf(species.Species).Count(breed => breed.Unlock == BreedUnlock.Premium);
            Equal(true, count >= 4 && count <= 8, $"{species.Species} has 4 to 8 premium breeds");
        }
    }

    private static void UnlockRules()
    {
        var everything = Catalog.AllBreeds.Where(breed => breed.ProductId != null).Select(breed => breed.ProductId).ToList();
        var full = new SpeciesGoalProgress(3, 100, true, 30);
        foreach (var breed in Catalog.AllBreeds)
        {
            var name = breed.Breed.ToString();
            switch (breed.Unlock)
            {
                case BreedUnlock.Free:
                    Equal(true, PetUnlocks.IsUnlocked(Catalog, breed.Breed, null, null), $"{name} is free with nothing");
                    Equal(true, PetUnlocks.CanAdopt(Catalog, breed.Breed, SpeciesGoalProgress.None, new string[0]), $"{name} is adoptable at once");
                    break;
                case BreedUnlock.Earned:
                    var goal = breed.Goal.Value;
                    Equal(false, PetUnlocks.IsUnlocked(Catalog, breed.Breed, SpeciesGoalProgress.None, everything), $"{name} is locked before its goal");
                    Equal(false, PetUnlocks.IsUnlocked(Catalog, breed.Breed, SpeciesGoalProgress.None,
                        everything.Concat(new[] { $"breed.{PetIds.Key(breed.Species)}.{name.ToLowerInvariant()}" }).ToList()),
                        $"{name} cannot be bought");
                    Equal(true, PetUnlocks.IsUnlocked(Catalog, breed.Breed, ProgressFor(goal), null), $"{name} unlocks with its goal");
                    foreach (EarnedGoal other in Enum.GetValues(typeof(EarnedGoal)))
                    {
                        if (other != goal)
                            Equal(false, PetUnlocks.IsUnlocked(Catalog, breed.Breed, ProgressFor(other), null), $"{name} ignores {other}");
                    }
                    break;
                case BreedUnlock.Premium:
                    Equal(false, PetUnlocks.IsUnlocked(Catalog, breed.Breed, full, null), $"{name} is not earned by care");
                    Equal(false, PetUnlocks.IsUnlocked(Catalog, breed.Breed, full, everything.Where(id => id != breed.ProductId).ToList()),
                        $"{name} needs its own product");
                    Equal(true, PetUnlocks.IsUnlocked(Catalog, breed.Breed, SpeciesGoalProgress.None, new[] { breed.ProductId }), $"{name} unlocks when owned");
                    Equal(breed.AvailableNow, PetUnlocks.CanAdopt(Catalog, breed.Breed, SpeciesGoalProgress.None, new[] { breed.ProductId }),
                        $"{name} is adoptable once owned only if its art has shipped");
                    break;
            }
        }

        PackUnlocks(full);

        Equal(false, PetUnlocks.IsGoalMet(EarnedGoal.Bond40, new SpeciesGoalProgress(0, 39.9, false, 0)), "Bond goal waits for 40");
        Equal(true, PetUnlocks.IsGoalMet(EarnedGoal.Bond40, new SpeciesGoalProgress(0, 40, false, 0)), "Bond goal at 40");
        Equal(false, PetUnlocks.IsGoalMet(EarnedGoal.SevenOnTimeCareDays, new SpeciesGoalProgress(9, 0, false, 6)), "seven on-time days needed");
        Equal(false, PetUnlocks.IsGoalMet(EarnedGoal.FirstFullCareDay, SpeciesGoalProgress.None), "no care day yet");

        Equal("DomesticShorthair", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Cat, SpeciesGoalProgress.None, null)),
            "a new household adopts the starter cat");
        Equal("DomesticShorthair,Tabby,Bengal", string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Cat,
            ProgressFor(EarnedGoal.FirstFullCareDay), new[] { "breed.cat.bengal", "breed.cat.ragdoll", "breed.dog.husky" })),
            "earned and owned shipped breeds are adoptable");
        Equal("DomesticShorthair,Tabby,Tuxedo,Siamese,Calico,Bengal,BritishShorthair,RussianBlue",
            string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Cat, full, everything)), "everything shipped is adoptable");

        var merged = new SpeciesGoalProgress(1, 45, false, 2).Merge(new SpeciesGoalProgress(0, 20, true, 7));
        Equal(1, merged.FullCareDays, "merge keeps care days");
        Equal(45.0, merged.HighestBond, "merge keeps best Bond");
        Equal(true, merged.RaisedToGrown, "merge keeps grown");
        Equal(7, merged.OnTimeCareDays, "merge keeps on-time days");

        var sim = new PetSimulation();
        var pet = sim.Adopt("Pip", PetAppearance.Default(Catalog, PetBreed.DomesticShorthair), Start);
        // A cat's Bond follows the baby's rules (slice 7): gains are capped each day, so Bond 40 takes a few days.
        for (var day = 0; day < 5; day++)
        {
            pet = sim.Advance(pet, Start.AddDays(day).AddHours(9)).State;
            for (var index = 0; index < 4; index++)
                pet = sim.ApplyCare(pet, PetVerb.Pet).State;
        }

        pet = sim.Advance(pet, Start.AddDays(7)).State;
        var fromPet = SpeciesGoalProgress.None.WithPet(pet);
        Equal(true, PetUnlocks.IsGoalMet(EarnedGoal.Bond40, fromPet), "a pet's Bond counts toward the goal");
        Equal(true, PetUnlocks.IsGoalMet(EarnedGoal.RaisedToGrown, fromPet), "a grown pet counts toward the goal");
    }

    private static void PackUnlocks(SpeciesGoalProgress full)
    {
        // Pack contents as sold in the first release. Never shrink these lists; packs never grow either.
        var expected = new Dictionary<Species, PetBreed[]>
        {
            [Species.Cat] = new[] { PetBreed.Ragdoll, PetBreed.MaineCoon, PetBreed.Bengal, PetBreed.BritishShorthair, PetBreed.RussianBlue, PetBreed.Sphynx },
            [Species.Dog] = new[] { PetBreed.Corgi, PetBreed.Poodle, PetBreed.Dachshund, PetBreed.Husky, PetBreed.Samoyed, PetBreed.Pomeranian,
                PetBreed.Dalmatian, PetBreed.BorderCollie },
            [Species.Fish] = new[] { PetBreed.NeonTetraSchool, PetBreed.Angelfish, PetBreed.Koi, PetBreed.Clownfish, PetBreed.Discus },
            [Species.Rabbit] = new[] { PetBreed.Lionhead, PetBreed.Angora, PetBreed.Rex, PetBreed.FlemishGiant, PetBreed.Harlequin },
            [Species.Hamster] = new[] { PetBreed.TeddyBear, PetBreed.Panda, PetBreed.Sapphire, PetBreed.Campbells },
        };
        Equal(6, Catalog.AllPacks.Count, "a pack per species and one for every species");
        foreach (var pair in expected)
        {
            var pack = Catalog.AllPacks.Single(candidate => candidate.Species == pair.Key);
            Equal($"breedpack.{PetIds.Key(pair.Key)}", pack.ProductId, $"{pair.Key} pack product ID");
            Equal(string.Join(",", pair.Value), string.Join(",", pack.Breeds), $"{pair.Key} pack lists its first-release premium breeds");
        }

        var all = Catalog.AllPacks.Single(pack => pack.Species == null);
        Equal("breedpack.all", all.ProductId, "every-breed pack product ID");
        Equal(string.Join(",", expected.Values.SelectMany(breeds => breeds).OrderBy(breed => breed)),
            string.Join(",", all.Breeds.OrderBy(breed => breed)), "every-breed pack lists every first-release premium breed");

        foreach (var breed in Catalog.AllBreeds)
        {
            var name = breed.Breed.ToString();
            var speciesPack = PetIds.BreedPackProductId(breed.Species);
            if (breed.Unlock != BreedUnlock.Premium)
            {
                if (breed.Unlock == BreedUnlock.Earned)
                    Equal(false, PetUnlocks.IsUnlocked(Catalog, breed.Breed, SpeciesGoalProgress.None, new[] { speciesPack, "breedpack.all" }),
                        $"{name} is never unlocked by a pack");
                Equal(0, Catalog.ProductsGranting(breed.Breed).Count, $"{name} is sold by nothing");
                continue;
            }

            Equal(true, PetUnlocks.IsUnlocked(Catalog, breed.Breed, SpeciesGoalProgress.None, new[] { speciesPack }), $"{name} unlocks with its species pack");
            Equal(true, PetUnlocks.IsUnlocked(Catalog, breed.Breed, SpeciesGoalProgress.None, new[] { "breedpack.all" }), $"{name} unlocks with every-breed pack");
            Equal(breed.AvailableNow, PetUnlocks.CanAdopt(Catalog, breed.Breed, SpeciesGoalProgress.None, new[] { speciesPack }),
                $"{name} is adoptable from the pack once its art has shipped");
            var otherPacks = Catalog.AllPacks.Where(pack => pack.Species.HasValue && pack.Species.Value != breed.Species)
                .Select(pack => pack.ProductId).ToList();
            Equal(false, PetUnlocks.IsUnlocked(Catalog, breed.Breed, full, otherPacks), $"{name} is not in other species' packs");
            Equal($"{breed.ProductId},{speciesPack},breedpack.all", string.Join(",", Catalog.ProductsGranting(breed.Breed)),
                $"{name} is sold singly, in its species pack and in every-breed pack");
        }

        Equal(true, PetUnlocks.CanAdopt(Catalog, PetBreed.Bengal, SpeciesGoalProgress.None, new[] { "breedpack.cat" }), "cat pack adopts a Bengal");
        Equal(true, PetUnlocks.CanAdopt(Catalog, PetBreed.Koi, SpeciesGoalProgress.None, new[] { "breedpack.all" }), "every-breed pack adopts a Koi");
        Equal(false, PetUnlocks.CanAdopt(Catalog, PetBreed.Sphynx, SpeciesGoalProgress.None, new[] { "breedpack.cat" }),
            "a pack breed waits for its art");
        Equal("DomesticShorthair,Bengal,BritishShorthair,RussianBlue",
            string.Join(",", PetUnlocks.AdoptableBreeds(Catalog, Species.Cat, SpeciesGoalProgress.None, new[] { "breedpack.cat" })),
            "a new household with the cat pack adopts the starter and shipped premium cats");
        Equal(false, Catalog.GrantsBreed("breedpack.dog", PetBreed.Bengal), "the dog pack grants no cat");
        Equal(false, Catalog.GrantsBreed(null, PetBreed.Bengal), "no product grants nothing");

        // A pack sells only what it lists: a catalog whose cat pack predates the Sphynx keeps it out.
        var catPack = Catalog.AllPacks.Single(pack => pack.Species == Species.Cat);
        var older = ReplacePack(new BreedPackDefinition("breedpack.cat", Species.Cat, catPack.Breeds.Where(breed => breed != PetBreed.Sphynx).ToArray()));
        Equal(0, older.Problems().Count, "a pack may list fewer breeds than its species has");
        Equal(false, PetUnlocks.IsUnlocked(older, PetBreed.Sphynx, SpeciesGoalProgress.None, new[] { "breedpack.cat" }),
            "a breed added after a pack is not in it");
        Equal(true, PetUnlocks.IsUnlocked(older, PetBreed.Sphynx, SpeciesGoalProgress.None, new[] { "breed.cat.sphynx" }),
            "and is still sold singly");
    }

    private static void PremiumNeverStronger()
    {
        foreach (var species in Catalog.AllSpecies)
        {
            var breeds = Catalog.BreedsOf(species.Species);
            var strongestFree = breeds.Where(breed => breed.Unlock != BreedUnlock.Premium).Max(breed => breed.TendencyMagnitude);
            var strongestFreeSingle = breeds.Where(breed => breed.Unlock != BreedUnlock.Premium)
                .SelectMany(breed => breed.Tendencies).Max(tendency => Math.Abs(tendency.Percent));
            foreach (var premium in breeds.Where(breed => breed.Unlock == BreedUnlock.Premium))
            {
                Equal(true, premium.TendencyMagnitude <= strongestFree, $"{premium.Breed} is no stronger in total than free {species.Species}");
                Equal(true, premium.Tendencies.All(tendency => Math.Abs(tendency.Percent) <= strongestFreeSingle),
                    $"{premium.Breed} has no tendency stronger than a free {species.Species}");
                Close(0, premium.NetDecayChangePerHour(species), $"{premium.Breed} tendencies balance by decay rate");
                Equal(true, premium.Tendencies.All(tendency => species.HasMeter(tendency.Meter)), $"{premium.Breed} uses species meters");
            }

            Equal(true, breeds.All(breed => Math.Abs(breed.NetDecayChangePerHour(species)) < 1e-9), $"no {species.Species} breed is harder");
        }

        // Absolute rule, measured in play: every breed of a species loses the same total need over
        // the 8-hour cap, so no breed (premium or free) is easier or harder to keep.
        var sim = new PetSimulation();
        var morning = Start.AddHours(8);
        foreach (var species in Catalog.AllSpecies)
        {
            var baseline = species.Meters.Sum(meter => Math.Min(meter.Initial, 8 * meter.DecayPerHour));
            foreach (var breed in Catalog.BreedsOf(species.Species))
            {
                var pet = sim.Adopt("Pip", PetAppearance.Default(Catalog, breed.Breed), morning);
                var later = sim.Advance(pet, morning.AddHours(8)).State;
                var lost = species.Meters.Sum(meter => pet.Meter(meter.Meter) - later.Meter(meter.Meter));
                Close(baseline, lost, $"{breed.Breed} loses the same need in 8 hours as any {species.Species}");
            }
        }
    }

    private static void AppearanceRules()
    {
        var look = new PetAppearance(PetBreed.Bengal, PetCoat.Silver, "coat.cat.tortoiseshell", PetAccessory.Collar, Outfit.Sky);
        var same = new PetAppearance(PetBreed.Bengal, PetCoat.Silver, "coat.cat.tortoiseshell", PetAccessory.Collar, Outfit.Sky);
        Equal(true, look.Equals(same), "appearance has value equality");
        Equal(look.GetHashCode(), same.GetHashCode(), "equal looks share a hash");
        Equal(false, look.Equals(look.WithCoat(PetCoat.Golden)), "coat changes the look");
        Equal(false, look.Equals(look.WithSpecialCoat(null)), "special coat changes the look");
        Equal(false, look.Equals(look.WithAccessory(PetAccessory.Bow)), "accessory changes the look");
        Equal(false, look.Equals(look.WithAccessoryAccent(Outfit.Oat)), "accent changes the look");
        Equal(false, look.Equals(new PetAppearance(PetBreed.Ragdoll, PetCoat.Silver, "coat.cat.tortoiseshell", PetAccessory.Collar, Outfit.Sky)),
            "breed changes the look");
        Equal(Species.Cat, look.Species, "appearance knows its species");
        Equal(PetCoat.Golden, PetAppearance.Default(Catalog, PetBreed.Bengal).Coat, "default look uses the breed's first coat");

        ExpectThrows<ArgumentOutOfRangeException>(() => new PetAppearance((PetBreed)199, PetCoat.Cream), "undefined breed");
        ExpectThrows<ArgumentOutOfRangeException>(() => new PetAppearance(PetBreed.Bengal, (PetCoat)99), "undefined coat");
        ExpectThrows<ArgumentOutOfRangeException>(() => new PetAppearance(PetBreed.Bengal, PetCoat.Golden, null, (PetAccessory)99), "undefined accessory");
        ExpectThrows<ArgumentOutOfRangeException>(() => new PetAppearance(PetBreed.Bengal, PetCoat.Golden, null, PetAccessory.None, (Outfit)99), "undefined accent");
        ExpectThrows<ArgumentException>(() => new PetAppearance(PetBreed.Bengal, PetCoat.Golden, "coat.dog.merle"), "special coat for another species");
        ExpectThrows<ArgumentException>(() => new PetAppearance(PetBreed.Bengal, PetCoat.Golden, "coat.cat.Tortie"), "special coat ID is lowercase");
        ExpectThrows<ArgumentException>(() => new PetAppearance(PetBreed.Bengal, PetCoat.Golden, "coat.cat."), "special coat needs a name");
        Equal("coat.dog.merle", new PetAppearance(PetBreed.MixedBreed, PetCoat.Tan, "coat.dog.merle").SpecialCoatId, "free breeds accept special coats");

        Catalog.ValidateChoice(look);
        ExpectThrows<ArgumentException>(() => Catalog.ValidateChoice(look.WithCoat(PetCoat.Blue)), "breed-limited coats");
        ExpectThrows<ArgumentException>(() => Catalog.ValidateChoice(new PetAppearance(PetBreed.Goldfish, PetCoat.Orange, null, PetAccessory.Collar)),
            "fish cannot wear a collar");
        Catalog.ValidateChoice(new PetAppearance(PetBreed.Goldfish, PetCoat.Orange, "coat.fish.aurora_fins", PetAccessory.TankCastle));
        Equal(PetCoat.Golden, Catalog.EffectiveCoat(look.WithCoat(PetCoat.Blue)), "an unoffered coat renders the breed default");
        Equal(PetCoat.Silver, Catalog.EffectiveCoat(look), "an offered coat renders as chosen");
        Equal(PetAccessory.None, Catalog.EffectiveAccessory(new PetAppearance(PetBreed.Goldfish, PetCoat.Orange, null, PetAccessory.Collar)),
            "an unoffered accessory is hidden");

        var sim = new PetSimulation();
        ExpectThrows<ArgumentException>(() => sim.Adopt("Kit", look.WithCoat(PetCoat.Blue), Start), "adoption checks the coat");
        ExpectThrows<ArgumentException>(() => sim.Adopt(" ", look, Start), "adoption needs a name");
        var pet = sim.Adopt("  Kit ", look, Start);
        Equal("Kit", pet.Name, "name is trimmed");
        Equal(look, pet.Appearance, "adopted look");
        Equal(pet, pet.WithAppearance(same), "same look is a no-op");
        Equal(PetAccessory.Bow, pet.WithAppearance(look.WithAccessory(PetAccessory.Bow)).Appearance.Accessory, "restyle keeps the pet");
        ExpectThrows<ArgumentException>(() => pet.WithAppearance(PetAppearance.Default(Catalog, PetBreed.Ragdoll)), "breed is fixed");
    }

    private static void TimeModel()
    {
        var sim = new PetSimulation();
        // A cat sleeps through quiet hours (slice 7), so the time model starts in the morning, when every species
        // spends its first eight hours awake.
        var Start = PetChecks.Start.AddHours(8);
        foreach (var species in Catalog.AllSpecies)
        {
            var starter = Catalog.BreedsOf(species.Species).Single(breed => breed.Unlock == BreedUnlock.Free);
            var pet = sim.Adopt("Pip", PetAppearance.Default(Catalog, starter.Breed), Start);
            var name = species.Species.ToString();
            Equal(species.Meters.Count + 1, pet.Meters.Count, $"{name} has its meters plus Health");
            foreach (var meter in species.Meters)
                Equal(meter.Initial, pet.Meter(meter.Meter), $"{name} {meter.Meter} starts at its initial value");
            Equal(100.0, pet.Health, $"{name} starts healthy");
            Equal(0.0, pet.Bond, $"{name} starts as an acquaintance");
            Equal((double)starter.LivelyBias, pet.Liveliness, $"{name} liveliness starts from the breed bias");
            Equal((double)starter.BoldBias, pet.Boldness, $"{name} boldness starts from the breed bias");
            Equal(PetStage.Young, pet.Stage, $"{name} starts young");

            var backwards = sim.Advance(pet, Start.AddHours(-5));
            Equal(pet, backwards.State, $"{name} ignores a backwards clock");
            Equal(false, backwards.BecameGrown, $"{name} backwards clock unlocks nothing");
            Equal(pet, sim.Advance(pet, Start.ToOffset(TimeSpan.FromHours(9))).State, $"{name} same instant in a new timezone is a no-op");

            var almost = sim.Advance(pet, Start.Add(species.GrownAge).AddTicks(-1));
            Equal(false, almost.BecameGrown, $"{name} waits for its Grown day");
            var grown = sim.Advance(almost.State, Start.Add(species.GrownAge));
            Equal(true, grown.BecameGrown, $"{name} grows up on its day");
            Equal(PetStage.Grown, grown.State.Stage, $"{name} is grown");
            Equal(false, sim.Advance(grown.State, Start.Add(species.GrownAge).AddDays(3)).BecameGrown, $"{name} grows up only once");
            Equal(false, sim.Advance(grown.State, Start).BecameGrown, $"{name} backwards clock after growing");

            var capped = sim.Advance(pet, Start.AddDays(30)).State;
            var atCap = sim.Advance(pet, Start.AddHours(8)).State;
            Equal(TimeSpan.FromDays(30), capped.Age, $"{name} age is not capped");
            foreach (var meter in species.Meters)
            {
                Equal(atCap.Meter(meter.Meter), capped.Meter(meter.Meter), $"{name} {meter.Meter} decay is capped at 8 hours");
                Close(Math.Max(0, meter.Initial - 8 * meter.DecayPerHour * starter.DecayFactor(meter.Meter)), capped.Meter(meter.Meter),
                    $"{name} {meter.Meter} decays at its rate");
            }
            Equal(true, capped.Meter(species.Meters[0].Meter) < pet.Meter(species.Meters[0].Meter), $"{name} needs fall while away");

            var neglected = pet;
            for (var day = 1; day <= 40; day++)
                neglected = sim.Advance(neglected, Start.AddDays(day)).State;
            Equal(PetSimulation.HealthFloor, neglected.Health, $"{name} Health rests at the floor; nothing dies");
            Equal(pet.Meters.Count, neglected.Meters.Count, $"{name} keeps every meter");
            Equal(pet.Name, neglected.Name, $"{name} stays in the household");
            foreach (var meter in species.Meters)
            {
                var cared = neglected;
                foreach (var verb in species.Verbs.Where(verb => verb.Effects.Any(effect => effect.Meter == meter.Meter && effect.Delta > 0)))
                    cared = sim.ApplyCare(cared, verb.Verb).State;
                Equal(true, cared.Meter(meter.Meter) > neglected.Meter(meter.Meter), $"{name} {meter.Meter} recovers with care");
            }
        }

        // Domestic shorthair: Energy drifts 10% faster and Comfort 15% slower, the same need per hour.
        var cat = sim.Adopt("Pip", PetAppearance.Default(Catalog, PetBreed.DomesticShorthair), Start);
        var twoHours = sim.Advance(cat, Start.AddHours(2)).State;
        Close(56, twoHours.Meter(PetMeter.Fullness), "cat fullness decays 7 per hour");
        Close(80 - 2 * 6 * 1.10, twoHours.Meter(PetMeter.Energy), "breed tendency speeds energy");
        Close(70 - 2 * 4 * 0.85, twoHours.Meter(PetMeter.Comfort), "breed tendency slows comfort");
        Close(84, twoHours.Meter(PetMeter.Cleanliness), "cat cleanliness decays slowly");
        Equal(Start.AddHours(2), twoHours.TimeCheckpoint, "advance moves the checkpoint");

        var preview = sim.AdvancePreview(cat, TimeSpan.FromDays(7));
        Equal(true, preview.BecameGrown, "preview can grow a pet");
        Equal(Start, preview.State.TimeCheckpoint, "preview leaves the real clock alone");
        ExpectThrows<ArgumentOutOfRangeException>(() => sim.AdvancePreview(cat, TimeSpan.Zero), "preview needs time");
        ExpectThrows<ArgumentOutOfRangeException>(() => new PetSimulation(Catalog, TimeSpan.Zero), "decay cap must be positive");
        Equal(TimeSpan.FromHours(8), PetSimulation.DefaultMaxNeedDecay, "care doc's 8-hour decay cap");

        var ancient = PetState.Restore(Catalog, "Pip", cat.Appearance, Start, PetState.MaxAge, Meters(Species.Cat, 50, 100), 0, 0, 0, 0, true);
        Equal(PetState.MaxAge, sim.Advance(ancient, Start.AddHours(1)).State.Age, "age saturates at the maximum instead of overflowing");
        Equal(PetState.MaxAge, sim.AdvancePreview(cat, TimeSpan.MaxValue).State.Age, "a huge preview saturates too");
        ExpectThrows<ArgumentOutOfRangeException>(() => PetState.Restore(Catalog, "Pip", cat.Appearance, Start, PetState.MaxAge.Add(TimeSpan.FromTicks(1)),
            Meters(Species.Cat, 50, 100), 0, 0, 0, 0, true), "an age beyond any clock is refused");

        var emoji = "Mochi \U0001F431";
        Equal(emoji, PetSaveCodec.Decode(PetSaveCodec.Encode(sim.Adopt(emoji, cat.Appearance, Start))).Name, "an emoji name saves");
        foreach (var bad in new[] { "Mo\u0001chi", "Mochi\nCo", emoji.Substring(0, emoji.Length - 1), "\uDC31Mochi", "Mochi\uFFFE" })
        {
            ExpectThrows<ArgumentException>(() => sim.Adopt(bad, cat.Appearance, Start), $"adopt refuses unsavable name {Escape(bad)}");
            ExpectThrows<ArgumentException>(() => PetState.Restore(Catalog, bad, cat.Appearance, Start, TimeSpan.Zero,
                Meters(Species.Cat, 50, 100), 0, 0, 0, 0, false), $"restore refuses unsavable name {Escape(bad)}");
        }

        var wellKept = PetState.Restore(Catalog, "Pip", cat.Appearance, Start, TimeSpan.Zero, Meters(Species.Cat, 100, 50), 0, 0, 0, 0, false);
        Close(56, sim.Advance(wellKept, Start.AddHours(1)).State.Health, "Health recovers while needs are met");

        var young = sim.ApplyCare(cat, PetVerb.Wand).State;
        Equal(cat.Liveliness + 2, young.Liveliness, "play makes a young cat livelier");
        var grownCat = preview.State;
        Equal(grownCat.Liveliness, sim.ApplyCare(grownCat, PetVerb.Wand).State.Liveliness, "temperament settles once grown");
    }

    private static void CareVerbs()
    {
        var sim = new PetSimulation();
        foreach (var species in Catalog.AllSpecies)
        {
            var starter = Catalog.BreedsOf(species.Species).Single(breed => breed.Unlock == BreedUnlock.Free);
            var middle = PetState.Restore(Catalog, "Pip", PetAppearance.Default(Catalog, starter.Breed), Start, TimeSpan.FromDays(1),
                Meters(species.Species, 50, 80), 10, 10, 0, 0, false);
            var seen = new HashSet<string>();
            foreach (var verb in species.Verbs)
            {
                var (cared, cue) = sim.ApplyCare(middle, verb.Verb);
                var name = $"{species.Species} {verb.Verb}";
                Equal(PetCue.Content, cue, $"{name} cue");
                Equal(middle.Age, cared.Age, $"{name} leaves age alone");
                Equal(middle.TimeCheckpoint, cared.TimeCheckpoint, $"{name} leaves the clock alone");
                Equal(middle.Health, cared.Health, $"{name} leaves Health to time");
                var change = string.Join(",", species.Meters.Select(meter => cared.Meter(meter.Meter) - middle.Meter(meter.Meter)))
                    + $"|{cared.Bond - middle.Bond}";
                Equal(true, change != string.Join(",", species.Meters.Select(meter => 0.0)) + "|0", $"{name} changes something");
                Equal(true, seen.Add(change), $"{name} has its own effect");
                Equal(true, sim.CanUse(middle, verb.Verb), $"{name} is available");
            }

            foreach (PetVerb verb in Enum.GetValues(typeof(PetVerb)))
            {
                if (species.FindVerb(verb) != null)
                    continue;

                Equal(false, sim.CanUse(middle, verb), $"{species.Species} cannot {verb}");
                ExpectThrows<InvalidOperationException>(() => sim.ApplyCare(middle, verb), $"{species.Species} rejects {verb}");
            }
        }

        var cat = sim.Adopt("Pip", PetAppearance.Default(Catalog, PetBreed.DomesticShorthair), Start);
        Equal(100.0, sim.ApplyCare(cat, PetVerb.Feed).State.Meter(PetMeter.Fullness), "cat feed fills Fullness to the cap");
        var petted = sim.ApplyCare(cat, PetVerb.Pet).State;
        Equal(90.0, petted.Meter(PetMeter.Comfort), "petting comforts the cat");
        Equal(3.0, petted.Bond, "petting builds Bond");
        Equal(68.0, sim.ApplyCare(cat, PetVerb.Wand).State.Meter(PetMeter.Energy), "wand play tires the cat");
        Equal(100.0, sim.ApplyCare(cat, PetVerb.Brush).State.Meter(PetMeter.Cleanliness), "brushing grooms the cat");

        var dog = sim.Adopt("Rex", PetAppearance.Default(Catalog, PetBreed.MixedBreed), Start);
        var walked = sim.ApplyCare(dog, PetVerb.Walk).State;
        Equal(100.0, walked.Meter(PetMeter.Exercise), "walk exercises the dog");
        Equal(70.0, walked.Meter(PetMeter.Energy), "walk tires the dog");
        Equal(85.0, walked.Meter(PetMeter.Cleanliness), "walk gets the dog a little muddy");
        Equal(true, sim.ApplyCare(dog, PetVerb.Fetch).State.Bond > walked.Bond, "fetch builds more Bond than a walk");

        var hamster = sim.Adopt("Nib", PetAppearance.Default(Catalog, PetBreed.SyrianGolden), Start);
        var wheel = sim.ApplyCare(hamster, PetVerb.Wheel).State;
        Equal(100.0, wheel.Meter(PetMeter.Exercise), "wheel exercises the hamster");
        Equal(70.0, wheel.Meter(PetMeter.Energy), "wheel tires the hamster");
        var held = sim.ApplyCare(hamster, PetVerb.GentleHold).State;
        Equal(3.0, held.Bond, "a gentle hold builds trust");
        Equal(hamster.Meter(PetMeter.Fullness), held.Meter(PetMeter.Fullness), "holding changes no need");

        var rabbit = sim.Adopt("Clover", PetAppearance.Default(Catalog, PetBreed.Dutch), Start);
        Equal(true, sim.ApplyCare(rabbit, PetVerb.FloorTime).State.Bond > sim.ApplyCare(rabbit, PetVerb.Groom).State.Bond,
            "floor time builds Bond fastest");
        Equal(100.0, sim.ApplyCare(rabbit, PetVerb.Litter).State.Meter(PetMeter.Litter), "litter change is complete");

        var fish = sim.Adopt("Bubbles", PetAppearance.Default(Catalog, PetBreed.Goldfish), Start);
        var murky = sim.Advance(fish, Start.AddHours(8)).State;
        Equal(100.0, sim.ApplyCare(murky, PetVerb.WaterChange).State.Meter(PetMeter.Water), "water change clears the tank");
        Equal(murky.Meter(PetMeter.Water) + 15, sim.ApplyCare(murky, PetVerb.WipeGlass).State.Meter(PetMeter.Water), "wiping helps a little");

        var friend = hamster;
        for (var index = 0; index < 40; index++)
            friend = sim.ApplyCare(friend, PetVerb.GentleHold).State;
        Equal(100.0, friend.Bond, "Bond is capped at 100");
        Equal(100.0, friend.HighestBond, "Bond high-water mark follows");
        Equal(BondTier.BestFriends, friend.BondTier, "best friends");
        Equal(BondTier.Trusting, PetState.Restore(Catalog, "Pip", cat.Appearance, Start, TimeSpan.Zero, Meters(Species.Cat, 50, 100), 40, 40, 0, 0, false).BondTier,
            "trusting at Bond 40");
        Equal(BondTier.Acquainted, cat.BondTier, "acquainted at first");

        var tired = PetState.Restore(Catalog, "Pip", cat.Appearance, Start, TimeSpan.Zero, WithMeter(Meters(Species.Cat, 50, 100), PetMeter.Energy, 5),
            0, 0, 0, 0, false);
        Equal(0.0, sim.ApplyCare(tired, PetVerb.Wand).State.Meter(PetMeter.Energy), "energy never falls below 0");
    }

    private static void FishOverfeeding()
    {
        var sim = new PetSimulation();
        var look = PetAppearance.Default(Catalog, PetBreed.Goldfish);
        var hungry = PetState.Restore(Catalog, "Bubbles", look, Start, TimeSpan.Zero,
            WithMeter(Meters(Species.Fish, 80, 100), PetMeter.Fullness, 89), 5, 5, 0, 0, false);
        var fed = sim.ApplyCare(hungry, PetVerb.SprinkleFood);
        Equal(PetCue.Content, fed.Cue, "a hungry fish eats");
        Equal(100.0, fed.State.Meter(PetMeter.Fullness), "flakes fill Fullness");
        Equal(80.0, fed.State.Meter(PetMeter.Water), "eaten flakes keep the water clear");
        Equal(6.0, fed.State.Bond, "feeding builds Bond");

        var extra = sim.ApplyCare(fed.State, PetVerb.SprinkleFood);
        Equal(PetCue.IgnoredFlakes, extra.Cue, "a full fish ignores flakes");
        Equal(100.0, extra.State.Meter(PetMeter.Fullness), "ignored flakes add nothing");
        Equal(80.0 - PetSimulation.CloudedWater, extra.State.Meter(PetMeter.Water), "ignored flakes cloud the water slightly");
        Equal(fed.State.Bond, extra.State.Bond, "overfeeding earns no Bond");
        Equal(fed.State.Meter(PetMeter.Calm), extra.State.Meter(PetMeter.Calm), "overfeeding leaves Calm alone");
        Equal(100.0, extra.State.Health, "overfeeding never harms Health");

        var atThreshold = PetState.Restore(Catalog, "Bubbles", look, Start, TimeSpan.Zero,
            WithMeter(WithMeter(Meters(Species.Fish, 80, 100), PetMeter.Fullness, 90), PetMeter.Water, 3), 0, 0, 0, 0, false);
        var cloudy = sim.ApplyCare(atThreshold, PetVerb.SprinkleFood);
        Equal(PetCue.IgnoredFlakes, cloudy.Cue, "Fullness 90 already counts as full");
        Equal(0.0, cloudy.State.Meter(PetMeter.Water), "clouding stops at 0");
        Equal(100.0, sim.ApplyCare(cloudy.State, PetVerb.WaterChange).State.Meter(PetMeter.Water), "a water change clears the haze");

        var fullCat = PetState.Restore(Catalog, "Pip", PetAppearance.Default(Catalog, PetBreed.DomesticShorthair), Start, TimeSpan.Zero,
            Meters(Species.Cat, 100, 100), 0, 0, 0, 0, false);
        Equal(PetCue.TooFull, sim.ApplyCare(fullCat, PetVerb.Feed).Cue, "the flake rule is fish-only; a full cat is gently overfed");
        Equal(100.0, sim.ApplyCare(fullCat, PetVerb.Feed).State.Meter(PetMeter.Fullness), "a full cat stays full");
    }

    private static void SaveRoundTrips()
    {
        var sim = new PetSimulation();
        foreach (var breed in Catalog.AllBreeds)
        {
            var species = Catalog.Get(breed.Species);
            var pet = sim.Adopt($"Pet {breed.Breed}", PetAppearance.Default(Catalog, breed.Breed)
                .WithAccessory(species.Accessories.Last()).WithAccessoryAccent(Outfit.Lilac), Start);
            foreach (var verb in species.Verbs)
                pet = sim.ApplyCare(pet, verb.Verb).State;
            pet = sim.Advance(pet, Start.AddHours(37).AddMinutes(13)).State;
            if (breed.Unlock == BreedUnlock.Premium)
                pet = pet.WithAppearance(pet.Appearance.WithSpecialCoat($"coat.{PetIds.Key(breed.Species)}.special_one"));

            var payload = PetSaveCodec.Encode(pet);
            var restored = PetSaveCodec.Decode(payload);
            var name = breed.Breed.ToString();
            Equal(payload, PetSaveCodec.Encode(restored), $"{name} save round-trips");
            Equal(pet.Name, restored.Name, $"{name} name");
            Equal(pet.Appearance, restored.Appearance, $"{name} appearance");
            Equal(pet.Species, restored.Species, $"{name} species");
            Equal(pet.TimeCheckpoint, restored.TimeCheckpoint, $"{name} checkpoint");
            Equal(pet.Age, restored.Age, $"{name} age");
            Equal(pet.Bond, restored.Bond, $"{name} bond");
            Equal(pet.HighestBond, restored.HighestBond, $"{name} bond high-water");
            Equal(pet.Liveliness, restored.Liveliness, $"{name} liveliness");
            Equal(pet.Boldness, restored.Boldness, $"{name} boldness");
            Equal(pet.IsGrown, restored.IsGrown, $"{name} stage");
            foreach (var pair in pet.Meters)
                Equal(pair.Value, restored.Meter(pair.Key), $"{name} {pair.Key}");
        }

        foreach (var species in Catalog.AllSpecies)
        {
            var starter = Catalog.BreedsOf(species.Species).First();
            var grown = sim.AdvancePreview(sim.Adopt("Old friend", PetAppearance.Default(Catalog, starter.Breed), Start), TimeSpan.FromDays(12)).State;
            var restored = PetSaveCodec.Decode(PetSaveCodec.Encode(grown));
            Equal(true, restored.IsGrown, $"{species.Species} Grown flag persists");
            Equal(false, sim.Advance(restored, Start.AddDays(1)).BecameGrown, $"{species.Species} reloaded pet does not grow twice");
        }

        var unlisted = PetSaveCodec.Decode(Golden.Replace("<breed>101</breed>", "<breed>107</breed>").Replace("coat=\"5\"", "coat=\"8\""));
        Equal(PetCoat.Blue, unlisted.Appearance.Coat, "a coat the breed no longer offers still decodes");
        Equal(PetCoat.Golden, Catalog.EffectiveCoat(unlisted.Appearance), "and renders with the breed default");
    }

    // A cat v1 record. Never edit this literal: it is what players' devices hold.
    private const string Golden =
        "<pet version=\"1\"><name>Mochi &amp; Co</name><species>0</species><breed>101</breed>" +
        "<appearance coat=\"5\" accessory=\"1\" accent=\"2\" special-coat=\"coat.cat.tortoiseshell\" />" +
        "<checkpoint>639028224000000000</checkpoint><age>3024000000000</age>" +
        "<meters><meter id=\"0\" value=\"56.5\" /><meter id=\"1\" value=\"66.2\" /><meter id=\"2\" value=\"63.2\" />" +
        "<meter id=\"3\" value=\"84\" /><meter id=\"4\" value=\"97\" /></meters>" +
        "<bond value=\"12\" highest=\"15\" /><personality lively=\"12\" bold=\"-3.5\" /><unlocks grown=\"false\" /></pet>";

    private static void GoldenPayload()
    {
        var pet = PetSaveCodec.Decode(Golden);
        Equal("Mochi & Co", pet.Name, "golden name");
        Equal(Species.Cat, pet.Species, "golden species");
        Equal(PetBreed.Tabby, pet.Breed, "golden breed");
        Equal(new PetAppearance(PetBreed.Tabby, PetCoat.Brown, "coat.cat.tortoiseshell", PetAccessory.Collar, Outfit.Sky), pet.Appearance, "golden look");
        Equal(Start, pet.TimeCheckpoint, "golden checkpoint");
        Equal(TimeSpan.FromDays(3.5), pet.Age, "golden age");
        Equal(56.5, pet.Meter(PetMeter.Fullness), "golden fullness");
        Equal(66.2, pet.Meter(PetMeter.Energy), "golden energy");
        Equal(63.2, pet.Meter(PetMeter.Comfort), "golden comfort");
        Equal(84.0, pet.Meter(PetMeter.Cleanliness), "golden cleanliness");
        Equal(97.0, pet.Health, "golden health");
        Equal(12.0, pet.Bond, "golden bond");
        Equal(15.0, pet.HighestBond, "golden bond high-water");
        Equal(12.0, pet.Liveliness, "golden liveliness");
        Equal(-3.5, pet.Boldness, "golden boldness");
        Equal(false, pet.IsGrown, "golden stage");
        Equal(true, PetSaveCodec.Encode(pet).StartsWith("<pet version=\"2\"", StringComparison.Ordinal),
            "a v1 cat re-encodes as v2 with its care records (slice 7)");
        Equal(PetSaveCodec.Encode(pet), PetSaveCodec.Encode(PetSaveCodec.Decode(PetSaveCodec.Encode(pet))),
            "the migrated cat round-trips");

        var built = PetState.Restore(Catalog, "Mochi & Co", pet.Appearance, Start.ToOffset(TimeSpan.FromHours(-5)), TimeSpan.FromDays(3.5),
            new Dictionary<PetMeter, double>
            {
                [PetMeter.Fullness] = 56.5, [PetMeter.Energy] = 66.2, [PetMeter.Comfort] = 63.2,
                [PetMeter.Cleanliness] = 84, [PetMeter.Health] = 97,
            },
            12, 15, 12, -3.5, false);
        Equal(PetSaveCodec.Encode(pet), PetSaveCodec.Encode(built), "the encoder writes what the golden payload decodes to");
    }

    private static void InvalidPayloads()
    {
        ExpectInvalid(Golden.Replace("<species>0</species>", "<species>1</species>"), "species and breed mismatch");
        ExpectInvalid(Golden.Replace("<species>0</species>", "<species>9</species>"), "undefined species");
        ExpectInvalid(Golden.Replace("<breed>101</breed>", "<breed>199</breed>"), "undefined breed in the cat block");
        ExpectInvalid(Golden.Replace("<breed>101</breed>", "<breed>900</breed>"), "breed outside every block");
        ExpectInvalid(Golden.Replace("<breed>101</breed>", "<breed>-1</breed>"), "negative breed");
        ExpectInvalid(Golden.Replace("<breed>101</breed>", "<breed>Tabby</breed>"), "breed by name");
        ExpectInvalid(Golden.Replace("coat=\"5\"", "coat=\"99\""), "undefined coat");
        ExpectInvalid(Golden.Replace("accessory=\"1\"", "accessory=\"99\""), "undefined accessory");
        ExpectInvalid(Golden.Replace("accent=\"2\"", "accent=\"99\""), "undefined accent");
        ExpectInvalid(Golden.Replace("coat.cat.tortoiseshell", "coat.dog.merle"), "special coat for another species");
        ExpectInvalid(Golden.Replace(" accessory=\"1\"", string.Empty), "missing accessory");
        ExpectInvalid(Golden.Replace("<meter id=\"3\" value=\"84\" />", string.Empty), "missing meter");
        ExpectInvalid(Golden.Replace("<meter id=\"3\" value=\"84\" />", "<meter id=\"3\" value=\"84\" /><meter id=\"7\" value=\"84\" />"),
            "meter the species does not have");
        ExpectInvalid(Golden.Replace("<meter id=\"3\" value=\"84\" />", "<meter id=\"3\" value=\"84\" /><meter id=\"42\" value=\"84\" />"),
            "undefined meter");
        ExpectInvalid(Golden.Replace("<meter id=\"3\" value=\"84\" />", "<meter id=\"3\" value=\"84\" /><meter id=\"3\" value=\"84\" />"),
            "duplicate meter");
        ExpectInvalid(Golden.Replace("value=\"84\"", "value=\"101\""), "meter above 100");
        ExpectInvalid(Golden.Replace("value=\"84\"", "value=\"NaN\""), "meter not a number");
        ExpectInvalid(Golden.Replace("value=\"84\"", "value=\"-1\""), "meter below 0");
        ExpectInvalid(Golden.Replace("<bond value=\"12\"", "<bond value=\"120\""), "bond above 100");
        ExpectInvalid(Golden.Replace("highest=\"15\"", "highest=\"11\""), "bond above its high-water mark");
        ExpectInvalid(Golden.Replace("lively=\"12\"", "lively=\"150\""), "personality out of range");
        ExpectInvalid(Golden.Replace("<unlocks grown=\"false\" />", string.Empty), "missing unlock flags");
        ExpectInvalid(Golden.Replace("grown=\"false\"", "grown=\"maybe\""), "unreadable unlock flag");
        ExpectInvalid(Golden.Replace("<age>3024000000000</age>", "<age>-1</age>"), "negative age");
        ExpectInvalid(Golden.Replace("<age>3024000000000</age>", "<age>9223372036854775807</age>"), "age beyond any clock");
        ExpectInvalid(Golden.Replace("<age>3024000000000</age>", "<age>9223372036854775808</age>"), "age beyond a long");
        ExpectInvalid(Golden.Replace("<name>Mochi &amp; Co</name>", "<name>Mo&#x1;chi</name>"), "control character in the name");
        ExpectInvalid(Golden.Replace("<name>Mochi &amp; Co</name>", "<name> </name>"), "blank name");
        ExpectInvalid(Golden.Replace("<pet version=\"1\">", "<baby version=\"1\">").Replace("</pet>", "</baby>"), "baby record in the pet codec");
        ExpectInvalid(Golden.Replace(" version=\"1\"", string.Empty), "missing version");
        ExpectInvalid("<pet", "malformed XML");
        ExpectThrows<NotSupportedException>(() => PetSaveCodec.Decode(Golden.Replace("version=\"1\"", "version=\"3\"")), "future pet version");
        ExpectThrows<NotSupportedException>(() => PetSaveCodec.Decode(Golden.Replace("version=\"1\"", "version=\"0\"")), "pet version 0");
    }

    private static Dictionary<PetMeter, double> Meters(Species species, double value, double health)
    {
        var meters = Catalog.Get(species).Meters.ToDictionary(meter => meter.Meter, meter => value);
        meters[PetMeter.Health] = health;
        return meters;
    }

    private static Dictionary<PetMeter, double> WithMeter(Dictionary<PetMeter, double> meters, PetMeter meter, double value)
    {
        meters[meter] = value;
        return meters;
    }

    private static SpeciesGoalProgress ProgressFor(EarnedGoal goal)
    {
        switch (goal)
        {
            case EarnedGoal.FirstFullCareDay: return new SpeciesGoalProgress(1, 0, false, 0);
            case EarnedGoal.Bond40: return new SpeciesGoalProgress(0, 40, false, 0);
            case EarnedGoal.RaisedToGrown: return new SpeciesGoalProgress(0, 0, true, 0);
            case EarnedGoal.SevenOnTimeCareDays: return new SpeciesGoalProgress(0, 0, false, 7);
            default: throw new ArgumentOutOfRangeException(nameof(goal));
        }
    }

    private static void ExpectMeters(Species species, params PetMeter[] meters) =>
        Equal(string.Join(",", meters), string.Join(",", Catalog.Get(species).Meters.Select(meter => meter.Meter)), $"{species} meters");

    private static void ExpectVerbs(Species species, params PetVerb[] verbs) =>
        Equal(string.Join(",", verbs), string.Join(",", Catalog.Get(species).Verbs.Select(verb => verb.Verb)), $"{species} care verbs");

    private static PetCatalog WithBreeds(IEnumerable<BreedDefinition> breeds) => new PetCatalog(Catalog.AllSpecies, breeds);

    private static PetCatalog ReplaceBreed(BreedDefinition replacement) =>
        WithBreeds(Catalog.AllBreeds.Select(breed => breed.Breed == replacement.Breed ? replacement : breed));

    private static PetCatalog ReplacePack(BreedPackDefinition replacement) =>
        new PetCatalog(Catalog.AllSpecies, Catalog.AllBreeds,
            Catalog.AllPacks.Select(pack => pack.Species == replacement.Species ? replacement : pack));

    private static string Escape(string value) =>
        string.Concat(value.Select(character => character < 0x20 || character > 0x7E ? $"\\u{(int)character:X4}" : character.ToString()));

    private static PetCatalog ReplaceSpecies(SpeciesDefinition replacement) =>
        new PetCatalog(Catalog.AllSpecies.Select(species => species.Species == replacement.Species ? replacement : species), Catalog.AllBreeds);

    private const string Keep = "\u0000keep";

    private static BreedDefinition Clone(BreedDefinition breed, Species? species = null, EarnedGoal? goal = null, bool clearGoal = false,
        string productId = Keep, BreedArtTier? art = null, bool? available = null, BreedTendency[] tendencies = null, PetCoat[] coats = null) =>
        new BreedDefinition(breed.Breed, species ?? breed.Species, breed.Unlock, clearGoal ? null : goal ?? breed.Goal,
            productId == Keep ? breed.ProductId : productId, art ?? breed.Art, available ?? breed.AvailableNow,
            coats ?? breed.Coats.ToArray(), tendencies ?? breed.Tendencies.ToArray(), breed.LivelyBias, breed.BoldBias);

    private static BreedTendency[] Trade(PetMeter faster, PetMeter slower, int percent) =>
        new[] { new BreedTendency(faster, percent), new BreedTendency(slower, -percent) };

    private static void ExpectProblem(PetCatalog catalog, string fragment, string description)
    {
        var problems = catalog.Problems();
        if (!problems.Any(problem => problem.Contains(fragment)))
            throw new InvalidOperationException($"{description}: expected a problem containing '{fragment}', got: {string.Join(" / ", problems)}");
        ExpectThrows<InvalidOperationException>(catalog.Validate, $"{description} fails validation");
    }

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

        throw new InvalidOperationException($"{description}: the pet save should be rejected.");
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

        throw new InvalidOperationException($"{description}: expected {typeof(TException).Name}.");
    }

    private static void Close(double expected, double actual, string description)
    {
        if (Math.Abs(expected - actual) > 1e-9)
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }
}
