// E1.S8b brief B, T-L43 (parent D51; coai risk 19ae7208): through the FINAL prefix — the delegated scope, the box, the runner — at MemoryMax 256 MiB and 512 MiB, each stack runs an
// allocation-churn program (bounded live data, 4 GiB allocated over its run) that must PASS, and an over-limit program that must end `memory limit` with `memory.peak` and `memory.events`
// kept in the result. What each runtime believes its limit is, is recorded by the probe and asserted by policy (exec-probe-memory.test.mjs). Where a stack fails the churn program under
// its own view, that is the finding OP-K exists for: it is reported, never tuned around.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
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

/**
 * The cells that must PASS under the decided policy. Node `medium-buffers` at 256 MiB is not among them and not pinned either way: typed arrays live outside V8's heap, so V8's heap limit does not see
 * them and the kernel's OOM kill does, under every policy (the OP-K experiment, `inputs/opk-experiment-2026-11-02.json`; module_lang_bench.md). It is a property of the runtime, not a fixture to tune.
 */
const passes = (stack, mode, limit) => !(stack === 'node' && mode === 'medium-buffers' && limit === 256);

test('Allocation_churn_with_bounded_live_data_completes_in_both_stacks_at_256_and_512_MiB', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  const seen = [];
  for (const limit of LIMITS) {
    for (const stack of STACKS) {
      for (const mode of ['churn', 'medium-buffers', 'medium-objects']) {
        if (!passes(stack, mode, limit)) continue;
        const ran = await runMode({ owned, scope, dirs: toolchains, csharpBuild, stack, mode, memoryMaxBytes: limit * MIB });
        seen.push(`${stack} ${mode} at ${limit} MiB: ${ran.status} ${ran.reason} peak ${ran.memory.peakBytes}`);
        assert.equal(ran.status, 'PASS', `${stack} ${mode} at ${limit} MiB did not complete: ${ran.reason} (${seen.join('; ')})`);
        assert.equal(ran.memory.oomKill, 0);
        assert.equal(ran.memory.runtimeAbort, null);
        assert.ok(ran.memory.peakBytes <= limit * MIB, 'the peak is within the limit');
      }
    }
  }
});

test('An_over_limit_program_is_memory_limit_with_its_evidence_kept', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  for (const limit of LIMITS) {
    for (const stack of STACKS) {
      const ran = await runMode({ owned, scope, dirs: toolchains, csharpBuild, stack, mode: 'overlimit', memoryMaxBytes: limit * MIB });
      assert.equal(ran.reason, 'memory limit', `${stack} at ${limit} MiB`);
      assert.equal(ran.memory.known, true, 'memory.peak and memory.events were both read');
      assert.ok(ran.memory.peakBytes > 0 && ran.memory.peakBytes <= limit * MIB, `${stack}: memory.peak kept (${ran.memory.peakBytes})`);
      assert.ok(ran.memory.events !== undefined, `${stack}: memory.events kept`);
      if (stack === 'csharp') assert.equal(ran.memory.runtimeAbort, 'dotnet', '.NET ends in its own abort (exit 134, "Out of memory."), judged memory limit by the runner rule (change 5.1)');
      else assert.ok(ran.memory.oomKill >= 1, 'Node ends in the kernel\'s oom_kill');
    }
  }
});

test('Each_stack_believes_prog_memory_max_in_its_own_cgroup_view_at_256_and_512_MiB', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  for (const limit of LIMITS) {
    const dotnet = await believed({ owned, scope, dirs: toolchains, csharpBuild, stack: 'csharp', memoryMaxBytes: limit * MIB });
    const node = await believed({ owned, scope, dirs: toolchains, csharpBuild, stack: 'node', memoryMaxBytes: limit * MIB });
    assert.equal(dotnet.hostMemoryMax, limit * MIB);
    assert.equal(dotnet.value.totalAvailableBytes, limit * MIB, `.NET TotalAvailableMemoryBytes at ${limit} MiB (the hard limit is the limit, not 75 % of it, and not the host's memory)`);
    assert.equal(node.value.constrainedBytes, limit * MIB, `Node constrainedMemory() at ${limit} MiB`);
    for (const seen of [dotnet.value, node.value]) assert.deepEqual([seen.cgroupVisible, seen.cgroupSelf, seen.cgroupChildren], [true, '0::/', 0], 'only the program\'s own cgroup');
  }
});
