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

        // Three pastel food cards with the food's own picture, what it does, and when it becomes available.
        private sealed class FeedingPanel
        {
            private const float Height = 360;

            public GameObject Root { get; }
            public Button Milk => milk.Button;
            public Text Feedback { get; }

            private readonly FoodCard milk;
            private readonly FoodCard banana;
            private readonly FoodCard oats;

            public FeedingPanel(RectTransform content, Action close, Action<Food> eat)
            {
                Root = content.parent.gameObject;
                SheetHeading("Feeding Heading", content, "Feeding", Height);
                SheetClose("Close feeding", "Done", content, Height, close);

                milk = new FoodCard(Food.Milk, "Fullness +25\nEnergy +5", "Milk", Peach, content, 0, 1f / 3, eat);
                banana = new FoodCard(Food.Banana, "Fullness +20\nComfort +8", "Banana", Butter, content, 1f / 3, 2f / 3, eat);
                oats = new FoodCard(Food.Oats, "Fullness +34", "Oats", Mint, content, 2f / 3, 1, eat);

                Feedback = Label("Feeding Feedback", content, "", 24, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, 4), Vector2.right, new Vector2(0, 70));
                FitText(Feedback, 17, 24);
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
                private readonly GameObject lockChip;

                public FoodCard(Food food, string effects, string art, Color tint, Transform parent, float left, float right,
                    Action<Food> eat)
                {
                    this.food = food;
                    var button = ActionButton(FoodName(food), parent, left, right, 80, Height - 92, () => eat(food), tint, Ink);
                    Button = button;
                    var rect = (RectTransform)button.transform;
                    rect.offsetMin = new Vector2(left == 0 ? 0 : 8, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(right == 1 ? 0 : -8, rect.offsetMax.y);
                    SoftShapes.Radius(button.image, Px(20));
                    var picture = Box(FoodName(food) + " Picture", button.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-28, -64), new Vector2(28, -8));
                    var image = Surface(picture, Color.white);
                    image.sprite = Resources.Load<Sprite>("BAIby/Baby/" + art);
                    image.preserveAspect = true;
                    image.enabled = image.sprite != null;
                    var title = button.GetComponentInChildren<Text>();
                    title.rectTransform.anchorMin = new Vector2(0, 1);
                    title.rectTransform.offsetMin = new Vector2(6, -100);
                    title.rectTransform.offsetMax = new Vector2(-6, -66);
                    FitText(title, 18, 25);
                    var details = Label("Effects", button.transform, effects, 20, Muted, TextAnchor.UpperCenter,
                        new Vector2(6, 34), Vector2.one, new Vector2(-6, -100));
                    FitText(details, 14, 20);
                    var chip = Box(FoodName(food) + " Lock", button.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                        new Vector2(-88, 6), new Vector2(88, 32));
                    SoftShapes.Pill(Surface(chip, new Color(1, 1, 1, 0.8f)));
                    lockChip = chip.gameObject;
                    lockNotice = Label(FoodName(food) + " Availability", chip, "", 16, Muted,
                        TextAnchor.MiddleCenter, new Vector2(6, 0), Vector2.one, new Vector2(-6, 0), FontStyle.Bold);
                    FitText(lockNotice, 11, 16);
                }

                public void UpdateAvailability(BabyState state)
                {
                    var available = BabySimulation.CanFeed(state, food);
                    Button.interactable = available;
                    lockNotice.text = available ? string.Empty : "From crawler stage";
                    lockChip.SetActive(!available);
                }
            }
        }
    }
}
