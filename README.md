# BAIby

BAIby is a virtual baby raising game. The Android and iOS restart focuses on a believable, stylized 3D baby whose reactions, routines, and abilities change through play and as they grow.

- [Restart roadmap](docs/roadmap.md)
- [Mobile architecture](docs/architecture.md)
- [Regression guardrails](docs/regression.md)
- [Original browser prototype](legacy/BAIby_v5.3.html) — download and open locally in a browser.

Run the baseline checks with Node.js 22 using `node --test tests/legacy.test.mjs`.

## Play in GitHub Codespaces

Open the branch in a Codespace. The preview starts on port **8000** and opens in the browser. If the tab does not open automatically, choose **Open in Browser** for port 8000 in the Codespaces **Ports** panel. Keep the forwarded port private for personal testing. The prototype stores progress in the browser, so use the same preview URL and browser profile to resume a save.

The Codespaces preview runs the archived HTML game. The Unity restart can support browser playtesting once a Unity Web build is available. Android and iOS device checks are still needed for native lifecycle and graphics performance.

## Mobile restart development

The [simulation source](unity/Assets/BAIby/Simulation) is a plain C# assembly at its intended Unity asset path. Run its regression checks with the .NET 8 SDK.

```sh
dotnet run --project tests/Simulation/SimulationChecks.csproj --configuration Release
```

The first rules slice covers adoption, feeding, soothing, toy familiarity, and the sitting and reaching milestone. Milestone time and the offline decay cap are supplied by the caller. The test timing is provisional gameplay tuning. The Unity scene and persistent mobile session are future integration work.
