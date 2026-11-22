namespace BAIby.Application
{
    public interface IBabySaveStore
    {
        // Read methods return null when their slot does not exist.
        string Read();
        string ReadBackup();

        // previousValidPayload is null for a new game.
        void Write(string payload, string previousValidPayload);
    }
}
