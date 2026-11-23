using System;

namespace BAIby.Simulation
{
    // The baby's needs between two instants (docs/care-and-growth.md, Daily routine: need retune).
    //
    // - Awake hours: Fullness -7, Comfort -4, Energy -6, Cleanliness -9 per hour.
    // - Night (quiet hours, from the bedtime window's due time until the wake time, on the local clock):
    //   Fullness -2.5, Comfort -1, Cleanliness -5, and Energy +9 up to 100 after an on-time bedtime, 85 after
    //   a late one, and 65 without one (the baby dozes on the mat). Each night reads its own evening's bedtime
    //   rest, and time before the first care-day boundary of a gap is placed with the clock and routine of the
    //   day it belongs to (a new offset or routine applies from the next care day), so splitting a night into
    //   several advances changes nothing.
    // - Away mode holds needs: they fall at awake rates but never below the hold level, or below where they
    //   were if that was lower. Health holds too.
    // - Outside Away mode at most `cap` hours count, from the start of the gap.
    // - Health recovers 6/h while every need is at 60 or more and falls 5/h while any is below 30.
    internal static class BabyNeeds
    {
        public const double AwakeFullness = 7;
        public const double AwakeComfort = 4;
        public const double AwakeEnergy = 6;
        public const double AwakeCleanliness = 9;
        public const double NightFullness = 2.5;
        public const double NightComfort = 1;
        public const double NightEnergyGain = 9;
        public const double NightCleanliness = 5;
        public const double RestedNightEnergy = 100;
        public const double LateBedtimeNightEnergy = 85;
        public const double DozingNightEnergy = 65;
        private const double RecoveryThreshold = 60;
        private const double LowNeedThreshold = 30;
        private const double RecoveryPerHour = 6;
        private const double HealthLossPerHour = 5;

        internal struct Values
        {
            public double Satiety;
            public double Comfort;
            public double Energy;
            public double Cleanliness;
            public double Health;

            public double Lowest => Math.Min(Math.Min(Satiety, Comfort), Math.Min(Energy, Cleanliness));
        }

        public static Values From(BabyState state) => new Values
        {
            Satiety = state.Satiety,
            Comfort = state.Comfort,
            Energy = state.Energy,
            Cleanliness = state.Cleanliness,
            Health = state.Health,
        };

        // Preview days pass no real clock time, so they count as awake hours.
        public static Values Awake(Values start, TimeSpan elapsed, TimeSpan cap) =>
            Segment(start, Math.Min(elapsed.TotalHours, cap.TotalHours), false, 0);

        // Real time from `before.Checkpoint` to `after.Checkpoint`. `before` is the ledger at the start of the gap
        // (its away period); `after` is the ledger CareRules.Advance produced and holds the bedtime rest. Until the
        // first care-day boundary the gap is placed on `before`'s offset and routine, after it on `after`'s, as
        // CareRules.BeginDay switches them, so one advance and many short ones agree.
        public static Values Real(Values start, CareRecord before, CareRecord after, TimeSpan cap, double holdLevel,
            TimeSpan dayStart)
        {
            var from = before.Checkpoint.UtcTicks;
            var to = after.Checkpoint.UtcTicks;
            if (to <= from)
                return start;

            var boundary = from;
            if (!before.AwaitingAnchor && after.CareDay > before.CareDay)
            {
                // CareRules.Advance closes the day at this instant, under the offset it reads at `now`.
                boundary = RoutineCalendar.CareDayStart(before.CareDay + 1, after.NextOffset, dayStart);
                boundary = Math.Max(from, Math.Min(to, boundary));
            }

            var placing = new Placement(before, after, boundary);
            var awayFrom = Math.Max(from, before.AwayFrom.UtcTicks);
            var awayUntil = Math.Min(to, before.AwayUntil.UtcTicks);
            var budget = cap.Ticks;
            var values = start;
            if (awayUntil > awayFrom)
            {
                values = Placed(values, from, awayFrom, placing, ref budget);
                values = Held(values, Hours(awayUntil - awayFrom), holdLevel);
                return Placed(values, awayUntil, to, placing, ref budget);
            }

            return Placed(values, from, to, placing, ref budget);
        }

        private readonly struct Placement
        {
            public readonly CareRecord Before;
            public readonly CareRecord After;
            public readonly long Boundary;

            public Placement(CareRecord before, CareRecord after, long boundary)
            {
                Before = before;
                After = after;
                Boundary = boundary;
            }
        }

        private static Values Placed(Values values, long from, long to, Placement placing, ref long budget)
        {
            if (from < placing.Boundary)
            {
                var split = Math.Min(to, placing.Boundary);
                var before = placing.Before;
                values = Counted(values, from, split, before.Offset, RoutineCalendar.Night(before.Preset, before.WakeTime),
                    placing.After, ref budget);
                from = split;
            }

            if (from >= to)
                return values;

            var after = placing.After;
            return Counted(values, from, to, after.Offset, RoutineCalendar.Night(after.Preset, after.WakeTime), after,
                ref budget);
        }

        // A night belongs to the care day whose evening it follows; only that day's bedtime rest counts.
        private static double NightEnergyCap(CareRecord record, long nightDay)
        {
            if (record.BedtimeRestDay != nightDay)
                return DozingNightEnergy;

            switch (record.BedtimeRest)
            {
                case MomentStatus.OnTime: return RestedNightEnergy;
                case MomentStatus.Late: return LateBedtimeNightEnergy;
                default: return DozingNightEnergy;
            }
        }

        // `rest` holds the bedtime rest; `offset` and `night` place the stretch on the local clock.
        private static Values Counted(Values values, long from, long to, TimeSpan offset, (TimeSpan Start, TimeSpan End) night,
            CareRecord rest, ref long budget)
        {
            var at = from;
            while (at < to && budget > 0)
            {
                var timeOfDay = Mod(at + offset.Ticks, TimeSpan.TicksPerDay);
                var isNight = timeOfDay >= night.Start.Ticks || timeOfDay < night.End.Ticks;
                long boundary;
                if (!isNight)
                    boundary = night.Start.Ticks - timeOfDay;
                else if (timeOfDay >= night.Start.Ticks)
                    boundary = TimeSpan.TicksPerDay - timeOfDay + night.End.Ticks;
                else
                    boundary = night.End.Ticks - timeOfDay;

                var length = Math.Min(Math.Min(to - at, boundary), budget);
                // The morning part of a night belongs to the previous evening's care day.
                var nightDay = FloorDiv(at + offset.Ticks, TimeSpan.TicksPerDay) - (timeOfDay < night.End.Ticks ? 1 : 0);
                values = Segment(values, Hours(length), isNight, isNight ? NightEnergyCap(rest, nightDay) : 0);
                budget -= length;
                at += length;
            }

            return values;
        }

        private static Values Held(Values values, double hours, double hold)
        {
            return new Values
            {
                Satiety = HeldNeed(values.Satiety, AwakeFullness, hours, hold),
                Comfort = HeldNeed(values.Comfort, AwakeComfort, hours, hold),
                Energy = HeldNeed(values.Energy, AwakeEnergy, hours, hold),
                Cleanliness = HeldNeed(values.Cleanliness, AwakeCleanliness, hours, hold),
                Health = values.Health,
            };
        }

        private static double HeldNeed(double value, double rate, double hours, double hold) =>
            Math.Max(Math.Min(value, hold), value - rate * hours);

        // One stretch of constant rates. Energy rises at night (rate below zero) and never passes energyCap.
        private static Values Segment(Values start, double hours, bool night, double energyCap)
        {
            if (hours <= 0)
                return start;

            var fullness = night ? NightFullness : AwakeFullness;
            var comfort = night ? NightComfort : AwakeComfort;
            var cleanliness = night ? NightCleanliness : AwakeCleanliness;
            var energy = !night ? AwakeEnergy : start.Energy >= energyCap ? 0 : -NightEnergyGain;
            var rates = new[] { fullness, comfort, energy, cleanliness };
            var values = new[] { start.Satiety, start.Comfort, start.Energy, start.Cleanliness };

            var recovering = HoursAtOrAbove(values, rates, energyCap, RecoveryThreshold, hours);
            var deteriorating = hours - HoursAtOrAbove(values, rates, energyCap, LowNeedThreshold, hours);
            var recovered = Math.Min(100, start.Health + recovering * RecoveryPerHour);
            return new Values
            {
                Satiety = Math.Max(0, start.Satiety - fullness * hours),
                Comfort = Math.Max(0, start.Comfort - comfort * hours),
                Energy = energy < 0 ? Math.Min(energyCap, start.Energy - energy * hours) : Math.Max(0, start.Energy - energy * hours),
                Cleanliness = Math.Max(0, start.Cleanliness - cleanliness * hours),
                Health = Math.Max(0, recovered - deteriorating * HealthLossPerHour),
            };
        }

        // How long, within [0, hours], every need is at or above the threshold. Each need is monotonic within a
        // segment, so the set is one interval.
        private static double HoursAtOrAbove(double[] values, double[] rates, double ceiling, double threshold, double hours)
        {
            var low = 0.0;
            var high = hours;
            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                var rate = rates[i];
                if (rate > 0)
                {
                    if (value < threshold)
                        return 0;
                    high = Math.Min(high, (value - threshold) / rate);
                }
                else if (value < threshold)
                {
                    if (rate == 0 || ceiling < threshold)
                        return 0;
                    low = Math.Max(low, (threshold - value) / -rate);
                }
            }

            return Math.Max(0, high - low);
        }

        private static double Hours(long ticks) => (double)ticks / TimeSpan.TicksPerHour;

        private static long FloorDiv(long value, long divisor)
        {
            var quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        private static long Mod(long value, long divisor)
        {
            var remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
