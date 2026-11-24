using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    // One <development v="1"> element: readiness, the latest quality sample, recent engagement, the day's variety and
    // the setback. Invalid data throws InvalidDataException; a newer version throws NotSupportedException so the
    // session leaves that save untouched.
    internal static class DevelopmentRecordCodec
    {
        public const string ElementName = "development";
        private const string EntryName = "e";
        private const int Version = 1;

        public static XElement Encode(DevelopmentRecord development)
        {
            if (development == null)
                throw new ArgumentNullException(nameof(development));

            var sample = development.Sample;
            var element = new XElement(ElementName,
                new XAttribute("v", Version),
                new XAttribute("readiness", development.Readiness),
                new XAttribute("mode", (int)sample.Mode),
                new XAttribute("wellbeing", sample.Wellbeing),
                new XAttribute("motor", sample.Motor),
                new XAttribute("learning", sample.Learning),
                new XAttribute("social", sample.Social),
                new XAttribute("physical", sample.Physical),
                new XAttribute("low", development.LowWellbeingTime.Ticks),
                new XAttribute("toys", development.StageToys),
                new XAttribute("category-day", development.CategoryDay),
                new XAttribute("categories", (int)development.Categories),
                new XAttribute("variety", development.VarietyGiven),
                new XAttribute("shy", (int)development.Shy),
                new XAttribute("setback-since", development.SetbackSince.UtcTicks),
                new XAttribute("recovery-day", development.RecoveryDay),
                new XAttribute("recovery-meals", development.RecoveryMeals),
                new XAttribute("recovery-soothes", development.RecoverySoothes));
            foreach (var entry in development.Engagement)
                element.Add(new XElement(EntryName,
                    new XAttribute("k", (int)entry.Kind),
                    new XAttribute("t", entry.At),
                    new XAttribute("f", entry.Factor)));
            return element;
        }

        public static DevelopmentRecord Decode(XElement element)
        {
            if (element == null)
                throw new InvalidDataException("The save is missing its development record.");

            try
            {
                if (element.Name != ElementName)
                    throw new InvalidDataException("The development record header is invalid.");
                var version = (int)Attribute(element, "v");
                if (version > Version)
                    throw new NotSupportedException($"Development record version {version} is unsupported.");
                if (version < 1)
                    throw new InvalidDataException("The development record version is invalid.");

                var engagement = new List<EngagementEntry>();
                foreach (var entry in element.Elements(EntryName))
                    engagement.Add(new EngagementEntry((EngagementKind)(int)Attribute(entry, "k"), (long)Attribute(entry, "t"),
                        (double)Attribute(entry, "f")));

                return new DevelopmentRecord.Builder
                {
                    Readiness = (double)Attribute(element, "readiness"),
                    Sample = new TrackSample((SampleMode)(int)Attribute(element, "mode"),
                        (double)Attribute(element, "wellbeing"), (double)Attribute(element, "motor"),
                        (double)Attribute(element, "learning"), (double)Attribute(element, "social"),
                        (double)Attribute(element, "physical")),
                    LowWellbeingTime = TimeSpan.FromTicks((long)Attribute(element, "low")),
                    Engagement = engagement,
                    StageToys = (int)Attribute(element, "toys"),
                    CategoryDay = (long)Attribute(element, "category-day"),
                    Categories = (CareCategories)(int)Attribute(element, "categories"),
                    VarietyGiven = (bool)Attribute(element, "variety"),
                    Shy = (MotorSkill)(int)Attribute(element, "shy"),
                    SetbackSince = new DateTimeOffset((long)Attribute(element, "setback-since"), TimeSpan.Zero),
                    RecoveryDay = (long)Attribute(element, "recovery-day"),
                    RecoveryMeals = (int)Attribute(element, "recovery-meals"),
                    RecoverySoothes = (int)Attribute(element, "recovery-soothes"),
                }.Build();
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException ||
                exception is OverflowException)
            {
                throw new InvalidDataException("The development record contains invalid data.", exception);
            }
        }

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The development record is missing {name}.");
    }
}
