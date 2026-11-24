using System;
using BAIby.Simulation.Pets;

namespace BAIby.Application
{
    // One pet companion's record, the pet counterpart of BabySession: restore with backup fallback, and
    // one write per change. Breed unlocks are the household's concern (HouseholdSession.AdoptPet).
    public sealed class PetSession
    {
        private readonly PetSimulation simulation;
        private readonly SavedRecord<PetState> record;
        private readonly Func<DateTimeOffset> now;

        // Null until a pet is adopted or a saved pet is restored.
        public PetState State => record.Value;

        public PetCatalog Catalog => simulation.Catalog;

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

            record.Commit(simulation.Adopt(name, appearance, now()));
        }

        // Advances to now. Returns true when the pet grew up during this advance; it is saved once.
        public bool Refresh()
        {
            if (State == null)
                return false;

            var update = simulation.Advance(State, now());
            if (update.State != State)
                record.Commit(update.State);

            return update.BecameGrown;
        }

        public bool CanUse(PetVerb verb) => State != null && simulation.CanUse(State, verb);

        // Time and care commit together in one write, as for the baby.
        public (PetCue Cue, bool BecameGrown) ApplyCare(PetVerb verb)
        {
            var elapsed = simulation.Advance(GetAdoptedPet(), now());
            var caredFor = simulation.ApplyCare(elapsed.State, verb);
            record.Commit(caredFor.State);
            return (caredFor.Cue, elapsed.BecameGrown);
        }

        // Playtest shortcut: advances the pet's own clock by elapsed without touching the real checkpoint.
        public bool AdvancePreview(TimeSpan elapsed)
        {
            var current = simulation.Advance(GetAdoptedPet(), now());
            var preview = simulation.AdvancePreview(current.State, elapsed);
            record.Commit(preview.State);
            return current.BecameGrown || preview.BecameGrown;
        }

        public void SetAppearance(PetAppearance appearance)
        {
            var updated = GetAdoptedPet().WithAppearance(appearance);
            if (updated != State)
                record.Commit(updated);
        }

        private PetState GetAdoptedPet() =>
            State ?? throw new InvalidOperationException("Adopt a pet first.");
    }
}
