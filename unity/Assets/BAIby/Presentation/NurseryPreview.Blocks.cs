using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private StackBlocksPanel blocks;

        private void OpenBlocks()
        {
            if (!session.State.CanWalk)
                return;

            // An upset or sick walker turns the blocks down at once, before the child builds anything.
            if (CareConsequences.RefusesPlay(session.State.Care) || CareConsequences.FeelsTooPoorly(session.State.Care))
            {
                Care(CareAction.StackBlocks);
                return;
            }

            blocks.ResetTower();
            ShowPanel(NurseryPanel.Blocks);
        }

        private void CompleteBlocks()
        {
            if (activePanel == NurseryPanel.Blocks)
                Care(CareAction.StackBlocks);
        }

        private sealed class StackBlocksPanel
        {
            private static readonly string[] Sizes = { "Large", "Medium", "Small" };
            private static readonly float[] Widths = { 208, 145, 84 };
            private static readonly Color[] BlockColors =
            {
                new Color32(78, 122, 99, 255),
                new Color32(176, 86, 66, 255),
                new Color32(139, 104, 52, 255)
            };

            private readonly RectTransform[] layers = new RectTransform[Sizes.Length];
            private readonly Button[] choices = new Button[Sizes.Length];
            private readonly Button finish;
            private int nextLayer;

            public GameObject Root { get; }
            public Text Feedback { get; }
            public Button FirstBlock => choices[0];

            public StackBlocksPanel(RectTransform content, Action close, Action complete)
            {
                Root = content.parent.gameObject;
                Label("Blocks Heading", content, "Stack blocks", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 327), Vector2.right, new Vector2(0, 371), FontStyle.Bold);
                Feedback = Label("Blocks Feedback", content, "", 23, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, 291), Vector2.right, new Vector2(0, 326));
                FitText(Feedback, 16, 23);

                var mat = Box("Tower mat", content, Vector2.zero, Vector2.right,
                    new Vector2(3, 192), new Vector2(-3, 289));
                SoftShapes.Radius(Surface(mat, Mist), Px(20));
                var bottomAnchor = new Vector2(0.5f, 0);
                var platform = Box("Tower platform", mat, bottomAnchor, bottomAnchor,
                    new Vector2(-143, 9), new Vector2(143, 17));
                SoftShapes.Pill(Surface(platform, new Color32(130, 102, 76, 255)));
                for (var index = 0; index < Sizes.Length; index++)
                {
                    var block = index;
                    var width = Widths[block];
                    var bottom = 17 + block * 23;
                    layers[block] = Box($"{Sizes[block]} tower layer", mat, bottomAnchor, bottomAnchor,
                        new Vector2(-width / 2, bottom), new Vector2(width / 2, bottom + 22));
                    SoftShapes.Radius(Surface(layers[block], BlockColors[block]), 6);

                    choices[block] = ActionButton($"{Sizes[block]} block", content, block / (float)Sizes.Length,
                        (block + 1) / (float)Sizes.Length, 99, 187, () => PlaceBlock(block), BlockColors[block], Color.white);
                    choices[block].GetComponentInChildren<Text>().text = Sizes[block];
                    SoftShapes.Radius(choices[block].image, Px(20));
                    var choiceRect = (RectTransform)choices[block].transform;
                    choiceRect.offsetMin = new Vector2(block == 0 ? 0 : 8, choiceRect.offsetMin.y);
                    choiceRect.offsetMax = new Vector2(block == Sizes.Length - 1 ? 0 : -8, choiceRect.offsetMax.y);
                }

                var cancel = ActionButton("Cancel", content, 0, 0.5f, 7, 95, close, Mist, Ink);
                cancel.name = "Cancel blocks";
                finish = ActionButton("Done", content, 0.5f, 1, 7, 95, () =>
                {
                    if (nextLayer == Sizes.Length)
                        complete();
                }, Fern, Color.white);
                finish.name = "Finish blocks";
                ResetTower();
                Root.SetActive(false);
            }

            public void ResetTower()
            {
                nextLayer = 0;
                for (var index = 0; index < layers.Length; index++)
                {
                    layers[index].gameObject.SetActive(false);
                    choices[index].interactable = true;
                }

                finish.interactable = false;
                Feedback.text = "Start with the largest block.";
            }

            private void PlaceBlock(int index)
            {
                if (nextLayer == Sizes.Length)
                    return;
                if (index != nextLayer)
                {
                    Feedback.text = $"Try the {Sizes[nextLayer].ToLowerInvariant()} block next.";
                    return;
                }

                layers[index].gameObject.SetActive(true);
                choices[index].interactable = false;
                nextLayer++;
                finish.interactable = nextLayer == Sizes.Length;
                if (finish.interactable)
                {
                    Feedback.text = "Your tower is ready. Tap Done to finish.";
                    finish.Select();
                }
                else
                {
                    Feedback.text = $"Now add the {Sizes[nextLayer].ToLowerInvariant()} block.";
                    choices[nextLayer].Select();
                }
            }
        }
    }
}
