// E1.S8b brief B: `exec-probe.mjs` — the host probe, run THROUGH the layer (the real prefix, the delegated scope, the v2 box). It records every line of the plan's probe list, then exits 3
// naming the first line that failed; the memory view is asserted by policy (OP-K, undecided while `memory-policy.json` says null: exit 3 `memory-policy-undecided`, never 0).
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { before, test } from 'node:test';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { ALLOW_LIST } from '../lib/exec-layer/env.mjs';
import { recordProblems } from '../lib/exec-probe/facts.mjs';
import { runDelegatedScopeLine } from '../lib/exec-probe/lines.mjs';
import { verdictFor } from '../lib/exec-probe/probe.mjs';
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
  for (const id of ['netns', 'program-in-prog', 'ping-over-loopback', 'bind-list', 'home-sentinel', 'mnt-c', 'cmd-exe', 'user-bus', 'empty-program-launch', 'bash-c-true-stderr', 'env-keys', 'delegated-scope', 'culture-env', 'locale-a', 'dotnet-culture', 'node-culture', 'invariant-switch']) assert.ok(ids.includes(id), `the record has the line ${id}`);
  assert.deepEqual(record.lines.filter(line => !line.ok).map(line => line.id), [], 'every line other than the memory policy passed on this host');
});

test('The_record_names_the_date_the_host_the_layer_identity_and_the_believed_limits_at_both_limits', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  assert.match(record.date, /^\d{4}-\d{2}-\d{2}$/);
  assert.ok(record.host.hostname && record.host.kernel && record.host.cgroup2 === true);
  assert.equal(record.layerIdentity, JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'specs.d', 'exec-layer.json'), 'utf8')).execLayer.treeSha256);
  assert.deepEqual(record.memoryView.observations.limits.map(at => at.memoryMaxBytes), [256 * 1024 * 1024, 512 * 1024 * 1024]);
  for (const at of record.memoryView.observations.limits) {
    assert.equal(at.hostMemoryMax, at.memoryMaxBytes, 'the host\'s prog/memory.max is the limit asked for');
    assert.equal(typeof at.csharp.totalAvailableBytes, 'number');
    assert.equal(typeof at.node.heapSizeLimitBytes, 'number');
    assert.equal(typeof at.csharp.cgroupVisible, 'boolean');
  }
});

test('The_probe_record_holds_only_allow_listed_keys_even_with_a_host_key_planted_in_the_probes_own_environment', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  assert.deepEqual(recordProblems(record), []);
  const keys = record.lines.find(line => line.id === 'env-keys').envKeys;
  assert.deepEqual([...keys].sort(), [...ALLOW_LIST].sort());
  assert.ok(!JSON.stringify(record).includes('WSL_INTEROP') && !JSON.stringify(record).includes('SECRET_TOKEN'));
});

test('A_policy_is_asserted_against_the_record_a_decided_one_that_holds_is_exit_0_and_one_that_does_not_is_exit_3', async t => {
  if (!needs(t)) return;
  const { record } = await probed;
  const decide = policy => verdictFor(record, { policy, decided: '2026-11-02', by: 'team lead' });
  assert.equal(decide('runtime-view-accepted').ok, true, 'both stacks behave: the churn passes and the over-limit program is memory limit');
  const exposed = decide('cgroup-view-readonly');
  assert.equal(exposed.ok, false);
  assert.equal(exposed.halt, 'memory-view-mismatch');
  assert.match(exposed.problems.join('\n'), /no cgroup is visible in the box/);
  assert.equal(decide('dotnet-gc-hard-limit-percent:75').ok, false, 'no hard limit is set in the box');
  assert.equal(verdictFor(record, { policy: null, decided: null, by: null }).halt, 'memory-policy-undecided');
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
