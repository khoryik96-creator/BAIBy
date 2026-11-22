# BAIby

BAIby is a virtual baby raising game. The Android and iOS restart focuses on a believable, stylized 3D baby whose reactions, routines, and abilities change through play and as they grow.

- [Restart roadmap](docs/roadmap.md)
- [Regression guardrails](docs/regression.md)
- [Original browser prototype](legacy/BAIby_v5.3.html) — download and open locally in a browser.

Run the baseline checks with Node.js 22 using `node --test tests/legacy.test.mjs`.

## Play in GitHub Codespaces

Open the branch in a Codespace. The preview starts on port **8000** and opens in the browser. If the tab does not open automatically, choose **Open in Browser** for port 8000 in the Codespaces **Ports** panel. Keep the forwarded port private for personal testing. The prototype stores progress in the browser, so use the same preview URL and browser profile to resume a save.

The Codespaces preview runs the archived HTML game. The Unity restart can support browser playtesting once a Unity Web build is available. Android and iOS device checks are still needed for native lifecycle and graphics performance.
