using System;
using System.Diagnostics;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BAIby.Infrastructure
{
    // The player's local time. Elapsed time uses the UTC instant and the offset only places care windows.
    //
    // .NET and Mono cache the local time zone for the life of the process, so a player who flies with the
    // app in the background would keep the departure zone. The cache is dropped on the first read, at most
    // once per RefreshInterval of running time, and whenever the wall clock moved by RefreshInterval or more
    // since the last drop (a resume after sleep). The Web build asks the browser instead, since WebGL
    // reports UTC.
    public sealed class LocalClock
    {
        public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MaxOffset = TimeSpan.FromHours(14);

        private readonly TimeSpan refreshInterval;
        private readonly Func<DateTime> utcNow;
        private readonly Func<TimeSpan> running;
        private bool refreshed;
        private DateTime lastRefreshUtc;
        private TimeSpan lastRefreshRunning;

        public LocalClock()
            : this(DefaultRefreshInterval)
        {
        }

        public LocalClock(TimeSpan refreshInterval)
            : this(refreshInterval, () => DateTime.UtcNow, Stopwatch.StartNew())
        {
        }

        internal LocalClock(TimeSpan refreshInterval, Func<DateTime> utcNow, Func<TimeSpan> running)
        {
            if (refreshInterval < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(refreshInterval), "The refresh interval cannot be negative.");

            this.refreshInterval = refreshInterval;
            this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            this.running = running ?? throw new ArgumentNullException(nameof(running));
        }

        private LocalClock(TimeSpan refreshInterval, Func<DateTime> utcNow, Stopwatch stopwatch)
            : this(refreshInterval, utcNow, () => stopwatch.Elapsed)
        {
        }

        // How many times the cached zone was dropped; checks use it to see the throttle.
        internal int Refreshes { get; private set; }

        public DateTimeOffset Now()
        {
            var utc = DateTime.SpecifyKind(utcNow(), DateTimeKind.Utc);
            var elapsed = running();
            if (!refreshed || elapsed - lastRefreshRunning >= refreshInterval ||
                (utc - lastRefreshUtc).Duration() >= refreshInterval)
            {
                DropCachedZone();
                refreshed = true;
                lastRefreshUtc = utc;
                lastRefreshRunning = elapsed;
                Refreshes++;
            }

            return new DateTimeOffset(utc).ToOffset(LocalOffset(utc));
        }

        // Places a UTC instant in the zone of the last refresh. Offsets outside the DateTimeOffset range or
        // not in whole minutes fall back to UTC rather than failing the session.
        private static TimeSpan LocalOffset(DateTime utc)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var offset = TimeSpan.FromMinutes(BAIbyLocalOffsetMinutes());
#else
            var offset = TimeZoneInfo.Local.GetUtcOffset(utc);
#endif
            return offset.Duration() <= MaxOffset && offset.Ticks % TimeSpan.TicksPerMinute == 0 ? offset : TimeSpan.Zero;
        }

        private static void DropCachedZone()
        {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            TimeZoneInfo.ClearCachedData();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // LocalClock.jslib: minutes east of UTC for the browser's current local time.
        [DllImport("__Internal")]
        private static extern int BAIbyLocalOffsetMinutes();
#endif
    }
}
