// Two fixes born while the quick run was running: a rehearsal series never counts toward the plan's spend (another worker's rehearsal of a new task must not stop a paid pair), and `--accept-noisy` starts at once
// instead of waiting out the quiet window first. The ledgers are fixtures in a temp home and the sampler is scripted; nothing is launched.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { renameSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { appendLedger } from '../../minigit-bench/lib/ledger.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { machineFor, pilotQuiet } from '../lib/machine.mjs';
import { exploreRoot } from '../lib/paths.mjs';
import { pauseVerdict, planSpend } from '../lib/pause.mjs';
import { quickPlan } from '../lib/quick.mjs';
import { createSeries, ledgerFileOf } from '../lib/series.mjs';

const EXPLORE = loadExplore().value;
const PLAN = quickPlan(EXPLORE);
const TASK = PLAN.planTasks[0];
const CALIBRATION = { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } };
let counter = 0;

/** A series of a quick task renamed to `name`, whose ledger holds one `usd` charge per entry. */
function seriesNamed(home, name, usds) {
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: TASK, rand: () => (counter++).toString(16).padStart(4, '0') });
  const target = path.join(exploreRoot(home), name);
  renameSync(made.value.dir, target);
  usds.forEach((usd, i) => appendLedger(ledgerFileOf(target), { kind: 'pilot', usd, what: 'completed', rid: `r${i}`, t: `2026-11-05T0${i % 10}:00:00Z` }));
}

test('A_rehearsal_series_of_a_plan_task_with_charges_in_its_ledger_adds_nothing_to_the_plans_spend', () => {
  const home = tmpDir('quick-spend');
  seriesNamed(home, 'x-20261105-aaaa', [7, 8]);
  seriesNamed(home, 'r-20261105-bbbb', [40, 40]);
  const spend = planSpend({ home, explore: EXPLORE, plan: PLAN });
  assert.deepEqual([spend.series, spend.sessions, spend.spentUsd], [1, 2, 15], 'only the paid series counts');
  const verdict = pauseVerdict({ home, explore: EXPLORE, plan: PLAN });
  assert.equal(verdict.stop, false, 'the rehearsal spend of 80 would have stopped the pair');
});

test('A_series_that_is_not_a_rehearsal_with_the_same_charges_does_count', () => {
  const home = tmpDir('quick-spend-paid');
  seriesNamed(home, 'x-20261105-aaaa', [7, 8]);
  seriesNamed(home, 'x-20261105-bbbb', [40, 40]);
  assert.equal(planSpend({ home, explore: EXPLORE, plan: PLAN }).spentUsd, 95);
});

const T = pilotQuiet().value;
const LOUD = { hostCpus: 6, harnessCpus: 0.05, foreignCpus: 5.9, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 };

function noisyRig(acceptNoisy) {
  let t = 0;
  const machine = machineFor({ thresholds: T, calibratedMs: 20, seriesDir: tmpDir('machine'), now: () => t, sleepFn: async ms => { t += ms; }, acceptNoisy, samplerFn: async () => async () => LOUD });
  return { machine, elapsed: () => t };
}

test('Accept_noisy_starts_at_once_it_does_not_first_wait_out_the_quiet_window', async () => {
  const accepted = noisyRig('the operator accepts the load');
  const q = await accepted.machine.quiet({ cellIds: [], say: () => {} });
  assert.deepEqual([q.ok, q.value.flags], [true, ['started not quiet']]);
  assert.ok(accepted.elapsed() < T.maxWaitMs / 2, `waited ${accepted.elapsed()} ms of ${T.maxWaitMs}`);
  const refused = noisyRig(null);
  assert.equal((await refused.machine.quiet({ cellIds: [], say: () => {} })).ok, false);
  assert.ok(refused.elapsed() >= T.maxWaitMs, 'without the flag the whole window is waited');
});
