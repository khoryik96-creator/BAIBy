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
        // Choose is "Who's joining your home?"; PetAdoption picks species, breed and name; PetRoom is a pet's room.
        private enum HouseholdView { Adoption, Nursery, Home, Goodbye, Gate, MoreRooms, Settings, Journal, Wake, Choose, PetAdoption, PetRoom, Helpers }

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

        // The switch pill sits in the heading row of whichever room's sheet is open (UpdateHouseholdChips moves it).
        private void BuildSwitchChip(RectTransform heading)
        {
            switchChip = ActionButton("Switch companion", heading, 0, 1, 0, 0, SwitchToNext, Mist, Ink);
            SoftShapes.Pill(switchChip.image);
            switchLabel = switchChip.GetComponentInChildren<Text>();
            switchLabel.rectTransform.offsetMin = new Vector2(Px(10), 0);
            switchLabel.rectTransform.offsetMax = new Vector2(-Px(10), 0);
            FitText(switchLabel, 16, 22);
            switchChip.gameObject.SetActive(false);
        }

        private void BuildHome()
        {
            home = new HomePanel(HomePage(), ShowActiveCompanion, OpenCompanion, StartAdoption, AskGoodbye,
                OpenJournal, AskWake, () => RequestGate(ShowMoreRooms), () => RequestGate(ShowSettings));
            goodbye = new GoodbyePanel(Tray("Goodbye Panel", false, 340), ShowHome, ContinueGoodbye, ConfirmGoodbye);
            BuildRoomPanels();
            BuildAdoptionPanels();
            BuildPetRoom();
            home.PetPortrait = PetPortrait;
        }

        // Home is a page on the cream ground over the whole screen; its content scrolls when there are many rooms.
        private RectTransform HomePage()
        {
            var root = Box("Home Panel", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Surface(Box("Home Ground", root, Vector2.zero, Vector2.one, new Vector2(-800, -800), new Vector2(800, 800)), Cream);
            var viewport = Box("Home Viewport", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var catcher = Surface(viewport, Color.clear);
            catcher.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Box("Content", viewport, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-StackWidth / 2, -1200), new Vector2(StackWidth / 2, 0));
            content.pivot = new Vector2(0.5f, 1);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30;
            scroll.inertia = true;
            return content;
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
            if (!nurseryShown)
                HideHealthChip();
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
            var heading = view == HouseholdView.PetRoom ? petHead : careHead;
            if (heading != null && switchChip.transform.parent != heading)
                switchChip.transform.SetParent(heading, false);
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
            switch (state.Care.Condition)
            {
                case CareCondition.Sick: return "Poorly, would love the doctor";
                case CareCondition.UnderTheWeather: return "A bit under the weather";
                case CareCondition.Recovering: return "Getting better";
            }

            if (state.Health < low)
                return "Looking a little pale";
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


        // Home: "Your home" and the rooms in use, the Grown-ups gear, two columns of companion cards (a round pastel
        // portrait, name, stage and day, a mood pill and Visit), the "Your home is full" row and the keepsakes.
        private sealed partial class HomePanel
        {
            private const float TopPad = 30;
            private const float CardHeight = 536;
            private const float Gap = 22;
            private const float Inner = 26;

            private readonly RectTransform content;
            private readonly RectTransform cards;
            private readonly RectTransform headingArea;
            private readonly Button close;
            private readonly RectTransform full;
            private readonly Text fullDetail;
            private readonly RectTransform kept;
            private readonly Text keepsakes;
            private readonly Text rooms;
            private readonly Text playtestBadge;
            private readonly RectTransform playtestChip;
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
                this.content = content;
                this.open = open;
                this.adopt = adopt;
                this.farewell = farewell;
                this.journal = journal;
                this.wake = wake;
                Root = content.parent.parent.gameObject;

                close = IconButton("Close home", content, "Back", closeHome, Paper);
                PlaceFromTop((RectTransform)close.transform, 0, 0, TopPad, TopPad + 82, 0, -82);
                SoftShapes.Lift(close.gameObject);
                headingArea = Box("Home Heading Area", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Label("Home Heading", headingArea, "Your home", 55, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 34), Vector2.one, Vector2.zero, FontStyle.Bold);
                rooms = Label("Home Rooms", headingArea, "", 26, Secondary, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.right, new Vector2(0, 34));

                // Household settings sit behind the parental gate; they hold Restore Purchases.
                var grownUps = IconButton("Household settings", content, "Gear", settings, Paper);
                PlaceFromTop((RectTransform)grownUps.transform, 1, 1, TopPad, TopPad + 82, -82, 0);
                SoftShapes.Lift(grownUps.gameObject);

                playtestChip = Box("Home playtest chip", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SoftShapes.Pill(Surface(playtestChip, FernSoft));
                playtestBadge = Label("Home playtest badge", playtestChip, "Playtest unlocks", 20, FernDeep,
                    TextAnchor.MiddleCenter, new Vector2(Px(10), 0), Vector2.one, new Vector2(-Px(10), 0), FontStyle.Bold);
                playtestChip.gameObject.SetActive(false);

                cards = Box("Home Cards", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                full = Box("Home full", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Card(full, Paper, Px(22), 0.06f);
                var fullIcon = Box("Home full Icon", full, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(Px(16), -Px(24)), new Vector2(Px(16) + Px(48), Px(24)));
                SoftShapes.Radius(Surface(fullIcon, Lilac), Px(16));
                IconAt("Icon", fullIcon, "HomeAdd", Ink, new Vector2(0.5f, 0.5f), Vector2.zero, Px(24));
                var textLeft = Px(16) + Px(48) + Px(14);
                var fullTitle = Label("Home full Title", full, "Your home is full", 31, Ink, TextAnchor.LowerLeft,
                    new Vector2(textLeft, 0), Vector2.one, new Vector2(-190, -24), FontStyle.Bold);
                fullTitle.rectTransform.anchorMin = new Vector2(0, 0.62f);
                FitText(fullTitle, 24, 31);
                fullDetail = Label("Home full Detail", full, "", 24, Secondary, TextAnchor.UpperLeft,
                    new Vector2(textLeft, 18), new Vector2(1, 0.62f), new Vector2(-190, -4));
                FitText(fullDetail, 15, 24);
                // A small, quiet link behind the parental gate. Nothing like it appears in a care flow.
                var more = ActionButton("More rooms", full, 1, 1, 0, 0, moreRooms, Color.clear, Fern);
                var moreRect = (RectTransform)more.transform;
                moreRect.anchorMin = new Vector2(1, 0.5f);
                moreRect.anchorMax = new Vector2(1, 0.5f);
                moreRect.offsetMin = new Vector2(-180, -41);
                moreRect.offsetMax = new Vector2(-10, 41);

                kept = Box("Home Keepsakes Area", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Label("Home Keepsakes Heading", kept, "Keepsakes", 33, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, -44), new Vector2(1, 1), Vector2.zero, FontStyle.Bold).rectTransform.anchorMin = new Vector2(0, 1);
                var box = Box("Home Keepsakes Box", kept, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -60));
                SoftShapes.Radius(Surface(box, new Color(1, 1, 1, 0.6f)), Px(20));
                SoftShapes.Dashed(Surface(Box("Home Keepsakes Edge", box, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                    Divider), Px(20));
                var heart = Box("Home Keepsakes Mark", box, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(Px(14), -Px(20)), new Vector2(Px(14) + Px(40), Px(20)));
                SoftShapes.Disc(Surface(heart, Mist));
                IconAt("Icon", heart, "Soothe", Secondary, new Vector2(0.5f, 0.5f), Vector2.zero, Px(20));
                keepsakes = Label("Home keepsakes", box, "", 24, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(Px(14) + Px(40) + Px(12), 12), Vector2.one, new Vector2(-Px(14), -12));
                FitText(keepsakes, 15, 24);

                var feedback = Box("Home Feedback Area", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Feedback = Label("Home Feedback", feedback, "", 24, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                FitText(Feedback, 16, 24);
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
                playtestChip.gameObject.SetActive(playtestOn || developerMode);
                var active = household.Active;
                close.gameObject.SetActive(active != null);
                var awake = Math.Min(household.Companions.Count, household.Capacity);
                rooms.text = $"{awake} of {household.Capacity} rooms" +
                    (household.Capacity == HouseholdRules.FreeCompanions ? " · free" : string.Empty);

                var left = active != null ? 82 + Gap : 0;
                PlaceFromTop(headingArea, 0, 1, TopPad + 2, TopPad + 98, left, 82 + Gap);
                var y = TopPad + 106f;
                if (playtestChip.gameObject.activeSelf)
                {
                    var chipWidth = PillWidth(playtestBadge, Px(10));
                    PlaceFromTop(playtestChip, 0, 0, y, y + 44, left, -left - chipWidth);
                    y += 56;
                }

                y += 18;
                var cardsTop = y;
                Button first = null;
                var companions = household.Companions;
                for (var i = 0; i < companions.Count; i++)
                {
                    var cell = Cell(i, cardsTop);
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
                    var card = AdoptCard(Cell(items, cardsTop));
                    if (first == null)
                        first = card;
                    items++;
                }

                y = cardsTop + (items + 1) / 2 * (CardHeight + Gap) + 6;
                full.gameObject.SetActive(household.IsFull);
                fullDetail.text = household.HasResting
                    ? "Some little ones are resting. They're safe, and their needs don't drop while they rest. Choose Wake to swap who has a room."
                    : "Every room has a little one to care for.";
                if (household.IsFull)
                {
                    PlaceFromTop(full, 0, 1, y, y + 190);
                    y += 190 + Gap + 8;
                }

                keepsakes.text = DescribeKeepsakes(household.Keepsakes);
                PlaceFromTop(kept, 0, 1, y, y + 60 + 120);
                y += 180 + Gap;

                PlaceFromTop((RectTransform)Feedback.transform.parent, 0, 1, y, y + 56, 6, 6);
                y += 56 + 40;
                content.sizeDelta = new Vector2(content.sizeDelta.x, y);
                content.anchoredPosition = Vector2.zero;
                SettleText(content);
                return first;
            }

            private RectTransform Cell(int index, float top)
            {
                var column = index % 2;
                var cellTop = top + index / 2 * (CardHeight + Gap);
                var cell = Box("Home cell " + index, cards, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(cell, column * 0.5f, (column + 1) * 0.5f, cellTop, cellTop + CardHeight,
                    column == 0 ? 0 : Gap / 2, column == 0 ? Gap / 2 : 0);
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

            // A white card that is itself a button: a soft shadow, a rounded surface, and the name as its first text.
            private static Button CardButton(string objectName, RectTransform cell, Action tap, string title)
            {
                var root = Box(objectName, cell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var surface = Card(root, Paper, Px(26), 0.08f);
                surface.raycastTarget = true;
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                button.colors = Pressable();
                button.onClick.AddListener(() => tap());
                var name = Label("Label", root, title, 37, Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero,
                    FontStyle.Bold);
                FitText(name, 24, 37);
                PlaceFromTop(name.rectTransform, 0, 1, 232, 278, Inner, Inner);
                return button;
            }

            // A baby's or a pet's card. A resting companion's card opens its journal instead of its room, looks
            // quieter, and offers Wake instead of Visit and Say goodbye.
            private CardDetails BuildCard(int slot, string name, RectTransform cell, bool isActive, bool isNew,
                bool canSayGoodbye, bool resting, Color portraitTint, bool isPet, out Button card)
            {
                Action tap = resting ? (Action)(() => journal(slot)) : () => open(slot);
                card = CardButton("Home card " + name, cell, tap, name);

                var circle = Box(card.name + " Frame", card.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-100, -224), new Vector2(100, -24));
                var disc = Surface(circle, resting ? Mist : portraitTint);
                SoftShapes.Disc(disc);
                circle.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var frame = Box(card.name + " Portrait", circle, Vector2.zero, Vector2.one,
                    isPet ? new Vector2(30, 30) : new Vector2(14, -34), isPet ? new Vector2(-30, -30) : new Vector2(-14, -8));
                var portrait = Surface(frame, resting ? new Color(1, 1, 1, 0.55f) : Color.white);
                portrait.preserveAspect = true;

                var stageLabel = Label(card.name + " Stage", card.transform, "", 24, Secondary, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero);
                FitText(stageLabel, 17, 24);
                PlaceFromTop(stageLabel.rectTransform, 0, 1, 278, 310, Inner, Inner);

                var pill = Box(card.name + " Mood Pill", card.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-120, -366), new Vector2(120, -322));
                var pillImage = Surface(pill, FernSoft);
                SoftShapes.Pill(pillImage);
                var dot = Box(card.name + " Mood Dot", pill, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(Px(10), -Px(4)), new Vector2(Px(18), Px(4)));
                var moodDot = Surface(dot, Fern);
                SoftShapes.Disc(moodDot);
                var moodLabel = Label(card.name + " Mood", pill, "", 22, FernDeep, TextAnchor.MiddleLeft,
                    new Vector2(Px(24), 0), Vector2.one, new Vector2(-Px(10), 0), FontStyle.Bold);
                FitText(moodLabel, 14, 22);

                if (isActive)
                {
                    var selection = Box(card.name + " Selection", card.transform, Vector2.zero, Vector2.one,
                        Vector2.zero, Vector2.zero);
                    SoftShapes.Outline(Surface(selection, Fern), Px(26));
                }

                var badge = Box(card.name + " New", card.transform, Vector2.up, Vector2.up,
                    new Vector2(16, -60), new Vector2(104, -16));
                SoftShapes.Pill(Surface(badge, Fern));
                Label("New Label", badge, "New!", 20, Color.white, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                badge.gameObject.SetActive(isNew);

                if (resting)
                {
                    var restBadge = Box(card.name + " Resting", card.transform, Vector2.up, Vector2.up,
                        new Vector2(16, -60), new Vector2(136, -16));
                    SoftShapes.Pill(Surface(restBadge, Mist));
                    Label("Resting Label", restBadge, "Resting", 20, Muted, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                }

                if (resting)
                {
                    var wakeButton = ActionButton("Wake " + name, card.transform, 0, 1, 0, 0, () => wake(slot), FernSoft, FernDeep);
                    wakeButton.GetComponentInChildren<Text>().text = "Wake";
                    SoftShapes.Radius(wakeButton.image, Px(14));
                    PlaceFromTop((RectTransform)wakeButton.transform, 0, 1, 384, 464, Inner, Inner);
                }
                else
                {
                    var visit = ActionButton(card.name + " Visit", card.transform, 0, 1, 0, 0, tap,
                        isActive ? Fern : Paper, isActive ? Color.white : Ink);
                    visit.GetComponentInChildren<Text>().text = "Visit";
                    SoftShapes.Radius(visit.image, Px(14));
                    PlaceFromTop((RectTransform)visit.transform, 0, 1, 384, 464, Inner, Inner);
                    if (canSayGoodbye)
                    {
                        var bye = ActionButton("Say goodbye to " + name, card.transform, 0, 1, 0, 0,
                            () => farewell(slot), Color.clear, Secondary);
                        var byeLabel = bye.GetComponentInChildren<Text>();
                        byeLabel.text = "Say goodbye";
                        byeLabel.fontSize = 22;
                        PlaceFromTop((RectTransform)bye.transform, 0, 1, 470, 522, Inner, Inner);
                    }
                }

                return new CardDetails(slot, portrait, stageLabel, pillImage, moodDot, moodLabel, badge.gameObject, resting);
            }

            private Button CompanionCard(int slot, BabyState state, RectTransform cell, bool isActive, bool isNew,
                bool canSayGoodbye, bool resting)
            {
                var details = BuildCard(slot, state.Name, cell, isActive, isNew, canSayGoodbye, resting, FernSoft, false,
                    out var card);
                Describe(details, state);
                companionCards.Add(details);
                return card;
            }

            // The parts of a companion card that change with time.
            private static void Describe(CardDetails card, BabyState state)
            {
                var stage = HouseholdRules.StageOf(state);
                card.Picture.sprite = Portrait(stage);
                card.Picture.enabled = card.Picture.sprite != null;
                card.Stage.text = StageDay(StageTitle(stage), state.Age);
                if (card.Resting)
                {
                    ShowMood(card, "Resting quietly", Mist, Muted, Divider);
                    return;
                }

                var mood = MoodLine(state, out var needsCare);
                if (needsCare)
                    ShowMood(card, mood, Butter, ButterInk, Hex(0xC99520));
                else
                    ShowMood(card, mood, FernSoft, FernDeep, Fern);
            }

            // A pill that hugs its words, centred under the stage line.
            private static void ShowMood(CardDetails card, string text, Color background, Color ink, Color dot)
            {
                card.Mood.text = text;
                card.Mood.color = ink;
                card.MoodPill.color = background;
                card.MoodDot.color = dot;
                var width = Mathf.Min(TextWidth(card.Mood) + Px(24) + Px(10) + 4, 319 - 2 * Inner);
                var rect = card.MoodPill.rectTransform;
                rect.offsetMin = new Vector2(-width / 2, rect.offsetMin.y);
                rect.offsetMax = new Vector2(width / 2, rect.offsetMax.y);
            }

            private static Button UnreadableCard(int slot, RectTransform cell)
            {
                var card = Box("Home card slot " + slot, cell, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Card(card, Paper, Px(26), 0.08f);
                var title = Label("Unreadable Title", card, "Couldn't open this save", 31, Ink, TextAnchor.MiddleCenter,
                    new Vector2(Inner, 270), Vector2.right, new Vector2(-Inner, 360), FontStyle.Bold);
                FitText(title, 20, 31);
                var detail = Label("Unreadable Detail", card, "It's kept safe and still uses a room.", 24, Secondary,
                    TextAnchor.UpperCenter, new Vector2(Inner, 150), Vector2.right, new Vector2(-Inner, 262));
                FitText(detail, 16, 24);
                return null;
            }

            // Adding someone: the same card shape with a dashed edge.
            private Button AdoptCard(RectTransform cell)
            {
                var card = ActionButton("+ Adopt", cell, 0, 1, 0, 0, adopt, new Color(1, 1, 1, 0.6f), Ink);
                Fill((RectTransform)card.transform);
                SoftShapes.Radius(card.image, Px(26));
                var outline = Box("+ Adopt Outline", card.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                SoftShapes.Dashed(Surface(outline, Divider), Px(26));
                var plus = Box("+ Adopt Mark", card.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-70, -224), new Vector2(70, -84));
                SoftShapes.Disc(Surface(plus, FernSoft));
                IconAt("Icon", plus, "Plus", Fern, new Vector2(0.5f, 0.5f), Vector2.zero, 64);
                var title = card.GetComponentInChildren<Text>();
                title.text = "Adopt";
                title.fontSize = 37;
                SetRole(title, TextRole.Heading);
                PlaceFromTop(title.rectTransform, 0, 1, 250, 296, Inner, Inner);
                var detail = Label("+ Adopt Detail", card.transform, "Welcome a baby or a pet", 24, Secondary,
                    TextAnchor.UpperCenter, Vector2.zero, Vector2.one, Vector2.zero);
                FitText(detail, 16, 24);
                PlaceFromTop(detail.rectTransform, 0, 1, 300, 370, Inner, Inner);
                return card;
            }

            private static void Fill(RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            // Names and dates of little ones who moved on, or a quiet note while there are none.
            private static string DescribeKeepsakes(IReadOnlyList<Keepsake> kept)
            {
                if (kept.Count == 0)
                    return "Memories of little ones who moved on will live here.";

                var names = new List<string>();
                foreach (var keepsake in kept)
                {
                    var species = keepsake.Species is Species kind ? SpeciesName(kind).ToLowerInvariant() + ", " : string.Empty;
                    names.Add($"{keepsake.Name} ({species}{Day(keepsake.AdoptedAt)} – {Day(keepsake.FarewellAt)})");
                }
                return string.Join(" · ", names);
            }

            private static string Day(DateTimeOffset instant) =>
                instant.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture);

            private sealed class CardDetails
            {
                public CardDetails(int slot, Image picture, Text stage, Image moodPill, Image moodDot, Text mood, GameObject badge,
                    bool resting)
                {
                    Resting = resting;
                    Slot = slot;
                    Picture = picture;
                    Stage = stage;
                    MoodPill = moodPill;
                    MoodDot = moodDot;
                    Mood = mood;
                    Badge = badge;
                }

                public int Slot { get; }
                public bool Resting { get; }
                public Image Picture { get; }
                public Text Stage { get; }
                public Image MoodPill { get; }
                public Image MoodDot { get; }
                public Text Mood { get; }
                public GameObject Badge { get; }
            }
        }

        // Saying goodbye takes two steps on a calm sheet: what stays behind, then the confirmation.
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
                heading = Label("Goodbye Heading", content, "", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 262), Vector2.right, new Vector2(0, 334), FontStyle.Bold);
                FitText(heading, 26, 38);
                detail = Label("Goodbye Detail", content, "", 26, Muted, TextAnchor.UpperLeft,
                    new Vector2(0, 150), Vector2.right, new Vector2(0, 254));
                FitText(detail, 17, 26);
                Keep = ActionButton("Keep companion", content, 0, 0.5f, 56, 142, keep, Mist, Ink);
                keepLabel = Keep.GetComponentInChildren<Text>();
                FitText(keepLabel, 16, 26);
                this.proceed = ActionButton("Continue goodbye", content, 0.5f, 1, 56, 142, proceed, Blush, BlushInk);
                this.proceed.GetComponentInChildren<Text>().text = "Continue";
                this.confirm = ActionButton("Confirm goodbye", content, 0.5f, 1, 56, 142, confirm, Blush, BlushInk);
                this.confirm.GetComponentInChildren<Text>().text = "Say goodbye";
                foreach (var button in new[] { Keep, this.proceed, this.confirm })
                {
                    var rect = (RectTransform)button.transform;
                    rect.offsetMin = new Vector2(button == Keep ? 0 : 9, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(button == Keep ? -9 : 0, rect.offsetMax.y);
                }

                Feedback = Label("Goodbye Feedback", content, "", 22, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 4), Vector2.right, new Vector2(0, 50));
                FitText(Feedback, 15, 22);
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
