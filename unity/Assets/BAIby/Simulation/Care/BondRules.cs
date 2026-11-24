using System;
using System.Collections.Generic;

namespace BAIby.Simulation
{
    // What time did to the relationship over one gap: the Bond, the record, the care days counted (instants, for the
    // journal's weekly pages), new stickers, and the first instant the clingy phase began or ended (long.MaxValue for
    // none) so the needs can follow it.
    public sealed class BondStep
    {
        public double Bond { get; internal set; }
        public BondRecord Record { get; internal set; }
        public List<long> CountedDays { get; } = new List<long>();
        public List<(long At, int Days)> Stickers { get; } = new List<(long At, int Days)>();
        public bool ClingyAtStart { get; internal set; }
        public long ClingyChangeAt { get; internal set; } = long.MaxValue;
    }

    // Bond, temperament and the care rhythm (docs/care-and-growth.md, Bond and temperament, Return loops). Pure and
    // species-agnostic; no randomness.
    //
    // - Bond gains (on-time meal, soothe, story, play, answering fussing or upset in time) count against a daily cap
    //   of raw gains, and count double below the high-water mark. Losses (a missed meal, every two hours of fussing,
    //   every three hours of upset, a helper day) count against a daily cap, smaller on a helper day. Bond never falls
    //   below a share of its high-water mark.
    // - Time losses follow the ledger's events and the mood at the start of the gap (the ladder only moves at an
    //   advance), ordered by care day and instant, so one long advance and many short ones agree while the mood holds.
    // - Temperament: when a care day closes, each axis moves a fifth of the way to that day's target. Covered, away
    //   and excused days leave it (a helper day takes a little Security).
    // - Care rhythm: a day counts with two meals, together time and a bedtime. A missed day halves it, once a week a
    //   grace day keeps it, and covered, away and excused days freeze it.
    public static class BondRules
    {
        private const int MealBit = 1 << (int)EngagementKind.Meal;
        private const int HygieneBit = 1 << (int)EngagementKind.Hygiene;

        // A new relationship (or one migrated from Connection): the high-water mark is the Bond itself, temperament is
        // neutral and the rhythm starts at 0.
        public static BondRecord Seed(double bond, CareRecord care)
        {
            if (care == null)
                throw new ArgumentNullException(nameof(care));

            return new BondRecord.Builder
            {
                HighWater = Clamp(bond, 0, 100),
                CapDay = care.CareDay,
                TallyDay = care.CareDay,
            }.Build();
        }

        public static SecurityPhase SecurityOf(BondRecord record, BondTunables tunables)
        {
            if (record == null)
                return SecurityPhase.Steady;

            return record.Security <= tunables.Clingy ? SecurityPhase.Clingy
                : record.Security >= tunables.Settled ? SecurityPhase.Settled : SecurityPhase.Steady;
        }

        public static CuriosityPhase CuriosityOf(BondRecord record, BondTunables tunables)
        {
            if (record == null)
                return CuriosityPhase.Steady;

            return record.Curiosity <= tunables.Cautious ? CuriosityPhase.Cautious
                : record.Curiosity >= tunables.Explorer ? CuriosityPhase.Explorer : CuriosityPhase.Steady;
        }

        // Engagement points x1.05 from a 3-day rhythm and x1.10 from a 7-day one.
        public static double RhythmBonus(BondRecord record, BondTunables tunables)
        {
            if (record == null)
                return 1;
            if (record.Rhythm >= tunables.RhythmBonusHighDays)
                return tunables.RhythmBonusHigh;

            return record.Rhythm >= tunables.RhythmBonusDays ? tunables.RhythmBonus : 1;
        }

        // Stickers come at 3 and 7 days of rhythm and then every 7 days; they are kept forever.
        public static bool IsSticker(int days) => days == 3 || (days >= 7 && days % 7 == 0);

        public static List<int> Stickers(int longestRhythm)
        {
            var stickers = new List<int>();
            for (var days = 3; days <= longestRhythm && stickers.Count < 1000; days = days < 7 ? 7 : days + 7)
                stickers.Add(days);
            return stickers;
        }

        // The next sticker after a rhythm.
        public static int NextSticker(int longestRhythm) => longestRhythm < 3 ? 3 : longestRhythm < 7 ? 7 : (longestRhythm / 7 + 1) * 7;

        // Bond never falls below this.
        public static double Floor(BondRecord record, BondTunables tunables) =>
            record == null ? 0 : record.HighWater * tunables.Floor;

        // A gain from care at a care day: capped per day on the raw amount, doubled below the high-water mark.
        public static (double Bond, BondRecord Record) Gain(double bond, BondRecord record, long careDay, double raw,
            BondTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));
            if (double.IsNaN(raw) || raw <= 0)
                return (bond, record);

            var draft = new Draft(bond, record);
            draft.Gain(raw, careDay, tunables);
            return (draft.Bond, draft.Record.Build());
        }

        // Counts one care action towards the day's tallies: a meal, together time, bedtime rest, fussing answered in
        // time, and a kind of play-like care.
        public static BondRecord Tally(BondRecord record, long careDay, bool meal, bool together, bool rest, bool answered,
            EngagementKind? kind)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var draft = record.ToBuilder();
            if (draft.TallyDay != careDay)
            {
                draft.TallyDay = careDay;
                draft.Meals = 0;
                draft.Together = false;
                draft.Rested = false;
                draft.Answered = 0;
                draft.Kinds = 0;
            }

            if (meal && draft.Meals < 100)
                draft.Meals++;
            draft.Together |= together;
            draft.Rested |= rest;
            if (answered && draft.Answered < 100)
                draft.Answered++;
            if (kind.HasValue)
                draft.Kinds |= 1 << (int)kind.Value;
            var next = draft.Build();
            return next.Equals(record) ? record : next;
        }

        // Real time from `before.Checkpoint` to `until` (UTC ticks). `ledger` holds what CareRules.Advance resolved over
        // that gap (or a longer one: later events are ignored), so a growth sample inside the gap sees the Bond a
        // shorter advance would have reached.
        public static BondStep Advance(double bond, BondRecord record, CareRecord before, IReadOnlyList<LedgerEvent> ledger,
            long until, BondTunables tunables, CareTunables care)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (before == null)
                throw new ArgumentNullException(nameof(before));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));
            if (care == null)
                throw new ArgumentNullException(nameof(care));

            var step = new BondStep { Bond = bond, Record = record, ClingyAtStart = SecurityOf(record, tunables) == SecurityPhase.Clingy };
            var items = new List<Item>();
            if (ledger != null)
            {
                foreach (var entry in ledger)
                {
                    if (entry.At <= until)
                        items.Add(new Item(entry.CareDay, entry.At, Rank(entry.Kind), entry, 0));
                }
            }

            AddMoodLosses(items, before, until, tunables, care);
            if (items.Count == 0)
                return step;

            items.Sort((a, b) => a.CareDay != b.CareDay ? a.CareDay.CompareTo(b.CareDay)
                : a.At != b.At ? a.At.CompareTo(b.At) : a.Rank.CompareTo(b.Rank));
            var draft = new Draft(bond, record);
            var clingy = step.ClingyAtStart;
            foreach (var item in items)
            {
                switch (item.Rank)
                {
                    case 0:
                        draft.Lose(tunables.MissedMealLoss, item.CareDay, tunables);
                        break;
                    case 1:
                        draft.Lose(item.Loss, item.CareDay, tunables);
                        break;
                    case 2:
                        CloseDay(draft, item.Event, tunables, step);
                        break;
                    default:
                        HelperDays(draft, item.Event, until, tunables, step, ref clingy);
                        break;
                }

                var now = draft.Record.Security <= tunables.Clingy;
                if (now != clingy)
                {
                    clingy = now;
                    if (step.ClingyChangeAt == long.MaxValue && item.Rank == 2)
                        step.ClingyChangeAt = item.At;
                }
            }

            step.Bond = draft.Bond;
            var built = draft.Record.Build();
            step.Record = built.Equals(record) ? record : built;
            return step;
        }

        // A preview day passes no real time, so no window closes: it counts for the rhythm when the care since the last
        // day mark would make a care day, and freezes it otherwise. Temperament follows the day's tallies (nothing is
        // missed), and the day's caps and tallies start again.
        public static BondStep PreviewDay(double bond, BondRecord record, long at, BondTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));

            var step = new BondStep { Bond = bond, ClingyAtStart = SecurityOf(record, tunables) == SecurityPhase.Clingy };
            var draft = new Draft(bond, record);
            var r = draft.Record;
            var cared = r.Meals > 0 || r.Together || r.Rested || r.Kinds != 0;
            if (cared)
                Drift(r, r.Answered, r.Kinds, 0, tunables);
            Rhythm(r, r.Meals >= 2 && r.Together && r.Rested, true, r.TallyDay, at, tunables, step);
            r.Meals = 0;
            r.Together = false;
            r.Rested = false;
            r.Answered = 0;
            r.Kinds = 0;
            r.Gained = 0;
            r.Lost = 0;
            r.HelperDay = false;
            step.Record = r.Build();
            if ((SecurityOf(step.Record, tunables) == SecurityPhase.Clingy) != step.ClingyAtStart)
                step.ClingyChangeAt = at;
            return step;
        }

        private static void CloseDay(Draft draft, LedgerEvent closed, BondTunables tunables, BondStep step)
        {
            var day = closed.Day;
            var r = draft.Record;
            draft.Roll(closed.CareDay);
            if (day.HelperCovered)
            {
                r.HelperDay = true;
                draft.Lose(tunables.HelperLoss, closed.CareDay, tunables);
                r.Security = Clamp(r.Security - tunables.HelperSecurity, BondRecord.AxisMin, BondRecord.AxisMax);
            }

            var qualifies = day.MealsDone >= 2 && day.Together && day.Bedtime;
            var frozen = day.Away || day.HelperCovered || day.Excused;
            if (!frozen)
            {
                var tallied = r.TallyDay == closed.CareDay;
                Drift(r, tallied ? r.Answered : 0, tallied ? r.Kinds : 0, day.MealsMissed, tunables);
            }

            Rhythm(r, qualifies, frozen, closed.CareDay, closed.At, tunables, step);
        }

        // Each whole day a helper covers: Bond -2 (within the helper cap, never below the floor) and Security -1.
        // Only the days that have ended by `until` count.
        private static void HelperDays(Draft draft, LedgerEvent helper, long until, BondTunables tunables, BondStep step,
            ref bool clingy)
        {
            var count = helper.Count;
            if (until < helper.At || count <= 0)
                return;

            var ended = (until - helper.At) / TimeSpan.TicksPerDay + 1;
            var days = (int)Math.Min(count, ended);
            var r = draft.Record;
            for (var i = 0; i < days; i++)
            {
                var careDay = helper.CareDay + i;
                if (draft.Bond <= Floor(draft, tunables) && r.Security <= BondRecord.AxisMin && i < days - 1)
                {
                    // Nothing more can fall; only the cap day moves on.
                    careDay = helper.CareDay + days - 1;
                    i = days - 1;
                }

                draft.Roll(careDay);
                r.HelperDay = true;
                draft.Lose(tunables.HelperLoss, careDay, tunables);
                r.Security = Clamp(r.Security - tunables.HelperSecurity, BondRecord.AxisMin, BondRecord.AxisMax);
                var now = r.Security <= tunables.Clingy;
                if (now != clingy)
                {
                    clingy = now;
                    if (step.ClingyChangeAt == long.MaxValue)
                        step.ClingyChangeAt = helper.At + i * TimeSpan.TicksPerDay;
                }
            }
        }

        private static void Drift(BondRecord.Builder r, int answered, int kinds, int missedMeals, BondTunables tunables)
        {
            var security = tunables.SecurityBase + tunables.SecurityPerAnswer * Math.Min(answered, tunables.AnsweredCap) -
                tunables.SecurityPerMiss * missedMeals;
            var curiosity = tunables.CuriosityBase + tunables.CuriosityPerKind * Count(kinds & ~(MealBit | HygieneBit));
            security = Clamp(security, BondRecord.AxisMin, BondRecord.AxisMax);
            curiosity = Clamp(curiosity, BondRecord.AxisMin, BondRecord.AxisMax);
            r.Security = Clamp(r.Security + (security - r.Security) * tunables.Drift, BondRecord.AxisMin, BondRecord.AxisMax);
            r.Curiosity = Clamp(r.Curiosity + (curiosity - r.Curiosity) * tunables.Drift, BondRecord.AxisMin, BondRecord.AxisMax);
        }

        private static void Rhythm(BondRecord.Builder r, bool qualifies, bool frozen, long careDay, long at,
            BondTunables tunables, BondStep step)
        {
            if (qualifies)
            {
                if (r.Rhythm < int.MaxValue)
                    r.Rhythm++;
                if (r.TotalCareDays < int.MaxValue)
                    r.TotalCareDays++;
                step.CountedDays.Add(at);
                if (r.Rhythm > r.LongestRhythm)
                {
                    r.LongestRhythm = r.Rhythm;
                    if (IsSticker(r.LongestRhythm))
                        step.Stickers.Add((at, r.LongestRhythm));
                }

                return;
            }

            if (frozen || r.Rhythm == 0)
                return;
            if (r.GraceDay == 0 || careDay - r.GraceDay >= tunables.GraceEvery)
                r.GraceDay = careDay;
            else
                r.Rhythm /= 2;
        }

        // Every FussyEvery of fussing and every UpsetEvery of upset since the mood began, counted only before the
        // absence ceiling and outside Away mode. The mood holds for the whole gap.
        private static void AddMoodLosses(List<Item> items, CareRecord before, long until, BondTunables tunables,
            CareTunables care)
        {
            if (before.AwaitingAnchor || before.Mood == CareMood.Content)
                return;

            var period = (before.Mood == CareMood.Fussy ? tunables.FussyEvery : tunables.UpsetEvery).Ticks;
            var loss = before.Mood == CareMood.Fussy ? tunables.FussyLoss : tunables.UpsetLoss;
            var start = before.MoodSince.UtcTicks;
            var from = before.Checkpoint.UtcTicks;
            var end = Math.Min(until, CareRules.AbsenceEnd(before, care));
            if (end <= from || loss <= 0)
                return;

            var k = (from - start) / period + 1;
            var awayFrom = before.AwayFrom.UtcTicks;
            var awayUntil = before.AwayUntil.UtcTicks;
            for (var at = start + k * period; at <= end && at > from; at += period)
            {
                if (at >= awayFrom && at < awayUntil)
                    continue;
                items.Add(new Item(RoutineCalendar.CareDayOf(at, before.NextOffset, care.DayStart), at, 1, default, loss));
            }
        }

        private static int Rank(LedgerEventKind kind)
        {
            switch (kind)
            {
                case LedgerEventKind.MealMissed: return 0;
                case LedgerEventKind.DayClosed: return 2;
                default: return 3;
            }
        }

        private static double Floor(Draft draft, BondTunables tunables) => draft.Record.HighWater * tunables.Floor;

        private static int Count(int bits)
        {
            var count = 0;
            for (; bits != 0; bits &= bits - 1)
                count++;
            return count;
        }

        private static double Clamp(double value, double min, double max) =>
            double.IsNaN(value) ? min : Math.Max(min, Math.Min(max, value));

        private readonly struct Item
        {
            public readonly long CareDay;
            public readonly long At;
            // 0 missed meal, 1 mood loss, 2 closed day, 3 helper days.
            public readonly int Rank;
            public readonly LedgerEvent Event;
            public readonly double Loss;

            public Item(long careDay, long at, int rank, LedgerEvent ledgerEvent, double loss)
            {
                CareDay = careDay;
                At = at;
                Rank = rank;
                Event = ledgerEvent;
                Loss = loss;
            }
        }

        // The Bond value and a record draft, with the daily caps.
        private sealed class Draft
        {
            public double Bond;
            public readonly BondRecord.Builder Record;

            public Draft(double bond, BondRecord record)
            {
                Bond = Clamp(bond, 0, 100);
                Record = record.ToBuilder();
            }

            public void Roll(long careDay)
            {
                if (Record.CapDay == careDay)
                    return;
                Record.CapDay = careDay;
                Record.Gained = 0;
                Record.Lost = 0;
                Record.HelperDay = false;
            }

            public void Gain(double raw, long careDay, BondTunables tunables)
            {
                Roll(careDay);
                var allowed = Math.Max(0, tunables.DailyGain - Record.Gained);
                raw = Math.Min(raw, allowed);
                if (raw <= 0)
                    return;

                Record.Gained += raw;
                var bond = Bond;
                var high = Record.HighWater;
                if (bond < high)
                {
                    // Gains count double until the high-water mark, then as usual.
                    var toHigh = (high - bond) / tunables.BelowHighWater;
                    bond = raw <= toHigh ? bond + raw * tunables.BelowHighWater : high + (raw - toHigh);
                }
                else
                {
                    bond += raw;
                }

                Bond = Math.Min(100, bond);
                Record.HighWater = Math.Max(Record.HighWater, Bond);
            }

            public void Lose(double amount, long careDay, BondTunables tunables)
            {
                Roll(careDay);
                var cap = (Record.HelperDay ? tunables.HelperDailyLoss : tunables.DailyLoss) - Record.Lost;
                var take = Math.Min(amount, cap);
                if (take <= 0)
                    return;

                var floor = Record.HighWater * tunables.Floor;
                var next = Math.Max(floor, Bond - take);
                if (next >= Bond)
                    return;

                Record.Lost += Bond - next;
                Bond = next;
            }
        }
    }
}
