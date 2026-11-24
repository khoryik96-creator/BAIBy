using System;
using BAIby.Simulation;

namespace BAIby.Presentation
{
    // Words for development (care slice 4): the projection of the next skill, track qualities, the growth chart band,
    // the setback and the reactions it brings. Warm and never blaming; no weights or body labels.
    public sealed partial class NurseryPreview
    {
        private static string SkillGerund(MotorSkill skill)
        {
            switch (skill)
            {
                case MotorSkill.Sitting: return "sitting up";
                case MotorSkill.Crawling: return "crawling";
                default: return "walking";
            }
        }

        private static string SkillVerb(MotorSkill skill)
        {
            switch (skill)
            {
                case MotorSkill.Sitting: return "sit up";
                case MotorSkill.Crawling: return "crawl";
                default: return "take first steps";
            }
        }

        // "Likely to crawl in about 2 days".
        private static string ProjectionLine(BabyState state, GrowthReport growth)
        {
            if (growth == null)
                return string.Empty;
            if (growth.Next == MotorSkill.None)
                return "Sitting, crawling and first steps reached";
            if (growth.Paused)
                return "Growing waits while " + state.Name + " is poorly";

            return "Likely to " + SkillVerb(growth.Next) + " " + Soon(growth.ExpectedIn);
        }

        private static string Soon(TimeSpan expected)
        {
            var days = expected.TotalDays;
            if (days < 1)
                return "within a day";
            if (days < 1.5)
                return "in about a day";

            return $"in about {Math.Round(days):0} days";
        }

        private static string ProjectionHint(BabyState state, GrowthReport growth)
        {
            if (growth == null)
                return string.Empty;
            if (growth.Shy != MotorSkill.None)
                return $"A bit shy about {SkillGerund(growth.Shy)}. A good day helps: meals on time " +
                    $"{Math.Min(3, growth.RecoveryMeals)} of 3, cuddles {Math.Min(2, growth.RecoverySoothes)} of 2.";
            if (growth.Paused)
                return "Feeds, cuddles and Dr. Lee will help.";
            if (growth.Next == MotorSkill.None)
                return "Play, stories and cuddles keep every track growing.";

            switch (growth.Pace)
            {
                case GrowthPace.TakingTime:
                    return $"{state.Name} is taking their time with {SkillGerund(growth.Next)}. {Helps(growth.Next)}";
                case GrowthPace.Ahead:
                    return "Lots of play and on-time meals are paying off.";
                default:
                    return "Meals on time, play and cuddles keep it coming.";
            }
        }

        private static string Helps(MotorSkill skill)
        {
            switch (skill)
            {
                case MotorSkill.Sitting: return "Play and cuddles help.";
                case MotorSkill.Crawling: return "Extra floor play helps.";
                default: return "Play and regular meals help.";
            }
        }

        private static string PaceName(GrowthPace pace)
        {
            switch (pace)
            {
                case GrowthPace.Ahead: return "Ahead";
                case GrowthPace.TakingTime: return "Taking their time";
                default: return "On track";
            }
        }

        private static string TrackName(DevelopmentTrack track)
        {
            switch (track)
            {
                case DevelopmentTrack.Motor: return "Moving";
                case DevelopmentTrack.Learning: return "Learning";
                case DevelopmentTrack.Social: return "Bonding";
                default: return "Growing";
            }
        }

        // A word for a track's quality, or what helps when it is low.
        private static string TrackWord(DevelopmentTrack track, double quality)
        {
            if (quality >= 0.75)
                return "Flourishing";
            if (quality >= 0.5)
                return "Steady";

            switch (track)
            {
                case DevelopmentTrack.Motor: return "Play helps";
                case DevelopmentTrack.Learning: return "Stories help";
                case DevelopmentTrack.Social: return "Cuddles help";
                default: return "Meals help";
            }
        }

        private static string BandName(GrowthBand band)
        {
            switch (band)
            {
                case GrowthBand.Thriving: return "Thriving";
                case GrowthBand.Steady: return "Steady";
                default: return "Needs regular meals";
            }
        }

        // A milestone line in the report: reached (or a bit shy), likely soon for the next one, or not yet.
        private static string MilestoneStatus(MotorSkill skill, bool reached, GrowthReport growth)
        {
            if (reached)
                return growth != null && growth.Shy == skill ? "A bit shy" : "Unlocked";
            if (growth != null && growth.Next == skill)
                return growth.Paused ? "Resting" : "Likely " + Soon(growth.ExpectedIn);

            return "Locked";
        }

        // The setback, while it lasts: the newest skill is shy and the baby shows the stage before it.
        private static string DescribeShy(BabyState state)
        {
            switch (state.ShySkill)
            {
                case MotorSkill.Walking:
                    return $"{state.Name} feels a little shy about walking and crawls to toys for now. " +
                        "A good day of meals on time and cuddles will help.";
                case MotorSkill.Crawling:
                    return $"{state.Name} feels a little shy about crawling and sits to play for now. " +
                        "A good day of meals on time and cuddles will help.";
                case MotorSkill.Sitting:
                    return $"{state.Name} feels a little shy about sitting up and lies back for now. " +
                        "A good day of meals on time and cuddles will help.";
                default:
                    return null;
            }
        }

        private static string DescribeGrowthReaction(BabyState state, Reaction reaction)
        {
            switch (reaction)
            {
                case Reaction.Bored:
                    return $"{state.Name} looks a little bored of that. Try something different!";
                case Reaction.SkillBack:
                    return $"{state.Name} is {SkillGerund(Newest(state))} again! A good day of meals and cuddles helped.";
                default:
                    return null;
            }
        }

        // The reaction the nursery plays: the walking-again celebration shows the skill itself.
        private static Reaction VisualReaction(BabyState state, Reaction reaction)
        {
            if (state == null || reaction != Reaction.SkillBack)
                return reaction;

            switch (Newest(state))
            {
                case MotorSkill.Walking: return Reaction.WalksToToy;
                case MotorSkill.Crawling: return Reaction.CrawlsToToy;
                default: return Reaction.ReachesForToy;
            }
        }

        private static MotorSkill Newest(BabyState state) =>
            state.CanWalk ? MotorSkill.Walking : state.CanCrawl ? MotorSkill.Crawling :
            state.CanSitAndReach ? MotorSkill.Sitting : MotorSkill.None;
    }
}
