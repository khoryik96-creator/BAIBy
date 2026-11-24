using System;

namespace BAIby.Simulation
{
    // Saved values are stable content IDs. Append new options; never renumber.
    public enum SkinTone { Warm = 0, Fair = 1, Light = 2, Tan = 3, Brown = 4, Deep = 5 }
    public enum HairColor { Chestnut = 0, Black = 1, Espresso = 2, Auburn = 3, Honey = 4, Platinum = 5 }
    public enum HairStyle { Curls = 0, Coils = 1, Smooth = 2, Tuft = 3 }
    public enum Outfit
    {
        Sage = 0, Oat = 1, Sky = 2, Blush = 3, Butter = 4, Lilac = 5, Terracotta = 6, Midnight = 7,
        // Look packs (Application/Entitlements/CosmeticCatalog): Cozy knits, Garden party, then the Founder pack.
        OatmealKnit = 8, BerryKnit = 9, MossKnit = 10, FogKnit = 11,
        Rosebud = 12, Marigold = 13, GardenMint = 14, Bluebell = 15,
        FernGold = 16
    }
    public enum Headwear { None = 0, Bow = 1, BearBeanie = 2, BucketHat = 3, DaisyClip = 4, PomBeanie = 5, SunHat = 6 }

    // The baby's chosen look. It has no effect on care rules or growth.
    public sealed class BabyAppearance : IEquatable<BabyAppearance>
    {
        public static readonly BabyAppearance Default =
            new BabyAppearance(SkinTone.Warm, HairColor.Chestnut, HairStyle.Curls, Outfit.Sage, Headwear.None);

        public SkinTone SkinTone { get; }
        public HairColor HairColor { get; }
        public HairStyle HairStyle { get; }
        public Outfit Outfit { get; }
        public Headwear Headwear { get; }

        public BabyAppearance(SkinTone skinTone, HairColor hairColor, HairStyle hairStyle, Outfit outfit, Headwear headwear)
        {
            if (!Enum.IsDefined(typeof(SkinTone), skinTone))
                throw new ArgumentOutOfRangeException(nameof(skinTone));
            if (!Enum.IsDefined(typeof(HairColor), hairColor))
                throw new ArgumentOutOfRangeException(nameof(hairColor));
            if (!Enum.IsDefined(typeof(HairStyle), hairStyle))
                throw new ArgumentOutOfRangeException(nameof(hairStyle));
            if (!Enum.IsDefined(typeof(Outfit), outfit))
                throw new ArgumentOutOfRangeException(nameof(outfit));
            if (!Enum.IsDefined(typeof(Headwear), headwear))
                throw new ArgumentOutOfRangeException(nameof(headwear));

            SkinTone = skinTone;
            HairColor = hairColor;
            HairStyle = hairStyle;
            Outfit = outfit;
            Headwear = headwear;
        }

        public BabyAppearance WithSkinTone(SkinTone value) => new BabyAppearance(value, HairColor, HairStyle, Outfit, Headwear);
        public BabyAppearance WithHairColor(HairColor value) => new BabyAppearance(SkinTone, value, HairStyle, Outfit, Headwear);
        public BabyAppearance WithHairStyle(HairStyle value) => new BabyAppearance(SkinTone, HairColor, value, Outfit, Headwear);
        public BabyAppearance WithOutfit(Outfit value) => new BabyAppearance(SkinTone, HairColor, HairStyle, value, Headwear);
        public BabyAppearance WithHeadwear(Headwear value) => new BabyAppearance(SkinTone, HairColor, HairStyle, Outfit, value);

        public bool Equals(BabyAppearance other) =>
            other != null && SkinTone == other.SkinTone && HairColor == other.HairColor &&
            HairStyle == other.HairStyle && Outfit == other.Outfit && Headwear == other.Headwear;

        public override bool Equals(object obj) => Equals(obj as BabyAppearance);

        public override int GetHashCode() =>
            (int)SkinTone | (int)HairColor << 4 | (int)HairStyle << 8 | (int)Outfit << 12 | (int)Headwear << 16;
    }
}
