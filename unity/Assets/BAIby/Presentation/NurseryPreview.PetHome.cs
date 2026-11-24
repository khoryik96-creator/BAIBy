using System;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Home cards for pets: the pet's portrait on a peach disc, name, a paw badge naming the species, its stage and
    // day, and a mood pill.
    public sealed partial class NurseryPreview
    {
        private sealed partial class HomePanel
        {
            // Builds a pet's portrait for its card; set by the preview, which owns the pet art.
            public Func<PetState, Sprite> PetPortrait { get; set; }

            // A pet's card: the same shape as a baby's, with a paw badge naming the species.
            private Button PetCard(int slot, PetState state, RectTransform cell, bool isActive, bool isNew, bool canSayGoodbye,
                bool resting)
            {
                if (state == null)
                    return UnreadableCard(slot, cell);

                var details = BuildCard(slot, state.Name, cell, isActive, isNew, canSayGoodbye, resting, Peach, true,
                    out var card);
                var glyph = Box(card.name + " Species", card.transform, Vector2.one, Vector2.one,
                    new Vector2(-16 - Px(30), -16 - Px(30)), new Vector2(-16, -16));
                SoftShapes.Disc(Surface(glyph, resting ? Mist : Butter));
                IconAt(card.name + " Species paw", glyph, "Paw", resting ? Secondary : Ink, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Px(16));
                DescribePet(details, state);
                companionCards.Add(details);
                return card;
            }

            // The parts of a pet's card that change with time.
            private void DescribePet(CardDetails card, PetState state)
            {
                var sprite = PetPortrait?.Invoke(state);
                if (card.Picture.sprite != sprite)
                    card.Picture.sprite = sprite;
                card.Picture.enabled = sprite != null;
                card.Stage.text = StageDay(PetStageTitle(state.Species, state.Stage), state.Age);
                if (card.Resting)
                {
                    ShowMood(card, "Resting quietly", Mist, Muted, Divider);
                    return;
                }

                var mood = PetMoodLine(state, out var needsCare);
                if (needsCare)
                    ShowMood(card, mood, Butter, ButterInk, Hex(0xC99520));
                else
                    ShowMood(card, mood, FernSoft, FernDeep, Fern);
            }
        }
    }
}
