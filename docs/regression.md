# Regression guardrails

## Browser prototype

GitHub Actions runs the legacy checks on pushes and pull requests. They verify that the archived file matches the upload, its inline script parses, the first feed advances the legacy growth counter once per day, and the legacy save round-trips. The rule tests isolate browser side effects and do not cover touch, rendering, or lifecycle behavior. Update the archive checksum only when intentionally replacing the historical baseline.

## Automated restart checks

CI compiles and exercises the engine-independent simulation and session. It covers care and elapsed-time rules, milestone delivery, save reload, corrupt-payload recovery, unsupported save versions, and failed-write handling. An assembly check enforces the dependency direction for the runtime assemblies. Build and device checks still need the Unity runtime.

## Unity restart acceptance checks

The Unity Play Mode smoke test loads the nursery, drives the care controls, advances the milestone, and restores an isolated save. The manual Web workflow checks its exported files and HTTP serving before uploading a preview artifact. Use a fake clock and seed for automated simulation tests. Keep a save fixture for every released schema once beta saves exist.

| Change | Evidence to attach to the pull request |
| --- | --- |
| Growth and care | Time advances age without feeding. Feeding changes immediate needs without advancing age. Repeated play can increase familiarity and new abilities unlock once. |
| Clock and lifecycle | Repeated resume at the same time applies elapsed time once. A backwards clock or timezone change cannot reverse age or duplicate a milestone. A long absence respects the offline cap. |
| Persistence | A fresh save round-trips. An older fixture migrates with identity, age and milestones intact. An interrupted write can recover the previous valid save. |
| Interaction | Touch invokes the action once, cues the matching reaction and restores the correct state after resume. Provide a tap path where gameplay also supports drag. |
| Character or camera | Compare clips of idle, toy tracking and reaching at phone gameplay size against the approved reference. Check the silhouette, eyelid and gaze behavior, hand contact, cloth clipping, age-stage readability, and transition pops. |
| Mobile performance | Record device, build, scene, frame pacing and memory for a nursery interaction on Android and iOS. Compare with the approved device baseline once captured. |

Document intentional changes in the pull request and update the affected test or reference clip with the reason. Review the relevant evidence before merging. When Unity CI exists, require its passing checks before merging gameplay changes.
