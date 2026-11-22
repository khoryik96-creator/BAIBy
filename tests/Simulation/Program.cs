using System;
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

var played = game.ApplyCare(newborn, CareAction.Play);
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
var repeat = game.Advance(resume.State, resume.State.TimeCheckpoint.ToOffset(TimeSpan.FromHours(8)));
Equal(resume.State, repeat.State, "same instant in a new timezone is a no-op");
Equal(false, repeat.UnlockedSitting, "milestone cannot fire twice");
Equal(resume.State, game.Advance(resume.State, start).State, "backwards clock preserves state");
Equal(false, game.Advance(resume.State, start.AddDays(11)).UnlockedSitting, "future ticks preserve milestone");
Console.WriteLine("Simulation checks passed.");

static void Equal<T>(T expected, T actual, string description)
{
    if (!Equals(expected, actual))
        throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
}
