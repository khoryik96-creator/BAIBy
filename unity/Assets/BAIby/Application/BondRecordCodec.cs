using System;
using System.IO;
using System.Xml.Linq;
using BAIby.Simulation;

namespace BAIby.Application
{
    // One <bond v="1"> element: Bond's high-water mark and the day's caps, temperament with its day tallies, and the
    // care rhythm with its lifetime records. The Bond value itself stays in the baby's <connection> element. Invalid
    // data throws InvalidDataException; a newer version throws NotSupportedException so the session leaves that save
    // untouched.
    internal static class BondRecordCodec
    {
        public const string ElementName = "bond";
        private const int Version = 1;

        public static XElement Encode(BondRecord bond)
        {
            if (bond == null)
                throw new ArgumentNullException(nameof(bond));

            return new XElement(ElementName,
                new XAttribute("v", Version),
                new XAttribute("high", bond.HighWater),
                new XAttribute("cap-day", bond.CapDay),
                new XAttribute("gained", bond.Gained),
                new XAttribute("lost", bond.Lost),
                new XAttribute("helper-day", bond.HelperDay),
                new XAttribute("security", bond.Security),
                new XAttribute("curiosity", bond.Curiosity),
                new XAttribute("tally-day", bond.TallyDay),
                new XAttribute("meals", bond.Meals),
                new XAttribute("together", bond.Together),
                new XAttribute("rested", bond.Rested),
                new XAttribute("answered", bond.Answered),
                new XAttribute("kinds", bond.Kinds),
                new XAttribute("rhythm", bond.Rhythm),
                new XAttribute("grace-day", bond.GraceDay),
                new XAttribute("care-days", bond.TotalCareDays),
                new XAttribute("longest", bond.LongestRhythm));
        }

        public static BondRecord Decode(XElement element)
        {
            if (element == null)
                throw new InvalidDataException("The save is missing its bond record.");

            try
            {
                if (element.Name != ElementName)
                    throw new InvalidDataException("The bond record header is invalid.");
                var version = (int)Attribute(element, "v");
                if (version > Version)
                    throw new NotSupportedException($"Bond record version {version} is unsupported.");
                if (version < 1)
                    throw new InvalidDataException("The bond record version is invalid.");

                return new BondRecord.Builder
                {
                    HighWater = (double)Attribute(element, "high"),
                    CapDay = (long)Attribute(element, "cap-day"),
                    Gained = (double)Attribute(element, "gained"),
                    Lost = (double)Attribute(element, "lost"),
                    HelperDay = (bool)Attribute(element, "helper-day"),
                    Security = (double)Attribute(element, "security"),
                    Curiosity = (double)Attribute(element, "curiosity"),
                    TallyDay = (long)Attribute(element, "tally-day"),
                    Meals = (int)Attribute(element, "meals"),
                    Together = (bool)Attribute(element, "together"),
                    Rested = (bool)Attribute(element, "rested"),
                    Answered = (int)Attribute(element, "answered"),
                    Kinds = (int)Attribute(element, "kinds"),
                    Rhythm = (int)Attribute(element, "rhythm"),
                    GraceDay = (long)Attribute(element, "grace-day"),
                    TotalCareDays = (int)Attribute(element, "care-days"),
                    LongestRhythm = (int)Attribute(element, "longest"),
                }.Build();
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException ||
                exception is OverflowException)
            {
                throw new InvalidDataException("The bond record contains invalid data.", exception);
            }
        }

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The bond record is missing {name}.");
    }
}
