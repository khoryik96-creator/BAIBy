using System;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private HygienePanel hygiene;

        // Wash up: a diaper change or a bath, each a pastel row with its icon, what it does and what it costs.
        private sealed class HygienePanel
        {
            private const float Height = 360;

            public GameObject Root { get; }
            public Button ChangeDiaper { get; }
            public Text Feedback { get; }

            public HygienePanel(RectTransform content, Action close, Action changeDiaper, Action bath)
            {
                Root = content.parent.gameObject;
                SheetHeading("Hygiene Heading", content, "Wash up", Height);
                SheetClose("Close hygiene", "Done", content, Height, close);

                ChangeDiaper = ChoiceRow(content, "Change diaper", "Restores Cleanliness  ·  Wipe or quick change",
                    "Diaper", Mint, 174, changeDiaper);
                ChoiceRow(content, "Bath time", "Cleanliness +45  ·  Comfort +30  ·  Uses 8 Energy", "Bath", Sky, 78, bath);
                Feedback = Label("Hygiene Feedback", content, "", 24, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, 4), Vector2.right, new Vector2(0, 72));
                FitText(Feedback, 17, 24);
                Root.SetActive(false);
            }
        }

        // A full-width pastel choice: an icon badge, then the title and a quiet line of effects. Named for its title;
        // the effects line is "<title> Effects".
        private static Button ChoiceRow(RectTransform content, string title, string effects, string icon, Color tint,
            float bottom, Action action)
        {
            var button = ActionButton(title, content, 0, 1, bottom, bottom + 88, action, tint, Ink);
            var rect = (RectTransform)button.transform;
            rect.offsetMin = new Vector2(0, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0, rect.offsetMax.y);
            SoftShapes.Radius(button.image, Px(20));
            var badge = Box(title + " Badge", button.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(16, -30), new Vector2(76, 30));
            SoftShapes.Radius(Surface(badge, new Color(1, 1, 1, 0.75f)), Px(12));
            IconAt("Icon", badge, icon, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, Px(20));
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.offsetMin = new Vector2(94, 40);
            label.rectTransform.offsetMax = new Vector2(-16, -6);
            label.alignment = TextAnchor.MiddleLeft;
            FitText(label, 20, 26);
            var details = Label(title + " Effects", button.transform, effects, 20, Muted, TextAnchor.MiddleLeft,
                new Vector2(94, 8), Vector2.right, new Vector2(-16, 40));
            FitText(details, 14, 20);
            return button;
        }
    }
}
