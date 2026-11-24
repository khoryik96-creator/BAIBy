"""Renders the interface's line icons to small PNG sprites for Unity.

Each icon is a 24 x 24 viewBox drawn with a 2-unit round stroke (the paths come from the approved mockups
where they exist). They render white on a transparent background at 96 px, so the game tints them with
Image.color: ink on light tiles, white on the sage primary, blush text colour inside the condition chip.

    pip install -r tools/ui/requirements.txt
    python3 tools/ui/render_icons.py

Writes unity/Assets/Resources/BAIby/UI/Icons/<name>.png and a sprite .meta for any new file; existing metas
keep their GUIDs.
"""

import io
import os
import sys
import uuid

import cairosvg

SIZE = 96
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Resources', 'BAIby', 'UI', 'Icons')

# name -> (stroked paths, filled paths)
ICONS = {
    'Home': (['M3 10.5 12 3l9 7.5', 'M5 9.5V20h14V9.5', 'M10 20v-5h4v5'], []),
    'HomeAdd': (['M3 10.5 12 3l9 7.5', 'M5 9.5V20h14V9.5', 'M12 11v5M9.5 13.5h5'], []),
    'Style': (['M9 3 4 6l2 4 2-1v12h8V9l2 1 2-4-5-3a3 3 0 0 1-6 0Z'], []),
    'Decor': (['M6 20v-2', 'M18 20v-2', 'M4 14a2 2 0 0 1 4 0v1h8v-1a2 2 0 0 1 4 0v4H4Z',
               'M6 12V8a3 3 0 0 1 3-3h6a3 3 0 0 1 3 3v4'], []),
    'Feed': (['M9 2h6M10 2v3l-2 3v12a2 2 0 0 0 2 2h4a2 2 0 0 0 2-2V8l-2-3V2M8 12h8'], []),
    'Soothe': (['M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10Z'], []),
    'Rest': (['M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5Z'], []),
    'Wash': (['M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11Z'], []),
    'Play': (['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18ZM3.5 9h17M3.5 15h17M12 3c2.5 2.5 2.5 15.5 0 18'], []),
    'Story': (['M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2ZM4 21V5M8 7h7'], []),
    'NextDay': (['M5 5v14l8-7Z', 'M13 5v14l8-7Z'], []),
    'Growth': (['M12 21v-9', 'M12 12c0-4 3-7 7-7 0 4-3 7-7 7Z', 'M12 14c0-3-2.5-5.5-6-5.5 0 3 2.5 5.5 6 5.5Z'], []),
    'Gear': (['M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z',
              'M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 '
              '1.5V21a2 2 0 0 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 '
              '1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 0 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1'
              '-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 0 1 4 0v.1a1.7 1.7 0 0 0 '
              '1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 '
              '1H21a2 2 0 0 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z'], []),
    'Paw': ([], ['M8.2 10a2.2 2.2 0 1 1-4.4 0 2.2 2.2 0 0 1 4.4 0Z', 'M12.2 5.5a2.2 2.2 0 1 1-4.4 0 2.2 2.2 0 0 1 4.4 0Z',
                 'M16.2 5.5a2.2 2.2 0 1 1-4.4 0 2.2 2.2 0 0 1 4.4 0Z', 'M20.2 10a2.2 2.2 0 1 1-4.4 0 2.2 2.2 0 0 1 4.4 0Z',
                 'M12 11c-3 0-6 4-6 7a3 3 0 0 0 3 3c1.2 0 2-.6 3-.6s1.8.6 3 .6a3 3 0 0 0 3-3c0-3-3-7-6-7Z']),
    'Thermometer': (['M14 4a2 2 0 0 0-4 0v10.5a4 4 0 1 0 4 0Z'], []),
    'Fever': (['M14 4a2 2 0 0 0-4 0v10.5a4 4 0 1 0 4 0Z', 'M12 9v7'], []),
    'Doctor': (['M6 3v6a4 4 0 0 0 8 0V3', 'M10 13v2a5 5 0 0 0 10 0v-2', 'M22 11a2 2 0 1 1-4 0 2 2 0 0 1 4 0Z'], []),
    'Medicine': (['M10.5 20.5 20.5 10.5a4.95 4.95 0 1 0-7-7l-10 10a4.95 4.95 0 1 0 7 7Z', 'M8.5 8.5l7 7'], []),
    'Rattle': (['M15 9a5 5 0 1 1-10 0 5 5 0 0 1 10 0Z', 'M8.2 7.8h.01M11.8 7.8h.01M10 10.8h.01', 'M13.5 12.5 19 18', 'M17 20.5l3.5-3.5'], []),
    'Plush': (['M18 13a6 6 0 1 1-12 0 6 6 0 0 1 12 0Z', 'M9.5 7.6A2.5 2.5 0 1 0 6.6 10',
               'M14.5 7.6A2.5 2.5 0 1 1 17.4 10', 'M10 12.5h.01M14 12.5h.01', 'M10.5 15.5c.8.7 2.2.7 3 0'], []),
    'Blocks': (['M4 14h7v7H4Z', 'M13 14h7v7h-7Z', 'M8.5 5h7v7h-7Z'], []),
    'Diaper': (['M3 6h18v3c0 5.5-4 9-9 9s-9-3.5-9-9Z', 'M8.5 6v2.5', 'M15.5 6v2.5'], []),
    'Bath': (['M3 12h18v2a6 6 0 0 1-6 6H9a6 6 0 0 1-6-6Z', 'M6 12V6a2 2 0 0 1 4 0', 'M7 20l-1 2', 'M17 20l1 2'], []),
    'Sound': (['M4 9v6h4l5 4V5L8 9Z', 'M16 9a4 4 0 0 1 0 6', 'M19 6a8 8 0 0 1 0 12'], []),
    'Report': (['M9 3h6v3H9Z', 'M7 4.5H6a2 2 0 0 0-2 2V19a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6.5a2 2 0 0 0-2-2h-1',
                'M8 11h8M8 15h5'], []),
    'Routine': (['M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z', 'M12 7v5l3 2'], []),
    'Back': (['M15 18 9 12l6-6'], []),
    'Close': (['M18 6 6 18', 'M6 6l12 12'], []),
    'Plus': (['M12 5v14', 'M5 12h14'], []),
    'Check': (['M20 6 9 17l-5-5'], []),
    'Lock': (['M6 11h12v10H6Z', 'M8 11V7a4 4 0 0 1 8 0v4'], []),
    'Star': (['M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9Z'], []),
    'Sun': (['M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z',
             'M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4'], []),
    'Explore': (['M18 11a7 7 0 1 1-14 0 7 7 0 0 1 14 0Z', 'M20.5 20.5 16 16'], []),
    'Bowl': (['M3 11h18c0 5-4 9-9 9s-9-4-9-9Z', 'M9 7c0-1.5 1.2-3 3-3s3 1.5 3 3'], []),
    'Wand': (['M4 20 14 10', 'M17 3v4M15 5h4', 'M20 9v2M19 10h2'], []),
    'Brush': (['M5 19l5-5', 'M10 14l7.5-7.5a2.5 2.5 0 0 1 3.5 3.5L13.5 17.5Z', 'M14.5 9.5l3 3'], []),
    'Pet': (['M7 12V6a1.5 1.5 0 0 1 3 0v5', 'M10 11V4.5a1.5 1.5 0 0 1 3 0V11', 'M13 11V5.5a1.5 1.5 0 0 1 3 0V12',
             'M16 12V8.5a1.5 1.5 0 0 1 3 0V14a7 7 0 0 1-7 7h-1a6 6 0 0 1-5-2.7L3.5 14a1.5 1.5 0 0 1 2.5-1.7L7 14'], []),
    'Heart': ([], ['M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10Z']),
}

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 128
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 128
    textureFormat: -1
    textureCompression: 0
    crunchedCompression: 0
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def svg(stroked, filled):
    parts = [f'<path d="{d}" fill="none" stroke="#FFFFFF" stroke-width="2" stroke-linecap="round" '
             f'stroke-linejoin="round"/>' for d in stroked]
    parts += [f'<path d="{d}" fill="#FFFFFF"/>' for d in filled]
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">'
            + ''.join(parts) + '</svg>')


def ensure_meta(path, template, **values):
    meta = path + '.meta'
    if not os.path.exists(meta):
        with open(meta, 'w', newline='\n') as handle:
            handle.write(template.format(guid=uuid.uuid4().hex, **values))


def main():
    for folder in (os.path.dirname(OUT), OUT):
        os.makedirs(folder, exist_ok=True)
        ensure_meta(folder, FOLDER_META)
    for name, (stroked, filled) in sorted(ICONS.items()):
        target = os.path.join(OUT, name + '.png')
        png = cairosvg.svg2png(bytestring=svg(stroked, filled).encode(), output_width=SIZE, output_height=SIZE)
        with open(target, 'wb') as handle:
            handle.write(png)
        ensure_meta(target, META, sprite_id=uuid.uuid4().hex[:16] + '0800000000000000')
    stale = sorted(f for f in os.listdir(OUT) if f.endswith('.png') and f[:-4] not in ICONS)
    if stale:
        print('Not generated by this script:', ', '.join(stale), file=sys.stderr)
    print(f'{len(ICONS)} icons in {os.path.relpath(OUT, ROOT)}')


if __name__ == '__main__':
    main()
