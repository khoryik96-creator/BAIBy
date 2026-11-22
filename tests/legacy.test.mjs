import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { createContext, Script } from 'node:vm';

const prototype = readFileSync(new URL('../legacy/BAIby_v5.3.html', import.meta.url));
const source = prototype.toString('utf8').match(/<script>([\s\S]*?)<\/script>/)[1];

function gameContext() {
  const save = new Map();
  const context = createContext({
    document: { createElement: () => ({}), head: { appendChild() {} }, addEventListener() {} },
    window: { addEventListener() {} },
    localStorage: {
      setItem: (key, value) => save.set(key, value),
      getItem: key => save.get(key) ?? null,
    },
  });

  const bootIndex = source.lastIndexOf('(function boot()');
  assert.ok(bootIndex > 0, 'Legacy boot entry point exists');
  new Script(source.slice(0, bootIndex)).runInContext(context);
  for (const effect of ['setBubble', 'animateBaby', 'toast', 'comp', 'renderAll', 'rebuildGrid', 'showSaveInd']) {
    context[effect] = () => {};
  }
  context.SFX = { feedHealthy() {} };
  context.G = context.freshState('Mia', 'girl', 'normal');
  return context;
}

test('the archived prototype matches the upload and its script parses', () => {
  const expected = readFileSync(new URL('legacy.sha256', import.meta.url), 'utf8').trim();
  assert.equal(createHash('sha256').update(prototype).digest('hex'), expected);
  assert.doesNotThrow(() => new Script(source));
});

test('the legacy first feed counts toward growth and a third feed is rejected', () => {
  const game = gameContext();
  const food = game.FOODS[0];
  game.doFeed(food);
  assert.equal(game.G.sdf, 1);
  game.doFeed(food);
  const satiety = game.G.hu;
  game.doFeed(food);
  assert.equal(game.G.uses.food, 2);
  assert.equal(game.G.sdf, 1);
  assert.equal(game.G.hu, satiety);
});

test('the legacy save restores identity, progress, customization and selected tab', () => {
  const game = gameContext();
  game.doFeed(game.FOODS[0]);
  game.G.custom.hair = 2;
  game.curTab = 'care';
  const savedState = JSON.stringify(game.G);
  game.saveGame();
  game.G = {};
  game.curTab = 'food';

  assert.equal(game.loadGame(), true);
  assert.equal(JSON.stringify(game.G), savedState);
  assert.equal(game.curTab, 'care');
});
