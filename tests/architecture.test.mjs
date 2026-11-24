import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import test from 'node:test';

const root = new URL('../unity/Assets/BAIby/', import.meta.url);
const allowed = new Map([
  ['BAIby.Simulation', []],
  ['BAIby.Application', ['BAIby.Simulation']],
  ['BAIby.Infrastructure', ['BAIby.Application']],
  ['BAIby.Character', ['BAIby.Simulation']],
  ['BAIby.Presentation', ['BAIby.Application', 'BAIby.Simulation', 'BAIby.Character']],
  ['BAIby.Bootstrap', ['BAIby.Application', 'BAIby.Simulation', 'BAIby.Presentation', 'BAIby.Infrastructure', 'BAIby.Character']],
]);

test('runtime assemblies preserve the simulation and storage boundaries', () => {
  const definitions = readdirSync(root, { recursive: true })
    .filter(path => path.endsWith('.asmdef'))
    .map(path => JSON.parse(readFileSync(new URL(path, root), 'utf8')));
  const byName = new Map(definitions.map(definition => [definition.name, definition]));

  for (const [name, dependencies] of allowed) {
    const definition = byName.get(name);
    assert.ok(definition, `Missing assembly definition: ${name}`);
    for (const dependency of definition.references ?? []) {
      assert.ok(!dependency.startsWith('GUID:'), `${name} must use named assembly references`);
      if (dependency.startsWith('BAIby.')) {
        assert.ok(dependencies.includes(dependency), `${name} cannot depend on ${dependency}`);
      }
    }
  }

  for (const name of ['BAIby.Simulation', 'BAIby.Application']) {
    assert.equal(byName.get(name).noEngineReferences, true, `${name} must stay Unity-independent`);
  }
});

test('the startup scene is enabled with its committed asset GUID', () => {
  const unity = new URL('../unity/', import.meta.url);
  const scene = 'Assets/Scenes/Nursery.unity';
  assert.ok(readFileSync(new URL(scene, unity), 'utf8').startsWith('%YAML'));
  const metadata = readFileSync(new URL(`${scene}.meta`, unity), 'utf8');
  const settings = readFileSync(new URL('ProjectSettings/EditorBuildSettings.asset', unity), 'utf8');
  const guid = metadata.match(/^guid: ([a-f0-9]{32})$/m)?.[1];
  assert.ok(guid, 'Nursery scene has a valid GUID');
  assert.match(settings, new RegExp(`enabled: 1\\s+path: Assets/Scenes/Nursery\\.unity\\s+guid: ${guid}`));
});

// The offline compile checks build every source file into one project, so they cannot see a missing assembly
// reference. Each BAIby namespace a source file names must belong to its own assembly or one it references.
test('every BAIby namespace a file uses is an assembly it references', () => {
  const assets = new URL('../unity/Assets/', import.meta.url);
  const asmdefs = readdirSync(assets, { recursive: true }).filter(path => path.endsWith('.asmdef'));
  const namespaceOf = name => name === 'BAIby.PlayModeTests' ? null : name;
  for (const asmdef of asmdefs) {
    const definition = JSON.parse(readFileSync(new URL(asmdef, assets), 'utf8'));
    const own = namespaceOf(definition.name);
    const visible = new Set([own, ...(definition.references ?? [])].filter(Boolean));
    const folder = asmdef.slice(0, asmdef.lastIndexOf('/') + 1);
    const sources = readdirSync(new URL(folder, assets), { recursive: true })
      .filter(path => path.endsWith('.cs'))
      .filter(path => !asmdefs.some(other => other !== asmdef && other.startsWith(folder) &&
        (folder + path).startsWith(other.slice(0, other.lastIndexOf('/') + 1))));
    for (const source of sources) {
      const code = readFileSync(new URL(folder + source, assets), 'utf8').replace(/\/\/.*$/gm, '');
      for (const match of code.matchAll(/\bBAIby\.(Simulation|Application|Infrastructure|Character|Presentation|Bootstrap)\b/g)) {
        const used = `BAIby.${match[1]}`;
        assert.ok(visible.has(used), `${folder}${source} uses ${used}, which ${definition.name} does not reference`);
      }
    }
  }
});
