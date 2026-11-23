using System;
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

        private void BuildStyleButton(RectTransform header)
        {
            styleButton = ActionButton("Style", header, 0.58f, 0.79f, 143, 231,
                () => ShowPanel(NurseryPanel.Style), Mist, Ink);
            styleButton.gameObject.SetActive(false);
            SoftShapes.Round(styleButton.image, 0.7f);
            SoftShapes.Lift(styleButton.gameObject);
            SoftShapes.Round(decorButton.image, 0.7f);
            SoftShapes.Lift(decorButton.gameObject);
        }

        private void ChooseAppearance(Func<BabyAppearance, BabyAppearance> change)
        {
            try
            {
                session.SetAppearance(change(session.State.Appearance));
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
            bool shown = nursery.SetAppearance(appearance, retryFailed);
            style.UpdateSelection(appearance);
            if (!shown)
                style.Feedback.text = "This look couldn't be shown right now.";
        }

        private static Color ToColor(AppearanceColor color) =>
            new Color((float)(color.R / 255.0), (float)(color.G / 255.0), (float)(color.B / 255.0), 1f);

        private sealed class StylePanel
        {
            // The tray is no taller than the Care tray (378), so the baby stays clear above it.
            private const float GridBottom = 42;
            private const float GridTop = 214;

            private readonly StyleCategory[] categories;
            private BabyAppearance current = BabyAppearance.Default;
            private int shown;

            public GameObject Root { get; }
            public Text Feedback { get; }

            public StylePanel(RectTransform content, Action close, Action<Func<BabyAppearance, BabyAppearance>> choose)
            {
                Root = content.parent.gameObject;
                Label("Style Heading", content, "Baby style", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 290), new Vector2(0.72f, 0), new Vector2(0, 366), FontStyle.Bold);
                var done = ActionButton("Done", content, 0.73f, 1, 290, 366, close, Mist, Ink);
                done.name = "Close style";
                SoftShapes.Round(done.image, 1f);
                SoftShapes.Lift(done.gameObject);

                var grid = Box("Style Swatches", content, Vector2.zero, Vector2.right,
                    new Vector2(0, GridBottom), new Vector2(0, GridTop));
                categories = new[]
                {
                    Category(0, "Skin", content, grid, choose, new[]
                    {
                        Skin(SkinTone.Fair), Skin(SkinTone.Light), Skin(SkinTone.Warm),
                        Skin(SkinTone.Tan), Skin(SkinTone.Brown), Skin(SkinTone.Deep)
                    }),
                    Category(1, "Hair", content, grid, choose, new[]
                    {
                        Hair(HairColor.Platinum), Hair(HairColor.Honey), Hair(HairColor.Auburn),
                        Hair(HairColor.Chestnut), Hair(HairColor.Espresso), Hair(HairColor.Black)
                    }),
                    Category(2, "Cut", content, grid, choose, new[]
                    {
                        Cut(HairStyle.Curls), Cut(HairStyle.Coils), Cut(HairStyle.Smooth), Cut(HairStyle.Tuft)
                    }),
                    Category(3, "Outfit", content, grid, choose, new[]
                    {
                        Wear(Outfit.Sage), Wear(Outfit.Oat), Wear(Outfit.Sky), Wear(Outfit.Blush),
                        Wear(Outfit.Butter), Wear(Outfit.Lilac), Wear(Outfit.Terracotta), Wear(Outfit.Midnight)
                    }),
                    Category(4, "Hat", content, grid, choose, new[]
                    {
                        Hat(Headwear.None, "None"), Hat(Headwear.Bow, "Bow"), Hat(Headwear.BearBeanie, "Bear beanie"),
                        Hat(Headwear.BucketHat, "Bucket hat"), Hat(Headwear.DaisyClip, "Daisy clip")
                    })
                };

                Feedback = Label("Style Feedback", content, "", 23, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 2), Vector2.right, new Vector2(0, GridBottom - 2));
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 18;
                Feedback.resizeTextMaxSize = 23;
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

            private void ShowCategory(int index)
            {
                shown = index;
                for (var i = 0; i < categories.Length; i++)
                    categories[i].SetShown(i == index);
            }

            private StyleCategory Category(int index, string title, RectTransform content, RectTransform grid,
                Action<Func<BabyAppearance, BabyAppearance>> choose, SwatchSpec[] specs)
            {
                const float width = 0.2f;
                var tab = ActionButton(title, content, index * width, (index + 1) * width, 222, 282,
                    () => ShowCategory(index), Mist, Ink);
                tab.name = "Style tab " + title;
                tab.GetComponentInChildren<Text>().fontSize = 23;
                SoftShapes.Round(tab.image, 0.6f);

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
                new SwatchSpec(outfit.ToString(), ToColor(AppearancePalette.OutfitMain(outfit, SageMain)),
                    ToColor(AppearancePalette.OutfitAccent(outfit, SageAccent)), look => look.WithOutfit(outfit));

            private static SwatchSpec Hat(Headwear headwear, string title) =>
                new SwatchSpec(title, null, null, look => look.WithHeadwear(headwear));
        }

        private sealed class SwatchSpec
        {
            public SwatchSpec(string title, Color? chip, Color? trim, Func<BabyAppearance, BabyAppearance> change)
            {
                Title = title;
                Chip = chip;
                Trim = trim;
                Change = change;
            }

            public string Title { get; }
            public Color? Chip { get; }
            public Color? Trim { get; }
            public Func<BabyAppearance, BabyAppearance> Change { get; }
        }

        private sealed class StyleCategory
        {
            private static readonly Color SelectedTabText = Color.white;

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

            public void SetShown(bool isShown)
            {
                group.SetActive(isShown);
                tab.image.color = isShown ? Fern : Mist;
                tabLabel.color = isShown ? SelectedTabText : Ink;
            }

            public void UpdateSelection(BabyAppearance appearance)
            {
                foreach (var swatch in swatches)
                    swatch.UpdateSelection(appearance);
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

        private sealed class StyleSwatch
        {
            private static readonly Color ChipEdge = new Color(43 / 255f, 51 / 255f, 44 / 255f, 0.18f);

            private readonly Func<BabyAppearance, BabyAppearance> change;
            private readonly Image ring;
            private readonly Text label;

            public Button Button { get; }

            public StyleSwatch(string category, SwatchSpec spec, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                Action<Func<BabyAppearance, BabyAppearance>> choose)
            {
                change = spec.Change;
                Button = ActionButton(spec.Title, parent, 0, 1, 0, 0, () => choose(spec.Change), Mist, Ink);
                Button.name = category + " " + spec.Title;
                var rect = (RectTransform)Button.transform;
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = new Vector2(4, 4);
                rect.offsetMax = new Vector2(-4, -4);
                SoftShapes.Round(Button.image, 1f);
                SoftShapes.Lift(Button.gameObject);

                label = Button.GetComponentInChildren<Text>();
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 15;
                if (spec.Chip is Color color)
                {
                    label.resizeTextMaxSize = 21;
                    label.rectTransform.offsetMin = new Vector2(6, 4);
                    label.rectTransform.offsetMax = new Vector2(-6, -46);
                    var chip = Box(Button.name + " Color", Button.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-18, -44), new Vector2(18, -8));
                    SoftShapes.Disc(Surface(chip, color));
                    if (spec.Trim is Color trim)
                    {
                        var stripe = Box(Button.name + " Accent", chip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                        SoftShapes.Trim(Surface(stripe, trim));
                    }

                    var edge = Box(Button.name + " Edge", chip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    SoftShapes.Edge(Surface(edge, ChipEdge));
                }
                else
                {
                    label.resizeTextMaxSize = 25;
                    label.rectTransform.offsetMin = new Vector2(8, 6);
                    label.rectTransform.offsetMax = new Vector2(-8, -6);
                }

                var selection = Box(Button.name + " Selection", Button.transform, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero);
                ring = Surface(selection, Fern);
                SoftShapes.Outline(ring);
            }

            public bool Matches(BabyAppearance appearance) => change(appearance).Equals(appearance);

            public void UpdateSelection(BabyAppearance appearance)
            {
                var isSelected = Matches(appearance);
                ring.gameObject.SetActive(isSelected);
                Button.image.color = isSelected ? Paper : Mist;
                label.color = isSelected ? Fern : Ink;
                label.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        // Soft, anti-aliased shapes generated once at runtime so the style controls read as rounded,
        // tactile cards without importing extra UI art.
        private static class SoftShapes
        {
            private const int CardSize = 64;
            private const float CardRadius = 22;
            private const int DiscSize = 96;

            private static Sprite card;
            private static Sprite cardOutline;
            private static Sprite disc;
            private static Sprite discEdge;
            private static Sprite discTrim;
            private static Sprite dashed;

            public static void Round(Image image, float pixelsPerUnit)
            {
                if (card == null)
                    card = Create("Style card", CardSize, CardRadius, 0, CardRadius + 2);
                image.sprite = card;
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.pixelsPerUnitMultiplier = pixelsPerUnit;
            }

            public static void Outline(Image image)
            {
                if (cardOutline == null)
                    cardOutline = Create("Style card outline", CardSize, CardRadius, 4, CardRadius + 2);
                image.sprite = cardOutline;
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
            }

            // A rounded outline whose straight edges tile as dashes, for cards that add something.
            public static void Dashed(Image image)
            {
                if (dashed == null)
                    dashed = Create("Dashed card", CardSize, CardRadius, 3, CardRadius + 2, true);
                image.sprite = dashed;
                image.type = Image.Type.Tiled;
                image.fillCenter = false;
                image.pixelsPerUnitMultiplier = 1;
            }

            public static void Disc(Image image) => Circle(image, ref disc, "Style disc", 0);

            public static void Edge(Image image) => Circle(image, ref discEdge, "Style disc edge", 4);

            public static void Trim(Image image) => Circle(image, ref discTrim, "Style disc trim", 18);

            public static void Lift(GameObject target)
            {
                var shadow = target.AddComponent<Shadow>();
                shadow.effectColor = new Color(43 / 255f, 51 / 255f, 44 / 255f, 0.14f);
                shadow.effectDistance = new Vector2(0, -3);
            }

            private static void Circle(Image image, ref Sprite sprite, string name, float thickness)
            {
                if (sprite == null)
                    sprite = Create(name, DiscSize, DiscSize / 2f, thickness, 0);
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }

            private static Sprite Create(string name, int size, float radius, float thickness, float border,
                bool dashedEdges = false)
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = name,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[size * size];
                var half = size / 2f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var px = x + 0.5f - half;
                        var py = y + 0.5f - half;
                        var alpha = Coverage(RoundedDistance(px, py, half, radius));
                        if (thickness > 0)
                            alpha -= Coverage(RoundedDistance(px, py, half - thickness, Mathf.Max(0, radius - thickness)));
                        if (dashedEdges)
                            alpha *= DashMask(x, y, size, border);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                sprite.name = name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }

            private static float RoundedDistance(float x, float y, float halfExtent, float radius)
            {
                var qx = Mathf.Abs(x) - (halfExtent - radius);
                var qy = Mathf.Abs(y) - (halfExtent - radius);
                var outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
                return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
            }

            private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

            // The straight middle of each edge is the tiled part of the sprite; drawing half of it keeps
            // one dash per tile. Corners stay solid.
            private static float DashMask(int x, int y, int size, float border)
            {
                var start = Mathf.RoundToInt(border);
                var end = size - start;
                var period = end - start;
                var along = x >= start && x < end ? x - start : y >= start && y < end ? y - start : -1;
                if (along < 0 || period <= 0)
                    return 1;

                return along >= period / 4 && along < period * 3 / 4 ? 1 : 0;
            }
        }
    }
}
