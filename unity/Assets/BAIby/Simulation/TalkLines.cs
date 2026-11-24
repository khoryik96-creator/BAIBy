using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // The little one's curated replies in Talk time, keyed by feeling x topic x tier, three to five variants each. A
    // topic without its own lines uses the feeling's lines; a tier without lines uses the tier below (so a Child tier can
    // be added as data alone).
    //
    // House rules for every line: the little one comforts, celebrates or keeps company. Never advice, a diagnosis or
    // blame; never a question about the child's life or personal details (an offer such as "Hug?" is fine); nothing a
    // child could take as a promise about later or an instruction. Present tense, short, warm. TalkChecks holds them to it.
    public static class TalkLines
    {
        // Replaced by the little one's name.
        public const string NamePlaceholder = "{name}";

        private static readonly Feeling[] Hard = { Feeling.Sad, Feeling.Worried, Feeling.Angry, Feeling.Tired, Feeling.Lonely };
        private static readonly Feeling[] Bright = { Feeling.Happy, Feeling.Proud, Feeling.Excited };

        private static readonly List<Entry> Entries = new List<Entry>
        {
            // Tier 1, right after First words: one or two words and a gesture.
            Of(Feeling.Happy, TalkTopic.None, TalkTier.FirstWords, "Happy!", "Yay!", "Me happy too!", "Big smile!", "Yay you!"),
            Of(Feeling.Sad, TalkTopic.None, TalkTier.FirstWords, "Hug?", "{name} here.", "Love you.", "Aww. Hug.", "Me here."),
            Of(Feeling.Worried, TalkTopic.None, TalkTier.FirstWords, "{name} here.", "Hold hand?", "Hug?", "Me here.", "Close, close."),
            Of(Feeling.Angry, TalkTopic.None, TalkTier.FirstWords, "Big feelings.", "{name} here.", "Hug?", "Mad. Okay.", "Love you."),
            Of(Feeling.Tired, TalkTopic.None, TalkTier.FirstWords, "Sleepy?", "Cozy.", "Yawn. Me too.", "{name} here.", "Snuggle?"),
            Of(Feeling.Proud, TalkTopic.None, TalkTier.FirstWords, "Yay you!", "Clap clap!", "Wow!", "Big yay!", "You did it!"),
            Of(Feeling.Lonely, TalkTopic.None, TalkTier.FirstWords, "{name} here.", "Me here.", "Hug?", "Love you.", "Me friend."),
            Of(Feeling.Excited, TalkTopic.None, TalkTier.FirstWords, "Yay!", "Wow!", "Ooh!", "Me too!", "Yippee!"),
            Of(Bright, TalkTopic.Pet, TalkTier.FirstWords, "Doggy! Yay!", "Kitty! Yay!", "Pets! Yay!"),
            Of(Bright, TalkTopic.Birthday, TalkTier.FirstWords, "Birthday! Yay!", "Cake! Yay!", "Party!"),
            Of(Hard, TalkTopic.Sick, TalkTier.FirstWords, "Ouch. Hug?", "Poorly. Hug.", "{name} here."),
            Of(Hard, TalkTopic.Sleep, TalkTier.FirstWords, "Night night. Hug?", "Cozy. Me here.", "{name} here."),

            // Tier 2, from about day 21 or Bond 60: short toddler sentences.
            Of(Feeling.Happy, TalkTopic.None, TalkTier.Toddler,
                "You are happy! Me happy too!", "Happy feels sunny. I like it!", "Yay! Happy dance with you!",
                "Your smile makes me smile.", "Happy day! Big hug!"),
            Of(Feeling.Sad, TalkTopic.None, TalkTier.Toddler,
                "Sad is okay. I sit with you.", "I am here. You are not alone.", "Big hug for you. I love you.",
                "Sad feels heavy. I hold your hand.", "{name} is here with you. Hug?"),
            Of(Feeling.Worried, TalkTopic.None, TalkTier.Toddler,
                "Worry feels big. I am here with you.", "I sit close. Worried is okay.", "Hug? I am right here.",
                "I hold your hand. Slow and cozy.", "Worry is a big feeling. You are not alone."),
            Of(Feeling.Angry, TalkTopic.None, TalkTier.Toddler,
                "Mad is a big feeling. I am still here.", "Angry is okay. I sit with you.", "Grr feelings! I still love you.",
                "Big mad. I am close by.", "{name} is here. Mad is okay."),
            Of(Feeling.Tired, TalkTopic.None, TalkTier.Toddler,
                "Sleepy feelings. Me sleepy too.", "Yawn! Tired is okay.", "Cozy and quiet with you.",
                "Tired is okay. Snuggle?", "{name} sits with you. Cozy."),
            Of(Feeling.Proud, TalkTopic.None, TalkTier.Toddler,
                "You did it! I am proud of you!", "Wow! Clap clap clap!", "Proud is a happy feeling. Yay you!",
                "Big proud! Happy dance!", "Yay! {name} is so happy for you!"),
            Of(Feeling.Lonely, TalkTopic.None, TalkTier.Toddler,
                "I am here. You have me.", "Lonely feels sad. I sit with you.", "Me and you. Together now.",
                "I love you. You are not alone.", "{name} is your friend. Hug?"),
            Of(Feeling.Excited, TalkTopic.None, TalkTier.Toddler,
                "Ooh! Exciting! Me too!", "Yay! Happy wiggles!", "So exciting! I bounce with you!", "Wow wow wow!",
                "Yippee! {name} is excited too!"),

            Of(Hard, TalkTopic.School, TalkTier.Toddler,
                "School can feel big. I am here.", "Tests feel big. I sit with you.", "School feelings are okay. Hug?",
                "Big school day. {name} is here."),
            Of(Bright, TalkTopic.School, TalkTier.Toddler,
                "School yay! Clap clap!", "Wow! Big school day!", "Yay! You did school things!", "School fun! Happy dance!"),
            Of(Hard, TalkTopic.Friend, TalkTier.Toddler,
                "Friend feelings are big. I am your friend too.", "I am here. Me your buddy.", "Hug for you. I like you lots.",
                "{name} is your friend. I sit with you."),
            Of(Bright, TalkTopic.Friend, TalkTier.Toddler,
                "Friends are fun! Yay!", "Yay friends! Happy dance!", "Friend fun! Me happy too!", "Wow! Friends! Clap clap!"),
            Of(Hard, TalkTopic.Family, TalkTier.Toddler,
                "Family feelings can be big. I love you.", "I am here. Hug?", "Big feelings. {name} sits with you.",
                "Family is big. I hold your hand."),
            Of(Bright, TalkTopic.Family, TalkTier.Toddler,
                "Family hugs! Yay!", "Yay! Love love love!", "Happy family feelings! Me too!", "Family fun! Happy dance!"),
            Of(Hard, TalkTopic.Sleep, TalkTier.Toddler,
                "Night feelings are big. I am here.", "Bedtime feelings. Cozy and close.", "Dreams can feel big. {name} is here.",
                "Cozy blanket. I sit close."),
            Of(Bright, TalkTopic.Sleep, TalkTier.Toddler, "Cozy dreams! Yay!", "Sleepy happy! Me too!", "Pillow fun! Yay!"),
            Of(Hard, TalkTopic.Sick, TalkTier.Toddler,
                "Poorly feels yucky. I sit with you.", "Ouch. Gentle hug?", "Feeling sick is no fun. I am here.",
                "{name} sits close. Gentle hugs."),
            Of(Bright, TalkTopic.Sick, TalkTier.Toddler, "Better feelings! Yay!", "Yay! Happy wiggle!", "Strong you! Clap clap!"),
            Of(Hard, TalkTopic.Pet, TalkTier.Toddler,
                "Pets are special. I sit with you.", "Big pet feelings. Hug?", "I love animals too. I am here.",
                "{name} is here. Pets are special."),
            Of(Bright, TalkTopic.Pet, TalkTier.Toddler,
                "Woof woof! Meow! Yay!", "Pets are fun! Yay!", "I love animals too!", "Wiggle wag! Happy!"),
            Of(Hard, TalkTopic.Birthday, TalkTier.Toddler,
                "Birthday feelings can be big. I am here.", "Hug for you. Party feelings are big.", "{name} sits with you. Hug?"),
            Of(Bright, TalkTopic.Birthday, TalkTier.Toddler,
                "Birthday! Yay yay!", "Party time! Happy dance!", "Cake feelings! Yum yay!", "Birthday feelings! Clap clap!"),
            Of(Hard, TalkTopic.Game, TalkTier.Toddler,
                "Game feelings are big. I sit with you.", "Grr game! I still love you.", "Big game feelings. Hug?"),
            Of(Bright, TalkTopic.Game, TalkTier.Toddler,
                "Game fun! Yay!", "Wow! Game high five!", "You played! Happy dance!", "Yay! Clap clap!"),
        };

        // Every line with where it applies, for checks.
        public static IEnumerable<(Feeling Feeling, TalkTopic Topic, TalkTier Tier, string Line)> All
        {
            get
            {
                foreach (var entry in Entries)
                {
                    foreach (var feeling in entry.Feelings)
                    {
                        foreach (var line in entry.Lines)
                            yield return (feeling, entry.Topic, entry.Tier, line);
                    }
                }
            }
        }

        // The lines for a feeling and topic at a tier: the topic's own, else the feeling's, else the tier below.
        public static IReadOnlyList<string> For(Feeling feeling, TalkTopic topic, TalkTier tier)
        {
            if (!Enum.IsDefined(typeof(Feeling), feeling))
                throw new ArgumentOutOfRangeException(nameof(feeling));
            if (!Enum.IsDefined(typeof(TalkTopic), topic))
                throw new ArgumentOutOfRangeException(nameof(topic));
            if (!Enum.IsDefined(typeof(TalkTier), tier))
                throw new ArgumentOutOfRangeException(nameof(tier));

            for (var level = (int)tier; level >= (int)TalkTier.FirstWords; level--)
            {
                var found = Find(feeling, topic, (TalkTier)level) ?? Find(feeling, TalkTopic.None, (TalkTier)level);
                if (found != null)
                    return found;
            }

            throw new InvalidOperationException($"Talk time has no lines for {feeling}.");
        }

        private static IReadOnlyList<string> Find(Feeling feeling, TalkTopic topic, TalkTier tier)
        {
            foreach (var entry in Entries)
            {
                if (entry.Topic == topic && entry.Tier == tier && Array.IndexOf(entry.Feelings, feeling) >= 0)
                    return entry.Lines;
            }

            return null;
        }

        private static Entry Of(Feeling feeling, TalkTopic topic, TalkTier tier, params string[] lines) =>
            new Entry(new[] { feeling }, topic, tier, lines);

        private static Entry Of(Feeling[] feelings, TalkTopic topic, TalkTier tier, params string[] lines) =>
            new Entry(feelings, topic, tier, lines);

        private sealed class Entry
        {
            public readonly Feeling[] Feelings;
            public readonly TalkTopic Topic;
            public readonly TalkTier Tier;
            public readonly string[] Lines;

            public Entry(Feeling[] feelings, TalkTopic topic, TalkTier tier, string[] lines)
            {
                Feelings = feelings;
                Topic = topic;
                Tier = tier;
                Lines = lines;
            }
        }
    }
}
