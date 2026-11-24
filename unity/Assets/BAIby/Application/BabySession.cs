using System;
using System.Collections.Generic;
using BAIby.Simulation;

namespace BAIby.Application
{
    public sealed class BabySession
    {
        private readonly BabySimulation simulation;
        private readonly SavedRecord<BabyState> record;
        private readonly Func<DateTimeOffset> now;

        // Null until the player adopts a baby or a saved baby is restored.
        public BabyState State => record.Value;

        // Developer read-out of the care ledger; empty before adoption.
        public string CareSummary => State == null ? string.Empty : simulation.DescribeCare(State);

        // Today's routine windows as of the last refresh; empty before adoption.
        public IReadOnlyList<RoutineSlot> Routine =>
            State == null ? (IReadOnlyList<RoutineSlot>)Array.Empty<RoutineSlot>() : BabySimulation.Routine(State);

        // Whether feeding now would overfeed.
        public bool IsFull => State != null && simulation.IsFull(State);

        public BabySession(BabySimulation simulation, IBabySaveStore saves, Func<DateTimeOffset> now)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            if (saves == null)
                throw new ArgumentNullException(nameof(saves));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            record = new SavedRecord<BabyState>(saves, BabySaveCodec.Decode, BabySaveCodec.Encode);
        }

        public void Adopt(string name)
        {
            if (State != null)
                throw new InvalidOperationException("The session already has an adopted baby.");

            Commit(simulation.Adopt(name, now()));
        }

        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) Refresh()
        {
            if (State == null)
                return (false, false, false);

            var update = simulation.Advance(State, now());
            if (update.State != State)
                Commit(update.State);

            return (update.UnlockedSitting, update.UnlockedCrawling, update.UnlockedWalking);
        }

        public (Reaction Reaction, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) ApplyCare(
            CareAction action, Toy toy = Toy.Rattle, Food food = Food.Milk)
        {
            var elapsed = simulation.Advance(GetAdoptedBaby(), now());
            var caredFor = simulation.ApplyCare(elapsed.State, action, toy, food);
            Commit(caredFor.State);
            return (caredFor.Reaction, elapsed.UnlockedSitting, elapsed.UnlockedCrawling, elapsed.UnlockedWalking);
        }

        // Whether the doctor's visit is offered (the baby is under the weather or sick), as of the last refresh.
        public bool CanVisitDoctor => State != null && BabySimulation.CanVisitDoctor(State);

        // Whether a medicine dose would count, as of the last refresh.
        public MedicineState Medicine => State == null ? MedicineState.NotNeeded : BabySimulation.Medicine(State);

        // The free doctor's visit, at the current instant.
        public (Reaction Reaction, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) VisitDoctor()
        {
            var elapsed = simulation.Advance(GetAdoptedBaby(), now());
            var visit = simulation.VisitDoctor(elapsed.State);
            if (visit.State != State)
                Commit(visit.State);
            return (visit.Reaction, elapsed.UnlockedSitting, elapsed.UnlockedCrawling, elapsed.UnlockedWalking);
        }

        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvancePreview(TimeSpan elapsed)
        {
            var current = simulation.Advance(GetAdoptedBaby(), now());
            var preview = simulation.AdvancePreview(current.State, elapsed);
            Commit(preview.State);
            return (current.UnlockedSitting || preview.UnlockedSitting,
                current.UnlockedCrawling || preview.UnlockedCrawling,
                current.UnlockedWalking || preview.UnlockedWalking);
        }

        // Starts Away mode now, for up to 14 days. Any care, or EndAway, brings the baby back to normal.
        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) StartAway(TimeSpan duration)
        {
            var current = simulation.Advance(GetAdoptedBaby(), now());
            Commit(simulation.StartAway(current.State, duration));
            return (current.UnlockedSitting, current.UnlockedCrawling, current.UnlockedWalking);
        }

        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) EndAway()
        {
            var current = simulation.Advance(GetAdoptedBaby(), now());
            var back = simulation.EndAway(current.State);
            if (back != State)
                Commit(back);

            return (current.UnlockedSitting, current.UnlockedCrawling, current.UnlockedWalking);
        }

        public void SetCareStyle(CareStyle style)
        {
            var updated = simulation.ChangeCareStyle(GetAdoptedBaby(), style);
            if (updated != State)
                Commit(updated);
        }

        public void SetNurseryTheme(NurseryTheme theme)
        {
            var updated = GetAdoptedBaby().WithNurseryTheme(theme);
            if (updated != State)
                Commit(updated);
        }

        public void SetAppearance(BabyAppearance appearance)
        {
            var updated = GetAdoptedBaby().WithAppearance(appearance);
            if (updated != State)
                Commit(updated);
        }

        private BabyState GetAdoptedBaby() =>
            State ?? throw new InvalidOperationException("Adopt a baby first.");

        private void Commit(BabyState updated) => record.Commit(updated);
    }
}
