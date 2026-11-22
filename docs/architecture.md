# Mobile architecture

Use a single Unity project with URP and assembly definitions. Android, iOS, and the browser preview share the same simulation and content. Start with one nursery and a local save. The following are planned boundaries for the first playable slice.

| Assembly | Owns | Dependencies |
| --- | --- | --- |
| Simulation | Baby state, needs, age, milestones, care and familiarity rules | Plain C# and .NET types. Disable Unity engine references. |
| Application | Session, command orchestration, clock and save interfaces | Simulation |
| Infrastructure | Platform storage, serialization, migrations, clock adapters | Application and Simulation |
| Presentation | Nursery, input, animation, audio, and content authoring | Application and Simulation |
| Bootstrap | Platform selection and wiring | All runtime assemblies |

## Commands and time

An interaction submits a command with stable content IDs. The simulation applies rules to state and returns outcomes. The session commits persistent changes, then publishes a view state and cues for the nursery. Animation, rendering, and audio use those outcomes. They never grant rewards or advance age. Gameplay RNG is seeded independently of visual effects and its state is saved where continuity matters.

Advance elapsed time on launch, resume, and explicit session ticks. Commit the time checkpoint together with age, needs and milestone state. Ignore negative elapsed time and cap offline need decay. Keep age progression and need decay independently tunable. Use a single writer for saves and guard against duplicate milestone triggers.

## Content and character growth

Use ScriptableObjects for authoring, then translate gameplay values into plain C# definitions at the boundary. Give catalog entries stable IDs and validate missing references, duplicates and invalid ranges in editor checks. A new toy or outfit should need catalog and asset changes only. Shared behavior goes into an existing rule or a tested simulation extension.

Map developmental stages to rig and animation profiles. Keep expressions and actions semantic, such as tired or reaching, so art can vary by stage without changing the simulation. Use compressed textures and profile asset loading in the nursery. Introduce on-demand packs when device measurements show the need.

## Storage and platform growth

Store a versioned save envelope with stable content IDs, a time checkpoint and the baby state. Keep platform storage behind the same interface. Native builds should keep a recoverable previous save. The Web build should use browser-backed persistence and verify reload and storage behavior independently. Cloud sync, if added, can use the same versioned state with explicit revision and conflict handling.

Build and test the plain C# simulation separately from scenes. Add an assembly dependency check when Unity CI lands. Add an editor content validator and platform smoke tests with the first content catalog and first save implementation. Use the browser build for frequent playtests and physical devices for performance and lifecycle acceptance.

## Design for 100,000 players

Use 100,000 monthly active players as an initial planning scenario. Track daily and peak concurrent players separately when forecasts exist. With the simulation and saves running locally, player count does not increase simulation compute or write traffic on a shared server. Plan distribution and optional services separately. Use Codespaces for individual playtesting, and static hosting backed by a CDN for a public Web build if demand warrants it.

If cross-device saves become necessary, add sync at the save interface. Start with managed authentication, one stateless API and a managed database keyed by player ID. Sync checkpoints instead of streaming care actions. Queue writes offline, retry with backoff, and use revision checks and idempotency keys so two devices cannot silently overwrite each other's progress.

As a provisional sync workload, assume 20,000 daily players with five sync requests each, or about 100,000 requests per day. Load-test a starting peak of 25 requests per second and a reconnect burst at 50 requests per second. Replace those assumptions with beta data before rollout. Check p95 latency, errors, database throughput, payload size and cost. Exercise retries and simultaneous saves from two devices with real-shaped fixtures. Increase capacity based on those measurements.

Preserve compatibility across app releases. Keep stable IDs, explicit save versions and tested migrations from every supported version. Run old-client and new-client contract tests before changing a sync API. Roll out service changes independently of app store releases where possible.
