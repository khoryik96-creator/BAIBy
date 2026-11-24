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
    // style and Away mode. Copy stays calm: it names what helps, never blames. A cat's room (care slice 7) shares the
    // strip (breakfast, play, dinner, nap and brush) and the panel.
    //
    // The world baby keeps its size while the canvas follows the screen's aspect, so the strip shows only
    // while it ends above the tallest pose (NurseryVisuals.PoseCeilingScreenY). On shorter screens (a phone
    // browser with its toolbars, a notched phone, a desktop window) it folds into a Routine button in the
    // Care tray's status row that names the next window to look after.
    public sealed partial class NurseryPreview
    {
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
            // A slim dot timeline under the needs card (LayoutTopStack places it). On a 390 x 844 screen it ends
            // above the tallest pose's head; where it would not (FitRoutineStrip), the Routine button in the care
            // sheet's bottom row stands in for it.
            var root = Box("Routine Strip", safeArea, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-StackWidth / 2, -314), new Vector2(StackWidth / 2, -262));
            routineStrip = new RoutineStripView(root, OpenRoutine);
            routineButton = ActionButton("Routine", previewDayButton.transform.parent, 0, 1, 0, 0,
                () => ShowPanel(NurseryPanel.Routine), Mist, Ink);
            routineButton.name = "Open routine";
            routineButton.transform.SetSiblingIndex(developmentButton.transform.GetSiblingIndex());
            WithIcon(routineButton, "Routine", "Routine", Ink);
            routineButtonLabel = routineButton.GetComponentInChildren<Text>();
            routineButton.gameObject.SetActive(false);
            routine = new RoutinePanel(Tray("Routine Panel", false, 378), CloseRoutine,
                ChooseCareStyle, StartAway, EndAway);
        }

        // The strip and the panel serve the baby in the nursery and the pet in its room.
        private void OpenRoutine()
        {
            if (view == HouseholdView.PetRoom)
                ShowPetPanel(PetPanel.Routine);
            else
                ShowPanel(NurseryPanel.Routine);
        }

        private void CloseRoutine()
        {
            if (view == HouseholdView.PetRoom)
                ShowPetPanel(PetPanel.Care);
            else
                ShowPanel(NurseryPanel.Care);
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

        // The strip, or the Routine button standing in for it between Next day and Growth.
        private void UpdateRoutineEntry()
        {
            routineStrip.Root.SetActive(routineStripWanted && routineStripFits);
            var folded = !routineStripFits;
            // A pet's room has its own Routine button in its sheet's bottom row.
            var petFolded = folded && routineStripWanted && view == HouseholdView.PetRoom;
            if (petRoutineButton != null && petRoutineButton.gameObject.activeSelf != petFolded)
            {
                petRoutineButton.gameObject.SetActive(petFolded);
                LayoutPetSheet();
            }

            if (routineButton.gameObject.activeSelf == folded)
                return;
            routineButton.gameObject.SetActive(folded);
            LayoutCareSheet();
        }

        private void UpdateRoutineSlots(IReadOnlyList<RoutineSlot> slots, RoutinePreset preset, bool pet = false)
        {
            routineStrip.UpdateSlots(slots, preset, pet);
            var label = FoldedLabel(slots, preset, pet);
            if (pet)
            {
                if (petRoutineLabel != null)
                    petRoutineLabel.text = label;
            }
            else
            {
                routineButtonLabel.text = label;
            }
        }

        // "Routine", or the first window that is late or due now, such as "Snack late" or "Breakfast now". A cat's
        // timed play window counts too.
        private static string FoldedLabel(IReadOnlyList<RoutineSlot> slots, RoutinePreset preset, bool pet = false)
        {
            foreach (var wanted in new[] { RoutineSlotState.Overdue, RoutineSlotState.Due })
            {
                foreach (var slot in slots)
                {
                    if (slot.State == wanted && (pet || slot.Kind != RoutineSlotKind.Play) && slot.Kind != RoutineSlotKind.Wash)
                        return SlotName(slot.Kind, preset, pet) + (wanted == RoutineSlotState.Overdue ? " late" : " now");
                }
            }

            return "Routine";
        }

        private void ChooseCareStyle(CareStyle choice)
        {
            if (view == HouseholdView.PetRoom)
            {
                ChoosePetCareStyle(choice);
                return;
            }

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
            if (view == HouseholdView.PetRoom)
            {
                StartPetAway(duration);
                return;
            }

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
            if (view == HouseholdView.PetRoom)
            {
                EndPetAway();
                return;
            }

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

        // What the baby's health, mood, the room and Away mode ask for, or null when the needs read-out says enough.
        private static string DescribeCondition(BabyState state)
        {
            var care = state.Care;
            if (care.IsAway)
                return $"{state.Name} is fine while you're away. Needs stay steady until you're back.";
            if (care.Condition == CareCondition.Sick)
                return $"{state.Name} is poorly and resting in bed with a little fever. Feeds and cuddles help, " +
                    "and Dr. Lee will help most.";

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
            }

            switch (care.Condition)
            {
                case CareCondition.Recovering:
                    return $"{state.Name} is getting better. Medicine goes with meals: one dose at two different mealtimes.";
                case CareCondition.UnderTheWeather:
                    return $"{state.Name} is a bit under the weather: sniffly and low on energy. A visit to Dr. Lee, " +
                        "or a good day of care, will help.";
                default:
                    return DescribeShy(state) ?? (care.IsMessy ? "The nursery is getting messy. Wash up to tidy it." : null);
            }
        }

        // A cat's bedtime is its evening nap and its wash is the daily brush.
        private static string SlotName(RoutineSlotKind kind, RoutinePreset preset, bool pet = false)
        {
            switch (kind)
            {
                case RoutineSlotKind.MorningMeal: return "Breakfast";
                case RoutineSlotKind.AfternoonMeal: return preset == RoutinePreset.Home ? "Lunch" : "Snack";
                case RoutineSlotKind.EveningMeal: return "Dinner";
                case RoutineSlotKind.Play: return "Play";
                case RoutineSlotKind.Bedtime: return pet ? "Nap" : "Bed";
                default: return pet ? "Brush" : "Wash";
            }
        }

        // Today's windows as a dot timeline: done is a filled dot, due now a peach ring, later a grey dot. The state
        // words stay on the chips, hidden, for the tests; the dots carry them on screen.
        private sealed class RoutineStripView
        {
            private const float DotRow = 26;

            public GameObject Root { get; }
            public Button Open { get; }
            private readonly Image[] chips = new Image[6];
            private readonly Image[] dots = new Image[6];
            private readonly Image[] centres = new Image[6];
            private readonly Text[] names = new Text[6];
            private readonly Text[] states = new Text[6];

            public RoutineStripView(RectTransform root, Action open)
            {
                Root = root.gameObject;
                var surface = Surface(root, new Color(1, 1, 1, 0.8f));
                SoftShapes.Radius(surface, Px(18));
                surface.raycastTarget = true;
                Open = Root.AddComponent<Button>();
                Open.targetGraphic = surface;
                Open.colors = Pressable();
                Open.onClick.AddListener(() => open());
                var content = Box("Routine Chips", root, Vector2.zero, Vector2.one, new Vector2(Px(8), 4), new Vector2(-Px(8), -4));
                for (var i = 0; i < chips.Length; i++)
                {
                    var chip = Box("Routine Chip " + (i + 1), content, new Vector2(i / 6f, 0), new Vector2((i + 1) / 6f, 1),
                        Vector2.zero, Vector2.zero);
                    chips[i] = Surface(chip, Color.clear);
                    var dot = Box("Routine Dot " + (i + 1), chip, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        Vector2.zero, Vector2.zero);
                    dots[i] = Surface(dot, Divider);
                    SoftShapes.Disc(dots[i]);
                    centres[i] = Surface(Box("Routine Dot Centre " + (i + 1), dot, new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(6, 6)), Paper);
                    SoftShapes.Disc(centres[i]);
                    names[i] = Label("Routine Chip Name " + (i + 1), chip, "", 20, Secondary, TextAnchor.MiddleCenter,
                        new Vector2(0, 0), Vector2.one, new Vector2(0, -DotRow));
                    FitText(names[i], 14, 20);
                    states[i] = Label("Routine Chip State " + (i + 1), chip, "", 16, Secondary, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.right, new Vector2(0, 20));
                    states[i].gameObject.SetActive(false);
                }

                Root.SetActive(false);
            }

            public void UpdateSlots(IReadOnlyList<RoutineSlot> slots, RoutinePreset preset, bool pet = false)
            {
                for (var i = 0; i < chips.Length; i++)
                {
                    var shown = i < slots.Count;
                    chips[i].gameObject.SetActive(shown);
                    if (!shown)
                        continue;

                    var slot = slots[i];
                    names[i].text = SlotName(slot.Kind, preset, pet);
                    states[i].text = StateText(slot);
                    var (dot, size, ring) = Dot(slot.State);
                    dots[i].color = dot;
                    var rect = dots[i].rectTransform;
                    rect.offsetMin = new Vector2(-size / 2, -DotRow / 2 - size / 2);
                    rect.offsetMax = new Vector2(size / 2, -DotRow / 2 + size / 2);
                    centres[i].gameObject.SetActive(ring);
                    var current = slot.State == RoutineSlotState.Due || slot.State == RoutineSlotState.Overdue;
                    names[i].color = current ? Ink : Secondary;
                    SetRole(names[i], current ? TextRole.Strong : TextRole.Body);
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

            // The dot's colour and size, and whether it is a ring (a white centre).
            private static (Color Color, float Size, bool Ring) Dot(RoutineSlotState state)
            {
                switch (state)
                {
                    case RoutineSlotState.Due: return (FullnessRing, 26, true);
                    case RoutineSlotState.Overdue: return (FullnessRing, 24, false);
                    case RoutineSlotState.OnTime: return (HealthRing, 18, false);
                    case RoutineSlotState.DoneLate: return (EnergyRing, 18, false);
                    case RoutineSlotState.Missed: return (Hex(0xD98C98), 18, false);
                    case RoutineSlotState.Away: return (Hex(0xCFC3EA), 18, false);
                    case RoutineSlotState.Later: return (Divider, 18, false);
                    default: return (Divider, 18, true);
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
                SheetHeading("Routine Heading", content, "Routine", 378);
                var done = ActionButton("Done", content, 0.73f, 1, 300, 366, close, Mist, Ink);
                done.name = "Close routine";

                Summary = Label("Routine Summary", content, "", 23, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, 232), Vector2.right, new Vector2(0, 294));
                FitText(Summary, 17, 23);

                Label("Routine Style Label", content, "Care style", 22, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, 156), new Vector2(0.3f, 0), new Vector2(0, 220), FontStyle.Bold);
                var segment = Box("Care style Segment", content, new Vector2(0.3f, 0), Vector2.right,
                    new Vector2(0, 156), new Vector2(0, 220));
                SoftShapes.Pill(Surface(segment, Mist));
                GentleButton = ActionButton("Gentle", segment, 0, 0.5f, 6, 58,
                    () => chooseStyle(CareStyle.Gentle), Mist, Ink);
                GentleButton.name = "Care style Gentle";
                StandardButton = ActionButton("Standard", segment, 0.5f, 1, 6, 58,
                    () => chooseStyle(CareStyle.Standard), Mist, Ink);
                StandardButton.name = "Care style Standard";
                foreach (var button in new[] { GentleButton, StandardButton })
                {
                    SoftShapes.Pill(button.image);
                    var rect = (RectTransform)button.transform;
                    rect.offsetMin = new Vector2(6, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(-6, rect.offsetMax.y);
                }

                awayLabel = Label("Routine Away Label", content, "", 21, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, 98), Vector2.right, new Vector2(0, 148));
                FitText(awayLabel, 16, 21);
                var choices = Box("Away Choices", content, Vector2.zero, Vector2.right, new Vector2(0, 8), new Vector2(0, 90));
                awayChoices = choices.gameObject;
                for (var i = 0; i < AwayChoices.Length; i++)
                {
                    var days = AwayChoices[i].Days;
                    var choice = ActionButton(AwayChoices[i].Label, choices, i / 4f, (i + 1) / 4f, 0, 82,
                        () => startAway(TimeSpan.FromDays(days)), Paper, Ink);
                    choice.name = "Away " + AwayChoices[i].Label;
                    if (i == 0)
                        FirstAwayChoice = choice;
                }

                EndAwayButton = ActionButton("We're back", content, 0, 1, 8, 90, endAway, Fern, Color.white);
                EndAwayButton.name = "End away";
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyState state, string summary) => UpdateFor(state.Name, state.Care, summary);

            // The same panel for a pet: its care style and Away mode.
            public void UpdateFor(string name, CareRecord care, string summary)
            {
                Summary.text = summary;
                Select(GentleButton, care.Style == CareStyle.Gentle);
                Select(StandardButton, care.Style != CareStyle.Gentle);
                awayChoices.SetActive(!care.IsAway);
                EndAwayButton.gameObject.SetActive(care.IsAway);
                awayLabel.text = care.IsAway
                    ? "Away until " + care.AwayUntil.ToOffset(care.Offset).ToString("ddd HH:mm", CultureInfo.InvariantCulture) +
                      ". Needs stay steady and nothing is missed."
                    : $"Going away? Away mode keeps {name}'s needs steady for up to 2 weeks.";
            }

            // The chosen segment is a white pill on the soft track.
            private static void Select(Button button, bool selected)
            {
                button.image.color = selected ? Paper : Mist;
                button.GetComponentInChildren<Text>().color = selected ? Ink : Muted;
                if (selected)
                    SoftShapes.Lift(button.gameObject);
                else if (button.GetComponent<UnityEngine.UI.Shadow>() is UnityEngine.UI.Shadow shade)
                    shade.effectColor = Color.clear;
            }
        }
    }
}
