// E1.S8b brief B: the memory view, ASSERTED (T-L43; coai risk 19ae7208; parent D51). OP-K is the team lead's to decide on the probe's record: `inputs/specs.d/memory-policy.json` says which
// policy holds, or `null` while it is undecided. The judgement is pure: observations in, `{ ok, halt, problems }` out.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { assertMemoryView, exitCodeOf, LIMITS_MIB, probeVerdict, isRatioFinding, overLimitRows, peakRatio, POLICY_DECIDED, POLICY_NAMES, POLICY_SUPERSEDED, readPolicy, validatePolicy, workloadAsserted, workloadRows } from '../lib/exec-probe/memory.mjs';
import { owned } from './support/owned.mjs';
import { spikeDir } from './support/exec-layer-host.mjs';

const MIB = 1024 * 1024;
const WORKLOADS = ['medium-buffers', 'medium-objects', 'objects-70', 'mixed'];
const workload = (status, limit, peak = 0.7) => ({ status, reason: status === 'PASS' ? '' : 'memory limit', oomKill: status === 'PASS' ? 0 : 1, runtimeAbort: null, peakBytes: Math.floor(peak * limit * MIB) });
/** What the probe saw at one limit on this host BEFORE OP-K was decided (2026-11-02): .NET sized to the host, Node unconstrained, no cgroup in the box; both stacks behave. */
const seen = (limit, over = {}) => ({
  memoryMaxBytes: limit * MIB, hostMemoryMax: limit * MIB,
  csharp: { totalAvailableBytes: 46_288_662_528, heapHardLimit: '', cgroupVisible: false, cgroupSelf: '', cgroupChildren: null },
  node: { constrainedBytes: 0, heapSizeLimitBytes: 4_395_630_592, cgroupVisible: false, cgroupSelf: '', cgroupChildren: null },
  churn: { csharp: 'PASS', node: 'PASS' },
  workloads: { csharp: Object.fromEntries(WORKLOADS.map(mode => [mode, workload('PASS', limit)])), node: Object.fromEntries(WORKLOADS.map(mode => [mode, workload('PASS', limit)])) },
  overLimit: { csharp: { reason: 'memory limit', oomKill: 1, peakBytes: limit * MIB, eventsKept: true }, node: { reason: 'memory limit', oomKill: 1, peakBytes: limit * MIB, eventsKept: true } },
  overLimitObjects: { csharp: { reason: 'memory limit', oomKill: 1, peakBytes: limit * MIB, eventsKept: true }, node: { reason: 'memory limit', oomKill: 1, peakBytes: limit * MIB, eventsKept: true } },
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
  assert.deepEqual([...POLICY_NAMES], ['runtime-view-accepted', 'dotnet-gc-hard-limit-percent:<n>', 'cgroup-view-readonly', 'cgroup-view-own+gchl-limit', 'cgroup-view-own+gchl-90+node-old-space-90']);
  assert.equal(POLICY_SUPERSEDED, 'cgroup-view-own+gchl-limit', 'the 100 % name of the first decision is kept: its evidence records name it');
  for (const policy of [null, 'runtime-view-accepted', 'cgroup-view-readonly', 'cgroup-view-own+gchl-limit', 'cgroup-view-own+gchl-90+node-old-space-90', 'dotnet-gc-hard-limit-percent:75']) assert.equal(validatePolicy({ policy, decided: '2026-11-02', by: 'team lead' }).ok, true, String(policy));
  for (const policy of ['dotnet-gc-hard-limit-percent:0', 'dotnet-gc-hard-limit-percent:101', 'dotnet-gc-hard-limit-percent:', 'whatever', '']) {
    const verdict = validatePolicy({ policy, decided: '2026-11-02', by: 'x' });
    assert.equal(verdict.ok, false, JSON.stringify(policy));
    assert.match(verdict.reason, /runtime-view-accepted.*cgroup-view-readonly/s);
  }
  assert.equal(validatePolicy({ policy: 'cgroup-view-readonly' }).ok, false, 'a decided policy says when and by whom');
});

test('The_committed_policy_file_is_the_decision_of_D55_with_its_evidence', () => {
  const read = readPolicy(path.join(spikeDir, 'inputs', 'specs.d', 'memory-policy.json'));
  assert.ok(read.ok, read.reason);
  assert.equal(read.value.policy, POLICY_DECIDED);
  assert.equal(read.value.policy, 'cgroup-view-own+gchl-90+node-old-space-90');
  assert.equal(read.value.decided, '2026-11-02');
  assert.match(read.value.by, /team lead.*D55/);
  assert.deepEqual(read.value.evidence, ['inputs/exec-probe-2026-11-02.json', 'inputs/opk-experiment-2026-11-02.json']);
  for (const file of read.value.evidence) assert.ok(readFileSync(path.join(spikeDir, file), 'utf8').length > 0, `${file} exists`);
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

/** D55: the share of L the runtimes may use, ⌊0.9·L⌋. */
const ninety = limit => Math.floor(0.9 * limit * MIB);
/** What the probe sees under the decided policy (D55): each program in its own cgroup view, .NET's hard limit 90 % of L (so `TotalAvailableMemoryBytes` is ⌊0.9·L⌋), Node's `constrainedMemory()` L and V8's heap 90 % of L plus the young space. */
const viewed = (limit, over = {}) => seen(limit, {
  csharp: { totalAvailableBytes: ninety(limit), heapHardLimit: '', cgroupVisible: true, cgroupSelf: '0::/', cgroupChildren: 0 },
  node: { constrainedBytes: limit * MIB, heapSizeLimitBytes: ninety(limit) + 5 * MIB, cgroupVisible: true, cgroupSelf: '0::/', cgroupChildren: 0 },
  ...over,
});
const decided = observations => assertMemoryView({ policy: POLICY_DECIDED, observations });
const cs = (total, over = {}) => ({ totalAvailableBytes: total, heapHardLimit: '', cgroupVisible: true, cgroupSelf: '0::/', cgroupChildren: 0, ...over });
const nd = (constrained, heap, over = {}) => ({ constrainedBytes: constrained, heapSizeLimitBytes: heap, cgroupVisible: true, cgroupSelf: '0::/', cgroupChildren: 0, ...over });

test('The_decided_policy_holds_when_dotnet_believes_ninety_percent_and_node_the_limit_in_their_own_cgroup_view', () => {
  assert.deepEqual(decided({ limits: [viewed(256), viewed(512)] }), { ok: true, halt: null, problems: [] });
  assert.equal(viewed(256).csharp.totalAvailableBytes, 241_591_910);
  assert.equal(viewed(512).csharp.totalAvailableBytes, 483_183_820);
});

test('The_decided_policy_names_a_dotnet_that_believes_the_limit_or_the_old_seventy_five_percent_and_not_ninety', () => {
  const full = decided({ limits: [viewed(256, { csharp: cs(256 * MIB) })] });
  assert.equal(full.ok, false, 'at 100 % the runtime has no room for its own memory (D55)');
  assert.match(full.problems.join('\n'), /csharp at 256 MiB: TotalAvailableMemoryBytes is 256 MiB, not 90 % of prog\/memory\.max \(241591910 bytes\)/);
  const capped = decided({ limits: [viewed(256, { csharp: cs(192 * MIB) })] });
  assert.equal(capped.ok, false, '.NET under the view alone caps itself at 75 %');
  assert.match(capped.problems.join('\n'), /csharp at 256 MiB: TotalAvailableMemoryBytes is 192 MiB/);
  assert.equal(decided({ limits: [seen(256)] }).ok, false, 'today\'s box (no view) is a mismatch: .NET believes the host\'s memory');
  assert.equal(decided({ limits: [viewed(256, { csharp: cs(241_591_911) })] }).ok, false, 'one byte over floor(0.9 L) is a mismatch: the check is exact');
  assert.equal(decided({ limits: [viewed(256, { csharp: cs(241_591_909) })] }).ok, false, 'and so is one byte under');
});

test('The_decided_policy_names_a_node_that_believes_another_limit_and_a_cgroup_that_is_not_only_its_own', () => {
  const node = decided({ limits: [viewed(512, { node: nd(0, 1) })] });
  assert.match(node.problems.join('\n'), /node at 512 MiB: constrainedMemory is 0 MiB/);
  assert.equal(decided({ limits: [viewed(512, { node: nd(ninety(512), ninety(512) + MIB) })] }).ok, false, 'Node\'s constrainedMemory is L, not 90 % of it: the flag is the heap\'s, the view is the limit');
  const sibling = decided({ limits: [viewed(256, { csharp: cs(ninety(256), { cgroupSelf: '0::/user.slice/x/prog', cgroupChildren: 2 }) })] });
  assert.match(sibling.problems.join('\n'), /not only its own/);
});

test('The_decided_policy_asserts_that_V8_heap_size_limit_is_in_ninety_percent_of_L_up_to_L_exclusive', () => {
  const at = (limit, heap) => decided({ limits: [viewed(limit, { node: nd(limit * MIB, heap) })] });
  assert.equal(at(256, ninety(256)).ok, true, 'exactly 90 % of L is in');
  assert.equal(at(256, 256 * MIB - 1).ok, true, 'one byte under L is in');
  const low = at(256, ninety(256) - 1);
  assert.equal(low.ok, false, 'one byte under 90 % is out');
  assert.match(low.problems.join('\n'), /node at 256 MiB: V8 heap_size_limit is 241591909 bytes, not in \[241591910, 268435456\)/);
  assert.equal(at(256, 256 * MIB).ok, false, 'L itself is out: the heap must leave room');
  const stale = at(512, 281_000_000);
  assert.equal(stale.ok, false, 'the view alone sized V8 to about 268 MiB whatever the cgroup (followUp3): 281 MB at 512 MiB is below 90 % of 512 MiB');
});

test('The_superseded_100_percent_policy_is_still_judged_by_its_own_rule_so_its_evidence_record_stays_readable', () => {
  const hundred = (limit, over = {}) => viewed(limit, { csharp: cs(limit * MIB), node: nd(limit * MIB, 262 * MIB), ...over });
  assert.deepEqual(assertMemoryView({ policy: POLICY_SUPERSEDED, observations: { limits: [hundred(256), hundred(512)] } }), { ok: true, halt: null, problems: [] });
  assert.equal(assertMemoryView({ policy: POLICY_SUPERSEDED, observations: { limits: [viewed(256)] } }).ok, false);
});

test('Under_the_decided_policy_the_behaviour_is_still_asserted_the_dotnet_abort_is_memory_limit', () => {
  const abortAsError = decided({ limits: [viewed(256, { overLimit: { csharp: { reason: 'exit status 134', oomKill: 0, peakBytes: 1, eventsKept: true }, node: { reason: 'memory limit', oomKill: 1, peakBytes: 1, eventsKept: true } } })] });
  assert.equal(abortAsError.ok, false, 'without the runner rule (change 5.1) .NET\'s abort is "exit status 134" and the policy is not met');
  const abortRule = decided({ limits: [viewed(256, { overLimit: { csharp: { reason: 'memory limit', oomKill: 0, runtimeAbort: 'dotnet', peakBytes: 197 * MIB, eventsKept: true }, node: { reason: 'memory limit', oomKill: 1, runtimeAbort: null, peakBytes: 256 * MIB, eventsKept: true } } })] });
  assert.equal(abortRule.ok, true, 'a runtime\'s own abort is memory limit with the evidence kept');
});

const withWorkload = (stack, mode, status, limit = 256) => ({ workloads: { ...seen(limit).workloads, [stack]: { ...seen(limit).workloads[stack], [mode]: workload(status, limit) } } });
const judged = (limit, over) => decided({ limits: [viewed(limit, over)] });

test('T_L43_asserts_the_object_graph_regression_and_the_medium_workloads_in_both_stacks_at_both_limits', () => {
  for (const mode of ['medium-buffers', 'medium-objects', 'objects-70']) {
    for (const stack of ['csharp', 'node']) {
      const verdict = judged(512, withWorkload(stack, mode, 'FAIL', 512));
      assert.equal(verdict.ok, false, `${stack} ${mode} at 512 MiB must pass`);
      assert.match(verdict.problems.join('\n'), new RegExp(`${stack} at 512 MiB: the ${mode} workload did not pass`));
    }
  }
  assert.equal(judged(256, withWorkload('csharp', 'medium-buffers', 'FAIL')).ok, false, '.NET medium-buffers at 256 MiB is asserted');
  assert.equal(judged(256, withWorkload('node', 'objects-70', 'FAIL')).ok, false, 'objects-70 in Node at 256 MiB is asserted: the object-graph regression');
  assert.equal(judged(256, { workloads: { csharp: {}, node: seen(256).workloads.node } }).ok, false, 'a workload that was not run cannot pass silently');
});

test('Node_medium_buffers_at_256_MiB_and_the_mixed_workload_are_recorded_and_never_asserted_T21', () => {
  assert.equal(judged(256, withWorkload('node', 'medium-buffers', 'FAIL', 256)).ok, true, 'Node typed arrays live outside the V8 heap: a runtime property (T21)');
  assert.equal(judged(512, withWorkload('node', 'medium-buffers', 'FAIL', 512)).ok, false, 'but at 512 MiB it is asserted');
  for (const stack of ['csharp', 'node']) for (const limit of [256, 512]) assert.equal(judged(limit, withWorkload(stack, 'mixed', 'FAIL', limit)).ok, true, `mixed in ${stack} at ${limit} MiB is recorded, not asserted`);
  assert.equal(workloadAsserted('node', 'medium-buffers', 256), false);
  assert.equal(workloadAsserted('node', 'medium-buffers', 512), true);
  assert.equal(workloadAsserted('csharp', 'mixed', 256), false);
  assert.equal(workloadAsserted('csharp', 'objects-70', 256), true);
  const rows = workloadRows({ limits: [viewed(256, withWorkload('node', 'mixed', 'FAIL')), viewed(512)] });
  const failed = rows.find(row => row.stack === 'node' && row.mode === 'mixed' && row.memoryMaxBytes === 256 * MIB);
  assert.deepEqual([failed.status, failed.asserted], ['FAIL', false], 'the failed mixed workload is in the rows, flagged not asserted');
  assert.equal(rows.length, 2 * 2 * 4 + 2 * 2, 'four workloads and the churn, per stack and limit');
});

test('The_peak_over_L_of_an_over_limit_fixture_is_recorded_and_a_ratio_within_10_points_of_half_is_a_finding_J2', () => {
  const L = 256 * MIB;
  assert.equal(peakRatio(L, L), 1);
  assert.equal(peakRatio(Math.floor(0.904 * L), L), 0.904);
  assert.equal(peakRatio(null, L), null, 'an unknown peak is not zero');
  assert.equal(isRatioFinding(Math.ceil(0.4 * L), L), true, '40 % is within 10 points of 50 %');
  assert.equal(isRatioFinding(Math.floor(0.6 * L), L), true, '60 % too');
  assert.equal(isRatioFinding(Math.ceil(0.4 * L) - 1, L), false, 'a byte under 40 %');
  assert.equal(isRatioFinding(Math.floor(0.6 * L) + 1, L), false, 'a byte over 60 %');
  assert.deepEqual([isRatioFinding(400, 1000), isRatioFinding(600, 1000), isRatioFinding(399, 1000), isRatioFinding(601, 1000)], [true, true, false, false], 'exactly 40 % and 60 % are in: the band is closed');
  assert.equal(isRatioFinding(L / 2, L), true);
  assert.equal(isRatioFinding(null, L), false);
  const overLimit = { csharp: { reason: 'memory limit', oomKill: 0, runtimeAbort: 'dotnet', peakBytes: Math.floor(0.52 * L), eventsKept: true }, node: { reason: 'memory limit', oomKill: 1, runtimeAbort: null, peakBytes: L, eventsKept: true } };
  const rows = overLimitRows({ limits: [viewed(256, { overLimit, overLimitObjects: overLimit })] });
  assert.deepEqual(rows.map(row => [row.stack, row.fixture, row.memoryMaxBytes, row.ratio, row.finding]), [
    ['csharp', 'overlimit', L, 0.52, true], ['node', 'overlimit', L, 1, false], ['csharp', 'overlimit-objects', L, 0.52, true], ['node', 'overlimit-objects', L, 1, false],
  ]);
  assert.equal(rows[0].reason, 'memory limit');
  assert.equal(rows[0].runtimeAbort, 'dotnet');
});

test('Both_over_limit_fixtures_are_asserted_memory_limit_with_their_evidence_in_both_stacks', () => {
  const abort = { reason: 'memory limit', oomKill: 0, runtimeAbort: 'v8', peakBytes: 250 * MIB, eventsKept: true };
  assert.equal(judged(256, { overLimitObjects: { csharp: abort, node: abort } }).ok, true, 'a runtime abort with its evidence is memory limit');
  const wrong = judged(256, { overLimitObjects: { csharp: { ...abort, reason: 'runtime error', runtimeAbort: null }, node: abort } });
  assert.equal(wrong.ok, false);
  assert.match(wrong.problems.join('\n'), /csharp at 256 MiB: the overlimit-objects program ended "runtime error"/);
  assert.match(judged(256, { overLimitObjects: { csharp: abort } }).problems.join('\n'), /overlimit-objects not observed for node at 256 MiB/);
  assert.match(judged(256, { overLimitObjects: { csharp: { ...abort, eventsKept: false }, node: abort } }).problems.join('\n'), /memory\.peak and memory\.events were not kept/);
});

test('The_probe_refuses_any_policy_but_the_pinned_one_exit_3_memory_view_mismatch_and_only_old_records_are_judged_by_the_others', () => {
  const observations = { limits: [viewed(256), viewed(512)] };
  assert.deepEqual(probeVerdict({ policy: POLICY_DECIDED, observations }), { ok: true, halt: null, problems: [] });
  for (const policy of ['runtime-view-accepted', 'cgroup-view-readonly', 'dotnet-gc-hard-limit-percent:75', POLICY_SUPERSEDED, 'whatever']) {
    const verdict = probeVerdict({ policy, observations });
    assert.equal(verdict.ok, false, policy);
    assert.equal(verdict.halt, 'memory-view-mismatch', policy);
    assert.equal(exitCodeOf(verdict), 3);
    assert.match(verdict.problems.join('\n'), /cgroup-view-own\+gchl-90\+node-old-space-90/, 'the pinned policy is named');
  }
  assert.equal(probeVerdict({ policy: null, observations }).halt, 'memory-policy-undecided', 'undecided keeps its own halt');
  assert.equal(assertMemoryView({ policy: 'runtime-view-accepted', observations }).ok, true, 'an old record can still be judged by its own policy');
});

// V7 (fix batch 3): a missing or partial observation is a NAMED PROBLEM (fail closed), never a thrown TypeError.
test('A_missing_or_partial_observation_is_a_named_problem_in_the_verdict_and_never_a_thrown_error', () => {
  const whole = viewed(256);
  const without = (...keys) => Object.fromEntries(Object.entries(whole).filter(([key]) => !keys.includes(key)));
  const cases = [
    [without('csharp'), /memory view not observed for csharp at 256 MiB/],
    [without('node'), /memory view not observed for node at 256 MiB/],
    [{ ...whole, csharp: { cgroupVisible: true } }, /memory view not observed for csharp at 256 MiB/],
    [{ ...whole, node: { constrainedBytes: 256 * MIB } }, /memory view not observed for node at 256 MiB/],
    [without('churn'), /churn not observed for csharp at 256 MiB/],
    [{ ...whole, churn: { csharp: 'PASS' } }, /churn not observed for node at 256 MiB/],
    [without('overLimit'), /overlimit not observed for csharp at 256 MiB/],
    [without('overLimitObjects'), /overlimit-objects not observed for csharp at 256 MiB/],
    [without('workloads'), /workloads not observed for csharp at 256 MiB/],
    [{ ...whole, workloads: { csharp: {}, node: whole.workloads.node } }, /medium-buffers not observed for csharp at 256 MiB/],
    [{ memoryMaxBytes: 256 * MIB }, /memory view not observed for csharp at 256 MiB/],
  ];
  for (const policy of [POLICY_DECIDED, POLICY_SUPERSEDED, 'cgroup-view-readonly', 'runtime-view-accepted', 'dotnet-gc-hard-limit-percent:75']) {
    for (const [at, expected] of cases) {
      let verdict;
      assert.doesNotThrow(() => { verdict = assertMemoryView({ policy, observations: { limits: [at] } }); }, `${policy}: ${expected}`);
      assert.equal(verdict.ok, false, `${policy}: fail closed`);
      assert.equal(verdict.halt, 'memory-view-mismatch');
      assert.match(verdict.problems.join('\n'), expected, policy);
    }
  }
  assert.deepEqual(assertMemoryView({ policy: POLICY_DECIDED, observations: { limits: [viewed(256), viewed(512)] } }), { ok: true, halt: null, problems: [] }, 'a whole observation is unchanged');
  assert.doesNotThrow(() => probeVerdict({ policy: POLICY_DECIDED, observations: { limits: [{}] } }), 'not even an empty observation');
});

// N3 (fix batch 4): the observed limits are EXACTLY the probe's two; an empty or one-limit record must not pass.
test('The_observed_limits_must_be_exactly_256_and_512_MiB_and_a_missing_limit_is_a_named_problem', () => {
  assert.deepEqual([...LIMITS_MIB], [256, 512]);
  const run = limits => probeVerdict({ policy: POLICY_DECIDED, observations: { limits } });
  assert.deepEqual(assertMemoryView({ policy: POLICY_DECIDED, observations: { limits: [viewed(256)] } }), { ok: true, halt: null, problems: [] }, 'assertMemoryView alone still judges the limits it is given (old records, one limit)');
  assert.deepEqual(assertMemoryView({ policy: POLICY_DECIDED, observations: { limits: [viewed(256)] }, limits: LIMITS_MIB }).ok, false, 'and refuses when it is told which limits to require');
  assert.deepEqual(run([viewed(256), viewed(512)]), { ok: true, halt: null, problems: [] });
  for (const [name, limits, expected] of [
    ['an empty record', [], /memory view not observed at 256 MiB.*memory view not observed at 512 MiB/s],
    ['one limit', [viewed(256)], /memory view not observed at 512 MiB/],
    ['the other limit', [viewed(512)], /memory view not observed at 256 MiB/],
    ['a wrong limit', [viewed(256), viewed(1024)], /memory view not observed at 512 MiB/],
    ['a duplicate', [viewed(256), viewed(256)], /memory view not observed at 512 MiB/],
  ]) {
    const verdict = run(limits);
    assert.equal(verdict.ok, false, name);
    assert.equal(verdict.halt, 'memory-view-mismatch', name);
    assert.match(verdict.problems.join('\n'), expected, name);
  }
  assert.equal(run([viewed(512), viewed(256)]).ok, true, 'the order does not matter');
});
