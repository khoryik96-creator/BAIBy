using System;

namespace BAIby.Simulation
{
    public enum NurseryTheme { Sage = 0, Peach = 1 }
    public enum Toy { Rattle, Plush }
    public enum Food { Milk = 0, Banana = 1, Oats = 2 }
    public enum CareAction { Feed, Soothe, Play, Rest, ReadStory, ChangeDiaper, ComfortNewSound, ExploreNewSound, Bath, StackBlocks }
    public enum Reaction
    {
        None, Satisfied, Calmed, TracksToy, ReachesForToy, Rested, CrawlsToToy,
        EnjoysStory, FreshDiaper, NewSoundComforted, NewSoundExplored, FreshBath, WalksToToy, StackedBlocks
    }

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
        public int BlockTowers { get; }
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
        public bool CanWalk { get; }
        public bool NewSoundResolved { get; }
        public NurseryTheme NurseryTheme { get; }
        // Cosmetic only: never affects care rules, needs or growth. Never null.
        public BabyAppearance Appearance { get; }
        // Shadow care ledger: evaluated and saved, but it changes no needs, growth or reactions yet. Never null.
        public CareRecord Care { get; }
        public bool HasPendingNewSound => CanSitAndReach && !NewSoundResolved;

        public static BabyState Restore(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, double energy, int toyFamiliarity, int plushFamiliarity,
            bool canSitAndReach, bool canCrawl, double connection, int storySessions, double cleanliness, double health,
            bool newSoundResolved, NurseryTheme nurseryTheme = NurseryTheme.Sage, bool canWalk = false, int blockTowers = 0,
            BabyAppearance appearance = null, CareRecord care = null)
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

            if (newSoundResolved && !canSitAndReach)
                throw new ArgumentException("The nursery incident requires the sitting milestone.", nameof(newSoundResolved));

            if (canWalk && !canCrawl)
                throw new ArgumentException("Walking requires the crawling milestone.", nameof(canWalk));

            if (blockTowers < 0)
                throw new ArgumentOutOfRangeException(nameof(blockTowers));
            if (blockTowers > 0 && !canWalk)
                throw new ArgumentException("Stacking blocks requires the walking milestone.", nameof(blockTowers));

            ValidateNurseryTheme(nurseryTheme);
            // A baby restored without a ledger predates care; its first advance excuses the whole gap.
            care = care ?? CareRules.Unanchored(timeCheckpoint);
            if (care.Checkpoint.UtcTicks != timeCheckpoint.UtcTicks)
                throw new ArgumentException("The care ledger must share the baby's time checkpoint.", nameof(care));

            return new BabyState(name, timeCheckpoint, age, satiety, comfort, energy,
                toyFamiliarity, plushFamiliarity, canSitAndReach, canCrawl, connection, storySessions, cleanliness, health, newSoundResolved, nurseryTheme, canWalk, blockTowers,
                appearance ?? BabyAppearance.Default, care);
        }

        internal BabyState(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, double energy, int toyFamiliarity, int plushFamiliarity,
            bool canSitAndReach, bool canCrawl, double connection, int storySessions, double cleanliness, double health,
            bool newSoundResolved, NurseryTheme nurseryTheme, bool canWalk, int blockTowers, BabyAppearance appearance,
            CareRecord care)
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
            BlockTowers = blockTowers;
            ToyFamiliarity = toyFamiliarity;
            PlushFamiliarity = plushFamiliarity;
            CanSitAndReach = canSitAndReach;
            CanCrawl = canCrawl;
            CanWalk = canWalk;
            NewSoundResolved = newSoundResolved;
            NurseryTheme = nurseryTheme;
            Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            Care = care ?? throw new ArgumentNullException(nameof(care));
        }

        // Copy-with: every omitted argument keeps its current value, so adding a field cannot shift
        // positional arguments at call sites. Like the constructor, it trusts simulation rules to keep
        // values valid; Restore validates data from outside.
        internal BabyState With(DateTimeOffset? timeCheckpoint = null, TimeSpan? age = null,
            double? satiety = null, double? comfort = null, double? energy = null,
            int? toyFamiliarity = null, int? plushFamiliarity = null, bool? canSitAndReach = null, bool? canCrawl = null,
            double? connection = null, int? storySessions = null, double? cleanliness = null, double? health = null,
            bool? newSoundResolved = null, NurseryTheme? nurseryTheme = null, bool? canWalk = null, int? blockTowers = null,
            BabyAppearance appearance = null, CareRecord care = null) =>
            new BabyState(Name, timeCheckpoint ?? TimeCheckpoint, age ?? Age,
                satiety ?? Satiety, comfort ?? Comfort, energy ?? Energy,
                toyFamiliarity ?? ToyFamiliarity, plushFamiliarity ?? PlushFamiliarity,
                canSitAndReach ?? CanSitAndReach, canCrawl ?? CanCrawl, connection ?? Connection,
                storySessions ?? StorySessions, cleanliness ?? Cleanliness, health ?? Health,
                newSoundResolved ?? NewSoundResolved, nurseryTheme ?? NurseryTheme, canWalk ?? CanWalk,
                blockTowers ?? BlockTowers, appearance ?? Appearance, care ?? Care);

        public BabyState WithNurseryTheme(NurseryTheme nurseryTheme)
        {
            ValidateNurseryTheme(nurseryTheme);
            if (NurseryTheme == nurseryTheme)
                return this;

            return With(nurseryTheme: nurseryTheme);
        }

        public BabyState WithAppearance(BabyAppearance appearance)
        {
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));
            if (Appearance.Equals(appearance))
                return this;

            return With(appearance: appearance);
        }

        private static void ValidateNurseryTheme(NurseryTheme nurseryTheme)
        {
            if (nurseryTheme != NurseryTheme.Sage && nurseryTheme != NurseryTheme.Peach)
                throw new ArgumentOutOfRangeException(nameof(nurseryTheme));
        }
    }
}
