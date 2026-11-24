using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private FeedingPanel feeding;
        private Button feedButton;

        private static string FoodName(Food food)
        {
            switch (food)
            {
                case Food.Banana: return "Mashed banana";
                case Food.Oats: return "Soft oats";
                default: return "Milk";
            }
        }

        private sealed class FeedingPanel
        {
            public GameObject Root { get; }
            public Button Milk => milk.Button;
            public Text Feedback { get; }

            private readonly FoodCard milk;
            private readonly FoodCard banana;
            private readonly FoodCard oats;

            public FeedingPanel(RectTransform content, Action close, Action<Food> eat)
            {
                Root = content.parent.gameObject;
                Label("Feeding Heading", content, "Feeding", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 265), new Vector2(0.72f, 0), new Vector2(0, 351), FontStyle.Bold);
                var done = ActionButton("Done", content, 0.73f, 1, 265, 353, close, Mist, Ink);
                done.name = "Close feeding";

                milk = new FoodCard(Food.Milk, "Fullness +25\nEnergy +5", content, 0, 1f / 3, eat);
                banana = new FoodCard(Food.Banana, "Fullness +20\nComfort +8", content, 1f / 3, 2f / 3, eat);
                oats = new FoodCard(Food.Oats, "Fullness +34", content, 2f / 3, 1, eat);

                Feedback = Label("Feeding Feedback", content, "", 25, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 9), Vector2.right, new Vector2(0, 78));
                Feedback.resizeTextForBestFit = true;
                Feedback.resizeTextMinSize = 18;
                Feedback.resizeTextMaxSize = 25;
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyState state, string feedback)
            {
                milk.UpdateAvailability(state);
                banana.UpdateAvailability(state);
                oats.UpdateAvailability(state);
                Feedback.text = feedback;
            }

            private sealed class FoodCard
            {
                public Button Button { get; }

                private readonly Food food;
                private readonly Text lockNotice;

                public FoodCard(Food food, string effects, Transform parent, float left, float right, Action<Food> eat)
                {
                    this.food = food;
                    var button = ActionButton(FoodName(food), parent, left, right, 84, 254, () => eat(food), Mist, Ink);
                    Button = button;
                    var title = button.GetComponentInChildren<Text>();
                    var color = button.colors;
                    color.disabledColor = new Color32(220, 220, 220, 255);
                    button.colors = color;
                    title.rectTransform.offsetMin = new Vector2(3, 109);
                    title.rectTransform.offsetMax = new Vector2(-3, -7);
                    title.resizeTextForBestFit = true;
                    title.resizeTextMinSize = 20;
                    title.resizeTextMaxSize = 25;
                    Label("Effects", button.transform, effects, 21, Ink, TextAnchor.MiddleCenter,
                        new Vector2(5, 42), Vector2.right, new Vector2(-5, 102));
                    lockNotice = Label(FoodName(food) + " Availability", button.transform, "", 18, Ink,
                        TextAnchor.MiddleCenter, new Vector2(4, 5), Vector2.right, new Vector2(-4, 35));
                }

                public void UpdateAvailability(BabyState state)
                {
                    var available = BabySimulation.CanFeed(state, food);
                    Button.interactable = available;
                    lockNotice.text = available ? string.Empty : "From crawler stage";
                }
            }
        }
    }
}
