using System;
using System.Collections.Generic;
using System.IO;

namespace BAIby.Application
{
    // Offline-first entitlements. The cache is trusted at launch and a grant stays valid indefinitely.
    // Only a definitive refund or revocation removes one: a network error, a signed-out store or a product
    // that is merely missing from a query never does. Write failures keep the grants in memory for this
    // session and retry with the next change; they never take anything away.
    //
    // A cache that exists but can't be read, with no readable backup, means ownership is unknown rather than
    // "owns nothing": OwnershipUnknown stays true until a store answers, so a damaged cache never makes a
    // companion rest. Playtest grants count only in builds that compile the playtest toggle; other builds
    // keep them in the cache untouched but ignore them.
    public sealed class CachedEntitlements : IEntitlements
    {
        private readonly IEntitlementCache cache;
        private readonly Func<DateTimeOffset> now;
        private readonly bool honorPlaytestGrants;
        private readonly List<EntitlementGrant> grants = new List<EntitlementGrant>();
        private string lastValidPayload;
        // A cache written by a newer build is kept byte for byte and never rewritten.
        private readonly bool cacheFromNewerBuild;

        // A release build: playtest grants are ignored.
        public CachedEntitlements(IEntitlementCache cache, Func<DateTimeOffset> now)
            : this(cache, now, false)
        {
        }

        // honorPlaytestGrants is true only in Web, Editor and Development builds, which compile TestEntitlements.
        public CachedEntitlements(IEntitlementCache cache, Func<DateTimeOffset> now, bool honorPlaytestGrants)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            this.honorPlaytestGrants = honorPlaytestGrants;

            // A corrupt cache never blocks launch or companions: the backup is tried, and failing that
            // ownership is unknown until the store answers.
            var primaryPayload = cache.Read();
            var backupPayload = cache.ReadBackup();
            var primary = TryDecode(primaryPayload, out var primaryNewer, out var primaryUnverified);
            if (primary != null)
            {
                grants.AddRange(primary);
                lastValidPayload = primaryPayload;
                OwnershipUnknown = primaryUnverified;
            }
            else
            {
                var backup = TryDecode(backupPayload, out _, out var backupUnverified);
                if (backup != null)
                {
                    grants.AddRange(backup);
                    lastValidPayload = backupPayload;
                    OwnershipUnknown = backupUnverified;
                }
                else
                {
                    // Nothing stored at all is a first launch: nothing is owned, and that is known.
                    OwnershipUnknown = primaryPayload != null || backupPayload != null;
                }
            }

            cacheFromNewerBuild = primaryNewer;
        }

        public event Action Changed;

        // True when the latest change couldn't be written; it is retried with the next change.
        public bool HasUnsavedChanges { get; private set; }

        // True when the cache existed but couldn't be read and no store has answered since. Capacity then
        // keeps every companion awake rather than treating the damage as a refund.
        public bool OwnershipUnknown { get; private set; }

        // Every cached grant, including playtest grants this build ignores.
        public IReadOnlyList<EntitlementGrant> Grants => grants.AsReadOnly();

        public IReadOnlyList<string> Owned
        {
            get
            {
                var owned = new List<string>();
                foreach (var grant in grants)
                {
                    if (Counts(grant))
                        owned.Add(grant.ProductId);
                }

                owned.Sort(StringComparer.Ordinal);
                return owned.AsReadOnly();
            }
        }

        public bool Owns(string productId)
        {
            var grant = Find(productId);
            return grant != null && Counts(grant);
        }

        // Drives the "Playtest unlocks" badge. Always false where playtest grants are ignored.
        public bool HasPlaytestGrants => honorPlaytestGrants && grants.Exists(grant => grant.Source == EntitlementSource.Playtest);

        // A confirmed purchase, restore or playtest unlock.
        public void Grant(string productId, EntitlementSource source)
        {
            if (!ProductIds.IsWellFormed(productId))
                throw new ArgumentException("A product ID is not well formed.", nameof(productId));
            if (source == EntitlementSource.Playtest && !honorPlaytestGrants)
                throw new InvalidOperationException("Playtest unlocks are not available in this build.");

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
        // Any answer ends an unknown ownership left by an unreadable cache.
        public void Apply(StoreAnswer answer)
        {
            if (answer == null)
                throw new ArgumentNullException(nameof(answer));
            if (!answer.Answered)
                return;

            var changed = OwnershipUnknown;
            OwnershipUnknown = false;
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

        // Returns true when the product became owned. A playtest grant never replaces a store grant.
        private bool Put(string productId, EntitlementSource source)
        {
            var existing = Find(productId);
            if (existing != null && existing.Source == EntitlementSource.Store && source == EntitlementSource.Playtest)
                return false;

            var wasOwned = existing != null && Counts(existing);
            var grant = new EntitlementGrant(productId, source, now());
            if (existing == null)
                grants.Add(grant);
            else
                grants[grants.IndexOf(existing)] = grant;

            return !wasOwned && Counts(grant);
        }

        private void Save()
        {
            if (cacheFromNewerBuild)
            {
                HasUnsavedChanges = true;
                return;
            }

            var payload = EntitlementCacheCodec.Encode(grants, OwnershipUnknown);
            try
            {
                // The first write also seeds the backup, so a single damaged key never loses a grant.
                cache.Write(payload, lastValidPayload ?? payload);
                lastValidPayload = payload;
                HasUnsavedChanges = false;
            }
            catch (Exception)
            {
                // Memory stays authoritative for this session and the next change retries the write.
                HasUnsavedChanges = true;
            }
        }

        private bool Counts(EntitlementGrant grant) =>
            honorPlaytestGrants || grant.Source != EntitlementSource.Playtest;

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

        private static List<EntitlementGrant> TryDecode(string payload, out bool newer, out bool unverified)
        {
            newer = false;
            unverified = false;
            if (payload == null)
                return null;

            try
            {
                return EntitlementCacheCodec.Decode(payload, out unverified);
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
