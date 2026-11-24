using System;
using System.IO;

namespace BAIby.Application
{
    // One companion record on its per-key store: restore with a backup fallback, and commit with the
    // previous valid payload kept as the backup. Shared by BabySession and PetSession.
    //
    // Restore rules: a corrupt primary (InvalidDataException) falls back to the backup; a primary written
    // by a newer build (NotSupportedException) is never replaced by an older backup and the error is
    // surfaced; a missing primary restores the backup when there is one.
    internal sealed class SavedRecord<T> where T : class
    {
        private readonly IBabySaveStore saves;
        private readonly Func<T, string> encode;
        private string lastSavedPayload;

        // Null until something is committed or a saved record is restored.
        public T Value { get; private set; }

        public SavedRecord(IBabySaveStore saves, Func<string, T> decode, Func<T, string> encode)
        {
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
            if (decode == null)
                throw new ArgumentNullException(nameof(decode));
            this.encode = encode ?? throw new ArgumentNullException(nameof(encode));

            var primary = saves.Read();
            if (primary != null)
            {
                try
                {
                    Restore(primary, decode);
                    return;
                }
                catch (InvalidDataException)
                {
                    var fallback = saves.ReadBackup();
                    if (fallback == null)
                        throw;

                    Restore(fallback, decode);
                    return;
                }
            }

            var backup = saves.ReadBackup();
            if (backup != null)
                Restore(backup, decode);
        }

        public void Commit(T updated)
        {
            if (updated == null)
                throw new ArgumentNullException(nameof(updated));

            var payload = encode(updated);
            saves.Write(payload, lastSavedPayload);
            lastSavedPayload = payload;
            Value = updated;
        }

        private void Restore(string payload, Func<string, T> decode)
        {
            Value = decode(payload);
            lastSavedPayload = payload;
        }
    }
}
