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

        // Growth: the day and stage, three pastel metric cards, the last moment, and tiles for a story, blocks and
        // the report.
        private sealed class DevelopmentPanel
        {
            private const float Height = 360;

            public GameObject Root { get; }
            public Text Feedback { get; }
            public Button ReadStory { get; }
            public Button StackBlocks { get; }
            public Button OpenReport { get; }

            private readonly Text age;
            private readonly Text connection;
            private readonly Text storySessions;
            private readonly Text blockTowers;
            private readonly Text blockAvailability;
            private readonly Text stackTitle;

            public DevelopmentPanel(RectTransform content, Action close, Action readStory, Action openBlocks,
                Action openReport)
            {
                Root = content.parent.gameObject;
                var heading = SheetHeading("Development Heading", content, "Growth", Height);
                heading.rectTransform.offsetMin = new Vector2(0, Height - 66);
                SheetClose("Done", "Done", content, Height, close);

                age = Label("Development Age", content, "", 22, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, Height - 96), new Vector2(0.72f, 0), new Vector2(0, Height - 64));
                FitText(age, 16, 22);
                storySessions = MetricCard("Story sessions", "Story Sessions Value", content, 0, 1f / 3, Lilac);
                connection = MetricCard("Connection", "Connection Value", content, 1f / 3, 2f / 3, Blush);
                blockTowers = MetricCard("Towers built", "Towers Value", content, 2f / 3, 1, Butter);

                Feedback = Label("Development Feedback", content, "", 23, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, 104), Vector2.right, new Vector2(0, 156));
                FitText(Feedback, 16, 23);
                ReadStory = Tile("Read a story", content, 0, 1f / 3, "Story", Blush, readStory);
                StackBlocks = Tile("Stack blocks", content, 1f / 3, 2f / 3, "Blocks", Butter, openBlocks);
                OpenReport = Tile("Baby report", content, 2f / 3, 1, "Report", Sky, openReport);
                stackTitle = StackBlocks.GetComponentInChildren<Text>();
                blockAvailability = Label("Blocks Availability", StackBlocks.transform, "Early walker stage",
                    16, Muted, TextAnchor.MiddleCenter, new Vector2(4, 4), Vector2.right, new Vector2(-4, 26), FontStyle.Bold);
                FitText(blockAvailability, 11, 16);
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyState state, string ageText, string feedback)
            {
                age.text = ageText;
                storySessions.text = state.StorySessions.ToString();
                connection.text = $"{Mathf.RoundToInt((float)state.Connection)} / 100";
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
                    new Vector2(left == 0 ? 0 : 8, 166), new Vector2(right == 1 ? 0 : -8, Height - 106));
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
