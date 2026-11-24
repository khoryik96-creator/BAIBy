using System;
using BAIby.Application;
using BAIby.Infrastructure;
using BAIby.Presentation;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BAIby.Bootstrap
{
    public static class NurseryStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Open()
        {
            if (SceneManager.GetActiveScene().path == "Assets/Scenes/Nursery.unity")
                StartPreview(PlayerPrefsBabySaveStore.DefaultKey);
        }

        public static NurseryPreview StartPreview(string saveKey) => StartPreview(saveKey, null);

        // `now` replaces the device clock (Play Mode tests); null reads the local clock.
        public static NurseryPreview StartPreview(string saveKey, Func<DateTimeOffset> now)
        {
            var view = new GameObject("Nursery Preview").AddComponent<NurseryPreview>();
            try
            {
                var simulation = new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay,
                    TimeSpan.FromDays(7), TimeSpan.FromDays(14));
                // The clock carries the local offset (from the browser on the Web build) and follows zone
                // changes while the app stays alive. Every household key derives from saveKey; slot 1 is
                // saveKey itself, so an existing single-baby save opens as the first companion uncopied.
                var clock = now ?? new LocalClock().Now;
                // The cached entitlement is trusted at launch, offline. This build has no store, so nothing is
                // sold and Restore Purchases reports that everything is up to date. Playtest grants count only
                // in builds that compile the playtest toggle; a release build ignores any it finds.
                var honorPlaytestGrants = false;
#if UNITY_WEBGL || UNITY_EDITOR || DEVELOPMENT_BUILD
                honorPlaytestGrants = true;
#endif
                var entitlements = new CachedEntitlements(new PlayerPrefsEntitlementCache(saveKey), clock,
                    honorPlaytestGrants);
                IPlaytestUnlocks playtest = null;
#if UNITY_WEBGL || UNITY_EDITOR || DEVELOPMENT_BUILD
                var unlocks = new TestEntitlements(entitlements);
                unlocks.ApplyUrlFlags(UnityEngine.Application.absoluteURL);
                playtest = unlocks;
#endif
                // Pets share the household, its slots and its rooms; premium breeds read the same entitlements.
                var household = new HouseholdSession(simulation, new PetSimulation(), new PlayerPrefsHouseholdStore(saveKey),
                    clock, entitlements);
                view.Open(household, entitlements, new OfflineStoreFront(), playtest);
            }
            catch (Exception error)
            {
                view.ReportStartupFailure(error);
            }

            return view;
        }
    }
}
