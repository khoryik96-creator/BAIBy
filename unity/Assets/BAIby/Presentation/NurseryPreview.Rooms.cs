using System;
using System.Collections.Generic;
using BAIby.Application;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Household rooms: More rooms, household settings with Restore Purchases, and resting companions.
    // These open only from Home, behind the parental gate where money or a store is involved, and never
    // from a care flow. The copy stays calm: nothing suggests a companion suffers without a purchase.
    public sealed partial class NurseryPreview
    {
        private IEntitlements entitlements;
        private IStoreFront storeFront = new OfflineStoreFront();
        private IPlaytestUnlocks playtest;
        private MoreRoomsPanel moreRooms;
        private SettingsPanel settings;
        private JournalPanel journal;
        private WakePanel wakePanel;
        private int wakeSlot;

        // Any playtest grant, from the toggle or a ?ent= URL flag, shows the badge.
        private bool PlaytestOn => playtest != null && playtest.HasGrants;

        // Developer mode: every product granted and every earned breed open (Web, Editor and Development builds).
        private bool DeveloperModeOn => playtest != null && playtest.DeveloperMode;

        private void BuildRoomPanels()
        {
            gate = new GatePanel(Tray("Parental Gate Panel", false, 420), CancelGate, SubmitGate);
            moreRooms = new MoreRoomsPanel(Tray("More Rooms Panel", false, 400), ShowHome, ToggleTestRooms);
            settings = new SettingsPanel(Tray("Household Settings Panel", false, 480), ShowHome, RestorePurchases, ShowMoreRooms,
                ToggleDeveloperMode, ShowHelpersPanel);
            helpersPanel = new HelpersPanel(Tray("Helpers Panel", false, 500), ShowSettings, ToggleHelper, ToggleTestHelpers);
            journal = new JournalPanel(Tray("Journal Panel", false, JournalPanel.PetHeight), CloseJournal, nursery.PhotoSprite);
            wakePanel = new WakePanel(Tray("Wake Panel", false, 360), ShowHome, ConfirmWake);
        }

        private void ApplyRoomPanels()
        {
            gate.Root.SetActive(view == HouseholdView.Gate);
            moreRooms.Root.SetActive(view == HouseholdView.MoreRooms);
            settings.Root.SetActive(view == HouseholdView.Settings);
            helpersPanel.Root.SetActive(view == HouseholdView.Helpers);
            journal.Root.SetActive(view == HouseholdView.Journal);
            wakePanel.Root.SetActive(view == HouseholdView.Wake);
        }

        // The feedback line of the household panel on screen, or null on any other view.
        private Text HouseholdFeedback()
        {
            switch (view)
            {
                case HouseholdView.Gate: return gate.Feedback;
                case HouseholdView.MoreRooms: return moreRooms.Feedback;
                case HouseholdView.Settings: return settings.Feedback;
                case HouseholdView.Helpers: return helpersPanel.Feedback;
                case HouseholdView.Journal: return journal.Feedback;
                case HouseholdView.Wake: return wakePanel.Feedback;
                case HouseholdView.Choose: return choice.Feedback;
                case HouseholdView.PetAdoption: return petAdoption.Feedback;
                // A shared sheet open in the pet's room shows its own message.
                case HouseholdView.PetRoom: return petPanel == PetPanel.Care ? petStatus : null;
                default: return null;
            }
        }

        private void OnDestroy()
        {
            if (entitlements != null)
                entitlements.Changed -= OnEntitlementsChanged;
        }

        // A grant, refund or restore changes capacity. Home is rebuilt; a room whose companion now rests
        // closes back to Home. Woken companions get their one capped advance when Home is left, as any
        // companion does.
        private void OnEntitlementsChanged()
        {
            if (household == null)
                return;

            switch (view)
            {
                case HouseholdView.Home:
                    ShowHome();
                    break;
                case HouseholdView.Nursery:
                    if (household.Active?.Session != session)
                        ShowHome();
                    else
                        UpdatePanelVisibility(session?.State != null);
                    break;
                case HouseholdView.PetRoom:
                    if (household.Active == null || household.Active.Slot != petSlot || household.Active.PetSession == null)
                        ShowHome();
                    else
                        UpdateHouseholdChips(true);
                    break;
                case HouseholdView.PetAdoption:
                    // A grant or refund can lock or unlock a premium breed in the picker.
                    RefreshPetAdoption();
                    break;
                case HouseholdView.Wake:
                    if (!household.IsResting(wakeSlot))
                        ShowHome();
                    break;
                case HouseholdView.MoreRooms:
                    moreRooms.Show(playtest, household);
                    break;
                case HouseholdView.Settings:
                    settings.Show(household, PlaytestOn, playtest);
                    break;
                case HouseholdView.Helpers:
                    helpersPanel.Show(household, playtest, HelperCopyFor(household.Active?.Name));
                    break;
            }
        }

        private void ShowMoreRooms()
        {
            if (household == null)
                return;

            view = HouseholdView.MoreRooms;
            ApplyHouseholdView();
            moreRooms.Show(playtest, household);
            moreRooms.Close.Select();
        }

        private void ShowSettings()
        {
            if (household == null)
                return;

            view = HouseholdView.Settings;
            ApplyHouseholdView();
            settings.Show(household, PlaytestOn, playtest);
            settings.Close.Select();
        }

        private void ToggleTestRooms()
        {
            if (playtest == null)
                return;

            var adding = !playtest.Owns(ProductIds.HomeRooms2);
            try
            {
                playtest.Set(ProductIds.HomeRooms2, adding);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The playtest rooms couldn't be changed. Please try again.");
                return;
            }

            moreRooms.Show(playtest, household);
            moreRooms.Feedback.text = adding
                ? "Two test rooms added."
                : household.HasResting
                    ? "Test rooms removed. Little ones beyond the free rooms are resting."
                    : "Test rooms removed.";
        }

        // Opens every room, breed and premium item for testing, or returns the game to what is really owned and
        // earned. Only reachable behind the parental gate, and only in builds that compile the playtest toggle.
        private void ToggleDeveloperMode()
        {
            if (playtest == null || household == null)
                return;

            var on = !playtest.DeveloperMode;
            try
            {
                household.UnlockEarnedBreeds = on;
                playtest.SetDeveloperMode(on, DeveloperUnlocks.Products(household.PetCatalog));
            }
            catch (Exception error)
            {
                household.UnlockEarnedBreeds = playtest.DeveloperMode;
                ReportFailure(error, "Developer mode couldn't be changed. Please try again.");
                return;
            }

            settings.Show(household, PlaytestOn, playtest);
            settings.Feedback.text = on
                ? "Developer mode is on: every room, breed and premium item is open."
                : household.HasResting
                    ? "Developer mode is off. Little ones beyond the free rooms are resting."
                    : "Developer mode is off. The game is back to what you own and have earned.";
        }

        private void RestorePurchases()
        {
            settings.Feedback.text = "Checking…";
            try
            {
                storeFront.RestorePurchases(outcome =>
                {
                    switch (outcome)
                    {
                        case RestoreOutcome.Restored:
                            settings.Feedback.text = "Your purchases are restored.";
                            break;
                        case RestoreOutcome.Unavailable:
                            settings.Feedback.text = "The store couldn't be reached. Nothing you own has changed.";
                            break;
                        default:
                            settings.Feedback.text = "Your purchases are up to date.";
                            break;
                    }
                });
            }
            catch (Exception error)
            {
                ReportFailure(error, "The store couldn't be reached. Nothing you own has changed.");
            }
        }

        // A resting companion's journal can always be read; nothing in it can be changed.
        private void OpenJournal(int slot)
        {
            var companion = FindCompanion(slot);
            if (companion == null || !companion.IsReadable)
                return;

            view = HouseholdView.Journal;
            ApplyHouseholdView();
            if (companion.PetState != null && companion.PetSession.HasCare)
                journal.ShowPetPages(companion.PetState, companion.PetSession.Journal, companion.PetSession.Bonding,
                    household.IsResting(slot), entry => PetPhoto(companion.PetState, entry));
            else if (companion.PetState != null)
                journal.ShowPet(companion.PetState, household.IsResting(slot));
            else
                journal.ShowBaby(companion.State, companion.Session.Journal, companion.Session.Bonding,
                    household.IsResting(slot));
            journal.Close.Select();
        }

        private void AskWake(int slot)
        {
            var companion = FindCompanion(slot);
            if (companion == null || !companion.IsReadable || !household.IsResting(slot))
                return;

            var awake = new List<HouseholdCompanion>();
            foreach (var other in household.Companions)
            {
                if (!household.IsResting(other.Slot))
                    awake.Add(other);
            }

            wakeSlot = slot;
            view = HouseholdView.Wake;
            ApplyHouseholdView();
            wakePanel.Ask(companion.Name, household.Capacity, awake).Select();
        }

        private void ConfirmWake(int restSlot)
        {
            var waking = FindCompanion(wakeSlot);
            var resting = FindCompanion(restSlot);
            if (waking == null || !waking.IsReadable || resting == null)
                return;

            try
            {
                household.SwapResting(wakeSlot, restSlot);
            }
            catch (Exception error)
            {
                if (household.IsResting(wakeSlot))
                {
                    ReportFailure(error, "That change couldn't be saved. Please try again.");
                    return;
                }

                // The swap is saved; only the woken companion's advance failed, and it is retried.
                ShowHome();
                ReportFailure(error, "Your progress couldn't be saved. Please try again.");
                return;
            }

            var name = waking.Name;
            var other = resting.Name;
            ShowHome();
            home.Feedback.text = other == null
                ? $"{name} is back in a room."
                : $"{name} is back in a room, and {other} is resting.";
        }

        private sealed class MoreRoomsPanel
        {
            private readonly Text heading;
            private readonly Text detail;
            private readonly Button toggle;
            private readonly Text toggleLabel;

            public GameObject Root { get; }
            public Button Close { get; }
            public Text Feedback { get; }

            public MoreRoomsPanel(RectTransform content, Action close, Action toggleRooms)
            {
                Root = content.parent.gameObject;
                heading = Label("More Rooms Heading", content, "More rooms", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                PlaceFromTop(heading.rectTransform, 0, 0.72f, 14, 76);
                Close = ActionButton("Close more rooms", content, 0.73f, 1, 0, 0, close, Mist, Ink);
                Close.GetComponentInChildren<Text>().text = "Done";
                PlaceFromTop((RectTransform)Close.transform, 0.73f, 1, 16, 76, 3, 3);
                SoftShapes.Round(Close.image, 1f);

                detail = Label("More Rooms Detail", content, "", 23, Ink, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 16;
                detail.resizeTextMaxSize = 23;
                PlaceFromTop(detail.rectTransform, 0, 1, 88, 206);

                toggle = ActionButton("Playtest rooms toggle", content, 0, 1, 0, 0, toggleRooms, Fern, Color.white);
                toggleLabel = toggle.GetComponentInChildren<Text>();
                PlaceFromTop((RectTransform)toggle.transform, 0, 1, 218, 300, 3, 3);
                SoftShapes.Round(toggle.image, 1f);
                SoftShapes.Lift(toggle.gameObject);

                Feedback = Label("More Rooms Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 15;
                Feedback.resizeTextMaxSize = 21;
                PlaceFromTop(Feedback.rectTransform, 0, 1, 312, 384);
                Root.SetActive(false);
            }

            // Devices without a store say the rooms are coming; Web, Editor and Development builds offer the
            // playtest toggle instead. Nothing here is a prompt to buy.
            public void Show(IPlaytestUnlocks playtest, HouseholdSession household)
            {
                Feedback.text = string.Empty;
                toggle.gameObject.SetActive(playtest != null);
                if (playtest == null)
                {
                    heading.text = "More rooms";
                    detail.text = "More rooms for a bigger family are coming soon to the app stores. " +
                        "Your home is just right as it is, and every little one is cared for the same.";
                    return;
                }

                var owned = playtest.Owns(ProductIds.HomeRooms2);
                heading.text = "Playtest unlocks";
                detail.text = $"This test build has no shop, and nothing is bought. Your home has {household.Capacity} rooms. " +
                    "Add or remove the two extra rooms to try the household and resting flow.";
                toggleLabel.text = owned ? "Remove test rooms" : "Add 2 test rooms";
            }
        }

        private sealed class SettingsPanel
        {
            private readonly Text detail;

            public GameObject Root { get; }
            public Button Close { get; }
            public Text Feedback { get; }

            private readonly Button developer;
            private readonly Text developerLabel;

            public SettingsPanel(RectTransform content, Action close, Action restore, Action moreRooms, Action toggleDeveloper,
                Action helpers)
            {
                Root = content.parent.gameObject;
                var heading = Label("Settings Heading", content, "For grown-ups", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                PlaceFromTop(heading.rectTransform, 0, 0.72f, 14, 76);
                Close = ActionButton("Close settings", content, 0.73f, 1, 0, 0, close, Mist, Ink);
                Close.GetComponentInChildren<Text>().text = "Done";
                PlaceFromTop((RectTransform)Close.transform, 0.73f, 1, 16, 76, 3, 3);
                SoftShapes.Round(Close.image, 1f);

                detail = Label("Settings Detail", content, "", 23, Ink, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 16;
                detail.resizeTextMaxSize = 23;
                PlaceFromTop(detail.rectTransform, 0, 1, 88, 200);

                var restoreButton = ActionButton("Restore purchases", content, 0, 0.38f, 0, 0, restore, Fern, Color.white);
                PlaceFromTop((RectTransform)restoreButton.transform, 0, 0.38f, 212, 296, 3, 3);
                var roomsButton = ActionButton("Settings more rooms", content, 0.38f, 0.69f, 0, 0, moreRooms, Mist, Ink);
                roomsButton.GetComponentInChildren<Text>().text = "More rooms";
                PlaceFromTop((RectTransform)roomsButton.transform, 0.38f, 0.69f, 212, 296, 3, 3);
                // Switch each owned helper on or off for the household; nothing here sells them.
                var helpersButton = ActionButton("Settings helpers", content, 0.69f, 1, 0, 0, helpers, Mist, Ink);
                helpersButton.GetComponentInChildren<Text>().text = "Helpers";
                PlaceFromTop((RectTransform)helpersButton.transform, 0.69f, 1, 212, 296, 3, 3);
                foreach (var button in new[] { restoreButton, roomsButton, helpersButton })
                {
                    var label = button.GetComponentInChildren<Text>();
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 16;
                    label.resizeTextMaxSize = 25;
                    SoftShapes.Round(button.image, 1f);
                    SoftShapes.Lift(button.gameObject);
                }

                // Developer mode: test builds only (the button stays hidden when there is no playtest toggle).
                developer = ActionButton("Developer mode", content, 0, 1, 0, 0, toggleDeveloper, Paper, Ink);
                developerLabel = developer.GetComponentInChildren<Text>();
                developerLabel.resizeTextForBestFit = true;
                developerLabel.resizeTextMinSize = 16;
                developerLabel.resizeTextMaxSize = 24;
                PlaceFromTop((RectTransform)developer.transform, 0, 1, 306, 380, 3, 3);
                SoftShapes.Round(developer.image, 1f);
                developer.gameObject.SetActive(false);

                Feedback = Label("Settings Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 15;
                Feedback.resizeTextMaxSize = 21;
                PlaceFromTop(Feedback.rectTransform, 0, 1, 388, 460);
                Root.SetActive(false);
            }

            public void Show(HouseholdSession household, bool playtestOn, IPlaytestUnlocks playtest)
            {
                developer.gameObject.SetActive(playtest != null);
                if (playtest != null)
                    developerLabel.text = playtest.DeveloperMode
                        ? "Developer mode: on · tap to turn off"
                        : "Developer mode: off · unlock everything";
                Feedback.text = string.Empty;
                var rooms = $"Your home has {household.Capacity} rooms.";
                if (household.HasResting)
                    rooms += " Little ones beyond them are resting and safe, and their needs don't drop.";
                if (playtestOn)
                    rooms += " Playtest unlocks are on.";
                detail.text = rooms + " Restore Purchases brings back anything bought on this account.";
            }
        }

        private sealed partial class JournalPanel
        {
            private readonly Text heading;
            private readonly Text state;
            private readonly Text age;
            private readonly Text sitting;
            private readonly Text crawling;
            private readonly Text walking;
            private readonly Text favoriteToy;
            private readonly Text stories;
            private readonly Text blocks;
            private readonly Text connection;

            public GameObject Root { get; }
            public Button Close { get; }
            public Text Feedback { get; }

            public JournalPanel(RectTransform content, Action close, Func<BabyAppearance, int, bool, Sprite> photoSprite)
            {
                Root = content.parent.gameObject;
                this.photoSprite = photoSprite;
                heading = Line("Journal Heading", content, 38, 0.72f, 6, 66, FontStyle.Bold);
                Close = ActionButton("Close journal", content, 0.73f, 1, 0, 0, close, Mist, Ink);
                Close.GetComponentInChildren<Text>().text = "Done";
                PlaceFromTop((RectTransform)Close.transform, 0.73f, 1, 10, 66, 3, 3);
                SoftShapes.Round(Close.image, 1f);

                state = Line("Journal Status", content, 21, 1, 70, 100);
                state.color = Secondary;
                // A pet's journal is a page of lines; a baby's has tabs and pages (NurseryPreview.Journal.cs).
                var lines = Box("Journal Pet Lines", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                petLines = lines.gameObject;
                age = Line("Journal Age", lines, 23, 1, 112, 146);
                Line("Journal Milestones", lines, 30, 1, 150, 186, FontStyle.Bold).text = "Milestones";
                sitting = Line("Journal Sitting", lines, 22, 1, 188, 218);
                crawling = Line("Journal Crawling", lines, 22, 1, 218, 248);
                walking = Line("Journal Walking", lines, 22, 1, 248, 278);
                Line("Journal Interests", lines, 30, 1, 282, 318, FontStyle.Bold).text = "Interests";
                favoriteToy = Line("Journal Favorite Toy", lines, 22, 1, 320, 350);
                stories = Line("Journal Stories", lines, 22, 1, 350, 380);
                blocks = Line("Journal Blocks", lines, 22, 1, 380, 410);
                connection = Line("Journal Bond", lines, 23, 1, 414, 448, FontStyle.Bold);
                connection.color = FernDeep;
                Feedback = Line("Journal Feedback", content, 19, 1, 448, 468);
                BuildPages(content);
                Root.SetActive(false);
            }

            private static Text Line(string name, Transform content, int size, float right, float top, float bottom,
                FontStyle weight = FontStyle.Normal)
            {
                var text = Label(name, content, "", size, Ink, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                    Vector2.zero, weight);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Math.Max(14, size - 8);
                text.resizeTextMaxSize = size;
                PlaceFromTop(text.rectTransform, 0, right, top, bottom);
                return text;
            }
        }

        private sealed class WakePanel
        {
            private const float RowHeight = 92;
            private const float ChoicesTop = 168;

            private readonly RectTransform tray;
            private readonly RectTransform choices;
            private readonly Text heading;
            private readonly Text detail;
            private readonly Button keepResting;
            private readonly Action<int> choose;

            public GameObject Root { get; }
            public Text Feedback { get; }

            public WakePanel(RectTransform content, Action cancel, Action<int> choose)
            {
                this.choose = choose;
                tray = (RectTransform)content.parent;
                Root = tray.gameObject;
                heading = Label("Wake Heading", content, "", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                heading.resizeTextForBestFit = true;
                heading.resizeTextMinSize = 24;
                heading.resizeTextMaxSize = 38;
                PlaceFromTop(heading.rectTransform, 0, 1, 12, 66);
                detail = Label("Wake Detail", content, "", 22, Ink, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 15;
                detail.resizeTextMaxSize = 22;
                PlaceFromTop(detail.rectTransform, 0, 1, 70, 160);
                choices = Box("Wake Choices", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                keepResting = ActionButton("Keep resting", content, 0, 1, 0, 0, cancel, Mist, Ink);
                keepResting.GetComponentInChildren<Text>().text = "Not now";
                SoftShapes.Round(keepResting.image, 1f);

                Feedback = Label("Wake Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 15;
                Feedback.resizeTextMaxSize = 21;
                Root.SetActive(false);
            }

            // Lists the awake companions, two to a row, so the player chooses who rests. Returns the button
            // to focus.
            public Button Ask(string name, int capacity, IReadOnlyList<HouseholdCompanion> awake)
            {
                for (var i = choices.childCount - 1; i >= 0; i--)
                {
                    var child = choices.GetChild(i);
                    child.gameObject.SetActive(false);
                    child.SetParent(null, false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }

                Feedback.text = string.Empty;
                heading.text = $"Wake {name}?";
                detail.text = $"Your home has {capacity} rooms. Who would like a rest instead? " +
                    "Resting little ones are safe, and their needs don't drop while they rest.";

                for (var i = 0; i < awake.Count; i++)
                {
                    var companion = awake[i];
                    var column = i % 2;
                    var top = ChoicesTop + i / 2 * RowHeight;
                    var label = companion.Name;
                    var slot = companion.Slot;
                    var button = ActionButton(label == null ? $"Rest slot {slot} instead" : $"Rest {label} instead",
                        choices, 0, 1, 0, 0, () => choose(slot), Lilac, Ink);
                    var text = button.GetComponentInChildren<Text>();
                    text.text = label == null ? "The unopened save rests" : $"{label} rests";
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 15;
                    text.resizeTextMaxSize = 24;
                    PlaceFromTop((RectTransform)button.transform, column * 0.5f, (column + 1) * 0.5f, top, top + RowHeight - 8, 3, 3);
                    SoftShapes.Radius(button.image, Px(20));
                }

                SettleText(choices);
                var rows = (awake.Count + 1) / 2;
                var y = ChoicesTop + rows * RowHeight;
                PlaceFromTop((RectTransform)keepResting.transform, 0, 1, y, y + 76, 3, 3);
                y += 84;
                PlaceFromTop(Feedback.rectTransform, 0, 1, y, y + 44);
                y += 52;
                tray.offsetMax = new Vector2(tray.offsetMax.x, tray.offsetMin.y + y + SheetChrome);
                return keepResting;
            }
        }
    }
}
