using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BAIby.Application;
using BAIby.Bootstrap;
using BAIby.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BAIby.Tests
{
    public sealed class NurserySmokeTest
    {
        private Scene previousScene;
        private Scene nurseryScene;
        private string saveKey;

        [UnityTest]
        public IEnumerator NurseryAcceptsCareAndRestoresGrowth()
        {
            previousScene = SceneManager.GetActiveScene();
            const string scenePath = "Assets/Scenes/Nursery";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            nurseryScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.That(nurseryScene.path, Is.EqualTo(scenePath + ".unity"));
            Assert.That(SceneManager.SetActiveScene(nurseryScene), Is.True);

            saveKey = "baiby.playmode." + Guid.NewGuid().ToString("N");
            var view = NurseryStartup.StartPreview(saveKey);
            yield return null;

            var newborn = Child<SpriteRenderer>(view.gameObject, "Newborn artwork");
            Assert.That(newborn.gameObject.activeInHierarchy && newborn.enabled, Is.True);
            Assert.That(newborn.sprite, Is.Not.Null);
            Assert.That(Child<Text>(view.gameObject, "Choice Heading").text, Is.EqualTo("Who's joining your home?"));
            Tap(view.gameObject, "Adopt a baby");
            Child<InputField>(view.gameObject, "Name").text = "Mia";
            Tap(view.gameObject, "Start raising");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            var originalAge = Child<Text>(view.gameObject, "Age").text;
            var originalFullness = NeedValue(view.gameObject, "Fullness");
            Tap(view.gameObject, "Decor");
            Assert.That(Child<Text>(view.gameObject, "Sage Selection").gameObject.activeInHierarchy, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Child<Button>(view.gameObject, "Sage").gameObject));
            Tap(view.gameObject, "Peach");
            Assert.That(Child<Text>(view.gameObject, "Peach Selection").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Sage Selection").gameObject.activeInHierarchy, Is.False);
            Assert.That(Child<Text>(view.gameObject, "Age").text, Is.EqualTo(originalAge));
            Assert.That(NeedValue(view.gameObject, "Fullness"), Is.EqualTo(originalFullness));
            yield return new WaitForSecondsRealtime(0.4f);
            var peach = Child<SpriteRenderer>(view.gameObject, "Nursery illustration peach");
            Assert.That(peach.enabled && peach.sprite != null, Is.True);
            Assert.That(peach.color.a, Is.EqualTo(1f).Within(0.01f));
            Tap(view.gameObject, "Close decor");

            // The routine strip shows today's windows (all free on the first day) only while it ends above the
            // tallest pose at this aspect; otherwise the Routine button in the Care tray stands in for it.
            yield return null;
            var routineStrip = Child<RectTransform>(view.gameObject, "Routine Strip");
            var stripCorners = new Vector3[4];
            routineStrip.GetWorldCorners(stripCorners);
            var poseCeiling = view.GetComponentInChildren<NurseryVisuals>().PoseCeilingScreenY;
            var stripShown = routineStrip.gameObject.activeInHierarchy;
            Assert.That(stripShown, Is.EqualTo(stripCorners[0].y >= poseCeiling), "the strip shows exactly when it clears the baby");
            Assert.That(Child<Button>(view.gameObject, "Open routine").gameObject.activeInHierarchy, Is.EqualTo(!stripShown));
            var routineOpener = stripShown ? "Routine Strip" : "Open routine";
            if (stripShown)
            {
                Assert.That(Child<Image>(view.gameObject, "Routine Breakfast").gameObject.activeInHierarchy, Is.True);
                Assert.That(Child<Image>(view.gameObject, "Routine Wash").gameObject.activeInHierarchy, Is.True);
            }
            else
            {
                Assert.That(Child<Button>(view.gameObject, "Open routine").transform.parent,
                    Is.EqualTo(Child<Button>(view.gameObject, "Preview next day").transform.parent),
                    "the Routine button joins the care sheet's bottom row");
            }

            Tap(view.gameObject, routineOpener);
            Assert.That(Child<RectTransform>(view.gameObject, "Routine Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Routine Panel").rect.height,
                Is.LessThanOrEqualTo(Child<RectTransform>(view.gameObject, "Care").rect.height));
            var saveBeforeStyleChoice = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Care style Gentle");
            Assert.That(PlayerPrefs.GetString(saveKey), Is.Not.EqualTo(saveBeforeStyleChoice));
            Tap(view.gameObject, "Care style Standard");
            Tap(view.gameObject, "Away 1 day");
            Assert.That(Child<Button>(view.gameObject, "End away").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Routine Summary").text, Does.Contain("while you're away"));
            Assert.That(Child<Text>(view.gameObject, "Routine Chip State 4").text, Is.EqualTo("Away"), "play is away");
            Tap(view.gameObject, "End away");
            Assert.That(Child<Button>(view.gameObject, "Away 1 day").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "Close routine");
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(Child<Button>(view.gameObject, routineOpener).gameObject), "closing the routine returns to its opener");
            Assert.That(Child<Text>(view.gameObject, "Age").text, Is.EqualTo(originalAge));
            // Away mode advanced the real clock by a moment; the style checks below compare against this.
            originalFullness = NeedValue(view.gameObject, "Fullness");

            var originalNewborn = newborn.sprite;
            var originalSitter = Child<SpriteRenderer>(view.gameObject, "Sitter artwork").sprite;
            Tap(view.gameObject, "Style");
            Assert.That(Child<Image>(view.gameObject, "Skin Warm Selection").gameObject.activeInHierarchy, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(Child<Button>(view.gameObject, "Skin Warm").gameObject));
            var saveBeforeStyle = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Skin Deep");
            Assert.That(Child<Image>(view.gameObject, "Skin Deep Selection").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Image>(view.gameObject, "Skin Warm Selection").gameObject.activeInHierarchy, Is.False);
            Assert.That(PlayerPrefs.GetString(saveKey), Is.Not.EqualTo(saveBeforeStyle));
            Assert.That(newborn.sprite != null && newborn.sprite != originalNewborn, Is.True);
            Assert.That(newborn.sprite.rect.size, Is.EqualTo(originalNewborn.rect.size));
            Tap(view.gameObject, "Style tab Hair");
            Assert.That(Child<RectTransform>(view.gameObject, "Hair Choices").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Skin Choices").gameObject.activeInHierarchy, Is.False);
            Tap(view.gameObject, "Hair Honey");
            Tap(view.gameObject, "Style tab Cut");
            Tap(view.gameObject, "Cut Coils");
            Tap(view.gameObject, "Style tab Outfit");
            // The Midnight outfit is earned with a 7-day care rhythm: its badge says so and a tap only explains.
            Assert.That(Child<Text>(view.gameObject, "Outfit Midnight Badge Label").text, Is.EqualTo("7 days"));
            var saveBeforeEarned = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Outfit Midnight");
            Assert.That(Child<Text>(view.gameObject, "Style Feedback").text, Does.Contain("7-day care rhythm"));
            Assert.That(Child<Image>(view.gameObject, "Outfit Midnight Selection").gameObject.activeInHierarchy, Is.False);
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(saveBeforeEarned));
            Tap(view.gameObject, "Outfit Terracotta");
            Tap(view.gameObject, "Style tab Hat");
            Assert.That(Child<Text>(view.gameObject, "Hat Daisy clip Badge Label").text, Is.EqualTo("3 days"));
            Tap(view.gameObject, "Hat Bear beanie");
            Assert.That(Child<Image>(view.gameObject, "Hat Bear beanie Selection").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Image>(view.gameObject, "Hat None Selection").gameObject.activeInHierarchy, Is.False);
            Assert.That(Child<Text>(view.gameObject, "Age").text, Is.EqualTo(originalAge));
            Assert.That(NeedValue(view.gameObject, "Fullness"), Is.EqualTo(originalFullness));
            Assert.That(Child<Text>(view.gameObject, "Style Feedback").text, Is.Empty);
            // The style tray stays no taller than the care tray, so it never covers more of the baby.
            Assert.That(Child<RectTransform>(view.gameObject, "Style Panel").rect.height,
                Is.LessThanOrEqualTo(Child<RectTransform>(view.gameObject, "Care").rect.height));
            Tap(view.gameObject, "Close style");
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(Child<Button>(view.gameObject, "Style").gameObject));
            Assert.That(Child<Button>(view.gameObject, "A new sound").gameObject.activeInHierarchy, Is.False);
            Tap(view.gameObject, "Feed");
            Assert.That(Child<Button>(view.gameObject, "Mashed banana").interactable, Is.False);
            Assert.That(Child<Button>(view.gameObject, "Soft oats").interactable, Is.False);
            Assert.That(Child<Text>(view.gameObject, "Mashed banana Availability").text, Does.Contain("crawler stage"));
            Tap(view.gameObject, "Milk");
            Assert.That(Child<Text>(view.gameObject, "Feeding Feedback").text, Does.Contain("satisfied after their milk"));
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Child<SpriteRenderer>(view.gameObject, "Nursery milk").enabled, Is.True);
            Tap(view.gameObject, "Close feeding");
            Tap(view.gameObject, "Soothe");
            Assert.That(Reaction(view.gameObject), Does.Contain("relaxes"));
            var initialEnergy = NeedValue(view.gameObject, "Energy");
            Tap(view.gameObject, "Play with rattle");
            Assert.That(Reaction(view.gameObject), Does.Contain("follows the rattle"));
            Tap(view.gameObject, "Play with plush");
            Assert.That(Reaction(view.gameObject), Does.Contain("follows the plush"));
            Assert.That(Child<Text>(view.gameObject, "Favorite Toy").text, Is.Empty);
            Tap(view.gameObject, "Play with plush");
            Assert.That(Child<Text>(view.gameObject, "Favorite Toy").text, Is.EqualTo("Favorite: Plush"));
            // Three journal photos a day, of the moment: the baby has just been playing.
            var saveBeforePhotos = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Take a photo");
            Assert.That(Reaction(view.gameObject), Does.Contain("A photo of Mia playing is in the journal"));
            Assert.That(PlayerPrefs.GetString(saveKey), Is.Not.EqualTo(saveBeforePhotos));
            Tap(view.gameObject, "Take a photo");
            Tap(view.gameObject, "Take a photo");
            Assert.That(Reaction(view.gameObject), Does.Contain("That's today's three"));
            Assert.That(Child<Button>(view.gameObject, "Take a photo").GetComponentInChildren<Text>().text, Is.EqualTo("Photos done"));
            var saveAfterPhotos = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Take a photo");
            Assert.That(Reaction(view.gameObject), Does.Contain("More tomorrow"));
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(saveAfterPhotos), "a fourth photo saves nothing");
            Tap(view.gameObject, "Development");
            Assert.That(Child<Text>(view.gameObject, "Development Age").text, Does.Contain("Newborn"));
            Assert.That(Child<Button>(view.gameObject, "Stack blocks").interactable, Is.False);
            Assert.That(Child<Text>(view.gameObject, "Blocks Availability").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Rhythm Value").text, Is.EqualTo("0 days"));
            Assert.That(Child<Text>(view.gameObject, "Growth Projection Text").text, Does.StartWith("Likely to sit up"));
            Assert.That(Child<Text>(view.gameObject, "Growth Pace Label").text, Is.Not.Empty);
            var saveBeforeReport = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Baby report");
            Assert.That(Child<Text>(view.gameObject, "Report Sitting").text, Does.StartWith("Likely"));
            Assert.That(Child<Text>(view.gameObject, "Report Moving").text, Does.StartWith("Moving · "));
            Assert.That(Child<Text>(view.gameObject, "Report Growth Chart").text, Does.StartWith("Growth chart · "));
            Assert.That(Child<Text>(view.gameObject, "Report Crawling").text, Is.EqualTo("Locked"));
            Assert.That(Child<Text>(view.gameObject, "Report Walking").text, Is.EqualTo("Locked"));
            Assert.That(Child<Text>(view.gameObject, "Report Favorite Toy").text, Does.Contain("Plush"));
            Tap(view.gameObject, "Close baby report");
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(saveBeforeReport));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(Child<Button>(view.gameObject, "Baby report").gameObject));
            var connection = Child<Text>(view.gameObject, "Bond Value");
            var previousConnection = connection.text;
            Tap(view.gameObject, "Read a story");
            Assert.That(Child<Text>(view.gameObject, "Development Feedback").text, Does.Contain("as you read together"));
            Assert.That(connection.text, Is.Not.EqualTo(previousConnection));

            // The journal opens from Growth: the arrival is the first moment, the sticker shelf waits for 3 days.
            Tap(view.gameObject, "Journal");
            Assert.That(Child<RectTransform>(view.gameObject, "Journal Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Journal Heading").text, Is.EqualTo("Mia's journal"));
            Assert.That(Child<Text>(view.gameObject, "Journal Summary").text, Does.Contain("Rhythm 0 days"));
            Assert.That(JournalRow(view.gameObject, 1), Is.EqualTo("Day 1 · Playtime"));
            var photo = Child<RectTransform>(view.gameObject, "Journal Row 1").Find("Journal Row Disc/Journal Row Photo")
                .GetComponent<Image>();
            Assert.That(photo.gameObject.activeSelf && photo.sprite != null, Is.True, "a photo is drawn from the pose art");
            Assert.That(JournalRow(view.gameObject, 4), Does.Contain("Mia came home"));
            Tap(view.gameObject, "Journal tab Stickers");
            Assert.That(JournalRow(view.gameObject, 1), Is.EqualTo("Next sticker at 3 days"));
            Tap(view.gameObject, "Journal tab Hard days");
            Assert.That(JournalRow(view.gameObject, 1), Is.EqualTo("No hard days so far"));
            // The journal sheet stays below the Report's height so the baby's face stays clear.
            Assert.That(Child<RectTransform>(view.gameObject, "Journal Panel").rect.height,
                Is.LessThanOrEqualTo(Child<RectTransform>(view.gameObject, "Report Panel").rect.height));
            Tap(view.gameObject, "Close journal");
            Assert.That(Child<RectTransform>(view.gameObject, "Development Panel").gameObject.activeInHierarchy, Is.True,
                "Done goes back to Growth");
            var connectionAfterStory = connection.text;
            yield return new WaitForSecondsRealtime(0.15f);
            var storybook = Child<SpriteRenderer>(view.gameObject, "Nursery storybook");
            Assert.That(storybook.enabled, Is.True);
            Assert.That(storybook.sprite, Is.Not.Null);
            Tap(view.gameObject, "Done");

            var afterPlay = NeedValue(view.gameObject, "Energy");
            Assert.That(afterPlay, Is.LessThan(initialEnergy));
            Tap(view.gameObject, "Rest");
            Assert.That(Reaction(view.gameObject), Does.Contain("woke up rested"));
            Assert.That(NeedValue(view.gameObject, "Energy"), Is.GreaterThan(afterPlay));

            var initialHealth = NeedValue(view.gameObject, "Health");
            var initialCleanliness = NeedValue(view.gameObject, "Cleanliness");
            Tap(view.gameObject, "Preview next day");
            var beforeBath = NeedValue(view.gameObject, "Cleanliness");
            Assert.That(beforeBath, Is.LessThan(initialCleanliness));
            Assert.That(Reaction(view.gameObject), Does.Contain("could use a fresh diaper"));
            var comfortBeforeBath = NeedValue(view.gameObject, "Comfort");
            var energyBeforeBath = NeedValue(view.gameObject, "Energy");
            Tap(view.gameObject, "Wash up");
            Assert.That(Child<Text>(view.gameObject, "Bath time Effects").text, Does.Contain("Comfort +30"));
            Tap(view.gameObject, "Bath time");
            var beforeChange = NeedValue(view.gameObject, "Cleanliness");
            Assert.That(beforeChange, Is.EqualTo(Mathf.Min(1f, beforeBath + 0.45f)).Within(0.01f));
            Assert.That(NeedValue(view.gameObject, "Comfort"),
                Is.EqualTo(Mathf.Min(1f, comfortBeforeBath + 0.30f)).Within(0.01f));
            Assert.That(NeedValue(view.gameObject, "Energy"),
                Is.EqualTo(Mathf.Max(0f, energyBeforeBath - 0.08f)).Within(0.01f));
            Assert.That(Reaction(view.gameObject), Does.Contain("splashed in the bath"));
            yield return new WaitForSecondsRealtime(0.5f);
            var bath = Child<SpriteRenderer>(view.gameObject, "Nursery bath basket");
            Assert.That(bath.enabled && bath.sprite != null, Is.True);

            var saveBeforeCancel = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Wash up");
            Tap(view.gameObject, "Change diaper");
            yield return null;
            Wipe(view.gameObject, "Diaper spot 1", "Diaper spot 1");
            Assert.That(Child<Button>(view.gameObject, "Finish diaper").interactable, Is.False);
            Tap(view.gameObject, "Cancel diaper");
            Assert.That(NeedValue(view.gameObject, "Cleanliness"), Is.EqualTo(beforeChange));
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(saveBeforeCancel));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(Child<Button>(view.gameObject, "Change diaper").gameObject));
            Tap(view.gameObject, "Close hygiene");

            Tap(view.gameObject, "Wash up");
            Tap(view.gameObject, "Change diaper");
            var saveBeforeQuickChange = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Quick change");
            Assert.That(PlayerPrefs.GetString(saveKey + ".backup"), Is.EqualTo(saveBeforeQuickChange));
            Assert.That(NeedValue(view.gameObject, "Cleanliness"), Is.GreaterThan(beforeChange));
            Assert.That(Reaction(view.gameObject), Does.Contain("is clean and dry"));
            yield return new WaitForSecondsRealtime(0.35f);
            var diaper = Child<SpriteRenderer>(view.gameObject, "Nursery fresh diaper");
            Assert.That(diaper.enabled, Is.True);
            Assert.That(diaper.sprite, Is.Not.Null);

            // Readiness sets the day: never before 0.8x the nominal day 3, always by 1.4x.
            Assert.That(PreviewUntil(view.gameObject, "Early sitter", 4), Is.InRange(3, 5));
            Assert.That(Reaction(view.gameObject), Does.Contain("can sit up now"));
            Tap(view.gameObject, "A new sound");
            Assert.That(Child<Text>(view.gameObject, "New Sound Scene").text, Does.Contain("window"));
            Assert.That(Child<Text>(view.gameObject, "Comfort them Effects").text, Does.Contain("Comfort +16"));
            Assert.That(Child<Text>(view.gameObject, "Explore together Effects").text, Does.Contain("Bond +5"));
            yield return new WaitForSecondsRealtime(0.45f);
            var chime = Child<SpriteRenderer>(view.gameObject, "Nursery chime");
            Assert.That(chime.enabled, Is.True);
            Assert.That(chime.sprite, Is.Not.Null);
            Tap(view.gameObject, "Later");
            Assert.That(Child<Button>(view.gameObject, "A new sound").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "Feed");
            Assert.That(Child<Button>(view.gameObject, "Milk").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "Close feeding");
            var healthBeforeReload = NeedValue(view.gameObject, "Health");
            Assert.That(healthBeforeReload, Is.LessThan(initialHealth));
            Tap(view.gameObject, "Wash up");
            Tap(view.gameObject, "Change diaper");
            yield return null;
            var saveBeforeWiping = PlayerPrefs.GetString(saveKey);
            Assert.That(Child<Button>(view.gameObject, "Finish diaper").interactable, Is.False);
            Wipe(view.gameObject, "Diaper spot 1", "Diaper spot 2");
            Wipe(view.gameObject, "Diaper spot 3", "Diaper spot 3");
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(saveBeforeWiping));
            Tap(view.gameObject, "Finish diaper");
            Assert.That(PlayerPrefs.GetString(saveKey + ".backup"), Is.EqualTo(saveBeforeWiping));
            var cleanlinessBeforeReload = NeedValue(view.gameObject, "Cleanliness");
            var energyBeforeReload = NeedValue(view.gameObject, "Energy");
            UnityEngine.Object.Destroy(view.gameObject);
            yield return null;

            var restored = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Assert.That(Child<Text>(restored.gameObject, "Age").text, Does.Contain("Early sitter"));
            Tap(restored.gameObject, "Decor");
            Assert.That(Child<Text>(restored.gameObject, "Peach Selection").gameObject.activeInHierarchy, Is.True);
            Tap(restored.gameObject, "Close decor");
            Tap(restored.gameObject, "Style");
            Assert.That(Child<Image>(restored.gameObject, "Skin Deep Selection").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Image>(restored.gameObject, "Skin Warm Selection").gameObject.activeSelf, Is.False);
            Assert.That(Child<Image>(restored.gameObject, "Hair Honey Selection").gameObject.activeSelf, Is.True);
            Assert.That(Child<Image>(restored.gameObject, "Cut Coils Selection").gameObject.activeSelf, Is.True);
            Assert.That(Child<Image>(restored.gameObject, "Outfit Terracotta Selection").gameObject.activeSelf, Is.True);
            Assert.That(Child<Image>(restored.gameObject, "Hat Bear beanie Selection").gameObject.activeSelf, Is.True);
            var restoredSitter = Child<SpriteRenderer>(restored.gameObject, "Sitter artwork");
            Assert.That(restoredSitter.sprite != null && restoredSitter.sprite != originalSitter, Is.True);
            Assert.That(restoredSitter.sprite.rect.size, Is.EqualTo(originalSitter.rect.size));
            // Hidden poses (the newborn, for a sitter) are built when they are first shown; until then
            // they keep art of the same size.
            Assert.That(Child<SpriteRenderer>(restored.gameObject, "Newborn artwork").sprite.rect.size,
                Is.EqualTo(originalNewborn.rect.size));
            Tap(restored.gameObject, "Close style");
            Assert.That(NeedValue(restored.gameObject, "Energy"), Is.EqualTo(energyBeforeReload).Within(0.01f));
            Assert.That(NeedValue(restored.gameObject, "Health"), Is.EqualTo(healthBeforeReload).Within(0.01f));
            Assert.That(NeedValue(restored.gameObject, "Cleanliness"), Is.EqualTo(cleanlinessBeforeReload).Within(0.01f));
            Tap(restored.gameObject, "Development");
            Assert.That(Child<Text>(restored.gameObject, "Development Age").text, Does.Contain("Early sitter"));
            Assert.That(Child<Text>(restored.gameObject, "Rhythm Value").text, Does.Contain("day"));
            Assert.That(Child<Text>(restored.gameObject, "Bond Value").text, Is.EqualTo(connectionAfterStory));
            // A sitter's journal has the milestone, celebrated on the day it came.
            Tap(restored.gameObject, "Journal");
            Assert.That(string.Join(" | ", Enumerable.Range(1, 4).Select(row => JournalRow(restored.gameObject, row))),
                Does.Contain("Sat up for the first time"));
            Tap(restored.gameObject, "Close journal");
            Tap(restored.gameObject, "Done");
            Assert.That(Child<Text>(restored.gameObject, "Favorite Toy").text, Is.EqualTo("Favorite: Plush"));
            Tap(restored.gameObject, "Play with plush");
            Assert.That(Reaction(restored.gameObject), Does.Contain("reaches for the plush"));
            Tap(restored.gameObject, "A new sound");
            Tap(restored.gameObject, "Explore together");
            Assert.That(Reaction(restored.gameObject), Does.Contain("explore the sound together"));
            Assert.That(Child<Button>(restored.gameObject, "A new sound").gameObject.activeInHierarchy, Is.False);

            Assert.That(PreviewUntil(restored.gameObject, "Crawler", 8), Is.InRange(6, 10));
            Assert.That(Reaction(restored.gameObject), Does.Contain("can crawl now"));
            yield return new WaitForSecondsRealtime(0.95f);
            var crawling = Child<SpriteRenderer>(restored.gameObject, "Crawler artwork");
            Assert.That(crawling.gameObject.activeInHierarchy && crawling.enabled, Is.True);
            Assert.That(crawling.sprite, Is.Not.Null);
            Tap(restored.gameObject, "Feed");
            var beforeBananaComfort = NeedValue(restored.gameObject, "Comfort");
            Tap(restored.gameObject, "Mashed banana");
            Assert.That(Child<Text>(restored.gameObject, "Feeding Feedback").text, Does.Contain("mashed banana"));
            Assert.That(NeedValue(restored.gameObject, "Comfort"), Is.GreaterThan(beforeBananaComfort));
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Child<SpriteRenderer>(restored.gameObject, "Nursery banana").enabled, Is.True);

            var beforeOatsFullness = NeedValue(restored.gameObject, "Fullness");
            Tap(restored.gameObject, "Soft oats");
            Assert.That(Child<Text>(restored.gameObject, "Feeding Feedback").text, Does.Contain("soft oats"));
            Assert.That(NeedValue(restored.gameObject, "Fullness"), Is.GreaterThan(beforeOatsFullness));
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Child<SpriteRenderer>(restored.gameObject, "Nursery oats").enabled, Is.True);
            Tap(restored.gameObject, "Close feeding");

            Tap(restored.gameObject, "Play with plush");
            Assert.That(Reaction(restored.gameObject), Does.Contain("crawls toward the plush"));

            UnityEngine.Object.Destroy(restored.gameObject);
            yield return null;
            var crawler = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Text>(crawler.gameObject, "Age").text, Does.Contain("Crawler"));
            Assert.That(Child<Button>(crawler.gameObject, "A new sound").gameObject.activeInHierarchy, Is.False);
            Tap(crawler.gameObject, "Decor");
            Assert.That(Child<Text>(crawler.gameObject, "Peach Selection").gameObject.activeInHierarchy, Is.True);
            Tap(crawler.gameObject, "Sage");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Child<SpriteRenderer>(crawler.gameObject, "Nursery illustration").enabled, Is.True);
            Assert.That(Child<SpriteRenderer>(crawler.gameObject, "Nursery illustration peach").enabled, Is.False);
            Tap(crawler.gameObject, "Close decor");

            Assert.That(PreviewUntil(crawler.gameObject, "Early walker", 14), Is.InRange(12, 20));
            Assert.That(Reaction(crawler.gameObject), Does.Contain("can take a few steps now"));
            yield return new WaitForSecondsRealtime(0.55f);
            var walking = Child<SpriteRenderer>(crawler.gameObject, "Walker artwork");
            Assert.That(walking.enabled && walking.sprite != null, Is.True);
            Tap(crawler.gameObject, "Play with plush");
            Assert.That(Reaction(crawler.gameObject), Does.Contain("takes a few steps toward the plush"));

            Tap(crawler.gameObject, "Development");
            Assert.That(Child<Text>(crawler.gameObject, "Towers Value").text, Is.EqualTo("0"));
            var beforeBlocks = PlayerPrefs.GetString(saveKey);
            Tap(crawler.gameObject, "Stack blocks");
            Tap(crawler.gameObject, "Small block");
            Assert.That(Child<Text>(crawler.gameObject, "Blocks Feedback").text, Does.Contain("large block next"));
            Assert.That(Child<Button>(crawler.gameObject, "Finish blocks").interactable, Is.False);
            Tap(crawler.gameObject, "Large block");
            Tap(crawler.gameObject, "Cancel blocks");
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(beforeBlocks));
            Assert.That(Child<Text>(crawler.gameObject, "Towers Value").text, Is.EqualTo("0"));

            Tap(crawler.gameObject, "Stack blocks");
            Assert.That(Child<RectTransform>(crawler.gameObject, "Large tower layer").gameObject.activeSelf, Is.False);
            Tap(crawler.gameObject, "Large block");
            Tap(crawler.gameObject, "Medium block");
            Tap(crawler.gameObject, "Small block");
            Assert.That(Child<RectTransform>(crawler.gameObject, "Small tower layer").gameObject.activeInHierarchy, Is.True);
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(beforeBlocks));
            Tap(crawler.gameObject, "Finish blocks");
            Assert.That(PlayerPrefs.GetString(saveKey + ".backup"), Is.EqualTo(beforeBlocks));
            Assert.That(Child<Text>(crawler.gameObject, "Towers Value").text, Is.EqualTo("1"));
            Assert.That(Child<Text>(crawler.gameObject, "Development Feedback").text, Does.Contain("block tower"));
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.That(Child<SpriteRenderer>(crawler.gameObject, "Nursery block tower").enabled, Is.True);

            UnityEngine.Object.Destroy(crawler.gameObject);
            yield return null;
            var walker = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Text>(walker.gameObject, "Age").text, Does.Contain("Early walker"));
            Tap(walker.gameObject, "Play with plush");
            Assert.That(Reaction(walker.gameObject), Does.Contain("takes a few steps toward the plush"));
            Tap(walker.gameObject, "Development");
            Assert.That(Child<Text>(walker.gameObject, "Towers Value").text, Is.EqualTo("1"));
            Tap(walker.gameObject, "Read a story");
            var expectedBond = Child<Text>(walker.gameObject, "Bond Value").text;
            Tap(walker.gameObject, "Baby report");
            Assert.That(Child<Text>(walker.gameObject, "Report Sitting").text, Is.EqualTo("Unlocked"));
            Assert.That(Child<Text>(walker.gameObject, "Report Crawling").text, Is.EqualTo("Unlocked"));
            Assert.That(Child<Text>(walker.gameObject, "Report Walking").text, Is.EqualTo("Unlocked"));
            Assert.That(Child<Text>(walker.gameObject, "Report Favorite Toy").text, Does.Contain("Plush"));
            Assert.That(Child<Text>(walker.gameObject, "Report Stories").text, Does.Contain("Enjoys story time"));
            Assert.That(Child<Text>(walker.gameObject, "Report Stories").text, Does.Contain("2 stories read"));
            Assert.That(Child<Text>(walker.gameObject, "Report Blocks").text, Is.EqualTo("Towers built · 1"));
            Assert.That(Child<Text>(walker.gameObject, "Report Bond Value").text, Is.EqualTo(expectedBond));
            Tap(walker.gameObject, "Close baby report");
        }

        // Care slice 3 on a fake clock: preview days without care leave Health low, so the baby is under the weather;
        // 15 minutes later in the same visit it is sick; Dr. Lee's visit starts Recovering; two medicine doses in two
        // meal windows make the baby well again.
        [UnityTest]
        public IEnumerator SickBabySeesTheDoctorAndRecoversWithMedicine()
        {
            previousScene = SceneManager.GetActiveScene();
            const string scenePath = "Assets/Scenes/Nursery";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            nurseryScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.That(SceneManager.SetActiveScene(nurseryScene), Is.True);

            saveKey = "baiby.playmode." + Guid.NewGuid().ToString("N");
            var clock = new DateTimeOffset(2026, 3, 2, 6, 0, 0, TimeSpan.Zero);
            var view = NurseryStartup.StartPreview(saveKey, () => clock);
            yield return null;
            Adopt(view.gameObject, "Mia");
            Assert.That(Child<Image>(view.gameObject, "Condition Chip").gameObject.activeInHierarchy, Is.False);
            Assert.That(Child<Button>(view.gameObject, "Doctor").gameObject.activeInHierarchy, Is.False);

            for (var day = 1; day <= 4; day++)
            {
                clock = clock.AddMinutes(1);
                Tap(view.gameObject, "Preview next day");
            }

            Assert.That(Child<Text>(view.gameObject, "Condition Chip Label").text, Is.EqualTo("Under the weather"));
            Assert.That(Child<Image>(view.gameObject, "Condition Chip").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Favorite Toy").gameObject.activeInHierarchy, Is.False,
                "the chip takes the favorite toy's place");
            Assert.That(Child<Text>(view.gameObject, "Health Action Caption").text, Is.EqualTo("Free visit"));
            Assert.That(Child<Button>(view.gameObject, "Doctor").transform.parent.name, Is.EqualTo("Care Actions"),
                "a Doctor tile joins the care tiles");
            Assert.That(CareTiles(view.gameObject), Is.EqualTo(8));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(Child<SpriteRenderer>(view.gameObject, "Sniffle drop").enabled, Is.True);

            // Fifteen minutes into the visit, Health below 30 makes the baby sick.
            clock = clock.AddMinutes(21);
            Tap(view.gameObject, "Soothe");
            Assert.That(Child<Text>(view.gameObject, "Condition Chip Label").text, Is.EqualTo("Sick — needs the doctor"));
            Tap(view.gameObject, "Play with rattle");
            Assert.That(Reaction(view.gameObject), Does.Contain("too poorly"));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(Child<SpriteRenderer>(view.gameObject, "Health blanket").enabled, Is.True);
            Assert.That(Child<SpriteRenderer>(view.gameObject, "Fever thermometer").enabled, Is.True);

            Tap(view.gameObject, "Doctor");
            Assert.That(Child<RectTransform>(view.gameObject, "Doctor Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Doctor Panel").rect.height,
                Is.LessThanOrEqualTo(Child<RectTransform>(view.gameObject, "Care").rect.height));
            Assert.That(Child<Text>(view.gameObject, "Doctor Scene").text, Does.Contain("resting in bed"));
            Tap(view.gameObject, "Visit doctor");
            Assert.That(Child<Text>(view.gameObject, "Doctor Scene").text, Does.Contain("needs rest and medicine"));
            Assert.That(Child<Button>(view.gameObject, "Visit doctor").gameObject.activeInHierarchy, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(Child<Button>(view.gameObject, "Close doctor").gameObject));
            Assert.That(Child<Text>(view.gameObject, "Condition Chip Label").text, Is.EqualTo("Recovering · medicine 0 of 2"));
            Tap(view.gameObject, "Close doctor");
            var medicine = Child<Button>(view.gameObject, "Medicine");
            Assert.That(medicine.interactable, Is.False, "medicine waits for a meal");
            Assert.That(Child<Text>(view.gameObject, "Health Action Caption").text, Is.EqualTo("At breakfast 07:00"));

            clock = new DateTimeOffset(2026, 3, 2, 7, 30, 0, TimeSpan.Zero);
            Tap(view.gameObject, "Feed");
            Tap(view.gameObject, "Milk");
            Tap(view.gameObject, "Close feeding");
            Tap(view.gameObject, "Medicine");
            Assert.That(Reaction(view.gameObject), Does.Contain("Dose 1 of 2"));
            Assert.That(Child<Text>(view.gameObject, "Condition Chip Label").text, Is.EqualTo("Recovering · medicine 1 of 2"));
            Assert.That(Child<Button>(view.gameObject, "Medicine").interactable, Is.False, "one dose per meal");

            UnityEngine.Object.Destroy(view.gameObject);
            yield return null;
            var restored = NurseryStartup.StartPreview(saveKey, () => clock);
            yield return null;
            Assert.That(Child<Text>(restored.gameObject, "Condition Chip Label").text,
                Is.EqualTo("Recovering · medicine 1 of 2"), "recovery survives a reload");

            clock = new DateTimeOffset(2026, 3, 2, 14, 30, 0, TimeSpan.Zero);
            Tap(restored.gameObject, "Soothe");
            Tap(restored.gameObject, "Medicine");
            Assert.That(Reaction(restored.gameObject), Does.Contain("Well again"));
            Assert.That(Child<Image>(restored.gameObject, "Condition Chip").gameObject.activeInHierarchy, Is.False);
            Assert.That(Child<Button>(restored.gameObject, "Medicine").gameObject.activeInHierarchy, Is.False);
            Assert.That(CareTiles(restored.gameObject), Is.EqualTo(7), "well again, the care tiles are back to seven");
        }

        [UnityTest]
        public IEnumerator HouseholdAdoptsSwitchesAndRestoresBothBabies()
        {
            previousScene = SceneManager.GetActiveScene();
            const string scenePath = "Assets/Scenes/Nursery";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            nurseryScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.That(SceneManager.SetActiveScene(nurseryScene), Is.True);

            saveKey = "baiby.playmode." + Guid.NewGuid().ToString("N");
            var view = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Button>(view.gameObject, "Back to home").gameObject.activeInHierarchy, Is.False);
            Assert.That(Child<Button>(view.gameObject, "Choice back to home").gameObject.activeInHierarchy, Is.False);
            Tap(view.gameObject, "Adopt a baby");
            Child<InputField>(view.gameObject, "Name").text = "Mia";
            Tap(view.gameObject, "Start raising");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Assert.That(PlayerPrefs.HasKey(saveKey), Is.True, "the first baby is slot 1, the original save key");
            Assert.That(PlayerPrefs.HasKey(saveKey + ".household"), Is.True);
            Assert.That(Child<Button>(view.gameObject, "Home").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Button>(view.gameObject, "Switch companion").gameObject.activeInHierarchy, Is.False);
            Tap(view.gameObject, "Decor");
            Tap(view.gameObject, "Peach");
            Tap(view.gameObject, "Close decor");
            Assert.That(Child<Button>(view.gameObject, "Home").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "Play with plush");
            Tap(view.gameObject, "Play with plush");
            Assert.That(Child<Text>(view.gameObject, "Favorite Toy").text, Is.EqualTo("Favorite: Plush"));
            var miaSave = PlayerPrefs.GetString(saveKey);

            Tap(view.gameObject, "Home");
            Assert.That(Child<RectTransform>(view.gameObject, "Home Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Header").gameObject.activeInHierarchy, Is.False);
            Assert.That(Child<Image>(view.gameObject, "Home card Mia Portrait").sprite, Is.Not.Null);
            Assert.That(Child<Text>(view.gameObject, "Home card Mia Stage").text, Does.StartWith("Newborn"));
            Assert.That(Child<Image>(view.gameObject, "Home card Mia Selection").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Home full").gameObject.activeInHierarchy, Is.False);
            Tap(view.gameObject, "+ Adopt");
            Tap(view.gameObject, "Adopt a baby");
            Assert.That(Child<Button>(view.gameObject, "Back to home").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<InputField>(view.gameObject, "Name").text, Is.Empty);
            Child<InputField>(view.gameObject, "Name").text = "Leo";
            Tap(view.gameObject, "Start raising");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Leo"));
            Assert.That(PlayerPrefs.HasKey(saveKey + ".slot2"), Is.True);
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(miaSave), "adopting a second baby leaves slot 1 alone");
            Assert.That(Child<Text>(view.gameObject, "Favorite Toy").text, Is.Empty);
            Assert.That(Child<Text>(view.gameObject, "Age").text, Does.Contain("Newborn"));
            Tap(view.gameObject, "Decor");
            Assert.That(Child<Text>(view.gameObject, "Sage Selection").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "Close decor");
            Assert.That(SwitchLabel(view.gameObject), Is.EqualTo("Switch to Mia"));

            Tap(view.gameObject, "Switch companion");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Assert.That(Child<Text>(view.gameObject, "Favorite Toy").text, Is.EqualTo("Favorite: Plush"));
            Tap(view.gameObject, "Decor");
            Assert.That(Child<Text>(view.gameObject, "Peach Selection").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "Close decor");
            Tap(view.gameObject, "Home");
            Assert.That(Child<RectTransform>(view.gameObject, "Home full").gameObject.activeInHierarchy, Is.True);
            Assert.That(HasButton(view.gameObject, "+ Adopt"), Is.False, "capacity 2 hides adoption");
            Tap(view.gameObject, "Home card Leo");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Leo"));
            UnityEngine.Object.Destroy(view.gameObject);
            yield return null;

            var restored = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Leo"), "the active companion persists");
            Assert.That(Child<Text>(restored.gameObject, "Favorite Toy").text, Is.Empty);
            Assert.That(SwitchLabel(restored.gameObject), Is.EqualTo("Switch to Mia"));
            Tap(restored.gameObject, "Switch companion");
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Assert.That(Child<Text>(restored.gameObject, "Favorite Toy").text, Is.EqualTo("Favorite: Plush"));
            Tap(restored.gameObject, "Decor");
            Assert.That(Child<Text>(restored.gameObject, "Peach Selection").gameObject.activeInHierarchy, Is.True);
            Tap(restored.gameObject, "Close decor");

            Tap(restored.gameObject, "Home");
            Tap(restored.gameObject, "Say goodbye to Leo");
            Assert.That(Child<RectTransform>(restored.gameObject, "Goodbye Panel").gameObject.activeInHierarchy, Is.True);
            Tap(restored.gameObject, "Keep companion");
            Assert.That(PlayerPrefs.HasKey(saveKey + ".slot2"), Is.True, "keeping a companion changes nothing");
            Tap(restored.gameObject, "Say goodbye to Leo");
            Tap(restored.gameObject, "Continue goodbye");
            Assert.That(PlayerPrefs.HasKey(saveKey + ".slot2"), Is.True, "goodbye needs a second confirmation");
            Tap(restored.gameObject, "Confirm goodbye");
            Assert.That(PlayerPrefs.HasKey(saveKey + ".slot2"), Is.False);
            Assert.That(Child<RectTransform>(restored.gameObject, "Home Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(HasButton(restored.gameObject, "Home card Leo"), Is.False);
            Assert.That(HasButton(restored.gameObject, "+ Adopt"), Is.True, "goodbye frees a room");
            Assert.That(Child<Text>(restored.gameObject, "Home keepsakes").text, Does.Contain("Leo"));
            Tap(restored.gameObject, "Home card Mia");
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Assert.That(Child<Button>(restored.gameObject, "Switch companion").gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator ExtraRoomsRestAndRestoreThroughThePlaytestToggle()
        {
            previousScene = SceneManager.GetActiveScene();
            const string scenePath = "Assets/Scenes/Nursery";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            nurseryScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.That(SceneManager.SetActiveScene(nurseryScene), Is.True);

            saveKey = "baiby.playmode." + Guid.NewGuid().ToString("N");
            var view = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Adopt(view.gameObject, "Mia");
            Tap(view.gameObject, "Home");
            Tap(view.gameObject, "+ Adopt");
            Adopt(view.gameObject, "Leo");
            Tap(view.gameObject, "Home");
            Assert.That(Child<RectTransform>(view.gameObject, "Home full").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Home playtest badge").gameObject.activeInHierarchy, Is.False);

            // The More rooms link sits behind the parental gate, which accepts only the right number.
            Tap(view.gameObject, "More rooms");
            Assert.That(Child<RectTransform>(view.gameObject, "Parental Gate Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Home Panel").gameObject.activeInHierarchy, Is.False);
            var firstPrompt = Child<Text>(view.gameObject, "Gate Prompt").text;
            Assert.That(ParentalGate.AnswerFor(firstPrompt), Is.Not.Null);
            Child<InputField>(view.gameObject, "Gate Answer").text = "0";
            Tap(view.gameObject, "Gate continue");
            Assert.That(Child<RectTransform>(view.gameObject, "Parental Gate Panel").gameObject.activeInHierarchy, Is.True,
                "a wrong answer keeps the gate closed");
            Assert.That(Child<Text>(view.gameObject, "Gate Feedback").text, Is.Not.Empty);
            Assert.That(Child<Text>(view.gameObject, "Gate Prompt").text, Is.Not.EqualTo(firstPrompt), "a wrong answer draws a new number");
            var shownPrompt = Child<Text>(view.gameObject, "Gate Prompt").text;
            Tap(view.gameObject, "Gate cancel");
            Assert.That(Child<RectTransform>(view.gameObject, "Home Panel").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "More rooms");
            Assert.That(Child<Text>(view.gameObject, "Gate Prompt").text, Is.Not.EqualTo(shownPrompt),
                "reopening the gate never repeats the number last shown");
            PassGate(view.gameObject);
            Assert.That(Child<RectTransform>(view.gameObject, "More Rooms Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(ToggleLabel(view.gameObject), Is.EqualTo("Add 2 test rooms"));
            Tap(view.gameObject, "Playtest rooms toggle");
            Assert.That(PlayerPrefs.HasKey(saveKey + ".entitlements"), Is.True, "the playtest grant is cached");
            Assert.That(ToggleLabel(view.gameObject), Is.EqualTo("Remove test rooms"));
            Tap(view.gameObject, "Close more rooms");
            Assert.That(Child<Text>(view.gameObject, "Home playtest badge").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Home full").gameObject.activeInHierarchy, Is.False);

            Tap(view.gameObject, "+ Adopt");
            Adopt(view.gameObject, "Ivy");
            Assert.That(PlayerPrefs.HasKey(saveKey + ".slot3"), Is.True, "a third baby takes slot 3");

            // Restore Purchases lives in the household settings, behind the gate.
            Tap(view.gameObject, "Home");
            Tap(view.gameObject, "Household settings");
            Assert.That(Child<RectTransform>(view.gameObject, "Household Settings Panel").gameObject.activeInHierarchy, Is.False);
            PassGate(view.gameObject);
            Tap(view.gameObject, "Restore purchases");
            Assert.That(Child<Text>(view.gameObject, "Settings Feedback").text, Is.EqualTo("Your purchases are up to date."));

            // Revoking the test rooms works like a refund: Ivy rests, read-only, and her save is untouched.
            Tap(view.gameObject, "Settings more rooms");
            var ivySave = PlayerPrefs.GetString(saveKey + ".slot3");
            Tap(view.gameObject, "Playtest rooms toggle");
            Assert.That(Child<Text>(view.gameObject, "More Rooms Feedback").text, Does.Contain("resting"));
            Tap(view.gameObject, "Close more rooms");
            Assert.That(Child<RectTransform>(view.gameObject, "Home card Ivy Resting").gameObject.activeInHierarchy, Is.True);
            Assert.That(HasObject(view.gameObject, "Home card Leo Resting"), Is.False);
            Assert.That(HasButton(view.gameObject, "Say goodbye to Ivy"), Is.False, "a resting companion is read-only");
            Assert.That(HasButton(view.gameObject, "+ Adopt"), Is.False);
            Assert.That(Child<RectTransform>(view.gameObject, "Home full").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Home playtest badge").gameObject.activeInHierarchy, Is.False,
                "no playtest grant is left once the test rooms are removed");

            // Saying goodbye to an awake baby while another rests hands the room over, and the copy says so.
            Tap(view.gameObject, "Say goodbye to Leo");
            Tap(view.gameObject, "Continue goodbye");
            Assert.That(Child<Text>(view.gameObject, "Goodbye Detail").text, Does.Contain("Leo's room will go to Ivy, who wakes up"));
            Tap(view.gameObject, "Keep companion");
            Assert.That(Child<RectTransform>(view.gameObject, "Home card Ivy Resting").gameObject.activeInHierarchy, Is.True,
                "keeping Leo leaves Ivy resting");
            Tap(view.gameObject, "Home card Ivy");
            Assert.That(Child<RectTransform>(view.gameObject, "Journal Panel").gameObject.activeInHierarchy, Is.True,
                "a resting companion's journal opens instead of the room");
            Assert.That(Child<Text>(view.gameObject, "Journal Heading").text, Is.EqualTo("Ivy's journal"));
            Assert.That(Child<Text>(view.gameObject, "Journal Status").text, Does.Contain("resting"));
            Tap(view.gameObject, "Close journal");
            Tap(view.gameObject, "Close home");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Mia"), "a resting companion is never active");
            Assert.That(PlayerPrefs.GetString(saveKey + ".slot3"), Is.EqualTo(ivySave), "a resting companion makes no writes");
            UnityEngine.Object.Destroy(view.gameObject);
            yield return null;

            var restored = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Mia"), "Ivy still rests after a restart");
            Assert.That(PlayerPrefs.GetString(saveKey + ".slot3"), Is.EqualTo(ivySave), "a restart makes no resting writes");
            Tap(restored.gameObject, "Home");
            Assert.That(Child<RectTransform>(restored.gameObject, "Home card Ivy Resting").gameObject.activeInHierarchy, Is.True);

            // The player chooses who stays awake.
            Tap(restored.gameObject, "Wake Ivy");
            Assert.That(Child<RectTransform>(restored.gameObject, "Wake Panel").gameObject.activeInHierarchy, Is.True);
            Tap(restored.gameObject, "Rest Leo instead");
            Assert.That(Child<RectTransform>(restored.gameObject, "Home card Leo Resting").gameObject.activeInHierarchy, Is.True);
            Assert.That(HasObject(restored.gameObject, "Home card Ivy Resting"), Is.False);

            // Restoring the rooms wakes everyone.
            Tap(restored.gameObject, "More rooms");
            PassGate(restored.gameObject);
            Tap(restored.gameObject, "Playtest rooms toggle");
            Tap(restored.gameObject, "Close more rooms");
            Assert.That(HasObject(restored.gameObject, "Home card Leo Resting"), Is.False);
            Assert.That(HasObject(restored.gameObject, "Home card Ivy Resting"), Is.False);
            Tap(restored.gameObject, "Home card Leo");
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Leo"));
            Tap(restored.gameObject, "Home");
            Tap(restored.gameObject, "Home card Ivy");
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Ivy"));
        }

        [UnityTest]
        public IEnumerator HouseholdAdoptsACatBesideABabyAndRestoresIt()
        {
            previousScene = SceneManager.GetActiveScene();
            const string scenePath = "Assets/Scenes/Nursery";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            nurseryScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.That(SceneManager.SetActiveScene(nurseryScene), Is.True);

            saveKey = "baiby.playmode." + Guid.NewGuid().ToString("N");
            var view = NurseryStartup.StartPreview(saveKey);
            yield return null;

            // Who's joining your home? A pet: only the cat has arrived, the others are dimmed.
            Tap(view.gameObject, "Adopt a pet");
            Assert.That(Child<RectTransform>(view.gameObject, "Pet Adoption Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Button>(view.gameObject, "Species Dog").interactable, Is.False, "an unshipped species is dimmed");
            Assert.That(Child<Text>(view.gameObject, "Species Dog Note").text, Is.EqualTo("Arriving soon"));
            Tap(view.gameObject, "Species Cat");

            // Locked breeds can be previewed but not chosen; the free starter can.
            Tap(view.gameObject, "Breed Tabby");
            Assert.That(Child<Button>(view.gameObject, "Breed next").interactable, Is.False, "an earned breed waits for its goal");
            Assert.That(Child<Text>(view.gameObject, "Pet breed detail").text, Does.Contain("full care day"));
            Tap(view.gameObject, "Breed Bengal");
            Assert.That(Child<Text>(view.gameObject, "Breed Bengal Status").text, Is.EqualTo("Premium"));
            Assert.That(Child<Button>(view.gameObject, "Breed next").interactable, Is.False, "a premium breed is not owned on the Web");
            Tap(view.gameObject, "Breed Domestic shorthair");
            Tap(view.gameObject, "Coat Gray");
            var preview = Child<SpriteRenderer>(view.gameObject, "Pet awake artwork");
            Assert.That(preview.gameObject.activeInHierarchy && preview.enabled && preview.sprite != null, Is.True,
                "the breed picker previews the cat in the room");
            Assert.That(Child<SpriteRenderer>(view.gameObject, "Newborn artwork").gameObject.activeInHierarchy, Is.False,
                "the baby steps aside for the pet preview");
            Tap(view.gameObject, "Breed next");
            Child<InputField>(view.gameObject, "Pet Name").text = "Pip";
            Tap(view.gameObject, "Adopt pet");

            // The cat's room: slot 1 stays free for a baby.
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Pip"));
            Assert.That(Child<Text>(view.gameObject, "Age").text, Does.Contain("Kitten"));
            Assert.That(PlayerPrefs.HasKey(saveKey), Is.False, "a pet never takes slot 1");
            Assert.That(PlayerPrefs.GetString(saveKey + ".slot2"), Does.StartWith("<pet"));
            Assert.That(Child<RectTransform>(view.gameObject, "Pet Care Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<RectTransform>(view.gameObject, "Fullness").gameObject.activeInHierarchy, Is.False);
            Assert.That(Child<RectTransform>(view.gameObject, "Pet need 1").Find("Label").GetComponent<Text>().text,
                Is.EqualTo("Fullness"), "the room shows the cat's own meters");
            var fullness = NeedValue(view.gameObject, "Pet need 1");
            Tap(view.gameObject, "Pet care Feed");
            Assert.That(NeedValue(view.gameObject, "Pet need 1"), Is.GreaterThan(fullness));
            Assert.That(Child<Text>(view.gameObject, "Pet Reaction").text, Does.Contain("Pip"));
            Tap(view.gameObject, "Pet care Pet");
            Tap(view.gameObject, "Pet care Nap");
            Assert.That(Child<Button>(view.gameObject, "Home").gameObject.activeInHierarchy, Is.True);

            // Home shows the cat with its species glyph and stage; a baby joins beside it.
            Tap(view.gameObject, "Home");
            Assert.That(Child<Text>(view.gameObject, "Home card Pip Stage").text, Does.StartWith("Kitten"));
            Assert.That(HasObject(view.gameObject, "Home card Pip Species"), Is.True);
            Assert.That(Child<Image>(view.gameObject, "Home card Pip Portrait").sprite, Is.Not.Null);
            Tap(view.gameObject, "+ Adopt");
            Adopt(view.gameObject, "Mia");
            Assert.That(PlayerPrefs.HasKey(saveKey), Is.True, "the baby takes slot 1");
            Assert.That(SwitchLabel(view.gameObject), Is.EqualTo("Switch to Pip"));
            Tap(view.gameObject, "Home");
            Assert.That(HasButton(view.gameObject, "+ Adopt"), Is.False, "a baby and a cat fill the two free rooms");
            UnityEngine.Object.Destroy(view.gameObject);
            yield return null;

            var restored = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Tap(restored.gameObject, "Switch companion");
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Pip"), "the cat reopens in its room");
            Assert.That(Child<RectTransform>(restored.gameObject, "Pet Care Panel").gameObject.activeInHierarchy, Is.True);
            for (var day = 0; day < 7; day++)
                Tap(restored.gameObject, "Pet preview next day");
            Assert.That(Child<Text>(restored.gameObject, "Age").text, Does.Contain("Grown cat"));
            Assert.That(Child<Text>(restored.gameObject, "Pet Reaction").text, Does.Contain("grown up"));

            Tap(restored.gameObject, "Home");
            Tap(restored.gameObject, "Say goodbye to Pip");
            Tap(restored.gameObject, "Continue goodbye");
            Tap(restored.gameObject, "Confirm goodbye");
            Assert.That(PlayerPrefs.HasKey(saveKey + ".slot2"), Is.False);
            Assert.That(Child<Text>(restored.gameObject, "Home keepsakes").text, Does.Contain("Pip (cat"));
        }

        // Care slice 7 on a fake clock: the cat's routine strip, breakfast on time, Next day without care until the
        // cat is under the weather, sick fifteen minutes into the visit, the vet's visit, two medicine doses in two
        // meal windows, a milestone in the bubble and in the journal, and Growth's projection.
        [UnityTest]
        public IEnumerator CatKeepsItsRoutineSeesTheVetAndPounces()
        {
            previousScene = SceneManager.GetActiveScene();
            const string scenePath = "Assets/Scenes/Nursery";
            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            nurseryScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.That(SceneManager.SetActiveScene(nurseryScene), Is.True);

            saveKey = "baiby.playmode." + Guid.NewGuid().ToString("N");
            var clock = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
            var view = NurseryStartup.StartPreview(saveKey, () => clock);
            yield return null;
            Tap(view.gameObject, "Adopt a pet");
            Tap(view.gameObject, "Species Cat");
            Tap(view.gameObject, "Breed Domestic shorthair");
            Tap(view.gameObject, "Breed next");
            Child<InputField>(view.gameObject, "Pet Name").text = "Pip";
            Tap(view.gameObject, "Adopt pet");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Pip"));
            Assert.That(HasObject(view.gameObject, "Routine Breakfast") && HasObject(view.gameObject, "Routine Play") &&
                HasObject(view.gameObject, "Routine Dinner") && HasObject(view.gameObject, "Routine Nap") &&
                HasObject(view.gameObject, "Routine Brush"), Is.True, "the strip shows the cat's windows");
            Assert.That(Child<Button>(view.gameObject, "Take a pet photo").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Button>(view.gameObject, "Pet growth").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Button>(view.gameObject, "Vet").gameObject.activeInHierarchy, Is.False, "a well cat needs no vet");

            // Two days later, breakfast is on time (the first day is excused).
            clock = new DateTimeOffset(2026, 3, 4, 8, 30, 0, TimeSpan.Zero);
            var bubbles = new List<string>();
            Tap(view.gameObject, "Pet care Feed");
            bubbles.Add(Child<Text>(view.gameObject, "Pet Reaction").text);
            Assert.That(bubbles[0], Does.Contain("right on time"));

            // Next day without care: the cat's needs fall until it is under the weather.
            for (var day = 0; day < 8 && Child<Image>(view.gameObject, "Condition Chip").gameObject.activeInHierarchy == false; day++)
            {
                clock = clock.AddMinutes(1);
                Tap(view.gameObject, "Pet preview next day");
                bubbles.Add(Child<Text>(view.gameObject, "Pet Reaction").text);
            }

            Assert.That(Child<Text>(view.gameObject, "Condition Chip Label").text, Is.EqualTo("Under the weather"));
            Assert.That(Child<Text>(view.gameObject, "Pet Health Caption").text, Is.EqualTo("Free visit"));
            Assert.That(bubbles.Any(text => text.Contains("pounced")), Is.True, "the first pounce is celebrated");

            // Fifteen minutes into the visit, Health below 30 makes the cat sick: it rests with a thermometer.
            clock = clock.AddMinutes(21);
            Tap(view.gameObject, "Pet care Pet");
            Assert.That(Child<Text>(view.gameObject, "Condition Chip Label").text, Is.EqualTo("Sick — needs the vet"));
            Tap(view.gameObject, "Pet care Wand");
            Assert.That(Child<Text>(view.gameObject, "Pet Reaction").text, Does.Contain("too poorly"));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(Child<SpriteRenderer>(view.gameObject, "Pet thermometer").enabled, Is.True);
            Assert.That(Child<SpriteRenderer>(view.gameObject, "Pet rest artwork").color.a, Is.GreaterThan(0.5f),
                "a sick cat rests");

            Tap(view.gameObject, "Vet");
            Assert.That(Child<RectTransform>(view.gameObject, "Doctor Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Doctor Heading").text, Does.Contain("the vet"));
            Tap(view.gameObject, "Visit vet");
            Assert.That(Child<Text>(view.gameObject, "Doctor Scene").text, Does.Contain("needs rest and medicine"));
            Assert.That(Child<Text>(view.gameObject, "Condition Chip Label").text, Is.EqualTo("Recovering · medicine 0 of 2"));
            Tap(view.gameObject, "Close doctor");
            Assert.That(Child<RectTransform>(view.gameObject, "Pet Care Panel").gameObject.activeInHierarchy, Is.True);
            Tap(view.gameObject, "Pet medicine");
            Assert.That(Child<Text>(view.gameObject, "Pet Reaction").text, Does.Contain("Dose 1 of 2"));
            Assert.That(Child<Button>(view.gameObject, "Pet medicine").interactable, Is.False, "one dose per meal");

            clock = new DateTimeOffset(2026, 3, 4, 18, 30, 0, TimeSpan.Zero);
            Tap(view.gameObject, "Pet care Feed");
            Tap(view.gameObject, "Pet medicine");
            Assert.That(Child<Text>(view.gameObject, "Pet Reaction").text, Does.Contain("Well again"));
            Assert.That(Child<Image>(view.gameObject, "Condition Chip").gameObject.activeInHierarchy, Is.False);

            // Growth projects the next milestone; the journal keeps the pounce.
            Tap(view.gameObject, "Pet growth");
            Assert.That(Child<RectTransform>(view.gameObject, "Development Panel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Growth Projection Text").text, Does.Contain("Likely to").Or.Contain("reached"));
            Assert.That(Child<Button>(view.gameObject, "Read a story").gameObject.activeInHierarchy, Is.False);
            Tap(view.gameObject, "Journal");
            Assert.That(Child<Text>(view.gameObject, "Journal Heading").text, Is.EqualTo("Pip's journal"));
            var titles = Enumerable.Range(1, 4).Select(row => JournalRow(view.gameObject, row)).ToList();
            if (Child<Button>(view.gameObject, "Older journal page").interactable)
                Tap(view.gameObject, "Older journal page");
            titles.AddRange(Enumerable.Range(1, 4).Select(row => JournalRow(view.gameObject, row)));
            Assert.That(titles.Any(title => title.Contains("First pounce")), Is.True, "the pounce is in the journal");
            Tap(view.gameObject, "Close journal");
            Tap(view.gameObject, "Done");
            Assert.That(Child<RectTransform>(view.gameObject, "Pet Care Panel").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);

            if (nurseryScene.IsValid() && nurseryScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(nurseryScene);

            if (saveKey != null)
            {
                foreach (var key in HouseholdKeys(saveKey))
                    PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        // Every key a household derives from its save key: slot 1 is the key itself.
        private static IEnumerable<string> HouseholdKeys(string key)
        {
            for (var slot = 1; slot <= 8; slot++)
            {
                var slotKey = slot == 1 ? key : key + ".slot" + slot;
                yield return slotKey;
                yield return slotKey + ".backup";
            }

            yield return key + ".household";
            yield return key + ".household.backup";
            yield return key + ".entitlements";
            yield return key + ".entitlements.backup";
        }

        private static void Adopt(GameObject view, string name)
        {
            Tap(view, "Adopt a baby");
            Child<InputField>(view, "Name").text = name;
            Tap(view, "Start raising");
            Assert.That(Child<Text>(view, "Baby Name").text, Is.EqualTo(name));
        }

        // Answers the parental gate the way a grown-up would: the number in words, typed as digits.
        private static void PassGate(GameObject view)
        {
            Assert.That(Child<RectTransform>(view, "Parental Gate Panel").gameObject.activeInHierarchy, Is.True);
            var answer = ParentalGate.AnswerFor(Child<Text>(view, "Gate Prompt").text);
            Assert.That(answer, Is.Not.Null);
            Child<InputField>(view, "Gate Answer").text = answer;
            Tap(view, "Gate continue");
            Assert.That(Child<RectTransform>(view, "Parental Gate Panel").gameObject.activeInHierarchy, Is.False);
        }

        private static string ToggleLabel(GameObject view) =>
            Child<Button>(view, "Playtest rooms toggle").GetComponentInChildren<Text>(true).text;

        private static bool HasObject(GameObject view, string name) =>
            view.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == name);

        private static bool HasButton(GameObject view, string name) =>
            view.GetComponentsInChildren<Button>(true).Any(button => button.name == name);

        private static string SwitchLabel(GameObject view) =>
            Child<Button>(view, "Switch companion").GetComponentInChildren<Text>(true).text;

        private static T Child<T>(GameObject view, string name) where T : Component =>
            view.GetComponentsInChildren<T>(true).Single(component => component.name == name);

        private static string Reaction(GameObject view) => Child<Text>(view, "Reaction").text;

        // The title of a journal row (1 to 4) on the page shown.
        private static string JournalRow(GameObject view, int row) =>
            Child<RectTransform>(view, "Journal Row " + row).Find("Journal Row Title").GetComponent<Text>().text;

        // Taps Next day until the header shows the stage and returns its day ("Day 7 · Crawler" is day 7).
        private static int PreviewUntil(GameObject view, string stage, int maxTaps)
        {
            for (var tap = 0; tap < maxTaps; tap++)
            {
                Tap(view, "Preview next day");
                var age = Child<Text>(view, "Age").text;
                if (!age.Contains(stage))
                    continue;

                var start = age.IndexOf("Day ", StringComparison.Ordinal) + 4;
                return int.Parse(age.Substring(start, age.IndexOf(' ', start) - start));
            }

            Assert.Fail($"{stage} did not arrive within {maxTaps} preview days.");
            return -1;
        }

        // A ring meter's fill: Bar is the track, Bar/Fill the radial fill (0 to 1).
        private static float NeedValue(GameObject view, string name) =>
            Child<RectTransform>(view, name).Find("Bar/Fill").GetComponent<Image>().fillAmount;

        private static int CareTiles(GameObject view) =>
            Child<RectTransform>(view, "Care Actions").GetComponentsInChildren<Button>().Length;

        private static void Wipe(GameObject view, string from, string to)
        {
            var mat = Child<RectTransform>(view, "Diaper wipe mat");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, Child<RectTransform>(view, from).position)
            };
            Assert.That(ExecuteEvents.Execute(mat.gameObject, pointer, ExecuteEvents.pointerDownHandler), Is.True);
            if (from != to)
            {
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, Child<RectTransform>(view, to).position);
                Assert.That(ExecuteEvents.Execute(mat.gameObject, pointer, ExecuteEvents.dragHandler), Is.True);
            }
            Assert.That(ExecuteEvents.Execute(mat.gameObject, pointer, ExecuteEvents.pointerUpHandler), Is.True);
        }

        private static void Tap(GameObject view, string name)
        {
            var button = Child<Button>(view, name);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }
    }
}
