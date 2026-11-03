// E1.S8b: the case's workspace (consult B7; fix batch 1, K1/O2). No copy of the built tree per case: one EMPTY placeholder folder per case, and the box's overlay gives each launch a fresh writable
// layer over the read-only tree. `--case-view` names the placeholder with `{case}`.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { boxArgv } from '../lib/exec-layer/box.mjs';
import { CERTIFY_CASE, EMPTY_CASE, makeViews, removeTree, removeViews, viewDir, viewTemplate } from '../lib/exec-layer/case-view.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { owned } from './support/owned.mjs';
import { needs, sh, toolchains } from './support/exec-layer-host.mjs';
import { writeTree } from './toolchain-fixtures.mjs';

const RUN_SH = '#!/bin/bash\nif [ -e marker ]; then echo SEEN; else echo FRESH; fi\necho x > marker\n';
const hashTree = dir => createHash('sha256').update(readdirSync(dir, { recursive: true }).sort().map(name => { const full = path.join(dir, name); return statSync(full).isFile() ? `${name}:${readFileSync(full).toString('hex')}` : name; }).join('|')).digest('hex');

function fixture(t) {
  return { tree: writeTree(owned.ownedDir(t, 'tree'), { 'run.sh': RUN_SH, 'bin/app.dll': 'DLL-BYTES' }), runDir: owned.ownedDir(t, 'lrun') };
}

test('Every_case_and_the_reserved_ones_get_an_empty_placeholder_and_nothing_is_copied', t => {
  const { runDir } = fixture(t);
  const made = makeViews({ runDir, caseIds: ['s01', 's02'] });
  assert.ok(made.ok, made.reason);
  assert.deepEqual(readdirSync(path.join(runDir, 'views')).sort(), [CERTIFY_CASE, EMPTY_CASE, 's01', 's02']);
  for (const id of ['s01', 's02', EMPTY_CASE, CERTIFY_CASE]) assert.deepEqual(readdirSync(viewDir(runDir, id)), [], `${id} is an empty folder: the tree is not copied`);
  assert.equal(viewTemplate(runDir).replace('{case}', 's01'), viewDir(runDir, 's01'));
});

test('A_case_id_that_is_not_letters_digits_underscore_and_hyphen_or_is_reserved_is_refused', t => {
  const { runDir } = fixture(t);
  for (const id of ['../x', 'a b', 'a/b', '', EMPTY_CASE, CERTIFY_CASE]) assert.equal(makeViews({ runDir, caseIds: [id] }).ok, false, JSON.stringify(id));
  assert.equal(existsSync(path.join(runDir, 'views')), false, 'nothing was made');
});

test('Removing_the_placeholders_removes_every_one_and_names_what_it_removed', t => {
  const { runDir } = fixture(t);
  makeViews({ runDir, caseIds: ['s01'] });
  const report = removeViews(runDir);
  assert.deepEqual(report.left, []);
  assert.deepEqual(report.removed.sort(), [CERTIFY_CASE, EMPTY_CASE, 's01']);
  assert.equal(existsSync(path.join(runDir, 'views')), false);
  assert.deepEqual(removeViews(runDir), { removed: [], left: [] }, 'a second call finds nothing and does not throw');
});

test('A_tree_a_program_made_unusable_with_chmod_000_is_still_removed', t => {
  const { runDir } = fixture(t);
  makeViews({ runDir, caseIds: ['s01'] });
  const hidden = path.join(viewDir(runDir, 's01'), 'sub', 'deeper');
  mkdirSync(hidden, { recursive: true });
  writeFileSync(path.join(hidden, 'f'), 'x');
  chmodSync(hidden, 0o000);
  chmodSync(path.join(viewDir(runDir, 's01'), 'sub'), 0o000);
  t.after(() => { try { removeTree(viewDir(runDir, 's01')); } catch { /* gone */ } });
  const report = removeViews(runDir);
  assert.deepEqual(report.left, [], 'chmod u+rwx top-down before reading: nothing is left');
  assert.equal(existsSync(viewDir(runDir, 's01')), false);
});

test('Two_cases_never_share_a_writable_view', async t => {
  if (!needs(t)) return;
  const { tree, runDir } = fixture(t);
  assert.ok(makeViews({ runDir, caseIds: ['c1', 'c2'] }).ok);
  const before = hashTree(tree);
  const run = async view => (await sh('bwrap', [...boxArgv({ view, tree, dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: null }).slice(1), 'bash', 'run.sh'])).stdout.trim();
  assert.equal(await run(viewDir(runDir, 'c1')), 'FRESH');
  assert.equal(await run(viewDir(runDir, 'c2')), 'FRESH', 'the second case does not see the first one\'s file');
  assert.equal(hashTree(tree), before, 'the built tree is never written');
  assert.deepEqual(readdirSync(viewDir(runDir, 'c1')), [], 'and nothing of the box\'s writes lands on the host placeholder');
  const base = boxArgv({ view: viewDir(runDir, 'c2'), dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: null }).slice(1);
  await sh('bwrap', [...base, 'bash', '-c', 'echo x > marker']);
  assert.equal((await sh('bwrap', [...base, 'bash', '-c', 'if [ -e marker ]; then echo SEEN; else echo FRESH; fi'])).stdout.trim(), 'SEEN', 'the refuted shape — one writable folder bound for every launch — shows the first launch\'s file to the next');
});
