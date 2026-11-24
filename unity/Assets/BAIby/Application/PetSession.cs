using System;
using System.Collections.Generic;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    // One pet companion's record, the pet counterpart of BabySession: restore with backup fallback, and
    // one write per change. Breed unlocks are the household's concern (HouseholdSession.AdoptPet).
    // A species with a care profile (the cat) also offers the baby's care systems: the routine, the vet and medicine,
    // photos, Away mode, growth, Bond and the journal.
    public sealed class PetSession
    {
        private static readonly MotorSkill[] NoSkills = new MotorSkill[0];
        private readonly PetSimulation simulation;
        private readonly SavedRecord<PetState> record;
        private readonly Func<DateTimeOffset> now;
        private readonly List<HelperEvent> helperEvents = new List<HelperEvent>();

        // The household's helpers (owned and switched on), read at every advance; null means none. Only a species with a
        // care profile (the cat) has helpers.
        public Func<HelperSettings> Helpers { get; set; }

        // Whether the pet can have helpers (its species has a care profile).
        public bool HasHelpers => PetSimulation.HasHelpers(State);

        // Portions left in the auto-feeder; 0 before adoption or without a care profile.
        public int FeederStock => HasHelpers ? State.Care?.Helpers.Stock ?? 0 : 0;
        public int FeederCapacity => simulation.HelperRules.MaxStock;

        // Null until a pet is adopted or a saved pet is restored.
        public PetState State => record.Value;

        public PetCatalog Catalog => simulation.Catalog;

        // The milestones the last call reached on the way (time passing before care, a refresh or a preview day).
        public IReadOnlyList<MotorSkill> Reached { get; private set; } = NoSkills;

        // Whether the pet runs on the shared care systems (its species has a care profile).
        public bool HasCare => PetSimulation.HasCareProfile(State);

        // Read-outs as of the last refresh; empty or null before adoption and for a species without a care profile.
        public IReadOnlyList<RoutineSlot> Routine =>
            HasCare ? simulation.Routine(State) : (IReadOnlyList<RoutineSlot>)Array.Empty<RoutineSlot>();
        public GrowthReport Growth => HasCare ? simulation.Growth(State) : null;
        public BondRecord Bonding => HasCare ? simulation.Bonding(State) : null;
        public JournalRecord Journal => HasCare ? simulation.Journal(State) : null;
        public SecurityPhase Trust => HasCare ? simulation.Trust(State) : SecurityPhase.Steady;
        public CuriosityPhase Curiosity => HasCare ? simulation.Curiosity(State) : CuriosityPhase.Steady;
        public int PhotosLeft => HasCare ? simulation.PhotosLeft(State) : 0;
        public bool IsFull => HasCare && simulation.IsFull(State);
        public bool CanVisitVet => PetSimulation.CanVisitVet(State);
        public MedicineState Medicine => PetSimulation.Medicine(State);
        public int MedicineCourse => simulation.RecoveryDoses;
        public string CareSummary => State == null ? string.Empty : simulation.DescribeCare(State);

        public PetSession(PetSimulation simulation, IBabySaveStore saves, Func<DateTimeOffset> now)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            if (saves == null)
                throw new ArgumentNullException(nameof(saves));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            var catalog = simulation.Catalog;
            record = new SavedRecord<PetState>(saves, payload => PetSaveCodec.Decode(payload, catalog), PetSaveCodec.Encode);
        }

        public void Adopt(string name, PetAppearance appearance)
        {
            if (State != null)
                throw new InvalidOperationException("The session already has an adopted pet.");

            Reached = NoSkills;
            record.Commit(simulation.Adopt(name, appearance, now()));
        }

        // Advances to now. Returns true when the pet grew up during this advance; it is saved once.
        public bool Refresh()
        {
            Reached = NoSkills;
            if (State == null)
                return false;

            var update = Step(State);
            Reached = update.Reached;
            if (update.State != State)
                record.Commit(update.State);

            return update.BecameGrown;
        }

        public bool CanUse(PetVerb verb) => State != null && simulation.CanUse(State, verb);

        // Time and care commit together in one write, as for the baby.
        public (PetCue Cue, bool BecameGrown) ApplyCare(PetVerb verb)
        {
            var elapsed = Elapse();
            var caredFor = simulation.Care(elapsed.State, verb);
            record.Commit(caredFor.State);
            return (caredFor.Cue, elapsed.BecameGrown);
        }

        // The free vet's visit, at the current instant (a species with a care profile).
        public (PetCue Cue, bool BecameGrown) VisitVet()
        {
            var elapsed = Elapse();
            var visit = simulation.VisitVet(elapsed.State);
            if (visit.State != State)
                record.Commit(visit.State);
            return (visit.Cue, elapsed.BecameGrown);
        }

        // A medicine dose with a meal, at the current instant: one per meal window, only while recovering.
        public (PetCue Cue, bool BecameGrown) GiveMedicine()
        {
            var elapsed = Elapse();
            var dose = simulation.GiveMedicine(elapsed.State);
            record.Commit(dose.State);
            return (dose.Cue, elapsed.BecameGrown);
        }

        // A journal photo of the moment. Returns false (and writes nothing new) once today's three photos are taken.
        public (bool Taken, bool BecameGrown) TakePhoto(PhotoMoment moment)
        {
            var elapsed = Elapse();
            var photo = simulation.TakePhoto(elapsed.State, moment);
            if (photo.State != State)
                record.Commit(photo.State);
            return (photo.Taken, elapsed.BecameGrown);
        }

        // Away mode now, for up to 14 days. Any care, or EndAway, brings the pet back to normal.
        public bool StartAway(TimeSpan duration)
        {
            var elapsed = Elapse();
            record.Commit(simulation.StartAway(elapsed.State, duration));
            return elapsed.BecameGrown;
        }

        public bool EndAway()
        {
            var elapsed = Elapse();
            var back = simulation.EndAway(elapsed.State);
            if (back != State)
                record.Commit(back);
            return elapsed.BecameGrown;
        }

        // Playtest shortcut: advances the pet's own clock by elapsed without touching the real checkpoint.
        public bool AdvancePreview(TimeSpan elapsed)
        {
            var current = Elapse();
            var preview = simulation.PreviewStep(current.State, elapsed, CurrentHelpers, helperEvents);
            Trim();
            Reached = Join(Reached, preview.Reached);
            record.Commit(preview.State);
            return current.BecameGrown || preview.BecameGrown;
        }

        // Gentle or Standard care: how strongly the consequence ladder bites. Stored now, applied at once.
        public void SetCareStyle(CareStyle style)
        {
            var updated = simulation.ChangeCareStyle(GetAdoptedPet(), style);
            if (updated != State)
                record.Commit(updated);
        }

        public void SetAppearance(PetAppearance appearance)
        {
            var updated = GetAdoptedPet().WithAppearance(appearance);
            if (updated != State)
                record.Commit(updated);
        }

        // What the helpers did since the last call, oldest first, for the return summary. Each event is returned once.
        public IReadOnlyList<HelperEvent> TakeHelperEvents()
        {
            var taken = helperEvents.ToArray();
            helperEvents.Clear();
            return taken;
        }

        // Fills the auto-feeder at the current instant. Free; it is not care.
        public bool StockFeeder()
        {
            var elapsed = Elapse();
            if (!HasHelpers)
                return elapsed.BecameGrown;
            var stocked = simulation.StockFeeder(elapsed.State);
            if (stocked != State)
                record.Commit(stocked);
            return elapsed.BecameGrown;
        }

        private HelperSettings CurrentHelpers => Helpers?.Invoke() ?? HelperSettings.None;

        // Advances to now without a write; the caller commits with what it does next.
        private PetStep Elapse()
        {
            var step = Step(GetAdoptedPet());
            Reached = step.Reached;
            return step;
        }

        private PetStep Step(PetState state)
        {
            var step = simulation.Step(state, now(), CurrentHelpers, helperEvents);
            Trim();
            return step;
        }

        // Unread events are kept for a while, newest last.
        private void Trim()
        {
            const int kept = 64;
            if (helperEvents.Count > kept)
                helperEvents.RemoveRange(0, helperEvents.Count - kept);
        }

        private static IReadOnlyList<MotorSkill> Join(IReadOnlyList<MotorSkill> first, IReadOnlyList<MotorSkill> second)
        {
            if (second.Count == 0)
                return first;
            if (first.Count == 0)
                return second;
            var joined = new List<MotorSkill>(first);
            joined.AddRange(second);
            return joined;
        }

        private PetState GetAdoptedPet() =>
            State ?? throw new InvalidOperationException("Adopt a pet first.");
    }
}
