#if UNITY_WEBGL || UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using BAIby.Application;

namespace BAIby.Infrastructure
{
    // Playtest unlocks for Web, Editor and Development builds, which have no store. Turning a product on is a
    // playtest grant and turning it off is a definitive revocation, so the household sees exactly what a
    // purchase and a refund would do. Release device builds do not compile this class.
    public sealed class TestEntitlements : IPlaytestUnlocks
    {
        private readonly CachedEntitlements entitlements;

        public TestEntitlements(CachedEntitlements entitlements)
        {
            this.entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        }

        public bool Owns(string productId) => entitlements.Owns(productId);

        public void Set(string productId, bool owned)
        {
            if (owned)
                entitlements.Grant(productId, EntitlementSource.Playtest);
            else
                entitlements.Revoke(productId);
        }

        // Grants the products named in a Web preview URL, such as ?ent=home.rooms2.
        public void ApplyUrlFlags(string url)
        {
            foreach (var productId in PlaytestFlags.Parse(url))
            {
                if (!entitlements.Owns(productId))
                    entitlements.Grant(productId, EntitlementSource.Playtest);
            }
        }
    }
}
#endif
