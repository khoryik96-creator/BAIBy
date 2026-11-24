using System;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Decor: the room's theme, in the nursery (saved on the baby) and in a pet's room (one theme for every pet room,
    // saved in the household), and in a cat's room what the cat wears. Premium themes and wear carry a small "Special"
    // chip while locked; tapping one previews it in the room with a calm line that a grown-up can get it in Grown-ups.
    // Nothing here opens a shop or names a price.
    public sealed partial class NurseryPreview
    {
        // Cards in the order they show: the free rooms, the room packs, then the Founder pack's room.
        private static readonly NurseryTheme[] Themes =
            { NurseryTheme.Sage, NurseryTheme.Peach, NurseryTheme.CloudNursery, NurseryTheme.Starlight, NurseryTheme.GoldenHour };
        // What a cat can wear, in the order the Wear button cycles.
        private static readonly PetAccessory[] CatWear = { PetAccessory.None, PetAccessory.Collar, PetAccessory.Bandana, PetAccessory.Bow };

        private Button decorButton;
        private DecorPanel decor;
        // A locked theme or pet wear previewed in the room (not saved); the saved look comes back when Decor closes.
        private bool decorPreview;
        private PetAppearance petWearDraft;

        private bool DecorForPet => view == HouseholdView.PetRoom;

        private void OpenDecor()
        {
            if (DecorForPet)
            {
                petWearDraft = FindCompanion(petSlot)?.PetState?.Appearance;
                ShowPetPanel(PetPanel.Decor);
            }
            else
            {
                ShowPanel(NurseryPanel.Decor);
            }
        }

        private void CloseDecor()
        {
            if (DecorForPet)
                ShowPetPanel(PetPanel.Care);
            else
                ShowPanel(NurseryPanel.Care);
        }

        // Puts the saved theme (or the free default, for a refunded one) back after a preview.
        private void EndDecorPreview()
        {
            if (!decorPreview)
                return;
            decorPreview = false;
            if (DecorForPet)
            {
                ApplyPetRoomTheme();
                var pet = FindCompanion(petSlot)?.PetState;
                petWearDraft = pet?.Appearance;
                if (pet != null)
                    petStage.Show(ShownPet(pet.Appearance), household.PetCatalog, pet.Stage, IsSleepy(pet) || IsPoorly(pet));
            }
            else if (session?.State != null)
            {
                ApplyNurseryTheme(session.State.NurseryTheme);
            }
        }

        private void ChooseRoomTheme(NurseryTheme theme)
        {
            if (!CosmeticCatalog.IsUnlocked(Owned, theme))
            {
                // A preview only: the room shows the theme, and nothing is saved.
                decorPreview = true;
                nursery.SetTheme(theme);
                decor.UpdateSelection(DecorForPet ? household.PetRoomTheme : session.State.NurseryTheme, theme, Owned);
                decor.Feedback.text = CosmeticCatalog.LockedLine(CosmeticCatalog.PackFor(CosmeticCatalog.RoomId(theme)));
                return;
            }

            try
            {
                if (DecorForPet)
                    household.SetPetRoomTheme(theme);
                else
                    session.SetNurseryTheme(theme, Owned);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The decor couldn't be saved. Please try again.");
                return;
            }

            decorPreview = false;
            if (DecorForPet)
                ApplyPetRoomTheme();
            else
                ApplyNurseryTheme(session.State.NurseryTheme);
        }

        // The nursery shows the baby's saved theme, or Sage while a premium theme's pack is not owned.
        private void ApplyNurseryTheme(NurseryTheme theme)
        {
            nursery.SetTheme(ShownTheme(theme));
            decor.UpdateSelection(theme, theme, Owned);
        }

        private void ApplyPetRoomTheme()
        {
            if (household == null)
                return;
            nursery.SetTheme(ShownTheme(household.PetRoomTheme));
            decor.UpdateSelection(household.PetRoomTheme, household.PetRoomTheme, Owned);
        }

        // Steps what the cat wears (None, Collar, Bandana, Bow) or its colour. An open item is saved at once; a locked
        // one is previewed on the cat with the calm line, and the next tap carries on from it.
        private void CyclePetWear(bool colour)
        {
            var companion = FindCompanion(petSlot);
            var pet = companion?.PetState;
            if (pet == null)
                return;

            var draft = petWearDraft ?? pet.Appearance;
            var next = colour ? NextAccent(draft) : NextWear(draft, household.PetCatalog);
            petWearDraft = next;
            var locked = CosmeticCatalog.LockedPack(next, Owned);
            if (locked != null)
            {
                decorPreview = true;
                petStage.Show(next, household.PetCatalog, pet.Stage, IsSleepy(pet) || IsPoorly(pet));
                decor.ShowWear(next, Owned);
                decor.Feedback.text = CosmeticCatalog.LockedLine(locked);
                return;
            }

            try
            {
                household.SetPetAppearance(petSlot, next);
            }
            catch (Exception error)
            {
                ReportFailure(error, "That couldn't be saved. Please try again.");
                return;
            }

            decorPreview = false;
            petStage.Show(ShownPet(next), household.PetCatalog, pet.Stage, IsSleepy(pet) || IsPoorly(pet));
            decor.ShowWear(next, Owned);
            decor.Feedback.text = string.Empty;
        }

        private static PetAppearance NextWear(PetAppearance look, PetCatalog catalog)
        {
            var species = catalog.Get(look.Species);
            var at = Array.IndexOf(CatWear, look.Accessory);
            for (var step = 1; step <= CatWear.Length; step++)
            {
                var wear = CatWear[(at + step + CatWear.Length) % CatWear.Length];
                if (species.Offers(wear))
                    return look.WithAccessory(wear);
            }

            return look;
        }

        // The free colours, then the pet pack's.
        private static PetAppearance NextAccent(PetAppearance look)
        {
            var colours = AccentChoices();
            var at = Array.IndexOf(colours, look.AccessoryAccent);
            return look.WithAccessoryAccent(colours[(at + 1) % colours.Length]);
        }

        private static Outfit[] AccentChoices()
        {
            var colours = new Outfit[CosmeticCatalog.FreeAccents.Count + CosmeticCatalog.PackAccents.Count];
            for (var i = 0; i < CosmeticCatalog.FreeAccents.Count; i++)
                colours[i] = CosmeticCatalog.FreeAccents[i];
            for (var i = 0; i < CosmeticCatalog.PackAccents.Count; i++)
                colours[CosmeticCatalog.FreeAccents.Count + i] = CosmeticCatalog.PackAccents[i];
            return colours;
        }

        private static string WearName(PetAccessory wear)
        {
            switch (wear)
            {
                case PetAccessory.None: return "Nothing";
                case PetAccessory.TankCastle: return "Tank castle";
                default: return wear.ToString();
            }
        }

        private static string ThemeName(NurseryTheme theme)
        {
            switch (theme)
            {
                case NurseryTheme.CloudNursery: return "Cloud nursery";
                case NurseryTheme.GoldenHour: return "Golden hour";
                default: return theme.ToString();
            }
        }

        private static Color ThemeSwatch(NurseryTheme theme)
        {
            switch (theme)
            {
                case NurseryTheme.Peach: return new Color32(236, 188, 162, 255);
                case NurseryTheme.CloudNursery: return new Color32(198, 220, 238, 255);
                case NurseryTheme.Starlight: return new Color32(104, 108, 160, 255);
                case NurseryTheme.GoldenHour: return new Color32(240, 206, 140, 255);
                default: return new Color32(165, 181, 159, 255);
            }
        }

        private sealed class DecorPanel
        {
            private const float CardHeight = 96;
            private const float CardsTop = 86;

            private readonly RectTransform tray;
            private readonly Text heading;
            private readonly ThemeChoice[] choices = new ThemeChoice[Themes.Length];
            private readonly GameObject wearRow;
            private readonly Text wearLabel;
            private readonly Text colourLabel;
            private readonly Button wear;

            public GameObject Root { get; }
            public Text Feedback { get; }

            public DecorPanel(RectTransform content, Action close, Action<NurseryTheme> choose, Action<bool> cycleWear)
            {
                tray = (RectTransform)content.parent;
                Root = tray.gameObject;
                heading = SheetHeading("Decor Heading", content, "Nursery decor", 360);
                PlaceFromTop(heading.rectTransform, 0, 0.72f, 12, 78);
                var done = SheetClose("Close decor", "Done", content, 360, close);
                PlaceFromTop((RectTransform)done.transform, 0.74f, 1, 12, 78, 3, 3);
                for (var i = 0; i < Themes.Length; i++)
                {
                    var theme = Themes[i];
                    var row = i / 3;
                    var column = i % 3;
                    choices[i] = new ThemeChoice(theme, content, () => choose(theme));
                    var top = CardsTop + row * (CardHeight + 8);
                    PlaceFromTop((RectTransform)choices[i].Button.transform, column / 3f, (column + 1) / 3f, top, top + CardHeight,
                        column == 0 ? 0 : 5, column == 2 ? 0 : 5);
                }

                // A cat's room adds what the cat wears: the item and its colour, each a soft button that steps on.
                var wearTop = CardsTop + 2 * (CardHeight + 8);
                var rowBox = Box("Decor Wear Row", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(rowBox, 0, 1, wearTop, wearTop + 82);
                wearRow = rowBox.gameObject;
                wear = ActionButton("Pet wear", rowBox, 0, 0.5f, 0, 82, () => cycleWear(false), Mist, Ink);
                wearLabel = wear.GetComponentInChildren<Text>();
                FitText(wearLabel, 15, 23);
                var colour = ActionButton("Pet wear colour", rowBox, 0.5f, 1, 0, 82, () => cycleWear(true), Mist, Ink);
                colourLabel = colour.GetComponentInChildren<Text>();
                FitText(colourLabel, 15, 23);

                Feedback = Label("Decor Feedback", content, "", 23, Muted, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                FitText(Feedback, 16, 23);
                Layout(false);
                Root.SetActive(false);
            }

            public Button ChoiceFor(NurseryTheme theme)
            {
                foreach (var choice in choices)
                {
                    if (choice.Theme == theme)
                        return choice.Button;
                }

                return choices[0].Button;
            }

            // Nursery: the themes. A pet's room: "Pet room decor", the themes and, for a cat, what it wears.
            public void Layout(bool pet, bool wears = false)
            {
                heading.text = pet ? "Pet room decor" : "Nursery decor";
                wearRow.SetActive(wears);
                var top = CardsTop + 2 * (CardHeight + 8) + (wears ? 90 : 0);
                PlaceFromTop(Feedback.rectTransform, 0, 1, top, top + 62);
                var height = top + 66;
                tray.offsetMax = new Vector2(tray.offsetMax.x, tray.offsetMin.y + height + SheetChrome);
            }

            // saved is the saved theme (its card says Selected); shown is the one in the room, which a preview changes.
            public void UpdateSelection(NurseryTheme saved, NurseryTheme shown, IEntitlements owned)
            {
                foreach (var choice in choices)
                    choice.Show(choice.Theme == saved, choice.Theme == shown && shown != saved,
                        !CosmeticCatalog.IsUnlocked(owned, choice.Theme));
                Feedback.text = string.Empty;
            }

            public void ShowWear(PetAppearance look, IEntitlements owned)
            {
                if (look == null)
                    return;
                var wearLocked = look.Accessory != PetAccessory.None &&
                    !owned.IsCosmeticUnlocked(CosmeticCatalog.PetWearId(look.Species, look.Accessory));
                wearLabel.text = "Wears: " + WearName(look.Accessory) + (wearLocked ? " · Special" : string.Empty);
                var colourLocked = !CosmeticCatalog.IsPetAccentUnlocked(owned, look.AccessoryAccent);
                colourLabel.text = "Colour: " + OutfitName(look.AccessoryAccent) + (colourLocked ? " · Special" : string.Empty);
            }

            // A room theme card: a rounded colour preview, its name, and "Selected" with a sage outline when chosen, or a
            // small butter "Special" chip while its pack is not owned.
            private sealed class ThemeChoice
            {
                private readonly GameObject selected;
                private readonly GameObject selectedPill;
                private readonly Image card;
                private readonly GameObject special;

                public NurseryTheme Theme { get; }
                public Button Button { get; }

                public ThemeChoice(NurseryTheme theme, Transform parent, Action choose)
                {
                    Theme = theme;
                    var name = ThemeName(theme);
                    Button = ActionButton(name, parent, 0, 1, 0, 0, choose, Mist, Ink);
                    card = Button.image;
                    SoftShapes.Radius(card, Px(18));
                    var title = Button.GetComponentInChildren<Text>();
                    title.alignment = TextAnchor.MiddleLeft;
                    title.rectTransform.anchorMin = Vector2.zero;
                    title.rectTransform.anchorMax = new Vector2(1, 0);
                    title.rectTransform.offsetMin = new Vector2(12, 4);
                    title.rectTransform.offsetMax = new Vector2(-8, 38);
                    FitText(title, 14, 21);
                    var swatchArea = Box(name + " Color", Button.transform, Vector2.zero, Vector2.one,
                        new Vector2(10, 40), new Vector2(-10, -8));
                    SoftShapes.Radius(Surface(swatchArea, ThemeSwatch(theme)), Px(12));

                    var chip = Box(name + " Special", swatchArea, Vector2.one, Vector2.one, new Vector2(-78, -30), new Vector2(-4, -4));
                    SoftShapes.Pill(Surface(chip, Butter));
                    var chipLabel = Label(name + " Special Label", chip, "Special", 15, ButterInk, TextAnchor.MiddleCenter,
                        new Vector2(4, 0), Vector2.one, new Vector2(-4, 0), FontStyle.Bold);
                    FitText(chipLabel, 11, 15);
                    special = chip.gameObject;

                    // "Selected": a sage outline round the card and a white pill on the swatch.
                    var selection = Box(name + " Selected", Button.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    SoftShapes.Outline(Surface(selection, Fern), Px(18));
                    var pill = Box(name + " Selected Pill", swatchArea, Vector2.zero, Vector2.zero, new Vector2(4, 4), new Vector2(88, 28));
                    SoftShapes.Pill(Surface(pill, Paper));
                    var label = Label(name + " Selection", pill, "Selected", 16, FernDeep, TextAnchor.MiddleCenter,
                        new Vector2(4, 0), Vector2.one, new Vector2(-4, 0), FontStyle.Bold);
                    FitText(label, 11, 16);
                    selected = selection.gameObject;
                    selectedPill = pill.gameObject;
                }

                // previewing: the card being tried in the room (a locked theme), drawn soft sage without "Selected".
                public void Show(bool isSelected, bool previewing, bool locked)
                {
                    selected.SetActive(isSelected);
                    selectedPill.SetActive(isSelected);
                    card.color = isSelected || previewing ? FernSoft : Mist;
                    special.SetActive(locked);
                }
            }
        }
    }
}
