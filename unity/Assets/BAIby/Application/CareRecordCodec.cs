using System;
using System.IO;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    // One <care v="3"> element shared by every companion's save. Invalid data throws InvalidDataException; a
    // newer version throws NotSupportedException so the session leaves that save untouched.
    //
    // v2 adds the consequence state: wash, mess, mood, owed cores, care style and Away mode. A v1 record (the
    // shadow ledger, which nobody saw) migrates without surprises: its rings are cleared, and it waits for an
    // anchor again, so the first advance excuses the whole gap and moves nobody on the ladder.
    //
    // v3 adds the health axis: the condition (well, under the weather, sick, recovering), its cause and start, the
    // counted time and visits under the weather, the current visit's start, medicine doses and the last good care
    // day. Older records decode as well, with nothing counted, and their current visit starting at the checkpoint.
    internal static class CareRecordCodec
    {
        public const string ElementName = "care";
        private const int Version = 3;
        private const int VersionBeforeConsequences = 1;
        private const int VersionBeforeHealth = 2;

        public static XElement Encode(CareRecord care)
        {
            if (care == null)
                throw new ArgumentNullException(nameof(care));

            return new XElement(ElementName,
                new XAttribute("v", Version),
                new XAttribute("checkpoint", care.Checkpoint.UtcTicks),
                new XAttribute("offset", (int)care.Offset.TotalMinutes),
                new XAttribute("next-offset", (int)care.NextOffset.TotalMinutes),
                new XAttribute("day", care.CareDay),
                new XAttribute("preset", (int)care.Preset),
                new XAttribute("wake", (int)care.WakeTime.TotalMinutes),
                new XAttribute("next-preset", (int)care.NextPreset),
                new XAttribute("next-wake", (int)care.NextWakeTime.TotalMinutes),
                new XAttribute("moments", care.MomentCode),
                new XAttribute("together-today", care.TogetherToday),
                new XAttribute("day-excused", care.DayExcused),
                new XAttribute("awaiting-anchor", care.AwaitingAnchor),
                new XAttribute("excused-until", care.ExcusedUntil.UtcTicks),
                new XAttribute("last-care", care.LastCareAt.UtcTicks),
                new XAttribute("meals", care.Meals.ToCode()),
                new XAttribute("together", care.TogetherDays.ToCode()),
                new XAttribute("bedtimes", care.Bedtimes.ToCode()),
                new XAttribute("excused", care.ExcusedCount),
                new XAttribute("covered", care.CoveredCount),
                new XAttribute("reason", (int)care.LastReason),
                new XAttribute("wash", (int)care.Wash),
                new XAttribute("mess", care.Mess),
                new XAttribute("mood", (int)care.Mood),
                new XAttribute("mood-since", care.MoodSince.UtcTicks),
                new XAttribute("mood-cause", (int)care.MoodCause),
                new XAttribute("owed", (int)care.Owed),
                new XAttribute("style", (int)care.Style),
                new XAttribute("away-from", care.AwayFrom.UtcTicks),
                new XAttribute("away-until", care.AwayUntil.UtcTicks),
                new XAttribute("rest", (int)care.BedtimeRest),
                new XAttribute("rest-day", care.BedtimeRestDay),
                new XAttribute("condition", (int)care.Condition),
                new XAttribute("condition-cause", (int)care.ConditionCause),
                new XAttribute("condition-since", care.ConditionSince.UtcTicks),
                new XAttribute("unwell-time", care.UnwellTime.Ticks),
                new XAttribute("unwell-sessions", care.UnwellSessions),
                new XAttribute("session-start", care.SessionStart.UtcTicks),
                new XAttribute("missed-since-well", care.MissedSinceWell),
                new XAttribute("doses", care.MedicineDoses),
                new XAttribute("dose-window", care.LastDoseWindow),
                new XAttribute("good-day", care.GoodDay));
        }

        public static CareRecord Decode(XElement element)
        {
            if (element == null)
                throw new InvalidDataException("The save is missing its care record.");

            try
            {
                if (element.Name != ElementName)
                    throw new InvalidDataException("The care record header is invalid.");
                var version = (int)Attribute(element, "v");
                if (version > Version)
                    throw new NotSupportedException($"Care record version {version} is unsupported.");
                if (version < 1)
                    throw new InvalidDataException("The care record version is invalid.");

                var draft = new CareRecord.Builder
                {
                    Checkpoint = Instant(Attribute(element, "checkpoint")),
                    Offset = Minutes(Attribute(element, "offset")),
                    NextOffset = Minutes(Attribute(element, "next-offset")),
                    CareDay = (long)Attribute(element, "day"),
                    Preset = (RoutinePreset)(int)Attribute(element, "preset"),
                    WakeTime = Minutes(Attribute(element, "wake")),
                    NextPreset = (RoutinePreset)(int)Attribute(element, "next-preset"),
                    NextWakeTime = Minutes(Attribute(element, "next-wake")),
                    Moments = CareRecord.ParseMoments((string)Attribute(element, "moments")),
                    TogetherToday = (bool)Attribute(element, "together-today"),
                    DayExcused = (bool)Attribute(element, "day-excused"),
                    AwaitingAnchor = (bool)Attribute(element, "awaiting-anchor"),
                    ExcusedUntil = Instant(Attribute(element, "excused-until")),
                    LastCareAt = Instant(Attribute(element, "last-care")),
                    Meals = CareRing.Parse((string)Attribute(element, "meals")),
                    TogetherDays = CareRing.Parse((string)Attribute(element, "together")),
                    Bedtimes = CareRing.Parse((string)Attribute(element, "bedtimes")),
                    ExcusedCount = (long)Attribute(element, "excused"),
                    CoveredCount = (long)Attribute(element, "covered"),
                    LastReason = (CareReason)(int)Attribute(element, "reason"),
                };
                // Before the health axis every companion was well; its visit starts at the checkpoint.
                draft.ConditionSince = draft.Checkpoint;
                draft.SessionStart = draft.Checkpoint;
                if (version == VersionBeforeConsequences)
                {
                    draft.Meals = CareRing.Empty;
                    draft.TogetherDays = CareRing.Empty;
                    draft.Bedtimes = CareRing.Empty;
                    draft.AwaitingAnchor = true;
                    draft.Wash = MomentStatus.Excused;
                    return draft.Build();
                }

                draft.Wash = (MomentStatus)(int)Attribute(element, "wash");
                draft.Mess = (int)Attribute(element, "mess");
                draft.Mood = (CareMood)(int)Attribute(element, "mood");
                draft.MoodSince = Instant(Attribute(element, "mood-since"));
                draft.MoodCause = (CareMoodCause)(int)Attribute(element, "mood-cause");
                draft.Owed = (CareTags)(int)Attribute(element, "owed");
                draft.Style = (CareStyle)(int)Attribute(element, "style");
                draft.AwayFrom = Instant(Attribute(element, "away-from"));
                draft.AwayUntil = Instant(Attribute(element, "away-until"));
                draft.BedtimeRest = (MomentStatus)(int)Attribute(element, "rest");
                draft.BedtimeRestDay = (long)Attribute(element, "rest-day");
                if (version == VersionBeforeHealth)
                    return draft.Build();

                draft.Condition = (CareCondition)(int)Attribute(element, "condition");
                draft.ConditionCause = (CareConditionCause)(int)Attribute(element, "condition-cause");
                draft.ConditionSince = Instant(Attribute(element, "condition-since"));
                draft.UnwellTime = TimeSpan.FromTicks((long)Attribute(element, "unwell-time"));
                draft.UnwellSessions = (int)Attribute(element, "unwell-sessions");
                draft.SessionStart = Instant(Attribute(element, "session-start"));
                draft.MissedSinceWell = (bool)Attribute(element, "missed-since-well");
                draft.MedicineDoses = (int)Attribute(element, "doses");
                draft.LastDoseWindow = (long)Attribute(element, "dose-window");
                draft.GoodDay = (long)Attribute(element, "good-day");
                return draft.Build();
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException ||
                exception is OverflowException)
            {
                throw new InvalidDataException("The care record contains invalid data.", exception);
            }
        }

        private static DateTimeOffset Instant(XAttribute attribute) =>
            new DateTimeOffset((long)attribute, TimeSpan.Zero);

        private static TimeSpan Minutes(XAttribute attribute) =>
            TimeSpan.FromMinutes((int)attribute);

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The care record is missing {name}.");
    }
}
