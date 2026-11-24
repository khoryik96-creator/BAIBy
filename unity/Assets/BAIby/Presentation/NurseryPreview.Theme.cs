using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // The interface style (docs/interface-style.md): tokens, type, shapes, icons and the building blocks every
    // screen shares. Sizes are canvas units; the canvas is 720 units wide on a phone, so one mockup pixel at the
    // 390 px target is Px(1) = 1.85 units.
    public sealed partial class NurseryPreview
    {
        // Ground, surfaces and ink.
        private static readonly Color Cream = Hex(0xFBF6EE);
        private static readonly Color Paper = Color.white;
        private static readonly Color Glass = new Color(1, 1, 1, 0.9f);
        private static readonly Color Ink = Hex(0x2E2A33);
        private static readonly Color Secondary = Hex(0x6E6873);
        private static readonly Color Muted = Hex(0x5E5863);
        private static readonly Color Mist = Hex(0xF3EEE6);
        private static readonly Color Divider = Hex(0xE4DED6);
        private static readonly Color Outline = Hex(0xE9E3DB);
        private static readonly Color RingTrack = Hex(0xEFE9E1);
        // The primary accent (white text on it) and its soft tint.
        private static readonly Color Fern = Hex(0x4E7A63);
        private static readonly Color FernDeep = Hex(0x3B5F4C);
        private static readonly Color FernSoft = Hex(0xE2EEE6);
        // Pastel tiles.
        private static readonly Color Peach = Hex(0xFBE3D6);
        private static readonly Color Lilac = Hex(0xECE5F7);
        private static readonly Color Sky = Hex(0xE1ECF6);
        private static readonly Color Mint = Hex(0xDDF0EE);
        private static readonly Color Butter = Hex(0xFBF0CF);
        private static readonly Color Blush = Hex(0xF9E1E4);
        // Text on the warm chips: butter, blush and the sage chip.
        private static readonly Color ButterInk = Hex(0x7A5A14);
        private static readonly Color BlushInk = Hex(0x8E2F3E);
        // Need rings.
        private static readonly Color FullnessRing = Hex(0xE7966F);
        private static readonly Color ComfortRing = Hex(0x9C84CF);
        private static readonly Color EnergyRing = Hex(0xE5B94A);
        private static readonly Color CleanRing = Hex(0x6FA0C8);
        private static readonly Color HealthRing = Hex(0x5E9C7A);
        // Status colours kept from the earlier style, retuned to the pastel set.
        private static readonly Color Honey = Hex(0xFBF0CF);
        private static readonly Color Rose = Hex(0xF9E1E4);
        private static readonly Color Leaf = Hex(0xE2EEE6);
        private static readonly Color Amber = Hex(0xD9893F);
        private static readonly Color ShadowInk = new Color(46 / 255f, 42 / 255f, 51 / 255f, 1);

        // Canvas units per mockup pixel at the 390 px phone target.
        private const float PxUnits = 720f / 390f;
        private const float SideMargin = 30;
        private const float SheetChrome = 47;
        private const float SheetTopPad = 30;
        private const float SheetBottomPad = 17;

        private static float Px(float pixels) => pixels * PxUnits;

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        // Headings, names and numbers use Fredoka; body text Nunito Bold; buttons and labels Nunito ExtraBold.
        private enum TextRole { Body, Strong, Heading }

        private static class Typeface
        {
            private static readonly Dictionary<TextRole, Font> Loaded = new Dictionary<TextRole, Font>();
            private static Font fallback;

            public static Font For(TextRole role)
            {
                if (Loaded.TryGetValue(role, out var font))
                    return font;

                var path = role == TextRole.Heading ? "BAIby/Fonts/Fredoka-SemiBold"
                    : role == TextRole.Strong ? "BAIby/Fonts/Nunito-ExtraBold" : "BAIby/Fonts/Nunito-Bold";
                font = Resources.Load<Font>(path);
                if (font == null)
                    Debug.LogWarning($"Missing interface font {path}; using the built-in font.");
                Loaded[role] = font;
                return font;
            }

            public static Font Fallback
            {
                get
                {
                    if (fallback == null)
                        fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    return fallback;
                }
            }
        }

        private static void SetRole(Text text, TextRole role)
        {
            var font = Typeface.For(role);
            if (font != null)
            {
                text.font = font;
                text.fontStyle = FontStyle.Normal;
            }
            else
            {
                text.font = Typeface.Fallback;
                text.fontStyle = role == TextRole.Body ? FontStyle.Normal : FontStyle.Bold;
            }
        }

        // Single lines never vanish when a font's line is taller than its box; best-fit text keeps truncating so it
        // shrinks to fit instead of spilling. Call after building texts that set resizeTextForBestFit.
        private static void SettleText(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                text.verticalOverflow = text.resizeTextForBestFit ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
        }

        private static void FitText(Text text, int min, int max)
        {
            text.fontSize = max;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = max;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static Sprite Icon(string name)
        {
            if (!Icons.TryGetValue(name, out var sprite))
            {
                sprite = Resources.Load<Sprite>("BAIby/UI/Icons/" + name);
                if (sprite == null)
                    Debug.LogWarning($"Missing interface icon {name}.");
                Icons[name] = sprite;
            }

            return sprite;
        }

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        // A square icon centred on a point of its parent, tinted.
        private static Image IconAt(string objectName, Transform parent, string icon, Color color, Vector2 anchor,
            Vector2 offset, float size)
        {
            var rect = Box(objectName, parent, anchor, anchor, offset - Vector2.one * size / 2, offset + Vector2.one * size / 2);
            var image = Surface(rect, color);
            image.sprite = Icon(icon);
            image.preserveAspect = true;
            image.enabled = image.sprite != null;
            return image;
        }

        // A floating card: a soft shadow under a rounded surface. Returns the surface; its parent is the card's root.
        private static Image Card(RectTransform root, Color color, float radius, float shadow = 0.10f, float lift = 11)
        {
            if (shadow > 0)
            {
                var below = Box("Shadow", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var shade = Surface(below, WithAlpha(ShadowInk, shadow));
                SoftShapes.Shadow(shade, radius, below, lift);
            }

            var surface = Surface(Box("Surface", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), color);
            SoftShapes.Radius(surface, radius);
            return surface;
        }

        // The title at the top of a bottom sheet whose content is height units tall, left of its Done button.
        private static Text SheetHeading(string objectName, RectTransform content, string text, float height)
        {
            var heading = Label(objectName, content, text, 38, Ink, TextAnchor.MiddleLeft,
                new Vector2(0, height - 78), new Vector2(0.72f, 0), new Vector2(0, height - 12), FontStyle.Bold);
            FitText(heading, 26, 38);
            return heading;
        }

        // The Done (or Later, Cancel) button at the top right of a sheet whose content is height units tall.
        private static Button SheetClose(string objectName, string label, RectTransform content, float height, Action close)
        {
            var button = ActionButton(objectName, content, 0.74f, 1, height - 78, height - 12, close, Mist, Ink);
            button.GetComponentInChildren<Text>().text = label;
            return button;
        }

        // A small rounded pill with a dot or an icon and a label, such as the mood pill.
        private static (Image Pill, Text Label, Image Mark) Pill(string objectName, Transform parent, string text, Color background,
            Color foreground, Color mark, string icon = null)
        {
            var root = Box(objectName, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var pill = Surface(root, background);
            SoftShapes.Pill(pill);
            Image marker;
            if (icon != null)
                marker = IconAt(objectName + " Icon", root, icon, mark, new Vector2(0, 0.5f), new Vector2(Px(10) + 15, 0), 30);
            else
            {
                marker = Surface(Box(objectName + " Dot", root, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(Px(10), -Px(4)), new Vector2(Px(18), Px(4))), mark);
                SoftShapes.Disc(marker);
            }

            var label = Label(objectName + " Label", root, text, 22, foreground, TextAnchor.MiddleLeft,
                new Vector2(icon != null ? Px(10) + 36 : Px(24), 0), Vector2.one, new Vector2(-Px(10), 0), FontStyle.Bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return (pill, label, marker);
        }

        // The width a pill needs for its label, so it hugs its text.
        private static float PillWidth(Text label, float lead) => lead + TextWidth(label) + Px(12);

        // A label's single-line width at its own font size, ignoring best fit.
        private static float TextWidth(Text label)
        {
            if (string.IsNullOrEmpty(label.text))
                return 0;

            var settings = label.GetGenerationSettings(Vector2.zero);
            settings.resizeTextForBestFit = false;
            var width = label.cachedTextGeneratorForLayout.GetPreferredWidth(label.text, settings) /
                Mathf.Max(0.01f, label.pixelsPerUnit);
            if (float.IsNaN(width) || width <= 0)
                width = label.text.Length * label.fontSize * 0.6f;
            return Mathf.Ceil(width);
        }

        // Places a rect by distances from its parent's edges.
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        // Soft, anti-aliased shapes generated once at runtime: rounded cards, outlines, pills, discs, rings,
        // speech bubbles and shadows. Sliced sprites keep any radius crisp at any size.
        private static class SoftShapes
        {
            private const int CardSize = 96;
            private const float CardRadius = 40;
            private const int DiscSize = 96;
            private const int RingSize = 128;
            private const float RingThickness = 18;
            private const float ShadowBlur = 24;
            private const float ShadowRadius = 28;
            private const float LegacyRadius = 30;

            private static Sprite card;
            private static Sprite topCard;
            private static Sprite cardOutline;
            private static Sprite disc;
            private static Sprite discEdge;
            private static Sprite discTrim;
            private static Sprite halo;
            private static Sprite ring;
            private static Sprite dashed;
            private static Sprite shadow;
            private static Sprite bubbleLow;
            private static Sprite bubbleHigh;

            // The earlier multiplier-based radius, scaled so 1 is the standard 16 px button: radius = 30 / multiplier.
            public static void Round(Image image, float pixelsPerUnit) => Radius(image, LegacyRadius / pixelsPerUnit);

            // Also rounds an outlined button's Border to match.
            public static void Radius(Image image, float units)
            {
                if (card == null)
                    card = Create("Card", CardSize, CardRadius, 0, CardRadius + 2);
                Slice(image, card, CardRadius / Mathf.Max(1, units));
                var border = image.transform.Find("Border");
                if (border != null && border.GetComponent<Image>() is Image outline)
                    Outline(outline, units);
            }

            // Rounded top corners and square bottom ones, for bottom sheets.
            public static void TopRadius(Image image, float units)
            {
                if (topCard == null)
                    topCard = Create("Sheet", CardSize, CardRadius, 0, CardRadius + 2, topOnly: true);
                Slice(image, topCard, CardRadius / Mathf.Max(1, units));
            }

            // Fully round ends; the sprite's radius follows the shorter side at layout.
            public static void Pill(Image image)
            {
                if (card == null)
                    card = Create("Card", CardSize, CardRadius, 0, CardRadius + 2);
                Slice(image, card, 0.5f);
            }

            // A selection ring for a card of the given radius; thicker on larger radii, about 2 to 3 mockup px.
            public static void Outline(Image image, float radius = 30)
            {
                if (cardOutline == null)
                    cardOutline = Create("Card outline", CardSize, CardRadius, 6, CardRadius + 2);
                Slice(image, cardOutline, CardRadius / Mathf.Max(1, radius));
            }

            // A rounded outline whose straight edges tile as dashes, for cards that add something.
            public static void Dashed(Image image, float radius = 48)
            {
                if (dashed == null)
                    dashed = Create("Dashed card", CardSize, CardRadius, 4, CardRadius + 2, dashedEdges: true);
                image.sprite = dashed;
                image.type = Image.Type.Tiled;
                image.fillCenter = false;
                image.pixelsPerUnitMultiplier = CardRadius / Mathf.Max(1, radius);
            }

            public static void Disc(Image image) => Circle(image, ref disc, "Disc", 0);

            public static void Edge(Image image) => Circle(image, ref discEdge, "Disc edge", 4);

            public static void Trim(Image image) => Circle(image, ref discTrim, "Disc trim", 18);

            // The selection ring around a round colour chip.
            public static void Halo(Image image) => Circle(image, ref halo, "Halo", 7);

            // A ring for the need meters; as a filled image it sweeps clockwise from the top.
            public static void Ring(Image image, bool filled)
            {
                if (ring == null)
                    ring = Create("Ring", RingSize, RingSize / 2f, RingThickness, 0);
                image.sprite = ring;
                image.preserveAspect = true;
                if (!filled)
                {
                    image.type = Image.Type.Simple;
                    return;
                }

                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
            }

            // A speech bubble: rounded, with one small corner as its tail (bottom-left, or top-left when high).
            public static void Bubble(Image image, bool tailHigh, float radius)
            {
                if (bubbleLow == null)
                    bubbleLow = Create("Bubble", CardSize, CardRadius, 0, CardRadius + 2, tailCorner: 1);
                if (bubbleHigh == null)
                    bubbleHigh = Create("Bubble high", CardSize, CardRadius, 0, CardRadius + 2, tailCorner: 2);
                Slice(image, tailHigh ? bubbleHigh : bubbleLow, CardRadius / Mathf.Max(1, radius));
            }

            // A blurred shadow under a card of the given radius, pushed down by lift units.
            public static void Shadow(Image image, float radius, RectTransform rect, float lift)
            {
                if (shadow == null)
                    shadow = CreateShadow();
                var scale = Mathf.Clamp(radius / ShadowRadius, 0.6f, 2.4f);
                var spread = ShadowBlur * scale;
                Slice(image, shadow, 1 / scale);
                rect.offsetMin = new Vector2(-spread, -spread - lift);
                rect.offsetMax = new Vector2(spread, spread - lift);
            }

            // A gentle offset shadow for small controls, drawn by uGUI under the control's own graphic.
            public static void Lift(GameObject target)
            {
                var shade = target.GetComponent<UnityEngine.UI.Shadow>();
                if (shade == null)
                    shade = target.AddComponent<UnityEngine.UI.Shadow>();
                shade.effectColor = WithAlpha(ShadowInk, 0.08f);
                shade.effectDistance = new Vector2(0, -4);
            }

            private static void Slice(Image image, Sprite sprite, float multiplier)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.preserveAspect = false;
                image.pixelsPerUnitMultiplier = multiplier;
            }

            private static void Circle(Image image, ref Sprite sprite, string name, float thickness)
            {
                if (sprite == null)
                    sprite = Create(name, DiscSize, DiscSize / 2f, thickness, 0);
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }

            // tailCorner: 0 none, 1 bottom-left, 2 top-left; that corner keeps a small radius.
            private static Sprite Create(string name, int size, float radius, float thickness, float border,
                bool dashedEdges = false, bool topOnly = false, int tailCorner = 0)
            {
                var pixels = new Color32[size * size];
                var half = size / 2f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var px = x + 0.5f - half;
                        var py = y + 0.5f - half;
                        var corner = CornerRadius(px, py, radius, topOnly, tailCorner);
                        var alpha = Coverage(RoundedDistance(px, py, half, corner));
                        if (thickness > 0)
                            alpha -= Coverage(RoundedDistance(px, py, half - thickness, Mathf.Max(0, corner - thickness)));
                        if (dashedEdges)
                            alpha *= DashMask(x, y, size, border);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255));
                    }
                }

                return Build(name, size, pixels, border);
            }

            private static float CornerRadius(float x, float y, float radius, bool topOnly, int tailCorner)
            {
                if (topOnly && y < 0)
                    return 0;
                if (tailCorner == 1 && x < 0 && y < 0)
                    return radius * 0.25f;
                if (tailCorner == 2 && x < 0 && y > 0)
                    return radius * 0.25f;
                return radius;
            }

            // A rounded rectangle blurred like a CSS box-shadow, hollow inside so glass cards stay clean.
            private static Sprite CreateShadow()
            {
                var border = ShadowBlur + ShadowRadius;
                var size = Mathf.RoundToInt(border * 2 + 8);
                var half = size / 2f;
                var sigma = ShadowBlur / 2.4f;
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var distance = RoundedDistance(x + 0.5f - half, y + 0.5f - half, half - ShadowBlur, ShadowRadius);
                        var alpha = 0.5f * (1 - Erf(distance / (sigma * 1.41421f)));
                        alpha *= Mathf.Clamp01((distance + ShadowBlur * 0.5f) / (ShadowBlur * 0.5f));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255));
                    }
                }

                return Build("Shadow", size, pixels, border);
            }

            private static Sprite Build(string name, int size, Color32[] pixels, float border)
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = name,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                sprite.name = name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }

            private static float Erf(float x)
            {
                // Abramowitz and Stegun 7.1.26.
                var sign = Mathf.Sign(x);
                x = Mathf.Abs(x);
                var t = 1 / (1 + 0.3275911f * x);
                var y = 1 - ((((1.061405429f * t - 1.453152027f) * t + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f) *
                    t * Mathf.Exp(-x * x);
                return sign * y;
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
