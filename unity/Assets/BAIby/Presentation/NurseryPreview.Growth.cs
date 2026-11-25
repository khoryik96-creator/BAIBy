using System;
using BAIby.Simulation;

namespace BAIby.Presentation
{
    // Words for development (care slice 4): the projection of the next skill, track qualities, the growth chart band,
    // the setback and the reactions it brings. Warm and never blaming; no weights or body labels. A cat's milestones
    // (care slice 7: pounce, climb, high jump) share them.
    public sealed partial class NurseryPreview
    {
        private static string SkillGerund(MotorSkill skill)
        {
            switch (skill)
            {
                case MotorSkill.Sitting: return "sitting up";
                case MotorSkill.Crawling: return "crawling";
                case MotorSkill.Pounce: return "pouncing";
                case MotorSkill.Climb: return "climbing";
                case MotorSkill.HighJump: return "high jumps";
                case MotorSkill.Wheel: return "running on the wheel";
                case MotorSkill.Tunnel: return "the tunnel";
                case MotorSkill.HandTame: return "climbing onto your hand";
                case MotorSkill.Fetch: return "playing fetch";
                case MotorSkill.LeashWalk: return "walking on the lead";
                case MotorSkill.Agility: return "agility";
                case MotorSkill.DartsToGlass: return "darting to the glass";
                case MotorSkill.FollowsFinger: return "following your finger";
                case MotorSkill.HoopSwim: return "swimming through the hoop";
                case MotorSkill.FirstBinky: return "binkies";
                case MotorSkill.HopCircuit: return "hop circuits";
                case MotorSkill.FlopOfTrust: return "flopping beside you";
                default: return "walking";
            }
        }

        private static string SkillVerb(MotorSkill skill)
        {
            switch (skill)
            {
                case MotorSkill.Sitting: return "sit up";
                case MotorSkill.Crawling: return "crawl";
                case MotorSkill.Pounce: return "pounce";
                case MotorSkill.Climb: return "climb";
                case MotorSkill.HighJump: return "try a high jump";
                case MotorSkill.Wheel: return "run a whole spin on the wheel";
                case MotorSkill.Tunnel: return "scurry through the tunnel";
                case MotorSkill.HandTame: return "climb onto your hand";
                case MotorSkill.Fetch: return "bring the ball back";
                case MotorSkill.LeashWalk: return "trot nicely on the lead";
                case MotorSkill.Agility: return "try the little agility course";
                case MotorSkill.DartsToGlass: return "dart to the glass to say hello";
                case MotorSkill.FollowsFinger: return "follow your finger along the glass";
                case MotorSkill.HoopSwim: return "swim through the little hoop";
                case MotorSkill.FirstBinky: return "do a first binky";
                case MotorSkill.HopCircuit: return "hop a happy circuit of the pen";
                case MotorSkill.FlopOfTrust: return "flop over beside you";
                default: return "take first steps";
            }
        }

        // "Likely to crawl in about 2 days", or for a kitten "Likely to pounce in about 2 days".
        private static string ProjectionLine(string name, GrowthReport growth, bool pet = false)
        {
            if (growth == null)
                return string.Empty;
            if (growth.Next == MotorSkill.None && !pet && growth.Words != null)
                return growth.Words.Reached ? "Sitting, crawling, first steps and first words reached"
                    : growth.Paused ? "Growing waits while " + name + " is poorly"
                    : "Likely to say first words " + Soon(growth.Words.ExpectedIn);
            if (growth.Next == MotorSkill.None)
                return !pet ? "Sitting, crawling and first steps reached"
                    : HamsterShown ? "Wheel, tunnel and hand-tame reached"
                    : DogShown ? "Fetch, lead walking and agility reached"
                    : FishShown ? "Darting to the glass, following a finger and the hoop reached"
                    : RabbitShown ? "Binkies, hop circuits and the flop of trust reached"
                    : "Pouncing, climbing and high jumps reached";
            if (growth.Paused)
                return "Growing waits while " + name + " is poorly";

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

        // `meals` is how many on-time meals a good day asks for (three, two for a cat, or a hamster's one dinner).
        private static string ProjectionHint(string name, GrowthReport growth, bool pet = false, int meals = 3)
        {
            if (growth == null)
                return string.Empty;
            var cuddles = !pet ? "cuddles" : HamsterShown ? "holds" : DogShown ? "walks" : FishShown ? "calm moments"
                : RabbitShown ? "grooms or floor time" : "pets";
            if (growth.Shy != MotorSkill.None)
                return $"A bit shy about {SkillGerund(growth.Shy)}. A good day helps: meals on time " +
                    $"{Math.Min(meals, growth.RecoveryMeals)} of {meals}, {cuddles} {Math.Min(2, growth.RecoverySoothes)} of 2.";
            if (growth.Paused)
                return pet ? $"Meals, {Comforting} and {VetName} the vet will help." : "Feeds, cuddles and Dr. Lee will help.";
            if (growth.Next == MotorSkill.None && !pet && growth.Words != null)
                return growth.Words.Reached ? $"Talk time is open: tap Talk to tell {name} how you feel."
                    : "Stories and new sounds help with first words.";
            if (growth.Next == MotorSkill.None)
                return !pet ? "Play, stories and cuddles keep every track growing."
                    : HamsterShown ? "Wheel time, gentle holds and fresh bedding keep every track growing."
                    : DogShown ? "Walks, games of fetch and brushing keep every track growing."
                    : FishShown ? "Glass time, a clean glass and fresh water keep every track growing."
                    : RabbitShown ? "Floor time, grooming and a full hay rack keep every track growing."
                    : "Wand play, petting and brushing keep every track growing.";

            switch (growth.Pace)
            {
                case GrowthPace.TakingTime:
                    return $"{name} is taking their time with {SkillGerund(growth.Next)}. {Helps(growth.Next)}";
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
                case MotorSkill.Pounce:
                case MotorSkill.Climb: return "Wand play at playtime helps.";
                case MotorSkill.HighJump: return "Wand play and meals on time help.";
                case MotorSkill.Wheel:
                case MotorSkill.Tunnel: return "Wheel time in the evening helps.";
                case MotorSkill.HandTame: return "Gentle holds and dinner on time help.";
                case MotorSkill.Fetch: return "Games of fetch help.";
                case MotorSkill.LeashWalk: return "Walks on time help.";
                case MotorSkill.Agility: return "Walks, fetch and meals on time help.";
                case MotorSkill.DartsToGlass: return "Saying hello at the glass helps.";
                case MotorSkill.FollowsFinger: return "Glass time after school helps.";
                case MotorSkill.HoopSwim: return "Glass time and food on time help.";
                case MotorSkill.FirstBinky: return "Floor time with a happy, well-fed bunny helps.";
                case MotorSkill.HopCircuit: return "Floor time at dawn and after school helps.";
                case MotorSkill.FlopOfTrust: return "Sitting on the floor together and greens on time help.";
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
