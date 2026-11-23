# Premium and monetization

**Status: decided proposal.** Nothing here is built. Prices are placeholders mapped to store tiers. The decisions are listed at the end.

It merges an economy proposal and a policy review with the user's decisions: paid slots beyond the free 2, premium automation, cosmetics and rare skins. It follows `docs/pet-mode.md`. `docs/care-and-growth.md` did not exist yet, so its ladder, on-time windows and streaks come from its brief; recheck when it lands.

## Goals and principles

1. **Players buy convenience, more companions and looks.** Growth, Bond and Connection, development, milestones, Health and recovery are never sold.
2. **The free game is complete.** The consequence ladder is tuned and playtested with no paid items. A paid item never relieves a pain added so it could be sold.
3. **The audience includes children.** Every player is treated as a child: parental gate, real-money prices, nothing random or timed.
4. **Purchases never put a companion at risk.** A lost entitlement makes extra companions rest; nothing is deleted or degraded.
5. **One-time and offline.** No subscriptions, consumables, currency or ads at launch.

## What is free

- Every care action, stage, milestone and journal, and recovery from every consequence step.
- Two companions of any mix, every shipped species and breed, adoption, and "Say goodbye" with a keepsake.
- The base customization set (6 skin tones, 6 hair colors, 4 hair styles, 8 outfits, 4 headwear), the Sage and Peach nursery, and at least one free decor theme per future room.
- All real-human features, now and later: skin tones, hair colors and textures, facial and body features.
- Every look earned through care and one free seasonal item each season.
- A free **Away mode** ("Grandma visits", a pet-sitter) that pauses needs and growth for a planned absence.
- Notifications, streak protection, accessibility settings, save and restore.

## Premium catalogue

| Product | Does | Does not | Placeholder |
| --- | --- | --- | --- |
| `home.rooms2` "More rooms" | +2 household slots (4 total) | Speed growth or unlock species or breeds | $2.99 |
| `helper.feeder` | When Satiety (Fullness, Hay) reaches its floor, serves one stocked portion up to a ceiling below full. The player stocks up to 6 portions. | Act without stock, or give on-time, preference, milestone or streak credit | $2.99 |
| `helper.play` | When Comfort (Calm for fish) reaches its floor, a small top-up, at most 3 times per 24 h | Change familiarity, Connection, Bond, favorites, development counts or tricks | $2.99 |
| `helper.tidy` | When Cleanliness (Litter, Bedding, Water) reaches its floor, restores it to 70, at most 3 times per 24 h | Reach 100 (a full change, bath or water change stays hands-on), hygiene development | $2.99 |
| `helper.set` | All three helpers | Anything beyond the three | $5.99 |
| Look pack (Rainy Day, Little Explorer, Bedtime) | 6 to 10 named outfits, headwear or decor, previewed on the player's own companion | Affect any stat or reaction | $1.99 to $2.99 |
| Special coat | One named pattern on an existing breed (merle, tortoiseshell, aurora fins) | Change care, tendencies or Bond; it is not a breed | $0.99 to $1.99 |
| Baby special costume | Fantasy costume (dino onesie, bunny suit, star pajamas) | Change a skin tone, hair or any human feature | Inside look packs |

Helpers are household-wide one-time unlocks, so larger families do not pay more. There is no Rest or Health helper; sleep and Health recovery stay free.

## Automation, care and consequences

Helpers are a safety net, not a carer.

- **Floors and ceilings.** A helper acts only when a need falls to its floor. Each floor sits below the care-and-growth on-time threshold and above the low-need threshold of 30, where Health starts to fall. It tops up to a ceiling below full. Hands-on care always comes first and always looks better.
- **Two axes.** The consequence ladder splits into a needs axis (hungry to unwell), which helpers can hold back, and an attention axis ("missed you", quieter, slower interest growth), which only hands-on care resolves. Both stay reversible through free care.
- **Development-neutral.** Helper events never count toward development, milestones, familiarity, Bond, on-time windows or streaks. Helper-covered days count as absence under the free rules.
- **Coverage.** About 12 to 24 hours, then normal capped decay. Helpers never restore Health directly.
- **Presentation.** For pets, objects: an auto-feeder, a ball launcher, a self-cleaning litter box or filter. For babies a machine reads as neglect, so helpers appear as a **Sitter** who pops in.
- **Visibility.** Need bars show a floor marker for active helpers and a room prop shows stock. The return summary lists helper events first and attention lines last ("Mia missed you; a cuddle or story will help"), with no shop link. Journal entries use a muted gear or Sitter icon.
- **Copy.** Convenience, never protection: "Helpers keep things steady while you're busy. Only you can help Mia grow."

With today's 4-hour decay cap, helpers add little. Absence must not be made harsher to give them value.

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
- **Notifications.** Local only (no push token), off by default, at most one a day per household, quiet overnight and in school hours, observational ("Mia is waking up from her nap"). No guilt, streak loss, item names or shop links.

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
- **Simulation.** `HelperRules` tunables and plain `HelperSettings` data. Advance steps through the capped window in closed form, since decay is linear, and returns `HelperEvent` records without knowing about purchases. Feeder stock and activations bump the baby record to v12 and the pet record to v2; older saves load with helpers off.
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
| M4 Helpers | Feeder, Play, Tidy, Sitter for babies | M3 shipped; legal review; parent and child playtests |
| M5 Seasonal | First seasonal free item and pack | Returning-season copy reviewed |

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
3. **Helpers:** sold as one household set (Feeder, Play and Tidy together) to avoid many small purchases. Floors and ceilings follow the on-time threshold in [care and growth](care-and-growth.md).
4. **Babies:** baby helpers appear as a Sitter character. Pets use the Feeder, Play and Tidy helpers.
5. **Rare looks:** fantasy costumes for babies and special coat patterns for pets. Every real human feature (skin tone, hair, eyes) stays free. The rarest looks are earned through care and never sold.
6. **Store wording:** "Special", never "rare" or "limited".
7. **Seasonal content:** later, in M5.
8. **Family bundle:** not at launch.
9. **Audience:** treat every player as a child. There is no age screen, every purchase sits behind the parental gate, and there are no ads or third-party analytics.
10. **Revocation:** only an explicit refund or revocation from the store locks extra rooms. A missing item in a store query does not.
11. **Store listing:** no Kids Category listing at launch; publish as a family-friendly app that follows the child rules above. An external legal and store-policy review is required before M1 ships, and the product owner arranges it.
