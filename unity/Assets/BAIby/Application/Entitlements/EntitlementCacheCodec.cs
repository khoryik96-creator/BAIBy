using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace BAIby.Application
{
    // <entitlements version="1"><grant product="home.rooms2" source="0" confirmed="ticks" /></entitlements>
    // An optional unverified="1" marks grants written after an unreadable cache, before any store answer, so
    // a relaunch still treats ownership as unknown instead of as "owns nothing".
    internal static class EntitlementCacheCodec
    {
        private const int Version = 1;

        public static string Encode(IEnumerable<EntitlementGrant> grants, bool unverified = false)
        {
            if (grants == null)
                throw new ArgumentNullException(nameof(grants));

            var root = new XElement("entitlements", new XAttribute("version", Version));
            if (unverified)
                root.Add(new XAttribute("unverified", 1));
            foreach (var grant in grants)
            {
                root.Add(new XElement("grant",
                    new XAttribute("product", grant.ProductId),
                    new XAttribute("source", (int)grant.Source),
                    new XAttribute("confirmed", grant.ConfirmedAt.UtcDateTime.Ticks)));
            }

            return root.ToString(SaveOptions.DisableFormatting);
        }

        public static List<EntitlementGrant> Decode(string payload) => Decode(payload, out _);

        public static List<EntitlementGrant> Decode(string payload, out bool unverified)
        {
            unverified = false;
            try
            {
                var root = XElement.Parse(payload);
                if (root.Name != "entitlements" || root.Attribute("version") == null)
                    throw new InvalidDataException("The entitlement cache header is invalid.");

                var version = (int)root.Attribute("version");
                if (version < 1)
                    throw new InvalidDataException("The entitlement cache version is invalid.");
                if (version > Version)
                    throw new NotSupportedException($"Entitlement cache version {version} is unsupported.");

                var marker = root.Attribute("unverified");
                if (marker != null)
                {
                    var value = (int)marker;
                    if (value != 0 && value != 1)
                        throw new InvalidDataException("The entitlement cache's unverified marker is invalid.");
                    unverified = value == 1;
                }

                var grants = new List<EntitlementGrant>();
                var products = new HashSet<string>(StringComparer.Ordinal);
                foreach (var element in root.Elements("grant"))
                {
                    var grant = new EntitlementGrant((string)Attribute(element, "product"),
                        (EntitlementSource)(int)Attribute(element, "source"),
                        new DateTimeOffset((long)Attribute(element, "confirmed"), TimeSpan.Zero));
                    if (!products.Add(grant.ProductId))
                        throw new InvalidDataException($"{grant.ProductId} is cached twice.");
                    grants.Add(grant);
                }

                return grants;
            }
            catch (Exception exception) when (exception is XmlException || exception is FormatException ||
                exception is ArgumentException || exception is OverflowException)
            {
                throw new InvalidDataException("The entitlement cache contains invalid data.", exception);
            }
        }

        private static XAttribute Attribute(XElement element, string name) =>
            element.Attribute(name) ?? throw new InvalidDataException($"The entitlement cache is missing {element.Name} {name}.");
    }
}
