# Interface style

The in-game interface follows one calm, rounded style. The tokens and building blocks live in
[`NurseryPreview.Theme.cs`](../unity/Assets/BAIby/Presentation/NurseryPreview.Theme.cs); new screens use them rather
than new colours or shapes.

## Units

The canvas is 720 units wide on a phone (CanvasScaler reference 720 x 1120, Expand). At the 390 px target one mockup
pixel is `Px(1)` = 1.85 units, so a 16 px side margin is 30 units and a 44 px touch target is 82 units. Keep touch
targets at 82 units or more.

## Type

| Role | Font | Use |
| --- | --- | --- |
| Heading | Fredoka SemiBold | Names, sheet titles, numbers in rings and cards |
| Strong | Nunito ExtraBold | Buttons, tile labels, chips, need labels |
| Body | Nunito Bold | Body copy, secondary lines, messages |

`Label(...)` picks the role: bold at 30 units and up is a heading, other bold text is strong, the rest is body.
`SetRole` changes it later. The fonts are in `Resources/BAIby/Fonts` (SIL OFL, licence beside them); when they cannot
load, the built-in font is used. Best-fit text keeps truncating so it shrinks; other text overflows rather than
vanishing (`SettleText`).

## Colour

| Token | Hex | Use |
| --- | --- | --- |
| Cream | `#FBF6EE` | Page ground (Home) |
| Paper | `#FFFFFF` | Sheets, cards; glass cards are white at 90% over the room |
| Ink | `#2E2A33` | Text and icons |
| Secondary | `#6E6873` | Quiet lines on white |
| Muted | `#5E5863` | Quiet lines on tinted grounds |
| Mist | `#F3EEE6` | Soft neutral buttons, tracks, fields |
| Divider | `#E4DED6` | Grabber, dashed edges, later dots |
| Outline | `#E9E3DB` | Border of outlined buttons |
| Fern | `#4E7A63` | Primary accent, white text on it |
| FernSoft / FernDeep | `#E2EEE6` / `#3B5F4C` | Selected and content states |
| Peach, Lilac, Sky, Mint, Butter, Blush | `#FBE3D6`, `#ECE5F7`, `#E1ECF6`, `#DDF0EE`, `#FBF0CF`, `#F9E1E4` | Tile tints |
| ButterInk, BlushInk | `#7A5A14`, `#8E2F3E` | Text on butter and blush chips |

Need rings: Fullness `#E7966F`, Comfort `#9C84CF`, Energy `#E5B94A`, Clean `#6FA0C8`, Health `#5E9C7A`. Text keeps a
contrast of at least 4.5:1 against its ground.

## Shape

Radii in mockup pixels: header card 24, cards 26, sheet top corners 28, tiles 20, buttons 14 to 18, chips fully
round. Shadows are soft and blurred (`Card`, `Sheet`), or a gentle offset for small controls (`SoftShapes.Lift`);
no hard outlines on interface surfaces. The only dark outline is the quilt's, which matches the baby art.

## Building blocks

- `Tray(name, false, height)`: a bottom sheet with a grabber; `SheetHeading` and `SheetClose` give its title and Done.
- `Card(root, color, radius)`: a floating card with a soft shadow.
- `ActionButton`: primary (Fern), soft (Mist) or outlined (Paper) buttons; `IconButton` for 44 px icon buttons.
- `CareTile`: a pastel tile with an icon badge and a label; `ChoiceRow` for a full-width choice with effects.
- `Pill`, `PlacePill`: chips that hug their words. `Meter` and `PlaceMeters`: ring meters; `NeedMeter.SetFloor` puts a
  10-unit Ink dot (55%) on the ring's stroke at an active helper's floor, and nothing when no helper holds that need.
- A premium cosmetic that is not owned carries a small butter **Special** chip (Butter ground, ButterInk text) where an
  earned look shows its days; tapping it previews the item and the sheet's feedback line names the pack and Grown-ups.
  Never a price, a lock icon or a shop link in a picker.
- `IconAt`: a tinted line icon from `Resources/BAIby/UI/Icons`, rendered by
  [`tools/ui/render_icons.py`](../tools/ui/render_icons.py) (white, 96 px, tinted in game).

## Layout rules

- The header card, needs card and routine strip stack from the top; the strip folds into a Routine button when it
  would reach the tallest pose (`FitRoutineStrip`).
- The care sheet sizes its tiles to the screen height (`Metrics`) so the baby's face stays clear in short desktop
  windows; on wide screens the speech bubble moves beside the baby.
- Care tiles fill two rows, at most four on top (seven with Photo, eight with the Doctor tile, and one more, Talk, once
  First words are reached). The Talk sheet keeps its optional text field at the top, above the feeling chips, and rises
  over the on-screen keyboard while the field is focused on a phone. A cat's sheet has its five
  verbs, Photo and, when needed, Vet or Medicine; its bottom row is Next day and Growth (and Routine when the strip folds).
  A fish's Wipe glass opens a small sheet like the diaper cleanup: three smudges on a Sky pane with two soft highlights,
  wiped by tap or swipe (`DiaperWipeInput`), with Cancel, a one-tap Quick wipe and Done. A rabbit's sheet has its five
  verbs (Hay, Greens, Groom, Floor time, Litter) and Photo in the 3x2 grid; its rings are Hay (the Energy ring colour),
  Fullness, Litter and Comfort, and its six-slot strip reads Dawn hop, Breakfast, Doze, Floor time, Dinner, Litter.
  Binkies are a happy lap of the pen and quick jumps with a twist; scattered hay fades in over the pen floor. Tall sheets stay at the
  Report's height (600 units): the journal shows four rows a page (a round picture or icon disc, a title and a quiet
  line) under segmented tabs, with 82-unit arrows to turn the pages.
- Messages appear in the speech bubble; panels keep their own titles inside their sheets.
