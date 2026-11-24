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
                SheetHeading("Decor Heading", content, "Nursery decor", 360);
                SheetClose("Close decor", "Done", content, 360, close);
                sage = new ThemeChoice("Sage", new Color32(165, 181, 159, 255), content, 0, 0.5f,
                    () => choose(NurseryTheme.Sage));
                peach = new ThemeChoice("Peach", new Color32(236, 188, 162, 255), content, 0.5f, 1,
                    () => choose(NurseryTheme.Peach));
                Feedback = Label("Decor Feedback", content, "", 23, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, 4), Vector2.right, new Vector2(0, 70));
                FitText(Feedback, 17, 23);
                Root.SetActive(false);
            }

            public Button ChoiceFor(NurseryTheme theme) => theme == NurseryTheme.Peach ? peach.Button : sage.Button;

            public void UpdateSelection(NurseryTheme theme)
            {
                sage.SetSelected(theme == NurseryTheme.Sage);
                peach.SetSelected(theme == NurseryTheme.Peach);
                Feedback.text = string.Empty;
            }

            // A room theme card: a rounded colour preview, its name, and "Selected" with a sage outline when chosen.
            private sealed class ThemeChoice
            {
                private const float Height = 196;

                private readonly Text selected;
                private readonly Image card;
                public Button Button { get; }

                public ThemeChoice(string name, Color swatch, Transform parent, float left, float right, Action choose)
                {
                    Button = ActionButton(name, parent, left, right, 76, 76 + Height, choose, Mist, Ink);
                    card = Button.image;
                    var rect = (RectTransform)Button.transform;
                    rect.offsetMin = new Vector2(left == 0 ? 0 : 9, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(right == 1 ? 0 : -9, rect.offsetMax.y);
                    SoftShapes.Radius(card, Px(20));
                    var title = Button.GetComponentInChildren<Text>();
                    title.alignment = TextAnchor.MiddleLeft;
                    title.rectTransform.offsetMin = new Vector2(18, 8);
                    title.rectTransform.offsetMax = new Vector2(-18, -(Height - 52));
                    var swatchArea = Box(name + " Color", Button.transform, Vector2.zero, Vector2.one,
                        new Vector2(12, 58), new Vector2(-12, -12));
                    SoftShapes.Radius(Surface(swatchArea, swatch), Px(14));
                    selected = Label(name + " Selection", Button.transform, "Selected", 20, FernDeep, TextAnchor.MiddleRight,
                        new Vector2(18, 8), Vector2.right, new Vector2(-18, 52), FontStyle.Bold);
                    var ring = Surface(Box(name + " Selected Ring", selected.transform, Vector2.zero, Vector2.right,
                        new Vector2(-18, -8), new Vector2(18, Height - 8)), Fern);
                    SoftShapes.Outline(ring, Px(20));
                }

                public void SetSelected(bool isSelected)
                {
                    selected.gameObject.SetActive(isSelected);
                    card.color = isSelected ? FernSoft : Mist;
                }
            }
        }
    }
}
