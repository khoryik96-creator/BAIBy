using System;
using System.Collections.Generic;

namespace BAIby.Simulation.Pets
{
    // A dog's tricks (docs/pet-mode.md, Species: "tricks learned through familiarity counts"). Every game of fetch that is
    // not one too many (Bored) adds one to the dog's familiarity with the game, as a baby's familiarity with a toy grows,
    // and each trick arrives at a fixed count. Nothing is random, nothing is lost, and a trick is never asked for again:
    // it is something the dog shows off.
    public static class DogTricks
    {
        // Familiarity saturates here, well past the last trick, so the count fits any save.
        public const int MaxFamiliarity = 999;

        private static readonly (int At, string Name)[] Ladder =
        {
            (2, "Sit"),
            (5, "Paw"),
            (9, "Spin"),
            (14, "Roll over"),
            (20, "High five"),
        };

        public static int Count => Ladder.Length;

        // How many tricks a familiarity count has taught.
        public static int Learned(int familiarity)
        {
            var learned = 0;
            while (learned < Ladder.Length && familiarity >= Ladder[learned].At)
                learned++;
            return learned;
        }

        // The tricks learned so far, in the order they arrived.
        public static IReadOnlyList<string> Names(int familiarity)
        {
            var names = new List<string>();
            for (var index = 0; index < Learned(familiarity); index++)
                names.Add(Ladder[index].Name);
            return names;
        }

        // The newest trick learned, or null before the first.
        public static string Newest(int familiarity)
        {
            var learned = Learned(familiarity);
            return learned == 0 ? null : Ladder[learned - 1].Name;
        }

        // Games of fetch until the next trick, or 0 once every trick is learned.
        public static int GamesToNext(int familiarity)
        {
            var learned = Learned(familiarity);
            return learned >= Ladder.Length ? 0 : Ladder[learned].At - Math.Max(0, familiarity);
        }

        public static int Next(int familiarity) => Math.Min(MaxFamiliarity, Math.Max(0, familiarity) + 1);
    }
}
