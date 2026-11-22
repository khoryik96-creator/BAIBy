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
