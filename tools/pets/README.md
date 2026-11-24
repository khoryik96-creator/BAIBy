# Pet art pipeline (PLACEHOLDER cat and hamster)

> **Everything this tool produces is PLACEHOLDER art.** It exists so pet mode
> (P1 Cat, `docs/pet-mode.md`) is playable before the commissioned cat
> illustrations land. Replace the outputs with the commissioned poses and marking
> layers when they arrive; keep the file layout and the region-map contract so
> the game code does not change. `layout.json` carries `"placeholder": true`.

Offline Python tool that draws a chibi cat as anti-aliased vector shapes
(pycairo, drawn at 2x and box-downsampled) in the style of the baby poses: big
head, big glossy eyes with highlights, the baby's large soft peach cheek blush,
flat two-tone cel shading (one hard-edged shadow shape per part on the side away
from the top-left light, no airbrushed rims or specular blobs) with a faint
painted grain, and the baby's `#4D2917` dark-brown outline.

Line weight is sized for the scale the cat is drawn at in game: the canvas outline
is the baby's ~8 px outline divided by `runtimeScale` (Young ~12.8 px, Grown
~10.4 px at 1:1, so both are ~8 px on screen beside the baby); interior lines
scale by the same factor. Where one coat part overlaps another (the jaw over the
chest, forelegs over the chest, the chest over the haunches) the line drops to
interior weight, so the body reads as one animal rather than stacked stickers.

## Hamster (P2)

`hamster.py` draws the hamster with the cat's helpers (`cat.py`: parts, cel shading, outlines, eyes, blush), and
`habitat.py` its cage. Outputs go to `unity/Assets/Resources/BAIby/Pets/Hamster/` with the same contract and a
`layout.json` saying `"placeholder": true`.

| Path | What |
| --- | --- |
| `Base/{Pose}` (+`Regions`) | the Syrian golden: a round bun with no neck, puffy cheek pouches, small round ears, dark glossy eyes, tiny pink paws under the chin. Young (a pup with a bigger head) and Grown (chubbier), awake (sitting up) and rest (curled asleep, closed eyes, paws tucked under the chin). **The base carries G weight**: the light belly and muzzle are region G (`reference.markings` is cream), so the palette's `secondary` is the belly colour |
| `Markings/{WinterWhite,Sapphire,Campbells}/{Pose}Dark` | the dorsal stripe (gTarget `dark`, recoloured on its own with the coat's `dark`) |
| `Markings/Panda/{Pose}Dark` | the Panda's patches: ears, eye patches and a saddle band (gTarget `dark`) |
| `Markings/Roborovski/{Pose}` | white brow spots (G) |
| `Markings/TeddyBear/{Pose}` | the Teddy bear's long coat: pointed fur tufts in the **main coat (R weight)** on the cheeks, sides, crown and back. A feature overlay: it reaches past the base silhouette, so the pose crop is the union of the base and the coat |
| `Accessories/Bow/{Pose}` | a tiny bow between the ears (A) |
| `Habitat/Back`, `Habitat/Front` | the cage at the baby's pose-pixel scale (760 x 480, fixed colours, no region map): behind the hamster a wire dome on a pastel tray, a bedding mound, the exercise wheel and a play tunnel; in front the tray's lip with a spill of bedding and the seed bowl. `layout.json` `habitat` gives the paths, the size and `floor` (the floor line, 64 px above the bottom); `PetVisuals` draws them on the room's floor line |

`runtimeScale` is `young` 0.48 and `grown` 0.56 (a young hamster is smaller than a kitten; `preview.py`'s
`hamster-nursery.png` shows both beside the Sitter baby). Line weights follow that scale like the cat's. The palette
(`palette.py` `HAMSTER_COATS`) has every catalog coat of the seven breeds, with `Dark` eyes by default and `Ruby`
allowed for the Syrian golden, Winter white and Campbell's. `check.py` checks both species (the hamster's poses may be
smaller than the baby's, its base may carry G, only the Teddy bear's coat may leave the silhouette, the habitat layers
match the layout) and that each species' art stays under 2 MB (cat about 1.1 MB, hamster about 0.85 MB).
`preview.py` writes `hamster-breeds.png`, `hamster-coats.png`, `hamster-rest.png`, `hamster-bow.png`,
`hamster-nursery.png` and `hamster-detail.png` beside the cat's sheets.

Known limitations: the Teddy bear's tufts are procedural and read a little spiky at small sizes; the Panda's saddle is
a plain band; there is no head-anchor data; the habitat is a flat placeholder that does not follow the room theme.

## Requirements

* Python 3.10+
* `numpy`, `Pillow`, `scipy`, `pycairo` (see `requirements.txt`)
* `tools/appearance/recolor.py` (the baby recolour/composite math) is imported
  by `build.py` (overlay fidelity check) and `preview.py`.

```
pip install -r tools/pets/requirements.txt
```

## Usage

```
python3 tools/pets/build.py     # regenerate every cat and hamster asset + layout.json + .meta files (~4 min, 4 processes)
python3 tools/pets/build.py --species Hamster   # one species
python3 tools/pets/check.py     # self-check of the shipped files (exit code 1 on failure)
python3 tools/pets/preview.py   # contact sheets (default: the session scratchpad; --out DIR)
```

The build is deterministic (fixed geometry and seeds, fixed PNG encoder
settings) and keeps existing `.meta` GUIDs, so rerunning it without changes
produces no diff.

## Source files

| File | What |
| --- | --- |
| `raster.py` | layered rasteriser: cairo coverage masks composited in numpy, with per-paint region weights and tagged overlay capture |
| `cat.py` | style constants, reference colours, the head (ears, eyes, nose, mouth, whiskers, breed head marks), shared part/tail/collar helpers |
| `poses.py` | the four poses: sitting (awake) and curled loaf (rest), Young and Grown |
| `palette.py` | suggested coat/eye colours per breed and coat (written into `layout.json`) |
| `build.py` | renders base, marking and collar layers, crops, writes assets, `layout.json` and Unity metas |
| `check.py` | verifies the shipped files |
| `preview.py` | contact sheets built only from the shipped files (`wear.png`: collar, bandana and bow on every pose) |

## Outputs

Under `unity/Assets/Resources/BAIby/Pets/Cat/`. All images are PNGs stored with
a `.bytes` extension (Unity imports them as `TextAsset`s with exact pixels),
exactly like the baby appearance assets.

| Path | What |
| --- | --- |
| `Base/{Pose}` (+`Regions`) | plain cat, cropped to its silhouette + 4 px |
| `Markings/{Tabby,Tuxedo,Siamese,Calico}/{Pose}` (+`Regions`) | cropped breed marking overlay |
| `Accessories/{Collar,Bandana,Bow}/{Pose}` (+`Regions`) | cropped neck wear overlay (region A, recolourable with the outfit accent palette); the bandana and bow are the "Cat bandanas & bows" pack (`petwear.cat.bandanas-bows`) |
| `layout.json` | reference colours, pose sizes, overlay offsets, suggested palette |

Poses: `YoungAwake` and `GrownAwake` sit with small teardrop haunches tucked
behind a pear-shaped chest, hind feet pointing forward beside the front paws and
two forelegs whose contours start partway down the chest fur. `YoungRest` and
`GrownRest` are loaves with a low cranium (closed eyes mid-head), a closed thigh
with the hind paw at its front, forearms tucked under the chest with the paws
peeking out under the chin, and the tail along the front (Grown: it wraps the
front half and ends over the paws, below the muzzle). Young is a kitten: a big
head, round eyes, short body. Grown has a smaller head on a bigger body, longer
legs and neck, narrower almond eyes, a longer muzzle (nose lower), taller ears and
the tail held up with a hooked tip. The Domestic shorthair (free starter breed)
is the plain base. Breed marks: Tabby (forehead M as four separate tapered
strokes running into the crown, cheek swooshes, flank stripes, concentric haunch
arcs, foreleg bars, a broken necklace, tail rings), Tuxedo (white muzzle with a
rounded inverted-V blaze, bib, socks), Siamese (a crisp muzzle mask that tapers
to a point between the eyes, two-step cel socks per leg, fully pointed ears and
tail), Calico (organic patches, including on the tail).

`layout.json` `runtimeScale` (`young` 0.65, `grown` 0.8) is the suggested draw
scale of the cat relative to the baby pose pixels: the cat canvases are about
baby-sized, so drawn 1:1 the kitten would be as big as the seated baby.
`preview.py`'s `nursery.png` places every pose at that scale beside the baby.

## Contract

Region maps use the baby encoding (RGBA8, weights 0..255, sum <= 255):

| Channel | Meaning | Reference colour |
| --- | --- | --- |
| R | main coat | `reference.coat` |
| G | secondary coat / markings | `reference.markings` |
| B | iris | `reference.eyes` |
| A | accessory accent (collar) | `reference.accent` (== baby `outfitAccent` reference) |

Outlines, eye whites, pupils, highlights, nose, mouth, inner ears and the bell
are 0 in every channel, so they keep their drawn colour. Every coat pixel is
drawn as `reference * shade`, so the baby formula

```
out = s * (1 - Σw) + Σ w_r * clamp(s * target_r / reference_r)
```

keeps the cel shading for any coat colour.

Overlays: `layout.json` `overlays[]` gives `path`, `pose`, `x`, `y` (offset of
the overlay's top-left corner in the cropped base, y measured from the top),
`width`, `height` and `gTarget`. Composite order (as for the baby and
`docs/pet-mode.md`): base, markings, Calico dark patches, accessory, then one
recolour pass. Marking pixels carry only G weight, so the marking colour follows
the breed's secondary colour (`gTarget: "secondary"`).

Calico's dark patches are a separate overlay, `Markings/Calico/{Pose}Dark`
(`gTarget: "dark"`), drawn like the other markings (G weight, markings reference).
Recolour that overlay on its own first with targets `[main, dark, eyes, accent]`
(`dark` from the coat's palette entry), then composite it with its region weights
set to 0 so the final pass leaves it alone. This lets the tricolour coat get
charcoal patches and the dilute Calico Cream a light warm blue-grey.
`preview.py` (`Cat.layers`) shows the exact sequence.

`palette.coats` lists a suggested `main`/`secondary`/`dark` set and default eye
colour for every breed coat (`dark` equals `secondary` for coats without dark
patches) (`PetBreed`/`PetCoat` names); `palette.eyes` lists eye
colours. These are tuning suggestions, not part of the contract.

## Known placeholder limitations

* Markings are procedural shapes; `docs/pet-mode.md` notes procedural stripes on
  chibi art look flat and should not ship. Commission the marking layers.
* Overlays reproduce the tagged render exactly except on the anti-aliased rims
  of outlines drawn over a marking (a few px, `build.py` prints the worst case).
* The outline is one fixed colour (region 0), so on the charcoal coats the brown
  lines separate by value only; the coat is kept at charcoal rather than black so
  the cel step and the lines stay visible.
* There is no head-anchor data yet; the commissioned art should add it.
