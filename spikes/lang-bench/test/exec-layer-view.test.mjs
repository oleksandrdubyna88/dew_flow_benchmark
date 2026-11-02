// E1.S8b: a fresh writable view of the built tree per case (consult B7), made outside the timed window and removed with the run. `--case-view` names it with `{case}`.
import assert from 'node:assert/strict';
import { chmodSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, readlinkSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { boxArgv } from '../lib/exec-layer/box.mjs';
import { EMPTY_CASE, makeViews, removeViews, treeBytes, viewDir, viewTemplate } from '../lib/exec-layer/case-view.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { owned } from './support/owned.mjs';
import { needs, sh, toolchains } from './support/exec-layer-host.mjs';
import { writeTree } from './toolchain-fixtures.mjs';

/** A built tree with a nested folder, a read-only file and a link; and an owned run folder to put views in. */
function fixture(t) {
  const tree = writeTree(owned.ownedDir(t, 'tree'), { 'run.sh': '#!/bin/bash\nif [ -e marker ]; then echo SEEN; else echo FRESH; fi\necho x > marker\n', 'bin/app.dll': 'DLL-BYTES', 'bin/deep/data.txt': '12345' });
  symlinkSync('bin/app.dll', path.join(tree, 'current'));
  chmodSync(path.join(tree, 'bin', 'app.dll'), 0o444);
  return { tree, runDir: owned.ownedDir(t, 'lrun') };
}

test('Every_case_and_nothing_else_gets_a_view_of_its_own_and_the_control_gets_an_empty_one', t => {
  const { tree, runDir } = fixture(t);
  const made = makeViews({ tree, runDir, caseIds: ['s01', 's02'] });
  assert.ok(made.ok, made.reason);
  assert.deepEqual(readdirSync(path.join(runDir, 'views')).sort(), [EMPTY_CASE, 's01', 's02']);
  assert.deepEqual(readdirSync(viewDir(runDir, EMPTY_CASE)), [], 'the empty-program control starts in an empty folder: its launch is timed, nothing is read from it');
  assert.equal(viewTemplate(runDir).replace('{case}', 's01'), viewDir(runDir, 's01'));
  assert.equal(readFileSync(path.join(viewDir(runDir, 's02'), 'bin', 'deep', 'data.txt'), 'utf8'), '12345');
  assert.equal(readlinkSync(path.join(viewDir(runDir, 's01'), 'current')), 'bin/app.dll', 'a link is copied as a link, never followed');
  assert.ok(lstatSync(path.join(viewDir(runDir, 's01'), 'bin', 'app.dll')).mode & 0o200, 'a read-only file of the built tree is writable in the view');
  assert.deepEqual(readdirSync(tree).sort(), ['bin', 'current', 'run.sh'], 'the built tree itself is not touched');
});

test('A_case_id_that_is_not_letters_digits_underscore_and_hyphen_or_is_the_control_is_refused', t => {
  const { tree, runDir } = fixture(t);
  for (const id of ['../x', 'a b', 'a/b', '', EMPTY_CASE]) assert.equal(makeViews({ tree, runDir, caseIds: [id] }).ok, false, JSON.stringify(id));
  assert.equal(existsSync(path.join(runDir, 'views')), false, 'nothing was made');
});

test('The_disk_budget_of_the_views_is_the_tree_times_the_cases', t => {
  const { tree } = fixture(t);
  assert.equal(treeBytes(tree), Buffer.byteLength('#!/bin/bash\nif [ -e marker ]; then echo SEEN; else echo FRESH; fi\necho x > marker\n') + 'DLL-BYTES'.length + '12345'.length);
});

test('Removing_the_views_removes_every_one_even_a_read_only_one_and_names_what_it_removed', t => {
  const { tree, runDir } = fixture(t);
  makeViews({ tree, runDir, caseIds: ['s01'] });
  chmodSync(path.join(viewDir(runDir, 's01'), 'bin'), 0o555);
  const report = removeViews(runDir);
  assert.deepEqual(report.left, []);
  assert.deepEqual(report.removed.sort(), [EMPTY_CASE, 's01']);
  assert.equal(existsSync(path.join(runDir, 'views')), false);
  assert.deepEqual(removeViews(runDir), { removed: [], left: [] }, 'a second call finds nothing and does not throw');
});

test('Two_cases_never_share_a_writable_view', async t => {
  if (!needs(t)) return;
  const { tree, runDir } = fixture(t);
  assert.ok(makeViews({ tree, runDir, caseIds: ['c1', 'c2'] }).ok);
  const run = async view => (await sh('bwrap', [...boxArgv({ view, dirs: toolchains, env: boxEnv({ dirs: toolchains }) }).slice(1), 'bash', 'run.sh'])).stdout.trim();
  assert.equal(await run(viewDir(runDir, 'c1')), 'FRESH');
  assert.equal(await run(viewDir(runDir, 'c2')), 'FRESH', 'the second case does not see the first one\'s file');
  assert.equal(existsSync(path.join(tree, 'marker')), false, 'and the built tree never receives a case\'s file');
  const shared = viewDir(runDir, 'c1');
  assert.equal(await run(shared), 'SEEN', 'the refuted shape — one view bound for both cases — shows the first case\'s file to the next');
});
