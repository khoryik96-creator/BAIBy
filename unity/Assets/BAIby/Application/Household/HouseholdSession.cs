using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using BAIby.Simulation;

namespace BAIby.Application
{
    public sealed class HouseholdCompanion
    {
        public int Slot { get; }
        public DateTimeOffset AdoptedAt { get; }

        // Null when the slot's save couldn't be opened. The slot still counts toward capacity and its
        // bytes are kept, so it is never reused.
        public BabySession Session { get; }
        public bool IsReadable => Session != null;
        public BabyState State => Session?.State;

        internal HouseholdCompanion(int slot, DateTimeOffset adoptedAt, BabySession session)
        {
            Slot = slot;
            AdoptedAt = adoptedAt;
            Session = session;
        }
    }

    // The single writer of household slots and the manifest. It caches one BabySession per companion.
    // Every awake companion advances on launch, resume and the session tick, never on a switch, so need
    // decay does not depend on which companion is on screen.
    //
    // Companions beyond Capacity, in Home order, are resting: after a refund, say. A resting companion is
    // read-only. It does not advance, so it does not decay, and its record is never written; its journal
    // can still be read. The player chooses who stays awake by swapping places in Home order, which the
    // manifest already saves. When capacity returns, the next advance gives each woken companion one normal
    // capped advance. Resting is computed at runtime and never saved into records.
    public sealed class HouseholdSession
    {
        private readonly BabySimulation simulation;
        private readonly IHouseholdStore store;
        private readonly Func<DateTimeOffset> now;
        private readonly IEntitlements entitlements;
        private readonly List<HouseholdCompanion> companions = new List<HouseholdCompanion>();
        private readonly List<PendingCelebration> celebrations = new List<PendingCelebration>();
        private readonly List<Keepsake> keepsakes = new List<Keepsake>();
        // Slots with any stored bytes, readable or not. These are never offered for adoption.
        private readonly HashSet<int> occupied = new HashSet<int>();
        // Slots whose goodbye has started but whose final manifest is not yet written.
        private readonly HashSet<int> leaving = new HashSet<int>();
        private int activeSlot;
        private string storedManifest;
        private string lastValidManifest;
        // A manifest written by a newer build is kept byte for byte, like a newer slot record: this
        // build never writes the manifest, so a return to the newer build finds its home intact.
        private bool manifestFromNewerBuild;

        public HouseholdSession(BabySimulation simulation, IHouseholdStore store, Func<DateTimeOffset> now)
            : this(simulation, store, now, null)
        {
        }

        // entitlements decides capacity. Null means the free game.
        public HouseholdSession(BabySimulation simulation, IHouseholdStore store, Func<DateTimeOffset> now,
            IEntitlements entitlements)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            this.entitlements = entitlements ?? NoEntitlements.Instance;

            var sessions = ProbeSlots();
            var manifest = ReadManifest(out var trusted);
            if (manifest == null)
                Rebuild(sessions);
            else
                Reconcile(manifest, trusted, sessions);

            // A first launch with nothing saved writes nothing until the first adoption.
            if (storedManifest != null || companions.Count > 0 || keepsakes.Count > 0)
            {
                try
                {
                    WriteManifest();
                }
                catch (Exception)
                {
                    // Slots are the source of truth and the manifest is rebuildable, so a failed index
                    // write never keeps a readable companion from opening. storedManifest is unchanged,
                    // so the next write retries.
                }
            }
        }

        public int Capacity => HouseholdRules.CapacityFor(entitlements);
        public IReadOnlyList<HouseholdCompanion> Companions => companions.AsReadOnly();
        public IReadOnlyList<Keepsake> Keepsakes => keepsakes.AsReadOnly();

        // The companion whose room opens. A resting companion is never active: the saved active slot is
        // kept, and the first awake companion stands in until it wakes.
        public HouseholdCompanion Active
        {
            get
            {
                var saved = Find(activeSlot);
                return saved != null && saved.IsReadable && !IsResting(saved.Slot) ? saved : Find(FirstReadableSlot());
            }
        }

        public bool HasResting => companions.Count > Capacity;

        // True for a companion beyond capacity in Home order.
        public bool IsResting(int slot)
        {
            var index = companions.FindIndex(companion => companion.Slot == slot);
            return index >= 0 && index >= Capacity;
        }

        public bool IsFull => companions.Count >= Capacity;
        public bool CanAdopt => !IsFull && FindFreeSlot() != 0;
        // A goodbye needs its keepsake written, which a manifest from a newer build does not allow.
        public bool CanSayGoodbye => !manifestFromNewerBuild;

        public HouseholdCompanion Adopt(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the baby.", nameof(name));
            if (IsFull)
                throw new InvalidOperationException("The household is full.");

            var slot = FindFreeSlot();
            if (slot == 0)
                throw new InvalidOperationException("No free household slot is available.");

            // The record is written first. If the manifest write is interrupted, the next launch finds
            // the record by probing and lists it again.
            var session = new BabySession(simulation, store.Slot(slot), now);
            session.Adopt(name);
            var companion = new HouseholdCompanion(slot, session.State.TimeCheckpoint, session);
            companions.Add(companion);
            occupied.Add(slot);
            activeSlot = slot;
            WriteManifest();
            return companion;
        }

        // Makes another companion active. It does not advance time.
        public void Switch(int slot)
        {
            var companion = Find(slot) ?? throw new ArgumentException($"Slot {slot} has no companion.", nameof(slot));
            if (!companion.IsReadable)
                throw new InvalidOperationException("This save couldn't be opened.");
            if (IsResting(slot))
                throw new InvalidOperationException("This companion is resting.");
            if (activeSlot == slot)
                return;

            var previous = activeSlot;
            activeSlot = slot;
            try
            {
                WriteManifest();
            }
            catch
            {
                activeSlot = previous;
                throw;
            }
        }

        // The companion's state advanced to now without any write, for Home's minute tick. A resting
        // companion is shown as it was when it began to rest.
        public BabyState PreviewState(int slot)
        {
            var state = Find(slot)?.State;
            if (state == null || IsResting(slot))
                return state;

            return simulation.Advance(state, now()).State;
        }

        // Keeps wakeSlot in a room and lets restSlot rest instead, by swapping their places in Home order.
        // The companion that goes to rest first advances to now, so its awake time counts; the woken one gets
        // one normal capped advance. The swap is saved in the manifest; neither record stores the lock.
        public void SwapResting(int wakeSlot, int restSlot)
        {
            var waking = Find(wakeSlot) ?? throw new ArgumentException($"Slot {wakeSlot} has no companion.", nameof(wakeSlot));
            var resting = Find(restSlot) ?? throw new ArgumentException($"Slot {restSlot} has no companion.", nameof(restSlot));
            if (!IsResting(wakeSlot))
                throw new InvalidOperationException("Only a resting companion can be woken.");
            if (IsResting(restSlot))
                throw new InvalidOperationException("That companion is already resting.");
            if (!waking.IsReadable)
                throw new InvalidOperationException("This save couldn't be opened.");
            if (manifestFromNewerBuild)
                throw new InvalidOperationException("This home was saved by a newer version of BAIby. Update the game to choose who rests.");

            // Commit the awake time first. If this write fails nothing has moved.
            if (resting.IsReadable)
                AddCelebrations(restSlot, resting.Session.Refresh());

            var wakeIndex = companions.IndexOf(waking);
            var restIndex = companions.IndexOf(resting);
            var previousActive = activeSlot;
            companions[wakeIndex] = resting;
            companions[restIndex] = waking;
            if (activeSlot == restSlot)
                activeSlot = wakeSlot;
            try
            {
                WriteManifest();
            }
            catch
            {
                companions[wakeIndex] = waking;
                companions[restIndex] = resting;
                activeSlot = previousActive;
                throw;
            }

            // The woken companion's one capped advance. If it fails it is retried with the next advance.
            if (AddCelebrations(wakeSlot, waking.Session.Refresh()))
                WriteManifest();
        }

        public bool HasCelebrations(int slot) => celebrations.Exists(celebration => celebration.Slot == slot);

        // Returns the milestones reached while this companion was off screen, once.
        public IReadOnlyList<CompanionMilestone> TakeCelebrations(int slot)
        {
            var taken = new List<CompanionMilestone>();
            foreach (var celebration in celebrations)
            {
                if (celebration.Slot == slot)
                    taken.Add(celebration.Milestone);
            }

            if (taken.Count == 0)
                return taken.AsReadOnly();

            var previous = new List<PendingCelebration>(celebrations);
            celebrations.RemoveAll(celebration => celebration.Slot == slot);
            try
            {
                WriteManifest();
            }
            catch
            {
                celebrations.Clear();
                celebrations.AddRange(previous);
                throw;
            }

            return taken.AsReadOnly();
        }

        // Frees the slot and keeps a keepsake. The keepsake is written with the companion marked as
        // leaving, then the record is deleted, then the manifest is written without the companion.
        // A launch after an interruption finishes the goodbye with exactly one keepsake.
        public void SayGoodbye(int slot)
        {
            var companion = Find(slot);
            if (companion == null && leaving.Contains(slot))
            {
                // The record is already gone and only the final manifest write failed: retry it.
                WriteManifest();
                return;
            }

            if (companion == null)
                throw new ArgumentException($"Slot {slot} has no companion.", nameof(slot));
            if (!companion.IsReadable)
                throw new InvalidOperationException("A save that couldn't be opened is kept, not removed.");
            if (IsResting(slot) && !leaving.Contains(slot))
                throw new InvalidOperationException("A resting companion is kept as it is. Wake them to say goodbye.");
            if (manifestFromNewerBuild)
                throw new InvalidOperationException("This home was saved by a newer version of BAIby. Update the game to say goodbye.");

            if (!leaving.Contains(slot))
            {
                var state = companion.State;
                var keepsake = new Keepsake(state.Name, companion.AdoptedAt, now(), state.Age,
                    HouseholdRules.StageOf(state), state.Appearance);
                keepsakes.Add(keepsake);
                leaving.Add(slot);
                try
                {
                    WriteManifest();
                }
                catch
                {
                    keepsakes.Remove(keepsake);
                    leaving.Remove(slot);
                    throw;
                }
            }

            store.DeleteSlot(slot);
            companions.Remove(companion);
            occupied.Remove(slot);
            celebrations.RemoveAll(celebration => celebration.Slot == slot);
            if (activeSlot == slot)
                activeSlot = FirstReadableSlot();

            // Until this write succeeds the stored manifest still marks the slot as leaving, so the slot
            // is not offered for adoption in this session. A successful write releases it.
            WriteManifest();
        }

        // Advances every readable companion to now. Milestones of companions that are not on screen
        // are kept as pending celebrations; the active companion's unlocks are returned when it is shown.
        // Resting companions are skipped: they neither decay nor write. A companion that has just woken gets
        // its one normal capped advance here.
        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvanceAll(bool activeOnScreen = true)
        {
            var shown = (false, false, false);
            var added = false;
            Exception failure = null;
            var shownSlot = Active?.Slot ?? 0;
            foreach (var companion in companions)
            {
                if (!companion.IsReadable || IsResting(companion.Slot))
                    continue;

                try
                {
                    var unlocked = companion.Session.Refresh();
                    if (activeOnScreen && companion.Slot == shownSlot)
                        shown = unlocked;
                    else
                        added |= AddCelebrations(companion.Slot, unlocked);
                }
                catch (Exception error)
                {
                    // Keep advancing the others; the first failure is reported after the manifest write.
                    if (failure == null)
                        failure = error;
                }
            }

            // A failed advance returns nothing to the caller, so the on-screen companion's unlocks wait as
            // a pending celebration instead. Its record already holds the milestone, which is never
            // reported again.
            if (failure != null && activeOnScreen && shownSlot != 0)
                added |= AddCelebrations(shownSlot, shown);

            if (added)
            {
                try
                {
                    WriteManifest();
                }
                catch (Exception error)
                {
                    // The celebrations stay in memory and are written with the next manifest.
                    if (failure == null)
                    {
                        failure = error;
                        if (activeOnScreen && shownSlot != 0)
                            AddCelebrations(shownSlot, shown);
                    }
                }
            }

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();

            return shown;
        }

        private bool AddCelebrations(int slot, (bool Sitting, bool Crawling, bool Walking) unlocked)
        {
            var added = false;
            if (unlocked.Sitting)
                added |= AddCelebration(slot, CompanionMilestone.Sitting);
            if (unlocked.Crawling)
                added |= AddCelebration(slot, CompanionMilestone.Crawling);
            if (unlocked.Walking)
                added |= AddCelebration(slot, CompanionMilestone.Walking);
            return added;
        }

        private bool AddCelebration(int slot, CompanionMilestone milestone)
        {
            if (celebrations.Exists(celebration => celebration.Slot == slot && celebration.Milestone == milestone))
                return false;

            celebrations.Add(new PendingCelebration(slot, milestone));
            return true;
        }

        private Dictionary<int, BabySession> ProbeSlots()
        {
            // PlayerPrefs cannot list keys, so every physical slot is probed.
            var sessions = new Dictionary<int, BabySession>();
            for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            {
                var saves = store.Slot(slot);
                if (saves.Read() == null && saves.ReadBackup() == null)
                    continue;

                occupied.Add(slot);
                try
                {
                    sessions.Add(slot, new BabySession(simulation, saves, now));
                }
                catch (InvalidDataException)
                {
                    // Unreadable: kept as an occupied slot without a session.
                }
                catch (NotSupportedException)
                {
                    // Written by a newer build: kept as an occupied slot without a session.
                }
            }

            return sessions;
        }

        private HouseholdManifest ReadManifest(out bool trusted)
        {
            trusted = false;
            storedManifest = store.ReadManifest();
            var backup = store.ReadManifestBackup();
            var manifest = TryDecode(storedManifest, out var primaryNewer);
            var fallback = TryDecode(backup, out var backupNewer);
            // Writing would replace the newer primary, and the write after that its backup.
            manifestFromNewerBuild = primaryNewer || backupNewer;
            if (manifest != null)
            {
                trusted = true;
                lastValidManifest = storedManifest;
                return manifest;
            }

            if (fallback != null)
                lastValidManifest = backup;
            return fallback;
        }

        private static HouseholdManifest TryDecode(string payload, out bool newer)
        {
            newer = false;
            if (payload == null)
                return null;

            try
            {
                return HouseholdManifestCodec.Decode(payload);
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

        private void Rebuild(Dictionary<int, BabySession> sessions)
        {
            for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            {
                if (occupied.Contains(slot))
                    AddProbed(slot, sessions, null);
            }

            activeSlot = FirstReadableSlot();
        }

        private void Reconcile(HouseholdManifest manifest, bool trusted, Dictionary<int, BabySession> sessions)
        {
            foreach (var entry in manifest.Companions)
            {
                if (entry.Leaving && trusted)
                {
                    // Finish an interrupted goodbye. Its keepsake was written before the record was touched.
                    // A goodbye is only finished from the primary manifest: a stale backup could name a
                    // slot that has since been reused.
                    if (occupied.Remove(entry.Slot))
                    {
                        store.DeleteSlot(entry.Slot);
                        sessions.Remove(entry.Slot);
                    }

                    // Until a manifest without it is written, the slot is not offered for adoption.
                    leaving.Add(entry.Slot);
                    continue;
                }

                // A listed slot without a record has nothing left to show.
                if (occupied.Contains(entry.Slot))
                    AddProbed(entry.Slot, sessions, entry.AdoptedAt);
            }

            // Records the manifest does not list, for example after an interrupted adoption.
            for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            {
                if (occupied.Contains(slot) && Find(slot) == null)
                    AddProbed(slot, sessions, null);
            }

            foreach (var celebration in manifest.Celebrations)
            {
                var companion = Find(celebration.Slot);
                if (companion != null && companion.IsReadable)
                    celebrations.Add(celebration);
            }

            keepsakes.AddRange(manifest.Keepsakes);
            var active = Find(manifest.ActiveSlot);
            activeSlot = active != null && active.IsReadable ? active.Slot : FirstReadableSlot();
        }

        private void AddProbed(int slot, Dictionary<int, BabySession> sessions, DateTimeOffset? adoptedAt)
        {
            sessions.TryGetValue(slot, out var session);
            companions.Add(new HouseholdCompanion(slot, adoptedAt ?? EstimateAdoption(session), session));
        }

        // Records do not store the adoption date, so a rebuilt entry estimates it from age.
        private DateTimeOffset EstimateAdoption(BabySession session)
        {
            if (session?.State == null)
                return now();

            var checkpoint = session.State.TimeCheckpoint.UtcDateTime.Ticks;
            var age = session.State.Age.Ticks;
            return new DateTimeOffset(checkpoint > age ? checkpoint - age : checkpoint, TimeSpan.Zero);
        }

        private int FindFreeSlot()
        {
            for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            {
                if (occupied.Contains(slot) || leaving.Contains(slot))
                    continue;

                // Never overwrite bytes that appeared after the launch probe.
                var saves = store.Slot(slot);
                if (saves.Read() != null || saves.ReadBackup() != null)
                {
                    occupied.Add(slot);
                    continue;
                }

                return slot;
            }

            return 0;
        }

        // The first companion that can open, skipping resting ones.
        private int FirstReadableSlot()
        {
            var capacity = Capacity;
            for (var i = 0; i < companions.Count && i < capacity; i++)
            {
                if (companions[i].IsReadable)
                    return companions[i].Slot;
            }

            return 0;
        }

        private HouseholdCompanion Find(int slot)
        {
            foreach (var companion in companions)
            {
                if (companion.Slot == slot)
                    return companion;
            }

            return null;
        }

        private void WriteManifest()
        {
            if (manifestFromNewerBuild)
                return;

            var entries = new List<HouseholdEntry>();
            foreach (var companion in companions)
                entries.Add(new HouseholdEntry(companion.Slot, companion.AdoptedAt, leaving.Contains(companion.Slot)));

            var payload = HouseholdManifestCodec.Encode(new HouseholdManifest(entries, activeSlot, celebrations, keepsakes));
            if (payload != storedManifest)
            {
                store.WriteManifest(payload, lastValidManifest);
                storedManifest = payload;
                lastValidManifest = payload;
            }

            // The stored manifest no longer names finished goodbyes, so their slots are free again.
            leaving.RemoveWhere(slot => Find(slot) == null);
        }
    }
}
