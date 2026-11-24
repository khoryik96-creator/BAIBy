# Pet mode and household

**Status: decided proposal, being built.** Phase H0 (Home, slot 1 migration, a second companion, switching, goodbye and per-companion time) and the H1 scaffolding (entitlements, the parental gate and resting companions, with no store) are built. P1 Cat is playable with placeholder art: adoption of a baby or a pet, the cat's breeds and unlocks, its room, Home cards and keepsakes (see [the playtest](playtest.md#cats-p1)); P1 progress is listed under the slice plan. Care slice 7 of [care and growth](care-and-growth.md) is built for the cat: its routine, the consequence ladder with the vet, Away mode, milestones (pounce, climb, high jump), Bond, Trust, the care rhythm and a journal with pages (see [the playtest](playtest.md#cats-care-slice-7)). P2 Hamster is built with placeholder art on the same care profile mechanism: a nocturnal routine that naps through school hours, one dinner, evening wheel time, its seven breeds (Teddy bear included) and a modest habitat (see [the playtest](playtest.md#hamsters-p2)). P3 Dog is built with placeholder art on the same mechanism: two meals and two walks (the morning one with a kind grace for school days), tricks learned through games of fetch, the reunion greeting scaled to time away, and eleven of its twelve breeds, the premium Corgi included; the Dachshund is arriving soon (see [the playtest](playtest.md#dogs-p3)). The decisions are listed at the end.

It merges three proposals with a later user decision: 2 free companions, further slots paid.

## Goals and non-goals

Goals:

- At adoption the player chooses a baby or a pet, and for a pet a species and breed.
- Several companions, babies and pets mixed, are saved independently and the player switches between them.
- The existing baby moves into the household without loss.
- Species feel distinct but share the baby's time, save and recolor rules, and every slice ships with producible art.

Non-goals: babies and pets in one scene (needs paired art); death, running away or punitive endings for any species; currency, ads, gacha or timers; cloud sync.

## Adoption and the household

**First launch.** "Who's joining your home?" offers two rounded cards, *A baby* and *A pet* (a paw glyph built from circles). For a pet, a species carousel follows; unshipped species show as dimmed "Arriving soon" tiles with no lock or price. A breed picker shows a live recolored preview and breed-limited coats, then a name, pronouns and a short arrival beat.

**Home.** Home shows one rounded card per companion: portrait, name, species glyph, stage, and a mood line with an icon ("Napping in a sunbeam"). Two cards fit side by side at 390px. Below capacity, a dashed "+ Adopt" card appears.

- A Home chip sits in every room's top bar, and a second chip switches straight to the other companion. A switch never takes more than 2 taps.
- Startup opens the last active room directly.
- A dot on the Home chip shows when another companion has a need below the existing threshold of 30.
- Long-pressing a card offers "Say goodbye". After a two-step confirmation the slot is freed and a keepsake entry stays behind (name, species, portrait, dates, journal highlights).
- Existing players skip onboarding and land on Home with their baby in slot 1.

**Slots.**

| Rule | Value |
| --- | --- |
| Free companions | `HouseholdRules.FreeCompanions = 2`, one tunable constant, babies and pets combined |
| Paid unlock | One non-consumable that adds extra slots. The pack size and price tier are open; +2 is recommended. |
| Physical slot range | 8, so a larger pack or free limit needs no save migration |
| Full household on the free tier | A calm "Your home is full" card with a small "More rooms" link behind the parental gate |

## Species

Every pet has Fullness, Energy, Comfort, Cleanliness, Health and Bond (0 to 100, the pet counterpart of Connection). A species can rename, drop or swap meters. A room shows at most 4. Health stays a quiet background meter, as it is for the baby. Pets have two stages, Young and Grown, each with an awake and a rest pose. Growth stops at Grown.

| | Cat | Dog | Hamster | Fish | Rabbit |
| --- | --- | --- | --- | --- | --- |
| Meters | Fullness, Energy, Comfort, Cleanliness (slow) | Fullness, Energy, Exercise, Cleanliness | Fullness, Energy, Bedding, Exercise | Fullness, Water, Calm | Hay, Fullness, Litter, Comfort |
| Care verbs | Feed, Pet (touch zones), Wand, Brush, Nap | Feed, Walk, Fetch, Brush, Rest | Bowl, Wheel, Fluff bedding, Gentle hold, Nap | Sprinkle food, Wipe glass, Water change, Rest | Hay, Greens, Groom, Floor time, Litter |
| Cue to read | Tail flicks before the cat hops off; slow-blink builds trust | Tricks learned through familiarity counts | Evening rhythm, with a daytime-friendly setting | Extra flakes are ignored and cloud the water slightly | Sitting on the floor builds Bond faster |
| Signature moment | Kneading, purring, bringing a toy | Reunion greeting scaled to time away | Cheek pouches fill | Swims to the glass | Binkies |
| Young to Grown | 7 days | 10 days | 5 days | 5 days | 7 days |

Wipe glass reuses `DiaperWipeInput`. Every gesture has a one-tap fallback; no minigame can fail.

## Breeds

A breed must be producible from its species base by recolor, a marking overlay, a feature overlay (ears, mane, fins) or scale. A breed that needs a new silhouette is a later commission and never blocks a slice.

A breed sets:

- the coat and marking options and the default look;
- any feature overlay;
- at most two gentle tendencies of 10 to 20 percent;
- a starting personality bias.

No breed is harder or worse. Health-linked traits such as flat faces are not exaggerated, and mixed breeds are first-class choices.

Breeds unlock three ways. Each species has a free starter breed at adoption. Other breeds are **earned** by meeting a visible goal, or bought directly as **premium** breeds. Nothing is random, and every requirement and price is shown before the player commits. Unlocks are household-wide and permanent. A premium breed looks special but is never stronger; it follows the same care rules, tendencies range and Bond curve as any other breed.

| Species | Free starter | Earned | Premium |
| --- | --- | --- | --- |
| Cat | Domestic shorthair | Tabby, Tuxedo, Siamese, Calico | Ragdoll, Maine Coon, Bengal, British Shorthair, Russian Blue, Sphynx |
| Dog | Mixed breed | Golden, Beagle, Shiba | Corgi, Poodle, Dachshund, Husky, Samoyed, Pomeranian, Dalmatian, Border Collie |
| Hamster | Syrian golden | Winter white, Roborovski | Teddy bear, Panda, Sapphire, Campbell's |
| Fish | Goldfish | Guppy, Betta | Neon tetra school, Angelfish, Koi, Clownfish, Discus |
| Rabbit | Dutch | Netherland dwarf, Holland lop | Lionhead, Angora, Rex, Flemish giant, Harlequin |

Premium breeds come in two art tiers, which sets their release order:

| Art tier | Breeds | Art cost |
| --- | --- | --- |
| Recolor and markings on the species base | British Shorthair, Russian Blue, Bengal, Dalmatian, Husky, Panda, Sapphire, Campbell's, Koi, Clownfish, Harlequin, Rex | Low. These ship with their species slice. |
| Feature overlay (long coat, ears, tail) | Ragdoll, Maine Coon, Pomeranian, Samoyed, Border Collie, Poodle, Teddy bear, Angora, Angelfish, Discus | Medium. These ship in the first update after the species. |
| New silhouette | Sphynx, Corgi, Dachshund, Lionhead, Flemish giant, Neon tetra school | High. Each is commissioned art and ships when ready. |

Breeds whose recognizable look depends on a health problem, such as flat faces or folded ears, are left out. Premium breeds also accept special coats, so a premium breed and a special coat can be combined.

Earned breeds follow a short goal ladder per species. Goals reward hands-on care and never require spending.

| Goal | Unlocks |
| --- | --- |
| Adopt a pet of the species and complete a first full care day | The first earned breed |
| Reach Bond 40 with any pet of the species | The second earned breed |
| Raise a pet of the species to Grown | The third earned breed |
| 7 on-time care days for the species, using the forgiving streak from [care and growth](care-and-growth.md) | The fourth earned breed |

Premium breeds are one-time purchases behind the parental gate, sold singly or as a species breed pack. A player can preview a premium breed in the adoption picker before buying. Breeds that need a new silhouette, such as the Corgi, Dachshund and Lionhead, ship when their art is ready and never block a slice.

## Bond and personality

- Bond tiers (Acquainted, Trusting, Best friends) unlock behaviors, not stats.
- Two hidden axes, Mellow to Lively and Shy to Bold, start from the breed bias and drift with care during the Young stage.
- The Pet Journal, the pet counterpart of the Baby Report, uses warm phrasing only. It lists milestones, tricks and a favorite toy, using the baby's familiarity rule.

## Humane absence rules

- Each companion has its own checkpoint and capped decay (4 hours, as for the baby). Normal care recovers any companion in minutes.
- The worst state is a sleepy, hungry-looking pose. A low-water tank turns hazy. Nothing dies or leaves.
- Returns get a joyful reunion beat, never guilt text. Consistency shows as the forgiving care rhythm from [care and growth](care-and-growth.md), which halves on a missed day instead of resetting.
- Notifications are off by default. If enabled, there are at most two a day for the household, and they are observational.
- A resting (locked) companion does not decay.

## Architecture

### Placement

Pets go in new folders inside the existing assemblies, so the dependency directions in `architecture.test.mjs` hold. The store adapter is the only new assembly.

| Location | Adds |
| --- | --- |
| `Simulation/Pets/` | `PetIds`, `PetCatalog` with `Validate`, `PetAppearance`, `PetState`, `PetSimulation`, `CompanionKind` |
| `Application/` | `SavedRecord` (restore with backup fallback and commit, extracted from `BabySession`), `PetSaveCodec`, `PetSession` |
| `Application/Household/` | `HouseholdSession`, `HouseholdManifestCodec`, `IHouseholdStore`, `HouseholdRules`, `IEntitlements` |
| `Infrastructure/` | `PlayerPrefsHouseholdStore`, and `Delete` on the per-key store |
| `BAIby.Store` (new) | `UnityIapEntitlements`, excluded from the Web build and the dotnet tests |
| `Character/` | `AppearanceRecolor` generalized to four region targets, `PetPalette`, `PetVisuals` |
| `Presentation/` | `HouseholdPreview` (Home and adoption), `PetRoomPreview`, a rounded UI kit extracted from `NurseryPreview` |

`PetState` stays separate from `BabyState`, which has 11 save versions. One `PetSimulation` runs over a `SpeciesDefinition` table. A species gets its own code only where a rule really differs, and that code is a tested rule in the same class. The time model matches the baby's: negative elapsed time is ignored, decay is capped, age is not capped, and unlocks fire once. The catalog is plain C# so dotnet can test it here; ScriptableObject authoring can be added at the boundary later.

**Stable IDs.** All IDs are explicit and append-only. `Species { Cat = 0, Dog = 1, Fish = 2, Rabbit = 3, Hamster = 4 }`. `PetBreed` uses blocks of 100 per species (Cat 100+, Dog 200+, and so on), so a mismatched species and breed fails on decode. IDs for later breeds are reserved now.

### Saves and migration

There is one record per companion plus a manifest, not one envelope. A corrupt record affects one companion, care writes stay small, and the existing primary-plus-backup per key carries over.

| Key | Contents |
| --- | --- |
| `baiby.mobile.baby-save` (+ `.backup`) | Slot 1: the existing baby record at v11. It only ever holds a baby, so older builds never read a pet. |
| `{saveKey}.slot{n}` (+ `.backup`), n = 2..8 | A baby record or a `<pet version="2">` record (v2 adds a cat's care records; v1 records still load) |
| `{saveKey}.household` | Manifest: order, active slot, pending celebrations, keepsakes, settings |
| `{saveKey}.entitlements` | Cached entitlement and the time it was last confirmed |

- **Migration.** The legacy key *is* slot 1, so no record is copied or rewritten and the baby codec is not bumped. The first launch after the update writes only a new manifest. Saves from v1 to v11 keep decoding as they do now, and a rollback still finds the baby.
- **Recovery.** Slots are the source of truth. PlayerPrefs cannot list keys, so startup probes slots 1 to 8 and rebuilds a missing or corrupt manifest from them. Adopting writes the record, then the manifest. Goodbye first writes the keepsake with the companion marked as leaving, then deletes the record, then writes the manifest. A launch after an interruption finishes the goodbye with exactly one keepsake, but only from the primary manifest, never from its backup.
- **An unreadable slot is never treated as free.** Home shows "Couldn't open this save", the bytes are kept, and the slot counts toward capacity.
- **Decoding.** Undefined IDs fall back to the backup. A breed option later removed from the catalog still decodes and renders with the breed default.

### Sessions and time

- `BabySession` keeps its public API. `HouseholdSession` is the single writer of slots and the manifest, and caches one session per companion. `NurseryStartup.StartPreview(saveKey)` keeps its signature, and all keys derive from `saveKey`, so the smoke test stays isolated.
- Every unlocked companion advances on launch and resume, not on switch, so decay never depends on switching habits. Off-screen unlocks are saved as pending celebrations, shown as "New!" and played once in the room.
- The 60-second tick refreshes Home with a preview that makes no writes, updating the cards in place. Leaving Home commits the advance for every companion, like a resume. Only the active companion's textures stay in memory.
- A manifest written by a newer build is kept byte for byte and never rewritten, like a newer slot record. Goodbye is unavailable until the game is updated, because its keepsake could not be saved.
- `IBabySaveStore` becomes `ISaveSlot` with `Delete()`, in a mechanical commit after the appearance branch merges.

### Entitlement and the store

- `IEntitlements` reports how many extra slots the player owns. Capacity is `FreeCompanions` plus that number. On devices, Unity IAP uses Google Play Billing and StoreKit.
- **Offline first.** The cached entitlement is trusted at launch and refreshed when the store answers. Network errors never remove it. Receipts are validated locally, because the game has no server; this accepts some piracy.
- **Restore Purchases** sits in household settings behind the parental gate. Apple requires it.
- **Web build.** There are no payments, and `BAIby.Store` is not compiled. `TestEntitlements` reads a developer toggle.
- **Lost or refunded entitlement.** The entitlement is withdrawn only on a definitive "not owned" answer: a Play voided purchase or a StoreKit revocation. Companions are never deleted. Those beyond the free limit become *resting*: read-only, with no decay or writes, but their journal is still viewable. The player chooses which companions stay active. A restore unlocks resting companions, followed by one normal capped advance. The lock is computed at runtime and never saved into records.

### Child-audience policy

Needs a legal review before submission.

- A parental gate (an adult task, such as typing a number written in words) guards every purchase, restore and external link, as Apple's Kids category requires.
- No manipulative prompts: no purchase prompts in care flows, no companion asking, no timers or offers, and nothing implies a companion suffers without a purchase.
- For Google Play Families, COPPA and GDPR-K: no personal data and no ads. Analytics stay off, including any Unity Services defaults. Ask to Buy and Play family approval apply unchanged.
- **Talk time** ([care and growth](care-and-growth.md#first-words-and-talk-time), babies only). Feeling chips work without typing; typing is off until a parent turns it on behind the gate. Typed words (at most 140 characters) are read on the device by a deterministic keyword matcher and are never saved, logged or sent; the field clears after sending. Only the feeling is stored, in one journal line a day. The baby's replies are curated, never generated: they comfort, celebrate or keep company, and never give advice, diagnose, blame, ask for personal details or promise anything. Words about self-harm, suicide, being hurt or abused, or being unsafe get a fixed message instead: "That sounds really hard, and it matters. Please tell a grown-up you trust right now, like a parent, teacher or carer. If you might be in danger, call your local emergency number." Nothing else happens in that turn; the journal says only "We talked about something big". The game cannot see who is talking or contact anyone, so this message is the whole response: it points the child to a trusted adult. Have the safety list and message reviewed by a child-safeguarding specialist before submission, and consider adding a regional helpline per store territory.

### Tests

- **dotnet.** Widen `SimulationChecks.csproj`, which does not recurse into subfolders today. Cover the catalog validator, pet decay and unlocks, a golden pet v1 payload, v1 to v11 baby fixtures loading as slot 1 with zero writes, manifest rebuild, kills between writes, unreadable slots, capacity with and without the entitlement, and resting companions making no writes.
- **Node.** A `content-ids.test.mjs` snapshot fails on any renumbered saved enum, baby enums included.
- **Recolor.** The generalized recolor stays pixel-identical to the baby golden checks.
- **Play Mode.** Adopt a baby and a cat, switch, reload, then toggle the test entitlement and check that a third companion rests and restores.
- **Web.** Record in `playtest.md` that two tabs overwrite each other's saves.

## Art production

The baby recolor pipeline is already species-agnostic. For pets:

- R is the main coat, G is the secondary coat or pattern, B is the iris, and A is the accessory.
- The accessory uses the outfit accent palette, so collars can match a baby's outfit for free.
- Composite order is base, feature overlay, markings, accessory, then one recolor pass.

**Procedural or free:** all colors, decor colorways, portraits, motion (bob, squash, swim, tail rotation), particles, and possibly fish bodies from an offline tool. Procedural stripes on hand-drawn chibi art look flat and should not ship.

**Commissioned** (there is no image generator): base silhouettes, marking shapes, ear, mane and fin overlays, props and backdrops.

- Order layered sources with named layers, so region maps export directly and ears and tails can be animated.
- Use the baby artist if possible, with the baby poses as reference.
- A style check should enforce the #4D2917 outline, the canvas size range and matching head anchors.

| Species | Commissioned | Units |
| --- | --- | --- |
| Cat | 4 poses, 3 marking layers, room, 3 props | 9 to 11 |
| Hamster | 4 small poses, cage, 3 props | 5 to 6 |
| Dog | 4 poses, 2 ear sets x 4, 2 props | 9 to 11 |
| Fish | Tank, fallback sheet | 1 to 2 |
| Rabbit | 4 poses, lop ears x 4, markings, pen | 7 to 9 |
| Shared | Home scene, arrival | 2 to 3 |

One unit is roughly one baby pose. The total is about 33 to 42 units, against 8 for the baby so far, and extra reaction poses would roughly double it. Plan with an unverified US$50 to 300 per unit and get real quotes; art lead time sets the pace. Each species adds roughly 1.5 to 3 MB to the Web download, so measure each species and move pets to on-demand packs past an agreed budget.

## Phased slice plan

| Phase | Contents | New art |
| --- | --- | --- |
| H0 Household | Home, slot 1 migration, a second baby, switching, goodbye, per-companion time, a Pet card marked "Arriving soon", the content-ID test | None |
| H1 Slots | Entitlements, Unity IAP, parental gate, restore, resting companions, Web test flag. Must ship before any store beta. | None |
| P1 Cat | Pet framework, pet room, UI kit, 4 cat breeds, consent petting, wand, journal | Cat batch |
| P2 Hamster (built, placeholder art) | A second habitat, proving the model is not secretly a cat | Same batch as the cat |
| P3 Dog (built, placeholder art) | Walk, fetch, tricks, reunion | Placeholder (`tools/pets/dog.py`); commission the final batch |
| P4 Fish, P5 Rabbit | Water and Calm meters, wipe glass; floor time and binkies | Procedural fish, rabbit batch |

**P1 progress.** Built (care slice 7 for the cat): the cat's care profile, routine strip, Restless and Grumpy moods, the scratched rug, overfeeding, Away mode, the vet (Dr. Rosa) and medicine, the milestones Pounce, Climb and High jump with Growth's projection, the setback, Bond with the baby's caps, Trust, the care rhythm and stickers, and the Pet Journal's pages (milestones, photos, stickers, week pages, hard days). Built in P1: `PetSession` on a shared `SavedRecord`; a household of babies and pets (kinds in the manifest, pets in slots 2 to 8, manifest v2 only once pet data exists); breed unlocks with household-wide species progress (care days, best Bond, raised to Grown) that outlives goodbyes; pet keepsakes with species, breed, look and stage; `PetLayout`, `PetComposer` and `PetVisuals` for the cat art; the adoption choice, species row and breed picker with a live preview; the cat room with care verbs, cue lines, the rest pose and the Grown swap; pet Home cards and a pet journal. Not built yet: consent petting with touch zones and the tail-flick cue, wand as a gesture, behavior driven by the Lively and Bold axes, the Pet Journal's favorite toy, pronouns and an arrival animation, a premium breed store (premium breeds can only be granted by the playtest flag), and the final commissioned cat art. The species care-day rule for earned breeds is a first cut and is kept as it was (so saved progress keeps counting): a day counts with 2 meals, a Together action (any verb that builds Bond) and a bedtime (Nap or Rest) on a local calendar day; each missed day halves it, with no grace day. A care the cat refuses (too poorly, or grumpy) counts for nothing. The cat's own care rhythm, stickers and Bond follow care and growth.

**P2 progress.** Built with placeholder art (`tools/pets/hamster.py` and `habitat.py`): the hamster's `PetCareProfile` on the cat's mechanism, which is now species-generic in three places. A profile names the meter that plays each need role (a hamster's Exercise is its Comfort and its Bedding its Cleanliness), so moods, overfeeding (sluggish, and the extra stashed in the bedding), the mess cut, clingy Trust and the helpers' floors all carry over. A routine window can be a **nap block**, a Bedtime moment the companion keeps by itself: it closes excused, is never missed, owed or counted, and its rest always restores Energy (`RestsAlone`), even through the uncounted hours of a long absence. Quiet hours can sit inside one day (the day nap) instead of wrapping midnight. The good day, the care rhythm's day and the household's earned-breed care day ask for every meal of a routine with fewer than two (a hamster's one dinner), and a routine with a day nap asks for no bedtime. The hamster: Bowl (dinner, Fullness +45), Wheel (play, Exercise +30, Energy -10, Bond +2), Fluff bedding (the daily tidy, Bedding +40), Gentle hold (together time that comforts, Exercise +12, Bond +3) and Nap (Energy +35). A content hamster in its day nap sleeps through the wheel ("Shh, … is fast asleep in the nest"; it counts for nothing and costs nothing) and every other care is gentle: food is stashed in the cheek pouches, a hold is a quiet peek, the bedding can be fluffed around it. A fidgety hamster is awake anyway, so it may run. Moods read **Fidgety** and **Grumpy**; the vet (Dr. Rosa) and medicine work as for the cat, a dose with dinner on two evenings. Milestones **Wheel** (nominal day 2), **Tunnel** (day 4) and **Hand-tame** (day 6) ride the same readiness chain on the Motor quality (which includes Bond); the hamster grows up on day 5 (species table), so it may become hand-tame as a grown-up (guaranteed by day 8.4). The wheel is play for engagement (Motor 4, the cat's `PlayWand` kind) and a hold is soothing (Social 3). Helpers are the cat's in the hamster's words: a seed dispenser, a spare wheel and a self-cleaning litter corner. Breeds: the seven of the table, tuned so each loses the same need per hour; the Teddy bear's long coat ships with the species as a feature overlay, so every hamster breed is listed in the picker. Celebrations wait in their own `<hamster-celebrations>` manifest element, so a build from before the hamster loses only the announcement. Not built yet: the signature cheek-pouch moment as an animation, a tunnel prop the hamster uses, the Mellow/Lively axes driving behaviour, and the commissioned art.

**P3 progress.** Built with placeholder art (`tools/pets/dog.py`): the dog's `PetCareProfile` on the same mechanism, which gains a fifth routine moment (Dawn, for the dog only: every other routine keeps four, so their saves are unchanged), a **Walk** care tag answered only by a walk, and **grace windows**. The dog: **Feed** (Fullness +40), **Walk** (Exercise +40, Energy -8, Clean -3 for muddy paws, Bond +3; together time that comforts), **Fetch** (Exercise +15, Energy -10, Bond +2; play), **Brush** (the daily wash, Clean +35) and **Rest** (the bedtime, Energy +35). Its day: breakfast and dinner as for the cat, a morning walk before school that is a bonus (when school gets in the way it closes as free: never missed, owed or counted), an evening walk after school that keeps the day, bed in the evening and a daily brush; a missed brush leaves a **chewed slipper**. Moods read **Restless** ("nose at the door. A walk will help.") and **Grumpy** (it turns away from the ball; a meal, then a walk, calms it). Away mode: a neighbour pops in to walk the dog. The vet (Dr. Rosa) and medicine with breakfast and dinner. Milestones **Fetch** (day 3), **Leash walk** (day 6) and **Agility** (day 9); it grows up on day 10. **Cue to read: tricks learned through familiarity counts**: every game of fetch that is not one too many adds to the dog's familiarity, and Sit, Paw, Spin, Roll over and High five arrive at 2, 5, 9, 14 and 20 games (Growth lists them). **Signature moment: the reunion greeting scaled to time away** (since the last hands-on care, Away mode included): a wagging tail from 2 hours, zoomies from 16, the biggest welcome from two days, with more and higher happy bounces each step; always joy, never guilt (no line mentions the time away, and nothing is lost or gained by it). Helpers: an auto-feeder, a ball launcher and a doggy door with a paw mat; a walk can never be automated. **Breeds:** Mixed breed free; Golden, Beagle and Shiba earned (the cat's goal ladder); Corgi, Poodle, Husky, Samoyed, Pomeranian, Dalmatian and Border Collie premium (`breed.dog.<key>`, `breedpack.dog`, open in developer mode). Every shipped dog's ears are its feature overlay (button, floppy, long, curly or prick), with markings (the Beagle's saddle and the Dalmatian's spots as dark overlays; the Husky's mask, the Shiba's urajiro, the Corgi's and Border Collie's blaze) and fur (the Golden's chest feathering, the Poodle's curls, the Samoyed's and Pomeranian's ruff). **The Corgi ships**: its short legs are the base re-drawn with the leg region scaled to 55% (a breed base, `breedBases` in `layout.json`) and its big ears a feature overlay, so it is premium and adoptable, though its art tier stays "new silhouette" for the commissioned version. **The Dachshund is arriving soon**: its long body needs a true new silhouette, so the picker lists it as Soon and it cannot be adopted (a pack owner keeps it for when it arrives). Dog wear (collar, bandana, bow) is free. Not built yet: walks as a gesture or a little outing scene, tricks performed on request, a favourite toy, behaviour driven by the Lively and Bold axes, a curled tail for the Shiba, and the commissioned art.

**Recommended first species: Cat.** It has broad appeal and maps closely onto the existing care loop. All four launch breeds share one silhouette and differ only by recolor and markings, so one commission covers them all. Fish are cheaper but would shape the pet model around an edge case.

## Risks

- **Data loss** through slot reuse. Never free unreadable slots; test the write order.
- **False entitlement loss.** Lock only on a definitive store answer, never delete, and keep restore easy.
- **Store review** can reject the purchase flow. Review before H1.
- **Art cost and consistency.** Commission in layers, run the style check, and add one species at a time.
- **Untested code.** If the test globs are not widened, pet code goes untested.
- **Scope dilution.** The baby loop still has parity gaps.
- **Realism that reads as punishment** (hamster sleep, the cat hopping off). Playtest each. As built, the hamster's day nap asks nothing and closes excused, its Energy comes back through the nap whatever the player does, and only the wheel waits for the evening (no Bond, rhythm or score is lost for school hours).
- **Branch conflict.** Generalize the recolor only after the appearance branch merges.

## Decisions

The product owner delegated these choices on 2026-11-23. Revisit them with playtest data.

1. **Free companions:** keep `FreeCompanions = 2`. Revisit after the first external playtest.
2. **Paid unlock:** one non-consumable "+2 rooms" pack for a household of 4, at the lowest common price tier (about US$2.99). A second pack up to the 8 physical slots waits for demand.
3. **Freeing a slot:** "Say goodbye" with a keepsake. There is no returnable album, so the paid limit stays meaningful.
4. **Art:** commission the cat batch first (about 10 units), with the hamster in the same brief. Get real quotes before committing to the remaining species. H0 needs no new art and starts now.
5. **Species order:** cat, hamster, dog, fish, rabbit, as in the slice plan. The hamster proves the model is not cat-specific at the lowest art cost.
6. **Growth stages:** Young and Grown for every pet, with no senior stage.
7. **Breeds:** free starter, earned and premium breeds as in the Breeds section. The Corgi is a premium breed.
8. **Dog (P3, decided while building):** the morning walk is a grace window (a bonus that closes free when school gets in the way) and the evening walk keeps the day; the reunion greeting counts time from the last hands-on care, so Away mode counts and any care ends it; tricks come from games of fetch (not from walks), at fixed counts; the Corgi ships on its own short-legged base (premium, its art tier unchanged for the commissioned version) and the Dachshund is listed as arriving soon; dog wear is free; the ball launcher only lifts Exercise to its floor and never answers a walk window.

Consequences for neglect follow [care and growth](care-and-growth.md) and apply to pets as well as babies. Nothing ever dies, runs away for good, or is deleted.
