using System;
using System.IO;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7));
var newborn = game.Adopt("Mia", start);

var fed = game.ApplyCare(newborn, CareAction.Feed);
Equal(95.0, fed.State.Satiety, "feeding improves satiety");
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

var before = game.Advance(played.State, start.AddDays(3).AddTicks(-1));
Equal(false, before.UnlockedSitting, "milestone waits for threshold");
var threshold = game.Advance(before.State, start.AddDays(3));
Equal(true, threshold.UnlockedSitting, "milestone unlocks at threshold");
Equal(TimeSpan.FromDays(3), threshold.State.Age, "elapsed time advances age");
Equal(Reaction.ReachesForToy, game.ApplyCare(threshold.State, CareAction.Play).Reaction,
    "older baby reaches for familiar toy");
Equal(Reaction.TracksToy, game.ApplyCare(game.Advance(newborn, start.AddDays(3)).State, CareAction.Play).Reaction,
    "older baby tracks unfamiliar toy");

var resume = game.Advance(newborn, start.AddDays(10));
Equal(30.0, resume.State.Satiety, "offline satiety decay is capped");
Equal(55.0, resume.State.Comfort, "offline comfort decay is capped");
Equal(48.0, resume.State.Energy, "offline energy decay is capped");
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
Equal((true, false), session.AdvancePreview(TimeSpan.FromDays(3)), "preview unlocks sitting");
Equal(start, session.State.TimeCheckpoint, "preview leaves real clock alone");

var reopened = new BabySession(game, saves, () => now);
Equal("Mia & Co", reopened.State.Name, "name survives XML round trip");
Equal(TimeSpan.FromDays(3), reopened.State.Age, "preview aging persists");
Equal(1, reopened.State.ToyFamiliarity, "familiarity persists");
Equal(36.0, reopened.State.Energy, "energy persists");
Equal(true, reopened.State.CanSitAndReach, "milestone persists");
Equal((false, false), reopened.Refresh(), "same-time reopen does not unlock or write");
Equal(3, saves.Writes, "each edit writes one save and no-op refresh skips a write");

now = now.AddHours(1);
var afterReopenCare = reopened.ApplyCare(CareAction.Play);
Equal(Reaction.ReachesForToy, afterReopenCare.Reaction, "reopened care uses persisted familiarity");
Equal(TimeSpan.FromHours(73), reopened.State.Age, "real time advances after preview");
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
    Equal(20.0, reopened.State.Satiety, "failed care leaves visible state unchanged");
}

saves.Primary = "<baby";
var recovered = new BabySession(game, saves, () => now);
Equal(true, recovered.State.CanSitAndReach, "corrupt primary falls back to previous valid save");
Equal(1, recovered.State.ToyFamiliarity, "backup recovers last valid pre-edit state");
saves.FailWrites = false;
recovered.ApplyCare(CareAction.Play);
Equal(true, new BabySession(game, saves, () => now).State.CanSitAndReach, "recovery can save again");

saves.Primary = "<baby version=\"9\" />";
try
{
    _ = new BabySession(game, saves, () => now);
    throw new InvalidOperationException("A future save version must fail instead of loading older backup.");
}
catch (NotSupportedException)
{
    Equal("<baby version=\"9\" />", saves.Primary, "future save is preserved");
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
Equal(true, migratedSaves.Primary.Contains("version=\"8\""), "first edit after migration writes v8");
Equal(legacySave, migratedSaves.Backup, "v1 backup remains recoverable");
Equal(100.0, new BabySession(game, migratedSaves, () => start).State.Energy,
    "rest on migrated save persists energy in v8");

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

var almostCrawling = game.Advance(threshold.State, start.AddDays(7).AddTicks(-1));
Equal(false, almostCrawling.UnlockedCrawling, "crawling waits for age threshold");
Equal(Reaction.ReachesForToy, game.ApplyCare(almostCrawling.State, CareAction.Play).Reaction,
    "sitter continues to reach for a familiar toy");
var crawler = game.Advance(almostCrawling.State, start.AddDays(7));
Equal(false, crawler.UnlockedSitting, "crawling does not re-award sitting");
Equal(true, crawler.UnlockedCrawling, "crawling unlocks at exact threshold");
Equal(true, crawler.State.CanCrawl, "crawling persists in simulation state");
Equal(Reaction.CrawlsToToy, game.ApplyCare(crawler.State, CareAction.Play).Reaction,
    "crawler moves toward a familiar toy");
Equal(Reaction.TracksToy, game.ApplyCare(crawler.State, CareAction.Play, Toy.Plush).Reaction,
    "crawler still tracks an unfamiliar toy");
Equal(false, game.Advance(crawler.State, start.AddDays(8)).UnlockedCrawling,
    "future time cannot re-award crawling");
var overnight = game.Advance(newborn, start.AddDays(7));
Equal(true, overnight.UnlockedSitting && overnight.UnlockedCrawling, "long absence can unlock both milestones");

const string oldCrawlerSave = "<baby version=\"3\"><name>Sam</name><checkpoint>639028224000000000</checkpoint>" +
    "<age>6912000000000</age><satiety>41</satiety><comfort>52</comfort><energy>63</energy>" +
    "<toy-familiarity>2</toy-familiarity><plush-familiarity>3</plush-familiarity>" +
    "<can-sit-and-reach>true</can-sit-and-reach></baby>";
var crawlerSaves = new MemorySaveStore { Primary = oldCrawlerSave };
var crawlerSession = new BabySession(game, crawlerSaves, () => start);
Equal(3, crawlerSession.State.PlushFamiliarity, "v3 migration preserves plush history");
Equal((false, true), crawlerSession.Refresh(), "migration unlocks already earned crawling at same instant");
Equal(41.0, crawlerSession.State.Satiety, "migration at same instant does not decay satiety");
Equal(63.0, crawlerSession.State.Energy, "migration at same instant does not decay energy");
Equal(1, crawlerSaves.Writes, "migrated milestone persists once");
Equal(true, crawlerSaves.Primary.Contains("version=\"8\""), "crawling state writes the current schema");
var persistedCrawler = new BabySession(game, crawlerSaves, () => start);
Equal(true, persistedCrawler.State.CanCrawl, "v4 restores crawling");
Equal((false, false), persistedCrawler.Refresh(), "v4 refresh cannot re-award crawling");
Equal(Reaction.CrawlsToToy, persistedCrawler.ApplyCare(CareAction.Play, Toy.Plush).Reaction,
    "v4 restarts with familiar toy crawl behavior");
var previewCrawler = new BabySession(game, new MemorySaveStore(), () => start);
previewCrawler.Adopt("Mia");
Equal((true, true), previewCrawler.AdvancePreview(TimeSpan.FromDays(7)), "preview forwards both unlocks");
Equal((false, false), previewCrawler.AdvancePreview(TimeSpan.FromDays(1)), "preview does not re-award milestones");

var careClock = start;
var crawlingCare = new BabySession(game, new MemorySaveStore(), () => careClock);
crawlingCare.Adopt("Mia");
crawlingCare.ApplyCare(CareAction.Play, Toy.Plush);
careClock = start.AddDays(7);
Equal((Reaction.CrawlsToToy, true, true), crawlingCare.ApplyCare(CareAction.Play, Toy.Plush),
    "care forwards new milestones and uses the newly unlocked ability");
Equal((Reaction.CrawlsToToy, false, false), crawlingCare.ApplyCare(CareAction.Play, Toy.Plush),
    "same-time care does not re-award milestones");

Equal(0.0, newborn.Connection, "newborn starts building connection");
Equal(3.0, game.ApplyCare(newborn, CareAction.Soothe).State.Connection, "soothing builds lasting connection");
Equal(1.0, played.State.Connection, "playing builds lasting connection");
var story = game.ApplyCare(newborn, CareAction.ReadStory);
Equal(Reaction.EnjoysStory, story.Reaction, "reading has its own reaction");
Equal(1, story.State.StorySessions, "reading records a story session");
Equal(2.0, story.State.Connection, "reading builds connection");
Equal(74.0, story.State.Energy, "reading uses a little energy");
Equal(newborn.Age, story.State.Age, "reading does not advance age");
Equal(0.0, game.ApplyCare(tired, CareAction.ReadStory).State.Energy, "reading cannot reduce energy below zero");
var longAbsence = game.Advance(story.State, start.AddDays(10));
Equal(2.0, longAbsence.State.Connection, "connection does not decay while away");
Equal(1, longAbsence.State.StorySessions, "story history survives elapsed time");
var bedtime = story.State;
for (var i = 0; i < 60; i++)
    bedtime = game.ApplyCare(bedtime, CareAction.ReadStory).State;
Equal(100.0, bedtime.Connection, "repeated reading caps connection");
Equal(61, bedtime.StorySessions, "reading still records sessions when connection is capped");
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
Equal(0.0, journal.State.Connection, "v4 starts connection at baseline");
Equal(0, journal.State.StorySessions, "v4 has no story history");
journal.ApplyCare(CareAction.ReadStory);
journal.ApplyCare(CareAction.Soothe);
var resumedJournal = new BabySession(game, journalSaves, () => start);
Equal(5.0, resumedJournal.State.Connection, "v5 resumes lasting connection");
Equal(1, resumedJournal.State.StorySessions, "v5 resumes story session count");
Equal(true, resumedJournal.State.CanCrawl, "v5 retains reached milestones");

Equal(100.0, newborn.Health, "newborn starts healthy");
Equal(100.0, newborn.Cleanliness, "newborn starts clean");
Equal(44.0, resume.State.Cleanliness, "offline diaper decay respects the shared cap");
Equal(100.0, resume.State.Health, "one capped gap with no sustained low needs preserves health");
var fresh = game.ApplyCare(resume.State, CareAction.ChangeDiaper);
Equal(Reaction.FreshDiaper, fresh.Reaction, "diaper change has its own reaction");
Equal(100.0, fresh.State.Cleanliness, "diaper change restores cleanliness with a cap");
Equal(resume.State.Health, fresh.State.Health, "diaper change does not instantly change health");

var lowNeeds = BabyState.Restore("Mia", start, TimeSpan.Zero, 65, 100, 100, 0, 0, false, false, 0, 0, 100, 60, false);
var singleTick = game.Advance(lowNeeds, start.AddHours(4)).State;
var firstTick = game.Advance(lowNeeds, start.AddHours(1.25)).State;
var splitTicks = game.Advance(firstTick, start.AddHours(4)).State;
Equal(60.5, singleTick.Health, "health integrates only time within recovery and poor need thresholds");
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
Equal(0.0, exhausted.Health, "health cannot fall below zero");
Equal(0.0, exhausted.Cleanliness, "cleanliness cannot fall below zero");
for (var i = 0; i < 4; i++)
{
    exhausted = game.ApplyCare(exhausted, CareAction.Feed).State;
    exhausted = game.ApplyCare(exhausted, CareAction.Soothe).State;
    exhausted = game.ApplyCare(exhausted, CareAction.Rest).State;
}
exhausted = game.ApplyCare(exhausted, CareAction.ChangeDiaper).State;
Equal(100.0, exhausted.Cleanliness, "one diaper change restores cleanliness from zero");
Equal(6.0, game.Advance(exhausted, start.AddHours(5)).State.Health, "health can recover from zero through care");

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
Equal(34.0, healthSession.State.Connection, "v5 migration preserves connection");
Equal(8, healthSession.State.StorySessions, "v5 migration preserves story history");
healthClock = start.AddHours(3);
healthSession.Refresh();
Equal(true, healthSaves.Primary.Contains("version=\"8\""), "health save uses the current schema");
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
Equal(hungryCrawler.Satiety + 20, banana.State.Satiety, "banana improves satiety");
Equal(hungryCrawler.Comfort + 8, banana.State.Comfort, "banana improves comfort");
Equal(hungryCrawler.Energy, banana.State.Energy, "banana leaves energy unchanged");
Equal(Reaction.Satisfied, banana.Reaction, "banana uses the feeding reaction");
var oats = game.ApplyCare(hungryCrawler, CareAction.Feed, food: Food.Oats);
Equal(hungryCrawler.Satiety + 34, oats.State.Satiety, "oats give the largest satiety boost");
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
mealtimeClock = start.AddDays(7);
ExpectUnavailable(() => mealtime.ApplyCare(CareAction.Feed, food: (Food)99));
Equal(beforeMeal, mealtime.State, "unknown food cannot consume pending milestones");
Equal(1, mealtimeSaves.Writes, "unknown food commits nothing");
Equal((Reaction.Satisfied, true, true), mealtime.ApplyCare(CareAction.Feed, food: Food.Oats),
    "food availability uses age at the same instant as feeding");
Equal(2, mealtimeSaves.Writes, "milestones and valid feeding commit once together");

Equal(false, newborn.HasPendingNewSound, "incident does not appear before sitting");
Equal(true, threshold.State.HasPendingNewSound, "sitting makes the incident available");
Equal(true, crawler.State.HasPendingNewSound, "incident remains available until addressed");

var reassured = game.ApplyCare(threshold.State, CareAction.ComfortNewSound);
Equal(Reaction.NewSoundComforted, reassured.Reaction, "comfort has its own incident reaction");
Equal(threshold.State.Comfort + 16, reassured.State.Comfort, "comfort choice soothes the baby");
Equal(threshold.State.Connection + 2, reassured.State.Connection, "comfort choice builds connection");
Equal(threshold.State.Energy, reassured.State.Energy, "comfort choice leaves energy unchanged");
Equal(true, reassured.State.NewSoundResolved, "comfort resolves the incident");
Equal(false, reassured.State.HasPendingNewSound, "resolved incident is no longer pending");

var explored = game.ApplyCare(threshold.State, CareAction.ExploreNewSound);
Equal(Reaction.NewSoundExplored, explored.Reaction, "exploration has its own incident reaction");
Equal(threshold.State.Connection + 5, explored.State.Connection, "exploration builds more connection");
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

incidentClock = start.AddDays(3);
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
Equal((Reaction.NewSoundExplored, true, false), incidentSession.ApplyCare(CareAction.ExploreNewSound),
    "successful retry commits the pending milestone and choice together");
var incidentReload = new BabySession(game, incidentSaves, () => incidentClock);
Equal(true, incidentReload.State.NewSoundResolved, "incident resolution survives save and reload");
var resolvedPayload = incidentSaves.Primary;
incidentClock = start.AddDays(4);
ExpectUnavailable(() => incidentReload.ApplyCare(CareAction.ComfortNewSound));
ExpectUnavailable(() => incidentReload.ApplyCare(CareAction.ExploreNewSound));
Equal(resolvedPayload, incidentSaves.Primary, "duplicate choices cannot change the save");
Equal(start.AddDays(3), incidentReload.State.TimeCheckpoint, "duplicate choices cannot advance the checkpoint");

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
expectedTheme.SetAttributeValue("version", 8);
expectedTheme.Add(new XElement("nursery-theme", 1));
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

Console.WriteLine("Simulation and wipe geometry checks passed.");

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
