using System;
using System.Collections.Generic;
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
            petStage = PetVisuals.Create(nursery);
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
            FitInterface();
            FitRoutineStrip();
            FrameStage();
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
                case HouseholdView.MoreRooms:
                case HouseholdView.Settings:
                case HouseholdView.Journal:
                case HouseholdView.Wake:
                    ShowHome();
                    break;
                case HouseholdView.Helpers:
                    ShowSettings();
                    break;
                case HouseholdView.Gate:
                    CancelGate();
                    break;
                case HouseholdView.Home:
                    ShowActiveCompanion();
                    break;
                case HouseholdView.Adoption:
                    // The baby's name step goes back to the choice of a baby or a pet.
                    StartAdoption();
                    break;
                case HouseholdView.Choose:
                    if (household != null && household.Companions.Count > 0)
                        ShowHome();
                    break;
                case HouseholdView.PetAdoption:
                    PetAdoptionBack();
                    break;
                case HouseholdView.PetRoom:
                    if (petPanel != PetPanel.Care)
                        ShowPetPanel(petPanel == PetPanel.Journal ? PetPanel.Growth : PetPanel.Care);
                    break;
                default:
                    if (activePanel != NurseryPanel.Care)
                        ShowPanel(activePanel switch
                        {
                            NurseryPanel.Diaper => NurseryPanel.Hygiene,
                            NurseryPanel.Blocks or NurseryPanel.Report or NurseryPanel.Journal => NurseryPanel.Development,
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

            if (view == HouseholdView.PetRoom)
            {
                RefreshPetRoom();
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

            // A settled little one plays by themselves for a moment now and then.
            if (onScreen)
                Paint(Reaction.None, unlocked, idle: PlaysAlone(session.State));
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

            RememberMoment(result.Reaction);
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
            Toy? playedToy = null, Food food = Food.Milk, bool idle = false)
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
            // A shy skill (a setback) shows the stage before it; the walking-again celebration plays the skill.
            var shown = idle && reaction == Reaction.None ? Reaction.TracksToy : VisualReaction(state, reaction);
            nursery.Present(adopted, state?.ShowsSitting ?? false, shown, visibleToy, ambientToy,
                canCrawl: state?.ShowsCrawling ?? false, food: food, canWalk: state?.ShowsWalking ?? false);
            nursery.SetCondition(state?.Care.Mood ?? CareMood.Content, state?.Care.Mess ?? 0,
                state?.Care.Condition ?? CareCondition.Well);
            // After Present, so the look is built first for the stage that is actually shown.
            ApplyBabyAppearance(state?.Appearance ?? BabyAppearance.Default);

            if (!adopted)
            {
                title.text = household != null && household.Companions.Count > 0 ? "A new arrival" : "Your nursery";
                wake.text = "BAIby";
                age.text = string.Empty;
                favorite.text = string.Empty;
                LayoutTopStack();
                return;
            }

            title.text = state.Name;
            // The day line sits where this label would; an adopted companion's header shows only the day line.
            wake.text = string.Empty;
            age.text = DayLine(state.Age, StageName(state));
            careTitle.text = "Care for " + state.Name;
            favorite.text = state.FavoriteToy is Toy favored ? $"Favorite: {ToyName(favored)}" : string.Empty;
            satiety.Set(state.Satiety);
            comfort.Set(state.Comfort);
            energy.Set(state.Energy);
            health.Set(state.Health);
            cleanliness.Set(state.Cleanliness);
            ApplyBabyFloors(state);
            var reactionText = DescribeHealthReaction(state, reaction) ?? DescribeGrowthReaction(state, reaction) ??
                DescribeReaction(state.Name, reaction, visibleToy, food);
            // What the helpers did comes first; the return summary waits in the bubble until the player cares.
            var slot = household?.Active?.Slot ?? 0;
            var helperLine = TakeHelperLine(slot, state.Name, false);
            if (reaction != Reaction.None)
            {
                ClearHelperNote();
                if (helperLine != null)
                    reactionText = helperLine + " " + reactionText;
            }
            // Milestones are celebrated as before, and each has a page in the journal now. A new sticker waits for a
            // refresh without a milestone to celebrate.
            var milestone = unlocked.UnlockedSitting || unlocked.UnlockedCrawling || unlocked.UnlockedWalking;
            var sticker = milestone ? null : StickerLine(state);
            if (unlocked.UnlockedWalking)
                status.text = $"{state.Name} can take a few steps now, and it's in the journal. " +
                    (reaction == Reaction.None ? "Try offering a familiar toy." : reactionText);
            else if (unlocked.UnlockedCrawling)
                status.text = $"{state.Name} can crawl now, and it's in the journal. " +
                    (reaction == Reaction.None ? "Try offering a familiar toy." : reactionText);
            else if (unlocked.UnlockedSitting)
                status.text = $"{state.Name} can sit up now, and it's in the journal. " +
                    (reaction == Reaction.None ? "Try playing with the toy." : reactionText);
            else if (sticker != null)
                status.text = sticker;
            else
                status.text = reaction == Reaction.None
                    ? HelperNoteFor(slot, DescribeCondition(state)) ?? DescribeCondition(state) ??
                      DescribeNeeds(state, TemperamentMoment(state))
                    : reactionText;

            UpdateRoutineSlots(session.Routine, state.Care.Preset);
            routine.UpdateDetails(state, DescribeCondition(state) ??
                $"{state.Name} is settled. Meals, play, bed and a wash each day keep the routine.");
            UpdateRoutineHelpers(state.Name, false, session.FeederStock, session.FeederCapacity);
            var growth = session.Growth;
            development.UpdateDetails(state, age.text, status.text, growth, session.Bonding);
            report.UpdateDetails(BabyReport.From(state), growth,
                TemperamentLine(session.Security, session.Curiosity, state.Bond));
            UpdatePhotoTile();
            feeding.UpdateDetails(state, status.text);
            UpdateHealthControls(state, reaction, reactionText);
            UpdateMood(state.Care.Mood);
            LayoutTopStack();
            LayoutCareSheet();
        }

        // "Day 1" is the day a little one arrives.
        private static string DayLine(TimeSpan age, string stage) => $"Day {(int)age.TotalDays + 1} · {stage}";

        // The same, stage first, for Home cards: "Early sitter · Day 5".
        private static string StageDay(string stage, TimeSpan age) => $"{stage} · Day {(int)age.TotalDays + 1}";

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
                case Reaction.PushesToyAway: return $"{name} is too upset to play and pushes it away. Soothing will help.";
                case Reaction.TooFull:
                    return $"{name} is too full for the {FoodName(food).ToLowerInvariant()} and spits up a little. Try again later.";
                default: return string.Empty;
            }
        }

        // What the baby needs, or (when nothing does) where temperament stands, or a quiet line.
        private static string DescribeNeeds(BabyState state, string temperament = null)
        {
            const double low = 45;
            if (state.Health < low)
                return $"{state.Name} looks a little pale and could use some care.";
            if (state.Cleanliness < low)
                return $"{state.Name} could use a fresh diaper.";
            if (state.Energy < low && state.Energy <= state.Satiety && state.Energy <= state.Comfort)
                return $"{state.Name} rubs their eyes. It might be time for a nap.";
            if (state.Satiety < low && state.Satiety <= state.Comfort)
                return $"{state.Name} seems hungry.";
            if (state.Comfort < low)
                return $"{state.Name} is unsettled and might like some soothing.";
            return temperament ?? $"{state.Name} is looking around the nursery.";
        }

        private void ReportFailure(Exception error, string message)
        {
            Debug.LogException(error);
            if (HouseholdFeedback() is Text householdFeedback)
                householdFeedback.text = message;
            else if (goodbye.Root.activeSelf)
                goodbye.Feedback.text = message;
            else if (home.Root.activeSelf)
                home.Feedback.text = message;
            else if (blocks.Root.activeSelf)
                blocks.Feedback.text = message;
            else if (decor.Root.activeSelf)
                decor.Feedback.text = message;
            else if (routine.Root.activeSelf)
                routine.Summary.text = message;
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
            else if (journal.Root.activeSelf)
                journal.Feedback.text = message;
            else if (doctor.Root.activeSelf)
                doctor.Feedback.text = message;
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

            BuildHeader();

            var starter = Tray("Welcome", false, 300);
            welcome = starter.parent.gameObject;
            var prompt = Label("Prompt", starter, "What’s your baby’s name?", 36, Ink, TextAnchor.MiddleLeft,
                new Vector2(0, 222), new Vector2(0.7f, 1), new Vector2(0, -6), FontStyle.Bold);
            FitText(prompt, 26, 36);
            backToHome = ActionButton("Back to home", starter, 0.72f, 1, 222, 294, ShowHome, Mist, Ink);
            backToHome.GetComponentInChildren<Text>().text = "Home";
            backToHome.gameObject.SetActive(false);
            var field = Box("Name", starter, Vector2.zero, Vector2.right, new Vector2(3, 126), new Vector2(-3, 210));
            nameField = field.gameObject.AddComponent<InputField>();
            nameField.targetGraphic = TextField(field);
            nameField.characterLimit = 28;
            nameField.lineType = InputField.LineType.SingleLine;
            nameField.textComponent = Label("Entered Name", field, "", 30, Ink, TextAnchor.MiddleLeft,
                new Vector2(26, 0), Vector2.one, new Vector2(-26, 0), FontStyle.Bold);
            nameField.placeholder = Label("Placeholder", field, "Baby name", 30, Secondary,
                TextAnchor.MiddleLeft, new Vector2(26, 0), Vector2.one, new Vector2(-26, 0));
            adopt = ActionButton("Start raising", starter, 0, 1, 34, 114, Adopt, Fern, Color.white);
            adopt.interactable = false;
            saveError = Label("Save Message", starter, "", 20, Ink, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.right, new Vector2(0, 32));

            var panel = Tray("Care", false, 378);
            controls = panel.parent.gameObject;
            BuildCareSheet(panel);
            development = new DevelopmentPanel(Tray("Development Panel", false, DevelopmentPanel.Height),
                CloseGrowth, () => Care(CareAction.ReadStory), OpenBlocks,
                () => ShowPanel(NurseryPanel.Report), OpenGrowthJournal);
            report = new ReportPanel(Tray("Report Panel", false, ReportPanel.Height), () => ShowPanel(NurseryPanel.Development));
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
            BuildHealth(panel);
            BuildRoutine();
            BuildHome();
            controls.SetActive(false);
            satiety.Root.gameObject.SetActive(false);
            comfort.Root.gameObject.SetActive(false);
            energy.Root.gameObject.SetActive(false);
            health.Root.gameObject.SetActive(false);
            cleanliness.Root.gameObject.SetActive(false);
            SettleText(canvas.transform);
            LayoutTopStack();
            LayoutCareSheet();
        }

        private enum NurseryPanel { Care, Development, Blocks, Report, Feeding, NewSound, Hygiene, Diaper, Decor, Style, Routine, Doctor, Journal }

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
                        NurseryPanel.Journal => development.OpenJournal,
                        _ => development.ReadStory
                    };
                    developmentFocus.Select();
                    break;
                case NurseryPanel.Report:
                    report.Done.Select();
                    break;
                case NurseryPanel.Journal:
                    journal.Close.Select();
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
                case NurseryPanel.Routine:
                    (session.State.Care.Style == CareStyle.Gentle ? routine.GentleButton : routine.StandardButton).Select();
                    break;
                case NurseryPanel.Doctor:
                    (doctor.Visit.gameObject.activeSelf ? doctor.Visit : doctor.Done).Select();
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
                        case NurseryPanel.Routine: RoutineOpener.Select(); break;
                        case NurseryPanel.Doctor:
                            (healthButton.gameObject.activeSelf && healthButton.interactable ? healthButton : developmentButton).Select();
                            break;
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
            routine.Root.SetActive(adopted && activePanel == NurseryPanel.Routine);
            doctor.Root.SetActive(adopted && activePanel == NurseryPanel.Doctor);
            // The journal opens from Growth here, or from Home for any companion.
            journal.Root.SetActive(view == HouseholdView.Journal || (adopted && activePanel == NurseryPanel.Journal));
            routineStripWanted = adopted && (activePanel == NurseryPanel.Care || activePanel == NurseryPanel.Routine);
            decorButton.gameObject.SetActive(adopted && activePanel == NurseryPanel.Care);
            styleButton.gameObject.SetActive(adopted && activePanel == NurseryPanel.Care);
            UpdateHouseholdChips(adopted && activePanel == NurseryPanel.Care);
            var hasPendingSound = adopted && session.State.HasPendingNewSound;
            newSound.Root.SetActive(hasPendingSound && activePanel == NurseryPanel.NewSound);
            UpdateNewSoundNotice(hasPendingSound);
            UpdateRoutineEntry();
            LayoutTopStack();
            LayoutCareSheet();
        }

        // A bottom sheet as wide as a phone screen: rounded top corners, a soft shadow above, a grabber, and a
        // Content box height units tall whose bottom sits SheetBottomPad above the safe area's bottom edge.
        private RectTransform Tray(string label, bool top, float height)
        {
            var root = Box(label, safeArea, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-360, 0), new Vector2(360, height + SheetChrome));
            sheets.Add(root);
            Sheet(root);
            return Box("Content", root, Vector2.zero, Vector2.one, new Vector2(SideMargin, SheetBottomPad),
                new Vector2(-SideMargin, -SheetTopPad));
        }

        // Every bottom sheet, so the stage can keep the baby or pet above whichever one is open.
        private readonly List<RectTransform> sheets = new List<RectTransform>();
        private readonly Vector3[] stageCorners = new Vector3[4];

        // Tells the stage which part of the screen is open: above the tallest open sheet and below the header
        // cards and routine strip. The overlay canvas reports corners in screen pixels.
        private void FrameStage()
        {
            if (nursery == null || Screen.height < 1)
                return;

            var bottom = 0f;
            foreach (var sheet in sheets)
            {
                if (sheet == null || !sheet.gameObject.activeInHierarchy)
                    continue;
                sheet.GetWorldCorners(stageCorners);
                bottom = Mathf.Max(bottom, stageCorners[1].y);
            }

            // A pet is small beside its speech bubble, so its room keeps the pet above the bubble too.
            if (petBubbleRect != null && petBubbleRect.gameObject.activeInHierarchy)
            {
                petBubbleRect.GetWorldCorners(stageCorners);
                bottom = Mathf.Max(bottom, stageCorners[1].y);
            }

            var top = (float)Screen.height;
            if (header != null && header.activeInHierarchy)
            {
                ((RectTransform)header.transform).GetWorldCorners(stageCorners);
                top = Mathf.Min(top, stageCorners[0].y);
            }

            if (routineStrip != null && routineStrip.Root.activeInHierarchy)
            {
                ((RectTransform)routineStrip.Root.transform).GetWorldCorners(stageCorners);
                top = Mathf.Min(top, stageCorners[0].y);
            }

            nursery.SetOpenBand(bottom / Screen.height, top / Screen.height);
        }

        private static void Sheet(RectTransform root)
        {
            var below = Box("Sheet Shadow", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SoftShapes.Shadow(Surface(below, WithAlpha(ShadowInk, 0.14f)), Px(28), below, -Px(8));
            // Fills the space under the safe area, such as a phone's home indicator, in the sheet's white.
            Surface(Box("Sheet Bleed", root, Vector2.zero, Vector2.right, new Vector2(0, -600), new Vector2(0, 60)), Paper);
            SoftShapes.TopRadius(Surface(Box("Sheet Surface", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                Paper), Px(28));
            var grabber = Box("Grabber", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-Px(20), -Px(10) - Px(5)), new Vector2(Px(20), -Px(10)));
            SoftShapes.Pill(Surface(grabber, Divider));
        }

        // A rounded entry field on the soft neutral ground.
        private static Image TextField(RectTransform field)
        {
            var image = Surface(field, Mist);
            image.raycastTarget = true;
            SoftShapes.Radius(image, Px(16));
            return image;
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

        // A ring meter: the value in the middle, the label below. Bar is the track and Bar/Fill sweeps clockwise
        // from the top. The root is named for the need; label is what it reads.
        private static NeedMeter Meter(string objectName, Transform parent, string label, Color color)
        {
            var root = Box(objectName, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var bar = Box("Bar", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            SoftShapes.Ring(Surface(bar, RingTrack), false);
            var fill = Surface(Box("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), color);
            SoftShapes.Ring(fill, true);
            var value = Label("Value", bar, "", 24, Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero,
                FontStyle.Bold);
            SetRole(value, TextRole.Heading);
            var name = Label("Label", root, label, 20, Muted, TextAnchor.MiddleCenter, Vector2.zero, Vector2.right,
                Vector2.zero, FontStyle.Bold);
            FitText(name, 15, 20);
            return new NeedMeter(root, bar, fill, value, name);
        }

        private sealed class NeedMeter
        {
            public readonly RectTransform Root;
            private readonly RectTransform bar;
            private readonly Image fill;
            private readonly Text value;

            public NeedMeter(RectTransform root, RectTransform bar, Image fill, Text value, Text label)
            {
                Root = root;
                this.bar = bar;
                this.fill = fill;
                this.value = value;
                Label = label;
            }

            public Text Label { get; }

            public void Tint(Color color) => fill.color = color;

            private Image floor;

            // A small dot on the ring at a helper's floor while that helper is active; null hides it.
            public void SetFloor(double? value)
            {
                if (value == null)
                {
                    if (floor != null)
                        floor.gameObject.SetActive(false);
                    return;
                }

                if (floor == null)
                {
                    floor = Surface(Box("Helper floor", bar, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero),
                        WithAlpha(Ink, 0.55f));
                    SoftShapes.Disc(floor);
                    floor.raycastTarget = false;
                }

                // The ring sweeps clockwise from the top; the dot sits on the middle of its stroke.
                const float stroke = 0.86f;
                var angle = Mathf.Clamp01((float)(value.Value / 100)) * 2 * Mathf.PI;
                var at = new Vector2(0.5f + 0.5f * stroke * Mathf.Sin(angle), 0.5f + 0.5f * stroke * Mathf.Cos(angle));
                var rect = floor.rectTransform;
                rect.anchorMin = at;
                rect.anchorMax = at;
                rect.offsetMin = new Vector2(-5, -5);
                rect.offsetMax = new Vector2(5, 5);
                floor.gameObject.SetActive(true);
            }

            public void Set(double amount)
            {
                fill.fillAmount = Mathf.Clamp01((float)(amount / 100));
                value.text = Mathf.RoundToInt((float)amount).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            // Fills one column of the needs card: the ring at the top, the label under it.
            public void Place(float left, float right, float ring, float top, float labelHeight)
            {
                Root.anchorMin = new Vector2(left, 0);
                Root.anchorMax = new Vector2(right, 1);
                Root.offsetMin = Vector2.zero;
                Root.offsetMax = Vector2.zero;
                bar.offsetMin = new Vector2(-ring / 2, -top - ring);
                bar.offsetMax = new Vector2(ring / 2, -top);
                var name = Label.rectTransform;
                name.anchorMin = new Vector2(0, 1);
                name.anchorMax = new Vector2(1, 1);
                name.offsetMin = new Vector2(2, -top - ring - 4 - labelHeight);
                name.offsetMax = new Vector2(-2, -top - ring - 4);
            }
        }

        // A rounded button: primary sage (white text), soft neutral, or outlined white.
        private static Button ActionButton(string label, Transform parent, float left, float right,
            float bottom, float top, Action action, Color background, Color foreground)
        {
            var root = Box(label, parent, new Vector2(left, 0), new Vector2(right, 0),
                new Vector2(3, bottom), new Vector2(-3, top));
            var image = Surface(root, background);
            image.raycastTarget = true;
            if (background == Paper)
                SoftShapes.Outline(Surface(Box("Border", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), Outline));
            SoftShapes.Radius(image, Px(16));
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = Pressable();
            button.onClick.AddListener(() => action());
            Label("Label", root, label, 26, foreground, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
            return button;
        }

        // A square icon button on the soft neutral ground, 44 px on the phone target, as in the header.
        private static Button IconButton(string objectName, Transform parent, string icon, Action action,
            Color background)
        {
            var root = Box(objectName, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var image = Surface(root, background);
            image.raycastTarget = true;
            SoftShapes.Radius(image, Px(16));
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = Pressable();
            button.onClick.AddListener(() => action());
            IconAt(objectName + " Icon", root, icon, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, Px(22));
            return button;
        }

        private static ColorBlock Pressable()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1);
            colors.selectedColor = new Color(0.97f, 0.97f, 0.97f, 1);
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1);
            colors.disabledColor = new Color(1, 1, 1, 0.5f);
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static Image Surface(RectTransform target, Color color)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // Bold labels of 30 units and up are headings (Fredoka); other bold labels are strong (Nunito ExtraBold);
        // the rest are body text (Nunito Bold).
        private static Text Label(string objectName, Transform parent, string value, int size,
            Color color, TextAnchor alignment, Vector2 inset, Vector2 anchorMax, Vector2 end,
            FontStyle weight = FontStyle.Normal)
        {
            var root = Box(objectName, parent, Vector2.zero, anchorMax, inset, end);
            var text = root.gameObject.AddComponent<Text>();
            SetRole(text, weight == FontStyle.Normal ? TextRole.Body : size >= 30 ? TextRole.Heading : TextRole.Strong);
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.fontSize = size;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
