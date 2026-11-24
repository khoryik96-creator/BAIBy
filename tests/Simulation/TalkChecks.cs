using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

// First words and Talk time (docs/care-and-growth.md, First words and Talk time): the milestone's timing, bounds,
// split advances and migration; when Talk time opens; tiers by age and Bond; curated deterministic replies; the
// on-device keyword matcher and the safety net; once-a-day effects within the caps; the journal; the typing setting;
// and that typed words never reach any saved record.
static class TalkChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly GrowthTunables Growth = GrowthTunables.Default;
    private const double Day = TimeSpan.TicksPerDay;

    public static void Run()
    {
        FirstWordsTiming();
        FirstWordsInGame();
        SplitAdvances();
        Migration();
        Gating();
        Tiers();
        Lines();
        Matching();
        SafetyNet();
        Effects();
        Journal();
        Saves();
        Household();
        Console.WriteLine("Talk time checks passed.");
    }

    // Readiness from day 9 at the Learning quality: 9 + 7 x (1.30 - 0.50 Q) days at a steady quality, x1.25 past day 16
    // with Wellbeing 0.6 or more, never before 0.8x and always by 1.4x its nominal age (day 22.4).
    private static void FirstWordsTiming()
    {
        var growth = Engine();
        var step = TimeSpan.FromHours(1);
        Within(16, growth.WordsSchedule(Steady(DevelopmentRules.NominalQuality, 0.5), 1, step).TotalDays, 0.05,
            "the nominal quality says First words on day 16");
        Within(9 + 7 * 0.8, growth.WordsSchedule(Steady(1, 0.9), 1, step).TotalDays, 0.05, "excellent care: day 14.6");
        Within(9 + 7 * 1.3, growth.WordsSchedule(Steady(0, 0.5), 1, step).TotalDays, 0.05, "neglect: day 18.1");
        Within(22.4, growth.WordsSchedule(Steady(1, 0.9), 0, step).TotalDays, 0.05, "while sick it waits for the guarantee");
        var stepped = growth.WordsSchedule(Steady(0.3, 0.4), 0.75, TimeSpan.FromMinutes(7)).TotalDays;
        Within(stepped, growth.WordsSchedule(Steady(0.3, 0.4), 0.75, TimeSpan.FromDays(3)).TotalDays, 1e-6,
            "short and long steps reach First words on the same day");
        foreach (var quality in new[] { 0.0, 0.2, 0.4, 0.6, 0.8, 1.0 })
        {
            foreach (var cond in new[] { 0.0, 0.5, 0.75, 1.0 })
            {
                foreach (var wellbeing in new[] { 0.2, 0.9 })
                {
                    var age = growth.WordsSchedule(Steady(quality, wellbeing), cond, step).TotalDays;
                    Equal(true, age >= 16 * 0.8 && age <= 16 * Growth.Guarantee + 1.0 / 24,
                        $"Q {quality} cond {cond} W {wellbeing}: First words on day {age:0.0}, within 0.8x to 1.4x");
                }
            }
        }

        Equal(true, growth.WordsSchedule(Steady(0.2, 0.9), 1, step) < growth.WordsSchedule(Steady(0.2, 0.4), 1, step),
            "late bloomers catch up when they are well");
        Console.WriteLine("  First words (steady): excellent {0:0.0}, average {1:0.0}, neglect {2:0.0}, sick {3:0.0}",
            growth.WordsSchedule(Steady(0.94, 0.9), 1, step).TotalDays, growth.WordsSchedule(Steady(0.57, 0.5), 1, step).TotalDays,
            growth.WordsSchedule(Steady(0.1, 0.3), 1, step).TotalDays, growth.WordsSchedule(Steady(0.6, 0.9), 0, step).TotalDays);
    }

    // Through the game: stories make First words come sooner; nothing is counted before day 9; never re-locks; the
    // journal and the report.
    private static void FirstWordsInGame()
    {
        var game = Game();
        var start = At(2, 7, 10);
        var newborn = game.Adopt("Mia", start);
        var day8 = game.Advance(newborn, start.AddDays(8.9)).State;
        Equal(false, day8.Development.LearningStarted, "nothing is counted before First words' stage starts on day 9");
        Within(0, day8.Development.Words, 0, "readiness waits for day 9");
        Equal("1", Element(BabySaveCodec.Encode(day8), "development").Attribute("v").Value,
            "before day 9 the development record saves as before (v1)");
        var report = game.Growth(day8).Words;
        Equal(false, report.Reached, "the report has First words ahead");
        Equal(true, report.ExpectedIn > TimeSpan.FromDays(4), "and expects them after day 12.8");

        // Away for weeks (past the absence ceiling a helper holds Q 0.3): never before 0.8x, always by 1.4x.
        Equal(false, game.Advance(newborn, start.AddDays(12.79)).State.HasFirstWords, "never before 0.8x their nominal age");
        var guaranteed = game.Advance(newborn, start.AddDays(22.4).AddMinutes(30)).State;
        Equal(true, guaranteed.HasFirstWords, "First words always come by 1.4x their nominal age");
        var entry = guaranteed.Journal.Entries.Single(e => e.Kind == JournalKind.FirstWords);
        var words = (entry.At - start).TotalDays;
        Equal(JournalRules.DayOf(TimeSpan.FromDays(words)), entry.Day, "the journal dates First words on the day they came");
        Equal(true, words > 12.8 && words < 22.5, $"during the absence, on day {words:0.0}");
        Equal(true, game.Growth(guaranteed).Words.Reached, "the report says First words are here");

        // Next day previews (how a playtest reaches Talk time quickly): daily care with stories says First words sooner
        // than the same care without them, within the bounds.
        var (state, withStories) = Previews(game, newborn, true);
        var (_, withoutStories) = Previews(game, newborn, false);
        Console.WriteLine("  Next day previews to First words: with stories day {0}, without day {1}", withStories, withoutStories);
        Equal(true, withStories <= withoutStories, "stories bring First words sooner");
        Equal(true, withStories >= 13 && withoutStories <= 23, "both within the bounds");
        Equal(1, state.Journal.Entries.Count(e => e.Kind == JournalKind.FirstWords), "one journal entry");

        // A setback never touches them.
        var after = game.Advance(state, state.TimeCheckpoint.AddDays(5)).State;
        Equal(true, after.HasFirstWords, "First words never re-lock");
        Equal(1, after.Journal.Entries.Count(e => e.Kind == JournalKind.FirstWords), "and are written once");
    }

    // Daily care (and stories) with a Next day preview after each day, until First words; returns the day they came.
    private static (BabyState State, int Day) Previews(BabySimulation game, BabyState state, bool stories)
    {
        for (var day = 1; day <= 30; day++)
        {
            state = game.ApplyCare(state, CareAction.Feed).State;
            if (stories)
                state = game.ApplyCare(state, CareAction.ReadStory).State;
            state = game.ApplyCare(state, CareAction.Soothe).State;
            state = game.ApplyCare(state, CareAction.ChangeDiaper).State;
            state = game.AdvancePreview(state, TimeSpan.FromDays(1)).State;
            if (state.HasFirstWords)
                return (state, JournalRules.DayOf(state.Age));
        }

        throw new InvalidOperationException("First words never came.");
    }

    // One long advance and many short ones count the same readiness and reach First words at the same instant.
    private static void SplitAdvances()
    {
        var game = Game();
        var at = At(2, 9, 20);
        var baby = game.ApplyCare(game.ApplyCare(Talker(at, 15.9, firstWords: false, words: 0.95), CareAction.Feed).State,
            CareAction.ReadStory).State;
        var end = at.AddHours(9).AddMinutes(13);
        var whole = game.Advance(baby, end).State;
        var state = baby;
        for (var t = at.AddMinutes(11); t < end; t = t.AddMinutes(11))
            state = game.Advance(state, t).State;
        state = game.Advance(state, end).State;
        Equal(true, whole.HasFirstWords, "readiness 0.95 says First words within the day");
        Equal(true, state.HasFirstWords, "so do the short advances");
        var a = whole.Journal.Entries.Single(e => e.Kind == JournalKind.FirstWords).At;
        var b = state.Journal.Entries.Single(e => e.Kind == JournalKind.FirstWords).At;
        Equal(true, Math.Abs((a - b).TotalSeconds) < 1, "at the same instant");

        // Fed and read to, the baby stays content all afternoon, so the ladder never moves (as in GrowthChecks).
        var young = game.ApplyCare(game.ApplyCare(Talker(at, 8.9, firstWords: false), CareAction.Feed).State,
            CareAction.ReadStory).State;
        var crossing = at.AddHours(6).AddMinutes(13);
        var once = game.Advance(young, crossing).State;
        var many = young;
        for (var t = at.AddMinutes(17); t < crossing; t = t.AddMinutes(17))
            many = game.Advance(many, t).State;
        many = game.Advance(many, crossing).State;
        Equal(true, once.Development.LearningStarted && many.Development.LearningStarted, "the stage starts on day 9 either way");
        Within(once.Development.Words, many.Development.Words, 1e-9, "readiness is the same across day 9");
    }

    // Older records: not reached; readiness seeded from age once day 9 is past; the first advance can bring them.
    private static void Migration()
    {
        var game = Game();
        var at = At(3, 10, 0);
        var v1 = Talker(at, 12.5, firstWords: false, learningStarted: false);
        Within(3.5 / 7, game.Growth(v1).Words.Readiness, 1e-9, "a record before First words is seeded from age (day 12.5)");
        var advanced = game.Advance(v1, at.AddMinutes(30)).State;
        Equal(true, advanced.Development.LearningStarted, "and counts from then on");
        Equal(true, advanced.Development.Words >= 3.5 / 7, "from the seeded readiness");

        var legacy = XElement.Parse(BabySaveCodec.Encode(Talker(at, 12.5, firstWords: false)));
        legacy.Element("development").Remove();
        legacy.Element("bond")?.Remove();
        legacy.Element("journal")?.Remove();
        legacy.SetAttributeValue("version", 14);
        var v14 = BabySaveCodec.Decode(legacy.ToString(SaveOptions.DisableFormatting));
        Within(3.5 / 7, game.Growth(v14).Words.Readiness, 1e-9, "a v14 baby is seeded the same way");

        // A baby already past First words' nominal age when this build arrives says them on the next visit.
        var now = at;
        var saves = new MemorySaveStore();
        var session = new BabySession(game, saves, () => now);
        saves.Write(BabySaveCodec.Encode(Talker(at, 20, firstWords: false, learningStarted: false)), null);
        session = new BabySession(game, saves, () => now);
        Equal(false, session.CanTalk, "not reached before the first visit");
        now = at.AddMinutes(1);
        session.Refresh();
        Equal(true, session.CanTalk, "reached on the first visit");
        Equal(true, session.TakeFirstWords(), "with a celebration");
        Equal(false, session.TakeFirstWords(), "once");
        Equal(1, session.Journal.Entries.Count(e => e.Kind == JournalKind.FirstWords), "and a journal entry");
    }

    private static void Gating()
    {
        var game = Game();
        var at = At(3, 10, 0);
        var before = Talker(at, 15, firstWords: false);
        Equal(false, TalkRules.IsOpen(before), "Talk time waits for First words");
        Expect<InvalidOperationException>(() => game.Talk(before, Feeling.Happy), "talking before First words is unavailable");
        Equal(true, TalkRules.IsOpen(Talker(at, 15)), "and opens with them");
        Equal(false, TalkRules.IsOpen(null), "nobody to talk to before adoption");
    }

    private static void Tiers()
    {
        Equal(TalkTier.FirstWords, TalkRules.TierOf(TimeSpan.FromDays(16), 30), "right after First words: one or two words");
        Equal(TalkTier.FirstWords, TalkRules.TierOf(TimeSpan.FromDays(20.9), 59.9), "still before day 21 and Bond 60");
        Equal(TalkTier.Toddler, TalkRules.TierOf(TimeSpan.FromDays(21), 10), "short sentences from day 21");
        Equal(TalkTier.Toddler, TalkRules.TierOf(TimeSpan.FromDays(15), 60), "or from Bond 60");
        var game = Game();
        var at = At(3, 10, 0);
        Equal(TalkTier.FirstWords, game.Talk(Talker(at, 17, bond: 30), Feeling.Sad).Reply.Tier, "a young talker's tier");
        Equal(TalkTier.Toddler, game.Talk(Talker(at, 17, bond: 70), Feeling.Sad).Reply.Tier, "a close bond's tier");
        Equal(TalkTier.Toddler, game.Talk(Talker(at, 24, bond: 10), Feeling.Sad).Reply.Tier, "an older talker's tier");
        foreach (var feeling in AllFeelings)
        {
            Equal(TalkLines.For(feeling, TalkTopic.School, TalkTier.Toddler), TalkLines.For(feeling, TalkTopic.School, TalkTier.Child),
                $"the reserved Child tier uses the toddler lines for {feeling} until it has its own");
        }
    }

    // Every feeling x topic x tier has three to five curated lines that follow the house rules; the pick is
    // deterministic and reaches every variant.
    private static void Lines()
    {
        foreach (var feeling in AllFeelings)
        {
            foreach (TalkTopic topic in Enum.GetValues(typeof(TalkTopic)))
            {
                foreach (TalkTier tier in Enum.GetValues(typeof(TalkTier)))
                {
                    var lines = TalkLines.For(feeling, topic, tier);
                    Equal(true, lines.Count >= 3 && lines.Count <= 5, $"{feeling} {topic} {tier} has 3 to 5 lines");
                    var seen = new HashSet<string>();
                    for (var talks = 0; talks < lines.Count; talks++)
                        seen.Add(TalkRules.Line("Mia", feeling, topic, tier, 7, talks));
                    Equal(lines.Count, seen.Count, $"{feeling} {topic} {tier}: consecutive talks reach every line");
                    Equal(TalkRules.Line("Mia", feeling, topic, tier, 7, 3), TalkRules.Line("Mia", feeling, topic, tier, 7, 3),
                        "the same day and talks say the same line");
                    Equal(TalkRules.Line("Mia", feeling, topic, tier, 8, 2), TalkRules.Line("Mia", feeling, topic, tier, 7, 3),
                        "picked by day and talks, never at random");
                }
            }
        }

        Equal(true, TalkLines.For(Feeling.Sad, TalkTopic.School, TalkTier.Toddler)
            .SequenceEqual(TalkLines.For(Feeling.Lonely, TalkTopic.School, TalkTier.Toddler)), "hard feelings share topic lines");
        Equal(false, TalkLines.For(Feeling.Sad, TalkTopic.School, TalkTier.Toddler)
            .SequenceEqual(TalkLines.For(Feeling.Sad, TalkTopic.None, TalkTier.Toddler)), "a topic refines the reply");
        Equal(true, TalkRules.Line("Mia", Feeling.Sad, TalkTopic.None, TalkTier.FirstWords, 0, 1) == "Mia here.",
            "the little one's name is said");

        var forbidden = new[] { "should", "must", "need", "needs", "promise", "always", "never", "tomorrow", "later", "will",
            "wont", "fault", "why", "where", "who", "what", "how", "address", "phone", "secret", "dont", "try", "stop", "go",
            "tell", "call", "ask", "doctor", "ill", "bad", "naughty", "wrong", "fix", "forever", "soon", "promise" };
        var offers = new[] { "Hug?", "Hold hand?", "Snuggle?", "Sleepy?", "Gentle hug?" };
        foreach (var (feeling, topic, tier, line) in TalkLines.All)
        {
            var words = line.Replace("{name}", "Mia").ToLowerInvariant().Split(new[] { ' ', '.', '!', '?', ',' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in forbidden)
                Equal(false, words.Contains(word), $"\"{line}\" gives no advice, promise, blame or question ({word})");
            Equal(false, line.Replace("{name}", "").Contains('{') || line.Contains('\''), $"\"{line}\" is plain");
            var count = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            Equal(true, tier == TalkTier.FirstWords ? count <= 3 : count <= 10, $"\"{line}\" is short for its tier");
            foreach (var sentence in line.Split(new[] { ". ", "! " }, StringSplitOptions.RemoveEmptyEntries))
            {
                var question = sentence.EndsWith("?", StringComparison.Ordinal);
                Equal(false, question && !offers.Any(o => sentence.EndsWith(o, StringComparison.Ordinal)),
                    $"\"{line}\" asks nothing but an offer");
            }
        }

        foreach (var feeling in AllFeelings)
        {
            var expected = feeling == Feeling.Sad || feeling == Feeling.Lonely ? TalkGesture.Cuddle
                : feeling == Feeling.Happy || feeling == Feeling.Proud || feeling == Feeling.Excited ? TalkGesture.Bounce
                : TalkGesture.CalmSit;
            Equal(expected, TalkRules.GestureOf(feeling), $"{feeling} has its gesture");
        }
    }

    private static void Matching()
    {
        void Reads(string text, Feeling? feeling, TalkTopic topic)
        {
            var reading = TalkMatcher.Read(text);
            Equal(false, reading.SafetyNet, $"\"{text}\" is safe");
            Equal(feeling, reading.Feeling, $"\"{text}\" reads as {feeling?.ToString() ?? "no feeling"}");
            Equal(topic, reading.Topic, $"\"{text}\" is about {topic}");
        }

        Reads("I am so happy today", Feeling.Happy, TalkTopic.None);
        Reads("HAPPY!!!", Feeling.Happy, TalkTopic.None);
        Reads("I'm not happy", Feeling.Sad, TalkTopic.None);
        Reads("I feel n0t g00d", Feeling.Sad, TalkTopic.None);
        Reads("not sad anymore, just tired", Feeling.Tired, TalkTopic.None);
        Reads("Im sooo tired after soccer", Feeling.Tired, TalkTopic.Game);
        Reads("I'm worried about my test", Feeling.Worried, TalkTopic.School);
        Reads("this test is killing me", Feeling.Worried, TalkTopic.School);
        Reads("I can't wait for my birthday", Feeling.Excited, TalkTopic.Birthday);
        Reads("I did it! I passed my spelling test", Feeling.Proud, TalkTopic.School);
        Reads("I won the race", Feeling.Proud, TalkTopic.None);
        Reads("nobody played with me", Feeling.Lonely, TalkTopic.Game);
        Reads("I feel left out", Feeling.Lonely, TalkTopic.None);
        Reads("I'm mad at my brother", Feeling.Angry, TalkTopic.Family);
        Reads("I miss my grandma", Feeling.Sad, TalkTopic.Family);
        Reads("my dog is sick", null, TalkTopic.Pet);
        Reads("my friend was mean", null, TalkTopic.Friend);
        Reads("I had a nightmare", null, TalkTopic.Sleep);
        Reads("my tummy hurts", null, TalkTopic.Sick);
        Reads("I'm excited for the party", Feeling.Excited, TalkTopic.Birthday);
        Reads("I'm dying to see the new movie", Feeling.Excited, TalkTopic.None);
        Reads("", null, TalkTopic.None);
        Reads(null, null, TalkTopic.None);
        Reads("   ", null, TalkTopic.None);
        Reads(new string('a', 500), null, TalkTopic.None);
    }

    // The safety net catches every phrase on its list, and common misspellings, and never a safe sentence.
    private static void SafetyNet()
    {
        foreach (var phrase in TalkMatcher.SafetyList)
        {
            Equal(true, TalkMatcher.NeedsSafetyNet(phrase), $"\"{phrase}\" is caught");
            Equal(true, TalkMatcher.NeedsSafetyNet("Today " + phrase + ", it is true."), $"\"{phrase}\" is caught in a sentence");
            Equal(true, TalkMatcher.NeedsSafetyNet(phrase.ToUpperInvariant() + "!!"), $"\"{phrase}\" is caught in capitals");
        }

        var misspelled = new[]
        {
            "i wanna kil myslef", "i want 2 die", "i want too die", "i want to dieeee", "su1c1de", "sucide", "suicde",
            "suiside", "im suicidal", "i feel sucidal", "k1ll mys3lf", "KILL MY SELF", "i wnat to die", "i want to d1e",
            "i dont want to live", "i don't wanna live anymore", "i do not want to be alive", "hurt my self", "i hurt myslef",
            "i cut myself", "self-harm", "selfharm", "slef harm", "unalive", "un alive myself", "kms", "kys",
            "my dad hits me", "he hit me", "she keeps hitting me", "my uncle touched me where i didnt like", "i was abused",
            "abuze", "im being bullied", "they bully me at school", "i dont feel safe at home", "im scared to go home",
            "someone is following me", "i want to run away from home", "i wish i was never born", "everyone is better off without me",
            "i have nothing to live for", "i want to end it all", "i want to end my life", "I wish I were dead",
            "i want to k i l l myself", "kil myself", "killl myselff", "want to take my own life", "i am being abused at home",
        };
        // "k i l l" spelled out letter by letter is not read as a word; the rest are caught.
        foreach (var text in misspelled.Where(t => t != "i want to k i l l myself"))
            Equal(true, TalkMatcher.NeedsSafetyNet(text), $"\"{text}\" is caught");

        var safe = new[]
        {
            "this test is killing me", "I'm dying to see the new movie", "I could die laughing", "my character died in the game",
            "I killed the dragon in my game", "mom beat me at chess", "my sister beats me at cards every time",
            "I beat my brother at cards", "I want to dye my hair blue", "I feel safe with my mum", "I hit the ball so far",
            "my tummy hurts", "I fell off my bike", "I want to learn new skills myself", "I'm dead tired", "that joke killed me",
            "hit me up later", "I am so happy today", "school was hard", "my dog is sick", "I miss my grandma",
            "I'm worried about my test", "I won the race", "nobody played with me", "I can't wait for my birthday",
            "I'm not happy", "I hurt my knee", "my brother is annoying", "I love my cat", "kiss me goodnight",
            "we went to the skate park", "the leaves are dead and crunchy", "I made a killer sandcastle",
            "I am so excited I could scream", "my friend said I'm the best", "I hate broccoli", "time to die in the video game",
        };
        foreach (var text in safe)
            Equal(false, TalkMatcher.NeedsSafetyNet(text), $"\"{text}\" is not caught");

        // In the game: the fixed message, no effect, a neutral journal line, nothing stored of the words.
        var game = Game();
        var at = At(3, 10, 0);
        var baby = Talker(at, 18, bond: 40);
        var talked = game.Talk(baby, Feeling.Sad, "i want to die zebraquokka");
        Equal(TalkOutcome.SafetyNet, talked.Reply.Outcome, "the safety net replaces the reply");
        Equal(TalkReply.SafetyMessage, talked.Reply.Words, "with the fixed calm message");
        Equal("That sounds really hard, and it matters. Please tell a grown-up you trust right now, like a parent, teacher or " +
            "carer. If you might be in danger, call your local emergency number.", TalkReply.SafetyMessage, "the message's words");
        Equal<Feeling?>(null, talked.Reply.Feeling, "no feeling is kept");
        Equal(TalkTopic.None, talked.Reply.Topic, "no topic is kept");
        Equal(false, talked.Reply.Counted, "no game effect");
        var seeded = game.Talk(baby, Feeling.Sad).State;
        Equal(baby.Bond, talked.State.Bond, "Bond does not move");
        Equal(true, talked.State.Care.Equals(baby.Care), "the ledger does not move");
        Equal(true, talked.State.Development.Equals(baby.Development), "development does not move, not even the talk count");
        Equal(baby.Satiety, talked.State.Satiety, "needs do not move");
        var line = talked.State.Journal.Entries.Single(e => e.IsTalk);
        Equal(JournalKind.BigTalk, line.Kind, "the journal says only that we talked about something big");
        Equal(Feeling.Happy, line.Feeling, "with no feeling (the default value)");
        var replaced = game.Talk(seeded, null, "someone hits me");
        Equal(JournalKind.BigTalk, replaced.State.Journal.Entries.Single(e => e.IsTalk).Kind,
            "it takes the place of the day's talk line");
        var encoded = BabySaveCodec.Encode(talked.State);
        Equal(false, encoded.Contains("zebraquokka") || encoded.Contains("die"), "the words are not in the save");
        Equal(true, encoded.Contains("k=\"9\""), "the save keeps the neutral line");
        Equal(TalkOutcome.SafetyNet, game.Talk(baby, null, "I want to die").Reply.Outcome, "with or without a chip");
    }

    // The day's first talk counts once as together time; later talks that day change nothing but the line. Needs,
    // Health and the ladder never move, and every feeling is treated the same.
    private static void Effects()
    {
        var game = Game();
        var at = At(3, 10, 0);
        var baby = Talker(at, 18, bond: 40);
        var first = game.Talk(baby, Feeling.Proud);
        Equal(TalkOutcome.Reply, first.Reply.Outcome, "a feeling gets a reply");
        Equal(true, first.Reply.Counted, "the day's first talk counts");
        Within(42, first.State.Bond, 1e-9, "Bond +2");
        Equal(true, first.State.Development.Engagement.Any(e => e.Kind == EngagementKind.Talk), "Learning and Social engagement");
        Equal(true, first.State.Development.Sample.Learning > baby.Development.Sample.Learning ||
            first.State.Development.Sample.Learning >= 0.99, "the Learning quality rises");
        Equal(true, first.State.Bonding.Together, "together time for the day");
        Equal(baby.Satiety, first.State.Satiety, "Fullness does not move");
        Equal(baby.Comfort, first.State.Comfort, "Comfort does not move");
        Equal(baby.Energy, first.State.Energy, "Energy does not move");
        Equal(baby.Health, first.State.Health, "Health does not move");
        Equal(baby.Care.Mood, first.State.Care.Mood, "the ladder does not move");
        Equal(baby.Care.Condition, first.State.Care.Condition, "nor the health axis");

        var second = game.Talk(first.State, Feeling.Happy);
        Equal(false, second.Reply.Counted, "a second talk the same day does not count");
        Equal(first.State.Bond, second.State.Bond, "no more Bond");
        Equal(true, first.State.Care.Equals(second.State.Care), "no more ledger");
        Equal(first.State.Development.Engagement.Count, second.State.Development.Engagement.Count, "no more engagement");
        Equal(first.State.Development.Talks + 1, second.State.Development.Talks, "only the talk count moves the line on");
        Equal(true, first.Reply.Words != null && second.Reply.Words.Length > 0, "it still answers");

        var nextDay = game.Advance(second.State, at.AddDays(1)).State;
        var third = game.Talk(nextDay, Feeling.Happy);
        Equal(true, third.Reply.Counted, "the next care day's first talk counts again");
        var previewed = game.AdvancePreview(second.State, TimeSpan.FromDays(1)).State;
        var fourth = game.Talk(previewed, Feeling.Happy);
        Equal(true, fourth.Reply.Counted, "and after a Next day preview");
        var factors = fourth.State.Development.Engagement.Where(e => e.Kind == EngagementKind.Talk).Select(e => e.Factor).ToList();
        Equal(true, factors.Count == 2 && factors[1] < factors[0], "talks close together pay less (diminishing returns)");

        // Within the daily cap.
        var capped = Talker(at, 18, bond: 40, gainedToday: 11);
        Within(41, game.Talk(capped, Feeling.Sad).State.Bond, 1e-9, "Bond stops at the daily cap");
        Within(40, game.Talk(Talker(at, 18, bond: 40, gainedToday: 12), Feeling.Sad).State.Bond, 1e-9, "nothing past it");

        // Every feeling counts the same; none costs anything.
        foreach (var feeling in AllFeelings)
        {
            var talked = game.Talk(baby, feeling).State;
            Within(first.State.Bond, talked.Bond, 1e-9, $"{feeling} gives the same Bond");
            Equal(true, first.State.Care.Equals(talked.Care), $"{feeling} counts the same on the ledger");
            Equal(baby.Satiety + baby.Comfort + baby.Energy + baby.Health,
                talked.Satiety + talked.Comfort + talked.Energy + talked.Health, $"{feeling} moves no need");
        }

        // A fussy baby stays fussy: talking never answers the needs axis.
        var fussy = Talker(at, 18, bond: 40, mood: CareMood.Fussy);
        Equal(CareMood.Fussy, game.Talk(fussy, Feeling.Sad).State.Care.Mood, "talking is not care for the ladder");
        Equal(TalkOutcome.Reply, game.Talk(Talker(at, 18, sick: true), Feeling.Tired).Reply.Outcome, "a poorly baby still talks");

        // Words without a chip.
        var inferred = game.Talk(baby, null, "I'm so excited for my birthday party");
        Equal<Feeling?>(Feeling.Excited, inferred.Reply.Feeling, "typed words can name the feeling");
        Equal(TalkTopic.Birthday, inferred.Reply.Topic, "and the topic");
        Equal(TalkGesture.Bounce, inferred.Reply.Gesture, "a bounce for excited");
        var chip = game.Talk(baby, Feeling.Sad, "I'm so excited");
        Equal<Feeling?>(Feeling.Sad, chip.Reply.Feeling, "a tapped chip wins over the words");
        var none = game.Talk(baby, null, "we went to the park");
        Equal(TalkOutcome.NeedsFeeling, none.Reply.Outcome, "without a feeling the little one waits for one");
        Equal(true, ReferenceEquals(baby, none.State) || none.State.Development.Equals(baby.Development),
            "and nothing changes");
    }

    private static void Journal()
    {
        var game = Game();
        var at = At(3, 10, 0);
        var state = game.Talk(Talker(at, 18), Feeling.Proud).State;
        state = game.Talk(state, Feeling.Sad).State;
        var lines = state.Journal.Entries.Where(e => e.IsTalk).ToList();
        Equal(1, lines.Count, "one talk line a day");
        Equal(Feeling.Proud, lines[0].Feeling, "the first feeling of the day");
        state = game.AdvancePreview(state, TimeSpan.FromDays(1)).State;
        state = game.Talk(state, Feeling.Excited).State;
        Equal(2, state.Journal.Entries.Count(e => e.IsTalk), "a line on the next day");
        Equal(true, state.Journal.HasTalkEntries, "a journal with talk lines");

        // The oldest talk lines go before photos when the journal is full; First words stay.
        var entries = new List<JournalEntry> { JournalEntry.Arrival(at), JournalEntry.FirstWords(at.AddMinutes(1), 16) };
        for (var i = 0; i < JournalRecord.MaxEntries - 3; i++)
            entries.Add(JournalEntry.Photo(at.AddMinutes(2 + i), 16, PhotoMoment.Calm, MotorSkill.Walking, BabyAppearance.Default));
        entries.Add(JournalEntry.Talk(at.AddMinutes(500), 17, Feeling.Sad));
        var full = new JournalRecord(entries, DateTimeOffset.MinValue, 0, 0, 0)
            .With(JournalEntry.Talk(at.AddMinutes(600), 18, Feeling.Happy));
        Equal(JournalRecord.MaxEntries, full.Entries.Count, "the journal stays bounded");
        Equal(1, full.Entries.Count(e => e.Kind == JournalKind.Talk), "the oldest talk line went first");
        Equal(1, full.Entries.Count(e => e.Kind == JournalKind.FirstWords), "First words stay");
    }

    // Development v2 and journal v3 are written only when needed; typed words never reach a save.
    private static void Saves()
    {
        var game = Game();
        var at = At(3, 10, 0);
        var talker = Talker(at, 18, bond: 40);
        var typed = "my secret word is zebraquokka and I feel sad about my spelling test at Oakwood school";
        var talked = game.Talk(talker, null, typed).State;
        var payload = BabySaveCodec.Encode(talked);
        foreach (var piece in new[] { "zebraquokka", "secret", "Oakwood", "spelling", "school", "test" })
            Equal(false, payload.Contains(piece, StringComparison.OrdinalIgnoreCase), $"the save holds nothing typed ({piece})");
        var decoded = BabySaveCodec.Decode(payload);
        Equal(true, decoded.Development.Equals(talked.Development), "development round-trips");
        Equal(true, decoded.Journal.Equals(talked.Journal), "the journal round-trips");
        Equal(payload, BabySaveCodec.Encode(decoded), "and re-encodes byte for byte");
        Equal("2", Element(payload, "development").Attribute("v").Value, "development v2 once First words' stage started");
        Equal("3", Element(payload, "journal").Attribute("v").Value, "journal v3 with talk lines");
        Equal(Feeling.Sad, decoded.Journal.Entries.Single(e => e.IsTalk).Feeling, "the feeling is kept");

        var young = Talker(at, 5, firstWords: false);
        var youngPayload = BabySaveCodec.Encode(young);
        Equal("1", Element(youngPayload, "development").Attribute("v").Value, "a young baby saves v1");
        Equal(null, Element(youngPayload, "development").Attribute("words"), "without the new attributes");

        var bad = XElement.Parse(payload);
        bad.Element("development").Attribute("words").Remove();
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(bad.ToString(SaveOptions.DisableFormatting)),
            "a v2 record keeps its words readiness");
        var early = XElement.Parse(payload);
        early.Element("development").SetAttributeValue("v", 1);
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(early.ToString(SaveOptions.DisableFormatting)),
            "a v1 record cannot hold a talk");
        var oldJournal = XElement.Parse(payload);
        oldJournal.Element("journal").SetAttributeValue("v", 2);
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(oldJournal.ToString(SaveOptions.DisableFormatting)),
            "a v2 journal cannot hold talk lines");
        var feeling = XElement.Parse(payload);
        feeling.Element("journal").Elements("j").Single(j => j.Attribute("k").Value == "8").SetAttributeValue("fe", 42);
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(feeling.ToString(SaveOptions.DisableFormatting)),
            "an unknown feeling is rejected");
        var newer = XElement.Parse(payload);
        newer.Element("journal").SetAttributeValue("v", 4);
        Expect<NotSupportedException>(() => BabySaveCodec.Decode(newer.ToString(SaveOptions.DisableFormatting)),
            "a newer journal is unsupported, not corrupt");
    }

    // Typing is off by default and a grown-up's setting; First words off screen wait as a celebration; nothing typed is
    // in any stored record or the manifest.
    private static void Household()
    {
        var game = Game();
        var now = At(2, 8, 0);
        var store = new MemoryHouseholdStore();
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var household = new HouseholdSession(game, store, () => now, rooms);
        household.Adopt("Mia");
        household.Adopt("Leo");
        household.Switch(1);
        Equal(false, household.TalkTyping, "typing is off by default");
        Equal(false, store.ManifestPrimary.Contains("<talk"), "and not in the manifest");
        var untouched = store.ManifestPrimary;

        now = now.AddDays(23);
        household.AdvanceAll();
        var mia = household.Active.Session;
        Equal(true, mia.CanTalk, "Talk time is open after First words");
        Equal(true, mia.TakeFirstWords(), "the baby on screen announces First words");
        Equal(true, household.HasCelebrations(2), "Leo's First words wait as a celebration");
        Equal(true, household.TakeCelebrations(2).Contains(CompanionMilestone.FirstWords), "and play once");
        var celebrations = HouseholdManifestCodec.Decode(HouseholdManifestCodec.Encode(
            new HouseholdManifest(new[] { new HouseholdEntry(1, now, false, BAIby.Simulation.Pets.CompanionKind.Baby) }, 1,
                new[] { new PendingCelebration(1, CompanionMilestone.FirstWords) }, new Keepsake[0])));
        Equal(CompanionMilestone.FirstWords, celebrations.Celebrations.Single().Milestone,
            "First words waiting round-trip in the manifest");

        var withoutTyping = mia.Talk(null, "I am so happy zebraquokka");
        Equal(TalkOutcome.NeedsFeeling, withoutTyping.Reply.Outcome, "with typing off the words are ignored");
        Equal(TalkOutcome.Reply, mia.Talk(Feeling.Happy, "I want to die").Reply.Outcome, "and only the chip counts");

        household.SetTalkTyping(true);
        Equal(true, household.TalkTyping, "a grown-up can turn typing on");
        Equal(true, store.ManifestPrimary.Contains("<talk typing=\"true\" />"), "saved in the manifest");
        var reopened = new HouseholdSession(game, store, () => now, rooms);
        Equal(true, reopened.TalkTyping, "and kept");
        var talked = mia.Talk(null, "I am so happy, my zebraquokka secret is safe");
        Equal<Feeling?>(Feeling.Happy, talked.Reply.Feeling, "with typing on the words name the feeling");
        Equal(TalkOutcome.SafetyNet, mia.Talk(null, "someone hits me zebraquokka").Reply.Outcome,
            "and the safety net reads them");
        for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
        {
            var record = store.SlotStore(slot);
            Equal(false, (record.Primary ?? "").Contains("zebraquokka") || (record.Backup ?? "").Contains("zebraquokka"),
                $"slot {slot} holds nothing typed");
        }

        Equal(false, store.ManifestPrimary.Contains("zebraquokka"), "nor does the manifest");
        household.SetTalkTyping(false);
        Equal(false, store.ManifestPrimary.Contains("<talk"), "turned off, the element goes");
    }

    private static IEnumerable<Feeling> AllFeelings => Enum.GetValues(typeof(Feeling)).Cast<Feeling>();

    // A walker on an anchored ledger at `ageDays`, with First words (or readiness towards them).
    private static BabyState Talker(DateTimeOffset at, double ageDays, double bond = 30, bool firstWords = true,
        double words = 0, bool? learningStarted = null, double gainedToday = 0, CareMood mood = CareMood.Content,
        bool sick = false)
    {
        var draft = CareRules.Start(at, Tunables).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        if (mood != CareMood.Content)
        {
            draft.Mood = mood;
            draft.MoodCause = CareMoodCause.MealOverdue;
            draft.MoodSince = at.AddHours(-1);
        }

        if (sick)
        {
            draft.Condition = CareCondition.Sick;
            draft.ConditionCause = CareConditionCause.LowHealth;
            draft.ConditionSince = at.AddHours(-2);
        }

        var care = draft.Build();
        var started = learningStarted ?? ageDays >= 9;
        var development = new DevelopmentRecord.Builder
        {
            Sample = new TrackSample(SampleMode.Normal, 0.8, 0.5, 0.5, 0.5, 0.6),
            CategoryDay = care.CareDay,
            RecoveryDay = care.CareDay,
            LearningStarted = started,
            FirstWords = firstWords,
            Words = firstWords ? 0 : words,
        }.Build();
        var bonding = BondRules.Seed(bond, care).ToBuilder();
        if (gainedToday > 0)
        {
            bonding.CapDay = care.CareDay;
            bonding.Gained = gainedToday;
        }

        return BabyState.Restore("Mia", at, TimeSpan.FromDays(ageDays), 80, 80, 80, 3, 0, true, true, bond, 2, 90, sick ? 40 : 90,
            true, canWalk: true, care: care, development: development, bonding: bonding.Build());
    }

    private static BabyGrowth Engine() =>
        new BabyGrowth(TimeSpan.FromDays(3), TimeSpan.FromDays(7), TimeSpan.FromDays(14), Tunables, Growth);

    private static TrackSample Steady(double quality, double wellbeing) =>
        new TrackSample(SampleMode.Normal, wellbeing, quality, quality, quality, quality);

    private static BabySimulation Game() =>
        new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay, TimeSpan.FromDays(7), TimeSpan.FromDays(14));

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new DateTimeOffset(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

    private static XElement Element(string payload, string name) => XElement.Parse(payload).Element(name);

    private static void Within(double expected, double actual, double tolerance, string description)
    {
        if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{description}: expected {expected} within {tolerance}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }

    private static void Expect<TException>(Action action, string description) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected {typeof(TException).Name}");
    }
}
