# BAIby restart roadmap

The first release should let players get to know a baby, see how their care changes the baby's behavior, and recognize new abilities as the baby grows. Start offline on Android and iOS.

## 1. Prove the character on a phone

Build one illustrated nursery, one toy, and two developmental looks in Unity. Show the swaddled newborn from the welcome screen and give the same baby a distinct sitting pose as they grow. Use awake and sleeping illustrations and readable care reactions at phone size. Record reference clips and profile a physical Android phone and iPhone before producing more age-stage assets.

## 2. Ship an internal playable slice

Adopt a baby, then feed, soothe, and play. Show immediate need changes through behavior and let repeated play create familiarity with the toy. An accelerated test clock should reveal the first milestone. Close and reopen the game to verify the same baby, routine, and milestone state return. Test whether players can describe what the baby wanted, what their action changed, and what the baby learned.

Keep gameplay in a plain C# simulation with injected time and seeded gameplay randomness. A session layer handles commands, lifecycle, and versioned local saves. Unity scenes turn simulation outcomes into poses, gaze, audio, UI, and effects. Author toys, milestones, cosmetics, and reactions with stable content IDs. Keep the elapsed-time marker and the state it advances in the same save transaction.

## 3. Expand after the loop works

Add routines, outfits, toys, and developmental abilities in small batches. Growth follows elapsed game time, and care quality shifts each milestone within 0.8x to 1.3x of its nominal age, as described in [care and growth](care-and-growth.md). Care affects needs, development, familiarity, and preferences. Cap offline need decay so players can return after a break and recover through ordinary care. Decide pacing and any v5.3 save import before the first external beta. Treat browser behavior as historical reference and call out intentional changes, including the switch from feeding-based growth to elapsed-time growth.

## 4. Prepare mobile beta

Verify Android and iOS builds on physical devices. Play through fresh install, airplane mode, pause and resume, forced close, and upgrading a saved game. Profile the nursery during interaction and after a sustained session. Record frame pacing and memory on the target devices and agree on budgets before adding more scenes.
