using System;
using System.Collections.Generic;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // A pet's room: the nursery's top bar (name, age, Bond, the Home and switch chips) with the species'
    // meters, the pet on the floor at its layout scale, and care buttons for the verbs the species has.
    // The cue line answers each care action; a nap, or a sleepy pet, shows the rest pose, and growing up
    // swaps to the Grown poses with a celebration.
    public sealed partial class NurseryPreview
    {
        private const double SleepyEnergy = 30;

        private readonly NeedMeterSlot[] petMeters = new NeedMeterSlot[PetCatalog.MaxVisibleMeters];
        private readonly List<Button> petVerbButtons = new List<Button>();
        private GameObject petCare;
        private RectTransform petVerbArea;
        private Text petStatus;
        private Species? petVerbsFor;
        private int petSlot;

        private void BuildPetRoom()
        {
            // The pet's meters live in the nursery's top bar, two to a row where the baby's sit.
            var top = (RectTransform)header.transform.Find("Content");
            petMeters[0] = new NeedMeterSlot(Meter("Pet need 1", top, 0, 0.5f, 61));
            petMeters[1] = new NeedMeterSlot(Meter("Pet need 2", top, 0.5f, 1, 61));
            petMeters[2] = new NeedMeterSlot(Meter("Pet need 3", top, 0, 0.5f, 12));
            petMeters[3] = new NeedMeterSlot(Meter("Pet need 4", top, 0.5f, 1, 12));
            foreach (var meter in petMeters)
                meter.Root.gameObject.SetActive(false);

            var panel = Tray("Pet Care Panel", false, 378);
            petCare = panel.parent.gameObject;
            petStatus = Label("Pet Reaction", panel, "", 25, Ink, TextAnchor.MiddleLeft,
                new Vector2(0, 283), Vector2.right, new Vector2(0, 365));
            petStatus.resizeTextForBestFit = true;
            petStatus.resizeTextMinSize = 18;
            petStatus.resizeTextMaxSize = 25;
            petVerbArea = Box("Pet care buttons", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ActionButton("Pet preview next day", panel, 0, 1, 7, 91, PreviewPetNextDay, Mist, Ink)
                .GetComponentInChildren<Text>().text = "Preview next day";
            petCare.SetActive(false);
        }

        private void ShowPetRoom(HouseholdCompanion companion) => ShowPetRoom(companion, null);

        // Opens a pet's room. Switching never advances time; growing up while off screen is celebrated here, once.
        private void ShowPetRoom(HouseholdCompanion companion, string message)
        {
            if (companion?.PetState == null)
                return;

            petSlot = companion.Slot;
            view = HouseholdView.PetRoom;
            activePanel = NurseryPanel.Care;
            nursery.ResetPresentation();
            BuildPetVerbs(companion.PetState.Species);
            var celebration = TakePetCelebrations(out var failure);
            PaintPetRoom(celebration ?? message);
            var first = petVerbButtons.Count > 0 ? petVerbButtons[0] : null;
            if (first != null)
                first.Select();
            if (failure != null)
                Debug.LogException(failure);
        }

        // The minute tick and resume: every companion advances, and the room shows any growth.
        private void RefreshPetRoom()
        {
            Exception failure = null;
            try
            {
                household.AdvanceAll(activeOnScreen: false);
            }
            catch (Exception error)
            {
                failure = error;
            }

            if (FindCompanion(petSlot)?.PetState == null || household.IsResting(petSlot))
            {
                ShowHome();
                return;
            }

            var celebration = TakePetCelebrations(out var celebrationFailure);
            PaintPetRoom(celebration, keepStatus: celebration == null);
            if (failure != null)
                ReportFailure(failure, "Your progress couldn't be saved. Please try again.");
            else if (celebrationFailure != null)
                Debug.LogException(celebrationFailure);
        }

        private void CarePet(PetVerb verb)
        {
            PetCareOutcome outcome;
            try
            {
                outcome = household.ApplyPetCare(petSlot, verb);
            }
            catch (Exception error)
            {
                // The pet's record may hold the care while the household index waits for the next write.
                PaintPetRoom(null, keepStatus: true);
                ReportFailure(error, "This care couldn't be saved. Please try again.");
                return;
            }

            petStage.PlayCue(verb);
            var state = FindCompanion(petSlot)?.PetState;
            if (state == null)
                return;

            var text = CueText(state, verb, outcome.Cue);
            if (outcome.CompletedCareDay)
                text += $" That's a full day of care for {state.Name}.";
            foreach (var breed in outcome.UnlockedBreeds)
                text += $" New breed to adopt: {BreedName(breed)}!";
            var grown = TakePetCelebrations(out _);
            PaintPetRoom(grown != null ? grown + " " + text : text);
        }

        private void PreviewPetNextDay()
        {
            PetCareOutcome outcome;
            try
            {
                outcome = household.AdvancePetPreview(petSlot, TimeSpan.FromDays(1));
            }
            catch (Exception error)
            {
                PaintPetRoom(null, keepStatus: true);
                ReportFailure(error, "The next day couldn't be saved. Please try again.");
                return;
            }

            var state = FindCompanion(petSlot)?.PetState;
            var text = state == null ? null : $"A day later, {state.Name} is {(int)state.Age.TotalDays} days old.";
            foreach (var breed in outcome.UnlockedBreeds)
                text += $" New breed to adopt: {BreedName(breed)}!";
            var grown = TakePetCelebrations(out _);
            PaintPetRoom(grown ?? text);
        }

        // Plays this pet's pending celebrations once. Null when there is nothing to celebrate.
        private string TakePetCelebrations(out Exception failure)
        {
            failure = null;
            var state = FindCompanion(petSlot)?.PetState;
            if (state == null || !household.HasCelebrations(petSlot))
                return null;

            try
            {
                foreach (var milestone in household.TakeCelebrations(petSlot))
                {
                    if (milestone == CompanionMilestone.Grown)
                        return $"{state.Name} is all grown up! Look how big they are.";
                }
            }
            catch (Exception error)
            {
                // It stays pending and plays next time.
                failure = error;
            }

            return null;
        }

        private void PaintPetRoom(string message, bool keepStatus = false)
        {
            var companion = FindCompanion(petSlot);
            var state = companion?.PetState;
            if (state == null)
                return;

            ApplyHouseholdView();
            ApplyNurseryTheme(NurseryTheme.Sage);
            saveError.text = string.Empty;
            title.text = state.Name;
            var days = (int)state.Age.TotalDays;
            var stage = PetStageTitle(state.Species, state.Stage);
            age.text = days == 1 ? $"1 day old  ·  {stage}" : $"{days} days old  ·  {stage}";
            favorite.text = "Bond: " + BondTierName(state.BondTier);

            var species = household.PetCatalog.Get(state.Species);
            for (var i = 0; i < petMeters.Length; i++)
            {
                var shown = i < species.Meters.Count;
                petMeters[i].Shown = shown;
                petMeters[i].Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                var meter = species.Meters[i].Meter;
                petMeters[i].Label.text = MeterName(meter);
                petMeters[i].Set(state.Meter(meter));
            }

            foreach (var button in petVerbButtons)
                button.interactable = !household.IsResting(petSlot);

            petStage.Show(state.Appearance, household.PetCatalog, state.Stage, IsSleepy(state));
            if (message != null)
                petStatus.text = message;
            else if (!keepStatus || string.IsNullOrEmpty(petStatus.text))
                petStatus.text = DescribePetNeeds(state);
        }

        // One button per care verb the species has, three to a row above Preview next day.
        private void BuildPetVerbs(Species species)
        {
            if (petVerbsFor == species)
                return;

            petVerbButtons.Clear();
            for (var i = petVerbArea.childCount - 1; i >= 0; i--)
            {
                var child = petVerbArea.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            var verbs = household.PetCatalog.Get(species).Verbs;
            for (var i = 0; i < verbs.Count && i < 6; i++)
            {
                var verb = verbs[i].Verb;
                var column = i % 3;
                var row = i / 3;
                var button = ActionButton("Pet care " + verb, petVerbArea, column / 3f, (column + 1) / 3f,
                    195 - row * 96, 283 - row * 96, () => CarePet(verb), Fern, Color.white);
                var label = button.GetComponentInChildren<Text>();
                label.text = VerbTitle(verb);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 17;
                label.resizeTextMaxSize = 25;
                petVerbButtons.Add(button);
            }

            petVerbsFor = species;
        }

        private static bool IsSleepy(PetState state) => state.HasMeter(PetMeter.Energy) && state.Meter(PetMeter.Energy) < SleepyEnergy;

        private static string VerbTitle(PetVerb verb)
        {
            switch (verb)
            {
                case PetVerb.Wand: return "Wand play";
                case PetVerb.FluffBedding: return "Fluff bedding";
                case PetVerb.GentleHold: return "Gentle hold";
                case PetVerb.SprinkleFood: return "Sprinkle food";
                case PetVerb.WipeGlass: return "Wipe glass";
                case PetVerb.WaterChange: return "Water change";
                case PetVerb.FloorTime: return "Floor time";
                default: return verb.ToString();
            }
        }

        private static string MeterName(PetMeter meter) => meter.ToString();

        private static string BondTierName(BondTier tier)
        {
            switch (tier)
            {
                case BondTier.BestFriends: return "Best friends";
                case BondTier.Trusting: return "Trusting";
                default: return "Acquainted";
            }
        }

        private static string PetStageTitle(Species species, PetStage stage)
        {
            if (species == Species.Cat)
                return stage == PetStage.Grown ? "Grown cat" : "Kitten";

            var name = SpeciesName(species).ToLowerInvariant();
            return stage == PetStage.Grown ? "Grown " + name : "Young " + name;
        }

        // Warm cue lines; the cat's signature moments (purring, slow blinks, kneading, bringing a toy) grow
        // with Bond.
        private static string CueText(PetState state, PetVerb verb, PetCue cue)
        {
            var name = state.Name;
            if (cue == PetCue.IgnoredFlakes)
                return $"{name} is full and ignores the extra flakes. They cloud the water a little.";

            var tier = state.BondTier;
            switch (verb)
            {
                case PetVerb.Feed:
                    return $"{name} crunches happily and licks their whiskers.";
                case PetVerb.Pet:
                    return tier == BondTier.BestFriends ? $"{name} purrs and kneads the blanket beside you."
                        : tier == BondTier.Trusting ? $"{name} purrs and gives you a slow blink."
                        : $"{name} leans into your hand and starts to purr.";
                case PetVerb.Wand:
                    return tier == BondTier.BestFriends ? $"{name} pounces, then brings the toy back to you."
                        : $"{name} wiggles, pounces and bats the wand toy!";
                case PetVerb.Brush:
                    return $"{name}'s coat is soft and shiny after a gentle brush.";
                case PetVerb.Nap:
                case PetVerb.Rest:
                    return $"{name} curls up for a nap and wakes up rested.";
                default:
                    return $"{name} enjoys your company.";
            }
        }

        private static string DescribePetNeeds(PetState state)
        {
            const double low = 45;
            var name = state.Name;
            if (state.Health < low)
                return $"{name} seems a little off and could use some gentle care.";
            if (IsSleepy(state))
                return $"{name} is curled up and sleepy. A nap would help.";
            if (state.HasMeter(PetMeter.Fullness) && state.Meter(PetMeter.Fullness) < low)
                return $"{name} is circling the food bowl.";
            if (state.HasMeter(PetMeter.Comfort) && state.Meter(PetMeter.Comfort) < low)
                return $"{name} would love some petting.";
            if (state.HasMeter(PetMeter.Cleanliness) && state.Meter(PetMeter.Cleanliness) < low)
                return $"{name}'s coat could use a brush.";
            return state.IsGrown ? $"{name} is lounging in a sunbeam." : $"{name} is exploring the room.";
        }

        // A short Home mood line for a pet. needsCare tints its dot.
        private static string PetMoodLine(PetState state, out bool needsCare)
        {
            const double low = 45;
            needsCare = true;
            if (state.Health < low)
                return "Feeling a little off";
            if (IsSleepy(state))
                return "Ready for a catnap";
            if (state.HasMeter(PetMeter.Fullness) && state.Meter(PetMeter.Fullness) < low)
                return "A little peckish";
            if (state.HasMeter(PetMeter.Comfort) && state.Meter(PetMeter.Comfort) < low)
                return "Would love some petting";
            if (state.HasMeter(PetMeter.Cleanliness) && state.Meter(PetMeter.Cleanliness) < low)
                return "Could use a brush";

            needsCare = false;
            return state.IsGrown ? "Napping in a sunbeam" : "Happy and curious";
        }

        // A header meter whose label follows the species.
        private sealed class NeedMeterSlot
        {
            private readonly NeedMeter meter;

            public NeedMeterSlot(NeedMeter meter)
            {
                this.meter = meter;
                Label = meter.Root.Find("Label").GetComponent<Text>();
            }

            public RectTransform Root => meter.Root;
            public Text Label { get; }
            public bool Shown { get; set; }

            public void Set(double value) => meter.Set(value);
        }

        private sealed partial class JournalPanel
        {
            // A pet's journal: warm phrasing only, the pet counterpart of the baby's.
            public void ShowPet(PetState pet, bool resting)
            {
                Feedback.text = string.Empty;
                heading.text = $"{pet.Name}'s journal";
                state.text = resting
                    ? $"{pet.Name} is resting. They're safe, and their needs don't drop while they rest."
                    : $"{pet.Name} is awake in their room.";
                var days = (int)pet.Age.TotalDays;
                var stage = PetStageTitle(pet.Species, pet.Stage);
                age.text = days == 1 ? $"1 day old · {stage}" : $"{days} days old · {stage}";
                sitting.text = "All grown up · " + (pet.IsGrown ? "Yes" : "Not yet");
                crawling.text = "Breed · " + BreedName(pet.Breed) + ", " + pet.Appearance.Coat.ToString().ToLowerInvariant() + " coat";
                walking.text = "Bond · " + BondTierName(pet.BondTier);
                favoriteToy.text = "Temperament · " + Temperament(pet);
                stories.text = pet.Appearance.Accessory == PetAccessory.None ? "No collar" : "Wears a " + pet.Appearance.AccessoryAccent.ToString().ToLowerInvariant() + " collar";
                blocks.text = string.Empty;
                connection.text = $"Bond · {Mathf.RoundToInt((float)pet.Bond)} / 100";
            }

            private static string Temperament(PetState pet)
            {
                var lively = pet.Liveliness >= 15 ? "lively" : pet.Liveliness <= -15 ? "mellow" : "easygoing";
                var bold = pet.Boldness >= 15 ? "bold" : pet.Boldness <= -15 ? "a little shy" : "curious";
                return lively + " and " + bold;
            }
        }
    }
}
