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
                // The clock carries the local offset: elapsed time uses the UTC instant and the offset only
                // places care windows. TODO: WebGL may report UTC here; add a jslib-backed local clock.
                var session = new BabySession(simulation, new PlayerPrefsBabySaveStore(saveKey), () => DateTimeOffset.Now);
                view.Open(session);
            }
            catch (Exception error)
            {
                view.ReportStartupFailure(error);
            }

            return view;
        }
    }
}
