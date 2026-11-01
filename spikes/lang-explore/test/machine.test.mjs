// The machine-quiet check of the pilot (plan section 14, "the machine-quiet check"): v1's thresholds and window, a sampler that lists live sessions under BOTH ~/rsd/minigit and the pilot's root, a re-check
// every 60 s for 30 min printing each verdict, `--accept-noisy <reason>` starting anyway with the flag `started not quiet`. The sampler is scripted here; v1 owns the arithmetic of the verdict.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { processStartTime, writeState } from '../../minigit-bench/lib/session-state.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { liveSessionsOf, machineFor, pilotQuiet } from '../lib/machine.mjs';

const T = pilotQuiet().value;
const QUIET = { hostCpus: 0.1, harnessCpus: 0.05, foreignCpus: 0, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 };
const LOUD = { ...QUIET, hostCpus: 6, foreignCpus: 5.9 };

/** A fake clock whose sleep advances it; a sampler that serves `sample()` scripted by the clock. */
function rig(sampleAt) {
  let t = 0;
  const said = [];
  const machine = machineFor({
    thresholds: T, calibratedMs: 20, seriesDir: tmpDir('machine'), now: () => t, sleepFn: async ms => { t += ms; }, acceptNoisy: sampleAt.acceptNoisy ?? null,
    samplerFn: async () => async () => sampleAt.at(t),
  });
  return { machine, said, say: l => said.push(l), clock: () => t };
}

test('A_quiet_machine_passes_after_one_window', async () => {
  const r = rig({ at: () => QUIET });
  const q = await r.machine.quiet({ cellIds: [], say: r.say });
  assert.equal(q.ok, true, q.reason);
  assert.deepEqual([q.value.quiet, q.value.flags], [true, []]);
  assert.deepEqual(r.said, [], 'nothing to say about a quiet machine');
});

test('A_loaded_machine_is_re_checked_every_60_s_for_30_min_saying_each_verdict_then_refused', async () => {
  const r = rig({ at: () => LOUD });
  const q = await r.machine.quiet({ cellIds: [], say: r.say });
  assert.equal(q.ok, false);
  assert.match(q.reason, /the machine was not quiet for \d+ min: host-cpu/);
  assert.ok(r.said.length >= 14 && r.said.length <= 17, `${r.said.length} verdicts were printed, one per re-check (a 55 s window, then the 60 s wait)`);
  assert.ok(r.said.every(l => /not quiet \(host-cpu.*next check in 60 s/.test(l)), r.said[0]);
  assert.ok(r.clock() >= 30 * 60_000 && r.clock() < 36 * 60_000, `it gave up at the 30 min mark: ${r.clock()} ms`);
});

test('A_machine_that_turns_quiet_during_the_wait_starts_at_once', async () => {
  const r = rig({ at: t => (t < 5 * 60_000 ? LOUD : QUIET) });
  const q = await r.machine.quiet({ cellIds: [], say: r.say });
  assert.equal(q.ok, true, q.reason);
  assert.equal(q.value.quiet, true);
  assert.ok(r.said.length >= 3 && r.said.length < 10);
});

test('Accept_noisy_with_a_reason_starts_anyway_with_the_flag_and_without_one_it_is_refused', async () => {
  const noisy = rig({ at: () => LOUD, acceptNoisy: 'the operator accepts the load' });
  const q = await noisy.machine.quiet({ cellIds: [], say: noisy.say });
  assert.equal(q.ok, true, q.reason);
  assert.deepEqual([q.value.quiet, q.value.flags, q.value.reason], [false, ['started not quiet'], 'the operator accepts the load']);
  const blank = rig({ at: () => LOUD, acceptNoisy: '   ' });
  assert.equal((await blank.machine.quiet({ cellIds: [], say: blank.say })).ok, false, 'a blank reason is no reason');
});

test('A_live_session_under_either_root_makes_the_machine_not_quiet', () => {
  const home = tmpDir('home');
  const live = (root, series, rid) => {
    const dir = path.join(root, series, 's', rid);
    mkdirSync(dir, { recursive: true });
    writeState(path.join(dir, 'state.json'), { phase: 'running', hostPid: process.pid, hostStartTime: processStartTime(process.pid), cell: `cell-${rid}`, startedAt: new Date().toISOString(), heartbeatAt: new Date().toISOString() });
    return dir;
  };
  assert.deepEqual(liveSessionsOf(home)(), []);
  const a = live(path.join(home, 'rsd', 'minigit'), 's-20261030-aaaa', 'r1');
  const b = live(path.join(home, 'rsd', 'lang-v2', 'explore'), 'x-20261101-bbbb', 'r2');
  assert.deepEqual(liveSessionsOf(home)().sort(), [a, b].sort(), 'a v1 session and a pilot session both count as foreign load');
});
