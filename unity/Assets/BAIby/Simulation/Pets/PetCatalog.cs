using System;
using System.Collections.Generic;
using System.Linq;

namespace BAIby.Simulation.Pets
{
    // The species table and breed list from docs/pet-mode.md. Plain C# so dotnet can test it.
    public sealed class PetCatalog
    {
        public const int MaxVisibleMeters = 4;
        public const int MaxTendencies = 2;
        public const int MinTendencyPercent = 10;
        public const int MaxTendencyPercent = 20;
        public const int MaxPersonalityBias = 20;
        // Need per hour; tendency trades are exact, so this only absorbs floating-point error.
        private const double BalanceTolerance = 1e-9;

        public static readonly PetCatalog Default = CreateDefault();

        private readonly Dictionary<Species, SpeciesDefinition> speciesById = new Dictionary<Species, SpeciesDefinition>();
        private readonly Dictionary<PetBreed, BreedDefinition> breedsById = new Dictionary<PetBreed, BreedDefinition>();

        public IReadOnlyList<SpeciesDefinition> AllSpecies { get; }
        public IReadOnlyList<BreedDefinition> AllBreeds { get; }
        public IReadOnlyList<BreedPackDefinition> AllPacks { get; }

        // A catalog without breed packs.
        public PetCatalog(IEnumerable<SpeciesDefinition> species, IEnumerable<BreedDefinition> breeds)
            : this(species, breeds, null)
        {
        }

        // Does not validate, so a broken catalog can be built and reported by Validate.
        public PetCatalog(IEnumerable<SpeciesDefinition> species, IEnumerable<BreedDefinition> breeds,
            IEnumerable<BreedPackDefinition> packs)
        {
            AllSpecies = Array.AsReadOnly((species ?? throw new ArgumentNullException(nameof(species))).ToArray());
            AllBreeds = Array.AsReadOnly((breeds ?? throw new ArgumentNullException(nameof(breeds))).ToArray());
            AllPacks = Array.AsReadOnly((packs ?? Enumerable.Empty<BreedPackDefinition>()).ToArray());
            foreach (var definition in AllSpecies)
            {
                if (definition != null && !speciesById.ContainsKey(definition.Species))
                    speciesById.Add(definition.Species, definition);
            }

            foreach (var definition in AllBreeds)
            {
                if (definition != null && !breedsById.ContainsKey(definition.Breed))
                    breedsById.Add(definition.Breed, definition);
            }
        }

        public SpeciesDefinition Get(Species species) =>
            speciesById.TryGetValue(species, out var definition)
                ? definition
                : throw new ArgumentOutOfRangeException(nameof(species), $"No species definition for {species}.");

        public BreedDefinition Get(PetBreed breed) =>
            breedsById.TryGetValue(breed, out var definition)
                ? definition
                : throw new ArgumentOutOfRangeException(nameof(breed), $"No breed definition for {breed}.");

        public bool TryGet(PetBreed breed, out BreedDefinition definition) => breedsById.TryGetValue(breed, out definition);

        public IReadOnlyList<BreedDefinition> BreedsOf(Species species) =>
            AllBreeds.Where(breed => breed != null && breed.Species == species).ToArray();

        // True when owning this product unlocks the premium breed: its own breed.<species>.<breed>
        // product, or a breed pack that lists it. Free and earned breeds are never sold.
        public bool GrantsBreed(string productId, PetBreed breed)
        {
            if (productId == null || !breedsById.TryGetValue(breed, out var definition) || definition.Unlock != BreedUnlock.Premium)
                return false;
            if (string.Equals(productId, definition.ProductId, StringComparison.Ordinal))
                return true;

            foreach (var pack in AllPacks)
            {
                if (pack != null && string.Equals(pack.ProductId, productId, StringComparison.Ordinal) && pack.Includes(breed))
                    return true;
            }

            return false;
        }

        // Every product that unlocks the breed, single purchase first, for the store and the picker.
        public IReadOnlyList<string> ProductsGranting(PetBreed breed)
        {
            var products = new List<string>();
            if (!breedsById.TryGetValue(breed, out var definition) || definition.Unlock != BreedUnlock.Premium)
                return products;
            if (definition.ProductId != null)
                products.Add(definition.ProductId);

            foreach (var pack in AllPacks)
            {
                if (pack != null && pack.ProductId != null && pack.Includes(breed) && !products.Contains(pack.ProductId))
                    products.Add(pack.ProductId);
            }

            return products;
        }

        // The coat to render: a saved coat the breed no longer offers falls back to the breed default.
        public PetCoat EffectiveCoat(PetAppearance appearance)
        {
            var breed = Get(appearance.Breed);
            return breed.OffersCoat(appearance.Coat) ? appearance.Coat : breed.DefaultCoat;
        }

        public PetAccessory EffectiveAccessory(PetAppearance appearance) =>
            Get(appearance.Species).Offers(appearance.Accessory) ? appearance.Accessory : PetAccessory.None;

        // A new choice in the picker must be offered by the breed and species. Saved looks are
        // decoded more leniently; see EffectiveCoat.
        public void ValidateChoice(PetAppearance appearance)
        {
            if (appearance == null)
                throw new ArgumentNullException(nameof(appearance));

            var breed = Get(appearance.Breed);
            if (!breed.OffersCoat(appearance.Coat))
                throw new ArgumentException($"{appearance.Breed} does not offer the {appearance.Coat} coat.", nameof(appearance));
            if (!Get(appearance.Species).Offers(appearance.Accessory))
                throw new ArgumentException($"{appearance.Species} cannot wear {appearance.Accessory}.", nameof(appearance));
        }

        public void Validate()
        {
            var problems = Problems();
            if (problems.Count > 0)
                throw new InvalidOperationException("The pet catalog is invalid:\n" + string.Join("\n", problems));
        }

        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            ValidateSpecies(problems);
            ValidateBreeds(problems);
            ValidatePacks(problems);
            return problems;
        }

        private void ValidateSpecies(List<string> problems)
        {
            var seen = new HashSet<Species>();
            foreach (var species in AllSpecies)
            {
                if (species == null)
                {
                    problems.Add("A species definition is missing.");
                    continue;
                }

                var name = species.Species.ToString();
                if (!PetIds.IsDefined(species.Species))
                    problems.Add($"Species {(int)species.Species} is not a defined ID.");
                if (!seen.Add(species.Species))
                    problems.Add($"Species {name} is defined twice.");
                if (species.GrownAge <= TimeSpan.Zero)
                    problems.Add($"{name} needs a positive Young to Grown age.");
                if (species.Meters.Count == 0 || species.Meters.Count > MaxVisibleMeters)
                    problems.Add($"{name} shows {species.Meters.Count} meters; a room shows 1 to {MaxVisibleMeters}.");

                var meters = new HashSet<PetMeter>();
                foreach (var meter in species.Meters)
                {
                    if (meter == null)
                    {
                        problems.Add($"{name} has a missing meter definition.");
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(PetMeter), meter.Meter) || meter.Meter == PetMeter.Health)
                        problems.Add($"{name} lists meter {meter.Meter}; Health is implied and others must be defined.");
                    if (!meters.Add(meter.Meter))
                        problems.Add($"{name} lists {meter.Meter} twice.");
                    if (!(meter.DecayPerHour > 0) || meter.DecayPerHour > 100)
                        problems.Add($"{name} {meter.Meter} needs a decay between 0 and 100 per hour.");
                    if (!(meter.Initial >= 0 && meter.Initial <= 100))
                        problems.Add($"{name} {meter.Meter} needs an initial value from 0 to 100.");
                }

                if (species.Verbs.Count == 0)
                    problems.Add($"{name} has no care verbs.");

                var verbs = new HashSet<PetVerb>();
                var signatures = new HashSet<string>();
                foreach (var verb in species.Verbs)
                {
                    if (verb == null)
                    {
                        problems.Add($"{name} has a missing verb definition.");
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(PetVerb), verb.Verb))
                        problems.Add($"{name} lists undefined verb {(int)verb.Verb}.");
                    if (!verbs.Add(verb.Verb))
                        problems.Add($"{name} lists {verb.Verb} twice.");
                    if (verb.Effects.Count == 0 && verb.Bond == 0)
                        problems.Add($"{name} {verb.Verb} changes nothing.");
                    if (verb.Bond < 0)
                        problems.Add($"{name} {verb.Verb} cannot lower Bond.");
                    if (!signatures.Add(Signature(verb)))
                        problems.Add($"{name} {verb.Verb} has the same effect as another verb.");

                    var touched = new HashSet<PetMeter>();
                    foreach (var effect in verb.Effects)
                    {
                        if (effect.Meter == PetMeter.Health || !meters.Contains(effect.Meter))
                            problems.Add($"{name} {verb.Verb} changes {effect.Meter}, which the species does not show.");
                        if (effect.Delta == 0 || double.IsNaN(effect.Delta) || Math.Abs(effect.Delta) > 100)
                            problems.Add($"{name} {verb.Verb} needs a change between -100 and 100 for {effect.Meter}.");
                        if (!touched.Add(effect.Meter))
                            problems.Add($"{name} {verb.Verb} changes {effect.Meter} twice.");
                    }
                }

                if (species.Moments.Count == 0)
                    problems.Add($"{name} needs a signature moment.");
                if (species.Moments.Any(moment => !Enum.IsDefined(typeof(PetMoment), moment)) ||
                    species.Moments.Distinct().Count() != species.Moments.Count)
                    problems.Add($"{name} has undefined or repeated signature moments.");
                if (!species.Offers(PetAccessory.None) ||
                    species.Accessories.Any(accessory => !Enum.IsDefined(typeof(PetAccessory), accessory)) ||
                    species.Accessories.Distinct().Count() != species.Accessories.Count)
                    problems.Add($"{name} accessories must include None and be defined once each.");
            }

            foreach (Species species in Enum.GetValues(typeof(Species)))
            {
                if (!seen.Contains(species))
                    problems.Add($"Species {species} has no definition.");
            }
        }

        private void ValidateBreeds(List<string> problems)
        {
            var seen = new HashSet<PetBreed>();
            var productIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var breed in AllBreeds)
            {
                if (breed == null)
                {
                    problems.Add("A breed definition is missing.");
                    continue;
                }

                var name = breed.Breed.ToString();
                if (!PetIds.IsDefined(breed.Breed))
                    problems.Add($"Breed {(int)breed.Breed} is not a defined ID.");
                if (!seen.Add(breed.Breed))
                    problems.Add($"Breed {name} is defined twice.");
                if (!PetIds.TrySpeciesOfBlock((int)breed.Breed, out var blockSpecies) || blockSpecies != breed.Species)
                    problems.Add($"Breed {name} ({(int)breed.Breed}) is outside the {breed.Species} ID block.");
                if (!speciesById.TryGetValue(breed.Species, out var species))
                {
                    problems.Add($"Breed {name} belongs to {breed.Species}, which has no definition.");
                    continue;
                }

                switch (breed.Unlock)
                {
                    case BreedUnlock.Free:
                    case BreedUnlock.Premium:
                        if (breed.Goal.HasValue)
                            problems.Add($"Breed {name} is not earned and cannot have a goal.");
                        break;
                    case BreedUnlock.Earned:
                        if (!breed.Goal.HasValue || !Enum.IsDefined(typeof(EarnedGoal), breed.Goal.Value))
                            problems.Add($"Earned breed {name} needs a defined goal.");
                        break;
                    default:
                        problems.Add($"Breed {name} has an undefined unlock tier.");
                        break;
                }

                if (breed.Unlock == BreedUnlock.Premium)
                {
                    var prefix = $"breed.{PetIds.Key(breed.Species)}.";
                    if (breed.ProductId == null || !breed.ProductId.StartsWith(prefix, StringComparison.Ordinal) ||
                        !PetIds.IsIdSegment(breed.ProductId.Substring(prefix.Length)))
                        problems.Add($"Premium breed {name} needs a product ID of the form {prefix}<breed>.");
                    else if (!productIds.Add(breed.ProductId))
                        problems.Add($"Product ID {breed.ProductId} is used twice.");
                }
                else if (breed.ProductId != null)
                {
                    problems.Add($"Breed {name} is not premium and cannot have a product ID.");
                }

                if (!Enum.IsDefined(typeof(BreedArtTier), breed.Art))
                    problems.Add($"Breed {name} has an undefined art tier.");
                if (breed.Unlock != BreedUnlock.Premium && (breed.Art == BreedArtTier.NewSilhouette || !breed.AvailableNow))
                    problems.Add($"Breed {name} is free or earned, so its art must ship with the species.");
                if (breed.Unlock == BreedUnlock.Premium && breed.Art == BreedArtTier.Recolor && !breed.AvailableNow)
                    problems.Add($"Premium recolor breed {name} ships with its species.");

                if (breed.Coats.Count == 0 || breed.Coats.Distinct().Count() != breed.Coats.Count ||
                    breed.Coats.Any(coat => !Enum.IsDefined(typeof(PetCoat), coat)))
                    problems.Add($"Breed {name} needs at least one coat, each defined once.");

                if (Math.Abs(breed.LivelyBias) > MaxPersonalityBias || Math.Abs(breed.BoldBias) > MaxPersonalityBias)
                    problems.Add($"Breed {name} personality bias must stay within {MaxPersonalityBias}.");

                ValidateTendencies(breed, species, problems);
            }

            foreach (PetBreed breed in Enum.GetValues(typeof(PetBreed)))
            {
                if (!seen.Contains(breed))
                    problems.Add($"Breed {breed} has no definition.");
            }

            foreach (var species in AllSpecies.Where(species => species != null))
                ValidateSpeciesBreeds(species.Species, problems);
        }

        private static void ValidateTendencies(BreedDefinition breed, SpeciesDefinition species, List<string> problems)
        {
            var name = breed.Breed.ToString();
            if (breed.Tendencies.Count > MaxTendencies)
                problems.Add($"Breed {name} has more than {MaxTendencies} tendencies.");

            var meters = new HashSet<PetMeter>();
            foreach (var tendency in breed.Tendencies)
            {
                var size = Math.Abs(tendency.Percent);
                if (size < MinTendencyPercent || size > MaxTendencyPercent)
                    problems.Add($"Breed {name} {tendency.Meter} tendency of {tendency.Percent}% is outside {MinTendencyPercent} to {MaxTendencyPercent}%.");
                if (tendency.Meter == PetMeter.Health || !species.HasMeter(tendency.Meter))
                    problems.Add($"Breed {name} tendency names {tendency.Meter}, which {species.Species} does not show.");
                if (!meters.Add(tendency.Meter))
                    problems.Add($"Breed {name} has two tendencies for {tendency.Meter}.");
            }

            // No breed is harder or easier: the need a faster meter loses each hour is exactly the
            // need a slower one keeps. Balanced by absolute decay, since meters decay at different rates.
            var net = breed.NetDecayChangePerHour(species);
            if (Math.Abs(net) > BalanceTolerance)
                problems.Add($"Breed {name} tendencies must balance by decay rate; they change need lost by {net:0.###} per hour.");
        }

        private void ValidatePacks(List<string> problems)
        {
            var packIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pack in AllPacks)
            {
                if (pack == null)
                {
                    problems.Add("A breed pack definition is missing.");
                    continue;
                }

                var name = pack.ProductId ?? "(no product ID)";
                if (pack.Species.HasValue && !speciesById.ContainsKey(pack.Species.Value))
                    problems.Add($"Breed pack {name} covers {pack.Species.Value}, which has no definition.");

                var expected = pack.Species.HasValue
                    ? (PetIds.IsDefined(pack.Species.Value) ? PetIds.BreedPackProductId(pack.Species.Value) : null)
                    : PetIds.AllBreedsPackProductId;
                if (expected == null || !string.Equals(pack.ProductId, expected, StringComparison.Ordinal))
                    problems.Add($"Breed pack {name} needs the product ID {expected ?? "breedpack.<species>"}.");
                else if (!packIds.Add(pack.ProductId))
                    problems.Add($"Breed pack {name} is defined twice.");

                if (pack.Breeds.Count == 0)
                    problems.Add($"Breed pack {name} grants no breeds.");
                if (pack.Breeds.Distinct().Count() != pack.Breeds.Count)
                    problems.Add($"Breed pack {name} lists a breed twice.");

                foreach (var breed in pack.Breeds)
                {
                    if (!breedsById.TryGetValue(breed, out var definition))
                        problems.Add($"Breed pack {name} grants {breed}, which has no definition.");
                    else if (definition.Unlock != BreedUnlock.Premium)
                        problems.Add($"Breed pack {name} grants {breed}, which is not premium.");
                    else if (pack.Species.HasValue && definition.Species != pack.Species.Value)
                        problems.Add($"Breed pack {name} grants {breed}, which is not a {pack.Species.Value}.");
                }
            }
        }

        private void ValidateSpeciesBreeds(Species species, List<string> problems)
        {
            var breeds = BreedsOf(species);
            if (breeds.Count(breed => breed.Unlock == BreedUnlock.Free) != 1)
                problems.Add($"{species} needs exactly one free starter breed.");

            // The n-th earned breed follows the n-th goal of the ladder.
            var earned = breeds.Where(breed => breed.Unlock == BreedUnlock.Earned).ToArray();
            for (var index = 0; index < earned.Length; index++)
            {
                if (earned[index].Goal.HasValue && (int)earned[index].Goal.Value != index)
                    problems.Add($"Earned breed {earned[index].Breed} should use goal {(EarnedGoal)index}.");
            }

            // A premium breed looks special but is never stronger than a breed earned without paying.
            var strongestFree = breeds.Where(breed => breed.Unlock != BreedUnlock.Premium)
                .Select(breed => breed.TendencyMagnitude).DefaultIfEmpty(0).Max();
            foreach (var premium in breeds.Where(breed => breed.Unlock == BreedUnlock.Premium))
            {
                if (premium.TendencyMagnitude > strongestFree)
                    problems.Add($"Premium breed {premium.Breed} has stronger tendencies than any free or earned {species}.");
            }
        }

        private static string Signature(VerbDefinition verb) =>
            string.Join(";", verb.Effects.OrderBy(effect => effect.Meter)
                .Select(effect => $"{(int)effect.Meter}:{effect.Delta}")) + $"|{verb.Bond}";

        private static PetCatalog CreateDefault()
        {
            var collarSet = new[] { PetAccessory.None, PetAccessory.Collar, PetAccessory.Bandana, PetAccessory.Bow };
            var species = new[]
            {
                new SpeciesDefinition(Species.Cat, TimeSpan.FromDays(7),
                    new[]
                    {
                        Meter(PetMeter.Fullness, 7, 70), Meter(PetMeter.Energy, 6, 80),
                        Meter(PetMeter.Comfort, 4, 70), Meter(PetMeter.Cleanliness, 3, 90),
                    },
                    new[]
                    {
                        Verb(PetVerb.Feed, 1, 0, 0, Effect(PetMeter.Fullness, 30)),
                        Verb(PetVerb.Pet, 3, 0, 1, Effect(PetMeter.Comfort, 20)),
                        Verb(PetVerb.Wand, 2, 2, 1, Effect(PetMeter.Energy, -12), Effect(PetMeter.Comfort, 5)),
                        Verb(PetVerb.Brush, 2, -1, 0, Effect(PetMeter.Cleanliness, 25), Effect(PetMeter.Comfort, 8)),
                        Verb(PetVerb.Nap, 0, -1, 0, Effect(PetMeter.Energy, 35)),
                    },
                    new[] { PetMoment.Kneading, PetMoment.Purring, PetMoment.BringsToy }, collarSet),
                new SpeciesDefinition(Species.Dog, TimeSpan.FromDays(10),
                    new[]
                    {
                        Meter(PetMeter.Fullness, 7, 70), Meter(PetMeter.Energy, 6, 80),
                        Meter(PetMeter.Exercise, 6, 70), Meter(PetMeter.Cleanliness, 6, 90),
                    },
                    new[]
                    {
                        Verb(PetVerb.Feed, 1, 0, 0, Effect(PetMeter.Fullness, 30)),
                        Verb(PetVerb.Walk, 2, 0, 2, Effect(PetMeter.Exercise, 35), Effect(PetMeter.Energy, -10),
                            Effect(PetMeter.Cleanliness, -5)),
                        Verb(PetVerb.Fetch, 3, 2, 0, Effect(PetMeter.Exercise, 20), Effect(PetMeter.Energy, -12)),
                        Verb(PetVerb.Brush, 2, -1, 0, Effect(PetMeter.Cleanliness, 30)),
                        Verb(PetVerb.Rest, 0, -1, 0, Effect(PetMeter.Energy, 35)),
                    },
                    new[] { PetMoment.ReunionGreeting }, collarSet),
                new SpeciesDefinition(Species.Fish, TimeSpan.FromDays(5),
                    new[] { Meter(PetMeter.Fullness, 5, 70), Meter(PetMeter.Water, 3, 90), Meter(PetMeter.Calm, 4, 70) },
                    new[]
                    {
                        Verb(PetVerb.SprinkleFood, 1, 0, 0, Effect(PetMeter.Fullness, 25)),
                        Verb(PetVerb.WipeGlass, 1, 0, 1, Effect(PetMeter.Water, 15), Effect(PetMeter.Calm, 5)),
                        Verb(PetVerb.WaterChange, 0, 0, 0, Effect(PetMeter.Water, 100), Effect(PetMeter.Calm, -5)),
                        Verb(PetVerb.Rest, 0, -1, 0, Effect(PetMeter.Calm, 30)),
                    },
                    new[] { PetMoment.SwimsToGlass }, new[] { PetAccessory.None, PetAccessory.TankCastle }),
                new SpeciesDefinition(Species.Rabbit, TimeSpan.FromDays(7),
                    new[]
                    {
                        Meter(PetMeter.Hay, 6, 70), Meter(PetMeter.Fullness, 5, 70),
                        Meter(PetMeter.Litter, 6, 90), Meter(PetMeter.Comfort, 4, 70),
                    },
                    new[]
                    {
                        Verb(PetVerb.Hay, 0, 0, 0, Effect(PetMeter.Hay, 40)),
                        Verb(PetVerb.Greens, 1, 0, 0, Effect(PetMeter.Fullness, 25)),
                        Verb(PetVerb.Groom, 2, -1, 0, Effect(PetMeter.Comfort, 20)),
                        // Sitting on the floor builds Bond faster.
                        Verb(PetVerb.FloorTime, 4, 1, 2, Effect(PetMeter.Comfort, 10)),
                        Verb(PetVerb.Litter, 0, 0, 0, Effect(PetMeter.Litter, 100)),
                    },
                    new[] { PetMoment.Binky }, collarSet),
                new SpeciesDefinition(Species.Hamster, TimeSpan.FromDays(5),
                    new[]
                    {
                        Meter(PetMeter.Fullness, 6, 70), Meter(PetMeter.Energy, 6, 80),
                        Meter(PetMeter.Bedding, 3, 90), Meter(PetMeter.Exercise, 5, 70),
                    },
                    new[]
                    {
                        Verb(PetVerb.Bowl, 1, 0, 0, Effect(PetMeter.Fullness, 30)),
                        Verb(PetVerb.Wheel, 0, 2, 0, Effect(PetMeter.Exercise, 30), Effect(PetMeter.Energy, -10)),
                        Verb(PetVerb.FluffBedding, 1, 0, 0, Effect(PetMeter.Bedding, 40)),
                        Verb(PetVerb.GentleHold, 3, 0, 2),
                        Verb(PetVerb.Nap, 0, -1, 0, Effect(PetMeter.Energy, 35)),
                    },
                    new[] { PetMoment.CheekPouches }, new[] { PetAccessory.None, PetAccessory.Bow }),
            };

            const BreedArtTier recolor = BreedArtTier.Recolor;
            const BreedArtTier overlay = BreedArtTier.Overlay;
            const BreedArtTier silhouette = BreedArtTier.NewSilhouette;
            var breeds = new[]
            {
                Free(PetBreed.DomesticShorthair, recolor, Coats(PetCoat.Ginger, PetCoat.Gray, PetCoat.Black, PetCoat.Cream, PetCoat.White),
                    Trade(PetMeter.Energy, 10, PetMeter.Comfort, 15), 5, 5),
                Earned(PetBreed.Tabby, EarnedGoal.FirstFullCareDay, recolor, Coats(PetCoat.Brown, PetCoat.Ginger, PetCoat.Silver),
                    Trade(PetMeter.Energy, 14, PetMeter.Fullness, 12), 10, 5),
                Earned(PetBreed.Tuxedo, EarnedGoal.Bond40, recolor, Coats(PetCoat.Black, PetCoat.Gray),
                    Trade(PetMeter.Fullness, 12, PetMeter.Energy, 14), 0, 10),
                Earned(PetBreed.Siamese, EarnedGoal.RaisedToGrown, recolor, Coats(PetCoat.Seal, PetCoat.Blue, PetCoat.Lilac),
                    Trade(PetMeter.Comfort, 15, PetMeter.Energy, 10), 10, 15),
                Earned(PetBreed.Calico, EarnedGoal.SevenOnTimeCareDays, recolor, Coats(PetCoat.Tricolor, PetCoat.Cream),
                    Trade(PetMeter.Comfort, 12, PetMeter.Cleanliness, 16), 5, 0),
                Premium(PetBreed.Ragdoll, "ragdoll", overlay, Coats(PetCoat.Seal, PetCoat.Blue, PetCoat.Lilac),
                    Trade(PetMeter.Comfort, 15, PetMeter.Energy, 10), -15, 5),
                Premium(PetBreed.MaineCoon, "maine_coon", overlay, Coats(PetCoat.Brown, PetCoat.Silver, PetCoat.Red),
                    Trade(PetMeter.Fullness, 12, PetMeter.Energy, 14), -5, 10),
                Premium(PetBreed.Bengal, "bengal", recolor, Coats(PetCoat.Golden, PetCoat.Silver, PetCoat.Brown),
                    Trade(PetMeter.Energy, 10, PetMeter.Comfort, 15), 15, 10),
                Premium(PetBreed.BritishShorthair, "british_shorthair", recolor, Coats(PetCoat.Blue, PetCoat.Cream, PetCoat.Silver),
                    Trade(PetMeter.Fullness, 12, PetMeter.Energy, 14), -10, 0),
                Premium(PetBreed.RussianBlue, "russian_blue", recolor, Coats(PetCoat.Blue),
                    Trade(PetMeter.Comfort, 12, PetMeter.Cleanliness, 16), 0, -10),
                Premium(PetBreed.Sphynx, "sphynx", silhouette, Coats(PetCoat.Cream, PetCoat.Gray, PetCoat.Black),
                    Trade(PetMeter.Cleanliness, 16, PetMeter.Comfort, 12), 10, 10),

                Free(PetBreed.MixedBreed, recolor, Coats(PetCoat.Tan, PetCoat.Black, PetCoat.Brown, PetCoat.Cream, PetCoat.White),
                    Trade(PetMeter.Exercise, 14, PetMeter.Fullness, 12), 5, 5),
                Earned(PetBreed.Golden, EarnedGoal.FirstFullCareDay, overlay, Coats(PetCoat.Golden, PetCoat.Cream, PetCoat.Red),
                    Trade(PetMeter.Exercise, 10, PetMeter.Cleanliness, 10), 10, 10),
                Earned(PetBreed.Beagle, EarnedGoal.Bond40, overlay, Coats(PetCoat.Tricolor, PetCoat.Tan),
                    Trade(PetMeter.Fullness, 12, PetMeter.Energy, 14), 10, 5),
                Earned(PetBreed.Shiba, EarnedGoal.RaisedToGrown, recolor, Coats(PetCoat.Red, PetCoat.Sable, PetCoat.Cream, PetCoat.Black),
                    Trade(PetMeter.Exercise, 15, PetMeter.Cleanliness, 15), 5, 0),
                Premium(PetBreed.Corgi, "corgi", silhouette, Coats(PetCoat.Red, PetCoat.Sable, PetCoat.Tricolor),
                    Trade(PetMeter.Exercise, 10, PetMeter.Energy, 10), 10, 10),
                Premium(PetBreed.Poodle, "poodle", overlay, Coats(PetCoat.White, PetCoat.Black, PetCoat.Chocolate, PetCoat.Cream),
                    Trade(PetMeter.Cleanliness, 15, PetMeter.Exercise, 15), 5, 5),
                Premium(PetBreed.Dachshund, "dachshund", silhouette, Coats(PetCoat.Red, PetCoat.Chocolate, PetCoat.Black),
                    Trade(PetMeter.Fullness, 12, PetMeter.Exercise, 14), 0, 10),
                Premium(PetBreed.Husky, "husky", recolor, Coats(PetCoat.Gray, PetCoat.Black, PetCoat.Red),
                    Trade(PetMeter.Exercise, 14, PetMeter.Fullness, 12), 15, 10),
                Premium(PetBreed.Samoyed, "samoyed", overlay, Coats(PetCoat.White, PetCoat.Cream),
                    Trade(PetMeter.Cleanliness, 15, PetMeter.Energy, 15), 10, 10),
                Premium(PetBreed.Pomeranian, "pomeranian", overlay, Coats(PetCoat.Orange, PetCoat.Cream, PetCoat.Black),
                    Trade(PetMeter.Energy, 10, PetMeter.Exercise, 10), 15, 5),
                Premium(PetBreed.Dalmatian, "dalmatian", recolor, Coats(PetCoat.White),
                    Trade(PetMeter.Exercise, 10, PetMeter.Cleanliness, 10), 10, 5),
                Premium(PetBreed.BorderCollie, "border_collie", overlay, Coats(PetCoat.Black, PetCoat.Red, PetCoat.Blue),
                    Trade(PetMeter.Exercise, 14, PetMeter.Fullness, 12), 15, 5),

                Free(PetBreed.Goldfish, recolor, Coats(PetCoat.Orange, PetCoat.Red, PetCoat.White, PetCoat.Yellow),
                    Trade(PetMeter.Water, 16, PetMeter.Calm, 12), 0, 5),
                Earned(PetBreed.Guppy, EarnedGoal.FirstFullCareDay, overlay, Coats(PetCoat.Blue, PetCoat.Orange, PetCoat.Yellow, PetCoat.Purple),
                    Trade(PetMeter.Fullness, 12, PetMeter.Calm, 15), 15, 5),
                Earned(PetBreed.Betta, EarnedGoal.Bond40, overlay, Coats(PetCoat.Red, PetCoat.Blue, PetCoat.Purple, PetCoat.White),
                    Trade(PetMeter.Calm, 12, PetMeter.Water, 16), 5, 15),
                Premium(PetBreed.NeonTetraSchool, "neon_tetra_school", silhouette, Coats(PetCoat.Blue, PetCoat.Red),
                    Trade(PetMeter.Fullness, 12, PetMeter.Calm, 15), 10, -10),
                Premium(PetBreed.Angelfish, "angelfish", overlay, Coats(PetCoat.Silver, PetCoat.Black, PetCoat.Golden),
                    Trade(PetMeter.Calm, 12, PetMeter.Water, 16), -10, 5),
                Premium(PetBreed.Koi, "koi", recolor, Coats(PetCoat.White, PetCoat.Orange, PetCoat.Red, PetCoat.Black),
                    Trade(PetMeter.Fullness, 12, PetMeter.Calm, 15), -5, 10),
                Premium(PetBreed.Clownfish, "clownfish", recolor, Coats(PetCoat.Orange, PetCoat.Black),
                    Trade(PetMeter.Water, 16, PetMeter.Calm, 12), 10, 10),
                Premium(PetBreed.Discus, "discus", overlay, Coats(PetCoat.Blue, PetCoat.Red, PetCoat.Brown),
                    Trade(PetMeter.Water, 16, PetMeter.Calm, 12), -10, 0),

                Free(PetBreed.Dutch, recolor, Coats(PetCoat.Black, PetCoat.Brown, PetCoat.Gray, PetCoat.Chocolate),
                    Trade(PetMeter.Hay, 15, PetMeter.Litter, 15), 5, 5),
                Earned(PetBreed.NetherlandDwarf, EarnedGoal.FirstFullCareDay, recolor, Coats(PetCoat.White, PetCoat.Black, PetCoat.Fawn),
                    Trade(PetMeter.Comfort, 15, PetMeter.Fullness, 12), 10, -5),
                Earned(PetBreed.HollandLop, EarnedGoal.Bond40, overlay, Coats(PetCoat.Fawn, PetCoat.Brown, PetCoat.White),
                    Trade(PetMeter.Comfort, 15, PetMeter.Litter, 10), -5, 10),
                Premium(PetBreed.Lionhead, "lionhead", silhouette, Coats(PetCoat.Cream, PetCoat.Gray, PetCoat.Brown),
                    Trade(PetMeter.Hay, 10, PetMeter.Comfort, 15), 5, 5),
                Premium(PetBreed.Angora, "angora", overlay, Coats(PetCoat.White, PetCoat.Cream, PetCoat.Gray),
                    Trade(PetMeter.Comfort, 15, PetMeter.Litter, 10), -10, 0),
                Premium(PetBreed.Rex, "rex", recolor, Coats(PetCoat.Chocolate, PetCoat.Black, PetCoat.Lilac),
                    Trade(PetMeter.Fullness, 12, PetMeter.Hay, 10), 0, 10),
                Premium(PetBreed.FlemishGiant, "flemish_giant", silhouette, Coats(PetCoat.Gray, PetCoat.Fawn, PetCoat.Black),
                    Trade(PetMeter.Fullness, 12, PetMeter.Comfort, 15), -15, 10),
                Premium(PetBreed.Harlequin, "harlequin", recolor, Coats(PetCoat.Tricolor, PetCoat.Fawn),
                    Trade(PetMeter.Hay, 10, PetMeter.Litter, 10), 10, 5),

                Free(PetBreed.SyrianGolden, recolor, Coats(PetCoat.Golden, PetCoat.Cream, PetCoat.Brown),
                    Trade(PetMeter.Exercise, 12, PetMeter.Bedding, 20), 5, 5),
                Earned(PetBreed.WinterWhite, EarnedGoal.FirstFullCareDay, recolor, Coats(PetCoat.White, PetCoat.Gray, PetCoat.Sable),
                    Trade(PetMeter.Fullness, 10, PetMeter.Energy, 10), -5, 0),
                Earned(PetBreed.Roborovski, EarnedGoal.Bond40, recolor, Coats(PetCoat.Tan, PetCoat.White),
                    Trade(PetMeter.Exercise, 12, PetMeter.Energy, 10), 15, -10),
                Premium(PetBreed.TeddyBear, "teddy_bear", overlay, Coats(PetCoat.Golden, PetCoat.Cream, PetCoat.Chocolate),
                    Trade(PetMeter.Bedding, 20, PetMeter.Exercise, 12), -10, 5),
                Premium(PetBreed.Panda, "panda", recolor, Coats(PetCoat.White),
                    Trade(PetMeter.Fullness, 10, PetMeter.Bedding, 20), 0, 5),
                Premium(PetBreed.Sapphire, "sapphire", recolor, Coats(PetCoat.Blue, PetCoat.Gray),
                    Trade(PetMeter.Energy, 10, PetMeter.Exercise, 12), 5, -5),
                Premium(PetBreed.Campbells, "campbells", recolor, Coats(PetCoat.Gray, PetCoat.Fawn, PetCoat.Black),
                    Trade(PetMeter.Exercise, 12, PetMeter.Fullness, 10), 10, 5),
            };

            // Pack contents are fixed when a pack is published (monetization: packs never include
            // breeds added later), so they are listed here rather than derived from the breed list.
            // A premium breed whose art has not shipped is still in its pack; it becomes adoptable
            // for pack owners when its art ships, as it does for single-purchase owners.
            var speciesPacks = new[]
            {
                SpeciesPack(Species.Cat, PetBreed.Ragdoll, PetBreed.MaineCoon, PetBreed.Bengal, PetBreed.BritishShorthair,
                    PetBreed.RussianBlue, PetBreed.Sphynx),
                SpeciesPack(Species.Dog, PetBreed.Corgi, PetBreed.Poodle, PetBreed.Dachshund, PetBreed.Husky, PetBreed.Samoyed,
                    PetBreed.Pomeranian, PetBreed.Dalmatian, PetBreed.BorderCollie),
                SpeciesPack(Species.Fish, PetBreed.NeonTetraSchool, PetBreed.Angelfish, PetBreed.Koi, PetBreed.Clownfish, PetBreed.Discus),
                SpeciesPack(Species.Rabbit, PetBreed.Lionhead, PetBreed.Angora, PetBreed.Rex, PetBreed.FlemishGiant, PetBreed.Harlequin),
                SpeciesPack(Species.Hamster, PetBreed.TeddyBear, PetBreed.Panda, PetBreed.Sapphire, PetBreed.Campbells),
            };

            // "Every breed" covers the first-release species packs above and, like them, never grows.
            var allPack = new BreedPackDefinition(PetIds.AllBreedsPackProductId, null,
                speciesPacks.SelectMany(pack => pack.Breeds).ToArray());

            return new PetCatalog(species, breeds, speciesPacks.Concat(new[] { allPack }));
        }

        private static MeterDefinition Meter(PetMeter meter, double decayPerHour, double initial) =>
            new MeterDefinition(meter, decayPerHour, initial);

        private static MeterEffect Effect(PetMeter meter, double delta) => new MeterEffect(meter, delta);

        private static VerbDefinition Verb(PetVerb verb, double bond, double lively, double bold, params MeterEffect[] effects) =>
            new VerbDefinition(verb, bond, lively, bold, effects);

        private static PetCoat[] Coats(params PetCoat[] coats) => coats;

        // The first meter drifts faster and the second slower. The percentages are chosen so the need
        // lost per hour is unchanged: fasterPercent * faster decay == slowerPercent * slower decay.
        private static BreedTendency[] Trade(PetMeter faster, int fasterPercent, PetMeter slower, int slowerPercent) =>
            new[] { new BreedTendency(faster, fasterPercent), new BreedTendency(slower, -slowerPercent) };

        private static BreedPackDefinition SpeciesPack(Species species, params PetBreed[] breeds) =>
            new BreedPackDefinition(PetIds.BreedPackProductId(species), species, breeds);

        private static BreedDefinition Free(PetBreed breed, BreedArtTier art, PetCoat[] coats, BreedTendency[] tendencies,
            int lively, int bold) =>
            new BreedDefinition(breed, PetIds.SpeciesOf(breed), BreedUnlock.Free, null, null, art, true, coats, tendencies, lively, bold);

        private static BreedDefinition Earned(PetBreed breed, EarnedGoal goal, BreedArtTier art, PetCoat[] coats,
            BreedTendency[] tendencies, int lively, int bold) =>
            new BreedDefinition(breed, PetIds.SpeciesOf(breed), BreedUnlock.Earned, goal, null, art, true, coats, tendencies, lively, bold);

        // Recolor premium breeds ship with their species; overlay and new-silhouette art follows later.
        private static BreedDefinition Premium(PetBreed breed, string key, BreedArtTier art, PetCoat[] coats,
            BreedTendency[] tendencies, int lively, int bold)
        {
            var species = PetIds.SpeciesOf(breed);
            return new BreedDefinition(breed, species, BreedUnlock.Premium, null, PetIds.BreedProductId(species, key), art,
                art == BreedArtTier.Recolor, coats, tendencies, lively, bold);
        }
    }
}
