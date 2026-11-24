using System;
using BAIby.Application;
using UnityEngine;

namespace BAIby.Infrastructure
{
    public sealed class PlayerPrefsBabySaveStore : IBabySaveStore
    {
        public const string DefaultKey = "baiby.mobile.baby-save";
        private readonly string key;
        private readonly string backupKey;

        public PlayerPrefsBabySaveStore(string key = DefaultKey)
        {
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            backupKey = key + ".backup";
        }

        // Slot 1 is the original single-baby key, so an existing save is already slot 1.
        public static string SlotKey(string saveKey, int slot)
        {
            if (saveKey == null)
                throw new ArgumentNullException(nameof(saveKey));
            if (slot < 1 || slot > HouseholdRules.PhysicalSlots)
                throw new ArgumentOutOfRangeException(nameof(slot));

            return slot == 1 ? saveKey : saveKey + ".slot" + slot;
        }

        public string Read() => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        public string ReadBackup() => PlayerPrefs.HasKey(backupKey) ? PlayerPrefs.GetString(backupKey) : null;

        public void Write(string payload, string previousValidPayload)
        {
            if (previousValidPayload != null)
                PlayerPrefs.SetString(backupKey, previousValidPayload);

            PlayerPrefs.SetString(key, payload);
            PlayerPrefs.Save();
        }

        // Removes the record and its backup.
        public void Delete()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(backupKey);
            PlayerPrefs.Save();
        }
    }
}
