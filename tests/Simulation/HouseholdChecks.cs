using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BAIby.Application;
using BAIby.Simulation;

static class HouseholdChecks
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Run()
    {
        var game = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4), TimeSpan.FromDays(7), TimeSpan.FromDays(12));
        CheckRules();
        CheckManifestCodec();
        CheckLegacySavesLoadAsSlotOne(game);
        CheckAdoptionCapacityAndSwitching(game);
        CheckGoodbye(game);
        CheckManifestRecovery(game);
        CheckUnreadableSlots(game);
        CheckInterruptedWrites(game);
        CheckTimeAndCelebrations(game);
        Console.WriteLine("Household checks passed.");
    }

    private static void CheckRules()
    {
        Equal(2, HouseholdRules.FreeCompanions, "two companions are free");
        Equal(8, HouseholdRules.PhysicalSlots, "eight physical slots exist");
        Equal(0, (int)CompanionMilestone.Sitting, "sitting celebration ID is stable");
        Equal(1, (int)CompanionMilestone.Crawling, "crawling celebration ID is stable");
        Equal(2, (int)CompanionMilestone.Walking, "walking celebration ID is stable");
        Equal(0, (int)BabyStage.Newborn, "newborn stage ID is stable");
        Equal(3, (int)BabyStage.Walker, "walker stage ID is stable");
    }

    private static void CheckManifestCodec()
    {
        const string golden = "<household version=\"1\" active=\"2\"><companions>" +
            "<companion slot=\"1\" adopted=\"639028224000000000\" />" +
            "<companion slot=\"2\" adopted=\"639028224000000000\" leaving=\"true\" /></companions>" +
            "<celebrations><celebration slot=\"1\" milestone=\"1\" /></celebrations>" +
            "<keepsakes><keepsake name=\"Ada &amp; Co\" adopted=\"639028224000000000\" farewell=\"639029088000000000\" " +
            "age=\"864000000000\" stage=\"2\" skin=\"4\" hair-color=\"1\" hair-style=\"3\" outfit=\"7\" headwear=\"2\" />" +
            "</keepsakes></household>";
        var manifest = HouseholdManifestCodec.Decode(golden);
        Equal(2, manifest.ActiveSlot, "golden manifest keeps the active slot");
        Equal("1,2", string.Join(",", manifest.Companions.Select(entry => entry.Slot)), "golden manifest keeps order");
        Equal(true, manifest.Companions[1].Leaving, "golden manifest keeps an interrupted goodbye");
        Equal(Start, manifest.Companions[0].AdoptedAt, "golden manifest keeps the adoption date");
        Equal(CompanionMilestone.Crawling, manifest.Celebrations[0].Milestone, "golden manifest keeps celebrations");
        var keepsake = manifest.Keepsakes[0];
        Equal("Ada & Co", keepsake.Name, "keepsake name survives escaping");
        Equal(Start.AddDays(1), keepsake.FarewellAt, "keepsake keeps the farewell date");
        Equal(TimeSpan.FromDays(1), keepsake.Age, "keepsake keeps the age");
        Equal(BabyStage.Crawler, keepsake.Stage, "keepsake keeps the stage");
        Equal(new BabyAppearance(SkinTone.Brown, HairColor.Black, HairStyle.Tuft, Outfit.Midnight, Headwear.BearBeanie),
            keepsake.Appearance, "keepsake keeps the look");
        Equal(golden, HouseholdManifestCodec.Encode(manifest), "manifest encoding is stable");

        ExpectInvalidManifest("<household", "malformed manifest is rejected");
        ExpectInvalidManifest("<baby version=\"1\" />", "a baby record is not a manifest");
        ExpectInvalidManifest(golden.Replace("slot=\"2\"", "slot=\"1\""), "duplicate slots are rejected");
        ExpectInvalidManifest(golden.Replace("slot=\"2\" adopted", "slot=\"9\" adopted"), "slots beyond the physical range are rejected");
        ExpectInvalidManifest(golden.Replace("milestone=\"1\"", "milestone=\"7\""), "undefined milestones are rejected");
        ExpectInvalidManifest(golden.Replace("stage=\"2\"", "stage=\"9\""), "undefined stages are rejected");
        ExpectInvalidManifest(golden.Replace("skin=\"4\"", "skin=\"99\""), "undefined looks are rejected");
        ExpectInvalidManifest(golden.Replace("active=\"2\"", "active=\"5\""), "an unlisted active slot is rejected");
        ExpectInvalidManifest(golden.Replace("<celebrations>", "<celebrations><celebration slot=\"6\" milestone=\"0\" />"),
            "celebrations for unlisted slots are rejected");
        try
        {
            HouseholdManifestCodec.Decode("<household version=\"2\" active=\"0\" />");
            throw new InvalidOperationException("A future manifest version should be unsupported.");
        }
        catch (NotSupportedException)
        {
        }
    }

    private static void CheckLegacySavesLoadAsSlotOne(BabySimulation game)
    {
        for (var version = 1; version <= 11; version++)
        {
            var payload = LegacySave(version);
            var store = new MemoryHouseholdStore();
            store.Slot1.Primary = payload;
            var household = new HouseholdSession(game, store, () => Start);
            household.AdvanceAll();
            Equal(1, household.Companions.Count, $"v{version} save appears in the household");
            Equal(1, household.Active.Slot, $"v{version} save is slot 1");
            Equal("Sam", household.Active.State.Name, $"v{version} save keeps its baby");
            Equal(payload, store.Slot1.Primary, $"v{version} record is untouched");
            Equal<string>(null, store.Slot1.Backup, $"v{version} record gets no backup write");
            Equal(0, store.RecordWrites, $"v{version} migration makes zero record writes");
            Equal(1, store.ManifestWrites, $"v{version} migration only writes a new manifest");

            var reopened = new HouseholdSession(game, store, () => Start);
            reopened.AdvanceAll();
            Equal(0, store.RecordWrites, $"v{version} reopen still makes no record writes");
            Equal(1, store.ManifestWrites, $"v{version} unchanged manifest is not rewritten");
        }
    }

    private static void CheckAdoptionCapacityAndSwitching(BabySimulation game)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, store, () => now);
        Equal<HouseholdCompanion>(null, household.Active, "an empty household has no active companion");
        Equal(true, household.CanAdopt, "an empty household can adopt");
        Equal(0, store.ManifestWrites, "an empty household writes nothing");
        ExpectArgument(() => household.Adopt("  "), "adoption needs a name");

        var mia = household.Adopt("Mia");
        Equal(1, mia.Slot, "the first baby takes slot 1, the original save key");
        Equal(true, store.Slot1.Primary.Contains("<name>Mia</name>"), "the first baby is written to slot 1");
        Equal(1, household.Active.Slot, "a new baby becomes active");

        now = now.AddMinutes(5);
        var leo = household.Adopt("Leo");
        Equal(2, leo.Slot, "the second baby takes the next free slot");
        Equal(2, household.Active.Slot, "the second baby becomes active");
        Equal(true, household.IsFull, "two companions fill the free household");
        Equal(false, household.CanAdopt, "a full household cannot adopt");
        ExpectInvalidOperation(() => household.Adopt("Ivy"), "capacity 2 is enforced");
        Equal<string>(null, store.SlotStore(3).Primary, "a refused adoption writes no record");

        leo.Session.ApplyCare(CareAction.Play);
        household.Switch(1);
        Equal(1, household.Active.Slot, "switch changes the active companion");
        Equal(0, mia.Session.State.ToyFamiliarity, "care on one baby leaves the other alone");

        var reopened = new HouseholdSession(game, store, () => now);
        Equal(1, reopened.Active.Slot, "the active slot persists");
        Equal("Mia,Leo", string.Join(",", reopened.Companions.Select(companion => companion.State.Name)), "order persists");
        Equal(1, reopened.Companions[1].State.ToyFamiliarity, "each slot restores its own baby");
        reopened.Switch(2);
        Equal(2, new HouseholdSession(game, store, () => now).Active.Slot, "a second switch persists");
        ExpectArgument(() => reopened.Switch(5), "switching to an empty slot is refused");
    }

    private static void CheckGoodbye(BabySimulation game)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, store, () => now);
        household.Adopt("Mia");
        var leo = household.Adopt("Leo");
        leo.Session.SetAppearance(BabyAppearance.Default.WithOutfit(Outfit.Sky));
        now = now.AddDays(4);
        household.AdvanceAll();

        household.SayGoodbye(2);
        Equal<string>(null, store.SlotStore(2).Primary, "goodbye deletes the record");
        Equal<string>(null, store.SlotStore(2).Backup, "goodbye deletes the backup");
        Equal(1, household.Companions.Count, "goodbye frees the slot");
        Equal(1, household.Active.Slot, "the remaining companion becomes active");
        Equal(true, household.CanAdopt, "a freed slot can be adopted again");
        var keepsake = household.Keepsakes.Single();
        Equal("Leo", keepsake.Name, "the keepsake keeps the name");
        Equal(Start, keepsake.AdoptedAt, "the keepsake keeps the adoption date");
        Equal(now, keepsake.FarewellAt, "the keepsake keeps the farewell date");
        Equal(BabyStage.Sitter, keepsake.Stage, "the keepsake keeps the stage");
        Equal(Outfit.Sky, keepsake.Appearance.Outfit, "the keepsake keeps the look");

        var reopened = new HouseholdSession(game, store, () => now);
        Equal(1, reopened.Companions.Count, "goodbye persists");
        Equal("Leo", reopened.Keepsakes.Single().Name, "the keepsake persists");
        var ivy = reopened.Adopt("Ivy");
        Equal(2, ivy.Slot, "the freed slot is reused");
        Equal(1, reopened.Keepsakes.Count, "a new adoption keeps earlier keepsakes");

        reopened.SayGoodbye(1);
        reopened.SayGoodbye(2);
        Equal<HouseholdCompanion>(null, reopened.Active, "a household can say goodbye to everyone");
        Equal("Leo,Mia,Ivy", string.Join(",", reopened.Keepsakes.Select(kept => kept.Name)), "keepsakes accumulate in order");
        Equal<string>(null, store.Slot1.Primary, "goodbye clears the original key too");
        ExpectArgument(() => reopened.SayGoodbye(1), "goodbye needs a companion");
    }

    private static void CheckManifestRecovery(BabySimulation game)
    {
        var store = TwoBabies(game, out _);
        store.ManifestPrimary = "<household";
        store.ManifestBackup = null;
        var rebuilt = new HouseholdSession(game, store, () => Start);
        Equal("1,2", string.Join(",", rebuilt.Companions.Select(companion => companion.Slot)), "a corrupt manifest is rebuilt from slots");
        Equal(1, rebuilt.Active.Slot, "a rebuilt manifest activates the first companion");
        Equal(true, store.ManifestPrimary.StartsWith("<household version=\"1\""), "the rebuilt manifest is written");

        store.ManifestPrimary = null;
        store.ManifestBackup = null;
        Equal(2, new HouseholdSession(game, store, () => Start).Companions.Count, "a missing manifest is rebuilt from slots");

        var fromBackup = TwoBabies(game, out var household);
        household.Switch(1);
        household.Switch(2);
        fromBackup.ManifestPrimary = "<household version=\"1\" active=\"2\"><companions /></household>";
        Equal(2, new HouseholdSession(game, fromBackup, () => Start).Companions.Count,
            "a manifest missing sections falls back to its backup and the slots");

        var future = TwoBabies(game, out _);
        future.ManifestPrimary = "<household version=\"9\" />";
        future.ManifestBackup = null;
        Equal(2, new HouseholdSession(game, future, () => Start).Companions.Count, "an unsupported manifest is rebuilt from slots");
    }

    private static void CheckUnreadableSlots(BabySimulation game)
    {
        var store = new MemoryHouseholdStore();
        store.SlotStore(2).Primary = "<baby";
        var household = new HouseholdSession(game, store, () => Start);
        Equal(1, household.Companions.Count, "an unreadable slot is listed");
        Equal(false, household.Companions[0].IsReadable, "an unreadable slot has no session");
        Equal<HouseholdCompanion>(null, household.Active, "an unreadable slot is never active");
        var mia = household.Adopt("Mia");
        Equal(1, mia.Slot, "adoption skips the unreadable slot");
        Equal("<baby", store.SlotStore(2).Primary, "unreadable bytes are kept");
        Equal(true, household.IsFull, "an unreadable slot counts toward capacity");
        ExpectInvalidOperation(() => household.Adopt("Leo"), "an unreadable slot is never treated as free");
        ExpectInvalidOperation(() => household.Switch(2), "an unreadable slot cannot be opened");
        ExpectInvalidOperation(() => household.SayGoodbye(2), "an unreadable slot is not deleted by goodbye");
        Equal("<baby", store.SlotStore(2).Primary, "refused actions keep unreadable bytes");

        var reopened = new HouseholdSession(game, store, () => Start);
        Equal("2,1", string.Join(",", reopened.Companions.Select(companion => companion.Slot)), "an unreadable slot stays listed in order");

        var newer = new MemoryHouseholdStore();
        newer.Slot1.Primary = "<baby version=\"99\" />";
        var future = new HouseholdSession(game, newer, () => Start);
        Equal(false, future.Companions.Single().IsReadable, "a record from a newer build is kept unreadable");
        Equal(2, future.Adopt("Ada").Slot, "a newer record is not overwritten");
        Equal("<baby version=\"99\" />", newer.Slot1.Primary, "a newer record keeps its bytes");

        var backupOnly = new MemoryHouseholdStore();
        backupOnly.Slot1.Backup = LegacySave(11);
        var restored = new HouseholdSession(game, backupOnly, () => Start);
        Equal("Sam", restored.Active.State.Name, "a slot with only a backup is occupied and restored");
    }

    private static void CheckInterruptedWrites(BabySimulation game)
    {
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, store, () => Start);
        household.Adopt("Mia");
        store.FailManifestWrites = true;
        ExpectIo(() => household.Adopt("Leo"), "a failed manifest write is surfaced");
        Equal(true, store.SlotStore(2).Primary.Contains("<name>Leo</name>"), "the record was written first");
        store.FailManifestWrites = false;
        var recovered = new HouseholdSession(game, store, () => Start);
        Equal("Mia,Leo", string.Join(",", recovered.Companions.Select(companion => companion.State.Name)),
            "a kill between record and manifest writes recovers the new baby");
        Equal(true, recovered.IsFull, "the recovered baby counts toward capacity");

        store.FailDeletes = true;
        ExpectIo(() => recovered.SayGoodbye(2), "a failed delete is surfaced");
        store.FailDeletes = false;
        Equal(true, store.ManifestPrimary.Contains("leaving=\"true\""), "the keepsake is written before the record is touched");
        var finished = new HouseholdSession(game, store, () => Start);
        Equal(1, finished.Companions.Count, "an interrupted goodbye finishes on launch");
        Equal<string>(null, store.SlotStore(2).Primary, "an interrupted goodbye deletes the record on launch");
        Equal("Leo", finished.Keepsakes.Single().Name, "an interrupted goodbye keeps exactly one keepsake");
        Equal(false, store.ManifestPrimary.Contains("leaving"), "the finished goodbye is written");

        var retry = TwoBabies(game, out var retrying);
        retry.FailDeletes = true;
        ExpectIo(() => retrying.SayGoodbye(2), "a failed delete can be retried");
        retry.FailDeletes = false;
        retrying.SayGoodbye(2);
        Equal(1, retrying.Keepsakes.Count, "a retried goodbye keeps one keepsake");

        var stale = TwoBabies(game, out var staleHousehold);
        stale.FailDeletes = true;
        ExpectIo(() => staleHousehold.SayGoodbye(2), "a goodbye can stop before the delete");
        stale.FailDeletes = false;
        stale.ManifestBackup = stale.ManifestPrimary;
        stale.ManifestPrimary = "<household";
        var cautious = new HouseholdSession(game, stale, () => Start);
        Equal(2, cautious.Companions.Count, "a goodbye is never finished from a backup manifest");
        Equal(true, stale.SlotStore(2).Primary != null, "a backup manifest never deletes a record");

        var failedCare = TwoBabies(game, out _);
        failedCare.SlotStore(1).FailWrites = true;
        var later = Start.AddHours(1);
        var timed = new HouseholdSession(game, failedCare, () => later);
        ExpectIo(() => timed.AdvanceAll(), "a failed record write during advance is surfaced");
        Equal(Start.AddHours(1), timed.Companions[1].State.TimeCheckpoint, "other companions still advance");
    }

    private static void CheckTimeAndCelebrations(BabySimulation game)
    {
        var now = Start;
        var store = new MemoryHouseholdStore();
        var household = new HouseholdSession(game, store, () => now);
        household.Adopt("Mia");
        household.Adopt("Leo");
        Equal(2, household.Active.Slot, "Leo is on screen");

        now = now.AddHours(2);
        household.Switch(1);
        Equal(Start, household.Active.State.TimeCheckpoint, "a switch does not advance time");
        household.Switch(2);

        now = Start.AddDays(3);
        var shown = household.AdvanceAll();
        Equal((true, false, false), shown, "the on-screen companion reports its own unlock");
        Equal(now, household.Companions[0].State.TimeCheckpoint, "an off-screen companion advances on resume");
        Equal(now, household.Companions[1].State.TimeCheckpoint, "the active companion advances on resume");
        Equal(household.Companions[0].State.Satiety, household.Companions[1].State.Satiety,
            "decay does not depend on switching habits");
        Equal(true, household.HasCelebrations(1), "an off-screen milestone becomes a pending celebration");
        Equal(false, household.HasCelebrations(2), "the on-screen milestone is not pending");

        var writes = store.ManifestWrites;
        household.AdvanceAll();
        Equal(writes, store.ManifestWrites, "a pending celebration is recorded once");

        var reopened = new HouseholdSession(game, store, () => now);
        Equal(true, reopened.HasCelebrations(1), "a pending celebration survives reload");
        reopened.Switch(1);
        Equal("Sitting", string.Join(",", reopened.TakeCelebrations(1)), "the celebration is shown when opened");
        Equal(0, reopened.TakeCelebrations(1).Count, "the celebration is shown once");
        Equal(false, new HouseholdSession(game, store, () => now).HasCelebrations(1), "a shown celebration stays shown");

        now = Start.AddDays(7);
        var hidden = reopened.AdvanceAll(activeOnScreen: false);
        Equal((false, false, false), hidden, "nothing is reported while Home is on screen");
        Equal(true, reopened.HasCelebrations(1), "the active companion's milestone waits while Home is shown");
        Equal(true, reopened.HasCelebrations(2), "every off-screen companion keeps its milestone");
        Equal("Crawling", string.Join(",", reopened.TakeCelebrations(2)), "each companion keeps its own celebrations");

        var resting = Start.AddDays(7);
        Equal(resting, reopened.Companions[0].State.TimeCheckpoint, "home advance still commits time");
        Equal(false, HouseholdRules.NeedsAttention(game.Adopt("New", Start)), "a new baby needs no attention");
        Equal(true, HouseholdRules.NeedsAttention(reopened.Companions[0].State), "capped decay can leave a low need");
    }

    private static MemoryHouseholdStore TwoBabies(BabySimulation game, out HouseholdSession household)
    {
        var store = new MemoryHouseholdStore();
        household = new HouseholdSession(game, store, () => Start);
        household.Adopt("Mia");
        household.Adopt("Leo");
        return store;
    }

    // A legacy single-baby save at each version, as written by the build that introduced it.
    private static string LegacySave(int version)
    {
        var root = new XElement("baby", new XAttribute("version", version),
            new XElement("name", "Sam"),
            new XElement("checkpoint", Start.UtcDateTime.Ticks),
            new XElement("age", TimeSpan.FromDays(3).Ticks),
            new XElement("satiety", 41),
            new XElement("comfort", 52));
        if (version > 1)
            root.Add(new XElement("energy", 63));
        root.Add(new XElement("toy-familiarity", 2));
        if (version > 2)
            root.Add(new XElement("plush-familiarity", 1));
        root.Add(new XElement("can-sit-and-reach", true));
        if (version > 3)
            root.Add(new XElement("can-crawl", false));
        if (version > 4)
            root.Add(new XElement("connection", 12), new XElement("story-sessions", 1));
        if (version > 5)
            root.Add(new XElement("cleanliness", 70), new XElement("health", 90));
        if (version > 6)
            root.Add(new XElement("new-sound-resolved", false));
        if (version > 7)
            root.Add(new XElement("nursery-theme", 1));
        if (version > 8)
            root.Add(new XElement("can-walk", false));
        if (version > 9)
            root.Add(new XElement("block-towers", 0));
        if (version > 10)
            root.Add(new XElement("appearance", new XAttribute("skin", 3), new XAttribute("hair-color", 2),
                new XAttribute("hair-style", 1), new XAttribute("outfit", 4), new XAttribute("headwear", 1)));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static void ExpectInvalidManifest(string payload, string description)
    {
        try
        {
            HouseholdManifestCodec.Decode(payload);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException($"{description}: expected InvalidDataException");
    }

    private static void ExpectInvalidOperation(Action attempt, string description) =>
        Expect<InvalidOperationException>(attempt, description);

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

sealed class MemoryHouseholdStore : IHouseholdStore
{
    private readonly Dictionary<int, MemorySlot> slots = new Dictionary<int, MemorySlot>();

    public string ManifestPrimary { get; set; }
    public string ManifestBackup { get; set; }
    public int ManifestWrites { get; private set; }
    public bool FailManifestWrites { get; set; }
    public bool FailDeletes { get; set; }
    public MemorySlot Slot1 => SlotStore(1);
    public int RecordWrites => slots.Values.Sum(slot => slot.Writes);

    public MemorySlot SlotStore(int slot)
    {
        if (slot < 1 || slot > HouseholdRules.PhysicalSlots)
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (!slots.TryGetValue(slot, out var store))
            slots.Add(slot, store = new MemorySlot());
        return store;
    }

    public IBabySaveStore Slot(int slot) => SlotStore(slot);

    public void DeleteSlot(int slot)
    {
        if (FailDeletes)
            throw new IOException("Simulated delete failure.");

        var store = SlotStore(slot);
        store.Primary = null;
        store.Backup = null;
    }

    public string ReadManifest() => ManifestPrimary;
    public string ReadManifestBackup() => ManifestBackup;

    public void WriteManifest(string payload, string previousValidPayload)
    {
        if (FailManifestWrites)
            throw new IOException("Simulated manifest write failure.");

        if (previousValidPayload != null)
            ManifestBackup = previousValidPayload;
        ManifestPrimary = payload;
        ManifestWrites++;
    }
}

// Mirrors the PlayerPrefs store: a backup is only replaced when a previous valid payload exists.
sealed class MemorySlot : IBabySaveStore
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
            throw new IOException("Simulated write failure.");

        if (previousValidPayload != null)
            Backup = previousValidPayload;
        Primary = payload;
        Writes++;
    }
}
