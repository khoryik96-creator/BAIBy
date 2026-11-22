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
