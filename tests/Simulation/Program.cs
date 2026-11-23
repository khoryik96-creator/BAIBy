using System;
using System.IO;
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

saves.Primary = "<baby version=\"6\" />";
try
{
    _ = new BabySession(game, saves, () => now);
    throw new InvalidOperationException("A future save version must fail instead of loading older backup.");
}
catch (NotSupportedException)
{
    Equal("<baby version=\"6\" />", saves.Primary, "future save is preserved");
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
Equal(true, migratedSaves.Primary.Contains("version=\"5\""), "first edit after migration writes v5");
Equal(legacySave, migratedSaves.Backup, "v1 backup remains recoverable");
Equal(100.0, new BabySession(game, migratedSaves, () => start).State.Energy,
    "rest on migrated save persists energy in v5");

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

var maxToyPlay = BabyState.Restore("Mia", start, TimeSpan.Zero, 70, 75, 80, int.MaxValue, 0, false, false, 0, 0);
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
Equal(true, crawlerSaves.Primary.Contains("version=\"5\""), "crawling state writes the current schema");
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
var manyStories = BabyState.Restore("Mia", start, TimeSpan.Zero, 70, 75, 80, 0, 0, false, false, 0, int.MaxValue);
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

Console.WriteLine("Simulation checks passed.");

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
