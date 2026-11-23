using System;
using System.Collections.Generic;

namespace BAIby.Application
{
    // What the player owns, answered offline from the cache. Records never store purchase state: anything
    // an entitlement unlocks is computed at runtime, so a lost entitlement never rewrites a save.
    public interface IEntitlements
    {
        bool Owns(string productId);

        // Owned product IDs in a stable order.
        IReadOnlyList<string> Owned { get; }

        // Raised after a grant or a revocation changes what is owned.
        event Action Changed;
    }

    // The free game: owns nothing. Used where no entitlement source is wired, such as older tests.
    public sealed class NoEntitlements : IEntitlements
    {
        public static readonly NoEntitlements Instance = new NoEntitlements();

        private NoEntitlements()
        {
        }

        public bool Owns(string productId) => false;
        public IReadOnlyList<string> Owned => Array.Empty<string>();

        public event Action Changed
        {
            add { }
            remove { }
        }
    }
}
