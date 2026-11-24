using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // Pure formulas for development tracks (docs/care-and-growth.md, Development tracks and Milestone timing).
    // Species-agnostic: a species maps its actions to EngagementKind and its milestones to stage lengths.
    //
    // - Wellbeing W: per need s = clamp((x - 20) / 40, 0, 1); W = 0.5 min(s) + 0.5 mean(s).
    // - Quality per track, from W, the meal ring's credit, Bond (0 to 1), engagement E and Health (0 to 1):
    //   Motor 0.40 W + 0.20 meals + 0.10 Bond + 0.30 E; Learning 0.35 W + 0.10 meals + 0.20 Bond + 0.35 E;
    //   Social 0.30 W + 0.10 meals + 0.35 Bond + 0.25 E; Physical 0.30 W + 0.50 meals + 0.20 Health.
    // - Engagement: each action earns base x 1/(1 + 0.5 n) x (0.4 + 0.6 W) points (n repeats of the same kind in the
    //   repeat window, x1.5 for a toy's first use in a stage). E = min(1, points in the engagement window / target),
    //   with one kind supplying at most KindCap points.
    // - Readiness: dp/dt = cond / (D x Pace(Q)), Pace(Q) = 1.30 - 0.50 Q, so a steady quality lands a milestone
    //   between 0.8x and 1.3x of its stage length D.
    public static class DevelopmentRules
    {
        public const double FastestPace = 0.8;
        public const double SlowestPace = 1.3;
        private const int KindCount = 11;

        // The multiple of a stage's length that readiness takes at a steady quality.
        public static double Pace(double quality) => SlowestPace - (SlowestPace - FastestPace) * Clamp01(quality);

        // The quality that keeps the nominal pace (Pace 1.0).
        public static double NominalQuality => (SlowestPace - 1) / (SlowestPace - FastestPace);

        public static double NeedScore(double need) => Clamp01((need - 20) / 40);

        public static double Wellbeing(double fullness, double comfort, double energy, double cleanliness)
        {
            var a = NeedScore(fullness);
            var b = NeedScore(comfort);
            var c = NeedScore(energy);
            var d = NeedScore(cleanliness);
            var lowest = Math.Min(Math.Min(a, b), Math.Min(c, d));
            return Clamp01(0.5 * lowest + 0.5 * (a + b + c + d) / 4);
        }

        public static double Quality(DevelopmentTrack track, double wellbeing, double meals, double bond, double engagement,
            double health)
        {
            var w = Clamp01(wellbeing);
            var m = Clamp01(meals);
            var b = Clamp01(bond);
            var e = Clamp01(engagement);
            switch (track)
            {
                case DevelopmentTrack.Motor: return Clamp01(0.40 * w + 0.20 * m + 0.10 * b + 0.30 * e);
                case DevelopmentTrack.Learning: return Clamp01(0.35 * w + 0.10 * m + 0.20 * b + 0.35 * e);
                case DevelopmentTrack.Social: return Clamp01(0.30 * w + 0.10 * m + 0.35 * b + 0.25 * e);
                case DevelopmentTrack.Physical: return Clamp01(0.30 * w + 0.50 * m + 0.20 * Clamp01(health));
                default: throw new ArgumentOutOfRangeException(nameof(track));
            }
        }

        // A full sample from the inputs at one instant.
        public static TrackSample Sample(SampleMode mode, double wellbeing, double meals, double bond, double health,
            IReadOnlyList<EngagementEntry> engagement, long at, GrowthTunables tunables)
        {
            var motor = Engagement(engagement, DevelopmentTrack.Motor, at, tunables);
            var learning = Engagement(engagement, DevelopmentTrack.Learning, at, tunables);
            var social = Engagement(engagement, DevelopmentTrack.Social, at, tunables);
            return new TrackSample(mode, Clamp01(wellbeing),
                Quality(DevelopmentTrack.Motor, wellbeing, meals, bond, motor, health),
                Quality(DevelopmentTrack.Learning, wellbeing, meals, bond, learning, health),
                Quality(DevelopmentTrack.Social, wellbeing, meals, bond, social, health),
                Quality(DevelopmentTrack.Physical, wellbeing, meals, bond, 0, health));
        }

        // Points an action of this kind earns a track before repeats, wellbeing and novelty.
        // Play feeds Motor; stories and new sounds feed Learning; blocks feed both; soothing feeds Social; on-time
        // meals and hygiene (a cat's brush too) give Social a little. A cat's wand play feeds Motor. The day's first
        // Talk time feeds Learning and Social.
        public static double BasePoints(EngagementKind kind, DevelopmentTrack track)
        {
            switch (kind)
            {
                case EngagementKind.PlayRattle:
                case EngagementKind.PlayPlush:
                case EngagementKind.PlayWand:
                    return track == DevelopmentTrack.Motor ? 4 : 0;
                case EngagementKind.Story:
                    return track == DevelopmentTrack.Learning ? 5 : 0;
                case EngagementKind.Blocks:
                    return track == DevelopmentTrack.Motor || track == DevelopmentTrack.Learning ? 3 : 0;
                case EngagementKind.Soothe:
                    return track == DevelopmentTrack.Social ? 3 : 0;
                case EngagementKind.Meal:
                case EngagementKind.Hygiene:
                    return track == DevelopmentTrack.Social ? 1 : 0;
                case EngagementKind.ComfortSound:
                    return track == DevelopmentTrack.Learning ? 3 : track == DevelopmentTrack.Social ? 2 : 0;
                case EngagementKind.ExploreSound:
                    return track == DevelopmentTrack.Learning ? 4 : 0;
                case EngagementKind.Talk:
                    return track == DevelopmentTrack.Learning ? 3 : track == DevelopmentTrack.Social ? 3 : 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        // Entries of the same kind within the repeat window before `at`.
        public static int Repeats(IReadOnlyList<EngagementEntry> engagement, EngagementKind kind, long at, GrowthTunables tunables)
        {
            var since = at - tunables.RepeatWindow.Ticks;
            var repeats = 0;
            foreach (var entry in engagement)
            {
                if (entry.Kind == kind && entry.At > since && entry.At <= at)
                    repeats++;
            }

            return repeats;
        }

        // The points multiplier of one action: diminishing with repeats, smaller when the baby is not well kept.
        public static double Factor(int repeats, double wellbeing, bool novel, GrowthTunables tunables) =>
            1.0 / (1 + 0.5 * Math.Max(0, repeats)) * (0.4 + 0.6 * Clamp01(wellbeing)) * (novel ? tunables.NoveltyBonus : 1);

        // E for a track at `at`: points in the engagement window, each kind capped, over the target, at most 1.
        public static double Engagement(IReadOnlyList<EngagementEntry> engagement, DevelopmentTrack track, long at,
            GrowthTunables tunables)
        {
            if (engagement == null || engagement.Count == 0)
                return 0;

            var since = at - tunables.EngagementWindow.Ticks;
            var perKind = new double[KindCount];
            foreach (var entry in engagement)
            {
                if (entry.At > since && entry.At <= at)
                    perKind[(int)entry.Kind] += BasePoints(entry.Kind, track) * entry.Factor;
            }

            var total = 0.0;
            foreach (var points in perKind)
                total += Math.Min(tunables.KindCap, points);

            return Math.Min(1, total / tunables.EngagementTarget);
        }

        // Drops entries that can no longer count at or after `at`, then the oldest beyond the record's capacity.
        public static List<EngagementEntry> Prune(IEnumerable<EngagementEntry> engagement, long at, GrowthTunables tunables)
        {
            var since = at - tunables.EngagementWindow.Ticks;
            var kept = new List<EngagementEntry>();
            foreach (var entry in engagement)
            {
                if (entry.At > since)
                    kept.Add(entry);
            }

            if (kept.Count > DevelopmentRecord.MaxEntries)
                kept.RemoveRange(0, kept.Count - DevelopmentRecord.MaxEntries);
            return kept;
        }

        internal static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
    }
}
