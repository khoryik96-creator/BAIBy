# Care and growth

**Status: decided proposal.** Slices 1 (the shadow ledger), 2 (routine strip, need retune, Fussy and Upset, mess, overfeeding, Away mode and care styles) and 3 (Under the weather, Sick, the doctor, medicine and Recovering, for babies) are built; the helper is not. Numbers are starting tunables for playtests, and the open decisions are listed at the end.

It merges three proposals (care-to-growth, return loops, systems and tuning) with the user's request: care on time, every action affects growth, and real consequences for late, missed or poor care. It follows [pet mode](pet-mode.md) and [monetization](monetization.md) and names where it changes them.

## What changes

Today age alone unlocks milestones (sitting day 3, crawling day 7, walking day 14), and care only moves needs. The roadmap says growth follows elapsed time. This proposal keeps that and refines it: **time sets the pace, care shapes the outcome.** Each milestone keeps its nominal age, and care quality moves it to between 0.8x and 1.3x of that, so walking arrives around day 11.6 with excellent care and day 17.5 with neglect. Late, missed or poor care also moves the companion along a visible consequence ladder that ordinary care always reverses. The v5.3 missed-feeding game over becomes the ladder, and its daily quotas become diminishing returns.

Recommended roadmap wording: "Growth follows elapsed game time. Care quality shifts each milestone within 0.8x to 1.3x of its nominal age."

## Goals and non-goals

Goals: real-clock routines worth returning for; every action visibly feeding development; consequences felt within hours, escalating with severity and duration, always reversible through free care; one species-agnostic core that pets reuse through data.

Non-goals: death, permanent loss or deleted saves; guilt notifications, countdowns, or relief for sale; night feeds or school-hours slots; body or personality labels.

## Core model

The core never sees `CareAction`, `Food` or species. A species provides a `CareProfile` (data) and maps its verbs to care tags: `Meal`, `Together`, `Hygiene`, `Bedtime`, `Medicine`, `Tidy`. Three inputs drive everything:

| Input | Rule |
| --- | --- |
| Wellbeing W (0 to 1) | Per need `s = clamp((x - 20) / 40, 0, 1)`; `W = 0.5 min(s) + 0.5 mean(s)`, so one neglected need weighs more than an even spread |
| CareScore (0 to 1) | Ledger of the last 9 meals, 3 Together days and 3 bedtimes. On time 1, late 0.5, missed 0. `0.6 meals + 0.25 together + 0.15 bedtime`. Slots not filled yet count as on time, so empty rings count as 1 and a new baby's first miss weighs no more than a long-cared baby's |
| Bond (0 to 100) | Replaces the one-way Connection and can now fall |

The ledger advances by counted entries, not wall time, so a long absence cannot wipe old misses.

## Daily routine

The player sets a wake time (05:00 to 10:00, default 07:00), applied from the next care day. Care days start at 04:00 local. Windows default to a school-friendly day.

| Moment | Window | Late until | Core | Bonus |
| --- | --- | --- | --- | --- |
| Morning | 07:00 to 10:00 | 12:00 | Meal | Hygiene |
| Afternoon | 14:00 to 17:00 | 18:00 | Meal | Together |
| Evening | 18:00 to 20:30 | 21:30 | Meal | Bath or Story |
| Bedtime | 19:30 to 21:30 | +60 min | Rest | Bath, Story, Rest: Bond +3 |
| Night | until wake | none | none | Quiet hours |

- The **Home** preset, for a player who is around all day, has Morning 07:00 to 09:30 (late until 11:00), a lunch Afternoon 12:00 to 14:00 (15:00), Evening 17:30 to 19:30 (20:30) and Bedtime 19:00 to 20:30 (+60 min). In both presets the wake time moves only the Morning window.
- Feeding at Fullness 90 or more never counts as a meal. **Excused** (first 24 h, travel day, migration) and **Covered** (helper, Away mode) are left out of CareScore.
- A late bedtime restores Energy to 85 instead of 100. No bedtime means the baby dozes on the mat (Energy 65) and wakes Fussy. Playing at night costs Energy 15 and Comfort 10.

Need retune for real-clock meals (Fullness decays 10/h today):

| Need | Awake /h | Night /h | Notes |
| --- | --- | --- | --- |
| Fullness | 7 | 2.5 | Milk +35, banana +28, oats +40 |
| Comfort | 4 | 1 | |
| Energy | 6 | +9 in bed | |
| Cleanliness | 9 | 5 | |

The offline decay cap rises from 4 h to **8 h**, so a missed day is felt without needs collapsing.

## Development tracks

| Track | Quality Q | Baby milestones (nominal day) |
| --- | --- | --- |
| Motor | 0.40 W + 0.20 meals + 0.10 Bond + 0.30 E | Sit 3, Crawl 7, Walk 14 |
| Learning | 0.35 W + 0.10 meals + 0.20 Bond + 0.35 E | Babbles 4, Favourite story 9, First word 16 |
| Social | 0.30 W + 0.10 meals + 0.35 Bond + 0.25 E | Smile 2, Peekaboo 8, Waves 15 |
| Physical | 0.30 W + 0.50 meals + 0.20 Health | First tooth 6, growth checks 7 and 14 |

Engagement E per track comes from actions. Play and floor time feed Motor (3 to 4), Story, talk and New sound feed Learning (3 to 5), Soothe feeds Social (3), Blocks feeds Motor and Learning, and on-time meals and hygiene give Social 1.

`points = base x 1/(1 + 0.5n) x (0.4 + 0.6 W)`, where n is repeats of the same action in 6 hours. `E = min(1, 24-hour points / 10)`, with one action supplying at most 0.6. Grinding stops paying fast and shows a Bored reaction. Four or more categories in a day gives Bond +2. A new toy's first use in a stage gives x1.5.

## Milestone timing

```
dp/dt (per day) = cond / (D x (1.30 - 0.50 Q))      D = nominal stage length
unlock at p >= 1, and always by 1.4 x D
```

`cond` is 1.0 when well, 0.75 under the weather, 0.5 upset or recovering, 0 while sick before the doctor. Past D with W of 0.6 or more, the rate is x1.25 so late bloomers catch up. Milestones never re-lock. One unlocked during an absence plays live on the next visit.

| Play style | Q | Sit | Crawl | Walk |
| --- | --- | --- | --- | --- |
| Excellent | 0.94 | 2.5 | 5.8 | 11.6 |
| Average | 0.57 | 3.0 | 7.1 | 14.2 |
| Neglect | 0.10 | 3.8 | 8.8 | 17.5 |

A cumulative-points model was rejected: average players either hit the earliest date or slid to the cap.

## Bond and temperament

**Bond** gains: on-time meal +1, Soothe +3, Story +2, Play +1, and **answering a fussy or upset state within 30 minutes +5**. Losses: missed meal -2, fussy unanswered 2 h -2, each 3 h upset -1. Daily caps +12 and -8 (-4 while a helper covers). Bond never falls below 60% of its highest value, and gains double below that high-water mark.

**Temperament** has two hidden axes from -60 to +100, moving a fifth of the way to a daily target (about a 3-day half-life). Security follows answered fussing and missed meals: at -25 a clingy phase (Comfort decay x1.25, Soothe before Play); at +40 settled (plays alone). Curiosity follows variety: cautious or explorer. Copy is temporary and warm: "Going through a clingy phase, extra cuddles help." Pets use Trust for Security.

## Consequence ladder

Two axes, as in monetization. The **needs axis** follows needs and Health. The **attention axis** (Bond, temperament, setback, mess, rhythm) follows the ledger, and only hands-on care resolves it.

| Rung | Enter when | Seen and felt | Way back |
| --- | --- | --- | --- |
| Fussy | A need below 35, or a late meal | Whimpering, need bubble, music dims. Engagement x0.5 | Meet the need |
| Upset | A need below 15, fussy unanswered 2 h, 2 of the last 3 meals missed, or CareScore below 0.6 | Crying; **pushes toys away**, so Play, Story and Blocks give nothing; readiness x0.5; mess grows | Fullness 50, Comfort 40, then Soothe. Two on-time meals excuse the two oldest misses |
| Under the weather | Health below 50, or CareScore below 0.45 | Sniffles, droopy pose, Energy capped 70, readiness x0.75 | Free doctor visit, or one good care day |
| Sick | While present: Health below 30, or under the weather 18 h across 2 sessions | In bed with a fever icon, accepts only food and comfort, readiness paused | Doctor or vet scene, then Recovering |
| Recovering | After the visit | Blanket pose; Health +15 at once, recovery x2, readiness x0.5 | 2 medicine doses in 2 meal windows; ends anyway at 36 h if Health is 60 |
| Setback | While present: W below 0.3 for 2 days, 3 missed meals in 24 h, or sick 24 h without a doctor | **Newest skill goes shy**: the walker crawls to toys, readiness -0.15, never re-locks | One good day (3 on-time meals and 2 soothes) brings a "walking again" celebration |

Also felt: **overfeeding** (spit-up, Comfort -10, Cleanliness -15; three in 24 h cause a tummy ache, Health -8); a **messy room** (+1 item per missed moment, each cutting Comfort gains 5%, photo moments stop at 3, `Tidy` removes 2); a **growth chart** band (Thriving, Steady, "Needs regular meals", never weights); and the lost rhythm bonus.

Rules for any tunables: one `Advance` raises at most one rung; absence alone never passes Under the weather; Health never falls below 10; every rung has a tested way back; nothing on the ladder can be bought off.

## Absence

The first **36 counted hours** use the full rules. Then a helper covers ("Grandma popped in", a neighbour for pets): needs and Health held at 40, windows Covered, development at Q = 0.3, Bond -2 and Security -1 a day. The rest is computed in one step.

| From 19:00, Bond 55 | 1 day | 3 days | 2 weeks |
| --- | --- | --- | --- |
| Return state | Upset, crying, Fullness about 25 | Under the weather, Grandma's note | Under the weather, tidy room, postcards |
| Bond | About 51 | About 43 | Floor 33, "a bit shy" |
| Next milestone | About 6 h later | About 1.3 days later | About 2 days later, waiting to be shown |
| Way back | Feed, soothe, next meal | One good care day | 3 on-time moments over 2 days, doubled Bond gains |

**Away mode** (1 to 14 days, free, set before leaving) excuses windows, holds needs at 60, runs development at Q = 0.45, and costs no Bond or rhythm. Monetization says it pauses growth; this proposal prefers a neutral pace (decision 6). Paid Helpers count as Covered, never on-time care, with floors at 40 to 45, below the 60 that counts as well kept.

## Flagged decision: the strongest consequence

**"Staying with Grandma"** (baby) or **"staying with the pet sitter"** (pet). Not a default.

- **Trigger, present neglect only:** sick 48 counted hours without a doctor, across 3 sessions on 2 days. Absence never triggers it.
- **While away:** an empty crib with a postcard; development and Bond frozen, the slot kept.
- **Way back:** visits on 2 different days, each with an on-time meal, a Soothe and a Together action. Home in 24 to 48 hours, with a celebration.
- **Never** permanent, a countdown, purchasable, near the shop, or pushed in a notification.

The user named a **shelter** for pets. This proposal recommends against it: it implies surrender and adoption by others, which children read as permanent.

Risks: Google Play Families and Apple Kids review may treat caregiver separation as distressing; the UK Children's Code nudge standard and the OFT's virtual-pet guidance target loss-aversion mechanics; it echoes v5.3's "taken by the authorities" ending; it can turn a lapsed player into a lost one; it may affect the age rating.

Recommendation: build it behind a build flag, off. If shipped, only in a parent-gated **Realistic** care style. Styles: **Gentle** (no setback loss), **Standard** (default), **Realistic** (narrower windows, helper at 48 h, this option).

## Return loops

- **Daily rhythm:** three moments with different light and wants, plus one seeded bonus moment a day.
- **Care rhythm:** a day counts with 2 meals, a Together action and a bedtime. One automatic grace day per 7. A missed day halves the rhythm; Covered and Away days freeze it. At 3 days points are x1.05, at 7 x1.10, and that bonus is what a broken rhythm loses. Stickers at 3, 7, 14 and every 7 days stay forever, as do "Total care days" and "Longest rhythm".
- **Journal:** milestones, up to 3 photos a day, a weekly page, helper postcards, and "Hard days we got through."
- **Celebrations:** a live scene and an outfit per milestone, and an "Ahead, On track, Taking her time" projection.
- **Earned collections:** outfits, toys and decor from care, never sold, with weekly items returning later.

## Notifications

Local, opt-in, off by default, offered after the first complete care day behind the parental gate. Keep the household rule of **at most two a day**: 30 minutes into each of the moments the player picks, only if it is not done. A pure `ReminderPlanner` plans 48 hours ahead, so a player who stops opening the app hears nothing after 2 days. Nothing during quiet hours or Away mode. Copy is observational ("Breakfast time for Mia"); a test rejects words such as crying, sick, lonely or forgot. Consequences show in the game, never in a push. The Web build shows an in-page hint.

## Pets and household

| Species | Meals | Extra routine | Motor milestones | Mess |
| --- | --- | --- | --- | --- |
| Cat | Morning, Evening | Play | Pounce, climb, high jump | Scratched rug |
| Dog | Morning, Evening | Two walks | Fetch, leash walk, agility | Chewed slipper |
| Fish | Once a day | Weekly tank clean | Darts to glass, follows finger | Cloudy water |
| Rabbit | Hay, veg | | Binky, hop circuit | Scattered hay |
| Hamster | Evening only | Wheel | Wheel, tunnel | Messy bedding |

Each companion has its own ledger, ladder and checkpoint, sharing household moments, so one moment is one check-in and one reminder at most. Caring for one never penalizes another, and pacing never scales with household size.

## Architecture

New folder `Simulation/Care/`, plain C#: `CareProfile`, `CareTunables` with `Validate()`, an immutable `CareRecord` embedded in `BabyState` and later `PetState`, `RoutineCalendar`, pure `CareRules`, `CareDiagnostics` reason codes, and `ReminderPlanner`.

- `Advance` and `ApplyCare` return `AdvanceResult(State, Events)` instead of bool tuples. Presentation maps events to cues and never changes state.
- The clock returns a `DateTimeOffset` with the local offset. Elapsed time uses the UTC instant; the offset only places windows. The Web build adds a jslib-backed local clock, since WebGL may report UTC.
- A DST shift applies at the next care day; a jump over 60 minutes excuses that day; the care-day ordinal never decreases; a backwards clock changes nothing; a forward jump is absence.
- Consequences use no randomness. Within a session, split advances equal one advance. Across absences the counted cap is path dependent on purpose.
- Add a copy-with helper to `BabyState` (20 constructor arguments) before adding fields. Preview day advance counts as Covered.

## Save v12 and migration

`BabySaveCodec` v12 adds one `<care v="1">` element (wake time, rhythm offset, ordinal, ledger rings, condition and timers, tracks, Bond high-water, temperament, rhythm, mess, Away mode). A shared `CareRecordCodec` lets pet v1 records include it from the start.

Migration never surprises an existing baby. The first `Advance` excuses the whole gap. Rings start empty, the condition is well, Bond = Connection, Motor readiness follows current milestones, Learning is seeded from story sessions.

## Tests and tuning

Injected-clock checks cover window boundaries, DST and travel, clock rewinds, a 1-year absence matching 37 hours, the absence ceiling, one rung per advance, milestone bounds (0.8x, 1.3x, 1.4x), each rung's entry and exit, the grinding cap, v1 to v11 fixtures with no events on first advance, and reminder caps and banned words. Personas over 21 days (on time, late, once a day, weekend away, two-week break, active neglect, overfeeder, traveller) assert invariants at minimum, default and maximum tunables. A `tools/CareTuning` console runs them, and a dev panel shows the ledger, rung reason and timers.

## Slices

1. **Shadow ledger (one PR, built):** the `BabyState` copy-with helper, `RoutineCalendar`, the ledger and `CareRecord`, save v12 with migration, and calendar, clock and save tests. Windows are evaluated and saved but change nothing, visible only in a debug read-out (`BabySession.CareSummary`). A meal counts for the earliest window where it is on time, else the earliest where it is late. Together time is one entry per care day. Excused and Covered entries never enter a ring. A window that closes more than 36 hours after the last hands-on care is Covered, so split advances equal one advance. A travel jump excuses only the care day it lands on; the days it spans count like any absence, so changing the zone before reopening clears no misses. `Infrastructure/LocalClock` drops the cached time zone so a zone change while the app is alive is seen, and on the Web build reads the browser's offset through `LocalClock.jslib`. A newer `<care>` version is unsupported, not corrupt, so an older build leaves that save alone. `tests/Simulation/CareChecks.cs` proved needs and growth matched the pre-care simulation bit for bit (slice 2's retune retired that guarantee).
2. **Routine strip and the needs axis (built, without the helper):** `BabyNeeds` applies the retune by the local clock (quiet hours from the bedtime due time to the wake time, Energy restored to 100, 85 or 65 by that evening's bedtime rest) and the 8-hour cap; time before a care-day boundary keeps that day's offset and routine, so a DST shift or routine change gives the same needs in one advance or many. `CareConsequences` moves Content, Fussy and Upset at most one rung per advance, never while away, before an anchor or on a preview day; Upset waits at least 15 minutes of fussing and only a comforting action with Fullness 50 and Comfort 40 calms it. A missed meal or bedtime stays owed until answered. One wash window a day (morning opening to the evening meal's limits); a miss adds a mess item (at most 3, each cutting comfort gains 5%), hygiene clears them. Overfeeding at Fullness 90: Comfort -10, Cleanliness -15. Away mode (up to 14 days, free) covers windows, holds needs at 60 (lower needs stay), keeps development at its nominal pace and ends with any care; the absence ceiling counts from its planned end. Gentle softens every threshold (validated no harsher than Standard); Realistic is stored but behaves as Standard. The strip shows only where it clears the tallest pose; on shorter screens it folds into a Routine button in the status row. Save v13 carries `<care v="2">`; a v1 record clears its unseen rings and re-anchors, so its first advance excuses the gap and moves nobody. `tests/Simulation/ConsequenceChecks.cs` covers these rules.
3. **The health axis (built, babies only):** `CareRecord` keeps a second track beside the mood: Well, Under the weather, Sick or Recovering, with its cause and start, the counted time and visits under the weather, the start of the current visit, medicine doses with the meal window of the last one, a fresh-miss flag and the last good care day. `CareConsequences.SettleHealth` moves it after each advance, at most one rung, never while away, before an anchor or on a preview day. Well becomes Under the weather when Health falls below 50, or when CareScore is below 0.45 after a meal missed since the baby was last well (so a way back is never undone at once by the misses that caused it); an absence can do this. Under the weather becomes Sick only while the player is present: an advance within 30 minutes of the last (`PresenceGap`; a longer gap is a return and starts a new visit) and at least 15 minutes into the visit (`SickDwell`, so a returning player can still reach the doctor), with Health below 30 or 18 counted hours under the weather seen in two visits. Counted hours stop at the absence ceiling and skip Away mode, so they add up the same in one advance or many. Ways back: a good care day (no meal or bedtime missed or covered, two meals on time and together time) closed since the baby became unwell, with Health back at 50; or the free doctor's visit, which is hands-on care (it ends Away mode) and gives Health +15 at once. A sick baby, or one still below 50 after the visit, starts Recovering; a baby only a little under the weather goes home well. Recovering doubles Health recovery for 36 hours from the visit (a fixed window, so the boost is the same split or whole) and ends after two medicine doses in two different meal windows (`CareRules.MealWindowAt`: open to late-until, a second dose in the same meal counts once), or at 36 hours once Health is 60; Sick never ends by itself. Felt: under the weather or sick, Energy cannot rise past 70 (rest, milk and sleep stop there; higher Energy is kept); a sick baby stays in its nap pose under a blanket and gently refuses play, stories, blocks, baths and exploring (`Reaction.TooPoorly`) but accepts feeds, soothing, rest and changes. Health never falls below 10 (an older save below 10 never falls further). `CareConsequences.Readiness` returns the `cond` factor of Milestone timing (1, 0.75 under the weather, 0.5 upset or recovering, 0 sick) as a pure, tested function: development is still age-based, so slice 4's readiness timing applies it. Gentle softens every health threshold (Under the weather below 40 or CareScore 0.35, Sick below 20, after 24 hours in three visits, 30 minutes into a visit). The Care tray's bottom row gains a **Doctor** button (Dr. Lee's panel) or a **Medicine** button that says when the next meal is; a header chip names the condition; the baby is paler and droops, and a small bubble shows a sniffle, a thermometer or a capsule, all from UI and sprite shapes. Save v14 carries `<care v="3">`; every older record decodes well, with nothing counted and its visit starting at the checkpoint. `tests/Simulation/HealthChecks.cs` covers every entry, way back, presence, floor, medicine window and split advance, and the Play Mode smoke test walks a baby through sick, the doctor and two doses on a fake clock. Pets keep their own `PetSimulation` health rules: the vet scene will map onto the same track through `CareTags.Medicine` and the species' profile in slice 7.
4. Tracks, readiness timing, setback, projection and Report.
5. Bond, temperament, rhythm, journal and collections.
6. Opt-in reminders.
7. Pet profiles, cat first.
8. The flagged option, only after the decision and a store review.

## Risks

- Store policy: crying and sick states, reminders and above all the flagged option.
- Lapsed players returning to a shy, upset baby may quit. Playtest the helper, Bond floor and reunion gains.
- Fixed windows may not fit shift workers or every school day.
- Local-time bugs create unfair misses and need device and browser checks.
- No telemetry, so tuning rests on personas and playtests.
- Doc conflicts: pet mode's "no streak counters" and "nothing leaves", feature parity's dropped game over, and monetization's paused growth need updates after the decisions.
- Art scope for fussy, sick, doctor, clingy and helper scenes per stage and species.
- A guaranteed walk by about day 18 may make neglect feel cosmetic; refusals and the shy skill must carry the weight.

## Decisions

The product owner delegated these choices on 2026-11-23, asking for care that matters and consequences that are felt. Revisit them with playtest data.

1. **Milestone timing:** bounded, 0.8x to 1.3x of each milestone's nominal age, guaranteed by 1.4x.
2. **Strongest consequence:** "Staying with Grandma" for babies and "Staying with the pet sitter" for pets, never a shelter. It exists only in the Realistic care style, which a parent turns on behind the parental gate. The companion always comes home through a short welcome-home care task.
3. **Care styles:** Gentle, Standard and Realistic, with Standard as the default.
4. **Routine windows:** both presets as a player choice, with school-friendly windows as the default.
5. **Helper threshold:** 36 counted hours of absence before helpers take over.
6. **Away mode:** development runs at a neutral pace, up to 14 days. This replaces the paused growth in [monetization](monetization.md).
7. **Care rhythm:** replaces pet mode's "no streak counters". A missed day halves the rhythm instead of resetting it.
8. **Notifications:** up to two a day per household, opt-in, observational, with quiet hours overnight and during school hours.
9. **Milestones during an absence:** replay live on return.
10. **Body condition:** no body labels for any species. Treats affect only Fullness and Health.
11. **Retune:** accept the need retune and the 8-hour offline decay cap. Earlier playtest notes are superseded.
