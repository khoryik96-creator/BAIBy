using System;
using System.Collections.Generic;
using System.Linq;

namespace BAIby.Simulation.Pets
{
    // Household-wide progress toward one species' earned breeds. Unlocks are permanent, so the
    // household keeps the best value it has ever seen (Merge never lowers a field).
    public sealed class SpeciesGoalProgress
    {
        public static readonly SpeciesGoalProgress None = new SpeciesGoalProgress(0, 0, false, 0);

        // Completed full care days with a pet of the species (adoption is implied).
        public int FullCareDays { get; }
        // The highest Bond any pet of the species has reached.
        public double HighestBond { get; }
        public bool RaisedToGrown { get; }
        // On-time care days for the species, counted with the forgiving care rhythm.
        public int OnTimeCareDays { get; }

        public SpeciesGoalProgress(int fullCareDays, double highestBond, bool raisedToGrown, int onTimeCareDays)
        {
            if (fullCareDays < 0)
                throw new ArgumentOutOfRangeException(nameof(fullCareDays));
            if (double.IsNaN(highestBond) || highestBond < 0 || highestBond > 100)
                throw new ArgumentOutOfRangeException(nameof(highestBond));
            if (onTimeCareDays < 0)
                throw new ArgumentOutOfRangeException(nameof(onTimeCareDays));

            FullCareDays = fullCareDays;
            HighestBond = highestBond;
            RaisedToGrown = raisedToGrown;
            OnTimeCareDays = onTimeCareDays;
        }

        public SpeciesGoalProgress Merge(SpeciesGoalProgress other) =>
            other == null
                ? this
                : new SpeciesGoalProgress(Math.Max(FullCareDays, other.FullCareDays), Math.Max(HighestBond, other.HighestBond),
                    RaisedToGrown || other.RaisedToGrown, Math.Max(OnTimeCareDays, other.OnTimeCareDays));

        // What a pet's own record shows: its Bond high-water mark and whether it has grown.
        public SpeciesGoalProgress WithPet(PetState pet) =>
            pet == null
                ? this
                : new SpeciesGoalProgress(FullCareDays, Math.Max(HighestBond, pet.HighestBond), RaisedToGrown || pet.IsGrown, OnTimeCareDays);
    }

    // Which breeds a household may adopt. Free starters always; earned breeds only through their
    // visible goal, never through a purchase; premium breeds only with their owned product ID.
    // No store code: the caller passes the product IDs the player owns.
    public static class PetUnlocks
    {
        public const int FullCareDaysGoal = 1;
        public const double BondGoal = 40;
        public const int OnTimeCareDaysGoal = 7;

        public static bool IsGoalMet(EarnedGoal goal, SpeciesGoalProgress progress)
        {
            progress = progress ?? SpeciesGoalProgress.None;
            switch (goal)
            {
                case EarnedGoal.FirstFullCareDay: return progress.FullCareDays >= FullCareDaysGoal;
                case EarnedGoal.Bond40: return progress.HighestBond >= BondGoal;
                case EarnedGoal.RaisedToGrown: return progress.RaisedToGrown;
                case EarnedGoal.SevenOnTimeCareDays: return progress.OnTimeCareDays >= OnTimeCareDaysGoal;
                default: throw new ArgumentOutOfRangeException(nameof(goal));
            }
        }

        // Owned or earned, whether or not its art has shipped yet.
        public static bool IsUnlocked(PetCatalog catalog, PetBreed breed, SpeciesGoalProgress progress,
            ICollection<string> ownedProductIds)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var definition = catalog.Get(breed);
            switch (definition.Unlock)
            {
                case BreedUnlock.Free:
                    return true;
                case BreedUnlock.Earned:
                    return definition.Goal.HasValue && IsGoalMet(definition.Goal.Value, progress);
                case BreedUnlock.Premium:
                    return definition.ProductId != null && ownedProductIds != null && ownedProductIds.Contains(definition.ProductId);
                default:
                    return false;
            }
        }

        // Unlocked and its art is available now.
        public static bool CanAdopt(PetCatalog catalog, PetBreed breed, SpeciesGoalProgress progress,
            ICollection<string> ownedProductIds) =>
            IsUnlocked(catalog, breed, progress, ownedProductIds) && catalog.Get(breed).AvailableNow;

        public static IReadOnlyList<PetBreed> AdoptableBreeds(PetCatalog catalog, Species species, SpeciesGoalProgress progress,
            ICollection<string> ownedProductIds)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            return catalog.BreedsOf(species)
                .Where(breed => CanAdopt(catalog, breed.Breed, progress, ownedProductIds))
                .Select(breed => breed.Breed)
                .ToArray();
        }
    }
}
