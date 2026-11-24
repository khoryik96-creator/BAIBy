using System;
using System.Collections.Generic;
using System.Globalization;
using BAIby.Application;
using BAIby.Simulation;
using BAIby.Simulation.Pets;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Care slice 5 on screen (docs/care-and-growth.md, Bond and temperament, Return loops): the Photo tile, the baby's
    // journal (moments, week pages, hard days we got through and the sticker shelf), warm words for temperament, the
    // sticker line when a care rhythm earns one, and the small idle play of a settled little one. Copy is temporary and
    // warm: a phase, never a label of the child.
    public sealed partial class NurseryPreview
    {
        // A photo catches the moment of the last care for this long; after it the baby is just being themselves.
        private const float PhotoMomentSeconds = 120;
        private Button photoButton;
        private Text photoLabel;
        private PhotoMoment photoMoment = PhotoMoment.Calm;
        private float photoMomentAt = float.NegativeInfinity;
        // The companion whose rhythm stickers have been announced, and the longest rhythm seen for it.
        private BabySession stickerSession;
        private int shownLongest;

        private void TakePhoto()
        {
            if (session?.State == null)
                return;

            var moment = CurrentMoment(session.State);
            (bool Taken, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) result;
            try
            {
                result = session.TakePhoto(moment);
            }
            catch (Exception error)
            {
                ReportFailure(error, "The photo couldn't be saved. Please try again.");
                return;
            }

            var unlocked = (result.UnlockedSitting, result.UnlockedCrawling, result.UnlockedWalking);
            Paint(Reaction.None, unlocked);
            if (unlocked.UnlockedSitting || unlocked.UnlockedCrawling || unlocked.UnlockedWalking)
                return;

            var name = session.State.Name;
            var left = session.PhotosLeft;
            status.text = !result.Taken
                ? $"That's today's three photos of {name}. More tomorrow!"
                : $"Click! A photo of {name} {MomentWords(moment)} is in the journal. " +
                  (left == 0 ? "That's today's three." : left == 1 ? "One more today." : $"{left} more today.");
        }

        // The moment the baby is in: poorly in bed, the last care's reaction while it plays, or simply being themselves.
        private PhotoMoment CurrentMoment(BabyState state)
        {
            if (state.Care.Condition == CareCondition.Sick)
                return PhotoMoment.Poorly;
            return Time.unscaledTime - photoMomentAt <= PhotoMomentSeconds ? photoMoment : PhotoMoment.Calm;
        }

        private void RememberMoment(Reaction reaction)
        {
            var moment = MomentFor(reaction);
            if (moment == null)
                return;
            photoMoment = moment.Value;
            photoMomentAt = Time.unscaledTime;
        }

        private static PhotoMoment? MomentFor(Reaction reaction)
        {
            switch (reaction)
            {
                case Reaction.Satisfied: return PhotoMoment.Meal;
                case Reaction.Calmed:
                case Reaction.NewSoundComforted: return PhotoMoment.Cuddle;
                case Reaction.Rested: return PhotoMoment.Napping;
                case Reaction.EnjoysStory: return PhotoMoment.Story;
                case Reaction.FreshBath: return PhotoMoment.Bath;
                case Reaction.StackedBlocks: return PhotoMoment.Blocks;
                case Reaction.TracksToy:
                case Reaction.ReachesForToy:
                case Reaction.CrawlsToToy:
                case Reaction.WalksToToy:
                case Reaction.NewSoundExplored:
                case Reaction.SkillBack: return PhotoMoment.Playing;
                case Reaction.None: return null;
                default: return PhotoMoment.Calm;
            }
        }

        private static string MomentWords(PhotoMoment moment)
        {
            switch (moment)
            {
                case PhotoMoment.Playing: return "playing";
                case PhotoMoment.Napping: return "napping";
                case PhotoMoment.Story: return "at story time";
                case PhotoMoment.Cuddle: return "having a cuddle";
                case PhotoMoment.Meal: return "after a meal";
                case PhotoMoment.Bath: return "fresh from the bath";
                case PhotoMoment.Poorly: return "resting up";
                case PhotoMoment.Blocks: return "with the block tower";
                default: return "being themselves";
            }
        }

        private static string MomentTitle(PhotoMoment moment)
        {
            switch (moment)
            {
                case PhotoMoment.Playing: return "Playtime";
                case PhotoMoment.Napping: return "Nap time";
                case PhotoMoment.Story: return "Story time";
                case PhotoMoment.Cuddle: return "Cuddles";
                case PhotoMoment.Meal: return "Mealtime";
                case PhotoMoment.Bath: return "Bath time";
                case PhotoMoment.Poorly: return "Resting up";
                case PhotoMoment.Blocks: return "Building blocks";
                default: return "Just being me";
            }
        }

        private void UpdatePhotoTile()
        {
            if (photoButton == null || session?.State == null)
                return;
            photoLabel = photoLabel ?? photoButton.GetComponentInChildren<Text>();
            photoLabel.text = session.PhotosLeft > 0 ? "Photo" : "Photos done";
        }

        // A short line for the bubble when a care rhythm reaches a sticker (and any look it earns).
        private string StickerLine(BabyState state)
        {
            var longest = session.Bonding?.LongestRhythm ?? 0;
            if (!ReferenceEquals(stickerSession, session))
            {
                stickerSession = session;
                shownLongest = longest;
                return null;
            }

            if (longest <= shownLongest)
            {
                shownLongest = longest;
                return null;
            }

            shownLongest = longest;
            if (!BondRules.IsSticker(longest))
                return null;

            var look = EarnedLookAt(longest);
            return $"{longest} cared-for days in a row! {state.Name}'s journal has a new sticker" +
                (look == null ? "." : $", and the {look} is earned for good.");
        }

        private static string EarnedLookAt(int rhythm)
        {
            foreach (Headwear headwear in Enum.GetValues(typeof(Headwear)))
            {
                if (EarnedLooks.RhythmFor(headwear) == rhythm)
                    return HatName(headwear);
            }

            foreach (Outfit outfit in Enum.GetValues(typeof(Outfit)))
            {
                if (EarnedLooks.RhythmFor(outfit) == rhythm)
                    return outfit + " outfit";
            }

            return null;
        }

        private static string HatName(Headwear headwear)
        {
            switch (headwear)
            {
                case Headwear.Bow: return "Bow";
                case Headwear.BearBeanie: return "Bear beanie";
                case Headwear.BucketHat: return "Bucket hat";
                case Headwear.DaisyClip: return "Daisy clip";
                default: return "No hat";
            }
        }

        // The bubble's line when nothing needs doing: where temperament stands right now, warmly, or null.
        private string TemperamentMoment(BabyState state)
        {
            switch (session.Security)
            {
                case SecurityPhase.Clingy:
                    return $"{state.Name} is going through a clingy phase. Extra cuddles help.";
                case SecurityPhase.Settled:
                    return $"{state.Name} is happily playing on their own.";
            }

            switch (session.Curiosity)
            {
                case CuriosityPhase.Explorer:
                    return $"{state.Name} is in an exploring mood. Something new would be a delight.";
                case CuriosityPhase.Cautious:
                    return $"{state.Name} likes familiar things right now. A favourite toy feels cosy.";
                default:
                    return null;
            }
        }

        // The report's quieter line under Bond.
        private static string TemperamentLine(SecurityPhase security, CuriosityPhase curiosity, double bond)
        {
            if (security == SecurityPhase.Clingy)
                return "Going through a clingy phase. Extra cuddles help.";
            if (security == SecurityPhase.Settled)
                return "Settled, and happy to play alone a while";
            if (curiosity == CuriosityPhase.Explorer)
                return "In an exploring mood, loves something new";
            if (curiosity == CuriosityPhase.Cautious)
                return "Likes familiar things right now";

            return bond < 20 ? "Getting to know you" : "Growing closer every day";
        }

        // Whether a settled little one plays by themselves for a moment on this minute's refresh.
        private bool PlaysAlone(BabyState state) =>
            state != null && session.Security == SecurityPhase.Settled && state.Care.Mood == CareMood.Content &&
            state.Care.Condition == CareCondition.Well && activePanel == NurseryPanel.Care;

        private void OpenBabyJournal()
        {
            if (session?.State == null)
                return;

            journal.ShowBaby(session.State, session.Journal, session.Bonding, null);
            ShowPanel(NurseryPanel.Journal);
        }

        // Done goes back to wherever the journal was opened: Home or the Growth sheet (a baby's or a pet's).
        private void CloseJournal()
        {
            if (view == HouseholdView.Journal)
                ShowHome();
            else if (view == HouseholdView.PetRoom)
                ShowPetPanel(PetPanel.Growth);
            else
                ShowPanel(NurseryPanel.Development);
        }

        private sealed partial class JournalPanel
        {
            // A baby's journal is taller than a pet's: tabs, four rows a page and arrows to turn the pages.
            public const float BabyHeight = 600;
            public const float PetHeight = 470;
            private const int RowsPerPage = 4;
            private const float RowsTop = 232;
            private const float RowHeight = 78;
            private const float RowGap = 8;
            private static readonly string[] TabNames = { "Moments", "Weeks", "Hard days", "Stickers" };

            private readonly List<JournalRowView> rows = new List<JournalRowView>();
            private readonly List<(Button Tab, Text Label)> tabs = new List<(Button Tab, Text Label)>();
            private readonly Func<BabyAppearance, int, bool, Sprite> photoSprite;
            private RectTransform tray;
            private GameObject pages;
            private GameObject petLines;
            private Text summary;
            private Button newer;
            private Button older;
            private int tab;
            private int page;
            private JournalRecord record;
            private BondRecord bonding;
            // Whose journal is shown, and for a pet (care slice 7) how its photos are drawn.
            private string companion;
            private bool petPages;
            private Func<JournalEntry, Sprite> petPicture;

            // The pages of a baby's journal, built once beside the pet's lines.
            private void BuildPages(RectTransform content)
            {
                tray = (RectTransform)content.parent;
                var group = Box("Journal Pages", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                pages = group.gameObject;
                summary = Label("Journal Summary", group, "", 22, FernDeep, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                    Vector2.zero, FontStyle.Bold);
                FitText(summary, 15, 22);
                PlaceFromTop(summary.rectTransform, 0, 1, 102, 134);

                // Tabs on a soft track, and two arrows that turn the pages.
                var track = Box("Journal Tabs", group, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(track, 0, 1, 142, 224, 0, 2 * (82 + 10));
                SoftShapes.Pill(Surface(track, Mist));
                for (var i = 0; i < TabNames.Length; i++)
                {
                    var index = i;
                    var button = ActionButton("Journal tab " + TabNames[i], track, i / 4f, (i + 1) / 4f, 8, 74,
                        () => ShowTab(index), Mist, Muted);
                    var label = button.GetComponentInChildren<Text>();
                    label.text = TabNames[i];
                    FitText(label, 14, 21);
                    label.rectTransform.offsetMin = new Vector2(4, 0);
                    label.rectTransform.offsetMax = new Vector2(-4, 0);
                    SoftShapes.Pill(button.image);
                    var rect = (RectTransform)button.transform;
                    rect.offsetMin = new Vector2(i == 0 ? 6 : 2, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(i == TabNames.Length - 1 ? -6 : -2, rect.offsetMax.y);
                    tabs.Add((button, label));
                }

                newer = IconButton("Newer journal page", group, "Back", () => Turn(-1), Mist);
                older = IconButton("Older journal page", group, "Back", () => Turn(1), Mist);
                ((RectTransform)older.transform.Find("Older journal page Icon")).localRotation = Quaternion.Euler(0, 0, 180);
                PlaceFromTop((RectTransform)newer.transform, 1, 1, 142, 224, -2 * 82 - 10, 82 + 10);
                PlaceFromTop((RectTransform)older.transform, 1, 1, 142, 224, -82, 0);

                for (var i = 0; i < RowsPerPage; i++)
                {
                    var top = RowsTop + i * (RowHeight + RowGap);
                    rows.Add(new JournalRowView(group, i, top, top + RowHeight));
                }

                SettleText(group);
                pages.SetActive(false);
            }

            // The baby's journal, read-only: from Growth (resting null) or from Home (resting or awake).
            public void ShowBaby(BabyState state, JournalRecord journalRecord, BondRecord bondRecord, bool? resting)
            {
                Feedback.text = string.Empty;
                companion = state.Name;
                petPages = false;
                petPicture = null;
                record = journalRecord ?? JournalRecord.Empty;
                bonding = bondRecord;
                SetHeight(BabyHeight);
                petLines.SetActive(false);
                pages.SetActive(true);
                heading.text = $"{state.Name}'s journal";
                this.state.text = resting == true
                    ? $"{state.Name} is resting. They're safe, and their needs don't drop while they rest."
                    : resting == false ? $"{state.Name} is awake in their room."
                    : "Memories, stickers and the days you got through together.";
                var stage = StageTitle(HouseholdRules.StageOf(state));
                var rhythm = bonding?.Rhythm ?? 0;
                summary.text = $"{DayWord(JournalRules.DayOf(state.Age))} · {stage} · Bond {Mathf.RoundToInt((float)state.Bond)}" +
                    $" · Rhythm {rhythm} {(rhythm == 1 ? "day" : "days")}";
                ShowTab(0);
            }

            // A pet's journal with pages (a species with a care profile): milestones, photos drawn from its pose art,
            // stickers, week pages and hard days, read-only. From Growth (resting null) or from Home.
            public void ShowPetPages(PetState pet, JournalRecord journalRecord, BondRecord bondRecord, bool? resting,
                Func<JournalEntry, Sprite> picture)
            {
                Feedback.text = string.Empty;
                companion = pet.Name;
                petPages = true;
                petPicture = picture;
                record = journalRecord ?? JournalRecord.Empty;
                bonding = bondRecord;
                SetHeight(BabyHeight);
                petLines.SetActive(false);
                pages.SetActive(true);
                heading.text = $"{pet.Name}'s journal";
                state.text = resting == true
                    ? $"{pet.Name} is resting. They're safe, and their needs don't drop while they rest."
                    : resting == false ? $"{pet.Name} is awake in their room."
                    : "Milestones, photos and the days you got through together.";
                var rhythm = bonding?.Rhythm ?? 0;
                summary.text = $"{DayWord(JournalRules.DayOf(pet.Age))} · {PetStageTitle(pet.Species, pet.Stage)} · " +
                    $"Bond {Mathf.RoundToInt((float)pet.Bond)} · Rhythm {rhythm} {(rhythm == 1 ? "day" : "days")}";
                ShowTab(0);
            }

            private void SetHeight(float height)
            {
                tray.offsetMax = new Vector2(tray.offsetMax.x, tray.offsetMin.y + height + SheetChrome);
                PlaceFromTop(Feedback.rectTransform, 0, 1, height - 26, height);
            }

            private void ShowTab(int index)
            {
                tab = index;
                page = 0;
                for (var i = 0; i < tabs.Count; i++)
                {
                    var shown = i == index;
                    tabs[i].Tab.image.color = shown ? Paper : Mist;
                    tabs[i].Label.color = shown ? Ink : Muted;
                    SoftShapes.Lift(tabs[i].Tab.gameObject);
                    tabs[i].Tab.GetComponent<UnityEngine.UI.Shadow>().effectColor = shown ? WithAlpha(ShadowInk, 0.10f) : Color.clear;
                }

                Fill();
            }

            private void Turn(int by)
            {
                page += by;
                Fill();
            }

            private void Fill()
            {
                if (companion == null)
                    return;

                var items = Rows();
                var pageCount = Math.Max(1, (items.Count + RowsPerPage - 1) / RowsPerPage);
                page = Math.Max(0, Math.Min(page, pageCount - 1));
                for (var i = 0; i < rows.Count; i++)
                {
                    var index = page * RowsPerPage + i;
                    var item = index < items.Count ? items[index] : null;
                    // Only the photos on the page shown are drawn.
                    rows[i].Show(item, item?.Photo == null ? null : Picture(item.Photo));
                }

                newer.interactable = page > 0;
                older.interactable = page < pageCount - 1;
            }

            private List<JournalRow> Rows()
            {
                switch (tab)
                {
                    case 1: return WeekRows();
                    case 2: return HardDayRows();
                    case 3: return StickerRows();
                    default: return MomentRows();
                }
            }

            // Everything but the week pages, newest first.
            private List<JournalRow> MomentRows()
            {
                var list = new List<JournalRow>();
                var entries = record.Entries;
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    switch (entry.Kind)
                    {
                        case JournalKind.Arrival:
                            list.Add(new JournalRow("Home", Peach, $"Day 1 · {companion} came home",
                                $"{Date(entry.At)} · Welcome to the family!"));
                            break;
                        case JournalKind.Milestone:
                            list.Add(new JournalRow("Growth", Mint, $"{DayWord(entry.Day)} · {MilestoneTitle(entry.Skill)}",
                                $"{Date(entry.At)} · {MilestoneDetail(entry.Skill)}"));
                            break;
                        case JournalKind.Photo:
                            list.Add(PhotoRow(entry));
                            break;
                        case JournalKind.Sticker:
                            list.Add(StickerRow(entry));
                            break;
                        case JournalKind.HardDay:
                            list.Add(HardDayRow(entry));
                            break;
                        case JournalKind.Helper:
                            list.Add(HelperRow(entry));
                            break;
                        case JournalKind.FirstWords:
                            list.Add(new JournalRow("Talk", Butter, $"{DayWord(entry.Day)} · First words!",
                                $"{Date(entry.At)} · {companion} said their first words"));
                            break;
                        case JournalKind.Talk:
                            list.Add(new JournalRow("Talk", Lilac, $"{DayWord(entry.Day)} · Talk time",
                                TalkJournalLine(companion, entry.Feeling)));
                            break;
                        case JournalKind.BigTalk:
                            list.Add(new JournalRow("Talk", Mist, $"{DayWord(entry.Day)} · We talked about something big",
                                Date(entry.At)));
                            break;
                    }
                }

                return list;
            }

            // This week so far, then each week's page, newest first.
            private List<JournalRow> WeekRows()
            {
                var list = new List<JournalRow>();
                var week = record.Weeks + 1;
                var firstDay = record.Weeks * JournalRules.DaysPerWeek + 1;
                var soFar = Math.Max(0, (bonding?.TotalCareDays ?? 0) - record.WeekStartCareDays);
                list.Add(new JournalRow("Routine", Sky, $"This week · Days {firstDay} to {firstDay + 6}",
                    WeekDetail(soFar, firstDay, firstDay + 6, true)));
                var entries = record.Entries;
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    if (entry.Kind != JournalKind.Week || entry.Count >= week)
                        continue;
                    var last = entry.Count * JournalRules.DaysPerWeek;
                    list.Add(new JournalRow("Journal", Lilac, $"Week {entry.Count} · Days {last - 6} to {last}",
                        WeekDetail(entry.CareDays, last - 6, last, false)));
                }

                return list;
            }

            private string WeekDetail(int careDays, int fromDay, int toDay, bool soFar)
            {
                var photos = 0;
                var milestones = 0;
                var hard = 0;
                foreach (var entry in record.Entries)
                {
                    if (entry.Day < fromDay || entry.Day > toDay)
                        continue;
                    photos += entry.Kind == JournalKind.Photo ? 1 : 0;
                    milestones += entry.Kind == JournalKind.Milestone || entry.Kind == JournalKind.FirstWords ? 1 : 0;
                    hard += entry.Kind == JournalKind.HardDay ? 1 : 0;
                }

                var parts = new List<string> { Count(careDays, "care day") + (soFar ? " so far" : "") };
                if (milestones > 0)
                    parts.Add(Count(milestones, "milestone"));
                if (photos > 0)
                    parts.Add(Count(photos, "photo"));
                if (hard > 0)
                    parts.Add(Count(hard, "hard day") + " got through");
                return string.Join(" · ", parts);
            }

            private List<JournalRow> HardDayRows()
            {
                var list = new List<JournalRow>();
                var entries = record.Entries;
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    if (entries[i].Kind == JournalKind.HardDay)
                        list.Add(HardDayRow(entries[i]));
                }

                if (record.InSpell)
                    list.Insert(0, new JournalRow("Heart", Blush, $"Since {DayWord(record.SpellDay).ToLowerInvariant()} · Feeling poorly",
                        petPages ? $"Rest, gentle petting and {VetName} the vet help. You'll get through it together."
                            : "Rest, cuddles and Dr. Lee help. You'll get through it together."));
                if (list.Count == 0)
                    list.Add(new JournalRow("Heart", Blush, "No hard days so far",
                        "If one comes, you'll get through it together."));
                return list;
            }

            // The next sticker, the stickers earned (kept forever), and the looks a rhythm earns.
            private List<JournalRow> StickerRows()
            {
                var list = new List<JournalRow>();
                var longest = bonding?.LongestRhythm ?? 0;
                var rhythm = bonding?.Rhythm ?? 0;
                var next = BondRules.NextSticker(longest);
                list.Add(new JournalRow("Star", Mist, $"Next sticker at {next} days",
                    $"Rhythm {rhythm} · best {longest} · {Count(bonding?.TotalCareDays ?? 0, "care day")} in all"));
                var stickers = BondRules.Stickers(longest);
                for (var i = stickers.Count - 1; i >= 0; i--)
                    list.Add(new JournalRow("Star", Butter, $"{stickers[i]}-day care rhythm", StickerEarnedOn(stickers[i])));
                // Earned looks are the baby's outfits; a pet's rhythm earns stickers.
                if (petPages)
                    return list;
                foreach (var (look, days) in new[]
                {
                    (HatName(Headwear.DaisyClip), EarnedLooks.RhythmFor(Headwear.DaisyClip)),
                    ("Midnight outfit", EarnedLooks.RhythmFor(Outfit.Midnight)),
                    (HatName(Headwear.BucketHat), EarnedLooks.RhythmFor(Headwear.BucketHat)),
                })
                {
                    var earned = longest >= days;
                    list.Add(new JournalRow(earned ? "Style" : "Lock", earned ? FernSoft : Mist, look,
                        earned ? "Earned · find it in Style" : $"Earned with a {days}-day care rhythm"));
                }

                return list;
            }

            private string StickerEarnedOn(int days)
            {
                foreach (var entry in record.Entries)
                {
                    if (entry.Kind == JournalKind.Sticker && entry.Count == days)
                        return $"Earned on {DayWord(entry.Day).ToLowerInvariant()} · kept forever";
                }

                return "Earned · kept forever";
            }

            private static JournalRow PhotoRow(JournalEntry entry) =>
                new JournalRow("Photo", Sky, $"{DayWord(entry.Day)} · {MomentTitle(entry.Moment)}", $"{Date(entry.At)} · a photo",
                    entry);

            // A photo's picture: the pose of its stage, resting for a nap or a poorly day, in the look it kept.
            private Sprite Picture(JournalEntry entry)
            {
                if (petPages)
                {
                    try
                    {
                        return petPicture?.Invoke(entry);
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error);
                        return null;
                    }
                }

                var stage = entry.Skill == MotorSkill.Walking ? 3 : entry.Skill == MotorSkill.Crawling ? 2
                    : entry.Skill == MotorSkill.Sitting ? 1 : 0;
                var rest = entry.Moment == PhotoMoment.Napping || entry.Moment == PhotoMoment.Poorly;
                try
                {
                    return photoSprite?.Invoke(entry.Look, stage, rest);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    return null;
                }
            }

            private JournalRow StickerRow(JournalEntry entry)
            {
                var look = petPages ? null : EarnedLookAt(entry.Count);
                return new JournalRow("Star", Butter, $"{DayWord(entry.Day)} · {entry.Count}-day care rhythm",
                    look == null ? "A sticker for the shelf!" : $"A sticker, and the {look} to wear");
            }

            private JournalRow HardDayRow(JournalEntry entry)
            {
                var days = entry.SinceDay > 0 && entry.SinceDay < entry.Day
                    ? $"Days {entry.SinceDay} to {entry.Day}" : DayWord(entry.Day);
                if (entry.Spell == HardSpell.Shy)
                    return new JournalRow("Heart", Blush, $"{days} · A bit shy about {SkillGerund(entry.Skill)}",
                        $"A good day of meals and {(petPages ? "petting" : "cuddles")} brought {SkillGerund(entry.Skill)} back.");

                return new JournalRow("Heart", Blush, $"{days} · A poorly spell",
                    petPages ? "Rest, medicine and gentle petting helped. All better now!"
                        : "Rest, medicine and cuddles helped. All better now!");
            }

            private static string MilestoneTitle(MotorSkill skill)
            {
                switch (skill)
                {
                    case MotorSkill.Sitting: return "Sat up for the first time";
                    case MotorSkill.Crawling: return "First crawl";
                    case MotorSkill.Pounce: return "First pounce";
                    case MotorSkill.Climb: return "First climb";
                    case MotorSkill.HighJump: return "First high jump";
                    default: return "First steps";
                }
            }

            private static string MilestoneDetail(MotorSkill skill)
            {
                switch (skill)
                {
                    case MotorSkill.Sitting: return "sitting and reaching";
                    case MotorSkill.Crawling: return "off to explore";
                    case MotorSkill.Pounce: return "wiggle, wiggle, pounce!";
                    case MotorSkill.Climb: return "up the cat tree";
                    case MotorSkill.HighJump: return "all the way to the windowsill";
                    default: return "toddling towards a toy";
                }
            }

            private static string DayWord(int day) => $"Day {day}";

            // A helper day, muted: the Sitter (a baby) or the helpers (a pet) kept things steady. Never a milestone.
            private JournalRow HelperRow(JournalEntry entry)
            {
                var parts = new List<string>();
                if (petPages)
                {
                    if (entry.Feeds > 0)
                        parts.Add(Count(entry.Feeds, "feeder portion"));
                    if (entry.Plays > 0)
                        parts.Add("ball launcher " + Times(entry.Plays));
                    if (entry.Tidies > 0)
                        parts.Add("litter box " + Times(entry.Tidies));
                    return new JournalRow("Gear", Mist, $"{DayWord(entry.Day)} · The helpers kept things steady",
                        $"{Date(entry.At)} · {string.Join(" · ", parts)}");
                }

                if (entry.Feeds > 0)
                    parts.Add(Count(entry.Feeds, "bottle"));
                if (entry.Plays > 0)
                    parts.Add("peekaboo " + Times(entry.Plays));
                if (entry.Tidies > 0)
                    parts.Add("freshened up " + Times(entry.Tidies));
                return new JournalRow("Home", Mist, $"{DayWord(entry.Day)} · The Sitter popped in",
                    $"{Date(entry.At)} · {string.Join(" · ", parts)}");
            }

            private static string Times(int count) => count == 1 ? "once" : count == 2 ? "twice" : $"{count} times";

            private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

            private static string Date(DateTimeOffset at) => at.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture);
        }

        // One journal row's words and picture: a photo's entry, or an icon on a tinted disc.
        private sealed class JournalRow
        {
            public JournalRow(string icon, Color tint, string title, string detail, JournalEntry photo = null)
            {
                Icon = icon;
                Tint = tint;
                Title = title;
                Detail = detail;
                Photo = photo;
            }

            public string Icon { get; }
            public JournalEntry Photo { get; }
            public Color Tint { get; }
            public string Title { get; }
            public string Detail { get; }
        }

        // A row of the journal list: a soft card with a round picture, a title and a quiet line.
        private sealed class JournalRowView
        {
            private readonly GameObject root;
            private readonly Image disc;
            private readonly Image picture;
            private readonly Image icon;
            private readonly Text title;
            private readonly Text detail;

            public JournalRowView(Transform parent, int index, float top, float bottom)
            {
                var rect = Box("Journal Row " + (index + 1), parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                PlaceFromTop(rect, 0, 1, top, bottom);
                root = rect.gameObject;
                SoftShapes.Radius(Surface(rect, Mist), Px(18));
                var circle = Box("Journal Row Disc", rect, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(10, -32), new Vector2(74, 32));
                disc = Surface(circle, Paper);
                SoftShapes.Disc(disc);
                picture = Surface(Box("Journal Row Photo", circle, Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-4, -2)),
                    Color.white);
                picture.preserveAspect = true;
                icon = IconAt("Journal Row Icon", circle, "Star", Ink, new Vector2(0.5f, 0.5f), Vector2.zero, 32);
                title = Label("Journal Row Title", rect, "", 22, Ink, TextAnchor.MiddleLeft, new Vector2(88, 38),
                    Vector2.one, new Vector2(-14, -6), FontStyle.Bold);
                FitText(title, 15, 22);
                detail = Label("Journal Row Detail", rect, "", 19, Muted, TextAnchor.MiddleLeft, new Vector2(88, 6),
                    Vector2.right, new Vector2(-14, 40));
                FitText(detail, 13, 19);
            }

            public void Show(JournalRow row, Sprite photo)
            {
                root.SetActive(row != null);
                if (row == null)
                    return;

                disc.color = row.Tint;
                var hasPicture = photo != null;
                picture.gameObject.SetActive(hasPicture);
                picture.sprite = photo;
                icon.gameObject.SetActive(!hasPicture);
                if (!hasPicture)
                {
                    icon.sprite = Icon(row.Icon);
                    icon.enabled = icon.sprite != null;
                }

                title.text = row.Title;
                detail.text = row.Detail;
            }
        }
    }
}
