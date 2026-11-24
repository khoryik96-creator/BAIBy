using System;
using System.Globalization;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // The health axis on screen (care slice 3, docs/care-and-growth.md): a status chip on the header's age line, a
    // Doctor or Medicine button that joins the Care tray's bottom row, and Dr. Lee's panel. The baby's own look
    // (a paler tint, a droop, the bed and blanket, and a small bubble) comes from NurseryVisuals.SetCondition.
    // Copy stays calm: it says what helps, never blames, and never frightens.
    public sealed partial class NurseryPreview
    {
        private Image conditionChip;
        private Text conditionChipLabel;
        private Button previewDayButton;
        private Button healthButton;
        private Text healthButtonTitle;
        private Text healthButtonCaption;
        private DoctorPanel doctor;

        private void BuildHealth(RectTransform header, RectTransform careTray)
        {
            // On the age line, right of "3 days old · Early sitter"; the favorite toy steps aside while it shows.
            var chip = Box("Condition Chip", header, new Vector2(0.55f, 0), new Vector2(1, 0),
                new Vector2(0, 105), new Vector2(0, 141));
            conditionChip = Surface(chip, Honey);
            SoftShapes.Round(conditionChip, 1.6f);
            conditionChipLabel = Label("Condition Chip Label", chip, "", 19, Ink, TextAnchor.MiddleCenter,
                new Vector2(12, 0), Vector2.one, new Vector2(-12, 0), FontStyle.Bold);
            conditionChipLabel.resizeTextForBestFit = true;
            conditionChipLabel.resizeTextMinSize = 13;
            conditionChipLabel.resizeTextMaxSize = 19;
            chip.gameObject.SetActive(false);

            var previewLabel = previewDayButton.GetComponentInChildren<Text>();
            previewLabel.rectTransform.offsetMin = new Vector2(8, 0);
            previewLabel.rectTransform.offsetMax = new Vector2(-8, 0);
            previewLabel.resizeTextForBestFit = true;
            previewLabel.resizeTextMinSize = 16;
            previewLabel.resizeTextMaxSize = 25;

            // The third button of the bottom row, shown only when there is something to do for the baby's health.
            healthButton = ActionButton("Doctor", careTray, 2f / 3, 1, 7, 91, UseHealthAction, Honey, Ink);
            SoftShapes.Round(healthButton.image, 0.7f);
            var colors = healthButton.colors;
            colors.disabledColor = new Color32(220, 220, 220, 255);
            healthButton.colors = colors;
            healthButtonTitle = healthButton.GetComponentInChildren<Text>();
            healthButtonTitle.rectTransform.offsetMin = new Vector2(8, 32);
            healthButtonTitle.rectTransform.offsetMax = new Vector2(-8, -4);
            healthButtonTitle.resizeTextForBestFit = true;
            healthButtonTitle.resizeTextMinSize = 18;
            healthButtonTitle.resizeTextMaxSize = 25;
            healthButtonCaption = Label("Health Action Caption", healthButton.transform, "", 17, Ink, TextAnchor.MiddleCenter,
                new Vector2(6, 5), Vector2.right, new Vector2(-6, 36));
            healthButtonCaption.resizeTextForBestFit = true;
            healthButtonCaption.resizeTextMinSize = 12;
            healthButtonCaption.resizeTextMaxSize = 17;
            healthButton.gameObject.SetActive(false);

            doctor = new DoctorPanel(Tray("Doctor Panel", false, 378), () => ShowPanel(NurseryPanel.Care), VisitDoctor);
        }

        private void UseHealthAction()
        {
            if (session.CanVisitDoctor)
                ShowPanel(NurseryPanel.Doctor);
            else if (session.State.Care.Condition == CareCondition.Recovering)
                Care(CareAction.Medicine);
        }

        private void VisitDoctor()
        {
            (Reaction Reaction, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) result;
            try
            {
                result = session.VisitDoctor();
            }
            catch (Exception error)
            {
                ReportFailure(error, "The visit couldn't be saved. Please try again.");
                return;
            }

            Paint(result.Reaction, (result.UnlockedSitting, result.UnlockedCrawling, result.UnlockedWalking));
            doctor.Done.Select();
        }

        // The chip, the bottom row and the doctor's panel for the baby on screen.
        private void UpdateHealthControls(BabyState state, Reaction reaction, string reactionText)
        {
            var care = state.Care;
            var chip = ChipFor(care, session.MedicineCourse);
            conditionChip.gameObject.SetActive(chip.Text != null);
            favorite.gameObject.SetActive(chip.Text == null);
            if (chip.Text != null)
            {
                conditionChipLabel.text = chip.Text;
                conditionChip.color = chip.Color;
            }

            var doctorOffered = session.CanVisitDoctor;
            var recovering = care.Condition == CareCondition.Recovering;
            var offered = doctorOffered || recovering;
            healthButton.gameObject.SetActive(offered);
            var split = offered ? 1f / 3 : 0.5f;
            PlaceInRow(previewDayButton, 0, split);
            PlaceInRow(developmentButton, split, offered ? 2f / 3 : 1);
            if (doctorOffered)
            {
                healthButton.name = "Doctor";
                healthButtonTitle.text = "Doctor";
                healthButtonCaption.text = "Free visit";
                healthButton.interactable = true;
                healthButton.image.color = Honey;
            }
            else if (recovering)
            {
                var medicine = session.Medicine;
                healthButton.name = "Medicine";
                healthButtonTitle.text = "Medicine";
                healthButtonCaption.text = MedicineCaption(medicine, state);
                healthButton.interactable = medicine == MedicineState.Ready;
                healthButton.image.color = Leaf;
            }

            var doctorReaction = reaction == Reaction.DoctorVisited || reaction == Reaction.DoctorAllClear;
            doctor.UpdateDetails(state, doctorOffered, doctorReaction ? reactionText : null);
        }

        // Other views share the header: the pet room shows its Bond where the chip would be.
        private void HideHealthChip()
        {
            if (conditionChip == null)
                return;
            conditionChip.gameObject.SetActive(false);
            favorite.gameObject.SetActive(true);
        }

        private static void PlaceInRow(Button button, float left, float right)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, 0);
            rect.anchorMax = new Vector2(right, 0);
        }

        private static (string Text, Color Color) ChipFor(CareRecord care, int course)
        {
            switch (care.Condition)
            {
                case CareCondition.UnderTheWeather: return ("Under the weather", Honey);
                case CareCondition.Sick: return ("Sick — needs the doctor", Rose);
                case CareCondition.Recovering:
                    return ($"Recovering · medicine {Math.Min(care.MedicineDoses, course)} of {course}", Leaf);
                default: return (null, Mist);
            }
        }

        // Why the Medicine button can or can't be used right now.
        private static string MedicineCaption(MedicineState medicine, BabyState state)
        {
            switch (medicine)
            {
                case MedicineState.Ready: return "With this meal";
                case MedicineState.GivenThisMeal: return "Given · next meal";
                default: return "At " + NextMeal(state);
            }
        }

        // "snack 14:00", or "breakfast" when today's meals are over.
        private static string NextMeal(BabyState state)
        {
            foreach (var slot in BabySimulation.Routine(state))
            {
                var meal = slot.Kind == RoutineSlotKind.MorningMeal || slot.Kind == RoutineSlotKind.AfternoonMeal ||
                    slot.Kind == RoutineSlotKind.EveningMeal;
                if (meal && slot.Open > state.TimeCheckpoint)
                    return SlotName(slot.Kind, state.Care.Preset).ToLowerInvariant() + " " +
                        slot.Open.ToString("HH:mm", CultureInfo.InvariantCulture);
            }

            return "breakfast";
        }

        // Reactions of the health axis, or null for the others.
        private string DescribeHealthReaction(BabyState state, Reaction reaction)
        {
            var name = state.Name;
            switch (reaction)
            {
                case Reaction.TooPoorly:
                    return $"{name} feels too poorly for that and stays snuggled in bed. Feeds, cuddles and Dr. Lee will help.";
                case Reaction.DoctorVisited:
                    return $"Dr. Lee listens, checks a temperature and says {name} needs rest and medicine. " +
                        "One dose with two different meals will help.";
                case Reaction.DoctorAllClear:
                    return $"Dr. Lee listens, checks a temperature and says {name} is doing fine. " +
                        "Regular meals and naps will keep it that way.";
                case Reaction.MedicineTaken:
                    return $"{name} takes the medicine with a brave little face. Dose {state.Care.MedicineDoses} of " +
                        $"{session.MedicineCourse}; the next one goes with another meal.";
                case Reaction.MedicineDone:
                    return $"{name} takes the last dose and feels much better. Well again!";
                case Reaction.MedicineNotNow:
                    switch (BabySimulation.Medicine(state))
                    {
                        case MedicineState.GivenThisMeal:
                            return $"{name} already had medicine with this meal. The next dose goes with another meal.";
                        case MedicineState.WaitForMeal:
                            return $"Medicine goes with a meal. The next dose is at {NextMeal(state)}.";
                        default:
                            return $"{name} is well and doesn't need any medicine.";
                    }
                default:
                    return null;
            }
        }

        private sealed class DoctorPanel
        {
            public GameObject Root { get; }
            public Button Visit { get; }
            public Button Done { get; }
            public Text Scene { get; }
            public Text Feedback { get; }

            public DoctorPanel(RectTransform content, Action close, Action visit)
            {
                Root = content.parent.gameObject;
                // A small clinic badge: a soft disc with a white cross, made of UI shapes.
                var badge = Box("Doctor Badge", content, Vector2.zero, Vector2.zero, new Vector2(0, 296), new Vector2(60, 356));
                SoftShapes.Disc(Surface(badge, Rose));
                var across = Box("Doctor Badge Across", badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-17, -6), new Vector2(17, 6));
                SoftShapes.Round(Surface(across, Color.white), 4f);
                var down = Box("Doctor Badge Down", badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-6, -17), new Vector2(6, 17));
                SoftShapes.Round(Surface(down, Color.white), 4f);
                Label("Doctor Heading", content, "Dr. Lee", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(74, 283), new Vector2(0.72f, 0), new Vector2(0, 369), FontStyle.Bold);
                Done = ActionButton("Done", content, 0.73f, 1, 283, 371, close, Mist, Ink);
                Done.name = "Close doctor";

                Scene = Label("Doctor Scene", content, "", 23, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 186), Vector2.right, new Vector2(0, 279));
                Scene.resizeTextForBestFit = true;
                Scene.resizeTextMinSize = 17;
                Scene.resizeTextMaxSize = 23;

                Visit = ActionButton("Visit Dr. Lee", content, 0, 1, 92, 180, visit, Fern, Color.white);
                Visit.name = "Visit doctor";
                SoftShapes.Round(Visit.image, 0.7f);
                var title = Visit.GetComponentInChildren<Text>();
                title.rectTransform.offsetMin = new Vector2(12, 34);
                title.rectTransform.offsetMax = new Vector2(-12, -4);
                title.alignment = TextAnchor.MiddleLeft;
                var details = Label("Visit Dr. Lee Effects", Visit.transform, "Free  ·  Health +15  ·  A gentle check-up",
                    20, Color.white, TextAnchor.MiddleLeft, new Vector2(12, 6), Vector2.right, new Vector2(-12, 36));
                details.resizeTextForBestFit = true;
                details.resizeTextMinSize = 15;
                details.resizeTextMaxSize = 20;

                Feedback = Label("Doctor Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 8), Vector2.right, new Vector2(0, 86));
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 16;
                Feedback.resizeTextMaxSize = 21;
                Root.SetActive(false);
            }

            // Before the visit the panel invites; after it, it keeps Dr. Lee's words until it closes.
            public void UpdateDetails(BabyState state, bool offered, string outcome)
            {
                var name = state.Name;
                Visit.gameObject.SetActive(offered);
                if (offered)
                {
                    Scene.text = state.Care.Condition == CareCondition.Sick
                        ? $"{name} is poorly and resting in bed. Dr. Lee can see {name} right now."
                        : $"{name} is a little under the weather. Dr. Lee can see {name} right now.";
                    Feedback.text = "Visits are always free and only take a moment.";
                    return;
                }

                if (outcome != null)
                    Scene.text = outcome;
                else if (!Root.activeSelf)
                    Scene.text = $"{name} is well looked after. Dr. Lee is here whenever {name} feels poorly.";
                Feedback.text = state.Care.Condition == CareCondition.Recovering
                    ? "The Medicine button is in the care tray. Give it at mealtimes."
                    : string.Empty;
            }
        }
    }
}
