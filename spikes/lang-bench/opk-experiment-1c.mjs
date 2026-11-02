#!/usr/bin/env node
// OP-K, variant (1c): MEASUREMENT ONLY (the team lead's; the layer's default box and the policy file are NOT changed). NO cgroup view in the box (the layer's prefix without `--unshare-cgroup` and without the
// /sys/fs/cgroup bind, taken out in lib/exec-probe/opk.mjs), PLUS `DOTNET_GCHeapHardLimit` = the case's limit for .NET (the layer's own .NET environment), PLUS the runner as it is (rule 5.1 with its evidence check),
// Node with no flag. 5 modes x 2 stacks x 2 limits x 3 runs through the real layer and the runner; what .NET and V8 believe under (1c) is recorded too. Appended to the OP-K record under `followUp4`.
//   node spikes/lang-bench/opk-experiment-1c.mjs [--runs 3] [--record <file>]
import { readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { storeDirs } from './lib/preflight-box.mjs';
import { toolchainsOf } from './lib/exec-probe/toolchains.mjs';
import { believesUnder, POLICY_1C, runOnce, summarise } from './lib/exec-probe/opk.mjs';
import { buildCsharp } from './lib/exec-probe/programs.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const RUNS = Number(option('--runs', '3'));
const LIMITS = [256, 512];
const MODES = ['medium-buffers', 'medium-objects', 'objects-70', 'near-limit', 'overlimit'];
const say = text => process.stderr.write(`opk-experiment-1c: ${text}\n`);
const record = option('--record', path.join(spike, 'inputs', `opk-experiment-${new Date().toISOString().slice(0, 10)}.json`));

const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const helper = await import(url.href);
const owned = helper.ownedFor({ token: 'bench' });
const scope = helper.cleanupScope();
const dirs = toolchainsOf(spike, storeDirs, os.homedir());
try {
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) throw new Error(built.reason);
  const common = { owned, scope, dirs, csharpBuild: built.value };
  const believes = {};
  for (const limitMiB of LIMITS) {
    believes[limitMiB] = { dotnet: await believesUnder({ ...common, limitMiB, policy: POLICY_1C, stack: 'csharp' }), node: await believesUnder({ ...common, limitMiB, policy: POLICY_1C, stack: 'node' }) };
  }
  const runs = [];
  for (const stack of ['csharp', 'node']) {
    for (const mode of MODES) {
      for (const limitMiB of LIMITS) {
        for (let n = 1; n <= RUNS; n += 1) {
          const run = await runOnce({ ...common, policy: POLICY_1C, stack, mode, limitMiB });
          runs.push({ ...run, n });
          say(`${mode} ${stack} ${limitMiB} MiB run ${n}: ${run.result} (${run.ended}), peak ${run.peakBytes === null ? '?' : Math.round(run.peakBytes / 1048576)} MiB`);
        }
      }
    }
  }
  const whole = JSON.parse(readFileSync(record, 'utf8'));
  whole.followUp4 = { note: '(1c): no cgroup view + DOTNET_GCHeapHardLimit = the limit for .NET + the runner with rule 5.1 and its evidence check; Node no flag. Measurement only.', believes, table: summarise(runs), runs };
  writeFileSync(record, `${JSON.stringify(whole, null, 2)}\n`);
  for (const row of whole.followUp4.table) process.stdout.write(`${row.mode.padEnd(15)} ${row.stack.padEnd(6)} ${String(row.limitMiB).padStart(3)} MiB ${row.results.join(' / ')}  [${[...new Set(row.ended)].join(' | ')}]  peak MiB ${row.peakMiB.join('/')}\n`);
  for (const limit of LIMITS) process.stdout.write(`(1c) ${limit} MiB: ${JSON.stringify(believes[limit])}\n`);
  process.stdout.write(`opk-experiment-1c: appended to ${record}\n`);
} finally {
  await scope.close();
}
