using System;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace BAIby.Presentation
{
    // Talk time (docs/care-and-growth.md, First words and Talk time): a Talk tile in the care sheet once First words are
    // reached opens a sheet of feeling chips. When a grown-up allows typing (Grown-ups, behind the parental gate), a
    // field "Tell Mia more…" sits at the top of the sheet, above the chips, so it stays clear of the on-screen keyboard
    // as far as WebGL allows; on a phone the sheet lifts while the field is focused and the stage keeps the baby above it.
    //
    // The typed words go straight to the session, which reads them on the device and keeps nothing; the field is cleared
    // as soon as they are sent, and nothing here logs or stores them. Words about self-harm, being hurt or being unsafe
    // get the fixed calm message in the sheet, with no banter in the bubble.
    public sealed partial class NurseryPreview
    {
        private Button talkButton;
        private TalkPanel talk;

        private static readonly Feeling[] ChipOrder =
        {
            Feeling.Happy, Feeling.Sad, Feeling.Worried, Feeling.Angry, Feeling.Tired, Feeling.Proud, Feeling.Lonely, Feeling.Excited,
        };

        private void BuildTalk()
        {
            talk = new TalkPanel(Tray("Talk Panel", false, TalkPanel.Height), () => ShowPanel(NurseryPanel.Care),
                feeling => Talk(feeling), () => Talk(null));
        }

        // Shown once First words are reached.
        private void UpdateTalkTile(BabyState state)
        {
            if (talkButton != null)
                talkButton.gameObject.SetActive(state != null && state.HasFirstWords);
        }

        private void OpenTalk()
        {
            if (session?.State == null || !session.CanTalk)
                return;

            talk.Open(session.State.Name, session.CanType);
            ShowPanel(NurseryPanel.Talk);
        }

        private void Talk(Feeling? feeling)
        {
            // Read the words once, clear the field at once; they are never kept here.
            var typed = talk.TakeTyped(session.CanType);
            (TalkReply Reply, bool UnlockedSitting, bool UnlockedCrawling, bool UnlockedWalking) result;
            try
            {
                result = session.Talk(feeling, typed);
            }
            catch (Exception error)
            {
                ReportFailure(error, "That couldn't be saved. Please try again.");
                return;
            }

            var reply = result.Reply;
            var name = session.State.Name;
            var unlocked = (result.UnlockedSitting, result.UnlockedCrawling, result.UnlockedWalking);
            var milestone = unlocked.UnlockedSitting || unlocked.UnlockedCrawling || unlocked.UnlockedWalking;
            switch (reply.Outcome)
            {
                case TalkOutcome.SafetyNet:
                    // Only the calm message, in the sheet. The bubble stays empty: no banter this turn.
                    Paint(Reaction.SitsCalmly, unlocked);
                    status.text = string.Empty;
                    talk.ShowSafety(reply.Words);
                    break;
                case TalkOutcome.NeedsFeeling:
                    talk.ShowNote($"Tap a feeling so {name} knows how you feel.");
                    break;
                default:
                    Paint(GestureReaction(reply.Gesture), unlocked);
                    var said = $"“{reply.Words}” {GestureLine(name, reply.Gesture)}";
                    status.text = milestone ? status.text + " " + said : said;
                    talk.ShowReply(name, reply.Words);
                    break;
            }
        }

        private static Reaction GestureReaction(TalkGesture gesture)
        {
            switch (gesture)
            {
                case TalkGesture.Cuddle: return Reaction.Calmed;
                case TalkGesture.Bounce: return Reaction.HappyBounce;
                default: return Reaction.SitsCalmly;
            }
        }

        private static string GestureLine(string name, TalkGesture gesture)
        {
            switch (gesture)
            {
                case TalkGesture.Cuddle: return $"{name} snuggles in close.";
                case TalkGesture.Bounce: return $"{name} does a happy dance.";
                default: return $"{name} sits close beside you.";
            }
        }

        // The journal's words for a talk line: "You told Mia you felt proud. Mia did a happy dance."
        private static string TalkJournalLine(string name, Feeling feeling)
        {
            string did;
            switch (TalkRules.GestureOf(feeling))
            {
                case TalkGesture.Cuddle: did = "gave you a big cuddle"; break;
                case TalkGesture.Bounce: did = "did a happy dance"; break;
                default: did = "sat close beside you"; break;
            }

            return $"You told {name} you felt {FeelingWord(feeling)}. {name} {did}.";
        }

        private static string FeelingWord(Feeling feeling) => feeling.ToString().ToLowerInvariant();

        // While the field is focused on a phone, the sheet rises so the field sits above the on-screen keyboard. WebGL
        // reports no keyboard area, so 40% of the screen is assumed there; the stage keeps the baby above the sheet.
        private void FitTalkSheet()
        {
            if (talk == null || safeArea == null)
                return;

            var lift = 0f;
            if (talk.Root.activeInHierarchy && talk.Typing && UnityEngine.Application.isMobilePlatform && Screen.height > 0)
            {
                var keyboard = TouchScreenKeyboard.area.height;
                if (keyboard <= 0)
                    keyboard = Screen.height * 0.4f;
                var unitsPerPixel = safeArea.rect.height / Mathf.Max(1f, Screen.safeArea.height);
                var keyboardTop = (keyboard - Screen.safeArea.yMin) * unitsPerPixel;
                lift = Mathf.Max(0, keyboardTop + 16 - (SheetBottomPad + TalkPanel.FieldBottom));
                // The sheet's top stays a little past the middle of the screen at most; the stage frames the baby (and its
                // face) in the band above it, as for any open sheet.
                lift = Mathf.Min(lift, safeArea.rect.height * 0.56f - (TalkPanel.Height + SheetChrome));
                lift = Mathf.Max(0, lift);
            }

            talk.Lift(lift);
        }

        // The Talk sheet: the heading and Done, an optional field with Send, eight feeling chips in two rows, and the
        // little one's words (or the safety message) under them.
        private sealed class TalkPanel
        {
            public const float Height = 420;
            // The field row, from the content's bottom.
            public const float FieldBottom = Height - 152;
            private const float FieldTop = Height - 80;

            private readonly RectTransform root;
            private readonly Text heading;
            private readonly InputField field;
            private readonly Button send;
            private readonly Text hint;
            private readonly RectTransform chips;
            private readonly Image replyCard;
            private readonly Text replyName;
            private readonly Text reply;
            private readonly Text safety;
            private string name = "your little one";

            public GameObject Root { get; }
            public Button Done { get; }
            public Button FirstChip { get; }
            public bool Typing => field.gameObject.activeSelf && field.isFocused;

            public TalkPanel(RectTransform content, Action close, Action<Feeling> chip, Action sendTyped)
            {
                Root = content.parent.gameObject;
                root = (RectTransform)content.parent;
                heading = SheetHeading("Talk Heading", content, "Talk time", Height);
                Done = SheetClose("Close talk", "Done", content, Height, close);

                // The field first, near the top of the sheet.
                var box = Box("Talk Field", content, Vector2.zero, new Vector2(0.74f, 0), new Vector2(3, FieldBottom),
                    new Vector2(-3, FieldTop));
                field = box.gameObject.AddComponent<InputField>();
                field.targetGraphic = TextField(box);
                field.characterLimit = TalkRules.MaxTyped;
                field.lineType = InputField.LineType.SingleLine;
                field.textComponent = Label("Talk Typed", box, "", 26, Ink, TextAnchor.MiddleLeft,
                    new Vector2(22, 0), Vector2.one, new Vector2(-22, 0), FontStyle.Bold);
                field.placeholder = Label("Talk Placeholder", box, "Tell your little one more…", 26, Secondary,
                    TextAnchor.MiddleLeft, new Vector2(22, 0), Vector2.one, new Vector2(-22, 0));
                FitText((Text)field.placeholder, 18, 26);
                field.onEndEdit.AddListener(_ =>
                {
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        sendTyped();
                });
                send = ActionButton("Talk send", content, 0.74f, 1, FieldBottom, FieldTop, sendTyped, Fern, Color.white);
                send.GetComponentInChildren<Text>().text = "Send";
                hint = Label("Talk Hint", content, "", 23, Secondary, TextAnchor.MiddleLeft,
                    new Vector2(0, FieldBottom), Vector2.right, new Vector2(0, FieldTop));
                FitText(hint, 16, 23);

                chips = Box("Talk Chips", content, Vector2.zero, Vector2.right, new Vector2(0, 88), new Vector2(0, FieldBottom - 12));
                for (var i = 0; i < ChipOrder.Length; i++)
                {
                    var feeling = ChipOrder[i];
                    var row = i / 4;
                    var column = i % 4;
                    var button = ActionButton("Feeling " + feeling, chips, column / 4f, (column + 1) / 4f, 0, 0,
                        () => chip(feeling), ChipTint(feeling), Ink);
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = new Vector2(column / 4f, row == 0 ? 0.5f : 0);
                    rect.anchorMax = new Vector2((column + 1) / 4f, row == 0 ? 1 : 0.5f);
                    rect.offsetMin = new Vector2(5, row == 0 ? 6 : 0);
                    rect.offsetMax = new Vector2(-5, row == 0 ? 0 : -6);
                    SoftShapes.Pill(button.image);
                    var label = button.GetComponentInChildren<Text>();
                    label.text = feeling.ToString();
                    FitText(label, 17, 25);
                    if (i == 0)
                        FirstChip = button;
                }

                // The little one's words, or a note.
                var card = Box("Talk Reply Card", content, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 78));
                replyCard = Surface(card, Mist);
                SoftShapes.Radius(replyCard, Px(16));
                replyName = Label("Talk Reply Name", card, "", 20, Muted, TextAnchor.MiddleLeft,
                    new Vector2(20, 44), Vector2.one, new Vector2(-20, -4), FontStyle.Bold);
                FitText(replyName, 15, 20);
                reply = Label("Talk Reply", card, "", 26, Ink, TextAnchor.MiddleLeft,
                    new Vector2(20, 6), new Vector2(1, 0), new Vector2(-20, 48), FontStyle.Bold);
                FitText(reply, 17, 26);

                // The safety message takes the chips' place.
                safety = Label("Talk Safety", content, "", 27, Ink, TextAnchor.MiddleLeft,
                    new Vector2(0, 0), Vector2.right, new Vector2(0, FieldBottom - 12), FontStyle.Bold);
                safety.horizontalOverflow = HorizontalWrapMode.Wrap;
                FitText(safety, 18, 27);
                safety.gameObject.SetActive(false);
                Root.SetActive(false);
            }

            // Pastel tints, one per feeling; ink text on each.
            private static Color ChipTint(Feeling feeling)
            {
                switch (feeling)
                {
                    case Feeling.Happy: return Butter;
                    case Feeling.Sad: return Sky;
                    case Feeling.Worried: return Lilac;
                    case Feeling.Angry: return Blush;
                    case Feeling.Tired: return Mist;
                    case Feeling.Proud: return Mint;
                    case Feeling.Lonely: return FernSoft;
                    default: return Peach;
                }
            }

            // A fresh sheet: chips, the field when typing is allowed (empty), and a quiet line.
            public void Open(string companion, bool typing)
            {
                name = companion;
                heading.text = "Talk with " + companion;
                field.text = string.Empty;
                field.gameObject.SetActive(typing);
                send.gameObject.SetActive(typing);
                ((Text)field.placeholder).text = $"Tell {companion} more…";
                hint.gameObject.SetActive(!typing);
                hint.text = $"How do you feel? Tap a feeling to tell {companion}.";
                chips.gameObject.SetActive(true);
                replyCard.gameObject.SetActive(true);
                safety.gameObject.SetActive(false);
                replyName.text = string.Empty;
                reply.text = typing ? "Tap a feeling, or type a few words and Send." : $"{companion} is listening.";
                SetRole(reply, TextRole.Body);
            }

            // The typed words when typing is allowed, and an empty field at once.
            public string TakeTyped(bool allowed)
            {
                var typed = allowed && field.gameObject.activeSelf ? field.text : null;
                field.text = string.Empty;
                field.DeactivateInputField();
                return string.IsNullOrWhiteSpace(typed) ? null : typed;
            }

            public void ShowReply(string companion, string words)
            {
                chips.gameObject.SetActive(true);
                replyCard.gameObject.SetActive(true);
                safety.gameObject.SetActive(false);
                replyName.text = companion + " says";
                reply.text = words;
                SetRole(reply, TextRole.Strong);
            }

            public void ShowNote(string note)
            {
                replyName.text = string.Empty;
                reply.text = note;
                SetRole(reply, TextRole.Body);
            }

            // The fixed calm message in place of the chips and the reply.
            public void ShowSafety(string message)
            {
                chips.gameObject.SetActive(false);
                replyCard.gameObject.SetActive(false);
                safety.gameObject.SetActive(true);
                safety.text = message;
            }

            public void Lift(float units)
            {
                if (Mathf.Approximately(root.offsetMin.y, units))
                    return;
                root.offsetMin = new Vector2(root.offsetMin.x, units);
                root.offsetMax = new Vector2(root.offsetMax.x, Height + SheetChrome + units);
            }
        }
    }
}
