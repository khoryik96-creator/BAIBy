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
        public IEnumerator NurseryAcceptsCareAndRestoresTheMilestone()
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

            Child<InputField>(view.gameObject, "Name").text = "Mia";
            Tap(view.gameObject, "Start raising");
            Assert.That(Child<Text>(view.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Tap(view.gameObject, "Feed");
            Assert.That(Reaction(view.gameObject), Does.Contain("satisfied"));
            Tap(view.gameObject, "Soothe");
            Assert.That(Reaction(view.gameObject), Does.Contain("relaxes"));
            var initialEnergy = EnergyMeter(view.gameObject);
            Tap(view.gameObject, "Play");
            Assert.That(Reaction(view.gameObject), Does.Contain("follows the toy"));
            var afterPlay = EnergyMeter(view.gameObject);
            Assert.That(afterPlay, Is.LessThan(initialEnergy));
            Tap(view.gameObject, "Rest");
            Assert.That(Reaction(view.gameObject), Does.Contain("woke up rested"));
            Assert.That(EnergyMeter(view.gameObject), Is.GreaterThan(afterPlay));

            for (var day = 0; day < 3; day++)
                Tap(view.gameObject, "Preview next day");

            Assert.That(Reaction(view.gameObject), Does.Contain("can sit up now"));
            var beforeReload = EnergyMeter(view.gameObject);
            UnityEngine.Object.Destroy(view.gameObject);
            yield return null;

            var restored = NurseryStartup.StartPreview(saveKey);
            yield return null;
            Assert.That(Child<Text>(restored.gameObject, "Baby Name").text, Is.EqualTo("Mia"));
            Assert.That(Child<Text>(restored.gameObject, "Age").text, Does.Contain("Early sitter"));
            Assert.That(EnergyMeter(restored.gameObject), Is.EqualTo(beforeReload).Within(0.01f));
            Tap(restored.gameObject, "Play");
            Assert.That(Reaction(restored.gameObject), Does.Contain("reaches for the toy"));
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

        private static float EnergyMeter(GameObject view) =>
            Child<RectTransform>(view, "Energy").Find("Bar/Fill").GetComponent<RectTransform>().anchorMax.x;

        private static void Tap(GameObject view, string name)
        {
            var button = Child<Button>(view, name);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }
    }
}
