// E1.S8b brief B: the memory view, ASSERTED (T-L43; coai risk 19ae7208; parent D51). OP-K is the team lead's to decide on the probe's record: `inputs/specs.d/memory-policy.json` says which
// policy holds, or `null` while it is undecided. The judgement is pure: observations in, `{ ok, halt, problems }` out.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { assertMemoryView, exitCodeOf, POLICY_NAMES, readPolicy, validatePolicy } from '../lib/exec-probe/memory.mjs';
import { owned } from './support/owned.mjs';
import { spikeDir } from './support/exec-layer-host.mjs';

const MIB = 1024 * 1024;
/** What the probe saw at one limit, on this host (2026-11-02): .NET sized to the host, Node unconstrained, no cgroup in the box; both stacks behave. */
const seen = (limit, over = {}) => ({
  memoryMaxBytes: limit * MIB, hostMemoryMax: limit * MIB,
  csharp: { totalAvailableBytes: 46_288_662_528, heapHardLimit: '', cgroupVisible: false },
  node: { constrainedBytes: 0, heapSizeLimitBytes: 4_395_630_592, cgroupVisible: false },
  churn: { csharp: 'PASS', node: 'PASS' },
  overLimit: { csharp: { reason: 'memory limit', oomKill: 1, peakBytes: limit * MIB, eventsKept: true }, node: { reason: 'memory limit', oomKill: 1, peakBytes: limit * MIB, eventsKept: true } },
  ...over,
});
const both = over => ({ limits: [seen(256, over), seen(512, over)] });

/** The refuted shape: a probe that records what each runtime believes and exits 0 whatever it saw. */
const printOnly = () => 0;

test('A_memory_probe_that_only_prints_would_let_the_stacks_meet_different_bounds_and_that_is_the_point', () => {
  const observations = both();
  assert.equal(printOnly(observations), 0, 'the refuted probe exits 0 although .NET believes 46 GB, Node 4.4 GB and the cgroup says 256 MiB');
  const verdict = assertMemoryView({ policy: 'cgroup-view-readonly', observations });
  assert.equal(verdict.ok, false);
  assert.equal(exitCodeOf(verdict), 3, 'a mismatch is exit 3, never 0');
  assert.equal(verdict.halt, 'memory-view-mismatch');
  assert.ok(verdict.problems.some(p => /csharp/.test(p) && /256 MiB/.test(p)), verdict.problems.join('\n'));
  assert.ok(verdict.problems.some(p => /node/.test(p)), 'both stacks are named');
  assert.ok(verdict.problems.some(p => /cgroup/.test(p)), 'a cgroup that is not visible in the box is a mismatch for a policy that exposes one');
});

test('With_no_policy_decided_the_probe_halts_memory_policy_undecided_and_never_exits_0', () => {
  const verdict = assertMemoryView({ policy: null, observations: both() });
  assert.equal(verdict.ok, false);
  assert.equal(verdict.halt, 'memory-policy-undecided');
  assert.equal(exitCodeOf(verdict), 3);
  assert.equal(exitCodeOf(assertMemoryView({ policy: null, observations: both({ csharp: { totalAvailableBytes: 256 * MIB, heapHardLimit: '', cgroupVisible: true } }) })), 3, 'not even when the numbers agree');
});

test('The_runtime_view_accepted_policy_asserts_the_behaviour_not_the_numbers', () => {
  assert.deepEqual(assertMemoryView({ policy: 'runtime-view-accepted', observations: both() }), { ok: true, halt: null, problems: [] });
  const churnFails = assertMemoryView({ policy: 'runtime-view-accepted', observations: both({ churn: { csharp: 'FAIL', node: 'PASS' } }) });
  assert.equal(churnFails.ok, false);
  assert.match(churnFails.problems.join('\n'), /csharp.*churn/s, 'a stack that meets the kernel\'s OOM kill in a churn program is named');
  const noEvidence = assertMemoryView({ policy: 'runtime-view-accepted', observations: both({ overLimit: { csharp: { reason: 'exit status 134', oomKill: 0, peakBytes: 1, eventsKept: true }, node: { reason: 'memory limit', oomKill: 1, peakBytes: 1, eventsKept: false } } }) });
  assert.equal(noEvidence.ok, false);
  assert.equal(noEvidence.problems.length >= 2, true, 'an over-limit program that is not memory limit, and one whose evidence was not kept, are both named');
});

test('The_dotnet_gc_hard_limit_percent_policy_needs_a_hard_limit_inside_prog_memory_max', () => {
  const no = assertMemoryView({ policy: 'dotnet-gc-hard-limit-percent:75', observations: both() });
  assert.equal(no.ok, false, 'no hard limit is set in the box: .NET still meets the kernel with the host\'s size');
  const yes = assertMemoryView({ policy: 'dotnet-gc-hard-limit-percent:75', observations: both({ csharp: { totalAvailableBytes: 46_288_662_528, heapHardLimit: String(192 * MIB), cgroupVisible: false } }) });
  assert.deepEqual(yes, { ok: true, halt: null, problems: [] });
});

test('The_cgroup_view_readonly_policy_needs_every_believed_limit_to_equal_prog_memory_max', () => {
  const exposed = { csharp: { totalAvailableBytes: 256 * MIB, heapHardLimit: '', cgroupVisible: true }, node: { constrainedBytes: 256 * MIB, heapSizeLimitBytes: 256 * MIB, cgroupVisible: true } };
  const verdict = assertMemoryView({ policy: 'cgroup-view-readonly', observations: { limits: [seen(256, exposed), seen(512, { ...exposed, csharp: { ...exposed.csharp, totalAvailableBytes: 256 * MIB } })] } });
  assert.equal(verdict.ok, false, 'at 512 MiB .NET still believes 256 MiB');
  assert.match(verdict.problems.join('\n'), /512 MiB/);
  assert.equal(assertMemoryView({ policy: 'cgroup-view-readonly', observations: { limits: [seen(256, exposed)] } }).ok, true);
});

test('A_memory_max_that_is_not_the_one_asked_for_is_a_mismatch_under_every_policy', () => {
  const verdict = assertMemoryView({ policy: 'runtime-view-accepted', observations: { limits: [seen(256, { hostMemoryMax: 100 * MIB })] } });
  assert.equal(verdict.ok, false);
  assert.match(verdict.problems.join('\n'), /memory\.max/);
});

test('A_policy_is_one_of_the_three_names_or_null_and_an_unknown_one_names_the_legal_values', () => {
  assert.deepEqual([...POLICY_NAMES], ['runtime-view-accepted', 'dotnet-gc-hard-limit-percent:<n>', 'cgroup-view-readonly']);
  for (const policy of [null, 'runtime-view-accepted', 'cgroup-view-readonly', 'dotnet-gc-hard-limit-percent:75']) assert.equal(validatePolicy({ policy, decided: '2026-11-02', by: 'team lead' }).ok, true, String(policy));
  for (const policy of ['dotnet-gc-hard-limit-percent:0', 'dotnet-gc-hard-limit-percent:101', 'dotnet-gc-hard-limit-percent:', 'whatever', '']) {
    const verdict = validatePolicy({ policy, decided: '2026-11-02', by: 'x' });
    assert.equal(verdict.ok, false, JSON.stringify(policy));
    assert.match(verdict.reason, /runtime-view-accepted.*cgroup-view-readonly/s);
  }
  assert.equal(validatePolicy({ policy: 'cgroup-view-readonly' }).ok, false, 'a decided policy says when and by whom');
});

test('The_committed_policy_file_is_valid_and_while_undecided_says_null', () => {
  const read = readPolicy(path.join(spikeDir, 'inputs', 'specs.d', 'memory-policy.json'));
  assert.ok(read.ok, read.reason);
  assert.ok(read.value.policy === null || typeof read.value.policy === 'string');
});

test('A_policy_file_that_is_missing_or_malformed_is_a_failure_and_not_undecided', t => {
  const dir = owned.ownedDir(t, 'policy');
  assert.equal(readPolicy(path.join(dir, 'absent.json')).ok, false);
  writeFileSync(path.join(dir, 'bad.json'), '{ not json');
  assert.equal(readPolicy(path.join(dir, 'bad.json')).ok, false);
  writeFileSync(path.join(dir, 'ok.json'), JSON.stringify({ policy: null, decided: null, by: null }));
  assert.equal(readPolicy(path.join(dir, 'ok.json')).ok, true);
  assert.ok(readFileSync(path.join(dir, 'ok.json'), 'utf8').includes('null'));
});
