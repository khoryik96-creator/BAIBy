using System;
using System.Collections.Generic;

namespace BAIby.Application
{
    // Saved values are stable content IDs. Append new options; never renumber.
    public enum EntitlementSource { Store = 0, Playtest = 1 }

    // One owned product in the cache: where it came from and when it was last confirmed.
    public sealed class EntitlementGrant
    {
        public string ProductId { get; }
        public EntitlementSource Source { get; }
        public DateTimeOffset ConfirmedAt { get; }

        public EntitlementGrant(string productId, EntitlementSource source, DateTimeOffset confirmedAt)
        {
            if (!ProductIds.IsWellFormed(productId))
                throw new ArgumentException("A product ID is not well formed.", nameof(productId));
            if (!Enum.IsDefined(typeof(EntitlementSource), source))
                throw new ArgumentOutOfRangeException(nameof(source));

            ProductId = productId;
            Source = source;
            ConfirmedAt = confirmedAt;
        }
    }

    // Platform storage for the entitlement cache, {saveKey}.entitlements with a .backup.
    public interface IEntitlementCache
    {
        // Read methods return null when the cache does not exist.
        string Read();
        string ReadBackup();

        // previousValidPayload goes to the backup before payload replaces the primary. On the very first
        // write it is payload itself, so the cache always keeps two valid copies.
        void Write(string payload, string previousValidPayload);
    }

    // A store's answer to an ownership query or a purchase update.
    public sealed class StoreAnswer
    {
        // The store couldn't be reached or couldn't answer (offline, signed out, a timeout). Changes nothing.
        public static readonly StoreAnswer Unavailable = new StoreAnswer(false, new string[0], new string[0]);

        // True when the store answered.
        public bool Answered { get; }

        // Products the store confirmed as owned.
        public IReadOnlyList<string> Owned { get; }

        // Definitive "not owned" answers only: a Play voided purchase or a StoreKit revocation. A product
        // that is simply missing from a query is neither owned nor revoked.
        public IReadOnlyList<string> Revoked { get; }

        private StoreAnswer(bool answered, string[] owned, string[] revoked)
        {
            Answered = answered;
            Owned = Array.AsReadOnly(owned);
            Revoked = Array.AsReadOnly(revoked);
        }

        public static StoreAnswer Confirmed(IEnumerable<string> owned,
            IEnumerable<string> revoked = null)
        {
            if (owned == null)
                throw new ArgumentNullException(nameof(owned));

            return new StoreAnswer(true, Checked(owned), revoked == null ? new string[0] : Checked(revoked));
        }

        private static string[] Checked(IEnumerable<string> ids)
        {
            var list = new List<string>();
            foreach (var id in ids)
            {
                if (!ProductIds.IsWellFormed(id))
                    throw new ArgumentException("A product ID is not well formed.", nameof(ids));
                if (!list.Contains(id))
                    list.Add(id);
            }

            return list.ToArray();
        }
    }
}
