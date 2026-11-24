# Premium and monetization

**Status: decided proposal.** M1's entitlements, parental gate, Restore Purchases, resting companions and Web flags, developer mode, **M4 Helpers** (see [Helpers: built in M4](#helpers-built-in-m4)) and the first cosmetics, **M2b Looks and rooms** (see [Looks and rooms: built in M2b](#looks-and-rooms-built-in-m2b)), are built; no store is (this build sells nothing). Prices are placeholders mapped to store tiers. The decisions are listed at the end.

It merges an economy proposal and a policy review with the user's decisions: paid slots beyond the free 2, premium automation, cosmetics and rare skins. It follows `docs/pet-mode.md`. `docs/care-and-growth.md` did not exist yet, so its ladder, on-time windows and streaks come from its brief; recheck when it lands.

## Goals and principles

1. **Players buy convenience, more companions and looks.** Growth, Bond and Connection, development, milestones, Health and recovery are never sold.
2. **The free game is complete.** The consequence ladder is tuned and playtested with no paid items. A paid item never relieves a pain added so it could be sold.
3. **The audience includes children.** Every player is treated as a child: parental gate, real-money prices, nothing random or timed.
4. **Purchases never put a companion at risk.** A lost entitlement makes extra companions rest; nothing is deleted or degraded.
5. **One-time and offline.** No subscriptions, consumables, currency or ads at launch.

## What is free

- Every care action, stage, milestone and journal, and recovery from every consequence step.
- Two companions of any mix, every shipped species with its starter breed and every earned breed, adoption, and "Say goodbye" with a keepsake.
- The base customization set (6 skin tones, 6 hair colors, 4 hair styles, 8 outfits, 4 headwear), the Sage and Peach nursery, and at least one free decor theme per future room.
- All real-human features, now and later: skin tones, hair colors and textures, facial and body features.
- Every look earned through care and one free seasonal item each season.
- A free **Away mode** ("Grandma visits", a pet-sitter) that holds needs steady and keeps development at a neutral pace for a planned absence of up to 14 days.
- Notifications, streak protection, accessibility settings, save and restore.

## Premium catalogue

| Product | Does | Does not | Placeholder |
| --- | --- | --- | --- |
| `home.rooms2` "More rooms" | +2 household slots (4 total) | Speed growth or unlock species or breeds | $2.99 |
| `breed.<species>.<breed>` premium breed (for example `breed.dog.corgi`) | Unlocks one premium breed for adoption, household-wide, as listed in [pet mode](pet-mode.md) | Change care rules, tendencies or Bond, or skip any earned-breed goal | $1.99 |
| `breedpack.<species>` | Every current premium breed of one species | Include breeds added later | $4.99 |
| `breedpack.all` "Every breed" | Every current premium breed of every shipped species | Include breeds added later | $14.99 |
| `helper.feeder` | When Satiety (Fullness, Hay) reaches its floor, serves one stocked portion up to a ceiling below full. The player stocks up to 6 portions. | Act without stock, or give on-time, preference, milestone or streak credit | $2.99 |
| `helper.play` | When Comfort (Calm for fish) reaches its floor, a small top-up, at most 3 times per 24 h | Change familiarity, Connection, Bond, favorites, development counts or tricks | $2.99 |
| `helper.tidy` | When Cleanliness (Litter, Bedding, Water) reaches its floor, restores it to 70, at most 3 times per 24 h | Reach 100 (a full change, bath or water change stays hands-on), hygiene development | $2.99 |
| `helper.set` | All three helpers | Anything beyond the three | $5.99 |
| Look pack `look.<pack>` (built: Cozy knits, Garden party; later Rainy Day, Little Explorer, Bedtime) | 6 to 10 named outfits, headwear or decor, previewed on the player's own companion | Affect any stat or reaction | $1.99 to $2.99 |
| Special coat | One named pattern on an existing breed (merle, tortoiseshell, aurora fins) | Change care, tendencies or Bond; it is not a breed | $0.99 to $1.99 |
| Baby special costume | Fantasy costume (dino onesie, bunny suit, star pajamas) | Change a skin tone, hair or any human feature | Inside look packs |

### More premium lines

These widen what players can buy without touching growth speed, development or consequences. Each is a one-time, non-random purchase behind the parental gate, previewed on the player's own companion or room before buying.

| Product | Does | Does not | Placeholder |
| --- | --- | --- | --- |
| `room.<theme>` room theme (built: Cloud nursery, Starlight; later Treehouse, Beach cottage) | A full room illustration with matching rug and lighting, for the nursery or any pet room | Change needs, decay or any care effect | $1.99 |
| `habitat.<species>.<theme>` habitat theme (Castle tank, Coral reef, Cozy burrow, Hamster maze) | A themed tank, burrow or cage with its own idle animation | Change Water, Calm, Bedding or any meter | $1.99 |
| `furniture.<set>` furniture set (Scandi, Rattan, Pastel play) | Crib or bed, shelf, lamp and wall decor placed in the room | Add a care action | $1.99 |
| `petwear.<pack>` pet accessory pack (built: `petwear.cat.bandanas-bows`; later collars, sweaters, a fish-tank castle) | Wearables and ornaments for every pet of the matching species | Change Bond, tendencies or care | $1.99 |
| `toy.<id>` premium toy (music box, plush dino, ball launcher, crinkle tunnel) | A new toy with its own look, sound and reaction. It counts as the same toy kind as a free one, so it adds no extra familiarity, variety or development credit | Raise familiarity faster or count as extra variety | $0.99 |
| `sound.<pack>` lullaby and music pack | Extra lullabies, music-box songs and ambient room sounds | Change Comfort or sleep rules | $0.99 |
| `album.<pack>` photo frames and stickers | Frames, stickers and backgrounds for the growth journal and shareable keepsake cards | Add new journal moments | $0.99 |
| `species.<pack>` extra species pack (later: Birds, Guinea pig, Turtle) | A new species with its own care loop and a free starter breed inside the pack | Lock any species from the five launch species | $3.99 |
| `supporter.founder` Founder pack (built) | A small set of exclusive looks, a room theme and a Founder badge on the household card, for players who want to support the game | Give gameplay advantages | $4.99 |

Declined lines. These would undermine the care loop or the audience rules: paid growth or milestone skips, paid later life stages for an existing baby, paid consequence removal beyond Helpers, energy or wait timers, season passes, daily login rewards tied to spending, randomized bundles, and ads (including rewarded ads).

Helpers are household-wide one-time unlocks, so larger families do not pay more. There is no Rest or Health helper; sleep and Health recovery stay free.

### Looks and rooms: built in M2b

The first premium cosmetics, made from the existing art with the offline tools (no new illustration). They change only how things look: care, growth, Bond, development and every meter are untouched.

| Product | What is inside |
| --- | --- |
| `look.cozy-knits` Cozy knits | Four knit outfits (Oatmeal, Berry, Moss, Fog: `Outfit` 8 to 11) and the Pom-pom beanie (`Headwear.PomBeanie`, a knit beanie with a fold-up cuff, a main-colour stripe and a pom-pom) |
| `look.garden-party` Garden party | Four garden outfits (Rosebud, Marigold, Garden mint, Bluebell: `Outfit` 12 to 15) and the straw Sun hat (`Headwear.SunHat`, an accent ribbon and a small rose) |
| `room.cloud-nursery` Cloud nursery | A sky-blue room with soft painted clouds and a cloud-white rug (`NurseryTheme.CloudNursery`), for the nursery and pet rooms |
| `room.starlight` Starlight | A twilight room with softened moonlight, a sprinkle of golden stars and a lavender rug (`NurseryTheme.Starlight`) |
| `petwear.cat.bandanas-bows` Cat bandanas & bows | A bandana and a neck bow for every cat pose (`PetAccessory.Bandana`, `Bow`), and three more colours for collars, bandanas and bows (Butter, Lilac, Terracotta) |
| `supporter.founder` Founder pack | The Fern & gold outfit (`Outfit.FernGold`), the Golden hour room (`NurseryTheme.GoldenHour`, a honey wall and a fern rug) and a **Founder** badge on the household card |

- **Catalogue.** `CosmeticCatalog` (Application, engine-free) maps each product to content IDs (`outfit.moss-knit`, `headwear.sun-hat`, `room-theme.starlight`, `wear.cat.bandana`, `wear-color.butter`, `badge.founder`) and answers `IsCosmeticUnlocked(contentId)` (also as an `IEntitlements` extension and on `HouseholdSession`). Free and earned looks are in no pack; the Daisy clip, Midnight and the Bucket hat stay earned-only and no product unlocks them. `DeveloperUnlocks.Products` grants every cosmetics product, and `?ent=` flags work as for every product family.
- **Saves.** Cosmetics are the content IDs the records already hold (appearance, nursery theme, a pet's accessory and colour): new enum members only, so a save without them is byte for byte the same and `GoldenScenario` is unchanged. The pet rooms share one theme, a `<rooms pet-theme="N"/>` manifest element written only while it is not Sage.
- **Refunds.** As decided: the saved content ID is kept and the display falls back to the free default (Sage outfit, no hat, the Sage room, a Sage collar for a cat's bandana or bow); owning the pack again restores the look. A part saved before a refund may stay while another part changes. While ownership is unknown (a damaged cache) saved looks keep showing.
- **Presentation.** Style (a **More** tab for the pack outfits, the new hats under **Hat**), Decor (five room cards, in the nursery and in a pet's room, where a cat also picks what it wears and its colour) and the cat adoption page show locked items with a small butter **Special** chip; tapping one previews it on the companion or in the room with "Part of the Cozy knits pack — a grown-up can get it in Grown-ups." Nothing is saved, and no care flow links to a shop or names a price. **Grown-ups → Looks & rooms** (behind the parental gate) lists the packs and what is inside, with **Add test pack** / **Remove** in test builds.
- **Art.** `tools/appearance/headwear.py` draws the two hats (`build.py --only-hats` adds hats without touching the rest); `tools/pets` draws the bandana and bow overlays; `tools/rooms/build.py` recolours the Sage backdrop's wall and rug (masked by where the artist's Peach version differs) into ~0.3 MB JPEGs. About 1.4 MB of new art in all.
- **Not built.** A store, a furniture set (`furniture.<set>`), special coats, and look packs for other species.

## Automation, care and consequences

Helpers are a safety net, not a carer.

- **Floors and ceilings.** A helper acts only when a need falls to its floor. Each floor sits below the care-and-growth on-time threshold and above the low-need threshold of 30, where Health starts to fall. It tops up to a ceiling below full. Hands-on care always comes first and always looks better.
- **Two axes.** The consequence ladder splits into a needs axis (hungry to unwell), which helpers can hold back, and an attention axis ("missed you", quieter, slower interest growth), which only hands-on care resolves. Both stay reversible through free care.
- **Development-neutral.** Helper events never count toward development, milestones, familiarity, Bond, on-time windows or streaks. Helper-covered days count as absence under the free rules.
- **Coverage.** About 12 to 24 hours, then normal capped decay. Helpers never restore Health directly.
- **Presentation.** For pets, objects: an auto-feeder, a ball launcher, a self-cleaning litter box or filter. For babies a machine reads as neglect, so helpers appear as a **Sitter** who pops in.
- **Visibility.** Need bars show a floor marker for active helpers and a room prop shows stock. The return summary lists helper events first and attention lines last ("Mia missed you; a cuddle or story will help"), with no shop link. Journal entries use a muted gear or Sitter icon.
- **Copy.** Convenience, never protection: "Helpers keep things steady while you're busy. Only you can help Mia grow."

With the 8-hour decay cap, helpers add most to a busy day with the app open or checked now and then, and only a little to a long absence. Absence must not be made harsher to give them value, and it was not.

### Helpers: built in M4

What shipped, and where it differs from the plan above:

- **Products.** `helper.feeder`, `helper.play`, `helper.tidy` and `helper.set` (all three) in `ProductIds`; `HelperOwnership` reads them from `IEntitlements`, and developer mode grants all four. Decision 3's household set is the product to show; the single helpers stay in the catalogue as the table lists them. No store, so they come from developer mode, `?ent=helper.set` or the playtest toggle in **Grown-ups → Helpers**.
- **Who.** Babies (the Sitter) and cats (auto-feeder, ball launcher, self-cleaning litter box): every companion with a care record. Dogs, fish, rabbits and hamsters keep their simple rules and ignore helpers until their care slice; the Hay, Calm, Litter, Bedding and Water names in the table wait for them.
- **Rules** (`HelperRules`, validated). Floors: Fullness 45, Comfort 40, Cleanliness 40 (above the low-need 30 and the Fussy 35, below the well-kept 60). The feeder serves one stocked portion up to 70 (below full and below overfeeding); the player fills it to 6 with a free, hands-on control in the Routine sheet ("Leave bottles", "Fill the feeder"), which is not care. Play tops Comfort up to 58 and tidy restores Cleanliness to 70, each at most 3 times a **local care day** (from 04:00, the ledger's day) rather than a rolling 24 hours, so the limit is the same in one advance or many. A need already below its floor (a helper just switched on, or out of uses until 04:00) is raised at the first instant it may act.
- **Coverage.** Helpers act only within **24 hours of the last hands-on care**; after that the needs fall as without them, and an absence still counts at most 8 hours of decay. With 6 portions and 3 top-ups a day this gives about 12 to 24 hours of cover.
- **Two axes.** A meal window the feeder served in, with no hands-on meal, is **Helped**: like Covered it stays out of the meal ring and CareScore and is not owed (so no fussing for it, and no push towards Upset or Under the weather), but Bond, temperament and the care rhythm count it as a **missed meal**, exactly as with no helper. Helpers therefore never change the attention axis: within one advance Bond, temperament and the rhythm are identical with and without them. Held needs do spare the companion the Bond cost of fussing from hunger, which is the needs axis doing its job.
- **Development.** Helper events add no engagement, familiarity, counts, meal credit, milestones or on-time credit, and a helped meal never enters the ring. Held needs keep wellbeing from collapsing, the way Away mode holds needs for free, so development keeps the pace those needs give, with no extra credit. Fully "neutral" development would need a shadow simulation without helpers, which split advances cannot reproduce; this is the decision taken.
- **Health.** Never raised directly: a helper changes only its own need at the instant it acts (Energy and Health are continuous across it), there is no Rest or Health helper, and a sick companion stays sick.
- **Stepping.** `HelperRun` steps through `BabyNeeds` in closed form: a counted stretch ends where a need reaches its floor, the helper raises it, and the next stretch starts there. `Advance` and the preview day take plain `HelperSettings` and add `HelperEvent` records (kind, instant, before and after) to a list; nothing in the simulation knows about purchases. Repeated resume applies nothing twice, a backwards clock nothing, and one advance equals many short ones within the cap. A **Next day** preview passes no real time: inside the coverage window helpers act through its awake hours as on a new day, at the checkpoint.
- **Saves.** Element versions, not record versions: the care element becomes v4 with a `<helpers>` child (stock, the day's counts, the last serve) and the Helped moment letter `H`, and the journal v2 with helper days (kind 6), both only once a helper has been stocked or has acted. Older builds leave such a save alone; older saves load with helpers off. The household's switches are a `<helpers off="…">` manifest element, written only while a helper is off. `GoldenScenario` replays four days of a baby and a cat and checks the bytes against the build before helpers.
- **Presentation.** Need rings show a small dark floor dot for each active helper; the bubble's return summary lists helper events first and the attention line last ("While you were busy, the Sitter gave Mia 2 bottles and freshened them up. Mia missed you; a cuddle or story will help.") and stays until the player cares; the journal keeps one muted day per helper day (a Sitter or gear disc); the strip's meal chip reads **Helper**; the cat's room shows the feeder's stock as a small bowl chip. The copy everywhere is "Helpers keep things steady while you're busy. Only you can help Mia grow." Nothing in a care flow links to a shop. **Grown-ups → Helpers** (behind the parental gate) switches each owned helper on or off for the household.
- **Not built.** A Sitter character on screen (the copy and the journal carry her for now), helper postcards beyond the journal days, the store, and notifications.

## Rare and seasonal cosmetics

- **No randomized paid rewards.** No loot boxes, gacha, mystery packs, random eggs or rerolls, bought with money or anything money buys. Every paid item is named and previewed.
- **Earned rarity.** Rare looks come only from hands-on milestones and are never sold: First Steps booties, storybook pajamas after 10 stories, a "best friends" coat.
- **Wording.** Store copy says "special" or "collector", not "rare". No rarity tiers, glow or comparison.
- **Seasonal.** Each season has one free item earned through an in-season care activity and an optional paid pack. Both return every year and say so ("Back every winter"). Once owned, usable all year. No countdowns, "last chance" or stock limits.
- **No currency.** Rewards are named unlocks ("Keepsakes"). A non-spendable sticker collection gives the collecting pleasure without an exchange rate.

## Audience and store-policy guardrails

**Hard rules** (store rules or law in force; breaking them risks rejection, removal or legal exposure):

| Rule | Source |
| --- | --- |
| Parental gate on the shop entrance, restore, external links and rating prompts | Apple 1.3 (Kids Category); built to that standard everywhere |
| Non-consumables with Restore Purchases | Apple 3.1.1 |
| No paid random items | Brazil ECA Digital (since March 2026), Belgium; odds disclosure under Apple and Google |
| No direct exhortation to children to buy, in any channel | EU UCPD Annex I No. 28, UK DMCC Act |
| Notifications optional and never promotional | Apple 4.5.4 |
| No personal data, no identifier-sending SDKs, no ads | COPPA, Google Play Families |
| Honor store age signals where required, never for marketing | Texas, Utah, Brazil; Play Age Signals terms |

**Best practice** (regulator guidance and enforcement direction):

- No purchase prompt tied to a companion's needs or feelings, a streak, a care reaction, a return summary or a notification. The shop opens only from Home or Settings (OFT principles, CPC, FTC v. Epic).
- No countdowns or fake scarcity (CPC, ICO Children's Code, the proposed Digital Fairness Act).
- A full household shows a calm "Your home is full" card; goodbye is never framed as pressure.
- **Parental gate.** An adult task such as typing a number written in words, varied each time and screen-reader accessible. No hold-to-pass.
- **Purchase flow.** Item, real local price, then the platform sheet, away from care controls. An Owned items screen points to store refunds. Ask to Buy and Play approvals show "Waiting for a grown-up" with no nag loop.
- **Notifications.** Local only (no push token), off by default, at most two a day per household, quiet overnight and in school hours, observational ("Mia is waking up from her nap"). No guilt, streak loss, item names or shop links.

## Entitlements, offline behaviour, refunds, Web

This extends the entitlement design in `pet-mode.md`.

- **Interface.** `IEntitlements` in `Application/Household` adds `Owns(productId)`, `HelperUnlocked(kind)`, `IsCosmeticUnlocked(contentId)` and a `Changed` event. `EntitlementCatalog` maps products to grants.
- **Store.** Only `BAIby.Store` references Unity IAP. Pin a version that sends no analytics or identifiers.
- **Cache.** `{saveKey}.entitlements` holds each grant's product, source and last confirmation. A corrupt cache never blocks companions, and records never store purchase state.
- **Offline.** A cached grant stays valid indefinitely; network errors never remove it. Receipts are validated on device, accepting some piracy.
- **Revocation.** Only a Play voided purchase or a StoreKit revocation withdraws a grant. Restore runs quietly at launch and is idempotent.
- **Refunds.**
  - *Slots:* extra companions become resting, chosen by the player as in `pet-mode.md`. Nothing is deleted and the lock is computed at runtime.
  - *Helpers:* they stop. Effects already applied stay.
  - *Cosmetics:* the saved content ID is kept and the display falls back to the free default. Buying again restores the look.
- **Family Sharing.** Enable on iOS. Google Play Family Library excludes in-app products; the shop says so.
- **Web.** No shop and no `BAIby.Store`. `TestEntitlements` reads a developer toggle or URL flags (`?ent=home.rooms2,helper.set`), shows a "Playtest unlocks" badge, and compiles only into WebGL, Editor and Development builds. A `FakeStore` simulates success, cancel, pending, failure, restore and refund.
- **Simulation.** `HelperRules` tunables and plain `HelperSettings` data. Advance steps through the capped window in closed form, since decay is linear, and returns `HelperEvent` records without knowing about purchases. Built with element versions instead of record versions: feeder stock and activations make the care element v4 and the journal v2 (the baby record stays v16 and the pet record v2); older saves load with helpers off.
- **Content.** Catalog entries carry `Access: Free | Earned | Product | Seasonal`.

## Tests

- **Simulation.** With no helpers, Advance matches today exactly (golden regression). Helpers respect floor, ceiling, stock and daily limits and never touch Connection, Bond, familiarity, counts, age, milestones or Health directly. Repeated resume applies helpers once, a backwards clock gives no activations, and logs are deterministic.
- **Application.** Capacity blocks adoption. A refund rests companions and deletes nothing. Pending purchases grant nothing. Restore is idempotent. A cached grant survives store failure. A locked cosmetic shows the fallback.
- **Architecture.** `architecture.test.mjs` fails if anything outside `BAIby.Store` references `UnityEngine.Purchasing`.
- **Content.** The validator rejects unpriced paid items, dangling grants and paid human features.
- **Manual.** A premium checklist in `regression.md` before submission: gate, restore, Ask to Buy, offline launch, refund, notification copy.

## Phased rollout

| Phase | Ships | Gate |
| --- | --- | --- |
| M1 Slots | Pet-mode H1: entitlements, Unity IAP, parental gate, restore, resting companions, Web flags, "More rooms" | Legal and store review of the purchase flow |
| M2 Looks | Two look packs, earned looks, special coats with the cat, validator rule | Earned looks ship alongside |
| M3 Free care tools | Away mode, tuned consequence ladder, notification rules | Playtested with no paid items |
| M4 Helpers (built, no store) | Feeder, Play, Tidy, Sitter for babies, objects for cats | M3 shipped; legal review; parent and child playtests |
| M2b Rooms and wear (built without the furniture set, no store) | Two room themes, one furniture set, one cat accessory pack, the Founder pack, and the first two baby look packs | M2 shipped |
| M4b Toys, sound and album | Premium toys, a lullaby pack, album frames and stickers | Variety rule tested so premium toys add no development credit |
| M5 Seasonal | First seasonal free item and pack | Returning-season copy reviewed |
| M6 Species packs | First extra species pack after all five launch species ship | Art commissioned |

## Risks

- **Pay to avoid harm.** Paid automation with felt consequences matches the UK OFT example of a virtual animal ill unless the player pays. The policy review rates it amber at best and recommends dropping it.
- **Purchase loop.** More slots raise care load, which then sells helpers. Away mode covers every companion and pacing must not scale with household size.
- **Selling human features** if "rare skin" is read as skin tone. The validator blocks it.
- **Baby automation optics** with parents and reviewers.
- **False entitlement loss.** Revoke only on definitive answers.
- **Core time path change.** The golden regression must land before helper stepping.
- **Hidden SDK data collection** from Unity IAP or Services defaults.
- **Policy wording.** Google Play text came from search summaries; check it live.

## Decisions

The product owner delegated these choices on 2026-11-23. Revisit them with playtest data and the legal review.

1. **Paid automation:** ship it as Helpers in M4, behind every guardrail above. Helpers never build development, Bond, Connection, familiarity, streaks or Health.
2. **Slots:** one "+2 rooms" pack at about US$2.99, matching [pet mode](pet-mode.md).
3. **Helpers:** sold as one household set (Feeder, Play and Tidy together) to avoid many small purchases. Floors and ceilings follow the on-time threshold in [care and growth](care-and-growth.md). (Built: `helper.set` is the product to show; the single helpers exist in the catalogue and work the same.)
4. **Babies:** baby helpers appear as a Sitter character. Pets use the Feeder, Play and Tidy helpers.
5. **Breeds:** each species has a free starter breed, earned breeds unlocked through visible care goals, and premium breeds sold directly (for example the Corgi). Premium breeds are never stronger.
6. **Rare looks:** fantasy costumes for babies and special coat patterns for pets. Every real human feature (skin tone, hair, eyes) stays free. The rarest looks are earned through care and never sold.
7. **Store wording:** "Special", never "rare" or "limited".
8. **Seasonal content:** later, in M5.
9. **Family bundle:** not at launch.
10. **Audience:** treat every player as a child. There is no age screen, every purchase sits behind the parental gate, and there are no ads or third-party analytics.
11. **Revocation:** only an explicit refund or revocation from the store locks extra rooms. A missing item in a store query does not.
12. **Store listing:** no Kids Category listing at launch; publish as a family-friendly app that follows the child rules above. An external legal and store-policy review is required before M1 ships, and the product owner arranges it.
13. **More premium lines:** add room and habitat themes, furniture sets, pet accessory packs, premium toys that add no development credit, lullaby packs, album frames and stickers, later species packs, and a Founder supporter pack, with the rollout above. Paid growth, timers, season passes, randomized bundles and ads stay out.
14. **Premium breeds:** 4 to 8 premium breeds per species as listed in [pet mode](pet-mode.md), at $1.99 each, $4.99 per species pack, or $14.99 for every current breed. Low-cost recolor breeds ship with each species; overlay and new-silhouette breeds follow as their art is ready.
