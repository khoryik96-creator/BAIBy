# BAIby

BAIby is a virtual baby raising game. The Android and iOS restart focuses on a chibi-style 2D baby whose reactions, routines, and abilities change through play and as they grow.

- [Restart roadmap](docs/roadmap.md)
- [Feature parity with v5.3](docs/feature-parity.md)
- [Mobile architecture](docs/architecture.md)
- [Regression guardrails](docs/regression.md)
- [Nursery playtest checklist](docs/playtest.md)
- [Interface style](docs/interface-style.md)
- [Original browser prototype](legacy/BAIby_v5.3.html) — download and open locally in a browser.

Run the browser baseline and assembly checks with Node.js 22 using `node --test tests/*.test.mjs`.

## Play in GitHub Codespaces

Open the branch in a Codespace. The preview starts on port **8000** and opens in the browser. If the tab does not open automatically, choose **Open in Browser** for port 8000 in the Codespaces **Ports** panel. Keep the forwarded port private for personal testing. The prototype stores progress in the browser, so use the same preview URL and browser profile to resume a save.

The preview's front page opens the Unity game once a build is in `build/Web` (see below); until then it explains how to download one. The game itself lives at `/nursery/`, and the archived HTML prototype at `/legacy/`. You can also serve both locally with `bash .devcontainer/start-preview.sh`, which also replaces a preview server left running from an older layout. Android and iOS device checks cover native lifecycle and graphics performance.

### Play a GitHub build

The [Build Unity Web preview workflow](https://github.com/khoryik96-creator/BAIBy/actions/workflows/unity-web-preview.yml) produces a `baiby-web` artifact. GitHub requires the workflow on the default branch before the first manual run. The repository needs `UNITY_EMAIL`, `UNITY_PASSWORD`, and either `UNITY_LICENSE` for an activated Personal license or `UNITY_SERIAL` for an eligible serial-based seat. Configure those as GitHub Actions secrets.

In a Codespace, download the artifact from a successful run into an empty `build/Web` directory. Use the run ID shown on the GitHub run page.

```sh
gh run download RUN_ID --name baiby-web --dir build/Web
python3 tools/check-web-build.py build/Web
```

Open port **8000** to play: the front page goes straight to `/nursery/`. To try everything without locks, open **Home → Grown-ups** and turn on **Developer mode** (or add `?dev=1` to the address): every room, premium breed, earned breed, look pack and room theme opens. Test builds only. If using a downloaded zip, extract it so that `build/Web/index.html` exists.

## Mobile restart development

Open the `unity/` folder with the Editor version listed in [ProjectVersion.txt](unity/ProjectSettings/ProjectVersion.txt). In Unity, open the Nursery scene and enter Play Mode. Choose **A baby** and adopt one, try Feed, Soothe, Rest, the Wash menu, and both toys (Rattle and Plush), and use **Next day** to exercise the first milestone. Open **Style** to change the baby's skin tone, hair, outfit, and headwear. Choose **A pet** instead (or from **Home → Adopt**) to raise a cat with placeholder art; see the [cat playtest](docs/playtest.md#cats-p1).

Install the Web Build Support module for the pinned Editor to export a browser build. From the repository root, run:

```sh
Unity -batchmode -quit -projectPath "$PWD/unity" -buildTarget WebGL \
  -executeMethod BAIby.Editor.BuildPreview.BuildWeb -logFile -
```

You can also select **BAIby → Build Browser Preview** in the Editor. An activated Editor is required. The export goes to `build/Web`. Serve it through the preview route above.

In the Editor, open **Window → General → Test Runner** and run the Play Mode smoke test. It adopts a baby through the nursery UI, exercises the care buttons, and reopens an isolated save to verify that the milestone and toy familiarity persist. Other cases cover the household, resting companions, and a cat adopted beside a baby.

The [simulation](unity/Assets/BAIby/Simulation) and [session](unity/Assets/BAIby/Application) compile as plain C#. Run their regression checks with the .NET 8 SDK.

```sh
dotnet run --project tests/Simulation/SimulationChecks.csproj --configuration Release
dotnet run --project tests/Appearance/AppearanceChecks.csproj --configuration Release
```

The baby's look comes from region maps and hair and headwear overlays generated from the pose illustrations. Regenerate them with the tool in [tools/appearance](tools/appearance), which also renders preview sheets for review (`build.py --only-hats PomBeanie,SunHat` redraws just the named hats). The cat's art and its neck wear come from [tools/pets](tools/pets), and the premium room themes (Cloud nursery, Starlight, Golden hour) are recoloured from the Sage nursery by [tools/rooms/build.py](tools/rooms/build.py) (`--preview` writes a contact sheet). The look packs, room themes, cat wear and Founder pack are listed in [monetization](docs/monetization.md#looks-and-rooms-built-in-m2b); [the playtest](docs/playtest.md#looks-and-rooms-monetization-m2b) shows how to see each one with `?dev=1` or `?ent=`.

The character and milestone timing are for playtesting. The nursery uses layered 2D artwork, with awake and sleeping chibi poses for each developmental stage. The local adapter uses Unity PlayerPrefs with a previous valid payload for recovery. Verify persistence on each target before beta.
