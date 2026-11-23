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
    // Every companion advances on launch, resume and the session tick, never on a switch, so need decay
    // does not depend on which companion is on screen.
    public sealed class HouseholdSession
    {
        private readonly BabySimulation simulation;
        private readonly IHouseholdStore store;
        private readonly Func<DateTimeOffset> now;
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

        public HouseholdSession(BabySimulation simulation, IHouseholdStore store, Func<DateTimeOffset> now)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.now = now ?? throw new ArgumentNullException(nameof(now));

            var sessions = ProbeSlots();
            var manifest = ReadManifest(out var trusted);
            if (manifest == null)
                Rebuild(sessions);
            else
                Reconcile(manifest, trusted, sessions);

            // A first launch with nothing saved writes nothing until the first adoption.
            if (storedManifest != null || companions.Count > 0 || keepsakes.Count > 0)
                WriteManifest();
        }

        public int Capacity => HouseholdRules.FreeCompanions;
        public IReadOnlyList<HouseholdCompanion> Companions => companions.AsReadOnly();
        public IReadOnlyList<Keepsake> Keepsakes => keepsakes.AsReadOnly();
        public HouseholdCompanion Active => Find(activeSlot);
        public bool IsFull => companions.Count >= Capacity;
        public bool CanAdopt => !IsFull && FindFreeSlot() != 0;

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
            if (activeSlot == slot)
                return;

            activeSlot = slot;
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
            var companion = Find(slot) ?? throw new ArgumentException($"Slot {slot} has no companion.", nameof(slot));
            if (!companion.IsReadable)
                throw new InvalidOperationException("A save that couldn't be opened is kept, not removed.");

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
            // is not offered for adoption in this session.
            WriteManifest();
            leaving.Remove(slot);
        }

        // Advances every readable companion to now. Milestones of companions that are not on screen
        // are kept as pending celebrations; the active companion's unlocks are returned when it is shown.
        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvanceAll(bool activeOnScreen = true)
        {
            var shown = (false, false, false);
            var added = false;
            Exception failure = null;
            foreach (var companion in companions)
            {
                if (!companion.IsReadable)
                    continue;

                try
                {
                    var unlocked = companion.Session.Refresh();
                    if (activeOnScreen && companion.Slot == activeSlot)
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

            if (added)
                WriteManifest();
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
            var manifest = TryDecode(storedManifest);
            if (manifest != null)
            {
                trusted = true;
                lastValidManifest = storedManifest;
                return manifest;
            }

            var backup = store.ReadManifestBackup();
            manifest = TryDecode(backup);
            if (manifest != null)
                lastValidManifest = backup;
            return manifest;
        }

        private static HouseholdManifest TryDecode(string payload)
        {
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

        private int FirstReadableSlot()
        {
            foreach (var companion in companions)
            {
                if (companion.IsReadable)
                    return companion.Slot;
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
            var entries = new List<HouseholdEntry>();
            foreach (var companion in companions)
                entries.Add(new HouseholdEntry(companion.Slot, companion.AdoptedAt, leaving.Contains(companion.Slot)));

            var payload = HouseholdManifestCodec.Encode(new HouseholdManifest(entries, activeSlot, celebrations, keepsakes));
            if (payload == storedManifest)
                return;

            store.WriteManifest(payload, lastValidManifest);
            storedManifest = payload;
            lastValidManifest = payload;
        }
    }
}
