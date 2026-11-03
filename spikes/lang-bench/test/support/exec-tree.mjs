// A built tree, its samples and the options of one layer run, for the tests that go through `runCases` (E1.S8b). Toys only: `run.sh` is whatever the test says, ending with `exec` unless
// the test is about a program that does not.
import { chmodSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { owned } from './owned.mjs';
import { computeIdentity, LAYER_DIR } from '../../lib/exec-layer/identity.mjs';
import { toolchains } from './exec-layer-host.mjs';
import { writeTree } from '../toolchain-fixtures.mjs';

export const MIB = 1024 * 1024;

/** An owned folder holding `run.sh` (made executable) and `files`: the built tree a layer run copies per case. */
export function builtTree(t, runSh, files = {}) {
  const tree = writeTree(owned.ownedDir(t, 'built'), { 'run.sh': runSh, ...files });
  chmodSync(path.join(tree, 'run.sh'), 0o755);
  return tree;
}

/** A samples directory with one `<id>.in` / `<id>.out` pair per id: the program reads `input` and must print `output`. */
export function samplesDir(t, ids, { input = 'hello\n', output = 'hello\n' } = {}) {
  const dir = owned.ownedDir(t, 'samples');
  for (const id of ids) {
    writeFileSync(path.join(dir, `${id}.in`), input);
    writeFileSync(path.join(dir, `${id}.out`), output);
  }
  return dir;
}

/** The options of one run through the layer. `parent` is a folder of the test's own, so a sweep sees only this test's runs; `minSweepAgeMs: 0` lets it retire a run killed a moment ago. */
/** A pinned-identity file holding the layer's LIVE hash: tests of other behaviour do not depend on the committed pin (the identity's own tests do). */
function livePin(t) {
  const file = path.join(owned.ownedDir(t, 'livepin'), 'exec-layer.json');
  writeFileSync(file, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } }));
  return file;
}

/**
 * The options of one layer run. `retireCrashed: true` (M1) registers the hook that retires the runs of a crashed or timed-out child BEFORE anything else: `node:test` runs `after` hooks first-in, first-out, so
 * the retire must come before the removal of the runs parent (registered by `ownedDir` below), or it would find an empty folder.
 */
export function layerOptions(t, { runSh = '#!/bin/bash\nexec cat\n', files = {}, ids = ['s01'], retireCrashed = false, ...overrides } = {}) {
  let options = null;
  if (retireCrashed) t.after(() => retireCrashedRuns(options));
  options = {
    pinnedIdentityFile: livePin(t),
    owned, parent: owned.ownedDir(t, 'runs'), tree: builtTree(t, runSh, files), samplesDir: samplesDir(t, ids), caseIds: ids, dirs: toolchains,
    memoryMaxBytes: 256 * MIB, caseWallMs: 20_000, runCeilingMs: 120_000, minSweepAgeMs: 0, stack: overrides.data ? 'node' : undefined, ...overrides, // a data run names its stack (D55); a test of that refusal passes `stack: undefined`
  };
  return options;
}

/**
 * Retire what a CRASHED or timed-out child left of its runs in `options.parent` (K5, Opus L5): the child is dead, so its claims say `gone`, and the layer's own sweep ends each run by its id — holder, scopes, views
 * and folder. A test with a child registers this as a parent hook BEFORE the child runs.
 */
export async function retireCrashedRuns(options) {
  const { sweepStale } = await import('../../lib/exec-layer/cleanup.mjs');
  return sweepStale({ owned: options.owned, parent: options.parent, minAgeMs: 0 });
}
