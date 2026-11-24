using System;
using System.IO;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

if (args.Length > 0 && args[0] == "growth")
{
    GrowthChecks.Run();
    return;
}

if (args.Length > 0 && args[0] == "golden")
{
    Console.WriteLine(GoldenScenario.Hash());
    return;
}

if (args.Length > 0 && args[0] == "talk")
{
    TalkChecks.Run();
    return;
}

if (args.Length > 0 && args[0] == "bond")
{
    BondChecks.Run();
    return;
}

var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7), TimeSpan.FromDays(12));
var newborn = game.Adopt("Mia", start);

var fed = game.ApplyCare(newborn, CareAction.Feed);
Equal(100.0, fed.State.Satiety, "feeding improves satiety (milk +35)");
Equal(100.0, game.ApplyCare(fed.State, CareAction.Feed).State.Satiety, "satiety is capped");
Equal(TimeSpan.Zero, fed.State.Age, "care leaves age unchanged");
Equal(Reaction.Satisfied, fed.Reaction, "feeding cue");
Equal(95.0, game.ApplyCare(newborn, CareAction.Soothe).State.Comfort, "soothing improves comfort");

Equal(80.0, newborn.Energy, "newborn starts with energy");
var rested = game.ApplyCare(newborn, CareAction.Rest);
Equal(100.0, rested.State.Energy, "rest caps energy at 100");
Equal(Reaction.Rested, rested.Reaction, "rest reaction cue");
Equal(newborn.Age, rested.State.Age, "rest leaves age unchanged");
Equal(newborn.Satiety, rested.State.Satiety, "rest leaves satiety unchanged");

var played = game.ApplyCare(newborn, CareAction.Play);
Equal(68.0, played.State.Energy, "play uses energy");
Equal(1, played.State.ToyFamiliarity, "play creates familiarity");
Equal(Reaction.TracksToy, played.Reaction, "newborn tracks toy");

// Readiness sets the day (GrowthChecks); every milestone lands between 0.8x and 1.4x of its nominal age.
var before = game.Advance(played.State, start.AddDays(2.4).AddTicks(-1));
Equal(false, before.UnlockedSitting, "sitting never comes before 0.8x its nominal age");
var threshold = game.Advance(played.State, start.AddDays(4.2));
Equal(true, threshold.UnlockedSitting, "sitting always comes by 1.4x its nominal age");
Equal(TimeSpan.FromDays(4.2), threshold.State.Age, "elapsed time advances age");
Equal(Reaction.ReachesForToy, game.ApplyCare(threshold.State, CareAction.Play).Reaction,
    "older baby reaches for familiar toy");
Equal(Reaction.TracksToy, game.ApplyCare(game.Advance(newborn, start.AddDays(4.2)).State, CareAction.Play).Reaction,
    "older baby tracks unfamiliar toy");

// Adopted in the morning, so the capped hours are awake hours (Fullness 7/h, Comfort 4/h, Energy 6/h).
var resume = game.Advance(game.Adopt("Mia", start.AddHours(8)), start.AddDays(10).AddHours(8));
Equal(42.0, resume.State.Satiety, "offline satiety decay is capped");
Equal(59.0, resume.State.Comfort, "offline comfort decay is capped");
Equal(56.0, resume.State.Energy, "offline energy decay is capped");
var tired = resume.State;
for (var i = 0; i < 6; i++)
    tired = game.ApplyCare(tired, CareAction.Play).State;
Equal(0.0, tired.Energy, "play cannot reduce energy below zero");
Equal(0.0, game.Advance(tired, tired.TimeCheckpoint.AddHours(1)).State.Energy, "elapsed time cannot reduce energy below zero");
Equal(35.0, game.ApplyCare(tired, CareAction.Rest).State.Energy, "rest recovers energy at zero");
var repeat = game.Advance(resume.State, resume.State.TimeCheckpoint.ToOffset(TimeSpan.FromHours(8)));
Equal(resume.State, repeat.State, "same instant in a new timezone is a no-op");
Equal(false, repeat.UnlockedSitting, "milestone cannot fire twice");
Equal(resume.State, game.Advance(resume.State, start).State, "backwards clock preserves state");
Equal(false, game.Advance(resume.State, start.AddDays(11)).UnlockedSitting, "future ticks preserve milestone");
var now = start;
var saves = new MemorySaveStore();
var session = new BabySession(game, saves, () => now);
Equal<BabyState>(null, session.State, "new session starts with no baby");
session.Adopt("Mia & Co");
session.ApplyCare(CareAction.Play);
Equal((true, false, false), session.AdvancePreview(TimeSpan.FromDays(4.2)), "preview unlocks sitting");
Equal(start, session.State.TimeCheckpoint, "preview leaves real clock alone");

var reopened = new BabySession(game, saves, () => now);
Equal("Mia & Co", reopened.State.Name, "name survives XML round trip");
Equal(TimeSpan.FromDays(4.2), reopened.State.Age, "preview aging persists");
Equal(1, reopened.State.ToyFamiliarity, "familiarity persists");
Equal(44.0, reopened.State.Energy, "energy persists");
Equal(true, reopened.State.CanSitAndReach, "milestone persists");
Equal((false, false, false), reopened.Refresh(), "same-time reopen does not unlock or write");
Equal(3, saves.Writes, "each edit writes one save and no-op refresh skips a write");

now = now.AddHours(1);
var afterReopenCare = reopened.ApplyCare(CareAction.Play);
Equal(Reaction.ReachesForToy, afterReopenCare.Reaction, "reopened care uses persisted familiarity");
Equal(TimeSpan.FromDays(4.2) + TimeSpan.FromHours(1), reopened.State.Age, "real time advances after preview");
Equal(4, saves.Writes, "age and care commit together");

var savedBeforeFailure = saves.Primary;
saves.FailWrites = true;
try
{
    reopened.ApplyCare(CareAction.Feed);
    throw new InvalidOperationException("Failed write should be surfaced to the UI.");
}
catch (IOException)
{
    Equal(savedBeforeFailure, saves.Primary, "failed write leaves valid prior payload");
    Equal(39.5, reopened.State.Satiety, "failed care leaves visible state unchanged");
}

saves.Primary = "<baby";
var recovered = new BabySession(game, saves, () => now);
Equal(true, recovered.State.CanSitAndReach, "corrupt primary falls back to previous valid save");
Equal(1, recovered.State.ToyFamiliarity, "backup recovers last valid pre-edit state");
saves.FailWrites = false;
recovered.ApplyCare(CareAction.Play);
Equal(true, new BabySession(game, saves, () => now).State.CanSitAndReach, "recovery can save again");

saves.Primary = "<baby version=\"17\" />";
try
{
    _ = new BabySession(game, saves, () => now);
    throw new InvalidOperationException("A future save version must fail instead of loading older backup.");
}
catch (NotSupportedException)
{
    Equal("<baby version=\"17\" />", saves.Primary, "future save is preserved");
}

const string legacySave = "<baby version=\"1\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>2592000000000</age><satiety>41</satiety><comfort>52</comfort>" +
    "<toy-familiarity>2</toy-familiarity><can-sit-and-reach>true</can-sit-and-reach></baby>";
var migratedSaves = new MemorySaveStore { Primary = legacySave };
var migrated = new BabySession(game, migratedSaves, () => start);
Equal("Sam", migrated.State.Name, "v1 migration preserves name");
Equal(start, migrated.State.TimeCheckpoint, "v1 migration preserves time checkpoint");
Equal(TimeSpan.FromDays(3), migrated.State.Age, "v1 migration preserves age");
Equal(41.0, migrated.State.Satiety, "v1 migration preserves satiety");
Equal(52.0, migrated.State.Comfort, "v1 migration preserves comfort");
Equal(2, migrated.State.ToyFamiliarity, "v1 migration preserves rattle familiarity");
Equal(0, migrated.State.PlushFamiliarity, "v1 migration establishes plush as unfamiliar");
Equal(true, migrated.State.CanSitAndReach, "v1 migration preserves milestone");
Equal(80.0, migrated.State.Energy, "v1 migration supplies default energy");
migrated.ApplyCare(CareAction.Rest);
Equal(true, migratedSaves.Primary.Contains("version=\"16\""), "first edit after migration writes v14");
Equal(legacySave, migratedSaves.Backup, "v1 backup remains recoverable");
Equal(100.0, new BabySession(game, migratedSaves, () => start).State.Energy,
    "rest on migrated save persists energy in v13");

const string energySave = "<baby version=\"2\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>2592000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>3</toy-familiarity><can-sit-and-reach>true</can-sit-and-reach></baby>";
var toySaves = new MemorySaveStore { Primary = energySave };
var toys = new BabySession(game, toySaves, () => start);
Equal(63.0, toys.State.Energy, "v2 migration preserves energy");
Equal(3, toys.State.ToyFamiliarity, "v2 migration preserves rattle familiarity");
Equal(0, toys.State.PlushFamiliarity, "v2 migration establishes plush as unfamiliar");
Equal<Toy?>(Toy.Rattle, toys.State.FavoriteToy, "v2 history counts toward favoring the rattle");

Equal(Reaction.TracksToy, toys.ApplyCare(CareAction.Play, Toy.Plush).Reaction,
    "first play with plush is unfamiliar despite rattle history");
Equal(Reaction.ReachesForToy, toys.ApplyCare(CareAction.Play, Toy.Plush).Reaction,
    "sitting baby reaches for the plush once it is familiar");
Equal(3, toys.State.ToyFamiliarity, "playing with plush leaves rattle familiarity unchanged");
Equal<Toy?>(Toy.Rattle, toys.State.FavoriteToy, "rattle keeps the lead after two plush plays");
toys.ApplyCare(CareAction.Play, Toy.Plush);
Equal<Toy?>(null, toys.State.FavoriteToy, "equally familiar toys have no clear favorite");
toys.ApplyCare(CareAction.Play, Toy.Plush);
Equal<Toy?>(Toy.Plush, toys.State.FavoriteToy, "favorite can switch beyond the original count cap");
var persistedToys = new BabySession(game, toySaves, () => start);
Equal<Toy?>(Toy.Plush, persistedToys.State.FavoriteToy, "favorite derives from saved toy history");
Equal(3, persistedToys.State.ToyFamiliarity, "rattle count persists independently");
Equal(4, persistedToys.State.PlushFamiliarity, "plush count persists independently");
persistedToys.ApplyCare(CareAction.Play);
Equal<Toy?>(null, persistedToys.State.FavoriteToy, "default play still counts toward the rattle");
Equal(4, persistedToys.State.PlushFamiliarity, "default play leaves plush familiarity unchanged");

var maxToyPlay = BabyState.Restore("Mia", start, TimeSpan.Zero, 70, 75, 80, int.MaxValue, 0, false, false, 0, 0, 100, 100, false);
Equal(int.MaxValue, game.ApplyCare(maxToyPlay, CareAction.Play).State.ToyFamiliarity,
    "repeated play cannot overflow familiarity");

var almostCrawling = game.Advance(threshold.State, start.AddDays(5.6).AddTicks(-1));
Equal(false, almostCrawling.UnlockedCrawling, "crawling never comes before 0.8x its nominal age");
// Days unattended leave the baby upset; ordinary care calms them before play.
Equal(CareMood.Upset, almostCrawling.State.Care.Mood, "a long unattended stretch upsets the baby");
Equal(Reaction.ReachesForToy, game.ApplyCare(Calm(game, almostCrawling.State), CareAction.Play).Reaction,
    "sitter continues to reach for a familiar toy");
var crawler = game.Advance(almostCrawling.State, start.AddDays(9.8));
Equal(false, crawler.UnlockedSitting, "crawling does not re-award sitting");
Equal(true, crawler.UnlockedCrawling, "crawling always comes by 1.4x its nominal age");
Equal(true, crawler.State.CanCrawl, "crawling persists in simulation state");
Equal(Reaction.CrawlsToToy, game.ApplyCare(Calm(game, crawler.State), CareAction.Play).Reaction,
    "crawler moves toward a familiar toy");
Equal(Reaction.TracksToy, game.ApplyCare(Calm(game, crawler.State), CareAction.Play, Toy.Plush).Reaction,
    "crawler still tracks an unfamiliar toy");
Equal(false, game.Advance(crawler.State, start.AddDays(10.8)).UnlockedCrawling,
    "future time cannot re-award crawling");
var overnight = game.Advance(newborn, start.AddDays(9.8));
Equal(true, overnight.UnlockedSitting && overnight.UnlockedCrawling, "long absence can unlock both milestones");

const string oldCrawlerSave = "<baby version=\"3\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>6912000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach></baby>";
var crawlerSaves = new MemorySaveStore { Primary = oldCrawlerSave };
var crawlerSession = new BabySession(game, crawlerSaves, () => start);
Equal(3, crawlerSession.State.PlushFamiliarity, "v3 migration preserves plush history");
Equal((false, true, false), crawlerSession.Refresh(), "migration unlocks already earned crawling at same instant");
Equal(41.0, crawlerSession.State.Satiety, "migration at same instant does not decay satiety");
Equal(63.0, crawlerSession.State.Energy, "migration at same instant does not decay energy");
Equal(1, crawlerSaves.Writes, "migrated milestone persists once");
Equal(true, crawlerSaves.Primary.Contains("version=\"16\""), "crawling state writes the current schema");
var persistedCrawler = new BabySession(game, crawlerSaves, () => start);
Equal(true, persistedCrawler.State.CanCrawl, "v4 restores crawling");
Equal((false, false, false), persistedCrawler.Refresh(), "v4 refresh cannot re-award crawling");
Equal(Reaction.CrawlsToToy, persistedCrawler.ApplyCare(CareAction.Play, Toy.Plush).Reaction,
    "v4 restarts with familiar toy crawl behavior");
var previewCrawler = new BabySession(game, new MemorySaveStore(), () => start);
previewCrawler.Adopt("Mia");
Equal((true, true, false), previewCrawler.AdvancePreview(TimeSpan.FromDays(9.8)), "preview forwards both unlocks");
Equal((false, false, false), previewCrawler.AdvancePreview(TimeSpan.FromDays(1)), "preview does not re-award milestones");

var careClock = start;
var crawlingCare = new BabySession(game, new MemorySaveStore(), () => careClock);
crawlingCare.Adopt("Mia");
crawlingCare.ApplyCare(CareAction.Play, Toy.Plush);
careClock = start.AddDays(9.8);
Equal((Reaction.CrawlsToToy, true, true, false), crawlingCare.ApplyCare(CareAction.Play, Toy.Plush),
    "care forwards new milestones and uses the newly unlocked ability");
Equal((Reaction.CrawlsToToy, false, false, false), crawlingCare.ApplyCare(CareAction.Play, Toy.Plush),
    "same-time care does not re-award milestones");

Equal(0.0, newborn.Bond, "newborn starts building connection");
Equal(3.0, game.ApplyCare(newborn, CareAction.Soothe).State.Bond, "soothing builds lasting connection");
Equal(1.0, played.State.Bond, "playing builds lasting connection");
var story = game.ApplyCare(newborn, CareAction.ReadStory);
Equal(Reaction.EnjoysStory, story.Reaction, "reading has its own reaction");
Equal(1, story.State.StorySessions, "reading records a story session");
Equal(2.0, story.State.Bond, "reading builds connection");
Equal(74.0, story.State.Energy, "reading uses a little energy");
Equal(newborn.Age, story.State.Age, "reading does not advance age");
Equal(0.0, game.ApplyCare(tired, CareAction.ReadStory).State.Energy, "reading cannot reduce energy below zero");
var longAbsence = game.Advance(story.State, start.AddDays(10));
Equal(1.2, longAbsence.State.Bond, "missed meals cost Bond, never below 60% of its high-water mark");
Equal(1, longAbsence.State.StorySessions, "story history survives elapsed time");
var bedtime = story.State;
for (var i = 0; i < 60; i++)
    bedtime = game.ApplyCare(bedtime, CareAction.ReadStory).State;
Equal(12.0, bedtime.Bond, "repeated reading in one day stops at the daily Bond cap of 12");
Equal(61, bedtime.StorySessions, "reading still records sessions when Bond is capped");
var manyStories = BabyState.Restore("Mia", start, TimeSpan.Zero, 70, 75, 80, 0, 0, false, false, 0, int.MaxValue, 100, 100, false);
Equal(int.MaxValue, game.ApplyCare(manyStories, CareAction.ReadStory).State.StorySessions,
    "reading cannot overflow the story count");

const string preJournalSave = "<baby version=\"4\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>6912000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach><can-crawl>true</can-crawl></baby>";
var journalSaves = new MemorySaveStore { Primary = preJournalSave };
var journal = new BabySession(game, journalSaves, () => start);
Equal(true, journal.State.CanCrawl, "v4 journal migration preserves crawling");
Equal(3, journal.State.PlushFamiliarity, "v4 journal migration preserves toy history");
Equal(0.0, journal.State.Bond, "v4 starts connection at baseline");
Equal(0, journal.State.StorySessions, "v4 has no story history");
journal.ApplyCare(CareAction.ReadStory);
journal.ApplyCare(CareAction.Soothe);
var resumedJournal = new BabySession(game, journalSaves, () => start);
Equal(5.0, resumedJournal.State.Bond, "v5 resumes lasting connection");
Equal(1, resumedJournal.State.StorySessions, "v5 resumes story session count");
Equal(true, resumedJournal.State.CanCrawl, "v5 retains reached milestones");

Equal(100.0, newborn.Health, "newborn starts healthy");
Equal(100.0, newborn.Cleanliness, "newborn starts clean");
Equal(64.0, resume.State.Cleanliness, "offline diaper decay respects the shared cap");
Equal(100.0, resume.State.Health, "one capped gap with no sustained low needs preserves health");
var fresh = game.ApplyCare(resume.State, CareAction.ChangeDiaper);
Equal(Reaction.FreshDiaper, fresh.Reaction, "diaper change has its own reaction");
Equal(100.0, fresh.State.Cleanliness, "diaper change restores cleanliness with a cap");
Equal(resume.State.Health, fresh.State.Health, "diaper change does not instantly change health");

var lowNeeds = BabyState.Restore("Mia", start, TimeSpan.Zero, 65, 100, 100, 0, 0, false, false, 0, 0, 100, 60, false);
var singleTick = game.Advance(lowNeeds, start.AddHours(4)).State;
var firstTick = game.Advance(lowNeeds, start.AddHours(1.25)).State;
var splitTicks = game.Advance(firstTick, start.AddHours(4)).State;
// Midnight to 04:00 is quiet hours: Fullness falls 2.5/h and reaches 60 after two hours of recovery.
Equal(72.0, singleTick.Health, "health integrates only time within recovery and poor need thresholds");
Equal(singleTick.Health, splitTicks.Health, "splitting capped elapsed time leaves health identical");
Equal(singleTick.Cleanliness, splitTicks.Cleanliness, "splitting capped elapsed time leaves cleanliness identical");
var cappedOffline = game.Advance(lowNeeds, start.AddDays(30)).State;
Equal(singleTick.Health, cappedOffline.Health, "health calculation caps a long gap just like other needs");

var dirty = BabyState.Restore("Mia", start, TimeSpan.Zero, 100, 100, 100, 0, 0, false, false, 0, 0, 28, 70, false);
Equal(65.0, game.Advance(dirty, start.AddHours(1)).State.Health, "persistent low cleanliness can drain health");
var caredForDirty = game.ApplyCare(dirty, CareAction.ChangeDiaper).State;
Equal(100.0, caredForDirty.Cleanliness, "a dirty diaper can be addressed through care");
Equal(76.0, game.Advance(caredForDirty, start.AddHours(1)).State.Health,
    "health recovers when all needs are met after a diaper change");

var exhausted = BabyState.Restore("Mia", start, TimeSpan.Zero, 0, 0, 0, 0, 0, false, false, 0, 0, 0, 3, false);
exhausted = game.Advance(exhausted, start.AddHours(4)).State;
Equal(3.0, exhausted.Health, "a Health saved below the floor never falls further");
var worn = BabyState.Restore("Mia", start, TimeSpan.Zero, 0, 0, 0, 0, 0, false, false, 0, 0, 0, 20, false);
Equal(CareConsequences.HealthFloor, game.Advance(worn, start.AddHours(4)).State.Health, "Health never falls below 10");
Equal(0.0, exhausted.Cleanliness, "cleanliness cannot fall below zero");
for (var i = 0; i < 4; i++)
{
    exhausted = game.ApplyCare(exhausted, CareAction.Feed).State;
    exhausted = game.ApplyCare(exhausted, CareAction.Soothe).State;
    exhausted = game.ApplyCare(exhausted, CareAction.Rest).State;
}
exhausted = game.ApplyCare(exhausted, CareAction.ChangeDiaper).State;
Equal(100.0, exhausted.Cleanliness, "one diaper change restores cleanliness from zero");
Equal(3.0 + 6, game.Advance(exhausted, start.AddHours(5)).State.Health, "health can recover from its lowest point through care");

const string preHealthSave = "<baby version=\"5\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>6912000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach><can-crawl>true</can-crawl>" +
    "<connection>34</connection><story-sessions>8</story-sessions></baby>";
var healthClock = start;
var healthSaves = new MemorySaveStore { Primary = preHealthSave };
var healthSession = new BabySession(game, healthSaves, () => healthClock);
Equal(100.0, healthSession.State.Cleanliness, "v5 migration starts with full cleanliness");
Equal(100.0, healthSession.State.Health, "v5 migration starts with full health");
Equal(34.0, healthSession.State.Bond, "v5 migration preserves connection");
Equal(8, healthSession.State.StorySessions, "v5 migration preserves story history");
healthClock = start.AddHours(3);
healthSession.Refresh();
Equal(true, healthSaves.Primary.Contains("version=\"16\""), "health save uses the current schema");
var resumedHealth = new BabySession(game, healthSaves, () => healthClock);
Equal(healthSession.State.Health, resumedHealth.State.Health, "v6 restores health");
Equal(healthSession.State.Cleanliness, resumedHealth.State.Cleanliness, "v6 restores cleanliness");
Equal(Reaction.FreshDiaper, resumedHealth.ApplyCare(CareAction.ChangeDiaper).Reaction,
    "resumed game can change the diaper");

Equal(85.0, fed.State.Energy, "default milk feed improves energy");
Equal(fed.State.Energy, game.ApplyCare(newborn, CareAction.Feed, food: Food.Milk).State.Energy,
    "default feed remains equivalent to explicit milk");
Equal(100.0, game.ApplyCare(rested.State, CareAction.Feed).State.Energy, "milk caps energy at 100");
Equal(true, BabySimulation.CanFeed(newborn, Food.Milk), "newborn can drink milk");
Equal(false, BabySimulation.CanFeed(newborn, Food.Banana), "banana is unavailable to newborns");
Equal(false, BabySimulation.CanFeed(threshold.State, Food.Oats), "oats are unavailable to sitters");
Equal(true, BabySimulation.CanFeed(crawler.State, Food.Banana), "crawlers can eat banana");
Equal(true, BabySimulation.CanFeed(crawler.State, Food.Oats), "crawlers can eat oats");
Equal(false, BabySimulation.CanFeed(crawler.State, (Food)99), "unknown food is unavailable");

var hungryCrawler = overnight.State;
var banana = game.ApplyCare(hungryCrawler, CareAction.Feed, food: Food.Banana);
Equal(hungryCrawler.Satiety + 28, banana.State.Satiety, "banana improves satiety");
Equal(hungryCrawler.Comfort + 8, banana.State.Comfort, "banana improves comfort");
Equal(hungryCrawler.Energy, banana.State.Energy, "banana leaves energy unchanged");
Equal(Reaction.Satisfied, banana.Reaction, "banana uses the feeding reaction");
var oats = game.ApplyCare(hungryCrawler, CareAction.Feed, food: Food.Oats);
Equal(hungryCrawler.Satiety + 40, oats.State.Satiety, "oats give the largest satiety boost");
Equal(hungryCrawler.Comfort, oats.State.Comfort, "oats leave comfort unchanged");

var mealtimeClock = start;
var mealtimeSaves = new MemorySaveStore();
var mealtime = new BabySession(game, mealtimeSaves, () => mealtimeClock);
mealtime.Adopt("Mia");
var beforeMeal = mealtime.State;
var beforeMealSave = mealtimeSaves.Primary;
mealtimeClock = start.AddDays(3);
ExpectUnavailable(() => mealtime.ApplyCare(CareAction.Feed, food: Food.Banana));
Equal(beforeMealSave, mealtimeSaves.Primary, "locked food cannot change the save");
Equal(beforeMeal, mealtime.State, "locked food cannot advance elapsed state");
Equal(1, mealtimeSaves.Writes, "locked food commits nothing");
mealtimeClock = start.AddDays(9.8);
ExpectUnavailable(() => mealtime.ApplyCare(CareAction.Feed, food: (Food)99));
Equal(beforeMeal, mealtime.State, "unknown food cannot consume pending milestones");
Equal(1, mealtimeSaves.Writes, "unknown food commits nothing");
Equal((Reaction.Satisfied, true, true, false), mealtime.ApplyCare(CareAction.Feed, food: Food.Oats),
    "food availability uses age at the same instant as feeding");
Equal(2, mealtimeSaves.Writes, "milestones and valid feeding commit once together");

Equal(false, newborn.HasPendingNewSound, "incident does not appear before sitting");
Equal(true, threshold.State.HasPendingNewSound, "sitting makes the incident available");
Equal(true, crawler.State.HasPendingNewSound, "incident remains available until addressed");

var reassured = game.ApplyCare(threshold.State, CareAction.ComfortNewSound);
Equal(Reaction.NewSoundComforted, reassured.Reaction, "comfort has its own incident reaction");
Equal(threshold.State.Comfort + 16, reassured.State.Comfort, "comfort choice soothes the baby");
// Gains count double back up to the high-water mark (threshold lost a little Bond to missed meals).
double Regained(BabyState before, double raw) =>
    before.Bond + Math.Min(raw * 2, before.Bonding.HighWater - before.Bond) +
    Math.Max(0, raw - (before.Bonding.HighWater - before.Bond) / 2);
Equal(Regained(threshold.State, 2), reassured.State.Bond, "comfort choice builds Bond");
Equal(threshold.State.Energy, reassured.State.Energy, "comfort choice leaves energy unchanged");
Equal(true, reassured.State.NewSoundResolved, "comfort resolves the incident");
Equal(false, reassured.State.HasPendingNewSound, "resolved incident is no longer pending");

var explored = game.ApplyCare(threshold.State, CareAction.ExploreNewSound);
Equal(Reaction.NewSoundExplored, explored.Reaction, "exploration has its own incident reaction");
Equal(Regained(threshold.State, 5), explored.State.Bond, "exploration builds more Bond");
Equal(threshold.State.Energy - 6, explored.State.Energy, "exploration uses energy");
Equal(threshold.State.Comfort, explored.State.Comfort, "exploration leaves comfort unchanged");
Equal(true, explored.State.NewSoundResolved, "exploration resolves the incident");

var incidentClock = start;
var incidentSaves = new MemorySaveStore();
var incidentSession = new BabySession(game, incidentSaves, () => incidentClock);
incidentSession.Adopt("Mia");
var beforeIncident = incidentSession.State;
incidentClock = start.AddDays(1);
ExpectUnavailable(() => incidentSession.ApplyCare(CareAction.ComfortNewSound));
Equal(beforeIncident, incidentSession.State, "too-early choice cannot advance elapsed time");
Equal(1, incidentSaves.Writes, "too-early choice commits nothing");

incidentClock = start.AddDays(4.2);
incidentSaves.FailWrites = true;
try
{
    incidentSession.ApplyCare(CareAction.ExploreNewSound);
    throw new InvalidOperationException("Failed incident write should be surfaced.");
}
catch (IOException)
{
    Equal(beforeIncident, incidentSession.State, "failed incident write leaves all state unchanged");
}
incidentSaves.FailWrites = false;
Equal((Reaction.NewSoundExplored, true, false, false), incidentSession.ApplyCare(CareAction.ExploreNewSound),
    "successful retry commits the pending milestone and choice together");
var incidentReload = new BabySession(game, incidentSaves, () => incidentClock);
Equal(true, incidentReload.State.NewSoundResolved, "incident resolution survives save and reload");
var resolvedPayload = incidentSaves.Primary;
incidentClock = start.AddDays(5);
ExpectUnavailable(() => incidentReload.ApplyCare(CareAction.ComfortNewSound));
ExpectUnavailable(() => incidentReload.ApplyCare(CareAction.ExploreNewSound));
Equal(resolvedPayload, incidentSaves.Primary, "duplicate choices cannot change the save");
Equal(start.AddDays(4.2), incidentReload.State.TimeCheckpoint, "duplicate choices cannot advance the checkpoint");

const string preIncidentSave = "<baby version=\"6\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>6912000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach><can-crawl>true</can-crawl>" +
    "<connection>34</connection><story-sessions>8</story-sessions><cleanliness>51</cleanliness><health>74</health></baby>";
var migratedIncident = new BabySession(game, new MemorySaveStore { Primary = preIncidentSave }, () => start);
Equal(true, migratedIncident.State.HasPendingNewSound, "a sitter in a v6 save can address the new incident");
Equal(74.0, migratedIncident.State.Health, "v6 incident migration preserves unrelated progress");

Equal(true, BAIby.Presentation.WipeGeometry.TouchesSpot(10, 10, 10, 10, 10, 10, 8),
    "a tap cleans the spot directly under it");
Equal(false, BAIby.Presentation.WipeGeometry.TouchesSpot(10, 10, 10, 10, 19, 10, 8),
    "a tap outside a spot does not clean it");
Equal(true, BAIby.Presentation.WipeGeometry.TouchesSpot(-100, 3, 100, 3, 0, 0, 3),
    "a fast swipe clears a spot between pointer samples, including a tangent");
Equal(true, BAIby.Presentation.WipeGeometry.TouchesSpot(100, 3, -100, 3, 0, 0, 3),
    "swiping in the opposite direction has the same coverage");
Equal(false, BAIby.Presentation.WipeGeometry.TouchesSpot(-100, 0, -10, 0, 0, 0, 3),
    "a swipe's infinite extension cannot clear a spot past its endpoint");
Equal(false, BAIby.Presentation.WipeGeometry.TouchesSpot(-100, 12, 100, 12, 0, 0, 3),
    "a swipe that passes nearby does not clear a spot");

Equal(NurseryTheme.Sage, newborn.NurseryTheme, "new adoption starts in the sage nursery");
const string preThemeSave = "<baby version=\"7\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>6912000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach><can-crawl>true</can-crawl>" +
    "<connection>34</connection><story-sessions>8</story-sessions><cleanliness>51</cleanliness><health>74</health>" +
    "<new-sound-resolved>true</new-sound-resolved></baby>";
var themeSaves = new MemorySaveStore { Primary = preThemeSave };
var themeClock = start.AddHours(1);
var themeSession = new BabySession(game, themeSaves, () => themeClock);
Equal(NurseryTheme.Sage, themeSession.State.NurseryTheme, "v7 saves start in the sage nursery");
themeSession.SetNurseryTheme(NurseryTheme.Sage);
Equal(0, themeSaves.Writes, "selecting the same theme skips writes");
try
{
    themeSession.SetNurseryTheme((NurseryTheme)99);
    throw new InvalidOperationException("Unknown nursery themes should be rejected.");
}
catch (ArgumentOutOfRangeException)
{
    Equal(0, themeSaves.Writes, "unknown theme cannot alter the save");
}
themeSession.SetNurseryTheme(NurseryTheme.Peach);
Equal(1, themeSaves.Writes, "new theme saves immediately");
Equal(NurseryTheme.Peach, themeSession.State.NurseryTheme, "new theme applies immediately");
var expectedTheme = XElement.Parse(preThemeSave);
expectedTheme.SetAttributeValue("version", 16);
expectedTheme.Add(new XElement("nursery-theme", 1), new XElement("can-walk", false), new XElement("block-towers", 0),
    DefaultAppearanceElement(), UnanchoredCareElement());
Equal(true, XNode.DeepEquals(expectedTheme, XElement.Parse(themeSaves.Primary)),
    "theme change preserves all existing save fields despite elapsed time");
var themedReload = new BabySession(game, themeSaves, () => themeClock);
Equal(NurseryTheme.Peach, themedReload.State.NurseryTheme, "theme survives save reload");
themedReload.Refresh();
Equal(NurseryTheme.Peach, themedReload.State.NurseryTheme, "elapsed time preserves theme");
themedReload.AdvancePreview(TimeSpan.FromDays(1));
Equal(NurseryTheme.Peach, themedReload.State.NurseryTheme, "preview time preserves theme");
themedReload.ApplyCare(CareAction.Feed);
Equal(NurseryTheme.Peach, themedReload.State.NurseryTheme, "care preserves theme");
var themeStateBeforeFailure = themedReload.State;
themeSaves.FailWrites = true;
try
{
    themedReload.SetNurseryTheme(NurseryTheme.Sage);
    throw new InvalidOperationException("Failed theme write should be surfaced.");
}
catch (IOException)
{
    Equal(themeStateBeforeFailure, themedReload.State, "failed theme write leaves visible state unchanged");
}

var bathReady = BabyState.Restore("Mia", start, TimeSpan.FromDays(3), 71, 47, 53,
    2, 3, true, false, 42, 8, 23, 89, true, NurseryTheme.Peach);
var bath = game.ApplyCare(bathReady, CareAction.Bath);
Equal(Reaction.FreshBath, bath.Reaction, "bath produces a distinct reaction");
Equal(68.0, bath.State.Cleanliness, "bath improves cleanliness");
Equal(77.0, bath.State.Comfort, "bath improves comfort");
Equal(45.0, bath.State.Energy, "bath uses energy");
var expectedBath = XElement.Parse(BabySaveCodec.Encode(bathReady));
expectedBath.SetElementValue("cleanliness", 68);
expectedBath.SetElementValue("comfort", 77);
expectedBath.SetElementValue("energy", 45);
// The shadow care ledger records the bath; CareChecks covers it.
expectedBath.Element("care").ReplaceWith(CareRecordCodec.Encode(bath.State.Care));
// Care also feeds development (engagement and a fresh quality sample); GrowthChecks covers it.
expectedBath.Add(DevelopmentRecordCodec.Encode(bath.State.Development));
// Bond, temperament, the rhythm and the journal ride along (BondChecks covers them).
expectedBath.Add(BondRecordCodec.Encode(bath.State.Bonding), JournalRecordCodec.Encode(bath.State.Journal));
Equal(true, XNode.DeepEquals(expectedBath, XElement.Parse(BabySaveCodec.Encode(bath.State))),
    "bath leaves all other saved fields unchanged");
var changedInstead = game.ApplyCare(bathReady, CareAction.ChangeDiaper).State;
Equal(100.0, changedInstead.Cleanliness, "diaper still cleans fully");
Equal(bathReady.Comfort, changedInstead.Comfort, "diaper does not soothe");
var soothedInstead = game.ApplyCare(bathReady, CareAction.Soothe).State;
Equal(67.0, soothedInstead.Comfort, "soothing retains its own comfort effect");
Equal(bathReady.Cleanliness, soothedInstead.Cleanliness, "soothing does not clean");
Equal(45.0, soothedInstead.Bond, "soothing retains its connection effect");
var cappedBath = game.ApplyCare(bath.State, CareAction.Bath).State;
Equal(100.0, cappedBath.Cleanliness, "bath caps cleanliness");
Equal(100.0, cappedBath.Comfort, "bath caps comfort");
Equal(0.0, game.ApplyCare(tired, CareAction.Bath).State.Energy, "bath cannot put energy below zero");
Equal(Reaction.FreshBath, game.ApplyCare(newborn, CareAction.Bath).Reaction, "bath is available to newborns");

var bathClock = start;
var bathSaves = new MemorySaveStore();
var bathSession = new BabySession(game, bathSaves, () => bathClock);
ExpectUnavailable(() => bathSession.ApplyCare(CareAction.Bath));
Equal(0, bathSaves.Writes, "bath requires an adopted baby");
bathSession.Adopt("Mia");
bathClock = start.AddDays(4.2);
var beforeBathState = bathSession.State;
var beforeBathPayload = bathSaves.Primary;
bathSaves.FailWrites = true;
try
{
    bathSession.ApplyCare(CareAction.Bath);
    throw new InvalidOperationException("Failed bath write should be surfaced.");
}
catch (IOException)
{
    Equal(beforeBathState, bathSession.State, "failed bath leaves age, milestones, and needs unchanged");
    Equal(beforeBathPayload, bathSaves.Primary, "failed bath cannot change the save");
}
bathSaves.FailWrites = false;
Equal((Reaction.FreshBath, true, false, false), bathSession.ApplyCare(CareAction.Bath),
    "retry commits the bath and elapsed milestone together");
Equal(2, bathSaves.Writes, "successful bath commits once after adoption");
// The capped gap is four quiet hours (Cleanliness 80, Energy held at 80), then the bath.
var reloadedBath = new BabySession(game, bathSaves, () => bathClock).State;
Equal(100.0, reloadedBath.Cleanliness, "bath effects persist through the existing save schema");
Equal(72.0, reloadedBath.Energy, "the bath's energy cost persists");

var almostWalking = game.Advance(crawler.State, start.AddDays(10));
Equal(false, almostWalking.UnlockedWalking, "a neglected crawler takes its time with walking");
Equal(Reaction.CrawlsToToy, game.ApplyCare(Calm(game, almostWalking.State), CareAction.Play).Reaction,
    "a crawler still crawls toward a familiar toy before walking");
var walker = game.Advance(almostWalking.State, start.AddDays(16.8));
Equal(true, walker.UnlockedWalking, "walking always comes by 1.4x its nominal age");
Equal(true, walker.State.CanWalk, "walking ability persists in simulation state");
Equal(false, walker.UnlockedCrawling, "walking cannot re-award crawling");
Equal(Reaction.WalksToToy, game.ApplyCare(Calm(game, walker.State), CareAction.Play).Reaction,
    "a walker takes steps toward a familiar toy");
Equal(Reaction.TracksToy, game.ApplyCare(Calm(game, walker.State), CareAction.Play, Toy.Plush).Reaction,
    "walking does not change the response to unfamiliar toys");
Equal(false, game.Advance(walker.State, start.AddDays(17.8)).UnlockedWalking,
    "future time does not re-award walking");

var walkingClock = start;
var walkingSaves = new MemorySaveStore();
var walkingSession = new BabySession(game, walkingSaves, () => walkingClock);
walkingSession.Adopt("Mia");
walkingSession.ApplyCare(CareAction.Play, Toy.Plush);
walkingClock = start.AddDays(16.8);
Equal((Reaction.WalksToToy, true, true, true), walkingSession.ApplyCare(CareAction.Play, Toy.Plush),
    "care forwards all crossed milestones and uses the newly unlocked ability");
Equal(3, walkingSaves.Writes, "care commits the new milestones together");
var walkingPreview = new BabySession(game, new MemorySaveStore(), () => start);
walkingPreview.Adopt("Mia");
Equal((true, true, true), walkingPreview.AdvancePreview(TimeSpan.FromDays(16.8)),
    "preview forwards all crossed milestones");
Equal((false, false, false), walkingPreview.AdvancePreview(TimeSpan.FromDays(1)),
    "preview never re-awards reported milestones");

const string preWalkingSave = "<baby version=\"8\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>11232000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach><can-crawl>true</can-crawl>" +
    "<connection>34</connection><story-sessions>8</story-sessions><cleanliness>51</cleanliness><health>74</health>" +
    "<new-sound-resolved>true</new-sound-resolved><nursery-theme>1</nursery-theme></baby>";
var walkingMigrationSaves = new MemorySaveStore { Primary = preWalkingSave };
var migratedWalker = new BabySession(game, walkingMigrationSaves, () => start);
Equal(false, migratedWalker.State.CanWalk, "v8 has no stored walking flag");
Equal((false, false, true), migratedWalker.Refresh(), "an eligible v8 player unlocks walking without elapsed time");
var expectedWalker = XElement.Parse(preWalkingSave);
expectedWalker.SetAttributeValue("version", 16);
expectedWalker.Add(new XElement("can-walk", true), new XElement("block-towers", 0), DefaultAppearanceElement(),
    UnanchoredCareElement(), XElement.Parse(walkingMigrationSaves.Primary).Element("development"),
    XElement.Parse(walkingMigrationSaves.Primary).Element("bond"), XElement.Parse(walkingMigrationSaves.Primary).Element("journal"));
Equal(0.0, migratedWalker.State.Development.Readiness, "the walker's readiness is spent");
Equal(true, XNode.DeepEquals(expectedWalker, XElement.Parse(walkingMigrationSaves.Primary)),
    "same-time migration does not decay needs or alter any other existing fields");
var walkingReloaded = new BabySession(game, walkingMigrationSaves, () => start);
Equal(true, walkingReloaded.State.CanWalk, "current version restores walking");
Equal((false, false, false), walkingReloaded.Refresh(), "current version never re-awards walking after reload");
Equal(1, walkingMigrationSaves.Writes, "same-time refresh commits the new milestone only once");
walkingReloaded.SetNurseryTheme(NurseryTheme.Sage);
Equal(true, walkingReloaded.State.CanWalk, "cosmetics preserve walking");

Equal(0, newborn.BlockTowers, "newborn has no block towers");
ExpectUnavailable(() => game.ApplyCare(crawler.State, CareAction.StackBlocks));
var blockReady = BabyState.Restore("Mia", start, TimeSpan.FromDays(12), 71, 47, 53,
    2, 3, true, true, 42, 8, 23, 89, true, NurseryTheme.Peach, canWalk: true);
var stacked = game.ApplyCare(blockReady, CareAction.StackBlocks);
Equal(Reaction.StackedBlocks, stacked.Reaction, "stacking blocks uses a distinct reaction");
Equal(1, stacked.State.BlockTowers, "stacking records one tower");
Equal(45.0, stacked.State.Bond, "building together improves connection");
Equal(45.0, stacked.State.Energy, "building uses energy");
var expectedTower = XElement.Parse(BabySaveCodec.Encode(blockReady));
expectedTower.SetElementValue("block-towers", 1);
expectedTower.SetElementValue("connection", 45);
expectedTower.SetElementValue("energy", 45);
expectedTower.Element("care").ReplaceWith(CareRecordCodec.Encode(stacked.State.Care));
expectedTower.Add(DevelopmentRecordCodec.Encode(stacked.State.Development));
// Bond, temperament, the rhythm and the journal ride along (BondChecks covers them).
expectedTower.Add(BondRecordCodec.Encode(stacked.State.Bonding), JournalRecordCodec.Encode(stacked.State.Journal));
Equal(true, XNode.DeepEquals(expectedTower, XElement.Parse(BabySaveCodec.Encode(stacked.State))),
    "stacking preserves all unrelated fields");

var limitsForBlocks = BabyState.Restore("Mia", start, TimeSpan.FromDays(12), 71, 47, 4,
    2, 3, true, true, 99, 8, 23, 89, true, NurseryTheme.Peach, canWalk: true, blockTowers: int.MaxValue);
var clampedBlocks = game.ApplyCare(limitsForBlocks, CareAction.StackBlocks).State;
Equal(100.0, clampedBlocks.Bond, "stacking caps connection");
Equal(0.0, clampedBlocks.Energy, "stacking cannot make energy negative");
Equal(int.MaxValue, clampedBlocks.BlockTowers, "tower count cannot overflow");

var blockClock = start;
var blocksSaves = new MemorySaveStore();
var blocksSession = new BabySession(game, blocksSaves, () => blockClock);
blocksSession.Adopt("Mia");
var beforeBlocks = blocksSession.State;
var beforeBlocksPayload = blocksSaves.Primary;
blockClock = start.AddDays(9);
ExpectUnavailable(() => blocksSession.ApplyCare(CareAction.StackBlocks));
Equal(1, blocksSaves.Writes, "a locked attempt writes nothing");
Equal(beforeBlocks, blocksSession.State, "a locked attempt leaves pending milestones and elapsed time unchanged");
Equal(beforeBlocksPayload, blocksSaves.Primary, "a locked attempt leaves the persisted state unchanged");
blockClock = start.AddDays(16.8);
blocksSaves.FailWrites = true;
try
{
    blocksSession.ApplyCare(CareAction.StackBlocks);
    throw new InvalidOperationException("Failed tower write should be surfaced.");
}
catch (IOException)
{
    Equal(beforeBlocks, blocksSession.State, "failed tower write leaves visible state unchanged");
    Equal(beforeBlocksPayload, blocksSaves.Primary, "failed tower write leaves persisted state unchanged");
}
blocksSaves.FailWrites = false;
Equal((Reaction.StackedBlocks, true, true, true), blocksSession.ApplyCare(CareAction.StackBlocks),
    "stack at walking threshold commits the tower and elapsed milestones together");
Equal(2, blocksSaves.Writes, "successful stack commits once");
var resumedBlocks = new BabySession(game, blocksSaves, () => blockClock);
Equal(1, resumedBlocks.State.BlockTowers, "current save restores towers");
resumedBlocks.AdvancePreview(TimeSpan.FromDays(1));
resumedBlocks.ApplyCare(CareAction.Soothe);
resumedBlocks.SetNurseryTheme(NurseryTheme.Peach);
Equal(1, resumedBlocks.State.BlockTowers, "preview time, care, and cosmetics all preserve towers");

var preBlocks = XElement.Parse(preWalkingSave);
preBlocks.SetAttributeValue("version", 9);
preBlocks.Add(new XElement("can-walk", true));
var olderBlocksSaves = new MemorySaveStore { Primary = preBlocks.ToString(SaveOptions.DisableFormatting) };
var migratedBlocks = new BabySession(game, olderBlocksSaves, () => start);
Equal(true, migratedBlocks.State.CanWalk, "v9 migration keeps walking unlocked");
Equal(0, migratedBlocks.State.BlockTowers, "v9 starts with zero block towers");
migratedBlocks.ApplyCare(CareAction.StackBlocks);
Equal(true, olderBlocksSaves.Primary.Contains("version=\"16\""), "stacking migrates v9 to the current schema");
Equal(1, new BabySession(game, olderBlocksSaves, () => start).State.BlockTowers, "migrated v11 restores the first tower");

var newbornReport = BabyReport.From(newborn);
Equal("Mia", newbornReport.Name, "report shows adopted name");
Equal(TimeSpan.Zero, newbornReport.Age, "report reflects current game age");
Equal(false, newbornReport.CanSitAndReach || newbornReport.CanCrawl || newbornReport.CanWalk,
    "newborn report lists no milestones as unlocked");
Equal<Toy?>(null, newbornReport.FavoriteToy, "report has no favorite before play");
Equal(0.0, newbornReport.Bond, "report shows actual initial connection");
Equal(false, newbornReport.EnjoysStories, "report has no story interest without sessions");
Equal(false, newbornReport.EnjoysBlocks, "report has no block interest without towers");
Equal<Toy?>(null, BabyReport.From(played.State).FavoriteToy, "one toy play does not establish a favorite");
Equal(false, BabyReport.From(story.State).EnjoysStories, "one story is not an observed interest");

var sittingReport = BabyReport.From(threshold.State);
Equal(true, sittingReport.CanSitAndReach, "sitter report shows sitting");
Equal(false, sittingReport.CanCrawl || sittingReport.CanWalk, "sitter report leaves later milestones locked");
var crawlingReport = BabyReport.From(crawler.State);
Equal(true, crawlingReport.CanSitAndReach && crawlingReport.CanCrawl, "crawler report shows both earlier milestones");
Equal(false, crawlingReport.CanWalk, "crawler report leaves walking locked");

var reportSaves = new MemorySaveStore();
var reportSession = new BabySession(game, reportSaves, () => start);
reportSession.Adopt("Mia");
reportSession.AdvancePreview(TimeSpan.FromDays(16.8));
reportSession.ApplyCare(CareAction.ReadStory);
reportSession.ApplyCare(CareAction.Play, Toy.Rattle);
reportSession.ApplyCare(CareAction.Play, Toy.Rattle);
reportSession.ApplyCare(CareAction.Play, Toy.Plush);
reportSession.ApplyCare(CareAction.Play, Toy.Plush);
reportSession.ApplyCare(CareAction.StackBlocks);

var oneOfEach = BabyReport.From(reportSession.State);
Equal(1, oneOfEach.StorySessions, "report shows actual story count");
Equal(1, oneOfEach.BlockTowers, "report shows actual tower count");
Equal(false, oneOfEach.EnjoysStories || oneOfEach.EnjoysBlocks, "one action leaves both interests tentative");
Equal<Toy?>(null, oneOfEach.FavoriteToy, "tied repeated toy histories have no favorite");

reportSession.ApplyCare(CareAction.ReadStory);
reportSession.ApplyCare(CareAction.StackBlocks);
reportSession.ApplyCare(CareAction.Play, Toy.Plush);
var reportReloaded = new BabySession(game, reportSaves, () => start);
var reportState = reportReloaded.State;
var reportPayload = reportSaves.Primary;
var reportWrites = reportSaves.Writes;
var developedReport = BabyReport.From(reportState);
Equal(true, developedReport.CanSitAndReach && developedReport.CanCrawl && developedReport.CanWalk,
    "report shows all saved milestones");
Equal(TimeSpan.FromDays(16.8), developedReport.Age, "report reads elapsed game age from saved state");
Equal<Toy?>(Toy.Plush, developedReport.FavoriteToy, "report reflects favorite from saved history");
Equal(true, developedReport.EnjoysStories && developedReport.EnjoysBlocks,
    "two stories or towers establish the corresponding interest");
Equal(12.0, developedReport.Bond, "report shows the saved Bond (a day of care stops at the daily cap)");
Equal(reportState, reportReloaded.State, "creating a report leaves session state untouched");
Equal(reportPayload, reportSaves.Primary, "creating a report leaves the save unchanged");
Equal(reportWrites, reportSaves.Writes, "creating a report performs no save writes");

Equal(BabyAppearance.Default, newborn.Appearance, "new adoption starts with the default look");
Equal(BabyAppearance.Default, BabyState.Restore("Mia", start, TimeSpan.Zero, 70, 75, 80, 0, 0, false, false, 0, 0, 100, 100, false).Appearance,
    "restoring without a look uses the default look");
var customLook = new BabyAppearance(SkinTone.Deep, HairColor.Platinum, HairStyle.Coils, Outfit.Midnight, Headwear.BearBeanie);
Equal(false, customLook.Equals(BabyAppearance.Default), "custom look differs from the default");
Equal(newborn, newborn.WithAppearance(BabyAppearance.Default), "applying the same look returns the same state");
try
{
    newborn.WithAppearance(null);
    throw new InvalidOperationException("A missing look should be rejected.");
}
catch (ArgumentNullException)
{
}

var lookSaves = new MemorySaveStore();
var lookClock = start;
var lookSession = new BabySession(game, lookSaves, () => lookClock);
try
{
    lookSession.SetAppearance(customLook, unlockEarned: true);
    throw new InvalidOperationException("Styling requires an adopted baby.");
}
catch (InvalidOperationException exception) when (exception.Message == "Adopt a baby first.")
{
    Equal(0, lookSaves.Writes, "styling before adoption writes nothing");
}
lookSession.Adopt("Mia");
Equal(BabyAppearance.Default, lookSession.State.Appearance, "session adoption starts with the default look");
Equal(true, XNode.DeepEquals(DefaultAppearanceElement(), XElement.Parse(lookSaves.Primary).Element("appearance")),
    "adoption saves the default look in v12");
var adoptedLookPayload = lookSaves.Primary;
lookSession.SetAppearance(BabyAppearance.Default);
Equal(1, lookSaves.Writes, "selecting the current look skips writes");
lookSession.SetAppearance(new BabyAppearance(SkinTone.Warm, HairColor.Chestnut, HairStyle.Curls, Outfit.Sage, Headwear.None));
Equal(1, lookSaves.Writes, "an equal look instance skips writes");
try
{
    lookSession.SetAppearance(null);
    throw new InvalidOperationException("A missing look should be rejected by the session.");
}
catch (ArgumentNullException)
{
    Equal(1, lookSaves.Writes, "a missing look cannot alter the save");
}
// The custom look wears the Midnight outfit, earned at a 7-day rhythm: developer mode unlocks it (BondChecks covers
// earning it).
var lookStateBeforeFailure = lookSession.State;
lookSaves.FailWrites = true;
try
{
    lookSession.SetAppearance(customLook, unlockEarned: true);
    throw new InvalidOperationException("Failed look write should be surfaced.");
}
catch (IOException)
{
    Equal(lookStateBeforeFailure, lookSession.State, "failed look write leaves visible state unchanged");
    Equal(adoptedLookPayload, lookSaves.Primary, "failed look write leaves persisted state unchanged");
}
lookSaves.FailWrites = false;
lookSession.SetAppearance(customLook, unlockEarned: true);
Equal(2, lookSaves.Writes, "a new look saves immediately");
Equal(customLook, lookSession.State.Appearance, "a new look applies immediately");
var expectedLook = XElement.Parse(adoptedLookPayload);
expectedLook.Element("appearance").ReplaceWith(new XElement("appearance",
    new XAttribute("skin", 5), new XAttribute("hair-color", 5), new XAttribute("hair-style", 1),
    new XAttribute("outfit", 7), new XAttribute("headwear", 2)));
Equal(true, XNode.DeepEquals(expectedLook, XElement.Parse(lookSaves.Primary)),
    "a look change saves stable IDs and preserves all other fields");
lookSession.SetAppearance(customLook, unlockEarned: true);
Equal(2, lookSaves.Writes, "reselecting the saved look skips writes");

var lookReload = new BabySession(game, lookSaves, () => lookClock);
Equal(customLook, lookReload.State.Appearance, "a custom look survives save reload");
Equal(customLook, BabySaveCodec.Decode(BabySaveCodec.Encode(lookReload.State)).Appearance, "codec round-trips a custom look");
foreach (var skin in Enum.GetValues<SkinTone>())
    foreach (var outfit in Enum.GetValues<Outfit>())
    {
        var look = new BabyAppearance(skin, (HairColor)((int)skin % 6), (HairStyle)((int)outfit % 4), outfit, (Headwear)((int)outfit % 5));
        Equal(look, BabySaveCodec.Decode(BabySaveCodec.Encode(newborn.WithAppearance(look))).Appearance,
            $"codec round-trips {skin} skin with the {outfit} outfit");
    }
foreach (var hairColor in Enum.GetValues<HairColor>())
    foreach (var headwear in Enum.GetValues<Headwear>())
    {
        var look = BabyAppearance.Default.WithHairColor(hairColor).WithHeadwear(headwear).WithHairStyle(HairStyle.Tuft);
        Equal(look, BabySaveCodec.Decode(BabySaveCodec.Encode(newborn.WithAppearance(look))).Appearance,
            $"codec round-trips {hairColor} hair with {headwear} headwear");
    }

lookClock = start.AddDays(4.2);
Equal((true, false, false), lookReload.Refresh(), "elapsed time unlocks sitting for a styled baby");
Equal(customLook, lookReload.State.Appearance, "aging preserves the look");
lookReload.AdvancePreview(TimeSpan.FromDays(1));
Equal(customLook, lookReload.State.Appearance, "preview day preserves the look");
lookReload.ApplyCare(CareAction.Feed);
lookReload.ApplyCare(CareAction.Play, Toy.Plush);
Equal(customLook, lookReload.State.Appearance, "session care preserves the look");
lookReload.SetNurseryTheme(NurseryTheme.Peach);
Equal(customLook, lookReload.State.Appearance, "changing the nursery theme preserves the look");
Equal(customLook, new BabySession(game, lookSaves, () => lookClock).State.Appearance,
    "the look persists after aging, preview, care and decor");

var styledToddler = BabyState.Restore("Mia", start, TimeSpan.FromDays(12), 71, 47, 53,
    2, 3, true, true, 42, 8, 23, 89, false, NurseryTheme.Peach, canWalk: true, blockTowers: 1, appearance: customLook);
Equal(customLook, styledToddler.Appearance, "restore keeps the given look");
foreach (var careAction in Enum.GetValues<CareAction>())
{
    var styledCare = game.ApplyCare(styledToddler, careAction);
    Equal(customLook, styledCare.State.Appearance, $"{careAction} preserves the look");
}
foreach (var food in Enum.GetValues<Food>())
    Equal(customLook, game.ApplyCare(styledToddler, CareAction.Feed, food: food).State.Appearance, $"feeding {food} preserves the look");
var styledNewborn = game.Adopt("Mia", start).WithAppearance(customLook);
var styledSitter = game.Advance(styledNewborn, start.AddDays(4.2));
Equal(customLook, styledSitter.State.Appearance, "reaching sitting preserves the look");
var styledWalker = game.AdvancePreview(styledSitter.State, TimeSpan.FromDays(12.6));
Equal((true, true), (styledWalker.UnlockedCrawling, styledWalker.UnlockedWalking), "preview crosses crawling and walking");
Equal(customLook, styledWalker.State.Appearance, "every growth stage keeps the look");
Equal(customLook, styledWalker.State.WithNurseryTheme(NurseryTheme.Peach).Appearance, "decor changes keep the look");
Equal(NurseryTheme.Peach, styledWalker.State.WithNurseryTheme(NurseryTheme.Peach).WithAppearance(BabyAppearance.Default).NurseryTheme,
    "look changes keep the nursery theme");
var restyled = styledToddler.WithAppearance(BabyAppearance.Default);
var expectedRestyle = XElement.Parse(BabySaveCodec.Encode(styledToddler));
expectedRestyle.Element("appearance").ReplaceWith(DefaultAppearanceElement());
Equal(true, XNode.DeepEquals(expectedRestyle, XElement.Parse(BabySaveCodec.Encode(restyled))),
    "a look change preserves all unrelated fields");

const string preAppearanceSave = "<baby version=\"10\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>11232000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach><can-crawl>true</can-crawl>" +
    "<connection>34</connection><story-sessions>8</story-sessions><cleanliness>51</cleanliness><health>74</health>" +
    "<new-sound-resolved>true</new-sound-resolved><nursery-theme>1</nursery-theme>" +
    "<can-walk>true</can-walk><block-towers>2</block-towers></baby>";
var preAppearanceSaves = new MemorySaveStore { Primary = preAppearanceSave };
var migratedLook = new BabySession(game, preAppearanceSaves, () => start);
Equal(BabyAppearance.Default, migratedLook.State.Appearance, "v10 saves start with the default look");
Equal(NurseryTheme.Peach, migratedLook.State.NurseryTheme, "v10 migration keeps the nursery theme");
Equal(2, migratedLook.State.BlockTowers, "v10 migration keeps block towers");
Equal(true, migratedLook.State.CanWalk, "v10 migration keeps walking");
Equal((false, false, false), migratedLook.Refresh(), "same-time v10 reopen does not unlock or write");
Equal(0, preAppearanceSaves.Writes, "loading a v10 save writes nothing");
migratedLook.SetAppearance(BabyAppearance.Default);
Equal(0, preAppearanceSaves.Writes, "keeping the default look on a v10 save writes nothing");
migratedLook.SetAppearance(customLook, unlockEarned: true);
var expectedMigratedLook = XElement.Parse(preAppearanceSave);
expectedMigratedLook.SetAttributeValue("version", 16);
expectedMigratedLook.Add(new XElement("appearance",
    new XAttribute("skin", 5), new XAttribute("hair-color", 5), new XAttribute("hair-style", 1),
    new XAttribute("outfit", 7), new XAttribute("headwear", 2)), UnanchoredCareElement());
Equal(true, XNode.DeepEquals(expectedMigratedLook, XElement.Parse(preAppearanceSaves.Primary)),
    "styling a v10 baby writes v12 and preserves every existing field");
Equal(preAppearanceSave, preAppearanceSaves.Backup, "v10 backup remains recoverable");
Equal(customLook, new BabySession(game, preAppearanceSaves, () => start).State.Appearance, "migrated v11 restores the look");

foreach (var oldSave in new[] { legacySave, energySave, oldCrawlerSave, preJournalSave, preHealthSave, preIncidentSave, preThemeSave, preWalkingSave, preBlocks.ToString(SaveOptions.DisableFormatting), preAppearanceSave })
    Equal(BabyAppearance.Default, BabySaveCodec.Decode(oldSave).Appearance,
        $"save version {XElement.Parse(oldSave).Attribute("version").Value} decodes to the default look");

var validStyled = XElement.Parse(BabySaveCodec.Encode(styledToddler));
foreach (var (attribute, value) in new[] {
    ("skin", "6"), ("skin", "-1"), ("hair-color", "6"), ("hair-style", "4"), ("outfit", "17"), ("headwear", "7"),
    ("headwear", "99"), ("skin", "Deep"), ("outfit", ""), ("hair-style", "99999999999") })
{
    var badLook = new XElement(validStyled);
    badLook.Element("appearance").SetAttributeValue(attribute, value);
    ExpectInvalidSave(badLook.ToString(SaveOptions.DisableFormatting), $"appearance {attribute}=\"{value}\" is rejected");
}
foreach (var attribute in new[] { "skin", "hair-color", "hair-style", "outfit", "headwear" })
{
    var missingAttribute = new XElement(validStyled);
    missingAttribute.Element("appearance").Attribute(attribute).Remove();
    ExpectInvalidSave(missingAttribute.ToString(SaveOptions.DisableFormatting), $"missing appearance {attribute} is rejected");
}
var missingLook = new XElement(validStyled);
missingLook.Element("appearance").Remove();
ExpectInvalidSave(missingLook.ToString(SaveOptions.DisableFormatting), "a v11 save without a look is rejected");

var invalidLookSaves = new MemorySaveStore();
var invalidLookSession = new BabySession(game, invalidLookSaves, () => start);
invalidLookSession.Adopt("Mia");
invalidLookSession.SetAppearance(customLook, unlockEarned: true);
var corruptLook = XElement.Parse(invalidLookSaves.Primary);
corruptLook.Element("appearance").SetAttributeValue("skin", 99);
invalidLookSaves.Primary = corruptLook.ToString(SaveOptions.DisableFormatting);
var lookRecovered = new BabySession(game, invalidLookSaves, () => start);
Equal("Mia", lookRecovered.State.Name, "an invalid look ID falls back to the backup save");
Equal(BabyAppearance.Default, lookRecovered.State.Appearance, "backup restores the last valid look");
lookRecovered.SetAppearance(customLook, unlockEarned: true);
Equal(customLook, new BabySession(game, invalidLookSaves, () => start).State.Appearance, "recovery can save a look again");

PetChecks.Run();
CareChecks.Run();
ConsequenceChecks.Run();
HealthChecks.Run();
HouseholdChecks.Run();
PetHouseholdChecks.Run();
EntitlementChecks.Run();
GrowthChecks.Run();
BondChecks.Run();
CatCareChecks.Run();
HamsterCareChecks.Run();
HelperChecks.Run();
TalkChecks.Run();
CosmeticChecks.Run();
Console.WriteLine("Simulation and wipe geometry checks passed.");

// Ordinary care until the baby is content: meals first, then soothing, rest and a bath.
static BabyState Calm(BabySimulation game, BabyState state)
{
    for (var i = 0; i < 12 && state.Care.Mood != CareMood.Content; i++)
    {
        var action = state.Satiety < 60 ? CareAction.Feed
            : state.Comfort < 60 || state.Care.Mood == CareMood.Upset ? CareAction.Soothe
            : state.Energy < 50 ? CareAction.Rest
            : CareAction.Bath;
        state = game.ApplyCare(state, action).State;
    }

    if (state.Care.Mood != CareMood.Content)
        throw new InvalidOperationException("Ordinary care should always calm the baby.");
    return state;
}

static void ExpectUnavailable(Action attempt)
{
    try
    {
        attempt();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("An unavailable care choice should be rejected.");
}

static void ExpectInvalidSave(string payload, string description)
{
    try
    {
        BabySaveCodec.Decode(payload);
    }
    catch (InvalidDataException)
    {
        return;
    }

    throw new InvalidOperationException($"{description}: expected InvalidDataException");
}

static XElement DefaultAppearanceElement() =>
    new XElement("appearance", new XAttribute("skin", 0), new XAttribute("hair-color", 0),
        new XAttribute("hair-style", 0), new XAttribute("outfit", 0), new XAttribute("headwear", 0));

// A pre-care save that is edited without elapsed time keeps an unanchored ledger at its checkpoint.
static XElement UnanchoredCareElement() =>
    CareRecordCodec.Encode(CareRules.Unanchored(new DateTimeOffset(639028224000000000, TimeSpan.Zero)));

static void Equal<T>(T expected, T actual, string description)
{
    if (!Equals(expected, actual))
        throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
}

sealed class MemorySaveStore : IBabySaveStore
{
    public string Primary { get; set; }
    public string Backup { get; private set; }
    public int Writes { get; private set; }
    public bool FailWrites { get; set; }

    public string Read() => Primary;
    public string ReadBackup() => Backup;

    public void Write(string payload, string previousValidPayload)
    {
        if (FailWrites)
            throw new IOException("Simulated write failure.");

        Primary = payload;
        Backup = previousValidPayload;
        Writes++;
    }
}
