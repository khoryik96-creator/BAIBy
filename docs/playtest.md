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
| Feed, change, soothe and play, then select **Next day**; repeat until the baby sits (three or four times) and play with the familiar toy again | The baby sits and reaches for the familiar toy. The milestone appears once. Care sets the day: see [Development](#development-readiness-tracks-and-setback). |
| Open **A new sound** after sitting unlocks | The chime plays and the two responses preview their distinct effects. You can leave the choice pending and come back. |
| Choose **Comfort them** or **Explore together** | The chosen effects apply once and the notice stays resolved after reopening. |
| Keep caring and selecting **Next day** until crawling (between day 6 and day 10) | A distinct crawling pose appears and the milestone is announced. Play with a familiar toy to see the baby crawl toward it. |
| Keep going until first steps (between day 12 and day 20) | The Early walker pose and first-steps announcement appear. Play with a familiar toy to see the first steps toward it. |
| Select **Rest** after crawling unlocks | The baby settles into a lying-down nap pose for the current stage. |
| Open **Growth** and select **Read a story** | The storybook appears and the panel shows a higher Bond. The report counts one more story. |
| Open **Decor** and switch from Sage to Peach | The room fades to the new illustration. Baby stats stay the same and the selected style survives reload. |
| Open **Style** and try each tab: Skin, Hair, Cut, Outfit, and Hat | Each choice updates the baby at once. Hats take the outfit's accent color. Stats and the current pose stay the same. |
| Change the look, then advance through each stage and select **Rest** | The same skin, hair, outfit, and headwear appear on every awake and nap pose. No halos, seams, or floating hats. |
| Reload after changing the look | The chosen look returns. A save from before this build keeps the original Warm, Chestnut, Curls, and Sage look. |
| At Early walker, open **Growth → Stack blocks** | Choose large, medium, and small. A wrong size gives a hint. Finish the tower and see the saved count rise by one. |
| Cancel an unfinished tower | Growth counts and needs stay where they were. |
| Open **Growth → Baby report** | Current milestones and Bond reflect the save. After repeated story time or block stacking, those interests appear alongside any favorite toy. |
| Close and reopen the same build | The same baby, look, energy, favorite toy, developmental stage, story sessions, towers built, Bond, care rhythm, journal, cleanliness, and health return. |
| Play at phone size | Check whether the gaze is clear, the posture reads as a new stage, the hands meet the toy, and the main controls fit without crowding. |

## Development: readiness, tracks and setback

Care slice 4 of [care and growth](care-and-growth.md). Each milestone has a nominal day (sitting 3, crawling 7, walking 14). Good, varied, on-time care brings it sooner, down to 0.8x; neglect makes it later, up to 1.3x; and it always comes by 1.4x (day 4.2, 9.8 and 19.6). Skills never go away.

The quickest way to see faster and slower milestones is a Web, Editor or Development build with **Developer mode** on (Grown-ups gear on Home, or `?dev=1`) and the **Next day** button. A preview day passes no real time: it ages the baby a day and drains the needs for up to 8 hours, so the care you give before each tap is what it rewards.

| Step | Expected result |
| --- | --- |
| Adopt a baby, open **Growth** | A mint card under the day line reads "Likely to sit up in about 3 days" with an **On track** pill and a hint ("Meals on time, play and cuddles keep it coming."). |
| Before each **Next day**: feed (twice if hungry), change, soothe, play with the rattle and the plush (or read a story), and rest | The baby sits on the third preview day, crawls on about the seventh and walks on about the fourteenth. After play the card shows a shorter wait and, when it is short of the nominal day, **Ahead**. |
| Adopt another baby and select **Next day** with no care at all | Needs fall and the card turns to **Taking their time** ("Mia is taking their time with sitting up. Play and cuddles help."). The baby sits on the fourth preview day, crawls on about the ninth and walks on about the seventeenth. |
| Play with the same toy, or read, four times in a row | The fourth time the baby gives a small head shake: "looks a little bored of that. Try something different!" Repeats within six hours count for less; another kind of play is fresh. A preview day passes no real time, so repeating the same play before every **Next day** soon looks bored too. |
| Feed, change, soothe and read on the same care day | The fourth kind of care adds 2 to Bond, once a day. |
| Open **Growth → Baby report** | A Growth section lists Moving, Learning, Bonding and Growing as Flourishing, Steady, or what helps ("Stories help"), and the growth chart band: Thriving, Steady or "Needs regular meals". The next milestone reads "Likely in about N days"; reached ones read **Unlocked**. |
| Real time: leave a crawler or walker for a day so three meals are missed, come back and leave it unfed for 15 minutes in the nursery | A setback: the newest skill feels shy. A walker stands and crawls like a crawler ("feels a little shy about walking and crawls to toys for now"), a crawler sits. The skill stays in the report as **A bit shy**, blocks stay open, and readiness for the next skill drops a little. Feeding within the first 15 minutes of the visit avoids it. Gentle care has no setback. |
| Give a shy baby one good care day: three meals on time and two soothes | On the last of them the baby walks (or crawls) again with a celebration: "is walking again! A good day of meals and cuddles helped." The Growth card counts the good day while it lasts ("meals on time 1 of 3, cuddles 0 of 2"). |
| Make the baby sick (see Health below) and open **Growth** | "Growing waits while Mia is poorly"; readiness pauses until Dr. Lee's visit, and the guarantee still holds. |
| Reopen a save from before this build | The baby keeps every milestone it has, and the time since it was saved counts at the nominal pace, as before; from then on care sets the pace. The save becomes v16 on the next write. |

The developer read-out (`BabySession.CareSummary`) ends with the growth line: next skill, readiness, the projection in days, W and the four qualities, and any shy skill.

## First words and Talk time

[Care and growth](care-and-growth.md#first-words-and-talk-time). First words come between day 15 and day 18 with good
care and always by day 23; Talk time opens with them. The quickest way there is a Web, Editor or Development build with
**Developer mode** on (Grown-ups gear on Home, or `?dev=1`) and **Next day**: before each tap feed, change, soothe and
read a story (**Growth → Read a story**), about 17 taps. Tapping **Next day** with no care at all also gets there, at
the guarantee on day 23 (the baby is poorly on the way, which pauses readiness).

| Step | Expected result |
| --- | --- |
| Walk, then open **Growth** | The projection card reads "Likely to say first words in about N days". |
| Keep caring and selecting **Next day** | "Mia said their first words, and it's in the journal! Talk time is open: tap Talk to share how you feel." A lilac **Talk** tile joins the care sheet and the journal has **First words!** |
| Tap **Talk**, then **Proud** | The sheet shows "Mia says" and one or two words ("Yay you!"); the bubble repeats them and Mia bounces. Sad or Lonely gets a cuddle, Worried, Angry or Tired a calm sit. There is no text field: typing is off by default. |
| Tap more feelings the same day | Mia answers each time with a different line; Bond and the needs do not move after the first talk of the day. The journal keeps one line a day: "You told Mia you felt proud. Mia did a happy dance." |
| **Grown-ups → Let children type in Talk time** (behind the parental gate) | The button reads "Children can type in Talk time · tap to turn off" and the note says the words are never saved. Back in Talk, a field "Tell Mia more…" sits above the chips. |
| Type "I'm so excited for my birthday" and **Send** | Mia reads the feeling and topic ("Birthday! Yay!", or "Party time! Happy dance!" from day 21 or Bond 60). The field clears. |
| Type a sentence about being hurt or not wanting to live | The chips give way to the calm message ("That sounds really hard, and it matters. Please tell a grown-up you trust right now…"); the bubble stays empty and nothing in the game changes. The journal says only "We talked about something big". |
| Type "this test is killing me" | A normal, gentle reply about school (Worried), not the safety message. |
| On a phone, focus the field | The keyboard opens and the sheet rises so the field stays above it; the baby stays visible above the sheet. |
| Reopen a save from before this build at day 20 or later | First words arrive on the first visit, with the celebration. |

## Bond, the care rhythm and the journal

Care slice 5 of [care and growth](care-and-growth.md). **Bond** (0 to 100, in **Growth** and the Baby report) grows with on-time meals, cuddles, stories and play, and most of all when fussing or crying is answered within half an hour; it falls a little with missed meals and long unanswered distress, never below 60% of the best it has been, and gains count double on the way back. A day's gains stop at +12. The **care rhythm** counts care days in a row: two meals, play or a story, and bedtime. The **journal** keeps the arrival, milestones, photos, stickers, the hard days you got through and a page for each week.

The quickest way to see all of it is a Web, Editor or Development build with **Developer mode** on (Grown-ups gear on Home, or `?dev=1`) and **Next day**. A preview day passes no real time, so it closes a "preview care day" from the care given since the last tap: two feeds that were not too full, play (or a story) and **Rest** count it; anything less leaves the rhythm where it was. Each preview day also starts the day's Bond cap again.

| Step | Expected result |
| --- | --- |
| Adopt a baby, open **Growth** | The cards read **Care rhythm 0 days**, **Bond 0 / 100** and **Towers built 0**, and a fourth tile, **Journal**, sits beside the report. |
| Open **Journal** | "Mia's journal" with a line such as "Day 1 · Newborn · Bond 0 · Rhythm 0 days", four tabs (**Moments**, **Weeks**, **Hard days**, **Stickers**) and arrows to turn the pages. **Moments** starts with "Day 1 · Mia came home". **Stickers** says "Next sticker at 3 days" and lists the Daisy clip (3 days), Midnight outfit (7) and Bucket hat (14) as earned with a care rhythm. **Done** goes back to Growth. |
| Play with the rattle, then tap **Photo** in the care sheet three times | "Click! A photo of Mia playing is in the journal. 2 more today." The tile then reads **Photos done** and a fourth tap says "That's today's three photos of Mia. More tomorrow!" Moments shows "Day 1 · Playtime" with the baby's own pose and look on a round picture. A photo right after a meal, a soothe, a story, a bath or a nap names that moment; when sick it is "Resting up". |
| Select **Next day**, then feed twice, play, **Rest**, and select **Next day** again; repeat twice more | The rhythm reads 1, 2, then **3 days**: "3 cared-for days in a row! Mia's journal has a new sticker, and the Daisy clip is earned for good." **Stickers** shows the 3-day sticker and the Daisy clip as Earned. In **Style → Hat** the Daisy clip's badge turns from "3 days" to **Earned**. A **Next day** with less care leaves the rhythm as it was. |
| In **Style**, tap **Outfit → Midnight** before a 7-day rhythm (developer mode off) | "Earned with a 7-day care rhythm. Keep caring each day!" and the outfit stays as it was. With developer mode on, every earned look is open. |
| Keep the rhythm going to day 7 (or keep tapping **Next day** with care) | **Weeks** gets "Week 1 · Days 1 to 7" with its care days, milestones and photos, and "This week" counts the care days so far. A 7-day rhythm earns the Midnight outfit and pays engagement x1.10 (x1.05 from 3 days). |
| Soothe five times on one day | Bond rises by at most 12 that care day (+3 each, the fifth adds nothing). |
| Real time: let the baby get hungry until it fusses, then feed within 30 minutes | Bond +5 for answering (plus +1 for an on-time meal). After 30 minutes the meal still calms the baby but adds no extra Bond. Two hours of unanswered fussing cost -2. |
| Real time: leave a baby for a day without care | Missed meals cost Bond -2 each (at most -8 a care day), and the rhythm uses its weekly grace day; a second missed day halves it. Away mode costs no Bond and freezes the rhythm. After 36 hours a helper covers: -2 a day, never below 60% of the high-water mark. |
| Make the baby sick, visit Dr. Lee and give the two doses (see Health) | **Hard days** lists "Days 5 to 6 · A poorly spell: Rest, medicine and cuddles helped. All better now!" A shy skill that comes back after a good day is there too. |
| Several days with meals missed (real time) | A clingy phase: "Mia is going through a clingy phase. Extra cuddles help." Comfort falls a quarter faster until answered fussing and on-time meals bring Security back. Days with answered fussing and no misses settle the baby ("happily playing on their own", with a small look at the toy each minute). Varied play makes an explorer, routine-only days a cautious little one; the report's Bond card says which, warmly. |
| On Home, rest a baby (remove the test rooms) and tap its card | The same journal opens read-only, with "Ivy is resting." under the heading. |
| Reopen a save from before this build | Bond is the old Connection, the rhythm starts at 0 and the journal already has the arrival and each milestone reached, on its nominal day. The save becomes v16 on the next write. |

The developer read-out (`BabySession.CareSummary`) ends with the bond line: Bond and its high-water mark, today's gains and losses, Security, Curiosity, the rhythm, the longest rhythm and the care days.

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
| Reopen a save from before this build | The baby opens as it was: content, no misses, tidy room, Standard style. The save becomes v16 on the next write. |

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
| Reopen a save from before this build | The baby opens well, with nothing counted. The save becomes v16 (care v3) on the next write. |

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
| Pass the gate from the **Grown-ups** gear in a Web, Editor or Development build and select **Developer mode: off · unlock everything** | Developer mode turns on: every product this build knows is granted as a playtest grant (the two extra rooms, the helpers, every premium breed and breed pack) and earned breeds count as earned, so the breed picker offers every breed whose art ships. Home shows a "Developer mode" chip. It survives a reload, and opening the preview with `?dev=1` (for example `/nursery/?dev=1`) turns it on too. Saved progress toward earned breeds is not changed. Selecting it again turns it off: the grants are removed (companions beyond the free rooms rest, as after a refund) and locks return. Release device builds don't show the button. |
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

A household holds babies and pets together, two for free (babies and pets combined). A pet never takes slot 1, the original baby key, so an older build never reads a pet. A cat is saved as a `<pet version="2">` record (with its care records, see [Cats: care slice 7](#cats-care-slice-7)) in `{key}.slot2` to `{key}.slot8`. The manifest records each companion's kind and, per species, the care days and the best Bond and growth any pet has reached, which unlock earned breeds for good. A household with no pet data still writes the same v1 manifest as before; once a pet exists it is v2, which older household builds leave untouched. The cat art is placeholder art (`tools/pets`).

| Step | Expected result |
| --- | --- |
| Open a fresh install (or a new `?key`) | "Who's joining your home?" offers **A baby** and **A pet** cards. |
| Select **A pet** | The cat appears in the room. Cat is the only species that can be chosen; Hamster, Dog, Fish and Rabbit are dimmed "Arriving soon" tiles with no lock or price. **Back** returns to the choice. |
| Select **Cat** and try the breeds | Every breed tap updates the cat in the room. Domestic shorthair is free. Tabby, Tuxedo, Siamese and Calico say **Locked** and how to earn them; Bengal, British shorthair and Russian blue say **Premium** and that nothing is sold in this build. A locked breed can be previewed but **Choose a name** stays disabled. Coats and the collar color update the preview. |
| Name the cat and select **Welcome home** | The cat's room opens: the header card shows the name and "Day 1 · Kitten" with the **Home** button, the needs card shows the four cat rings (Fullness, Energy, Comfort, Clean), and the care sheet reads "Care for …" with the Bond tier under it and one tile per care verb. Slot 1 stays empty. |
| Try **Feed**, **Pet**, **Wand play**, **Brush** and **Nap** | Each changes the meters and shows a warm cue line; petting and wand play grow warmer as Bond grows (purring, slow blinks, kneading, bringing the toy back). **Nap** curls the cat into its rest pose for a moment; a sleepy cat (Energy below 30) rests until it has napped. |
| Give the cat 2 meals, petting or play, and a nap in one day | The cue line says it was a full day of care and announces **Tabby** as a new breed to adopt. Reaching Bond 40 (a few days of care: Bond gains are capped at 12 a day) unlocks the **Tuxedo**, raising a cat to Grown unlocks the **Siamese**, and a 7-day care rhythm unlocks the **Calico**. Unlocks survive a goodbye and a reload. |
| Select **Next day** seven times | On the way the kitten pounces, climbs and makes its high jump (each announced once and kept in the journal), then grows up: the day line says "Grown cat", the Grown poses replace the kitten's, and the room says the cat is all grown up, once. |
| Open **Home** | The cat's card shows its portrait on a peach disc, a paw badge for the species, its stage and day and a mood pill. **Adopt** still offers a baby or a pet while a room is free; a baby and a cat fill the two free rooms. |
| Adopt a baby beside the cat, then switch | **Switch to …** moves between the baby's nursery and the cat's room in one tap. Both advance on launch and resume, whichever is on screen. |
| Remove the playtest rooms with a cat third in Home order | The cat rests like a baby: a quiet card, no decay, no writes, and a read-only pet journal. |
| Say goodbye to the cat | The Keepsakes box names the species, for example "Pip (cat, Nov 24 – Nov 24)". |
| Grant a premium breed with `?ent=breed.cat.bengal` | The Bengal tile says **Owned** and can be adopted. Ragdoll, Maine Coon and Sphynx are not listed until their art ships. |

## Cats: care slice 7

Care slice 7 of [care and growth](care-and-growth.md): a cat keeps the baby's care systems through its care profile. Its routine has two meals (Morning 07:00 to 10:00, late until 12:00; Evening 18:00 to 20:30, late until 21:30), a **Play** window (15:00 to 18:00, late until 19:00; the Home preset 12:00 to 15:00) answered by **Wand play**, the evening **Nap** (19:30 to 21:30) and a daily **Brush** (the wash window). The first day is excused. The quickest way to see each state is a Web, Editor or Development build with **Developer mode** on (Grown-ups gear on Home, or `?dev=1`) and the cat's **Next day**. A preview day passes no real time: it ages the cat a day and drains its needs for up to 8 awake hours, and no routine window passes, so the states that need a window to close (a missed meal, the scratched rug, the setback) need the real clock (or the computer's clock moved forward, which counts as an absence).

| Step | Expected result |
| --- | --- |
| Adopt a cat | Under the header the routine strip shows **Breakfast, Play, Dinner, Nap, Brush** (on a short screen it folds into a **Routine** button beside **Next day** and **Growth**). The care sheet has the five verbs, **Photo** and, at the bottom, **Next day** and **Growth**. |
| On a later day, feed between 07:00 and 10:00 (or 18:00 and 20:30) | "crunches happily, right on time" and the strip's dot turns sage. After 10:00 it is "a little late"; a meal missed by 12:00 makes the cat **Restless** ("meowing for a missed meal. A meal will help.") and costs a little Bond. |
| Feed twice in a row | The second is "too full and leaves most of it": Comfort and Clean drop a little and it is not a meal. |
| Wand play between 15:00 and 18:00 | "Playtime!" and the Play dot is done. Petting is together time but not play; a play window missed by 19:00 lowers the CareScore (it is never owed). |
| Select **Next day** twice without feeding, then tap **Pet** (or wait for the minute refresh) | Fullness falls below 35: "Bond: … · Restless" in the sheet and a restless line in the bubble. Left restless for 2 hours, or with a need below 15, the cat turns **Grumpy**: it swats the wand away. Two meals, then **Pet**, calm it. Answering within 30 minutes of it starting gives Bond +5. |
| Miss the evening brush (real time past 21:30) | "scratched the rug. A good brush settles them and tidies up." Each scratched rug cuts comfort gains by 5%, at most three; **Brush** tidies it. |
| Select **Next day** four times with no care | Once Health is below 50 the next tap makes the cat **Under the weather** (butter chip) with a sniffle bubble above its head, and a **Vet** (Free visit) tile joins the tiles. |
| Stay 15 minutes with Health below 30 (keep tapping or keep the tab in front) | **Sick — needs the vet**: the cat rests in its rest pose with a thermometer bubble. **Wand play** and **Brush** are gently refused ("feels too poorly for that and stays curled up"); Feed, Pet and Nap still work. |
| Tap **Vet**, then **Visit Dr. Rosa** | "Dr. Rosa, the vet" sheet: "Dr. Rosa listens, checks a temperature and says Pip needs rest and medicine." Health +15, **Recovering · medicine 0 of 2** and a capsule bubble. The **Medicine** tile works in a meal window (breakfast or dinner), once per meal; two doses in two meals say "Well again!". |
| Select **Next day** with care before each tap (two feeds that were not too full, wand play, petting, a brush and a nap) | The kitten pounces around day 2 or 3, climbs around day 4 and makes its high jump around day 5 or 6 (always by day 7, when it grows up); each is announced once. With no care it is later, never past 1.4x. **Growth** shows "Likely to pounce in about 2 days" with **Ahead**, **On track** or **Taking their time**, the care rhythm, Bond and **Trust**, and the **Journal** tile. |
| Keep the care going for three **Next day** taps | The rhythm reads 3 days: "3 cared-for days in a row! Pip's journal has a new sticker." **Stickers** in the journal shows it (pets earn stickers, not outfits). |
| Tap **Photo** after a care action | "Click! A photo of Pip playing is in the journal." Three a day; the journal draws each photo from the cat's pose (the rest pose for a nap or a poorly day, the Grown pose from day 8). |
| Miss three meals in a row while in the room (real time) | The newest skill goes shy: "feels a little shy about pouncing and naps instead of playing", and **Wand play** shows a quiet rest instead of the pounce. Two on-time meals and two pets in one day bring it back: "pouncing again!" and a hard day in the journal. |
| Open **Routine** (the strip or the button) and choose **3 days** away | The cat is fine while you're away (a neighbour pops in); windows are covered, needs stay steady and nothing costs Bond. Any care, or **We're back**, ends it. |
| Leave the cat alone for more than 36 hours | On return: "Welcome back! A neighbour popped in to check on Pip while you were away." |
| Open **Home** | The card reads "A bit restless", "A little grumpy", "A bit under the weather", "Feeling poorly · the vet can help", "Getting better" or "A bit shy today"; a missed meal still unanswered, a grumpy or a sick cat lights the Home dot. |
| Reopen a cat saved before this build | The cat opens as it was (stage, Bond, look, meters), with the milestones its age passed; the first refresh excuses the whole gap and the record becomes `<pet version="2">`. |

## Helpers (monetization M4)

Helpers are a safety net, not a carer ([monetization](monetization.md#helpers-built-in-m4)). For a baby they are the **Sitter**, who pops in; for a cat they are objects: an **auto-feeder**, a **ball launcher** and a **self-cleaning litter box**. They act only when a need falls to its floor (Fullness 45, Comfort 40, Clean 40), only within 24 hours of the last hands-on care, and never build Bond, the care rhythm, development, on-time credit or Health. Dogs, fish, rabbits and hamsters keep their simple rules and have no helpers yet.

This build has no shop. To own them in a Web, Editor or Development build, use any of: **Developer mode** (Grown-ups gear, or `?dev=1`), the URL flag `?ent=helper.set` (or one helper: `?ent=helper.feeder`, `helper.play`, `helper.tidy`), or **Grown-ups → Helpers → Add test helpers**. Owned helpers are on by default.

| Step | Expected result |
| --- | --- |
| Before owning helpers, open the Routine sheet and look at the rings | Nothing about helpers anywhere: no marker, no row, no link. |
| Own the helpers, then open **Grown-ups → Helpers** (behind the parental gate) | "Helpers keep things steady while you're busy. Only you can help Mia grow." and three switches, **Feeding · On**, **Play · On**, **Tidy · On**. Tapping one turns it off for the whole household (saved in the manifest); a helper you don't own reads **Not owned** and can't be tapped. In a build without the playtest toggle the panel says helpers are coming to the app stores later; it never shows a price or a buy button. |
| Back in the nursery, look at the needs card | Comfort and Clean show a small dark dot on the ring at the helper's floor. Fullness gets one once the feeder is stocked. |
| Open the Routine sheet | Under Away mode: the helpers line and "The Sitter's bottles: 0 of 6." with **Leave bottles**. Tapping it fills the six bottles (free, hands-on, not care: no Bond, no credit) and the button reads **Bottles ready**. A cat's sheet says "Auto-feeder: 0 of 6 portions." with **Fill the feeder**, and the cat's room shows a small bowl chip with the stock (**6/6**) at the top-left of the care sheet. |
| Select **Next day** (within a day of your last care) | The bubble reads, for example, "While you were busy, the Sitter gave Mia 2 bottles and freshened them up. Mia missed you; a cuddle or story will help." What the helpers did always comes first and the attention line last; it stays until you care. Fullness ends held near the floor instead of falling, the stock drops by the bottles given, and **Growth → Journal** shows a muted "Day N · The Sitter popped in · 2 bottles · freshened up once" (a gear and "The helpers kept things steady" for a cat). The rhythm, Bond and the milestones move exactly as they would with no helpers. Each preview day starts the day's play and tidy count again. |
| Real time: feed breakfast, then leave the tab open (or come back) after lunch without feeding | When Fullness reaches 45 the Sitter gives a bottle (Fullness 70). The Snack chip reads **Helper** instead of **Late**, the baby does not fuss for the meal, and when the window closes it is **Helped**: it stays out of the meal ring and isn't owed, but Bond takes the missed meal's -2 and the rhythm counts the day as one with a missed meal. A hands-on meal in the same window still counts on time. |
| Real time: let Comfort or Clean run down for a whole day | Play tops Comfort up to 58 and tidy restores Clean to 70, each at most three times a care day (counted from 04:00 local); after the third a need falls again until the next care day. Tidy never reaches 100 and never clears the scratched rug or the floor spots a missed wash leaves. |
| Real time: no hands-on care for more than 24 hours | The helpers stop at 24 hours after the last care, stock or not, and the needs fall as without them (the usual 8-hour cap still applies to an absence). The next care starts the window again. |
| Switch a helper off, or remove the test helpers | It stops at once; what it already did (needs, stock, journal days) stays. Removing them is the refund path. |
| Reopen a save from before this build | It opens with helpers off: no stock, nothing counted. Its records stay byte for byte (care v3, journal v1) until a helper is stocked or acts; then the care record becomes v4 and the journal v2, which an older build leaves alone. |

The developer read-out (`BabySession.CareSummary`) adds "helpers stock N today feeds/plays/tidies" once a helper has been used.

## Looks and rooms (monetization M2b)

The first premium cosmetics ([monetization](monetization.md#looks-and-rooms-built-in-m2b)): two baby look packs, two room themes, a cat accessory pack and the Founder pack. They change only how things look; care, growth and Bond are untouched, and the looks earned with a care rhythm (Daisy clip, Midnight, Bucket hat) stay earned-only.

This build has no shop. To own them in a Web, Editor or Development build, use any of: **Developer mode** (Grown-ups gear, or `?dev=1`), **Grown-ups → Looks & rooms → Add test pack**, or URL flags, one product or several separated by commas:

| Pack | URL flag | Where to see it |
| --- | --- | --- |
| Cozy knits | `?ent=look.cozy-knits` | **Style → More**: Oatmeal, Berry, Moss, Fog; **Style → Hat**: Pom-pom beanie |
| Garden party | `?ent=look.garden-party` | **Style → More**: Rosebud, Marigold, Garden mint, Bluebell; **Style → Hat**: Sun hat |
| Cloud nursery | `?ent=room.cloud-nursery` | **Decor** in the nursery or in a pet's room |
| Starlight | `?ent=room.starlight` | **Decor** in the nursery or in a pet's room |
| Cat bandanas & bows | `?ent=petwear.cat.bandanas-bows` | A cat's room → **Decor**: **Wears** (Bandana, Bow) and **Colour** (Butter, Lilac, Terracotta); the same two buttons on the adoption name page |
| Founder pack | `?ent=supporter.founder` | **Style → More**: Fern & gold; **Decor**: Golden hour; Home: a **Founder** chip on the household card |

For example `/nursery/?ent=look.cozy-knits,room.starlight`.

| Step | Expected result |
| --- | --- |
| Without any pack, open **Style → More** | Nine outfits, each with a small butter **Special** chip; **Style → Hat** shows the Pom-pom beanie and Sun hat with the chip too. Free and earned looks are unchanged (earned looks still show their days or **Earned**). |
| Tap a Special item | The baby shows it as a preview and the sheet says "Part of the Cozy knits pack — a grown-up can get it in Grown-ups." Nothing is saved: the selection ring stays on the saved look, and closing Style puts the saved look back. There is no price, buy button or shop link anywhere outside Grown-ups. |
| Open **Decor** | Five room cards: Sage and Peach (free), Cloud nursery and Starlight, and Golden hour (Founder). Locked cards carry **Special**; tapping one shows it in the room with the calm line, and the saved card keeps **Selected**. |
| Open **Grown-ups → Looks & rooms** (behind the parental gate) | Two tabs (**Baby looks**, **Rooms & pet wear**), three packs each with what is inside. A test build shows **Add test pack** / **Remove**; a release build shows **Owned** for owned packs and says they are coming to the app stores. |
| Add a pack, then choose its items | The chips disappear and the items save like free ones. In a pet's room **Decor** sets one theme for every pet room; a cat's **Wears** and **Colour** step through what it can wear and save at once. |
| Remove the pack (the refund path), or reload with the flag gone | The saved choice is kept (the Style ring stays on it) but the baby shows Sage and no hat, the room shows Sage, and a cat shows a Sage collar. Changing another part (say the hair) still works and keeps the saved premium ID. Adding the pack again shows the look again. |
| Reopen a save from before this build | Byte for byte the same: looks and rooms are content IDs in the existing appearance and decor fields; a pet room theme is a `<rooms pet-theme="N"/>` manifest element written only when it is not Sage. |

The included character is a prototype asset for checking animation and game feedback. Record short clips from the same camera for art reviews. Confirm mobile frame pacing and lifecycle separately on Android and iOS hardware.
