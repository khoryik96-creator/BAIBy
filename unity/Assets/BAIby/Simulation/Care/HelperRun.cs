using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // The helpers stepping through one advance (or one preview day) beside BabyNeeds. Needs fall linearly between
    // instants where something changes, so the instant a need reaches its floor is known in closed form: BabyNeeds ends
    // a stretch there, Act raises the need, and the next stretch starts from the new value. An advance that is split into
    // many shorter ones finds the same instants, so helpers act the same either way.
    //
    // A helper acts at an instant when it is switched on, the need is at or below its floor, the instant is inside the
    // coverage window (Coverage after the last hands-on care), and it still may: the feeder has stock, play and tidy have
    // acted fewer than DailyLimit times on that care day. Only the needs it serves change; Energy, Health, Bond and
    // everything else are untouched.
    internal sealed class HelperRun
    {
        // A need within this much of its floor counts as at the floor (closed-form instants are rounded up to a tick).
        private const double AtFloor = 1e-9;
        private static readonly HelperKind[] Kinds = { HelperKind.Feeder, HelperKind.Play, HelperKind.Tidy };

        private readonly HelperSettings settings;
        private readonly HelperRules rules;
        private readonly long coverageEnd;
        private readonly long gapStart;
        private readonly TimeSpan dayStart;
        // A preview day passes no real time: every event is at the checkpoint, and the day's counts start again.
        private readonly bool preview;
        private int stock;
        private long day;
        private int feeds;
        private int plays;
        private int tidies;
        private long lastServe;
        // The preview day's care day, and whether its counts still have to start again at the first activation.
        private long previewDay;
        private bool fresh;

        private HelperRun(HelperSettings settings, HelperRules rules, HelperRecord record, long coverageEnd, long gapStart,
            TimeSpan dayStart, bool preview)
        {
            this.settings = settings;
            this.rules = rules;
            this.coverageEnd = coverageEnd;
            this.gapStart = gapStart;
            this.dayStart = dayStart;
            this.preview = preview;
            stock = record.Stock;
            day = record.Day;
            feeds = record.Feeds;
            plays = record.Plays;
            tidies = record.Tidies;
            lastServe = record.LastServe;
        }

        public List<HelperEvent> Events { get; } = new List<HelperEvent>();
        // When the feeder served, in order (UTC ticks of real advances).
        public List<long> Served { get; } = new List<long>();

        public HelperRecord Record => new HelperRecord(stock, day, feeds, plays, tidies, lastServe);

        // Null when no helper may act, so the needs follow exactly the rules without helpers.
        public static HelperRun Begin(CareRecord care, HelperSettings settings, HelperRules rules, TimeSpan dayStart)
        {
            if (care == null || settings == null || rules == null || !settings.Any || care.AwaitingAnchor)
                return null;

            var from = care.Checkpoint.UtcTicks;
            return new HelperRun(settings, rules, care.Helpers, Add(care.LastCareAt.UtcTicks, rules.Coverage.Ticks), from,
                dayStart, false);
        }

        // A preview day: helpers act through its hours when the checkpoint is inside the coverage window, as a new day.
        public static HelperRun BeginPreview(CareRecord care, HelperSettings settings, HelperRules rules, TimeSpan dayStart)
        {
            if (care == null || settings == null || rules == null || !settings.Any || care.AwaitingAnchor)
                return null;

            var at = care.Checkpoint.UtcTicks;
            var covered = at < Add(care.LastCareAt.UtcTicks, rules.Coverage.Ticks);
            var run = new HelperRun(settings, rules, care.Helpers, covered ? long.MaxValue : long.MinValue, at, dayStart, true);
            run.previewDay = RoutineCalendar.CareDayOf(at, care.Offset, dayStart);
            run.fresh = true;
            return run;
        }

        // Every helper that can act at `at` (real) or `elapsed` into a preview day raises its need.
        public BabyNeeds.Values Act(BabyNeeds.Values values, long at, TimeSpan offset, long elapsed)
        {
            if (at >= coverageEnd)
                return values;

            // The counts belong to the care day of the last activation; a new day starts them again.
            var today = preview ? previewDay : RoutineCalendar.CareDayOf(at, offset, dayStart);
            foreach (var kind in Kinds)
            {
                if (!CanAct(kind, today) || Need(values, kind) > rules.FloorOf(kind) + AtFloor)
                    continue;

                if (fresh || today != day)
                {
                    fresh = false;
                    day = today;
                    feeds = 0;
                    plays = 0;
                    tidies = 0;
                }

                var before = Need(values, kind);
                var after = Math.Max(before, rules.LevelOf(kind));
                values = With(values, kind, after);
                switch (kind)
                {
                    case HelperKind.Feeder:
                        stock--;
                        feeds = Math.Min(HelperRecord.CountLimit, feeds + 1);
                        if (!preview)
                        {
                            lastServe = at;
                            Served.Add(at);
                        }

                        break;
                    case HelperKind.Play:
                        plays++;
                        break;
                    default:
                        tidies++;
                        break;
                }

                Events.Add(new HelperEvent(kind, new DateTimeOffset(preview ? gapStart : at, TimeSpan.Zero),
                    TimeSpan.FromTicks(preview ? elapsed : at - gapStart), before, after));
            }

            return values;
        }

        // Ticks from `at` until a helper could next act in a stretch where Fullness, Comfort and Cleanliness fall at
        // these rates per hour (long.MaxValue when none can). A need already at its floor that may not act yet (the
        // daily limit) waits for the next care day; a preview day has no next day.
        public long Next(BabyNeeds.Values values, long at, TimeSpan offset, double fullness, double comfort,
            double cleanliness)
        {
            if (at >= coverageEnd)
                return long.MaxValue;

            var next = long.MaxValue;
            foreach (var kind in Kinds)
            {
                if (!settings.Has(kind) || (kind == HelperKind.Feeder && stock <= 0))
                    continue;

                var value = Need(values, kind);
                var floor = rules.FloorOf(kind);
                long wait;
                if (value > floor + AtFloor)
                {
                    var rate = kind == HelperKind.Feeder ? fullness : kind == HelperKind.Play ? comfort : cleanliness;
                    if (rate <= 0)
                        continue;
                    wait = (long)Math.Ceiling((value - floor) / rate * TimeSpan.TicksPerHour);
                }
                else if (preview)
                {
                    continue;
                }
                else
                {
                    // At the floor but out of uses today: the next care day.
                    var today = RoutineCalendar.CareDayOf(at, offset, dayStart);
                    wait = RoutineCalendar.CareDayStart(today + 1, offset, dayStart) - at;
                }

                wait = Math.Max(1, wait);
                if (at <= coverageEnd - wait)
                    next = Math.Min(next, wait);
            }

            return next;
        }

        private bool CanAct(HelperKind kind, long today)
        {
            if (!settings.Has(kind))
                return false;

            switch (kind)
            {
                case HelperKind.Feeder: return stock > 0;
                case HelperKind.Play: return fresh || today != day || plays < rules.DailyLimit;
                default: return fresh || today != day || tidies < rules.DailyLimit;
            }
        }

        private static double Need(BabyNeeds.Values values, HelperKind kind) =>
            kind == HelperKind.Feeder ? values.Satiety : kind == HelperKind.Play ? values.Comfort : values.Cleanliness;

        private static BabyNeeds.Values With(BabyNeeds.Values values, HelperKind kind, double value)
        {
            switch (kind)
            {
                case HelperKind.Feeder:
                    values.Satiety = value;
                    break;
                case HelperKind.Play:
                    values.Comfort = value;
                    break;
                default:
                    values.Cleanliness = value;
                    break;
            }

            return values;
        }

        private static long Add(long value, long amount) =>
            amount > 0 && value > long.MaxValue - amount ? long.MaxValue : value + amount;
    }
}
