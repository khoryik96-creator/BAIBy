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
