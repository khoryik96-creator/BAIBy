using System;

namespace BAIby.Simulation
{
    public enum CareAction { Feed, Soothe, Play }
    public enum Reaction { None, Satisfied, Calmed, TracksToy, ReachesForToy }

    public sealed class BabyState
    {
        public string Name { get; }
        public DateTimeOffset TimeCheckpoint { get; }
        public TimeSpan Age { get; }
        public double Satiety { get; }
        public double Comfort { get; }
        public int ToyFamiliarity { get; }
        public bool CanSitAndReach { get; }

        public static BabyState Restore(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, int toyFamiliarity, bool canSitAndReach)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The saved baby needs a name.", nameof(name));
            if (age < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(age));
            if (double.IsNaN(satiety) || satiety < 0 || satiety > 100)
                throw new ArgumentOutOfRangeException(nameof(satiety));
            if (double.IsNaN(comfort) || comfort < 0 || comfort > 100)
                throw new ArgumentOutOfRangeException(nameof(comfort));
            if (toyFamiliarity < 0 || toyFamiliarity > BabySimulation.MaxFamiliarity)
                throw new ArgumentOutOfRangeException(nameof(toyFamiliarity));

            return new BabyState(name, timeCheckpoint, age, satiety, comfort, toyFamiliarity, canSitAndReach);
        }

        internal BabyState(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, int toyFamiliarity, bool canSitAndReach)
        {
            Name = name;
            TimeCheckpoint = timeCheckpoint;
            Age = age;
            Satiety = satiety;
            Comfort = comfort;
            ToyFamiliarity = toyFamiliarity;
            CanSitAndReach = canSitAndReach;
        }
    }
}
