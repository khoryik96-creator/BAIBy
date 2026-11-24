using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BAIby.Character;
using BAIby.Simulation;

namespace BAIby.Tests.Appearance
{
    internal static class Program
    {
        private static int failures;
        private static int checks;

        private static int Main()
        {
            IdentityLookLeavesPixelsExact();
            PaletteTargetsMatchContract();
            KnownValueRecolor();
            RecolorRoundsHalfToEvenAndClamps();
            WeightsSumRule();
            AlphaOverMatchesStraightAlphaFormula();
            OverlayOffsetsConvertBetweenRowOrders();
            OverlaysClipAtCanvasEdges();
            TransparentEdgesTakeNeighborColors();
            LayoutValidatesAndNamesResources();
            CommittedLayoutParsesWhenPresent();
            PetArtChecks.Run(Check);

            Console.WriteLine(failures == 0
                ? "Appearance checks passed (" + checks + " checks)."
                : failures + " of " + checks + " appearance checks failed.");
            return failures == 0 ? 0 : 1;
        }

        private static readonly AppearanceColors Reference = new AppearanceColors(
            new AppearanceColor(232, 178, 141),
            new AppearanceColor(121, 74, 48.5),
            new AppearanceColor(153, 176, 148),
            new AppearanceColor(98, 129, 107));

        private static void IdentityLookLeavesPixelsExact()
        {
            var random = new Random(11);
            const int count = 20000;
            var pixels = RandomPixels(random, count);
            var regions = RandomRegions(random, count);
            var original = (byte[])pixels.Clone();

            var identity = AppearancePalette.Targets(BabyAppearance.Default, Reference);
            Check(identity.Equals(Reference), "the default look resolves to the reference colors");
            AppearanceRecolor.Recolor(pixels, regions, count, Reference, identity);
            Check(Same(pixels, original), "recoloring with the identity look leaves every pixel exact");

            // Identity options for one region stay exact while other regions change.
            var fair = AppearancePalette.Targets(BabyAppearance.Default.WithSkinTone(SkinTone.Fair), Reference);
            var hairOnly = new byte[count * 4];
            for (int i = 0; i < count; i++)
                hairOnly[i * 4 + 1] = regions[i * 4 + 1];
            var hairPixels = (byte[])original.Clone();
            AppearanceRecolor.Recolor(hairPixels, hairOnly, count, Reference, fair);
            Check(Same(hairPixels, original), "a skin change leaves hair-only pixels exact when hair is the identity option");

            // Untagged pixels (outlines, eyes) never change.
            var untagged = (byte[])original.Clone();
            AppearanceRecolor.Recolor(untagged, new byte[count * 4], count, Reference,
                AppearancePalette.Targets(new BabyAppearance(SkinTone.Deep, HairColor.Platinum, HairStyle.Coils, Outfit.Midnight, Headwear.Bow), Reference));
            Check(Same(untagged, original), "pixels with zero weights keep their original color");
        }

        private static void PaletteTargetsMatchContract()
        {
            foreach (SkinTone tone in Enum.GetValues(typeof(SkinTone)))
                AppearancePalette.Skin(tone, Reference.Skin);
            foreach (HairColor color in Enum.GetValues(typeof(HairColor)))
                AppearancePalette.Hair(color, Reference.Hair);
            foreach (Outfit outfit in Enum.GetValues(typeof(Outfit)))
            {
                AppearancePalette.OutfitMain(outfit, Reference.OutfitMain);
                AppearancePalette.OutfitAccent(outfit, Reference.OutfitAccent);
            }

            Check(AppearancePalette.Skin(SkinTone.Warm, Reference.Skin).Equals(Reference.Skin), "Warm is the reference skin");
            Check(AppearancePalette.Hair(HairColor.Chestnut, Reference.Hair).Equals(Reference.Hair), "Chestnut is the reference hair");
            Check(AppearancePalette.OutfitMain(Outfit.Sage, Reference.OutfitMain).Equals(Reference.OutfitMain), "Sage is the reference outfit");
            Check(AppearancePalette.OutfitAccent(Outfit.Sage, Reference.OutfitAccent).Equals(Reference.OutfitAccent), "Sage is the reference accent");
            Check(AppearancePalette.Skin(SkinTone.Deep, Reference.Skin).Equals(new AppearanceColor(104, 62, 40)), "Deep skin target");
            Check(AppearancePalette.Hair(HairColor.Honey, Reference.Hair).Equals(new AppearanceColor(200, 146, 80)), "Honey hair target");
            Check(AppearancePalette.OutfitMain(Outfit.Midnight, Reference.OutfitMain).Equals(new AppearanceColor(70, 82, 110)), "Midnight main target");
            Check(AppearancePalette.OutfitAccent(Outfit.Terracotta, Reference.OutfitAccent).Equals(new AppearanceColor(196, 116, 84)), "Terracotta accent target");

            var look = new BabyAppearance(SkinTone.Tan, HairColor.Auburn, HairStyle.Smooth, Outfit.Lilac, Headwear.DaisyClip);
            var targets = AppearancePalette.Targets(look, Reference);
            Check(targets.Skin.Equals(new AppearanceColor(196, 128, 82)) && targets.Hair.Equals(new AppearanceColor(150, 70, 40)) &&
                targets.OutfitMain.Equals(new AppearanceColor(236, 228, 244)) && targets.OutfitAccent.Equals(new AppearanceColor(170, 150, 200)),
                "a full look resolves every region target");
            Throws<ArgumentOutOfRangeException>(() => AppearancePalette.Skin((SkinTone)99, Reference.Skin), "unknown skin IDs are rejected");
        }

        private static void KnownValueRecolor()
        {
            var reference = new AppearanceColors(
                new AppearanceColor(200, 150, 100), new AppearanceColor(120, 100, 80),
                new AppearanceColor(40, 40, 40), new AppearanceColor(10, 10, 10));
            var target = new AppearanceColors(
                new AppearanceColor(100, 150, 200), new AppearanceColor(60, 50, 40),
                new AppearanceColor(80, 80, 80), new AppearanceColor(10, 10, 10));

            var full = Recolor1(new byte[] { 200, 100, 50, 77 }, new byte[] { 255, 0, 0, 0 }, reference, target);
            CheckPixel(full, 100, 100, 100, 77, "a full skin weight takes the scaled color and keeps alpha");

            var half = Recolor1(new byte[] { 200, 150, 100, 255 }, new byte[] { 128, 0, 0, 0 }, reference, target);
            // 200 * 127/255 + 128/255 * 100 = 149.80 -> 150; G stays 150; 100 * 127/255 + 128/255 * 200 = 150.20 -> 150.
            CheckPixel(half, 150, 150, 150, 255, "a partial weight blends source and recolor");

            var mixedReference = new AppearanceColors(
                new AppearanceColor(120, 100, 80), new AppearanceColor(1, 1, 1),
                new AppearanceColor(40, 40, 40), new AppearanceColor(1, 1, 1));
            var mixedTarget = new AppearanceColors(
                new AppearanceColor(60, 50, 40), new AppearanceColor(1, 1, 1),
                new AppearanceColor(80, 80, 80), new AppearanceColor(1, 1, 1));
            var mixed = Recolor1(new byte[] { 120, 80, 40, 255 }, new byte[] { 100, 0, 60, 0 }, mixedReference, mixedTarget);
            CheckPixel(mixed, 125, 83, 42, 255, "skin and outfit weights mix per the contract formula");

            Check(AppearanceRecolor.RecolorChannel(90, 45, 180) == 22, "90 * 45 / 180 = 22.5 rounds half to even");
        }

        private static void RecolorRoundsHalfToEvenAndClamps()
        {
            Check(AppearanceRecolor.RecolorChannel(3, 1, 2) == 2, "1.5 rounds to 2");
            Check(AppearanceRecolor.RecolorChannel(5, 1, 2) == 2, "2.5 rounds to 2");
            Check(AppearanceRecolor.RecolorChannel(200, 255, 100) == 255, "bright targets clamp at 255");
            Check(AppearanceRecolor.RecolorChannel(7, 0, 100) == 0, "a black target channel gives 0");
            Check(AppearanceRecolor.RecolorChannel(9, 200, 0) == 9, "a zero reference channel keeps the source value");
            Check(AppearanceRecolor.RoundToByte(-3.2) == 0, "negative values clamp at 0");
            Check(AppearanceRecolor.RoundToByte(300) == 255, "large values clamp at 255");
            Check(AppearanceRecolor.RoundToByte(double.NaN) == 0, "NaN becomes 0");

            var reference = Solid(new AppearanceColor(100, 100, 100));
            var bright = Solid(new AppearanceColor(255, 255, 255));
            var clamped = Recolor1(new byte[] { 240, 10, 128, 200 }, new byte[] { 0, 0, 255, 0 }, reference, bright);
            CheckPixel(clamped, 255, 26, 255, 200, "whole-pixel recolor clamps each channel independently");
        }

        private static void WeightsSumRule()
        {
            var reference = new AppearanceColors(
                new AppearanceColor(200, 150, 100), new AppearanceColor(100, 100, 100),
                new AppearanceColor(50, 50, 50), new AppearanceColor(25, 25, 25));
            var target = new AppearanceColors(
                new AppearanceColor(100, 150, 200), new AppearanceColor(200, 200, 200),
                new AppearanceColor(25, 25, 25), new AppearanceColor(50, 50, 50));

            // Weights summing to exactly 255 leave nothing of the source.
            var split = Recolor1(new byte[] { 100, 100, 100, 255 }, new byte[] { 51, 102, 0, 102 }, reference, target);
            // Skin recolor of 100 is (50, 100, 200); hair and accent give 200.
            // R: 0.2*50 + 0.4*200 + 0.4*200 = 170; G: 0.2*100 + 160 = 180; B: 0.2*200 + 160 = 200.
            CheckPixel(split, 170, 180, 200, 255, "weights summing to 255 replace the source completely");

            // A malformed map whose weights exceed 255 is normalized rather than extrapolated.
            var over = Recolor1(new byte[] { 100, 100, 100, 255 }, new byte[] { 255, 255, 0, 0 }, reference, target);
            // Half skin (50, 100, 200) + half hair (200, 200, 200) = (125, 150, 200).
            CheckPixel(over, 125, 150, 200, 255, "weights past 255 are normalized");

            // Compositing two valid region maps keeps R + G + B + A <= 255.
            var random = new Random(5);
            bool valid = true;
            bool convex = true;
            for (int trial = 0; trial < 20000; trial++)
            {
                var canvas = RandomPixels(random, 1);
                var canvasRegions = RandomRegions(random, 1);
                var overlay = RandomPixels(random, 1);
                var overlayRegions = RandomRegions(random, 1);
                var before = (byte[])canvasRegions.Clone();
                AppearanceRecolor.CompositeOver(canvas, canvasRegions, 1, 1, overlay, overlayRegions, 1, 1, 0, 0, PixelRowOrder.BottomUp);
                int sum = canvasRegions[0] + canvasRegions[1] + canvasRegions[2] + canvasRegions[3];
                valid &= sum <= 255;
                for (int channel = 0; channel < 4; channel++)
                {
                    int low = Math.Min(before[channel], overlayRegions[channel]);
                    int high = Math.Max(before[channel], overlayRegions[channel]);
                    convex &= canvasRegions[channel] >= low - 2 && canvasRegions[channel] <= high;
                }
            }
            Check(valid, "composited region maps never sum past 255");
            Check(convex, "composited region weights stay between the canvas and overlay weights");
        }

        private static void AlphaOverMatchesStraightAlphaFormula()
        {
            var canvas = new byte[] { 0, 0, 255, 255 };
            var canvasRegions = new byte[] { 255, 0, 0, 0 };
            AppearanceRecolor.CompositeOver(canvas, canvasRegions, 1, 1,
                new byte[] { 255, 0, 0, 128 }, new byte[] { 0, 255, 0, 0 }, 1, 1, 0, 0, PixelRowOrder.TopDown);
            CheckPixel(canvas, 128, 0, 127, 255, "half-transparent red over opaque blue");
            CheckPixel(canvasRegions, 127, 128, 0, 0, "half-covered weights blend by overlay alpha");

            var clear = new byte[] { 9, 9, 9, 0 };
            var clearRegions = new byte[] { 0, 0, 0, 200 };
            AppearanceRecolor.CompositeOver(clear, clearRegions, 1, 1,
                new byte[] { 10, 20, 30, 90 }, new byte[] { 0, 255, 0, 0 }, 1, 1, 0, 0, PixelRowOrder.TopDown);
            CheckPixel(clear, 10, 20, 30, 90, "over a transparent pixel the overlay color is kept unpremultiplied");
            CheckPixel(clearRegions, 0, 255, 0, 0, "over a transparent pixel the overlay weights win");

            var both = new byte[] { 0, 0, 255, 128 };
            var bothRegions = new byte[4];
            AppearanceRecolor.CompositeOver(both, bothRegions, 1, 1,
                new byte[] { 255, 0, 0, 128 }, new byte[4], 1, 1, 0, 0, PixelRowOrder.TopDown);
            // a_out = 191.75 -> 192; r = 255 * 0.66754 = 170.22 -> 170; b = 255 * 0.33246 = 84.78 -> 85.
            CheckPixel(both, 170, 0, 85, 192, "half-transparent over half-transparent");

            var keep = new byte[] { 1, 2, 3, 4 };
            var keepRegions = new byte[] { 5, 6, 7, 8 };
            AppearanceRecolor.CompositeOver(keep, keepRegions, 1, 1,
                new byte[] { 200, 200, 200, 0 }, new byte[] { 255, 0, 0, 0 }, 1, 1, 0, 0, PixelRowOrder.TopDown);
            CheckPixel(keep, 1, 2, 3, 4, "a transparent overlay pixel leaves the canvas");
            CheckPixel(keepRegions, 5, 6, 7, 8, "a transparent overlay pixel leaves the canvas weights");

            var replace = new byte[] { 1, 2, 3, 4 };
            var replaceRegions = new byte[] { 5, 6, 7, 8 };
            AppearanceRecolor.CompositeOver(replace, replaceRegions, 1, 1,
                new byte[] { 200, 100, 50, 255 }, new byte[] { 0, 0, 0, 255 }, 1, 1, 0, 0, PixelRowOrder.TopDown);
            CheckPixel(replace, 200, 100, 50, 255, "an opaque overlay pixel replaces the canvas");
            CheckPixel(replaceRegions, 0, 0, 0, 255, "an opaque overlay pixel replaces the canvas weights");
        }

        private static void OverlayOffsetsConvertBetweenRowOrders()
        {
            Check(AppearanceRecolor.FirstCanvasRow(541, 220, 4, PixelRowOrder.TopDown) == 4, "top-down rows keep the PIL offset");
            Check(AppearanceRecolor.FirstCanvasRow(541, 220, 4, PixelRowOrder.BottomUp) == 317, "bottom-up rows flip the PIL offset");

            // A single marked pixel at PIL (x=2, y=0) on a 4x4 canvas is the top row: bottom-up row 3.
            var bottomUp = new byte[4 * 4 * 4];
            AppearanceRecolor.CompositeOver(bottomUp, new byte[bottomUp.Length], 4, 4,
                new byte[] { 255, 255, 255, 255 }, new byte[4], 1, 1, 2, 0, PixelRowOrder.BottomUp);
            Check(bottomUp[(3 * 4 + 2) * 4] == 255 && Sum(bottomUp) == 255 * 4, "y = 0 lands on the top row of a bottom-up buffer");

            var random = new Random(23);
            bool same = true;
            for (int trial = 0; trial < 60; trial++)
            {
                int width = random.Next(1, 24), height = random.Next(1, 24);
                int overlayWidth = random.Next(1, 16), overlayHeight = random.Next(1, 16);
                int x = random.Next(-5, width + 2), y = random.Next(-5, height + 2);
                var canvas = RandomPixels(random, width * height);
                var canvasRegions = RandomRegions(random, width * height);
                var overlay = RandomPixels(random, overlayWidth * overlayHeight);
                var overlayRegions = RandomRegions(random, overlayWidth * overlayHeight);

                var topDown = (byte[])canvas.Clone();
                var topDownRegions = (byte[])canvasRegions.Clone();
                AppearanceRecolor.CompositeOver(topDown, topDownRegions, width, height,
                    overlay, overlayRegions, overlayWidth, overlayHeight, x, y, PixelRowOrder.TopDown);

                var flipped = Flip(canvas, width, height);
                var flippedRegions = Flip(canvasRegions, width, height);
                AppearanceRecolor.CompositeOver(flipped, flippedRegions, width, height,
                    Flip(overlay, overlayWidth, overlayHeight), Flip(overlayRegions, overlayWidth, overlayHeight),
                    overlayWidth, overlayHeight, x, y, PixelRowOrder.BottomUp);

                same &= Same(Flip(flipped, width, height), topDown) && Same(Flip(flippedRegions, width, height), topDownRegions);
            }
            Check(same, "bottom-up compositing matches top-down compositing after flipping rows");

            // The layout's y is measured from the top: a 2-row overlay at y=1 on a 5-row canvas covers
            // top-down rows 1-2, which are bottom-up rows 2-3.
            var tall = new byte[1 * 5 * 4];
            AppearanceRecolor.CompositeOver(tall, new byte[tall.Length], 1, 5,
                new byte[] { 10, 10, 10, 255, 20, 20, 20, 255 }, new byte[8], 1, 2, 0, 1, PixelRowOrder.BottomUp);
            // Bottom-up overlay row 0 is the overlay's bottom (top-down row 2 of the canvas = bottom-up row 2).
            Check(tall[2 * 4] == 10 && tall[3 * 4] == 20 && tall[0] == 0 && tall[4] == 0 && tall[16] == 0,
                "a bottom-up overlay keeps its own bottom row lowest");
        }

        private static void OverlaysClipAtCanvasEdges()
        {
            var canvas = new byte[3 * 3 * 4];
            var overlay = new byte[4 * 4 * 4];
            for (int i = 3; i < overlay.Length; i += 4)
            {
                overlay[i - 3] = 50;
                overlay[i] = 255;
            }
            AppearanceRecolor.CompositeOver(canvas, new byte[canvas.Length], 3, 3,
                overlay, new byte[overlay.Length], 4, 4, -2, 2, PixelRowOrder.TopDown);
            // Covers top-down rows 2.. and columns ..1: pixels (0,2) and (1,2).
            int covered = 0;
            for (int i = 0; i < 9; i++)
                if (canvas[i * 4 + 3] == 255)
                    covered++;
            Check(covered == 2 && canvas[(2 * 3 + 0) * 4 + 3] == 255 && canvas[(2 * 3 + 1) * 4 + 3] == 255,
                "overlays clip at the canvas edges");

            var scratch = new byte[100 * 4];
            AppearanceRecolor.CompositeOver(scratch, new byte[scratch.Length], 2, 2,
                new byte[] { 1, 1, 1, 255 }, new byte[4], 1, 1, 1, 1, PixelRowOrder.TopDown);
            Check(scratch[(1 * 2 + 1) * 4 + 3] == 255 && Sum(scratch) == 258, "oversized scratch buffers use only the stated canvas");
            Throws<ArgumentException>(() => AppearanceRecolor.CompositeOver(new byte[4], new byte[4], 2, 2,
                new byte[4], new byte[4], 1, 1, 0, 0, PixelRowOrder.TopDown), "short canvas buffers are rejected");
        }

        private static void TransparentEdgesTakeNeighborColors()
        {
            // 4x3 canvas: an opaque red pixel at (1,1), a half-transparent blue pixel at (2,1), the rest clear.
            var pixels = new byte[4 * 3 * 4];
            void Set(int x, int y, byte r, byte g, byte b, byte a)
            {
                int o = (y * 4 + x) * 4;
                pixels[o] = r;
                pixels[o + 1] = g;
                pixels[o + 2] = b;
                pixels[o + 3] = a;
            }
            Set(1, 1, 200, 0, 0, 255);
            Set(2, 1, 0, 0, 90, 85);
            var visible = new[] { pixels[(1 * 4 + 1) * 4], pixels[(1 * 4 + 2) * 4 + 2] };
            AppearanceRecolor.BleedTransparentEdges(pixels, 4, 3);

            int At(int x, int y, int channel) => pixels[(y * 4 + x) * 4 + channel];
            Check(At(1, 1, 0) == visible[0] && At(1, 1, 3) == 255 && At(2, 1, 2) == visible[1] && At(2, 1, 3) == 85,
                "edge filling leaves visible pixels unchanged");
            Check(At(0, 0, 0) == 200 && At(0, 0, 2) == 0 && At(0, 0, 3) == 0, "a clear pixel beside one visible pixel takes its color");
            // (2,0) touches red (alpha 255) and blue (alpha 85): (200*255)/340 = 150, (90*85)/340 = 22.5 -> 23.
            Check(At(2, 0, 0) == 150 && At(2, 0, 2) == 23 && At(2, 0, 3) == 0, "neighbor colors are weighted by alpha");
            Check(At(3, 2, 0) == 0 && At(3, 2, 2) == 90 && At(3, 2, 3) == 0, "only visible neighbors contribute");
            var empty = new byte[2 * 2 * 4];
            AppearanceRecolor.BleedTransparentEdges(empty, 2, 2);
            Check(Sum(empty) == 0, "fully clear images stay black and clear");
            Throws<ArgumentException>(() => AppearanceRecolor.BleedTransparentEdges(new byte[4], 2, 2),
                "short edge-fill buffers are rejected");
        }

        private static void LayoutValidatesAndNamesResources()
        {
            var data = new AppearanceLayoutData
            {
                reference = new AppearanceReferenceData
                {
                    skin = new double[] { 232, 178, 141 },
                    hair = new double[] { 121, 74, 48 },
                    outfitMain = new double[] { 153, 176, 148 },
                    outfitAccent = new double[] { 98, 129, 107 },
                },
                poses = new[] { new AppearancePoseData { name = "Newborn", width = 464, height = 541 } },
                overlays = new[]
                {
                    new AppearanceOverlayData { path = "Hair/Curls/Newborn", pose = "Newborn", x = 10, y = 4, width = 300, height = 220 },
                },
            };

            var layout = AppearanceLayout.FromData(data);
            Check(layout.Reference.Hair.Equals(new AppearanceColor(121, 74, 48)), "the layout reads reference colors");
            Check(layout.TryGetPoseSize("Newborn", out var width, out var height) && width == 464 && height == 541, "the layout reads pose sizes");
            Check(layout.TryGetOverlay("Hair/Curls/Newborn", out var overlay) && overlay.X == 10 && overlay.YFromTop == 4 &&
                overlay.Width == 300 && overlay.Height == 220, "the layout reads overlay rectangles");
            Check(!layout.TryGetOverlay("Hair/Coils/Newborn", out _), "missing overlays are reported");

            Check(AppearanceLayout.BasePath("Sitter") == "Base/Sitter", "base path");
            Check(AppearanceLayout.RegionsPath(AppearanceLayout.BasePath("Sitter")) == "Base/SitterRegions", "base regions path");
            Check(AppearanceLayout.HairPath(HairStyle.Tuft, "WalkerRest") == "Hair/Tuft/WalkerRest", "hair path");
            Check(AppearanceLayout.HeadwearPath(Headwear.BearBeanie, "Crawler") == "Headwear/BearBeanie/Crawler", "headwear path");
            Check(AppearanceLayout.HeadwearPath(Headwear.None, "Crawler") == null, "no headwear has no overlay");
            foreach (HairStyle style in Enum.GetValues(typeof(HairStyle)))
                Check(AppearanceLayout.StyleName(style) == style.ToString(), "hair folder matches enum name " + style);
            foreach (Headwear kind in Enum.GetValues(typeof(Headwear)))
                if (kind != Headwear.None)
                    Check(AppearanceLayout.HeadwearName(kind) == kind.ToString(), "headwear folder matches enum name " + kind);
            Check(AppearanceLayout.PoseNames.Count == 8, "eight poses");

            data.reference.skin = new double[] { 1, 2 };
            Throws<FormatException>(() => AppearanceLayout.FromData(data), "short reference colors are rejected");
            data.reference.skin = new double[] { 232, 178, 141 };
            data.overlays[0].pose = "Sleeper";
            Throws<FormatException>(() => AppearanceLayout.FromData(data), "overlays for unknown poses are rejected");
        }

        // The art pipeline writes layout.json concurrently; when it exists, make sure its shape matches.
        private static void CommittedLayoutParsesWhenPresent()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "unity", "Assets", "Resources", "BAIby", "Baby", "Appearance", "layout.json");
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
            {
                Console.WriteLine("Skipped committed layout check: " + path + " does not exist yet.");
                return;
            }

            var options = new JsonSerializerOptions { IncludeFields = true };
            var data = JsonSerializer.Deserialize<AppearanceLayoutData>(File.ReadAllText(path), options);
            var layout = AppearanceLayout.FromData(data);
            var missing = new List<string>();
            foreach (var pose in AppearanceLayout.PoseNames)
            {
                if (!layout.TryGetPoseSize(pose, out _, out _))
                    missing.Add("pose " + pose);
                foreach (HairStyle style in Enum.GetValues(typeof(HairStyle)))
                    if (!layout.TryGetOverlay(AppearanceLayout.HairPath(style, pose), out _))
                        missing.Add(AppearanceLayout.HairPath(style, pose));
                foreach (Headwear kind in Enum.GetValues(typeof(Headwear)))
                {
                    var overlayPath = AppearanceLayout.HeadwearPath(kind, pose);
                    if (overlayPath != null && !layout.TryGetOverlay(overlayPath, out _))
                        missing.Add(overlayPath);
                }
            }
            Check(missing.Count == 0, "the committed layout lists every pose and overlay" +
                (missing.Count == 0 ? "" : " (missing: " + string.Join(", ", missing) + ")"));
        }

        private static byte[] Recolor1(byte[] pixel, byte[] region, AppearanceColors reference, AppearanceColors target)
        {
            var pixels = (byte[])pixel.Clone();
            AppearanceRecolor.Recolor(pixels, (byte[])region.Clone(), 1, reference, target);
            return pixels;
        }

        private static AppearanceColors Solid(AppearanceColor color) => new AppearanceColors(color, color, color, color);

        private static byte[] RandomPixels(Random random, int count)
        {
            var pixels = new byte[count * 4];
            random.NextBytes(pixels);
            return pixels;
        }

        private static byte[] RandomRegions(Random random, int count)
        {
            var regions = new byte[count * 4];
            for (int i = 0; i < count; i++)
            {
                int left = random.Next(0, 4) == 0 ? 255 : random.Next(0, 256);
                for (int channel = 0; channel < 4; channel++)
                {
                    int weight = channel == 3 ? left : random.Next(0, left + 1);
                    if (random.Next(0, 3) == 0)
                        weight = 0;
                    regions[i * 4 + channel] = (byte)weight;
                    left -= weight;
                }
            }
            return regions;
        }

        // Reverses row order: converts a top-down buffer to bottom-up and back.
        private static byte[] Flip(byte[] buffer, int width, int height)
        {
            var flipped = new byte[buffer.Length];
            int stride = width * 4;
            for (int row = 0; row < height; row++)
                Buffer.BlockCopy(buffer, row * stride, flipped, (height - 1 - row) * stride, stride);
            return flipped;
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        private static int Sum(byte[] buffer)
        {
            int sum = 0;
            foreach (var value in buffer)
                sum += value;
            return sum;
        }

        private static void CheckPixel(byte[] pixel, int r, int g, int b, int a, string label)
        {
            Check(pixel[0] == r && pixel[1] == g && pixel[2] == b && pixel[3] == a,
                label + " (got " + pixel[0] + ", " + pixel[1] + ", " + pixel[2] + ", " + pixel[3] +
                "; expected " + r + ", " + g + ", " + b + ", " + a + ")");
        }

        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try
            {
                action();
                Check(false, label + " (no exception)");
            }
            catch (T)
            {
                Check(true, label);
            }
        }

        private static void Check(bool condition, string label)
        {
            checks++;
            if (condition)
                return;
            failures++;
            Console.WriteLine("FAIL: " + label);
        }
    }
}
