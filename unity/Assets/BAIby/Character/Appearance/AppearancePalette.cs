using System;
using BAIby.Simulation;

namespace BAIby.Character
{
    // An sRGB color on the 0-255 scale. Channels are doubles so a measured reference such as
    // 201.5 survives unchanged from layout.json; the palette targets themselves are whole numbers.
    public readonly struct AppearanceColor : IEquatable<AppearanceColor>
    {
        public AppearanceColor(double r, double g, double b)
        {
            R = Check(r, nameof(r));
            G = Check(g, nameof(g));
            B = Check(b, nameof(b));
        }

        public double R { get; }
        public double G { get; }
        public double B { get; }

        public double this[int channel]
        {
            get
            {
                switch (channel)
                {
                    case 0: return R;
                    case 1: return G;
                    case 2: return B;
                    default: throw new ArgumentOutOfRangeException(nameof(channel));
                }
            }
        }

        public bool Equals(AppearanceColor other) => R == other.R && G == other.G && B == other.B;

        public override bool Equals(object obj) => obj is AppearanceColor other && Equals(other);

        public override int GetHashCode() => R.GetHashCode() ^ G.GetHashCode() << 1 ^ B.GetHashCode() << 2;

        public override string ToString() => "(" + R + ", " + G + ", " + B + ")";

        private static double Check(double value, string name)
        {
            if (double.IsNaN(value) || value < 0 || value > 255)
                throw new ArgumentOutOfRangeException(name, "Colors use the 0-255 sRGB scale.");
            return value;
        }
    }

    // One color per recolorable region, in region-map channel order:
    // 0 = skin (R), 1 = hair (G), 2 = outfit main (B), 3 = outfit accent (A).
    public sealed class AppearanceColors : IEquatable<AppearanceColors>
    {
        public const int RegionCount = 4;

        public AppearanceColors(AppearanceColor skin, AppearanceColor hair, AppearanceColor outfitMain, AppearanceColor outfitAccent)
        {
            Skin = skin;
            Hair = hair;
            OutfitMain = outfitMain;
            OutfitAccent = outfitAccent;
        }

        public AppearanceColor Skin { get; }
        public AppearanceColor Hair { get; }
        public AppearanceColor OutfitMain { get; }
        public AppearanceColor OutfitAccent { get; }

        public AppearanceColor Region(int region)
        {
            switch (region)
            {
                case 0: return Skin;
                case 1: return Hair;
                case 2: return OutfitMain;
                case 3: return OutfitAccent;
                default: throw new ArgumentOutOfRangeException(nameof(region));
            }
        }

        public bool Equals(AppearanceColors other) =>
            other != null && Skin.Equals(other.Skin) && Hair.Equals(other.Hair) &&
            OutfitMain.Equals(other.OutfitMain) && OutfitAccent.Equals(other.OutfitAccent);

        public override bool Equals(object obj) => Equals(obj as AppearanceColors);

        public override int GetHashCode() =>
            Skin.GetHashCode() ^ Hair.GetHashCode() << 3 ^ OutfitMain.GetHashCode() << 5 ^ OutfitAccent.GetHashCode() << 7;
    }

    // Target colors for every look option. The identity options (Warm, Chestnut, Sage) return the
    // measured reference color unchanged, so recoloring with them reproduces the original art exactly.
    // Keep this table identical to tools/appearance (the Python preview) and the shared contract.
    public static class AppearancePalette
    {
        public static AppearanceColor Skin(SkinTone tone, AppearanceColor reference)
        {
            switch (tone)
            {
                case SkinTone.Warm: return reference;
                case SkinTone.Fair: return new AppearanceColor(248, 218, 196);
                case SkinTone.Light: return new AppearanceColor(238, 192, 154);
                case SkinTone.Tan: return new AppearanceColor(196, 128, 82);
                case SkinTone.Brown: return new AppearanceColor(156, 96, 60);
                case SkinTone.Deep: return new AppearanceColor(104, 62, 40);
                default: throw new ArgumentOutOfRangeException(nameof(tone));
            }
        }

        public static AppearanceColor Hair(HairColor color, AppearanceColor reference)
        {
            switch (color)
            {
                case HairColor.Chestnut: return reference;
                case HairColor.Black: return new AppearanceColor(46, 36, 34);
                case HairColor.Espresso: return new AppearanceColor(82, 54, 40);
                case HairColor.Auburn: return new AppearanceColor(150, 70, 40);
                case HairColor.Honey: return new AppearanceColor(200, 146, 80);
                case HairColor.Platinum: return new AppearanceColor(232, 214, 170);
                default: throw new ArgumentOutOfRangeException(nameof(color));
            }
        }

        public static AppearanceColor OutfitMain(Outfit outfit, AppearanceColor reference)
        {
            switch (outfit)
            {
                case Outfit.Sage: return reference;
                case Outfit.Oat: return new AppearanceColor(226, 206, 178);
                case Outfit.Sky: return new AppearanceColor(236, 240, 242);
                case Outfit.Blush: return new AppearanceColor(246, 226, 220);
                case Outfit.Butter: return new AppearanceColor(248, 236, 196);
                case Outfit.Lilac: return new AppearanceColor(236, 228, 244);
                case Outfit.Terracotta: return new AppearanceColor(240, 222, 200);
                case Outfit.Midnight: return new AppearanceColor(70, 82, 110);
                default: throw new ArgumentOutOfRangeException(nameof(outfit));
            }
        }

        public static AppearanceColor OutfitAccent(Outfit outfit, AppearanceColor reference)
        {
            switch (outfit)
            {
                case Outfit.Sage: return reference;
                case Outfit.Oat: return new AppearanceColor(150, 112, 86);
                case Outfit.Sky: return new AppearanceColor(138, 170, 196);
                case Outfit.Blush: return new AppearanceColor(212, 150, 150);
                case Outfit.Butter: return new AppearanceColor(226, 190, 96);
                case Outfit.Lilac: return new AppearanceColor(170, 150, 200);
                case Outfit.Terracotta: return new AppearanceColor(196, 116, 84);
                case Outfit.Midnight: return new AppearanceColor(226, 190, 120);
                default: throw new ArgumentOutOfRangeException(nameof(outfit));
            }
        }

        public static AppearanceColors Targets(BabyAppearance appearance, AppearanceColors reference)
        {
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            return new AppearanceColors(
                Skin(appearance.SkinTone, reference.Skin),
                Hair(appearance.HairColor, reference.Hair),
                OutfitMain(appearance.Outfit, reference.OutfitMain),
                OutfitAccent(appearance.Outfit, reference.OutfitAccent));
        }
    }
}
