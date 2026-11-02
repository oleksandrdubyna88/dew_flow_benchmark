// E1.S8b fix batch 1 (O10, O14): the scope holder's bound comes from the RUNNER's limits — the time limit T plus the stop grace G plus one margin — not from a fixed wall plus 60 s; a caller's `caseWallMs`
// shorter than T + G is refused. One margin constant serves the scope holder and the netns holder.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { cleanup } from '../lib/exec-layer/cleanup.mjs';
import { HOLD_MARGIN_S, holdSeconds, launchPrefix } from '../lib/exec-layer/prefix.mjs';
import { openRun } from '../lib/exec-layer/run.mjs';
import { needs } from './support/exec-layer-host.mjs';
import { layerOptions } from './support/exec-tree.mjs';
import { owned } from './support/owned.mjs';

function limitsFile(t, limits) {
  const file = path.join(owned.ownedDir(t, 'limits'), 'limits.json');
  writeFileSync(file, JSON.stringify(limits));
  return file;
}

async function opened(t, overrides) {
  const result = await openRun(layerOptions(t, overrides));
  if (result.ok) t.after(() => cleanup(result.value));
  return result;
}

test('The_scope_holder_is_bound_by_T_plus_G_plus_the_margin_of_the_runners_limits', async t => {
  if (!needs(t)) return;
  const run = await opened(t, { limitsFile: limitsFile(t, { timeS: 30, graceS: 5 }), caseWallMs: undefined });
  assert.ok(run.ok, run.reason);
  assert.equal(holdSeconds(run.value), 35 + HOLD_MARGIN_S);
  const prefix = launchPrefix(run.value);
  const entry = prefix.findIndex(arg => arg.endsWith('scope-entry.sh'));
  assert.equal(prefix[entry + 3], String(35 + HOLD_MARGIN_S), 'the third argument of scope-entry.sh is the hold');
});

test('Without_a_limits_file_the_series_defaults_T_10_s_and_G_5_s_decide_the_bound', async t => {
  if (!needs(t)) return;
  const run = await opened(t, { caseWallMs: undefined });
  assert.equal(holdSeconds(run.value), 15 + HOLD_MARGIN_S);
});

test('A_case_wall_shorter_than_T_plus_G_is_refused_and_nothing_is_left', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t, { limitsFile: limitsFile(t, { timeS: 30, graceS: 5 }), caseWallMs: 20_000 });
  const refused = await openRun(options);
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /caseWallMs 20000 is shorter than the time limit 30 s plus the stop grace 5 s/);
  assert.deepEqual((await import('node:fs')).readdirSync(options.parent), [], 'no run folder, claim or holder is left');
});

test('A_limit_the_runner_would_refuse_is_refused_here_too', async t => {
  if (!needs(t)) return;
  const refused = await openRun(layerOptions(t, { limitsFile: limitsFile(t, { timeS: 'soon' }), caseWallMs: undefined }));
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /timeS/);
});
