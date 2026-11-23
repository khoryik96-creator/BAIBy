using System;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private HygienePanel hygiene;

        private sealed class HygienePanel
        {
            public GameObject Root { get; }
            public Button ChangeDiaper { get; }
            public Text Feedback { get; }

            public HygienePanel(RectTransform content, Action close, Action changeDiaper, Action bath)
            {
                Root = content.parent.gameObject;
                Label("Hygiene Heading", content, "Wash up", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 265), new Vector2(0.72f, 0), new Vector2(0, 351), FontStyle.Bold);
                var done = ActionButton("Done", content, 0.73f, 1, 265, 353, close, Mist, Ink);
                done.name = "Close hygiene";

                ChangeDiaper = WashChoice(content, "Change diaper",
                    "Restores Cleanliness  ·  Wipe or quick change", 169, changeDiaper);
                WashChoice(content, "Bath time", "Cleanliness +45  ·  Comfort +30  ·  Uses 8 Energy", 74, bath);
                Feedback = Label("Hygiene Feedback", content, "", 23, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 8), Vector2.right, new Vector2(0, 70));
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 18;
                Feedback.resizeTextMaxSize = 23;
                Root.SetActive(false);
            }

            private static Button WashChoice(RectTransform content, string title, string effects, float bottom, Action action)
            {
                var button = ActionButton(title, content, 0, 1, bottom, bottom + 88, action, Fern, Color.white);
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(12, 34);
                label.rectTransform.offsetMax = new Vector2(-12, -4);
                label.alignment = TextAnchor.MiddleLeft;
                var details = Label(title + " Effects", button.transform, effects, 20, Color.white, TextAnchor.MiddleLeft,
                    new Vector2(12, 6), Vector2.right, new Vector2(-12, 36));
                details.resizeTextForBestFit = true;
                details.resizeTextMinSize = 16;
                details.resizeTextMaxSize = 20;
                return button;
            }
        }
    }
}
