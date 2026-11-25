using System;
using System.Collections.Generic;

namespace BAIby.Simulation.Pets
{
    // One simulation for every species, driven by the catalog's species table. The time model
    // matches BabySimulation: negative elapsed time is ignored, need decay is capped, age is not,
    // and the Young to Grown unlock fires once. Nothing dies, leaves or is deleted.
    //
    // A species with a care profile (PetCareProfile: since P5 every species, the cat, the hamster, the dog, the fish and
    // the rabbit) runs on the baby's care systems through PetCare: routine windows, the needs and health axes of the
    // consequence ladder (the vet for the doctor), Away mode, development with readiness-timed milestones and the
    // setback, Bond, Trust and the care rhythm, and the journal. The simple rules below are kept for a species added
    // later before its own slice, as each species had until its slice.
    public sealed class PetSimulation
    {
        // care-and-growth decision 11: the offline decay cap is 8 hours.
        public static readonly TimeSpan DefaultMaxNeedDecay = TimeSpan.FromHours(8);

        // Fish rule: flakes sprinkled at this Fullness or above are ignored and cloud the water slightly. Since P4 the fish
        // runs on its care profile, which uses the care style's full-meal threshold and this much clouding (never more
        // than the style's overfeeding).
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
        private readonly PetCare care;

        public PetCatalog Catalog { get; }

        public PetSimulation() : this(PetCatalog.Default, DefaultMaxNeedDecay)
        {
        }

        public PetSimulation(PetCatalog catalog, TimeSpan maxNeedDecay, CareTunables careTunables = null,
            GrowthTunables growthTunables = null, BondTunables bondTunables = null, HelperRules helperRules = null)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (maxNeedDecay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxNeedDecay));

            catalog.Validate();
            Catalog = catalog;
            this.maxNeedDecay = maxNeedDecay;
            var careRules = careTunables ?? CareTunables.Default;
            careRules.Validate();
            var growthRules = growthTunables ?? GrowthTunables.Default;
            growthRules.Validate();
            var bondRules = bondTunables ?? BondTunables.Default;
            bondRules.Validate();
            var helpers = helperRules ?? HelperRules.Default;
            helpers.Validate(careRules.Standard.FussyNeed, careRules.FullMealThreshold);
            care = new PetCare(catalog, maxNeedDecay, careRules, growthRules, bondRules, helpers);
        }

        // Whether the pet's species runs on the shared care systems.
        public static bool HasCareProfile(PetState state) => state != null && PetCareProfile.For(state.Species) != null;

        // Breed unlocks are checked by the household (PetUnlocks); this checks the look is offered.
        public PetState Adopt(string name, PetAppearance appearance, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enter a name for the pet.", nameof(name));
            if (!PetState.IsSavableName(name))
                throw new ArgumentException("The pet's name has characters that cannot be saved.", nameof(name));

            Catalog.ValidateChoice(appearance);
            var species = Catalog.Get(appearance.Species);
            var breed = Catalog.Get(appearance.Breed);
            var meters = new Dictionary<PetMeter, double> { [PetMeter.Health] = InitialHealth };
            foreach (var meter in species.Meters)
                meters[meter.Meter] = meter.Initial;

            var fresh = new PetState(name.Trim(), appearance, now, TimeSpan.Zero, meters, 0, 0,
                breed.LivelyBias, breed.BoldBias, false);
            return PetCareProfile.For(appearance.Species) == null ? fresh : care.Adopt(fresh);
        }

        public (PetState State, bool BecameGrown) Advance(PetState state, DateTimeOffset now)
        {
            var step = Step(state, now);
            return (step.State, step.BecameGrown);
        }

        // Real time, with the milestones a species with a care profile reached on the way.
        public PetStep Step(PetState state, DateTimeOffset now) => Step(state, now, null, null);

        // With the household's helpers (null for none). Only a species with a care profile has helpers; the others keep
        // their simple rules and ignore them.
        public PetStep Step(PetState state, DateTimeOffset now, HelperSettings helpers, ICollection<HelperEvent> events = null)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (HasCareProfile(state))
                return care.Advance(state, now, helpers, events);
            var legacy = now <= state.TimeCheckpoint
                ? AdvanceElapsed(state, state.TimeCheckpoint, TimeSpan.Zero)
                : AdvanceElapsed(state, now, now - state.TimeCheckpoint);
            return new PetStep(legacy.State, legacy.BecameGrown);
        }

        public (PetState State, bool BecameGrown) AdvancePreview(PetState state, TimeSpan elapsed)
        {
            var step = PreviewStep(state, elapsed);
            return (step.State, step.BecameGrown);
        }

        // A playtest day: the pet's own clock moves without real time passing (no routine window passes).
        public PetStep PreviewStep(PetState state, TimeSpan elapsed) => PreviewStep(state, elapsed, null, null);

        public PetStep PreviewStep(PetState state, TimeSpan elapsed, HelperSettings helpers,
            ICollection<HelperEvent> events = null)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (elapsed <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (HasCareProfile(state))
                return care.Preview(state, elapsed, helpers, events);

            var legacy = AdvanceElapsed(state, state.TimeCheckpoint, elapsed);
            return new PetStep(legacy.State, legacy.BecameGrown);
        }

        public bool CanUse(PetState state, PetVerb verb) =>
            state != null && Catalog.Get(state.Species).FindVerb(verb) != null;

        public (PetState State, PetCue Cue) ApplyCare(PetState state, PetVerb verb)
        {
            var step = Care(state, verb);
            return (step.State, step.Cue);
        }

        // One care verb. Advance to the current instant first.
        public PetStep Care(PetState state, PetVerb verb)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (HasCareProfile(state))
            {
                if (!CanUse(state, verb))
                    throw new InvalidOperationException($"A {state.Species} has no {verb} care.");
                return care.ApplyCare(state, verb);
            }

            var legacy = LegacyCare(state, verb);
            return new PetStep(legacy.State, false, null, legacy.Cue);
        }

        // Whether the vet's visit is offered: the pet is under the weather or sick.
        public static bool CanVisitVet(PetState state) => state?.Care != null && state.Care.IsUnwell;

        // Whether the pet is in its day nap at its checkpoint (a hamster through school hours): a cosy, normal sleep.
        public static bool IsNapping(PetState state) => state?.Care != null && CareRules.IsNapping(state.Care);

        // How big a welcome the pet gives at its checkpoint (a dog's reunion greeting, scaled to time away since the
        // last hands-on care; None for other species).
        public ReunionScale Reunion(PetState state) => PetReunion.Of(state, Catalog);

        // The free vet's visit (Health +15, then Recovering with medicine, or home well). Advance first.
        public PetStep VisitVet(PetState state) => care.VisitVet(Profiled(state));

        // Whether a medicine dose would count now: only while recovering, one per meal window.
        public static MedicineState Medicine(PetState state) =>
            state?.Care == null ? MedicineState.NotNeeded : CareConsequences.Medicine(state.Care);

        // A medicine dose with a meal. Advance first.
        public PetStep GiveMedicine(PetState state) => care.ApplyCare(Profiled(state), null);

        public int RecoveryDoses => care.CareTunables.RecoveryDoses;

        // Photos the pet can still have taken today (three a day), and one of the moment. Advance first.
        public int PhotosLeft(PetState state) => care.PhotosLeft(Profiled(state));

        public (PetState State, bool Taken) TakePhoto(PetState state, PhotoMoment moment) =>
            care.TakePhoto(Profiled(state), moment);

        // Development for the Growth panel: track qualities, the next milestone and when it is likely.
        public GrowthReport Growth(PetState state) => care.Report(Profiled(state));

        // Bond's high-water mark, temperament (Trust and Curiosity) and the care rhythm, seeded for an older save.
        public BondRecord Bonding(PetState state) => care.Bonding(Profiled(state));

        public SecurityPhase Trust(PetState state) => BondRules.SecurityOf(Bonding(state), care.BondTunables);

        public CuriosityPhase Curiosity(PetState state) => BondRules.CuriosityOf(Bonding(state), care.BondTunables);

        public JournalRecord Journal(PetState state) => care.Journal(Profiled(state));

        // Today's routine windows at the state's checkpoint.
        public IReadOnlyList<RoutineSlot> Routine(PetState state) => RoutineStrip.Today(care.Seeded(Profiled(state)).Care);

        // Away mode, up to 14 days, free: windows are Covered, needs held, development at its nominal pace.
        public PetState StartAway(PetState state, TimeSpan duration) => care.StartAway(Profiled(state), duration);

        public PetState EndAway(PetState state) => care.EndAway(Profiled(state));

        // Whether the pet can have helpers (its species has a care profile).
        public static bool HasHelpers(PetState state) => HasCareProfile(state);

        // Fills the auto-feeder (a species with a care profile).
        public PetState StockFeeder(PetState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            return HasCareProfile(state) ? care.StockFeeder(state) : state;
        }

        public HelperRules HelperRules => care.HelperRules;

        public PetState ChangeCareStyle(PetState state, CareStyle style) => care.ChangeStyle(Profiled(state), style);

        public PetState ChangeRoutine(PetState state, RoutinePreset preset, TimeSpan wakeTime) =>
            care.ChangeRoutine(Profiled(state), preset, wakeTime);

        // Whether feeding now would overfeed (species with a care profile).
        public bool IsFull(PetState state) => HasCareProfile(state) && care.IsFull(state);

        // Developer read-out of the ledger, growth and Bond; empty for a species without a care profile.
        public string DescribeCare(PetState state) => HasCareProfile(state) ? care.Describe(state) : string.Empty;

        private static PetState Profiled(PetState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (!HasCareProfile(state))
                throw new InvalidOperationException($"A {state.Species} has no care profile yet.");
            return state;
        }

        private (PetState State, PetCue Cue) LegacyCare(PetState state, PetVerb verb)
        {

            var definition = Catalog.Get(state.Species).FindVerb(verb)
                ?? throw new InvalidOperationException($"A {state.Species} has no {verb} care.");
            var meters = new Dictionary<PetMeter, double>();
            foreach (var pair in state.Meters)
                meters[pair.Key] = pair.Value;

            foreach (var effect in definition.Effects)
                meters[effect.Meter] = Clamp(meters[effect.Meter] + effect.Delta, 0, 100);

            // Temperament drifts with care only during the Young stage.
            var liveliness = state.IsGrown ? state.Liveliness : ClampPersonality(state.Liveliness + definition.Lively);
            var boldness = state.IsGrown ? state.Boldness : ClampPersonality(state.Boldness + definition.Bold);
            return (Copy(state, meters, Math.Min(100, state.Bond + definition.Bond), liveliness, boldness), PetCue.Content);
        }

        private (PetState State, bool BecameGrown) AdvanceElapsed(PetState state, DateTimeOffset now, TimeSpan elapsed)
        {
            var species = Catalog.Get(state.Species);
            var breed = Catalog.Get(state.Breed);
            // Saturates at PetState.MaxAge instead of overflowing, so any restorable pet advances.
            var age = elapsed >= PetState.MaxAge - state.Age ? PetState.MaxAge : state.Age + elapsed;
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
