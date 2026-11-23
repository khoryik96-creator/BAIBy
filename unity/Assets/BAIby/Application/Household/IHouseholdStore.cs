namespace BAIby.Application
{
    // Platform storage for a household: one record per slot plus a manifest. Slot 1 is the original
    // single-baby save, so existing players need no data copy.
    public interface IHouseholdStore
    {
        // Returns the per-slot store for slot 1 to HouseholdRules.PhysicalSlots.
        IBabySaveStore Slot(int slot);

        // Removes the slot's record and its backup.
        void DeleteSlot(int slot);

        // Read methods return null when the manifest does not exist.
        string ReadManifest();
        string ReadManifestBackup();

        // previousValidPayload is null when no valid manifest was stored before.
        void WriteManifest(string payload, string previousValidPayload);
    }
}
