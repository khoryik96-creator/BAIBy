# Baby appearance pipeline

Offline Python tool that turns the eight pose illustrations in
`unity/Assets/Resources/BAIby/Baby/{Pose}.png` into the customization assets
described by the shared appearance contract (skin tone, hair colour, hair style,
outfit, headwear). Output lives in
`unity/Assets/Resources/BAIby/Baby/Appearance/` and is committed; the original
PNG sprites are never modified.

## Requirements

* Python 3.10+
* `numpy`, `Pillow`, `scipy` (see `requirements.txt`)

```
pip install -r tools/appearance/requirements.txt
```

## Usage

```
python3 tools/appearance/build.py      # regenerate every asset + layout.json + .meta files (~10 min)
python3 tools/appearance/build.py --no-hats [--poses Sitter,Walker]   # dev: faster partial rebuild
python3 tools/appearance/build.py --only-hats PomBeanie,SunHat [--poses Sitter]   # redraw only these hats
python3 tools/appearance/check.py      # contract self-check (exit code 1 on failure)
python3 tools/appearance/preview.py    # contact sheets (default: the session scratchpad; --out DIR)
python3 tools/appearance/fit_heads.py  # dev helper: suggests refined skull parameters for heads.py
```

The build is deterministic (fixed seeds, fixed PNG encoder settings) and keeps
existing `.meta` GUIDs, so rerunning it without changes produces no diff on the
machine that built it. Newer numpy/scipy/Pillow versions shift a few
anti-aliased pixels of the Base and hair, so to add or redraw a hat use
`--only-hats`: it draws the named hats against the *shipped* Base and hair
overlays (exactly what the game composites under them) and leaves every other
file untouched.

## Outputs

All images are PNGs stored with a `.bytes` extension (Unity imports them as
`TextAsset`s with exact pixels).

| Path | What |
| --- | --- |
| `Base/{Pose}` | pose with the hair removed and the skull/forehead rebuilt (skin fill + dark-brown head outline) |
| `Base/{Pose}Regions` | region map, same size as the pose |
| `Hair/{Curls,Coils,Smooth,Tuft}/{Pose}` (+`Regions`) | cropped hair overlay |
| `Headwear/{Bow,BearBeanie,BucketHat,DaisyClip,PomBeanie,SunHat}/{Pose}` (+`Regions`) | cropped headwear overlay (composited above hair); PomBeanie (Cozy knits) and SunHat (Garden party) are look-pack hats |
| `layout.json` | measured reference colours, pose sizes, overlay offsets (`y` from the top) |

Region maps are RGBA8: R = skin, G = hair, B = outfit main, A = outfit accent
weight; R+G+B+A <= 255; outlines, eyes, eye whites, mouth and fixed details are 0.

## How it works

* `skinnorm.py` – the rest-pose illustrations were painted with a lighter, pinker
  skin than the awake ones; their skin is scaled to the awake median before
  anything is built, so sleep/wake never changes the complexion. Base + Curls
  therefore reproduces the *normalised* illustration for the rest poses.
* `heads.py` – hand-tuned skull geometry per pose (two half-ellipses, superellipse
  crown, tilt), ear ellipses (matching the outer edge of each ear's outline), and
  small `erase` patches for curl tips tucked behind an ear. `fit_heads.py` refines
  the numbers against the visible face outline.
* `separate.py` – finds the hair layer (hair-coloured core + its own outline strokes,
  thin strands and the hair shadow on the forehead), then rebuilds the hair-free
  Base: smooth skin fill continued from the surrounding skin (push-pull fill), the
  art's own clean skin feathered in where it shows, and a new 8 px head outline.
  Its radius is measured per angle on the visible face outline (on an ear's inner
  line where an ear covers it), bridged across the hair-covered arc while ignoring
  the temple notch where the face line used to meet the hair, and the whole arc
  plus a margin is redrawn so the splice lands on the art's own line. Leftover curl
  tips outside the head and line stubs inside it are cleaned up.
  Curls = the original pixels wherever Base differs, so Base + Curls reproduces
  the original (`check.py` reports the error).
* `regions.py` – soft region weights. Each class is the segment between the outline
  colour and the class reference; the weight `t * L(C) / L(p)` makes anti-aliased line
  edges fade out while shaded fabric/skin recolours fully. On top of that:
  line art next to skin or outfit-main (outlines, irises, brows, closed-eye lines)
  gets weight 1 of that class, so it multiplies with the target (near-black lines
  on deep skin and on dark outfits, unchanged on the identity look); hair/skin mixes
  are split between both classes; cheek blush keeps a share of its own colour so it
  stays rosy on every tone; dark line pixels never carry accent weight (no rusty
  speckle). In the Curls overlay only the hair mass is hair: thin strands and the
  hair's shadow on the forehead are skin shading (no blown-out light strands), and
  overlay pixels outside the hair mass keep their outfit weights.
* `paint.py`, `hairstyles.py`, `headwear.py` – new art drawn in the head-local frame
  on a 4x supersampled canvas (exact anti-aliased region weights). Hair is painted in
  multiples of the measured hair reference (hair weight 1), outlines in the art's
  dark brown (weight 0). Hats are sized per pose to cover every hair style above the
  brim and to stay inside the pose image; hair styles stay inside a crown zone that
  hats can always cover.
* `recolor.py` – the contract recolour + composite math, mirroring
  `unity/Assets/BAIby/Character/Appearance/AppearanceRecolor.cs` operation for
  operation (round-half-to-even, region weights blended with the colour coefficients,
  rounding excess trimmed from the largest weight). `preview.py` and `check.py` only
  use the shipped files through this module.

## Palette

The palette table lives in `recolor.py` and must stay identical to
`AppearancePalette.cs` and the contract. `tests/Appearance` checks that every
outfit is named here and that every look-pack outfit (after Midnight: the Cozy
knits, Garden party and Founder colourways) has exactly the C# targets. Identity options (Warm, Chestnut, Sage)
use the measured references from `layout.json`.

Art-QA palette tweaks (relative to the first contract draft): Brown (166,104,68),
Deep (120,72,48) so faces stay readable; Platinum (224,206,168) so light hair is
not blown out; Oat accent (188,150,116) so it does not merge with the outline;
Sky main (222,234,242) and Lilac main (232,222,242) so the pastel mains do not
read as plain white. These tweaks (and Brown, Deep, Platinum above) were never
carried into `AppearancePalette.cs`, so the preview of those free options differs
slightly from the game; the look-pack outfits match exactly.

`preview.py` also writes `g_look_packs.png`: each pack outfit with its pack's hat
on the four stages.
