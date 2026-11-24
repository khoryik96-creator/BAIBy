using System;

namespace BAIby.Simulation
{
    public sealed class BabyReport
    {
        private const int InterestThreshold = 2;

        public string Name { get; }
        public TimeSpan Age { get; }
        public bool CanSitAndReach { get; }
        public bool CanCrawl { get; }
        public bool CanWalk { get; }
        public Toy? FavoriteToy { get; }
        public int StorySessions { get; }
        public int BlockTowers { get; }
        public double Bond { get; }

        public bool EnjoysStories => StorySessions >= InterestThreshold;
        public bool EnjoysBlocks => BlockTowers >= InterestThreshold;

        public static BabyReport From(BabyState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return new BabyReport(state);
        }

        private BabyReport(BabyState state)
        {
            Name = state.Name;
            Age = state.Age;
            CanSitAndReach = state.CanSitAndReach;
            CanCrawl = state.CanCrawl;
            CanWalk = state.CanWalk;
            FavoriteToy = state.FavoriteToy;
            StorySessions = state.StorySessions;
            BlockTowers = state.BlockTowers;
            Bond = state.Bond;
        }
    }
}
