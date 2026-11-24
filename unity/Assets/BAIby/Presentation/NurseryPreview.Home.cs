using System;
using System.Collections.Generic;
using System.Globalization;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private static readonly Color Amber = new Color32(201, 132, 62, 255);

        // Choose is "Who's joining your home?"; PetAdoption picks species, breed and name; PetRoom is a pet's room.
        private enum HouseholdView { Adoption, Nursery, Home, Goodbye, Gate, MoreRooms, Settings, Journal, Wake, Choose, PetAdoption, PetRoom }

        private HouseholdView view = HouseholdView.Adoption;
        private HouseholdSession household;
        private GameObject header;
        private Button homeChip;
        private Image homeDot;
        private Button switchChip;
        private Text switchLabel;
        private Button backToHome;
        private HomePanel home;
        private GoodbyePanel goodbye;
        private int goodbyeSlot;

        public void Open(HouseholdSession householdSession) => Open(householdSession, null, null, null);

        // ownedProducts reports what is owned and announces refunds and restores; store is the shop seam
        // (no store in this build); playtestUnlocks is the developer toggle, null outside Web, Editor and
        // Development builds.
        public void Open(HouseholdSession householdSession, IEntitlements ownedProducts, IStoreFront store,
            IPlaytestUnlocks playtestUnlocks)
        {
            household = householdSession ?? throw new ArgumentNullException(nameof(householdSession));
            if (entitlements != null)
                entitlements.Changed -= OnEntitlementsChanged;
            entitlements = ownedProducts;
            storeFront = store ?? new OfflineStoreFront();
            playtest = playtestUnlocks;
            if (entitlements != null)
                entitlements.Changed += OnEntitlementsChanged;
            adopt.interactable = true;
            var active = household.Active;
            (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) launch = default;
            Exception failure = null;
            try
            {
                // Every companion advances on launch, so time away counts the same for each of them.
                launch = household.AdvanceAll(active != null);
            }
            catch (Exception error)
            {
                failure = error;
            }

            refreshIn = 60;
            if (active != null)
                launch = ShowCompanion(active, launch);
            else if (household.Companions.Count > 0)
                ShowHome();
            else
                StartAdoption();

            // A failed launch advance keeps the active companion's unlocks pending, so ShowCompanion
            // still plays them.
            if (failure != null)
                ReportUnlessAnnounced(failure, "Your progress couldn't be saved. Please try again.", launch);
        }

        private void BuildHouseholdChips(RectTransform top)
        {
            homeChip = ActionButton("Home", top, 0, 0.17f, 190, 232, ShowHome, Mist, Ink);
            homeChip.GetComponentInChildren<Text>().fontSize = 21;
            SoftShapes.Round(homeChip.image, 1f);
            SoftShapes.Lift(homeChip.gameObject);
            var dot = Box("Home Attention", homeChip.transform, Vector2.one, Vector2.one,
                new Vector2(-17, -17), new Vector2(-3, -3));
            homeDot = Surface(dot, Amber);
            SoftShapes.Disc(homeDot);
            dot.gameObject.SetActive(false);
            homeChip.gameObject.SetActive(false);

            switchChip = ActionButton("Switch companion", top, 0.18f, 0.56f, 190, 232, SwitchToNext, Mist, Ink);
            switchLabel = switchChip.GetComponentInChildren<Text>();
            switchLabel.rectTransform.offsetMin = new Vector2(10, 0);
            switchLabel.rectTransform.offsetMax = new Vector2(-10, 0);
            switchLabel.resizeTextForBestFit = true;
            switchLabel.resizeTextMinSize = 15;
            switchLabel.resizeTextMaxSize = 21;
            SoftShapes.Round(switchChip.image, 1f);
            SoftShapes.Lift(switchChip.gameObject);
            switchChip.gameObject.SetActive(false);
        }

        private void BuildHome()
        {
            home = new HomePanel(Tray("Home Panel", true, 700), ShowActiveCompanion, OpenCompanion, StartAdoption, AskGoodbye,
                OpenJournal, AskWake, () => RequestGate(ShowMoreRooms), () => RequestGate(ShowSettings));
            goodbye = new GoodbyePanel(Tray("Goodbye Panel", false, 340), ShowHome, ContinueGoodbye, ConfirmGoodbye);
            BuildRoomPanels();
            BuildAdoptionPanels();
            BuildPetRoom();
            home.PetPortrait = PetPortrait;
        }

        // Shows the chosen view's trays and hides the others. The nursery's own panels follow activePanel.
        private void ApplyHouseholdView()
        {
            var nurseryShown = view == HouseholdView.Nursery && session?.State != null;
            header.SetActive(view == HouseholdView.Nursery || view == HouseholdView.Adoption || view == HouseholdView.Choose ||
                view == HouseholdView.PetAdoption || view == HouseholdView.PetRoom);
            welcome.SetActive(view == HouseholdView.Adoption);
            backToHome.gameObject.SetActive(view == HouseholdView.Adoption && household != null &&
                household.Companions.Count > 0);
            home.Root.SetActive(view == HouseholdView.Home);
            goodbye.Root.SetActive(view == HouseholdView.Goodbye);
            ApplyRoomPanels();
            UpdatePanelVisibility(nurseryShown);
            ApplyPetViews();
        }

        private void UpdateHouseholdChips(bool visible)
        {
            homeChip.gameObject.SetActive(visible);
            wake.gameObject.SetActive(!visible);
            var next = visible ? NextCompanion() : null;
            switchChip.gameObject.SetActive(next != null);
            if (next != null)
                switchLabel.text = "Switch to " + next.Name;
            homeDot.gameObject.SetActive(visible && AnotherNeedsAttention());
        }

        private void ShowHome()
        {
            if (household == null)
                return;

            view = HouseholdView.Home;
            ApplyHouseholdView();
            var first = home.Show(household, PlaytestOn, DeveloperModeOn);
            if (first != null)
                first.Select();
        }

        private void ShowActiveCompanion()
        {
            var active = household?.Active;
            if (active == null)
                return;

            var failure = CommitHomeTime();
            var shown = ShowCompanion(active, default);
            if (failure != null)
                ReportUnlessAnnounced(failure, "Your progress couldn't be saved. Please try again.", shown);
        }

        private void OpenCompanion(int slot)
        {
            var failure = CommitHomeTime();
            try
            {
                household.Switch(slot);
            }
            catch (Exception error)
            {
                ReportFailure(error, "That room couldn't be opened. Please try again.");
                return;
            }

            var shown = ShowCompanion(household.Active, default);
            if (failure != null)
                ReportUnlessAnnounced(failure, "Your progress couldn't be saved. Please try again.", shown);
        }

        // Home only previews time, so leaving it commits every companion's advance, as a resume does.
        // Every companion advances alike, so decay still never depends on switching. Unlocks wait as
        // pending celebrations and the room that opens plays its own.
        private Exception CommitHomeTime()
        {
            if (view != HouseholdView.Home)
                return null;

            try
            {
                household.AdvanceAll(activeOnScreen: false);
                return null;
            }
            catch (Exception error)
            {
                return error;
            }
        }

        private void SwitchToNext()
        {
            var next = NextCompanion();
            if (next != null)
                OpenCompanion(next.Slot);
        }

        // Re-binds the nursery to a companion's session: its state, look and room theme. Switching never
        // advances time; milestones reached off screen are shown here, once. Returns what was announced.
        // A pet opens its own room.
        private (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) ShowCompanion(
            HouseholdCompanion companion, (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) unlocked)
        {
            if (companion.PetSession != null)
            {
                ShowPetRoom(companion);
                return unlocked;
            }

            session = companion.Session;
            view = HouseholdView.Nursery;
            activePanel = NurseryPanel.Care;
            nursery.ResetPresentation();
            var failure = TakeCelebrations(companion.Slot, ref unlocked);
            Paint(Reaction.None, unlocked);
            if (failure != null)
                ReportUnlessAnnounced(failure, "Your progress couldn't be saved. Please try again.", unlocked);
            return unlocked;
        }

        // Adds the companion's pending celebrations to unlocked, once. When the write fails they stay
        // pending and are shown next time.
        private Exception TakeCelebrations(int slot,
            ref (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) unlocked)
        {
            try
            {
                foreach (var milestone in household.TakeCelebrations(slot))
                {
                    unlocked.UnlockedSitting |= milestone == CompanionMilestone.Sitting;
                    unlocked.UnlockedCrawling |= milestone == CompanionMilestone.Crawling;
                    unlocked.UnlockedWalking |= milestone == CompanionMilestone.Walking;
                }

                return null;
            }
            catch (Exception error)
            {
                return error;
            }
        }

        private void StartBabyAdoption()
        {
            if (household == null || !household.CanAdopt)
                return;

            view = HouseholdView.Adoption;
            nameField.text = string.Empty;
            nursery.ResetPresentation();
            Paint(Reaction.None);
        }

        private void AskGoodbye(int slot)
        {
            var companion = FindCompanion(slot);
            if (companion == null || !companion.IsReadable)
                return;

            goodbyeSlot = slot;
            view = HouseholdView.Goodbye;
            ApplyHouseholdView();
            goodbye.Ask(companion.Name);
            goodbye.Keep.Select();
        }

        private void ContinueGoodbye()
        {
            var companion = FindCompanion(goodbyeSlot);
            if (companion == null || !companion.IsReadable)
                return;

            // With companions resting, the freed room goes straight to the next one in Home order.
            var next = household.NextToWake;
            goodbye.Confirm(companion.Name, next == null ? null : next.Name ?? "a resting little one");
        }

        private void ConfirmGoodbye()
        {
            try
            {
                household.SayGoodbye(goodbyeSlot);
            }
            catch (Exception error)
            {
                if (FindCompanion(goodbyeSlot) != null)
                {
                    ReportFailure(error, "The goodbye couldn't be saved. Please try again.");
                    return;
                }

                // The record is already gone and only Home's list couldn't be saved; it is saved with
                // the next change. Leave the goodbye instead of offering a retry that can't apply.
                LeaveGoodbye();
                ReportFailure(error, "The goodbye is done, but Home couldn't be saved yet.");
                return;
            }

            LeaveGoodbye();
        }

        private void LeaveGoodbye()
        {
            goodbyeSlot = 0;
            if (household.Companions.Count == 0 && (household.CanAdopt || household.CanAdoptPet))
                StartAdoption();
            else
                ShowHome();
        }

        private HouseholdCompanion FindCompanion(int slot)
        {
            if (household == null)
                return null;

            foreach (var companion in household.Companions)
            {
                if (companion.Slot == slot)
                    return companion;
            }

            return null;
        }

        // The companion after the active one in Home order, so a switch is always one tap.
        private HouseholdCompanion NextCompanion()
        {
            var active = household?.Active;
            if (active == null)
                return null;

            var companions = household.Companions;
            var start = 0;
            for (var i = 0; i < companions.Count; i++)
            {
                if (companions[i] == active)
                    start = i;
            }

            for (var step = 1; step < companions.Count; step++)
            {
                var candidate = companions[(start + step) % companions.Count];
                if (candidate.IsReadable && !household.IsResting(candidate.Slot))
                    return candidate;
            }

            return null;
        }

        private bool AnotherNeedsAttention()
        {
            var active = household?.Active;
            if (active == null)
                return false;

            foreach (var companion in household.Companions)
            {
                if (companion != active && companion.IsReadable && !household.IsResting(companion.Slot) &&
                    companion.NeedsAttention)
                    return true;
            }

            return false;
        }

        private static string StageTitle(BabyStage stage)
        {
            switch (stage)
            {
                case BabyStage.Walker: return "Early walker";
                case BabyStage.Crawler: return "Crawler";
                case BabyStage.Sitter: return "Early sitter";
                default: return "Newborn";
            }
        }

        // A short, warm line for a Home card. needsCare tints its dot.
        private static string MoodLine(BabyState state, out bool needsCare)
        {
            const double low = 45;
            needsCare = true;
            if (state.Health < low)
                return "Feeling under the weather";
            if (state.Cleanliness < low)
                return "Ready for a fresh diaper";
            if (state.Energy < low)
                return "Getting sleepy";
            if (state.Satiety < low)
                return "A little hungry";
            if (state.Comfort < low)
                return "Would love a cuddle";

            needsCare = false;
            return "Happy and content";
        }

        private static Sprite Portrait(BabyStage stage) => Resources.Load<Sprite>("BAIby/Baby/" + stage);

        // Positions a rect by distances from its parent's top edge.
        private static void PlaceFromTop(RectTransform rect, float left, float right, float top, float bottom,
            float insetLeft = 0, float insetRight = 0)
        {
            rect.anchorMin = new Vector2(left, 1);
            rect.anchorMax = new Vector2(right, 1);
            rect.offsetMin = new Vector2(insetLeft, -bottom);
            rect.offsetMax = new Vector2(-insetRight, -top);
        }

        private sealed partial class HomePanel
        {
            private const float CardsTop = 112;
            private const float CardHeight = 360;
            private const float Gap = 16;

            private readonly RectTransform tray;
            private readonly RectTransform cards;
            private readonly Button close;
            private readonly RectTransform full;
            private readonly Text fullDetail;
            private readonly Text keepsakes;
            private readonly Text playtestBadge;
            private readonly Action<int> open;
            private readonly Action adopt;
            private readonly Action<int> farewell;
            private readonly Action<int> journal;
            private readonly Action<int> wake;
            private readonly List<CardDetails> companionCards = new List<CardDetails>();

            public GameObject Root { get; }
            public Text Feedback { get; }

            public HomePanel(RectTransform content, Action closeHome, Action<int> open, Action adopt, Action<int> farewell,
                Action<int> journal, Action<int> wake, Action moreRooms, Action settings)
            {
                this.open = open;
                this.adopt = adopt;
                this.farewell = farewell;
                this.journal = journal;
                this.wake = wake;
                tray = (RectTransform)content.parent;
                Root = tray.gameObject;

                var heading = Box("Home Heading Area", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(heading, 0, 0.44f, 18, 90);
                Label("Home Heading", heading, "Home", 40, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                playtestBadge = Label("Home playtest badge", content, "Playtest unlocks", 17, Fern, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                PlaceFromTop(playtestBadge.rectTransform, 0, 0.44f, 86, 108);
                playtestBadge.gameObject.SetActive(false);

                // Household settings sit behind the parental gate; they hold Restore Purchases.
                var grownUps = ActionButton("Household settings", content, 0.45f, 0.72f, 0, 0, settings, Paper, Secondary);
                var grownUpsLabel = grownUps.GetComponentInChildren<Text>();
                grownUpsLabel.text = "Grown-ups";
                grownUpsLabel.fontSize = 20;
                grownUpsLabel.fontStyle = FontStyle.Normal;
                PlaceFromTop((RectTransform)grownUps.transform, 0.45f, 0.72f, 30, 84, 3, 3);
                SoftShapes.Round(grownUps.image, 1f);
                close = ActionButton("Close home", content, 0.73f, 1, 0, 0, closeHome, Mist, Ink);
                close.GetComponentInChildren<Text>().text = "Back";
                PlaceFromTop((RectTransform)close.transform, 0.73f, 1, 20, 96, 3, 3);
                SoftShapes.Round(close.image, 1f);
                SoftShapes.Lift(close.gameObject);

                cards = Box("Home Cards", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                full = Box("Home full", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SoftShapes.Round(Surface(full, Mist), 0.7f);
                Label("Home full Title", full, "Your home is full", 27, Ink, TextAnchor.MiddleLeft,
                    new Vector2(22, 62), new Vector2(0.64f, 1), new Vector2(0, -10), FontStyle.Bold);
                fullDetail = Label("Home full Detail", full, "", 20, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(22, 10), Vector2.right, new Vector2(-22, 62));
                fullDetail.resizeTextForBestFit = true;
                fullDetail.resizeTextMinSize = 15;
                fullDetail.resizeTextMaxSize = 20;
                // A small, quiet link behind the parental gate. Nothing like it appears in a care flow.
                var more = ActionButton("More rooms", full, 0.66f, 1, 66, 112, moreRooms, Paper, Fern);
                var moreLabel = more.GetComponentInChildren<Text>();
                moreLabel.fontSize = 19;
                moreLabel.fontStyle = FontStyle.Normal;
                ((RectTransform)more.transform).offsetMax = new Vector2(-16, 112);
                SoftShapes.Round(more.image, 1f);

                var kept = Box("Home Keepsakes Area", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                keepsakes = Label("Home keepsakes", kept, "", 20, Secondary, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                keepsakes.resizeTextForBestFit = true;
                keepsakes.resizeTextMinSize = 14;
                keepsakes.resizeTextMaxSize = 20;

                var feedback = Box("Home Feedback Area", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Feedback = Label("Home Feedback", feedback, "", 21, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 15;
                Feedback.resizeTextMaxSize = 21;
                Root.SetActive(false);
            }

            // Updates the companion cards in place for the minute tick, so focus, a press in progress and
            // any feedback survive it. The states are previews; nothing is written.
            public void Refresh(HouseholdSession household)
            {
                foreach (var card in companionCards)
                {
                    var state = household.PreviewState(card.Slot);
                    if (state != null)
                        Describe(card, state);
                    else if (household.PreviewPetState(card.Slot) is PetState pet)
                        DescribePet(card, pet);
                    card.Badge.SetActive(!card.Resting && household.HasCelebrations(card.Slot));
                }
            }

            // Rebuilds the cards from the household and returns the first card for focus.
            public Button Show(HouseholdSession household, bool playtestOn, bool developerMode)
            {
                ClearCards();
                Feedback.text = string.Empty;
                playtestBadge.text = developerMode ? "Developer mode" : "Playtest unlocks";
                playtestBadge.gameObject.SetActive(playtestOn || developerMode);
                var active = household.Active;
                close.gameObject.SetActive(active != null);

                Button first = null;
                var companions = household.Companions;
                for (var i = 0; i < companions.Count; i++)
                {
                    var cell = Cell(i);
                    var companion = companions[i];
                    var resting = household.IsResting(companion.Slot);
                    var card = !companion.IsReadable
                        ? UnreadableCard(companion.Slot, cell)
                        : companion.IsPet
                            ? PetCard(companion.Slot, household.PreviewPetState(companion.Slot), cell, companion == active,
                                !resting && household.HasCelebrations(companion.Slot), household.CanSayGoodbye, resting)
                            : CompanionCard(companion.Slot, household.PreviewState(companion.Slot), cell, companion == active,
                                !resting && household.HasCelebrations(companion.Slot), household.CanSayGoodbye, resting);
                    if (first == null)
                        first = card;
                }

                var items = companions.Count;
                if (household.CanAdopt || household.CanAdoptPet)
                {
                    var card = AdoptCard(Cell(items));
                    if (first == null)
                        first = card;
                    items++;
                }

                var y = CardsTop + (items + 1) / 2 * (CardHeight + Gap);
                full.gameObject.SetActive(household.IsFull);
                fullDetail.text = household.HasResting
                    ? "Some little ones are resting. They're safe, and their needs don't drop while they rest. Choose Wake to swap who has a room."
                    : "Every room has a little one to care for. Enjoy your time together.";
                if (household.IsFull)
                {
                    PlaceFromTop(full, 0, 1, y, y + 124, 3, 3);
                    y += 124 + Gap;
                }

                keepsakes.text = DescribeKeepsakes(household.Keepsakes);
                var keptArea = (RectTransform)keepsakes.transform.parent;
                keptArea.gameObject.SetActive(keepsakes.text.Length > 0);
                if (keepsakes.text.Length > 0)
                {
                    PlaceFromTop(keptArea, 0, 1, y, y + 40, 6, 6);
                    y += 44;
                }

                PlaceFromTop((RectTransform)Feedback.transform.parent, 0, 1, y, y + 40, 6, 6);
                y += 48;
                tray.offsetMin = new Vector2(tray.offsetMin.x, -y - 17);
                tray.offsetMax = new Vector2(tray.offsetMax.x, -17);
                return first;
            }

            private RectTransform Cell(int index)
            {
                var column = index % 2;
                var top = CardsTop + index / 2 * (CardHeight + Gap);
                var cell = Box("Home cell " + index, cards, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(cell, column * 0.5f, (column + 1) * 0.5f, top, top + CardHeight,
                    column == 0 ? 3 : Gap / 2, column == 0 ? Gap / 2 : 3);
                return cell;
            }

            private void ClearCards()
            {
                companionCards.Clear();
                // Detach before destroying so the rebuilt cards are the only ones in the hierarchy this frame.
                for (var i = cards.childCount - 1; i >= 0; i--)
                {
                    var child = cards.GetChild(i);
                    child.gameObject.SetActive(false);
                    child.SetParent(null, false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }

            // A resting companion's card opens its journal instead of its room, looks quieter, and offers Wake
            // instead of Say goodbye.
            private Button CompanionCard(int slot, BabyState state, RectTransform cell, bool isActive, bool isNew,
                bool canSayGoodbye, bool resting)
            {
                Action tap = resting ? (Action)(() => journal(slot)) : () => open(slot);
                var card = ActionButton("Home card " + state.Name, cell, 0, 1, 0, 0, tap, resting ? Paper : Mist, Ink);
                Fill((RectTransform)card.transform);
                SoftShapes.Round(card.image, 0.7f);
                SoftShapes.Lift(card.gameObject);

                var frame = Box(card.name + " Portrait", card.transform, Vector2.zero, Vector2.one,
                    new Vector2(16, 178), new Vector2(-16, -14));
                var portrait = Surface(frame, resting ? new Color(1, 1, 1, 0.55f) : Color.white);
                portrait.preserveAspect = true;

                var name = card.GetComponentInChildren<Text>();
                name.text = state.Name;
                name.rectTransform.anchorMax = Vector2.right;
                name.rectTransform.offsetMin = new Vector2(10, 132);
                name.rectTransform.offsetMax = new Vector2(-10, 174);
                name.fontSize = 28;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = 28;
                var stageLabel = Label(card.name + " Stage", card.transform, "", 21, Secondary, TextAnchor.MiddleCenter,
                    new Vector2(10, 100), Vector2.right, new Vector2(-10, 130));

                var dot = Box(card.name + " Mood Dot", card.transform, Vector2.zero, Vector2.zero,
                    new Vector2(18, 76), new Vector2(32, 90));
                var moodDot = Surface(dot, Fern);
                SoftShapes.Disc(moodDot);
                var moodLabel = Label(card.name + " Mood", card.transform, "", 20, Ink, TextAnchor.MiddleLeft,
                    new Vector2(40, 66), Vector2.right, new Vector2(-10, 100));
                moodLabel.resizeTextForBestFit = true;
                moodLabel.resizeTextMinSize = 14;
                moodLabel.resizeTextMaxSize = 20;

                if (isActive)
                {
                    var selection = Box(card.name + " Selection", card.transform, Vector2.zero, Vector2.one,
                        Vector2.zero, Vector2.zero);
                    SoftShapes.Outline(Surface(selection, Fern));
                }

                var badge = Box(card.name + " New", card.transform, Vector2.up, Vector2.up,
                    new Vector2(12, -50), new Vector2(92, -14));
                SoftShapes.Round(Surface(badge, Fern), 1f);
                Label("New Label", badge, "New!", 19, Color.white, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                badge.gameObject.SetActive(isNew);

                if (resting)
                {
                    var restBadge = Box(card.name + " Resting", card.transform, Vector2.up, Vector2.up,
                        new Vector2(12, -50), new Vector2(122, -14));
                    SoftShapes.Round(Surface(restBadge, Mist), 1f);
                    Label("Resting Label", restBadge, "Resting", 19, Secondary, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                }

                var details = new CardDetails(slot, portrait, stageLabel, moodDot, moodLabel, badge.gameObject, resting);
                Describe(details, state);
                companionCards.Add(details);

                if (resting)
                {
                    var wakeButton = ActionButton("Wake " + state.Name, card.transform, 0.12f, 0.88f, 14, 60,
                        () => wake(slot), Mist, Fern);
                    var wakeLabel = wakeButton.GetComponentInChildren<Text>();
                    wakeLabel.text = "Wake";
                    wakeLabel.fontSize = 19;
                    SoftShapes.Round(wakeButton.image, 1f);
                }
                else if (canSayGoodbye)
                {
                    var bye = ActionButton("Say goodbye to " + state.Name, card.transform, 0.12f, 0.88f, 14, 60,
                        () => farewell(slot), Paper, Secondary);
                    var byeLabel = bye.GetComponentInChildren<Text>();
                    byeLabel.text = "Say goodbye";
                    byeLabel.fontSize = 19;
                    byeLabel.fontStyle = FontStyle.Normal;
                    SoftShapes.Round(bye.image, 1f);
                }

                return card;
            }

            // The parts of a companion card that change with time.
            private static void Describe(CardDetails card, BabyState state)
            {
                var stage = HouseholdRules.StageOf(state);
                card.Picture.sprite = Portrait(stage);
                card.Picture.enabled = card.Picture.sprite != null;
                card.Stage.text = StageTitle(stage);
                if (card.Resting)
                {
                    card.Mood.text = "Resting quietly";
                    card.MoodDot.color = Mist;
                    return;
                }

                card.Mood.text = MoodLine(state, out var needsCare);
                card.MoodDot.color = needsCare ? Amber : Fern;
            }

            private static Button UnreadableCard(int slot, RectTransform cell)
            {
                var card = Box("Home card slot " + slot, cell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SoftShapes.Round(Surface(card, Mist), 0.7f);
                var title = Label("Unreadable Title", card, "Couldn't open this save", 25, Ink, TextAnchor.MiddleCenter,
                    new Vector2(16, 190), Vector2.right, new Vector2(-16, 270), FontStyle.Bold);
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 17;
                title.resizeTextMaxSize = 25;
                Label("Unreadable Detail", card, "It's kept safe and still uses a room.", 20, Secondary,
                    TextAnchor.MiddleCenter, new Vector2(16, 110), Vector2.right, new Vector2(-16, 186));
                return null;
            }

            private Button AdoptCard(RectTransform cell)
            {
                var card = ActionButton("+ Adopt", cell, 0, 1, 0, 0, adopt, Paper, Fern);
                Fill((RectTransform)card.transform);
                SoftShapes.Round(card.image, 0.7f);
                var outline = Box("+ Adopt Outline", card.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SoftShapes.Dashed(Surface(outline, Fern));
                var title = card.GetComponentInChildren<Text>();
                title.fontSize = 34;
                title.rectTransform.offsetMin = new Vector2(10, 170);
                title.rectTransform.offsetMax = new Vector2(-10, -120);
                Label("+ Adopt Detail", card.transform, "Welcome a baby or a pet", 21, Secondary, TextAnchor.MiddleCenter,
                    new Vector2(10, 124), Vector2.right, new Vector2(-10, 166));
                return card;
            }

            private static void Fill(RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            private static string DescribeKeepsakes(IReadOnlyList<Keepsake> kept)
            {
                if (kept.Count == 0)
                    return string.Empty;

                var names = new List<string>();
                foreach (var keepsake in kept)
                {
                    var species = keepsake.Species is Species kind ? SpeciesName(kind).ToLowerInvariant() + ", " : string.Empty;
                    names.Add($"{keepsake.Name} ({species}{Day(keepsake.AdoptedAt)} – {Day(keepsake.FarewellAt)})");
                }
                return "Keepsakes · " + string.Join(" · ", names);
            }

            private static string Day(DateTimeOffset instant) =>
                instant.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture);

            private sealed class CardDetails
            {
                public CardDetails(int slot, Image picture, Text stage, Image moodDot, Text mood, GameObject badge,
                    bool resting)
                {
                    Resting = resting;
                    Slot = slot;
                    Picture = picture;
                    Stage = stage;
                    MoodDot = moodDot;
                    Mood = mood;
                    Badge = badge;
                }

                public int Slot { get; }
                public bool Resting { get; }
                public Image Picture { get; }
                public Text Stage { get; }
                public Image MoodDot { get; }
                public Text Mood { get; }
                public GameObject Badge { get; }
            }
        }

        private sealed class GoodbyePanel
        {
            private readonly Text heading;
            private readonly Text detail;
            private readonly Text keepLabel;
            private readonly Button proceed;
            private readonly Button confirm;

            public GameObject Root { get; }
            public Button Keep { get; }
            public Text Feedback { get; }

            public GoodbyePanel(RectTransform content, Action keep, Action proceed, Action confirm)
            {
                Root = content.parent.gameObject;
                heading = Label("Goodbye Heading", content, "", 34, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 262), Vector2.right, new Vector2(0, 330), FontStyle.Bold);
                heading.resizeTextForBestFit = true;
                heading.resizeTextMinSize = 22;
                heading.resizeTextMaxSize = 34;
                detail = Label("Goodbye Detail", content, "", 23, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 150), Vector2.right, new Vector2(0, 260));
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 17;
                detail.resizeTextMaxSize = 23;
                Keep = ActionButton("Keep companion", content, 0, 0.5f, 54, 142, keep, Mist, Ink);
                keepLabel = Keep.GetComponentInChildren<Text>();
                keepLabel.resizeTextForBestFit = true;
                keepLabel.resizeTextMinSize = 16;
                keepLabel.resizeTextMaxSize = 25;
                this.proceed = ActionButton("Continue goodbye", content, 0.5f, 1, 54, 142, proceed, Fern, Color.white);
                this.proceed.GetComponentInChildren<Text>().text = "Continue";
                this.confirm = ActionButton("Confirm goodbye", content, 0.5f, 1, 54, 142, confirm, Fern, Color.white);
                this.confirm.GetComponentInChildren<Text>().text = "Say goodbye";
                foreach (var button in new[] { Keep, this.proceed, this.confirm })
                {
                    SoftShapes.Round(button.image, 1f);
                    SoftShapes.Lift(button.gameObject);
                }

                Feedback = Label("Goodbye Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 6), Vector2.right, new Vector2(0, 50));
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 15;
                Feedback.resizeTextMaxSize = 21;
                Root.SetActive(false);
            }

            // First step: explain what stays behind.
            public void Ask(string name)
            {
                heading.text = $"Say goodbye to {name}?";
                detail.text = $"{name} will leave your home. A keepsake with their name, dates and look stays on Home.";
                keepLabel.text = $"Keep {name}";
                proceed.gameObject.SetActive(true);
                confirm.gameObject.SetActive(false);
                Feedback.text = string.Empty;
            }

            // Second step: the confirmation that frees the room, or hands it to the resting companion who wakes.
            public void Confirm(string name, string wakes)
            {
                heading.text = "Are you sure?";
                detail.text = wakes == null
                    ? $"This can't be undone. {name}'s room will be free for someone new."
                    : $"This can't be undone. {name}'s room will go to {wakes}, who wakes up and moves in.";
                proceed.gameObject.SetActive(false);
                confirm.gameObject.SetActive(true);
                Feedback.text = string.Empty;
                confirm.Select();
            }
        }
    }
}
