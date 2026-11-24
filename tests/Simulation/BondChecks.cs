using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

// Care slice 5: Bond (gains, losses, caps, floor, doubled gains below the high-water mark, answering in time),
// temperament, the care rhythm (grace, halving, freezing, bonus, stickers), the journal (entries, photos, caps, hard
// days, weekly pages), earned looks, split advances and the v16 save. See docs/care-and-growth.md (Bond and temperament,
// Return loops).
static class BondChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly BondTunables Bond = BondTunables.Default;
    private static readonly TimeSpan Paris = TimeSpan.FromHours(1);

    public static void Run()
    {
        Gains();
        AnsweringInTime();
        DailyGainCap();
        DoubledBelowHighWater();
        Losses();
        LossCapAndHelper();
        Floor();
        TemperamentDrift();
        TemperamentFelt();
        RhythmRules();
        RhythmInTheNursery();
        RhythmBonus();
        PreviewDays();
        JournalEntries();
        Photos();
        JournalCap();
        HardDays();
        WeekPages();
        EarnedLooksRules();
        SplitAdvances();
        SaveAndMigration();
        Validation();
        Console.WriteLine("Bond, rhythm and journal checks passed.");
    }

    private static void Gains()
    {
        var game = Game();
        var at = At(2026, 3, 2, 8, 0);
        var baby = Baby(at, bond: 20, satiety: 50);
        var fed = game.ApplyCare(baby, CareAction.Feed).State;
        Near(21, fed.Bond, "an on-time meal adds Bond +1");
        var soothed = game.ApplyCare(fed, CareAction.Soothe).State;
        Near(24, soothed.Bond, "a soothe adds Bond +3");
        var read = game.ApplyCare(soothed, CareAction.ReadStory).State;
        Near(26, read.Bond, "a story adds Bond +2");
        var played = game.ApplyCare(read, CareAction.Play).State;
        Near(29, played.Bond, "play adds Bond +1, and the fourth kind of care today adds the variety +2");
        Near(29, played.Bonding.HighWater, "the high-water mark follows Bond up");

        var late = Baby(At(2026, 3, 2, 11, 0), bond: 20, satiety: 50);
        var lateMeal = game.ApplyCare(late, CareAction.Feed);
        Equal(CareReason.MealLate, lateMeal.State.Care.LastReason, "an 11:00 breakfast is late");
        Near(20, lateMeal.State.Bond, "a late meal adds no Bond");
        var full = Baby(at, bond: 20, satiety: 95);
        Near(20, game.ApplyCare(full, CareAction.Feed).State.Bond, "overfeeding is no meal and adds no Bond");
    }

    private static void AnsweringInTime()
    {
        var game = Game();
        var at = At(2026, 3, 2, 13, 0);
        // Hungry and fussing for ten minutes: a meal (outside any window) answers it.
        var fussy = Baby(at, bond: 20, satiety: 30, mood: CareMood.Fussy, moodSince: at.AddMinutes(-10));
        var answered = game.ApplyCare(fussy, CareAction.Feed).State;
        Equal(CareMood.Content, answered.Care.Mood, "the meal calms the fussing");
        Near(25, answered.Bond, "answering fussing within 30 minutes adds Bond +5");
        Equal(1, answered.Bonding.Answered, "the day's tally counts the answer");

        var waited = Baby(at, bond: 20, satiety: 30, mood: CareMood.Fussy, moodSince: at.AddMinutes(-31));
        var late = game.ApplyCare(waited, CareAction.Feed).State;
        Equal(CareMood.Content, late.Care.Mood, "a later meal still calms the fussing");
        Near(20, late.Bond, "after 30 minutes the answer adds nothing");
        Equal(0, late.Bonding.Answered, "nor counts for Security");

        // Upset: feeding first (Fullness 50) is not yet an answer; the soothe that follows is.
        var upset = Baby(at, bond: 20, satiety: 20, comfort: 50, mood: CareMood.Upset, moodSince: at.AddMinutes(-5));
        var meal = game.ApplyCare(upset, CareAction.Feed).State;
        Equal(CareMood.Upset, meal.Care.Mood, "food alone leaves an upset baby upset");
        Near(20, meal.Bond, "so no answer yet");
        var calmed = game.ApplyCare(meal, CareAction.Soothe).State;
        Equal(CareMood.Content, calmed.Care.Mood, "the soothe calms the upset baby");
        Near(28, calmed.Bond, "answering upset within 30 minutes adds +5 with the soothe's +3");
    }

    private static void DailyGainCap()
    {
        var game = Game();
        var at = At(2026, 3, 2, 13, 0);
        var baby = Baby(at, bond: 20);
        for (var i = 0; i < 5; i++)
            baby = game.ApplyCare(baby, CareAction.Soothe).State;
        Near(32, baby.Bond, "five soothes give at most +12 in a care day");
        Near(12, baby.Bonding.Gained, "the day's raw gains are counted");
        var tomorrow = game.Advance(baby, At(2026, 3, 3, 9, 0)).State;
        var again = game.ApplyCare(tomorrow, CareAction.Soothe).State;
        Equal(again.Care.CareDay, again.Bonding.CapDay, "the cap rolls over with the care day");
        Near(3, again.Bonding.Gained, "a new care day counts afresh");
        Equal(true, again.Bond > tomorrow.Bond, "and a soothe adds Bond again");
    }

    private static void DoubledBelowHighWater()
    {
        var game = Game();
        var at = At(2026, 3, 2, 13, 0);
        var below = Baby(at, bond: 40, high: 50);
        Near(46, game.ApplyCare(below, CareAction.Soothe).State.Bond, "gains count double below the high-water mark");
        var near = Baby(at, bond: 49, high: 50);
        Near(52.5, game.ApplyCare(near, CareAction.Soothe).State.Bond, "doubled up to the mark, then as usual");
        var top = Baby(at, bond: 99, high: 99);
        Near(100, game.ApplyCare(top, CareAction.Soothe).State.Bond, "Bond stops at 100");
    }

    private static void Losses()
    {
        var game = Game();
        // Content and well fed after lunch: the snack window closes missed at 18:00.
        var at = At(2026, 3, 2, 13, 0);
        var baby = Baby(at, bond: 20, satiety: 100, comfort: 100, energy: 100);
        Near(20, game.Advance(baby, At(2026, 3, 2, 17, 59)).State.Bond, "nothing is lost before the snack window closes");
        Near(18, game.Advance(baby, At(2026, 3, 2, 18, 0)).State.Bond, "a missed meal costs Bond -2");

        var fussy = Baby(at, bond: 20, satiety: 30, mood: CareMood.Fussy, moodSince: at);
        Near(20, game.Advance(fussy, at.AddMinutes(119)).State.Bond, "fussing under two hours costs nothing");
        Near(18, game.Advance(fussy, at.AddHours(2)).State.Bond, "two hours of unanswered fussing cost Bond -2");
        var upset = Baby(at, bond: 20, satiety: 10, mood: CareMood.Upset, moodSince: at);
        Near(20, game.Advance(upset, at.AddMinutes(179)).State.Bond, "upset under three hours costs nothing");
        Near(19, game.Advance(upset, at.AddHours(3)).State.Bond, "each three hours upset cost Bond -1");
    }

    // Upset from 08:00 with nobody coming: day one loses 3 meals and 6 upset spells (-12), capped at -8; day two loses
    // 6 upset spells and 2 meals before the absence ceiling (20:00), capped at -8, and its dinner and bedtime are
    // covered, so it is a helper day: Security -1 (and no more Bond: the helper cap of -4 is spent).
    private static void LossCapAndHelper()
    {
        var game = Game();
        var at = At(2026, 3, 2, 8, 0);
        var upset = Baby(at, bond: 50, satiety: 10, mood: CareMood.Upset, moodSince: at);
        var end = game.Advance(upset, At(2026, 3, 4, 8, 0)).State;
        Near(34, end.Bond, "each day's losses stop at -8");
        Near(-13, end.Bonding.Security, "a day of missed meals moves Security a fifth of the way to -60, a helper day -1 more");

        // Two weeks away from Bond 55: the floor of 60% holds it at 33 (docs/care-and-growth.md, Absence).
        var evening = At(2026, 3, 2, 19, 0);
        var left = Baby(evening, bond: 55, satiety: 90, comfort: 90, energy: 90);
        var back = game.Advance(left, evening.AddDays(14)).State;
        Near(33, back.Bond, "a long absence settles at the floor, 60% of the high-water mark");
        Equal(true, back.Bonding.Security < -10, "each helper day takes a little Security");
        Equal(0, back.Bonding.Rhythm, "an absence has no rhythm to keep");

        // The Absence table (docs/care-and-growth.md): from 19:00 at Bond 55, fed and content when left.
        var table = new[] { 1, 3, 14 }.Select(days => game.Advance(left, evening.AddDays(days)).State.Bond).ToArray();
        Console.WriteLine($"  absence from 19:00 at Bond 55: 1 day {table[0]:0.#}, 3 days {table[1]:0.#}, 2 weeks {table[2]:0.#}");
        Equal(true, table[0] > table[1] && table[1] >= table[2], "a longer absence costs more Bond, down to the floor");

        // Away mode costs nothing.
        var away = game.StartAway(Baby(evening, bond: 55, satiety: 90, comfort: 90, energy: 90), TimeSpan.FromDays(3));
        var home = game.Advance(away, evening.AddDays(3).AddMinutes(-1)).State;
        Near(55, home.Bond, "Away mode costs no Bond");
        Near(0, home.Bonding.Security, "nor Security");
    }

    private static void Floor()
    {
        var game = Game();
        // Bond 20 (high-water 20): a day and a half of misses would cost -10, but Bond stops at 12.
        var at = At(2026, 3, 2, 13, 0);
        var baby = Baby(at, bond: 20, satiety: 100, comfort: 100, energy: 100);
        var back = game.Advance(baby, At(2026, 3, 5, 9, 0)).State;
        Near(12, back.Bond, "Bond never falls below 60% of its high-water mark");
        Near(20, back.Bonding.HighWater, "the high-water mark is kept");
        Near(12 * 1, BondRules.Floor(back.Bonding, Bond), "the floor is 60% of the mark");
    }

    private static void TemperamentDrift()
    {
        var at = At(2026, 3, 2, 10, 0);
        var care = Anchored(at);
        var record = BondRules.Seed(30, care);
        Equal(SecurityPhase.Steady, BondRules.SecurityOf(record, Bond), "a new relationship is steady");

        // Days with fussing answered twice: Security moves a fifth of the way to 85 each day, settled on the third.
        var settled = record;
        var values = new List<double>();
        for (var day = 0; day < 3; day++)
        {
            var careDay = care.CareDay + day;
            settled = BondRules.Tally(settled, careDay, false, false, false, true, null);
            settled = BondRules.Tally(settled, careDay, false, false, false, true, null);
            settled = CloseDay(settled, care, careDay, Summary(3, 0));
            values.Add(settled.Security);
        }

        Near(17, values[0], "one answered day moves Security a fifth of the way to its target");
        Near(30.6, values[1], "a second day another fifth");
        Equal(SecurityPhase.Steady, BondRules.SecurityOf(Seeded(30.6), Bond), "30.6 is still steady");
        Equal(SecurityPhase.Settled, BondRules.SecurityOf(settled, Bond), "three such days settle the baby (41.5)");

        // Days with every meal missed: a fifth of the way to -60, a clingy phase on the third.
        var clingy = record;
        for (var day = 0; day < 3; day++)
            clingy = CloseDay(clingy, care, care.CareDay + day, Summary(0, 3));
        Near(-29.28, clingy.Security, "three missed days bring Security to -29.28");
        Equal(SecurityPhase.Clingy, BondRules.SecurityOf(clingy, Bond), "a clingy phase");
        Equal(SecurityPhase.Clingy, BondRules.SecurityOf(Seeded(-25), Bond), "-25 is clingy");
        Equal(SecurityPhase.Steady, BondRules.SecurityOf(Seeded(-24.9), Bond), "just above -25 is not");
        Equal(SecurityPhase.Settled, BondRules.SecurityOf(Seeded(40), Bond), "+40 is settled");

        // Curiosity follows the kinds of play-like care in the day; meals and washes are routine.
        var explorer = record;
        var cautious = record;
        for (var day = 0; day < 5; day++)
        {
            var careDay = care.CareDay + day;
            foreach (var kind in new[] { EngagementKind.PlayRattle, EngagementKind.PlayPlush, EngagementKind.Story,
                EngagementKind.Soothe, EngagementKind.Meal })
                explorer = BondRules.Tally(explorer, careDay, false, false, false, false, kind);
            explorer = CloseDay(explorer, care, careDay, Summary(3, 0));
            cautious = BondRules.Tally(cautious, careDay, true, false, false, false, EngagementKind.Meal);
            cautious = CloseDay(cautious, care, careDay, Summary(3, 0));
        }

        Near(80 * (1 - Math.Pow(0.8, 5)), explorer.Curiosity, "four kinds of play a day lead towards +80");
        Equal(CuriosityPhase.Explorer, BondRules.CuriosityOf(explorer, Bond), "an explorer after five such days");
        Near(-40 * (1 - Math.Pow(0.8, 5)), cautious.Curiosity, "days of routine only lead towards -40");
        Equal(CuriosityPhase.Cautious, BondRules.CuriosityOf(cautious, Bond), "a cautious little one after five days");

        // Covered, away and excused days freeze temperament.
        foreach (var (summary, name) in new[]
        {
            (new CareDaySummary(0, 0, false, false, false, true, false), "an away day"),
            (new CareDaySummary(1, 0, true, true, false, false, true), "an excused day"),
        })
        {
            Near(settled.Security, CloseDay(settled, care, care.CareDay + 5, summary).Security, $"{name} leaves Security");
        }

        Near(settled.Security - 1, CloseDay(settled, care, care.CareDay + 5,
            new CareDaySummary(0, 0, false, false, true, false, false)).Security, "a helper day takes Security -1 only");
        Near(BondRecord.AxisMin, CloseDay(Seeded(-59.5), care, care.CareDay + 5,
            new CareDaySummary(0, 0, false, false, true, false, false)).Security, "Security stops at -60");
    }

    private static void TemperamentFelt()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var steady = Baby(at, bond: 20, comfort: 80);
        var clingy = Baby(at, bond: 20, comfort: 80, security: -30);
        Equal(SecurityPhase.Clingy, game.Security(clingy), "Security -30 is a clingy phase");
        Near(72, game.Advance(steady, at.AddHours(2)).State.Comfort, "Comfort falls 4 an awake hour");
        Near(70, game.Advance(clingy, at.AddHours(2)).State.Comfort, "in a clingy phase it falls 1.25 times faster");

        // An explorer enjoys a fresh kind of play a little more; a cautious little one a familiar one.
        var plain = Baby(at, bond: 20);
        var explorer = Baby(at, bond: 20, curiosity: 60);
        var cautious = Baby(at, bond: 20, curiosity: -40);
        var plainFactor = LastFactor(game.ApplyCare(plain, CareAction.ReadStory).State);
        Near(plainFactor * Bond.TemperamentEngagement, LastFactor(game.ApplyCare(explorer, CareAction.ReadStory).State),
            "an explorer's fresh play pays x1.05");
        Near(plainFactor, LastFactor(game.ApplyCare(cautious, CareAction.ReadStory).State), "a cautious first story pays as usual");
        var plainAgain = LastFactor(game.ApplyCare(game.ApplyCare(plain, CareAction.ReadStory).State, CareAction.ReadStory).State);
        var cautiousAgain = LastFactor(game.ApplyCare(game.ApplyCare(cautious, CareAction.ReadStory).State, CareAction.ReadStory).State);
        Near(plainAgain * Bond.TemperamentEngagement, cautiousAgain, "a cautious little one's familiar story pays x1.05");
    }

    private static void RhythmRules()
    {
        var at = At(2026, 3, 2, 10, 0);
        var care = Anchored(at);
        var day = care.CareDay;
        var record = BondRules.Seed(30, care);
        var good = Summary(2, 1);
        Equal(false, Qualifies(Summary(1, 2)), "one meal is not a care day");
        Equal(false, Qualifies(new CareDaySummary(2, 1, false, true, false, false, false)), "no together time is not a care day");
        Equal(false, Qualifies(new CareDaySummary(2, 1, true, false, false, false, false)), "no bedtime is not a care day");
        var stickers = new List<int>();
        for (var i = 0; i < 4; i++)
            record = CloseDay(record, care, day + i, good, stickers);
        Equal(4, record.Rhythm, "four care days make a rhythm of 4");
        Equal(4, record.TotalCareDays, "and four care days in all");
        Equal("3", string.Join(",", stickers), "a sticker at 3 days");

        var missed = new CareDaySummary(1, 2, false, false, false, false, false);
        var graced = CloseDay(record, care, day + 4, missed);
        Equal(4, graced.Rhythm, "the first missed day is a grace day");
        Equal(day + 4, graced.GraceDay, "the grace day is remembered");
        var halved = CloseDay(graced, care, day + 5, missed);
        Equal(2, halved.Rhythm, "another missed day within a week halves the rhythm");
        Equal(4, halved.LongestRhythm, "the longest rhythm is kept");
        Equal(4, halved.TotalCareDays, "and every care day counted");
        var weekLater = CloseDay(halved, care, day + 11, missed);
        Equal(2, weekLater.Rhythm, "a week after the last grace day, another grace day");
        Equal(1, CloseDay(weekLater, care, day + 12, missed).Rhythm, "then halving again");
        foreach (var (frozen, name) in new[]
        {
            (new CareDaySummary(0, 0, false, false, true, false, false), "a helper day"),
            (new CareDaySummary(0, 0, false, false, false, true, false), "an away day"),
            (new CareDaySummary(0, 0, false, false, false, false, true), "an excused day"),
        })
        {
            var kept = CloseDay(halved, care, day + 6, frozen);
            Equal(2, kept.Rhythm, $"{name} freezes the rhythm");
            Equal(halved.GraceDay, kept.GraceDay, $"{name} uses no grace");
        }

        var long21 = record;
        stickers.Clear();
        for (var i = 4; i < 21; i++)
            long21 = CloseDay(long21, care, day + i, good, stickers);
        Equal("7,14,21", string.Join(",", stickers), "stickers at 7, 14 and every 7 days");
        Equal("3,7,14,21", string.Join(",", BondRules.Stickers(long21.LongestRhythm)), "kept forever with the longest rhythm");
        Equal(28, BondRules.NextSticker(21), "the next sticker is at 28");
        Equal(3, BondRules.NextSticker(0), "the first sticker is at 3");
        Equal(0, CloseDay(BondRules.Seed(0, care), care, day, missed).Rhythm, "a missed day with no rhythm spends no grace");
        Equal(0L, CloseDay(BondRules.Seed(0, care), care, day, missed).GraceDay, "so no grace day is used");
    }

    // A whole week of real care days in the nursery: the rhythm, its sticker, a grace day, halving, and an away day.
    private static void RhythmInTheNursery()
    {
        var game = Game();
        var start = At(2026, 3, 2, 7, 0);
        var baby = Baby(start, bond: 20, satiety: 60, comfort: 80, energy: 80);
        for (var day = 0; day < 3; day++)
            baby = CareDay(game, baby, start.AddDays(day), refresh: null).State;
        var morning = game.Advance(baby, start.AddDays(3).AddMinutes(10)).State;
        Equal(3, morning.Bonding.Rhythm, "three cared-for days make a rhythm of 3");
        Equal(3, morning.Bonding.TotalCareDays, "three care days in all");
        Equal(1, morning.Journal.Entries.Count(entry => entry.Kind == JournalKind.Sticker && entry.Count == 3),
            "the journal keeps a 3-day sticker");
        // One breakfast a day is not a care day (and keeps the absence ceiling away).
        var oneMeal = game.ApplyCare(game.Advance(morning, start.AddDays(3).AddHours(1)).State, CareAction.Feed).State;
        var skipped = game.Advance(oneMeal, start.AddDays(4).AddMinutes(10)).State;
        Equal(3, skipped.Bonding.Rhythm, "a missed day is graced");
        oneMeal = game.ApplyCare(game.Advance(skipped, start.AddDays(4).AddHours(1)).State, CareAction.Feed).State;
        var again = game.Advance(oneMeal, start.AddDays(5).AddMinutes(10)).State;
        Equal(1, again.Bonding.Rhythm, "a second missed day halves it");
        Equal(3, again.Bonding.LongestRhythm, "the longest rhythm stays 3");

        var away = game.StartAway(CareDay(game, baby, start.AddDays(3), refresh: null).State, TimeSpan.FromDays(2));
        var returned = game.Advance(away, start.AddDays(5).AddMinutes(10)).State;
        Equal(4, returned.Bonding.Rhythm, "away days freeze the rhythm");
    }

    private static void RhythmBonus()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var plain = LastFactor(game.ApplyCare(Baby(at, bond: 20), CareAction.ReadStory).State);
        Near(plain * 1.05, LastFactor(game.ApplyCare(Baby(at, bond: 20, rhythm: 3), CareAction.ReadStory).State),
            "a 3-day rhythm pays engagement x1.05");
        Near(plain * 1.10, LastFactor(game.ApplyCare(Baby(at, bond: 20, rhythm: 7), CareAction.ReadStory).State),
            "a 7-day rhythm pays engagement x1.10");
        Near(plain, LastFactor(game.ApplyCare(Baby(at, bond: 20, rhythm: 2), CareAction.ReadStory).State),
            "a shorter rhythm pays as usual");
    }

    // Next day: the care since the last day mark counts for the rhythm; with none it freezes.
    private static void PreviewDays()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = game.Adopt("Mia", at);
        baby = game.AdvancePreview(baby, TimeSpan.FromDays(1)).State;
        Equal(0, baby.Bonding.Rhythm, "a preview day without care freezes the rhythm");
        for (var day = 0; day < 3; day++)
        {
            baby = game.ApplyCare(baby, CareAction.Feed).State;
            baby = game.ApplyCare(baby, CareAction.Feed).State;
            baby = game.ApplyCare(baby, CareAction.Play).State;
            baby = game.ApplyCare(baby, CareAction.Rest).State;
            baby = game.AdvancePreview(baby, TimeSpan.FromDays(1)).State;
        }

        Equal(3, baby.Bonding.Rhythm, "three cared-for preview days make a rhythm of 3");
        Equal(true, baby.Journal.Entries.Any(entry => entry.Kind == JournalKind.Sticker && entry.Count == 3),
            "and a sticker in the journal");
        Near(0, baby.Bonding.Gained, "each preview day starts the Bond caps again");
        var idle = game.AdvancePreview(baby, TimeSpan.FromDays(1)).State;
        Equal(3, idle.Bonding.Rhythm, "an idle preview day never halves the rhythm");
    }

    private static void JournalEntries()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = game.Adopt("Mia", at);
        Equal(JournalKind.Arrival, baby.Journal.Entries[0].Kind, "a new journal begins with the arrival");
        Equal(1, baby.Journal.Entries[0].Day, "on day 1");

        // A sitter whose readiness is nearly full sits during the afternoon, on the day and at the instant it happened.
        var sitter = Baby(At(2026, 3, 2, 13, 0), bond: 20, satiety: 90, comfort: 90, energy: 90, age: TimeSpan.FromDays(2.6),
            sitting: false);
        sitter = WithReadiness(sitter, 0.98);
        var whole = game.Advance(sitter, At(2026, 3, 2, 17, 0));
        Equal(true, whole.UnlockedSitting, "the baby sits during the afternoon");
        var entry = whole.State.Journal.Entries.Single(e => e.Kind == JournalKind.Milestone);
        Equal(MotorSkill.Sitting, entry.Skill, "the journal records sitting");
        Equal(3, entry.Day, "on day 3");
        Equal(true, entry.At > sitter.TimeCheckpoint && entry.At < whole.State.TimeCheckpoint,
            "at the instant it happened, not at the refresh");
        var split = Stepped(game, sitter, At(2026, 3, 2, 17, 0), 13);
        Equal(entry, split.Journal.Entries.Single(e => e.Kind == JournalKind.Milestone), "short advances write the same entry");
    }

    private static void Photos()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = game.Adopt("Mia", at);
        Equal(3, game.PhotosLeft(baby), "three photos a day");
        var taken = new List<bool>();
        foreach (var moment in new[] { PhotoMoment.Calm, PhotoMoment.Playing, PhotoMoment.Story, PhotoMoment.Cuddle })
        {
            var photo = game.TakePhoto(baby, moment);
            taken.Add(photo.Taken);
            baby = photo.State;
        }

        Equal("True,True,True,False", string.Join(",", taken), "the fourth photo of the day is not taken");
        Equal(0, game.PhotosLeft(baby), "no photos left today");
        var photos = baby.Journal.Entries.Where(e => e.Kind == JournalKind.Photo).ToList();
        Equal(3, photos.Count, "three photos in the journal");
        Equal(PhotoMoment.Story, photos[2].Moment, "each keeps its moment");
        Equal(BabyAppearance.Default, photos[0].Look, "and the look of the day");
        Equal(MotorSkill.None, photos[0].Skill, "and the stage shown");
        var styled = game.TakePhoto(game.AdvancePreview(baby, TimeSpan.FromDays(1)).State.WithAppearance(
            BabyAppearance.Default.WithOutfit(Outfit.Sky)), PhotoMoment.Napping);
        Equal(true, styled.Taken, "the next day brings three more");
        Equal(Outfit.Sky, styled.State.Journal.Entries.Last().Look.Outfit, "a photo keeps the outfit worn");
        Equal(2, styled.State.Journal.Entries.Last().Day, "on day 2");
    }

    private static void JournalCap()
    {
        var at = At(2026, 3, 2, 10, 0);
        var journal = JournalRules.Start(at).With(JournalEntry.Milestone(at.AddMinutes(1), 1, MotorSkill.Sitting));
        for (var i = 0; i < JournalRecord.MaxEntries; i++)
            journal = journal.With(JournalEntry.Photo(at.AddMinutes(2 + i), 1 + i / 3, PhotoMoment.Calm, MotorSkill.Sitting,
                BabyAppearance.Default));
        Equal(JournalRecord.MaxEntries, journal.Entries.Count, "a journal holds at most 200 entries");
        Equal(JournalKind.Arrival, journal.Entries[0].Kind, "the arrival stays");
        Equal(JournalKind.Milestone, journal.Entries[1].Kind, "milestones stay");
        Equal(at.AddMinutes(4).UtcTicks, journal.Entries[2].At.UtcTicks, "the oldest photos go first");
        var withWeek = journal.With(JournalEntry.Week(at.AddDays(10), 7, 1, 5));
        Equal(JournalKind.Week, withWeek.Entries.Last().Kind, "a new entry joins in time order");
        Equal(true, XElement.Parse(BabySaveCodec.Encode(Game().Adopt("Mia", at).With(journal: withWeek))).ToString().Length < 24000,
            "a full journal keeps the save small");
    }

    private static void HardDays()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        // A little under the weather since yesterday: Dr. Lee sends the baby home well.
        var poorly = Baby(at, bond: 20, health: 60, condition: CareCondition.UnderTheWeather, conditionSince: at.AddDays(-1));
        var journal = game.Journal(poorly);
        Equal(true, journal.InSpell, "a spell under way is seeded from its start");
        var visited = game.VisitDoctor(poorly);
        Equal(Reaction.DoctorAllClear, visited.Reaction, "Dr. Lee sends the baby home well");
        var hard = visited.State.Journal.Entries.Single(e => e.Kind == JournalKind.HardDay);
        Equal(HardSpell.Poorly, hard.Spell, "a poorly spell we got through");
        Equal(at.AddDays(-1).UtcTicks, hard.Since.UtcTicks, "from the day it began");
        Equal(at.UtcTicks, hard.At.UtcTicks, "until today");
        Equal(false, visited.State.Journal.InSpell, "the spell is over");

        // The spell begins when the baby stops being well and ends with the medicine course.
        var well = JournalRules.Start(at);
        var started = JournalRules.AfterCondition(well, CareCondition.Well, CareCondition.UnderTheWeather, at, TimeSpan.FromDays(5));
        Equal(6, started.SpellDay, "the spell began on day 6");
        var stillUnwell = JournalRules.AfterCondition(started, CareCondition.Sick, CareCondition.Recovering, at.AddHours(5),
            TimeSpan.FromDays(5.3));
        Equal(started, stillUnwell, "recovering is still the same spell");
        var over = JournalRules.AfterCondition(stillUnwell, CareCondition.Recovering, CareCondition.Well, at.AddDays(2),
            TimeSpan.FromDays(7));
        var entry = over.Entries.Last();
        Equal(6, entry.SinceDay, "it ran from day 6");
        Equal(8, entry.Day, "to day 8");

        // A shy skill back after a good day.
        var shy = Baby(At(2026, 3, 2, 18, 30), bond: 20, satiety: 50, age: TimeSpan.FromDays(15), crawling: true, walking: true);
        shy = WithDevelopment(shy, draft =>
        {
            draft.Shy = MotorSkill.Walking;
            draft.SetbackSince = shy.TimeCheckpoint.AddDays(-1);
            draft.RecoveryDay = shy.Care.CareDay;
            draft.RecoveryMeals = 2;
            draft.RecoverySoothes = 2;
        });
        var back = game.ApplyCare(shy, CareAction.Feed);
        Equal(Reaction.SkillBack, back.Reaction, "the third on-time meal brings walking back");
        var shyEntry = back.State.Journal.Entries.Last();
        Equal(HardSpell.Shy, shyEntry.Spell, "the journal remembers the shy spell");
        Equal(MotorSkill.Walking, shyEntry.Skill, "and which skill came back");
        Equal(15, shyEntry.SinceDay, "it began on day 15");
    }

    private static void WeekPages()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = game.Adopt("Mia", at);
        for (var day = 0; day < 8; day++)
        {
            // A newborn is still full on the first day, so care comes on days 2, 4, 6 and 8.
            if (day % 2 == 1)
            {
                baby = game.ApplyCare(baby, CareAction.Feed).State;
                baby = game.ApplyCare(baby, CareAction.Feed).State;
                baby = game.ApplyCare(baby, CareAction.Play).State;
                baby = game.ApplyCare(baby, CareAction.Rest).State;
            }

            baby = game.AdvancePreview(baby, TimeSpan.FromDays(1)).State;
        }

        var weeks = baby.Journal.Entries.Where(e => e.Kind == JournalKind.Week).ToList();
        Equal(1, weeks.Count, "eight days make one week page");
        Equal(1, weeks[0].Count, "for week 1");
        Equal(7, weeks[0].Day, "ending on day 7");
        Equal(3, weeks[0].CareDays, "with its three care days (the fourth falls in week 2)");
        Equal(1, baby.Journal.Weeks, "the journal knows week 1 has its page");

        // A real week: pages come at the instant the week ended, and short advances agree.
        var real = Baby(At(2026, 3, 2, 10, 0), bond: 20, age: TimeSpan.FromDays(6.9), satiety: 90, comfort: 90, energy: 90);
        var whole = game.Advance(real, At(2026, 3, 2, 14, 0)).State;
        var page = whole.Journal.Entries.Single(e => e.Kind == JournalKind.Week);
        Equal(At(2026, 3, 2, 12, 24).UtcTicks, page.At.UtcTicks, "the page is dated when day 7 ended");
        Equal(page, Stepped(game, real, At(2026, 3, 2, 14, 0), 17).Journal.Entries.Single(e => e.Kind == JournalKind.Week),
            "short advances write the same page");
    }

    private static void EarnedLooksRules()
    {
        var daisy = BabyAppearance.Default.WithHeadwear(Headwear.DaisyClip);
        var midnight = BabyAppearance.Default.WithOutfit(Outfit.Midnight);
        var bucket = BabyAppearance.Default.WithHeadwear(Headwear.BucketHat);
        Equal(false, EarnedLooks.CanWear(BabyAppearance.Default, daisy, 2), "the Daisy clip waits for a 3-day rhythm");
        Equal(true, EarnedLooks.CanWear(BabyAppearance.Default, daisy, 3), "and is earned at 3 days");
        Equal(false, EarnedLooks.CanWear(BabyAppearance.Default, midnight, 6), "the Midnight outfit waits for 7");
        Equal(true, EarnedLooks.CanWear(BabyAppearance.Default, bucket, 14), "the Bucket hat comes at 14");
        Equal(true, EarnedLooks.CanWear(midnight, midnight.WithHeadwear(Headwear.Bow), 0),
            "a look already worn stays wearable while other parts change");
        Equal(true, EarnedLooks.CanWear(BabyAppearance.Default, BabyAppearance.Default.WithOutfit(Outfit.Sky), 0),
            "other looks are always free");

        var now = At(2026, 3, 2, 10, 0);
        var saves = new BondSaveStore();
        var session = new BabySession(Game(), saves, () => now);
        session.Adopt("Mia");
        Expect<InvalidOperationException>(() => session.SetAppearance(daisy), "an unearned look cannot be put on");
        Equal(false, session.CanWear(daisy), "the session says so");
        Equal(true, session.CanWear(daisy, unlockEarned: true), "developer mode unlocks earned looks");
        session.SetAppearance(daisy, unlockEarned: true);
        Equal(Headwear.DaisyClip, session.State.Appearance.Headwear, "and puts them on");
    }

    // One long advance and many short ones agree while the mood holds: an upset baby through two days of losses, and
    // three care days refreshed every 23 minutes or every 4 hours.
    private static void SplitAdvances()
    {
        var game = Game();
        var at = At(2026, 3, 2, 8, 0);
        var upset = Baby(at, bond: 50, satiety: 10, mood: CareMood.Upset, moodSince: at);
        var end = At(2026, 3, 4, 9, 30);
        var whole = game.Advance(upset, end).State;
        var split = Stepped(game, upset, end, 10);
        Near(whole.Bond, split.Bond, "Bond is the same in one advance or many");
        Equal(whole.Bonding, split.Bonding, "and so are its caps, temperament and rhythm");

        var start = At(2026, 3, 2, 7, 0);
        var baby = Baby(start, bond: 20, satiety: 60, comfort: 80, energy: 80);
        var quiet = baby;
        var busy = baby;
        // The quiet player still opens the app every few hours: a longer gap would pass the 8-hour decay cap, which is
        // path dependent on purpose.
        for (var day = 0; day < 3; day++)
        {
            quiet = CareDay(game, quiet, start.AddDays(day), refresh: 240).State;
            busy = CareDay(game, busy, start.AddDays(day), refresh: 23).State;
        }

        var morning = start.AddDays(3).AddMinutes(30);
        quiet = Stepped(game, quiet, morning, 240);
        busy = Stepped(game, busy, morning, 23);
        Equal(3, quiet.Bonding.Rhythm, "the scripted days count");
        Near(quiet.Bond, busy.Bond, "refreshing in between changes no Bond");
        Equal(quiet.Bonding, busy.Bonding, "nor temperament or the rhythm");
        Equal(Describe(quiet.Journal), Describe(busy.Journal), "nor the journal");
    }

    private static void SaveAndMigration()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = game.TakePhoto(game.ApplyCare(Baby(at, bond: 30, security: 12.5, curiosity: -3, rhythm: 4),
            CareAction.Soothe).State, PhotoMoment.Cuddle).State;
        baby = baby.With(journal: baby.Journal.With(JournalEntry.HardDay(at, 4, HardSpell.Shy, at.AddDays(-1), 3,
            MotorSkill.Sitting)).With(JournalEntry.Week(at.AddMinutes(1), 7, 1, 3)).With(JournalEntry.Sticker(at.AddMinutes(2), 4, 3)));
        var payload = BabySaveCodec.Encode(baby);
        Equal(true, payload.StartsWith("<baby version=\"16\"") && payload.Contains("<bond v=\"1\"") &&
            payload.Contains("<journal v=\"1\""), "the save is v16 with bond and journal elements");
        var decoded = BabySaveCodec.Decode(payload);
        Equal(baby.Bonding, decoded.Bonding, "the bond record round-trips exactly");
        Equal(baby.Journal, decoded.Journal, "the journal round-trips exactly");
        Near(baby.Bond, decoded.Bond, "Bond is saved in the connection element");

        // A v15 baby: Connection becomes Bond, its high-water mark is the Bond itself, temperament is neutral, the rhythm
        // 0, and the journal is seeded with the milestones already reached on their nominal days.
        var crawler = Baby(at, bond: 34, age: TimeSpan.FromDays(8.5), crawling: true);
        var v15 = XElement.Parse(BabySaveCodec.Encode(crawler));
        v15.SetAttributeValue("version", 15);
        v15.Element("bond")?.Remove();
        v15.Element("journal")?.Remove();
        var migrated = BabySaveCodec.Decode(v15.ToString(SaveOptions.DisableFormatting));
        Equal<BondRecord>(null, migrated.Bonding, "a v15 baby has no bond record yet");
        Near(34, migrated.Bond, "Connection becomes Bond");
        var seeded = game.Bonding(migrated);
        Near(34, seeded.HighWater, "the high-water mark is the current Bond");
        Near(0, seeded.Security, "Security is neutral");
        Near(0, seeded.Curiosity, "Curiosity is neutral");
        Equal(0, seeded.Rhythm, "the rhythm starts at 0");
        var journal = game.Journal(migrated);
        Equal("Arrival,Milestone,Milestone", string.Join(",", journal.Entries.Select(e => e.Kind)),
            "the journal has the arrival and both milestones");
        Equal(at.AddDays(-8.5).UtcTicks, journal.Entries[0].At.UtcTicks, "the arrival is dated by age");
        Equal(4, journal.Entries[1].Day, "sitting on its nominal day 4");
        Equal(at.AddDays(-5.5).UtcTicks, journal.Entries[1].At.UtcTicks, "dated by its nominal age");
        Equal(8, journal.Entries[2].Day, "crawling on its nominal day 8");
        Equal(1, journal.Weeks, "the week already lived gets no page");

        var saves = new BondSaveStore { Primary = v15.ToString(SaveOptions.DisableFormatting) };
        var now = at;
        var session = new BabySession(game, saves, () => now);
        Equal(MotorSkill.Crawling, session.Journal.Entries.Last().Skill, "the session shows the seeded journal");
        now = at.AddMinutes(5);
        session.Refresh();
        Equal(true, saves.Primary.Contains("version=\"16\"") && saves.Primary.Contains("<bond v=\"1\"") &&
            saves.Primary.Contains("<journal v=\"1\""), "the next write is v16 with both elements");
        Near(34, session.State.Bond, "Bond carries over");

        var v4 = new BondSaveStore
        {
            Primary = "<baby version=\"4\"><name>Sam</name><checkpoint>639028224000000000</checkpoint><age>6912000000000</age>" +
                "<satiety>41</satiety><comfort>52</comfort><energy>63</energy><toy-familiarity>2</toy-familiarity>" +
                "<plush-familiarity>3</plush-familiarity><can-sit-and-reach>true</can-sit-and-reach><can-crawl>true</can-crawl></baby>",
        };
        var old = new BabySession(game, v4, () => new DateTimeOffset(639028224000000000, TimeSpan.Zero));
        Near(0, old.State.Bond, "a v4 baby starts Bond at 0");
        Equal(3, old.Journal.Entries.Count, "and its journal remembers sitting and crawling");

        foreach (var (element, name) in new[] { ("bond", "bond record"), ("journal", "journal") })
        {
            var newer = XElement.Parse(payload);
            newer.Element(element).SetAttributeValue("v", 2);
            Expect<NotSupportedException>(() => BabySaveCodec.Decode(newer.ToString(SaveOptions.DisableFormatting)),
                $"a newer {name} is unsupported, not corrupt");
        }

        var future = XElement.Parse(payload);
        future.SetAttributeValue("version", 17);
        Expect<NotSupportedException>(() => BabySaveCodec.Decode(future.ToString(SaveOptions.DisableFormatting)),
            "a newer baby save is unsupported");
        foreach (var (name, value) in new[] { ("high", "20"), ("security", "101"), ("curiosity", "-61"), ("kinds", "1024"),
            ("longest", "1"), ("gained", "NaN"), ("meals", "-1") })
        {
            var broken = XElement.Parse(payload);
            broken.Element("bond").SetAttributeValue(name, value);
            Expect<InvalidDataException>(() => BabySaveCodec.Decode(broken.ToString(SaveOptions.DisableFormatting)),
                $"invalid bond {name} is rejected");
        }

        foreach (var (name, value, kind) in new[] { ("k", "99", JournalKind.Photo), ("look", "9.9.9.9.9", JournalKind.Photo),
            ("m", "42", JournalKind.Photo), ("d", "0", JournalKind.Arrival), ("sp", "7", JournalKind.HardDay),
            ("fd", "0", JournalKind.HardDay) })
        {
            var broken = XElement.Parse(payload);
            broken.Element("journal").Elements("j").First(e => (int)e.Attribute("k") == (int)kind).SetAttributeValue(name, value);
            Expect<InvalidDataException>(() => BabySaveCodec.Decode(broken.ToString(SaveOptions.DisableFormatting)),
                $"an invalid journal {name} is rejected");
        }

        var unordered = XElement.Parse(payload);
        unordered.Element("journal").Elements("j").Last().SetAttributeValue("t", 0);
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(unordered.ToString(SaveOptions.DisableFormatting)),
            "journal entries out of order are rejected");
        var aboveMark = XElement.Parse(payload);
        aboveMark.SetElementValue("connection", 99);
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(aboveMark.ToString(SaveOptions.DisableFormatting)),
            "Bond above its high-water mark is rejected");
    }

    private static void Validation()
    {
        Expect<ArgumentOutOfRangeException>(() => new BondTunables(floor: 1.2).Validate(), "the floor is a share");
        Expect<ArgumentOutOfRangeException>(() => new BondTunables(helperDailyLoss: 10).Validate(),
            "a helper day costs no more than any day");
        Expect<ArgumentOutOfRangeException>(() => new BondTunables(rhythmBonus: 1.2).Validate(), "a longer rhythm never pays less");
        Expect<ArgumentException>(() => new BondTunables(clingy: 50).Validate(), "clingy sits below settled");
        Expect<ArgumentOutOfRangeException>(() => new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8),
            TimeSpan.FromDays(7), TimeSpan.FromDays(14), bondTunables: new BondTunables(drift: 0)),
            "the simulation validates bond tunables");
        Expect<ArgumentOutOfRangeException>(() => Game().TakePhoto(Game().Adopt("Mia", At(2026, 3, 2, 10, 0)), (PhotoMoment)42),
            "an unknown photo moment is refused");
    }

    // A care day from 07:00: changes, cuddles and naps through the day, breakfast, a snack with play, dinner and
    // bedtime, each window on time, keeping every need above the fussy line. `refresh` adds advances every so many minutes between the care.
    private static (BabyState State, bool Content) CareDay(BabySimulation game, BabyState baby, DateTimeOffset morning, int? refresh)
    {
        var steps = new (double Hour, CareAction Action)[]
        {
            (0.1, CareAction.ChangeDiaper), (0.15, CareAction.Feed), (0.2, CareAction.Soothe), (4, CareAction.ChangeDiaper),
            (4.05, CareAction.Rest), (4.07, CareAction.Feed), (4.1, CareAction.Soothe), (7.25, CareAction.Feed), (7.3, CareAction.Play),
            (7.4, CareAction.Soothe), (8.5, CareAction.ChangeDiaper), (10, CareAction.Rest), (11.25, CareAction.Feed),
            (11.3, CareAction.ChangeDiaper), (11.35, CareAction.Soothe), (12.7, CareAction.ChangeDiaper), (12.75, CareAction.Rest),
        };
        var content = true;
        foreach (var (hour, action) in steps)
        {
            var when = morning.AddHours(hour);
            baby = refresh.HasValue ? Stepped(game, baby, when, refresh.Value) : game.Advance(baby, when).State;
            if (baby.Care.Mood != CareMood.Content)
                throw new InvalidOperationException(
                    $"The scripted care day should keep the baby content: {baby.Care.Mood} ({baby.Care.MoodCause}) at {when:MM-dd HH:mm}, " +
                    $"needs {baby.Satiety:0} {baby.Comfort:0} {baby.Energy:0} {baby.Cleanliness:0}");
            baby = game.ApplyCare(baby, action).State;
        }

        if (!content)
            throw new InvalidOperationException("The scripted care day should keep the baby content.");
        return (baby, content);
    }

    private static BondRecord CloseDay(BondRecord record, CareRecord care, long careDay, CareDaySummary summary,
        List<int> stickers = null)
    {
        var dayEnd = RoutineCalendar.CareDayStart(careDay + 1, care.Offset, Tunables.DayStart);
        var events = new[] { new LedgerEvent(LedgerEventKind.DayClosed, dayEnd, careDay, day: summary) };
        var step = BondRules.Advance(record.HighWater, record, care, events, dayEnd, Bond, Tunables);
        stickers?.AddRange(step.Stickers.Select(sticker => sticker.Days));
        return step.Record;
    }

    private static bool Qualifies(CareDaySummary summary)
    {
        var care = Anchored(At(2026, 3, 2, 10, 0));
        return CloseDay(BondRules.Seed(0, care), care, care.CareDay, summary).Rhythm == 1;
    }

    private static CareDaySummary Summary(int mealsDone, int mealsMissed) =>
        new CareDaySummary(mealsDone, mealsMissed, true, true, false, false, false);

    private static BondRecord Seeded(double security)
    {
        var draft = BondRules.Seed(0, Anchored(At(2026, 3, 2, 10, 0))).ToBuilder();
        draft.Security = security;
        return draft.Build();
    }

    private static string Describe(JournalRecord journal) =>
        string.Join(" | ", journal.Entries.Select(e => $"{e.Kind} {e.At:MM-dd HH:mm} d{e.Day} {e.Skill} {e.Count} {e.CareDays}")) +
        $" spell {journal.SpellDay} weeks {journal.Weeks}/{journal.WeekStartCareDays}";

    private static double LastFactor(BabyState state) => state.Development.Engagement.Last().Factor;

    private static BabyState Stepped(BabySimulation game, BabyState state, DateTimeOffset end, int minutes)
    {
        for (var t = state.TimeCheckpoint.AddMinutes(minutes); t < end; t = t.AddMinutes(minutes))
            state = game.Advance(state, t).State;
        return game.Advance(state, end).State;
    }

    private static BabySimulation Game() =>
        new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay, TimeSpan.FromDays(7), TimeSpan.FromDays(14));

    // A sitter (by default) at day 5 whose windows count from two days ago, present for an hour, with a bond record.
    private static BabyState Baby(DateTimeOffset at, double bond, double? high = null, double satiety = 80, double comfort = 80,
        double energy = 80, double health = 90, CareMood mood = CareMood.Content, DateTimeOffset? moodSince = null,
        double security = 0, double curiosity = 0, int rhythm = 0, TimeSpan? age = null, bool sitting = true,
        bool crawling = false, bool walking = false, CareCondition condition = CareCondition.Well,
        DateTimeOffset? conditionSince = null)
    {
        var care = Anchored(at).ToBuilder();
        care.Mood = mood;
        care.MoodSince = moodSince ?? at;
        care.MoodCause = mood == CareMood.Content ? CareMoodCause.None : CareMoodCause.LowNeed;
        care.SessionStart = at.AddHours(-1);
        care.Condition = condition;
        care.ConditionSince = conditionSince ?? at;
        care.UnwellSessions = condition == CareCondition.Well ? 0 : 1;
        var record = care.Build();
        var bonding = BondRules.Seed(high ?? bond, record).ToBuilder();
        bonding.Security = security;
        bonding.Curiosity = curiosity;
        bonding.Rhythm = rhythm;
        bonding.LongestRhythm = rhythm;
        return BabyState.Restore("Mia", at, age ?? TimeSpan.FromDays(5), satiety, comfort, energy, 3, 3, sitting || crawling,
            crawling, bond, 1, 90, health, sitting || crawling, canWalk: walking, care: record, bonding: bonding.Build());
    }

    private static BabyState WithReadiness(BabyState state, double readiness) =>
        WithDevelopment(state, draft => draft.Readiness = readiness);

    private static BabyState WithDevelopment(BabyState state, Action<DevelopmentRecord.Builder> change)
    {
        var draft = state.Development?.ToBuilder() ?? new DevelopmentRecord.Builder
        {
            Sample = Game().Growth(state).Sample,
            CategoryDay = state.Care.CareDay,
            RecoveryDay = state.Care.CareDay,
        };
        change(draft);
        return BabyState.Restore(state.Name, state.TimeCheckpoint, state.Age, state.Satiety, state.Comfort, state.Energy,
            state.ToyFamiliarity, state.PlushFamiliarity, state.CanSitAndReach, state.CanCrawl, state.Bond, state.StorySessions,
            state.Cleanliness, state.Health, state.NewSoundResolved, state.NurseryTheme, state.CanWalk, state.BlockTowers,
            state.Appearance, state.Care, draft.Build(), state.Bonding, state.Journal);
    }

    private static CareRecord Anchored(DateTimeOffset at)
    {
        var draft = CareRules.Start(at, Tunables).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-2);
        return draft.Build();
    }

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, Paris);

    private static void Near(double expected, double actual, string description)
    {
        if (double.IsNaN(actual) || Math.Abs(expected - actual) > 1e-9)
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }

    private static void Expect<TException>(Action attempt, string description) where TException : Exception
    {
        try
        {
            attempt();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected {typeof(TException).Name}");
    }

    private sealed class BondSaveStore : IBabySaveStore
    {
        public string Primary { get; set; }
        public string Backup { get; private set; }

        public string Read() => Primary;
        public string ReadBackup() => Backup;

        public void Write(string payload, string previousValidPayload)
        {
            Primary = payload;
            Backup = previousValidPayload;
        }
    }
}
