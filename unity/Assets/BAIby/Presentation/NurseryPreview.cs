using System;
using BAIby.Application;
using BAIby.Character;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(43, 51, 44, 255);
        private static readonly Color Secondary = new Color32(75, 85, 74, 255);
        private static readonly Color Paper = new Color32(250, 247, 234, 255);
        private static readonly Color Fern = new Color32(58, 89, 70, 255);
        private static readonly Color Mist = new Color32(224, 232, 217, 255);

        // The active companion's session; the household owns and caches every companion's session.
        private BabySession session;
        private NurseryVisuals nursery;
        private RectTransform safeArea;
        private Rect previousSafeArea;
        private Vector2Int previousScreen;
        private GameObject welcome;
        private GameObject controls;
        private Text title;
        private Text age;
        private Text favorite;
        private Text status;
        private Text saveError;
        private Text wake;
        private Button adopt;
        private InputField nameField;
        private NeedMeter satiety;
        private NeedMeter comfort;
        private NeedMeter energy;
        private NeedMeter health;
        private NeedMeter cleanliness;
        private float refreshIn;
        private NurseryPanel activePanel;
        private Button washUpButton;

        private void Awake()
        {
            nursery = NurseryVisuals.Create();
            nursery.transform.SetParent(transform, true);
            nursery.Present(false, false, Reaction.None);
            BuildInterface();
        }

        public void ReportStartupFailure(Exception error)
        {
            ReportFailure(error, "The saved nursery couldn't be opened.");
            adopt.interactable = false;
        }

        private void Update()
        {
            FitSafeArea();
            if (Input.GetKeyDown(KeyCode.Escape))
                GoBack();

            refreshIn -= Time.unscaledDeltaTime;
            if (refreshIn <= 0)
                Refresh();
        }

        private void GoBack()
        {
            switch (view)
            {
                case HouseholdView.Goodbye:
                    ShowHome();
                    break;
                case HouseholdView.Home:
                    ShowActiveCompanion();
                    break;
                case HouseholdView.Adoption:
                    if (backToHome.gameObject.activeSelf)
                        ShowHome();
                    break;
                default:
                    if (activePanel != NurseryPanel.Care)
                        ShowPanel(activePanel switch
                        {
                            NurseryPanel.Diaper => NurseryPanel.Hygiene,
                            NurseryPanel.Blocks or NurseryPanel.Report => NurseryPanel.Development,
                            _ => NurseryPanel.Care
                        });
                    break;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && household != null)
                Refresh(resumed: true);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && household != null)
                Refresh(resumed: true);
        }

        // Advances every companion on resume and on the minute tick. Milestones of companions that are
        // not on screen wait as pending celebrations. On Home the minute tick is a preview that makes no
        // writes; resuming or leaving Home commits it.
        private void Refresh(bool resumed = false)
        {
            refreshIn = 60;
            if (household == null)
                return;

            if (view == HouseholdView.Home && !resumed)
            {
                home.Refresh(household);
                return;
            }

            var onScreen = view == HouseholdView.Nursery && session?.State != null;
            (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) unlocked = default;
            Exception failure = null;
            try
            {
                unlocked = household.AdvanceAll(onScreen);
            }
            catch (Exception error)
            {
                // The on-screen companion's unlocks were kept as a pending celebration; play them now.
                failure = error;
                var active = household.Active;
                if (onScreen && active != null)
                    TakeCelebrations(active.Slot, ref unlocked);
            }

            if (onScreen)
                Paint(Reaction.None, unlocked);
            else if (view == HouseholdView.Home)
                home.Refresh(household);

            if (failure != null)
                ReportUnlessAnnounced(failure, "Your progress couldn't be saved. Please try again.", unlocked);
        }

        // An announced milestone stays on screen. The failed write is logged, retried on the next tick
        // and reported then.
        private void ReportUnlessAnnounced(Exception error, string message,
            (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) announced)
        {
            if (announced.UnlockedSitting || announced.UnlockedCrawling || announced.UnlockedWalking)
                Debug.LogException(error);
            else
                ReportFailure(error, message);
        }

        private void Adopt()
        {
            if (string.IsNullOrWhiteSpace(nameField.text))
            {
                saveError.text = "Give your baby a name to begin.";
                return;
            }

            if (household == null)
                return;

            var before = household.Companions.Count;
            try
            {
                ShowCompanion(household.Adopt(nameField.text), default);
            }
            catch (Exception error)
            {
                // The record is written before the manifest, so an adopted baby is opened even when
                // the manifest write fails; the next launch lists it again.
                if (household.Companions.Count > before && view == HouseholdView.Adoption)
                    ShowCompanion(household.Active, default);
                ReportFailure(error, "The nursery couldn't be saved. Please try again.");
            }
        }

        private bool Care(CareAction action, Toy toy = Toy.Rattle, Food food = Food.Milk)
        {
            (Reaction Reaction, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) result;
            try
            {
                result = session.ApplyCare(action, toy, food);
            }
            catch (Exception error)
            {
                ReportFailure(error, "This action couldn't be saved. Please try again.");
                return false;
            }

            if (activePanel == NurseryPanel.Blocks)
                ShowPanel(NurseryPanel.Development);
            else if (activePanel == NurseryPanel.Diaper || activePanel == NurseryPanel.Hygiene ||
                (activePanel == NurseryPanel.NewSound && !session.State.HasPendingNewSound))
                ShowPanel(NurseryPanel.Care);
            Paint(result.Reaction, (result.UnlockedSitting, result.UnlockedCrawling, result.UnlockedWalking),
                action == CareAction.Play ? toy : (Toy?)null, food);
            return true;
        }

        private void OpenDiaperExercise()
        {
            diaper.ResetActivity();
            ShowPanel(NurseryPanel.Diaper);
        }

        private bool CompleteDiaper() =>
            activePanel == NurseryPanel.Diaper && Care(CareAction.ChangeDiaper);

        private void NextPreviewDay()
        {
            try
            {
                Paint(Reaction.None, session.AdvancePreview(TimeSpan.FromDays(1)));
            }
            catch (Exception error)
            {
                ReportFailure(error, "The next day couldn't be saved. Please try again.");
            }
        }

        private void Paint(Reaction reaction,
            (bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) unlocked = default,
            Toy? playedToy = null, Food food = Food.Milk)
        {
            var state = view == HouseholdView.Nursery ? session?.State : null;
            var adopted = state != null;
            ApplyHouseholdView();
            satiety.Root.gameObject.SetActive(adopted);
            comfort.Root.gameObject.SetActive(adopted);
            energy.Root.gameObject.SetActive(adopted);
            health.Root.gameObject.SetActive(adopted);
            cleanliness.Root.gameObject.SetActive(adopted);
            saveError.text = string.Empty;
            ApplyNurseryTheme(state?.NurseryTheme ?? NurseryTheme.Sage);
            var ambientToy = state?.FavoriteToy ?? Toy.Rattle;
            var visibleToy = playedToy ?? ambientToy;
            nursery.Present(adopted, state?.CanSitAndReach ?? false, reaction, visibleToy, ambientToy,
                canCrawl: state?.CanCrawl ?? false, food: food, canWalk: state?.CanWalk ?? false);
            // After Present, so the look is built first for the stage that is actually shown.
            ApplyBabyAppearance(state?.Appearance ?? BabyAppearance.Default);

            if (!adopted)
            {
                title.text = household != null && household.Companions.Count > 0 ? "A new arrival" : "Your nursery";
                wake.text = "BAIby";
                age.text = string.Empty;
                favorite.text = string.Empty;
                return;
            }

            title.text = state.Name;
            wake.text = "NURSERY";
            var days = (int)state.Age.TotalDays;
            var stage = StageName(state);
            age.text = days == 1 ? $"1 day old  ·  {stage}" : $"{days} days old  ·  {stage}";
            favorite.text = state.FavoriteToy is Toy favored ? $"Favorite: {ToyName(favored)}" : string.Empty;
            satiety.Set(state.Satiety);
            comfort.Set(state.Comfort);
            energy.Set(state.Energy);
            health.Set(state.Health);
            cleanliness.Set(state.Cleanliness);
            var reactionText = DescribeReaction(state.Name, reaction, visibleToy, food);
            if (unlocked.UnlockedWalking)
                status.text = $"{state.Name} can take a few steps now. " +
                    (reaction == Reaction.None ? "Try offering a familiar toy." : reactionText);
            else if (unlocked.UnlockedCrawling)
                status.text = $"{state.Name} can crawl now. " +
                    (reaction == Reaction.None ? "Try offering a familiar toy." : reactionText);
            else if (unlocked.UnlockedSitting)
                status.text = $"{state.Name} can sit up now. " +
                    (reaction == Reaction.None ? "Try playing with the toy." : reactionText);
            else
                status.text = reaction == Reaction.None ? DescribeNeeds(state) : reactionText;

            development.UpdateDetails(state, age.text, status.text);
            report.UpdateDetails(BabyReport.From(state));
            feeding.UpdateDetails(state, status.text);
        }

        private static string StageName(BabyState state)
        {
            if (state.CanWalk)
                return "Early walker";
            if (state.CanCrawl)
                return "Crawler";

            return state.CanSitAndReach ? "Early sitter" : "Newborn";
        }

        private static string ToyName(Toy toy) => toy == Toy.Plush ? "Plush" : "Rattle";

        private static string DescribeReaction(string name, Reaction reaction, Toy toy, Food food)
        {
            switch (reaction)
            {
                case Reaction.Satisfied: return $"{name} looks satisfied after their {FoodName(food).ToLowerInvariant()}.";
                case Reaction.Calmed: return $"{name} relaxes as you soothe them.";
                case Reaction.Rested: return $"{name} settled for a nap and woke up rested.";
                case Reaction.FreshDiaper: return $"{name} is clean and dry after a fresh diaper.";
                case Reaction.FreshBath: return $"{name} splashed in the bath and feels fresh and relaxed.";
                case Reaction.EnjoysStory: return $"{name} watches the pages as you read together.";
                case Reaction.StackedBlocks: return $"{name} beams at the block tower you built together.";
                case Reaction.NewSoundComforted: return $"{name} settles in your arms as the sound fades.";
                case Reaction.NewSoundExplored: return $"{name} listens closely as you explore the sound together.";
                case Reaction.TracksToy: return $"{name} follows the {ToyName(toy).ToLowerInvariant()} with their eyes.";
                case Reaction.ReachesForToy: return $"{name} sits up and reaches for the {ToyName(toy).ToLowerInvariant()}.";
                case Reaction.CrawlsToToy: return $"{name} crawls toward the {ToyName(toy).ToLowerInvariant()}.";
                case Reaction.WalksToToy: return $"{name} takes a few steps toward the {ToyName(toy).ToLowerInvariant()}.";
                default: return string.Empty;
            }
        }

        private static string DescribeNeeds(BabyState state)
        {
            const double low = 45;
            if (state.Health < low)
                return $"{state.Name} seems under the weather and could use some care.";
            if (state.Cleanliness < low)
                return $"{state.Name} could use a fresh diaper.";
            if (state.Energy < low && state.Energy <= state.Satiety && state.Energy <= state.Comfort)
                return $"{state.Name} rubs their eyes. It might be time for a nap.";
            if (state.Satiety < low && state.Satiety <= state.Comfort)
                return $"{state.Name} seems hungry.";
            if (state.Comfort < low)
                return $"{state.Name} is unsettled and might like some soothing.";
            return $"{state.Name} is looking around the nursery.";
        }

        private void ReportFailure(Exception error, string message)
        {
            Debug.LogException(error);
            if (goodbye.Root.activeSelf)
                goodbye.Feedback.text = message;
            else if (home.Root.activeSelf)
                home.Feedback.text = message;
            else if (blocks.Root.activeSelf)
                blocks.Feedback.text = message;
            else if (decor.Root.activeSelf)
                decor.Feedback.text = message;
            else if (style.Root.activeSelf)
                style.Feedback.text = message;
            else if (diaper.Root.activeSelf)
                diaper.Feedback.text = message;
            else if (hygiene.Root.activeSelf)
                hygiene.Feedback.text = message;
            else if (newSound.Root.activeSelf)
                newSound.Scene.text = message;
            else if (feeding.Root.activeSelf)
                feeding.Feedback.text = message;
            else if (development.Root.activeSelf)
                development.Feedback.text = message;
            else if (controls.activeSelf)
                status.text = message;
            else
                saveError.text = message;
        }

        private void BuildInterface()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var input = new GameObject("Nursery Input", typeof(EventSystem), typeof(StandaloneInputModule));
                input.transform.SetParent(transform, false);
            }

            var canvasObject = new GameObject("Nursery Overlay", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1120);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            safeArea = Box("Safe Area", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            FitSafeArea();

            var top = Tray("Header", true, 239);
            header = top.parent.gameObject;
            wake = Label("Header Label", top, "BAIby", 19, Fern, TextAnchor.MiddleLeft,
                new Vector2(0, 195), new Vector2(1, 1), new Vector2(0, -15));
            title = Label("Baby Name", top, "Your nursery", 48, Ink, TextAnchor.MiddleLeft,
                new Vector2(0, 135), new Vector2(0.56f, 1), new Vector2(0, -52), FontStyle.Bold);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 27;
            title.resizeTextMaxSize = 48;
            decorButton = ActionButton("Decor", top, 0.79f, 1, 143, 231, () => ShowPanel(NurseryPanel.Decor), Mist, Ink);
            decorButton.gameObject.SetActive(false);
            BuildStyleButton(top);
            BuildHouseholdChips(top);
            age = Label("Age", top, "", 22, Secondary, TextAnchor.MiddleLeft,
                new Vector2(0, 108), new Vector2(1, 1), new Vector2(0, -101));
            favorite = Label("Favorite Toy", top, "", 22, Fern, TextAnchor.MiddleRight,
                new Vector2(0, 108), Vector2.one, new Vector2(0, -101));
            satiety = Meter("Fullness", top, 0, 1f / 3, 61);
            comfort = Meter("Comfort", top, 1f / 3, 2f / 3, 61);
            energy = Meter("Energy", top, 2f / 3, 1, 61);
            cleanliness = Meter("Cleanliness", top, 0, 0.5f, 12);
            health = Meter("Health", top, 0.5f, 1, 12);

            var starter = Tray("Welcome", false, 250);
            welcome = starter.parent.gameObject;
            Label("Prompt", starter, "What’s your baby’s name?", 29, Ink, TextAnchor.MiddleLeft,
                new Vector2(0, 165), new Vector2(0.72f, 1), new Vector2(0, -24), FontStyle.Bold);
            backToHome = ActionButton("Back to home", starter, 0.73f, 1, 170, 230, ShowHome, Mist, Ink);
            backToHome.GetComponentInChildren<Text>().fontSize = 21;
            backToHome.gameObject.SetActive(false);
            var field = Box("Name", starter, Vector2.zero, Vector2.right, new Vector2(0, 104), new Vector2(0, 165));
            nameField = field.gameObject.AddComponent<InputField>();
            nameField.targetGraphic = Surface(field, Mist);
            nameField.targetGraphic.raycastTarget = true;
            nameField.characterLimit = 28;
            nameField.lineType = InputField.LineType.SingleLine;
            nameField.textComponent = Label("Entered Name", field, "", 28, Ink, TextAnchor.MiddleLeft,
                new Vector2(18, 0), Vector2.one, new Vector2(-18, 0));
            nameField.placeholder = Label("Placeholder", field, "Baby name", 28, Secondary,
                TextAnchor.MiddleLeft, new Vector2(18, 0), Vector2.one, new Vector2(-18, 0));
            adopt = ActionButton("Start raising", starter, 0, 1, 31, 94, Adopt, Fern, Color.white);
            adopt.interactable = false;
            saveError = Label("Save Message", starter, "", 17, Ink, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.right, new Vector2(0, 30));

            var panel = Tray("Care", false, 378);
            controls = panel.parent.gameObject;
            newSoundNotice = BuildNewSoundNotice(panel);
            status = Label("Reaction", panel, "", 25, Ink, TextAnchor.MiddleLeft,
                new Vector2(0, 283), Vector2.right, new Vector2(0, 365));
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 19;
            status.resizeTextMaxSize = 25;
            feedButton = ActionButton("Feed", panel, 0, 1f / 3, 195, 283,
                () => ShowPanel(NurseryPanel.Feeding), Fern, Color.white);
            ActionButton("Soothe", panel, 1f / 3, 2f / 3, 195, 283, () => Care(CareAction.Soothe), Fern, Color.white);
            ActionButton("Rest", panel, 2f / 3, 1, 195, 283, () => Care(CareAction.Rest), Fern, Color.white);
            washUpButton = ActionButton("Wash up", panel, 0, 1f / 3, 99, 187,
                () => ShowPanel(NurseryPanel.Hygiene), Fern, Color.white);
            ActionButton("Play with rattle", panel, 1f / 3, 2f / 3, 99, 187,
                () => Care(CareAction.Play, Toy.Rattle), Fern, Color.white);
            ActionButton("Play with plush", panel, 2f / 3, 1, 99, 187,
                () => Care(CareAction.Play, Toy.Plush), Fern, Color.white);
            ActionButton("Preview next day", panel, 0, 0.5f, 7, 91, NextPreviewDay, Mist, Ink);
            developmentButton = ActionButton("Development", panel, 0.5f, 1, 7, 91,
                () => ShowPanel(NurseryPanel.Development), Mist, Ink);
            development = new DevelopmentPanel(Tray("Development Panel", false, 360),
                () => ShowPanel(NurseryPanel.Care), () => Care(CareAction.ReadStory), OpenBlocks,
                () => ShowPanel(NurseryPanel.Report));
            report = new ReportPanel(Tray("Report Panel", false, 470), () => ShowPanel(NurseryPanel.Development));
            blocks = new StackBlocksPanel(Tray("Blocks Panel", false, 378),
                () => ShowPanel(NurseryPanel.Development), CompleteBlocks);
            feeding = new FeedingPanel(Tray("Feeding Panel", false, 360),
                () => ShowPanel(NurseryPanel.Care), food => Care(CareAction.Feed, food: food));
            newSound = new NewSoundPanel(Tray("New Sound Panel", false, 360),
                () => ShowPanel(NurseryPanel.Care), action => Care(action));
            hygiene = new HygienePanel(Tray("Hygiene Panel", false, 360),
                () => ShowPanel(NurseryPanel.Care), OpenDiaperExercise, () => Care(CareAction.Bath));
            diaper = new DiaperPanel(Tray("Diaper Panel", false, 378),
                () => ShowPanel(NurseryPanel.Hygiene), CompleteDiaper);
            decor = new DecorPanel(Tray("Decor Panel", false, 360),
                () => ShowPanel(NurseryPanel.Care), ChooseNurseryTheme);
            style = new StylePanel(Tray("Style Panel", false, 378), () => ShowPanel(NurseryPanel.Care), ChooseAppearance);
            BuildHome();
            controls.SetActive(false);
            satiety.Root.gameObject.SetActive(false);
            comfort.Root.gameObject.SetActive(false);
            energy.Root.gameObject.SetActive(false);
            health.Root.gameObject.SetActive(false);
            cleanliness.Root.gameObject.SetActive(false);
        }

        private enum NurseryPanel { Care, Development, Blocks, Report, Feeding, NewSound, Hygiene, Diaper, Decor, Style }

        private void ShowPanel(NurseryPanel next)
        {
            var previous = activePanel;
            activePanel = next;
            UpdatePanelVisibility(true);
            switch (next)
            {
                case NurseryPanel.Development:
                    var developmentFocus = previous switch
                    {
                        NurseryPanel.Blocks => development.StackBlocks,
                        NurseryPanel.Report => development.OpenReport,
                        _ => development.ReadStory
                    };
                    developmentFocus.Select();
                    break;
                case NurseryPanel.Report:
                    report.Done.Select();
                    break;
                case NurseryPanel.Blocks:
                    blocks.FirstBlock.Select();
                    break;
                case NurseryPanel.Feeding:
                    feeding.Milk.Select();
                    break;
                case NurseryPanel.NewSound:
                    newSound.Comfort.Select();
                    break;
                case NurseryPanel.Hygiene:
                    hygiene.Feedback.text = string.Empty;
                    hygiene.ChangeDiaper.Select();
                    break;
                case NurseryPanel.Diaper:
                    diaper.StartControl.Select();
                    break;
                case NurseryPanel.Decor:
                    decor.ChoiceFor(session.State.NurseryTheme).Select();
                    break;
                case NurseryPanel.Style:
                    style.Open(session.State.Appearance);
                    // Shows the failure note again if the saved look's art could not be built.
                    ApplyBabyAppearance(session.State.Appearance);
                    break;
                default:
                    switch (previous)
                    {
                        case NurseryPanel.Feeding: feedButton.Select(); break;
                        case NurseryPanel.Development: developmentButton.Select(); break;
                        case NurseryPanel.Diaper:
                        case NurseryPanel.Hygiene: washUpButton.Select(); break;
                        case NurseryPanel.Decor: decorButton.Select(); break;
                        case NurseryPanel.Style: styleButton.Select(); break;
                        case NurseryPanel.NewSound:
                            (newSoundNotice.gameObject.activeSelf ? newSoundNotice : feedButton).Select();
                            break;
                    }
                    break;
            }
        }

        private void UpdatePanelVisibility(bool adopted)
        {
            controls.SetActive(adopted && activePanel == NurseryPanel.Care);
            development.Root.SetActive(adopted && activePanel == NurseryPanel.Development);
            blocks.Root.SetActive(adopted && activePanel == NurseryPanel.Blocks);
            report.Root.SetActive(adopted && activePanel == NurseryPanel.Report);
            feeding.Root.SetActive(adopted && activePanel == NurseryPanel.Feeding);
            hygiene.Root.SetActive(adopted && activePanel == NurseryPanel.Hygiene);
            diaper.Root.SetActive(adopted && activePanel == NurseryPanel.Diaper);
            decor.Root.SetActive(adopted && activePanel == NurseryPanel.Decor);
            style.Root.SetActive(adopted && activePanel == NurseryPanel.Style);
            decorButton.gameObject.SetActive(adopted && activePanel == NurseryPanel.Care);
            styleButton.gameObject.SetActive(adopted && activePanel == NurseryPanel.Care);
            UpdateHouseholdChips(adopted && activePanel == NurseryPanel.Care);
            var hasPendingSound = adopted && session.State.HasPendingNewSound;
            newSound.Root.SetActive(hasPendingSound && activePanel == NurseryPanel.NewSound);
            UpdateNewSoundNotice(hasPendingSound);
        }

        private RectTransform Tray(string label, bool top, float height)
        {
            float edge = top ? 1 : 0;
            float bottom = top ? -height - 17 : 17;
            var root = Box(label, safeArea, new Vector2(0.5f, edge), new Vector2(0.5f, edge),
                new Vector2(-332, bottom), new Vector2(332, bottom + height));
            Surface(root, Paper);
            return Box("Content", root, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-20, 0));
        }

        private void FitSafeArea()
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen.x < 1 || screen.y < 1 || (area == previousSafeArea && screen == previousScreen))
                return;
            safeArea.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            safeArea.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            previousSafeArea = area;
            previousScreen = screen;
        }

        private static NeedMeter Meter(string label, Transform parent, float left, float right, float bottom)
        {
            var root = Box(label, parent, new Vector2(left, 0), new Vector2(right, 0),
                new Vector2(0, bottom), new Vector2(-12, bottom + 44));
            Label("Label", root, label, 20, Ink, TextAnchor.UpperLeft,
                new Vector2(0, 12), Vector2.one, Vector2.zero);
            var bar = Box("Bar", root, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 8));
            Surface(bar, Mist);
            var fill = Box("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Surface(fill, Fern);
            return new NeedMeter(root, fill);
        }

        private sealed class NeedMeter
        {
            public readonly RectTransform Root;
            private readonly RectTransform fill;

            public NeedMeter(RectTransform root, RectTransform fill)
            {
                Root = root;
                this.fill = fill;
            }

            public void Set(double value)
            {
                fill.anchorMax = new Vector2((float)(value / 100), 1);
            }
        }

        private static Button ActionButton(string label, Transform parent, float left, float right,
            float bottom, float top, Action action, Color background, Color foreground)
        {
            var root = Box(label, parent, new Vector2(left, 0), new Vector2(right, 0),
                new Vector2(3, bottom), new Vector2(-3, top));
            var image = Surface(root, background);
            image.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Label("Label", root, label, 25, foreground, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
            return button;
        }

        private static Image Surface(RectTransform target, Color color)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string objectName, Transform parent, string value, int size,
            Color color, TextAnchor alignment, Vector2 inset, Vector2 anchorMax, Vector2 end,
            FontStyle weight = FontStyle.Normal)
        {
            var root = Box(objectName, parent, Vector2.zero, anchorMax, inset, end);
            var text = root.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = weight;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static RectTransform Box(string label, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }
    }
}
