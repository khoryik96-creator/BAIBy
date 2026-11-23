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
