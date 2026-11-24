using System;

namespace BAIby.Simulation
{
    // The household helpers (docs/monetization.md, Automation, care and consequences). Saved as numbers: append, never
    // renumber. For a baby all three are the Sitter, who pops in; for a cat they are objects (an auto-feeder, a ball
    // launcher and a self-cleaning litter box).
    public enum HelperKind { Feeder = 0, Play = 1, Tidy = 2 }

    // Helpers are a safety net, not a carer. Starting values for playtests; call Validate() after changing them.
    //
    // - A helper acts only when its need has fallen to its floor, and raises it to a level below full: the feeder serves
    //   one stocked portion (Fullness to FeederLevel), play gives a small top-up (Comfort to PlayLevel) and tidy restores
    //   Cleanliness to TidyLevel. Every floor sits above the low-need threshold of 30, where Health starts to fall, and
    //   above the Fussy threshold, and below the 60 that counts as well kept, so hands-on care always looks better.
    // - The feeder needs stock (the player stocks up to MaxStock portions); play and tidy act at most DailyLimit times a
    //   care day. Nothing restores Energy or Health: sleep and Health recovery stay free.
    // - Coverage: helpers act only within Coverage of the last hands-on care. After that the needs fall as without
    //   helpers (the normal capped decay), so a helper can hold a busy day steady but never stands in for a carer.
    public sealed class HelperRules
    {
        public static readonly HelperRules Default = new HelperRules();

        // The lowest floor a helper may have: the low-need threshold, below which Health falls.
        public const double LowNeed = 30;
        // The well-kept level: no floor reaches it.
        public const double WellKept = 60;

        public double FeederFloor { get; }
        public double FeederLevel { get; }
        public int MaxStock { get; }
        public double PlayFloor { get; }
        public double PlayLevel { get; }
        public double TidyFloor { get; }
        public double TidyLevel { get; }
        // Play and tidy act at most this many times a care day each.
        public int DailyLimit { get; }
        public TimeSpan Coverage { get; }

        public HelperRules(double feederFloor = 45, double feederLevel = 70, int maxStock = 6, double playFloor = 40,
            double playLevel = 58, double tidyFloor = 40, double tidyLevel = 70, int dailyLimit = 3, TimeSpan? coverage = null)
        {
            FeederFloor = feederFloor;
            FeederLevel = feederLevel;
            MaxStock = maxStock;
            PlayFloor = playFloor;
            PlayLevel = playLevel;
            TidyFloor = tidyFloor;
            TidyLevel = tidyLevel;
            DailyLimit = dailyLimit;
            Coverage = coverage ?? TimeSpan.FromHours(24);
        }

        public double FloorOf(HelperKind kind)
        {
            switch (kind)
            {
                case HelperKind.Feeder: return FeederFloor;
                case HelperKind.Play: return PlayFloor;
                case HelperKind.Tidy: return TidyFloor;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public double LevelOf(HelperKind kind)
        {
            switch (kind)
            {
                case HelperKind.Feeder: return FeederLevel;
                case HelperKind.Play: return PlayLevel;
                case HelperKind.Tidy: return TidyLevel;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        // `fussy` is the highest need level that makes a companion fussy in any care style; `fullMeal` the Fullness at
        // which feeding overfeeds.
        public void Validate(double fussy = 35, double fullMeal = 90)
        {
            foreach (var kind in new[] { HelperKind.Feeder, HelperKind.Play, HelperKind.Tidy })
            {
                var floor = FloorOf(kind);
                var level = LevelOf(kind);
                if (double.IsNaN(floor) || floor <= LowNeed || floor <= fussy || floor >= WellKept)
                    throw new ArgumentOutOfRangeException(kind + "Floor", "A floor sits above the low and fussy levels and below well kept.");
                if (double.IsNaN(level) || level <= floor || level >= 100 || level >= fullMeal)
                    throw new ArgumentOutOfRangeException(kind + "Level", "A helper tops up above its floor and below full.");
            }

            if (MaxStock < 1 || MaxStock > HelperRecord.StockLimit)
                throw new ArgumentOutOfRangeException(nameof(MaxStock));
            if (DailyLimit < 1 || DailyLimit > HelperRecord.CountLimit)
                throw new ArgumentOutOfRangeException(nameof(DailyLimit));
            if (Coverage <= TimeSpan.Zero || Coverage > TimeSpan.FromDays(2))
                throw new ArgumentOutOfRangeException(nameof(Coverage));
        }
    }

    // Which helpers may act: owned (an entitlement) and switched on in the household's settings. Plain data; the
    // simulation never knows about purchases.
    public sealed class HelperSettings : IEquatable<HelperSettings>
    {
        public static readonly HelperSettings None = new HelperSettings(false, false, false);
        public static readonly HelperSettings All = new HelperSettings(true, true, true);

        public bool Feeder { get; }
        public bool Play { get; }
        public bool Tidy { get; }
        public bool Any => Feeder || Play || Tidy;

        public HelperSettings(bool feeder, bool play, bool tidy)
        {
            Feeder = feeder;
            Play = play;
            Tidy = tidy;
        }

        public bool Has(HelperKind kind)
        {
            switch (kind)
            {
                case HelperKind.Feeder: return Feeder;
                case HelperKind.Play: return Play;
                case HelperKind.Tidy: return Tidy;
                default: return false;
            }
        }

        public HelperSettings With(HelperKind kind, bool on) =>
            new HelperSettings(kind == HelperKind.Feeder ? on : Feeder, kind == HelperKind.Play ? on : Play,
                kind == HelperKind.Tidy ? on : Tidy);

        public bool Equals(HelperSettings other) =>
            other != null && Feeder == other.Feeder && Play == other.Play && Tidy == other.Tidy;

        public override bool Equals(object obj) => Equals(obj as HelperSettings);
        public override int GetHashCode() => (Feeder ? 1 : 0) | (Play ? 2 : 0) | (Tidy ? 4 : 0);
    }

    // One helper activation, in time order. Not saved: the journal keeps a day's summary and the record its counts.
    public sealed class HelperEvent
    {
        public HelperKind Kind { get; }
        // When it acted. A preview day passes no real time, so its events are at the checkpoint.
        public DateTimeOffset At { get; }
        // How far into the advance (or the preview day) it acted, which places it on the companion's day.
        public TimeSpan Elapsed { get; }
        // The need before and after.
        public double Before { get; }
        public double After { get; }

        public HelperEvent(HelperKind kind, DateTimeOffset at, TimeSpan elapsed, double before, double after)
        {
            if (!Enum.IsDefined(typeof(HelperKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));

            Kind = kind;
            At = at;
            Elapsed = elapsed;
            Before = before;
            After = after;
        }

        public override string ToString() =>
            FormattableString.Invariant($"{Kind} at {At.UtcTicks} +{Elapsed.Ticks} {Before:0.###}->{After:0.###}");
    }
}
