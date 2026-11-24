using System;
using System.Collections.Generic;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Premium cosmetics (docs/monetization.md, M2b and the baby look packs): what is shown and the Grown-ups panel.
    // - Every look, room and pet's wear is drawn through CosmeticCatalog.Displayed: a saved premium ID whose pack is not
    //   owned (a refund) shows the free default, and the saved choice is kept, so owning the pack again restores it.
    // - Pickers (Style, Decor, the cat's wear) mark locked items with a small "Special" chip; tapping one previews it and
    //   says calmly that a grown-up can get it in Grown-ups. No picker or care flow links to a shop or names a price.
    // - Grown-ups → Looks & rooms (behind the parental gate) lists each pack and what is inside; test builds add or
    //   remove a pack as a playtest grant. There is no store in this build.
    public sealed partial class NurseryPreview
    {
        private LooksPanel looksPanel;

        // What the household owns; the free game when nothing is wired.
        private IEntitlements Owned => household?.Entitlements ?? entitlements ?? NoEntitlements.Instance;

        private BabyAppearance ShownLook(BabyAppearance look) => CosmeticCatalog.Displayed(look ?? BabyAppearance.Default, Owned);
        private PetAppearance ShownPet(PetAppearance look) => look == null ? null : CosmeticCatalog.Displayed(look, Owned);
        private NurseryTheme ShownTheme(NurseryTheme theme) => CosmeticCatalog.Displayed(theme, Owned);

        private bool OwnsFounderBadge => Owned.IsCosmeticUnlocked(CosmeticCatalog.FounderBadge);

        private static string OutfitName(Outfit outfit)
        {
            switch (outfit)
            {
                case Outfit.OatmealKnit: return "Oatmeal";
                case Outfit.BerryKnit: return "Berry";
                case Outfit.MossKnit: return "Moss";
                case Outfit.FogKnit: return "Fog";
                case Outfit.GardenMint: return "Garden mint";
                case Outfit.FernGold: return "Fern & gold";
                default: return outfit.ToString();
            }
        }

        private void ShowLooksPanel()
        {
            if (household == null)
                return;

            view = HouseholdView.Looks;
            ApplyHouseholdView();
            looksPanel.Show(Owned, playtest);
            looksPanel.Close.Select();
        }

        // Test builds only: one pack as a playtest grant, like the playtest rooms and helpers.
        private void ToggleTestPack(string productId)
        {
            if (playtest == null)
                return;

            var adding = !playtest.Owns(productId);
            try
            {
                playtest.Set(productId, adding);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The playtest pack couldn't be changed. Please try again.");
                return;
            }

            var pack = CosmeticCatalog.PackForProduct(productId);
            looksPanel.Show(Owned, playtest);
            looksPanel.Feedback.text = adding
                ? $"{pack?.Title} added for testing. Find it in Style, Decor or a cat's room."
                : $"{pack?.Title} removed. Anything chosen from it stays saved and shows the free look until it's back.";
        }

        // Shows the companion's look and room again after a grant, refund or restore.
        private void RefreshCosmetics()
        {
            if (view == HouseholdView.Nursery && session?.State != null)
            {
                ApplyBabyAppearance(session.State.Appearance);
                ApplyNurseryTheme(session.State.NurseryTheme);
            }
            else if (view == HouseholdView.PetRoom)
            {
                ApplyPetRoomTheme();
                var pet = FindCompanion(petSlot)?.PetState;
                if (pet != null)
                {
                    petWearDraft = pet.Appearance;
                    petStage.Show(ShownPet(pet.Appearance), household.PetCatalog, pet.Stage, IsSleepy(pet) || IsPoorly(pet));
                    decor.ShowWear(pet.Appearance, Owned);
                }
            }
        }

        // Grown-ups → Looks & rooms: the packs on two tabs (baby looks, then rooms and pet wear), what each holds, and
        // whether the household has it. Nothing here is a purchase: the store is not in this build.
        private sealed class LooksPanel
        {
            public const float Height = 600;
            private const float RowsTop = 222;
            private const float RowHeight = 104;
            private static readonly string[][] Tabs =
            {
                new[] { CosmeticCatalog.CozyKnits, CosmeticCatalog.GardenParty, CosmeticCatalog.Founder },
                new[] { CosmeticCatalog.CloudNursery, CosmeticCatalog.Starlight, CosmeticCatalog.CatBandanasBows },
            };

            private readonly Text detail;
            private readonly Button[] tabs = new Button[2];
            private readonly PackRow[] rows = new PackRow[3];
            private int shown;
            private IEntitlements owned;
            private IPlaytestUnlocks playtest;

            public GameObject Root { get; }
            public Button Close { get; }
            public Text Feedback { get; }

            public LooksPanel(RectTransform content, Action close, Action<string> toggleTest)
            {
                Root = content.parent.gameObject;
                var heading = Label("Looks Heading", content, "Looks & rooms", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                FitText(heading, 26, 38);
                PlaceFromTop(heading.rectTransform, 0, 0.72f, 14, 76);
                Close = ActionButton("Close looks", content, 0.73f, 1, 0, 0, close, Mist, Ink);
                Close.GetComponentInChildren<Text>().text = "Done";
                PlaceFromTop((RectTransform)Close.transform, 0.73f, 1, 16, 76, 3, 3);
                SoftShapes.Round(Close.image, 1f);

                var track = Box("Looks Tabs", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(track, 0, 1, 86, 146);
                SoftShapes.Pill(Surface(track, Mist));
                var titles = new[] { "Baby looks", "Rooms & pet wear" };
                for (var i = 0; i < tabs.Length; i++)
                {
                    var index = i;
                    tabs[i] = ActionButton("Looks tab " + titles[i], track, i * 0.5f, (i + 1) * 0.5f, 6, 54,
                        () => ShowTab(index), Mist, Muted);
                    tabs[i].GetComponentInChildren<Text>().text = titles[i];
                    FitText(tabs[i].GetComponentInChildren<Text>(), 16, 22);
                    SoftShapes.Pill(tabs[i].image);
                }

                detail = Label("Looks Detail", content, "", 21, Secondary, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, Vector2.zero);
                FitText(detail, 15, 21);
                PlaceFromTop(detail.rectTransform, 0, 1, 154, 214);
                for (var i = 0; i < rows.Length; i++)
                {
                    var top = RowsTop + i * RowHeight;
                    var slot = i;
                    rows[i] = new PackRow(content, i, top, top + RowHeight - 8, () => toggleTest(rows[slot].ProductId));
                }

                Feedback = Label("Looks Feedback", content, "", 20, Ink, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero);
                FitText(Feedback, 14, 20);
                PlaceFromTop(Feedback.rectTransform, 0, 1, RowsTop + 3 * RowHeight, Height - 4);
                Root.SetActive(false);
            }

            public void Show(IEntitlements owned, IPlaytestUnlocks playtest)
            {
                this.owned = owned;
                this.playtest = playtest;
                Feedback.text = string.Empty;
                detail.text = "Special looks and rooms only change how things look, never care, growth or Bond. " +
                    (playtest == null
                        ? "They're coming to the app stores later; anything you own shows here."
                        : "This test build has no shop and nothing is bought.");
                ShowTab(shown);
            }

            private void ShowTab(int index)
            {
                shown = index;
                for (var i = 0; i < tabs.Length; i++)
                {
                    tabs[i].image.color = i == index ? Paper : Mist;
                    tabs[i].GetComponentInChildren<Text>().color = i == index ? Ink : Muted;
                }

                for (var i = 0; i < rows.Length; i++)
                    rows[i].Show(CosmeticCatalog.PackForProduct(Tabs[index][i]), owned, playtest);
                Feedback.text = string.Empty;
            }

            // One pack: its name, what is inside, and "Owned" or, in test builds, Add test pack / Remove.
            private sealed class PackRow
            {
                private readonly Text title;
                private readonly Text contents;
                private readonly Button toggle;
                private readonly Text toggleLabel;
                private readonly GameObject ownedChip;

                public string ProductId { get; private set; }

                public PackRow(RectTransform content, int index, float top, float bottom, Action toggleTest)
                {
                    var row = Box("Looks Pack " + index, content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    PlaceFromTop(row, 0, 1, top, bottom);
                    SoftShapes.Radius(Surface(row, Mist), Px(18));
                    title = Label("Looks Pack Title " + index, row, "", 24, Ink, TextAnchor.MiddleLeft,
                        Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                    FitText(title, 17, 24);
                    PlaceFromTop(title.rectTransform, 0, 0.64f, 6, 38, 16, 4);
                    contents = Label("Looks Pack Contents " + index, row, "", 17, Muted, TextAnchor.UpperLeft,
                        Vector2.zero, Vector2.one, Vector2.zero);
                    FitText(contents, 12, 17);
                    PlaceFromTop(contents.rectTransform, 0, 0.64f, 38, bottom - top - 6, 16, 4);

                    toggle = ActionButton("Looks pack toggle " + index, row, 0.65f, 1, 0, 0, toggleTest, Fern, Color.white);
                    toggleLabel = toggle.GetComponentInChildren<Text>();
                    FitText(toggleLabel, 14, 21);
                    PlaceFromTop((RectTransform)toggle.transform, 0.65f, 1, 12, bottom - top - 12, 4, 10);
                    SoftShapes.Round(toggle.image, 1f);

                    var chip = Box("Looks Pack Owned " + index, row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    PlaceFromTop(chip, 0.70f, 1, 30, 62, 4, 14);
                    SoftShapes.Pill(Surface(chip, FernSoft));
                    var chipLabel = Label("Looks Pack Owned Label " + index, chip, "Owned", 17, FernDeep, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                    FitText(chipLabel, 12, 17);
                    ownedChip = chip.gameObject;
                }

                public void Show(CosmeticPack pack, IEntitlements owned, IPlaytestUnlocks playtest)
                {
                    ProductId = pack?.ProductId;
                    title.text = pack?.Title ?? string.Empty;
                    contents.text = pack?.Contents ?? string.Empty;
                    var has = pack != null && owned != null && owned.Owns(pack.ProductId);
                    toggle.gameObject.SetActive(playtest != null && pack != null);
                    ownedChip.SetActive(playtest == null && has);
                    if (playtest == null || pack == null)
                        return;
                    var test = playtest.Owns(pack.ProductId);
                    toggleLabel.text = test ? "Remove" : "Add test pack";
                    toggle.image.color = test ? Paper : Fern;
                    toggleLabel.color = test ? Ink : Color.white;
                }
            }
        }
    }
}
