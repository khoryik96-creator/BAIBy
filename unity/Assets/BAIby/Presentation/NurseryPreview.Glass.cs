using System;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Wiping a fish's glass (P4, docs/pet-mode.md: "Wipe glass reuses DiaperWipeInput"): the daily wipe as a little
    // gesture, the baby's diaper cleanup in fish form. Three soft smudges sit on the glass; a swipe or a tap across each
    // clears it, and Done wipes the glass. Quick wipe is the one-tap way, and nothing can fail: Cancel just closes the
    // sheet, and the smudges come back next time. A sick fish is too poorly for a wipe, so the tile answers at once.
    public sealed partial class NurseryPreview
    {
        private GlassPanel glass;

        private void BuildGlassPanel() =>
            glass = new GlassPanel(Tray("Glass Panel", false, 378), () => ShowPetPanel(PetPanel.Care), CompleteGlassWipe);

        // The Wipe glass tile: the wipe sheet, or the care's answer at once when the fish is too poorly for it.
        private void OpenGlassWipe()
        {
            var state = FindCompanion(petSlot)?.PetState;
            if (state == null || IsPoorly(state) || glass == null)
            {
                CarePet(PetVerb.WipeGlass);
                return;
            }

            glass.ResetActivity(state.Name);
            ShowPetPanel(PetPanel.Glass);
        }

        private void CompleteGlassWipe()
        {
            ShowPetPanel(PetPanel.Care);
            CarePet(PetVerb.WipeGlass);
        }

        private sealed class GlassPanel
        {
            private const float HitRadius = 40;
            private static readonly Vector2[] SmudgeCenters =
            {
                new Vector2(.22f, .38f),
                new Vector2(.50f, .66f),
                new Vector2(.78f, .42f),
            };

            private readonly RectTransform mat;
            private readonly RectTransform[] smudges = new RectTransform[SmudgeCenters.Length];
            private readonly Button finish;
            private readonly Action wipe;
            private int remaining;
            private bool committed;

            internal GameObject Root { get; }
            internal Button StartControl { get; }
            internal Text Feedback { get; }

            internal GlassPanel(RectTransform content, Action close, Action wipe)
            {
                Root = content.parent.gameObject;
                this.wipe = wipe;
                Label("Glass title", content, "Wipe the glass", 36, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 328), Vector2.right, new Vector2(0, 374), FontStyle.Bold);
                Label("Glass instructions", content, "Tap or swipe across all three smudges.", 23, Secondary,
                    TextAnchor.MiddleLeft, new Vector2(0, 301), Vector2.right, new Vector2(0, 332));
                mat = Box("Glass wipe mat", content, Vector2.zero, Vector2.right, new Vector2(0, 129), new Vector2(0, 298));
                var pane = Surface(mat, Sky);
                SoftShapes.Radius(pane, Px(20));
                pane.raycastTarget = true;
                // Two soft highlights, so the mat reads as a pane of glass.
                foreach (var (left, width) in new[] { (.70f, .05f), (.78f, .025f) })
                {
                    var streak = Box("Glass highlight", mat, new Vector2(left, .14f), new Vector2(left + width, .86f),
                        Vector2.zero, Vector2.zero);
                    streak.localRotation = Quaternion.Euler(0, 0, -14);
                    var shine = Surface(streak, new Color(1, 1, 1, .55f));
                    SoftShapes.Pill(shine);
                    shine.raycastTarget = false;
                }

                mat.gameObject.AddComponent<DiaperWipeInput>().Bind(mat, Wipe);
                for (var i = 0; i < smudges.Length; i++)
                    smudges[i] = AddSmudge(i);

                Feedback = Label("Glass feedback", content, "", 21, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 98), Vector2.right, new Vector2(0, 126));
                var cancel = ActionButton("Cancel", content, 0, .245f, 7, 95, close, Mist, Ink);
                cancel.name = "Cancel glass";
                StartControl = ActionButton("Quick wipe", content, .245f, .68f, 7, 95, Complete, Mist, Ink);
                StartControl.name = "Quick wipe glass";
                finish = ActionButton("Done", content, .68f, 1, 7, 95, Finish, Fern, Color.white);
                finish.name = "Finish glass";
                ResetActivity(null);
                Root.SetActive(false);
            }

            internal void ResetActivity(string name)
            {
                remaining = smudges.Length;
                committed = false;
                foreach (var smudge in smudges)
                    smudge.gameObject.SetActive(true);

                StartControl.interactable = true;
                finish.interactable = false;
                ShowProgress(name);
            }

            private RectTransform AddSmudge(int index)
            {
                var center = SmudgeCenters[index];
                var smudge = Box($"Glass smudge {index + 1}", mat, center, center, new Vector2(-38, -30), new Vector2(38, 30));
                var mark = Surface(smudge, new Color32(214, 226, 204, 235));
                SoftShapes.Disc(mark);
                mark.raycastTarget = false;
                smudge.localRotation = Quaternion.Euler(0, 0, (index - 1) * 21);
                return smudge;
            }

            private void Wipe(Vector2 start, Vector2 end)
            {
                if (committed || remaining == 0)
                    return;

                var bounds = mat.rect;
                var cleaned = false;
                for (var index = 0; index < smudges.Length; index++)
                {
                    if (!smudges[index].gameObject.activeSelf)
                        continue;

                    var center = SmudgeCenters[index];
                    var x = Mathf.Lerp(bounds.xMin, bounds.xMax, center.x);
                    var y = Mathf.Lerp(bounds.yMin, bounds.yMax, center.y);
                    if (!WipeGeometry.TouchesSpot(start.x, start.y, end.x, end.y, x, y, HitRadius))
                        continue;

                    smudges[index].gameObject.SetActive(false);
                    remaining--;
                    cleaned = true;
                }

                if (!cleaned)
                    return;
                finish.interactable = remaining == 0;
                ShowProgress(null);
            }

            private void ShowProgress(string name)
            {
                Feedback.text = remaining == 0 ? "Sparkly clean. Tap Done to finish."
                    : remaining == smudges.Length && name != null ? $"{name} is watching you through the glass."
                    : $"{smudges.Length - remaining} of {smudges.Length} smudges wiped";
            }

            private void Finish()
            {
                if (remaining == 0)
                    Complete();
            }

            private void Complete()
            {
                if (committed)
                    return;

                committed = true;
                StartControl.interactable = false;
                finish.interactable = false;
                wipe();
            }
        }
    }
}
