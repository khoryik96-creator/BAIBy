using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private Button decorButton;
        private DecorPanel decor;

        private void ChooseNurseryTheme(NurseryTheme theme)
        {
            try
            {
                session.SetNurseryTheme(theme);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The decor couldn't be saved. Please try again.");
                return;
            }

            ApplyNurseryTheme(session.State.NurseryTheme);
        }

        private void ApplyNurseryTheme(NurseryTheme theme)
        {
            nursery.SetTheme(theme);
            decor.UpdateSelection(theme);
        }

        private sealed class DecorPanel
        {
            private readonly ThemeChoice sage;
            private readonly ThemeChoice peach;

            public GameObject Root { get; }
            public Text Feedback { get; }

            public DecorPanel(RectTransform content, Action close, Action<NurseryTheme> choose)
            {
                Root = content.parent.gameObject;
                Label("Decor Heading", content, "Nursery decor", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 265), new Vector2(0.72f, 0), new Vector2(0, 351), FontStyle.Bold);
                var done = ActionButton("Done", content, 0.73f, 1, 265, 353, close, Mist, Ink);
                done.name = "Close decor";
                sage = new ThemeChoice("Sage", new Color32(165, 181, 159, 255), content, 0, 0.5f,
                    () => choose(NurseryTheme.Sage));
                peach = new ThemeChoice("Peach", new Color32(236, 188, 162, 255), content, 0.5f, 1,
                    () => choose(NurseryTheme.Peach));
                Feedback = Label("Decor Feedback", content, "", 23, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 9), Vector2.right, new Vector2(0, 80));
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 18;
                Feedback.resizeTextMaxSize = 23;
                Root.SetActive(false);
            }

            public Button ChoiceFor(NurseryTheme theme) => theme == NurseryTheme.Peach ? peach.Button : sage.Button;

            public void UpdateSelection(NurseryTheme theme)
            {
                sage.SetSelected(theme == NurseryTheme.Sage);
                peach.SetSelected(theme == NurseryTheme.Peach);
                Feedback.text = string.Empty;
            }

            private sealed class ThemeChoice
            {
                private readonly Text selected;
                public Button Button { get; }

                public ThemeChoice(string name, Color swatch, Transform parent, float left, float right, Action choose)
                {
                    Button = ActionButton(name, parent, left, right, 81, 253, choose, Mist, Ink);
                    var title = Button.GetComponentInChildren<Text>();
                    title.rectTransform.offsetMin = new Vector2(0, 31);
                    title.rectTransform.offsetMax = new Vector2(0, -102);
                    var swatchArea = Box(name + " Color", Button.transform, Vector2.zero, Vector2.one,
                        new Vector2(10, 74), new Vector2(-10, -10));
                    Surface(swatchArea, swatch);
                    selected = Label(name + " Selection", Button.transform, "Selected", 20, Fern, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.right, new Vector2(0, 31));
                }

                public void SetSelected(bool isSelected)
                {
                    selected.gameObject.SetActive(isSelected);
                }
            }
        }
    }
}
