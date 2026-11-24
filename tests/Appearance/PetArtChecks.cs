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
    // The cat layout, palette and compositing (tools/pets/README.md), checked against the shipped art.
    internal static class PetArtChecks
    {
        private static readonly string CatFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "unity", "Assets", "Resources", "BAIby", "Pets", "Cat"));

        public static void Run(Action<bool, string> check)
        {
            var layout = LoadLayout();
            LayoutCoversEveryCatBreed(layout, check);
            PaletteFollowsTheContract(layout, check);
            ComposesEveryPoseFromTheShippedArt(layout, check);
            CalicoDarkPatchesKeepTheirColor(layout, check);
            InvalidLayoutsAreRejected(check);
        }

        private static PetLayout LoadLayout()
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            var data = JsonSerializer.Deserialize<PetLayoutData>(File.ReadAllText(Path.Combine(CatFolder, "layout.json")), options);
            return PetLayout.FromData(data);
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
                    check(layout.TryGetOverlay(PetLayout.AccessoryPath(PetAccessory.Collar, pose), out _),
                        "the collar has a " + pose + " overlay");
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
