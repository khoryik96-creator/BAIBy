using System;
using BAIby.Application;
using BAIby.Infrastructure;
using BAIby.Presentation;
using BAIby.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BAIby.Bootstrap
{
    public static class NurseryStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Open()
        {
            if (SceneManager.GetActiveScene().path == "Assets/Scenes/Nursery.unity")
                StartPreview(PlayerPrefsBabySaveStore.DefaultKey);
        }

        public static NurseryPreview StartPreview(string saveKey)
        {
            var view = new GameObject("Nursery Preview").AddComponent<NurseryPreview>();
            try
            {
                var simulation = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4),
                    TimeSpan.FromDays(7), TimeSpan.FromDays(14));
                // The clock carries the local offset (from the browser on the Web build) and follows zone
                // changes while the app stays alive. Every household key derives from saveKey; slot 1 is
                // saveKey itself, so an existing single-baby save opens as the first companion uncopied.
                var clock = new LocalClock();
                var household = new HouseholdSession(simulation, new PlayerPrefsHouseholdStore(saveKey), clock.Now);
                view.Open(household);
            }
            catch (Exception error)
            {
                view.ReportStartupFailure(error);
            }

            return view;
        }
    }
}
