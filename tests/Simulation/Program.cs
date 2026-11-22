using System;
using System.IO;
using BAIby.Application;
using BAIby.Simulation;

var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4));
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
Equal(true, session.AdvancePreview(TimeSpan.FromDays(3)), "preview unlocks sitting");
Equal(start, session.State.TimeCheckpoint, "preview leaves real clock alone");

var reopened = new BabySession(game, saves, () => now);
Equal("Mia & Co", reopened.State.Name, "name survives XML round trip");
Equal(TimeSpan.FromDays(3), reopened.State.Age, "preview aging persists");
Equal(1, reopened.State.ToyFamiliarity, "familiarity persists");
Equal(36.0, reopened.State.Energy, "energy persists");
Equal(true, reopened.State.CanSitAndReach, "milestone persists");
Equal(false, reopened.Refresh(), "same-time reopen does not unlock or write");
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

saves.Primary = "<baby version=\"3\" />";
try
{
    _ = new BabySession(game, saves, () => now);
    throw new InvalidOperationException("A future save version must fail instead of loading older backup.");
}
catch (NotSupportedException)
{
    Equal("<baby version=\"3\" />", saves.Primary, "future save is preserved");
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
Equal(2, migrated.State.ToyFamiliarity, "v1 migration preserves familiarity");
Equal(true, migrated.State.CanSitAndReach, "v1 migration preserves milestone");
Equal(80.0, migrated.State.Energy, "v1 migration supplies default energy");
migrated.ApplyCare(CareAction.Rest);
Equal(true, migratedSaves.Primary.Contains("version=\"2\""), "first edit after migration writes v2");
Equal(legacySave, migratedSaves.Backup, "v1 backup remains recoverable");
Equal(100.0, new BabySession(game, migratedSaves, () => start).State.Energy,
    "rest on migrated save persists energy in v2");

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
