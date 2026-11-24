using System;

namespace BAIby.Simulation
{
    // Saved content IDs (append only). Sage and Peach are free; the others are room themes (CosmeticCatalog).
    public enum NurseryTheme { Sage = 0, Peach = 1, CloudNursery = 2, Starlight = 3, GoldenHour = 4 }
    public enum Toy { Rattle, Plush }
    public enum Food { Milk = 0, Banana = 1, Oats = 2 }
    public enum CareAction { Feed, Soothe, Play, Rest, ReadStory, ChangeDiaper, ComfortNewSound, ExploreNewSound, Bath, StackBlocks, Medicine }
    public enum Reaction
    {
        None, Satisfied, Calmed, TracksToy, ReachesForToy, Rested, CrawlsToToy,
        EnjoysStory, FreshDiaper, NewSoundComforted, NewSoundExplored, FreshBath, WalksToToy, StackedBlocks,
        PushesToyAway, TooFull,
        // The health axis: a sick baby gently refuses play, stories, blocks and baths; the doctor's visit; medicine.
        TooPoorly, DoctorVisited, DoctorAllClear, MedicineTaken, MedicineDone, MedicineNotNow,
        // Development: the same play over and over pays less and looks bored; a good day after a setback brings the
        // shy skill back.
        Bored, SkillBack,
        // Talk time's gestures besides a cuddle (Calmed): a happy bounce, and a calm sit close by.
        HappyBounce, SitsCalmly
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
        // Bond, 0 to 100: the relationship meter that replaced Connection (saved under its old name). It grows with
        // care and falls with missed meals and unanswered distress, never below 60% of its high-water mark.
        public double Bond { get; }
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
        // The care ledger and consequence state (mood, mess, Away mode, care style). Never null.
        public CareRecord Care { get; }
        // Development tracks, readiness and the setback. Null for a baby saved before tracks existed: the
        // simulation seeds it from age on first use.
        public DevelopmentRecord Development { get; }
        // Bond's high-water mark and caps, temperament and the care rhythm. Null for a baby saved before them: the
        // simulation seeds it (high-water mark = Bond, neutral temperament, no rhythm) on first use.
        public BondRecord Bonding { get; }
        // Milestones, photos, stickers, hard days and weekly pages. Null for a baby saved before the journal: the
        // simulation seeds it with the milestones already reached.
        public JournalRecord Journal { get; }
        public bool HasPendingNewSound => CanSitAndReach && !NewSoundResolved;
        // The skill a setback made shy, if any. It stays reached (skills never re-lock); the baby just shows the
        // stage before it for a while, so the walker crawls to toys.
        public MotorSkill ShySkill => Development?.Shy ?? MotorSkill.None;
        public bool ShowsSitting => CanSitAndReach && ShySkill != MotorSkill.Sitting;
        public bool ShowsCrawling => CanCrawl && ShySkill != MotorSkill.Crawling;
        public bool ShowsWalking => CanWalk && ShySkill != MotorSkill.Walking;
        // First words, the Learning track's milestone: Talk time opens with it. Never re-locks.
        public bool HasFirstWords => Development?.FirstWords ?? false;

        public static BabyState Restore(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, double energy, int toyFamiliarity, int plushFamiliarity,
            bool canSitAndReach, bool canCrawl, double connection, int storySessions, double cleanliness, double health,
            bool newSoundResolved, NurseryTheme nurseryTheme = NurseryTheme.Sage, bool canWalk = false, int blockTowers = 0,
            BabyAppearance appearance = null, CareRecord care = null, DevelopmentRecord development = null,
            BondRecord bonding = null, JournalRecord journal = null)
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
            if (development != null)
            {
                var reached = canWalk ? MotorSkill.Walking : canCrawl ? MotorSkill.Crawling :
                    canSitAndReach ? MotorSkill.Sitting : MotorSkill.None;
                if (development.Shy > reached)
                    throw new ArgumentException("Only a reached skill can be shy.", nameof(development));
            }

            if (bonding != null && bonding.HighWater < connection)
                throw new ArgumentException("Bond cannot be above its high-water mark.", nameof(bonding));

            return new BabyState(name, timeCheckpoint, age, satiety, comfort, energy,
                toyFamiliarity, plushFamiliarity, canSitAndReach, canCrawl, connection, storySessions, cleanliness, health, newSoundResolved, nurseryTheme, canWalk, blockTowers,
                appearance ?? BabyAppearance.Default, care, development, bonding, journal);
        }

        internal BabyState(string name, DateTimeOffset timeCheckpoint, TimeSpan age,
            double satiety, double comfort, double energy, int toyFamiliarity, int plushFamiliarity,
            bool canSitAndReach, bool canCrawl, double connection, int storySessions, double cleanliness, double health,
            bool newSoundResolved, NurseryTheme nurseryTheme, bool canWalk, int blockTowers, BabyAppearance appearance,
            CareRecord care, DevelopmentRecord development = null, BondRecord bonding = null, JournalRecord journal = null)
        {
            Name = name;
            TimeCheckpoint = timeCheckpoint;
            Age = age;
            Satiety = satiety;
            Comfort = comfort;
            Energy = energy;
            Cleanliness = cleanliness;
            Health = health;
            Bond = connection;
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
            Development = development;
            Bonding = bonding;
            Journal = journal;
        }

        // Copy-with: every omitted argument keeps its current value, so adding a field cannot shift
        // positional arguments at call sites. Like the constructor, it trusts simulation rules to keep
        // values valid; Restore validates data from outside.
        internal BabyState With(DateTimeOffset? timeCheckpoint = null, TimeSpan? age = null,
            double? satiety = null, double? comfort = null, double? energy = null,
            int? toyFamiliarity = null, int? plushFamiliarity = null, bool? canSitAndReach = null, bool? canCrawl = null,
            double? bond = null, int? storySessions = null, double? cleanliness = null, double? health = null,
            bool? newSoundResolved = null, NurseryTheme? nurseryTheme = null, bool? canWalk = null, int? blockTowers = null,
            BabyAppearance appearance = null, CareRecord care = null, DevelopmentRecord development = null,
            BondRecord bonding = null, JournalRecord journal = null) =>
            new BabyState(Name, timeCheckpoint ?? TimeCheckpoint, age ?? Age,
                satiety ?? Satiety, comfort ?? Comfort, energy ?? Energy,
                toyFamiliarity ?? ToyFamiliarity, plushFamiliarity ?? PlushFamiliarity,
                canSitAndReach ?? CanSitAndReach, canCrawl ?? CanCrawl, bond ?? Bond,
                storySessions ?? StorySessions, cleanliness ?? Cleanliness, health ?? Health,
                newSoundResolved ?? NewSoundResolved, nurseryTheme ?? NurseryTheme, canWalk ?? CanWalk,
                blockTowers ?? BlockTowers, appearance ?? Appearance, care ?? Care, development ?? Development,
                bonding ?? Bonding, journal ?? Journal);

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
            if (!Enum.IsDefined(typeof(NurseryTheme), nurseryTheme))
                throw new ArgumentOutOfRangeException(nameof(nurseryTheme));
        }
    }
}
