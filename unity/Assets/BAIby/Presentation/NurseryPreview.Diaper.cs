using System;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private DiaperPanel diaper;

        private sealed class DiaperPanel
        {
            private const float HitRadius = 33;
            private static readonly Vector2[] SpotCenters =
            {
                new Vector2(.24f, .42f),
                new Vector2(.50f, .68f),
                new Vector2(.75f, .40f)
            };

            private readonly RectTransform mat;
            private readonly Sprite spotArtwork;
            private readonly RectTransform[] spots = new RectTransform[SpotCenters.Length];
            private readonly Button finish;
            private readonly Func<bool> change;
            private int remaining;
            private bool submitting;
            private bool committed;

            internal GameObject Root { get; }
            internal Button StartControl { get; }
            internal Text Feedback { get; }

            internal DiaperPanel(RectTransform content, Action close, Func<bool> change)
            {
                Root = content.parent.gameObject;
                this.change = change;
                Label("Diaper title", content, "Diaper cleanup", 29, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 331), Vector2.right, new Vector2(0, 370), FontStyle.Bold);
                Label("Diaper instructions", content, "Tap or swipe across all three spots.", 23, Secondary,
                    TextAnchor.MiddleLeft, new Vector2(0, 301), Vector2.right, new Vector2(0, 332));
                mat = Box("Diaper wipe mat", content, Vector2.zero, Vector2.right,
                    new Vector2(0, 129), new Vector2(0, 298));
                var image = Surface(mat, Mist);
                var illustration = Resources.Load<Sprite>("BAIby/Baby/CleanupMat");
                if (illustration != null)
                {
                    image.sprite = illustration;
                    image.color = Color.white;
                }
                else
                {
                    Debug.LogError("Missing cleanup mat illustration in Resources/BAIby/Baby/CleanupMat.");
                }

                spotArtwork = Resources.Load<Sprite>("BAIby/Baby/CleanupSpot");
                if (spotArtwork == null)
                    Debug.LogError("Missing cleanup spot illustration in Resources/BAIby/Baby/CleanupSpot.");

                image.raycastTarget = true;
                mat.gameObject.AddComponent<DiaperWipeInput>().Bind(mat, Wipe);
                for (int i = 0; i < spots.Length; i++)
                    spots[i] = AddSpot(i);

                Feedback = Label("Diaper feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 98), Vector2.right, new Vector2(0, 126));
                var cancel = ActionButton("Cancel", content, 0, .245f, 7, 95, close, Mist, Ink);
                cancel.name = "Cancel diaper";
                StartControl = ActionButton("Quick change", content, .245f, .68f, 7, 95, Complete, Mist, Ink);
                finish = ActionButton("Done", content, .68f, 1, 7, 95, Finish, Fern, Color.white);
                finish.name = "Finish diaper";
                ResetActivity();
            }

            internal void ResetActivity()
            {
                remaining = spots.Length;
                submitting = false;
                committed = false;
                foreach (var spot in spots)
                    spot.gameObject.SetActive(true);

                StartControl.interactable = true;
                finish.interactable = false;
                ShowProgress();
            }

            private RectTransform AddSpot(int index)
            {
                var center = SpotCenters[index];
                var spot = Box($"Diaper spot {index + 1}", mat, center, center,
                    new Vector2(-35, -35), new Vector2(35, 35));
                var mark = Surface(spot, spotArtwork != null ? Color.white : new Color32(145, 91, 54, 255));
                mark.sprite = spotArtwork;
                mark.preserveAspect = true;
                spot.localRotation = Quaternion.Euler(0, 0, (index - 1) * 17);
                return spot;
            }

            private void Wipe(Vector2 start, Vector2 end)
            {
                if (submitting || committed || remaining == 0)
                    return;

                var bounds = mat.rect;
                bool cleaned = false;
                for (int index = 0; index < spots.Length; index++)
                {
                    if (!spots[index].gameObject.activeSelf)
                        continue;

                    var center = SpotCenters[index];
                    var x = Mathf.Lerp(bounds.xMin, bounds.xMax, center.x);
                    var y = Mathf.Lerp(bounds.yMin, bounds.yMax, center.y);
                    if (!WipeGeometry.TouchesSpot(start.x, start.y, end.x, end.y, x, y, HitRadius))
                        continue;

                    spots[index].gameObject.SetActive(false);
                    remaining--;
                    cleaned = true;
                }

                if (!cleaned)
                    return;
                finish.interactable = remaining == 0;
                ShowProgress();
            }

            private void ShowProgress()
            {
                Feedback.text = remaining == 0 ? "All clean. Tap Done to finish."
                    : $"{spots.Length - remaining} of {spots.Length} spots cleaned";
            }

            private void Finish()
            {
                if (remaining == 0)
                    Complete();
            }

            private void Complete()
            {
                if (submitting || committed)
                    return;

                submitting = true;
                try
                {
                    if (!change())
                        return;

                    committed = true;
                    StartControl.interactable = false;
                    finish.interactable = false;
                }
                finally
                {
                    submitting = false;
                }
            }
        }
    }
}
