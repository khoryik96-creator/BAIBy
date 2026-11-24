using System;
using BAIby.Application;
using UnityEngine;

namespace BAIby.Infrastructure
{
    // Keys all derive from one save key: slot 1 is the key itself, slots 2 to 8 are {key}.slot{n},
    // and the manifest is {key}.household. Each has a .backup companion.
    public sealed class PlayerPrefsHouseholdStore : IHouseholdStore
    {
        private readonly string saveKey;
        private readonly PlayerPrefsBabySaveStore[] slots = new PlayerPrefsBabySaveStore[HouseholdRules.PhysicalSlots];
        private readonly string manifestKey;
        private readonly string manifestBackupKey;

        public PlayerPrefsHouseholdStore(string saveKey = PlayerPrefsBabySaveStore.DefaultKey)
        {
            this.saveKey = saveKey ?? throw new ArgumentNullException(nameof(saveKey));
            manifestKey = ManifestKey(saveKey);
            manifestBackupKey = manifestKey + ".backup";
        }

        public static string ManifestKey(string saveKey) =>
            (saveKey ?? throw new ArgumentNullException(nameof(saveKey))) + ".household";

        public IBabySaveStore Slot(int slot) => SlotStore(slot);

        public void DeleteSlot(int slot) => SlotStore(slot).Delete();

        public string ReadManifest() => PlayerPrefs.HasKey(manifestKey) ? PlayerPrefs.GetString(manifestKey) : null;

        public string ReadManifestBackup() =>
            PlayerPrefs.HasKey(manifestBackupKey) ? PlayerPrefs.GetString(manifestBackupKey) : null;

        public void WriteManifest(string payload, string previousValidPayload)
        {
            if (previousValidPayload != null)
                PlayerPrefs.SetString(manifestBackupKey, previousValidPayload);

            PlayerPrefs.SetString(manifestKey, payload);
            PlayerPrefs.Save();
        }

        private PlayerPrefsBabySaveStore SlotStore(int slot)
        {
            if (slot < 1 || slot > HouseholdRules.PhysicalSlots)
                throw new ArgumentOutOfRangeException(nameof(slot));

            return slots[slot - 1] ?? (slots[slot - 1] = new PlayerPrefsBabySaveStore(PlayerPrefsBabySaveStore.SlotKey(saveKey, slot)));
        }
    }
}
