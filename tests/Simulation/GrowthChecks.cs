using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

// Care slice 4: development tracks, readiness-based milestone timing, the setback rung, projection and the v15 save.
// See docs/care-and-growth.md (Development tracks, Milestone timing, Consequence ladder: Setback).
static class GrowthChecks
{
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly GrowthTunables Growth = GrowthTunables.Default;
    private static readonly TimeSpan Paris = TimeSpan.FromHours(1);
    private static readonly double[] Nominal = { 3, 7, 14 };

    public static void Run()
    {
        Formulas();
        PlayStyleTable();
        Bounds();
        CatchUp();
        ConditionPacesReadiness();
        Engagement();
        BoredAndVariety();
        QualityFollowsCare();
        SplitAdvances();
        AwayAndAbsence();
        PreviewDays();
        SetbackEntry();
        SetbackWayBack();
        SetbackOtherTriggers();
        Projection();
        Personas();
        SaveAndMigration();
        Validation();
        Console.WriteLine("Growth checks passed.");
    }

    private static void Formulas()
    {
        Near(1, DevelopmentRules.Wellbeing(100, 100, 100, 100), "full needs are fully well kept");
        Near(0, DevelopmentRules.Wellbeing(20, 20, 20, 20), "needs at 20 are not well kept");
        // s = 1, 1, 1, 0.5: min 0.5, mean 0.875.
        Near(0.5 * 0.5 + 0.5 * 0.875, DevelopmentRules.Wellbeing(60, 80, 100, 40), "one low need weighs more than an even spread");
        Near(0.40 + 0.20 + 0.10 + 0.30, DevelopmentRules.Quality(DevelopmentTrack.Motor, 1, 1, 1, 1, 1), "Motor tops out at 1");
        Near(0.40 * 0.5 + 0.20 * 1 + 0.10 * 0.2 + 0.30 * 0.4,
            DevelopmentRules.Quality(DevelopmentTrack.Motor, 0.5, 1, 0.2, 0.4, 0.9), "Motor weights");
        Near(0.35 * 0.5 + 0.10 + 0.20 * 0.2 + 0.35 * 0.4,
            DevelopmentRules.Quality(DevelopmentTrack.Learning, 0.5, 1, 0.2, 0.4, 0.9), "Learning weights");
        Near(0.30 * 0.5 + 0.10 + 0.35 * 0.2 + 0.25 * 0.4,
            DevelopmentRules.Quality(DevelopmentTrack.Social, 0.5, 1, 0.2, 0.4, 0.9), "Social weights");
        Near(0.30 * 0.5 + 0.50 + 0.20 * 0.9,
            DevelopmentRules.Quality(DevelopmentTrack.Physical, 0.5, 1, 0.2, 0.4, 0.9), "Physical weights meals and Health");
        Near(0.8, DevelopmentRules.Pace(1), "the best care takes 0.8 of a stage");
        Near(1.3, DevelopmentRules.Pace(0), "the least care takes 1.3 of a stage");
        Near(1, DevelopmentRules.Pace(DevelopmentRules.NominalQuality), "the nominal quality keeps the nominal pace");
    }

    // The play-style table of Milestone timing, within 0.15 day, whatever the step size.
    private static void PlayStyleTable()
    {
        var growth = Engine();
        var styles = new (string Name, double Q, double[] Days)[]
        {
            ("Excellent", 0.94, new[] { 2.5, 5.8, 11.6 }),
            ("Average", 0.57, new[] { 3.0, 7.1, 14.2 }),
            ("Neglect", 0.10, new[] { 3.8, 8.8, 17.5 }),
        };
        foreach (var (name, q, days) in styles)
        {
            foreach (var step in new[] { TimeSpan.FromHours(1), TimeSpan.FromMinutes(7), TimeSpan.FromDays(30) })
            {
                var ages = growth.Schedule(Steady(q, wellbeing: q), 1, step);
                for (var i = 0; i < 3; i++)
                {
                    if (Math.Abs(ages[i].TotalDays - days[i]) > 0.15)
                        throw new InvalidOperationException(
                            $"{name} milestone {i} lands at day {ages[i].TotalDays:0.00}, expected about {days[i]}");
                }
            }

            var hourly = growth.Schedule(Steady(q, wellbeing: q), 1, TimeSpan.FromHours(1));
            var whole = growth.Schedule(Steady(q, wellbeing: q), 1, TimeSpan.FromDays(30));
            for (var i = 0; i < 3; i++)
                Within(hourly[i].TotalDays, whole[i].TotalDays, 1e-6, $"{name} milestone {i} is the same in one step or many");
        }
    }

    // Every milestone lands within 0.8x and 1.4x of its nominal age; a steady pace within 1.3x; sick forever at 1.4x.
    private static void Bounds()
    {
        var growth = Engine();
        foreach (var q in new[] { 0, 0.1, 0.25, 0.5, 0.57, 0.75, 0.94, 1 })
        {
            foreach (var w in new[] { 0, 0.3, 0.6, 1 })
            {
                foreach (var cond in new[] { 1, 0.75, 0.5, 0.25 })
                {
                    var ages = growth.Schedule(Steady(q, w), cond, TimeSpan.FromHours(1));
                    for (var i = 0; i < 3; i++)
                    {
                        var ratio = ages[i].TotalDays / Nominal[i];
                        if (ratio < 0.8 - 1e-9 || ratio > 1.4 + 1e-9)
                            throw new InvalidOperationException($"Q {q} W {w} cond {cond}: milestone {i} at {ratio:0.000}x");
                        if (cond == 1 && w < Growth.CatchUpWellbeing && ratio > 1.3 + 1e-6)
                            throw new InvalidOperationException($"Q {q}: a steady pace lands milestone {i} by 1.3x");
                        if (i > 0 && ages[i] <= ages[i - 1])
                            throw new InvalidOperationException("Milestones unlock in order");
                    }
                }
            }
        }

        var best = growth.Schedule(Steady(1, 1), 1, TimeSpan.FromHours(1));
        Within(2.4, best[0].TotalDays, 1e-6, "the best care sits at 0.8x");
        Within(11.2, best[2].TotalDays, 1e-6, "the best care walks at 0.8x");
        var sick = growth.Schedule(Steady(1, 1), 0, TimeSpan.FromHours(1));
        Within(4.2, sick[0].TotalDays, 1e-6, "readiness paused, sitting still comes at 1.4x");
        Within(9.8, sick[1].TotalDays, 1e-6, "crawling still comes at 1.4x");
        Within(19.6, sick[2].TotalDays, 1e-6, "even neglect reaches walking by 1.4x");
    }

    // Past the nominal age with Wellbeing 0.6 or more, readiness grows x1.25 so late bloomers catch up.
    private static void CatchUp()
    {
        var growth = Engine();
        var slow = growth.Schedule(Steady(0, 0.59), 1, TimeSpan.FromHours(1));
        var caught = growth.Schedule(Steady(0, 0.6), 1, TimeSpan.FromHours(1));
        Within(3 * 1.3, slow[0].TotalDays, 1e-6, "without catch-up Q 0 sits at 1.3x");
        // p reaches 1/1.3 at day 3, then grows 1.25 / 1.3 a stage-day.
        Within(3 + (1 - 1 / 1.3) * 3 * 1.3 / 1.25, caught[0].TotalDays, 1e-6, "catch-up after the nominal age");
        Equal(true, caught[2] < slow[2], "a well-kept late bloomer catches up");
        var early = growth.Schedule(Steady(1, 1), 1, TimeSpan.FromHours(1));
        Within(2.4, early[0].TotalDays, 1e-6, "catch-up never speeds a baby before the nominal age");
    }

    // cond: 1 well, 0.75 under the weather, 0.5 upset or recovering, 0 sick before the doctor.
    private static void ConditionPacesReadiness()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        double Gain(BabyState baby) => game.Advance(baby, at.AddHours(1)).State.Development.Readiness - baby.Development.Readiness;
        var well = Gain(Crawler(at));
        Equal(true, well > 0, "a well baby gets readier");
        Near(well * 0.75, Gain(Crawler(at, condition: CareCondition.UnderTheWeather)), "under the weather paces 0.75");
        Near(well * 0.5, Gain(Crawler(at, condition: CareCondition.Recovering)), "recovering paces 0.5");
        Near(well * 0.5, Gain(Crawler(at, mood: CareMood.Upset)), "upset paces 0.5");
        Near(0, Gain(Crawler(at, condition: CareCondition.Sick)), "sick pauses readiness until the doctor");
        // An hour of the 7-day walking stage at Q: 1 / (168 x Pace(Q)).
        var q = Crawler(at).Development.Sample.Motor;
        Near(1 / (168 * DevelopmentRules.Pace(q)), well, "readiness follows the formula");
    }

    private static void Engagement()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = Crawler(at, satiety: 100, comfort: 100, energy: 100, cleanliness: 100);
        // Play with the rattle three times: x1 (and x1.5 as the toy's first use this stage), x2/3, x1/2.
        var first = game.ApplyCare(baby, CareAction.Play).State;
        var factors = first.Development.Engagement.Select(entry => entry.Factor).ToArray();
        Near(1.5, factors.Last(), "a toy's first use in a stage pays x1.5");
        var second = game.ApplyCare(first, CareAction.Play).State;
        Near(1.0 / 1.5 * (0.4 + 0.6 * BabyGrowth.Wellbeing(Needs(first))), second.Development.Engagement.Last().Factor,
            "a repeat within 6 hours pays 1/(1 + 0.5n), scaled by Wellbeing");
        var third = game.ApplyCare(second, CareAction.Play).State;
        Equal(EngagementKind.PlayRattle, third.Development.Engagement.Last().Kind, "play with the rattle counts as rattle play");
        Near(1.0 / 2 * (0.4 + 0.6 * BabyGrowth.Wellbeing(Needs(second))), third.Development.Engagement.Last().Factor,
            "the second repeat pays half");

        // One kind supplies at most 0.6 of a track's engagement.
        var log = Enumerable.Range(0, 10).Select(i => new EngagementEntry(EngagementKind.PlayRattle, at.UtcTicks + i, 1)).ToList();
        Near(0.6, DevelopmentRules.Engagement(log, DevelopmentTrack.Motor, at.UtcTicks + 20, Growth), "one kind caps at 0.6");
        log.Add(new EngagementEntry(EngagementKind.PlayPlush, at.UtcTicks + 15, 1));
        Near(1, DevelopmentRules.Engagement(log, DevelopmentTrack.Motor, at.UtcTicks + 20, Growth), "variety fills engagement");
        Near(0, DevelopmentRules.Engagement(log, DevelopmentTrack.Motor, at.AddHours(25).UtcTicks, Growth),
            "engagement counts the last 24 hours");
        Near(0, DevelopmentRules.Engagement(log, DevelopmentTrack.Learning, at.UtcTicks + 20, Growth), "play feeds Motor only");
        var spread = new[]
        {
            new EngagementEntry(EngagementKind.Story, at.UtcTicks, 1), new EngagementEntry(EngagementKind.Blocks, at.UtcTicks, 1),
            new EngagementEntry(EngagementKind.Soothe, at.UtcTicks, 1), new EngagementEntry(EngagementKind.Meal, at.UtcTicks, 1),
        };
        Near(0.8, DevelopmentRules.Engagement(spread, DevelopmentTrack.Learning, at.UtcTicks, Growth), "stories and blocks feed Learning");
        Near(0.3, DevelopmentRules.Engagement(spread, DevelopmentTrack.Motor, at.UtcTicks, Growth), "blocks feed Motor");
        Near(0.4, DevelopmentRules.Engagement(spread, DevelopmentTrack.Social, at.UtcTicks, Growth), "soothing and meals feed Social");

        // A refused action earns nothing.
        var upset = Crawler(at, mood: CareMood.Upset);
        var refused = game.ApplyCare(upset, CareAction.Play);
        Equal(Reaction.PushesToyAway, refused.Reaction, "an upset baby pushes the toy away");
        Equal(upset.Development.Engagement.Count, refused.State.Development.Engagement.Count, "a refused toy earns nothing");
        // A meal counts only when on time.
        var late = Crawler(At(2026, 3, 2, 11, 0), satiety: 50);
        Equal(0, game.ApplyCare(late, CareAction.Feed).State.Development.Engagement.Count, "a late meal earns no engagement");
        var onTime = Crawler(At(2026, 3, 2, 8, 0), satiety: 50);
        Equal(EngagementKind.Meal, game.ApplyCare(onTime, CareAction.Feed).State.Development.Engagement.Single().Kind,
            "an on-time meal earns a little Social");
    }

    private static void BoredAndVariety()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = Crawler(at, satiety: 100, comfort: 100, energy: 100, cleanliness: 100);
        var reactions = new Reaction[5];
        for (var i = 0; i < reactions.Length; i++)
        {
            var played = game.ApplyCare(baby, CareAction.ReadStory);
            baby = played.State;
            reactions[i] = played.Reaction;
        }

        Equal(Reaction.EnjoysStory, reactions[2], "three stories in a row are still enjoyed");
        Equal(Reaction.Bored, reactions[3], "the fourth story in six hours shows a Bored reaction");
        Equal(6, baby.StorySessions, "a bored baby still hears the story");
        var later = game.Advance(baby, at.AddHours(7)).State;
        Equal(Reaction.EnjoysStory, game.ApplyCare(later, CareAction.ReadStory).Reaction, "after six hours a story is fresh again");

        // Four kinds of care in one care day add Bond +2, once.
        var day = Crawler(At(2026, 3, 2, 8, 0), satiety: 60);
        var connection = day.Connection;
        day = game.ApplyCare(day, CareAction.Feed).State;
        day = game.ApplyCare(day, CareAction.ChangeDiaper).State;
        day = game.ApplyCare(day, CareAction.Rest).State;
        Near(connection, day.Connection, "three kinds of care add no bonus");
        day = game.ApplyCare(day, CareAction.Soothe).State;
        Near(connection + 3 + Growth.VarietyBond, day.Connection, "the fourth kind adds Bond +2 with the soothe");
        day = game.ApplyCare(day, CareAction.ReadStory).State;
        Near(connection + 3 + Growth.VarietyBond + 2, day.Connection, "the bonus comes once a day");
        var tomorrow = game.Advance(day, At(2026, 3, 3, 8, 0)).State;
        Equal(CareCategories.Rest | CareCategories.Story | CareCategories.Comfort | CareCategories.Hygiene | CareCategories.Meal,
            tomorrow.Development.Categories, "the categories wait for the next care action to roll over");
        tomorrow = game.ApplyCare(tomorrow, CareAction.Soothe).State;
        Equal(CareCategories.Comfort, tomorrow.Development.Categories, "a new care day counts afresh");
    }

    // Good care raises quality at once; neglect lowers it as needs fall.
    private static void QualityFollowsCare()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = Crawler(at, satiety: 100, comfort: 100, energy: 100, cleanliness: 100);
        var before = baby.Development.Sample.Motor;
        baby = game.ApplyCare(baby, CareAction.Play).State;
        Equal(true, baby.Development.Sample.Motor > before, "play raises Motor quality at once");
        var learning = baby.Development.Sample.Learning;
        baby = game.ApplyCare(baby, CareAction.ReadStory).State;
        Equal(true, baby.Development.Sample.Learning > learning, "a story raises Learning quality");
        var neglected = game.Advance(baby, at.AddHours(8)).State;
        Equal(true, neglected.Development.Sample.Wellbeing < baby.Development.Sample.Wellbeing, "needs fall, Wellbeing falls");
        Equal(true, neglected.Development.Sample.Motor < baby.Development.Sample.Motor, "and Motor quality with it");
    }

    // Within a session, one long advance equals many short ones, across sample instants and an unlock.
    private static void SplitAdvances()
    {
        var game = Game();
        var at = At(2026, 3, 2, 9, 20);
        // Fed and played with, the baby stays content all afternoon, so the ladder never moves.
        var baby = game.ApplyCare(game.ApplyCare(Crawler(at, readiness: 0.97, satiety: 60, comfort: 90, energy: 90,
            cleanliness: 100), CareAction.Feed).State, CareAction.Play).State;
        var end = at.AddHours(6).AddMinutes(13);
        var whole = game.Advance(baby, end);
        var state = baby;
        var unlocked = false;
        for (var t = at.AddMinutes(11); t < end; t = t.AddMinutes(11))
        {
            var step = game.Advance(state, t);
            state = step.State;
            unlocked |= step.UnlockedWalking;
        }

        var last = game.Advance(state, end);
        unlocked |= last.UnlockedWalking;
        state = last.State;
        Equal(true, whole.UnlockedWalking, "readiness 0.97 walks within the afternoon");
        Equal(true, unlocked, "so do the short advances");
        SameGrowth(whole.State, state, "across an unlock");

        var evening = At(2026, 3, 2, 19, 45);
        var fedDinner = game.ApplyCare(Crawler(evening, readiness: 0.2, crawler: false, satiety: 50, comfort: 95, energy: 40,
            cleanliness: 95), CareAction.Feed).State;
        var tucked = game.ApplyCare(fedDinner, CareAction.Rest).State;
        var morning = At(2026, 3, 3, 3, 30);
        SameGrowth(game.Advance(tucked, morning).State, Stepped(game, tucked, morning, 17), "through the night");

        // Needs slide from 12:30 (breakfast is behind) without reaching Upset: Wellbeing falls below 0.3 on the way.
        var noon = At(2026, 3, 2, 12, 30);
        var sliding = Crawler(noon, readiness: 0.2, crawler: false, satiety: 50, comfort: 40, energy: 45, cleanliness: 60);
        var slid = game.Advance(sliding, noon.AddHours(3)).State;
        Equal(true, slid.Development.Sample.Wellbeing < Growth.SetbackWellbeing, "Wellbeing ends below the setback threshold");
        Equal(true, slid.Development.LowWellbeingTime > TimeSpan.Zero, "low Wellbeing is counted");
        var slidSplit = Stepped(game, sliding, noon.AddHours(3), 20);
        Equal(CareMood.Fussy, slidSplit.Care.Mood, "the short advances leave the baby fussy, not upset");
        SameGrowth(slid, slidSplit, "as Wellbeing slides below the setback threshold");
    }

    private static void AwayAndAbsence()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = Crawler(at, readiness: 0, satiety: 30, comfort: 30, energy: 30, cleanliness: 30, mood: CareMood.Upset);
        var away = game.StartAway(baby, TimeSpan.FromDays(2));
        var held = game.Advance(away, at.AddDays(1)).State;
        // Away mode holds the nominal pace (Pace 1, cond 1) from the first sample: a day of the 7-day stage.
        Within(1.0 / 7, held.Development.Readiness, 0.01, "Away mode develops at the nominal pace");
        Equal(SampleMode.Away, held.Development.Sample.Mode, "the sample says Away");
        SameGrowth(held, Stepped(game, away, at.AddDays(1), 70), "while away");

        // Past the absence ceiling a helper holds Q 0.3, and the guarantee still comes.
        var gone = game.Advance(Crawler(at, readiness: 0), at.AddDays(3)).State;
        Equal(SampleMode.Helper, gone.Development.Sample.Mode, "a long absence is covered by a helper");
        Equal(true, gone.Development.Readiness > 0.3 && gone.Development.Readiness < 3.0 / 7, "development goes on at a slower pace");
        var year = game.Advance(Crawler(at, readiness: 0), at.AddDays(365));
        Equal(true, year.State.CanWalk && year.UnlockedWalking, "a year away still reaches walking, played on return");
    }

    // Preview days follow the needs as they fall; care before them makes milestones sooner (docs/playtest.md).
    private static void PreviewDays()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        int[] PreviewDaysTo(BabyState baby, bool care)
        {
            var days = new int[3];
            var turn = 0;
            var plays = new[] { CareAction.Play, CareAction.ReadStory, CareAction.StackBlocks };
            for (var day = 1; day <= 21 && days[2] == 0; day++)
            {
                if (care)
                {
                    foreach (var action in new[] { CareAction.Feed, CareAction.Feed, CareAction.ChangeDiaper, CareAction.Soothe,
                        plays[turn++ % 3], plays[turn++ % 3], CareAction.Rest, CareAction.Rest })
                    {
                        if (action == CareAction.StackBlocks && !baby.CanWalk || action == CareAction.Feed && game.IsFull(baby))
                            continue;
                        baby = game.ApplyCare(baby, action, day % 2 == 0 ? Toy.Rattle : Toy.Plush).State;
                    }
                }

                var next = game.AdvancePreview(baby, TimeSpan.FromDays(1));
                baby = next.State;
                if (next.UnlockedSitting)
                    days[0] = day;
                if (next.UnlockedCrawling)
                    days[1] = day;
                if (next.UnlockedWalking)
                    days[2] = day;
            }

            return days;
        }

        var newborn = game.Adopt("Mia", at);
        var cared = PreviewDaysTo(newborn, true);
        var left = PreviewDaysTo(newborn, false);
        Console.WriteLine($"  Next day previews to sit, crawl, walk: with care {string.Join(" / ", cared)}, " +
            $"without {string.Join(" / ", left)}");
        for (var i = 0; i < 3; i++)
            Equal(true, cared[i] < left[i], $"care before each Next day brings milestone {i} sooner");
        Equal(3, cared[0], "with care the baby sits on the third Next day");
        Equal(4, left[0], "without care on the fourth");
    }

    // Present neglect makes the newest skill shy; absence alone never does; skills never re-lock.
    private static void SetbackEntry()
    {
        var game = Game();
        var at = At(2026, 3, 2, 21, 50);
        var walker = Walker(at, misses: "MMM", owed: CareTags.Meal);
        var returned = game.Advance(walker, at.AddHours(2)).State;
        Equal(MotorSkill.None, returned.ShySkill, "a return is not presence: no setback yet");
        var fed = game.ApplyCare(game.Advance(returned, at.AddHours(2).AddMinutes(10)).State, CareAction.Feed).State;
        Equal(MotorSkill.None, game.Advance(fed, at.AddHours(2).AddMinutes(30)).State.ShySkill,
            "answering the missed meal within the visit avoids a setback");

        var present = game.Advance(returned, at.AddHours(2).AddMinutes(16)).State;
        Equal(MotorSkill.Walking, present.ShySkill, "three missed meals unanswered while present: walking goes shy");
        Equal(true, present.CanWalk && present.CanCrawl, "a shy skill never re-locks");
        Equal(false, present.ShowsWalking, "the walker shows crawling for now");
        var content = WithDevelopment(Walker(at), draft => draft.Shy = MotorSkill.Walking);
        Equal(Reaction.CrawlsToToy, game.ApplyCare(content, CareAction.Play, Toy.Plush).Reaction, "the shy walker crawls to toys");
        Equal(Reaction.StackedBlocks, game.ApplyCare(content, CareAction.StackBlocks).Reaction, "blocks stay open");

        var crawler = Crawler(At(2026, 3, 2, 21, 50), readiness: 0.5, misses: "MMM", owed: CareTags.Meal);
        var shy = game.Advance(game.Advance(crawler, At(2026, 3, 2, 22, 0)).State, At(2026, 3, 2, 22, 10)).State;
        Equal(MotorSkill.Crawling, shy.ShySkill, "a crawler's crawling goes shy");
        Within(0.5 - 0.15, shy.Development.Readiness, 0.01, "readiness drops by 0.15");
        Equal(false, shy.ShowsCrawling, "the shy crawler sits");
        var newborn = Restore(At(2026, 3, 2, 21, 50), TimeSpan.FromDays(1), false, false, false, 0.3, misses: "MMM",
            owed: CareTags.Meal);
        Equal(MotorSkill.None, game.Advance(game.Advance(newborn, At(2026, 3, 2, 22, 0)).State, At(2026, 3, 2, 22, 10)).State.ShySkill,
            "a newborn has no skill to go shy");

        var gentle = game.ChangeCareStyle(walker, CareStyle.Gentle);
        Equal(MotorSkill.None, game.Advance(game.Advance(gentle, at.AddMinutes(5)).State, at.AddMinutes(25)).State.ShySkill,
            "Gentle care has no setback");
    }

    // One good day (three on-time meals and two soothes) brings the skill back with a celebration.
    private static void SetbackWayBack()
    {
        var game = Game();
        var day = At(2026, 3, 3, 0, 0);
        var shy = WithDevelopment(Walker(At(2026, 3, 3, 7, 30), satiety: 30), draft =>
        {
            draft.Shy = MotorSkill.Walking;
            draft.SetbackSince = day;
        });
        var state = shy;
        Reaction last = Reaction.None;
        foreach (var (hour, action) in new[] { (7.5, CareAction.Feed), (8.0, CareAction.Soothe), (14.5, CareAction.Feed),
            (15.0, CareAction.Soothe), (18.5, CareAction.Feed) })
        {
            state = game.Advance(state, day.AddHours(hour)).State;
            if (action == CareAction.Feed)
                state = WithSatiety(state, 40);
            var cared = game.ApplyCare(state, action);
            state = cared.State;
            last = cared.Reaction;
            if (hour < 18)
                Equal(MotorSkill.Walking, state.ShySkill, "the skill stays shy until the good day is done");
        }

        Equal(Reaction.SkillBack, last, "the third on-time meal brings the walking-again celebration");
        Equal(MotorSkill.None, state.ShySkill, "walking is back");
        Equal(true, state.ShowsWalking, "the walker walks again");

        // Counts reset with the care day.
        var split = shy;
        split = game.ApplyCare(WithSatiety(game.Advance(split, day.AddHours(7.5)).State, 40), CareAction.Feed).State;
        split = game.ApplyCare(game.Advance(split, day.AddHours(8)).State, CareAction.Soothe).State;
        split = game.ApplyCare(game.Advance(split, day.AddHours(8.5)).State, CareAction.Soothe).State;
        split = game.ApplyCare(WithSatiety(game.Advance(split, day.AddHours(14.5)).State, 40), CareAction.Feed).State;
        var nextDay = game.ApplyCare(WithSatiety(game.Advance(split, day.AddHours(31.5)).State, 40), CareAction.Feed);
        Equal(MotorSkill.Walking, nextDay.State.ShySkill, "a good day is one care day: yesterday's meals do not count");
        Equal(1, nextDay.State.Development.RecoveryMeals, "the new day counts from one");
        Equal(Reaction.Satisfied, nextDay.Reaction, "an ordinary meal is just a meal");
    }

    private static void SetbackOtherTriggers()
    {
        var game = Game();
        // Wellbeing below 0.3 for two counted days, seen while present.
        var at = At(2026, 3, 2, 10, 0);
        var low = WithDevelopment(Walker(at, satiety: 5, comfort: 5, energy: 5, cleanliness: 5), draft =>
        {
            draft.LowWellbeingTime = TimeSpan.FromHours(47.5);
            draft.Sample = new TrackSample(SampleMode.Normal, 0, 0.1, 0.1, 0.1, 0.3);
        });
        var first = game.Advance(low, at.AddMinutes(20)).State;
        Equal(MotorSkill.None, first.ShySkill, "47 hours and 50 minutes is not two days");
        var second = game.Advance(first, at.AddMinutes(40)).State;
        Equal(MotorSkill.Walking, second.ShySkill, "two days of low Wellbeing, present: walking goes shy");
        Equal(TimeSpan.Zero, second.Development.LowWellbeingTime, "the count starts again");
        var counted = game.Advance(WithDevelopment(Walker(at, satiety: 5, comfort: 5, energy: 5, cleanliness: 5),
            draft => draft.Sample = new TrackSample(SampleMode.Normal, 0, 0.1, 0.1, 0.1, 0.3)), at.AddHours(3)).State;
        Equal(TimeSpan.FromHours(3), counted.Development.LowWellbeingTime, "low Wellbeing is counted");
        var cared = game.ApplyCare(counted, CareAction.Feed).State;
        Equal(TimeSpan.FromHours(3), cared.Development.LowWellbeingTime, "care that leaves Wellbeing low keeps the count");
        foreach (var action in new[] { CareAction.Feed, CareAction.Soothe, CareAction.Soothe, CareAction.ChangeDiaper,
            CareAction.Rest, CareAction.Rest })
            cared = game.ApplyCare(cared, action).State;

        Equal(TimeSpan.Zero, cared.Development.LowWellbeingTime, "care that lifts Wellbeing clears the count");

        // Sick for 24 hours without the doctor, once per spell.
        var sickAt = At(2026, 3, 3, 10, 0);
        var sick = WithCare(Walker(sickAt), draft =>
        {
            draft.Condition = CareCondition.Sick;
            draft.ConditionCause = CareConditionCause.LowHealth;
            draft.ConditionSince = sickAt.AddHours(-23.9);
            draft.SessionStart = sickAt.AddHours(-1);
        });
        var notYet = game.Advance(sick, sickAt.AddMinutes(1)).State;
        Equal(MotorSkill.None, notYet.ShySkill, "sick for under a day");
        var shy = game.Advance(notYet, sickAt.AddMinutes(10)).State;
        Equal(MotorSkill.Walking, shy.ShySkill, "sick a day without the doctor: walking goes shy");
        var back = WithDevelopment(shy, draft => draft.Shy = MotorSkill.None);
        Equal(MotorSkill.None, game.Advance(back, sickAt.AddMinutes(20)).State.ShySkill, "one setback per sick spell");
    }

    private static void Projection()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var newborn = game.Adopt("Mia", at);
        var report = game.Growth(newborn);
        Equal(MotorSkill.Sitting, report.Next, "a newborn's next skill is sitting");
        Within(3 * DevelopmentRules.Pace(newborn.Development.Sample.Motor), report.ExpectedIn.TotalDays, 0.01,
            "the projection holds the current pace");
        var ahead = game.Growth(WithDevelopment(newborn, draft => draft.Sample = Steady(0.94, 0.95)));
        Equal(GrowthPace.Ahead, ahead.Pace, "excellent care is ahead");
        Within(2.5, ahead.ExpectedIn.TotalDays, 0.05, "likely to sit in about two and a half days");
        Equal(GrowthPace.OnTrack, game.Growth(WithDevelopment(newborn, draft => draft.Sample = Steady(0.6, 0.6))).Pace,
            "nominal care is on track");
        var slow = game.Growth(WithDevelopment(newborn, draft => draft.Sample = Steady(0.1, 0.1)));
        Equal(GrowthPace.TakingTime, slow.Pace, "neglect is taking its time");
        Equal(GrowthBand.NeedsRegularMeals, slow.Band, "the growth chart asks for regular meals");
        Equal(GrowthBand.Thriving, ahead.Band, "the growth chart says thriving");
        Equal(GrowthBand.Steady, game.Growth(WithDevelopment(newborn,
            draft => draft.Sample = new TrackSample(SampleMode.Normal, 0.6, 0.6, 0.6, 0.6, 0.6))).Band, "steady in between");
        var sick = game.Growth(Crawler(at, condition: CareCondition.Sick, readiness: 0.5));
        Equal(true, sick.Paused, "a sick baby's readiness is paused");
        Within(19.6 - 8, sick.ExpectedIn.TotalDays, 0.01, "paused, the projection is the guarantee");
        var walker = game.Growth(Walker(at));
        Equal(MotorSkill.None, walker.Next, "a walker has no next motor skill");
        Equal(true, game.DescribeCare(newborn).Contains("growth Sitting"), "the developer read-out shows growth");
    }

    // Scripted players over three weeks of real time. Attentive players answer needs before they play; a busy one
    // visits three times a day and its baby is often unwell; a neglectful one feeds once a day.
    private static void Personas()
    {
        var excellent = Persona(Enumerable.Range(7, 13).Select(hour => (double)hour).ToArray(), attentive: true);
        var attentive = Persona(new[] { 8.0, 11.0, 15.0, 18.5, 20.0 }, attentive: true);
        var busy = Persona(new[] { 8.0, 15.0, 19.0 }, attentive: true);
        var neglect = Persona(new[] { 19.0 }, attentive: false);
        Console.WriteLine($"  personas (sit, crawl, walk): excellent {Days(excellent)}, attentive {Days(attentive)}, " +
            $"busy {Days(busy)}, neglect {Days(neglect)}");
        for (var i = 0; i < 3; i++)
        {
            Equal(true, excellent[i] <= attentive[i] && attentive[i] <= busy[i] && busy[i] <= neglect[i],
                $"better care reaches milestone {i} no later");
            // Unlocks are seen at the next visit: a day apart for the neglectful player.
            foreach (var ages in new[] { excellent, attentive, busy, neglect })
                Equal(true, ages[i] >= 0.8 * Nominal[i] - 1e-9 && ages[i] <= 1.4 * Nominal[i] + 1, $"milestone {i} stays within bounds");
        }

        Equal(true, attentive[2] < busy[2] && busy[2] < neglect[2], "walking comes later with less care");
        Equal(true, excellent[2] < 13, "attentive, varied care all day walks well before day 14");
        Equal(true, attentive[2] < 14, "attentive care walks before day 14");
        Equal(true, neglect[2] > 16, "neglect walks late, but walks");
    }

    private static double[] Persona(double[] visits, bool attentive)
    {
        var game = Game();
        var midnight = At(2026, 3, 1, 0, 0);
        var baby = game.Adopt("Mia", midnight.AddHours(7));
        var ages = new double[3];
        var turn = 0;
        var engagement = new[] { CareAction.Play, CareAction.ReadStory, CareAction.Play, CareAction.StackBlocks, CareAction.Soothe };
        for (var date = 0; date < 21 && ages[2] == 0; date++)
        {
            foreach (var hour in visits)
            {
                var step = game.Advance(baby, midnight.AddDays(date).AddHours(hour));
                baby = step.State;
                Mark(step, baby, ages);
                var meal = hour >= 7 && hour < 10 || hour >= 14 && hour < 17 || hour >= 18 && hour < 20.5;
                var actions = new System.Collections.Generic.List<CareAction>();
                if (!attentive)
                    actions.AddRange(new[] { CareAction.Feed, CareAction.ChangeDiaper });
                else
                {
                    if (meal || baby.Satiety < 60)
                        actions.Add(CareAction.Feed);
                    if (baby.Satiety < 30)
                        actions.Add(CareAction.Feed);
                    if (baby.Cleanliness < 60)
                        actions.Add(CareAction.ChangeDiaper);
                    if (baby.Energy < 50 || hour >= 19.5)
                        actions.Add(CareAction.Rest);
                    if (baby.Comfort < 60 || baby.Care.Mood != CareMood.Content)
                        actions.Add(CareAction.Soothe);
                    actions.Add(engagement[turn++ % engagement.Length]);
                }

                foreach (var action in actions)
                {
                    if (action == CareAction.StackBlocks && !baby.CanWalk || action == CareAction.Feed && game.IsFull(baby))
                        continue;
                    baby = game.ApplyCare(baby, action, turn % 2 == 0 ? Toy.Rattle : Toy.Plush).State;
                }
            }
        }

        return ages;
    }

    private static void Mark((BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) step,
        BabyState baby, double[] ages)
    {
        if (step.UnlockedSitting)
            ages[0] = baby.Age.TotalDays;
        if (step.UnlockedCrawling)
            ages[1] = baby.Age.TotalDays;
        if (step.UnlockedWalking)
            ages[2] = baby.Age.TotalDays;
    }
    private static string Days(double[] ages) => string.Join(" / ", ages.Select(age => age.ToString("0.0")));

    private static void SaveAndMigration()
    {
        var game = Game();
        var at = At(2026, 3, 2, 10, 0);
        var baby = game.ApplyCare(game.ApplyCare(Crawler(at, readiness: 0.4), CareAction.Play).State, CareAction.ReadStory).State;
        baby = WithDevelopment(baby, draft =>
        {
            draft.Shy = MotorSkill.Crawling;
            draft.SetbackSince = at;
            draft.LowWellbeingTime = TimeSpan.FromHours(5);
            draft.RecoveryMeals = 1;
        });
        var payload = BabySaveCodec.Encode(baby);
        Equal(true, payload.StartsWith("<baby version=\"15\"") && payload.Contains("<development v=\"1\""), "the save is v15");
        var decoded = BabySaveCodec.Decode(payload);
        Equal(baby.Development, decoded.Development, "the development record round-trips exactly");
        Equal(MotorSkill.Crawling, decoded.ShySkill, "the setback survives a reload");

        // A v14 baby keeps its milestones, starts with readiness from its age, and its first advance keeps the
        // nominal pace, as before tracks existed.
        var v14 = XElement.Parse(payload);
        v14.SetAttributeValue("version", 14);
        v14.Element("development").Remove();
        var sitter = BabyState.Restore("Sam", at, TimeSpan.FromDays(5), 50, 50, 50, 1, 0, true, false, 20, 1, 50, 80, true,
            care: Anchored(at));
        var legacy = XElement.Parse(BabySaveCodec.Encode(sitter));
        legacy.SetAttributeValue("version", 14);
        var migrated = BabySaveCodec.Decode(legacy.ToString(SaveOptions.DisableFormatting));
        Equal<DevelopmentRecord>(null, migrated.Development, "a v14 baby has no development record yet");
        Within(0.5, game.Growth(migrated).Readiness, 1e-9, "readiness starts from age: half way from sitting to crawling");
        Equal(true, BabySaveCodec.Decode(v14.ToString(SaveOptions.DisableFormatting)).CanCrawl, "milestones are kept");
        var almost = game.Advance(migrated, at.AddDays(2).AddTicks(-1));
        Equal(false, almost.UnlockedCrawling, "the first advance keeps the nominal pace");
        Equal(true, game.Advance(migrated, at.AddDays(2)).UnlockedCrawling, "crawling at day 7, as it always was");
        Equal(SampleMode.Normal, almost.State.Development.Sample.Mode, "after it, development is sampled as usual");
        var walkerV8 = BabyState.Restore("Old", at, TimeSpan.FromDays(30), 50, 50, 50, 1, 0, true, true, 20, 1, 50, 80, true,
            canWalk: true);
        Equal(true, game.Advance(walkerV8, at.AddHours(1)).State.CanWalk, "a migrated walker never re-locks");

        var newer = XElement.Parse(payload);
        newer.Element("development").SetAttributeValue("v", 2);
        Expect<NotSupportedException>(() => BabySaveCodec.Decode(newer.ToString(SaveOptions.DisableFormatting)),
            "a newer development record is unsupported, not corrupt");
        var future = XElement.Parse(payload);
        future.SetAttributeValue("version", 16);
        Expect<NotSupportedException>(() => BabySaveCodec.Decode(future.ToString(SaveOptions.DisableFormatting)),
            "a newer baby save is unsupported");
        foreach (var (name, value) in new[] { ("readiness", "1.5"), ("mode", "7"), ("shy", "3"), ("motor", "NaN"), ("toys", "8"),
            ("categories", "64"), ("low", "-1") })
        {
            var broken = XElement.Parse(payload);
            broken.Element("development").SetAttributeValue(name, value);
            Expect<InvalidDataException>(() => BabySaveCodec.Decode(broken.ToString(SaveOptions.DisableFormatting)),
                $"invalid development {name} is rejected");
        }

        var badEntry = XElement.Parse(payload);
        badEntry.Element("development").Element("e").SetAttributeValue("k", 99);
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(badEntry.ToString(SaveOptions.DisableFormatting)),
            "an unknown engagement kind is rejected");

        // The session writes v15 after the first refresh of a v14 save.
        var saves = new GrowthSaveStore { Primary = legacy.ToString(SaveOptions.DisableFormatting) };
        var now = at.AddHours(1);
        var session = new BabySession(game, saves, () => now);
        session.Refresh();
        Equal(true, saves.Primary.Contains("version=\"15\"") && saves.Primary.Contains("<development v=\"1\""),
            "the next write is v15 with a development record");
        Equal(MotorSkill.Crawling, session.Growth.Next, "the session reports growth");
    }

    private static void Validation()
    {
        Expect<ArgumentOutOfRangeException>(() => new GrowthTunables(guarantee: 1.2).Validate(),
            "the guarantee cannot come before the slowest steady pace");
        Expect<ArgumentOutOfRangeException>(() => new GrowthTunables(kindCap: 10).Validate(),
            "one kind of action cannot fill engagement alone");
        Expect<ArgumentOutOfRangeException>(() => new GrowthTunables(sampleInterval: TimeSpan.FromMinutes(7)).Validate(),
            "samples divide the day evenly");
        Expect<ArgumentOutOfRangeException>(() => new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8),
            TimeSpan.FromDays(7), TimeSpan.FromDays(14), growthTunables: new GrowthTunables(catchUp: 0.5)),
            "the simulation validates growth tunables");
        Equal(true, CareStyleRules.GentleDefault.Setbacks == false && CareStyleRules.StandardDefault.Setbacks, "only Gentle skips setbacks");
        Expect<ArgumentException>(() => new CareTunables(gentle: new CareStyleRules(fussyNeed: 25, upsetNeed: 8,
            upsetDwell: TimeSpan.FromMinutes(30), escalation: TimeSpan.FromHours(4), missedMealsForUpset: 3, upsetCareScore: 0.4,
            overdueMealIsFussy: false, calmFullness: 40, calmComfort: 30, overfeedComfort: 5, overfeedCleanliness: 5,
            messComfortCut: 0.03, unwellHealth: 40, unwellCareScore: 0.35, sickHealth: 20, sickUnwellTime: TimeSpan.FromHours(24),
            sickSessions: 3, sickDwell: TimeSpan.FromMinutes(30)), standard: new CareStyleRules(setbacks: false)).Validate(),
            "Gentle cannot have setbacks that Standard lacks");
        var at = At(2026, 3, 2, 10, 0);
        var draft = Game().Adopt("Mia", at).Development.ToBuilder();
        draft.Shy = MotorSkill.Walking;
        Expect<ArgumentException>(() => BabyState.Restore("Mia", at, TimeSpan.Zero, 50, 50, 50, 0, 0, false, false, 0, 0, 50, 50,
            false, development: draft.Build()), "only a reached skill can be shy");
    }

    private static void SameGrowth(BabyState expected, BabyState actual, string description)
    {
        Within(expected.Development.Readiness, actual.Development.Readiness, 1e-9, $"readiness is the same {description}");
        Equal(expected.Development.Sample.Mode, actual.Development.Sample.Mode, $"the sample mode is the same {description}");
        Within(expected.Development.Sample.Motor, actual.Development.Sample.Motor, 1e-9, $"Motor quality is the same {description}");
        Within(expected.Development.Sample.Wellbeing, actual.Development.Sample.Wellbeing, 1e-9, $"Wellbeing is the same {description}");
        Equal(expected.Development.LowWellbeingTime, actual.Development.LowWellbeingTime, $"low Wellbeing time is the same {description}");
        Equal(expected.CanSitAndReach, actual.CanSitAndReach, $"sitting is the same {description}");
        Equal(expected.CanCrawl, actual.CanCrawl, $"crawling is the same {description}");
        Equal(expected.CanWalk, actual.CanWalk, $"walking is the same {description}");
        Within(expected.Satiety, actual.Satiety, 1e-9, $"Fullness is the same {description}");
    }

    private static BabyState Stepped(BabySimulation game, BabyState state, DateTimeOffset end, int minutes)
    {
        for (var t = state.TimeCheckpoint.AddMinutes(minutes); t < end; t = t.AddMinutes(minutes))
            state = game.Advance(state, t).State;
        return game.Advance(state, end).State;
    }

    private static BabyGrowth Engine() =>
        new BabyGrowth(TimeSpan.FromDays(3), TimeSpan.FromDays(7), TimeSpan.FromDays(14), Tunables, Growth);

    private static TrackSample Steady(double quality, double wellbeing) =>
        new TrackSample(SampleMode.Normal, wellbeing, quality, quality, quality, quality);

    private static BabySimulation Game() =>
        new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay, TimeSpan.FromDays(7), TimeSpan.FromDays(14));

    private static BabyNeeds.Values Needs(BabyState state) => BabyNeeds.From(state);

    // A crawler (or sitter) at day 4 whose windows count from `at`, with a seeded development record.
    private static BabyState Crawler(DateTimeOffset at, double readiness = 0.3, bool crawler = true, double satiety = 80,
        double comfort = 80, double energy = 80, double cleanliness = 90, CareCondition condition = CareCondition.Well,
        CareMood mood = CareMood.Content, string misses = "", CareTags owed = CareTags.None) =>
        Restore(at, TimeSpan.FromDays(crawler ? 8 : 4), true, crawler, false, readiness, satiety, comfort, energy, cleanliness,
            condition, mood, misses, owed);

    private static BabyState Walker(DateTimeOffset at, double satiety = 80, double comfort = 80, double energy = 80,
        double cleanliness = 90, string misses = "", CareTags owed = CareTags.None) =>
        Restore(at, TimeSpan.FromDays(15), true, true, true, 0, satiety, comfort, energy, cleanliness, misses: misses, owed: owed);

    private static BabyState Restore(DateTimeOffset at, TimeSpan age, bool sitting, bool crawling, bool walking, double readiness,
        double satiety = 80, double comfort = 80, double energy = 80, double cleanliness = 90,
        CareCondition condition = CareCondition.Well, CareMood mood = CareMood.Content, string misses = "",
        CareTags owed = CareTags.None)
    {
        var care = Anchored(at).ToBuilder();
        care.Condition = condition;
        care.ConditionSince = at;
        care.UnwellSessions = condition == CareCondition.Well ? 0 : 1;
        care.Mood = mood;
        care.MoodSince = at;
        care.Meals = CareRing.Parse(misses);
        care.Owed = owed;
        // The visit began a while ago, so the player is present.
        care.SessionStart = at.AddHours(-1);
        var record = care.Build();
        var baby = BabyState.Restore("Mia", at, age, satiety, comfort, energy, 3, 3, sitting, crawling, 40, 1, cleanliness, 90,
            sitting, canWalk: walking, care: record);
        var seeded = Game().Growth(baby);
        var development = new DevelopmentRecord.Builder
        {
            Readiness = walking ? 0 : readiness,
            Sample = seeded.Sample,
            CategoryDay = record.CareDay,
            RecoveryDay = record.CareDay,
        }.Build();
        return BabyState.Restore("Mia", at, age, satiety, comfort, energy, 3, 3, sitting, crawling, 40, 1, cleanliness, 90,
            sitting, canWalk: walking, care: record, development: development);
    }

    private static BabyState WithDevelopment(BabyState state, Action<DevelopmentRecord.Builder> change)
    {
        var draft = state.Development.ToBuilder();
        change(draft);
        return Rebuild(state, state.Satiety, state.Care, draft.Build());
    }

    private static BabyState WithCare(BabyState state, Action<CareRecord.Builder> change)
    {
        var draft = state.Care.ToBuilder();
        change(draft);
        return Rebuild(state, state.Satiety, draft.Build(), state.Development);
    }

    private static BabyState WithSatiety(BabyState state, double satiety) => Rebuild(state, satiety, state.Care, state.Development);

    private static BabyState Rebuild(BabyState state, double satiety, CareRecord care, DevelopmentRecord development) =>
        BabyState.Restore(state.Name, state.TimeCheckpoint, state.Age, satiety, state.Comfort, state.Energy,
            state.ToyFamiliarity, state.PlushFamiliarity, state.CanSitAndReach, state.CanCrawl, state.Connection,
            state.StorySessions, state.Cleanliness, state.Health, state.NewSoundResolved, state.NurseryTheme, state.CanWalk,
            state.BlockTowers, state.Appearance, care, development);

    private static CareRecord Anchored(DateTimeOffset at)
    {
        var draft = CareRules.Start(at, Tunables).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-2);
        return draft.Build();
    }

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, Paris);

    private static void Near(double expected, double actual, string description) => Within(expected, actual, 1e-9, description);

    private static void Within(double expected, double actual, double tolerance, string description)
    {
        if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
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

    private sealed class GrowthSaveStore : IBabySaveStore
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
