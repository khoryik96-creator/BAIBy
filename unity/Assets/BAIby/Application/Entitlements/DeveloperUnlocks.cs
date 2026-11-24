using System;
using System.Collections.Generic;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    // Developer mode, for Web, Editor and Development builds only: every product this build knows is granted
    // as a playtest grant, and earned breeds count as earned. Nothing is bought and nothing is written into a
    // companion's record, so turning it off returns the game to exactly what the player owns and has earned.
    public static class DeveloperUnlocks
    {
        // A URL flag that turns developer mode on, for example /nursery/?dev=1.
        public const string UrlFlag = "dev=1";

        // Every product this build can sell: the extra rooms, each premium breed and each breed pack. Add new
        // product families here as they ship (looks, helpers, room themes), so developer mode keeps covering
        // everything.
        public static IReadOnlyList<string> Products(PetCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var products = new List<string> { ProductIds.HomeRooms2 };
            foreach (var breed in catalog.AllBreeds)
            {
                if (breed == null)
                    continue;
                foreach (var product in catalog.ProductsGranting(breed.Breed))
                {
                    if (!products.Contains(product))
                        products.Add(product);
                }
            }

            foreach (var pack in catalog.AllPacks)
            {
                if (pack?.ProductId != null && !products.Contains(pack.ProductId))
                    products.Add(pack.ProductId);
            }

            return products.AsReadOnly();
        }

        // True when the Web preview URL asks for developer mode with ?dev=1.
        public static bool RequestedBy(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            var query = url.IndexOf('?');
            if (query < 0)
                return false;

            var end = url.IndexOf('#', query);
            var parameters = (end < 0 ? url.Substring(query + 1) : url.Substring(query + 1, end - query - 1)).Split('&');
            foreach (var parameter in parameters)
            {
                if (string.Equals(parameter, UrlFlag, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
