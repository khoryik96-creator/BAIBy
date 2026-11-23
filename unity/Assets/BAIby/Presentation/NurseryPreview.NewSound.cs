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

        private Button BuildNewSoundNotice(RectTransform panel)
        {
            var notice = ActionButton("A new sound", panel, 0, 1, 283, 371, OpenNewSound, Mist, Ink);
            var title = notice.GetComponentInChildren<Text>();
            title.alignment = TextAnchor.MiddleLeft;
            title.rectTransform.offsetMin = new Vector2(13, 47);
            title.rectTransform.offsetMax = new Vector2(-13, 0);
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
            status.rectTransform.offsetMin = new Vector2(pending ? 16 : 0, 283);
            status.rectTransform.offsetMax = new Vector2(pending ? -16 : 0, pending ? 330 : 365);
        }

        private sealed class NewSoundPanel
        {
            private const string Description = "A soft sound comes from the window. Your baby turns toward it and looks back at you.";

            public GameObject Root { get; }
            public Button Comfort { get; }
            public Text Scene { get; }

            public NewSoundPanel(RectTransform content, Action defer, Action<CareAction> choose)
            {
                Root = content.parent.gameObject;
                Label("New Sound Heading", content, "A new sound", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 265), new Vector2(0.72f, 0), new Vector2(0, 351), FontStyle.Bold);
                ActionButton("Later", content, 0.73f, 1, 265, 353, defer, Mist, Ink);

                Scene = Label("New Sound Scene", content, Description, 26, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 202), Vector2.right, new Vector2(0, 265));
                Comfort = Choice(content, "Comfort them", "Comfort +16  ·  Connection +2", 103,
                    () => choose(CareAction.ComfortNewSound));
                Choice(content, "Explore together", "Connection +5  ·  Energy -6", 9,
                    () => choose(CareAction.ExploreNewSound));
                Root.SetActive(false);
            }

            public void ResetScene()
            {
                Scene.text = Description;
            }

            private static Button Choice(RectTransform content, string title, string effects, float bottom, Action choose)
            {
                var button = ActionButton(title, content, 0, 1, bottom, bottom + 88, choose, Fern, Color.white);
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(12, 34);
                label.rectTransform.offsetMax = new Vector2(-12, -4);
                label.alignment = TextAnchor.MiddleLeft;
                Label(title + " Effects", button.transform, effects, 20, Color.white, TextAnchor.MiddleLeft,
                    new Vector2(12, 6), Vector2.right, new Vector2(-12, 36));
                return button;
            }
        }
    }
}
