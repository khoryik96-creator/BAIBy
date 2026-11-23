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

        private sealed class DevelopmentPanel
        {
            public GameObject Root { get; }
            public Text Feedback { get; }
            public Button ReadStory { get; }

            private readonly Text age;
            private readonly Text connection;
            private readonly Text storySessions;

            public DevelopmentPanel(RectTransform content, Action close, Action readStory)
            {
                Root = content.parent.gameObject;
                Label("Development Heading", content, "Development", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 265), new Vector2(0.72f, 0), new Vector2(0, 351), FontStyle.Bold);
                ActionButton("Done", content, 0.73f, 1, 265, 353, close, Mist, Ink);

                age = Label("Development Age", content, "", 22, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, 228), Vector2.right, new Vector2(0, 265));
                storySessions = MetricCard("Story sessions", "Story Sessions Value", content, 0, 0.5f);
                connection = MetricCard("Connection", "Connection Value", content, 0.5f, 1);

                Feedback = Label("Development Feedback", content, "", 25, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 102), Vector2.right, new Vector2(0, 147));
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 18;
                Feedback.resizeTextMaxSize = 25;
                ReadStory = ActionButton("Read a story", content, 0, 1, 9, 97, readStory, Fern, Color.white);
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyState state, string ageText, string feedback)
            {
                age.text = ageText;
                storySessions.text = state.StorySessions.ToString();
                connection.text = $"{Mathf.RoundToInt((float)state.Connection)} / 100";
                Feedback.text = feedback;
            }

            private static Text MetricCard(string label, string valueName, Transform parent, float left, float right)
            {
                var card = Box(label, parent, new Vector2(left, 0), new Vector2(right, 0),
                    new Vector2(3, 151), new Vector2(-3, 224));
                Surface(card, Mist);
                Label("Metric Label", card, label, 20, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(14, 41), Vector2.one, new Vector2(-14, -7));
                return Label(valueName, card, "", 29, Ink, TextAnchor.MiddleLeft,
                    new Vector2(14, 3), Vector2.right, new Vector2(-14, 42), FontStyle.Bold);
            }
        }
    }
}
