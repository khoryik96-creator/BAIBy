"""Suggested PLACEHOLDER cat colours (sRGB) for the recolour pass.

Each breed coat maps to two targets: ``main`` for region R (coat base) and
``secondary`` for region G (markings), plus ``dark`` for the Calico dark-patch
overlay (recoloured on its own, see README "Contract"; equal to ``secondary`` for
coats without dark patches).  Breed names and coats follow
PetBreed / PetCoat and the cat entries in PetCatalog.CreateDefault.  The first
coat per breed is the breed default.  These are tuning suggestions written into
layout.json for whoever wires up PetPalette; they are not a contract.
"""

from __future__ import annotations

WHITE = (250, 247, 242)
BLACK = (84, 82, 98)        # a cool charcoal, not crushed black: the cel shadow step and the
                            # dark-brown outline both stay visible against it

COATS = {
    "DomesticShorthair": {
        "Ginger": ((240, 172, 110), (204, 120, 66)),
        "Gray": ((178, 180, 190), (120, 122, 134)),
        "Black": (BLACK, (40, 37, 46)),
        "Cream": ((246, 226, 196), (222, 186, 146)),
        "White": (WHITE, (226, 220, 212)),
    },
    "Tabby": {
        "Brown": ((206, 162, 118), (112, 78, 54)),
        "Ginger": ((240, 182, 126), (204, 124, 74)),
        "Silver": ((214, 216, 222), (102, 104, 116)),
    },
    "Tuxedo": {
        "Black": (BLACK, WHITE),
        "Gray": ((146, 148, 160), WHITE),
    },
    "Siamese": {
        "Seal": ((246, 232, 210), (112, 80, 62)),
        "Blue": ((240, 238, 236), (134, 142, 160)),
        "Lilac": ((246, 238, 236), (150, 128, 142)),
    },
    "Calico": {
        "Tricolor": (WHITE, (232, 146, 78), (72, 68, 80)),      # ginger + charcoal patches
        "Cream": (WHITE, (240, 198, 146), (168, 166, 176)),    # dilute: cream + light warm blue-grey
    },
}

EYES = {
    "Amber": (228, 170, 66),
    "Green": (128, 176, 100),
    "Blue": (112, 170, 230),
    "Copper": (218, 130, 66),
    "Hazel": (170, 172, 84),
}

DEFAULT_EYES = {
    "DomesticShorthair": "Amber",
    "Tabby": "Green",
    "Tuxedo": "Amber",
    "Siamese": "Blue",
    "Calico": "Copper",
}


# Eye colours a breed may use (default: all of EYES).  Siamese are always blue-eyed.
BREED_EYES = {
    "Siamese": ["Blue"],
}

# Pet collar accents per outfit-accent name, where the baby outfit's accent would
# surprise a player: the baby "Midnight" outfit has a gold accent, but a "Midnight"
# collar should be a deep night blue.  Other names use the baby outfit accent.
COLLAR_ACCENTS = {
    "Midnight": (70, 86, 140),
}


def allowed_eyes(breed):
    return BREED_EYES.get(breed, list(EYES))


def coat_colors(breed, coat):
    """(main, secondary, dark) targets of one breed coat."""
    cols = COATS[breed][coat]
    return cols[0], cols[1], (cols[2] if len(cols) > 2 else cols[1])


def breed_entries():
    out = []
    for breed, coats in COATS.items():
        for coat, cols in coats.items():
            main, sec = cols[0], cols[1]
            dark = cols[2] if len(cols) > 2 else sec
            out.append({"breed": breed, "coat": coat, "main": list(main), "secondary": list(sec),
                        "dark": list(dark), "eyes": DEFAULT_EYES[breed], "allowedEyes": allowed_eyes(breed)})
    return out


def eye_entries():
    return [{"name": k, "color": list(v)} for k, v in EYES.items()]


def collar_entries():
    return [{"name": k, "color": list(v)} for k, v in COLLAR_ACCENTS.items()]
