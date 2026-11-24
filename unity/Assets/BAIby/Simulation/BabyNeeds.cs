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
    // - Health recovers 6/h while every need is at 60 or more and falls 5/h while any is below 30, never below
    //   CareConsequences.HealthFloor (a save from before the floor keeps a lower Health but never loses more).
    // - The health condition at the start of the gap holds for the whole gap (the ladder only moves at an
    //   advance): under the weather or sick, Energy cannot rise past the unwell cap; recovering, Health recovers
    //   faster until a fixed instant after the doctor's visit, so the boost is the same in one advance or many.
    // - Pets share these rules through NeedRates: a cat's four meters (Fullness, Comfort, Energy, Cleanliness) fall at
    //   its species' and breed's rates instead of the baby's; the night, the bedtime rest, Away mode and Health work
    //   the same way.
    // - Helpers (HelperRun) end a counted stretch where a need reaches its floor and raise it there, inside the counted
    //   hours only (never while Away mode holds the needs). Without helpers nothing here changes.
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

        // How fast each need falls per hour, awake and at night, and how fast Energy comes back at night.
        internal readonly struct NeedRates
        {
            public static readonly NeedRates Baby = new NeedRates(AwakeFullness, AwakeComfort, AwakeEnergy, AwakeCleanliness,
                BabyNeeds.NightFullness, BabyNeeds.NightComfort, NightEnergyGain, BabyNeeds.NightCleanliness);

            public readonly double Fullness;
            public readonly double Comfort;
            public readonly double Energy;
            public readonly double Cleanliness;
            public readonly double NightFullness;
            public readonly double NightComfort;
            public readonly double NightEnergy;
            public readonly double NightCleanliness;

            public NeedRates(double fullness, double comfort, double energy, double cleanliness, double nightFullness,
                double nightComfort, double nightEnergy, double nightCleanliness)
            {
                Fullness = fullness;
                Comfort = comfort;
                Energy = energy;
                Cleanliness = cleanliness;
                NightFullness = nightFullness;
                NightComfort = nightComfort;
                NightEnergy = nightEnergy;
                NightCleanliness = nightCleanliness;
            }
        }

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
        public static Values Awake(Values start, TimeSpan elapsed, TimeSpan cap, double recoveryBoost = 1,
            double comfortFactor = 1) =>
            Awake(start, elapsed, cap, NeedRates.Baby, recoveryBoost, comfortFactor);

        public static Values Awake(Values start, TimeSpan elapsed, TimeSpan cap, NeedRates rates, double recoveryBoost = 1,
            double comfortFactor = 1) =>
            Segment(start, Math.Min(elapsed.TotalHours, cap.TotalHours), false, 0, recoveryBoost, rates, comfortFactor);

        // A preview day with helpers: the awake hours, ended where a helper acts.
        public static Values Awake(Values start, TimeSpan elapsed, TimeSpan cap, NeedRates rates, double recoveryBoost,
            double comfortFactor, HelperRun helpers)
        {
            if (helpers == null)
                return Awake(start, elapsed, cap, rates, recoveryBoost, comfortFactor);

            var total = Math.Min(elapsed.Ticks, cap.Ticks);
            var values = start;
            var at = 0L;
            while (at < total)
            {
                values = helpers.Act(values, 0, TimeSpan.Zero, at);
                var next = helpers.Next(values, 0, TimeSpan.Zero, rates.Fullness, rates.Comfort * comfortFactor,
                    rates.Cleanliness);
                var length = Math.Min(total - at, next);
                values = Segment(values, Hours(length), false, 0, recoveryBoost, rates, comfortFactor);
                at += length;
            }

            return values;
        }

        // What the condition at the start of a gap, and a clingy phase (which can begin or end when a care day closes
        // inside the gap), do to the needs over it. Shared by every companion with a care record.
        public static Effects EffectsFor(CareRecord care, BondStep bond, CareTunables careTunables, BondTunables bondTunables)
        {
            var ceiling = care.IsUnwell ? careTunables.UnwellEnergyCap : 100;
            var clingy = bond != null && bond.ClingyAtStart;
            var comfort = clingy ? bondTunables.ClingyComfort : 1;
            var switchAt = bond?.ClingyChangeAt ?? long.MaxValue;
            var after = switchAt == long.MaxValue ? comfort : clingy ? 1 : bondTunables.ClingyComfort;
            if (care.Condition != CareCondition.Recovering)
                return new Effects(ceiling, 1, long.MinValue, comfort, after, switchAt);

            var since = care.ConditionSince.UtcTicks;
            var window = careTunables.RecoveryWindow.Ticks;
            var until = since > long.MaxValue - window ? long.MaxValue : since + window;
            return new Effects(ceiling, careTunables.RecoveryBoost, until, comfort, after, switchAt);
        }

        // What the health condition does to the needs over a gap.
        public readonly struct Effects
        {
            public static readonly Effects None = new Effects(100, 1, long.MinValue);
            // Energy cannot rise past this.
            public readonly double EnergyCeiling;
            // Health recovers this many times faster before BoostUntil (UTC ticks).
            public readonly double RecoveryBoost;
            public readonly long BoostUntil;
            // Comfort falls this many times faster (a clingy phase), switching to ComfortAfter at ComfortSwitch (UTC
            // ticks) when a care day's close starts or ends the phase inside the gap.
            public readonly double Comfort;
            public readonly double ComfortAfter;
            public readonly long ComfortSwitch;

            public Effects(double energyCeiling, double recoveryBoost, long boostUntil, double comfort = 1,
                double comfortAfter = 1, long comfortSwitch = long.MaxValue)
            {
                EnergyCeiling = energyCeiling;
                RecoveryBoost = recoveryBoost;
                BoostUntil = boostUntil;
                Comfort = comfort;
                ComfortAfter = comfortAfter;
                ComfortSwitch = comfortSwitch;
            }
        }

        // Real time from `before.Checkpoint` to `after.Checkpoint`. `before` is the ledger at the start of the gap
        // (its away period); `after` is the ledger CareRules.Advance produced and holds the bedtime rest. Until the
        // first care-day boundary the gap is placed on `before`'s offset and routine, after it on `after`'s, as
        // CareRules.BeginDay switches them, so one advance and many short ones agree.
        public static Values Real(Values start, CareRecord before, CareRecord after, TimeSpan cap, double holdLevel,
            TimeSpan dayStart, Effects effects) =>
            Real(start, before, after, cap, holdLevel, dayStart, effects, NeedRates.Baby);

        public static Values Real(Values start, CareRecord before, CareRecord after, TimeSpan cap, double holdLevel,
            TimeSpan dayStart, Effects effects, NeedRates rates) =>
            Real(start, before, after, cap, holdLevel, dayStart, effects, rates, null);

        // The same with helpers acting through the counted hours; null helpers is exactly the rules without them.
        public static Values Real(Values start, CareRecord before, CareRecord after, TimeSpan cap, double holdLevel,
            TimeSpan dayStart, Effects effects, NeedRates rates, HelperRun helpers)
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

            var placing = new Placement(before, after, boundary, effects, rates, helpers);
            var awayFrom = Math.Max(from, before.AwayFrom.UtcTicks);
            var awayUntil = Math.Min(to, before.AwayUntil.UtcTicks);
            var budget = cap.Ticks;
            var values = start;
            if (awayUntil > awayFrom)
            {
                values = Placed(values, from, awayFrom, placing, ref budget);
                values = Held(values, Hours(awayUntil - awayFrom), holdLevel, rates);
                return Placed(values, awayUntil, to, placing, ref budget);
            }

            return Placed(values, from, to, placing, ref budget);
        }

        private readonly struct Placement
        {
            public readonly CareRecord Before;
            public readonly CareRecord After;
            public readonly long Boundary;
            public readonly Effects Effects;
            public readonly NeedRates Rates;
            public readonly HelperRun Helpers;

            public Placement(CareRecord before, CareRecord after, long boundary, Effects effects, NeedRates rates,
                HelperRun helpers)
            {
                Before = before;
                After = after;
                Boundary = boundary;
                Effects = effects;
                Rates = rates;
                Helpers = helpers;
            }
        }

        private static Values Placed(Values values, long from, long to, Placement placing, ref long budget)
        {
            if (from < placing.Boundary)
            {
                var split = Math.Min(to, placing.Boundary);
                var before = placing.Before;
                values = Counted(values, from, split, before.Offset,
                    RoutineCalendar.Night(before.Routine, before.Preset, before.WakeTime), placing.After, placing.Effects,
                    placing.Rates, placing.Helpers, ref budget);
                from = split;
            }

            if (from >= to)
                return values;

            var after = placing.After;
            return Counted(values, from, to, after.Offset, RoutineCalendar.Night(after.Routine, after.Preset, after.WakeTime),
                after, placing.Effects, placing.Rates, placing.Helpers, ref budget);
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
            CareRecord rest, Effects effects, NeedRates rates, HelperRun helpers, ref long budget)
        {
            var at = from;
            while (at < to && budget > 0)
            {
                if (helpers != null)
                    values = helpers.Act(values, at, offset, 0);
                var timeOfDay = Mod(at + offset.Ticks, TimeSpan.TicksPerDay);
                var isNight = timeOfDay >= night.Start.Ticks || timeOfDay < night.End.Ticks;
                long boundary;
                if (!isNight)
                    boundary = night.Start.Ticks - timeOfDay;
                else if (timeOfDay >= night.Start.Ticks)
                    boundary = TimeSpan.TicksPerDay - timeOfDay + night.End.Ticks;
                else
                    boundary = night.End.Ticks - timeOfDay;

                // The recovery boost ends at a fixed instant, so a stretch never straddles it.
                var boosted = at < effects.BoostUntil;
                if (boosted)
                    boundary = Math.Min(boundary, effects.BoostUntil - at);
                // So is the instant a clingy phase begins or ends.
                var beforeSwitch = at < effects.ComfortSwitch;
                if (beforeSwitch)
                    boundary = Math.Min(boundary, effects.ComfortSwitch - at);
                // And so is the instant a helper acts.
                if (helpers != null)
                    boundary = Math.Min(boundary, helpers.Next(values, at, offset, isNight ? rates.NightFullness : rates.Fullness,
                        (isNight ? rates.NightComfort : rates.Comfort) * (beforeSwitch ? effects.Comfort : effects.ComfortAfter),
                        isNight ? rates.NightCleanliness : rates.Cleanliness));
                var length = Math.Min(Math.Min(to - at, boundary), budget);
                // The morning part of a night belongs to the previous evening's care day.
                var nightDay = FloorDiv(at + offset.Ticks, TimeSpan.TicksPerDay) - (timeOfDay < night.End.Ticks ? 1 : 0);
                var energyCap = isNight ? Math.Min(NightEnergyCap(rest, nightDay), effects.EnergyCeiling) : 0;
                values = Segment(values, Hours(length), isNight, energyCap, boosted ? effects.RecoveryBoost : 1, rates,
                    beforeSwitch ? effects.Comfort : effects.ComfortAfter);
                budget -= length;
                at += length;
            }

            return values;
        }

        private static Values Held(Values values, double hours, double hold, NeedRates rates)
        {
            return new Values
            {
                Satiety = HeldNeed(values.Satiety, rates.Fullness, hours, hold),
                Comfort = HeldNeed(values.Comfort, rates.Comfort, hours, hold),
                Energy = HeldNeed(values.Energy, rates.Energy, hours, hold),
                Cleanliness = HeldNeed(values.Cleanliness, rates.Cleanliness, hours, hold),
                Health = values.Health,
            };
        }

        private static double HeldNeed(double value, double rate, double hours, double hold) =>
            Math.Max(Math.Min(value, hold), value - rate * hours);

        // One stretch of constant rates. Energy rises at night (rate below zero) and never passes energyCap.
        private static Values Segment(Values start, double hours, bool night, double energyCap, double recoveryBoost,
            NeedRates r, double comfortFactor = 1)
        {
            if (hours <= 0)
                return start;

            var fullness = night ? r.NightFullness : r.Fullness;
            var comfort = (night ? r.NightComfort : r.Comfort) * comfortFactor;
            var cleanliness = night ? r.NightCleanliness : r.Cleanliness;
            var energy = !night ? r.Energy : start.Energy >= energyCap ? 0 : -r.NightEnergy;
            var rates = new[] { fullness, comfort, energy, cleanliness };
            var values = new[] { start.Satiety, start.Comfort, start.Energy, start.Cleanliness };

            // Health changes in time order: loss while a need is low before the safe stretch, recovery inside
            // it (RecoveryThreshold is above LowNeedThreshold, so recovery never falls outside), loss after it.
            // Applying them in order keeps one long advance equal to many short ones.
            var safe = WindowAtOrAbove(values, rates, energyCap, LowNeedThreshold, hours);
            var recovery = WindowAtOrAbove(values, rates, energyCap, RecoveryThreshold, hours);
            var floor = Math.Min(start.Health, CareConsequences.HealthFloor);
            var health = Math.Max(floor, start.Health - safe.Low * HealthLossPerHour);
            health = Math.Min(100, health + (recovery.High - recovery.Low) * RecoveryPerHour * recoveryBoost);
            health = Math.Max(floor, health - (hours - safe.High) * HealthLossPerHour);
            return new Values
            {
                Satiety = Math.Max(0, start.Satiety - fullness * hours),
                Comfort = Math.Max(0, start.Comfort - comfort * hours),
                Energy = energy < 0 ? Math.Min(energyCap, start.Energy - energy * hours) : Math.Max(0, start.Energy - energy * hours),
                Cleanliness = Math.Max(0, start.Cleanliness - cleanliness * hours),
                Health = health,
            };
        }

        // When, within [0, hours], every need is at or above the threshold. Each need is monotonic within a
        // segment, so the set is one interval; an empty set is (hours, hours).
        private static (double Low, double High) WindowAtOrAbove(double[] values, double[] rates, double ceiling,
            double threshold, double hours)
        {
            var empty = (hours, hours);
            var low = 0.0;
            var high = hours;
            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                var rate = rates[i];
                if (rate > 0)
                {
                    if (value < threshold)
                        return empty;
                    high = Math.Min(high, (value - threshold) / rate);
                }
                else if (value < threshold)
                {
                    if (rate == 0 || ceiling < threshold)
                        return empty;
                    low = Math.Max(low, (threshold - value) / -rate);
                }
            }

            return high > low ? (low, high) : empty;
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
