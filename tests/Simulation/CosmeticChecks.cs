using System;
using System.Collections.Generic;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

// Look packs, room themes, cat wear and the Founder pack (docs/monetization.md, M2b): a catalogue of content IDs, computed
// from entitlements at runtime. A locked saved ID stays saved and shows the free default; owning the pack again shows it.
static class CosmeticChecks
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    public static void Run()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7), TimeSpan.FromDays(12));
        CheckCatalogue();
        CheckEarnedLooksAreNeverSold();
        CheckBabyLooks(game);
        CheckRoomThemes(game);
        CheckPetWear(game);
        CheckOwnershipUnknownKeepsLooks();
        CheckDeveloperModeAndFlags();
        Console.WriteLine("Cosmetic checks passed.");
    }

    private static void CheckCatalogue()
    {
        var products = CosmeticCatalog.Products;
        Equal(6, products.Count, "six cosmetics products");
        foreach (var expected in new[] { "look.cozy-knits", "look.garden-party", "room.cloud-nursery", "room.starlight",
                     "petwear.cat.bandanas-bows", "supporter.founder" })
            Equal(true, products.Contains(expected), $"{expected} is a cosmetics product");
        foreach (var product in products)
        {
            Equal(true, ProductIds.IsKnownFamily(product), $"{product} is in a catalogue family");
            var pack = CosmeticCatalog.PackForProduct(product);
            Equal(true, pack != null && pack.ContentIds.Count > 0 && pack.Title.Length > 0 && pack.Contents.Length > 0,
                $"{product} names what is inside");
        }

        var all = CosmeticCatalog.Packs.SelectMany(pack => pack.ContentIds).ToList();
        Equal(all.Count, all.Distinct().Count(), "a content ID belongs to one pack");

        Equal("outfit.oatmeal-knit", CosmeticCatalog.OutfitId(Outfit.OatmealKnit), "outfit content IDs");
        Equal("headwear.pom-beanie", CosmeticCatalog.HeadwearId(Headwear.PomBeanie), "headwear content IDs");
        Equal("room-theme.cloud-nursery", CosmeticCatalog.RoomId(NurseryTheme.CloudNursery), "room content IDs");
        Equal("wear.cat.bandana", CosmeticCatalog.PetWearId(Species.Cat, PetAccessory.Bandana), "pet wear content IDs");
        Equal("wear-color.butter", CosmeticCatalog.PetAccentId(Outfit.Butter), "pet colour content IDs");

        void InPack(string id, string product) => Equal(product, CosmeticCatalog.PackFor(id)?.ProductId, $"{id} is in {product}");
        foreach (var outfit in new[] { Outfit.OatmealKnit, Outfit.BerryKnit, Outfit.MossKnit, Outfit.FogKnit })
            InPack(CosmeticCatalog.OutfitId(outfit), CosmeticCatalog.CozyKnits);
        InPack(CosmeticCatalog.HeadwearId(Headwear.PomBeanie), CosmeticCatalog.CozyKnits);
        foreach (var outfit in new[] { Outfit.Rosebud, Outfit.Marigold, Outfit.GardenMint, Outfit.Bluebell })
            InPack(CosmeticCatalog.OutfitId(outfit), CosmeticCatalog.GardenParty);
        InPack(CosmeticCatalog.HeadwearId(Headwear.SunHat), CosmeticCatalog.GardenParty);
        InPack(CosmeticCatalog.RoomId(NurseryTheme.CloudNursery), CosmeticCatalog.CloudNursery);
        InPack(CosmeticCatalog.RoomId(NurseryTheme.Starlight), CosmeticCatalog.Starlight);
        InPack(CosmeticCatalog.PetWearId(Species.Cat, PetAccessory.Bandana), CosmeticCatalog.CatBandanasBows);
        InPack(CosmeticCatalog.PetWearId(Species.Cat, PetAccessory.Bow), CosmeticCatalog.CatBandanasBows);
        foreach (var accent in CosmeticCatalog.PackAccents)
            InPack(CosmeticCatalog.PetAccentId(accent), CosmeticCatalog.CatBandanasBows);
        InPack(CosmeticCatalog.OutfitId(Outfit.FernGold), CosmeticCatalog.Founder);
        InPack(CosmeticCatalog.RoomId(NurseryTheme.GoldenHour), CosmeticCatalog.Founder);
        InPack(CosmeticCatalog.FounderBadge, CosmeticCatalog.Founder);

        // Every outfit, hat and room added after the free set is in a pack; the free set stays free.
        foreach (Outfit outfit in Enum.GetValues(typeof(Outfit)))
            Equal(outfit > Outfit.Midnight ? CosmeticAccess.Product : outfit == Outfit.Midnight ? CosmeticAccess.Earned : CosmeticAccess.Free,
                CosmeticCatalog.AccessOf(CosmeticCatalog.OutfitId(outfit)), $"{outfit} access");
        foreach (Headwear headwear in Enum.GetValues(typeof(Headwear)))
            Equal(headwear > Headwear.DaisyClip ? CosmeticAccess.Product
                    : EarnedLooks.RhythmFor(headwear) > 0 ? CosmeticAccess.Earned : CosmeticAccess.Free,
                CosmeticCatalog.AccessOf(CosmeticCatalog.HeadwearId(headwear)), $"{headwear} access");
        foreach (NurseryTheme theme in Enum.GetValues(typeof(NurseryTheme)))
            Equal(theme > NurseryTheme.Peach ? CosmeticAccess.Product : CosmeticAccess.Free,
                CosmeticCatalog.AccessOf(CosmeticCatalog.RoomId(theme)), $"{theme} access");
        foreach (var accent in CosmeticCatalog.FreeAccents)
            Equal(true, CosmeticCatalog.IsPetAccentUnlocked(NoEntitlements.Instance, accent), $"{accent} collars are free");
        Equal(CosmeticAccess.Free, CosmeticCatalog.AccessOf(CosmeticCatalog.PetWearId(Species.Cat, PetAccessory.Collar)),
            "the collar is free");

        // Real human features are never sold (decision 6): no skin, hair colour or hair style is in a pack.
        foreach (var id in all)
            Equal(false, id.StartsWith("skin.") || id.StartsWith("hair"), $"{id} is not a human feature");
        Equal("Part of the Cozy knits pack — a grown-up can get it in Grown-ups.",
            CosmeticCatalog.LockedLine(CosmeticCatalog.PackFor(CosmeticCatalog.OutfitId(Outfit.MossKnit))), "the calm locked line");
    }

    private static void CheckEarnedLooksAreNeverSold()
    {
        var earned = new[]
        {
            CosmeticCatalog.HeadwearId(Headwear.DaisyClip), CosmeticCatalog.HeadwearId(Headwear.BucketHat),
            CosmeticCatalog.OutfitId(Outfit.Midnight),
        };
        foreach (var id in earned)
        {
            Equal(CosmeticAccess.Earned, CosmeticCatalog.AccessOf(id), $"{id} is earned");
            Equal(null, CosmeticCatalog.PackFor(id), $"{id} is in no pack");
            Equal(false, CosmeticCatalog.Packs.Any(pack => pack.ContentIds.Contains(id)), $"no product sells {id}");
        }

        // Owning every product never unlocks an earned look: its care rhythm still decides.
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7), TimeSpan.FromDays(12));
        var session = new BabySession(game, new MemorySaveStore(), () => Start);
        session.Adopt("Mia");
        var everything = new FakeEntitlements(CosmeticCatalog.Products.ToArray());
        ExpectInvalid(() => session.SetAppearance(BabyAppearance.Default.WithHeadwear(Headwear.DaisyClip), false, everything),
            "every pack owned, the Daisy clip still waits for its rhythm");
    }

    private static void CheckBabyLooks(BabySimulation game)
    {
        var owned = new FakeEntitlements();
        var saves = new MemorySaveStore();
        var session = new BabySession(game, saves, () => Start);
        session.Adopt("Mia");
        var freeLook = BabyAppearance.Default.WithOutfit(Outfit.Sky).WithHeadwear(Headwear.Bow);
        session.SetAppearance(freeLook, false, owned);
        var freePayload = saves.Primary;

        var knit = freeLook.WithOutfit(Outfit.MossKnit).WithHeadwear(Headwear.PomBeanie);
        Equal(false, CosmeticCatalog.CanWear(session.State.Appearance, knit, owned), "a locked look can't be chosen");
        ExpectInvalid(() => session.SetAppearance(knit, false, owned), "the session refuses a locked look");
        ExpectInvalid(() => session.SetAppearance(knit), "no entitlements is the free game");
        Equal(freePayload, saves.Primary, "a refused look writes nothing");
        Equal(freeLook, CosmeticCatalog.Displayed(freeLook, owned), "a free look is shown as it is");
        Equal(BabyAppearance.Default.WithOutfit(Outfit.Sky), CosmeticCatalog.Displayed(freeLook.WithHeadwear(Headwear.SunHat), owned),
            "a locked hat shows no hat");
        Equal(CosmeticCatalog.GardenParty, CosmeticCatalog.LockedPack(freeLook.WithHeadwear(Headwear.SunHat), owned)?.ProductId,
            "the locked part names its pack");

        owned.Grant(CosmeticCatalog.CozyKnits);
        session.SetAppearance(knit, false, owned);
        Equal(knit, session.State.Appearance, "an owned look is saved");
        Equal(knit, CosmeticCatalog.Displayed(knit, owned), "an owned look is shown");
        Equal(null, CosmeticCatalog.LockedPack(knit, owned), "nothing is locked");

        // Refund: the saved IDs stay, the display falls back to the free defaults, and another part can still change.
        owned.Revoke(CosmeticCatalog.CozyKnits);
        var reopened = new BabySession(game, saves, () => Start);
        Equal(knit, reopened.State.Appearance, "a refund keeps the saved look");
        Equal(freeLook.WithOutfit(Outfit.Sage).WithHeadwear(Headwear.None), CosmeticCatalog.Displayed(reopened.State.Appearance, owned),
            "a refunded look shows Sage and no hat");
        var curls = knit.WithHairStyle(HairStyle.Coils);
        reopened.SetAppearance(curls, false, owned);
        Equal(curls, reopened.State.Appearance, "a kept premium part may stay while the hair changes");
        ExpectInvalid(() => reopened.SetAppearance(curls.WithOutfit(Outfit.BerryKnit), false, owned),
            "another locked outfit still can't be chosen");
        owned.Grant(CosmeticCatalog.CozyKnits);
        Equal(curls, CosmeticCatalog.Displayed(reopened.State.Appearance, owned), "buying again restores the look");

        // The record holds only content IDs: a look-pack baby round-trips, and an older free payload is unchanged.
        Equal(curls, new BabySession(game, saves, () => Start).State.Appearance, "premium content IDs round-trip");
        Equal(false, freePayload.Contains("look.") || freePayload.Contains("cozy"), "records never store purchases");
    }

    private static void CheckRoomThemes(BabySimulation game)
    {
        var owned = new FakeEntitlements();
        var session = new BabySession(game, new MemorySaveStore(), () => Start);
        session.Adopt("Mia");
        session.SetNurseryTheme(NurseryTheme.Peach, owned);
        Equal(NurseryTheme.Peach, session.State.NurseryTheme, "Peach is free");
        ExpectInvalid(() => session.SetNurseryTheme(NurseryTheme.Starlight, owned), "a locked room can't be chosen");
        owned.Grant(CosmeticCatalog.Starlight);
        session.SetNurseryTheme(NurseryTheme.Starlight, owned);
        Equal(NurseryTheme.Starlight, CosmeticCatalog.Displayed(session.State.NurseryTheme, owned), "an owned room shows");
        owned.Revoke(CosmeticCatalog.Starlight);
        Equal(NurseryTheme.Starlight, session.State.NurseryTheme, "a refund keeps the saved room");
        Equal(NurseryTheme.Sage, CosmeticCatalog.Displayed(session.State.NurseryTheme, owned), "a refunded room shows Sage");
        ExpectInvalid(() => session.SetNurseryTheme(NurseryTheme.GoldenHour, owned), "the Founder room needs the Founder pack");
        owned.Grant(CosmeticCatalog.Founder);
        session.SetNurseryTheme(NurseryTheme.GoldenHour, owned);
        Equal(NurseryTheme.GoldenHour, session.State.NurseryTheme, "the Founder pack opens Golden hour");

        // Pet rooms share one theme, kept in the manifest only while it is not Sage.
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, new PetSimulation(), store, () => Start, owned);
        household.Adopt("Mia");
        var before = store.ManifestPrimary;
        Equal(false, before.Contains("<rooms"), "a household that never picks a pet room theme writes no element");
        ExpectInvalid(() => household.SetPetRoomTheme(NurseryTheme.CloudNursery), "a locked pet room can't be chosen");
        household.SetPetRoomTheme(NurseryTheme.Peach);
        Equal(true, store.ManifestPrimary.Contains("<rooms pet-theme=\"1\""), "the pet room theme is saved");
        Equal(NurseryTheme.Peach, new HouseholdSession(game, new PetSimulation(), store, () => Start, owned).PetRoomTheme,
            "the pet room theme survives a restart");
        household.SetPetRoomTheme(NurseryTheme.Sage);
        Equal(before, store.ManifestPrimary, "back to Sage, the manifest is byte for byte as before");
        owned.Grant(CosmeticCatalog.CloudNursery);
        household.SetPetRoomTheme(NurseryTheme.CloudNursery);
        owned.Revoke(CosmeticCatalog.CloudNursery);
        var after = new HouseholdSession(game, new PetSimulation(), store, () => Start, owned);
        Equal(NurseryTheme.CloudNursery, after.PetRoomTheme, "a refund keeps the pet room theme");
        Equal(NurseryTheme.Sage, CosmeticCatalog.Displayed(after.PetRoomTheme, owned), "and shows Sage");
    }

    private static void CheckPetWear(BabySimulation game)
    {
        var owned = new FakeEntitlements();
        var store = new MemoryHouseholdStore();
        var pets = new PetSimulation();
        var household = new HouseholdSession(game, pets, store, () => Start, owned);
        household.Adopt("Mia");
        var plain = PetAppearance.Default(pets.Catalog, PetBreed.DomesticShorthair)
            .WithAccessory(PetAccessory.Collar).WithAccessoryAccent(Outfit.Sky);
        var bandana = plain.WithAccessory(PetAccessory.Bandana);
        ExpectInvalid(() => household.AdoptPet("Pip", bandana), "a locked bandana can't be chosen at adoption");
        ExpectInvalid(() => household.AdoptPet("Pip", plain.WithAccessoryAccent(Outfit.Butter)), "a pack colour needs the pack");
        ExpectInvalid(() => household.AdoptPet("Pip", plain.WithAccessoryAccent(Outfit.Rosebud)),
            "a look-pack colour needs its look pack");
        var pip = household.AdoptPet("Pip", plain);
        ExpectInvalid(() => household.SetPetAppearance(pip.Slot, bandana), "a locked bandana can't be put on later");

        owned.Grant(CosmeticCatalog.CatBandanasBows);
        var bow = plain.WithAccessory(PetAccessory.Bow).WithAccessoryAccent(Outfit.Lilac);
        household.SetPetAppearance(pip.Slot, bow);
        Equal(bow, household.Companions.First(c => c.Slot == pip.Slot).PetState.Appearance, "an owned bow is saved");
        Equal(bow, CosmeticCatalog.Displayed(bow, owned), "and shown");
        ExpectArgument(() => household.SetPetAppearance(pip.Slot, bow.WithCoat(PetCoat.Gray)), "only the accessory can change");

        owned.Revoke(CosmeticCatalog.CatBandanasBows);
        var reopened = new HouseholdSession(game, pets, store, () => Start, owned);
        var kept = reopened.Companions.First(c => c.Slot == pip.Slot).PetState.Appearance;
        Equal(bow, kept, "a refund keeps the saved bow");
        Equal(plain.WithAccessoryAccent(Outfit.Sage), CosmeticCatalog.Displayed(kept, owned), "a refunded bow shows a Sage collar");
        Equal(CosmeticCatalog.CatBandanasBows, CosmeticCatalog.LockedPack(kept, owned)?.ProductId, "the locked bow names its pack");
        owned.Grant(CosmeticCatalog.CatBandanasBows);
        Equal(bow, CosmeticCatalog.Displayed(kept, owned), "buying again restores the bow");
    }

    private static void CheckOwnershipUnknownKeepsLooks()
    {
        var unknown = new FakeEntitlements { OwnershipUnknown = true };
        var look = BabyAppearance.Default.WithOutfit(Outfit.Rosebud).WithHeadwear(Headwear.SunHat);
        Equal(look, CosmeticCatalog.Displayed(look, unknown), "a damaged cache never takes a look away");
        Equal(NurseryTheme.Starlight, CosmeticCatalog.Displayed(NurseryTheme.Starlight, unknown), "nor a room");
        Equal(false, CosmeticCatalog.CanWear(BabyAppearance.Default, look, unknown), "but nothing new opens");
    }

    private static void CheckDeveloperModeAndFlags()
    {
        var products = DeveloperUnlocks.Products(new PetSimulation().Catalog);
        foreach (var product in CosmeticCatalog.Products)
            Equal(true, products.Contains(product), $"developer mode grants {product}");
        Equal(products.Count, products.Distinct().Count(), "each product is granted once");

        var flags = PlaytestFlags.Parse("/nursery/?ent=look.cozy-knits,room.starlight,petwear.cat.bandanas-bows,supporter.founder");
        foreach (var product in new[] { "look.cozy-knits", "room.starlight", "petwear.cat.bandanas-bows", "supporter.founder" })
            Equal(true, flags.Contains(product), $"?ent= grants {product}");
        Equal(true, PlaytestFlags.Parse("/nursery/?ent=look.garden-party").Contains(CosmeticCatalog.GardenParty), "a single look pack flag");
    }

    private static void ExpectInvalid(Action attempt, string description) => Expect<InvalidOperationException>(attempt, description);
    private static void ExpectArgument(Action attempt, string description) => Expect<ArgumentException>(attempt, description);

    private static void Expect<TException>(Action attempt, string description) where TException : Exception
    {
        try
        {
            attempt();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected {typeof(TException).Name}");
    }

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }
}
