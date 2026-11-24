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
    // A pet with a care profile (the cat, care slice 7) on screen: the nursery's care systems in the pet's room. The
    // routine strip shows its windows (breakfast, play, dinner, nap and brush), the header's condition chip its health,
    // the care sheet gains a Photo tile and a Vet or Medicine tile and a Growth button, and the baby's sheets open in a
    // pet mode: Routine (care style and Away mode), Growth (the projection, rhythm, Bond and Trust), the journal and
    // the vet (Dr. Rosa). Mood and health speak in the bubble: restless, grumpy, sniffly, poorly, a shy skill. Copy stays
    // warm: it says what helps and never blames.
    public sealed partial class NurseryPreview
    {
        private const string VetName = "Dr. Rosa";
        // A photo catches the moment of the last care for this long, as for the baby.
        private const float PetMomentSeconds = 120;

        private enum PetPanel { Care, Routine, Growth, Journal, Vet }

        private PetPanel petPanel;
        private Button petRoutineButton;
        private Text petRoutineLabel;
        private Button petGrowthButton;
        private Button petPhotoButton;
        private Text petPhotoLabel;
        private Button petHealthButton;
        private Image petHealthIcon;
        private Text petHealthTitle;
        private Text petHealthCaption;
        private string petVetText;
        private PhotoMoment petMoment = PhotoMoment.Calm;
        private float petMomentAt = float.NegativeInfinity;
        private int petStickerSlot;
        private int petShownLongest;

        // The bottom row's Routine (standing in for the strip when it does not fit) and Growth buttons.
        private void BuildPetCareControls(RectTransform panel)
        {
            petRoutineButton = ActionButton("Pet routine", panel, 0, 1, 0, 0, () => ShowPetPanel(PetPanel.Routine), Mist, Ink);
            WithIcon(petRoutineButton, "Routine", "Routine", Ink);
            petRoutineLabel = petRoutineButton.GetComponentInChildren<Text>();
            petRoutineButton.gameObject.SetActive(false);
            petGrowthButton = ActionButton("Pet growth", panel, 0, 1, 0, 0, () => ShowPetPanel(PetPanel.Growth), Fern, Color.white);
            WithIcon(petGrowthButton, "Growth", "Growth", Color.white);
            petGrowthButton.gameObject.SetActive(false);
        }

        // The Photo tile and the Vet or Medicine tile after the species' verbs, for a species with a care profile.
        private void BuildPetCareTiles(Species species)
        {
            petPhotoButton = null;
            petHealthButton = null;
            if (PetCareProfile.For(species) == null)
                return;

            petPhotoButton = CareTile("Take a pet photo", petVerbArea, "Photo", "Photo", Mist, TakePetPhoto);
            petPhotoLabel = petPhotoButton.GetComponentInChildren<Text>();
            petHealthButton = CareTile("Vet", petVerbArea, "Vet", "Doctor", Sky, UsePetHealthAction);
            petHealthIcon = petHealthButton.transform.Find("Badge/Icon").GetComponent<Image>();
            petHealthTitle = petHealthButton.GetComponentInChildren<Text>();
            // Fixed bands from the bottom so the caption reads on compact tiles, as on the baby's Doctor tile.
            petHealthTitle.rectTransform.anchorMin = Vector2.zero;
            petHealthTitle.rectTransform.anchorMax = Vector2.right;
            petHealthTitle.rectTransform.offsetMin = new Vector2(6, 27);
            petHealthTitle.rectTransform.offsetMax = new Vector2(-6, 54);
            FitText(petHealthTitle, 17, 24);
            var badge = (RectTransform)petHealthButton.transform.Find("Badge");
            badge.anchorMin = new Vector2(0.5f, 1);
            badge.anchorMax = new Vector2(0.5f, 1);
            badge.offsetMin = new Vector2(-20, -44);
            badge.offsetMax = new Vector2(20, -4);
            petHealthIcon.rectTransform.sizeDelta = new Vector2(28, 28);
            petHealthCaption = Label("Pet Health Caption", petHealthButton.transform, "", 19, Muted, TextAnchor.MiddleCenter,
                new Vector2(6, 2), Vector2.right, new Vector2(-6, 28));
            FitText(petHealthCaption, 14, 19);
            petHealthButton.gameObject.SetActive(false);
        }

        // Growth and its Journal tile serve the baby in the nursery and the pet in its room.
        private void CloseGrowth()
        {
            if (view == HouseholdView.PetRoom)
                ShowPetPanel(PetPanel.Care);
            else
                ShowPanel(NurseryPanel.Care);
        }

        private void OpenGrowthJournal()
        {
            if (view == HouseholdView.PetRoom)
                ShowPetPanel(PetPanel.Journal);
            else
                OpenBabyJournal();
        }

        // Which of the shared sheets is open in the pet's room (the others stay closed), and the routine strip.
        private void ApplyPetPanels()
        {
            if (view != HouseholdView.PetRoom || routine == null)
                return;

            var cared = FindCompanion(petSlot)?.PetSession?.HasCare == true;
            routine.Root.SetActive(cared && petPanel == PetPanel.Routine);
            development.Root.SetActive(cared && petPanel == PetPanel.Growth);
            journal.Root.SetActive(cared && petPanel == PetPanel.Journal);
            doctor.Root.SetActive(cared && petPanel == PetPanel.Vet);
            routineStripWanted = cared && (petPanel == PetPanel.Care || petPanel == PetPanel.Routine);
            UpdateRoutineEntry();
        }

        private void ShowPetPanel(PetPanel next)
        {
            var companion = FindCompanion(petSlot);
            if (companion?.PetState == null || view != HouseholdView.PetRoom)
                return;

            var previous = petPanel;
            petPanel = companion.PetSession.HasCare ? next : PetPanel.Care;
            if (petPanel == PetPanel.Journal)
                journal.ShowPetPages(companion.PetState, companion.PetSession.Journal, companion.PetSession.Bonding, null,
                    entry => PetPhoto(companion.PetState, entry));
            if (petPanel == PetPanel.Vet && previous != PetPanel.Vet)
                petVetText = null;
            PaintPetRoom(null, keepStatus: true);

            switch (petPanel)
            {
                case PetPanel.Routine:
                    (companion.PetState.Care.Style == CareStyle.Gentle ? routine.GentleButton : routine.StandardButton).Select();
                    break;
                case PetPanel.Growth:
                    development.OpenJournal.Select();
                    break;
                case PetPanel.Journal:
                    journal.Close.Select();
                    break;
                case PetPanel.Vet:
                    (doctor.Visit.gameObject.activeSelf ? doctor.Visit : doctor.Done).Select();
                    break;
                default:
                    var focus = previous switch
                    {
                        PetPanel.Growth or PetPanel.Journal => petGrowthButton,
                        PetPanel.Routine => routineStripFits ? routineStrip.Open : (Selectable)petRoutineButton,
                        PetPanel.Vet when petHealthButton != null && petHealthButton.gameObject.activeSelf &&
                            petHealthButton.interactable => petHealthButton,
                        _ => null,
                    };
                    if (focus != null && focus.gameObject.activeInHierarchy)
                        focus.Select();
                    break;
            }
        }

        // The care systems' read-outs for the pet on screen: the chip, the tiles, the strip and the shared sheets.
        private void PaintPetCare(HouseholdCompanion companion)
        {
            var session = companion.PetSession;
            var state = companion.PetState;
            var cared = session.HasCare;
            if (petGrowthButton != null)
                petGrowthButton.gameObject.SetActive(cared);
            if (!cared)
            {
                petStage.SetCondition(CareCondition.Well, false);
                return;
            }

            var care = state.Care;
            var resting = household.IsResting(petSlot);
            var chip = ChipFor(care, session.MedicineCourse, true);
            conditionChip.gameObject.SetActive(chip.Text != null);
            favorite.gameObject.SetActive(chip.Text == null);
            if (chip.Text != null)
            {
                conditionChipLabel.text = chip.Text;
                conditionChip.color = chip.Background;
                conditionChipLabel.color = chip.Ink;
                conditionIcon.color = chip.Ink;
            }

            if (petHealthButton != null)
            {
                var vet = session.CanVisitVet;
                var recovering = care.Condition == CareCondition.Recovering;
                petHealthButton.gameObject.SetActive(vet || recovering);
                if (vet)
                {
                    petHealthButton.name = "Vet";
                    petHealthTitle.text = "Vet";
                    petHealthCaption.text = "Free visit";
                    petHealthButton.interactable = !resting;
                    petHealthButton.image.color = Sky;
                    petHealthIcon.sprite = Icon("Doctor");
                }
                else if (recovering)
                {
                    var medicine = session.Medicine;
                    petHealthButton.name = "Pet medicine";
                    petHealthTitle.text = "Medicine";
                    petHealthCaption.text = medicine == MedicineState.Ready ? "With this meal"
                        : medicine == MedicineState.GivenThisMeal ? "Given · next meal" : "At " + NextPetMeal(session);
                    petHealthButton.interactable = medicine == MedicineState.Ready && !resting;
                    petHealthButton.image.color = FernSoft;
                    petHealthIcon.sprite = Icon("Medicine");
                }
            }

            if (petPhotoButton != null)
            {
                petPhotoLabel.text = session.PhotosLeft > 0 ? "Photo" : "Photos done";
                petPhotoButton.interactable = !resting;
            }

            UpdateRoutineSlots(session.Routine, care.Preset, true);
            routine.UpdateFor(state.Name, care, DescribePetCondition(state) ??
                $"{state.Name} is settled. Two meals, playtime, a brush and the evening nap keep the routine.");
            if (session.HasHelpers)
                UpdateRoutineHelpers(state.Name, true, session.FeederStock, session.FeederCapacity);
            else
                routine.HideHelpers();
            var meals = Math.Min(GrowthTunables.Default.RecoveryMeals, RoutineCalendar.MealWindows(care.Routine));
            development.UpdatePet(state, age.text, petStatus.text, session.Growth, session.Bonding, session.Trust, meals);
            doctor.UpdateFor(state.Name, care, session.CanVisitVet, petVetText, session.MedicineCourse, true);
            petStage.SetCondition(care.Condition, state.ShySkill != MotorSkill.None);
        }

        private void UsePetHealthAction()
        {
            var session = FindCompanion(petSlot)?.PetSession;
            if (session?.State == null)
                return;
            if (session.CanVisitVet)
                ShowPetPanel(PetPanel.Vet);
            else if (session.State.Care?.Condition == CareCondition.Recovering)
                GivePetMedicine();
        }

        private void VisitVet()
        {
            PetCareOutcome outcome;
            try
            {
                outcome = household.VisitPetVet(petSlot);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The visit couldn't be saved. Please try again.");
                return;
            }

            var state = FindCompanion(petSlot)?.PetState;
            if (state == null)
                return;
            petVetText = CareCueText(state, PetVerb.Pet, outcome.Cue);
            var grown = TakePetCelebrations(out _);
            PaintPetRoom(grown != null ? grown + " " + petVetText : petVetText);
            doctor.Done.Select();
        }

        private void GivePetMedicine()
        {
            PetCareOutcome outcome;
            try
            {
                outcome = household.GivePetMedicine(petSlot);
            }
            catch (Exception error)
            {
                PaintPetRoom(null, keepStatus: true);
                ReportFailure(error, "The medicine couldn't be saved. Please try again.");
                return;
            }

            var state = FindCompanion(petSlot)?.PetState;
            if (state == null)
                return;
            var text = CareCueText(state, PetVerb.Feed, outcome.Cue);
            var grown = TakePetCelebrations(out _);
            PaintPetRoom(grown != null ? grown + " " + text : text);
        }

        private void TakePetPhoto()
        {
            var state = FindCompanion(petSlot)?.PetState;
            if (state == null)
                return;

            var moment = CurrentPetMoment(state);
            PetCareOutcome outcome;
            try
            {
                outcome = household.TakePetPhoto(petSlot, moment);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The photo couldn't be saved. Please try again.");
                return;
            }

            var session = FindCompanion(petSlot)?.PetSession;
            var left = session?.PhotosLeft ?? 0;
            var text = outcome.Cue == PetCue.Bored
                ? $"That's today's three photos of {state.Name}. More tomorrow!"
                : $"Click! A photo of {state.Name} {MomentWords(moment)} is in the journal. " +
                  (left == 0 ? "That's today's three." : left == 1 ? "One more today." : $"{left} more today.");
            var grown = TakePetCelebrations(out _);
            PaintPetRoom(grown != null ? grown + " " + text : text);
        }

        private void ChoosePetCareStyle(CareStyle style)
        {
            try
            {
                household.SetPetCareStyle(petSlot, style);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The care style couldn't be saved. Please try again.");
                return;
            }

            PaintPetRoom(null, keepStatus: true);
        }

        private void StartPetAway(TimeSpan duration)
        {
            try
            {
                household.StartPetAway(petSlot, duration);
            }
            catch (Exception error)
            {
                ReportFailure(error, "Away mode couldn't be saved. Please try again.");
                return;
            }

            PaintPetRoom(TakePetCelebrations(out _), keepStatus: true);
            routine.EndAwayButton.Select();
        }

        private void EndPetAway()
        {
            try
            {
                household.EndPetAway(petSlot);
            }
            catch (Exception error)
            {
                ReportFailure(error, "Away mode couldn't be saved. Please try again.");
                return;
            }

            PaintPetRoom(TakePetCelebrations(out _), keepStatus: true);
            routine.FirstAwayChoice.Select();
        }

        // A journal photo's picture: the pet's pose for the stage it was in that day (Grown from its grown day on),
        // resting for a nap or a poorly day, in the pet's look.
        private Sprite PetPhoto(PetState state, JournalEntry entry)
        {
            if (state == null || entry == null || household == null)
                return null;
            var grownDay = (int)household.PetCatalog.Get(state.Species).GrownAge.TotalDays + 1;
            var stage = entry.Day >= grownDay ? PetStage.Grown : PetStage.Young;
            var rest = entry.Moment == PhotoMoment.Napping || entry.Moment == PhotoMoment.Poorly;
            return petStage.PortraitFor(state.Appearance, household.PetCatalog, stage, rest);
        }

        private void RememberPetMoment(PetVerb verb, PetCue cue)
        {
            if (RefusedCue(cue) || cue == PetCue.TooFull || cue == PetCue.IgnoredFlakes)
                return;
            switch (verb)
            {
                case PetVerb.Feed: petMoment = PhotoMoment.Meal; break;
                case PetVerb.Pet: petMoment = PhotoMoment.Cuddle; break;
                case PetVerb.Wand: petMoment = PhotoMoment.Playing; break;
                case PetVerb.Brush: petMoment = PhotoMoment.Bath; break;
                case PetVerb.Nap: petMoment = PhotoMoment.Napping; break;
                default: petMoment = PhotoMoment.Calm; break;
            }

            petMomentAt = Time.unscaledTime;
        }

        private PhotoMoment CurrentPetMoment(PetState state)
        {
            if (IsPoorly(state))
                return PhotoMoment.Poorly;
            return Time.unscaledTime - petMomentAt <= PetMomentSeconds ? petMoment : PhotoMoment.Calm;
        }

        private static bool RefusedCue(PetCue cue) =>
            cue == PetCue.TooPoorly || cue == PetCue.PushesToyAway || cue == PetCue.MedicineNotNow;

        private static bool IsPoorly(PetState state) => state?.Care != null && state.Care.Condition == CareCondition.Sick;

        // "dinner 18:00", or "breakfast" when today's meals are over.
        private static string NextPetMeal(PetSession session)
        {
            var state = session.State;
            foreach (var slot in session.Routine)
            {
                var meal = slot.Kind == RoutineSlotKind.MorningMeal || slot.Kind == RoutineSlotKind.AfternoonMeal ||
                    slot.Kind == RoutineSlotKind.EveningMeal;
                if (meal && slot.Open > state.TimeCheckpoint)
                    return SlotName(slot.Kind, state.Care.Preset, true).ToLowerInvariant() + " " +
                        slot.Open.ToString("HH:mm", CultureInfo.InvariantCulture);
            }

            return "breakfast";
        }

        // A milestone a pet reached, for the bubble: each is in the journal too.
        private static string MilestoneLine(PetState state, CompanionMilestone milestone)
        {
            var name = state.Name;
            switch (milestone)
            {
                case CompanionMilestone.Pounce: return $"{name} wiggled and pounced for the first time, and it's in the journal!";
                case CompanionMilestone.Climb: return $"{name} climbed all the way up the cat tree, and it's in the journal!";
                case CompanionMilestone.HighJump: return $"{name} made a high jump to the windowsill, and it's in the journal!";
                case CompanionMilestone.Grown: return $"{name} is all grown up! Look how big they are.";
                default: return null;
            }
        }

        // A short line when the care rhythm reaches a sticker.
        private string PetStickerLine(PetState state)
        {
            var session = FindCompanion(petSlot)?.PetSession;
            if (state == null || session == null || !session.HasCare)
                return null;

            var longest = session.Bonding?.LongestRhythm ?? 0;
            if (petStickerSlot != petSlot)
            {
                petStickerSlot = petSlot;
                petShownLongest = longest;
                return null;
            }

            var earned = longest > petShownLongest && BondRules.IsSticker(longest);
            petShownLongest = longest;
            return earned ? $"{longest} cared-for days in a row! {state.Name}'s journal has a new sticker." : null;
        }

        // " · Restless" or " · Grumpy" beside the Bond tier, while it lasts.
        private static string MoodWord(PetState state)
        {
            switch (state.Care?.Mood)
            {
                case CareMood.Fussy: return " · Restless";
                case CareMood.Upset: return " · Grumpy";
                default: return string.Empty;
            }
        }

        // What the pet's health, mood, a shy skill, the rug and Away mode ask for, or null.
        private static string DescribePetCondition(PetState state)
        {
            var care = state.Care;
            if (care == null)
                return null;

            var name = state.Name;
            if (care.IsAway)
                return $"{name} is fine while you're away. A neighbour pops in, and needs stay steady until you're back.";
            if (care.Condition == CareCondition.Sick)
                return $"{name} is poorly and curled up with a little fever. Meals and gentle petting help, " +
                    $"and {VetName} the vet will help most.";

            switch (care.Mood)
            {
                case CareMood.Upset:
                    return state.Meter(PetMeter.Fullness) < 50
                        ? $"{name} is grumpy and swishing their tail. A meal first, then gentle petting, will help."
                        : $"{name} is grumpy and swishing their tail. Gentle petting will help.";
                case CareMood.Fussy:
                    if ((care.Owed & CareTags.Meal) != 0)
                        return $"{name} is restless and meowing for a missed meal. A meal will help.";
                    if ((care.Owed & CareTags.Bedtime) != 0)
                        return $"{name} is restless after missing the evening nap. A nap will help.";
                    if (care.MoodCause == CareMoodCause.MealOverdue)
                        return $"{name} is restless. It's past mealtime.";
                    return $"{name} is restless. {DescribePetNeeds(state)}";
            }

            switch (care.Condition)
            {
                case CareCondition.Recovering:
                    return $"{name} is getting better. Medicine goes with meals: one dose at breakfast and one at dinner.";
                case CareCondition.UnderTheWeather:
                    return $"{name} is a bit under the weather: sniffly and sleepy. A visit to {VetName} the vet, " +
                        "or a good day of care, will help.";
            }

            if (state.ShySkill != MotorSkill.None)
                return $"{name} feels a little shy about {SkillGerund(state.ShySkill)} and naps instead of playing for now. " +
                    "A good day of meals on time and petting will help.";
            if (care.LastReason == CareReason.AbsenceCovered)
                return $"Welcome back! A neighbour popped in to check on {name} while you were away.";
            return care.IsMessy ? $"{name} scratched the rug. A good brush settles them and tidies up." : null;
        }

        // Words for the answers a cat gives on the care ladder, or null for the species' usual cue line.
        private static string CareCueText(PetState state, PetVerb verb, PetCue cue)
        {
            var name = state.Name;
            var course = CareTunables.Default.RecoveryDoses;
            switch (cue)
            {
                case PetCue.TooFull:
                    return $"{name} is too full and leaves most of it. Try again at the next mealtime.";
                case PetCue.PushesToyAway:
                    return $"{name} is too grumpy to play and swats the wand away. Gentle petting will help.";
                case PetCue.TooPoorly:
                    return $"{name} feels too poorly for that and stays curled up. Meals, petting and {VetName} will help.";
                case PetCue.Bored:
                    return $"{name} looks a little bored of that. Try something different!";
                case PetCue.SkillBack:
                    return $"{name} is {SkillGerund(state.NewestSkill)} again! A good day of meals and petting helped.";
                case PetCue.MedicineTaken:
                    return $"{name} takes the medicine hidden in a treat. Dose {state.Care?.MedicineDoses ?? 1} of {course}; " +
                        "the next one goes with another meal.";
                case PetCue.MedicineDone:
                    return $"{name} takes the last dose and feels much better. Well again!";
                case PetCue.MedicineNotNow:
                    switch (PetSimulation.Medicine(state))
                    {
                        case MedicineState.GivenThisMeal:
                            return $"{name} already had medicine with this meal. The next dose goes with another meal.";
                        case MedicineState.WaitForMeal:
                            return "Medicine goes with a meal: breakfast or dinner.";
                        default:
                            return $"{name} is well and doesn't need any medicine.";
                    }
                case PetCue.VetVisited:
                    return $"{VetName} listens, checks a temperature and says {name} needs rest and medicine. " +
                        "One dose with breakfast and one with dinner will help.";
                case PetCue.VetAllClear:
                    return $"{VetName} listens, checks a temperature and says {name} is doing fine. " +
                        "Meals on time and naps will keep it that way.";
            }

            if (state.Care == null)
                return null;
            switch (verb)
            {
                case PetVerb.Feed when state.Care.LastReason == CareReason.MealOnTime:
                    return $"{name} crunches happily, right on time, and licks their whiskers.";
                case PetVerb.Feed when state.Care.LastReason == CareReason.MealLate:
                    return $"{name} crunches happily. A little late, but very welcome.";
                case PetVerb.Wand when state.Care.LastReason == CareReason.PlayOnTime:
                    return $"Playtime! {name} wiggles, pounces and bats the wand toy.";
                case PetVerb.Wand when state.ShySkill != MotorSkill.None:
                    return $"{name} watches the wand from a cosy spot. Still a little shy about {SkillGerund(state.ShySkill)}.";
                case PetVerb.Brush when state.Care.LastReason == CareReason.WashOnTime || state.Care.LastReason == CareReason.WashLate:
                    return $"{name}'s coat is soft and shiny after the daily brush, and the rug is tidy again.";
                default:
                    return null;
            }
        }

        // Home's mood line for a pet on the care ladder, or null for the usual needs line.
        private static string CareMoodLine(PetState state)
        {
            var care = state.Care;
            if (care == null)
                return null;
            if (care.IsAway)
                return "Fine while you're away";
            if (care.Condition == CareCondition.Sick)
                return "Feeling poorly · the vet can help";
            if (care.Mood == CareMood.Upset)
                return "A little grumpy";
            if (care.Mood == CareMood.Fussy)
                return "A bit restless";
            if (care.Condition == CareCondition.UnderTheWeather)
                return "A bit under the weather";
            if (care.Condition == CareCondition.Recovering)
                return "Getting better";
            return state.ShySkill != MotorSkill.None ? "A bit shy today" : null;
        }
    }
}
