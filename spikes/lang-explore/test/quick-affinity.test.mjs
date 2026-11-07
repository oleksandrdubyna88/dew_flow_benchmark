// The affinity read-back (section 2.3). A mask that was asked for is not a mask that was applied: the pass child's own `Cpus_allowed_list` is read back and compared in any spelling.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { findOnPath } from '../../minigit-bench/lib/find-on-path.mjs';
import { hostLacks, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { EXIT } from '../../minigit-bench/lib/bare-exit.mjs';
import { gates } from '../../minigit-bench/lib/bare-gates.mjs';
import { affinityProblem, normalizeCpuList, probeMask } from '../lib/affinity.mjs';
import { maskReadBack } from '../lib/mask-preflight.mjs';
import { MASKS } from '../lib/series.mjs';
import { preflightDep } from '../lib/run-ctx.mjs';
import { EXPLORE } from './quick-series-fixture.mjs';

test('The_read_back_accepts_a_Cpus_allowed_list_that_is_exactly_the_profile_in_any_spelling_and_refuses_any_other', () => {
  for (const [wanted, observed] of [['0-7', '0,1,2,3,4,5,6,7'], ['0-7', '0-3,4-7'], ['6-7', '6,7'], ['0-7', '0-7'], ['6-7', '7,6']]) assert.equal(affinityProblem({ wanted, observed }), null, `${wanted} against ${observed}`);
  for (const [wanted, observed] of [['0-7', '0-6'], ['0-7', '0-8'], ['6-7', '6'], ['6-7', ''], ['6-7', null], ['0-7', 'x-y']]) {
    const problem = affinityProblem({ wanted, observed });
    assert.match(problem, /^affinity-not-applied: wanted /, `${wanted} against ${observed}`);
    assert.ok(problem.includes(wanted) && problem.includes(String(observed ?? '')), 'both masks are named');
  }
  assert.equal(normalizeCpuList('0,1,2-7'), '0-7');
  assert.equal(normalizeCpuList('3'), '3');
  assert.equal(normalizeCpuList('1,3,4'), '1,3-4');
  for (const bad of ['', null, undefined, 'a', '3-1', '1,,2']) assert.equal(normalizeCpuList(bad), null, String(bad));
});

test('The_mask_probe_runs_the_status_read_under_taskset_and_answers_the_list_the_kernel_applied', async () => {
  const seen = [];
  const spawn = async (exe, args) => { seen.push([exe, ...args]); return ok({ code: 0, stdout: 'Name:\tcat\nCpus_allowed_list:\t6-7\nMems_allowed:\t1\n', stderr: '', timedOut: false }); };
  const r = await probeMask({ cpus: '6-7', spawn });
  assert.deepEqual(r, ok('6-7'));
  assert.deepEqual(seen, [['taskset', '-c', '6-7', 'cat', '/proc/self/status']]);
  assert.equal((await probeMask({ cpus: '6-7', spawn: async () => fail('no taskset') })).ok, false);
  assert.equal((await probeMask({ cpus: '6-7', spawn: async () => ok({ code: 1, stdout: '', stderr: 'bad cpu list', timedOut: false }) })).ok, false);
  assert.equal((await probeMask({ cpus: '6-7', spawn: async () => ok({ code: 1, stdout: 'Cpus_allowed_list:\t6-7\n', stderr: '', timedOut: false }) })).ok, false, 'a failed probe is no read-back even with a list in its output');
  assert.equal((await probeMask({ cpus: '6-7', spawn: async () => ok({ code: 0, stdout: 'Name: cat\n', stderr: '', timedOut: false }) })).ok, false, 'a status with no list is no read-back');
});

test('Before_a_paid_launch_the_preflight_probes_each_mask_of_the_pair_and_refuses_the_launch_resumably_when_a_probe_reads_back_another_mask', async () => {
  const seriesDir = tmpDir('quick-mask-preflight');
  const ctx = { kind: 'pilot', pair: 1, seriesDir, series: { draw: { csharpHigh: true } } };
  const probed = [];
  const wrong = async cpus => { probed.push(cpus); return ok(cpus === MASKS.low ? '0-4' : cpus); };
  const refused = await maskReadBack({ ctx, probe: wrong });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /^affinity-not-applied: wanted 0-5, observed 0-4/);
  assert.deepEqual([...probed].sort(), [MASKS.high, MASKS.low].sort(), 'both masks of the pair are probed');
  assert.equal(existsSync(path.join(seriesDir, 'readback.json')), false, 'readback.json is not written for a refusal');
  // the loop's gates turn it into a resumable halt before anything is launched
  let launched = 0;
  const deps = { guard: async () => ({ ok: true }), retireStale: async () => {}, preflight: async () => maskReadBack({ ctx, probe: wrong }), launch: () => { launched += 1; } };
  const halt = await gates({ ctx, deps, step: { phase: 'v1', sessions: [] }, state: {}, h: { get: () => ({ sessions: {} }) }, say: () => {} });
  assert.equal(halt.exit, EXIT.halt);
  assert.match(halt.message, /the preflight refuses: affinity-not-applied/);
  assert.equal(launched, 0);
  // a good read-back goes, and says so in readback.json (the pair's masks as wanted and as observed)
  const good = await maskReadBack({ ctx, probe: async cpus => ok(cpus.replace('0-5', '0,1,2,3,4,5')), now: () => new Date('2026-11-07T10:00:00.000Z') });
  assert.equal(good.ok, true, good.reason);
  assert.deepEqual(JSON.parse(readFileSync(path.join(seriesDir, 'readback.json'), 'utf8')), { pairs: { 1: { at: '2026-11-07T10:00:00.000Z', masks: { '0-5': '0,1,2,3,4,5', '8-13': '8-13' } } } });
});

test('The_runs_preflight_edge_calls_the_mask_read_back_after_the_base_preflight_and_a_rehearsal_asks_for_none', async () => {
  const seriesDir = tmpDir('quick-preflight-edge');
  const ctx = { kind: 'pilot', pair: 2, seriesDir, series: { draw: { csharpHigh: false } }, nodeDir: '/n', tsDir: '/t' };
  const world = { explore: EXPLORE, paths: { claudeBinary: '/x/claude' } };
  const order = [];
  const baseFn = async () => { order.push('base'); return { ok: true }; };
  const refused = await preflightDep({ ctx, world, preflightHome: '/h', baseFn, maskProbe: async cpus => { order.push(`probe ${cpus}`); return ok('0-4'); } })();
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /^affinity-not-applied: /);
  assert.equal(order[0], 'base', 'the base preflight comes first');
  assert.ok(order.length >= 2 && order.slice(1).every(x => x.startsWith('probe ')));
  const stub = await preflightDep({ ctx, world, preflightHome: '/h', baseFn, maskProbe: null })();
  assert.equal(stub.ok, true, 'a rehearsal asks for no read-back');
  const baseRefuses = await preflightDep({ ctx, world, preflightHome: '/h', baseFn: async () => ({ ok: false, reason: 'no token file', problems: [] }), maskProbe: async () => ok('x') })();
  assert.match(baseRefuses.reason, /no token file/);
});

// ---- on this host ---------------------------------------------------------------------------------------------------------------------------------------

const NESTED = 'const { spawnSync } = require("node:child_process"); const out = {}; for (const mask of ["0-7", "6-7"]) { const r = spawnSync("taskset", ["-c", mask, "cat", "/proc/self/status"], { encoding: "utf8" }); out[mask] = /^Cpus_allowed_list:\\s*(\\S+)/m.exec(r.stdout)?.[1] ?? null; } process.stdout.write(JSON.stringify(out));';

test('On_this_host_a_pass_child_started_under_each_profile_reads_back_exactly_that_profile_even_from_a_parent_pinned_to_CPUs_6_and_7', t => {
  const cpus = os.cpus().length;
  if (process.platform !== 'linux' || findOnPath('taskset') === null) return hostLacks(t, 'taskset on Linux is needed to read a mask back');
  if (cpus < 8) return hostLacks(t, `the profiles name CPUs 0 to 7 and this host has ${cpus}`);
  const r = spawnSync('taskset', ['-c', '6,7', process.execPath, '-e', NESTED], { encoding: 'utf8', timeout: 30_000 });
  assert.equal(r.status, 0, r.stderr);
  const seen = JSON.parse(r.stdout);
  assert.equal(normalizeCpuList(seen['0-7']), '0-7', 'a child may widen past its parent\'s mask');
  assert.equal(normalizeCpuList(seen['6-7']), '6-7');
  assert.equal(affinityProblem({ wanted: '0-7', observed: seen['0-7'] }), null);
});
