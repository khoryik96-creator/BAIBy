using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using BAIby.Character;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Tests.Appearance
{
    // The cat, hamster, dog, fish and rabbit layouts, palettes and compositing (tools/pets/README.md), checked against the
    // shipped art.
    internal static class PetArtChecks
    {
        private static readonly string CatFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "unity", "Assets", "Resources", "BAIby", "Pets", "Cat"));
        private static readonly string HamsterFolder = Path.GetFullPath(Path.Combine(CatFolder, "..", "Hamster"));
        private static readonly string DogFolder = Path.GetFullPath(Path.Combine(CatFolder, "..", "Dog"));
        private static readonly string FishFolder = Path.GetFullPath(Path.Combine(CatFolder, "..", "Fish"));
        private static readonly string RabbitFolder = Path.GetFullPath(Path.Combine(CatFolder, "..", "Rabbit"));

        public static void Run(Action<bool, string> check)
        {
            var layout = LoadLayout();
            LayoutCoversEveryCatBreed(layout, check);
            PaletteFollowsTheContract(layout, check);
            ComposesEveryPoseFromTheShippedArt(layout, check);
            CalicoDarkPatchesKeepTheirColor(layout, check);
            InvalidLayoutsAreRejected(check);
            Hamster(LoadLayout(HamsterFolder), check);
            Dog(LoadLayout(DogFolder), check);
            Fish(LoadLayout(FishFolder), check);
            Rabbit(LoadLayout(RabbitFolder), check);
        }

        // P5: every rabbit breed ships; each composes from the shipped art in every pose with its ears (its markings
        // overlay) and a collar, the Flemish giant on its own larger base, and the Harlequin's half face and bands keep
        // their own dark color. The pen is a habitat the rabbit stands in, its haze slot the scattered hay.
        private static void Rabbit(PetLayout layout, Action<bool, string> check)
        {
            check(layout.YoungScale > 0 && layout.YoungScale < layout.GrownScale, "a young rabbit is drawn smaller than a grown one");
            var pen = layout.Habitat;
            check(pen != null && pen.Swim == 0, "the rabbit stands in its pen");
            check(pen?.Haze != null && pen.Castle == null, "the pen has its scattered hay and no castle");
            if (pen != null)
            {
                foreach (var path in new[] { pen.Back, pen.Front, pen.Haze })
                    check(Png.Read(Path.Combine(RabbitFolder, path + ".bytes"), pen.Width, pen.Height).Length > 0,
                        "the pen layer " + path + " is the declared size");
            }

            check(layout.HasOwnBase(PetBreed.FlemishGiant), "the Flemish giant has its own base");
            check(!layout.HasOwnBase(PetBreed.Dutch), "other rabbits share the base");
            layout.TryGetPoseSize("GrownAwake", out var plainWidth, out var plainHeight);
            layout.TryGetPoseSize("FlemishGiantGrownAwake", out var giantWidth, out var giantHeight);
            check(giantWidth > plainWidth && giantHeight > plainHeight, "the Flemish giant is drawn larger");
            var composer = new PetComposer(layout);
            var loads = new List<string>();
            PetArtSource source = (path, width, height) =>
            {
                loads.Add(path);
                return Png.Read(Path.Combine(RabbitFolder, path + ".bytes"), width, height);
            };
            foreach (var breed in PetCatalog.Default.BreedsOf(Species.Rabbit))
            {
                check(breed.AvailableNow, breed.Breed + " ships with the rabbit");
                foreach (var coat in breed.Coats)
                    check(layout.TryGetCoat(breed.Breed, coat, out _), "the rabbit palette lists " + breed.Breed + " " + coat);
                foreach (PetStage stage in Enum.GetValues(typeof(PetStage)))
                {
                    foreach (var resting in new[] { false, true })
                    {
                        var pose = PetLayout.PoseName(stage, resting);
                        var shown = layout.ShownPose(breed.Breed, pose);
                        check(layout.TryGetOverlay(PetLayout.MarkingsPath(breed.Breed, pose), out var ears) && ears.Pose == shown,
                            breed.Breed + " " + pose + " has its ears over its own base");
                        loads.Clear();
                        var pixels = composer.Compose(breed.Breed, breed.DefaultCoat, PetAccessory.Collar, Outfit.Sky, pose, source,
                            PixelRowOrder.TopDown, out var width, out var height);
                        var visible = 0;
                        for (var i = 3; i < width * height * 4; i += 4)
                            if (pixels[i] > 0)
                                visible++;
                        check(visible > width * height / 5, breed.Breed + " " + pose + " composes a visible rabbit");
                        var marked = breed.Breed == PetBreed.Harlequin ? 4 : 2;
                        check(loads.Count == 4 + marked, breed.Breed + " " + pose + " loads base, ears, markings and collar (" +
                            string.Join(", ", loads) + ")");
                        check(loads[0] == PetLayout.BasePath(shown), breed.Breed + " " + pose + " sits on " + shown);
                        check(loads.Contains(PetLayout.AccessoryPath(PetAccessory.Collar, shown)), breed.Breed + " " + pose + " wears its collar");
                    }
                }
            }

            var harlequin = (byte[])composer.Compose(PetBreed.Harlequin, PetCoat.Tricolor, PetAccessory.None, Outfit.Sage, "YoungAwake",
                source, PixelRowOrder.TopDown, out var w, out _).Clone();
            layout.TryGetOverlay(PetLayout.DarkMarkingsPath(PetBreed.Harlequin, "YoungAwake"), out var bands);
            var bandPixels = Png.Read(Path.Combine(RabbitFolder, bands.Path + ".bytes"), bands.Width, bands.Height);
            var bandRegions = Png.Read(Path.Combine(RabbitFolder, bands.Path + "Regions.bytes"), bands.Width, bands.Height);
            AppearanceRecolor.Recolor(bandPixels, bandRegions, bands.Width * bands.Height, layout.Reference,
                PetPalette.DarkTargets(layout, PetBreed.Harlequin, PetCoat.Tricolor, Outfit.Sage));
            var matched = 0;
            var opaque = 0;
            for (var y = 0; y < bands.Height; y++)
            {
                for (var x = 0; x < bands.Width; x++)
                {
                    var at = (y * bands.Width + x) * 4;
                    if (bandPixels[at + 3] != 255)
                        continue;
                    opaque++;
                    var target = ((bands.YFromTop + y) * w + bands.X + x) * 4;
                    if (harlequin[target] == bandPixels[at] && harlequin[target + 1] == bandPixels[at + 1] &&
                        harlequin[target + 2] == bandPixels[at + 2])
                        matched++;
                }
            }

            check(opaque > 0 && matched > opaque * 9 / 10, "the Harlequin's bands keep their own recolor (" + matched + "/" + opaque + ")");
            var black = PetPalette.Targets(layout, PetBreed.Dutch, PetCoat.Black, Outfit.Sage);
            check(black.Region(1).R > 240, "a Dutch rabbit's markings are white");
        }

        // P4: every fish breed ships, each composes from the shipped art in every pose with its fins (its markings
        // overlay), the Neon tetra school on its own base, and the Koi's patches keep their own dark color. The tank is a
        // habitat the fish swims in, with its haze and castle layers; the tank castle has no pose art.
        private static void Fish(PetLayout layout, Action<bool, string> check)
        {
            check(layout.YoungScale > 0 && layout.YoungScale < layout.GrownScale, "a young fish is drawn smaller than a grown one");
            var tank = layout.Habitat;
            check(tank != null && tank.Swim > 0 && tank.Swim < tank.Height - tank.Floor, "the fish swims in its tank");
            check(LoadLayout(HamsterFolder).Habitat.Swim == 0, "a hamster stands on its cage floor");
            check(tank?.Haze != null && tank.Castle != null, "the tank has its haze and castle layers");
            if (tank != null)
            {
                foreach (var path in new[] { tank.Back, tank.Front, tank.Haze, tank.Castle })
                    check(Png.Read(Path.Combine(FishFolder, path + ".bytes"), tank.Width, tank.Height).Length > 0,
                        "the tank layer " + path + " is the declared size");
            }

            check(PetLayout.AccessoryPath(PetAccessory.TankCastle, "YoungAwake") == null, "the tank castle is no pose art");
            check(layout.HasOwnBase(PetBreed.NeonTetraSchool), "the Neon tetra school has its own base");
            check(!layout.HasOwnBase(PetBreed.Goldfish), "other fish share the base");
            var dark = new[] { PetBreed.NeonTetraSchool, PetBreed.Angelfish, PetBreed.Koi, PetBreed.Clownfish };
            var composer = new PetComposer(layout);
            var loads = new List<string>();
            PetArtSource source = (path, width, height) =>
            {
                loads.Add(path);
                return Png.Read(Path.Combine(FishFolder, path + ".bytes"), width, height);
            };
            foreach (var breed in PetCatalog.Default.BreedsOf(Species.Fish))
            {
                check(breed.AvailableNow, breed.Breed + " ships with the fish");
                foreach (var coat in breed.Coats)
                    check(layout.TryGetCoat(breed.Breed, coat, out _), "the fish palette lists " + breed.Breed + " " + coat);
                foreach (PetStage stage in Enum.GetValues(typeof(PetStage)))
                {
                    foreach (var resting in new[] { false, true })
                    {
                        var pose = PetLayout.PoseName(stage, resting);
                        var shown = layout.ShownPose(breed.Breed, pose);
                        check(layout.TryGetOverlay(PetLayout.MarkingsPath(breed.Breed, pose), out var fins) && fins.Pose == shown,
                            breed.Breed + " " + pose + " has its fins over its own base");
                        loads.Clear();
                        var pixels = composer.Compose(breed.Breed, breed.DefaultCoat, PetAccessory.TankCastle, Outfit.Sky, pose, source,
                            PixelRowOrder.TopDown, out var width, out var height);
                        var visible = 0;
                        for (var i = 3; i < width * height * 4; i += 4)
                            if (pixels[i] > 0)
                                visible++;
                        check(visible > width * height / 5, breed.Breed + " " + pose + " composes a visible fish");
                        var marked = Array.IndexOf(dark, breed.Breed) >= 0 ? 4 : 2;
                        check(loads.Count == 2 + marked, breed.Breed + " " + pose + " loads base, fins and markings, no wear (" +
                            string.Join(", ", loads) + ")");
                        check(loads[0] == PetLayout.BasePath(shown), breed.Breed + " " + pose + " swims on " + shown);
                    }
                }
            }

            var koi = (byte[])composer.Compose(PetBreed.Koi, PetCoat.White, PetAccessory.None, Outfit.Sage, "GrownAwake",
                source, PixelRowOrder.TopDown, out var w, out _).Clone();
            layout.TryGetOverlay(PetLayout.DarkMarkingsPath(PetBreed.Koi, "GrownAwake"), out var patches);
            var patchPixels = Png.Read(Path.Combine(FishFolder, patches.Path + ".bytes"), patches.Width, patches.Height);
            var patchRegions = Png.Read(Path.Combine(FishFolder, patches.Path + "Regions.bytes"), patches.Width, patches.Height);
            AppearanceRecolor.Recolor(patchPixels, patchRegions, patches.Width * patches.Height, layout.Reference,
                PetPalette.DarkTargets(layout, PetBreed.Koi, PetCoat.White, Outfit.Sage));
            var matched = 0;
            var opaque = 0;
            for (var y = 0; y < patches.Height; y++)
            {
                for (var x = 0; x < patches.Width; x++)
                {
                    var at = (y * patches.Width + x) * 4;
                    if (patchPixels[at + 3] != 255)
                        continue;
                    opaque++;
                    var target = ((patches.YFromTop + y) * w + patches.X + x) * 4;
                    if (koi[target] == patchPixels[at] && koi[target + 1] == patchPixels[at + 1] && koi[target + 2] == patchPixels[at + 2])
                        matched++;
                }
            }

            check(opaque > 0 && matched > opaque * 9 / 10, "the Koi's patches keep their own recolor (" + matched + "/" + opaque + ")");
            var orange = PetPalette.Targets(layout, PetBreed.Goldfish, PetCoat.Orange, Outfit.Sage);
            check(orange.Region(0).R > orange.Region(0).B, "the goldfish is orange");
        }

        // P3: every dog breed but the Dachshund ships; each composes from the shipped art in every pose with its ears
        // (its markings overlay) and a collar, the Corgi on its own short-legged base, and the Beagle's saddle and the
        // Dalmatian's spots keep their own dark color.
        private static void Dog(PetLayout layout, Action<bool, string> check)
        {
            check(layout.YoungScale > 0 && layout.YoungScale < layout.GrownScale, "a puppy is drawn smaller than a grown dog");
            check(layout.Habitat == null, "a dog lives in the room as it is");
            check(layout.HasOwnBase(PetBreed.Corgi), "the Corgi has its own base");
            check(!layout.HasOwnBase(PetBreed.MixedBreed) && !LoadLayout().HasOwnBase(PetBreed.Tabby), "other breeds share the base");
            check(layout.ShownPose(PetBreed.Corgi, "YoungAwake") == "CorgiYoungAwake", "the Corgi's pose");
            layout.TryGetPoseSize("YoungAwake", out _, out var plainHeight);
            layout.TryGetPoseSize("CorgiYoungAwake", out _, out var corgiHeight);
            check(corgiHeight < plainHeight, "the Corgi sits lower on its short legs");
            var dark = new[] { PetBreed.Beagle, PetBreed.Dalmatian };
            var composer = new PetComposer(layout);
            var loads = new List<string>();
            PetArtSource source = (path, width, height) =>
            {
                loads.Add(path);
                return Png.Read(Path.Combine(DogFolder, path + ".bytes"), width, height);
            };
            foreach (var breed in PetCatalog.Default.BreedsOf(Species.Dog))
            {
                var shipped = breed.Breed != PetBreed.Dachshund;
                check(breed.AvailableNow == shipped, breed.Breed + (shipped ? " ships with the dog" : " is arriving soon"));
                if (!shipped)
                {
                    check(!layout.TryGetOverlay(PetLayout.MarkingsPath(breed.Breed, "YoungAwake"), out _), "the Dachshund has no art yet");
                    continue;
                }

                foreach (var coat in breed.Coats)
                    check(layout.TryGetCoat(breed.Breed, coat, out _), "the dog palette lists " + breed.Breed + " " + coat);
                foreach (PetStage stage in Enum.GetValues(typeof(PetStage)))
                {
                    foreach (var resting in new[] { false, true })
                    {
                        var pose = PetLayout.PoseName(stage, resting);
                        var shown = layout.ShownPose(breed.Breed, pose);
                        check(layout.TryGetOverlay(PetLayout.MarkingsPath(breed.Breed, pose), out var ears) && ears.Pose == shown,
                            breed.Breed + " " + pose + " has its ears over its own base");
                        loads.Clear();
                        var pixels = composer.Compose(breed.Breed, breed.DefaultCoat, PetAccessory.Collar, Outfit.Sky, pose, source,
                            PixelRowOrder.TopDown, out var width, out var height);
                        var visible = 0;
                        for (var i = 3; i < width * height * 4; i += 4)
                            if (pixels[i] > 0)
                                visible++;
                        check(visible > width * height / 4, breed.Breed + " " + pose + " composes a visible dog");
                        var marked = Array.IndexOf(dark, breed.Breed) >= 0 ? 4 : 2;
                        check(loads.Count == 4 + marked, breed.Breed + " " + pose + " loads base, ears, markings and collar (" +
                            string.Join(", ", loads) + ")");
                        check(loads[0] == PetLayout.BasePath(shown), breed.Breed + " " + pose + " stands on " + shown);
                        check(loads.Contains(PetLayout.AccessoryPath(PetAccessory.Collar, shown)), breed.Breed + " " + pose + " wears its collar");
                    }
                }
            }

            var dalmatian = (byte[])composer.Compose(PetBreed.Dalmatian, PetCoat.White, PetAccessory.None, Outfit.Sage, "YoungAwake",
                source, PixelRowOrder.TopDown, out var w, out _).Clone();
            layout.TryGetOverlay(PetLayout.DarkMarkingsPath(PetBreed.Dalmatian, "YoungAwake"), out var spots);
            var spotPixels = Png.Read(Path.Combine(DogFolder, spots.Path + ".bytes"), spots.Width, spots.Height);
            var spotRegions = Png.Read(Path.Combine(DogFolder, spots.Path + "Regions.bytes"), spots.Width, spots.Height);
            AppearanceRecolor.Recolor(spotPixels, spotRegions, spots.Width * spots.Height, layout.Reference,
                PetPalette.DarkTargets(layout, PetBreed.Dalmatian, PetCoat.White, Outfit.Sage));
            var matched = 0;
            var opaque = 0;
            for (var y = 0; y < spots.Height; y++)
            {
                for (var x = 0; x < spots.Width; x++)
                {
                    var at = (y * spots.Width + x) * 4;
                    if (spotPixels[at + 3] != 255)
                        continue;
                    opaque++;
                    var target = ((spots.YFromTop + y) * w + spots.X + x) * 4;
                    if (dalmatian[target] == spotPixels[at] && dalmatian[target + 1] == spotPixels[at + 1] &&
                        dalmatian[target + 2] == spotPixels[at + 2])
                        matched++;
                }
            }

            check(opaque > 0 && matched > opaque * 9 / 10, "the Dalmatian's spots keep their own recolor (" + matched + "/" + opaque + ")");
            var tan = PetPalette.Targets(layout, PetBreed.MixedBreed, PetCoat.Tan, Outfit.Sage);
            check(tan.Region(0).Equals(layout.Reference.Region(0)), "the reference coat is the mixed breed's tan");
        }

        private static PetLayout LoadLayout(string folder = null)
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            var data = JsonSerializer.Deserialize<PetLayoutData>(File.ReadAllText(Path.Combine(folder ?? CatFolder, "layout.json")), options);
            return PetLayout.FromData(data);
        }

        // P2: every hamster breed ships (the Teddy bear's long coat included), each composes from the shipped art in
        // every pose with its tiny bow, and the dorsal stripe and Panda patches keep their own dark color.
        private static void Hamster(PetLayout layout, Action<bool, string> check)
        {
            check(layout.YoungScale > 0 && layout.YoungScale < layout.GrownScale, "a young hamster is drawn smaller than a grown one");
            check(layout.Habitat != null && layout.Habitat.Floor > 0 && layout.Habitat.Floor < layout.Habitat.Height,
                "the hamster lives in its habitat");
            check(LoadLayout().Habitat == null, "the cat has no habitat");
            foreach (var path in new[] { layout.Habitat.Back, layout.Habitat.Front })
                check(Png.Read(Path.Combine(HamsterFolder, path + ".bytes"), layout.Habitat.Width, layout.Habitat.Height).Length > 0,
                    "the habitat layer " + path + " is the declared size");
            var striped = new[] { PetBreed.WinterWhite, PetBreed.Sapphire, PetBreed.Campbells, PetBreed.Panda };
            foreach (var breed in PetCatalog.Default.BreedsOf(Species.Hamster))
            {
                check(breed.AvailableNow, breed.Breed + " ships with the hamster");
                foreach (var coat in breed.Coats)
                    check(layout.TryGetCoat(breed.Breed, coat, out _), "the hamster palette lists " + breed.Breed + " " + coat);
            }

            var composer = new PetComposer(layout);
            var loads = new List<string>();
            PetArtSource source = (path, width, height) =>
            {
                loads.Add(path);
                return Png.Read(Path.Combine(HamsterFolder, path + ".bytes"), width, height);
            };
            foreach (var breed in PetCatalog.Default.BreedsOf(Species.Hamster))
            {
                foreach (PetStage stage in Enum.GetValues(typeof(PetStage)))
                {
                    foreach (var resting in new[] { false, true })
                    {
                        var pose = PetLayout.PoseName(stage, resting);
                        check(layout.TryGetOverlay(PetLayout.AccessoryPath(PetAccessory.Bow, pose), out _), "the hamster's bow has a " + pose + " overlay");
                        loads.Clear();
                        var pixels = composer.Compose(breed.Breed, breed.DefaultCoat, PetAccessory.Bow, Outfit.Blush, pose, source,
                            PixelRowOrder.TopDown, out var width, out var height);
                        var visible = 0;
                        for (var i = 3; i < width * height * 4; i += 4)
                            if (pixels[i] > 0)
                                visible++;
                        check(visible > width * height / 4, breed.Breed + " " + pose + " composes a visible hamster");
                        var marked = breed.Breed == PetBreed.SyrianGolden ? 0 : 2;
                        check(loads.Count == 4 + marked, breed.Breed + " " + pose + " loads base, markings and bow (" + string.Join(", ", loads) + ")");
                        check(layout.IsDarkOverlay(PetLayout.DarkMarkingsPath(breed.Breed, pose)) == Array.IndexOf(striped, breed.Breed) >= 0,
                            breed.Breed + " " + pose + " has a dark overlay only for the stripe or patches");
                    }
                }
            }

            // The Panda's patches are recolored once with its dark color, like Calico's.
            var panda = (byte[])composer.Compose(PetBreed.Panda, PetCoat.White, PetAccessory.None, Outfit.Sage, "YoungAwake",
                source, PixelRowOrder.TopDown, out var w, out var h).Clone();
            layout.TryGetOverlay(PetLayout.DarkMarkingsPath(PetBreed.Panda, "YoungAwake"), out var overlay);
            var dark = Png.Read(Path.Combine(HamsterFolder, overlay.Path + ".bytes"), overlay.Width, overlay.Height);
            var darkRegions = Png.Read(Path.Combine(HamsterFolder, overlay.Path + "Regions.bytes"), overlay.Width, overlay.Height);
            AppearanceRecolor.Recolor(dark, darkRegions, overlay.Width * overlay.Height, layout.Reference,
                PetPalette.DarkTargets(layout, PetBreed.Panda, PetCoat.White, Outfit.Sage));
            var matched = 0;
            var opaque = 0;
            for (var y = 0; y < overlay.Height; y++)
            {
                for (var x = 0; x < overlay.Width; x++)
                {
                    var source0 = (y * overlay.Width + x) * 4;
                    if (dark[source0 + 3] != 255)
                        continue;
                    opaque++;
                    var target = ((overlay.YFromTop + y) * w + overlay.X + x) * 4;
                    if (panda[target] == dark[source0] && panda[target + 1] == dark[source0 + 1] && panda[target + 2] == dark[source0 + 2])
                        matched++;
                }
            }

            check(opaque > 0 && matched > opaque * 9 / 10, "the Panda's patches keep their own recolor (" + matched + "/" + opaque + ")");
            var golden = PetPalette.Targets(layout, PetBreed.SyrianGolden, PetCoat.Golden, Outfit.Sage);
            check(golden.Region(0).Equals(layout.Reference.Region(0)), "the reference coat is the Syrian golden");
        }

        private static void LayoutCoversEveryCatBreed(PetLayout layout, Action<bool, string> check)
        {
            check(layout.YoungScale > 0 && layout.YoungScale < layout.GrownScale, "a kitten is drawn smaller than a grown cat");
            foreach (PetStage stage in Enum.GetValues(typeof(PetStage)))
            {
                foreach (var resting in new[] { false, true })
                {
                    var pose = PetLayout.PoseName(stage, resting);
                    check(layout.TryGetPoseSize(pose, out _, out _), "the cat layout has the " + pose + " pose");
                    foreach (var accessory in new[] { PetAccessory.Collar, PetAccessory.Bandana, PetAccessory.Bow })
                        check(layout.TryGetOverlay(PetLayout.AccessoryPath(accessory, pose), out _),
                            "the " + accessory + " has a " + pose + " overlay");
                    foreach (var breed in new[] { PetBreed.Tabby, PetBreed.Tuxedo, PetBreed.Siamese, PetBreed.Calico })
                        check(layout.TryGetOverlay(PetLayout.MarkingsPath(breed, pose), out _), breed + " has " + pose + " markings");
                    check(layout.IsDarkOverlay(PetLayout.DarkMarkingsPath(PetBreed.Calico, pose)), "Calico has dark " + pose + " patches");
                    check(!layout.TryGetOverlay(PetLayout.MarkingsPath(PetBreed.DomesticShorthair, pose), out _),
                        "the free starter is the plain base");
                }
            }

            check(PetLayout.AccessoryPath(PetAccessory.None, "YoungAwake") == null, "no accessory has no art path");
            foreach (var breed in PetCatalog.Default.BreedsOf(Species.Cat))
            {
                if (breed.Unlock == BreedUnlock.Premium)
                    continue;
                foreach (var coat in breed.Coats)
                    check(layout.TryGetCoat(breed.Breed, coat, out _), "the palette lists " + breed.Breed + " " + coat);
            }
        }

        private static void PaletteFollowsTheContract(PetLayout layout, Action<bool, string> check)
        {
            var ginger = PetPalette.Targets(layout, PetBreed.DomesticShorthair, PetCoat.Ginger, Outfit.Sage);
            check(ginger.Region(0).Equals(layout.Reference.Region(0)), "the reference coat is the Domestic shorthair Ginger");
            check(ginger.Region(3).Equals(layout.Reference.Region(3)), "a Sage collar keeps the drawn accent");
            check(PetPalette.Targets(layout, PetBreed.DomesticShorthair, PetCoat.Ginger, Outfit.Sky).Region(3)
                .Equals(AppearancePalette.OutfitAccent(Outfit.Sky, layout.Reference.Region(3))), "a collar uses the outfit accent palette");
            check(layout.TryGetEyes("Blue", out var blue) &&
                PetPalette.Targets(layout, PetBreed.Siamese, PetCoat.Seal, Outfit.Sage).Region(2).Equals(blue), "a Siamese has blue eyes");

            var bengal = PetPalette.CoatColors(layout, PetBreed.Bengal, PetCoat.Golden);
            check(bengal.Main.Equals(PetPalette.FallbackCoat(PetCoat.Golden)), "a breed missing from the palette uses the generic coat");
            check(bengal.Secondary.R < bengal.Main.R && bengal.Secondary.G < bengal.Main.G, "the generic secondary is deeper");
            var calico = PetPalette.DarkTargets(layout, PetBreed.Calico, PetCoat.Tricolor, Outfit.Sage);
            check(calico.Region(1).Equals(PetPalette.CoatColors(layout, PetBreed.Calico, PetCoat.Tricolor).Dark),
                "dark targets put the dark color in G");
            foreach (PetCoat coat in Enum.GetValues(typeof(PetCoat)))
                PetPalette.FallbackCoat(coat);
            check(true, "every coat has a generic color");
        }

        private static void ComposesEveryPoseFromTheShippedArt(PetLayout layout, Action<bool, string> check)
        {
            var composer = new PetComposer(layout);
            var loads = new List<string>();
            PetArtSource source = (path, width, height) =>
            {
                loads.Add(path);
                return Png.Read(Path.Combine(CatFolder, path + ".bytes"), width, height);
            };

            foreach (var breed in new[] { PetBreed.DomesticShorthair, PetBreed.Tabby, PetBreed.Tuxedo, PetBreed.Siamese, PetBreed.Calico, PetBreed.Bengal })
            {
                var definition = PetCatalog.Default.Get(breed);
                foreach (PetStage stage in Enum.GetValues(typeof(PetStage)))
                {
                    foreach (var resting in new[] { false, true })
                    {
                        var pose = PetLayout.PoseName(stage, resting);
                        loads.Clear();
                        var pixels = composer.Compose(breed, definition.DefaultCoat, PetAccessory.Collar, Outfit.Sky, pose, source,
                            PixelRowOrder.TopDown, out var width, out var height);
                        var visible = 0;
                        for (var i = 3; i < width * height * 4; i += 4)
                            if (pixels[i] > 0)
                                visible++;
                        check(visible > width * height / 4, breed + " " + pose + " composes a visible cat");
                        var expected = 4 + (breed == PetBreed.DomesticShorthair || breed == PetBreed.Bengal ? 0 : 2) +
                            (breed == PetBreed.Calico ? 2 : 0);
                        check(loads.Count == expected, breed + " " + pose + " loads base, markings and collar (" + string.Join(", ", loads) + ")");
                    }
                }
            }

            // Without overlays the composer is exactly the base recolored once and edge-filled.
            var plain = (byte[])composer.Compose(PetBreed.DomesticShorthair, PetCoat.Gray, PetAccessory.None, Outfit.Sage, "YoungAwake",
                source, PixelRowOrder.TopDown, out var w, out var h).Clone();
            var expectedPixels = Png.Read(Path.Combine(CatFolder, "Base", "YoungAwake.bytes"), w, h);
            var baseRegions = Png.Read(Path.Combine(CatFolder, "Base", "YoungAwakeRegions.bytes"), w, h);
            AppearanceRecolor.Recolor(expectedPixels, baseRegions, w * h, layout.Reference,
                PetPalette.Targets(layout, PetBreed.DomesticShorthair, PetCoat.Gray, Outfit.Sage));
            AppearanceRecolor.BleedTransparentEdges(expectedPixels, w, h);
            var same = true;
            for (var i = 0; i < w * h * 4 && same; i++)
                same = plain[i] == expectedPixels[i];
            check(same, "a plain look is the base recolored once");

            composer.Release();
            var again = composer.Compose(PetBreed.Tuxedo, PetCoat.Black, PetAccessory.None, Outfit.Sage, "GrownRest", source,
                PixelRowOrder.BottomUp, out _, out _);
            check(again != null, "the composer works again after releasing its buffers");

            var missing = new PetComposer(layout);
            Throws<InvalidOperationException>(() => missing.Compose(PetBreed.Tabby, PetCoat.Brown, PetAccessory.None, Outfit.Sage,
                "YoungAwake", (path, width, height) => null, PixelRowOrder.TopDown, out _, out _), "missing art is reported", check);
            Throws<InvalidOperationException>(() => missing.Compose(PetBreed.Tabby, PetCoat.Brown, PetAccessory.None, Outfit.Sage,
                "SeniorAwake", source, PixelRowOrder.TopDown, out _, out _), "an unknown pose is reported", check);
        }

        private static void CalicoDarkPatchesKeepTheirColor(PetLayout layout, Action<bool, string> check)
        {
            // A dark overlay pixel ends with the dark target applied once, not twice.
            var composer = new PetComposer(layout);
            PetArtSource source = (path, width, height) => Png.Read(Path.Combine(CatFolder, path + ".bytes"), width, height);
            var tricolor = (byte[])composer.Compose(PetBreed.Calico, PetCoat.Tricolor, PetAccessory.None, Outfit.Sage, "YoungAwake",
                source, PixelRowOrder.TopDown, out var width, out var height).Clone();
            layout.TryGetOverlay(PetLayout.DarkMarkingsPath(PetBreed.Calico, "YoungAwake"), out var overlay);
            var dark = Png.Read(Path.Combine(CatFolder, overlay.Path + ".bytes"), overlay.Width, overlay.Height);
            var darkRegions = Png.Read(Path.Combine(CatFolder, overlay.Path + "Regions.bytes"), overlay.Width, overlay.Height);
            AppearanceRecolor.Recolor(dark, darkRegions, overlay.Width * overlay.Height, layout.Reference,
                PetPalette.DarkTargets(layout, PetBreed.Calico, PetCoat.Tricolor, Outfit.Sage));
            var matched = 0;
            var opaque = 0;
            for (var y = 0; y < overlay.Height; y++)
            {
                for (var x = 0; x < overlay.Width; x++)
                {
                    var source0 = (y * overlay.Width + x) * 4;
                    if (dark[source0 + 3] != 255)
                        continue;
                    opaque++;
                    var target = ((overlay.YFromTop + y) * width + overlay.X + x) * 4;
                    if (tricolor[target] == dark[source0] && tricolor[target + 1] == dark[source0 + 1] && tricolor[target + 2] == dark[source0 + 2])
                        matched++;
                }
            }

            check(opaque > 0 && matched > opaque * 9 / 10, "Calico dark patches keep their own recolor (" + matched + "/" + opaque + ")");
        }

        private static void InvalidLayoutsAreRejected(Action<bool, string> check)
        {
            var reference = new PetReferenceData { coat = new double[] { 1, 2, 3 }, markings = new double[] { 1, 2, 3 },
                eyes = new double[] { 1, 2, 3 }, accent = new double[] { 1, 2, 3 } };
            var poses = new[] { new AppearancePoseData { name = "YoungAwake", width = 2, height = 2 } };
            Throws<FormatException>(() => PetLayout.FromData(null), "an empty pet layout is rejected", check);
            Throws<FormatException>(() => PetLayout.FromData(new PetLayoutData { reference = reference, poses = poses }),
                "a pet layout needs runtime scales", check);
            Throws<FormatException>(() => PetLayout.FromData(new PetLayoutData
            {
                reference = reference, poses = poses, runtimeScale = new PetRuntimeScaleData { young = 0.5, grown = 0.7 },
                overlays = new[] { new PetOverlayData { path = "Markings/Tabby/GrownAwake", pose = "GrownAwake", width = 1, height = 1 } },
            }), "an overlay on an unknown pose is rejected", check);
            Throws<FormatException>(() => PetLayout.FromData(new PetLayoutData
            {
                reference = new PetReferenceData { coat = new double[] { 1, 2 } }, poses = poses,
                runtimeScale = new PetRuntimeScaleData { young = 0.5, grown = 0.7 },
            }), "a malformed reference color is rejected", check);
        }

        private static void Throws<T>(Action action, string label, Action<bool, string> check) where T : Exception
        {
            try
            {
                action();
                check(false, label + " (no exception)");
            }
            catch (T)
            {
                check(true, label);
            }
        }

        // A minimal PNG reader for the shipped art: 8-bit RGBA, not interlaced. Returns top-down rows.
        private static class Png
        {
            public static byte[] Read(string path, int width, int height)
            {
                var bytes = File.ReadAllBytes(path);
                var data = new MemoryStream();
                int w = 0, h = 0;
                for (var offset = 8; offset < bytes.Length;)
                {
                    var length = (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
                    var type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
                    if (type == "IHDR")
                    {
                        w = Big(bytes, offset + 8);
                        h = Big(bytes, offset + 12);
                        if (bytes[offset + 16] != 8 || bytes[offset + 17] != 6 || bytes[offset + 20] != 0)
                            throw new InvalidDataException(path + " is not 8-bit RGBA without interlacing.");
                    }
                    else if (type == "IDAT")
                    {
                        data.Write(bytes, offset + 8, length);
                    }

                    offset += 12 + length;
                }

                if (w != width || h != height)
                    throw new InvalidOperationException(path + " is " + w + "x" + h + ", not " + width + "x" + height + ".");

                data.Position = 0;
                var raw = new MemoryStream();
                using (var inflate = new ZLibStream(data, CompressionMode.Decompress))
                    inflate.CopyTo(raw);
                return Unfilter(raw.ToArray(), w, h);
            }

            private static int Big(byte[] bytes, int offset) =>
                (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

            private static byte[] Unfilter(byte[] raw, int width, int height)
            {
                var stride = width * 4;
                var output = new byte[stride * height];
                for (var y = 0; y < height; y++)
                {
                    var filter = raw[y * (stride + 1)];
                    var line = y * (stride + 1) + 1;
                    for (var x = 0; x < stride; x++)
                    {
                        int a = x >= 4 ? output[y * stride + x - 4] : 0;
                        int b = y > 0 ? output[(y - 1) * stride + x] : 0;
                        int c = x >= 4 && y > 0 ? output[(y - 1) * stride + x - 4] : 0;
                        int value = raw[line + x];
                        switch (filter)
                        {
                            case 1: value += a; break;
                            case 2: value += b; break;
                            case 3: value += (a + b) / 2; break;
                            case 4: value += Paeth(a, b, c); break;
                        }

                        output[y * stride + x] = (byte)value;
                    }
                }

                return output;
            }

            private static int Paeth(int a, int b, int c)
            {
                int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
            }
        }
    }
}
