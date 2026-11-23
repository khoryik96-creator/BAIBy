// Saved enums are stable content IDs. This snapshot fails on any renumbered or removed member.
// Appends (a new member, or a new numbered enum) must update the snapshot in the same change:
//   UPDATE_CONTENT_IDS=1 node --test tests/content-ids.test.mjs
// The update refuses to record a renumber or removal; those need a save migration instead.
import assert from 'node:assert/strict';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import test from 'node:test';

const assets = new URL('../unity/Assets/BAIby/', import.meta.url);
const snapshotUrl = new URL('./content-ids.snapshot.json', import.meta.url);

function sources(folder) {
  const root = new URL(`${folder}/`, assets);
  return readdirSync(root, { recursive: true })
    .filter(path => path.endsWith('.cs'))
    .sort()
    .map(path => ({ path: `${folder}/${path}`, text: stripComments(readFileSync(new URL(path, root), 'utf8')) }));
}

function stripComments(text) {
  return text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/gm, '');
}

// Every enum declared in the simulation, with C#'s implicit numbering applied.
function parseEnums(files) {
  const enums = new Map();
  for (const { path, text } of files) {
    for (const match of text.matchAll(/\benum\s+(\w+)\s*(?::\s*[\w.]+\s*)?\{([^}]*)\}/g)) {
      const [, name, body] = match;
      assert.ok(!enums.has(name), `Enum ${name} is declared twice (${path}); content-ID enums need unique names`);
      const members = [];
      let next = 0;
      let explicit = 0;
      for (const raw of body.split(',').map(part => part.replace(/\[[^\]]*\]/g, '').trim()).filter(Boolean)) {
        const member = raw.match(/^(\w+)(?:\s*=\s*(-?\d+))?$/);
        assert.ok(member, `${path}: cannot read enum member '${raw}' in ${name}; use plain integer literals`);
        let value = next;
        if (member[2] !== undefined) {
          value = Number(member[2]);
          explicit++;
        }
        members.push([member[1], value]);
        next = value + 1;
      }
      enums.set(name, { path, members, explicit });
    }
  }
  return enums;
}

// Numbered enums are content IDs: every member must carry its value, and no two may share one.
function contentIds(enums) {
  const ids = {};
  for (const [name, { path, members, explicit }] of [...enums].sort(([a], [b]) => a.localeCompare(b))) {
    if (explicit === 0) continue;
    assert.equal(explicit, members.length, `${path}: ${name} numbers some members; content-ID enums number every member`);
    const values = members.map(([, value]) => value);
    assert.equal(new Set(values).size, values.length, `${path}: ${name} reuses a value`);
    ids[name] = Object.fromEntries(members);
  }
  return ids;
}

// Renumbers and removals relative to the committed snapshot.
function breakingChanges(snapshot, current) {
  const problems = [];
  for (const [name, members] of Object.entries(snapshot)) {
    if (!current[name]) {
      problems.push(`${name} was removed or lost its explicit numbering`);
      continue;
    }
    for (const [member, value] of Object.entries(members)) {
      if (!(member in current[name])) problems.push(`${name}.${member} = ${value} was removed or renamed`);
      else if (current[name][member] !== value) problems.push(`${name}.${member} was renumbered from ${value} to ${current[name][member]}`);
    }
    for (const [member, value] of Object.entries(current[name])) {
      const owner = Object.entries(members).find(([other, old]) => old === value && other !== member);
      if (owner && !(member in members)) problems.push(`${name}.${member} reuses ${value}, which belonged to ${owner[0]}`);
    }
  }
  return problems;
}

function unrecordedAppends(snapshot, current) {
  const appends = [];
  for (const [name, members] of Object.entries(current)) {
    for (const [member, value] of Object.entries(members)) {
      if (!snapshot[name] || !(member in snapshot[name])) appends.push(`${name}.${member} = ${value}`);
    }
  }
  return appends;
}

const simulation = sources('Simulation');
const current = contentIds(parseEnums(simulation));
const snapshot = JSON.parse(readFileSync(snapshotUrl, 'utf8'));

test('saved enums keep every committed content ID', () => {
  const problems = breakingChanges(snapshot, current);
  assert.deepEqual(problems, [], `Saved content IDs changed; append new IDs instead:\n${problems.join('\n')}`);
});

test('new content IDs are recorded in the snapshot', () => {
  const appends = unrecordedAppends(snapshot, current);
  if (appends.length > 0 && process.env.UPDATE_CONTENT_IDS === '1') {
    writeFileSync(snapshotUrl, `${JSON.stringify(current, null, 2)}\n`);
    return;
  }
  assert.deepEqual(appends, [], `Record appended content IDs with UPDATE_CONTENT_IDS=1:\n${appends.join('\n')}`);
});

test('the snapshot covers the baby and pet saved enums', () => {
  for (const name of ['NurseryTheme', 'SkinTone', 'HairColor', 'HairStyle', 'Outfit', 'Headwear',
    'Species', 'PetBreed', 'PetMeter', 'PetCoat', 'PetAccessory', 'CompanionKind']) {
    assert.ok(snapshot[name], `${name} is saved and must be in the snapshot`);
  }
  assert.deepEqual(snapshot.Species, { Cat: 0, Dog: 1, Fish: 2, Rabbit: 3, Hamster: 4 });
  assert.deepEqual(snapshot.CompanionKind, { Baby: 0, Pet: 1 });
  const species = Object.entries(snapshot.Species);
  for (const [breed, id] of Object.entries(snapshot.PetBreed)) {
    const block = Math.floor(id / 100) - 1;
    assert.ok(species.some(([, value]) => value === block), `PetBreed.${breed} = ${id} is outside every species block`);
  }
});

test('every enum a save codec decodes is a snapshotted content ID', () => {
  for (const { path, text } of sources('Application')) {
    for (const [, name] of text.matchAll(/\((\w+)\)\s*\(int\)/g)) {
      assert.ok(current[name], `${path} decodes ${name}, which must number every member and be in the snapshot`);
    }
  }
});

test('the content-ID checks catch renumbers, removals and reused values', () => {
  const before = { Coat: { Cream: 0, Ginger: 1 } };
  assert.deepEqual(breakingChanges(before, { Coat: { Cream: 0, Ginger: 1, Gray: 2 } }), []);
  assert.equal(breakingChanges(before, { Coat: { Cream: 0, Ginger: 2 } }).length, 1);
  assert.equal(breakingChanges(before, { Coat: { Cream: 0 } }).length, 1);
  assert.equal(breakingChanges(before, { Coat: { Cream: 0, Red: 1 } }).length, 2);
  assert.equal(breakingChanges(before, {}).length, 1);
  assert.deepEqual(unrecordedAppends(before, { Coat: { Cream: 0, Ginger: 1, Gray: 2 } }), ['Coat.Gray = 2']);
  const parsed = contentIds(parseEnums([{ path: 'x.cs', text: 'enum Plain { A, B } enum Saved { A = 3, B = 5 }' }]));
  assert.deepEqual(parsed, { Saved: { A: 3, B: 5 } });
  assert.throws(() => contentIds(parseEnums([{ path: 'x.cs', text: 'enum Half { A = 0, B }' }])), /number every member/);
});
