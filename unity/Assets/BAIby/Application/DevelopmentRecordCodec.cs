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
    //
    // v2 adds First words and Talk time (a baby past day 9, when First words' stage starts): readiness in words, whether
    // they came in first-words, and the talks so far with the care day whose first talk counted. Nothing typed is ever
    // kept. A record before that stage is still written as v1 byte for byte; a v1 record decodes with the stage not
    // started, and a baby past day 9 is then seeded from age (BabyGrowth). An older build leaves a v2 record alone.
    internal static class DevelopmentRecordCodec
    {
        public const string ElementName = "development";
        private const string EntryName = "e";
        private const int Version = 2;
        private const int VersionBeforeWords = 1;

        public static XElement Encode(DevelopmentRecord development)
        {
            if (development == null)
                throw new ArgumentNullException(nameof(development));

            var sample = development.Sample;
            var element = new XElement(ElementName,
                new XAttribute("v", development.LearningStarted ? Version : VersionBeforeWords),
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
            if (development.LearningStarted)
                element.Add(new XAttribute("words", development.Words),
                    new XAttribute("first-words", development.FirstWords),
                    new XAttribute("talks", development.Talks),
                    new XAttribute("talk-day", development.TalkDay),
                    new XAttribute("talk-counted", development.TalkCounted));
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
                {
                    var kind = (EngagementKind)(int)Attribute(entry, "k");
                    if (kind == EngagementKind.Talk && version < Version)
                        throw new InvalidDataException("A development record before First words cannot hold a talk.");
                    engagement.Add(new EngagementEntry(kind, (long)Attribute(entry, "t"), (double)Attribute(entry, "f")));
                }

                var words = version >= Version;
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
                    LearningStarted = words,
                    Words = words ? (double)Attribute(element, "words") : 0,
                    FirstWords = words && (bool)Attribute(element, "first-words"),
                    Talks = words ? (int)Attribute(element, "talks") : 0,
                    TalkDay = words ? (long)Attribute(element, "talk-day") : 0,
                    TalkCounted = words && (bool)Attribute(element, "talk-counted"),
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
