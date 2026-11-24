#if UNITY_WEBGL || UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using BAIby.Application;
using UnityEngine;

namespace BAIby.Infrastructure
{
    // Playtest unlocks for Web, Editor and Development builds, which have no store. Turning a product on is a
    // playtest grant and turning it off is a definitive revocation, so the household sees exactly what a
    // purchase and a refund would do. Release device builds do not compile this class.
    public sealed class TestEntitlements : IPlaytestUnlocks
    {
        private readonly CachedEntitlements entitlements;
        private readonly string developerModeKey;

        // saveKey places the developer-mode switch beside the household keys ({saveKey}.developer-mode).
        public TestEntitlements(CachedEntitlements entitlements, string saveKey = PlayerPrefsBabySaveStore.DefaultKey)
        {
            this.entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
            developerModeKey = (saveKey ?? throw new ArgumentNullException(nameof(saveKey))) + ".developer-mode";
        }

        public bool DeveloperMode => PlayerPrefs.GetInt(developerModeKey, 0) == 1;

        public void SetDeveloperMode(bool on, IReadOnlyList<string> products)
        {
            if (products == null)
                throw new ArgumentNullException(nameof(products));

            // The switch is written first, so a launch after an interruption finishes the job from the saved
            // switch: StartPreview grants everything again while it is on.
            PlayerPrefs.SetInt(developerModeKey, on ? 1 : 0);
            PlayerPrefs.Save();
            foreach (var productId in products)
            {
                if (on && !entitlements.Owns(productId))
                    entitlements.Grant(productId, EntitlementSource.Playtest);
                else if (!on && entitlements.Owns(productId))
                    entitlements.Revoke(productId);
            }
        }

        public bool Owns(string productId) => entitlements.Owns(productId);

        public bool HasGrants => entitlements.HasPlaytestGrants;

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
