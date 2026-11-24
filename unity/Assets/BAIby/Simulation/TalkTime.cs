using System;

namespace BAIby.Simulation
{
    // A feeling the child shares in Talk time. Saved in journal talk lines as numbers: append, never renumber.
    public enum Feeling { Happy = 0, Sad = 1, Worried = 2, Angry = 3, Tired = 4, Proud = 5, Lonely = 6, Excited = 7 }

    // What typed words were about, read on the device. It refines the reply; it is never saved today, but it may be one
    // day (only the tag, never the text), so it is numbered: append, never renumber.
    public enum TalkTopic { None = 0, School = 1, Friend = 2, Family = 3, Sleep = 4, Sick = 5, Pet = 6, Birthday = 7, Game = 8 }

    // How the little one speaks: one or two words and a gesture right after First words, short toddler sentences from
    // about day 21 or Bond 60, and (reserved for a future Child or Teen stage with its own art) longer replies. A tier
    // without lines of its own uses the highest tier below it that has them.
    public enum TalkTier { FirstWords = 1, Toddler = 2, Child = 3 }

    // What the little one does while replying, reusing the nursery's reactions: a cuddle for sad and lonely, a bounce for
    // happy, proud and excited, and a calm sit for worried, angry and tired.
    public enum TalkGesture { Cuddle, Bounce, CalmSit }

    // How a talk went: a reply, the safety net (a fixed calm message and nothing else), or no feeling to answer yet.
    public enum TalkOutcome { Reply, SafetyNet, NeedsFeeling }

    // The little one's answer. Holds the feeling and topic tag, never what was typed.
    public sealed class TalkReply
    {
        // Shown in place of a reply when typed words are about self-harm, wanting to die, being hurt or abused, or being
        // unsafe. Never changed by the game, never followed by banter in that turn.
        public const string SafetyMessage = "That sounds really hard, and it matters. Please tell a grown-up you trust " +
            "right now, like a parent, teacher or carer. If you might be in danger, call your local emergency number.";

        public TalkOutcome Outcome { get; }
        public Feeling? Feeling { get; }
        public TalkTopic Topic { get; }
        public TalkTier Tier { get; }
        // What the little one says (the safety message for the safety net, empty when a feeling is needed).
        public string Words { get; }
        public TalkGesture Gesture { get; }
        // Whether this was the day's first talk, which counts as together time (see TalkRules).
        public bool Counted { get; }

        internal TalkReply(TalkOutcome outcome, Feeling? feeling, TalkTopic topic, TalkTier tier, string words,
            TalkGesture gesture, bool counted)
        {
            Outcome = outcome;
            Feeling = feeling;
            Topic = topic;
            Tier = tier;
            Words = words ?? string.Empty;
            Gesture = gesture;
            Counted = counted;
        }

        internal static TalkReply SafetyNet(TalkTier tier) =>
            new TalkReply(TalkOutcome.SafetyNet, null, TalkTopic.None, tier, SafetyMessage, TalkGesture.CalmSit, false);

        internal static TalkReply NeedsFeeling(TalkTier tier, TalkTopic topic) =>
            new TalkReply(TalkOutcome.NeedsFeeling, null, topic, tier, string.Empty, TalkGesture.CalmSit, false);
    }

    // Talk time (docs/care-and-growth.md, First words and Talk time): when it opens, which tier the little one speaks in,
    // the gesture for a feeling and which curated line is said. Deterministic: the line is picked by the little one's
    // day and the number of talks so far, never at random.
    public static class TalkRules
    {
        // The typed field holds at most this many characters (the interface limit; the matcher reads any length).
        public const int MaxTyped = 140;
        // Toddler sentences from this age, or from this Bond, whichever comes first.
        public static readonly TimeSpan ToddlerAge = TimeSpan.FromDays(21);
        public const double ToddlerBond = 60;
        // Bond for the day's first talk, within the daily gain cap.
        public const double TalkBond = 2;

        public static bool IsOpen(BabyState state) => state != null && state.HasFirstWords;

        // The tier the little one speaks in now. The Child tier needs a stage that is not in the game yet.
        public static TalkTier TierOf(TimeSpan age, double bond) =>
            age >= ToddlerAge || bond >= ToddlerBond ? TalkTier.Toddler : TalkTier.FirstWords;

        public static TalkGesture GestureOf(Feeling feeling)
        {
            switch (feeling)
            {
                case Feeling.Sad:
                case Feeling.Lonely: return TalkGesture.Cuddle;
                case Feeling.Happy:
                case Feeling.Proud:
                case Feeling.Excited: return TalkGesture.Bounce;
                case Feeling.Worried:
                case Feeling.Angry:
                case Feeling.Tired: return TalkGesture.CalmSit;
                default: throw new ArgumentOutOfRangeException(nameof(feeling));
            }
        }

        // The line for a feeling and topic at a tier, picked by day and talks so far, with the little one's name.
        public static string Line(string name, Feeling feeling, TalkTopic topic, TalkTier tier, int day, int talks)
        {
            var lines = TalkLines.For(feeling, topic, tier);
            var index = (int)(((long)Math.Max(0, day) + Math.Max(0, talks)) % lines.Count);
            return lines[index].Replace(TalkLines.NamePlaceholder, string.IsNullOrWhiteSpace(name) ? "Me" : name.Trim());
        }
    }
}
