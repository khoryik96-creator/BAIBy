using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;

static class EntitlementChecks
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Run()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7), TimeSpan.FromDays(12));
        CheckProductIds();
        CheckCapacityRules();
        CheckCapacityInHousehold(game);
        CheckRefundRestsExtras(game);
        CheckRestoreWakesWithOneCappedAdvance(game);
        CheckPlayerChoosesWhoRests(game);
        CheckLongRestCatchesUpAgeOnWaking(game);
        CheckGoodbyeWhileOthersRest(game);
        CheckCacheSurvivesRestart();
        CheckCacheRecovery(game);
        CheckUnreadableCacheRestsNobody(game);
        CheckPlaytestGrantsOnlyInTestBuilds(game);
        CheckStoreAnswers();
        CheckParentalGate();
        CheckPlaytestFlagsAndStoreFront();
        CheckDeveloperMode(game);
        Console.WriteLine("Entitlement checks passed.");
    }

    // Developer mode (test builds): every product granted and every earned breed open, without touching
    // saved progress, and fully reversible.
    private static void CheckDeveloperMode(BabySimulation game)
    {
        var pets = new PetSimulation();
        var products = DeveloperUnlocks.Products(pets.Catalog);
        Equal(true, products.Contains(ProductIds.HomeRooms2), "developer mode grants the extra rooms");
        Equal(true, products.Contains("breed.dog.corgi"), "developer mode grants premium breeds such as the Corgi");
        Equal(true, products.Contains("breed.cat.bengal"), "developer mode grants the premium cats");
        foreach (var helper in new[] { "helper.feeder", "helper.play", "helper.tidy", "helper.set" })
            Equal(true, products.Contains(helper), $"developer mode grants {helper}");
        Equal(products.Count, products.Distinct().Count(), "each product is granted once");
        foreach (var product in products)
            Equal(true, ProductIds.IsKnownFamily(product), $"{product} is a catalogue product");
        foreach (var breed in pets.Catalog.AllBreeds.Where(breed => breed.Unlock == BreedUnlock.Premium))
            Equal(true, pets.Catalog.ProductsGranting(breed.Breed).Any(products.Contains), $"developer mode covers {breed.Breed}");

        Equal(true, DeveloperUnlocks.RequestedBy("https://x.app.github.dev/nursery/?dev=1"), "?dev=1 asks for developer mode");
        Equal(true, DeveloperUnlocks.RequestedBy("/nursery/?ent=home.rooms2&dev=1#top"), "dev=1 among other flags");
        Equal(false, DeveloperUnlocks.RequestedBy("/nursery/?dev=0"), "dev=0 does not");
        Equal(false, DeveloperUnlocks.RequestedBy("/nursery/?devmode=1"), "only the exact flag counts");
        Equal(false, DeveloperUnlocks.RequestedBy("/nursery/#dev=1"), "a fragment is not a query");
        Equal(false, DeveloperUnlocks.RequestedBy(null), "no URL, no developer mode");

        var owned = new FakeEntitlements();
        var household = new HouseholdSession(game, pets, new MemoryHouseholdStore(), () => Start, owned);
        var progress = household.ProgressFor(Species.Cat);
        Equal(false, household.CanAdoptBreed(PetBreed.Tabby), "an earned breed starts locked");
        Equal(false, household.CanAdoptBreed(PetBreed.Bengal), "a premium breed starts locked");

        household.UnlockEarnedBreeds = true;
        foreach (var product in products)
            owned.Grant(product);
        Equal(true, household.CanAdoptBreed(PetBreed.Tabby), "developer mode opens earned breeds");
        Equal(true, household.CanAdoptBreed(PetBreed.Bengal), "developer mode opens premium breeds");
        Equal(HouseholdRules.FreeCompanions + 2, household.Capacity, "developer mode opens the extra rooms");
        Equal(HelperSettings.All, household.Helpers, "developer mode turns every helper on");
        Equal(progress.FullCareDays, household.ProgressFor(Species.Cat).FullCareDays, "real progress is untouched");
        Equal(progress.HighestBond, household.ProgressFor(Species.Cat).HighestBond, "real Bond progress is untouched");

        household.UnlockEarnedBreeds = false;
        foreach (var product in products)
            owned.Revoke(product);
        Equal(false, household.CanAdoptBreed(PetBreed.Tabby), "turning developer mode off locks earned breeds again");
        Equal(false, household.CanAdoptBreed(PetBreed.Bengal), "turning developer mode off locks premium breeds again");
        Equal(HouseholdRules.FreeCompanions, household.Capacity, "turning developer mode off returns to the free rooms");
        Equal(HelperSettings.None, household.Helpers, "turning developer mode off stops the helpers");
    }

    private static void CheckProductIds()
    {
        Equal("home.rooms2", ProductIds.HomeRooms2, "the More rooms product ID is stable");
        foreach (var id in new[] { "home.rooms2", "breed.dog.corgi", "breedpack.cat", "breedpack.all", "helper.set", "helper.feeder" })
            Equal(true, ProductIds.IsKnownFamily(id), $"{id} is a catalogue product");
        foreach (var id in new[] { "", "home", "home.", ".rooms2", "Home.rooms2", "home..rooms2", "home rooms2", "home.rooms2!" })
            Equal(false, ProductIds.IsWellFormed(id), $"'{id}' is not a well-formed product ID");
        Equal(true, ProductIds.IsWellFormed("future.thing"), "an unknown family is still well formed, for newer builds");
        Equal(false, ProductIds.IsKnownFamily("future.thing"), "an unknown family is not a catalogue product");
    }

    private static void CheckCapacityRules()
    {
        Equal(2, HouseholdRules.CapacityFor(null), "no entitlements means the free capacity");
        Equal(2, HouseholdRules.CapacityFor(NoEntitlements.Instance), "the free game holds two");
        var owned = new FakeEntitlements(ProductIds.HomeRooms2);
        Equal(4, HouseholdRules.CapacityFor(owned), "More rooms adds two rooms");
        Equal(true, HouseholdRules.CapacityFor(owned) <= HouseholdRules.PhysicalSlots, "capacity never exceeds the physical slots");
        Equal(2, HouseholdRules.CapacityFor(new FakeEntitlements("breed.dog.corgi", "helper.set")),
            "other products add no rooms");
        var unknown = new FakeEntitlements { OwnershipUnknown = true };
        Equal(2, HouseholdRules.CapacityFor(unknown, 1), "unknown ownership never adds a room for adoption");
        Equal(3, HouseholdRules.CapacityFor(unknown, 3), "unknown ownership keeps every companion awake");
        Equal(HouseholdRules.PhysicalSlots, HouseholdRules.CapacityFor(unknown, 20), "unknown ownership stays within the slots");
        Equal(2, HouseholdRules.CapacityFor(NoEntitlements.Instance, 3), "known ownership rests companions beyond capacity");
    }

    private static void CheckCapacityInHousehold(BabySimulation game)
    {
        var free = new HouseholdSession(game, new MemoryHouseholdStore(), () => Start, NoEntitlements.Instance);
        free.Adopt("Mia");
        free.Adopt("Leo");
        Equal(2, free.Capacity, "the free household holds two");
        Equal(false, free.CanAdopt, "the free household is full at two");
        ExpectInvalidOperation(() => free.Adopt("Ivy"), "a third adoption needs more rooms");

        var store = new MemoryHouseholdStore();
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var household = new HouseholdSession(game, store, () => Start, rooms);
        Equal(4, household.Capacity, "More rooms holds four");
        foreach (var name in new[] { "Mia", "Leo", "Ivy", "Ada" })
            household.Adopt(name);
        Equal(false, household.CanAdopt, "four companions fill a household with More rooms");
        Equal(false, household.HasResting, "nobody rests while every companion has a room");
        ExpectInvalidOperation(() => household.Adopt("Kit"), "capacity 4 is enforced");
        Equal<string>(null, store.SlotStore(5).Primary, "a refused adoption writes no record");
    }

    private static void CheckRefundRestsExtras(BabySimulation game)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var household = new HouseholdSession(game, store, () => now, rooms);
        foreach (var name in new[] { "Mia", "Leo", "Ivy", "Ada" })
            household.Adopt(name);
        Equal(4, household.Active.Slot, "the newest baby is active");
        var ivy = store.SlotStore(3).Primary;
        var ada = store.SlotStore(4).Primary;
        var restingWrites = store.SlotStore(3).Writes + store.SlotStore(4).Writes;
        var manifestBefore = store.ManifestPrimary;

        rooms.Revoke(ProductIds.HomeRooms2);
        Equal(2, household.Capacity, "a refund returns to the free capacity");
        Equal(true, household.HasResting, "companions beyond capacity rest");
        Equal(false, household.IsResting(1), "the first companion stays awake");
        Equal(false, household.IsResting(2), "the second companion stays awake");
        Equal(true, household.IsResting(3), "the third companion rests");
        Equal(true, household.IsResting(4), "the fourth companion rests");
        Equal(4, household.Companions.Count, "a refund deletes nothing");
        Equal(1, household.Active.Slot, "a resting companion is never active");
        Equal(manifestBefore, store.ManifestPrimary, "the lock is not written into the manifest");
        ExpectInvalidOperation(() => household.Switch(3), "a resting companion's room doesn't open");
        ExpectInvalidOperation(() => household.SayGoodbye(4), "a resting companion is read-only");
        Equal(false, household.CanAdopt, "a household with resting companions is full");

        var frozen = household.Companions[2].State;
        now = now.AddHours(10);
        household.AdvanceAll();
        household.AdvanceAll(activeOnScreen: false);
        Equal(restingWrites, store.SlotStore(3).Writes + store.SlotStore(4).Writes, "resting companions make zero writes");
        Equal(ivy, store.SlotStore(3).Primary, "a resting record is untouched");
        Equal(ada, store.SlotStore(4).Primary, "every resting record is untouched");
        Equal(frozen, household.Companions[2].State, "a resting companion does not decay");
        Equal(frozen, household.PreviewState(3), "Home shows a resting companion as it was");
        Equal(true, household.Companions[0].State.Satiety < frozen.Satiety, "awake companions still advance");
        Equal(false, household.HasCelebrations(3), "a resting companion gains no celebrations");

        // A restart while refunded: the lock is computed again and still makes no writes.
        var reopened = new HouseholdSession(game, store, () => now, rooms);
        reopened.AdvanceAll();
        Equal(true, reopened.IsResting(3) && reopened.IsResting(4), "resting is recomputed after a restart");
        Equal(ivy, store.SlotStore(3).Primary, "a restart writes no resting record");
        Equal(restingWrites, store.SlotStore(3).Writes + store.SlotStore(4).Writes, "a restart makes zero resting writes");
        Equal(false, store.SlotStore(3).Primary.Contains("resting"), "records never store the lock");
    }

    private static void CheckRestoreWakesWithOneCappedAdvance(BabySimulation game)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var household = new HouseholdSession(game, store, () => now, rooms);
        foreach (var name in new[] { "Mia", "Leo", "Ivy" })
            household.Adopt(name);

        rooms.Revoke(ProductIds.HomeRooms2);
        var frozen = household.Companions[2].State;
        var writes = store.SlotStore(3).Writes;
        now = now.AddHours(30);
        household.AdvanceAll();
        Equal(writes, store.SlotStore(3).Writes, "the third baby rests through the refund");

        rooms.Grant(ProductIds.HomeRooms2);
        Equal(false, household.IsResting(3), "a restore wakes resting companions");
        Equal(BabySaveCodec.Encode(game.Advance(frozen, now).State), BabySaveCodec.Encode(household.PreviewState(3)), "Home previews the woken baby's capped advance");
        household.AdvanceAll();
        Equal(writes + 1, store.SlotStore(3).Writes, "waking is one advance and one write");
        var woken = household.Companions[2].State;
        Equal(BabySaveCodec.Encode(game.Advance(frozen, now).State), BabySaveCodec.Encode(woken), "waking is one normal advance");
        var capped = game.Advance(frozen, frozen.TimeCheckpoint.AddHours(4)).State;
        Equal(capped.Satiety, woken.Satiety, "the woken baby's decay is capped");
        Equal(capped.Comfort, woken.Comfort, "the woken baby's comfort decay is capped");
        household.AdvanceAll();
        Equal(writes + 1, store.SlotStore(3).Writes, "a second advance at the same moment writes nothing");
        household.Switch(3);
        Equal(3, household.Active.Slot, "a woken companion's room opens");
    }

    private static void CheckPlayerChoosesWhoRests(BabySimulation game)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var household = new HouseholdSession(game, store, () => now, rooms);
        foreach (var name in new[] { "Mia", "Leo", "Ivy" })
            household.Adopt(name);
        household.Switch(2);
        rooms.Revoke(ProductIds.HomeRooms2);

        now = now.AddHours(1);
        var ivyWrites = store.SlotStore(3).Writes;
        var leoWrites = store.SlotStore(2).Writes;
        ExpectInvalidOperation(() => household.SwapResting(2, 3), "only a resting companion can be woken");
        ExpectInvalidOperation(() => household.SwapResting(3, 3), "a companion can't swap with itself");
        household.SwapResting(3, 2);
        Equal(true, household.IsResting(2), "the chosen companion rests instead");
        Equal(false, household.IsResting(3), "the woken companion has a room");
        Equal(3, household.Active.Slot, "the woken companion takes the resting one's place on screen");
        Equal(leoWrites + 1, store.SlotStore(2).Writes, "the companion going to rest commits its awake time");
        Equal(ivyWrites + 1, store.SlotStore(3).Writes, "the woken companion gets one advance");
        Equal("Mia,Ivy,Leo", string.Join(",", household.Companions.Select(companion => companion.State.Name)),
            "the choice is a place in Home order");

        var leo = store.SlotStore(2).Primary;
        now = now.AddHours(6);
        var reopened = new HouseholdSession(game, store, () => now, rooms);
        reopened.AdvanceAll();
        Equal(true, reopened.IsResting(2), "the choice survives a restart");
        Equal(leo, store.SlotStore(2).Primary, "the newly resting companion makes no writes");

        rooms.Grant(ProductIds.HomeRooms2);
        Equal(false, reopened.HasResting, "a restore wakes everyone");
        Equal("Mia,Ivy,Leo", string.Join(",", reopened.Companions.Select(companion => companion.State.Name)),
            "Home order is kept after a restore");

        var failing = new MemoryHouseholdStore();
        var failingRooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var failingHome = new HouseholdSession(game, failing, () => now, failingRooms);
        foreach (var name in new[] { "Mia", "Leo", "Ivy" })
            failingHome.Adopt(name);
        failingRooms.Revoke(ProductIds.HomeRooms2);
        failing.FailManifestWrites = true;
        ExpectIo(() => failingHome.SwapResting(3, 1), "a failed swap reports the failure");
        Equal(true, failingHome.IsResting(3), "a failed swap leaves the resting companion resting");
        Equal(false, failingHome.IsResting(1), "a failed swap keeps the awake companion awake");
    }

    // Waking is one normal capped advance: needs decay for at most the cap, while age counts the whole rest,
    // as after any absence. Milestones reached meanwhile wait as celebrations. The copy promises only that
    // needs don't drop while a companion rests.
    private static void CheckLongRestCatchesUpAgeOnWaking(BabySimulation game)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var household = new HouseholdSession(game, store, () => now, rooms);
        foreach (var name in new[] { "Mia", "Leo", "Ivy" })
            household.Adopt(name);
        household.Switch(1);
        rooms.Revoke(ProductIds.HomeRooms2);
        var frozen = household.Companions[2].State;
        Equal(BabyStage.Newborn, HouseholdRules.StageOf(frozen), "Ivy goes to rest as a newborn");

        now = now.AddDays(60);
        household.AdvanceAll();
        Equal(TimeSpan.Zero, household.PreviewState(3).Age, "a resting companion's age is shown as it was");
        Equal(false, household.HasCelebrations(3), "no milestone is announced while resting");

        rooms.Grant(ProductIds.HomeRooms2);
        household.AdvanceAll();
        var woken = household.Companions[2].State;
        Equal(TimeSpan.FromDays(60), woken.Age, "waking counts the whole rest as age, like any absence");
        Equal(BabyStage.Walker, HouseholdRules.StageOf(woken), "milestones reached during the rest are kept");
        Equal(true, household.HasCelebrations(3), "milestones reached during the rest wait as celebrations");
        // Sitting, crawling, walking and (at 60 days) First words.
        Equal(4, household.TakeCelebrations(3).Count, "each milestone is celebrated once on waking");
        var capped = game.Advance(frozen, frozen.TimeCheckpoint.AddHours(4)).State;
        Equal(capped.Satiety, woken.Satiety, "needs decay for no more than the cap, however long the rest");
    }

    // A goodbye to an awake companion hands the room to the first resting one, and the household says so.
    private static void CheckGoodbyeWhileOthersRest(BabySimulation game)
    {
        var store = new MemoryHouseholdStore();
        var rooms = new FakeEntitlements(ProductIds.HomeRooms2);
        var household = new HouseholdSession(game, store, () => Start, rooms);
        foreach (var name in new[] { "Mia", "Leo", "Ivy" })
            household.Adopt(name);
        Equal<HouseholdCompanion>(null, household.NextToWake, "nobody waits to wake while everyone has a room");
        rooms.Revoke(ProductIds.HomeRooms2);
        Equal(3, household.NextToWake.Slot, "the first resting companion is next to wake");
        household.SayGoodbye(2);
        Equal(false, household.IsResting(3), "the freed room goes to the resting companion");
        Equal<HouseholdCompanion>(null, household.NextToWake, "nobody else waits to wake");
        Equal(false, household.CanAdopt, "the home stays full");
    }

    private static void CheckCacheSurvivesRestart()
    {
        var now = Start;
        var cache = new MemoryEntitlementCache();
        var entitlements = new CachedEntitlements(cache, () => now);
        Equal(0, entitlements.Owned.Count, "a new cache owns nothing");
        Equal(0, cache.Writes, "opening an empty cache writes nothing");
        var changes = 0;
        entitlements.Changed += () => changes++;
        entitlements.Grant(ProductIds.HomeRooms2, EntitlementSource.Store);
        Equal(1, changes, "a grant is announced");
        Equal(true, entitlements.Owns(ProductIds.HomeRooms2), "a grant is owned at once");
        Equal(1, cache.Writes, "a grant is cached");
        Equal(cache.Primary, cache.Backup, "the first grant also seeds the backup, so one damaged key loses nothing");

        now = now.AddDays(400);
        var restarted = new CachedEntitlements(cache, () => now);
        Equal(true, restarted.Owns(ProductIds.HomeRooms2), "the cache is trusted at launch, however old");
        Equal(Start, restarted.Grants[0].ConfirmedAt, "the cache keeps when the grant was confirmed");
        Equal(EntitlementSource.Store, restarted.Grants[0].Source, "the cache keeps the grant's source");
        Equal(1, cache.Writes, "launching writes nothing");

        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4),
            TimeSpan.FromDays(7), TimeSpan.FromDays(12)), store, () => now, restarted);
        Equal(4, household.Capacity, "a cached grant opens More rooms offline");

        const string golden = "<entitlements version=\"1\"><grant product=\"home.rooms2\" source=\"1\" confirmed=\"639028224000000000\" /></entitlements>";
        var decoded = new CachedEntitlements(new MemoryEntitlementCache { Primary = golden }, () => now, honorPlaytestGrants: true);
        Equal(EntitlementSource.Playtest, decoded.Grants[0].Source, "golden cache keeps a playtest grant");
        Equal(Start, decoded.Grants[0].ConfirmedAt, "golden cache keeps the confirmation time");
        Equal(true, decoded.HasPlaytestGrants, "playtest grants are visible for the badge");
        Equal(0, (int)EntitlementSource.Store, "store source ID is stable");
        Equal(1, (int)EntitlementSource.Playtest, "playtest source ID is stable");
    }

    private static void CheckCacheRecovery(BabySimulation game)
    {
        var valid = "<entitlements version=\"1\"><grant product=\"home.rooms2\" source=\"0\" confirmed=\"639028224000000000\" /></entitlements>";
        var corrupt = new MemoryEntitlementCache { Primary = "<entitlements", Backup = valid };
        var fromBackup = new CachedEntitlements(corrupt, () => Start);
        Equal(true, fromBackup.Owns(ProductIds.HomeRooms2), "a corrupt cache falls back to its backup");
        fromBackup.Grant("helper.set", EntitlementSource.Store);
        Equal(valid, corrupt.Backup, "the backup keeps the last valid cache, not the corrupt bytes");

        foreach (var payload in new[]
        {
            "<entitlements", "<entitlements version=\"0\" />", "<baby version=\"1\" />",
            valid.Replace("source=\"0\"", "source=\"7\""), valid.Replace("home.rooms2", "Home Rooms"),
            valid.Replace("</entitlements>", "<grant product=\"home.rooms2\" source=\"0\" confirmed=\"1\" /></entitlements>"),
            valid.Replace("version=\"1\"", "version=\"1\" unverified=\"2\""), "<entitlements version=\"9\" />"
        })
        {
            var broken = new CachedEntitlements(new MemoryEntitlementCache { Primary = payload }, () => Start);
            Equal(true, broken.OwnershipUnknown, $"an unreadable cache with no backup means ownership is unknown: {payload}");
            Equal(4, HouseholdOfFour(game, broken).Capacity, $"an unreadable cache rests nobody: {payload}");
        }

        // A single purchase, then a partial write truncates the primary: the backup seeded by the first write
        // brings the purchase back.
        var single = new MemoryEntitlementCache();
        new CachedEntitlements(single, () => Start).Grant(ProductIds.HomeRooms2, EntitlementSource.Store);
        single.Primary = single.Primary.Substring(0, single.Primary.Length / 2);
        var truncated = new CachedEntitlements(single, () => Start);
        Equal(true, truncated.Owns(ProductIds.HomeRooms2), "a truncated primary falls back to the first write's backup");
        Equal(false, truncated.OwnershipUnknown, "a readable backup makes ownership known");

        var newer = new MemoryEntitlementCache { Primary = "<entitlements version=\"9\" />", Backup = valid };
        var fromNewer = new CachedEntitlements(newer, () => Start);
        Equal(true, fromNewer.Owns(ProductIds.HomeRooms2), "a cache from a newer build falls back to its backup");
        fromNewer.Grant("helper.set", EntitlementSource.Store);
        Equal("<entitlements version=\"9\" />", newer.Primary, "a newer cache is never rewritten");
        Equal(true, fromNewer.Owns("helper.set"), "a grant still applies for this session");

        var failing = new MemoryEntitlementCache { FailWrites = true };
        var unsaved = new CachedEntitlements(failing, () => Start);
        unsaved.Grant(ProductIds.HomeRooms2, EntitlementSource.Store);
        Equal(true, unsaved.Owns(ProductIds.HomeRooms2), "a failed cache write keeps the grant in memory");
        Equal(true, unsaved.HasUnsavedChanges, "a failed cache write is retried");
        failing.FailWrites = false;
        unsaved.Grant("helper.set", EntitlementSource.Store);
        Equal(false, unsaved.HasUnsavedChanges, "the next change saves everything");
        Equal(true, new CachedEntitlements(failing, () => Start).Owns(ProductIds.HomeRooms2), "the retried grant is cached");
    }

    // Adopts four companions with More rooms owned, then reopens the household with the given entitlements.
    private static HouseholdSession HouseholdOfFour(BabySimulation game, IEntitlements entitlements)
    {
        var store = new MemoryHouseholdStore();
        var adopting = new HouseholdSession(game, store, () => Start, new FakeEntitlements(ProductIds.HomeRooms2));
        foreach (var name in new[] { "Mia", "Leo", "Ivy", "Ada" })
            adopting.Adopt(name);
        return new HouseholdSession(game, store, () => Start, entitlements);
    }

    private static void CheckUnreadableCacheRestsNobody(BabySimulation game)
    {
        var now = Start;
        var cache = new MemoryEntitlementCache { Primary = "<entitlements version=\"1\"><gra" };
        var entitlements = new CachedEntitlements(cache, () => now, honorPlaytestGrants: true);
        Equal(true, entitlements.OwnershipUnknown, "a truncated only copy leaves ownership unknown");
        Equal(0, cache.Writes, "opening a damaged cache writes nothing");
        var household = HouseholdOfFour(game, entitlements);
        Equal(false, household.HasResting, "a damaged cache is not a refund");
        Equal(false, household.IsResting(3) || household.IsResting(4), "the extra companions stay awake");
        Equal(false, household.CanAdopt, "a damaged cache never opens a room for adoption");
        Equal<HouseholdCompanion>(null, household.NextToWake, "nobody waits to wake");

        // A playtest grant while unknown keeps the marker, so a relaunch is still unknown, not "owns nothing".
        entitlements.Grant("helper.set", EntitlementSource.Playtest);
        Equal(true, cache.Primary.Contains("unverified=\"1\""), "grants written while unknown are marked unverified");
        var relaunched = new CachedEntitlements(cache, () => now, honorPlaytestGrants: true);
        Equal(true, relaunched.OwnershipUnknown, "the unknown state survives a relaunch");
        Equal(true, relaunched.Owns("helper.set"), "the grant written while unknown is kept");

        // An unavailable store changes nothing; an answer ends the unknown state.
        var changes = 0;
        relaunched.Changed += () => changes++;
        relaunched.Apply(StoreAnswer.Unavailable);
        Equal(true, relaunched.OwnershipUnknown, "a network error leaves ownership unknown");
        var afterAnswer = HouseholdOfFour(game, relaunched);
        relaunched.Apply(StoreAnswer.Confirmed(new string[0]));
        Equal(false, relaunched.OwnershipUnknown, "a store answer makes ownership known");
        Equal(1, changes, "the end of unknown ownership is announced");
        Equal(true, afterAnswer.IsResting(3) && afterAnswer.IsResting(4), "only an answered store rests the extras");
        Equal(false, cache.Primary.Contains("unverified"), "the answer clears the marker");

        // A cache from a newer build with no backup is unknown too, and is never rewritten.
        var newer = new MemoryEntitlementCache { Primary = "<entitlements version=\"9\" />" };
        var fromNewer = new CachedEntitlements(newer, () => now);
        Equal(true, fromNewer.OwnershipUnknown, "a newer cache without a backup leaves ownership unknown");
        Equal(false, HouseholdOfFour(game, fromNewer).HasResting, "a rollback rests nobody");
    }

    private static void CheckPlaytestGrantsOnlyInTestBuilds(BabySimulation game)
    {
        var cache = new MemoryEntitlementCache();
        var development = new CachedEntitlements(cache, () => Start, honorPlaytestGrants: true);
        development.Grant(ProductIds.HomeRooms2, EntitlementSource.Playtest);
        development.Grant("helper.set", EntitlementSource.Playtest);
        Equal(true, development.HasPlaytestGrants, "a test build shows the playtest badge for any playtest grant");
        Equal(4, HouseholdRules.CapacityFor(development), "a test build honours playtest rooms");

        // The release build installed over it reads the same cache.
        var release = new CachedEntitlements(cache, () => Start);
        Equal(false, release.Owns(ProductIds.HomeRooms2), "a release build ignores playtest rooms");
        Equal(0, release.Owned.Count, "a release build owns no playtest grants");
        Equal(false, release.HasPlaytestGrants, "a release build shows no playtest badge");
        Equal(false, release.OwnershipUnknown, "ignored playtest grants are known, not unknown");
        Equal(2, HouseholdRules.CapacityFor(release), "a release build gives no free rooms");
        Equal(true, HouseholdOfFour(game, release).IsResting(3), "playtest companions rest in a release build");
        ExpectInvalidOperation(() => release.Grant(ProductIds.HomeRooms2, EntitlementSource.Playtest),
            "a release build makes no playtest grants");
        release.Apply(StoreAnswer.Confirmed(new string[0]));
        Equal(2, release.Grants.Count, "a release build keeps playtest grants in the cache");
        Equal(true, new CachedEntitlements(cache, () => Start, honorPlaytestGrants: true).Owns(ProductIds.HomeRooms2),
            "a later test build still sees its playtest grants");

        var changes = 0;
        release.Changed += () => changes++;
        release.Apply(StoreAnswer.Confirmed(new[] { ProductIds.HomeRooms2 }));
        Equal(true, release.Owns(ProductIds.HomeRooms2), "a real purchase replaces an ignored playtest grant");
        Equal(1, changes, "the purchase is announced");
    }

    private static void CheckStoreAnswers()
    {
        var now = Start;
        var cache = new MemoryEntitlementCache();
        var entitlements = new CachedEntitlements(cache, () => now, honorPlaytestGrants: true);
        var changes = 0;
        entitlements.Changed += () => changes++;
        entitlements.Apply(StoreAnswer.Confirmed(new[] { ProductIds.HomeRooms2 }));
        Equal(true, entitlements.Owns(ProductIds.HomeRooms2), "a confirmed purchase is granted");
        Equal(1, changes, "a new grant is announced");

        var writes = cache.Writes;
        for (var attempt = 0; attempt < 5; attempt++)
            entitlements.Apply(StoreAnswer.Unavailable);
        Equal(true, entitlements.Owns(ProductIds.HomeRooms2), "a network error never revokes");
        Equal(writes, cache.Writes, "a network error writes nothing");
        Equal(1, changes, "a network error changes nothing");

        entitlements.Apply(StoreAnswer.Confirmed(new string[0]));
        Equal(true, entitlements.Owns(ProductIds.HomeRooms2), "a product missing from a query is not revoked");
        Equal(true, new CachedEntitlements(cache, () => now).Owns(ProductIds.HomeRooms2), "the cache still holds it");

        now = now.AddDays(3);
        entitlements.Apply(StoreAnswer.Confirmed(new[] { ProductIds.HomeRooms2 }));
        Equal(now, entitlements.Grants[0].ConfirmedAt, "a repeated confirmation refreshes the time");
        Equal(1, changes, "a repeated confirmation changes nothing owned");

        entitlements.Apply(StoreAnswer.Confirmed(new[] { ProductIds.HomeRooms2 }, new[] { ProductIds.HomeRooms2 }));
        Equal(true, entitlements.Owns(ProductIds.HomeRooms2), "an ambiguous answer keeps the room");

        entitlements.Apply(StoreAnswer.Confirmed(new string[0], new[] { ProductIds.HomeRooms2 }));
        Equal(false, entitlements.Owns(ProductIds.HomeRooms2), "an explicit revocation removes the grant");
        Equal(2, changes, "a revocation is announced");
        Equal(false, new CachedEntitlements(cache, () => now).Owns(ProductIds.HomeRooms2), "a revocation is cached");
        Equal(false, entitlements.Revoke(ProductIds.HomeRooms2), "revoking twice changes nothing");

        entitlements.Grant(ProductIds.HomeRooms2, EntitlementSource.Playtest);
        Equal(true, entitlements.Revoke(ProductIds.HomeRooms2), "the playtest toggle revokes like a refund");
        entitlements.Apply(StoreAnswer.Confirmed(new[] { ProductIds.HomeRooms2 }));
        entitlements.Grant(ProductIds.HomeRooms2, EntitlementSource.Playtest);
        Equal(EntitlementSource.Store, entitlements.Grants.Single(grant => grant.ProductId == ProductIds.HomeRooms2).Source,
            "a playtest grant never replaces a store grant");
        ExpectArgument(() => entitlements.Grant("not a product", EntitlementSource.Store), "malformed products are refused");
        ExpectArgument(() => StoreAnswer.Confirmed(new[] { "Bad Id" }), "a store answer with a malformed ID is refused");
    }

    private static void CheckParentalGate()
    {
        var picks = new Queue<int>(new[] { 3, 3, 7, 0 });
        var gate = new ParentalGate(count => picks.Count > 0 ? picks.Dequeue() : 0);
        Equal("forty-eight", gate.Prompt, "the gate shows a number in words");
        Equal(true, gate.Check("48"), "the right digits pass");
        Equal(true, gate.Check(" 48 "), "surrounding spaces are ignored");
        foreach (var wrong in new[] { null, "", " ", "47", "49", "480", "forty-eight", "Forty-eight", "48.0", "+48", "4 8", "４８", "-48" })
            Equal(false, gate.Check(wrong), $"'{wrong}' does not pass");

        gate.Next();
        Equal(false, gate.Prompt == "forty-eight", "a new challenge never repeats the last one");
        Equal(false, gate.Check("48"), "the old answer no longer passes");
        Equal(true, gate.Check(ParentalGate.AnswerFor(gate.Prompt)), "the new answer passes");
        gate.Next();
        Equal("sixty-six", gate.Prompt, "challenges come from a fixed list");

        var answers = new HashSet<string>();
        for (var index = 0; index < ParentalGate.ChallengeCount; index++)
        {
            var fixedGate = new ParentalGate(count => index);
            var answer = ParentalGate.AnswerFor(fixedGate.Prompt);
            Equal(true, fixedGate.Check(answer), $"{fixedGate.Prompt} accepts its own answer");
            Equal(true, answers.Add(answer), $"{fixedGate.Prompt} has a unique answer");
            Equal(false, fixedGate.Prompt.Any(char.IsDigit), $"{fixedGate.Prompt} gives no digits away");
            Equal(true, int.Parse(answer) > 20, $"{fixedGate.Prompt} is not a trivial number");
        }

        Equal<string>(null, ParentalGate.AnswerFor("eleventy"), "unknown prompts have no answer");
        ExpectInvalidOperation(() => new ParentalGate(count => count), "a bad random pick is refused");
        var random = new Random(7);
        var varied = new HashSet<string>();
        var drawn = new ParentalGate(random.Next);
        for (var draw = 0; draw < 40; draw++)
        {
            varied.Add(drawn.Prompt);
            drawn.Next();
        }
        Equal(true, varied.Count > 5, "the gate varies its challenge");
    }

    private static void CheckPlaytestFlagsAndStoreFront()
    {
        Equal("home.rooms2,helper.set", string.Join(",", PlaytestFlags.Parse("https://x.test/nursery/?ent=home.rooms2,helper.set#top")),
            "URL flags list playtest products");
        Equal("home.rooms2", string.Join(",", PlaytestFlags.Parse("/nursery/?a=1&ent=home.rooms2%2Cbogus,Home.Rooms2,home.rooms2")),
            "unknown, malformed and repeated flags are ignored");
        Equal(0, PlaytestFlags.Parse("/nursery/").Count, "no query means no flags");
        Equal(0, PlaytestFlags.Parse(null).Count, "no URL means no flags");

        var store = new OfflineStoreFront();
        Equal(false, store.SellsProducts, "this build sells nothing");
        RestoreOutcome? outcome = null;
        store.RestorePurchases(result => outcome = result);
        Equal<RestoreOutcome?>(RestoreOutcome.UpToDate, outcome, "restore succeeds without a store");
    }

    private static void ExpectInvalidOperation(Action attempt, string description) => Expect<InvalidOperationException>(attempt, description);
    private static void ExpectArgument(Action attempt, string description) => Expect<ArgumentException>(attempt, description);
    private static void ExpectIo(Action attempt, string description) => Expect<IOException>(attempt, description);

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
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{description}: expected {expected}, got {actual}");
    }
}

// A cached-entitlement stand-in whose ownership the checks change directly.
sealed class FakeEntitlements : IEntitlements
{
    private readonly List<string> owned;

    public FakeEntitlements(params string[] products)
    {
        owned = new List<string>(products);
    }

    public event Action Changed;
    public IReadOnlyList<string> Owned => owned.AsReadOnly();
    public bool OwnershipUnknown { get; set; }
    public bool Owns(string productId) => owned.Contains(productId);

    public void Grant(string productId)
    {
        owned.Add(productId);
        Changed?.Invoke();
    }

    public void Revoke(string productId)
    {
        owned.Remove(productId);
        Changed?.Invoke();
    }
}

sealed class MemoryEntitlementCache : IEntitlementCache
{
    public string Primary { get; set; }
    public string Backup { get; set; }
    public int Writes { get; private set; }
    public bool FailWrites { get; set; }

    public string Read() => Primary;
    public string ReadBackup() => Backup;

    public void Write(string payload, string previousValidPayload)
    {
        if (FailWrites)
            throw new IOException("Simulated cache write failure.");

        if (previousValidPayload != null)
            Backup = previousValidPayload;
        Primary = payload;
        Writes++;
    }
}
