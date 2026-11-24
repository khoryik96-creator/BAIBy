using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// Monetization phase M4: Helpers, a safety net and not a carer (docs/monetization.md). The golden regression (nothing
// owned plays and saves byte for byte as before helpers), each helper's floor, level, stock and daily limit, coverage,
// helped meals (Covered on the needs axis, missed for Bond), no effect on Bond, temperament, the rhythm, development,
// counts or Health, repeated and backwards advances, split advances, determinism, preview days, the journal, saves and
// migration, the cat (and a dog that keeps its simple rules), entitlements, developer mode and the household setting.
static class HelperChecks
{
    // GoldenScenario.Hash() on the build before helpers (ec8b678). Never edit it to make a change pass.
    private const string GoldenHash = "8fe157fa3317b344020b9de474b69abedf6875a8f6958ea04a50039c02b1a53f";
    private static readonly CareTunables Tunables = CareTunables.Default;
    private static readonly HelperRules Rules = HelperRules.Default;
    private static readonly PetCatalog Catalog = PetCatalog.Default;
    private static readonly PetAppearance Tabby = PetAppearance.Default(Catalog, PetBreed.Tabby);
    private static readonly HelperSettings FeederOnly = new HelperSettings(true, false, false);

    public static void Run()
    {
        Golden();
        RulesAndSettings();
        Feeder();
        HelpedMeal();
        PlayAndTidyLimits();
        Coverage();
        NothingElseMoves();
        ResumeAndClock();
        SplitAdvances();
        Determinism();
        AwayAndMigration();
        PreviewDay();
        Journal();
        Saves();
        Cat();
        Household();
        Console.WriteLine("Helper checks passed.");
    }

    private static void Golden()
    {
        Equal(GoldenHash, GoldenScenario.Hash(), "with no helpers owned, the household plays and saves byte for byte as before");
        Equal(GoldenHash, GoldenScenario.Hash(new FakeEntitlements(ProductIds.HomeRooms2)),
            "other products leave helpers off");
        Equal(false, GoldenScenario.Hash(new FakeEntitlements(ProductIds.HelperSet)) == GoldenHash,
            "the same days with the helpers owned play differently, so the scenario reaches them");

        // No helper that may act is exactly the rules without helpers, at every step.
        var game = Game();
        var baby = Baby(At(3, 8, 0), satiety: 60, comfort: 50, cleanliness: 50);
        foreach (var settings in new[] { null, HelperSettings.None })
        {
            var events = new List<HelperEvent>();
            var plain = game.Advance(baby, At(3, 15, 0)).State;
            var none = game.Advance(baby, At(3, 15, 0), settings, events).State;
            Equal(BabySaveCodec.Encode(plain), BabySaveCodec.Encode(none), "no helpers: the same record");
            Equal(0, events.Count, "no helpers: no events");
        }

        // Owned helpers that never reach a floor change nothing either.
        var calm = game.Advance(baby, At(3, 9, 0), HelperSettings.All).State;
        Equal(BabySaveCodec.Encode(game.Advance(baby, At(3, 9, 0)).State), BabySaveCodec.Encode(calm),
            "helpers that do not act leave the record (and its bytes) alone");
        Equal(true, calm.Care.Helpers.IsEmpty, "and keep no helper state");
    }

    private static void RulesAndSettings()
    {
        Rules.Validate();
        Equal(true, Rules.FeederFloor > HelperRules.LowNeed && Rules.FeederFloor < HelperRules.WellKept, "the feeder floor sits between 30 and 60");
        Equal(true, Rules.PlayFloor >= 40 && Rules.TidyFloor >= 40 && Rules.FeederFloor <= 45, "floors at 40 to 45");
        Equal(70.0, Rules.TidyLevel, "tidy restores Cleanliness to 70");
        Equal(true, Rules.FeederLevel < 100 && Rules.FeederLevel < Tunables.FullMealThreshold, "the feeder fills below full");
        Equal(true, Rules.PlayLevel < HelperRules.WellKept, "play is a small top-up, below well kept");
        Equal(6, Rules.MaxStock, "the feeder holds 6 portions");
        Equal(3, Rules.DailyLimit, "play and tidy act at most 3 times a care day");
        Equal(true, Rules.Coverage >= TimeSpan.FromHours(12) && Rules.Coverage <= TimeSpan.FromHours(24), "coverage of 12 to 24 hours");
        foreach (var broken in new Func<HelperRules>[]
        {
            () => new HelperRules(feederFloor: 30), () => new HelperRules(playFloor: 34), () => new HelperRules(tidyFloor: 60),
            () => new HelperRules(feederLevel: 90), () => new HelperRules(tidyLevel: 100), () => new HelperRules(playLevel: 39),
            () => new HelperRules(maxStock: 0), () => new HelperRules(dailyLimit: 0), () => new HelperRules(coverage: TimeSpan.Zero),
        })
            Expect<ArgumentOutOfRangeException>(() => broken().Validate(), "helper tunables are validated");
        Expect<ArgumentOutOfRangeException>(() => new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(8),
            TimeSpan.FromDays(7), TimeSpan.FromDays(14), helperRules: new HelperRules(feederFloor: 20)), "the simulation validates them");

        Equal(false, HelperSettings.None.Any, "no helpers");
        Equal(true, HelperSettings.None.With(HelperKind.Tidy, true).Tidy, "a helper switched on");
        Equal(false, HelperSettings.All.With(HelperKind.Play, false).Play, "a helper switched off");
    }

    // The feeder serves one stocked portion when Fullness reaches its floor, up to a level below full; no stock, no action.
    private static void Feeder()
    {
        var game = Game();
        var baby = Baby(At(3, 8, 0), satiety: 70);
        var events = new List<HelperEvent>();
        var empty = game.Advance(baby, At(3, 11, 50), FeederOnly, events).State;
        Equal(0, events.Count, "an empty feeder does nothing");
        Near(70 - 7 * (3 + 50 / 60.0), empty.Satiety, "and Fullness falls as without helpers");

        var stocked = game.StockFeeder(baby);
        Equal(6, stocked.Care.Helpers.Stock, "stocking fills the feeder to 6 portions");
        Equal(baby.Care.LastCareAt, stocked.Care.LastCareAt, "stocking is not care");
        Equal(baby.Satiety, stocked.Satiety, "and moves no need");
        Equal(stocked, game.StockFeeder(stocked), "a full feeder stays full");

        var served = game.Advance(stocked, At(3, 11, 50), FeederOnly, events).State;
        Equal(1, events.Count, "Fullness reaching 45 brings one portion");
        var portion = events[0];
        Equal(HelperKind.Feeder, portion.Kind, "the feeder served");
        Near(Rules.FeederFloor, portion.Before, "at the floor", 1e-6);
        Equal(Rules.FeederLevel, portion.After, "up to the feeder's level");
        var expected = At(3, 8, 0).AddTicks((long)Math.Ceiling(25.0 / 7 * TimeSpan.TicksPerHour));
        Equal(expected, portion.At, "at the instant Fullness reached the floor");
        Near(Rules.FeederLevel - 7 * (At(3, 11, 50) - expected).TotalHours, served.Satiety, "then Fullness falls again", 1e-6);
        Equal(5, served.Care.Helpers.Stock, "one portion used");
        Equal(expected.UtcTicks, served.Care.Helpers.LastServe, "the serve is remembered");
        Equal(1, served.Care.Helpers.Feeds, "and counted for the day");
        Equal(false, CareRules.HasOverdueMeal(served.Care), "a meal the feeder served is not overdue");
        Equal(true, CareRules.HasOverdueMeal(empty.Care), "without it the meal is overdue");
        Equal(RoutineSlotState.Helped, BabySimulation.Routine(served)[0].State, "the strip shows the helper");

        // Never above the level: a need above the floor is left alone, and the feeder never fills past its level.
        var full = game.StockFeeder(Baby(At(3, 8, 0), satiety: 90));
        events.Clear();
        game.Advance(full, At(3, 13, 0), FeederOnly, events);
        Equal(true, events.All(e => e.After <= Rules.FeederLevel && e.Before <= Rules.FeederFloor + 1e-6),
            "the feeder only acts at its floor and stops below full");

        // Six portions, then nothing: from 45 a portion lasts (70 - 45) / 7 hours awake.
        var hungry = game.StockFeeder(Baby(At(3, 7, 0), satiety: 46));
        var state = hungry;
        events.Clear();
        for (var step = 1; step <= 72; step++)
        {
            state = game.Advance(state, At(3, 7, 0).AddHours(step * 0.5), FeederOnly, events).State;
            // A cuddle now and then keeps the coverage window open; it is no meal.
            if (step % 12 == 0)
                state = game.ApplyCare(state, CareAction.Soothe).State;
        }

        Equal(6, events.Count(e => e.Kind == HelperKind.Feeder), "the feeder serves at most its stock");
        Equal(0, state.Care.Helpers.Stock, "then it is empty");
    }

    // A meal window the feeder served in and nobody fed by hand is Helped: out of the meal ring and not owed (the needs
    // axis), but a missed meal for Bond. A hands-on meal in the same window still counts on time.
    private static void HelpedMeal()
    {
        var game = Game();
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 70));
        var without = game.Advance(baby, At(3, 12, 0)).State;
        var with = game.Advance(baby, At(3, 12, 0), FeederOnly).State;
        Equal(MomentStatus.Missed, without.Care.StatusOf(CareMoment.Morning), "without a helper breakfast is missed");
        Equal(MomentStatus.Helped, with.Care.StatusOf(CareMoment.Morning), "with the feeder it is Helped");
        Equal("M", without.Care.Meals.ToCode(), "a miss enters the meal ring");
        Equal("", with.Care.Meals.ToCode(), "a helped meal stays out of it, like Covered");
        Equal(CareTags.Meal, without.Care.Owed, "a missed meal is owed");
        Equal(CareTags.None, with.Care.Owed, "a helped meal is not");
        Equal(CareRules.Score(baby.Care, Tunables), CareRules.Score(with.Care, Tunables), "the CareScore holds");
        Equal(CareReason.MealHelped, with.Care.LastReason, "the ledger says why");
        Equal(baby.Care.CoveredCount + 1, with.Care.CoveredCount, "and counts it as covered");
        Near(without.Bond, with.Bond, "a helped meal costs the missed meal's Bond");
        Equal(true, with.Bond < baby.Bond, "which is a loss");
        Equal(false, with.Care.StatusOf(CareMoment.Morning) == MomentStatus.OnTime, "never on time");
        Equal(true, CareRules.Score(baby.Care, Tunables) >= CareRules.Score(with.Care, Tunables), "never credit");

        // Hands-on care first: fed by hand after the portion, breakfast is on time.
        var portion = game.Advance(baby, At(3, 11, 40), FeederOnly).State;
        var fed = game.ApplyCare(portion, CareAction.Feed).State;
        Equal(MomentStatus.Late, fed.Care.StatusOf(CareMoment.Morning), "a hands-on meal after the portion counts as itself");
        Equal("L", fed.Care.Meals.ToCode(), "and enters the ring as usual");
        var early = game.ApplyCare(game.Advance(baby, At(3, 9, 0), FeederOnly).State, CareAction.Feed).State;
        Equal(MomentStatus.OnTime, early.Care.StatusOf(CareMoment.Morning), "an on-time hands-on meal is on time");

        // The day closes as a day with a missed meal: the same rhythm and temperament as without the helper.
        var dayWith = game.Advance(with, At(4, 4, 30), FeederOnly).State;
        var dayWithout = game.Advance(without, At(4, 4, 30)).State;
        Equal(dayWithout.Bonding.Rhythm, dayWith.Bonding.Rhythm, "the rhythm is the same");
        Equal(dayWithout.Bonding.HelperDay, dayWith.Bonding.HelperDay, "a paid helper's day is not the absence helper's day");
        Near(dayWithout.Bonding.Security, dayWith.Bonding.Security, "temperament is the same");
        Equal(dayWithout.Bonding.TotalCareDays, dayWith.Bonding.TotalCareDays, "care days are the same");
    }

    // Play tops Comfort up from its floor, tidy restores Cleanliness to 70, each at most three times a local care day.
    private static void PlayAndTidyLimits()
    {
        var game = Game();
        var baby = Baby(At(3, 8, 0), satiety: 95, comfort: 41, cleanliness: 90);
        var events = new List<HelperEvent>();
        var state = baby;
        for (var step = 1; step <= 24; step++)
            state = game.Advance(state, At(3, 8, 0).AddHours(step), HelperSettings.All, events).State;
        var day3 = RoutineCalendar.CareDayOf(At(3, 12, 0).UtcTicks, TimeSpan.Zero, Tunables.DayStart);
        foreach (var kind in new[] { HelperKind.Play, HelperKind.Tidy })
        {
            var mine = events.Where(e => e.Kind == kind).ToList();
            Equal(3, mine.Count(e => RoutineCalendar.CareDayOf(e.At.UtcTicks, TimeSpan.Zero, Tunables.DayStart) == day3),
                $"{kind} acts three times on a care day");
            var next = mine.Where(e => RoutineCalendar.CareDayOf(e.At.UtcTicks, TimeSpan.Zero, Tunables.DayStart) == day3 + 1).ToList();
            Equal(true, next.Count >= 1, $"{kind} acts again on the next care day");
            Equal(RoutineCalendar.CareDayStart(day3 + 1, TimeSpan.Zero, Tunables.DayStart), next[0].At.UtcTicks,
                $"{kind} waited at its floor for the new care day (04:00)");
            foreach (var helped in mine)
            {
                Equal(true, helped.Before <= Rules.FloorOf(kind) + 1e-6, $"{kind} acts only at its floor");
                Equal(Math.Max(helped.Before, Rules.LevelOf(kind)), helped.After, $"{kind} raises the need to its level");
            }
        }

        Equal(true, events.Where(e => e.Kind == HelperKind.Tidy).All(e => e.After == 70), "tidy restores to 70, never 100");
        // The wash stays hands-on: a missed wash still leaves its mess, as without helpers.
        var plain = baby;
        for (var step = 1; step <= 24; step++)
            plain = game.Advance(plain, At(3, 8, 0).AddHours(step)).State;
        Equal(plain.Care.Mess, state.Care.Mess, "tidy never clears the mess a missed wash leaves");
        Equal(true, state.Care.Mess > 0, "which is there");
        Equal(plain.Care.Wash, state.Care.Wash, "or the wash");
    }

    // Helpers act only within 24 hours of the last hands-on care; then the needs fall as without them.
    private static void Coverage()
    {
        var game = Game();
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 70, comfort: 60, cleanliness: 60));
        var events = new List<HelperEvent>();
        var state = baby;
        for (var step = 1; step <= 48; step++)
            state = game.Advance(state, At(3, 8, 0).AddHours(step), HelperSettings.All, events).State;
        var end = baby.Care.LastCareAt + Rules.Coverage;
        Equal(true, events.Count > 0, "helpers act inside the coverage window");
        Equal(true, events.All(e => e.At < end), "and never after it");
        Equal(true, state.Satiety < Rules.FeederFloor, "after it the needs fall as without helpers");

        // Hands-on care opens a new window.
        var cared = game.ApplyCare(game.StockFeeder(state), CareAction.Soothe).State;
        events.Clear();
        game.Advance(cared, cared.TimeCheckpoint.AddMinutes(1), HelperSettings.All, events);
        Equal(true, events.Any(e => e.Kind == HelperKind.Feeder), "care starts the coverage again");
    }

    // Helpers never touch Bond, temperament, the rhythm, engagement, counts, age, milestones, Energy or Health directly.
    private static void NothingElseMoves()
    {
        var game = Game();
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 70, comfort: 45, cleanliness: 45));
        baby = game.ApplyCare(baby, CareAction.Play).State;
        var events = new List<HelperEvent>();
        game.Advance(baby, At(3, 11, 50), HelperSettings.All, events);
        Equal(true, events.Select(e => e.Kind).Distinct().Count() == 3, "all three helpers act");
        foreach (var helped in events)
        {
            // Across the instant it acts, only its need moves.
            var before = game.Advance(baby, helped.At.AddTicks(-1), HelperSettings.All).State;
            var after = game.Advance(baby, helped.At.AddTicks(1), HelperSettings.All).State;
            Near(before.Energy, after.Energy, $"{helped.Kind} leaves Energy alone", 1e-6);
            Near(before.Health, after.Health, $"{helped.Kind} never restores Health", 1e-6);
            Near(before.Bond, after.Bond, $"{helped.Kind} leaves Bond alone", 1e-12);
            Equal(before.Bonding, after.Bonding, $"{helped.Kind} leaves temperament and the rhythm alone");
            Equal(before.Development.Engagement.Count, after.Development.Engagement.Count, $"{helped.Kind} adds no engagement");
            double Need(BabyState state) => helped.Kind == HelperKind.Feeder ? state.Satiety
                : helped.Kind == HelperKind.Play ? state.Comfort : state.Cleanliness;
            Near(helped.After - helped.Before, Need(after) - Need(before), $"{helped.Kind} raises its need", 1e-5);
        }

        // One long advance from a content baby: Bond, temperament, rhythm, engagement and counts as without helpers.
        var from = game.Advance(baby, At(3, 9, 0)).State;
        var dayWith = game.Advance(from, At(4, 4, 30), HelperSettings.All).State;
        var dayWithout = game.Advance(from, At(4, 4, 30)).State;
        Equal(true, game.Journal(dayWith).Entries.Any(e => e.Kind == JournalKind.Helper), "the helpers acted");
        Near(dayWithout.Bond, dayWith.Bond, "Bond is the same");
        Equal(dayWithout.Bonding, dayWith.Bonding, "and so are temperament, the rhythm and the day's caps");
        Equal(string.Join(",", dayWithout.Development.Engagement.Select(e => e.Kind + "@" + e.At)),
            string.Join(",", dayWith.Development.Engagement.Select(e => e.Kind + "@" + e.At)), "helpers add no engagement");
        Equal((dayWithout.ToyFamiliarity, dayWithout.PlushFamiliarity, dayWithout.StorySessions, dayWithout.BlockTowers),
            (dayWith.ToyFamiliarity, dayWith.PlushFamiliarity, dayWith.StorySessions, dayWith.BlockTowers), "or familiarity and counts");
        Equal((dayWithout.CanSitAndReach, dayWithout.CanCrawl, dayWithout.CanWalk), (dayWith.CanSitAndReach, dayWith.CanCrawl,
            dayWith.CanWalk), "or milestones");
        var meals = dayWith.Care.Meals.ToCode();
        Equal(false, meals.Contains('O') || meals.Contains('L'), "helped meals give no meal credit");

        // No Health helper: a sick baby's Health is never raised by a helper.
        var sick = BabyState.Restore("Mia", At(3, 8, 0), TimeSpan.FromDays(4), 46, 41, 80, 3, 0, true, false, 10, 1, 41, 25,
            true, care: Sick(At(3, 8, 0)));
        events.Clear();
        var later = game.Advance(game.StockFeeder(sick), At(3, 8, 30), HelperSettings.All, events).State;
        Equal(true, events.Count > 0, "helpers still hold the needs of a sick baby");
        Equal(true, later.Health <= sick.Health, "but never raise Health");
        Equal(CareCondition.Sick, later.Care.Condition, "or end a sickness");
    }

    private static void ResumeAndClock()
    {
        var game = Game();
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 70, comfort: 45, cleanliness: 45));
        var events = new List<HelperEvent>();
        var once = game.Advance(baby, At(3, 14, 0), HelperSettings.All, events).State;
        var count = events.Count;
        Equal(true, count >= 3, "helpers acted");
        var again = game.Advance(once, At(3, 14, 0), HelperSettings.All, events).State;
        Equal(once, again, "a repeated resume at the same instant changes nothing");
        Equal(count, events.Count, "and applies no helper twice");
        var reopened = BabySaveCodec.Decode(BabySaveCodec.Encode(once));
        game.Advance(reopened, At(3, 14, 0), HelperSettings.All, events);
        Equal(count, events.Count, "nor after a reload");
        var back = game.Advance(once, At(3, 10, 0), HelperSettings.All, events).State;
        Equal(once, back, "a backwards clock changes nothing");
        Equal(count, events.Count, "and gives no activations");
    }

    // One long advance and many short ones find the same activations (within the 8-hour cap), across a care-day boundary
    // where a helper at its daily limit waits for 04:00.
    private static void SplitAdvances()
    {
        var game = Game();
        var day = Baby(At(3, 8, 0), satiety: 70, comfort: 45, cleanliness: 45);
        // Play has used its three top-ups today and waits at its floor for 04:00 (Comfort stays above the fussy level,
        // since a mood that changes between short advances is path dependent on purpose).
        var night = Baby(At(4, 1, 0), satiety: 50, comfort: 39, cleanliness: 45, energy: 60);
        var nightDay = RoutineCalendar.CareDayOf(At(4, 1, 0).UtcTicks, TimeSpan.Zero, Tunables.DayStart);
        night = night.WithCareForTests(night.Care.WithHelpers(new HelperRecord(2, nightDay, 0, 3, 0, 0)));
        foreach (var (name, restored, to, steps) in new[]
        {
            ("day", game.StockFeeder(day), At(3, 15, 50), 47), ("night", night, At(4, 8, 30), 31),
        })
        {
            // A restored baby seeds its development on its first advance (at the nominal pace), so start from one that has.
            var start = game.Advance(restored, restored.TimeCheckpoint.AddMinutes(1), HelperSettings.All).State;
            var wholeEvents = new List<HelperEvent>();
            var whole = game.Advance(start, to, HelperSettings.All, wholeEvents).State;
            var splitEvents = new List<HelperEvent>();
            var split = start;
            var from = start.TimeCheckpoint;
            for (var i = 1; i <= steps; i++)
                split = game.Advance(split, from + TimeSpan.FromTicks((to - from).Ticks * i / steps), HelperSettings.All,
                    splitEvents).State;
            Equal(true, wholeEvents.Count >= 3, $"{name}: helpers acted");
            Equal(string.Join(",", wholeEvents.Select(e => e.Kind)), string.Join(",", splitEvents.Select(e => e.Kind)),
                $"{name}: the same activations");
            for (var i = 0; i < wholeEvents.Count; i++)
                Equal(true, Math.Abs(wholeEvents[i].At.UtcTicks - splitEvents[i].At.UtcTicks) <= 1,
                    $"{name}: at the same instants");
            Near(whole.Satiety, split.Satiety, $"{name}: Fullness", 1e-9);
            Near(whole.Comfort, split.Comfort, $"{name}: Comfort", 1e-9);
            Near(whole.Cleanliness, split.Cleanliness, $"{name}: Cleanliness", 1e-9);
            Near(whole.Energy, split.Energy, $"{name}: Energy", 1e-9);
            Near(whole.Health, split.Health, $"{name}: Health", 1e-9);
            var w = whole.Care.Helpers;
            var s = split.Care.Helpers;
            Equal((w.Stock, w.Day, w.Feeds, w.Plays, w.Tidies), (s.Stock, s.Day, s.Feeds, s.Plays, s.Tidies), $"{name}: helper counts");
            Equal(true, Math.Abs(w.LastServe - s.LastServe) <= 1, $"{name}: last serve");
            Equal(whole.Care.MomentCode, split.Care.MomentCode, $"{name}: the ledger");
            Equal(whole.Care.Meals.ToCode(), split.Care.Meals.ToCode(), $"{name}: the meal ring");
            Near(whole.Bond, split.Bond, $"{name}: Bond", 1e-9);
            Near(whole.Development.Readiness, split.Development.Readiness, $"{name}: readiness", 1e-9);
            Equal(string.Join(";", Helpers(whole)), string.Join(";", Helpers(split)), $"{name}: the journal's helper days");
        }

        Equal(true, Helpers(game.Advance(night, At(4, 8, 30), HelperSettings.All).State).Count > 0, "the night's helpers write a day");
    }

    private static void Determinism()
    {
        string Log()
        {
            var game = Game();
            var events = new List<HelperEvent>();
            var state = game.StockFeeder(Baby(At(3, 8, 0), satiety: 52, comfort: 44, cleanliness: 47));
            for (var step = 1; step <= 30; step++)
                state = game.Advance(state, At(3, 8, 0).AddMinutes(step * 47), HelperSettings.All, events).State;
            return string.Join("\n", events) + "\n" + BabySaveCodec.Encode(state);
        }

        var first = Log();
        Equal(first, Log(), "the same days give the same helper log and save");
    }

    private static void AwayAndMigration()
    {
        var game = Game();
        // Away mode holds the needs itself; helpers do nothing while it lasts.
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 50, comfort: 42, cleanliness: 42));
        var away = game.StartAway(baby, TimeSpan.FromDays(2));
        var events = new List<HelperEvent>();
        var held = game.Advance(away, At(4, 8, 0), HelperSettings.All, events).State;
        Equal(0, events.Count, "helpers rest while Away mode holds the needs");
        Equal(6, held.Care.Helpers.Stock, "and keep their stock");

        // A save from before care (or a migrated ledger) excuses its first gap: helpers wait for the anchor too.
        var old = BabyState.Restore("Sam", At(3, 8, 0), TimeSpan.FromDays(2), 30, 30, 50, 0, 0, false, false, 5, 0, 30, 90, false);
        var anchored = game.Advance(old, At(3, 15, 0), HelperSettings.All, events).State;
        Equal(0, events.Count, "an unanchored ledger gets no helpers on its first advance");
        Equal(true, anchored.Care.Helpers.IsEmpty, "and keeps no helper state");
        game.Advance(anchored, At(3, 15, 30), HelperSettings.All, events);
        Equal(true, events.Count > 0, "then they act");
    }

    // Next day: inside the coverage window helpers act through the preview's awake hours as on a new day.
    private static void PreviewDay()
    {
        var game = Game();
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 60, comfort: 50, cleanliness: 60));
        var events = new List<HelperEvent>();
        var next = game.AdvancePreview(baby, TimeSpan.FromDays(1), HelperSettings.All, events).State;
        Equal(true, events.Count >= 2, "helpers act in a preview day");
        Equal(true, events.All(e => e.At == baby.TimeCheckpoint), "at the checkpoint, since no real time passes");
        Equal(baby.Care.CareDay, next.Care.CareDay, "the ledger holds");
        Equal(baby.Care.MomentCode, next.Care.MomentCode, "no window passes");
        Equal(6 - events.Count(e => e.Kind == HelperKind.Feeder), next.Care.Helpers.Stock, "portions are used");
        Equal(true, Helpers(next).Count == 1, "the preview day has a helper entry");
        var plain = game.AdvancePreview(baby, TimeSpan.FromDays(1)).State;
        Near(plain.Bond, next.Bond, "a preview day's helpers leave Bond alone");
        Equal(plain.Bonding, next.Bonding, "and the rhythm");

        // Each preview day starts the counts again, so Next day keeps showing the helpers.
        var third = game.AdvancePreview(game.AdvancePreview(next, TimeSpan.FromDays(1), HelperSettings.All, events).State,
            TimeSpan.FromDays(1), HelperSettings.All, events).State;
        Equal(true, events.Count(e => e.Kind == HelperKind.Tidy) >= 3, "tidy acts on each preview day");
        Equal(true, Helpers(third).Count >= 2, "with an entry per day");

        // Outside the coverage window nothing acts.
        var late = game.Advance(baby, At(4, 9, 0)).State;
        events.Clear();
        game.AdvancePreview(late, TimeSpan.FromDays(1), HelperSettings.All, events);
        Equal(0, events.Count, "a preview day past the coverage window has no helpers");
    }

    private static void Journal()
    {
        var game = Game();
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 70, comfort: 45, cleanliness: 45));
        var events = new List<HelperEvent>();
        var state = game.Advance(baby, At(3, 15, 0), HelperSettings.All, events).State;
        var entries = Helpers(state);
        Equal(1, entries.Count, "one helper entry for the day");
        var entry = game.Journal(state).Entries.Single(e => e.Kind == JournalKind.Helper);
        Equal((events.Count(e => e.Kind == HelperKind.Feeder), events.Count(e => e.Kind == HelperKind.Play),
            events.Count(e => e.Kind == HelperKind.Tidy)), (entry.Feeds, entry.Plays, entry.Tidies), "with the day's counts");
        Equal(events.Last().At, entry.At, "at the last of them");
        Equal(JournalRules.DayOf(state.Age), entry.Day, "on the little one's day");

        // Kept to one entry a day, and the first to go when the journal is full.
        var full = JournalRules.Start(At(1, 8, 0));
        for (var i = 0; i < JournalRecord.MaxEntries - 1; i++)
            full = full.With(JournalEntry.Photo(At(1, 9, 0).AddMinutes(i), 1, PhotoMoment.Calm, MotorSkill.None, BabyAppearance.Default));
        full = JournalRules.Helpers(full, new[] { new HelperEvent(HelperKind.Tidy, At(1, 9, 0), TimeSpan.Zero, 40, 70) }, TimeSpan.Zero);
        full = full.With(JournalEntry.Photo(At(2, 9, 0), 2, PhotoMoment.Calm, MotorSkill.None, BabyAppearance.Default));
        Equal(0, full.Entries.Count(e => e.Kind == JournalKind.Helper), "a helper day goes before any photo");

        var encoded = JournalRecordCodecProbe(game.Journal(state));
        Equal(true, encoded.Contains("<journal v=\"2\""), "a journal with helper days is v2");
        Equal(true, JournalRecordCodecProbe(game.Journal(baby)).Contains("<journal v=\"1\""), "one without them stays v1");
    }

    private static void Saves()
    {
        var game = Game();
        var baby = game.StockFeeder(Baby(At(3, 8, 0), satiety: 70, comfort: 45, cleanliness: 45));
        var state = game.Advance(baby, At(3, 15, 0), HelperSettings.All).State;
        var payload = BabySaveCodec.Encode(state);
        Equal(true, payload.Contains("<care v=\"4\"") && payload.Contains("<helpers stock=\"5\""), "helper state is saved in care v4");
        var decoded = BabySaveCodec.Decode(payload);
        Equal(state.Care, decoded.Care, "and round-trips");
        Equal(payload, BabySaveCodec.Encode(decoded), "byte for byte");
        var plain = BabySaveCodec.Encode(game.Advance(Baby(At(3, 8, 0)), At(3, 9, 0)).State);
        Equal(true, plain.Contains("<care v=\"3\"") && !plain.Contains("<helpers"), "without helper state the record stays v3");

        // An older build leaves a v4 record alone; a v3 record cannot hold a helped moment.
        var root = XElement.Parse(payload);
        root.Element("care").SetAttributeValue("v", 5);
        Expect<NotSupportedException>(() => BabySaveCodec.Decode(root.ToString(SaveOptions.DisableFormatting)),
            "a newer care record is unsupported");
        var helped = game.Advance(game.StockFeeder(Baby(At(3, 8, 0), satiety: 70)), At(3, 11, 50), FeederOnly).State;
        var v3 = XElement.Parse(BabySaveCodec.Encode(helped));
        v3.Element("care").SetAttributeValue("v", 3);
        v3.Element("care").Element("helpers").Remove();
        v3.Element("care").SetAttributeValue("moments", "HPPP");
        Expect<InvalidDataException>(() => BabySaveCodec.Decode(v3.ToString(SaveOptions.DisableFormatting)),
            "a helped moment needs care v4");
        foreach (var (name, value) in new[] { ("stock", "13"), ("stock", "-1"), ("plays", "-2"), ("served", "-5"),
            ("served", long.MaxValue.ToString()) })
        {
            var broken = XElement.Parse(payload);
            broken.Element("care").Element("helpers").SetAttributeValue(name, value);
            Expect<InvalidDataException>(() => BabySaveCodec.Decode(broken.ToString(SaveOptions.DisableFormatting)),
                $"helpers {name}={value} is rejected");
        }

        // A save from before helpers (any older version) loads with helpers off: no stock, nothing counted.
        Equal(true, BabySaveCodec.Decode(plain).Care.Helpers.IsEmpty, "an older record has no helper state");
    }

    // A cat's helpers are objects: an auto-feeder, a ball launcher and a self-cleaning litter box. A dog keeps its simple
    // rules and ignores them.
    private static void Cat()
    {
        var sim = new PetSimulation();
        var cat = sim.StockFeeder(CatAt(At(3, 8, 0), fullness: 55, comfort: 42, cleanliness: 42));
        Equal(6, cat.Care.Helpers.Stock, "the auto-feeder is stocked");
        var events = new List<HelperEvent>();
        var helped = sim.Step(cat, At(3, 12, 0), HelperSettings.All, events).State;
        Equal(true, events.Any(e => e.Kind == HelperKind.Feeder), "the auto-feeder serves");
        Equal(true, events.Any(e => e.Kind == HelperKind.Play), "the ball launcher plays");
        Equal(true, events.Any(e => e.Kind == HelperKind.Tidy), "the litter box cleans itself");
        Equal(MomentStatus.Helped, helped.Care.StatusOf(CareMoment.Morning), "breakfast from the feeder is Helped");
        var plain = sim.Step(cat, At(3, 12, 0)).State;
        Near(plain.Bond, helped.Bond, "the cat's Bond is the same");
        Equal(plain.HighestBond, helped.HighestBond, "and its highest Bond");
        Equal((plain.Liveliness, plain.Boldness), (helped.Liveliness, helped.Boldness), "and its temperament");
        Equal(plain.Milestones, helped.Milestones, "and its milestones");
        Equal(true, Helpers(helped, sim).Count == 1, "the cat's journal has the day");
        var payload = PetSaveCodec.Encode(helped);
        Equal(true, payload.Contains("<helpers "), "the pet record saves its helpers");
        Equal(payload, PetSaveCodec.Encode(PetSaveCodec.Decode(payload, Catalog)), "and round-trips");

        // The dog has helpers from P3 (DogCareChecks); a rabbit keeps the simple rules.
        var rabbit = sim.Adopt("Clover", PetAppearance.Default(Catalog,
            Catalog.BreedsOf(Species.Rabbit).First(b => b.Unlock == BreedUnlock.Free).Breed), At(3, 8, 0));
        events.Clear();
        var rabbitHelped = sim.Step(rabbit, At(3, 20, 0), HelperSettings.All, events).State;
        Equal(0, events.Count, "a rabbit keeps its simple rules: no helpers yet");
        Equal(PetSaveCodec.Encode(sim.Step(rabbit, At(3, 20, 0)).State), PetSaveCodec.Encode(rabbitHelped), "and the same record");
        Equal(rabbit, sim.StockFeeder(rabbit), "a rabbit has no feeder to stock");
    }

    // Entitlements, developer mode, the household setting and the sessions.
    private static void Household()
    {
        Equal(HelperSettings.None, HelperOwnership.Settings(NoEntitlements.Instance, null), "the free game has no helpers");
        Equal(HelperSettings.All, HelperOwnership.Settings(new FakeEntitlements(ProductIds.HelperSet), null), "the set is all three");
        Equal(new HelperSettings(true, false, false), HelperOwnership.Settings(new FakeEntitlements(ProductIds.HelperFeeder), null),
            "a single helper on its own");
        Equal(new HelperSettings(false, true, true), HelperOwnership.Settings(new FakeEntitlements(ProductIds.HelperSet),
            new[] { HelperKind.Feeder }), "a switched-off helper stays off");
        foreach (var product in ProductIds.HelperProducts)
            Equal(true, ProductIds.IsKnownFamily(product), $"{product} is a catalogue product");

        var now = At(3, 8, 0);
        var store = new MemoryHouseholdStore();
        var owned = new FakeEntitlements();
        var game = Game();
        var household = new HouseholdSession(game, new PetSimulation(), store, () => now, owned);
        var baby = household.Adopt("Mia");
        Equal(HelperSettings.None, household.Helpers, "nothing owned, no helpers");
        Equal(false, household.OwnsAnyHelper, "and none owned");
        var manifest = store.ManifestPrimary;

        owned.Grant(ProductIds.HelperSet);
        Equal(HelperSettings.All, household.Helpers, "owned helpers are on by default");
        Equal(true, household.IsHelperSwitchedOn(HelperKind.Feeder), "the feeder is on");
        household.SetHelperOn(HelperKind.Feeder, false);
        Equal(true, store.ManifestPrimary.Contains("<helpers off=\"0\" />"), "switching a helper off is saved in the manifest");
        Equal(new HelperSettings(false, true, true), household.Helpers, "and stops it");
        var reopened = new HouseholdSession(game, new PetSimulation(), store, () => now, owned);
        Equal(false, reopened.IsHelperSwitchedOn(HelperKind.Feeder), "the setting survives a reload");
        Equal(new HelperSettings(false, true, true), reopened.Helpers, "for the whole household");
        reopened.SetHelperOn(HelperKind.Feeder, true);
        Equal(manifest, store.ManifestPrimary, "switching it back on returns the manifest to its bytes");

        // The session advances with the household's helpers and reports what they did once.
        var session = reopened.Active.Session;
        session.StockFeeder();
        Equal(6, session.FeederStock, "the Sitter's bottles are stocked");
        now = At(3, 15, 0);
        reopened.AdvanceAll();
        var done = reopened.TakeHelperEvents(baby.Slot);
        Equal(true, done.Count > 0, "the session reports what the helpers did");
        Equal(0, reopened.TakeHelperEvents(baby.Slot).Count, "once");
        Equal(true, session.FeederStock < 6, "the stock is saved");
        Equal(session.FeederStock, new HouseholdSession(game, new PetSimulation(), store, () => now, owned).Active.Session.FeederStock,
            "and reloads");

        // A refund stops the helpers; what they did stays.
        var before = session.State;
        owned.Revoke(ProductIds.HelperSet);
        now = At(3, 20, 0);
        reopened.AdvanceAll();
        Equal(0, reopened.TakeHelperEvents(baby.Slot).Count, "a refunded helper stops");
        Equal(before.Care.Helpers.Stock, session.State.Care.Helpers.Stock, "and its effects and stock stay");
        Equal(true, game.Journal(session.State).Entries.Any(e => e.Kind == JournalKind.Helper), "with its journal days");

        // Home's preview tick shows helper-held needs without writing.
        owned.Grant(ProductIds.HelperSet);
        var writes = store.RecordWrites;
        now = At(4, 2, 0);
        var preview = reopened.PreviewState(baby.Slot);
        Equal(writes, store.RecordWrites, "Home's preview writes nothing");
        Equal(true, preview.Cleanliness >= Rules.TidyFloor - 1e-6 || preview.Care.Helpers.Tidies >= Rules.DailyLimit,
            "but shows the helpers at work");
    }

    private static List<(int Day, int Feeds, int Plays, int Tidies)> Helpers(BabyState state) =>
        Game().Journal(state).Entries.Where(e => e.Kind == JournalKind.Helper).Select(e => (e.Day, e.Feeds, e.Plays, e.Tidies)).ToList();

    private static List<(int Day, int Feeds, int Plays, int Tidies)> Helpers(PetState state, PetSimulation sim) =>
        sim.Journal(state).Entries.Where(e => e.Kind == JournalKind.Helper).Select(e => (e.Day, e.Feeds, e.Plays, e.Tidies)).ToList();

    private static string JournalRecordCodecProbe(JournalRecord journal)
    {
        // The journal is saved inside the baby's record; read it back from there.
        var game = Game();
        var baby = Baby(At(3, 8, 0));
        var payload = BabySaveCodec.Encode(baby.WithJournalForTests(journal));
        return XElement.Parse(payload).Element("journal").ToString(SaveOptions.DisableFormatting);
    }

    private static BabySimulation Game() =>
        new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay, TimeSpan.FromDays(7), TimeSpan.FromDays(14));

    // A sitter on an anchored ledger whose windows count from `at`, on UTC.
    private static BabyState Baby(DateTimeOffset at, double satiety = 80, double comfort = 80, double energy = 80,
        double cleanliness = 90) =>
        BabyState.Restore("Mia", at, TimeSpan.FromDays(4), satiety, comfort, energy, 3, 0, true, false, 30, 1, cleanliness, 90, true,
            care: Anchored(at));

    private static CareRecord Anchored(DateTimeOffset at)
    {
        var draft = CareRules.Start(at, Tunables).ToBuilder();
        // Windows already open count too (those that closed before `at` stay excused).
        draft.ExcusedUntil = at.AddDays(-1);
        return draft.Build();
    }

    private static CareRecord Sick(DateTimeOffset at)
    {
        var draft = Anchored(at).ToBuilder();
        draft.Condition = CareCondition.Sick;
        draft.ConditionCause = CareConditionCause.LowHealth;
        draft.ConditionSince = at.AddHours(-2);
        return draft.Build();
    }

    private static PetState CatAt(DateTimeOffset at, double fullness = 80, double comfort = 80, double cleanliness = 80)
    {
        var draft = CareRules.Start(at, Tunables, CareRoutine.Cat).ToBuilder();
        draft.ExcusedUntil = at.AddDays(-1);
        var meters = new Dictionary<PetMeter, double>
        {
            [PetMeter.Fullness] = fullness, [PetMeter.Energy] = 80, [PetMeter.Comfort] = comfort,
            [PetMeter.Cleanliness] = cleanliness, [PetMeter.Health] = 100,
        };
        return PetState.Restore(Catalog, "Kit", Tabby, at, TimeSpan.FromDays(1), meters, 20, 20, 0, 0, false,
            draft.Build(), null, null, null, 0);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new DateTimeOffset(2026, 3, day, hour, minute, 0, TimeSpan.Zero);

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

    private static void Near(double expected, double actual, string description, double tolerance = 1e-9)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }
}

static class HelperTestExtensions
{
    // Test-only ways to set one part of a restored baby, through its save.
    public static BabyState WithCareForTests(this BabyState state, CareRecord care) =>
        BabyState.Restore(state.Name, state.TimeCheckpoint, state.Age, state.Satiety, state.Comfort, state.Energy,
            state.ToyFamiliarity, state.PlushFamiliarity, state.CanSitAndReach, state.CanCrawl, state.Bond, state.StorySessions,
            state.Cleanliness, state.Health, state.NewSoundResolved, state.NurseryTheme, state.CanWalk, state.BlockTowers,
            state.Appearance, care, state.Development, state.Bonding, state.Journal);

    public static BabyState WithJournalForTests(this BabyState state, JournalRecord journal) =>
        BabyState.Restore(state.Name, state.TimeCheckpoint, state.Age, state.Satiety, state.Comfort, state.Energy,
            state.ToyFamiliarity, state.PlushFamiliarity, state.CanSitAndReach, state.CanCrawl, state.Bond, state.StorySessions,
            state.Cleanliness, state.Health, state.NewSoundResolved, state.NurseryTheme, state.CanWalk, state.BlockTowers,
            state.Appearance, state.Care, state.Development, state.Bonding ?? BondRules.Seed(state.Bond, state.Care), journal);
}
