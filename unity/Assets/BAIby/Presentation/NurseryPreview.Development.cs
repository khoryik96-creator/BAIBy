using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private DevelopmentPanel development;
        private Button developmentButton;

        // Growth: the day and stage, a projection card for the next skill, three pastel metric cards (the care rhythm,
        // Bond and towers), the last moment, and tiles for a story, blocks, the report and the journal.
        private sealed class DevelopmentPanel
        {
            public const float Height = 446;

            public GameObject Root { get; }
            public Text Feedback { get; }
            public Button ReadStory { get; }
            public Button StackBlocks { get; }
            public Button OpenReport { get; }
            public Button OpenJournal { get; }

            private readonly Text age;
            private readonly Text bond;
            private readonly Text rhythm;
            private readonly Text blockTowers;
            private readonly Text blockAvailability;
            private readonly Text stackTitle;
            private readonly Text projection;
            private readonly Text projectionHint;
            private readonly (Image Pill, Text Label, Image Mark) pace;

            public DevelopmentPanel(RectTransform content, Action close, Action readStory, Action openBlocks,
                Action openReport, Action openJournal)
            {
                Root = content.parent.gameObject;
                var heading = SheetHeading("Development Heading", content, "Growth", Height);
                heading.rectTransform.offsetMin = new Vector2(0, Height - 66);
                SheetClose("Done", "Done", content, Height, close);

                age = Label("Development Age", content, "", 22, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, Height - 96), new Vector2(0.72f, 0), new Vector2(0, Height - 64));
                FitText(age, 16, 22);

                // The next skill and when it is likely, with a gentle hint when the baby is taking their time.
                var card = Box("Growth Projection", content, Vector2.zero, Vector2.right,
                    new Vector2(0, Height - 184), new Vector2(0, Height - 104));
                SoftShapes.Radius(Surface(card, Mint), Px(20));
                IconAt("Growth Projection Icon", card, "Growth", FernDeep, new Vector2(0, 0.5f), new Vector2(38, 0), Px(18));
                projection = Label("Growth Projection Text", card, "", 24, Ink, TextAnchor.MiddleLeft,
                    new Vector2(70, 38), Vector2.one, new Vector2(-170, -6), FontStyle.Bold);
                FitText(projection, 16, 24);
                projectionHint = Label("Growth Projection Hint", card, "", 19, Muted, TextAnchor.MiddleLeft,
                    new Vector2(70, 6), Vector2.right, new Vector2(-16, 40));
                FitText(projectionHint, 14, 19);
                pace = Pill("Growth Pace", card, "", Color.white, FernDeep, Fern);
                rhythm = MetricCard("Care rhythm", "Rhythm Value", content, 0, 1f / 3, Lilac);
                bond = MetricCard("Bond", "Bond Value", content, 1f / 3, 2f / 3, Blush);
                blockTowers = MetricCard("Towers built", "Towers Value", content, 2f / 3, 1, Butter);

                Feedback = Label("Development Feedback", content, "", 23, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, 104), Vector2.right, new Vector2(0, 156));
                FitText(Feedback, 16, 23);
                ReadStory = Tile("Read a story", content, 0, 0.25f, "Story", Blush, readStory);
                StackBlocks = Tile("Stack blocks", content, 0.25f, 0.5f, "Blocks", Butter, openBlocks);
                OpenReport = Tile("Baby report", content, 0.5f, 0.75f, "Report", Sky, openReport);
                OpenJournal = Tile("Journal", content, 0.75f, 1, "Journal", Lilac, openJournal);
                stackTitle = StackBlocks.GetComponentInChildren<Text>();
                blockAvailability = Label("Blocks Availability", StackBlocks.transform, "Early walker stage",
                    16, Muted, TextAnchor.MiddleCenter, new Vector2(4, 4), Vector2.right, new Vector2(-4, 26), FontStyle.Bold);
                FitText(blockAvailability, 11, 16);
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyState state, string ageText, string feedback, GrowthReport growth, BondRecord bonding)
            {
                age.text = ageText;
                projection.text = ProjectionLine(state, growth);
                projectionHint.text = ProjectionHint(state, growth);
                var paceText = growth == null || growth.Next == MotorSkill.None || growth.Paused ? string.Empty
                    : PaceName(growth.Pace);
                pace.Label.text = paceText;
                pace.Pill.gameObject.SetActive(paceText.Length > 0);
                var pill = (RectTransform)pace.Pill.transform;
                var width = PillWidth(pace.Label, Px(24));
                Place(pill, new Vector2(1, 1), new Vector2(-14, -10), new Vector2(width, 32));
                projection.rectTransform.offsetMax = new Vector2(-(paceText.Length > 0 ? width + 24 : 16), -6);
                var days = bonding?.Rhythm ?? 0;
                rhythm.text = days == 1 ? "1 day" : $"{days} days";
                bond.text = $"{Mathf.RoundToInt((float)state.Bond)} / 100";
                blockTowers.text = state.BlockTowers.ToString();
                StackBlocks.interactable = state.CanWalk;
                stackTitle.rectTransform.offsetMin = new Vector2(stackTitle.rectTransform.offsetMin.x, state.CanWalk ? 8 : 26);
                blockAvailability.gameObject.SetActive(!state.CanWalk);
                Feedback.text = feedback;
            }

            // A compact pastel tile: an icon left of its label.
            private static Button Tile(string title, RectTransform content, float left, float right, string icon, Color tint,
                Action action)
            {
                var button = ActionButton(title, content, left, right, 6, 100, action, tint, Ink);
                var rect = (RectTransform)button.transform;
                rect.offsetMin = new Vector2(left == 0 ? 0 : 8, rect.offsetMin.y);
                rect.offsetMax = new Vector2(right == 1 ? 0 : -8, rect.offsetMax.y);
                SoftShapes.Radius(button.image, Px(20));
                IconAt("Icon", button.transform, icon, Ink, new Vector2(0.5f, 1), new Vector2(0, -24), Px(18));
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(6, 8);
                label.rectTransform.offsetMax = new Vector2(-6, -46);
                FitText(label, 15, 23);
                return button;
            }

            private static Text MetricCard(string label, string valueName, Transform parent, float left, float right, Color tint)
            {
                var card = Box(label, parent, new Vector2(left, 0), new Vector2(right, 0),
                    new Vector2(left == 0 ? 0 : 8, 166), new Vector2(right == 1 ? 0 : -8, Height - 192));
                SoftShapes.Radius(Surface(card, tint), Px(20));
                var name = Label("Metric Label", card, label, 20, Muted, TextAnchor.MiddleLeft,
                    new Vector2(16, 44), Vector2.one, new Vector2(-12, -6), FontStyle.Bold);
                FitText(name, 14, 20);
                var value = Label(valueName, card, "", 34, Ink, TextAnchor.MiddleLeft,
                    new Vector2(16, 4), Vector2.right, new Vector2(-12, 48), FontStyle.Bold);
                FitText(value, 22, 34);
                return value;
            }
        }
    }
}
