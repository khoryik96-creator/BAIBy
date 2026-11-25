using System;
using System.Globalization;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // The health axis on screen (care slice 3, docs/care-and-growth.md): a condition chip in the header card, a
    // Doctor or Medicine tile in the care sheet, and Dr. Lee's sheet with the three recovery steps. A cat (care slice 7)
    // shares the chip and the sheet, which becomes the vet's (Dr. Rosa) in the pet room. The baby's own
    // look (a paler tint, a droop, the bed and quilt, and a small bubble) comes from NurseryVisuals.SetCondition.
    // Copy stays calm: it says what helps, never blames, and never frightens.
    public sealed partial class NurseryPreview
    {
        private Image conditionChip;
        private Text conditionChipLabel;
        private Image conditionIcon;
        private Button previewDayButton;
        private Button healthButton;
        private Image healthIcon;
        private Text healthButtonTitle;
        private Text healthButtonCaption;
        private DoctorPanel doctor;

        private void BuildHealth(RectTransform careSheet)
        {
            // In the header card: on a second row under Style and Decor, or beside the name while another panel
            // is open. The favourite toy steps aside while it shows.
            var chip = Box("Condition Chip", headerCard, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            conditionChip = Surface(chip, Honey);
            SoftShapes.Pill(conditionChip);
            conditionIcon = IconAt("Condition Chip Icon", chip, "Thermometer", ButterInk, new Vector2(0, 0.5f),
                new Vector2(16 + 15, 0), 30);
            conditionChipLabel = Label("Condition Chip Label", chip, "", 24, Ink, TextAnchor.MiddleLeft,
                new Vector2(56, 0), Vector2.one, new Vector2(-18, 0), FontStyle.Bold);
            FitText(conditionChipLabel, 16, 24);
            chip.gameObject.SetActive(false);

            // A seventh tile, shown only when there is something to do for the baby's health; LayoutCareSheet puts
            // it at the end of the second row, a little wider so its caption reads.
            healthButton = CareTile("Doctor", careActions, "Doctor", "Doctor", FernSoft, UseHealthAction);
            healthIcon = healthButton.transform.Find("Badge/Icon").GetComponent<Image>();
            healthButtonTitle = healthButton.GetComponentInChildren<Text>();
            // Fixed bands from the bottom so the caption reads on the compact (96-unit) tiles too.
            healthButtonTitle.rectTransform.anchorMin = Vector2.zero;
            healthButtonTitle.rectTransform.anchorMax = Vector2.right;
            healthButtonTitle.rectTransform.offsetMin = new Vector2(6, 27);
            healthButtonTitle.rectTransform.offsetMax = new Vector2(-6, 54);
            FitText(healthButtonTitle, 17, 24);
            var badge = (RectTransform)healthButton.transform.Find("Badge");
            badge.anchorMin = new Vector2(0.5f, 1);
            badge.anchorMax = new Vector2(0.5f, 1);
            badge.offsetMin = new Vector2(-20, -44);
            badge.offsetMax = new Vector2(20, -4);
            healthIcon.rectTransform.sizeDelta = new Vector2(28, 28);
            healthButtonCaption = Label("Health Action Caption", healthButton.transform, "", 19, Muted, TextAnchor.MiddleCenter,
                new Vector2(6, 2), Vector2.right, new Vector2(-6, 28));
            FitText(healthButtonCaption, 14, 19);
            healthButton.gameObject.SetActive(false);
            careTiles.Add(healthButton);

            doctor = new DoctorPanel(Tray("Doctor Panel", false, 378), CloseDoctor, VisitDoctorOrVet);
        }

        // The sheet is the baby's doctor in the nursery and the vet in a pet's room.
        private void CloseDoctor()
        {
            if (view == HouseholdView.PetRoom)
                ShowPetPanel(PetPanel.Care);
            else
                ShowPanel(NurseryPanel.Care);
        }

        private void VisitDoctorOrVet()
        {
            if (view == HouseholdView.PetRoom)
                VisitVet();
            else
                VisitDoctor();
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

        // The chip, the health tile and the doctor's sheet for the baby on screen.
        private void UpdateHealthControls(BabyState state, Reaction reaction, string reactionText)
        {
            var care = state.Care;
            var chip = ChipFor(care, session.MedicineCourse, false);
            conditionChip.gameObject.SetActive(chip.Text != null);
            favorite.gameObject.SetActive(chip.Text == null);
            if (chip.Text != null)
            {
                conditionChipLabel.text = chip.Text;
                conditionChip.color = chip.Background;
                conditionChipLabel.color = chip.Ink;
                conditionIcon.color = chip.Ink;
            }

            var doctorOffered = session.CanVisitDoctor;
            var recovering = care.Condition == CareCondition.Recovering;
            healthButton.gameObject.SetActive(doctorOffered || recovering);
            if (doctorOffered)
            {
                healthButton.name = "Doctor";
                healthButtonTitle.text = "Doctor";
                healthButtonCaption.text = "Free visit";
                healthButton.interactable = true;
                healthButton.image.color = Sky;
                healthIcon.sprite = Icon("Doctor");
            }
            else if (recovering)
            {
                var medicine = session.Medicine;
                healthButton.name = "Medicine";
                healthButtonTitle.text = "Medicine";
                healthButtonCaption.text = MedicineCaption(medicine, state);
                healthButton.interactable = medicine == MedicineState.Ready;
                healthButton.image.color = FernSoft;
                healthIcon.sprite = Icon("Medicine");
            }

            var doctorReaction = reaction == Reaction.DoctorVisited || reaction == Reaction.DoctorAllClear;
            doctor.UpdateDetails(state, doctorOffered, doctorReaction ? reactionText : null, session.MedicineCourse);
        }

        // Other views share the header: the pet room shows its Bond in its own sheet.
        private void HideHealthChip()
        {
            if (conditionChip == null)
                return;
            conditionChip.gameObject.SetActive(false);
            favorite.gameObject.SetActive(true);
        }

        private static (string Text, Color Background, Color Ink) ChipFor(CareRecord care, int course, bool pet)
        {
            switch (care.Condition)
            {
                case CareCondition.UnderTheWeather: return ("Under the weather", Butter, ButterInk);
                case CareCondition.Sick: return (pet ? "Sick — needs the vet" : "Sick — needs the doctor", Blush, BlushInk);
                case CareCondition.Recovering:
                    return ($"Recovering · medicine {Math.Min(care.MedicineDoses, course)} of {course}", FernSoft, FernDeep);
                default: return (null, Mist, Ink);
            }
        }

        // Why the Medicine tile can or can't be used right now.
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

        // Dr. Lee's sheet: who and what, the three steps to well again, and Visit Dr. Lee.
        private sealed class DoctorPanel
        {
            private const float Height = 378;

            private readonly Image[] steps = new Image[3];
            private readonly Text[] stepLabels = new Text[3];
            private readonly Text doneLabel;
            private readonly Text heading;
            private readonly Text visitTitle;
            private readonly Text visitDetails;

            public GameObject Root { get; }
            public Button Visit { get; }
            public Button Done { get; }
            public Text Scene { get; }
            public Text Feedback { get; }

            public DoctorPanel(RectTransform content, Action close, Action visit)
            {
                Root = content.parent.gameObject;
                var badge = Box("Doctor Badge", content, Vector2.zero, Vector2.zero,
                    new Vector2(0, Height - 96), new Vector2(96, Height));
                SoftShapes.Radius(Surface(badge, Sky), Px(18));
                IconAt("Doctor Badge Icon", badge, "Doctor", Ink, new Vector2(0.5f, 0.5f), Vector2.zero, Px(28));
                heading = Label("Doctor Heading", content, "Dr. Lee", 39, Ink, TextAnchor.MiddleLeft,
                    new Vector2(116, Height - 48), new Vector2(0.73f, 0), new Vector2(0, Height - 2), FontStyle.Bold);
                FitText(heading, 28, 39);
                Feedback = Label("Doctor Feedback", content, "", 24, Secondary, TextAnchor.UpperLeft,
                    new Vector2(116, Height - 100), new Vector2(0.73f, 0), new Vector2(0, Height - 50));
                FitText(Feedback, 15, 24);

                Done = SheetClose("Close doctor", "Not now", content, Height, close);
                doneLabel = Done.GetComponentInChildren<Text>();

                Scene = Label("Doctor Scene", content, "", 27, Hex(0x3F3A45), TextAnchor.MiddleLeft,
                    new Vector2(0, 190), Vector2.right, new Vector2(0, Height - 108));
                FitText(Scene, 18, 27);

                var row = Box("Doctor Steps", content, Vector2.zero, Vector2.right, new Vector2(0, 104), new Vector2(0, 182));
                string[] labels = { "Visit Dr. Lee", "Medicine at a meal", "Medicine at the next" };
                for (var i = 0; i < steps.Length; i++)
                {
                    var step = Box("Doctor Step " + (i + 1), row, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1),
                        new Vector2(i == 0 ? 0 : 8, 0), new Vector2(i == 2 ? 0 : -8, 0));
                    steps[i] = Surface(step, Mist);
                    SoftShapes.Radius(steps[i], Px(16));
                    var number = Label("Number", step, (i + 1).ToString(CultureInfo.InvariantCulture), 28, Ink,
                        TextAnchor.MiddleCenter, new Vector2(4, 40), Vector2.one, new Vector2(-4, -4), FontStyle.Bold);
                    SetRole(number, TextRole.Heading);
                    IconAt("Finished", step, "Check", FernDeep, new Vector2(0.5f, 1), new Vector2(0, -22), 30)
                        .gameObject.SetActive(false);
                    stepLabels[i] =Label("Doctor Step Label " + (i + 1), step, labels[i], 22, Muted, TextAnchor.MiddleCenter,
                        new Vector2(6, 6), Vector2.right, new Vector2(-6, 44), FontStyle.Bold);
                    FitText(stepLabels[i], 14, 22);
                }

                Visit = ActionButton("Visit Dr. Lee", content, 0, 1, 0, 94, visit, Fern, Color.white);
                Visit.name = "Visit doctor";
                SoftShapes.Radius(Visit.image, Px(18));
                visitTitle = Visit.GetComponentInChildren<Text>();
                visitTitle.fontSize = 30;
                visitTitle.rectTransform.offsetMin = new Vector2(12, 34);
                visitTitle.rectTransform.offsetMax = new Vector2(-12, -6);
                visitDetails = Label("Visit Dr. Lee Effects", Visit.transform, "Free  ·  Health +15  ·  A gentle check-up",
                    20, new Color(1, 1, 1, 0.86f), TextAnchor.MiddleCenter, new Vector2(12, 8), Vector2.right, new Vector2(-12, 38));
                FitText(visitDetails, 15, 20);
                Root.SetActive(false);
            }

            // Before the visit the sheet invites; after it, it keeps Dr. Lee's words until it closes. The steps
            // follow the recovery: the current one is tinted sage, finished ones get a tick in their number.
            public void UpdateDetails(BabyState state, bool offered, string outcome, int course) =>
                UpdateFor(state.Name, state.Care, offered, outcome, course, false);

            // The same sheet for a pet: the vet, Dr. Rosa, sees it in its basket.
            public void UpdateFor(string name, CareRecord care, bool offered, string outcome, int course, bool pet)
            {
                var doctorName = pet ? VetName : "Dr. Lee";
                heading.text = pet ? VetName + ", the vet" : "Dr. Lee";
                visitTitle.text = "Visit " + doctorName;
                Visit.name = pet ? "Visit vet" : "Visit doctor";
                visitDetails.text = pet ? "Free  ·  Health +15  ·  A gentle check-up" : "Free  ·  Health +15  ·  A gentle check-up";
                stepLabels[0].text = "Visit " + doctorName;
                Visit.gameObject.SetActive(offered);
                var recovering = care.Condition == CareCondition.Recovering;
                var current = offered ? 0 : recovering ? 1 + Math.Min(care.MedicineDoses, 1) : -1;
                for (var i = 0; i < steps.Length; i++)
                {
                    var finished = !offered && recovering && i < current;
                    steps[i].color = i == current ? FernSoft : Mist;
                    stepLabels[i].color = i == current ? FernDeep : Muted;
                    steps[i].transform.Find("Number").gameObject.SetActive(!finished);
                    steps[i].transform.Find("Finished").gameObject.SetActive(finished);
                }

                stepLabels[2].text = course > 2 ? "More doses at meals" : "Medicine at the next";
                PlaceDone(offered);
                if (offered)
                {
                    Scene.text = care.Condition == CareCondition.Sick
                        ? $"{name} is poorly and resting {(pet ? HamsterShown ? "curled up in the nest" : FishShown ? "quietly by the plants" : RabbitShown ? "tucked in the hutch" : "curled up in a basket" : "in bed")}. " +
                          $"{doctorName} can see {name} right now."
                        : $"{name} is a little under the weather. {doctorName} can see {name} right now.";
                    Feedback.text = "Visits are always free and only take a moment.";
                    return;
                }

                if (outcome != null)
                    Scene.text = outcome;
                else if (!Root.activeSelf)
                    Scene.text = $"{name} is well looked after. {doctorName} is here whenever {name} feels poorly.";
                Feedback.text = recovering
                    ? "The Medicine tile is in the care sheet. Give it at mealtimes."
                    : string.Empty;
            }

            // "Not now" sits at the top right while the visit is offered; afterwards "Done" fills the bottom row.
            private void PlaceDone(bool offered)
            {
                var rect = (RectTransform)Done.transform;
                doneLabel.text = offered ? "Not now" : "Done";
                rect.anchorMin = new Vector2(offered ? 0.74f : 0, 0);
                rect.anchorMax = new Vector2(1, 0);
                rect.offsetMin = new Vector2(3, offered ? Height - 78 : 0);
                rect.offsetMax = new Vector2(-3, offered ? Height - 12 : 94);
            }
        }
    }
}
