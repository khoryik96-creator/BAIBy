using System;
using System.Collections.Generic;
using BAIby.Application;
using BAIby.Character;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Adoption: "Who's joining your home?" (a baby or a pet), then for a pet the species, a breed picker
    // with a live preview in the room, and a name. Unshipped species are dimmed "Arriving soon" tiles with
    // no lock or price. Locked breeds can be previewed; they say how they are earned, or that they are
    // premium, and only adoptable breeds can be chosen.
    public sealed partial class NurseryPreview
    {
        // Species in the order they arrive (docs/pet-mode.md decision 5). Only shipped species can be chosen.
        private static readonly Species[] SpeciesOrder = { Species.Cat, Species.Hamster, Species.Dog, Species.Fish, Species.Rabbit };
        private static readonly Species[] ShippedSpecies = { Species.Cat };
        // Collar choices: none, then outfit accents so a collar can match a baby's outfit.
        private static readonly Outfit?[] CollarChoices = { null, Outfit.Sage, Outfit.Sky, Outfit.Blush, Outfit.Midnight };

        private PetVisuals petStage;
        private ChoicePanel choice;
        private PetAdoptionPanel petAdoption;
        private Species draftSpecies = Species.Cat;
        private PetBreed draftBreed = PetBreed.DomesticShorthair;
        private PetCoat draftCoat = PetCoat.Ginger;
        private int draftCollar = 1;

        private void BuildAdoptionPanels()
        {
            choice = new ChoicePanel(Tray("Adopt Choice Panel", false, 378), ShowHome, StartBabyAdoption, StartPetAdoption);
            petAdoption = new PetAdoptionPanel(Tray("Pet Adoption Panel", false, 378), PetAdoptionBack, ChooseSpecies,
                ChooseBreed, ChooseCoat, () => ShowPetAdoptionPage(2), CycleCollar, AdoptPet);
        }

        // "+ Adopt" and the first launch: choose a baby or a pet.
        private void StartAdoption()
        {
            if (household == null || (!household.CanAdopt && !household.CanAdoptPet))
                return;

            view = HouseholdView.Choose;
            nursery.ResetPresentation();
            Paint(Reaction.None);
            choice.Show(household.Companions.Count > 0, household.CanAdopt, household.CanAdoptPet);
            (household.CanAdopt ? choice.Baby : choice.Pet).Select();
        }

        private void StartPetAdoption()
        {
            if (household == null)
                return;
            if (!household.CanAdoptPet)
            {
                choice.Feedback.text = "There's no free room for a pet right now.";
                return;
            }

            draftSpecies = Species.Cat;
            draftBreed = PetBreed.DomesticShorthair;
            draftCoat = household.PetCatalog.Get(draftBreed).DefaultCoat;
            draftCollar = 1;
            petAdoption.NameField.text = string.Empty;
            view = HouseholdView.PetAdoption;
            petStage.PlayArrival();
            ShowPetAdoptionPage(0);
        }

        private void PetAdoptionBack()
        {
            if (petAdoption.Page > 0)
                ShowPetAdoptionPage(petAdoption.Page - 1);
            else
                StartAdoption();
        }

        private void ChooseSpecies(Species species)
        {
            if (Array.IndexOf(ShippedSpecies, species) < 0)
                return;

            if (species != draftSpecies)
            {
                draftSpecies = species;
                draftBreed = FirstBreed(species);
                draftCoat = household.PetCatalog.Get(draftBreed).DefaultCoat;
            }

            ShowPetAdoptionPage(1);
        }

        private void ChooseBreed(PetBreed breed)
        {
            draftBreed = breed;
            draftCoat = household.PetCatalog.Get(breed).DefaultCoat;
            petStage.PlayArrival();
            RefreshPetAdoption();
        }

        private void ChooseCoat(PetCoat coat)
        {
            draftCoat = coat;
            RefreshPetAdoption();
        }

        private void CycleCollar()
        {
            draftCollar = (draftCollar + 1) % CollarChoices.Length;
            RefreshPetAdoption();
        }

        private void ShowPetAdoptionPage(int page)
        {
            if (page == 2 && !household.CanAdoptBreed(draftBreed))
                page = 1;

            petAdoption.ShowPage(page);
            ApplyHouseholdView();
            title.text = "A new arrival";
            age.text = string.Empty;
            favorite.text = string.Empty;
            RefreshPetAdoption();
            petAdoption.FocusPage();
        }

        // Updates the picker and the live preview from the draft look.
        private void RefreshPetAdoption()
        {
            if (household == null || view != HouseholdView.PetAdoption)
                return;

            var catalog = household.PetCatalog;
            var breeds = new List<BreedDefinition>();
            foreach (var breed in catalog.BreedsOf(draftSpecies))
            {
                if (breed.AvailableNow)
                    breeds.Add(breed);
            }

            var statuses = new List<string>();
            var unlocked = new List<bool>();
            foreach (var breed in breeds)
            {
                var isUnlocked = household.IsBreedUnlocked(breed.Breed);
                unlocked.Add(isUnlocked);
                statuses.Add(BreedStatus(breed, isUnlocked));
            }

            petAdoption.ShowSpecies(draftSpecies);
            petAdoption.ShowBreeds(draftSpecies, breeds, statuses, unlocked, draftBreed);
            petAdoption.ShowCoats(catalog.Get(draftBreed).Coats, draftCoat);
            var adoptable = household.CanAdoptBreed(draftBreed);
            petAdoption.ShowBreedDetail(DescribeBreed(catalog.Get(draftBreed), adoptable), adoptable);
            var collar = CollarChoices[draftCollar];
            petAdoption.ShowName(SpeciesName(draftSpecies), collar == null ? "No collar" : "Collar: " + collar.Value);
            petStage.Show(DraftLook(), catalog, PetStage.Young, false);
        }

        private PetAppearance DraftLook()
        {
            var collar = CollarChoices[draftCollar];
            return new PetAppearance(draftBreed, draftCoat, null,
                collar == null ? PetAccessory.None : PetAccessory.Collar, collar ?? Outfit.Sage);
        }

        private PetBreed FirstBreed(Species species)
        {
            foreach (var breed in household.PetCatalog.BreedsOf(species))
            {
                if (breed.Unlock == BreedUnlock.Free)
                    return breed.Breed;
            }

            return household.PetCatalog.BreedsOf(species)[0].Breed;
        }

        private void AdoptPet()
        {
            var name = petAdoption.NameField.text;
            if (string.IsNullOrWhiteSpace(name))
            {
                petAdoption.Feedback.text = $"Give your {SpeciesName(draftSpecies).ToLowerInvariant()} a name to welcome them home.";
                return;
            }

            if (!PetState.IsSavableName(name))
            {
                petAdoption.Feedback.text = "That name has a character that can't be saved. Please try another.";
                return;
            }

            if (!household.CanAdoptBreed(draftBreed))
            {
                ShowPetAdoptionPage(1);
                return;
            }

            var before = household.Companions.Count;
            try
            {
                var companion = household.AdoptPet(name, DraftLook());
                petStage.PlayArrival();
                ShowPetRoom(companion, $"Welcome home, {companion.Name}! Have a look around your new room.");
            }
            catch (Exception error)
            {
                // The record is written before the manifest, so an adopted pet opens even when the manifest
                // write fails; the next launch lists it again.
                if (household.Companions.Count > before && view == HouseholdView.PetAdoption && household.Active?.PetSession != null)
                    ShowPetRoom(household.Active);
                ReportFailure(error, "Your new pet couldn't be saved. Please try again.");
            }
        }

        // Shows the adoption, pet room and pet stage pieces for the current view. The pet stands in the room
        // while its room, the picker or Home (with a pet active) is shown; the baby stands there otherwise.
        private void ApplyPetViews()
        {
            choice.Root.SetActive(view == HouseholdView.Choose);
            petAdoption.Root.SetActive(view == HouseholdView.PetAdoption);
            petCare.SetActive(view == HouseholdView.PetRoom);
            foreach (var meter in petMeters)
                meter.Root.gameObject.SetActive(view == HouseholdView.PetRoom && meter.Shown);

            var active = household?.Active;
            var petInRoom = view == HouseholdView.PetRoom || view == HouseholdView.PetAdoption;
            var petBehindHome = view != HouseholdView.Nursery && view != HouseholdView.Adoption && view != HouseholdView.Choose &&
                active?.PetState != null;
            if (petInRoom || petBehindHome)
            {
                nursery.Present(false, false, Reaction.None);
                nursery.SetCondition(CareMood.Content, 0);
            }

            nursery.SetBabyShown(!petInRoom && !petBehindHome);
            petStage.SetShown(petInRoom || petBehindHome);
            if (petBehindHome && !petInRoom)
                petStage.Show(active.PetState.Appearance, household.PetCatalog, active.PetState.Stage, IsSleepy(active.PetState));
            if (view != HouseholdView.Home)
                petStage.ReleasePortraits();
            if (petInRoom)
            {
                satiety.Root.gameObject.SetActive(false);
                comfort.Root.gameObject.SetActive(false);
                energy.Root.gameObject.SetActive(false);
                health.Root.gameObject.SetActive(false);
                cleanliness.Root.gameObject.SetActive(false);
            }

            if (view == HouseholdView.PetRoom)
                UpdateHouseholdChips(true);
        }

        private Sprite PetPortrait(PetState state) =>
            state == null || household == null ? null : petStage.PortraitFor(state.Appearance, household.PetCatalog, state.Stage);

        private string BreedStatus(BreedDefinition breed, bool unlocked)
        {
            switch (breed.Unlock)
            {
                case BreedUnlock.Free: return "Free";
                case BreedUnlock.Earned: return unlocked ? "Earned" : "Locked";
                default: return unlocked ? "Owned" : "Premium";
            }
        }

        private string DescribeBreed(BreedDefinition breed, bool adoptable)
        {
            var name = BreedName(breed.Breed);
            switch (breed.Unlock)
            {
                case BreedUnlock.Free:
                    return $"{name} · Always free. Every breed is cared for the same way.";
                case BreedUnlock.Earned:
                    if (adoptable)
                        return $"{name} · Earned. Ready to welcome home.";
                    return $"{name} · Locked. {GoalText(breed)}";
                default:
                    return adoptable
                        ? $"{name} · A premium breed you own."
                        : $"{name} · Premium. Preview it here; it isn't sold in this build.";
            }
        }

        private string GoalText(BreedDefinition breed)
        {
            var progress = household.ProgressFor(breed.Species);
            var kind = SpeciesName(breed.Species).ToLowerInvariant();
            switch (breed.Goal)
            {
                case EarnedGoal.FirstFullCareDay:
                    return $"Earn it with a full care day for a {kind}: 2 meals, petting or play, and a nap.";
                case EarnedGoal.Bond40:
                    return $"Earn it by reaching Bond {PetUnlocks.BondGoal:0} with any {kind} (best so far {progress.HighestBond:0}).";
                case EarnedGoal.RaisedToGrown:
                    return $"Earn it by raising a {kind} until they're grown.";
                case EarnedGoal.SevenOnTimeCareDays:
                    return $"Earn it with a care rhythm of {PetUnlocks.OnTimeCareDaysGoal} days for your {kind}s " +
                        $"(best so far {progress.OnTimeCareDays}).";
                default:
                    return "Earn it through care.";
            }
        }

        private static string SpeciesName(Species species)
        {
            switch (species)
            {
                case Species.Cat: return "Cat";
                case Species.Dog: return "Dog";
                case Species.Fish: return "Fish";
                case Species.Rabbit: return "Rabbit";
                case Species.Hamster: return "Hamster";
                default: return "Pet";
            }
        }

        // "DomesticShorthair" reads as "Domestic shorthair".
        private static string BreedName(PetBreed breed)
        {
            var raw = breed.ToString();
            var builder = new System.Text.StringBuilder(raw.Length + 4);
            for (var i = 0; i < raw.Length; i++)
            {
                var character = raw[i];
                if (i > 0 && char.IsUpper(character))
                {
                    builder.Append(' ');
                    builder.Append(char.ToLowerInvariant(character));
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        // The paw line icon, filling the given square.
        private static void PawGlyph(RectTransform area, string name, Color color)
        {
            var paw = Surface(Box(name, area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), color);
            paw.sprite = Icon("Paw");
            paw.preserveAspect = true;
            paw.enabled = paw.sprite != null;
        }

        // "Who's joining your home?" as two big pastel cards.
        private sealed class ChoicePanel
        {
            private readonly Button back;
            private readonly Text babyDetail;
            private readonly Text petDetail;

            public GameObject Root { get; }
            public Button Baby { get; }
            public Button Pet { get; }
            public Text Feedback { get; }

            public ChoicePanel(RectTransform content, Action backToHome, Action baby, Action pet)
            {
                Root = content.parent.gameObject;
                var heading = Label("Choice Heading", content, "Who's joining your home?", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                FitText(heading, 26, 38);
                PlaceFromTop(heading.rectTransform, 0, 0.72f, 4, 70);
                back = ActionButton("Choice back to home", content, 0.74f, 1, 0, 0, backToHome, Mist, Ink);
                back.GetComponentInChildren<Text>().text = "Home";
                PlaceFromTop((RectTransform)back.transform, 0.74f, 1, 4, 70, 3, 3);

                Baby = Card("Adopt a baby", "A baby", "Raise a little one", content, 0, 0.5f, baby, FernSoft,
                    out babyDetail, out var babyIcon);
                var portrait = Surface(babyIcon, Color.white);
                portrait.sprite = Resources.Load<Sprite>("BAIby/Baby/Newborn");
                portrait.preserveAspect = true;
                portrait.enabled = portrait.sprite != null;
                Pet = Card("Adopt a pet", "A pet", "A kitten to love", content, 0.5f, 1, pet, Peach,
                    out petDetail, out var petIcon);
                var paw = Box("Adopt a pet paw area", petIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-30, -30), new Vector2(30, 30));
                PawGlyph(paw, "Adopt a pet paw", Ink);

                Feedback = Label("Choice Feedback", content, "", 22, Ink, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero);
                FitText(Feedback, 15, 22);
                PlaceFromTop(Feedback.rectTransform, 0, 1, 326, 376);
                Root.SetActive(false);
            }

            public void Show(bool canGoHome, bool canAdoptBaby, bool canAdoptPet)
            {
                Feedback.text = string.Empty;
                back.gameObject.SetActive(canGoHome);
                Baby.interactable = canAdoptBaby;
                Pet.interactable = canAdoptPet;
                babyDetail.text = canAdoptBaby ? "Raise a little one" : "No free nursery";
                petDetail.text = canAdoptPet ? "A kitten to love" : "No free pet room";
            }

            private static Button Card(string name, string title, string detail, RectTransform content, float left, float right,
                Action action, Color tint, out Text detailText, out RectTransform icon)
            {
                var card = ActionButton(name, content, left, right, 0, 0, action, tint, Ink);
                PlaceFromTop((RectTransform)card.transform, left, right, 82, 320, left == 0 ? 0 : 9, left == 0 ? 9 : 0);
                SoftShapes.Radius(card.image, Px(26));
                var label = card.GetComponentInChildren<Text>();
                label.text = title;
                label.fontSize = 37;
                SetRole(label, TextRole.Heading);
                PlaceFromTop(label.rectTransform, 0, 1, 150, 194);
                detailText = Label(name + " Detail", card.transform, detail, 22, Muted, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero);
                FitText(detailText, 15, 22);
                PlaceFromTop(detailText.rectTransform, 0, 1, 196, 230, 12, 12);
                icon = Box(name + " Icon", card.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-60, -140), new Vector2(60, -20));
                SoftShapes.Disc(Surface(Box(name + " Icon Ground", card.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-60, -140), new Vector2(60, -20)), new Color(1, 1, 1, 0.8f)));
                icon.SetAsLastSibling();
                return card;
            }
        }

        private sealed class PetAdoptionPanel
        {
            private const int BreedColumns = 4;

            private readonly RectTransform speciesGroup;
            private readonly RectTransform breedGroup;
            private readonly RectTransform nameGroup;
            private readonly RectTransform breedGrid;
            private readonly RectTransform coatRow;
            private readonly Text heading;
            private readonly Text breedDetail;
            private readonly Button back;
            private readonly Button next;
            private readonly Button adopt;
            private readonly Button collar;
            private readonly Text collarLabel;
            private readonly Text nameHeading;
            private readonly Text namePlaceholder;
            private readonly Action<PetBreed> chooseBreed;
            private readonly Action<PetCoat> chooseCoat;
            private readonly Dictionary<Species, Button> speciesTiles = new Dictionary<Species, Button>();
            private readonly List<PickerTile> breedTiles = new List<PickerTile>();
            private readonly List<PickerTile> coatTiles = new List<PickerTile>();
            private Species? builtSpecies;
            private PetBreed? builtCoatsFor;

            public GameObject Root { get; }
            public InputField NameField { get; }
            public Text Feedback { get; }
            public int Page { get; private set; }

            public PetAdoptionPanel(RectTransform content, Action back, Action<Species> chooseSpecies, Action<PetBreed> chooseBreed,
                Action<PetCoat> chooseCoat, Action next, Action cycleCollar, Action adopt)
            {
                this.chooseBreed = chooseBreed;
                this.chooseCoat = chooseCoat;
                Root = content.parent.gameObject;
                heading = Label("Pet Adoption Heading", content, "", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                FitText(heading, 26, 38);
                PlaceFromTop(heading.rectTransform, 0, 0.72f, 0, 62);
                this.back = ActionButton("Pet adoption back", content, 0.74f, 1, 0, 0, back, Mist, Ink);
                this.back.GetComponentInChildren<Text>().text = "Back";
                PlaceFromTop((RectTransform)this.back.transform, 0.74f, 1, 0, 62, 3, 3);

                // Rounded species tiles: the cat is here (peach), the others are dimmed with "Arriving soon".
                speciesGroup = Group("Pet species page", content);
                for (var i = 0; i < SpeciesOrder.Length; i++)
                {
                    var species = SpeciesOrder[i];
                    var shipped = Array.IndexOf(ShippedSpecies, species) >= 0;
                    var left = i / (float)SpeciesOrder.Length;
                    var right = (i + 1) / (float)SpeciesOrder.Length;
                    var tile = ActionButton("Species " + SpeciesName(species), speciesGroup, left, right, 0, 0,
                        () => chooseSpecies(species), shipped ? Peach : Mist, shipped ? Ink : Muted);
                    PlaceFromTop((RectTransform)tile.transform, left, right, 78, 252, i == 0 ? 0 : 6, i == SpeciesOrder.Length - 1 ? 0 : 6);
                    SoftShapes.Radius(tile.image, Px(20));
                    var label = tile.GetComponentInChildren<Text>();
                    label.text = SpeciesName(species);
                    FitText(label, 15, 24);
                    PlaceFromTop(label.rectTransform, 0, 1, 100, 134, 4, 4);
                    var ground = Box(tile.name + " Glyph Ground", tile.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-34, -90), new Vector2(34, -22));
                    SoftShapes.Disc(Surface(ground, new Color(1, 1, 1, shipped ? 0.8f : 0.6f)));
                    var glyph = Box(tile.name + " Glyph", tile.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-18, -74), new Vector2(18, -38));
                    PawGlyph(glyph, tile.name + " paw", shipped ? Ink : WithAlpha(Secondary, 0.5f));
                    var chip = Box(tile.name + " Note Chip", tile.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    PlaceFromTop(chip, 0, 1, 140, 168, 8, 8);
                    SoftShapes.Pill(Surface(chip, shipped ? FernSoft : new Color(1, 1, 1, 0.7f)));
                    var note = Label(tile.name + " Note", chip, shipped ? "Here now" : "Arriving soon", 16,
                        shipped ? FernDeep : Muted, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                    FitText(note, 11, 16);
                    if (!shipped)
                    {
                        // Dimmed, with no lock or price: these friends simply haven't arrived yet.
                        tile.interactable = false;
                        label.color = WithAlpha(Muted, 0.7f);
                    }

                    speciesTiles.Add(species, tile);
                }

                var speciesNote = Label("Pet species detail", speciesGroup, "Cats are here now. More friends are on their way.", 22,
                    Secondary, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero);
                FitText(speciesNote, 15, 22);
                PlaceFromTop(speciesNote.rectTransform, 0, 1, 262, 304);

                breedGroup = Group("Pet breed page", content);
                breedGrid = Box("Pet breed grid", breedGroup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(breedGrid, 0, 1, 66, 194);
                coatRow = Box("Pet coat row", breedGroup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(coatRow, 0, 1, 198, 252);
                breedDetail = Label("Pet breed detail", breedGroup, "", 21, Muted, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                FitText(breedDetail, 14, 21);
                PlaceFromTop(breedDetail.rectTransform, 0, 1, 256, 306);
                this.next = ActionButton("Breed next", breedGroup, 0.5f, 1, 0, 0, next, Fern, Color.white);
                this.next.GetComponentInChildren<Text>().text = "Choose a name";
                PlaceFromTop((RectTransform)this.next.transform, 0.5f, 1, 312, 378, 3, 0);

                nameGroup = Group("Pet name page", content);
                nameHeading = Label("Pet name prompt", nameGroup, "", 24, Muted, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                PlaceFromTop(nameHeading.rectTransform, 0, 1, 68, 104);
                var field = Box("Pet Name", nameGroup, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(field, 0, 1, 108, 190);
                NameField = field.gameObject.AddComponent<InputField>();
                NameField.targetGraphic = TextField(field);
                NameField.characterLimit = 28;
                NameField.lineType = InputField.LineType.SingleLine;
                NameField.textComponent = Label("Entered Pet Name", field, "", 30, Ink, TextAnchor.MiddleLeft,
                    new Vector2(26, 0), Vector2.one, new Vector2(-26, 0), FontStyle.Bold);
                namePlaceholder = Label("Pet Name Placeholder", field, "Name", 30, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(26, 0), Vector2.one, new Vector2(-26, 0));
                NameField.placeholder = namePlaceholder;
                collar = ActionButton("Pet collar", nameGroup, 0, 0.42f, 0, 0, cycleCollar, Mist, Ink);
                collarLabel = collar.GetComponentInChildren<Text>();
                FitText(collarLabel, 15, 24);
                PlaceFromTop((RectTransform)collar.transform, 0, 0.42f, 200, 278, 0, 9);
                this.adopt = ActionButton("Adopt pet", nameGroup, 0.42f, 1, 0, 0, adopt, Fern, Color.white);
                this.adopt.GetComponentInChildren<Text>().text = "Welcome home";
                PlaceFromTop((RectTransform)this.adopt.transform, 0.42f, 1, 200, 278, 9, 0);

                Feedback = Label("Pet Adoption Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                FitText(Feedback, 14, 21);
                Root.SetActive(false);
            }

            public void ShowPage(int page)
            {
                Page = Mathf.Clamp(page, 0, 2);
                Feedback.text = string.Empty;
                speciesGroup.gameObject.SetActive(Page == 0);
                breedGroup.gameObject.SetActive(Page == 1);
                nameGroup.gameObject.SetActive(Page == 2);
                heading.text = Page == 0 ? "Which pet?" : Page == 1 ? "Choose a breed" : "Welcome home";
                // On the breed page the feedback shares the bottom row with the Next button.
                PlaceFromTop(Feedback.rectTransform, 0, Page == 1 ? 0.49f : 1, Page == 2 ? 288 : 314, 378);
            }

            public void FocusPage()
            {
                switch (Page)
                {
                    case 0: speciesTiles[Species.Cat].Select(); break;
                    case 1:
                        foreach (var tile in breedTiles)
                        {
                            if (tile.Selected)
                                tile.Button.Select();
                        }
                        break;
                    default: NameField.Select(); break;
                }
            }

            public void ShowSpecies(Species species)
            {
                foreach (var pair in speciesTiles)
                    pair.Value.image.color = pair.Key == species ? Peach : Mist;
            }

            public void ShowBreeds(Species species, IReadOnlyList<BreedDefinition> breeds, IReadOnlyList<string> statuses,
                IReadOnlyList<bool> unlocked, PetBreed selected)
            {
                if (builtSpecies != species)
                {
                    Clear(breedGrid, breedTiles);
                    builtCoatsFor = null;
                    var rows = Mathf.Max(1, (breeds.Count + BreedColumns - 1) / BreedColumns);
                    for (var i = 0; i < breeds.Count; i++)
                    {
                        var breed = breeds[i].Breed;
                        var column = i % BreedColumns;
                        var row = i / BreedColumns;
                        var tile = new PickerTile("Breed " + BreedName(breed), BreedName(breed), breedGrid,
                            new Vector2(column / (float)BreedColumns, 1 - (row + 1) / (float)rows),
                            new Vector2((column + 1) / (float)BreedColumns, 1 - row / (float)rows), () => chooseBreed(breed), null);
                        tile.Value = (int)breed;
                        breedTiles.Add(tile);
                    }

                    builtSpecies = species;
                }

                for (var i = 0; i < breedTiles.Count && i < breeds.Count; i++)
                    breedTiles[i].Show(breedTiles[i].Value == (int)selected, statuses[i], unlocked[i]);
            }

            public void ShowCoats(IReadOnlyList<PetCoat> coats, PetCoat selected)
            {
                var forBreed = breedTiles.Find(tile => tile.Selected)?.Value;
                if (builtCoatsFor == null || forBreed == null || (int)builtCoatsFor.Value != forBreed.Value)
                {
                    Clear(coatRow, coatTiles);
                    var count = Mathf.Max(5, coats.Count);
                    for (var i = 0; i < coats.Count; i++)
                    {
                        var coat = coats[i];
                        var tile = new PickerTile("Coat " + coat, coat.ToString(), coatRow,
                            new Vector2(i / (float)count, 0), new Vector2((i + 1) / (float)count, 1), () => chooseCoat(coat),
                            ToColor(PetPalette.FallbackCoat(coat)));
                        tile.Value = (int)coat;
                        coatTiles.Add(tile);
                    }

                    builtCoatsFor = forBreed == null ? (PetBreed?)null : (PetBreed)forBreed.Value;
                }

                foreach (var tile in coatTiles)
                    tile.Show(tile.Value == (int)selected, null, true);
            }

            public void ShowBreedDetail(string detail, bool adoptable)
            {
                breedDetail.text = detail;
                next.interactable = adoptable;
                next.GetComponentInChildren<Text>().text = adoptable ? "Choose a name" : "Locked";
            }

            public void ShowName(string species, string collarText)
            {
                nameHeading.text = $"What's your {species.ToLowerInvariant()}'s name?";
                namePlaceholder.text = species + " name";
                collarLabel.text = collarText;
            }

            private static RectTransform Group(string name, RectTransform content) =>
                Box(name, content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            private static void Clear(RectTransform parent, List<PickerTile> tiles)
            {
                tiles.Clear();
                for (var i = parent.childCount - 1; i >= 0; i--)
                {
                    var child = parent.GetChild(i);
                    child.gameObject.SetActive(false);
                    child.SetParent(null, false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        // One rounded choice in the pet picker: a breed tile (with its unlock status as a small chip) or a coat pill
        // (with a colour disc). The chosen one turns soft sage with a sage outline.
        private sealed class PickerTile
        {
            private readonly Image ring;
            private readonly Text label;
            private readonly Image statusChip;
            private readonly Text status;
            private readonly Image lockMark;

            public PickerTile(string name, string title, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Action choose,
                Color? chip)
            {
                Button = ActionButton(name, parent, 0, 1, 0, 0, choose, Mist, Ink);
                var rect = (RectTransform)Button.transform;
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = new Vector2(5, 4);
                rect.offsetMax = new Vector2(-5, -4);
                label = Button.GetComponentInChildren<Text>();
                label.text = title;
                FitText(label, 13, 21);
                var radius = chip.HasValue ? 26f : Px(16);
                if (chip is Color color)
                {
                    SoftShapes.Pill(Button.image);
                    label.rectTransform.anchorMin = Vector2.zero;
                    label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(40, 2);
                    label.rectTransform.offsetMax = new Vector2(-6, -2);
                    label.alignment = TextAnchor.MiddleLeft;
                    var swatch = Box(name + " Color", Button.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                        new Vector2(9, -13), new Vector2(35, 13));
                    SoftShapes.Disc(Surface(swatch, color));
                    SoftShapes.Edge(Surface(Box(name + " Edge", swatch, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                        WithAlpha(ShadowInk, 0.16f)));
                }
                else
                {
                    SoftShapes.Radius(Button.image, radius);
                    label.rectTransform.offsetMin = new Vector2(4, 30);
                    label.rectTransform.offsetMax = new Vector2(-4, -4);
                    var chipRect = Box(name + " Status Chip", Button.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                        new Vector2(-48, 6), new Vector2(48, 32));
                    statusChip = Surface(chipRect, Paper);
                    SoftShapes.Pill(statusChip);
                    status = Label(name + " Status", chipRect, "", 16, Secondary, TextAnchor.MiddleCenter,
                        new Vector2(6, 0), Vector2.one, new Vector2(-6, 0), FontStyle.Bold);
                    FitText(status, 11, 16);
                    lockMark = IconAt(name + " Lock", chipRect, "Lock", Muted, new Vector2(0, 0.5f), new Vector2(12, 0), 16);
                }

                var selection = Box(name + " Selection", Button.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                ring = Surface(selection, Fern);
                SoftShapes.Outline(ring, radius);
            }

            public Button Button { get; }
            public int Value { get; set; }
            public bool Selected { get; private set; }

            public void Show(bool selected, string statusText, bool unlocked)
            {
                Selected = selected;
                ring.gameObject.SetActive(selected);
                Button.image.color = selected ? FernSoft : Mist;
                SetRole(label, selected ? TextRole.Strong : TextRole.Body);
                label.color = unlocked ? (selected ? FernDeep : Ink) : Muted;
                if (status == null)
                    return;

                status.text = statusText ?? string.Empty;
                var premium = statusText == "Premium";
                statusChip.color = premium ? Butter : unlocked ? Paper : new Color(1, 1, 1, 0.7f);
                status.color = premium ? ButterInk : unlocked ? Muted : Muted;
                lockMark.gameObject.SetActive(!unlocked && !premium);
                status.rectTransform.offsetMin = new Vector2(lockMark.gameObject.activeSelf ? 22 : 6, 0);
            }
        }
    }
}
