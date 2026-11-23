using System;
using System.IO;
using BAIby.Simulation;

namespace BAIby.Application
{
    public sealed class BabySession
    {
        private readonly BabySimulation simulation;
        private readonly IBabySaveStore saves;
        private readonly Func<DateTimeOffset> now;
        private string lastSavedPayload;

        // Null until the player adopts a baby or a saved baby is restored.
        public BabyState State { get; private set; }

        // Developer read-out of the shadow care ledger; empty before adoption.
        public string CareSummary => State == null ? string.Empty : simulation.DescribeCare(State);

        public BabySession(BabySimulation simulation, IBabySaveStore saves, Func<DateTimeOffset> now)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
            this.now = now ?? throw new ArgumentNullException(nameof(now));

            var primary = saves.Read();
            if (primary != null)
            {
                try
                {
                    Restore(primary);
                    return;
                }
                catch (InvalidDataException)
                {
                    var fallback = saves.ReadBackup();
                    if (fallback == null)
                        throw;

                    Restore(fallback);
                    return;
                }
            }

            var backup = saves.ReadBackup();
            if (backup != null)
                Restore(backup);
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

        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvancePreview(TimeSpan elapsed)
        {
            var current = simulation.Advance(GetAdoptedBaby(), now());
            var preview = simulation.AdvancePreview(current.State, elapsed);
            Commit(preview.State);
            return (current.UnlockedSitting || preview.UnlockedSitting,
                current.UnlockedCrawling || preview.UnlockedCrawling,
                current.UnlockedWalking || preview.UnlockedWalking);
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

        private void Restore(string payload)
        {
            State = BabySaveCodec.Decode(payload);
            lastSavedPayload = payload;
        }

        private void Commit(BabyState updated)
        {
            var payload = BabySaveCodec.Encode(updated);
            saves.Write(payload, lastSavedPayload);
            lastSavedPayload = payload;
            State = updated;
        }
    }
}
