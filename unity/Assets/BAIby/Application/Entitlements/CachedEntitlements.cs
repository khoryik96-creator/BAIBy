using System;
using System.Collections.Generic;
using System.IO;

namespace BAIby.Application
{
    // Offline-first entitlements. The cache is trusted at launch and a grant stays valid indefinitely.
    // Only a definitive refund or revocation removes one: a network error, a signed-out store or a product
    // that is merely missing from a query never does. Write failures keep the grants in memory for this
    // session and retry with the next change; they never take anything away.
    public sealed class CachedEntitlements : IEntitlements
    {
        private readonly IEntitlementCache cache;
        private readonly Func<DateTimeOffset> now;
        private readonly List<EntitlementGrant> grants = new List<EntitlementGrant>();
        private string lastValidPayload;
        // A cache written by a newer build is kept byte for byte and never rewritten.
        private readonly bool cacheFromNewerBuild;

        public CachedEntitlements(IEntitlementCache cache, Func<DateTimeOffset> now)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.now = now ?? throw new ArgumentNullException(nameof(now));

            // A corrupt cache never blocks launch: the backup is tried, and failing that nothing is owned
            // until the store answers or the player restores purchases.
            var primary = TryDecode(cache.Read(), out var primaryNewer, out var primaryPayload);
            if (primary != null)
            {
                grants.AddRange(primary);
                lastValidPayload = primaryPayload;
            }
            else
            {
                var backup = TryDecode(cache.ReadBackup(), out _, out var backupPayload);
                if (backup != null)
                {
                    grants.AddRange(backup);
                    lastValidPayload = backupPayload;
                }
            }

            cacheFromNewerBuild = primaryNewer;
        }

        public event Action Changed;

        // True when the latest change couldn't be written; it is retried with the next change.
        public bool HasUnsavedChanges { get; private set; }

        public IReadOnlyList<EntitlementGrant> Grants => grants.AsReadOnly();

        public IReadOnlyList<string> Owned
        {
            get
            {
                var owned = new List<string>();
                foreach (var grant in grants)
                    owned.Add(grant.ProductId);
                owned.Sort(StringComparer.Ordinal);
                return owned.AsReadOnly();
            }
        }

        public bool Owns(string productId) => Find(productId) != null;

        public bool HasPlaytestGrants => grants.Exists(grant => grant.Source == EntitlementSource.Playtest);

        // A confirmed purchase, restore or playtest unlock.
        public void Grant(string productId, EntitlementSource source)
        {
            if (!ProductIds.IsWellFormed(productId))
                throw new ArgumentException("A product ID is not well formed.", nameof(productId));

            var added = Put(productId, source);
            Save();
            if (added)
                Changed?.Invoke();
        }

        // A definitive refund or revocation: a Play voided purchase, a StoreKit revocation, or the playtest
        // toggle. Returns false when the product wasn't owned.
        public bool Revoke(string productId)
        {
            var grant = Find(productId);
            if (grant == null)
                return false;

            grants.Remove(grant);
            Save();
            Changed?.Invoke();
            return true;
        }

        // Applies a store answer. An unavailable store changes nothing. Confirmed products are granted or
        // re-confirmed; revoked products are removed; anything the answer does not mention is kept. A product
        // listed as both owned and revoked stays owned, so an ambiguous answer never locks a room.
        public void Apply(StoreAnswer answer)
        {
            if (answer == null)
                throw new ArgumentNullException(nameof(answer));
            if (!answer.Answered)
                return;

            var changed = false;
            foreach (var productId in answer.Owned)
                changed |= Put(productId, EntitlementSource.Store);

            foreach (var productId in answer.Revoked)
            {
                if (Contains(answer.Owned, productId))
                    continue;

                var grant = Find(productId);
                if (grant != null)
                {
                    grants.Remove(grant);
                    changed = true;
                }
            }

            if (answer.Owned.Count > 0 || changed)
                Save();
            if (changed)
                Changed?.Invoke();
        }

        private bool Put(string productId, EntitlementSource source)
        {
            var existing = Find(productId);
            var index = existing == null ? grants.Count : grants.IndexOf(existing);
            var grant = new EntitlementGrant(productId, source, now());
            if (existing == null)
                grants.Add(grant);
            else
                grants[index] = grant;

            return existing == null;
        }

        private void Save()
        {
            if (cacheFromNewerBuild)
            {
                HasUnsavedChanges = true;
                return;
            }

            var payload = EntitlementCacheCodec.Encode(grants);
            try
            {
                cache.Write(payload, lastValidPayload);
                lastValidPayload = payload;
                HasUnsavedChanges = false;
            }
            catch (Exception)
            {
                // Memory stays authoritative for this session and the next change retries the write.
                HasUnsavedChanges = true;
            }
        }

        private EntitlementGrant Find(string productId)
        {
            foreach (var grant in grants)
            {
                if (string.Equals(grant.ProductId, productId, StringComparison.Ordinal))
                    return grant;
            }

            return null;
        }

        private static bool Contains(IReadOnlyList<string> ids, string productId)
        {
            foreach (var id in ids)
            {
                if (string.Equals(id, productId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static List<EntitlementGrant> TryDecode(string payload, out bool newer, out string decoded)
        {
            newer = false;
            decoded = null;
            if (payload == null)
                return null;

            try
            {
                var grants = EntitlementCacheCodec.Decode(payload);
                decoded = payload;
                return grants;
            }
            catch (InvalidDataException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                newer = true;
                return null;
            }
        }
    }
}
