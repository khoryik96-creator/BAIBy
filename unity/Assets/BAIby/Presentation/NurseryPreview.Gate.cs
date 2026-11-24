using System;
using BAIby.Application;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private GatePanel gate;
        private Action afterGate;
        private readonly System.Random gateRandom = new System.Random();
        // One gate for the whole session, so each opening draws a number different from the last one shown.
        private ParentalGate parentalGate;

        // Every purchase, restore and external link opens behind this gate, as Apple's Kids category asks.
        // Each opening draws a new number; cancelling returns to Home.
        private void RequestGate(Action passed)
        {
            if (household == null || passed == null)
                return;

            afterGate = passed;
            view = HouseholdView.Gate;
            ApplyHouseholdView();
            if (parentalGate == null)
                parentalGate = new ParentalGate(gateRandom.Next);
            else
                parentalGate.Next();
            gate.Open(parentalGate);
        }

        private void SubmitGate()
        {
            if (!gate.Check())
                return;

            var passed = afterGate;
            afterGate = null;
            passed?.Invoke();
        }

        private void CancelGate()
        {
            afterGate = null;
            ShowHome();
        }

        private sealed class GatePanel
        {
            private readonly Text prompt;
            private readonly InputField answer;
            private ParentalGate challenge;

            public GameObject Root { get; }
            public Text Feedback { get; }

            public GatePanel(RectTransform content, Action cancel, Action submit)
            {
                Root = content.parent.gameObject;
                Place(Label("Gate Heading", content, "For grown-ups", 38, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold).rectTransform, 14, 66);
                Place(Label("Gate Instructions", content, "Type this number using digits.", 23, Secondary,
                    TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero).rectTransform, 70, 108);
                prompt = Label("Gate Prompt", content, "", 34, Fern, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                prompt.resizeTextForBestFit = true;
                prompt.resizeTextMinSize = 22;
                prompt.resizeTextMaxSize = 34;
                Place(prompt.rectTransform, 112, 170);

                var field = Box("Gate Answer", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Place(field, 180, 244);
                answer = field.gameObject.AddComponent<InputField>();
                answer.targetGraphic = TextField(field);
                answer.characterLimit = 4;
                answer.contentType = InputField.ContentType.IntegerNumber;
                answer.lineType = InputField.LineType.SingleLine;
                answer.textComponent = Label("Gate Entered", field, "", 30, Ink, TextAnchor.MiddleLeft,
                    new Vector2(26, 0), Vector2.one, new Vector2(-26, 0), FontStyle.Bold);
                answer.placeholder = Label("Gate Placeholder", field, "Number", 30, Secondary,
                    TextAnchor.MiddleLeft, new Vector2(26, 0), Vector2.one, new Vector2(-26, 0));

                var back = ActionButton("Gate cancel", content, 0, 0.5f, 0, 0, cancel, Mist, Ink);
                back.GetComponentInChildren<Text>().text = "Back";
                PlaceFromTop((RectTransform)back.transform, 0, 0.5f, 256, 340, 3, 3);
                var proceed = ActionButton("Gate continue", content, 0.5f, 1, 0, 0, submit, Fern, Color.white);
                proceed.GetComponentInChildren<Text>().text = "Continue";
                PlaceFromTop((RectTransform)proceed.transform, 0.5f, 1, 256, 340, 3, 3);
                foreach (var button in new[] { back, proceed })
                {
                    SoftShapes.Round(button.image, 1f);
                    SoftShapes.Lift(button.gameObject);
                }

                Feedback = Label("Gate Feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    Vector2.zero, Vector2.one, Vector2.zero);
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 15;
                Feedback.resizeTextMaxSize = 21;
                Place(Feedback.rectTransform, 350, 404);
                Root.SetActive(false);
            }

            public void Open(ParentalGate next)
            {
                challenge = next ?? throw new ArgumentNullException(nameof(next));
                prompt.text = challenge.Prompt;
                answer.text = string.Empty;
                Feedback.text = string.Empty;
                answer.Select();
            }

            // A wrong answer draws a different number and gives no hint about the old one.
            public bool Check()
            {
                if (challenge != null && challenge.Check(answer.text))
                    return true;

                challenge?.Next();
                prompt.text = challenge?.Prompt ?? string.Empty;
                answer.text = string.Empty;
                Feedback.text = "That isn't it. Here's a new number to try.";
                return false;
            }

            private static void Place(RectTransform rect, float top, float bottom) =>
                PlaceFromTop(rect, 0, 1, top, bottom);
        }
    }
}
