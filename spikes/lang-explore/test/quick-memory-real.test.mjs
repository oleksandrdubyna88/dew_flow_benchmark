// The one memory value on the certifying host (section 2.4, M7): a real box whose user scope can read `memory.peak`. Invented DAEMON programs on the real daemon templates of both stacks (`quick-real-programs.mjs`: the C# one built by
// the template's `build.sh` in the build box) are asked to allocate until the limit stops them, as one pass through the lang-explore pass path (`runPass` of score-quick.mjs, kind `daemon`, the scenario grammar of the runner) with the limit
// from the one registered file. The runner reads the enforced limit from `prog/memory.max`, the verdict is `memory limit`, its cause is recorded (a kernel OOM kill, or the runtime's own abort with the peak at half the limit or more), the
// memory claim is certified and the run's cleanup is clean. No fake home: the pinned toolchains are found under the real one. Behind `needs`: skipped with the reason, a failure under BENCH_REQUIRE_SANDBOX=1.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { needs, toolchains } from '../../lang-bench/test/support/exec-layer-host.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { boxBuild } from '../lib/build-quick.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { memoryLimit } from '../lib/memory-limit.mjs';
import { quickSnapshots } from '../lib/quick.mjs';
import { layerRun, runPass } from '../lib/score-quick.mjs';
import { boomScenario, boomTranscript, layDaemonTree } from './quick-real-programs.mjs';

const MIB = 1024 * 1024;
const WALL_MS = 120_000;
const CEILING_MS = 300_000;

/** The tree of `stack`, built when the stack has a build (C#: the template's `build.sh` in the real build box). */
async function builtTree({ stack, owned }) {
  const tree = tmpDir(`quick-memory-real-${stack}`);
  layDaemonTree({ stack, dest: tree });
  if (stack === 'csharp') {
    const snapshots = quickSnapshots({ base: loadExplore().value, home: os.homedir() });
    assert.equal(snapshots.ok, true, snapshots.reason);
    const built = await boxBuild({ owned, dirs: toolchains, snapshots: snapshots.value, stack })({ tree, timeoutMs: 600_000 });
    assert.equal(built.ok && built.value.ok, true, built.reason ?? built.value.tail);
  }
  return tree;
}

function casesFor(word) {
  const samples = tmpDir('quick-memory-cases');
  mkdirSync(samples, { recursive: true });
  writeFileSync(path.join(samples, 'p1.in'), boomScenario(word));
  writeFileSync(path.join(samples, 'p1.out'), boomTranscript);
  return samples;
}

test('On_the_certifying_host_a_case_scope_reads_memory_max_536870912_and_an_invented_workload_over_the_limit_is_stopped_and_classified_memory_limit_with_its_cause_in_both_stacks', { timeout: 1_200_000 }, async t => {
  if (!needs(t)) return;
  const owned = ownedFor({ token: 'bench' });
  const limit = memoryLimit().value;
  for (const stack of ['csharp', 'node']) {
    const tree = await builtTree({ stack, owned });
    for (const word of ['boom', 'boomobj']) {
      const limits = { memoryBytes: limit.bytes };
      const runFn = layerRun({ owned, dirs: toolchains, stack, kind: 'daemon', limits, caseWallMs: WALL_MS, runCeilingMs: CEILING_MS });
      const ran = await runPass({ a: { stack, limits, profile: { kind: 'daemon' }, runFn }, copy: tree, cases: { dir: casesFor(word), ids: ['p1'] }, owned, dirs: toolchains });
      const where = `${stack} ${word} (daemon path)`;
      assert.equal(ran.ok, true, `${where}: ${ran.reason}`);
      const kase = ran.value.cases[0];
      const memory = ran.value.record.cases[0].lifecycle.memory;
      assert.equal(ran.value.record.kind, 'daemon', `${where}: the runner ran the daemon kind, not the olympiad one`);
      assert.equal(kase.memoryMaxBytes, 536870912, `${where}: the runner read the limit from prog/memory.max`);
      assert.equal(kase.verdict, 'memory limit', `${where}: the runner said "${kase.reason}" (memory ${JSON.stringify(memory)})`);
      const cause = memory.oomKill >= 1 || (['dotnet', 'v8'].includes(memory.runtimeAbort) && memory.peakBytes * 2 >= memory.maxBytes);
      assert.ok(cause, `${where}: the cause is recorded (oomKill ${memory.oomKill}, runtimeAbort ${memory.runtimeAbort}, peak ${memory.peakBytes / MIB} MiB)`);
      assert.equal(ran.value.ran.memoryCertified, true, where);
      assert.equal(ran.value.ran.cleanup.clean, true, JSON.stringify(ran.value.ran.cleanup.left));
    }
  }
});
