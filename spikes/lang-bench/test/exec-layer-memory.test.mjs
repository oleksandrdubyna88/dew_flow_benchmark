// E1.S8b brief B, T-L43 (parent D51; coai risk 19ae7208): through the FINAL prefix — the delegated scope, the box, the runner — at MemoryMax 256 MiB and 512 MiB, each stack runs an
// allocation-churn program (bounded live data, 4 GiB allocated over its run) that must PASS, and an over-limit program that must end `memory limit` with `memory.peak` and `memory.events`
// kept in the result. What each runtime believes its limit is, is recorded by the probe and asserted by policy (exec-probe-memory.test.mjs). Where a stack fails the churn program under
// its own view, that is the finding OP-K exists for: it is reported, never tuned around.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { runMode } from '../lib/exec-probe/observe.mjs';
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

test('Allocation_churn_with_bounded_live_data_completes_in_both_stacks_at_256_and_512_MiB', async t => {
  if (!needs(t)) return;
  const { scope, csharpBuild } = await bank(t);
  const seen = [];
  for (const limit of LIMITS) {
    for (const stack of STACKS) {
      const ran = await runMode({ owned, scope, dirs: toolchains, csharpBuild, stack, mode: 'churn', memoryMaxBytes: limit * MIB });
      seen.push(`${stack} at ${limit} MiB: ${ran.status} ${ran.reason} peak ${ran.memory.peakBytes} oom_kill ${ran.memory.oomKill}`);
      assert.equal(ran.status, 'PASS', `${stack} at ${limit} MiB did not complete: ${ran.reason} — the finding OP-K exists for (${seen.join('; ')})`);
      assert.equal(ran.memory.oomKill, 0);
      assert.ok(ran.memory.peakBytes < limit * MIB, 'the peak is under the limit');
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
      assert.ok(ran.memory.events.oom_kill >= 1 && ran.memory.oomKill >= 1, `${stack}: memory.events kept with oom_kill`);
    }
  }
});
