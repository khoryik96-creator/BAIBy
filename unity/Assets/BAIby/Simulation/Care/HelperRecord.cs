using System;

namespace BAIby.Simulation
{
    // What a companion's helpers hold, kept in its care record: the feeder's stock, how often each helper acted on the
    // care day it last acted (play and tidy have a daily limit), and when the feeder last served (a meal window it
    // served in is Helped, not missed). Empty until a helper is stocked or acts, and an empty record is never saved,
    // so a household without helpers keeps its saves byte for byte. Immutable.
    public sealed class HelperRecord : IEquatable<HelperRecord>
    {
        // Bounds a saved record may hold; the tunables ask for less.
        public const int StockLimit = 12;
        public const int CountLimit = 999;
        public static readonly HelperRecord Empty = new HelperRecord(0, 0, 0, 0, 0, 0);

        // Feeder portions left.
        public int Stock { get; }
        // The care day the counts belong to (0 before any helper acted).
        public long Day { get; }
        public int Feeds { get; }
        public int Plays { get; }
        public int Tidies { get; }
        // UTC ticks of the feeder's last portion (0 for none).
        public long LastServe { get; }

        public bool IsEmpty => Stock == 0 && Day == 0 && Feeds == 0 && Plays == 0 && Tidies == 0 && LastServe == 0;

        public HelperRecord(int stock, long day, int feeds, int plays, int tidies, long lastServe)
        {
            if (stock < 0 || stock > StockLimit)
                throw new ArgumentOutOfRangeException(nameof(stock));
            if (day < 0)
                throw new ArgumentOutOfRangeException(nameof(day));
            if (feeds < 0 || feeds > CountLimit || plays < 0 || plays > CountLimit || tidies < 0 || tidies > CountLimit)
                throw new ArgumentOutOfRangeException(nameof(feeds));
            if (lastServe < 0)
                throw new ArgumentOutOfRangeException(nameof(lastServe));

            Stock = stock;
            Day = day;
            Feeds = feeds;
            Plays = plays;
            Tidies = tidies;
            LastServe = lastServe;
        }

        // Times a helper acted on `careDay` (0 when the counts belong to another day).
        public int CountOn(HelperKind kind, long careDay)
        {
            if (careDay != Day)
                return 0;

            switch (kind)
            {
                case HelperKind.Feeder: return Feeds;
                case HelperKind.Play: return Plays;
                case HelperKind.Tidy: return Tidies;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public HelperRecord WithStock(int stock) => new HelperRecord(stock, Day, Feeds, Plays, Tidies, LastServe);

        public bool Equals(HelperRecord other) =>
            other != null && Stock == other.Stock && Day == other.Day && Feeds == other.Feeds && Plays == other.Plays &&
            Tidies == other.Tidies && LastServe == other.LastServe;

        public override bool Equals(object obj) => Equals(obj as HelperRecord);
        public override int GetHashCode() => Stock ^ Day.GetHashCode() ^ LastServe.GetHashCode() ^ (Feeds << 8);
    }
}
