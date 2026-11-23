using System;

namespace BAIby.Simulation
{
    public enum Toy { Rattle, Plush }
    public enum Food { Milk = 0, Banana = 1, Oats = 2 }
    public enum CareAction { Feed, Soothe, Play, Rest, ReadStory, ChangeDiaper }
    public enum Reaction { None, Satisfied, Calmed, TracksToy, ReachesForToy, Rested, CrawlsToToy, EnjoysStory, FreshDiaper }

    public sealed class BabyState
    {
        public string Name { get; }
        public DateTimeOffset TimeCheckpoint { get; }
        public TimeSpan Age { get; }
        public double Satiety { get; }
        public double Comfort { get; }
        public double Energy { get; }
        public double Cleanliness { get; }
        public double Health { get; }
        public double Connection { get; }
        public int StorySessions { get; }
        // The original familiarity count is now specific to the rattle.
        public int ToyFamiliarity { get; }
        public int PlushFamiliarity { get; }
        public Toy? FavoriteToy
        {
            get
            {
                if (ToyFamiliarity >= 2 && ToyFamiliarity > PlushFamiliarity)
                    return Toy.Rattle;
                if (PlushFamiliarity >= 2 && PlushFamiliarity > ToyFamiliarity)
                    return Toy.Plush;

                return null;
            }
        }
        public bool CanSitAndReach { get; }
        public bool CanCrawl { get; }

        public static BabyState Restore(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, double energy, int toyFamiliarity, int plushFamiliarity,
            bool canSitAndReach, bool canCrawl, double connection, int storySessions, double cleanliness, double health)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The saved baby needs a name.", nameof(name));
            if (age < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(age));
            if (double.IsNaN(satiety) || satiety < 0 || satiety > 100)
                throw new ArgumentOutOfRangeException(nameof(satiety));
            if (double.IsNaN(comfort) || comfort < 0 || comfort > 100)
                throw new ArgumentOutOfRangeException(nameof(comfort));
            if (double.IsNaN(energy) || energy < 0 || energy > 100)
                throw new ArgumentOutOfRangeException(nameof(energy));
            if (toyFamiliarity < 0)
                throw new ArgumentOutOfRangeException(nameof(toyFamiliarity));
            if (plushFamiliarity < 0)
                throw new ArgumentOutOfRangeException(nameof(plushFamiliarity));

            if (double.IsNaN(connection) || connection < 0 || connection > 100)
                throw new ArgumentOutOfRangeException(nameof(connection));
            if (storySessions < 0)
                throw new ArgumentOutOfRangeException(nameof(storySessions));

            if (double.IsNaN(cleanliness) || cleanliness < 0 || cleanliness > 100)
                throw new ArgumentOutOfRangeException(nameof(cleanliness));
            if (double.IsNaN(health) || health < 0 || health > 100)
                throw new ArgumentOutOfRangeException(nameof(health));

            if (canCrawl && !canSitAndReach)
                throw new ArgumentException("Crawling requires the sitting milestone.", nameof(canCrawl));

            return new BabyState(name, timeCheckpoint, age, satiety, comfort, energy,
                toyFamiliarity, plushFamiliarity, canSitAndReach, canCrawl, connection, storySessions, cleanliness, health);
        }

        internal BabyState(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, double energy, int toyFamiliarity, int plushFamiliarity,
            bool canSitAndReach, bool canCrawl, double connection, int storySessions, double cleanliness, double health)
        {
            Name = name;
            TimeCheckpoint = timeCheckpoint;
            Age = age;
            Satiety = satiety;
            Comfort = comfort;
            Energy = energy;
            Cleanliness = cleanliness;
            Health = health;
            Connection = connection;
            StorySessions = storySessions;
            ToyFamiliarity = toyFamiliarity;
            PlushFamiliarity = plushFamiliarity;
            CanSitAndReach = canSitAndReach;
            CanCrawl = canCrawl;
        }
    }
}
