// The start-of-run sweep of the suite (common.test-leftovers §4; fix batch 4): `run-tests.mjs` retires what a KILLED earlier run left in the temp root, on the two keys the rule names — the name and kernel
// owner are this repository's and the caller's (scope), and the claim's process is gone (abandonment). A live run's folder, a young one, an unmarked one and another token's are kept.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, utimesSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { sweepOwnedLeftovers } from './support/sweep.mjs';
import { owned } from './support/owned.mjs';

const OWNED_HELPER = fileURLToPath(new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url));

/** A folder a run made and then died without cleaning (SIGKILL): its claim names a process that is gone. */
function leftBehind(root, what, token = 'bench') {
  const script = `
    import { ownedFor } from ${JSON.stringify(`file://${OWNED_HELPER}`)};
    const o = ownedFor({ token: ${JSON.stringify(token)} });
    const scope = o.cleanupScope();
    console.log(o.ownedDir(scope, ${JSON.stringify(what)}));
    process.kill(process.pid, 'SIGKILL');
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', env: { ...process.env, TMPDIR: root } });
  return ran.stdout.trim();
}

const age = (dir, seconds) => utimesSync(dir, new Date(Date.now() - seconds * 1000), new Date(Date.now() - seconds * 1000));

test('The_suite_sweep_retires_a_dead_runs_folder_and_keeps_a_young_an_unmarked_and_another_tokens', t => {
  const root = owned.ownedDir(t, 'sweeproot');
  const dead = leftBehind(root, 'reservenext');
  const young = leftBehind(root, 'young');
  const other = leftBehind(root, 'other', 'someoneelse');
  const unmarked = path.join(root, 'testsToDelete-u0-bench-unmarked-AAAAAA'); // not this caller's owner form, and with no claim
  mkdirSync(unmarked);
  assert.ok(existsSync(dead) && existsSync(young) && existsSync(other), 'the killed runs left their folders');
  for (const dir of [dead, other, unmarked]) age(dir, 3600);
  const report = sweepOwnedLeftovers({ owned, tmp: root, minAgeMs: 600_000 });
  assert.deepEqual(report.removed.map(dir => path.basename(dir)), [path.basename(dead)], 'only the old folder of a dead run is removed');
  assert.equal(existsSync(dead), false);
  assert.equal(existsSync(`${dead}.owner`), false, 'and its claim');
  assert.ok(existsSync(young), 'a young one is kept: the age is a selector that keeps the sweep off a run between creation and claim');
  assert.ok(existsSync(other), 'another repository token\'s folder is not this sweep\'s');
  assert.ok(existsSync(unmarked), 'an unmarked folder is nobody\'s the helper knows: listed, never collected');
  assert.ok(report.kept.length >= 2);
});

test('The_suite_sweep_never_throws_on_a_missing_temp_root_and_a_live_folder_is_kept', t => {
  assert.deepEqual(sweepOwnedLeftovers({ owned, tmp: '/nonexistent-root-for-the-sweep', minAgeMs: 0 }).removed, []);
  const root = owned.ownedDir(t, 'sweeplive');
  const folder = owned.ownedDir(t, 'inuse');
  const report = sweepOwnedLeftovers({ owned, tmp: path.dirname(folder), minAgeMs: 0 });
  age(folder, 3600);
  const aged = sweepOwnedLeftovers({ owned, tmp: path.dirname(folder), minAgeMs: 0 });
  assert.ok(existsSync(folder), 'the folder of a live run (this very process) is never removed, however old');
  assert.ok(aged.kept.some(entry => entry.path === folder && /live/.test(entry.why)), 'and the sweep says why');
  assert.ok(existsSync(root));
  writeFileSync(path.join(root, 'file'), 'x');
  assert.ok(Array.isArray(report.errors));
});
