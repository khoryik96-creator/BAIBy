using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    public sealed class HouseholdCompanion
    {
        public int Slot { get; }
        public DateTimeOffset AdoptedAt { get; }
        public CompanionKind Kind { get; }

        // A baby's session; null for a pet, or when the slot's save couldn't be opened. An unreadable slot
        // still counts toward capacity and its bytes are kept, so it is never reused.
        public BabySession Session { get; }
        // A pet's session; null for a baby, or when the slot's save couldn't be opened.
        public PetSession PetSession { get; }
        public bool IsReadable => Session != null || PetSession != null;
        public bool IsPet => Kind == CompanionKind.Pet;
        // The baby's state; null for a pet.
        public BabyState State => Session?.State;
        // The pet's state; null for a baby.
        public PetState PetState => PetSession?.State;
        public string Name => Session?.State?.Name ?? PetSession?.State?.Name;

        // Whether Health or a shown need is below the household threshold, as committed.
        public bool NeedsAttention =>
            State != null ? HouseholdRules.NeedsAttention(State) : PetState != null && HouseholdRules.NeedsAttention(PetState);

        internal HouseholdCompanion(int slot, DateTimeOffset adoptedAt, BabySession session)
            : this(slot, adoptedAt, CompanionKind.Baby, session, null)
        {
        }

        internal HouseholdCompanion(int slot, DateTimeOffset adoptedAt, CompanionKind kind, BabySession session, PetSession petSession)
        {
            Slot = slot;
            AdoptedAt = adoptedAt;
            Kind = kind;
            Session = session;
            PetSession = petSession;
        }
    }

    // What a pet care action did: the cue to show, whether the pet grew up on the way, whether it
    // finished the species' care day, and any earned breeds that unlocked.
    public readonly struct PetCareOutcome
    {
        public PetCareOutcome(PetCue cue, bool becameGrown, bool completedCareDay, IReadOnlyList<PetBreed> unlockedBreeds)
        {
            Cue = cue;
            BecameGrown = becameGrown;
            CompletedCareDay = completedCareDay;
            UnlockedBreeds = unlockedBreeds ?? Array.Empty<PetBreed>();
        }

        public PetCue Cue { get; }
        public bool BecameGrown { get; }
        public bool CompletedCareDay { get; }
        public IReadOnlyList<PetBreed> UnlockedBreeds { get; }
    }

    // The single writer of household slots and the manifest. It caches one BabySession or PetSession per
    // companion. Slot 1 is the original single-baby key and only ever holds a baby, so a build from before
    // the household never reads a pet; pets take slots 2 and up.
    // Every awake companion advances on launch, resume and the session tick, never on a switch, so need
    // decay does not depend on which companion is on screen.
    //
    // Companions beyond Capacity, in Home order, are resting: after a refund, say. A resting companion is
    // read-only. It does not advance, so it does not decay, and its record is never written; its journal
    // can still be read. The player chooses who stays awake by swapping places in Home order, which the
    // manifest already saves. When capacity returns, the next advance gives each woken companion one normal
    // capped advance: needs decay for at most the cap, while age counts the whole rest, as after any absence,
    // so milestones reached meanwhile are celebrated on waking. Resting is computed at runtime and never
    // saved into records.
    public sealed class HouseholdSession
    {
        private readonly BabySimulation simulation;
        private readonly PetSimulation pets;
        private readonly IHouseholdStore store;
        private readonly Func<DateTimeOffset> now;
        private readonly IEntitlements entitlements;
        private readonly List<HouseholdCompanion> companions = new List<HouseholdCompanion>();
        private readonly List<PendingCelebration> celebrations = new List<PendingCelebration>();
        private readonly List<Keepsake> keepsakes = new List<Keepsake>();
        private readonly Dictionary<Species, SpeciesRecord> speciesRecords = new Dictionary<Species, SpeciesRecord>();
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
            : this(simulation, new PetSimulation(), store, now, entitlements)
        {
        }

        // entitlements decides capacity and which premium breeds are owned. Null means the free game.
        public HouseholdSession(BabySimulation simulation, PetSimulation pets, IHouseholdStore store, Func<DateTimeOffset> now,
            IEntitlements entitlements)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.pets = pets ?? throw new ArgumentNullException(nameof(pets));
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

        public int Capacity => HouseholdRules.CapacityFor(entitlements, companions.Count);
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

        // The resting companion that takes the next free room: the first one beyond capacity in Home order.
        // A goodbye to an awake companion wakes it. Null when nobody rests.
        public HouseholdCompanion NextToWake
        {
            get
            {
                var capacity = Capacity;
                return companions.Count > capacity ? companions[capacity] : null;
            }
        }

        public bool IsFull => companions.Count >= Capacity;
        // A baby can take any free slot, slot 1 first.
        public bool CanAdopt => !IsFull && FindFreeSlot(1) != 0;
        // A pet never takes slot 1.
        public bool CanAdoptPet => !IsFull && FindFreeSlot(FirstPetSlot) != 0;
        public PetCatalog PetCatalog => pets.Catalog;
        // A goodbye needs its keepsake written, which a manifest from a newer build does not allow.
        public bool CanSayGoodbye => !manifestFromNewerBuild;

        public HouseholdCompanion Adopt(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the baby.", nameof(name));
            if (IsFull)
                throw new InvalidOperationException("The household is full.");

            var slot = FindFreeSlot(1);
            if (slot == 0)
                throw new InvalidOperationException("No free household slot is available.");

            // The record is written first. If the manifest write is interrupted, the next launch finds
            // the record by probing and lists it again.
            var session = new BabySession(simulation, store.Slot(slot), now);
            session.Adopt(name);
            return Welcome(new HouseholdCompanion(slot, session.State.TimeCheckpoint, session));
        }

        // Adopts a pet with a name and look. The breed must be adoptable now: a free starter, an earned
        // breed whose goal the household has met, or a premium breed it owns, with its art shipped.
        public HouseholdCompanion AdoptPet(string name, PetAppearance appearance)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the pet.", nameof(name));
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));
            if (!CanAdoptBreed(appearance.Breed))
                throw new InvalidOperationException($"The {appearance.Breed} breed is not unlocked yet.");
            pets.Catalog.ValidateChoice(appearance);
            if (IsFull)
                throw new InvalidOperationException("The household is full.");

            var slot = FindFreeSlot(FirstPetSlot);
            if (slot == 0)
                throw new InvalidOperationException("No free household slot is available for a pet.");

            var session = new PetSession(pets, store.Slot(slot), now);
            session.Adopt(name, appearance);
            return Welcome(new HouseholdCompanion(slot, session.State.TimeCheckpoint, CompanionKind.Pet, null, session));
        }

        // Household-wide progress toward a species' earned breeds: the saved record plus what every
        // current pet of the species shows.
        public SpeciesGoalProgress ProgressFor(Species species) => RecordFor(species).Progress;

        // Owned or earned, whether or not the art has shipped.
        public bool IsBreedUnlocked(PetBreed breed) =>
            PetUnlocks.IsUnlocked(pets.Catalog, breed, ProgressFor(PetIds.SpeciesOf(breed)), OwnedProducts());

        // Unlocked and its art is available now.
        public bool CanAdoptBreed(PetBreed breed) =>
            PetIds.IsDefined(breed) && pets.Catalog.TryGet(breed, out _) &&
            PetUnlocks.CanAdopt(pets.Catalog, breed, ProgressFor(PetIds.SpeciesOf(breed)), OwnedProducts());

        // Cares for a pet: time and care commit in one record write. Care that counts toward the species'
        // care day is saved in the manifest, and earned breeds unlocked by it are returned once.
        public PetCareOutcome ApplyPetCare(int slot, PetVerb verb)
        {
            var companion = AwakePet(slot);
            var species = companion.PetState.Species;
            var before = RecordFor(species);
            var care = companion.PetSession.ApplyCare(verb);
            var kind = PetCareDays.KindOf(pets.Catalog.Get(species), verb);
            var day = before.CareDays.Record(kind, PetCareDays.DayNumber(now()));
            var completed = day.Completed;
            if (care.BecameGrown)
                AddCelebration(slot, CompanionMilestone.Grown);
            SaveProgress(species, before.WithCareDays(day.Days));
            return new PetCareOutcome(care.Cue, care.BecameGrown, completed, NewlyUnlocked(species, before));
        }

        // Playtest shortcut, like the nursery's Preview next day.
        public PetCareOutcome AdvancePetPreview(int slot, TimeSpan elapsed)
        {
            var companion = AwakePet(slot);
            var species = companion.PetState.Species;
            var before = RecordFor(species);
            var grown = companion.PetSession.AdvancePreview(elapsed);
            if (grown)
                AddCelebration(slot, CompanionMilestone.Grown);
            SaveProgress(species, before);
            return new PetCareOutcome(PetCue.Content, grown, false, NewlyUnlocked(species, before));
        }

        // The pet's state advanced to now without any write, for Home's minute tick.
        public PetState PreviewPetState(int slot)
        {
            var state = Find(slot)?.PetState;
            if (state == null || IsResting(slot))
                return state;

            return pets.Advance(state, now()).State;
        }

        private HouseholdCompanion Welcome(HouseholdCompanion companion)
        {
            companions.Add(companion);
            occupied.Add(companion.Slot);
            activeSlot = companion.Slot;
            WriteManifest();
            return companion;
        }

        private HouseholdCompanion AwakePet(int slot)
        {
            var companion = Find(slot) ?? throw new ArgumentException($"Slot {slot} has no companion.", nameof(slot));
            if (companion.PetSession?.State == null)
                throw new InvalidOperationException("This companion is not a pet that can be cared for.");
            if (IsResting(slot))
                throw new InvalidOperationException("This companion is resting.");
            return companion;
        }

        // Saves the species' progress and any new celebration. The pet's record is already saved, so a failed
        // manifest write keeps both in memory for the next write, which is retried with the next change. An
        // unchanged manifest is not rewritten.
        private void SaveProgress(Species species, SpeciesRecord updated)
        {
            speciesRecords[species] = updated;
            WriteManifest();
        }

        private IReadOnlyList<PetBreed> NewlyUnlocked(Species species, SpeciesRecord before)
        {
            var after = RecordFor(species).Progress;
            var unlocked = new List<PetBreed>();
            foreach (var breed in pets.Catalog.BreedsOf(species))
            {
                if (breed.Unlock == BreedUnlock.Earned && breed.Goal.HasValue &&
                    !PetUnlocks.IsGoalMet(breed.Goal.Value, before.Progress) && PetUnlocks.IsGoalMet(breed.Goal.Value, after))
                    unlocked.Add(breed.Breed);
            }

            return unlocked.AsReadOnly();
        }

        // The stored record merged with every readable pet of the species.
        private SpeciesRecord RecordFor(Species species)
        {
            var record = speciesRecords.TryGetValue(species, out var stored) ? stored : SpeciesRecord.Empty(species);
            foreach (var companion in companions)
                record = record.WithPet(companion.PetState);
            return record;
        }

        private ICollection<string> OwnedProducts()
        {
            var owned = entitlements.Owned;
            return owned == null ? (ICollection<string>)Array.Empty<string>() : owned.ToList();
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
                Refresh(resting);

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
            if (Refresh(waking))
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
                var keepsake = KeepsakeOf(companion);
                // A pet's Bond and growth stay with its species, so earned breeds outlive the goodbye.
                var pet = companion.PetState;
                SpeciesRecord stored = null;
                var hadRecord = pet != null && speciesRecords.TryGetValue(pet.Species, out stored);
                if (pet != null)
                    speciesRecords[pet.Species] = RecordFor(pet.Species);
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
                    if (hadRecord)
                        speciesRecords[pet.Species] = stored;
                    else if (pet != null)
                        speciesRecords.Remove(pet.Species);
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
                    if (companion.PetSession != null)
                    {
                        // A pet's growth always waits as a celebration; its room plays it.
                        added |= Refresh(companion);
                        continue;
                    }

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

        // Advances one companion to now and keeps what it reached as pending celebrations.
        private bool Refresh(HouseholdCompanion companion)
        {
            if (companion.PetSession != null)
                return companion.PetSession.Refresh() && AddCelebration(companion.Slot, CompanionMilestone.Grown);

            return companion.Session != null && AddCelebrations(companion.Slot, companion.Session.Refresh());
        }

        private Keepsake KeepsakeOf(HouseholdCompanion companion)
        {
            var pet = companion.PetState;
            if (pet != null)
                return new Keepsake(pet.Name, companion.AdoptedAt, now(), pet.Age, pet.Stage, pet.Appearance);

            var state = companion.State;
            return new Keepsake(state.Name, companion.AdoptedAt, now(), state.Age, HouseholdRules.StageOf(state), state.Appearance);
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

        private Dictionary<int, HouseholdCompanion> ProbeSlots()
        {
            // PlayerPrefs cannot list keys, so every physical slot is probed. Each record says what it
            // holds by its root element, so a slot opens as a baby or a pet whatever the manifest says.
            var probed = new Dictionary<int, HouseholdCompanion>();
            for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            {
                var saves = store.Slot(slot);
                var primary = saves.Read();
                var backup = saves.ReadBackup();
                if (primary == null && backup == null)
                    continue;

                occupied.Add(slot);
                var kind = KindOf(primary) ?? KindOf(backup);
                try
                {
                    probed.Add(slot, kind == CompanionKind.Pet
                        ? new HouseholdCompanion(slot, default, CompanionKind.Pet, null, new PetSession(pets, saves, now))
                        : new HouseholdCompanion(slot, default, CompanionKind.Baby, new BabySession(simulation, saves, now), null));
                }
                catch (InvalidDataException)
                {
                    // Unreadable: kept as an occupied slot without a session.
                    probed.Add(slot, Unreadable(slot, kind));
                }
                catch (NotSupportedException)
                {
                    // Written by a newer build: kept as an occupied slot without a session.
                    probed.Add(slot, Unreadable(slot, kind));
                }
            }

            return probed;
        }

        private static HouseholdCompanion Unreadable(int slot, CompanionKind? kind) =>
            new HouseholdCompanion(slot, default, kind ?? CompanionKind.Baby, null, null);

        // A record's kind from its root element: <pet ...> or <baby ...>. Null when the payload is missing
        // or too damaged to tell.
        private static CompanionKind? KindOf(string payload)
        {
            if (payload == null)
                return null;

            var text = payload.TrimStart();
            if (text.StartsWith("<pet", StringComparison.Ordinal))
                return CompanionKind.Pet;
            if (text.StartsWith("<baby", StringComparison.Ordinal))
                return CompanionKind.Baby;
            return null;
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

        private void Rebuild(Dictionary<int, HouseholdCompanion> sessions)
        {
            for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            {
                if (occupied.Contains(slot))
                    AddProbed(slot, sessions, null, null);
            }

            activeSlot = FirstReadableSlot();
        }

        private void Reconcile(HouseholdManifest manifest, bool trusted, Dictionary<int, HouseholdCompanion> sessions)
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
                    AddProbed(entry.Slot, sessions, entry.AdoptedAt, entry.Kind);
            }

            // Records the manifest does not list, for example after an interrupted adoption.
            for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            {
                if (occupied.Contains(slot) && Find(slot) == null)
                    AddProbed(slot, sessions, null, null);
            }

            foreach (var celebration in manifest.Celebrations)
            {
                var companion = Find(celebration.Slot);
                if (companion != null && companion.IsReadable)
                    celebrations.Add(celebration);
            }

            keepsakes.AddRange(manifest.Keepsakes);
            foreach (var record in manifest.SpeciesRecords)
                speciesRecords[record.Species] = record;
            var active = Find(manifest.ActiveSlot);
            activeSlot = active != null && active.IsReadable ? active.Slot : FirstReadableSlot();
        }

        // knownKind is the manifest's word for the slot. It only names an unreadable slot's kind: a
        // readable record's own root element wins.
        private void AddProbed(int slot, Dictionary<int, HouseholdCompanion> sessions, DateTimeOffset? adoptedAt,
            CompanionKind? knownKind)
        {
            sessions.TryGetValue(slot, out var probed);
            var kind = probed != null && probed.IsReadable ? probed.Kind : knownKind ?? probed?.Kind ?? CompanionKind.Baby;
            companions.Add(new HouseholdCompanion(slot, adoptedAt ?? EstimateAdoption(probed), kind,
                probed?.Session, probed?.PetSession));
        }

        // Records do not store the adoption date, so a rebuilt entry estimates it from age.
        private DateTimeOffset EstimateAdoption(HouseholdCompanion probed)
        {
            DateTimeOffset checkpoint;
            TimeSpan age;
            if (probed?.State != null)
            {
                checkpoint = probed.State.TimeCheckpoint;
                age = probed.State.Age;
            }
            else if (probed?.PetState != null)
            {
                checkpoint = probed.PetState.TimeCheckpoint;
                age = probed.PetState.Age;
            }
            else
            {
                return now();
            }

            var ticks = checkpoint.UtcDateTime.Ticks;
            return new DateTimeOffset(ticks > age.Ticks ? ticks - age.Ticks : ticks, TimeSpan.Zero);
        }

        // Pets start at slot 2: slot 1 is the original baby key that older builds read.
        private const int FirstPetSlot = 2;

        private int FindFreeSlot(int firstSlot)
        {
            for (var slot = firstSlot; slot <= HouseholdRules.PhysicalSlots; slot++)
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

        // Species with a stored record or a readable pet, in ID order so the manifest encodes stably.
        private IEnumerable<Species> SpeciesWithProgress()
        {
            var species = new SortedSet<Species>(speciesRecords.Keys);
            foreach (var companion in companions)
            {
                if (companion.PetState != null)
                    species.Add(companion.PetState.Species);
            }

            return species;
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
                entries.Add(new HouseholdEntry(companion.Slot, companion.AdoptedAt, leaving.Contains(companion.Slot),
                    companion.Slot == 1 ? CompanionKind.Baby : companion.Kind));

            // Each species keeps the best Bond and growth its pets have shown, so a goodbye never loses them.
            var records = new List<SpeciesRecord>();
            foreach (var species in SpeciesWithProgress())
                records.Add(RecordFor(species));

            var payload = HouseholdManifestCodec.Encode(new HouseholdManifest(entries, activeSlot, celebrations, keepsakes, records));
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
