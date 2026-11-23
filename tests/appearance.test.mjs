import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import test from 'node:test';

const resources = new URL('../unity/Assets/Resources/BAIby/Baby/', import.meta.url);
const appearance = new URL('Appearance/', resources);
const model = readFileSync(new URL('../unity/Assets/BAIby/Simulation/BabyAppearance.cs', import.meta.url), 'utf8');
const poses = ['Newborn', 'NewbornRest', 'Sitter', 'SitterRest', 'Crawler', 'CrawlerRest', 'Walker', 'WalkerRest'];

function enumMembers(name) {
  const body = model.match(new RegExp(`enum ${name} \\{([^}]*)\\}`))?.[1];
  assert.ok(body, `BabyAppearance.cs declares ${name}`);
  return body.split(',').map(entry => entry.split('=')[0].trim()).filter(Boolean);
}

function pngSize(url) {
  const bytes = readFileSync(url);
  assert.equal(bytes.toString('latin1', 1, 4), 'PNG', `${url.pathname} is a PNG`);
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}

function assertMeta(url) {
  const meta = new URL(url.href.replace(/\/$/, '') + '.meta');
  assert.ok(existsSync(meta), `${url.pathname} has a Unity .meta file`);
  assert.match(readFileSync(meta, 'utf8'), /^guid: [a-f0-9]{32}$/m, `${meta.pathname} has a GUID`);
}

const layout = JSON.parse(readFileSync(new URL('layout.json', appearance), 'utf8'));

test('appearance layout names every pose at its illustration size', () => {
  for (const pose of poses) {
    const entry = layout.poses.find(candidate => candidate.name === pose);
    assert.ok(entry, `layout lists ${pose}`);
    const original = pngSize(new URL(`${pose}.png`, resources));
    assert.deepEqual({ width: entry.width, height: entry.height }, original, `${pose} layout matches its illustration`);
    for (const file of [`Base/${pose}.bytes`, `Base/${pose}Regions.bytes`]) {
      assert.deepEqual(pngSize(new URL(file, appearance)), original, `${file} matches ${pose}`);
    }
  }
  for (const region of ['skin', 'hair', 'outfitMain', 'outfitAccent']) {
    const color = layout.reference[region];
    assert.ok(Array.isArray(color) && color.length === 3 && color.every(c => Number.isInteger(c) && c > 0 && c <= 255),
      `reference ${region} is a usable RGB color`);
  }
});

test('every hair style and headwear piece has an overlay for every pose', () => {
  const expected = [
    ...enumMembers('HairStyle').map(style => `Hair/${style}`),
    ...enumMembers('Headwear').filter(kind => kind !== 'None').map(kind => `Headwear/${kind}`),
  ];
  for (const folder of expected) {
    for (const pose of poses) {
      const path = `${folder}/${pose}`;
      const overlay = layout.overlays.find(candidate => candidate.path === path);
      assert.ok(overlay, `layout places ${path}`);
      assert.equal(overlay.pose, pose);
      const bounds = layout.poses.find(candidate => candidate.name === pose);
      assert.ok(overlay.x >= 0 && overlay.y >= 0 && overlay.x + overlay.width <= bounds.width &&
        overlay.y + overlay.height <= bounds.height, `${path} lies inside ${pose}`);
      for (const file of [`${path}.bytes`, `${path}Regions.bytes`]) {
        assert.deepEqual(pngSize(new URL(file, appearance)), { width: overlay.width, height: overlay.height },
          `${file} matches its layout size`);
      }
    }
  }
});

test('every appearance asset and folder has a Unity .meta file', () => {
  assertMeta(appearance);
  for (const entry of readdirSync(appearance, { recursive: true })) {
    if (entry.endsWith('.meta')) continue;
    const url = new URL(entry, appearance);
    assertMeta(statSync(url).isDirectory() ? new URL(entry + '/', appearance) : url);
  }
});
