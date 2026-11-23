using System;
using System.Collections.Generic;
using System.Globalization;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // The routine strip under the header (today's care windows) and the Routine panel it opens: the care
    // style and Away mode. Copy stays calm: it names what helps, never blames.
    //
    // The world baby keeps its size while the canvas follows the screen's aspect, so the strip shows only
    // while it ends above the tallest pose (NurseryVisuals.PoseCeilingScreenY). On shorter screens (a phone
    // browser with its toolbars, a notched phone, a desktop window) it folds into a Routine button in the
    // Care tray's status row that names the next window to look after.
    public sealed partial class NurseryPreview
    {
        private static readonly Color Honey = new Color32(240, 214, 160, 255);
        private static readonly Color Rose = new Color32(236, 207, 198, 255);
        private static readonly Color Leaf = new Color32(208, 226, 202, 255);
        private RoutineStripView routineStrip;
        private RoutinePanel routine;
        private Button routineButton;
        private Text routineButtonLabel;
        private bool routineStripWanted;
        private bool routineStripFits = true;
        private readonly Vector3[] stripCorners = new Vector3[4];

        // The control that opened the Routine panel, for focus when it closes.
        private Selectable RoutineOpener => routineStripFits ? routineStrip.Open : (Selectable)routineButton;

        private void BuildRoutine()
        {
            // A slim 52-unit tray just below the header. On a 390 x 844 screen it ends above the tallest pose's head;
            // where it would not (FitRoutineStrip), the Routine button below stands in for it.
            var root = Box("Routine Strip", safeArea, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-332, -314), new Vector2(332, -262));
            routineStrip = new RoutineStripView(root, () => ShowPanel(NurseryPanel.Routine));
            routineButton = ActionButton("Routine", status.transform.parent, 0.73f, 1, 291, 361,
                () => ShowPanel(NurseryPanel.Routine), Mist, Ink);
            routineButton.name = "Open routine";
            routineButtonLabel = routineButton.GetComponentInChildren<Text>();
            routineButtonLabel.rectTransform.offsetMin = new Vector2(8, 0);
            routineButtonLabel.rectTransform.offsetMax = new Vector2(-8, 0);
            routineButtonLabel.resizeTextForBestFit = true;
            routineButtonLabel.resizeTextMinSize = 15;
            routineButtonLabel.resizeTextMaxSize = 22;
            SoftShapes.Round(routineButton.image, 0.7f);
            routineButton.gameObject.SetActive(false);
            routine = new RoutinePanel(Tray("Routine Panel", false, 378), () => ShowPanel(NurseryPanel.Care),
                ChooseCareStyle, StartAway, EndAway);
        }

        // Checks every frame, since the safe area, the window and the browser's toolbars can all change.
        private void FitRoutineStrip()
        {
            if (routineStrip == null || nursery == null)
                return;

            // A Screen Space Overlay canvas places its corners in screen pixels, like the camera projection.
            ((RectTransform)routineStrip.Root.transform).GetWorldCorners(stripCorners);
            var fits = stripCorners[0].y >= nursery.PoseCeilingScreenY;
            if (fits == routineStripFits)
                return;

            var focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject == RoutineOpener.gameObject;
            routineStripFits = fits;
            UpdateRoutineEntry();
            if (focused && RoutineOpener.gameObject.activeInHierarchy)
                RoutineOpener.Select();
        }

        // The strip, or the Routine button standing in for it. A pending new sound fills the status row for a
        // moment, so the button waits for it.
        private void UpdateRoutineEntry()
        {
            routineStrip.Root.SetActive(routineStripWanted && routineStripFits);
            var folded = !routineStripFits && !newSoundNotice.gameObject.activeSelf;
            routineButton.gameObject.SetActive(folded);
            status.rectTransform.anchorMax = new Vector2(folded ? 0.72f : 1, 0);
        }

        private void UpdateRoutineSlots(IReadOnlyList<RoutineSlot> slots, RoutinePreset preset)
        {
            routineStrip.UpdateSlots(slots, preset);
            routineButtonLabel.text = FoldedLabel(slots, preset);
        }

        // "Routine", or the first window that is late or due now, such as "Snack late" or "Breakfast now".
        private static string FoldedLabel(IReadOnlyList<RoutineSlot> slots, RoutinePreset preset)
        {
            foreach (var wanted in new[] { RoutineSlotState.Overdue, RoutineSlotState.Due })
            {
                foreach (var slot in slots)
                {
                    if (slot.State == wanted && slot.Kind != RoutineSlotKind.Play && slot.Kind != RoutineSlotKind.Wash)
                        return SlotName(slot.Kind, preset) + (wanted == RoutineSlotState.Overdue ? " late" : " now");
                }
            }

            return "Routine";
        }

        private void ChooseCareStyle(CareStyle choice)
        {
            try
            {
                session.SetCareStyle(choice);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The care style couldn't be saved. Please try again.");
                return;
            }

            Paint(Reaction.None);
        }

        private void StartAway(TimeSpan duration)
        {
            try
            {
                Paint(Reaction.None, session.StartAway(duration));
            }
            catch (Exception error)
            {
                ReportFailure(error, "Away mode couldn't be saved. Please try again.");
                return;
            }

            routine.EndAwayButton.Select();
        }

        private void EndAway()
        {
            try
            {
                Paint(Reaction.None, session.EndAway());
            }
            catch (Exception error)
            {
                ReportFailure(error, "Away mode couldn't be saved. Please try again.");
                return;
            }

            routine.FirstAwayChoice.Select();
        }

        // What the baby's mood, the room and Away mode ask for, or null when the needs read-out says enough.
        private static string DescribeCondition(BabyState state)
        {
            var care = state.Care;
            if (care.IsAway)
                return $"{state.Name} is fine while you're away. Needs stay steady until you're back.";

            switch (care.Mood)
            {
                case CareMood.Upset:
                    return state.Satiety < 50
                        ? $"{state.Name} is upset and crying. A feed first, then soothing, will help."
                        : $"{state.Name} is upset and crying. Soothing will help.";
                case CareMood.Fussy:
                    if ((care.Owed & CareTags.Meal) != 0)
                        return $"{state.Name} is fussing for a missed meal. A feed will help.";
                    if ((care.Owed & CareTags.Bedtime) != 0)
                        return $"{state.Name} is fussing after missing bedtime. A rest will help.";
                    if (care.MoodCause == CareMoodCause.MealOverdue)
                        return $"{state.Name} is fussing. It's past mealtime.";
                    return $"{state.Name} is fussing. {DescribeNeeds(state)}";
                default:
                    return care.IsMessy ? "The nursery is getting messy. Wash up to tidy it." : null;
            }
        }

        private static string SlotName(RoutineSlotKind kind, RoutinePreset preset)
        {
            switch (kind)
            {
                case RoutineSlotKind.MorningMeal: return "Breakfast";
                case RoutineSlotKind.AfternoonMeal: return preset == RoutinePreset.Home ? "Lunch" : "Snack";
                case RoutineSlotKind.EveningMeal: return "Dinner";
                case RoutineSlotKind.Play: return "Play";
                case RoutineSlotKind.Bedtime: return "Bed";
                default: return "Wash";
            }
        }

        private sealed class RoutineStripView
        {
            public GameObject Root { get; }
            public Button Open { get; }
            private readonly Image[] chips = new Image[6];
            private readonly Text[] names = new Text[6];
            private readonly Text[] states = new Text[6];

            public RoutineStripView(RectTransform root, Action open)
            {
                Root = root.gameObject;
                var surface = Surface(root, Paper);
                surface.raycastTarget = true;
                Open = Root.AddComponent<Button>();
                Open.targetGraphic = surface;
                Open.onClick.AddListener(() => open());
                var content = Box("Routine Chips", root, Vector2.zero, Vector2.one, new Vector2(10, 5), new Vector2(-10, -5));
                for (var i = 0; i < chips.Length; i++)
                {
                    var chip = Box("Routine Chip " + (i + 1), content, new Vector2(i / 6f, 0), new Vector2((i + 1) / 6f, 1),
                        new Vector2(3, 0), new Vector2(-3, 0));
                    chips[i] = Surface(chip, Mist);
                    SoftShapes.Round(chips[i], 1.6f);
                    names[i] = Label("Routine Chip Name " + (i + 1), chip, "", 17, Ink, TextAnchor.MiddleCenter,
                        new Vector2(2, 20), Vector2.one, new Vector2(-2, -1), FontStyle.Bold);
                    states[i] = Label("Routine Chip State " + (i + 1), chip, "", 16, Secondary, TextAnchor.MiddleCenter,
                        new Vector2(2, 1), Vector2.right, new Vector2(-2, 21));
                    foreach (var text in new[] { names[i], states[i] })
                    {
                        text.resizeTextForBestFit = true;
                        text.resizeTextMinSize = 12;
                        text.resizeTextMaxSize = text.fontSize;
                    }
                }

                Root.SetActive(false);
            }

            public void UpdateSlots(IReadOnlyList<RoutineSlot> slots, RoutinePreset preset)
            {
                for (var i = 0; i < chips.Length; i++)
                {
                    var shown = i < slots.Count;
                    chips[i].gameObject.SetActive(shown);
                    if (!shown)
                        continue;

                    var slot = slots[i];
                    names[i].text = SlotName(slot.Kind, preset);
                    states[i].text = StateText(slot);
                    var (background, foreground) = Colors(slot.State);
                    chips[i].color = background;
                    names[i].color = foreground == Color.white ? foreground : Ink;
                    states[i].color = foreground;
                    chips[i].name = "Routine " + names[i].text;
                }
            }

            private static string StateText(RoutineSlot slot)
            {
                switch (slot.State)
                {
                    case RoutineSlotState.Later: return slot.Open.ToString("HH:mm", CultureInfo.InvariantCulture);
                    case RoutineSlotState.Due: return slot.Kind == RoutineSlotKind.Play ? "Today" : "Now";
                    case RoutineSlotState.Overdue: return "Late";
                    case RoutineSlotState.OnTime: return "On time";
                    case RoutineSlotState.DoneLate: return "Done late";
                    case RoutineSlotState.Missed: return "Missed";
                    case RoutineSlotState.Away: return "Away";
                    default: return "Free";
                }
            }

            private static (Color Background, Color Foreground) Colors(RoutineSlotState state)
            {
                switch (state)
                {
                    case RoutineSlotState.Due: return (Fern, Color.white);
                    case RoutineSlotState.Overdue: return (Honey, Ink);
                    case RoutineSlotState.Missed: return (Rose, Ink);
                    case RoutineSlotState.OnTime:
                    case RoutineSlotState.DoneLate: return (Leaf, Fern);
                    case RoutineSlotState.Later: return (Mist, Secondary);
                    default: return (Paper, Secondary);
                }
            }
        }

        private sealed class RoutinePanel
        {
            private static readonly (string Label, double Days)[] AwayChoices =
            {
                ("1 day", 1), ("3 days", 3), ("1 week", 7), ("2 weeks", 14),
            };

            public GameObject Root { get; }
            public Text Summary { get; }
            public Button FirstAwayChoice { get; }
            public Button EndAwayButton { get; }
            public Button GentleButton { get; }
            public Button StandardButton { get; }
            private readonly GameObject awayChoices;
            private readonly Text awayLabel;

            public RoutinePanel(RectTransform content, Action close, Action<CareStyle> chooseStyle,
                Action<TimeSpan> startAway, Action endAway)
            {
                Root = content.parent.gameObject;
                Label("Routine Heading", content, "Routine", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 283), new Vector2(0.72f, 0), new Vector2(0, 369), FontStyle.Bold);
                var done = ActionButton("Done", content, 0.73f, 1, 283, 371, close, Mist, Ink);
                done.name = "Close routine";

                Summary = Label("Routine Summary", content, "", 22, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 228), Vector2.right, new Vector2(0, 281));
                Summary.resizeTextForBestFit = true;
                Summary.resizeTextMinSize = 17;
                Summary.resizeTextMaxSize = 22;

                Label("Routine Style Label", content, "Care style", 21, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, 150), new Vector2(0.3f, 0), new Vector2(0, 222));
                GentleButton = ActionButton("Gentle", content, 0.3f, 0.65f, 150, 222,
                    () => chooseStyle(CareStyle.Gentle), Mist, Ink);
                GentleButton.name = "Care style Gentle";
                StandardButton = ActionButton("Standard", content, 0.65f, 1, 150, 222,
                    () => chooseStyle(CareStyle.Standard), Mist, Ink);
                StandardButton.name = "Care style Standard";

                awayLabel = Label("Routine Away Label", content, "", 20, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, 100), Vector2.right, new Vector2(0, 146));
                awayLabel.resizeTextForBestFit = true;
                awayLabel.resizeTextMinSize = 16;
                awayLabel.resizeTextMaxSize = 20;
                var choices = Box("Away Choices", content, Vector2.zero, Vector2.right, new Vector2(0, 8), new Vector2(0, 94));
                awayChoices = choices.gameObject;
                for (var i = 0; i < AwayChoices.Length; i++)
                {
                    var days = AwayChoices[i].Days;
                    var choice = ActionButton(AwayChoices[i].Label, choices, i / 4f, (i + 1) / 4f, 0, 86,
                        () => startAway(TimeSpan.FromDays(days)), Fern, Color.white);
                    choice.name = "Away " + AwayChoices[i].Label;
                    if (i == 0)
                        FirstAwayChoice = choice;
                }

                EndAwayButton = ActionButton("We're back", content, 0, 1, 8, 94, endAway, Fern, Color.white);
                EndAwayButton.name = "End away";
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyState state, string summary)
            {
                var care = state.Care;
                Summary.text = summary;
                Select(GentleButton, care.Style == CareStyle.Gentle);
                Select(StandardButton, care.Style != CareStyle.Gentle);
                awayChoices.SetActive(!care.IsAway);
                EndAwayButton.gameObject.SetActive(care.IsAway);
                awayLabel.text = care.IsAway
                    ? "Away until " + care.AwayUntil.ToOffset(care.Offset).ToString("ddd HH:mm", CultureInfo.InvariantCulture) +
                      ". Needs stay steady and nothing is missed."
                    : $"Going away? Away mode keeps {state.Name}'s needs steady for up to 2 weeks.";
            }

            private static void Select(Button button, bool selected)
            {
                button.image.color = selected ? Fern : Mist;
                button.GetComponentInChildren<Text>().color = selected ? Color.white : Ink;
            }
        }
    }
}
