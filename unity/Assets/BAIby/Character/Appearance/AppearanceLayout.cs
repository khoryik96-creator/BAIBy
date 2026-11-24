using System;
using System.Collections.Generic;
using BAIby.Simulation;

namespace BAIby.Character
{
    // JsonUtility shapes for Resources/BAIby/Baby/Appearance/layout.json. They stay [Serializable]
    // classes with public fields (JsonUtility ignores properties), and link.xml preserves them from
    // managed code stripping because only Unity's native serializer writes these fields.
    [Serializable]
    public sealed class AppearanceLayoutData
    {
        public AppearanceReferenceData reference;
        public AppearancePoseData[] poses;
        public AppearanceOverlayData[] overlays;
    }

    [Serializable]
    public sealed class AppearanceReferenceData
    {
        public double[] skin;
        public double[] hair;
        public double[] outfitMain;
        public double[] outfitAccent;
    }

    [Serializable]
    public sealed class AppearancePoseData
    {
        public string name;
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class AppearanceOverlayData
    {
        public string path;
        public string pose;
        // Top-left corner in pose pixels, y measured from the TOP of the pose (PIL convention).
        public int x;
        public int y;
        public int width;
        public int height;
    }

    public readonly struct AppearanceOverlay
    {
        public AppearanceOverlay(string path, string pose, int x, int yFromTop, int width, int height)
        {
            Path = path;
            Pose = pose;
            X = x;
            YFromTop = yFromTop;
            Width = width;
            Height = height;
        }

        public string Path { get; }
        public string Pose { get; }
        public int X { get; }
        public int YFromTop { get; }
        public int Width { get; }
        public int Height { get; }
    }

    // Validated layout plus the resource naming rules for the appearance art. Resource paths are
    // relative to Resources/BAIby/Baby/Appearance/ and omit the ".bytes" extension.
    public sealed class AppearanceLayout
    {
        public const string RegionsSuffix = "Regions";

        // Pose illustration names, matching Resources/BAIby/Baby/{Pose}.png.
        public static readonly IReadOnlyList<string> PoseNames = new[]
        {
            "Newborn", "NewbornRest", "Sitter", "SitterRest", "Crawler", "CrawlerRest", "Walker", "WalkerRest",
        };

        private readonly Dictionary<string, AppearancePoseData> poses;
        private readonly Dictionary<string, AppearanceOverlay> overlays;

        private AppearanceLayout(AppearanceColors reference, Dictionary<string, AppearancePoseData> poses,
            Dictionary<string, AppearanceOverlay> overlays)
        {
            Reference = reference;
            this.poses = poses;
            this.overlays = overlays;
        }

        public AppearanceColors Reference { get; }

        public static AppearanceLayout FromData(AppearanceLayoutData data)
        {
            if (data == null)
                throw new FormatException("The appearance layout is empty.");
            if (data.reference == null)
                throw new FormatException("The appearance layout has no reference colors.");

            var reference = new AppearanceColors(
                ReadColor(data.reference.skin, "skin"),
                ReadColor(data.reference.hair, "hair"),
                ReadColor(data.reference.outfitMain, "outfitMain"),
                ReadColor(data.reference.outfitAccent, "outfitAccent"));

            var poses = new Dictionary<string, AppearancePoseData>(StringComparer.Ordinal);
            foreach (var pose in data.poses ?? Array.Empty<AppearancePoseData>())
            {
                if (pose == null || string.IsNullOrEmpty(pose.name) || pose.width <= 0 || pose.height <= 0)
                    throw new FormatException("The appearance layout has an invalid pose entry.");
                if (poses.ContainsKey(pose.name))
                    throw new FormatException("The appearance layout repeats pose " + pose.name + ".");
                poses.Add(pose.name, pose);
            }

            var overlays = new Dictionary<string, AppearanceOverlay>(StringComparer.Ordinal);
            foreach (var overlay in data.overlays ?? Array.Empty<AppearanceOverlayData>())
            {
                if (overlay == null || string.IsNullOrEmpty(overlay.path) || overlay.width <= 0 || overlay.height <= 0)
                    throw new FormatException("The appearance layout has an invalid overlay entry.");
                if (overlay.pose == null || !poses.ContainsKey(overlay.pose))
                    throw new FormatException("Overlay " + overlay.path + " names an unknown pose.");
                if (overlays.ContainsKey(overlay.path))
                    throw new FormatException("The appearance layout repeats overlay " + overlay.path + ".");
                overlays.Add(overlay.path, new AppearanceOverlay(
                    overlay.path, overlay.pose, overlay.x, overlay.y, overlay.width, overlay.height));
            }

            return new AppearanceLayout(reference, poses, overlays);
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

        public static string BasePath(string pose) => "Base/" + pose;

        public static string RegionsPath(string imagePath) => imagePath + RegionsSuffix;

        public static string HairPath(HairStyle style, string pose) => "Hair/" + StyleName(style) + "/" + pose;

        // Null when the look has no headwear.
        public static string HeadwearPath(Headwear headwear, string pose)
        {
            var kind = HeadwearName(headwear);
            return kind == null ? null : "Headwear/" + kind + "/" + pose;
        }

        // Explicit names instead of Enum.ToString keep the folder names independent of enum renames.
        public static string StyleName(HairStyle style)
        {
            switch (style)
            {
                case HairStyle.Curls: return "Curls";
                case HairStyle.Coils: return "Coils";
                case HairStyle.Smooth: return "Smooth";
                case HairStyle.Tuft: return "Tuft";
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
        }

        public static string HeadwearName(Headwear headwear)
        {
            switch (headwear)
            {
                case Headwear.None: return null;
                case Headwear.Bow: return "Bow";
                case Headwear.BearBeanie: return "BearBeanie";
                case Headwear.BucketHat: return "BucketHat";
                case Headwear.DaisyClip: return "DaisyClip";
                default: throw new ArgumentOutOfRangeException(nameof(headwear));
            }
        }

        private static AppearanceColor ReadColor(double[] values, string name)
        {
            if (values == null || values.Length != 3)
                throw new FormatException("Reference color " + name + " must be [r, g, b].");
            for (int channel = 0; channel < 3; channel++)
                if (double.IsNaN(values[channel]) || values[channel] < 0 || values[channel] > 255)
                    throw new FormatException("Reference color " + name + " must use the 0-255 scale.");
            return new AppearanceColor(values[0], values[1], values[2]);
        }
    }
}
