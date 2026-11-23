using System;
using System.Collections;
using System.Linq;
using BAIby.Bootstrap;
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
            Tap(view.gameObject, "Outfit Midnight");
            Tap(view.gameObject, "Style tab Hat");
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
            Tap(view.gameObject, "Development");
            Assert.That(Child<Text>(view.gameObject, "Development Age").text, Does.Contain("Newborn"));
            Assert.That(Child<Button>(view.gameObject, "Stack blocks").interactable, Is.False);
            Assert.That(Child<Text>(view.gameObject, "Blocks Availability").gameObject.activeInHierarchy, Is.True);
            Assert.That(Child<Text>(view.gameObject, "Story Sessions Value").text, Is.EqualTo("0"));
            var saveBeforeReport = PlayerPrefs.GetString(saveKey);
            Tap(view.gameObject, "Baby report");
            Assert.That(Child<Text>(view.gameObject, "Report Sitting").text, Is.EqualTo("Locked"));
            Assert.That(Child<Text>(view.gameObject, "Report Crawling").text, Is.EqualTo("Locked"));
            Assert.That(Child<Text>(view.gameObject, "Report Walking").text, Is.EqualTo("Locked"));
            Assert.That(Child<Text>(view.gameObject, "Report Favorite Toy").text, Does.Contain("Plush"));
            Tap(view.gameObject, "Close baby report");
            Assert.That(PlayerPrefs.GetString(saveKey), Is.EqualTo(saveBeforeReport));
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(Child<Button>(view.gameObject, "Baby report").gameObject));
            var connection = Child<Text>(view.gameObject, "Connection Value");
            var previousConnection = connection.text;
            Tap(view.gameObject, "Read a story");
            Assert.That(Child<Text>(view.gameObject, "Development Feedback").text, Does.Contain("as you read together"));
            Assert.That(Child<Text>(view.gameObject, "Story Sessions Value").text, Is.EqualTo("1"));
            Assert.That(connection.text, Is.Not.EqualTo(previousConnection));
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

            for (var day = 1; day < 3; day++)
                Tap(view.gameObject, "Preview next day");

            Assert.That(Reaction(view.gameObject), Does.Contain("can sit up now"));
            Tap(view.gameObject, "A new sound");
            Assert.That(Child<Text>(view.gameObject, "New Sound Scene").text, Does.Contain("window"));
            Assert.That(Child<Text>(view.gameObject, "Comfort them Effects").text, Does.Contain("Comfort +16"));
            Assert.That(Child<Text>(view.gameObject, "Explore together Effects").text, Does.Contain("Connection +5"));
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
            Assert.That(Child<Image>(restored.gameObject, "Outfit Midnight Selection").gameObject.activeSelf, Is.True);
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
            Assert.That(Child<Text>(restored.gameObject, "Story Sessions Value").text, Is.EqualTo("1"));
            Assert.That(Child<Text>(restored.gameObject, "Connection Value").text, Is.EqualTo(connectionAfterStory));
            Tap(restored.gameObject, "Done");
            Assert.That(Child<Text>(restored.gameObject, "Favorite Toy").text, Is.EqualTo("Favorite: Plush"));
            Tap(restored.gameObject, "Play with plush");
            Assert.That(Reaction(restored.gameObject), Does.Contain("reaches for the plush"));
            Tap(restored.gameObject, "A new sound");
            Tap(restored.gameObject, "Explore together");
            Assert.That(Reaction(restored.gameObject), Does.Contain("explore the sound together"));
            Assert.That(Child<Button>(restored.gameObject, "A new sound").gameObject.activeInHierarchy, Is.False);

            for (var day = 3; day < 6; day++)
                Tap(restored.gameObject, "Preview next day");

            Assert.That(Child<Text>(restored.gameObject, "Age").text, Does.Contain("6 days old  ·  Early sitter"));
            Tap(restored.gameObject, "Preview next day");
            Assert.That(Child<Text>(restored.gameObject, "Age").text, Does.Contain("7 days old  ·  Crawler"));
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

            for (var day = 7; day < 13; day++)
                Tap(crawler.gameObject, "Preview next day");

            Assert.That(Child<Text>(crawler.gameObject, "Age").text, Does.Contain("13 days old  ·  Crawler"));
            Tap(crawler.gameObject, "Preview next day");
            Assert.That(Child<Text>(crawler.gameObject, "Age").text, Does.Contain("14 days old  ·  Early walker"));
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
            var expectedBond = Child<Text>(walker.gameObject, "Connection Value").text;
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

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);

            if (nurseryScene.IsValid() && nurseryScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(nurseryScene);

            if (saveKey != null)
            {
                PlayerPrefs.DeleteKey(saveKey);
                PlayerPrefs.DeleteKey(saveKey + ".backup");
                PlayerPrefs.Save();
            }
        }

        private static T Child<T>(GameObject view, string name) where T : Component =>
            view.GetComponentsInChildren<T>(true).Single(component => component.name == name);

        private static string Reaction(GameObject view) => Child<Text>(view, "Reaction").text;

        private static float NeedValue(GameObject view, string name) =>
            Child<RectTransform>(view, name).Find("Bar/Fill").GetComponent<RectTransform>().anchorMax.x;

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
