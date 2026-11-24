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

        private static readonly Color[] TileTints = { Peach, Lilac, Sky, Mint, Butter, Blush };

        private readonly NeedMeterSlot[] petMeters = new NeedMeterSlot[PetCatalog.MaxVisibleMeters];
        private readonly List<Button> petVerbButtons = new List<Button>();
        private GameObject petCare;
        private RectTransform petHead;
        private RectTransform petVerbArea;
        private Text petTitle;
        private Text petBond;
        private Text petStatus;
        private Image petBubble;
        private RectTransform petBubbleRect;
        private string petBubbleText;
        private Button petNextDay;
        private Species? petVerbsFor;
        private int petSlot;

        // The pet's room follows the nursery's pattern: its rings in the needs card, and a care sheet with one
        // pastel tile per care verb above Next day.
        private void BuildPetRoom()
        {
            for (var i = 0; i < petMeters.Length; i++)
            {
                petMeters[i] = new NeedMeterSlot(Meter("Pet need " + (i + 1), needsCard, "", Fern));
                petMeters[i].Root.gameObject.SetActive(false);
            }

            var panel = Tray("Pet Care Panel", false, 378);
            petCare = panel.parent.gameObject;
            petHead = Box("Pet Care Heading", panel, Vector2.zero, Vector2.right, Vector2.zero, Vector2.zero);
            petTitle = Label("Pet Care Title", petHead, "", 36, Ink, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
            FitText(petTitle, 26, 36);
            petBond = Label("Pet Bond", petHead, "", 22, Secondary, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero);
            FitText(petBond, 16, 22);
            petVerbArea = Box("Pet care buttons", panel, Vector2.zero, Vector2.right, Vector2.zero, Vector2.zero);
            petNextDay = ActionButton("Pet preview next day", panel, 0, 1, 0, 0, PreviewPetNextDay, Paper, Ink);
            WithIcon(petNextDay, "Next day", "NextDay", Ink);

            petBubbleRect = Box("Pet Speech Bubble", petCare.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero);
            petBubbleRect.pivot = Vector2.zero;
            petBubble = Surface(petBubbleRect, Paper);
            SoftShapes.Bubble(petBubble, true, Px(16));
            SoftShapes.Lift(petBubble.gameObject);
            petStatus = Label("Pet Reaction", petBubbleRect, "", 24, Ink, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero);
            petStatus.rectTransform.offsetMin = new Vector2(Px(12), Px(8));
            petStatus.rectTransform.offsetMax = new Vector2(-Px(12), -Px(8));
            petCare.SetActive(false);
        }

        // Lays out the pet's care sheet like the nursery's: heading, tiles in two rows, Next day.
        private void LayoutPetSheet()
        {
            if (petVerbArea == null || safeArea == null)
                return;

            var metrics = Metrics();
            PlaceRow(new[] { petNextDay }, metrics.Bottom, metrics.Row, metrics.Gap, StackWidth);
            var y = metrics.Bottom + metrics.Row + metrics.Gap + 4;
            var tiles = new List<(Button Button, float Weight)>();
            foreach (var button in petVerbButtons)
                tiles.Add((button, 1));
            LayoutTiles(petVerbArea, tiles, y, metrics.Tile, metrics.Gap);
            y += 2 * metrics.Tile + metrics.Gap * 2 + 6;
            petHead.offsetMin = new Vector2(0, y);
            petHead.offsetMax = new Vector2(0, y + metrics.Heading);
            var right = 0f;
            if (switchChip != null && switchChip.gameObject.activeSelf && switchChip.transform.parent == petHead)
                right = PlacePill((RectTransform)switchChip.transform, switchLabel, right, metrics.Heading, 260, 0);
            LayoutTitleBlock(petTitle, petBond, right, metrics.Heading);
            var sheet = (RectTransform)petCare.transform;
            sheet.offsetMax = new Vector2(sheet.offsetMax.x, y + metrics.Heading + SheetBottomPad + SheetTopPad);
            LayoutBubble(petBubble, petBubbleRect, petStatus, sheet);
            petBubbleText = petStatus.text;
        }

        private void UpdatePetBubble()
        {
            if (petStatus == null || petStatus.text == petBubbleText)
                return;
            LayoutBubble(petBubble, petBubbleRect, petStatus, (RectTransform)petCare.transform);
            petBubbleText = petStatus.text;
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
            age.text = DayLine(state.Age, PetStageTitle(state.Species, state.Stage));
            favorite.text = string.Empty;
            petTitle.text = "Care for " + state.Name;
            petBond.text = "Bond: " + BondTierName(state.BondTier);

            var species = household.PetCatalog.Get(state.Species);
            var shownMeters = new List<NeedMeter>();
            for (var i = 0; i < petMeters.Length; i++)
            {
                var shown = i < species.Meters.Count;
                petMeters[i].Shown = shown;
                petMeters[i].Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                var meter = species.Meters[i].Meter;
                petMeters[i].Label.text = MeterName(meter);
                petMeters[i].Meter.Tint(MeterColor(meter));
                petMeters[i].Set(state.Meter(meter));
                shownMeters.Add(petMeters[i].Meter);
            }

            PlaceMeters(shownMeters);

            foreach (var button in petVerbButtons)
                button.interactable = !household.IsResting(petSlot);

            petStage.Show(state.Appearance, household.PetCatalog, state.Stage, IsSleepy(state));
            if (message != null)
                petStatus.text = message;
            else if (!keepStatus || string.IsNullOrEmpty(petStatus.text))
                petStatus.text = DescribePetNeeds(state);
            LayoutTopStack();
            LayoutPetSheet();
        }

        // One pastel tile per care verb the species has, in two rows above Next day.
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
                var button = CareTile("Pet care " + verb, petVerbArea, VerbTitle(verb), VerbIcon(verb),
                    TileTints[i % TileTints.Length], () => CarePet(verb));
                petVerbButtons.Add(button);
            }

            petVerbsFor = species;
            SettleText(petVerbArea);
            LayoutPetSheet();
        }

        private static string VerbIcon(PetVerb verb)
        {
            switch (verb)
            {
                case PetVerb.Feed:
                case PetVerb.Bowl:
                case PetVerb.SprinkleFood:
                case PetVerb.Hay:
                    return "Bowl";
                case PetVerb.Pet:
                    return "Pet";
                case PetVerb.Wand:
                    return "Wand";
                case PetVerb.Brush:
                case PetVerb.Groom:
                    return "Brush";
                case PetVerb.Nap:
                case PetVerb.Rest:
                case PetVerb.FluffBedding:
                    return "Rest";
                case PetVerb.Fetch:
                case PetVerb.Wheel:
                    return "Play";
                case PetVerb.GentleHold:
                    return "Soothe";
                case PetVerb.WipeGlass:
                case PetVerb.WaterChange:
                case PetVerb.Litter:
                    return "Wash";
                case PetVerb.Greens:
                    return "Growth";
                default:
                    return "Paw";
            }
        }

        private static Color MeterColor(PetMeter meter)
        {
            switch (meter)
            {
                case PetMeter.Fullness:
                case PetMeter.Hay:
                    return FullnessRing;
                case PetMeter.Comfort:
                case PetMeter.Calm:
                case PetMeter.Bedding:
                    return ComfortRing;
                case PetMeter.Energy:
                case PetMeter.Exercise:
                    return EnergyRing;
                case PetMeter.Cleanliness:
                case PetMeter.Water:
                case PetMeter.Litter:
                    return CleanRing;
                default:
                    return HealthRing;
            }
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

        // The ring's label; Cleanliness reads "Clean", as on the baby's card.
        private static string MeterName(PetMeter meter) => meter == PetMeter.Cleanliness ? "Clean" : meter.ToString();

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
            public NeedMeterSlot(NeedMeter meter)
            {
                Meter = meter;
                Label = meter.Root.Find("Label").GetComponent<Text>();
            }

            public NeedMeter Meter { get; }
            public RectTransform Root => Meter.Root;
            public Text Label { get; }
            public bool Shown { get; set; }

            public void Set(double value) => Meter.Set(value);
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
