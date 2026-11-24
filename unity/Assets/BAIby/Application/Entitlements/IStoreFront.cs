using System;

namespace BAIby.Application
{
    public enum RestoreOutcome { UpToDate, Restored, Unavailable }

    // The shop seam. Only the future BAIby.Store assembly talks to a real store (Unity IAP); everything else
    // sees this interface. Every entry point sits behind the parental gate and opens only from Home or the
    // household settings, never from a care flow.
    public interface IStoreFront
    {
        // False while no store is compiled in: the "More rooms" entry then says it is coming soon.
        bool SellsProducts { get; }

        // Asks the store to restore earlier purchases. done runs once, possibly later on the main thread.
        void RestorePurchases(Action<RestoreOutcome> done);
    }

    // This build has no store: nothing is sold, and Restore Purchases succeeds without changing anything.
    public sealed class OfflineStoreFront : IStoreFront
    {
        public bool SellsProducts => false;

        public void RestorePurchases(Action<RestoreOutcome> done)
        {
            if (done == null)
                throw new ArgumentNullException(nameof(done));

            done(RestoreOutcome.UpToDate);
        }
    }

    // A developer toggle for products, compiled only into Web, Editor and Development builds. Nothing is
    // bought: a playtest grant and its removal go through the same cache as a store grant and a refund.
    public interface IPlaytestUnlocks
    {
        bool Owns(string productId);
        void Set(string productId, bool owned);

        // True while any playtest grant is cached, which shows the "Playtest unlocks" badge.
        bool HasGrants { get; }

        // Developer mode: every product granted and every earned breed open. Saved on this device only.
        bool DeveloperMode { get; }

        // Turns developer mode on (granting products) or off (removing them, so the game returns to what is
        // really owned). products is every product the build knows; see DeveloperUnlocks.Products.
        void SetDeveloperMode(bool on, System.Collections.Generic.IReadOnlyList<string> products);
    }
}
