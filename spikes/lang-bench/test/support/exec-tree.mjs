// A built tree, its samples and the options of one layer run, for the tests that go through `runCases` (E1.S8b). Toys only: `run.sh` is whatever the test says, ending with `exec` unless
// the test is about a program that does not.
import { chmodSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { owned } from './owned.mjs';
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
export function layerOptions(t, { runSh = '#!/bin/bash\nexec cat\n', files = {}, ids = ['s01'], ...overrides } = {}) {
  return {
    owned, parent: owned.ownedDir(t, 'runs'), tree: builtTree(t, runSh, files), samplesDir: samplesDir(t, ids), caseIds: ids, dirs: toolchains,
    memoryMaxBytes: 256 * MIB, caseWallMs: 20_000, runCeilingMs: 120_000, minSweepAgeMs: 0, ...overrides,
  };
}
