using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // What a care verb means to the routine. The care core sees only these tags, never a species' verbs.
    [Flags]
    public enum CareTags
    {
        None = 0,
        Meal = 1,
        Together = 2,
        Hygiene = 4,
        Bedtime = 8,
        Medicine = 16,
        Tidy = 32,
        // Settling a distressed companion (soothing, cuddles). Calms the Upset rung.
        Comfort = 64,
        // Play that answers a timed play window (a cat's wand play), with Together. 128 stays unused: the care checks
        // keep it as an unknown tag.
        Play = 256,
        // A walk that answers a dog's walk windows (P3), with Together. A walk is never automated: no helper carries it.
        Walk = 512,
    }

    // A species' care data: its stable verb IDs and the tags each one carries.
    public sealed class CareProfile
    {
        private const CareTags AllTags = CareTags.Meal | CareTags.Together | CareTags.Hygiene |
            CareTags.Bedtime | CareTags.Medicine | CareTags.Tidy | CareTags.Comfort | CareTags.Play | CareTags.Walk;
        private readonly Dictionary<string, CareTags> verbs;

        public string Species { get; }
        public IEnumerable<string> Verbs => verbs.Keys;

        public CareProfile(string species, IEnumerable<KeyValuePair<string, CareTags>> verbTags)
        {
            if (string.IsNullOrWhiteSpace(species))
                throw new ArgumentException("A care profile needs a species ID.", nameof(species));
            if (verbTags == null)
                throw new ArgumentNullException(nameof(verbTags));

            verbs = new Dictionary<string, CareTags>(StringComparer.Ordinal);
            foreach (var pair in verbTags)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Care verb IDs cannot be empty.", nameof(verbTags));
                if ((pair.Value & ~AllTags) != 0)
                    throw new ArgumentOutOfRangeException(nameof(verbTags), $"Care verb {pair.Key} has unknown tags.");
                if (verbs.ContainsKey(pair.Key))
                    throw new ArgumentException($"Care verb {pair.Key} is listed twice.", nameof(verbTags));

                verbs.Add(pair.Key, pair.Value);
            }

            if (verbs.Count == 0)
                throw new ArgumentException("A care profile needs at least one verb.", nameof(verbTags));

            Species = species;
        }

        public CareTags TagsFor(string verb)
        {
            if (verb == null || !verbs.TryGetValue(verb, out var tags))
                throw new ArgumentOutOfRangeException(nameof(verb), $"The {Species} profile has no care verb {verb}.");

            return tags;
        }
    }
}
