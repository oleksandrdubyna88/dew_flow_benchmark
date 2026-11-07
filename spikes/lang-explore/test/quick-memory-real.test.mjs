// The one memory value on the certifying host (section 2.4, M7): a real box whose user scope can read `memory.peak`. The probe's invented over-limit programs (`lang-bench/lib/exec-probe/programs.mjs`, both stacks, both modes) run as ONE pass
// through the lang-explore pass path (`runPass` of score-quick.mjs) with the limit from the one registered file; the runner reads the enforced limit from `prog/memory.max`, the verdict is `memory limit`, its cause is recorded, the memory
// claim is certified and the run's cleanup is clean. No fake home: the pinned toolchains are found under the real one. Behind `needs`: skipped with the reason, a failure under BENCH_REQUIRE_SANDBOX=1.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { buildCsharp, programTree } from '../../lang-bench/lib/exec-probe/programs.mjs';
import { needs, toolchains } from '../../lang-bench/test/support/exec-layer-host.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { memoryLimit } from '../lib/memory-limit.mjs';
import { layerRun, runPass } from '../lib/score-quick.mjs';

const MIB = 1024 * 1024;
const WALL_MS = 70_000;
const CEILING_MS = 200_000;

test('On_the_certifying_host_a_case_scope_reads_memory_max_536870912_and_an_invented_workload_over_the_limit_is_stopped_and_classified_memory_limit_with_its_cause_in_both_stacks', { timeout: 900_000 }, async t => {
  if (!needs(t)) return;
  const owned = ownedFor({ token: 'bench' });
  const scope = owned.cleanupScope();
  t.after(() => scope.close());
  const limit = memoryLimit().value;
  const built = await buildCsharp({ owned, scope, dirs: toolchains });
  assert.ok(built.ok, built.reason);
  for (const stack of ['csharp', 'node']) {
    for (const mode of ['overlimit', 'overlimit-objects']) {
      const samples = tmpDir('quick-memory-real');
      mkdirSync(samples, { recursive: true });
      writeFileSync(path.join(samples, 'p1.in'), 'x\n');
      writeFileSync(path.join(samples, 'p1.out'), 'OK\n');
      const tree = programTree({ owned, scope, stack, mode: `${mode} ${limit.mib}`, csharpBuild: built.value });
      const limits = { memoryBytes: limit.bytes };
      const limitsFile = path.join(samples, '..', `limits-${stack}-${mode}.json`);
      writeFileSync(limitsFile, JSON.stringify({ timeS: 60 })); // the toy programs are olympiad cases: the runner's own time limit, as the probe gives it
      const runFn = layerRun({ owned, dirs: toolchains, stack, kind: undefined, limits, caseWallMs: WALL_MS, runCeilingMs: CEILING_MS, runOptions: { limitsFile } });
      const ran = await runPass({ a: { stack, limits, profile: {}, runFn }, copy: tree, cases: { dir: samples, ids: ['p1'] }, owned, dirs: toolchains });
      const where = `${stack} ${mode}`;
      assert.equal(ran.ok, true, `${where}: ${ran.reason}`);
      const kase = ran.value.cases[0];
      const memory = ran.value.record.cases[0].lifecycle.memory;
      assert.equal(kase.memoryMaxBytes, 536870912, `${where}: the runner read the limit from prog/memory.max`);
      assert.equal(kase.verdict, 'memory limit', `${where}: the runner said "${kase.reason}" (memory ${JSON.stringify(memory)})`);
      const cause = memory.oomKill >= 1 || (['dotnet', 'v8'].includes(memory.runtimeAbort) && memory.peakBytes * 2 >= memory.maxBytes);
      assert.ok(cause, `${where}: the cause is recorded (oomKill ${memory.oomKill}, runtimeAbort ${memory.runtimeAbort}, peak ${memory.peakBytes / MIB} MiB)`);
      assert.equal(ran.value.ran.memoryCertified, true, where);
      assert.equal(ran.value.ran.cleanup.clean, true, JSON.stringify(ran.value.ran.cleanup.left));
    }
  }
});
