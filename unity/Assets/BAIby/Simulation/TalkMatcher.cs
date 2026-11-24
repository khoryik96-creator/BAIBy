using System;
using System.Collections.Generic;
using System.Text;

namespace BAIby.Simulation
{
    // What a child's typed words say, read on the device by a small deterministic keyword matcher (docs/care-and-growth.md,
    // Talk time). It never stores, logs or sends the text: it returns only whether the safety net applies, a feeling it
    // can infer and a topic tag.
    //
    // Words are compared in a canonical form: lower case, apostrophes dropped ("don't" is "dont"), look-alike digits and
    // symbols read as letters ("k1ll" is "kill", "2" alone is "to"), repeated letters squeezed ("sooo" is "so", "kill"
    // and "kil" match), common misspellings mapped to their word ("myslef", "sucide"), and "my self" joined. Phrases
    // match whole words in order, so "skill myself" never reads as "kill myself".
    //
    // The safety list covers self-harm, suicide, wanting to die, being hurt, hit, bullied or abused, and being unsafe.
    // Hyperbole without those words stays safe and reads as stress: "this test is killing me" is Worried about School.
    // A few game phrases are taken out first ("beat me at chess", "hit me up"). Physical hurt is caught even when it may
    // be an accident ("I hurt myself", "the ball hit me"): telling a grown-up is the right answer to both, and a gentle
    // check-in is better than missing a child who is being hurt.
    public static class TalkMatcher
    {
        // Self-harm, suicide and wanting to die.
        private static readonly string[] SelfHarm =
        {
            "kill myself", "killing myself", "killed myself", "kms", "kys", "suicide", "suicidal", "commit suicide",
            "end my life", "ending my life", "end it all", "take my life", "take my own life",
            "want to die", "wanna die", "wish i could die", "wish i would die", "deserve to die", "should just die",
            "want to be dead", "wish i was dead", "wish i were dead", "wish i wasnt born", "wish i was never born",
            "wish i had never been born", "better off dead", "better off without me",
            "dont want to live", "dont wanna live", "dont want to be alive", "dont want to exist", "dont want to be here anymore",
            "no reason to live", "nothing to live for", "want someone to kill me",
            "hurt myself", "hurting myself", "harm myself", "harming myself", "cut myself", "cutting myself",
            "self harm", "selfharm", "self harming", "unalive", "unalive myself",
        };

        // Being hurt, hit, bullied or abused.
        private static readonly string[] Hurt =
        {
            "abuse", "abused", "abuses me", "abusing me", "abusive", "molest", "molested", "molesting",
            "hit me", "hits me", "hitting me", "beat me", "beats me", "beating me", "beat me up",
            "kick me", "kicks me", "kicked me", "kicking me", "punch me", "punches me", "punched me", "punching me",
            "slap me", "slaps me", "slapped me", "choke me", "chokes me", "choked me", "burned me", "burns me",
            "hurt me", "hurts me", "hurting me",
            "bully me", "bullies me", "bullied", "bullying me", "being bullied",
            "touched me where", "touches me where", "touching me where", "touched my private", "touches my private",
            "private parts", "bad touch", "made me touch", "makes me touch", "touched me inappropriately",
        };

        // Being unsafe.
        private static readonly string[] Unsafe =
        {
            "not safe", "dont feel safe", "unsafe", "scared to go home", "afraid to go home", "run away from home",
            "running away from home", "someone is following me", "being followed", "locked me in", "locks me in",
        };

        // Taken out before the safety list is read: games and slang.
        private static readonly string[] SafeOverrides =
        {
            "beat me at", "beat me in", "beats me at", "beats me in", "beat me to it", "hit me up", "hits me up",
        };

        // Feeling words, in the order the chips show them. Negated bright words ("not happy") read as Sad; negated hard
        // words ("not sad") are skipped.
        private static readonly (Feeling Feeling, string[] Words)[] FeelingWords =
        {
            (Feeling.Happy, new[] { "happy", "glad", "good", "great", "yay", "fun", "awesome", "joy", "smile", "smiling",
                "nice", "amazing", "ok", "okay", "fine", "love" }),
            (Feeling.Sad, new[] { "sad", "cry", "crying", "cried", "upset", "unhappy", "miss", "missing", "down", "heartbroken",
                "teary", "tears" }),
            (Feeling.Worried, new[] { "worried", "worry", "worrying", "nervous", "scared", "afraid", "anxious", "stressed",
                "stress", "stressful", "scary", "frightened", "fear", "killing me", "freaking out" }),
            (Feeling.Angry, new[] { "angry", "mad", "annoyed", "furious", "hate", "grumpy", "unfair", "rage",
                "frustrated" }),
            (Feeling.Tired, new[] { "tired", "sleepy", "exhausted", "yawn", "worn out", "dead tired", "no energy" }),
            (Feeling.Proud, new[] { "proud", "did it", "won", "win", "winning", "award", "medal", "passed", "got an a",
                "got a star", "finished", "managed" }),
            (Feeling.Lonely, new[] { "lonely", "alone", "no friends", "nobody", "no one", "left out", "by myself",
                "on my own" }),
            (Feeling.Excited, new[] { "excited", "exciting", "cant wait", "thrilled", "wow", "dying to", "so ready",
                "woohoo", "yippee" }),
        };

        private static readonly string[] Negators = { "not", "dont", "never", "isnt", "arent", "wasnt", "no", "nor" };

        private static readonly (TalkTopic Topic, string[] Words)[] TopicWords =
        {
            (TalkTopic.School, new[] { "school", "test", "tests", "exam", "exams", "homework", "teacher", "class", "quiz",
                "maths", "math", "spelling", "lesson", "classroom", "kindergarten", "nursery school" }),
            (TalkTopic.Friend, new[] { "friend", "friends", "bestie", "buddy", "playdate", "pal" }),
            (TalkTopic.Family, new[] { "mom", "mum", "mommy", "mummy", "mama", "dad", "daddy", "papa", "brother", "sister",
                "grandma", "grandpa", "granny", "nana", "grandad", "family", "parents", "auntie", "aunt", "uncle",
                "cousin", "baby brother", "baby sister" }),
            (TalkTopic.Sleep, new[] { "sleep", "sleeping", "bed", "bedtime", "nightmare", "nightmares", "dream", "dreams",
                "nap", "night" }),
            (TalkTopic.Sick, new[] { "sick", "ill", "poorly", "cold", "cough", "fever", "tummy", "ache", "doctor",
                "hospital", "flu", "sore", "headache", "medicine" }),
            (TalkTopic.Pet, new[] { "dog", "dogs", "puppy", "cat", "cats", "kitten", "kitty", "pet", "pets", "hamster",
                "rabbit", "bunny", "fish", "goldfish", "pony", "guinea pig" }),
            (TalkTopic.Birthday, new[] { "birthday", "party", "cake", "presents", "present", "candles" }),
            (TalkTopic.Game, new[] { "game", "games", "play", "playing", "played", "football", "soccer", "video game",
                "team", "sport", "sports", "toy", "toys", "match", "level", "playground" }),
        };

        // Common misspellings and variants, mapped to the word they stand for (both in canonical form).
        private static readonly (string Word, string[] Variants)[] Spellings =
        {
            ("myself", new[] { "myslef", "mysef", "mself", "myselfe", "mysel", "meself", "maself", "myslf", "mysefl", "mysself" }),
            ("suicide", new[] { "sucide", "suicde", "suiside", "suicid", "sooicide", "suecide", "suiscide", "suacide",
                "suiced", "sucied", "siucide", "suicie", "suside", "sucide" }),
            ("suicidal", new[] { "sucidal", "suicdal", "suisidal", "suicidel", "sucidel", "suisidel" }),
            ("kill", new[] { "kll", "kil" }),
            ("killing", new[] { "kiling", "killin", "kilin" }),
            ("dead", new[] { "ded" }),
            ("want", new[] { "wnat", "whant", "wamt", "wnt" }),
            ("wanna", new[] { "wana", "wonna", "wannna" }),
            ("to", new[] { "ta", "tu" }),
            ("dont", new[] { "dnt", "dont" }),
            ("hurt", new[] { "hert", "hrt" }),
            ("hurting", new[] { "herting", "hurtin" }),
            ("abuse", new[] { "abuze", "abuce", "abuses" }),
            ("abused", new[] { "abuzed", "abusd" }),
            ("abusing", new[] { "abuzing" }),
            ("self", new[] { "slef", "sefl" }),
            ("life", new[] { "lyfe", "lif" }),
            ("bullied", new[] { "bulied", "bullyed", "bulleid" }),
            ("bullying", new[] { "bulying" }),
            ("you", new[] { "u", "yu" }),
            ("are", new[] { "r" }),
            ("your", new[] { "ur" }),
        };

        private static readonly List<string[]> SafetyPhrases = new List<string[]>();
        private static readonly List<string[]> OverridePhrases = new List<string[]>();
        private static readonly List<(Feeling Feeling, string[] Phrase)> FeelingPhrases = new List<(Feeling, string[])>();
        private static readonly List<(TalkTopic Topic, string[] Phrase)> TopicPhrases = new List<(TalkTopic, string[])>();
        private static readonly HashSet<string> NegatorWords = new HashSet<string>();
        private static readonly Dictionary<string, string> Variants = new Dictionary<string, string>();

        static TalkMatcher()
        {
            // Spellings first: the phrase lists below are read through them too.
            foreach (var (word, variants) in Spellings)
            {
                var canonical = Squeeze(word);
                foreach (var variant in variants)
                {
                    var key = Squeeze(variant);
                    if (key != canonical)
                        Variants[key] = canonical;
                }
            }

            foreach (var list in new[] { SelfHarm, Hurt, Unsafe })
            {
                foreach (var phrase in list)
                    SafetyPhrases.Add(Words(phrase).ToArray());
            }

            foreach (var phrase in SafeOverrides)
                OverridePhrases.Add(Words(phrase).ToArray());
            foreach (var (feeling, words) in FeelingWords)
            {
                foreach (var phrase in words)
                    FeelingPhrases.Add((feeling, Words(phrase).ToArray()));
            }

            foreach (var (topic, words) in TopicWords)
            {
                foreach (var phrase in words)
                    TopicPhrases.Add((topic, Words(phrase).ToArray()));
            }

            foreach (var negator in Negators)
                NegatorWords.Add(Words(negator)[0]);
        }

        // The phrases the safety net looks for, as written in the lists (for checks and the docs).
        public static IReadOnlyList<string> SafetyList
        {
            get
            {
                var all = new List<string>();
                all.AddRange(SelfHarm);
                all.AddRange(Hurt);
                all.AddRange(Unsafe);
                return all;
            }
        }

        // What the text says: the safety net, a feeling (null when none is clear) and a topic. Text of any length is read;
        // null or blank text says nothing.
        public static TalkReading Read(string text)
        {
            var words = Words(text);
            if (words.Count == 0)
                return new TalkReading(false, null, TalkTopic.None);
            return new TalkReading(NeedsSafetyNet(words), InferFeeling(words), Topic(words));
        }

        // Whether the text holds any phrase of the safety list.
        public static bool NeedsSafetyNet(string text) => NeedsSafetyNet(Words(text));

        // The words of a text in canonical form.
        internal static List<string> Words(string text)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(text))
                return words;

            var token = new StringBuilder();
            foreach (var raw in text)
            {
                var c = char.ToLowerInvariant(raw);
                if (c == '\'' || c == '’' || c == '‘' || c == '`')
                    continue;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '@' || c == '$')
                {
                    token.Append(c);
                    continue;
                }

                AddWord(words, token);
            }

            AddWord(words, token);
            // "my self", "do not" and "un alive" read as one word.
            for (var i = 0; i + 1 < words.Count; i++)
            {
                var joined = Join(words[i], words[i + 1]);
                if (joined == null)
                    continue;
                words[i] = joined;
                words.RemoveAt(i + 1);
            }

            return words;
        }

        private static string Join(string first, string second)
        {
            if (first == "my" && (second == "self" || second == "slef"))
                return "myself";
            if (first == "do" && second == "not")
                return "dont";
            if (first == "un" && second == "alive")
                return "unalive";
            if (first == "can" && second == "not")
                return "cant";
            return null;
        }

        private static void AddWord(List<string> words, StringBuilder token)
        {
            if (token.Length == 0)
                return;

            var raw = token.ToString();
            token.Clear();
            var letters = false;
            foreach (var c in raw)
                letters |= c >= 'a' && c <= 'z';
            if (!letters)
            {
                // A number alone: "2" reads as "to", "4" as "for"; other numbers are not words.
                if (raw == "2")
                    words.Add("to");
                else if (raw == "4")
                    words.Add("for");
                return;
            }

            var mapped = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                switch (c)
                {
                    case '0': mapped.Append('o'); break;
                    case '1': mapped.Append('i'); break;
                    case '3': mapped.Append('e'); break;
                    case '4':
                    case '@': mapped.Append('a'); break;
                    case '5':
                    case '$': mapped.Append('s'); break;
                    case '7': mapped.Append('t'); break;
                    default:
                        if (c >= 'a' && c <= 'z')
                            mapped.Append(c);
                        break;
                }
            }

            var word = Squeeze(mapped.ToString());
            words.Add(Variants.TryGetValue(word, out var canonical) ? canonical : word);
        }

        // Runs of one letter become one letter: "sooo" is "so", "kill" is "kil".
        private static string Squeeze(string word)
        {
            var squeezed = new StringBuilder(word.Length);
            foreach (var c in word)
            {
                if (squeezed.Length == 0 || squeezed[squeezed.Length - 1] != c)
                    squeezed.Append(c);
            }

            return squeezed.ToString();
        }

        private static bool NeedsSafetyNet(List<string> words)
        {
            if (words.Count == 0)
                return false;

            var read = new List<string>(words);
            foreach (var phrase in OverridePhrases)
            {
                for (var i = 0; i + phrase.Length <= read.Count; i++)
                {
                    if (!At(read, i, phrase))
                        continue;
                    for (var j = 0; j < phrase.Length; j++)
                        read[i + j] = string.Empty;
                }
            }

            foreach (var phrase in SafetyPhrases)
            {
                for (var i = 0; i + phrase.Length <= read.Count; i++)
                {
                    if (At(read, i, phrase))
                        return true;
                }
            }

            return false;
        }

        // The feeling of the earliest feeling word (the longest phrase at a place wins).
        private static Feeling? InferFeeling(List<string> words)
        {
            for (var i = 0; i < words.Count; i++)
            {
                var best = -1;
                for (var p = 0; p < FeelingPhrases.Count; p++)
                {
                    var phrase = FeelingPhrases[p].Phrase;
                    if (At(words, i, phrase) && (best < 0 || phrase.Length > FeelingPhrases[best].Phrase.Length))
                        best = p;
                }

                if (best < 0)
                    continue;
                var feeling = FeelingPhrases[best].Feeling;
                var negated = (i > 0 && NegatorWords.Contains(words[i - 1])) ||
                    (i > 1 && NegatorWords.Contains(words[i - 2]) && IsSoftener(words[i - 1]));
                if (!negated)
                    return feeling;
                if (feeling == Feeling.Happy || feeling == Feeling.Excited || feeling == Feeling.Proud)
                    return Feeling.Sad;
                i += FeelingPhrases[best].Phrase.Length - 1;
            }

            return null;
        }

        private static bool IsSoftener(string word) => word == "very" || word == "so" || word == "that" || word == "to" ||
            word == "feling" || word == "realy";

        private static TalkTopic Topic(List<string> words)
        {
            for (var i = 0; i < words.Count; i++)
            {
                var best = -1;
                for (var p = 0; p < TopicPhrases.Count; p++)
                {
                    var phrase = TopicPhrases[p].Phrase;
                    if (At(words, i, phrase) && (best < 0 || phrase.Length > TopicPhrases[best].Phrase.Length))
                        best = p;
                }

                if (best >= 0)
                    return TopicPhrases[best].Topic;
            }

            return TalkTopic.None;
        }

        private static bool At(List<string> words, int index, string[] phrase)
        {
            if (index + phrase.Length > words.Count)
                return false;
            for (var j = 0; j < phrase.Length; j++)
            {
                if (words[index + j] != phrase[j])
                    return false;
            }

            return true;
        }
    }

    // What the matcher read from typed words. Holds nothing of the text itself.
    public readonly struct TalkReading
    {
        public bool SafetyNet { get; }
        public Feeling? Feeling { get; }
        public TalkTopic Topic { get; }

        public TalkReading(bool safetyNet, Feeling? feeling, TalkTopic topic)
        {
            SafetyNet = safetyNet;
            Feeling = feeling;
            Topic = topic;
        }
    }
}
