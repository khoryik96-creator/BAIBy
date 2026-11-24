using System;
using System.Collections.Generic;
using BAIby.Simulation.Pets;

namespace BAIby.Character
{
    // JsonUtility shapes for Resources/BAIby/Pets/{Species}/layout.json (tools/pets/README.md). Public
    // fields on [Serializable] classes, preserved from stripping by link.xml like the baby layout.
    [Serializable]
    public sealed class PetLayoutData
    {
        public string species;
        public PetReferenceData reference;
        public PetRuntimeScaleData runtimeScale;
        public AppearancePoseData[] poses;
        public PetOverlayData[] overlays;
        public PetPaletteData palette;
    }

    [Serializable]
    public sealed class PetReferenceData
    {
        public double[] coat;
        public double[] markings;
        public double[] eyes;
        public double[] accent;
    }

    [Serializable]
    public sealed class PetRuntimeScaleData
    {
        public double young;
        public double grown;
    }

    [Serializable]
    public sealed class PetOverlayData
    {
        public string path;
        public string pose;
        public int x;
        public int y;
        public int width;
        public int height;
        // "secondary" for markings and the collar, "dark" for overlays recolored on their own first.
        public string gTarget;
    }

    [Serializable]
    public sealed class PetPaletteData
    {
        public PetCoatColorData[] coats;
        public PetNamedColorData[] eyes;
    }

    [Serializable]
    public sealed class PetCoatColorData
    {
        public string breed;
        public string coat;
        public double[] main;
        public double[] secondary;
        public double[] dark;
        public string eyes;
    }

    [Serializable]
    public sealed class PetNamedColorData
    {
        public string name;
        public double[] color;
    }

    // One breed coat's suggested colors from the layout palette.
    public readonly struct PetCoatColors
    {
        public PetCoatColors(AppearanceColor main, AppearanceColor secondary, AppearanceColor dark, AppearanceColor eyes)
        {
            Main = main;
            Secondary = secondary;
            Dark = dark;
            Eyes = eyes;
        }

        public AppearanceColor Main { get; }
        public AppearanceColor Secondary { get; }
        public AppearanceColor Dark { get; }
        public AppearanceColor Eyes { get; }
    }

    // Validated pet layout plus the resource naming rules of the pet art. Paths are relative to
    // Resources/BAIby/Pets/{Species}/ and omit ".bytes". Region channels: R main coat, G secondary coat or
    // markings, B iris, A accessory accent; they recolor through AppearanceColors in that channel order.
    public sealed class PetLayout
    {
        public const string DarkSuffix = "Dark";

        private readonly Dictionary<string, AppearancePoseData> poses;
        private readonly Dictionary<string, AppearanceOverlay> overlays;
        private readonly HashSet<string> darkOverlays;
        private readonly Dictionary<string, PetCoatColors> coats;
        private readonly Dictionary<string, AppearanceColor> eyes;

        private PetLayout(AppearanceColors reference, double youngScale, double grownScale,
            Dictionary<string, AppearancePoseData> poses, Dictionary<string, AppearanceOverlay> overlays,
            HashSet<string> darkOverlays, Dictionary<string, PetCoatColors> coats, Dictionary<string, AppearanceColor> eyes)
        {
            Reference = reference;
            YoungScale = youngScale;
            GrownScale = grownScale;
            this.poses = poses;
            this.overlays = overlays;
            this.darkOverlays = darkOverlays;
            this.coats = coats;
            this.eyes = eyes;
        }

        // Reference colors in region order: coat (R), markings (G), eyes (B), accent (A).
        public AppearanceColors Reference { get; }
        // Draw scale relative to the baby's pose pixels.
        public double YoungScale { get; }
        public double GrownScale { get; }

        public double RuntimeScale(PetStage stage) => stage == PetStage.Grown ? GrownScale : YoungScale;

        public static string PoseName(PetStage stage, bool resting) =>
            (stage == PetStage.Grown ? "Grown" : "Young") + (resting ? "Rest" : "Awake");

        public static string BasePath(string pose) => "Base/" + pose;

        public static string MarkingsPath(PetBreed breed, string pose) => "Markings/" + breed + "/" + pose;

        public static string DarkMarkingsPath(PetBreed breed, string pose) => MarkingsPath(breed, pose) + DarkSuffix;

        // Null when the accessory has no art yet; only the collar ships with the cat.
        public static string AccessoryPath(PetAccessory accessory, string pose) =>
            accessory == PetAccessory.Collar ? "Accessories/Collar/" + pose : null;

        public static string RegionsPath(string imagePath) => imagePath + AppearanceLayout.RegionsSuffix;

        public static PetLayout FromData(PetLayoutData data)
        {
            if (data == null)
                throw new FormatException("The pet layout is empty.");
            if (data.reference == null)
                throw new FormatException("The pet layout has no reference colors.");
            if (data.runtimeScale == null || !(data.runtimeScale.young > 0) || !(data.runtimeScale.grown > 0))
                throw new FormatException("The pet layout needs positive young and grown runtime scales.");

            var reference = new AppearanceColors(
                ReadColor(data.reference.coat, "coat"),
                ReadColor(data.reference.markings, "markings"),
                ReadColor(data.reference.eyes, "eyes"),
                ReadColor(data.reference.accent, "accent"));

            var poses = new Dictionary<string, AppearancePoseData>(StringComparer.Ordinal);
            foreach (var pose in data.poses ?? Array.Empty<AppearancePoseData>())
            {
                if (pose == null || string.IsNullOrEmpty(pose.name) || pose.width <= 0 || pose.height <= 0)
                    throw new FormatException("The pet layout has an invalid pose entry.");
                if (poses.ContainsKey(pose.name))
                    throw new FormatException("The pet layout repeats pose " + pose.name + ".");
                poses.Add(pose.name, pose);
            }

            var overlays = new Dictionary<string, AppearanceOverlay>(StringComparer.Ordinal);
            var dark = new HashSet<string>(StringComparer.Ordinal);
            foreach (var overlay in data.overlays ?? Array.Empty<PetOverlayData>())
            {
                if (overlay == null || string.IsNullOrEmpty(overlay.path) || overlay.width <= 0 || overlay.height <= 0)
                    throw new FormatException("The pet layout has an invalid overlay entry.");
                if (overlay.pose == null || !poses.ContainsKey(overlay.pose))
                    throw new FormatException("Overlay " + overlay.path + " names an unknown pose.");
                if (overlays.ContainsKey(overlay.path))
                    throw new FormatException("The pet layout repeats overlay " + overlay.path + ".");
                overlays.Add(overlay.path, new AppearanceOverlay(
                    overlay.path, overlay.pose, overlay.x, overlay.y, overlay.width, overlay.height));
                if (string.Equals(overlay.gTarget, "dark", StringComparison.Ordinal))
                    dark.Add(overlay.path);
            }

            var eyes = new Dictionary<string, AppearanceColor>(StringComparer.Ordinal);
            var coats = new Dictionary<string, PetCoatColors>(StringComparer.Ordinal);
            if (data.palette != null)
            {
                foreach (var eye in data.palette.eyes ?? Array.Empty<PetNamedColorData>())
                {
                    if (eye == null || string.IsNullOrEmpty(eye.name) || eyes.ContainsKey(eye.name))
                        throw new FormatException("The pet layout has an invalid or repeated eye color.");
                    eyes.Add(eye.name, ReadColor(eye.color, "eyes " + eye.name));
                }

                foreach (var coat in data.palette.coats ?? Array.Empty<PetCoatColorData>())
                {
                    if (coat == null || string.IsNullOrEmpty(coat.breed) || string.IsNullOrEmpty(coat.coat))
                        throw new FormatException("The pet layout has an invalid palette coat.");
                    var key = CoatKey(coat.breed, coat.coat);
                    if (coats.ContainsKey(key))
                        throw new FormatException("The pet layout repeats palette coat " + key + ".");
                    var main = ReadColor(coat.main, key + " main");
                    var secondary = ReadColor(coat.secondary, key + " secondary");
                    var darkColor = coat.dark == null ? secondary : ReadColor(coat.dark, key + " dark");
                    var eyeColor = coat.eyes != null && eyes.TryGetValue(coat.eyes, out var named) ? named : reference.Region(2);
                    coats.Add(key, new PetCoatColors(main, secondary, darkColor, eyeColor));
                }
            }

            return new PetLayout(reference, data.runtimeScale.young, data.runtimeScale.grown, poses, overlays, dark, coats, eyes);
        }

        public bool TryGetPoseSize(string pose, out int width, out int height)
        {
            if (pose != null && poses.TryGetValue(pose, out var entry))
            {
                width = entry.width;
                height = entry.height;
                return true;
            }

            width = 0;
            height = 0;
            return false;
        }

        public bool TryGetOverlay(string path, out AppearanceOverlay overlay)
        {
            if (path != null)
                return overlays.TryGetValue(path, out overlay);
            overlay = default(AppearanceOverlay);
            return false;
        }

        // A "dark" overlay is recolored on its own with the coat's dark color before it is composited.
        public bool IsDarkOverlay(string path) => path != null && darkOverlays.Contains(path);

        public bool TryGetCoat(PetBreed breed, PetCoat coat, out PetCoatColors colors) =>
            coats.TryGetValue(CoatKey(breed.ToString(), coat.ToString()), out colors);

        public bool TryGetEyes(string name, out AppearanceColor color)
        {
            if (name != null)
                return eyes.TryGetValue(name, out color);
            color = default(AppearanceColor);
            return false;
        }

        private static string CoatKey(string breed, string coat) => breed + "/" + coat;

        private static AppearanceColor ReadColor(double[] values, string name)
        {
            if (values == null || values.Length != 3)
                throw new FormatException("Pet color " + name + " must be [r, g, b].");
            for (int channel = 0; channel < 3; channel++)
                if (double.IsNaN(values[channel]) || values[channel] < 0 || values[channel] > 255)
                    throw new FormatException("Pet color " + name + " must use the 0-255 scale.");
            return new AppearanceColor(values[0], values[1], values[2]);
        }
    }
}
