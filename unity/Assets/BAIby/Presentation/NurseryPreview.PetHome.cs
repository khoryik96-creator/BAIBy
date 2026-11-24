using System;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Home cards for pets: the pet's portrait, name, a paw species glyph, its stage and a mood line.
    public sealed partial class NurseryPreview
    {
        private sealed partial class HomePanel
        {
            // Builds a pet's portrait for its card; set by the preview, which owns the pet art.
            public Func<PetState, Sprite> PetPortrait { get; set; }

            // A pet's card: the same shape as a baby's, with a paw glyph naming the species.
            private Button PetCard(int slot, PetState state, RectTransform cell, bool isActive, bool isNew, bool canSayGoodbye,
                bool resting)
            {
                if (state == null)
                    return UnreadableCard(slot, cell);

                Action tap = resting ? (Action)(() => journal(slot)) : () => open(slot);
                var card = ActionButton("Home card " + state.Name, cell, 0, 1, 0, 0, tap, resting ? Paper : Mist, Ink);
                Fill((RectTransform)card.transform);
                SoftShapes.Round(card.image, 0.7f);
                SoftShapes.Lift(card.gameObject);

                var frame = Box(card.name + " Portrait", card.transform, Vector2.zero, Vector2.one,
                    new Vector2(16, 178), new Vector2(-16, -14));
                var portrait = Surface(frame, resting ? new Color(1, 1, 1, 0.55f) : Color.white);
                portrait.preserveAspect = true;

                var glyph = Box(card.name + " Species", card.transform, Vector2.one, Vector2.one,
                    new Vector2(-54, -54), new Vector2(-14, -14));
                PawGlyph(glyph, card.name + " Species paw", resting ? Secondary : Fern);

                var name = card.GetComponentInChildren<Text>();
                name.text = state.Name;
                name.rectTransform.anchorMax = Vector2.right;
                name.rectTransform.offsetMin = new Vector2(10, 132);
                name.rectTransform.offsetMax = new Vector2(-10, 174);
                name.fontSize = 28;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = 28;
                var stageLabel = Label(card.name + " Stage", card.transform, "", 21, Secondary, TextAnchor.MiddleCenter,
                    new Vector2(10, 100), Vector2.right, new Vector2(-10, 130));

                var dot = Box(card.name + " Mood Dot", card.transform, Vector2.zero, Vector2.zero,
                    new Vector2(18, 76), new Vector2(32, 90));
                var moodDot = Surface(dot, Fern);
                SoftShapes.Disc(moodDot);
                var moodLabel = Label(card.name + " Mood", card.transform, "", 20, Ink, TextAnchor.MiddleLeft,
                    new Vector2(40, 66), Vector2.right, new Vector2(-10, 100));
                moodLabel.resizeTextForBestFit = true;
                moodLabel.resizeTextMinSize = 14;
                moodLabel.resizeTextMaxSize = 20;

                if (isActive)
                {
                    var selection = Box(card.name + " Selection", card.transform, Vector2.zero, Vector2.one,
                        Vector2.zero, Vector2.zero);
                    SoftShapes.Outline(Surface(selection, Fern));
                }

                var badge = Box(card.name + " New", card.transform, Vector2.up, Vector2.up,
                    new Vector2(12, -50), new Vector2(92, -14));
                SoftShapes.Round(Surface(badge, Fern), 1f);
                Label("New Label", badge, "New!", 19, Color.white, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                badge.gameObject.SetActive(isNew);

                if (resting)
                {
                    var restBadge = Box(card.name + " Resting", card.transform, Vector2.up, Vector2.up,
                        new Vector2(12, -50), new Vector2(122, -14));
                    SoftShapes.Round(Surface(restBadge, Mist), 1f);
                    Label("Resting Label", restBadge, "Resting", 19, Secondary, TextAnchor.MiddleCenter,
                        Vector2.zero, Vector2.one, Vector2.zero, FontStyle.Bold);
                }

                var details = new CardDetails(slot, portrait, stageLabel, moodDot, moodLabel, badge.gameObject, resting);
                DescribePet(details, state);
                companionCards.Add(details);

                if (resting)
                {
                    var wakeButton = ActionButton("Wake " + state.Name, card.transform, 0.12f, 0.88f, 14, 60,
                        () => wake(slot), Mist, Fern);
                    var wakeLabel = wakeButton.GetComponentInChildren<Text>();
                    wakeLabel.text = "Wake";
                    wakeLabel.fontSize = 19;
                    SoftShapes.Round(wakeButton.image, 1f);
                }
                else if (canSayGoodbye)
                {
                    var bye = ActionButton("Say goodbye to " + state.Name, card.transform, 0.12f, 0.88f, 14, 60,
                        () => farewell(slot), Paper, Secondary);
                    var byeLabel = bye.GetComponentInChildren<Text>();
                    byeLabel.text = "Say goodbye";
                    byeLabel.fontSize = 19;
                    byeLabel.fontStyle = FontStyle.Normal;
                    SoftShapes.Round(bye.image, 1f);
                }

                return card;
            }

            // The parts of a pet's card that change with time.
            private void DescribePet(CardDetails card, PetState state)
            {
                var sprite = PetPortrait?.Invoke(state);
                if (card.Picture.sprite != sprite)
                    card.Picture.sprite = sprite;
                card.Picture.enabled = sprite != null;
                card.Stage.text = PetStageTitle(state.Species, state.Stage);
                if (card.Resting)
                {
                    card.Mood.text = "Resting quietly";
                    card.MoodDot.color = Mist;
                    return;
                }

                card.Mood.text = PetMoodLine(state, out var needsCare);
                card.MoodDot.color = needsCare ? Amber : Fern;
            }
        }
    }
}
