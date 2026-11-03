// E1.S8b brief B: `exec-probe.mjs` — the host probe, run THROUGH the layer (the real prefix, the delegated scope, the v2 box). It records every line of the plan's probe list, then exits 3
// naming the first line that failed; the memory view is asserted by policy (OP-K, undecided while `memory-policy.json` says null: exit 3 `memory-policy-undecided`, never 0).
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { before, test } from 'node:test';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { allowFor, caseKeysIn } from '../lib/exec-layer/env.mjs';
import { computeIdentity, LAYER_DIR } from '../lib/exec-layer/identity.mjs';
import { recordProblems } from '../lib/exec-probe/facts.mjs';
import { buildBoxEnv } from '../lib/exec-probe/programs.mjs';
import { runDelegatedScopeLine } from '../lib/exec-probe/lines.mjs';
import { readPolicy } from '../lib/exec-probe/memory.mjs';
import { finish, shouldStampProbedOn, verdictFor } from '../lib/exec-probe/probe.mjs';
import { needs, spikeDir, toolchains } from './support/exec-layer-host.mjs';
import { owned } from './support/owned.mjs';

const PROBE = path.join(spikeDir, 'exec-probe.mjs');
let probed;

/** The probe run once as a command, with a planted host key in its environment; every test that reads the record shares it. */
async function probeOnce() {
  const scope = cleanupScope();
  const dir = owned.ownedDir(scope, 'probeout');
  const policy = path.join(dir, 'memory-policy.json');
  writeFileSync(policy, JSON.stringify({ policy: null, decided: null, by: null }));
  const out = path.join(dir, 'record.json');
  const child = spawn(process.execPath, [PROBE, '--out', out, '--policy', policy, '--no-specs'], { env: { ...process.env, WSL_INTEROP: '/run/WSL/1_interop', SECRET_TOKEN: 'planted' }, stdio: ['ignore', 'pipe', 'pipe'] });
  let stderr = '';
  child.stderr.on('data', chunk => { stderr += chunk; });
  const code = await new Promise(resolve => child.once('close', resolve));
  const record = JSON.parse(readFileSync(out, 'utf8'));
  await scope.close();
  return { code, stderr, record };
}

before(async () => { if (needs({ skip() {} })) probed = probeOnce(); });

test('The_probe_records_every_line_of_the_plan_and_exits_3_memory_policy_undecided_while_OP_K_is_open', async t => {
  if (!needs(t)) return;
  const { code, stderr, record } = await probed;
  assert.equal(code, 3, stderr);
  assert.match(stderr, /memory-policy-undecided/);
  assert.equal(record.exit.halt, 'memory-policy-undecided');
  const ids = record.lines.map(line => line.id);
  for (const id of ['netns', 'program-in-prog', 'ping-over-loopback', 'bind-list', 'cgroup-view-own', 'home-sentinel', 'mnt-c', 'cmd-exe', 'user-bus', 'empty-program-launch', 'bash-c-true-stderr', 'env-keys', 'delegated-scope', 'culture-env', 'locale-a', 'dotnet-culture', 'node-culture', 'invariant-switch']) assert.ok(ids.includes(id), `the record has the line ${id}`);
  assert.deepEqual(record.lines.filter(line => !line.ok).map(line => line.id), [], 'every line other than the memory policy passed on this host');
});

test('A_long_probe_is_not_silent_one_line_on_stderr_as_each_probe_line_and_each_memory_limit_starts_and_ends', async t => {
  if (!needs(t)) return;
  const { stderr, record } = await probed;
  for (const line of record.lines) assert.match(stderr, new RegExp(`exec-probe: ${line.id}: (ok|FAIL)`), `${line.id} has its result line`);
  for (const group of ['network', 'bind list', 'box facts', 'culture', 'empty-program launch', 'delegated scope']) assert.match(stderr, new RegExp(`exec-probe: ${group}: start`), `${group} announces its start`);
  for (const limit of [256, 512]) {
    assert.match(stderr, new RegExp(`exec-probe: memory view at ${limit} MiB: start`));
    assert.match(stderr, new RegExp(`exec-probe: memory view at ${limit} MiB: done`));
  }
  assert.ok(stderr.indexOf('network: start') < stderr.indexOf('netns: ok'), 'a start comes before its result');
});

test('The_record_names_the_date_the_host_the_layer_identity_and_the_believed_limits_at_both_limits', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  assert.match(record.date, /^\d{4}-\d{2}-\d{2}$/);
  assert.ok(record.host.hostname && record.host.kernel && record.host.cgroup2 === true);
  assert.equal(record.layerIdentity, computeIdentity(LAYER_DIR).value.execLayer.treeSha256, 'the identity is COMPUTED from the tree that ran, not read from the pin');
  assert.equal(record.layerIdentity, JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'specs.d', 'exec-layer.json'), 'utf8')).execLayer.treeSha256, 'and it is the pin');
  const runner = JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'specs.d', 'runner.json'), 'utf8')).runner;
  assert.deepEqual(record.runner, { version: runner.version, manifestSha256: runner.manifestSha256 }, 'the record names the runner it ran');
  assert.deepEqual(record.memoryView.observations.limits.map(at => at.memoryMaxBytes), [256 * 1024 * 1024, 512 * 1024 * 1024]);
  for (const at of record.memoryView.observations.limits) {
    assert.equal(at.hostMemoryMax, at.memoryMaxBytes, 'the host\'s prog/memory.max is the limit asked for');
    assert.equal(typeof at.csharp.totalAvailableBytes, 'number');
    assert.equal(typeof at.node.heapSizeLimitBytes, 'number');
    assert.equal(typeof at.csharp.cgroupVisible, 'boolean');
  }
});

test('The_record_carries_V8s_heap_size_limit_against_prog_memory_max_for_the_decision_that_has_not_been_made', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  assert.equal(record.memoryView.v8Heap.length, 2);
  for (const entry of record.memoryView.v8Heap) {
    assert.equal(typeof entry.v8HeapSizeLimitBytes, 'number');
    assert.equal(entry.ratio, Number((entry.v8HeapSizeLimitBytes / entry.memoryMaxBytes).toFixed(3)), 'the ratio is recorded');
    assert.equal(entry.asserted, false, 'recorded now; the check waits for the team lead\'s decision');
    assert.ok(entry.ratio >= 0.9 && entry.ratio < 1, `V8's heap limit is in [0.9 L, L) under D55 (${entry.ratio})`);
  }
});

test('The_record_carries_the_workloads_the_peak_over_L_of_every_over_limit_fixture_and_the_findings_T_L43_J2', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  const view = record.memoryView;
  const L = [256 * 1024 * 1024, 512 * 1024 * 1024];
  assert.equal(view.workloads.length, 2 * 2 * 5, 'five workloads (churn, medium-buffers, medium-objects, objects-70, mixed) per stack and limit');
  for (const stack of ['csharp', 'node']) for (const limit of L) for (const mode of ['churn', 'medium-buffers', 'medium-objects', 'objects-70', 'mixed']) assert.ok(view.workloads.some(row => row.stack === stack && row.mode === mode && row.memoryMaxBytes === limit), `${stack} ${mode} at ${limit}`);
  const recorded = view.workloads.filter(row => !row.asserted).map(row => `${row.stack} ${row.mode} ${row.memoryMaxBytes / 1024 / 1024}`).sort();
  assert.deepEqual(recorded, ['csharp mixed 256', 'csharp mixed 512', 'node medium-buffers 256', 'node mixed 256', 'node mixed 512'], 'the recorded-only cells are named: T21');
  assert.equal(view.overLimit.length, 2 * 2 * 2, 'two over-limit fixtures per stack and limit');
  for (const row of view.overLimit) {
    assert.equal(row.ratio, Number((row.peakBytes / row.memoryMaxBytes).toFixed(3)), 'peak/L is recorded');
    assert.equal(row.finding, Math.abs(row.ratio - 0.5) <= 0.1 + 1e-9, `${row.stack} ${row.fixture}: the finding flag follows the 10-point band`);
  }
  assert.deepEqual(view.findings, view.overLimit.filter(row => row.finding), 'findings are the rows in the band');
});

test('The_probe_record_holds_only_allow_listed_keys_even_with_a_host_key_planted_in_the_probes_own_environment', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  assert.deepEqual(recordProblems(record), []);
  const keys = record.lines.find(line => line.id === 'env-keys').envKeys;
  assert.deepEqual([...keys].sort(), [...allowFor(undefined)].sort());
  assert.ok(!JSON.stringify(record).includes('WSL_INTEROP') && !JSON.stringify(record).includes('SECRET_TOKEN'));
});

test('The_decided_policy_holds_on_the_record_and_the_others_are_judged_against_the_same_observations', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  const decide = policy => verdictFor(record, { policy, decided: '2026-11-02', by: 'team lead' });
  assert.deepEqual(decide('cgroup-view-own+gchl-90+node-old-space-90'), { ok: true, halt: null, problems: [] }, 'D55: .NET believes floor(0.9 L), Node believes L with a V8 heap in [0.9 L, L), in their own cgroup view');
  assert.equal(decide('cgroup-view-own+gchl-limit').ok, false, 'the 100 % policy of the first decision no longer holds: .NET believes 90 %');
  assert.equal(decide('runtime-view-accepted').ok, true, 'verdictFor judges an OLD record by its own policy');
  assert.equal(decide('dotnet-gc-hard-limit-percent:75').ok, false, 'no GC hard limit shows in AppContext: the percent form is not what is set');
  for (const other of ['runtime-view-accepted', 'cgroup-view-readonly', 'cgroup-view-own+gchl-limit']) {
    const finished = finish({ found: record.lines, memoryView: { policy: { policy: other, decided: '2026-11-02', by: 'x' }, observations: record.memoryView.observations }, now: new Date('2026-11-02T00:00:00Z'), identity: record.layerIdentity });
    assert.deepEqual([finished.exit.code, finished.exit.halt], [3, 'memory-view-mismatch'], `the probe itself refuses ${other}: it asserts the pinned policy only`);
  }
  assert.equal(verdictFor(record, { policy: null, decided: null, by: null }).halt, 'memory-policy-undecided');
  const committed = readPolicy(path.join(spikeDir, 'inputs', 'specs.d', 'memory-policy.json'));
  assert.equal(verdictFor(record, committed.value).ok, true, 'the committed policy holds: the probe exits 0');
});

test('A_host_where_memory_peak_cannot_be_read_is_exit_3_memory_not_certifiable', async t => {
  if (!needs(t)) return;
  const scope = cleanupScope();
  t.after(() => scope.close());
  const unreadable = await runDelegatedScopeLine({ owned, scope, dirs: toolchains, deps: { readPeak: () => null } });
  assert.equal(unreadable.ok, false);
  assert.equal(unreadable.halt, 'memory-not-certifiable');
  const real = await runDelegatedScopeLine({ owned, scope, dirs: toolchains });
  assert.equal(real.ok, true, JSON.stringify(real));
  assert.equal(real.memoryMax, 256 * 1024 * 1024);
  assert.equal(real.swapMax, 0);
  assert.ok(real.peakBytes > 0, 'memory.peak is read after the workload\'s exit and is above zero');
});

test('The_build_box_of_the_probe_carries_neither_per_case_heap_key', () => {
  // T-L14 / §4.3: a GC hard limit sized for a case would break `dotnet build`; the build step's environment carries neither key, and the check is made where the box is made.
  const env = buildBoxEnv({ dirs: toolchains });
  assert.deepEqual(caseKeysIn(env), []);
  assert.equal('DOTNET_GCHeapHardLimit' in env, false);
  assert.equal(env.NODE_OPTIONS, '', 'the base NODE_OPTIONS, empty');
  assert.equal(env.NUGET_PACKAGES, '/tmp/pk', 'the build\'s own extras are there');
});

const FOUND_OK = [{ id: 'netns', ok: true }];
const OBSERVED = { limits: [] };
const finishedWith = (policy, runner = { version: '9.9.9', manifestSha256: 'ab' }) => finish({ found: FOUND_OK, memoryView: { policy, observations: OBSERVED }, now: new Date('2026-11-02T00:00:00Z'), identity: 'f'.repeat(64), runner });

test('The_record_names_the_runner_and_v8_heap_is_asserted_exactly_when_the_pinned_policy_asserts_it_and_probedOn_is_stamped_only_on_exit_0', () => {
  const decided = { policy: 'cgroup-view-own+gchl-90+node-old-space-90', decided: '2026-11-02', by: 'x' };
  const withNode = { limits: [{ memoryMaxBytes: 256 * 1024 * 1024, node: { heapSizeLimitBytes: 247463936 } }] };
  const rec = policy => finish({ found: FOUND_OK, memoryView: { policy, observations: withNode }, now: new Date('2026-11-02T00:00:00Z'), identity: 'f'.repeat(64), runner: { version: '9.9.9', manifestSha256: 'ab' } });
  assert.deepEqual(rec(decided).runner, { version: '9.9.9', manifestSha256: 'ab' });
  assert.equal(rec(decided).memoryView.v8Heap[0].asserted, true, 'the pinned policy asserts the V8 range');
  assert.equal(rec({ policy: null, decided: null, by: null }).memoryView.v8Heap[0].asserted, false, 'an undecided policy asserts nothing');
  assert.equal(rec({ policy: 'runtime-view-accepted', decided: '2026-11-02', by: 'x' }).memoryView.v8Heap[0].asserted, false, 'a policy that does not assert it');
  assert.equal(shouldStampProbedOn({ exit: { code: 0, halt: null }, lines: FOUND_OK }), true);
  assert.equal(shouldStampProbedOn({ exit: { code: 3, halt: 'memory-view-mismatch' }, lines: FOUND_OK }), false, 'every line passed but the memory verdict failed: not stamped');
  assert.equal(shouldStampProbedOn({ exit: { code: 0, halt: null }, lines: [{ id: 'x', ok: false }] }), false);
  assert.ok(finishedWith({ policy: null }).runner);
});

test('A_probe_whose_layer_tree_is_not_the_pinned_identity_exits_3_layer_identity_mismatch_before_probing_and_writes_no_record', t => {
  const dir = owned.ownedDir(t, 'staleprobe');
  const stale = path.join(dir, 'exec-layer.json');
  writeFileSync(stale, JSON.stringify({ execLayer: { treeSha256: '0'.repeat(64), files: [] } }));
  const out = path.join(dir, 'record.json');
  const ran = spawnSync(process.execPath, [PROBE, '--out', out, '--pinned-identity', stale, '--no-specs'], { encoding: 'utf8', timeout: 30_000 });
  assert.equal(ran.status, 3, ran.stderr);
  assert.match(ran.stderr, /layer-identity-mismatch/);
  assert.equal(existsSync(out), false, 'no record, and no placeholder left');
  assert.equal(ran.stdout, '');
});
