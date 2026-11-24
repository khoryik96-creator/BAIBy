using System;
using System.Collections.Generic;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // The care view (docs/interface-style.md): a floating header card (Home, the name and day, Style and Decor, and
    // the condition chip), a card of ring meters, the routine strip, the baby's message as a speech bubble, and the
    // care sheet with pastel action tiles and the Next day and Growth buttons.
    //
    // The world baby keeps its size while the canvas follows the screen, so the sheet's tiles shrink on short
    // screens (a desktop window) to keep the baby's face clear, and on wide screens the bubble moves beside the baby.
    public sealed partial class NurseryPreview
    {
        private const float HeaderTop = 14;
        private const float HeaderRow = 112;
        private const float HeaderPad = 15;
        private const float HeaderButton = 82;
        private const float HeaderGap = 18;
        private const float ChipHeight = 50;
        private const float NeedsHeight = 112;
        private const float StackGap = 10;
        private const float StripGap = 8;
        private const float StripHeight = 60;
        private const float StackWidth = 660;

        private RectTransform headerCard;
        private RectTransform needsCard;
        private RectTransform careHead;
        private RectTransform careActions;
        private Text careTitle;
        private Image moodPill;
        private Text moodLabel;
        private Image moodDot;
        private Image bubble;
        private RectTransform bubbleRect;
        private readonly List<Button> careTiles = new List<Button>();
        private Vector2 laidOutArea = new Vector2(-1, -1);
        private string bubbleText;

        private void BuildHeader()
        {
            var top = Box("Header", safeArea, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-StackWidth / 2, -HeaderTop - HeaderRow), new Vector2(StackWidth / 2, -HeaderTop));
            header = top.gameObject;
            headerCard = Box("Header Card", top, new Vector2(0, 1), Vector2.one, new Vector2(0, -HeaderRow), Vector2.zero);
            Card(headerCard, Glass, Px(24));
            needsCard = Box("Needs Card", top, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            Card(needsCard, Glass, Px(22));

            homeChip = IconButton("Home", headerCard, "Home", ShowHome, Mist);
            var dot = Box("Home Attention", homeChip.transform, Vector2.one, Vector2.one,
                new Vector2(-22, -22), new Vector2(-6, -6));
            homeDot = Surface(dot, Amber);
            SoftShapes.Disc(homeDot);
            dot.gameObject.SetActive(false);
            homeChip.gameObject.SetActive(false);
            styleButton = IconButton("Style", headerCard, "Style", () => ShowPanel(NurseryPanel.Style), Mist);
            styleButton.gameObject.SetActive(false);
            decorButton = IconButton("Decor", headerCard, "Decor", OpenDecor, Mist);
            decorButton.gameObject.SetActive(false);

            title = Label("Baby Name", headerCard, "Your nursery", 41, Ink, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
            FitText(title, 28, 41);
            age = Label("Age", headerCard, "", 24, Secondary, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero);
            FitText(age, 17, 24);
            wake = Label("Header Label", headerCard, "BAIby", 22, Fern, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);

            satiety = Meter("Fullness", needsCard, "Fullness", FullnessRing);
            comfort = Meter("Comfort", needsCard, "Comfort", ComfortRing);
            energy = Meter("Energy", needsCard, "Energy", EnergyRing);
            cleanliness = Meter("Cleanliness", needsCard, "Clean", CleanRing);
            health = Meter("Health", needsCard, "Health", HealthRing);
            PlaceMeters(new[] { satiety, comfort, energy, cleanliness, health });
        }

        // Rings share the needs card's width evenly.
        private static void PlaceMeters(IReadOnlyList<NeedMeter> meters)
        {
            for (var i = 0; i < meters.Count; i++)
                meters[i].Place(i / (float)meters.Count, (i + 1) / (float)meters.Count, 64, 10, 28);
        }

        private void BuildCareSheet(RectTransform panel)
        {
            careHead = Box("Care Heading", panel, Vector2.zero, Vector2.right, Vector2.zero, Vector2.zero);
            careTitle = Label("Care Title", careHead, "Care", 36, Ink, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
            FitText(careTitle, 26, 36);
            careTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            favorite = Label("Favorite Toy", careHead, "", 22, Secondary, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero);
            FitText(favorite, 16, 22);
            var mood = Pill("Mood", careHead, "Content", FernSoft, FernDeep, Fern);
            moodPill = mood.Pill;
            moodLabel = mood.Label;
            moodDot = mood.Mark;
            newSoundNotice = BuildNewSoundNotice(careHead);
            BuildSwitchChip(careHead);

            careActions = Box("Care Actions", panel, Vector2.zero, Vector2.right, Vector2.zero, Vector2.zero);
            feedButton = CareTile("Feed", careActions, "Feed", "Feed", Peach, () => ShowPanel(NurseryPanel.Feeding));
            CareTile("Soothe", careActions, "Soothe", "Soothe", Lilac, () => Care(CareAction.Soothe));
            CareTile("Rest", careActions, "Rest", "Rest", Sky, () => Care(CareAction.Rest));
            washUpButton = CareTile("Wash up", careActions, "Wash", "Wash", Mint, () => ShowPanel(NurseryPanel.Hygiene));
            CareTile("Play with rattle", careActions, "Rattle", "Rattle", Butter, () => Care(CareAction.Play, Toy.Rattle));
            CareTile("Play with plush", careActions, "Plush", "Plush", Blush, () => Care(CareAction.Play, Toy.Plush));
            // Talk time, once First words are reached.
            talkButton = CareTile("Talk", careActions, "Talk", "Talk", Lilac, OpenTalk);
            // Not care: a journal photo of the moment, three a day.
            photoButton = CareTile("Take a photo", careActions, "Photo", "Photo", Mist, TakePhoto);
            careTiles.AddRange(careActions.GetComponentsInChildren<Button>(true));
            talkButton.gameObject.SetActive(false);

            previewDayButton = ActionButton("Preview next day", panel, 0, 0.5f, 0, 0, NextPreviewDay, Paper, Ink);
            WithIcon(previewDayButton, "Next day", "NextDay", Ink);
            developmentButton = ActionButton("Development", panel, 0.5f, 1, 0, 0,
                () => ShowPanel(NurseryPanel.Development), Fern, Color.white);
            WithIcon(developmentButton, "Growth", "Growth", Color.white);

            // The baby's current message, in a bubble that floats above the sheet near the baby.
            bubbleRect = Box("Speech Bubble", controls.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero);
            bubbleRect.pivot = Vector2.zero;
            bubble = Surface(bubbleRect, Paper);
            SoftShapes.Bubble(bubble, true, Px(16));
            SoftShapes.Lift(bubble.gameObject);
            bubble.GetComponent<UnityEngine.UI.Shadow>().effectColor = WithAlpha(ShadowInk, 0.12f);
            status = Label("Reaction", bubbleRect, "", 24, Ink, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, Vector2.zero);
            status.rectTransform.offsetMin = new Vector2(Px(12), Px(8));
            status.rectTransform.offsetMax = new Vector2(-Px(12), -Px(8));
        }

        // A pastel action tile: an icon on a soft badge above its label.
        private static Button CareTile(string objectName, Transform parent, string label, string icon, Color tint, Action action)
        {
            var button = ActionButton(objectName, parent, 0, 1, 0, 0, action, tint, Ink);
            SoftShapes.Radius(button.image, Px(20));
            var text = button.GetComponentInChildren<Text>();
            text.text = label;
            FitText(text, 18, 26);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = new Vector2(1, 0.38f);
            text.rectTransform.offsetMin = new Vector2(6, 4);
            text.rectTransform.offsetMax = new Vector2(-6, 0);
            var badge = Box("Badge", button.transform, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f),
                new Vector2(-28, -28), new Vector2(28, 28));
            SoftShapes.Radius(Surface(badge, new Color(1, 1, 1, 0.75f)), Px(12));
            IconAt("Icon", badge, icon, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, Px(20));
            return button;
        }

        // A label beside a leading icon, centred together in the button.
        private static void WithIcon(Button button, string label, string icon, Color color)
        {
            var text = button.GetComponentInChildren<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleLeft;
            FitText(text, 18, 26);
            var image = IconAt("Icon", button.transform, icon, color, new Vector2(0.5f, 0.5f), Vector2.zero, Px(18));
            CenterWithIcon(text, image, 320);
        }

        // Keeps an icon and its label centred as a pair within the button's width; a long label shrinks to fit.
        private static void CenterWithIcon(Text text, Image icon, float buttonWidth)
        {
            var size = icon.rectTransform.sizeDelta.x;
            var width = Mathf.Min(TextWidth(text), buttonWidth - size - 12 - 24);
            var total = size + 12 + width;
            icon.rectTransform.anchoredPosition = new Vector2(-total / 2 + size / 2, 0);
            text.rectTransform.anchorMin = new Vector2(0.5f, 0);
            text.rectTransform.anchorMax = new Vector2(0.5f, 1);
            text.rectTransform.offsetMin = new Vector2(-total / 2 + size + 12, 0);
            text.rectTransform.offsetMax = new Vector2(total / 2 + 4, 0);
        }

        private void UpdateMood(CareMood mood)
        {
            switch (mood)
            {
                case CareMood.Upset:
                    moodPill.color = Blush;
                    moodLabel.color = BlushInk;
                    moodDot.color = Hex(0xC2473A);
                    moodLabel.text = "Upset";
                    break;
                case CareMood.Fussy:
                    moodPill.color = Butter;
                    moodLabel.color = ButterInk;
                    moodDot.color = Hex(0xC99520);
                    moodLabel.text = "Fussy";
                    break;
                default:
                    moodPill.color = FernSoft;
                    moodLabel.color = FernDeep;
                    moodDot.color = Fern;
                    moodLabel.text = "Content";
                    break;
            }
        }

        // Every frame: a new screen size or safe area lays the sheets out again; a new message resizes the bubble.
        private void FitInterface()
        {
            if (safeArea == null || careActions == null)
                return;

            var area = safeArea.rect.size;
            if (area != laidOutArea)
            {
                laidOutArea = area;
                LayoutTopStack();
                LayoutCareSheet();
                LayoutPetSheet();
            }
            else if (status.text != bubbleText)
            {
                LayoutBubble(bubble, bubbleRect, status, (RectTransform)controls.transform);
                bubbleText = status.text;
            }

            UpdatePetBubble();
        }

        // The header card, the needs card under it and the routine strip under that. The condition chip sits on
        // the right of the name row when Style and Decor are hidden (another panel is open), or on a second row.
        private void LayoutTopStack()
        {
            if (headerCard == null)
                return;

            var homeShown = homeChip.gameObject.activeSelf;
            var toolsShown = styleButton.gameObject.activeSelf || decorButton.gameObject.activeSelf;
            var chipShown = conditionChip != null && conditionChip.gameObject.activeSelf;
            var columnLeft = homeShown ? HeaderPad + HeaderButton + HeaderGap : HeaderPad + 12;
            var columnRight = toolsShown ? HeaderPad + 2 * HeaderButton + 2 * HeaderGap : HeaderPad + 6;
            var chipInRow = chipShown && !toolsShown;
            var chipWidth = 0f;
            if (chipShown)
            {
                var room = chipInRow ? StackWidth - columnLeft - HeaderPad - 200 : StackWidth - 2 * HeaderPad;
                chipWidth = Mathf.Min(PillWidth(conditionChipLabel, 56), room);
                if (chipInRow)
                    columnRight = HeaderPad + chipWidth + HeaderGap;
            }

            var cardHeight = chipShown && !chipInRow ? HeaderRow + ChipHeight + 8 : HeaderRow;
            PlaceInHeader((RectTransform)homeChip.transform, HeaderPad, HeaderPad, HeaderButton, HeaderButton, false);
            PlaceInHeader((RectTransform)decorButton.transform, HeaderPad, HeaderPad, HeaderButton, HeaderButton, true);
            PlaceInHeader((RectTransform)styleButton.transform, HeaderPad + HeaderButton + HeaderGap, HeaderPad,
                HeaderButton, HeaderButton, true);
            Stretch(title.rectTransform, columnLeft, columnRight, 14, 64);
            Stretch(age.rectTransform, columnLeft, columnRight, 64, 96);
            Stretch(wake.rectTransform, columnLeft, columnRight, 64, 96);
            // The header label and the day line share one spot; a companion's day line always wins, in every view.
            wake.enabled = string.IsNullOrEmpty(age.text);
            if (chipShown)
            {
                var chipTop = chipInRow ? (HeaderRow - ChipHeight) / 2 : HeaderRow - 6;
                PlaceInHeader((RectTransform)conditionChip.transform, HeaderPad, chipTop, chipWidth, ChipHeight, true);
            }

            headerCard.offsetMin = new Vector2(0, -cardHeight);
            headerCard.offsetMax = Vector2.zero;
            var needsShown = AnyMeterShown();
            needsCard.gameObject.SetActive(needsShown);
            var needsTop = cardHeight + StackGap;
            needsCard.offsetMin = new Vector2(0, -needsTop - NeedsHeight);
            needsCard.offsetMax = new Vector2(0, -needsTop);
            var total = needsShown ? needsTop + NeedsHeight : cardHeight;
            var root = (RectTransform)header.transform;
            root.offsetMin = new Vector2(-StackWidth / 2, -HeaderTop - total);
            root.offsetMax = new Vector2(StackWidth / 2, -HeaderTop);

            if (routineStrip != null)
            {
                var strip = (RectTransform)routineStrip.Root.transform;
                var stripTop = HeaderTop + total + StripGap;
                strip.offsetMin = new Vector2(-StackWidth / 2, -stripTop - StripHeight);
                strip.offsetMax = new Vector2(StackWidth / 2, -stripTop);
            }
        }

        private bool AnyMeterShown()
        {
            foreach (var meter in new[] { satiety, comfort, energy, cleanliness, health })
            {
                if (meter.Root.gameObject.activeSelf)
                    return true;
            }

            foreach (var meter in petMeters)
            {
                if (meter != null && meter.Root.gameObject.activeSelf)
                    return true;
            }

            return false;
        }

        // Places a control in the header card by its distance from the card's top and its left or right edge.
        private static void PlaceInHeader(RectTransform rect, float side, float top, float width, float height, bool fromRight)
        {
            var anchor = new Vector2(fromRight ? 1 : 0, 1);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(fromRight ? -side : side, -top);
        }

        // Stretches a rect across its parent between left and right insets, between top and bottom from its top.
        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, -bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // Sizes of the care sheet at this screen height: roomy on a phone, compact in a short desktop window.
        private struct SheetMetrics
        {
            public float Tile;
            public float Row;
            public float Gap;
            public float Bottom;
            public float Heading;
        }

        private SheetMetrics Metrics()
        {
            var t = Mathf.Clamp01((safeArea.rect.height - 1120f) / 440f);
            return new SheetMetrics
            {
                Tile = Mathf.Lerp(96, 128, t),
                Row = Mathf.Lerp(80, 88, t),
                Gap = Mathf.Lerp(14, 18, t),
                Bottom = Mathf.Lerp(6, 10, t),
                Heading = Mathf.Lerp(60, 70, t),
            };
        }

        private void LayoutCareSheet()
        {
            if (careActions == null || safeArea == null)
                return;

            var metrics = Metrics();
            var bottomRow = new List<Button> { previewDayButton };
            if (routineButton != null && routineButton.gameObject.activeSelf)
                bottomRow.Add(routineButton);
            bottomRow.Add(developmentButton);
            PlaceRow(bottomRow, metrics.Bottom, metrics.Row, metrics.Gap, StackWidth);

            var y = metrics.Bottom + metrics.Row + metrics.Gap + 4;
            var tiles = new List<(Button Button, float Weight)>();
            foreach (var tile in careTiles)
            {
                if (tile.gameObject.activeSelf)
                    tiles.Add((tile, tile == healthButton ? 1.6f : 1));
            }

            LayoutTiles(careActions, tiles, y, metrics.Tile, metrics.Gap);
            y += 2 * metrics.Tile + metrics.Gap * 2 + 6;
            careHead.offsetMin = new Vector2(0, y);
            careHead.offsetMax = new Vector2(0, y + metrics.Heading);
            LayoutCareHeading(metrics.Heading);
            var sheet = (RectTransform)controls.transform;
            sheet.offsetMax = new Vector2(sheet.offsetMax.x, y + metrics.Heading + SheetBottomPad + SheetTopPad);
            LayoutBubble(bubble, bubbleRect, status, sheet);
            bubbleText = status.text;
        }

        // A row of equal buttons across the sheet.
        private static void PlaceRow(IReadOnlyList<Button> buttons, float bottom, float height, float gap, float rowWidth)
        {
            var each = (rowWidth - gap * (buttons.Count - 1)) / buttons.Count;
            for (var i = 0; i < buttons.Count; i++)
            {
                var rect = (RectTransform)buttons[i].transform;
                rect.anchorMin = new Vector2(i / (float)buttons.Count, 0);
                rect.anchorMax = new Vector2((i + 1) / (float)buttons.Count, 0);
                rect.offsetMin = new Vector2(i == 0 ? 0 : gap / 2, bottom);
                rect.offsetMax = new Vector2(i == buttons.Count - 1 ? 0 : -gap / 2, bottom + height);
                if (buttons[i].transform.Find("Icon") is Transform icon && icon.GetComponent<Image>() is Image image)
                    CenterWithIcon(buttons[i].GetComponentInChildren<Text>(), image, each);
            }
        }

        // Tiles in two rows (four on top at most, half of them rounded up), each row sharing the width by weight.
        private static void LayoutTiles(RectTransform grid, IReadOnlyList<(Button Button, float Weight)> tiles, float bottom,
            float tile, float gap)
        {
            grid.offsetMin = new Vector2(0, bottom);
            grid.offsetMax = new Vector2(0, bottom + 2 * tile + gap);
            var first = tiles.Count > 3 ? Mathf.Min(4, (tiles.Count + 1) / 2) : tiles.Count;
            PlaceTileRow(tiles, 0, first, tile + gap, tile, gap);
            PlaceTileRow(tiles, first, tiles.Count, 0, tile, gap);
        }

        private static void PlaceTileRow(IReadOnlyList<(Button Button, float Weight)> tiles, int from, int to, float bottom,
            float height, float gap)
        {
            var total = 0f;
            for (var i = from; i < to; i++)
                total += tiles[i].Weight;
            var start = 0f;
            for (var i = from; i < to; i++)
            {
                var rect = (RectTransform)tiles[i].Button.transform;
                var end = start + tiles[i].Weight;
                rect.anchorMin = new Vector2(start / total, 0);
                rect.anchorMax = new Vector2(end / total, 0);
                rect.offsetMin = new Vector2(i == from ? 0 : gap / 2, bottom);
                rect.offsetMax = new Vector2(i == to - 1 ? 0 : -gap / 2, bottom + height);
                start = end;
            }
        }

        // "Care for Mia" and the favourite toy on the left; the mood pill (or a new sound) and the switch on the right.
        private void LayoutCareHeading(float height)
        {
            var right = 0f;
            if (switchChip != null && switchChip.gameObject.activeSelf && switchChip.transform.parent == careHead)
                right = PlacePill((RectTransform)switchChip.transform, switchLabel, right, height, 260, 0);
            var lead = newSoundNotice.gameObject.activeSelf
                ? PlacePill((RectTransform)newSoundNotice.transform, newSoundNotice.GetComponentInChildren<Text>(), right, height,
                    260, 44)
                : PlacePill((RectTransform)moodPill.transform, moodLabel, right, height, 200, Px(24) - Px(10));
            moodPill.gameObject.SetActive(!newSoundNotice.gameObject.activeSelf);
            right = lead;
            LayoutTitleBlock(careTitle, favorite, right, height);
        }

        // A title with an optional quiet line under it, left of the pills.
        private static void LayoutTitleBlock(Text heading, Text detail, float right, float height)
        {
            var hasDetail = detail.gameObject.activeSelf && !string.IsNullOrEmpty(detail.text);
            var split = hasDetail ? height * 0.42f : 0;
            heading.rectTransform.anchorMin = Vector2.zero;
            heading.rectTransform.anchorMax = Vector2.one;
            heading.rectTransform.offsetMin = new Vector2(0, split);
            heading.rectTransform.offsetMax = new Vector2(-right, 0);
            detail.rectTransform.anchorMin = Vector2.zero;
            detail.rectTransform.anchorMax = new Vector2(1, 0);
            detail.rectTransform.offsetMin = Vector2.zero;
            detail.rectTransform.offsetMax = new Vector2(-right, split);
        }

        // Places a pill at the right of a heading row, after `right` units already used. Returns the new right edge.
        private static float PlacePill(RectTransform pill, Text label, float right, float rowHeight, float max, float lead)
        {
            var width = Mathf.Min(PillWidth(label, lead + Px(10)), max);
            var height = Px(27);
            pill.anchorMin = new Vector2(1, 0.5f);
            pill.anchorMax = new Vector2(1, 0.5f);
            pill.pivot = new Vector2(1, 0.5f);
            pill.sizeDelta = new Vector2(width, height);
            pill.anchoredPosition = new Vector2(-right, 0);
            return right + width + 12;
        }

        // The bubble floats above its sheet, its small corner toward the baby; on wide screens it sits beside the
        // baby's head. Long messages shrink their type a little before the bubble grows taller.
        // lift raises the bubble above something that sits on the sheet's top edge, such as the pet feeder's chip.
        private void LayoutBubble(Image shape, RectTransform rect, Text text, RectTransform sheet, float lift = 0)
        {
            if (shape == null || text == null || safeArea == null)
                return;

            var message = text.text;
            rect.gameObject.SetActive(!string.IsNullOrEmpty(message));
            if (string.IsNullOrEmpty(message))
                return;

            var area = safeArea.rect.size;
            var beside = area.x >= 0.48f * area.y + 840;
            var maxWidth = beside ? 440f : StackWidth;
            var padX = Px(12);
            var padY = Px(8);
            var sheetHeight = sheet.rect.height;
            // Above the sheet the bubble stays under the baby's chin, about 46% of the height from the bottom.
            var room = beside ? 132 : Mathf.Max(64, 0.46f * area.y - sheetHeight - 14 - lift - 2 * padY);
            var size = 24;
            Vector2 content;
            do
            {
                text.fontSize = size;
                content = Measure(text, maxWidth - 2 * padX);
                size -= 1;
            } while (content.y > room && size >= 18);

            var width = content.x + 2 * padX;
            var height = content.y + 2 * padY;
            rect.sizeDelta = new Vector2(width, height);
            if (beside)
            {
                SoftShapes.Bubble(shape, false, Px(16));
                var x = Mathf.Min(0.24f * area.y, area.x / 2 - SideMargin - width);
                rect.anchoredPosition = new Vector2(x, 0.58f * area.y - sheetHeight);
            }
            else
            {
                SoftShapes.Bubble(shape, true, Px(16));
                var x = width > 370 ? -width / 2 : Mathf.Clamp(-40, -StackWidth / 2, StackWidth / 2 - width);
                rect.anchoredPosition = new Vector2(x, 14 + lift);
            }
        }

        // The size a text needs at most maxWidth wide, wrapped.
        private static Vector2 Measure(Text text, float maxWidth)
        {
            var scale = Mathf.Max(0.01f, text.pixelsPerUnit);
            var generator = text.cachedTextGeneratorForLayout;
            var single = generator.GetPreferredWidth(text.text, text.GetGenerationSettings(Vector2.zero)) / scale;
            var width = Mathf.Min(Mathf.Ceil(single) + 2, maxWidth);
            var height = generator.GetPreferredHeight(text.text, text.GetGenerationSettings(new Vector2(width, 0))) / scale;
            return new Vector2(width, Mathf.Ceil(height));
        }
    }
}
