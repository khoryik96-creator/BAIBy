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
                SheetHeading("Report Heading", content, "Baby report", 470);
                Done = SheetClose("Close baby report", "Done", content, 470, close);

                Section("Milestones", content, 333, 370);
                sitting = Milestone("Sitting and reaching", "Report Sitting", content, 299);
                crawling = Milestone("Crawling", "Report Crawling", content, 265);
                walking = Milestone("First steps", "Report Walking", content, 231);

                Section("Interests", content, 189, 226);
                favoriteToy = Line("Report Favorite Toy", content, 154, 187);
                stories = Line("Report Stories", content, 122, 154);
                blocks = Line("Report Blocks", content, 90, 122);

                var bondCard = Box("Report Bond", content, Vector2.zero, Vector2.right,
                    new Vector2(0, 8), new Vector2(0, 82));
                SoftShapes.Radius(Surface(bondCard, FernSoft), Px(20));
                IconAt("Bond Icon", bondCard, "Soothe", FernDeep, new Vector2(0, 0.5f), new Vector2(40, 0), Px(20));
                Label("Bond Label", bondCard, "Connection", 26, FernDeep, TextAnchor.MiddleLeft,
                    new Vector2(70, 0), Vector2.one, new Vector2(-20, 0), FontStyle.Bold);
                bond = Label("Report Bond Value", bondCard, "", 32, FernDeep, TextAnchor.MiddleRight,
                    new Vector2(20, 0), Vector2.one, new Vector2(-20, 0), FontStyle.Bold);
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
                Label(name, content, name, 30, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), Vector2.right, new Vector2(0, top), FontStyle.Bold);
            }

            private static Text Line(string name, Transform content, float bottom, float top)
            {
                return Label(name, content, "", 23, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), Vector2.right, new Vector2(0, top));
            }

            private static Text Milestone(string title, string valueName, Transform parent, float bottom)
            {
                Label(title, parent, title, 24, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), new Vector2(0.70f, 0), new Vector2(0, bottom + 34));
                return Label(valueName, parent, "", 22, Secondary, TextAnchor.MiddleRight,
                    new Vector2(0, bottom), Vector2.right, new Vector2(0, bottom + 34), FontStyle.Bold);
            }

            private static void SetMilestone(Text status, bool unlocked)
            {
                status.text = unlocked ? "Unlocked" : "Locked";
                status.color = unlocked ? FernDeep : Secondary;
            }
        }
    }
}
