using System;
using System.Collections.Generic;

namespace BAIby.Simulation.Pets
{
    // One simulation for every species, driven by the catalog's species table. The time model
    // matches BabySimulation: negative elapsed time is ignored, need decay is capped, age is not,
    // and the Young to Grown unlock fires once. Nothing dies, leaves or is deleted.
    public sealed class PetSimulation
    {
        // care-and-growth decision 11: the offline decay cap is 8 hours.
        public static readonly TimeSpan DefaultMaxNeedDecay = TimeSpan.FromHours(8);

        // Fish rule: flakes sprinkled at this Fullness or above are ignored and cloud the water.
        public const double OverfedFullness = 90;
        public const double CloudedWater = 8;

        public const double InitialHealth = 100;
        // The worst state is a sleepy, hungry-looking pose: Health never decays below this floor.
        public const double HealthFloor = 10;
        private const double RecoveryThreshold = 60;
        private const double LowNeedThreshold = 30;
        private const double RecoveryPerHour = 6;
        private const double HealthLossPerHour = 5;

        private readonly TimeSpan maxNeedDecay;

        public PetCatalog Catalog { get; }

        public PetSimulation() : this(PetCatalog.Default, DefaultMaxNeedDecay)
        {
        }

        public PetSimulation(PetCatalog catalog, TimeSpan maxNeedDecay)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (maxNeedDecay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxNeedDecay));

            catalog.Validate();
            Catalog = catalog;
            this.maxNeedDecay = maxNeedDecay;
        }

        // Breed unlocks are checked by the household (PetUnlocks); this checks the look is offered.
        public PetState Adopt(string name, PetAppearance appearance, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the pet.", nameof(name));

            Catalog.ValidateChoice(appearance);
            var species = Catalog.Get(appearance.Species);
            var breed = Catalog.Get(appearance.Breed);
            var meters = new Dictionary<PetMeter, double> { [PetMeter.Health] = InitialHealth };
            foreach (var meter in species.Meters)
                meters[meter.Meter] = meter.Initial;

            return new PetState(name.Trim(), appearance, now, TimeSpan.Zero, meters, 0, 0,
                breed.LivelyBias, breed.BoldBias, false);
        }

        public (PetState State, bool BecameGrown) Advance(PetState state, DateTimeOffset now)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (now <= state.TimeCheckpoint)
                return AdvanceElapsed(state, state.TimeCheckpoint, TimeSpan.Zero);

            return AdvanceElapsed(state, now, now - state.TimeCheckpoint);
        }

        public (PetState State, bool BecameGrown) AdvancePreview(PetState state, TimeSpan elapsed)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));

            return AdvanceElapsed(state, state.TimeCheckpoint, elapsed);
        }

        public bool CanUse(PetState state, PetVerb verb) =>
            state != null && Catalog.Get(state.Species).FindVerb(verb) != null;

        public (PetState State, PetCue Cue) ApplyCare(PetState state, PetVerb verb)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var definition = Catalog.Get(state.Species).FindVerb(verb)
                ?? throw new InvalidOperationException($"A {state.Species} has no {verb} care.");
            var meters = new Dictionary<PetMeter, double>();
            foreach (var pair in state.Meters)
                meters[pair.Key] = pair.Value;

            if (IgnoresFlakes(state, verb))
            {
                meters[PetMeter.Water] = Math.Max(0, meters[PetMeter.Water] - CloudedWater);
                return (Copy(state, meters, state.Bond, state.Liveliness, state.Boldness), PetCue.IgnoredFlakes);
            }

            foreach (var effect in definition.Effects)
                meters[effect.Meter] = Clamp(meters[effect.Meter] + effect.Delta, 0, 100);

            // Temperament drifts with care only during the Young stage.
            var liveliness = state.IsGrown ? state.Liveliness : ClampPersonality(state.Liveliness + definition.Lively);
            var boldness = state.IsGrown ? state.Boldness : ClampPersonality(state.Boldness + definition.Bold);
            return (Copy(state, meters, Math.Min(100, state.Bond + definition.Bond), liveliness, boldness), PetCue.Content);
        }

        // The one species-specific care rule from the species table: extra flakes for a full fish
        // are ignored and cloud the water slightly. It is never harmful beyond that.
        private static bool IgnoresFlakes(PetState state, PetVerb verb) =>
            state.Species == Species.Fish && verb == PetVerb.SprinkleFood && state.Meter(PetMeter.Fullness) >= OverfedFullness;

        private (PetState State, bool BecameGrown) AdvanceElapsed(PetState state, DateTimeOffset now, TimeSpan elapsed)
        {
            var species = Catalog.Get(state.Species);
            var breed = Catalog.Get(state.Breed);
            var age = state.Age + elapsed;
            var isGrown = state.IsGrown || age >= species.GrownAge;
            var becameGrown = isGrown && !state.IsGrown;
            if (elapsed == TimeSpan.Zero && !becameGrown)
                return (state, false);

            var decayHours = Math.Min(elapsed.TotalHours, maxNeedDecay.TotalHours);
            var meters = new Dictionary<PetMeter, double>();
            foreach (var meter in species.Meters)
                meters[meter.Meter] = Math.Max(0, state.Meter(meter.Meter) - decayHours * DecayRate(meter, breed));

            meters[PetMeter.Health] = HealthAfterDecay(state, species, breed, decayHours);
            return (new PetState(state.Name, state.Appearance, now, age, meters, state.Bond, state.HighestBond,
                state.Liveliness, state.Boldness, isGrown), becameGrown);
        }

        private static double DecayRate(MeterDefinition meter, BreedDefinition breed) =>
            meter.DecayPerHour * breed.DecayFactor(meter.Meter);

        // Mirrors the baby: Health recovers while every need stays at 60 or more and falls once a
        // need drops below 30, using the actual time on each side. It never falls below the floor.
        private static double HealthAfterDecay(PetState state, SpeciesDefinition species, BreedDefinition breed, double hours)
        {
            var recovering = Math.Min(hours, HoursUntilFirstLowNeed(state, species, breed, RecoveryThreshold));
            var deteriorating = Math.Max(0, hours - HoursUntilFirstLowNeed(state, species, breed, LowNeedThreshold));
            var recovered = Math.Min(100, state.Health + recovering * RecoveryPerHour);
            var floor = Math.Min(HealthFloor, state.Health);
            return Math.Max(floor, recovered - deteriorating * HealthLossPerHour);
        }

        private static double HoursUntilFirstLowNeed(PetState state, SpeciesDefinition species, BreedDefinition breed, double threshold)
        {
            var hours = double.MaxValue;
            foreach (var meter in species.Meters)
                hours = Math.Min(hours, (state.Meter(meter.Meter) - threshold) / DecayRate(meter, breed));

            return Math.Max(0, hours);
        }

        private static PetState Copy(PetState state, Dictionary<PetMeter, double> meters, double bond, double liveliness, double boldness) =>
            new PetState(state.Name, state.Appearance, state.TimeCheckpoint, state.Age, meters, bond,
                Math.Max(state.HighestBond, bond), liveliness, boldness, state.IsGrown);

        private static double ClampPersonality(double value) => Clamp(value, -PetState.PersonalityLimit, PetState.PersonalityLimit);

        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    }
}
