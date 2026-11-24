using System;
using System.Collections.Generic;

namespace BAIby.Application
{
    // Store product IDs from docs/monetization.md. They are saved in the entitlement cache as text, so
    // an ID is never renamed once a build that sells it ships.
    public static class ProductIds
    {
        // "More rooms": two extra household rooms.
        public const string HomeRooms2 = "home.rooms2";

        // Helpers (docs/monetization.md): each on its own, or the household set of all three.
        public const string HelperFeeder = "helper.feeder";
        public const string HelperPlay = "helper.play";
        public const string HelperTidy = "helper.tidy";
        public const string HelperSet = "helper.set";

        // Every helper product, the set last.
        public static IReadOnlyList<string> HelperProducts { get; } =
            Array.AsReadOnly(new[] { HelperFeeder, HelperPlay, HelperTidy, HelperSet });

        // Product families in the premium catalogue, for example breed.dog.corgi or helper.set. A cached
        // grant from a family this build does not know yet is kept untouched for a newer build.
        private static readonly string[] Families =
        {
            "home.", "breed.", "breedpack.", "helper.", "look.", "coat.", "room.", "habitat.", "furniture.",
            "petwear.", "toy.", "sound.", "album.", "species.", "supporter."
        };

        public const int MaxLength = 64;

        public static IReadOnlyList<string> KnownFamilies => Array.AsReadOnly(Families);

        // Lower-case letters, digits, dots, dashes and underscores, in at least two dot-separated parts.
        public static bool IsWellFormed(string productId)
        {
            if (string.IsNullOrEmpty(productId) || productId.Length > MaxLength)
                return false;

            var dot = productId.IndexOf('.');
            if (dot <= 0 || productId[productId.Length - 1] == '.' || productId.Contains(".."))
                return false;

            foreach (var character in productId)
            {
                var allowed = (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') ||
                    character == '.' || character == '-' || character == '_';
                if (!allowed)
                    return false;
            }

            return true;
        }

        public static bool IsKnownFamily(string productId)
        {
            if (!IsWellFormed(productId))
                return false;

            foreach (var family in Families)
            {
                if (productId.StartsWith(family, StringComparison.Ordinal) && productId.Length > family.Length)
                    return true;
            }

            return false;
        }
    }
}
