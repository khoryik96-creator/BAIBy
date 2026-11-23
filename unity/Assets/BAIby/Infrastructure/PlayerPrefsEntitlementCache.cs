using System;
using BAIby.Application;
using UnityEngine;

namespace BAIby.Infrastructure
{
    // The entitlement cache lives at {saveKey}.entitlements with a .backup, beside the household keys.
    public sealed class PlayerPrefsEntitlementCache : IEntitlementCache
    {
        private readonly string key;
        private readonly string backupKey;

        public PlayerPrefsEntitlementCache(string saveKey = PlayerPrefsBabySaveStore.DefaultKey)
        {
            key = CacheKey(saveKey);
            backupKey = key + ".backup";
        }

        public static string CacheKey(string saveKey) =>
            (saveKey ?? throw new ArgumentNullException(nameof(saveKey))) + ".entitlements";

        public string Read() => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        public string ReadBackup() => PlayerPrefs.HasKey(backupKey) ? PlayerPrefs.GetString(backupKey) : null;

        public void Write(string payload, string previousValidPayload)
        {
            if (previousValidPayload != null)
                PlayerPrefs.SetString(backupKey, previousValidPayload);

            PlayerPrefs.SetString(key, payload);
            PlayerPrefs.Save();
        }
    }
}
