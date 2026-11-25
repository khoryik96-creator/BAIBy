using System;
using System.Collections.Generic;
using BAIby.Application;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Helpers (monetization M4): a safety net, not a carer. For a baby they are the Sitter, who pops in; for a cat they
    // are objects (an auto-feeder, a ball launcher, a self-cleaning litter box), and for a hamster a seed dispenser, a
    // spare wheel and a self-cleaning litter corner, and for a dog an auto-feeder, a ball launcher and a doggy door with a
    // paw mat. No helper ever walks a dog: the walk windows always ask for a walk. What shows:
    // - a small floor marker on each need ring an active helper holds;
    // - the return summary in the speech bubble, helper events first and the attention line last;
    // - muted journal days (NurseryPreview.Journal.cs);
    // - the feeder's stock and a hands-on "stock" control in the Routine sheet, and a stock chip in the cat's room;
    // - the Helpers panel in Grown-ups (behind the parental gate) to switch each owned helper on or off.
    // Nothing here links to a shop, and nothing appears unless the household owns a helper.
    public sealed partial class NurseryPreview
    {
        private const string HelperCopy = "Helpers keep things steady while you're busy. Only you can help {0} grow.";
        private HelpersPanel helpersPanel;
        // The return summary stays in the bubble until the player cares, for the companion it was written for: what the
        // helpers did, then (when there is one) the condition line, then the attention line.
        private string helperNote;
        private string helperAttention;
        private int helperNoteSlot;
        private (Image Pill, Text Label, Image Mark) feederChip;

        private void ShowHelpersPanel()
        {
            if (household == null)
                return;

            view = HouseholdView.Helpers;
            ApplyHouseholdView();
            helpersPanel.Show(household, playtest, HelperCopyFor(household.Active?.Name));
            helpersPanel.Close.Select();
        }

        private static string HelperCopyFor(string name) =>
            string.Format(HelperCopy, string.IsNullOrEmpty(name) ? "your little ones" : name);

        private void ToggleHelper(HelperKind kind)
        {
            if (household == null || !household.OwnsHelper(kind))
                return;

            var on = !household.IsHelperSwitchedOn(kind);
            try
            {
                household.SetHelperOn(kind, on);
            }
            catch (Exception error)
            {
                ReportFailure(error, "That setting couldn't be saved. Please try again.");
                return;
            }

            helpersPanel.Show(household, playtest, HelperCopyFor(household.Active?.Name));
            helpersPanel.Feedback.text = $"{HelperName(kind, false)} {(on ? "is on" : "is off")} for the whole household.";
        }

        // Test builds only: the whole set as a playtest grant, like the playtest rooms.
        private void ToggleTestHelpers()
        {
            if (playtest == null)
                return;

            var adding = !playtest.Owns(ProductIds.HelperSet);
            try
            {
                playtest.Set(ProductIds.HelperSet, adding);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The playtest helpers couldn't be changed. Please try again.");
                return;
            }

            helpersPanel.Show(household, playtest, HelperCopyFor(household.Active?.Name));
            helpersPanel.Feedback.text = adding
                ? "Test helpers added. Stock the feeder in the Routine sheet, then try Next day or an absence."
                : "Test helpers removed. What they already did stays.";
        }

        // Which needs a helper holds right now, for the ring markers: switched on, owned, and the feeder stocked.
        private HelperSettings ActiveHelpers(int stock)
        {
            var settings = household?.Helpers ?? HelperSettings.None;
            return settings.Feeder && stock <= 0 ? settings.With(HelperKind.Feeder, false) : settings;
        }

        private void ApplyBabyFloors(BabyState state)
        {
            var active = ActiveHelpers(state?.Care.Helpers.Stock ?? 0);
            var rules = HelperRules.Default;
            satiety.SetFloor(active.Feeder ? rules.FeederFloor : (double?)null);
            comfort.SetFloor(active.Play ? rules.PlayFloor : (double?)null);
            cleanliness.SetFloor(active.Tidy ? rules.TidyFloor : (double?)null);
            energy.SetFloor(null);
            health.SetFloor(null);
        }

        private double? PetFloor(Simulation.Pets.PetState state, Simulation.Pets.PetMeter meter)
        {
            if (state == null || !Simulation.Pets.PetSimulation.HasHelpers(state))
                return null;

            var active = ActiveHelpers(state.Care?.Helpers.Stock ?? 0);
            var rules = HelperRules.Default;
            // By the role the meter plays: a hamster's Exercise is held by the spare wheel, its Bedding by the litter corner.
            switch (Simulation.Pets.PetCareProfile.For(state.Species)?.RoleOf(meter) ?? meter)
            {
                case Simulation.Pets.PetMeter.Fullness: return active.Feeder ? rules.FeederFloor : (double?)null;
                case Simulation.Pets.PetMeter.Comfort: return active.Play ? rules.PlayFloor : (double?)null;
                case Simulation.Pets.PetMeter.Cleanliness: return active.Tidy ? rules.TidyFloor : (double?)null;
                default: return null;
            }
        }

        // What the helpers did since the bubble last spoke, as one line (null when nothing). The full return summary,
        // with the attention line last, stays in the bubble until the player cares.
        private string TakeHelperLine(int slot, string name, bool pet)
        {
            if (household == null || slot == 0)
                return null;

            IReadOnlyList<HelperEvent> events;
            try
            {
                events = household.TakeHelperEvents(slot);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                return null;
            }

            if (events.Count == 0)
                return null;

            var line = HelperLine(name, pet, events);
            helperNote = line;
            helperAttention = AttentionLine(name, pet);
            helperNoteSlot = slot;
            return line;
        }

        // The return summary for this companion, with `middle` (a condition line) between the helpers and the attention
        // line; null when there is none.
        private string HelperNoteFor(int slot, string middle = null) =>
            helperNoteSlot != slot || helperNote == null ? null
                : helperNote + (string.IsNullOrEmpty(middle) ? "" : " " + middle) + " " + helperAttention;

        private void ClearHelperNote() => helperNote = null;

        // "While you were busy, the Sitter gave Mia a bottle and freshened them up."
        private static string HelperLine(string name, bool pet, IReadOnlyList<HelperEvent> events)
        {
            int feeds = 0, plays = 0, tidies = 0;
            foreach (var helped in events)
            {
                switch (helped.Kind)
                {
                    case HelperKind.Feeder: feeds++; break;
                    case HelperKind.Play: plays++; break;
                    default: tidies++; break;
                }
            }

            var parts = new List<string>();
            if (pet)
            {
                if (HamsterShown)
                {
                    if (feeds > 0)
                        parts.Add(feeds == 1 ? $"the seed dispenser served {name} a meal" : $"the seed dispenser served {name} {feeds} meals");
                    if (plays > 0)
                        parts.Add(plays == 1 ? "the spare wheel kept them busy" : $"the spare wheel kept them busy {plays} times");
                    if (tidies > 0)
                        parts.Add("the litter corner cleaned itself");
                    return "While you were busy, " + Join(parts) + ".";
                }

                if (feeds > 0)
                    parts.Add(feeds == 1 ? $"the auto-feeder served {name} a meal" : $"the auto-feeder served {name} {feeds} meals");
                if (plays > 0)
                    parts.Add(plays == 1 ? "the ball launcher kept them busy" : $"the ball launcher kept them busy {plays} times");
                if (tidies > 0)
                    parts.Add(DogShown ? "the doggy door's paw mat kept them clean" : "the litter box cleaned itself");
                return "While you were busy, " + Join(parts) + "." + (DogShown ? " Walks still need you." : "");
            }

            // The Sitter pops in: one subject, and the baby's name in the first thing they did.
            if (feeds > 0)
                parts.Add(feeds == 1 ? $"gave {name} a bottle" : $"gave {name} {feeds} bottles");
            if (plays > 0)
                parts.Add((parts.Count == 0 ? $"played peekaboo with {name}" : "played peekaboo") + Times(plays));
            if (tidies > 0)
                parts.Add((parts.Count == 0 ? $"freshened {name} up" : "freshened them up") + Times(tidies));
            return "While you were busy, the Sitter " + Join(parts) + ".";
        }

        // Only hands-on care resolves the attention axis, and the line says so plainly.
        private static string AttentionLine(string name, bool pet) =>
            !pet ? $"{name} missed you; a cuddle or story will help."
                : HamsterShown ? $"{name} missed you; a gentle hold or wheel time will help."
                : DogShown ? $"{name} missed you; a walk or a game of fetch will help."
                : $"{name} missed you; some petting or wand play will help.";

        private static string Times(int count) => count == 1 ? "" : count == 2 ? " twice" : $" {count} times";

        private static string Join(List<string> parts)
        {
            if (parts.Count == 1)
                return parts[0];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        private static string HelperName(HelperKind kind, bool pet)
        {
            switch (kind)
            {
                case HelperKind.Feeder: return pet ? HamsterShown ? "The seed dispenser" : "The auto-feeder" : "Feeding";
                case HelperKind.Play: return pet ? HamsterShown ? "The spare wheel" : "The ball launcher" : "Play";
                default: return pet ? HamsterShown ? "The litter corner" : DogShown ? "The doggy door" : "The litter box" : "Tidy";
            }
        }

        // The Routine sheet's helper row for the companion on screen; hidden without an owned, switched-on helper.
        private void UpdateRoutineHelpers(string name, bool pet, int stock, int capacity)
        {
            var settings = household?.Helpers ?? HelperSettings.None;
            if (!settings.Any)
            {
                routine.HideHelpers();
                return;
            }

            string line;
            if (settings.Feeder)
                line = !pet ? $"The Sitter's bottles: {stock} of {capacity}."
                    : HamsterShown ? $"Seed dispenser: {stock} of {capacity} portions." : $"Auto-feeder: {stock} of {capacity} portions.";
            else
                line = !pet ? "The Sitter pops in to play and tidy."
                    : HamsterShown ? "The spare wheel and litter corner are on."
                    : DogShown ? "The ball launcher and doggy door are on. Walks always need you."
                    : "The ball launcher and litter box are on.";
            routine.ShowHelpers(HelperCopyFor(name), line, settings.Feeder,
                pet ? (stock >= capacity ? "Feeder full" : "Fill the feeder") : (stock >= capacity ? "Bottles ready" : "Leave bottles"),
                stock < capacity);
        }

        // Hands-on and free: the player leaves the Sitter a full set of bottles, or fills the cat's feeder.
        private void StockFeeder()
        {
            if (view == HouseholdView.PetRoom)
            {
                try
                {
                    household.StockPetFeeder(petSlot);
                }
                catch (Exception error)
                {
                    PaintPetRoom(null, keepStatus: true);
                    ReportFailure(error, "The feeder couldn't be saved. Please try again.");
                    return;
                }

                var pet = FindCompanion(petSlot)?.PetState;
                PaintPetRoom(pet == null ? null : $"The feeder is full: {pet.Care?.Helpers.Stock ?? 0} portions for {pet.Name}.");
                routine.Summary.text = HamsterShown
                    ? "The seed dispenser serves a portion only when the bowl runs low."
                    : "The auto-feeder serves a portion only when the bowl runs low.";
                return;
            }

            if (session?.State == null)
                return;

            try
            {
                Paint(Reaction.None, session.StockFeeder());
            }
            catch (Exception error)
            {
                ReportFailure(error, "The bottles couldn't be saved. Please try again.");
                return;
            }

            routine.Summary.text = $"The Sitter has {session.FeederStock} bottles ready, for when {session.State.Name} gets hungry.";
        }

        // The cat's room shows the feeder's stock as a small chip at the sheet's top-left, like a bowl on the floor.
        private void UpdateFeederChip(Simulation.Pets.PetState state)
        {
            var shown = state != null && Simulation.Pets.PetSimulation.HasHelpers(state) && (household?.Helpers.Feeder ?? false);
            if (feederChip.Pill == null)
            {
                if (!shown)
                    return;
                feederChip = Pill("Feeder Stock", petCare.transform, "", Paper, Ink, Muted, "Bowl");
                SoftShapes.Lift(feederChip.Pill.gameObject);
            }

            var root = (RectTransform)feederChip.Pill.transform;
            if (root.gameObject.activeSelf != shown)
                petBubbleText = null; // the speech bubble moves up or down with the chip on its next layout
            root.gameObject.SetActive(shown);
            if (!shown)
                return;

            var stock = state.Care?.Helpers.Stock ?? 0;
            feederChip.Label.text = $"{stock}/{HelperRules.Default.MaxStock}";
            feederChip.Label.color = stock == 0 ? Muted : Ink;
            var width = PillWidth(feederChip.Label, Px(10) + 36);
            root.anchorMin = new Vector2(0, 1);
            root.anchorMax = new Vector2(0, 1);
            root.offsetMin = new Vector2(SideMargin, 12);
            root.offsetMax = new Vector2(SideMargin + width, 12 + 52);
        }

        // The pet's speech bubble sits above the feeder chip when the chip shows, so neither covers the other.
        private float FeederChipLift => feederChip.Pill != null && feederChip.Pill.gameObject.activeSelf ? 60 : 0;

        // Grown-ups: switch each owned helper on or off for the household, behind the parental gate. Test builds add a
        // playtest toggle for the whole set; a build without a store says helpers are coming. No prices, no prompts.
        private sealed class HelpersPanel
        {
            private static readonly HelperKind[] Kinds = { HelperKind.Feeder, HelperKind.Play, HelperKind.Tidy };
            private readonly Text detail;
            private readonly Button[] toggles = new Button[3];
            private readonly Text[] toggleLabels = new Text[3];
            private readonly Button test;
            private readonly Text testLabel;

            public GameObject Root { get; }
            public Button Close { get; }
            public Text Feedback { get; }

            public HelpersPanel(RectTransform content, Action close, Action<HelperKind> toggle, Action toggleTest)
            {
                Root = content.parent.gameObject;
                var heading = Label("Helpers Heading", content, "Helpers", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                PlaceFromTop(heading.rectTransform, 0, 0.72f, 14, 76);
                Close = ActionButton("Close helpers", content, 0.73f, 1, 0, 0, close, Mist, Ink);
                Close.GetComponentInChildren<Text>().text = "Done";
                PlaceFromTop((RectTransform)Close.transform, 0.73f, 1, 16, 76, 3, 3);
                SoftShapes.Round(Close.image, 1f);

                detail = Label("Helpers Detail", content, "", 23, Ink, TextAnchor.UpperLeft, Vector2.zero, Vector2.one,
                    Vector2.zero);
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 15;
                detail.resizeTextMaxSize = 23;
                PlaceFromTop(detail.rectTransform, 0, 1, 88, 226);

                for (var i = 0; i < Kinds.Length; i++)
                {
                    var kind = Kinds[i];
                    var button = ActionButton("Helper " + kind, content, i / 3f, (i + 1) / 3f, 0, 0, () => toggle(kind), Mist, Ink);
                    PlaceFromTop((RectTransform)button.transform, i / 3f, (i + 1) / 3f, 238, 320, 3, 3);
                    var label = button.GetComponentInChildren<Text>();
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 15;
                    label.resizeTextMaxSize = 23;
                    SoftShapes.Round(button.image, 1f);
                    SoftShapes.Lift(button.gameObject);
                    toggles[i] = button;
                    toggleLabels[i] = label;
                }

                test = ActionButton("Playtest helpers toggle", content, 0, 1, 0, 0, toggleTest, Fern, Color.white);
                testLabel = test.GetComponentInChildren<Text>();
                PlaceFromTop((RectTransform)test.transform, 0, 1, 332, 414, 3, 3);
                SoftShapes.Round(test.image, 1f);
                SoftShapes.Lift(test.gameObject);

                Feedback = Label("Helpers Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                    Vector2.zero);
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 15;
                Feedback.resizeTextMaxSize = 21;
                PlaceFromTop(Feedback.rectTransform, 0, 1, 424, 492);
                Root.SetActive(false);
            }

            public void Show(HouseholdSession household, IPlaytestUnlocks playtest, string copy)
            {
                Feedback.text = string.Empty;
                var owned = household.OwnsAnyHelper;
                var text = copy + " For a baby the Sitter pops in; a cat has an auto-feeder, a ball launcher and a " +
                    "self-cleaning litter box, a hamster a seed dispenser, a spare wheel and a self-cleaning litter corner, " +
                    "and a dog an auto-feeder, a ball launcher and a doggy door (walks always need you). " +
                    "They act only when a need runs low, and never count as care.";
                if (!owned)
                    text += playtest == null
                        ? " Helpers are coming to the app stores later."
                        : " This test build has no shop and nothing is bought.";
                detail.text = text;
                for (var i = 0; i < Kinds.Length; i++)
                {
                    var kind = Kinds[i];
                    var has = household.OwnsHelper(kind);
                    var on = has && household.IsHelperSwitchedOn(kind);
                    toggles[i].interactable = has;
                    toggles[i].image.color = on ? FernSoft : Mist;
                    toggleLabels[i].color = has ? (on ? FernDeep : Ink) : Muted;
                    toggleLabels[i].text = Title(kind) + (has ? on ? " · On" : " · Off" : " · Not owned");
                }

                test.gameObject.SetActive(playtest != null);
                if (playtest != null)
                    testLabel.text = playtest.Owns(ProductIds.HelperSet) ? "Remove test helpers" : "Add test helpers";
            }

            private static string Title(HelperKind kind)
            {
                switch (kind)
                {
                    case HelperKind.Feeder: return "Feeding";
                    case HelperKind.Play: return "Play";
                    default: return "Tidy";
                }
            }
        }
    }
}
