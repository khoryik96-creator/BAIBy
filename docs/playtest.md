# First nursery playtest

Open the nursery in Unity Play Mode or in the Web preview. Use a fresh save to run the adoption flow. Use the same browser profile and preview URL when testing Web persistence.

| Step | Expected result |
| --- | --- |
| Open the nursery with a fresh save | The newborn is visible above the name prompt before adoption. |
| Adopt a baby and select the **Rattle** tile | The newborn tracks the toy and starts to remember it. |
| Play twice with the plush bunny | The bunny moves toward the baby. The nursery shows the bunny as the favorite when it takes the lead. |
| Play, then rest | Play spends energy. Rest raises energy and cues a short nap. |
| Advance a preview day and select **Wash → Change diaper** | Cleanliness falls as time advances. The cleanup panel opens without changing the saved state. |
| Tap or swipe all three marks, then finish | The change restores Cleanliness and shows the illustrated diaper cue. **Quick change** applies the same care without the activity. |
| Cancel after cleaning one mark | Cleanliness stays where it was, and the next cleanup starts fresh. |
| Select **Wash → Bath time** | Cleanliness and Comfort rise, Energy falls a little, and the bath basket appears. |
| Keep basic needs met and let game time pass | Low Health can recover over time. Health falls only after a need spends time very low. |
| Open **Feed** before crawling | Milk is available. Mashed banana and soft oats show when they unlock. |
| Open **Feed** as a crawler | All three choices are available. Milk helps energy, banana helps comfort, and oats give the largest fullness increase. |
| Feed and soothe | Fullness and comfort rise, with a distinct reaction for each interaction. Age follows the game clock. |
| Select **Next day** three times and play with the familiar toy again | The baby sits and reaches for the familiar toy. The milestone appears once. |
| Open **A new sound** after sitting unlocks | The chime plays and the two responses preview their distinct effects. You can leave the choice pending and come back. |
| Choose **Comfort them** or **Explore together** | The chosen effects apply once and the notice stays resolved after reopening. |
| Advance from day six to day seven | A distinct crawling pose appears and the milestone is announced. Play with a familiar toy to see the baby crawl toward it. |
| Advance from day 13 to day 14 | The Early walker pose and first-steps announcement appear. Play with a familiar toy to see the first steps toward it. |
| Select **Rest** after crawling unlocks | The baby settles into a lying-down nap pose for the current stage. |
| Open **Growth** and select **Read a story** | The storybook appears and the panel shows one more story session and increased connection. |
| Open **Decor** and switch from Sage to Peach | The room fades to the new illustration. Baby stats stay the same and the selected style survives reload. |
| Open **Style** and try each tab: Skin, Hair, Cut, Outfit, and Hat | Each choice updates the baby at once. Hats take the outfit's accent color. Stats and the current pose stay the same. |
| Change the look, then advance through each stage and select **Rest** | The same skin, hair, outfit, and headwear appear on every awake and nap pose. No halos, seams, or floating hats. |
| Reload after changing the look | The chosen look returns. A save from before this build keeps the original Warm, Chestnut, Curls, and Sage look. |
| At Early walker, open **Growth → Stack blocks** | Choose large, medium, and small. A wrong size gives a hint. Finish the tower and see the saved count rise by one. |
| Cancel an unfinished tower | Growth counts and needs stay where they were. |
| Open **Growth → Baby report** | Current milestones and connection reflect the save. After repeated story time or block stacking, those interests appear alongside any favorite toy. |
| Close and reopen the same build | The same baby, look, energy, favorite toy, developmental stage, story sessions, towers built, connection, cleanliness, and health return. |
| Play at phone size | Check whether the gaze is clear, the posture reads as a new stage, the hands meet the toy, and the main controls fit without crowding. |

## Daily routine and consequences

Care slice 2 of [care and growth](care-and-growth.md). Needs follow the real clock: awake hours take Fullness 7, Comfort 4, Energy 6 and Cleanliness 9 an hour; quiet hours (from the bedtime due time until 07:00) take much less. An absence counts at most 8 hours of decay. The first 24 hours after adoption, and the first refresh after updating from an older build, are free: no window counts and nobody fusses about a routine they never saw.

| Step | Expected result |
| --- | --- |
| Look under the needs card at 390 x 844 | A slim dot timeline shows Breakfast, Snack (Lunch on the Home preset), Dinner, Play, Bed and Wash. A filled sage dot is done on time (yellow when done late, rose when missed), a peach ring is due now (a filled peach dot when late), a grey dot is later, a lilac dot is away, and a grey ring is free. The strip ends above the baby's head and never overlaps the header or the care sheet. |
| Shrink the window (a phone browser with its toolbars, about 390 x 664, or a desktop window such as 640 x 950) | The strip never covers the baby's head: where it would, it folds into a **Routine** button between **Next day** and **Growth** in the care sheet, which names the window to look after (**Breakfast now**, **Snack late**) or reads **Routine**. Tapping it opens the Routine panel. |
| Open the Routine panel from the strip or the button, then press Escape or **Done** | Keyboard focus returns to the strip or the button that opened it. |
| Feed inside the breakfast window | The Breakfast chip reads **On time**. |
| Leave breakfast past 10:00 | The chip reads **Late** and the baby starts fussing: a gentle fidget, a slightly cooler tint and "is fussing. It's past mealtime." Feeding calms it and the chip reads **Done late**. |
| Leave breakfast past 12:00 | The chip reads **Missed** and the baby fusses for the missed meal until fed. Left unanswered, the baby becomes upset. |
| Let a need fall below 35, then below 15 | The baby fusses, then (after at least 15 minutes) becomes upset: crying copy, a stronger fidget, a droop and a cooler tint. Play, stories and blocks are pushed away and give nothing: the **Plush** tile shows the plush sliding away, and **Stack blocks** is refused at once instead of after the tower is built. |
| Miss one meal on a new baby and leave it | The baby fusses until fed; one miss alone never upsets it. Two of the last three meals missed, unanswered, do. |
| Calm an upset baby | Feed until Fullness is 50 or more, then **Soothe** (or comfort a new sound). The baby is content again and plays. |
| Skip the day's wash | The next morning one spot appears on the floor (up to three). Each spot trims comfort gains by 5%. **Wash** (bath or diaper) clears them all. |
| Feed when Fullness is 90 or more | The baby is "too full" and spits up a little: Comfort -10, Cleanliness -15, no Health change. It never counts as a meal. |
| Put the baby to bed in the bedtime window, then reopen in the morning | Energy is back to 100 (85 after a late bedtime). With no bedtime the baby dozes to 65 and wakes fussing until it rests. |
| Tap the strip (or **Routine**), then **Away 1 day** (or 3 days, 1 week, 2 weeks) | The chips read **Away**, the status says needs stay steady, and **We're back** replaces the choices. While away, needs settle no lower than 60 (lower needs stay put), Health holds, nothing is missed and age keeps its normal pace. |
| Reopen after the Away period ends | Windows count again from the planned end; a return a day late is an ordinary absence. Any care, or **We're back**, ends Away mode early. |
| Tap the strip (or **Routine**) and choose **Gentle** | Fussing starts only below 25, overdue meals never fuss, escalation waits 4 hours, and overfeeding costs less. **Standard** is the default. |
| Reopen a save from before this build | The baby opens as it was: content, no misses, tidy room, Standard style. The save becomes v14 on the next write. |

## Health: under the weather, sick and the doctor

Care slice 3 of [care and growth](care-and-growth.md). Health is a second track beside the mood: Well, **Under the weather**, **Sick** and **Recovering**. It moves at most one step per refresh, an absence alone never makes a baby sick, and the doctor is always free. A refresh more than 30 minutes after the last one is a return and starts a new visit; the minute tick on the nursery keeps a visit going (Home's tick does not save, so a long stay on Home counts as a return).

| Step | Expected result |
| --- | --- |
| Adopt a baby and select **Next day** four times with no care in between | Each preview drains needs for up to 8 hours. Once Health is below 50, the next refresh (the next tap of any care or preview) puts the baby under the weather: a butter **Under the weather** chip with a thermometer appears in the header card (under Style and Decor; the favorite toy line under "Care for …" steps aside), the baby looks paler and droops a little, a bubble with a blue sniffle drop floats beside the head, and a **Doctor** (Free visit) tile joins the care tiles at the end of the second row. Nothing overlaps the baby's face or the care tiles at 390 x 844. |
| Rest or feed milk while under the weather | Energy stops at 70 (higher Energy is kept). Play, stories and baths still work. |
| Stay in the nursery for 15 minutes with Health below 30 (keep the tab in front) | The baby becomes **Sick — needs the doctor** (rose chip): it lies in its nap pose under a lilac blanket, a thermometer bubble replaces the sniffle, and the status says it is resting with a little fever. Returning to the app never makes a baby sick on the spot; the 15 minutes count from the return. |
| Tap **Rattle**, **Read a story**, **Stack blocks** or **Wash → Bath time** while sick | Gently refused: "feels too poorly for that and stays snuggled in bed", a small shake, and nothing changes. **Feed**, **Soothe**, **Rest** and **Change diaper** still work. |
| Tap **Doctor**, then **Visit Dr. Lee** | The panel invites a free visit, then says "Dr. Lee listens, checks a temperature and says Mia needs rest and medicine." Health rises by 15. A sick baby, or one still below Health 50, is now **Recovering · medicine 0 of 2** (sage chip, the quilt and a capsule bubble); a baby only a little under the weather goes home well. Dr. Lee's sheet shows the three steps (visit, medicine at a meal, medicine at the next) with the current one in sage. **Not now** closes the sheet before the visit and **Done** after it. |
| Look at **Medicine** outside a meal window | The tile is dimmed and says when the next meal starts (School day: breakfast 07:00 to 12:00, snack 14:00 to 18:00, dinner 18:00 to 21:30). |
| Tap **Medicine** during a meal, then again in the same meal | The first dose counts ("Dose 1 of 2") and the chip reads **1 of 2**; the tile then reads "Given · next meal" until the next meal window. |
| Give the second dose in another meal window | "Well again!": the chip, the blanket and the button disappear. If Health is still below 50, the baby is under the weather again at the next refresh, so keep feeding and resting. |
| Leave a recovering baby alone for 36 hours | Recovering ends by itself once Health is 60 or more. Health recovers twice as fast for the first 36 hours after the visit. |
| Give an under-the-weather baby a good care day: two meals on time, nothing missed, play or a story, and bedtime | After the care day ends (04:00) the baby is well again, without the doctor, if Health is back at 50. |
| Choose **Gentle** in the Routine panel | Under the weather starts below Health 40, Sick below 20 and only after 30 minutes present. |
| Open **Home** | The card reads "A bit under the weather", "Poorly, would love the doctor" or "Getting better", and a sick baby lights the Home dot. |
| Reopen a save from before this build | The baby opens well, with nothing counted. The save becomes v14 (care v3) on the next write. |

## Household

A household holds up to two babies for free (`HouseholdRules.FreeCompanions`). The first baby, and any baby saved before households existed, is slot 1 and keeps the original save key. Other babies are saved as `{key}.slot2` to `{key}.slot8`, and the household manifest is `{key}.household`. Each key has a `.backup`.

Owned products are cached offline at `{key}.entitlements` (with a `.backup`, written from the first grant on). The **More rooms** product (`home.rooms2`) adds two rooms, for four. The cache is trusted at launch; only a definitive refund or revocation removes a product, and a network error never does. A cache that can't be read and has no readable backup counts as unknown, not as owning nothing: every existing baby stays awake until a store answers. This build has no store. Web, Editor and Development builds offer a playtest toggle for the extra rooms instead, and a Web preview URL can grant products with `?ent=home.rooms2`. Release builds ignore playtest grants left in the cache by a test build. Which companions rest is computed at runtime and never written into a save.

| Step | Expected result |
| --- | --- |
| Open a build that already has a baby | The nursery opens straight to that baby with no new onboarding. Stats, look and decor are unchanged, and a **Home** button appears at the left of the header card. |
| Select **Home** | "Your home" shows how many rooms are in use and a card with the baby's portrait on a round pastel disc, name, "stage · Day N", a mood pill and **Visit**. Below the limit, a dashed **Adopt** card appears. |
| Select **Adopt**, then **A baby**, enter a name and select **Start raising** | "Who's joining your home?" offers **A baby** and **A pet**. The baby's naming flow adopts a second newborn. **Home** (beside the name prompt) returns to Home without adopting. The first baby's save is untouched. |
| Look at the nursery after the second adoption | The room shows the new baby's own look, decor and stats. A **Switch to …** pill in the care sheet's heading row names the other baby. |
| Select **Switch to …** | The nursery switches to the other baby's state, look and decor in one tap. Switching does not advance time or change needs. |
| Open **Home** with two babies | Both cards fit side by side at phone width, the active baby has an outline, and a calm "Your home is full" row replaces **Adopt**, with a small **More rooms** link. No purchase prompt appears in any care flow. |
| Select **More rooms** or the **Grown-ups** gear on Home | A "For grown-ups" gate asks you to type a number written in words using digits. A wrong answer draws a new number with no hint; **Back** returns to Home. Each opening draws a number different from the last one shown. |
| Pass the gate from **More rooms** on a device build | A calm "coming soon to the app stores" panel opens. Nothing can be bought. |
| Pass the gate from **More rooms** in a Web, Editor or Development build | The Playtest unlocks panel opens. **Add 2 test rooms** raises the home to four rooms, Home shows a "Playtest unlocks" chip, and **Adopt** returns. The grant survives a reload. Any playtest grant, including one from a `?ent=` URL flag such as `?ent=helper.set`, shows the badge. |
| Pass the gate from the **Grown-ups** gear in a Web, Editor or Development build and select **Developer mode: off · unlock everything** | Developer mode turns on: every product this build knows is granted as a playtest grant (the two extra rooms, every premium breed and breed pack) and earned breeds count as earned, so the breed picker offers every breed whose art ships. Home shows a "Developer mode" chip. It survives a reload, and opening the preview with `?dev=1` (for example `/nursery/?dev=1`) turns it on too. Saved progress toward earned breeds is not changed. Selecting it again turns it off: the grants are removed (companions beyond the free rooms rest, as after a refund) and locks return. Release device builds don't show the button. |
| Adopt a third baby, then select **Remove test rooms** | This works like a refund. Babies beyond the first two in Home order show **Resting** on a quieter card. Their saves are not written and their needs do not change. Tapping a resting card opens a read-only journal. A resting baby cannot be opened, switched to or said goodbye to, and the nursery opens an awake baby instead. |
| On a resting card select **Wake**, then pick who rests instead | The two babies swap places in Home order. The one going to rest keeps its time up to now; the woken one catches up with one normal, capped advance. The choice survives a reload. |
| With a baby resting, select **Say goodbye** on an awake baby and **Continue** | The confirmation says the room goes to the first resting baby, who wakes up. Confirming wakes that baby; Home stays full and **Adopt** does not return. **Keep** changes nothing. |
| Add the test rooms again | Everyone wakes. Each woken baby gets one normal, capped advance, the same as after any absence: needs drop by no more than the usual cap, while age counts the whole rest, so a long rest can bring milestones that are celebrated on waking. Home order is kept. |
| Damage `{key}.entitlements` (and its backup) while four babies live at home, then reload | Nobody rests and nothing is lost. The home holds the babies it has and offers no new room. |
| Pass the gate from the **Grown-ups** gear and select **Restore purchases** | "Your purchases are up to date." Nothing owned changes. |
| Leave a baby off screen until a milestone age, then open their room | The milestone is announced once, the first time the room opens. Their Home card shows **New!** until then. |
| Let a baby who is off screen get very hungry or tired | A dot appears on the **Home** button. |
| Close and reopen | The last active baby opens, and both babies keep their own progress. |
| On Home, select **Say goodbye**, then **Continue** and **Say goodbye** | Two confirmations are needed. The slot is freed, **Adopt** returns, and the Keepsakes box keeps the name and dates. **Keep** at either step changes nothing. |
| Corrupt one slot's save in storage and reopen | Home shows "Couldn't open this save" for that slot. The slot still counts toward the limit and is never reused or deleted. |
| Open the Web build in two tabs | Each tab overwrites the other's saves. Test with one tab at a time. |

## Cats (P1)

A household holds babies and pets together, two for free (babies and pets combined). A pet never takes slot 1, the original baby key, so an older build never reads a pet. A cat is saved as a `<pet version="1">` record in `{key}.slot2` to `{key}.slot8`. The manifest records each companion's kind and, per species, the care days and the best Bond and growth any pet has reached, which unlock earned breeds for good. A household with no pet data still writes the same v1 manifest as before; once a pet exists it is v2, which older household builds leave untouched. The cat art is placeholder art (`tools/pets`).

| Step | Expected result |
| --- | --- |
| Open a fresh install (or a new `?key`) | "Who's joining your home?" offers **A baby** and **A pet** cards. |
| Select **A pet** | The cat appears in the room. Cat is the only species that can be chosen; Hamster, Dog, Fish and Rabbit are dimmed "Arriving soon" tiles with no lock or price. **Back** returns to the choice. |
| Select **Cat** and try the breeds | Every breed tap updates the cat in the room. Domestic shorthair is free. Tabby, Tuxedo, Siamese and Calico say **Locked** and how to earn them; Bengal, British shorthair and Russian blue say **Premium** and that nothing is sold in this build. A locked breed can be previewed but **Choose a name** stays disabled. Coats and the collar color update the preview. |
| Name the cat and select **Welcome home** | The cat's room opens: the header card shows the name and "Day 1 · Kitten" with the **Home** button, the needs card shows the four cat rings (Fullness, Energy, Comfort, Clean), and the care sheet reads "Care for …" with the Bond tier under it and one tile per care verb. Slot 1 stays empty. |
| Try **Feed**, **Pet**, **Wand play**, **Brush** and **Nap** | Each changes the meters and shows a warm cue line; petting and wand play grow warmer as Bond grows (purring, slow blinks, kneading, bringing the toy back). **Nap** curls the cat into its rest pose for a moment; a sleepy cat (Energy below 30) rests until it has napped. |
| Give the cat 2 meals, petting or play, and a nap in one day | The cue line says it was a full day of care and announces **Tabby** as a new breed to adopt. Reaching Bond 40 (about 14 pets) unlocks the **Tuxedo**, raising a cat to Grown unlocks the **Siamese**, and a 7-day care rhythm unlocks the **Calico**. Unlocks survive a goodbye and a reload. |
| Select **Next day** seven times | The cat grows up: the day line says "Grown cat", the Grown poses replace the kitten's, and the room says the cat is all grown up, once. |
| Open **Home** | The cat's card shows its portrait on a peach disc, a paw badge for the species, its stage and day and a mood pill. **Adopt** still offers a baby or a pet while a room is free; a baby and a cat fill the two free rooms. |
| Adopt a baby beside the cat, then switch | **Switch to …** moves between the baby's nursery and the cat's room in one tap. Both advance on launch and resume, whichever is on screen. |
| Remove the playtest rooms with a cat third in Home order | The cat rests like a baby: a quiet card, no decay, no writes, and a read-only pet journal. |
| Say goodbye to the cat | The Keepsakes box names the species, for example "Pip (cat, Nov 24 – Nov 24)". |
| Grant a premium breed with `?ent=breed.cat.bengal` | The Bengal tile says **Owned** and can be adopted. Ragdoll, Maine Coon and Sphynx are not listed until their art ships. |

The included character is a prototype asset for checking animation and game feedback. Record short clips from the same camera for art reviews. Confirm mobile frame pacing and lifecycle separately on Android and iOS hardware.
