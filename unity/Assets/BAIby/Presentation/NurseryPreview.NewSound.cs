using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private Button newSoundNotice;
        private NewSoundPanel newSound;

        // A butter pill in the care sheet's heading row, in place of the mood pill, while a new sound waits.
        private Button BuildNewSoundNotice(RectTransform heading)
        {
            var notice = ActionButton("A new sound", heading, 0, 1, 0, 0, OpenNewSound, Butter, ButterInk);
            SoftShapes.Pill(notice.image);
            var title = notice.GetComponentInChildren<Text>();
            title.fontSize = 22;
            title.alignment = TextAnchor.MiddleLeft;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.rectTransform.offsetMin = new Vector2(Px(10) + 36, 0);
            title.rectTransform.offsetMax = new Vector2(-Px(10), 0);
            IconAt("A new sound Icon", notice.transform, "Sound", ButterInk, new Vector2(0, 0.5f), new Vector2(Px(10) + 15, 0), 30);
            notice.gameObject.SetActive(false);
            return notice;
        }

        private void OpenNewSound()
        {
            if (!session.State.HasPendingNewSound)
                return;

            newSound.ResetScene();
            ShowPanel(NurseryPanel.NewSound);
            nursery.PlayNewSoundCue();
        }

        private void UpdateNewSoundNotice(bool pending)
        {
            newSoundNotice.gameObject.SetActive(pending);
        }

        // The new-sound moment keeps its title inside its own sheet.
        private sealed class NewSoundPanel
        {
            private const string Description = "A soft sound comes from the window. Your baby turns toward it and looks back at you.";
            private const float Height = 360;

            public GameObject Root { get; }
            public Button Comfort { get; }
            public Text Scene { get; }

            public NewSoundPanel(RectTransform content, Action defer, Action<CareAction> choose)
            {
                Root = content.parent.gameObject;
                SheetHeading("New Sound Heading", content, "A new sound", Height);
                SheetClose("Later", "Later", content, Height, defer);

                Scene = Label("New Sound Scene", content, Description, 26, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, 200), Vector2.right, new Vector2(0, Height - 86));
                FitText(Scene, 18, 26);
                Comfort = ChoiceRow(content, "Comfort them", "Comfort +16  ·  Connection +2", "Soothe", Lilac, 102,
                    () => choose(CareAction.ComfortNewSound));
                ChoiceRow(content, "Explore together", "Connection +5  ·  Energy -6", "Explore", Sky, 6,
                    () => choose(CareAction.ExploreNewSound));
                Root.SetActive(false);
            }

            public void ResetScene()
            {
                Scene.text = Description;
            }
        }
    }
}
