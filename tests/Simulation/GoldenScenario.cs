using System;
using System.Security.Cryptography;
using System.Text;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// The golden regression for helpers (docs/monetization.md, Tests): a household with a baby and a cat played for
// four days through the public sessions (care at odd hours, a long absence, a Next day preview, Away mode), owning
// nothing. Every record and the manifest it leaves are hashed. The expected hash was produced by the build before
// helpers existed (ec8b678), so with no helpers owned the game must play and save byte for byte as before.
// Only APIs that build already had may be used here.
static class GoldenScenario
{
    public static string Hash(IEntitlements entitlements = null)
    {
        var start = new DateTimeOffset(2026, 3, 2, 7, 10, 0, TimeSpan.FromHours(1));
        var now = start;
        var store = new MemoryHouseholdStore();
        var game = new BabySimulation(TimeSpan.FromDays(3), BabySimulation.DefaultMaxNeedDecay, TimeSpan.FromDays(7),
            TimeSpan.FromDays(14));
        var household = new HouseholdSession(game, new PetSimulation(), store, () => now, entitlements);
        var baby = household.Adopt("Mia");
        var catalog = PetCatalog.Default;
        PetAppearance catLook = null;
        foreach (var breed in catalog.BreedsOf(Species.Cat))
        {
            if (breed.Unlock == BreedUnlock.Free)
            {
                catLook = PetAppearance.Default(catalog, breed.Breed);
                break;
            }
        }

        var cat = household.AdoptPet("Pip", catLook);
        var log = new StringBuilder();

        void Tick(double hours)
        {
            now = now.AddHours(hours);
            household.AdvanceAll(false);
        }

        void Care(CareAction action, Food food = Food.Milk) => baby.Session.ApplyCare(action, Toy.Rattle, food);

        for (var day = 0; day < 4; day++)
        {
            // Morning: breakfast, a change and play; the cat is fed and brushed.
            Care(CareAction.Feed);
            Care(CareAction.ChangeDiaper);
            Care(CareAction.Play);
            household.ApplyPetCare(cat.Slot, PetVerb.Feed);
            household.ApplyPetCare(cat.Slot, PetVerb.Brush);
            Tick(2.4);
            Care(CareAction.Soothe);
            Tick(5.1);
            if (day != 1)
            {
                // Afternoon: lunch, a story and wand play (day 1 is a busy day with nobody home).
                Care(CareAction.Feed);
                Care(CareAction.ReadStory);
                household.ApplyPetCare(cat.Slot, PetVerb.Wand);
            }

            Tick(4.25);
            Care(CareAction.Feed);
            household.ApplyPetCare(cat.Slot, PetVerb.Feed);
            household.ApplyPetCare(cat.Slot, PetVerb.Pet);
            Tick(1.5);
            Care(CareAction.Bath);
            Care(CareAction.Rest);
            household.ApplyPetCare(cat.Slot, PetVerb.Nap);
            if (day == 2)
            {
                // A Next day preview, then away for most of a day and a half.
                baby.Session.AdvancePreview(TimeSpan.FromDays(1));
                household.AdvancePetPreview(cat.Slot, TimeSpan.FromDays(1));
                Tick(31);
                log.Append(store.SlotStore(baby.Slot).Primary).Append('\n');
                continue;
            }

            if (day == 3)
                baby.Session.StartAway(TimeSpan.FromDays(2));
            Tick(12.75);
        }

        Tick(40);
        for (var slot = 1; slot <= HouseholdRules.PhysicalSlots; slot++)
            log.Append(slot).Append(':').Append(store.SlotStore(slot).Primary).Append('\n');
        log.Append(store.ManifestPrimary);
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(log.ToString()));
            var text = new StringBuilder();
            foreach (var b in hash)
                text.Append(b.ToString("x2"));
            return text.ToString();
        }
    }
}
