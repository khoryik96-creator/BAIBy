using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    public sealed partial class NurseryPreview
    {
        private ReportPanel report;

        // The baby report: each track's quality and the growth chart band, the milestones with the next one's
        // projection, interests, and Bond with a warm word for where temperament stands right now.
        private sealed class ReportPanel
        {
            public const float Height = 600;
            private readonly Text[] tracks = new Text[4];
            private readonly Text band;
            private readonly Text sitting;
            private readonly Text crawling;
            private readonly Text walking;
            private readonly Text favoriteToy;
            private readonly Text stories;
            private readonly Text blocks;
            private readonly Text bond;
            private readonly Text temperament;

            public GameObject Root { get; }
            public Button Done { get; }

            public ReportPanel(RectTransform content, Action close)
            {
                Root = content.parent.gameObject;
                SheetHeading("Report Heading", content, "Baby report", Height);
                Done = SheetClose("Close baby report", "Done", content, Height, close);

                Section("Report Growth Section", content, 474, 510, "Growth");
                var right = Box("Report Tracks Right", content, new Vector2(0.5f, 0), Vector2.right, new Vector2(8, 0), Vector2.zero);
                tracks[(int)DevelopmentTrack.Motor] = Line("Report Moving", content, 440, 472, 0.5f);
                tracks[(int)DevelopmentTrack.Learning] = Line("Report Learning", right, 440, 472);
                tracks[(int)DevelopmentTrack.Social] = Line("Report Bonding", content, 408, 440, 0.5f);
                tracks[(int)DevelopmentTrack.Physical] = Line("Report Growing", right, 408, 440);
                band = Line("Report Growth Chart", content, 376, 408);
                band.color = FernDeep;

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
                Label("Bond Label", bondCard, "Bond", 26, FernDeep, TextAnchor.MiddleLeft,
                    new Vector2(70, 34), Vector2.one, new Vector2(-150, -4), FontStyle.Bold);
                temperament = Label("Report Temperament", bondCard, "", 18, FernDeep, TextAnchor.MiddleLeft,
                    new Vector2(70, 6), Vector2.right, new Vector2(-150, 36));
                FitText(temperament, 13, 18);
                bond = Label("Report Bond Value", bondCard, "", 32, FernDeep, TextAnchor.MiddleRight,
                    new Vector2(20, 0), Vector2.one, new Vector2(-20, 0), FontStyle.Bold);
                Root.SetActive(false);
            }

            public void UpdateDetails(BabyReport details, GrowthReport growth, string temperamentLine)
            {
                foreach (DevelopmentTrack track in Enum.GetValues(typeof(DevelopmentTrack)))
                    tracks[(int)track].text = growth == null ? string.Empty
                        : $"{TrackName(track)} · {TrackWord(track, growth.Quality(track))}";
                band.text = growth == null ? string.Empty : "Growth chart · " + BandName(growth.Band);
                SetMilestone(sitting, MotorSkill.Sitting, details.CanSitAndReach, growth);
                SetMilestone(crawling, MotorSkill.Crawling, details.CanCrawl, growth);
                SetMilestone(walking, MotorSkill.Walking, details.CanWalk, growth);
                favoriteToy.text = details.FavoriteToy is Toy favorite ? $"Favorite toy · {ToyName(favorite)}" : "No favorite toy yet";
                stories.text = details.EnjoysStories
                    ? $"Enjoys story time · {details.StorySessions} stories read"
                    : $"Stories read · {details.StorySessions}";
                blocks.text = details.EnjoysBlocks
                    ? $"Enjoys blocks · {details.BlockTowers} towers built"
                    : $"Towers built · {details.BlockTowers}";
                bond.text = $"{Mathf.RoundToInt((float)details.Bond)} / 100";
                temperament.text = temperamentLine ?? string.Empty;
            }

            private static void Section(string name, Transform content, float bottom, float top, string text = null)
            {
                Label(name, content, text ?? name, 30, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), Vector2.right, new Vector2(0, top), FontStyle.Bold);
            }

            private static Text Line(string name, Transform content, float bottom, float top, float right = 1)
            {
                var line = Label(name, content, "", 23, Muted, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), new Vector2(right, 0), new Vector2(right < 1 ? -8 : 0, top));
                FitText(line, 16, 23);
                return line;
            }

            private static Text Milestone(string title, string valueName, Transform parent, float bottom)
            {
                Label(title, parent, title, 24, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, bottom), new Vector2(0.52f, 0), new Vector2(0, bottom + 34));
                // The status takes the right half: "Unlocked", "A bit shy", or "Likely in about 2 days".
                var column = Box(valueName + " Column", parent, new Vector2(0.52f, 0), Vector2.right, Vector2.zero, Vector2.zero);
                var value = Label(valueName, column, "", 22, Secondary, TextAnchor.MiddleRight,
                    new Vector2(8, bottom), Vector2.right, new Vector2(0, bottom + 34), FontStyle.Bold);
                FitText(value, 15, 22);
                return value;
            }

            private static void SetMilestone(Text status, MotorSkill skill, bool unlocked, GrowthReport growth)
            {
                status.text = MilestoneStatus(skill, unlocked, growth);
                status.color = unlocked ? FernDeep : Secondary;
            }
        }
    }
}
