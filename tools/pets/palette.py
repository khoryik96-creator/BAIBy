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


# ---------------------------------------------------------------- hamster (P2)
# Placeholder hamster colours per breed coat: ``main`` (region R, the back), ``secondary`` (region G, the light
# belly and muzzle, and the Roborovski's brow spots) and ``dark`` (the dorsal stripe of the dwarf breeds and the
# Panda's patches, recoloured on their own: see hamster.py).  Coats follow the hamster entries in
# PetCatalog.CreateDefault; the first coat per breed is its default.
HAMSTER_COATS = {
    "SyrianGolden": {
        "Golden": ((232, 170, 98), (250, 240, 222)),
        "Cream": ((244, 220, 182), (252, 246, 234)),
        "Brown": ((178, 124, 86), (246, 230, 210)),
    },
    "WinterWhite": {
        "White": ((240, 238, 236), (255, 253, 250), (156, 152, 158)),
        "Gray": ((168, 164, 168), (248, 246, 242), (88, 84, 90)),
        "Sable": ((162, 128, 100), (248, 240, 228), (98, 72, 56)),
    },
    "Roborovski": {
        "Tan": ((226, 188, 136), (252, 248, 242)),
        "White": ((246, 242, 236), (255, 253, 250)),
    },
    "TeddyBear": {
        "Golden": ((234, 182, 112), (252, 240, 222)),
        "Cream": ((246, 226, 192), (253, 247, 236)),
        "Chocolate": ((140, 96, 70), (236, 216, 196)),
    },
    "Panda": {
        "White": ((250, 247, 242), (250, 247, 242), (66, 62, 72)),
    },
    "Sapphire": {
        "Blue": ((168, 174, 196), (244, 244, 248), (96, 100, 124)),
        "Gray": ((184, 184, 188), (246, 246, 246), (104, 104, 110)),
    },
    "Campbells": {
        "Gray": ((166, 146, 124), (240, 232, 220), (86, 70, 58)),
        "Fawn": ((226, 190, 142), (250, 242, 228), (150, 110, 78)),
        "Black": ((88, 84, 98), (210, 204, 210), (46, 42, 52)),
    },
}

HAMSTER_EYES = {
    "Dark": (62, 40, 36),
    "Ruby": (150, 58, 74),
}

# Most hamsters have dark eyes; a few colours carry ruby ones.
HAMSTER_BREED_EYES = {
    "SyrianGolden": ["Dark", "Ruby"],
    "WinterWhite": ["Dark", "Ruby"],
    "Campbells": ["Dark", "Ruby"],
}


def hamster_allowed_eyes(breed):
    return HAMSTER_BREED_EYES.get(breed, ["Dark"])


def hamster_coat_colors(breed, coat):
    cols = HAMSTER_COATS[breed][coat]
    return cols[0], cols[1], (cols[2] if len(cols) > 2 else cols[1])


def hamster_entries():
    out = []
    for breed, coats in HAMSTER_COATS.items():
        for coat in coats:
            main, sec, dark = hamster_coat_colors(breed, coat)
            out.append({"breed": breed, "coat": coat, "main": list(main), "secondary": list(sec), "dark": list(dark),
                        "eyes": "Dark", "allowedEyes": hamster_allowed_eyes(breed)})
    return out


def hamster_eye_entries():
    return [{"name": k, "color": list(v)} for k, v in HAMSTER_EYES.items()]


# ---------------------------------------------------------------- dog (P3)
# Placeholder dog colours per breed coat: ``main`` (region R), ``secondary`` (region G: the light muzzle, bib and paw
# tips, the Husky's mask, the Shiba's urajiro, the Corgi's and Border Collie's blaze) and ``dark`` (the Beagle's saddle
# and the Dalmatian's spots, recoloured on their own: see dog.py).  Coats follow the dog entries in
# PetCatalog.CreateDefault; the first coat per breed is its default.  The Dachshund has no art yet (arriving soon).
DOG_COATS = {
    "MixedBreed": {
        "Tan": ((214, 160, 104), (250, 238, 218)),
        "Black": ((78, 70, 76), (236, 222, 204)),
        "Brown": ((156, 104, 72), (242, 224, 200)),
        "Cream": ((240, 220, 184), (253, 246, 232)),
        "White": ((246, 242, 236), (255, 253, 250)),
    },
    "Golden": {
        "Golden": ((232, 178, 96), (248, 222, 168)),
        "Cream": ((244, 222, 178), (252, 240, 214)),
        "Red": ((204, 122, 68), (238, 190, 140)),
    },
    "Beagle": {
        "Tricolor": ((210, 150, 90), (252, 248, 242), (64, 56, 58)),
        "Tan": ((220, 172, 116), (252, 248, 242), (176, 116, 72)),
    },
    "Shiba": {
        "Red": ((226, 138, 72), (252, 238, 214)),
        "Sable": ((190, 128, 80), (248, 232, 208)),
        "Cream": ((244, 226, 196), (254, 248, 238)),
        "Black": ((72, 64, 68), (238, 208, 162)),
    },
    "Corgi": {
        "Red": ((226, 140, 70), (254, 248, 240)),
        "Sable": ((194, 132, 84), (252, 246, 238)),
        "Tricolor": ((72, 64, 70), (254, 248, 240)),
    },
    "Poodle": {
        "White": ((248, 244, 238), (255, 252, 248)),
        "Black": ((68, 62, 68), (98, 90, 96)),
        "Chocolate": ((126, 84, 62), (156, 110, 84)),
        "Cream": ((246, 226, 190), (252, 238, 212)),
    },
    "Husky": {
        "Gray": ((150, 152, 164), (250, 250, 252)),
        "Black": ((72, 70, 78), (250, 250, 252)),
        "Red": ((196, 124, 84), (252, 246, 240)),
    },
    "Samoyed": {
        "White": ((250, 248, 244), (255, 255, 255)),
        "Cream": ((248, 232, 204), (255, 250, 240)),
    },
    "Pomeranian": {
        "Orange": ((236, 152, 74), (250, 214, 160)),
        "Cream": ((246, 226, 190), (253, 244, 226)),
        "Black": ((68, 62, 68), (118, 106, 110)),
    },
    "Dalmatian": {
        "White": ((250, 248, 244), (255, 253, 250), (60, 54, 58)),
    },
    "BorderCollie": {
        "Black": ((62, 58, 64), (252, 250, 246)),
        "Red": ((158, 94, 64), (252, 248, 240)),
        "Blue": ((122, 130, 144), (252, 250, 246)),
    },
}

DOG_EYES = {
    "Brown": (124, 80, 54),
    "Amber": (180, 122, 62),
    "Blue": (110, 160, 212),
}


def dog_allowed_eyes(breed):
    return ["Brown", "Blue"] if breed == "Husky" else ["Brown", "Amber"]


def dog_coat_colors(breed, coat):
    cols = DOG_COATS[breed][coat]
    return cols[0], cols[1], (cols[2] if len(cols) > 2 else cols[1])


def dog_entries():
    out = []
    for breed, coats in DOG_COATS.items():
        for coat in coats:
            main, sec, dark = dog_coat_colors(breed, coat)
            out.append({"breed": breed, "coat": coat, "main": list(main), "secondary": list(sec), "dark": list(dark),
                        "eyes": "Blue" if breed == "Husky" else "Brown", "allowedEyes": dog_allowed_eyes(breed)})
    return out


def dog_eye_entries():
    return [{"name": k, "color": list(v)} for k, v in DOG_EYES.items()]
