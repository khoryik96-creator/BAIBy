using System;
using System.IO;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    // One <care v="1"> element shared by every companion's save. Invalid data throws InvalidDataException.
    internal static class CareRecordCodec
    {
        public const string ElementName = "care";
        private const int Version = 1;

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
                new XAttribute("reason", (int)care.LastReason));
        }

        public static CareRecord Decode(XElement element)
        {
            if (element == null)
                throw new InvalidDataException("The save is missing its care record.");

            try
            {
                if (element.Name != ElementName)
                    throw new InvalidDataException("The care record header is invalid.");
                if ((int)Attribute(element, "v") != Version)
                    throw new InvalidDataException("The care record version is unsupported.");

                return new CareRecord.Builder
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
                }.Build();
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
