# First nursery playtest

Open the nursery in Unity Play Mode or in the Web preview. Use a fresh save to run the adoption flow. Use the same browser profile and preview URL when testing Web persistence.

| Step | Expected result |
| --- | --- |
| Open the nursery with a fresh save | The newborn is visible above the name prompt before adoption. |
| Adopt a baby and select **Play with rattle** | The newborn tracks the toy and starts to remember it. |
| Play twice with the plush bunny | The bunny moves toward the baby. The nursery shows the bunny as the favorite when it takes the lead. |
| Play, then rest | Play spends energy. Rest raises energy and cues a short nap. |
| Advance a preview day and select **Wash up → Change diaper** | Cleanliness falls as time advances. The cleanup panel opens without changing the saved state. |
| Tap or swipe all three marks, then finish | The change restores Cleanliness and shows the illustrated diaper cue. **Quick change** applies the same care without the activity. |
| Cancel after cleaning one mark | Cleanliness stays where it was, and the next cleanup starts fresh. |
| Select **Wash up → Bath time** | Cleanliness and Comfort rise, Energy falls a little, and the bath basket appears. |
| Keep basic needs met and let game time pass | Low Health can recover over time. Health falls only after a need spends time very low. |
| Open **Feed** before crawling | Milk is available. Mashed banana and soft oats show when they unlock. |
| Open **Feed** as a crawler | All three choices are available. Milk helps energy, banana helps comfort, and oats give the largest fullness increase. |
| Feed and soothe | Fullness and comfort rise, with a distinct reaction for each interaction. Age follows the game clock. |
| Select **Preview next day** three times and play with the familiar toy again | The baby sits and reaches for the familiar toy. The milestone appears once. |
| Open **A new sound** after sitting unlocks | The chime plays and the two responses preview their distinct effects. You can leave the choice pending and come back. |
| Choose **Comfort them** or **Explore together** | The chosen effects apply once and the notice stays resolved after reopening. |
| Advance from day six to day seven | A distinct crawling pose appears and the milestone is announced. Play with a familiar toy to see the baby crawl toward it. |
| Advance from day 13 to day 14 | The Early walker pose and first-steps announcement appear. Play with a familiar toy to see the first steps toward it. |
| Select **Rest** after crawling unlocks | The baby settles into a lying-down nap pose for the current stage. |
| Open **Development** and select **Read a story** | The storybook appears and the panel shows one more story session and increased connection. |
| Open **Decor** and switch from Sage to Peach | The room fades to the new illustration. Baby stats stay the same and the selected style survives reload. |
| Open **Style** and try each tab: Skin, Hair, Cut, Outfit, and Hat | Each choice updates the baby at once. Hats take the outfit's accent color. Stats and the current pose stay the same. |
| Change the look, then advance through each stage and select **Rest** | The same skin, hair, outfit, and headwear appear on every awake and nap pose. No halos, seams, or floating hats. |
| Reload after changing the look | The chosen look returns. A save from before this build keeps the original Warm, Chestnut, Curls, and Sage look. |
| At Early walker, open **Development → Stack blocks** | Choose large, medium, and small. A wrong size gives a hint. Finish the tower and see the saved count rise by one. |
| Cancel an unfinished tower | Development counts and needs stay where they were. |
| Open **Development → Baby report** | Current milestones and connection reflect the save. After repeated story time or block stacking, those interests appear alongside any favorite toy. |
| Close and reopen the same build | The same baby, look, energy, favorite toy, developmental stage, story sessions, towers built, connection, cleanliness, and health return. |
| Play at phone size | Check whether the gaze is clear, the posture reads as a new stage, the hands meet the toy, and the main controls fit without crowding. |

## Daily routine and consequences

Care slice 2 of [care and growth](care-and-growth.md). Needs follow the real clock: awake hours take Fullness 7, Comfort 4, Energy 6 and Cleanliness 9 an hour; quiet hours (from the bedtime due time until 07:00) take much less. An absence counts at most 8 hours of decay. The first 24 hours after adoption, and the first refresh after updating from an older build, are free: no window counts and nobody fusses about a routine they never saw.

| Step | Expected result |
| --- | --- |
| Look under the header at 390 x 844 | A slim strip shows Breakfast, Snack (Lunch on the Home preset), Dinner, Play, Bed and Wash. Each chip reads its opening time, **Now**, **Late**, **On time**, **Done late**, **Missed**, **Free** or **Away**. The strip ends above the baby's head and never overlaps the header or the care tray. |
| Shrink the window (a phone browser with its toolbars, about 390 x 664, or a desktop window such as 640 x 950) | The strip never covers the baby's head: where it would, it folds into a **Routine** button at the right of the status line, which names the window to look after (**Breakfast now**, **Snack late**) or reads **Routine**. Tapping it opens the Routine panel. |
| Open the Routine panel from the strip or the button, then press Escape or **Done** | Keyboard focus returns to the strip or the button that opened it. |
| Feed inside the breakfast window | The Breakfast chip reads **On time**. |
| Leave breakfast past 10:00 | The chip reads **Late** and the baby starts fussing: a gentle fidget, a slightly cooler tint and "is fussing. It's past mealtime." Feeding calms it and the chip reads **Done late**. |
| Leave breakfast past 12:00 | The chip reads **Missed** and the baby fusses for the missed meal until fed. Left unanswered, the baby becomes upset. |
| Let a need fall below 35, then below 15 | The baby fusses, then (after at least 15 minutes) becomes upset: crying copy, a stronger fidget, a droop and a cooler tint. Play, stories and blocks are pushed away and give nothing: **Play with plush** shows the plush sliding away, and **Stack blocks** is refused at once instead of after the tower is built. |
| Miss one meal on a new baby and leave it | The baby fusses until fed; one miss alone never upsets it. Two of the last three meals missed, unanswered, do. |
| Calm an upset baby | Feed until Fullness is 50 or more, then **Soothe** (or comfort a new sound). The baby is content again and plays. |
| Skip the day's wash | The next morning one spot appears on the floor (up to three). Each spot trims comfort gains by 5%. **Wash up** (bath or diaper) clears them all. |
| Feed when Fullness is 90 or more | The baby is "too full" and spits up a little: Comfort -10, Cleanliness -15, no Health change. It never counts as a meal. |
| Put the baby to bed in the bedtime window, then reopen in the morning | Energy is back to 100 (85 after a late bedtime). With no bedtime the baby dozes to 65 and wakes fussing until it rests. |
| Tap the strip (or **Routine**), then **Away 1 day** (or 3 days, 1 week, 2 weeks) | The chips read **Away**, the status says needs stay steady, and **We're back** replaces the choices. While away, needs settle no lower than 60 (lower needs stay put), Health holds, nothing is missed and age keeps its normal pace. |
| Reopen after the Away period ends | Windows count again from the planned end; a return a day late is an ordinary absence. Any care, or **We're back**, ends Away mode early. |
| Tap the strip (or **Routine**) and choose **Gentle** | Fussing starts only below 25, overdue meals never fuss, escalation waits 4 hours, and overfeeding costs less. **Standard** is the default. |
| Reopen a save from before this build | The baby opens as it was: content, no misses, tidy room, Standard style. The save becomes v13 on the next write. |

## Household

A household holds up to two babies for free (`HouseholdRules.FreeCompanions`). The first baby, and any baby saved before households existed, is slot 1 and keeps the original save key. Other babies are saved as `{key}.slot2` to `{key}.slot8`, and the household manifest is `{key}.household`. Each key has a `.backup`.

| Step | Expected result |
| --- | --- |
| Open a build that already has a baby | The nursery opens straight to that baby with no new onboarding. Stats, look and decor are unchanged, and a **Home** chip appears in the top bar. |
| Select **Home** | Home shows a rounded card with the baby's portrait for the current stage, name, stage and a mood line. Below the limit, a dashed **+ Adopt** card appears. |
| Select **+ Adopt**, enter a name and select **Start raising** | The same naming flow adopts a second newborn. **Back to home** returns to Home without adopting. The first baby's save is untouched. |
| Look at the nursery after the second adoption | The room shows the new baby's own look, decor and stats. A **Switch to …** chip next to **Home** names the other baby. |
| Select **Switch to …** | The nursery switches to the other baby's state, look and decor in one tap. Switching does not advance time or change needs. |
| Open **Home** with two babies | Both cards fit side by side at phone width, the active baby has an outline, and a calm "Your home is full" card replaces **+ Adopt**. There is no shop link. |
| Leave a baby off screen until a milestone age, then open their room | The milestone is announced once, the first time the room opens. Their Home card shows **New!** until then. |
| Let a baby who is off screen get very hungry or tired | A dot appears on the **Home** chip. |
| Close and reopen | The last active baby opens, and both babies keep their own progress. |
| On Home, select **Say goodbye**, then **Continue** and **Say goodbye** | Two confirmations are needed. The slot is freed, **+ Adopt** returns, and a keepsake line keeps the name and dates. **Keep** at either step changes nothing. |
| Corrupt one slot's save in storage and reopen | Home shows "Couldn't open this save" for that slot. The slot still counts toward the limit and is never reused or deleted. |
| Open the Web build in two tabs | Each tab overwrites the other's saves. Test with one tab at a time. |

The included character is a prototype asset for checking animation and game feedback. Record short clips from the same camera for art reviews. Confirm mobile frame pacing and lifecycle separately on Android and iOS hardware.
