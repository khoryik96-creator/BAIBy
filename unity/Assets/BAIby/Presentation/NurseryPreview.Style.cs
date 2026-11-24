using System;
using BAIby.Application;
using BAIby.Character;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        // Approximate colors of the original artwork, shown for the identity options (Warm, Chestnut, Sage)
        // whose recolor target is the measured reference itself.
        private static readonly AppearanceColor WarmSkin = new AppearanceColor(231, 160, 107);
        private static readonly AppearanceColor ChestnutHair = new AppearanceColor(128, 80, 57);
        private static readonly AppearanceColor SageMain = new AppearanceColor(246, 222, 198);
        private static readonly AppearanceColor SageAccent = new AppearanceColor(174, 168, 120);

        private Button styleButton;
        private StylePanel style;

        private void ChooseAppearance(Func<BabyAppearance, BabyAppearance> change)
        {
            var saved = session.State.Appearance;
            var next = change(saved);
            if (!session.CanWear(next, DeveloperModeOn))
            {
                // Earned looks wait for their care rhythm; they are never sold.
                var days = Math.Max(EarnedLooks.RhythmFor(next.Headwear), EarnedLooks.RhythmFor(next.Outfit));
                style.Feedback.text = $"Earned with a {days}-day care rhythm. Keep caring each day!";
                return;
            }

            if (!CosmeticCatalog.CanWear(saved, next, Owned))
            {
                // A special look is previewed on the baby and not saved; the saved look comes back when Style closes.
                // Only the part just tapped is previewed over what is shown now.
                var shown = ShownLook(saved);
                var preview = change(shown);
                nursery.SetAppearance(preview, true);
                style.UpdateSelection(saved);
                style.Feedback.text = CosmeticCatalog.LockedLine(CosmeticCatalog.LockedPack(preview, Owned));
                return;
            }

            try
            {
                session.SetAppearance(next, DeveloperModeOn, Owned);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The new look couldn't be saved. Please try again.");
                return;
            }

            // A choice made in the panel retries a look whose art failed before.
            ApplyBabyAppearance(session.State.Appearance, true);
        }

        private void ApplyBabyAppearance(BabyAppearance appearance, bool retryFailed = false)
        {
            // A saved special look whose pack isn't owned (a refund) shows the free default; the choice is kept.
            bool shown = nursery.SetAppearance(ShownLook(appearance), retryFailed);
            style.SetEarned(session?.Bonding?.LongestRhythm ?? 0, DeveloperModeOn);
            style.SetSpecial(Owned);
            style.UpdateSelection(appearance);
            if (!shown)
                style.Feedback.text = "This look couldn't be shown right now.";
        }

        private static Color ToColor(AppearanceColor color) =>
            new Color((float)(color.R / 255.0), (float)(color.G / 255.0), (float)(color.B / 255.0), 1f);


        // The Style sheet: category tabs as a segmented pill, colours as round chips with a sage ring when chosen,
        // and haircuts and hats as rounded option chips. Looks a care rhythm earns carry a small badge: Earned, or
        // the rhythm that earns them.
        private sealed class StylePanel
        {
            // The sheet is no taller than the care sheet, so the baby stays clear above it.
            private const float GridBottom = 42;
            private const float GridTop = 212;
            private const int CategoryCount = 6;

            private readonly StyleCategory[] categories;
            private BabyAppearance current = BabyAppearance.Default;
            private int shown;

            public GameObject Root { get; }
            public Text Feedback { get; }

            public StylePanel(RectTransform content, Action close, Action<Func<BabyAppearance, BabyAppearance>> choose)
            {
                Root = content.parent.gameObject;
                SheetHeading("Style Heading", content, "Baby style", 378);
                SheetClose("Close style", "Done", content, 378, close);

                var tabs = Box("Style Tabs", content, Vector2.zero, Vector2.right, new Vector2(0, 224), new Vector2(0, 288));
                SoftShapes.Pill(Surface(tabs, Mist));
                var grid = Box("Style Swatches", content, Vector2.zero, Vector2.right,
                    new Vector2(0, GridBottom), new Vector2(0, GridTop));
                categories = new[]
                {
                    Category(0, "Skin", tabs, grid, choose, new[]
                    {
                        Skin(SkinTone.Fair), Skin(SkinTone.Light), Skin(SkinTone.Warm),
                        Skin(SkinTone.Tan), Skin(SkinTone.Brown), Skin(SkinTone.Deep)
                    }),
                    Category(1, "Hair", tabs, grid, choose, new[]
                    {
                        Hair(HairColor.Platinum), Hair(HairColor.Honey), Hair(HairColor.Auburn),
                        Hair(HairColor.Chestnut), Hair(HairColor.Espresso), Hair(HairColor.Black)
                    }),
                    Category(2, "Cut", tabs, grid, choose, new[]
                    {
                        Cut(HairStyle.Curls), Cut(HairStyle.Coils), Cut(HairStyle.Smooth), Cut(HairStyle.Tuft)
                    }),
                    Category(3, "Outfit", tabs, grid, choose, new[]
                    {
                        Wear(Outfit.Sage), Wear(Outfit.Oat), Wear(Outfit.Sky), Wear(Outfit.Blush),
                        Wear(Outfit.Butter), Wear(Outfit.Lilac), Wear(Outfit.Terracotta), Wear(Outfit.Midnight)
                    }),
                    // The look packs' outfits: Cozy knits, Garden party, then the Founder pack's Fern & gold.
                    Category(4, "More", tabs, grid, choose, new[]
                    {
                        Wear(Outfit.OatmealKnit), Wear(Outfit.BerryKnit), Wear(Outfit.MossKnit), Wear(Outfit.FogKnit),
                        Wear(Outfit.FernGold), Wear(Outfit.Rosebud), Wear(Outfit.Marigold), Wear(Outfit.GardenMint),
                        Wear(Outfit.Bluebell)
                    }),
                    Category(5, "Hat", tabs, grid, choose, new[]
                    {
                        Hat(Headwear.None, "None"), Hat(Headwear.Bow, "Bow"), Hat(Headwear.BearBeanie, "Bear beanie"),
                        Hat(Headwear.BucketHat, "Bucket hat"), Hat(Headwear.DaisyClip, "Daisy clip"),
                        Hat(Headwear.PomBeanie, "Pom-pom beanie"), Hat(Headwear.SunHat, "Sun hat")
                    })
                };

                Feedback = Label("Style Feedback", content, "", 23, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 2), Vector2.right, new Vector2(0, GridBottom - 2));
                FitText(Feedback, 18, 23);
                ShowCategory(0);
                Root.SetActive(false);
            }

            public void Open(BabyAppearance appearance)
            {
                UpdateSelection(appearance);
                categories[shown].SelectedButton(current).Select();
            }

            public void UpdateSelection(BabyAppearance appearance)
            {
                current = appearance ?? BabyAppearance.Default;
                foreach (var category in categories)
                    category.UpdateSelection(current);
                Feedback.text = string.Empty;
            }

            // Which earned looks are earned: by the longest care rhythm, or all of them in developer mode.
            public void SetEarned(int longestRhythm, bool unlockAll)
            {
                foreach (var category in categories)
                    category.SetEarned(longestRhythm, unlockAll);
            }

            // Which special looks the household owns: a locked one carries the "Special" chip.
            public void SetSpecial(IEntitlements owned)
            {
                foreach (var category in categories)
                    category.SetSpecial(owned);
            }

            private void ShowCategory(int index)
            {
                shown = index;
                for (var i = 0; i < categories.Length; i++)
                    categories[i].SetShown(i == index);
            }

            private StyleCategory Category(int index, string title, RectTransform tabs, RectTransform grid,
                Action<Func<BabyAppearance, BabyAppearance>> choose, SwatchSpec[] specs)
            {
                var width = 1f / CategoryCount;
                var tab = ActionButton(title, tabs, index * width, (index + 1) * width, 6, 58,
                    () => ShowCategory(index), Mist, Muted);
                tab.name = "Style tab " + title;
                FitText(tab.GetComponentInChildren<Text>(), 17, 23);
                SoftShapes.Pill(tab.image);
                var tabRect = (RectTransform)tab.transform;
                tabRect.offsetMin = new Vector2(index == 0 ? 6 : 2, tabRect.offsetMin.y);
                tabRect.offsetMax = new Vector2(index == CategoryCount - 1 ? -6 : -2, tabRect.offsetMax.y);

                var group = Box(title + " Choices", grid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var columns = (specs.Length + 1) / 2;
                var swatches = new StyleSwatch[specs.Length];
                for (var i = 0; i < specs.Length; i++)
                {
                    var row = i / columns;
                    var inRow = Math.Min(columns, specs.Length - row * columns);
                    var column = i % columns + (columns - inRow) / 2f;
                    var anchorMin = new Vector2(column / columns, row == 0 ? 0.5f : 0);
                    var anchorMax = new Vector2((column + 1) / columns, row == 0 ? 1 : 0.5f);
                    swatches[i] = new StyleSwatch(title, specs[i], group, anchorMin, anchorMax, choose);
                }

                return new StyleCategory(tab, group.gameObject, swatches);
            }

            private static SwatchSpec Skin(SkinTone tone) =>
                new SwatchSpec(tone.ToString(), ToColor(AppearancePalette.Skin(tone, WarmSkin)), null,
                    look => look.WithSkinTone(tone));

            private static SwatchSpec Hair(HairColor color) =>
                new SwatchSpec(color.ToString(), ToColor(AppearancePalette.Hair(color, ChestnutHair)), null,
                    look => look.WithHairColor(color));

            private static SwatchSpec Cut(HairStyle cut) =>
                new SwatchSpec(cut.ToString(), null, null, look => look.WithHairStyle(cut));

            private static SwatchSpec Wear(Outfit outfit) =>
                new SwatchSpec(OutfitName(outfit), ToColor(AppearancePalette.OutfitMain(outfit, SageMain)),
                    ToColor(AppearancePalette.OutfitAccent(outfit, SageAccent)), look => look.WithOutfit(outfit),
                    EarnedLooks.RhythmFor(outfit), CosmeticCatalog.PackFor(CosmeticCatalog.OutfitId(outfit)) != null
                        ? CosmeticCatalog.OutfitId(outfit) : null);

            private static SwatchSpec Hat(Headwear headwear, string title) =>
                new SwatchSpec(title, null, null, look => look.WithHeadwear(headwear), EarnedLooks.RhythmFor(headwear),
                    CosmeticCatalog.PackFor(CosmeticCatalog.HeadwearId(headwear)) != null ? CosmeticCatalog.HeadwearId(headwear) : null);
        }

        private sealed class SwatchSpec
        {
            public SwatchSpec(string title, Color? chip, Color? trim, Func<BabyAppearance, BabyAppearance> change,
                int earnedAt = 0, string specialId = null)
            {
                SpecialId = specialId;
                Title = title;
                Chip = chip;
                Trim = trim;
                Change = change;
                EarnedAt = earnedAt;
            }

            // The care rhythm (days) that earns this look; 0 for a look that is always there.
            public int EarnedAt { get; }
            // The content ID of a look-pack item (CosmeticCatalog); null for a free or earned look.
            public string SpecialId { get; }

            public string Title { get; }
            public Color? Chip { get; }
            public Color? Trim { get; }
            public Func<BabyAppearance, BabyAppearance> Change { get; }
        }

        private sealed class StyleCategory
        {
            private readonly Button tab;
            private readonly Text tabLabel;
            private readonly GameObject group;
            private readonly StyleSwatch[] swatches;

            public StyleCategory(Button tab, GameObject group, StyleSwatch[] swatches)
            {
                this.tab = tab;
                tabLabel = tab.GetComponentInChildren<Text>();
                this.group = group;
                this.swatches = swatches;
            }

            // The shown tab is a white segment lifted off the soft track.
            public void SetShown(bool isShown)
            {
                group.SetActive(isShown);
                tab.image.color = isShown ? Paper : Mist;
                tabLabel.color = isShown ? Ink : Muted;
                SoftShapes.Lift(tab.gameObject);
                tab.GetComponent<UnityEngine.UI.Shadow>().effectColor = isShown ? WithAlpha(ShadowInk, 0.10f) : Color.clear;
            }

            public void UpdateSelection(BabyAppearance appearance)
            {
                foreach (var swatch in swatches)
                    swatch.UpdateSelection(appearance);
            }

            public void SetEarned(int longestRhythm, bool unlockAll)
            {
                foreach (var swatch in swatches)
                    swatch.SetEarned(longestRhythm, unlockAll);
            }

            public void SetSpecial(IEntitlements owned)
            {
                foreach (var swatch in swatches)
                    swatch.SetSpecial(owned);
            }

            public Button SelectedButton(BabyAppearance appearance)
            {
                foreach (var swatch in swatches)
                {
                    if (swatch.Matches(appearance))
                        return swatch.Button;
                }

                return swatches[0].Button;
            }
        }

        // A colour is a round chip with its name under it and a sage halo when chosen; a cut or a hat is a rounded
        // option chip that turns soft sage with a sage outline when chosen.
        private sealed class StyleSwatch
        {
            private const float Chip = 50;
            private static readonly Color ChipEdge = new Color(46 / 255f, 42 / 255f, 51 / 255f, 0.16f);

            private readonly Func<BabyAppearance, BabyAppearance> change;
            private readonly Image ring;
            private readonly Text label;
            private readonly bool colour;
            private readonly int earnedAt;
            private readonly string specialId;
            private readonly Image badge;
            private readonly Text badgeLabel;
            private bool locked;

            public Button Button { get; }

            public StyleSwatch(string category, SwatchSpec spec, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                Action<Func<BabyAppearance, BabyAppearance>> choose)
            {
                change = spec.Change;
                colour = spec.Chip.HasValue;
                Button = ActionButton(spec.Title, parent, 0, 1, 0, 0, () => choose(spec.Change), colour ? Color.clear : Mist, Ink);
                Button.name = category + " " + spec.Title;
                var rect = (RectTransform)Button.transform;
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = new Vector2(5, 5);
                rect.offsetMax = new Vector2(-5, -5);

                label = Button.GetComponentInChildren<Text>();
                if (spec.Chip is Color color)
                {
                    FitText(label, 14, 20);
                    label.rectTransform.anchorMax = new Vector2(1, 0);
                    label.rectTransform.offsetMin = new Vector2(2, -2);
                    label.rectTransform.offsetMax = new Vector2(-2, 24);
                    var chip = Box(Button.name + " Color", Button.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-Chip / 2, -Chip - 2), new Vector2(Chip / 2, -2));
                    SoftShapes.Disc(Surface(chip, color));
                    if (spec.Trim is Color trim)
                    {
                        var stripe = Box(Button.name + " Accent", chip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                        SoftShapes.Trim(Surface(stripe, trim));
                    }

                    var edge = Box(Button.name + " Edge", chip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    SoftShapes.Edge(Surface(edge, ChipEdge));
                    var selection = Box(Button.name + " Selection", Button.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-Chip / 2 - 7, -Chip - 9), new Vector2(Chip / 2 + 7, 5));
                    ring = Surface(selection, Fern);
                    SoftShapes.Halo(ring);
                }
                else
                {
                    FitText(label, 16, 23);
                    label.rectTransform.offsetMin = new Vector2(10, 4);
                    label.rectTransform.offsetMax = new Vector2(-10, -4);
                    SoftShapes.Pill(Button.image);
                    var selection = Box(Button.name + " Selection", Button.transform, Vector2.zero, Vector2.one,
                        Vector2.zero, Vector2.zero);
                    ring = Surface(selection, Fern);
                    SoftShapes.Outline(ring, 38);
                }

                // An earned look's badge sits on the chip's top right: "Earned", or the rhythm that earns it.
                earnedAt = spec.EarnedAt;
                specialId = spec.SpecialId;
                if (earnedAt > 0 || specialId != null)
                {
                    // Beside a colour chip's top right, or on a pill's top right corner; clear of the tabs above.
                    var anchor = colour ? new Vector2(0.5f, 1) : Vector2.one;
                    var corner = colour
                        ? Box(Button.name + " Badge", Button.transform, anchor, anchor, new Vector2(6, -18), new Vector2(90, 8))
                        : Box(Button.name + " Badge", Button.transform, anchor, anchor, new Vector2(-86, -16), new Vector2(-4, 10));
                    badge = Surface(corner, FernSoft);
                    SoftShapes.Pill(badge);
                    badgeLabel = Label(Button.name + " Badge Label", corner, "Earned", 15, FernDeep, TextAnchor.MiddleCenter,
                        new Vector2(4, 0), Vector2.one, new Vector2(-4, 0), FontStyle.Bold);
                    FitText(badgeLabel, 11, 15);
                }
            }

            public void SetEarned(int longestRhythm, bool unlockAll)
            {
                if (earnedAt <= 0)
                    return;

                locked = !unlockAll && longestRhythm < earnedAt;
                badge.color = locked ? Mist : FernSoft;
                badgeLabel.color = locked ? Muted : FernDeep;
                badgeLabel.text = locked ? $"{earnedAt} days" : "Earned";
            }

            // A look-pack item carries a small butter "Special" chip while its pack is not owned, and nothing once owned.
            public void SetSpecial(IEntitlements owned)
            {
                if (specialId == null)
                    return;

                locked = !owned.IsCosmeticUnlocked(specialId);
                badge.gameObject.SetActive(locked);
                badge.color = Butter;
                badgeLabel.color = ButterInk;
                badgeLabel.text = "Special";
            }

            public bool Matches(BabyAppearance appearance) => change(appearance).Equals(appearance);

            public void UpdateSelection(BabyAppearance appearance)
            {
                var isSelected = Matches(appearance);
                ring.gameObject.SetActive(isSelected);
                if (!colour)
                    Button.image.color = isSelected ? FernSoft : Mist;
                label.color = isSelected ? (colour ? Ink : FernDeep) : locked ? Secondary : Muted;
                SetRole(label, isSelected ? TextRole.Strong : TextRole.Body);
            }
        }
    }
}
