using System;
using BAIby.Application;
using BAIby.Infrastructure;
using BAIby.Presentation;
using BAIby.Simulation;
using UnityEngine;

namespace BAIby.Bootstrap
{
    internal static class NurseryStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Open()
        {
            var view = new GameObject("Nursery Preview").AddComponent<NurseryPreview>();
            try
            {
                var simulation = new BabySimulation(TimeSpan.FromDays(3), TimeSpan.FromHours(4));
                var session = new BabySession(simulation, new PlayerPrefsBabySaveStore(), () => DateTimeOffset.UtcNow);
                view.Open(session);
            }
            catch (Exception error)
            {
                view.ReportStartupFailure(error);
            }
        }
    }
}
