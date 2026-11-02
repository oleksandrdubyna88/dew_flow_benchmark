#!/usr/bin/env node
// OP-K for Node: MEASUREMENT ONLY (fix batch 1, O4; the team lead decides on these numbers; the decided policy and the box are NOT changed). `objects-70` is an ordinary object graph of about 70 % of the limit
// (measured by the heap after a full GC), kept to the end, plus 1 GiB of short-lived small objects, run 3x at 256 and 512 MiB through the real layer and the runner:
//   (i)  the DECIDED policy as the layer's own box now is: .NET with `DOTNET_GCHeapHardLimit` = the limit, Node with the own-cgroup view and no flag;
//   (ii) the same, plus `NODE_OPTIONS=--max-old-space-size=<limit in MiB>` for Node only (an experiment env, set in the prefix as the OP-K scripts did).
// Also recorded: what each runtime believes (V8's `heap_size_limit`, Node's `constrainedMemory()`, .NET's `TotalAvailableMemoryBytes`) under each. Appended to the opk record under `followUp3`.
//   node spikes/lang-bench/opk-experiment-node.mjs [--runs 3] [--record <file>]
import { readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { storeDirs } from './lib/preflight-box.mjs';
import { toolchainsOf } from './lib/exec-probe/toolchains.mjs';
import { believesUnder, POLICY_DECIDED_RUN, POLICY_NODE_FLAG, runOnce, summarise } from './lib/exec-probe/opk.mjs';
import { buildCsharp } from './lib/exec-probe/programs.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const RUNS = Number(option('--runs', '3'));
const LIMITS = [256, 512];
const say = text => process.stderr.write(`opk-experiment-node: ${text}\n`);
const record = option('--record', path.join(spike, 'inputs', `opk-experiment-${new Date().toISOString().slice(0, 10)}.json`));

const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const helper = await import(url.href);
const owned = helper.ownedFor({ token: 'bench' });
const scope = helper.cleanupScope();
const dirs = toolchainsOf(spike, storeDirs, os.homedir());
const matrix = LIMITS.flatMap(limitMiB => [
  { policy: POLICY_DECIDED_RUN, stack: 'csharp', mode: 'objects-70', limitMiB },
  { policy: POLICY_DECIDED_RUN, stack: 'node', mode: 'objects-70', limitMiB },
  { policy: POLICY_NODE_FLAG, stack: 'node', mode: 'objects-70', limitMiB },
]);
try {
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) throw new Error(built.reason);
  const common = { owned, scope, dirs, csharpBuild: built.value };
  const believes = {};
  for (const limitMiB of LIMITS) {
    believes[limitMiB] = {
      dotnetDecided: await believesUnder({ ...common, limitMiB, policy: POLICY_DECIDED_RUN, stack: 'csharp' }),
      nodeDecided: await believesUnder({ ...common, limitMiB, policy: POLICY_DECIDED_RUN, stack: 'node' }),
      nodeWithFlag: await believesUnder({ ...common, limitMiB, policy: POLICY_NODE_FLAG, stack: 'node' }),
    };
  }
  const runs = [];
  for (const cell of matrix) {
    for (let n = 1; n <= RUNS; n += 1) {
      const run = await runOnce({ ...common, ...cell });
      runs.push({ ...run, n });
      say(`${cell.policy} ${cell.mode} ${cell.stack} ${cell.limitMiB} MiB run ${n}: ${run.result} (${run.ended}), peak ${run.peakBytes === null ? '?' : Math.round(run.peakBytes / 1048576)} MiB`);
    }
  }
  const whole = JSON.parse(readFileSync(record, 'utf8'));
  whole.followUp3 = { note: 'objects-70: live ordinary objects at 70 % of the limit + 1 GiB short-lived; (i) decided policy (the layer\'s box); (ii) plus NODE_OPTIONS=--max-old-space-size=<limit MiB> for Node. Measurement only.', believes, table: summarise(runs), runs };
  writeFileSync(record, `${JSON.stringify(whole, null, 2)}\n`);
  for (const row of whole.followUp3.table) process.stdout.write(`${row.mode.padEnd(11)} ${row.stack.padEnd(6)} ${String(row.limitMiB).padStart(3)} MiB ${row.policy.padEnd(28)} ${row.results.join(' / ')}  [${[...new Set(row.ended)].join(' | ')}]  peak MiB ${row.peakMiB.join('/')}\n`);
  for (const limit of LIMITS) process.stdout.write(`${limit} MiB: ${JSON.stringify(believes[limit])}\n`);
  process.stdout.write(`opk-experiment-node: appended to ${record}\n`);
} finally {
  await scope.close();
}
