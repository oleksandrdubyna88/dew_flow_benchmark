// E1.S8b fix batch 1 (O11): the sweep reads the run folder's PARENT at the next run's start, so a caller must use a stable parent. The layer refuses a parent it cannot sweep later — relative, missing,
// not a directory, not the caller's, or writable by others (the shared temp root) — and names the rule; the probe and the experiment scripts use `stableRunsParent()`.
import assert from 'node:assert/strict';
import { chmodSync, mkdirSync, statSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { checkParent, openRun, stableRunsParent } from '../lib/exec-layer/run.mjs';
import { layerOptions } from './support/exec-tree.mjs';
import { owned } from './support/owned.mjs';

test('A_parent_that_cannot_be_swept_later_is_refused_naming_the_rule', t => {
  const dir = owned.ownedDir(t, 'parentcheck');
  const file = path.join(dir, 'a-file');
  writeFileSync(file, '');
  const open = path.join(dir, 'open');
  mkdirSync(open);
  chmodSync(open, 0o777);
  for (const [parent, why] of [['relative/dir', /absolute/], [path.join(dir, 'missing'), /exist/], [file, /directory/], [open, /writable by others/], [os.tmpdir(), /the caller's|writable by others/], ['/usr', /the caller's/]]) {
    const verdict = checkParent(parent);
    assert.equal(verdict.ok, false, parent);
    assert.match(verdict.reason, why, parent);
    assert.match(verdict.reason, /stable/, 'the rule is named');
  }
});

test('The_callers_own_private_folder_is_a_parent_and_so_is_the_stable_one', t => {
  assert.equal(checkParent(owned.ownedDir(t, 'parentok')).ok, true);
  const stable = stableRunsParent();
  assert.equal(checkParent(stable).ok, true);
  assert.equal(stable, stableRunsParent(), 'the same path every time');
  assert.equal(statSync(stable).mode & 0o077, 0, 'private to the caller');
  assert.ok(stable.startsWith(os.homedir()), 'under the home, not the temp root');
});

test('openRun_refuses_a_parent_before_it_makes_anything', async t => {
  const refused = await openRun(layerOptions(t, { parent: os.tmpdir() }));
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /stable/);
});
