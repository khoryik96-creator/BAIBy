# BAIby

BAIby is a virtual baby raising game. The Android and iOS restart focuses on a believable, stylized 3D baby whose reactions, routines, and abilities change through play and as they grow.

- [Restart roadmap](docs/roadmap.md)
- [Mobile architecture](docs/architecture.md)
- [Regression guardrails](docs/regression.md)
- [Nursery playtest checklist](docs/playtest.md)
- [Original browser prototype](legacy/BAIby_v5.3.html) — download and open locally in a browser.

Run the browser baseline and assembly checks with Node.js 22 using `node --test tests/*.test.mjs`.

## Play in GitHub Codespaces

Open the branch in a Codespace. The preview starts on port **8000** and opens in the browser. If the tab does not open automatically, choose **Open in Browser** for port 8000 in the Codespaces **Ports** panel. Keep the forwarded port private for personal testing. The prototype stores progress in the browser, so use the same preview URL and browser profile to resume a save.

The preview opens the archived HTML game at the root. After you export a Unity Web build into `build/Web`, open `/nursery/` on the same forwarded URL. You can also serve both versions locally with `bash .devcontainer/start-preview.sh`. Android and iOS device checks cover native lifecycle and graphics performance.

## Mobile restart development

Open the `unity/` folder with the Editor version listed in [ProjectVersion.txt](unity/ProjectSettings/ProjectVersion.txt). In Unity, open the Nursery scene and enter Play Mode. Adopt a baby, try the three care actions, and use **Preview next day** to exercise the first milestone.

Install the Web Build Support module for the pinned Editor to export a browser build. From the repository root, run:

```sh
Unity -batchmode -quit -projectPath "$PWD/unity" -buildTarget WebGL \
  -executeMethod BAIby.Editor.BuildPreview.BuildWeb -logFile -
```

You can also select **BAIby → Build Browser Preview** in the Editor. An activated Editor is required. The export goes to `build/Web`. Serve it through the preview route above.

The [simulation](unity/Assets/BAIby/Simulation) and [session](unity/Assets/BAIby/Application) compile as plain C#. Run their regression checks with the .NET 8 SDK.

```sh
dotnet run --project tests/Simulation/SimulationChecks.csproj --configuration Release
```

The character and milestone timing are for playtesting. The slice uses Unity’s built-in renderer. The production roadmap targets URP after the first visual review. The local adapter uses Unity PlayerPrefs with a previous valid payload for recovery. Verify persistence on each target before beta.
