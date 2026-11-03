// E1.S8b brief B, T-L43 (parent D51; coai risk 19ae7208): through the FINAL prefix — the delegated scope, the box, the runner — at MemoryMax 256 MiB and 512 MiB, each stack runs an
// allocation-churn program (bounded live data, 4 GiB allocated over its run), the medium workloads and the object-graph regression (live objects at 70 % of L) that must PASS under D55 (both stacks capped
// at 90 % of L), and over-limit programs that must end `memory limit` with `memory.peak` and `memory.events` kept in the result and their peak/L recorded (J2). Node `medium-buffers` at 256 MiB and the
// `mixed` workload are RECORDED, not asserted (T21). What each runtime believes its limit is is recorded by the probe and asserted by policy (exec-probe-memory.test.mjs). Where a stack fails under its
// settings, that is a finding: it is reported, never tuned around.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { isRatioFinding, peakRatio, workloadAsserted } from '../lib/exec-probe/memory.mjs';
import { believed, runMode } from '../lib/exec-probe/observe.mjs';
import { buildCsharp, STACKS } from '../lib/exec-probe/programs.mjs';
import { needs, toolchains } from './support/exec-layer-host.mjs';
import { owned } from './support/owned.mjs';

const MIB = 1024 * 1024;
const LIMITS = Object.freeze([256, 512]);

async function bank(t) {
  const scope = cleanupScope();
  t.after(() => scope.close());
  const built = await buildCsharp({ owned, scope, dirs: toolchains });
  assert.ok(built.ok, built.reason);
  return { scope, csharpBuild: built.value };
}

/** The cells that must PASS under D55: Node `medium-buffers` at 256 MiB and `mixed` are recorded only (T21: typed arrays live outside V8's heap, so the flag does not see them and the kernel does). */
const asserted = (stack, mode, limit) => workloadAsserted(stack, mode, limit);

test('Allocation_churn_the_medium_workloads_and_the_object_graph_regression_pass_in_both_stacks_at_256_and_512_MiB_under_D55', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  const seen = [];
  for (const limit of LIMITS) {
    for (const stack of STACKS) {
      for (const mode of ['churn', 'medium-buffers', 'medium-objects', 'objects-70']) {
        const ran = await runMode({ owned, scope, dirs: toolchains, csharpBuild, stack, mode, memoryMaxBytes: limit * MIB });
        seen.push(`${stack} ${mode} at ${limit} MiB: ${ran.status} ${ran.reason} peak ${ran.memory.peakBytes}`);
        if (!asserted(stack, mode, limit)) continue;
        assert.equal(ran.status, 'PASS', `${stack} ${mode} at ${limit} MiB did not complete: ${ran.reason} (${seen.join('; ')})`);
        assert.equal(ran.memory.oomKill, 0);
        assert.equal(ran.memory.runtimeAbort, null);
        assert.ok(ran.memory.peakBytes <= limit * MIB, 'the peak is within the limit');
      }
    }
  }
});

test('The_mixed_workload_is_run_and_recorded_in_both_stacks_and_never_asserted_T21', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  for (const limit of LIMITS) {
    for (const stack of STACKS) {
      const ran = await runMode({ owned, scope, dirs: toolchains, csharpBuild, stack, mode: 'mixed', memoryMaxBytes: limit * MIB });
      t.diagnostic(`mixed ${stack} at ${limit} MiB: ${ran.status} ${ran.reason} peak ${ran.memory.peakBytes} (${peakRatio(ran.memory.peakBytes, limit * MIB)} of L)`);
      assert.ok(['PASS', 'FAIL'].includes(ran.status), `${stack} at ${limit} MiB: the workload ran and ended with a status (${ran.status}), whichever it is`);
      assert.equal(ran.memory.known, true, 'its memory was read');
      assert.equal(workloadAsserted(stack, 'mixed', limit), false, 'and it is never asserted');
    }
  }
});

test('An_over_limit_program_is_memory_limit_with_its_evidence_kept_and_its_peak_over_L_recorded', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  for (const limit of LIMITS) {
    for (const stack of STACKS) {
      for (const mode of ['overlimit', 'overlimit-objects']) {
        const ran = await runMode({ owned, scope, dirs: toolchains, csharpBuild, stack, mode, memoryMaxBytes: limit * MIB });
        const where = `${stack} ${mode} at ${limit} MiB`;
        assert.equal(ran.reason, 'memory limit', where);
        assert.equal(ran.memory.known, true, `${where}: memory.peak and memory.events were both read`);
        assert.ok(ran.memory.peakBytes > 0 && ran.memory.peakBytes <= limit * MIB, `${where}: memory.peak kept (${ran.memory.peakBytes})`);
        assert.ok(ran.memory.events !== undefined, `${where}: memory.events kept`);
        t.diagnostic(`peak/L ${where}: ${peakRatio(ran.memory.peakBytes, limit * MIB)}${isRatioFinding(ran.memory.peakBytes, limit * MIB) ? ' FINDING: within 10 points of 50 %' : ''}`);
        if (stack === 'csharp') assert.equal(ran.memory.runtimeAbort, 'dotnet', `${where}: .NET ends in its own abort (exit 134, "Out of memory."), judged memory limit by the runner rule (change 5.1)`);
        else assert.ok(ran.memory.oomKill >= 1 || ran.memory.runtimeAbort === 'v8', `${where}: Node ends in the kernel's oom_kill or V8's own abort`);
      }
    }
  }
});

test('Each_stack_believes_the_ninety_percent_settings_in_its_own_cgroup_view_at_256_and_512_MiB', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  for (const limit of LIMITS) {
    const dotnet = await believed({ owned, scope, dirs: toolchains, csharpBuild, stack: 'csharp', memoryMaxBytes: limit * MIB });
    const node = await believed({ owned, scope, dirs: toolchains, csharpBuild, stack: 'node', memoryMaxBytes: limit * MIB });
    assert.equal(dotnet.hostMemoryMax, limit * MIB);
    assert.equal(dotnet.value.totalAvailableBytes, Math.floor(0.9 * limit * MIB), `.NET TotalAvailableMemoryBytes at ${limit} MiB is floor(0.9 L): the hard limit of D55, not L and not 75 %`);
    assert.equal(node.value.constrainedBytes, limit * MIB, `Node constrainedMemory() at ${limit} MiB is L: the view, not the flag`);
    assert.ok(node.value.heapSizeLimitBytes >= Math.floor(0.9 * limit * MIB) && node.value.heapSizeLimitBytes < limit * MIB, `V8 heap_size_limit at ${limit} MiB is in [0.9 L, L): ${node.value.heapSizeLimitBytes}`);
    for (const seen of [dotnet.value, node.value]) assert.deepEqual([seen.cgroupVisible, seen.cgroupSelf, seen.cgroupChildren], [true, '0::/', 0], 'only the program\'s own cgroup');
  }
});
