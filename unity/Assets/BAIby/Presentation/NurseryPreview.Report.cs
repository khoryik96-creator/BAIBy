using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private ReportPanel report;

        private sealed class ReportPanel
        {
            private readonly Text sitting;
            private readonly Text crawling;
            private readonly Text walking;
            private readonly Text favoriteToy;
            private readonly Text stories;
            private readonly Text blocks;
            private readonly Text bond;

            public GameObject Root { get; }
            public Button Done { get; }

            public ReportPanel(RectTransform content, Action close)
            {
                Root = content.parent.gameObject;
                Label("Report Heading", content, "Baby report", 38, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 375), new Vector2(0.72f, 0), new Vector2(0, 463), FontStyle.Bold);
                Done = ActionButton("Done", content, 0.73f, 1, 375, 463, close, Mist, Ink);
                Done.name = "Close baby report";

                Section("Milestones", content, 333, 370);
                sitting = Milestone("Sitting and reaching", "Report Sitting", content, 299);
                crawling = Milestone("Crawling", "Report Crawling", content, 265);
                walking = Milestone("First steps", "Report Walking", content, 231);

                Section("Interests", content, 189, 226);
                favoriteToy = Line("Report Favorite Toy", content, 154, 187);
                stories = Line("Report Stories", content, 122, 154);
                blocks = Line("Report Blocks", content, 90, 122);

                var bondCard = Box("Report Bond", content, Vector2.zero, Vector2.right,
                    new Vector2(0, 12), new Vector2(0, 81));
                Surface(bondCard, Mist);
                Label("Bond Label", bondCard, "Connection", 27, Ink, TextAnchor.MiddleLeft,
                    new Vector2(16, 0), Vector2.one, new Vector2(-16, 0), FontStyle.Bold);
                bond = Label("Report Bond Value", bondCard, "", 27, Fern, TextAnchor.MiddleRight,
                    new Vector2(16, 0), Vector2.one, new Vector2(-16, 0), FontStyle.Bold);
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyReport details)
            {
                SetMilestone(sitting, details.CanSitAndReach);
                SetMilestone(crawling, details.CanCrawl);
                SetMilestone(walking, details.CanWalk);
                favoriteToy.text = details.FavoriteToy is Toy favorite ? $"Favorite toy · {ToyName(favorite)}" : "No favorite toy yet";
                stories.text = details.EnjoysStories
                    ? $"Enjoys story time · {details.StorySessions} stories read"
                    : $"Stories read · {details.StorySessions}";
                blocks.text = details.EnjoysBlocks
                    ? $"Enjoys blocks · {details.BlockTowers} towers built"
                    : $"Towers built · {details.BlockTowers}";
                bond.text = $"{Mathf.RoundToInt((float)details.Connection)} / 100";
            }

            private static void Section(string name, Transform content, float bottom, float top)
            {
                Label(name, content, name, 27, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), Vector2.right, new Vector2(0, top), FontStyle.Bold);
            }

            private static Text Line(string name, Transform content, float bottom, float top)
            {
                return Label(name, content, "", 23, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), Vector2.right, new Vector2(0, top));
            }

            private static Text Milestone(string title, string valueName, Transform parent, float bottom)
            {
                Label(title, parent, title, 24, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), new Vector2(0.70f, 0), new Vector2(0, bottom + 34));
                return Label(valueName, parent, "", 22, Secondary, TextAnchor.MiddleRight,
                    new Vector2(0, bottom), Vector2.right, new Vector2(0, bottom + 34));
            }

            private static void SetMilestone(Text status, bool unlocked)
            {
                status.text = unlocked ? "Unlocked" : "Locked";
                status.color = unlocked ? Fern : Secondary;
            }
        }
    }
}
