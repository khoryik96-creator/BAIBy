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
        private readonly List<HelperEvent> helperEvents = new List<HelperEvent>();
        private bool firstWordsHeard;

        // The household's helpers (owned and switched on), read at every advance; null means none.
        public Func<HelperSettings> Helpers { get; set; }

        // Whether a grown-up let children type in Talk time (the household setting); null means no. Without it typed
        // words are ignored and only the feeling chips count.
        public Func<bool> TalkTyping { get; set; }

        public bool CanType => TalkTyping?.Invoke() ?? false;

        // Whether Talk time is open (First words reached), as of the last refresh.
        public bool CanTalk => State != null && TalkRules.IsOpen(State);

        // Feeder portions left (the Sitter's bottles); 0 before adoption.
        public int FeederStock => State?.Care.Helpers.Stock ?? 0;
        public int FeederCapacity => simulation.HelperRules.MaxStock;

        // Null until the player adopts a baby or a saved baby is restored.
        public BabyState State => record.Value;

        // Developer read-out of the care ledger; empty before adoption.
        public string CareSummary => State == null ? string.Empty : simulation.DescribeCare(State);

        // Today's routine windows as of the last refresh; empty before adoption.
        public IReadOnlyList<RoutineSlot> Routine =>
            State == null ? (IReadOnlyList<RoutineSlot>)Array.Empty<RoutineSlot>() : BabySimulation.Routine(State);

        // Development for the Growth panel and the report (track qualities, the growth chart band, the next skill
        // and when it is likely), as of the last refresh; null before adoption.
        public GrowthReport Growth => State == null ? null : simulation.Growth(State);

        // Whether feeding now would overfeed.
        public bool IsFull => State != null && simulation.IsFull(State);

        // Bond's high-water mark, temperament and the care rhythm, as of the last refresh; null before adoption.
        public BondRecord Bonding => State == null ? null : simulation.Bonding(State);

        // Where temperament stands (a clingy phase, settled, cautious or an explorer); Steady before adoption.
        public SecurityPhase Security => State == null ? SecurityPhase.Steady : simulation.Security(State);
        public CuriosityPhase Curiosity => State == null ? CuriosityPhase.Steady : simulation.Curiosity(State);

        // The journal, as of the last refresh; null before adoption.
        public JournalRecord Journal => State == null ? null : simulation.Journal(State);

        // Photos left today (three a day).
        public int PhotosLeft => State == null ? 0 : simulation.PhotosLeft(State);

        // Whether a look can be put on: earned looks need their care rhythm unless earned looks are unlocked (developer
        // mode).
        public bool CanWear(BabyAppearance appearance, bool unlockEarned = false) =>
            State != null && appearance != null && simulation.CanWear(State, appearance, unlockEarned);

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

            var update = Advance(State);
            if (update.State != State)
                Commit(update.State);

            return (update.UnlockedSitting, update.UnlockedCrawling, update.UnlockedWalking);
        }

        public (Reaction Reaction, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) ApplyCare(
            CareAction action, Toy toy = Toy.Rattle, Food food = Food.Milk)
        {
            var elapsed = Advance(GetAdoptedBaby());
            var caredFor = simulation.ApplyCare(elapsed.State, action, toy, food);
            Commit(caredFor.State);
            return (caredFor.Reaction, elapsed.UnlockedSitting, elapsed.UnlockedCrawling, elapsed.UnlockedWalking);
        }

        // Whether the doctor's visit is offered (the baby is under the weather or sick), as of the last refresh.
        public bool CanVisitDoctor => State != null && BabySimulation.CanVisitDoctor(State);

        // Medicine doses that end Recovering.
        public int MedicineCourse => simulation.RecoveryDoses;

        // Whether a medicine dose would count, as of the last refresh.
        public MedicineState Medicine => State == null ? MedicineState.NotNeeded : BabySimulation.Medicine(State);

        // The free doctor's visit, at the current instant.
        public (Reaction Reaction, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) VisitDoctor()
        {
            var elapsed = Advance(GetAdoptedBaby());
            var visit = simulation.VisitDoctor(elapsed.State);
            if (visit.State != State)
                Commit(visit.State);
            return (visit.Reaction, elapsed.UnlockedSitting, elapsed.UnlockedCrawling, elapsed.UnlockedWalking);
        }

        // A journal photo of the moment, at the current instant. Returns false (and writes nothing) once today's three
        // photos are taken.
        public (bool Taken, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) TakePhoto(PhotoMoment moment)
        {
            var current = Advance(GetAdoptedBaby());
            var photo = simulation.TakePhoto(current.State, moment);
            if (photo.State != State)
                Commit(photo.State);
            return (photo.Taken, current.UnlockedSitting, current.UnlockedCrawling, current.UnlockedWalking);
        }

        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) AdvancePreview(TimeSpan elapsed)
        {
            var current = Advance(GetAdoptedBaby());
            var preview = simulation.AdvancePreview(current.State, elapsed, CurrentHelpers, helperEvents);
            Trim();
            NoteFirstWords(current.State, preview.State);
            Commit(preview.State);
            return (current.UnlockedSitting || preview.UnlockedSitting,
                current.UnlockedCrawling || preview.UnlockedCrawling,
                current.UnlockedWalking || preview.UnlockedWalking);
        }

        // Starts Away mode now, for up to 14 days. Any care, or EndAway, brings the baby back to normal.
        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) StartAway(TimeSpan duration)
        {
            var current = Advance(GetAdoptedBaby());
            Commit(simulation.StartAway(current.State, duration));
            return (current.UnlockedSitting, current.UnlockedCrawling, current.UnlockedWalking);
        }

        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) EndAway()
        {
            var current = Advance(GetAdoptedBaby());
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

        // Earned looks (a care rhythm's reward) can only be put on once earned, unless unlockEarned (developer mode).
        public void SetAppearance(BabyAppearance appearance, bool unlockEarned = false)
        {
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));
            if (!simulation.CanWear(GetAdoptedBaby(), appearance, unlockEarned))
                throw new InvalidOperationException("This look is earned with a longer care rhythm.");

            var updated = GetAdoptedBaby().WithAppearance(appearance);
            if (updated != State)
                Commit(updated);
        }

        // Talk time at the current instant: a feeling chip and, when typing is allowed, a few typed words (read on the
        // device, never saved, logged or sent). Returns the little one's reply and any milestones the advance reached.
        public (TalkReply Reply, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) Talk(Feeling? feeling,
            string typed = null)
        {
            var current = Advance(GetAdoptedBaby());
            var talked = simulation.Talk(current.State, feeling, CanType ? typed : null);
            if (talked.State != State)
                Commit(talked.State);
            return (talked.Reply, current.UnlockedSitting, current.UnlockedCrawling, current.UnlockedWalking);
        }

        // Whether First words came since the last call (an advance or a preview day reached them). Returns true once.
        public bool TakeFirstWords()
        {
            var heard = firstWordsHeard;
            firstWordsHeard = false;
            return heard;
        }

        // What the helpers did since the last call, oldest first, for the return summary. Each event is returned once.
        public IReadOnlyList<HelperEvent> TakeHelperEvents()
        {
            var taken = helperEvents.ToArray();
            helperEvents.Clear();
            return taken;
        }

        // Fills the Sitter's bottles (the feeder) at the current instant. Free; it is not care.
        public (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) StockFeeder()
        {
            var current = Advance(GetAdoptedBaby());
            var stocked = simulation.StockFeeder(current.State);
            if (stocked != State)
                Commit(stocked);
            return (current.UnlockedSitting, current.UnlockedCrawling, current.UnlockedWalking);
        }

        private HelperSettings CurrentHelpers => Helpers?.Invoke() ?? HelperSettings.None;

        private (BabyState State, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) Advance(BabyState state)
        {
            var update = simulation.Advance(state, now(), CurrentHelpers, helperEvents);
            Trim();
            NoteFirstWords(state, update.State);
            return update;
        }

        private void NoteFirstWords(BabyState before, BabyState after)
        {
            if (!before.HasFirstWords && after.HasFirstWords)
                firstWordsHeard = true;
        }

        // Unread events are kept for a while, newest last.
        private void Trim()
        {
            const int kept = 64;
            if (helperEvents.Count > kept)
                helperEvents.RemoveRange(0, helperEvents.Count - kept);
        }

        private BabyState GetAdoptedBaby() =>
            State ?? throw new InvalidOperationException("Adopt a baby first.");

        private void Commit(BabyState updated) => record.Commit(updated);
    }
}
