using System;
using System.Collections.Generic;

namespace BAIby.Application
{
    // Reads playtest unlocks from a Web preview URL, for example /nursery/?ent=home.rooms2,helper.set.
    // Unknown or malformed IDs are ignored. Only Web, Editor and Development builds read these flags.
    public static class PlaytestFlags
    {
        public static IReadOnlyList<string> Parse(string url)
        {
            var products = new List<string>();
            if (string.IsNullOrEmpty(url))
                return products.AsReadOnly();

            var query = url.IndexOf('?');
            if (query < 0)
                return products.AsReadOnly();

            var end = url.IndexOf('#', query);
            var parameters = (end < 0 ? url.Substring(query + 1) : url.Substring(query + 1, end - query - 1)).Split('&');
            foreach (var parameter in parameters)
            {
                if (!parameter.StartsWith("ent=", StringComparison.Ordinal))
                    continue;

                var value = Uri.UnescapeDataString(parameter.Substring(4).Replace('+', ' '));
                foreach (var raw in value.Split(','))
                {
                    var product = raw.Trim();
                    if (ProductIds.IsKnownFamily(product) && !products.Contains(product))
                        products.Add(product);
                }
            }

            return products.AsReadOnly();
        }
    }
}
