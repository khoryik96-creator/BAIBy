using System;

namespace BAIby.Simulation
{
    // A few looks are earned with a care rhythm, never sold (docs/care-and-growth.md, Earned collections): the Daisy
    // clip at 3 days, the Midnight outfit at 7 and the Bucket hat at 14. Earned for good once the longest rhythm reaches
    // them. A look already worn stays wearable, so an older save never loses what it has on.
    public static class EarnedLooks
    {
        public static int RhythmFor(Headwear headwear)
        {
            switch (headwear)
            {
                case Headwear.DaisyClip: return 3;
                case Headwear.BucketHat: return 14;
                default: return 0;
            }
        }

        public static int RhythmFor(Outfit outfit) => outfit == Outfit.Midnight ? 7 : 0;

        // Whether the look can be put on: every earned part is earned, or already worn.
        public static bool CanWear(BabyAppearance current, BabyAppearance next, int longestRhythm)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));

            current = current ?? BabyAppearance.Default;
            return (next.Headwear == current.Headwear || RhythmFor(next.Headwear) <= longestRhythm) &&
                (next.Outfit == current.Outfit || RhythmFor(next.Outfit) <= longestRhythm);
        }
    }
}
