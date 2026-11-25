using System;

namespace BAIby.Simulation.Pets
{
    // How big a dog's welcome is when the player comes back (docs/pet-mode.md: the reunion greeting scaled to time away).
    // Display only, never saved.
    public enum ReunionScale { None, TailWag, Zoomies, BiggestWelcome }

    // A dog's signature moment. Time away is measured from the last hands-on care (CareRecord.LastCareAt), so Away mode
    // counts as time away and any care ends the greeting. The greeting is always joy, never guilt: a longer absence
    // only makes the welcome bigger, and no line mentions how long the player was gone.
    public static class PetReunion
    {
        // A quick trip out gets no fuss; a school day or a night a wagging tail; most of a day and more, zoomies; two
        // days or more (a holiday with Away mode) the biggest welcome.
        public static readonly TimeSpan TailWagAfter = TimeSpan.FromHours(2);
        public static readonly TimeSpan ZoomiesAfter = TimeSpan.FromHours(16);
        public static readonly TimeSpan BiggestAfter = TimeSpan.FromHours(48);

        public static ReunionScale ScaleFor(TimeSpan away) =>
            away >= BiggestAfter ? ReunionScale.BiggestWelcome
            : away >= ZoomiesAfter ? ReunionScale.Zoomies
            : away >= TailWagAfter ? ReunionScale.TailWag
            : ReunionScale.None;

        // Time since the last hands-on care at the pet's checkpoint, or zero for a pet without a ledger yet.
        public static TimeSpan TimeAway(PetState state)
        {
            if (state?.Care == null || state.Care.AwaitingAnchor)
                return TimeSpan.Zero;
            var away = state.TimeCheckpoint - state.Care.LastCareAt;
            return away < TimeSpan.Zero ? TimeSpan.Zero : away;
        }

        // The greeting at the pet's checkpoint: only a species whose signature moment is the reunion greets (the dog).
        public static ReunionScale Of(PetState state, PetCatalog catalog)
        {
            if (state == null || catalog == null)
                return ReunionScale.None;
            var greets = false;
            foreach (var moment in catalog.Get(state.Species).Moments)
                greets |= moment == PetMoment.ReunionGreeting;
            return greets ? ScaleFor(TimeAway(state)) : ReunionScale.None;
        }

        // How many happy bounces the welcome has, and how high (in pet heights): more the longer the time away.
        public static int Bounces(ReunionScale scale) => scale == ReunionScale.None ? 0 : (int)scale + 1;

        public static double BounceHeight(ReunionScale scale) =>
            scale == ReunionScale.BiggestWelcome ? 0.16
            : scale == ReunionScale.Zoomies ? 0.12
            : scale == ReunionScale.TailWag ? 0.06 : 0;

        // A fish's signature moment (P4, docs/pet-mode.md: "Swims to the glass"): back after a while (the dog's first step,
        // two hours since the last hands-on care), the fish swims straight to the glass to say hello. Display only, like
        // the dog's welcome, and never about how long anyone was gone. A resting fish (lights off) stays asleep.
        public static bool SwimsToGlass(PetState state, PetCatalog catalog)
        {
            if (state == null || catalog == null)
                return false;
            var swims = false;
            foreach (var moment in catalog.Get(state.Species).Moments)
                swims |= moment == PetMoment.SwimsToGlass;
            return swims && TimeAway(state) >= TailWagAfter;
        }

        public static string GlassLine(string name) => $"{name} swims straight to the glass to say hello!";

        // The welcome line: warm, and never about how long anyone was gone.
        public static string Line(ReunionScale scale, string name)
        {
            switch (scale)
            {
                case ReunionScale.TailWag: return $"{name}'s tail is wagging so fast! Welcome home.";
                case ReunionScale.Zoomies: return $"Zoomies! {name} races round the room, so happy you're here.";
                case ReunionScale.BiggestWelcome:
                    return $"The biggest welcome ever! {name} wiggles, bounces and leans in for a cuddle.";
                default: return null;
            }
        }
    }
}
