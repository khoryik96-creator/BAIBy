using System;
using System.Globalization;
using System.Text;

namespace BAIby.Simulation
{
    // Why the ledger last changed. Saved as numbers: append new codes; never renumber.
    public enum CareReason
    {
        None = 0,
        Started = 1,
        AwaitingAnchor = 2,
        GapExcused = 3,
        NewCareDay = 4,
        OffsetShiftScheduled = 5,
        TravelDayExcused = 6,
        AbsenceCovered = 7,
        WindowMissed = 8,
        MealOnTime = 9,
        MealLate = 10,
        MealTooFull = 11,
        MealOutsideWindow = 12,
        MealExcused = 13,
        BedtimeOnTime = 14,
        BedtimeLate = 15,
        BedtimeOutsideWindow = 16,
        BedtimeExcused = 17,
        TogetherCounted = 18,
        TogetherAlreadyCounted = 19,
        TogetherExcused = 20,
        NoRoutineTag = 21,
        RoutineScheduled = 22,
        StyleChanged = 23,
        AwayStarted = 24,
        AwayEnded = 25,
        WashOnTime = 26,
        WashLate = 27,
        WashExcused = 28,
        WashOutsideWindow = 29,
        DoctorVisited = 30,
        MedicineDose = 31,
        PlayOnTime = 32,
        PlayLate = 33,
        PlayExcused = 34,
        PlayOutsideWindow = 35,
        MealHelped = 36,
        WalkOnTime = 37,
        WalkLate = 38,
        WalkExcused = 39,
        WalkOutsideWindow = 40,
    }

    // Developer read-outs of the care ledger. Never shown to players.
    public static class CareDiagnostics
    {
        public static string Describe(CareReason reason)
        {
            switch (reason)
            {
                case CareReason.None: return "nothing yet";
                case CareReason.Started: return "ledger started, first day excused";
                case CareReason.AwaitingAnchor: return "migrated, waiting for the first advance";
                case CareReason.GapExcused: return "migration gap excused";
                case CareReason.NewCareDay: return "new care day";
                case CareReason.OffsetShiftScheduled: return "clock offset changes at the next care day";
                case CareReason.TravelDayExcused: return "travel day excused";
                case CareReason.AbsenceCovered: return "absence past the ceiling is covered";
                case CareReason.WindowMissed: return "a window was missed";
                case CareReason.MealOnTime: return "meal on time";
                case CareReason.MealLate: return "meal late";
                case CareReason.MealTooFull: return "too full, not a meal";
                case CareReason.MealOutsideWindow: return "meal outside any window";
                case CareReason.MealExcused: return "meal in an excused window";
                case CareReason.BedtimeOnTime: return "bedtime on time";
                case CareReason.BedtimeLate: return "bedtime late";
                case CareReason.BedtimeOutsideWindow: return "rest outside bedtime";
                case CareReason.BedtimeExcused: return "bedtime in an excused window";
                case CareReason.TogetherCounted: return "together time counted";
                case CareReason.TogetherAlreadyCounted: return "together time already counted today";
                case CareReason.TogetherExcused: return "together time on an excused day";
                case CareReason.NoRoutineTag: return "care outside the routine";
                case CareReason.RoutineScheduled: return "routine changes at the next care day";
                case CareReason.StyleChanged: return "care style changed";
                case CareReason.AwayStarted: return "away mode started";
                case CareReason.AwayEnded: return "away mode ended";
                case CareReason.WashOnTime: return "wash on time";
                case CareReason.WashLate: return "wash late";
                case CareReason.WashExcused: return "wash in an excused window";
                case CareReason.WashOutsideWindow: return "wash outside the window";
                case CareReason.DoctorVisited: return "visited the doctor";
                case CareReason.MedicineDose: return "medicine dose with a meal";
                case CareReason.PlayOnTime: return "play on time";
                case CareReason.PlayLate: return "play late";
                case CareReason.PlayExcused: return "play in an excused window";
                case CareReason.PlayOutsideWindow: return "play outside the window";
                case CareReason.MealHelped: return "a helper served the meal (counted as missed for Bond)";
                case CareReason.WalkOnTime: return "walk on time";
                case CareReason.WalkLate: return "walk late";
                case CareReason.WalkExcused: return "walk excused";
                case CareReason.WalkOutsideWindow: return "walk outside a walk window";
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }

        // One line for the Development panel, for example:
        // "Care day 2026-03-02 UTC+01:00 | today OPPP together | meals OOL (0.83) | together O | bed O | score 0.90 | excused 5 covered 0 | meal late"
        public static string Summary(CareRecord record, CareTunables tunables)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            if (tunables == null)
                throw new ArgumentNullException(nameof(tunables));

            var invariant = CultureInfo.InvariantCulture;
            var line = new StringBuilder();
            if (record.AwaitingAnchor)
                line.Append("Care day pending");
            else
                line.Append("Care day ").Append(RoutineCalendar.CalendarDate(record.CareDay).ToString("yyyy-MM-dd", invariant))
                    .Append(" UTC").Append(record.Offset < TimeSpan.Zero ? '-' : '+')
                    .Append(record.Offset.Duration().ToString(@"hh\:mm", invariant));

            if (record.NextOffset != record.Offset)
                line.Append(" (next ").Append(record.NextOffset < TimeSpan.Zero ? '-' : '+')
                    .Append(record.NextOffset.Duration().ToString(@"hh\:mm", invariant)).Append(')');
            if (record.Preset != RoutinePreset.SchoolDay || record.WakeTime != RoutineCalendar.DefaultWakeTime)
                line.Append(' ').Append(record.Preset).Append(" wake ").Append(record.WakeTime.ToString(@"hh\:mm", invariant));

            line.Append(" | today ").Append(record.MomentCode).Append(" wash ").Append("POLMEC"[(int)record.Wash])
                .Append(record.TogetherToday ? " together" : "")
                .Append(record.DayExcused ? " excused" : "")
                .Append(" | meals ").Append(Ring(record.Meals))
                .Append(" (").Append(record.Meals.Credit(tunables.MealRing, tunables.LateCredit).ToString("0.00", invariant)).Append(')')
                .Append(" | together ").Append(Ring(record.TogetherDays))
                .Append(" | bed ").Append(Ring(record.Bedtimes))
                .Append(" | score ").Append(CareRules.Score(record, tunables).ToString("0.00", invariant))
                .Append(" | excused ").Append(record.ExcusedCount.ToString(invariant))
                .Append(" covered ").Append(record.CoveredCount.ToString(invariant))
                .Append(" | ").Append(record.Mood).Append(record.MoodCause == CareMoodCause.None ? "" : " (" + record.MoodCause + ")")
                .Append(record.Owed == CareTags.None ? "" : " owed " + record.Owed)
                .Append(" mess ").Append(record.Mess.ToString(invariant))
                .Append(record.Style == CareStyle.Standard ? "" : " " + record.Style)
                .Append(record.IsAway ? " away" : "")
                .Append(record.Helpers.IsEmpty ? "" : string.Format(invariant, " | helpers stock {0} today {1}/{2}/{3}",
                    record.Helpers.Stock, record.Helpers.Feeds, record.Helpers.Plays, record.Helpers.Tidies))
                .Append(" | ").Append(record.Condition)
                .Append(record.ConditionCause == CareConditionCause.None ? "" : " (" + record.ConditionCause + ")")
                .Append(record.Condition == CareCondition.UnderTheWeather
                    ? " " + record.UnwellTime.TotalHours.ToString("0.0", invariant) + "h in " +
                      record.UnwellSessions.ToString(invariant) + " visits"
                    : "")
                .Append(record.Condition == CareCondition.Recovering
                    ? " doses " + record.MedicineDoses.ToString(invariant) + "/" + tunables.RecoveryDoses.ToString(invariant)
                    : "")
                .Append(" | ").Append(Describe(record.LastReason));
            return line.ToString();
        }

        private static string Ring(CareRing ring) => ring.Count == 0 ? "-" : ring.ToCode();
    }
}
