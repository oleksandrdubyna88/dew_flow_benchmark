#!/usr/bin/env node
// The OP-K experiment, round 3 (E1.S8b; the team lead's, 2026-11-02), appended to the same record under `followUp2`. Experiment only: the box module and the policy file are not touched.
//   (3c) = view (3) plus DOTNET_GCHeapHardLimit=<the case's limit in bytes, hexadecimal> in the .NET environment only.
//   .NET under (3c): medium-buffers, medium-objects, near-limit (live 80 % of the limit + 1 GiB short-lived) and over-limit, at 256 and 512 MiB, with TotalAvailableMemoryBytes.
//   Node under (3): near-limit.   .NET under (1), today's box: near-limit, for comparison.
// 3 runs each, through the real layer and the runner.   node spikes/lang-bench/opk-experiment-round3.mjs [--runs 3] [--record <file>]
import { readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { storeDirs } from './lib/preflight-box.mjs';
import { toolchainsOf } from './lib/exec-probe/toolchains.mjs';
import { dotnetBelieves, extraEnvFor, POLICY_3C, runOnce, summarise } from './lib/exec-probe/opk.mjs';
import { buildCsharp } from './lib/exec-probe/programs.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const RUNS = Number(option('--runs', '3'));
const LIMITS = [256, 512];
const say = text => process.stderr.write(`opk-experiment-round3: ${text}\n`);
const record = option('--record', path.join(spike, 'inputs', `opk-experiment-${new Date().toISOString().slice(0, 10)}.json`));

const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const helper = await import(url.href);
const owned = helper.ownedFor({ token: 'bench' });
const scope = helper.cleanupScope();
const dirs = toolchainsOf(spike, storeDirs, os.homedir());
const cell = (policy, stack, modes) => modes.flatMap(mode => LIMITS.map(limitMiB => ({ policy, mode, stack, limitMiB })));
const matrix = [
  ...cell(POLICY_3C, 'csharp', ['medium-buffers', 'medium-objects', 'near-limit', 'overlimit']),
  ...cell('cgroup-view-own', 'node', ['near-limit']),
  ...cell('today', 'csharp', ['near-limit']),
];
try {
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) throw new Error(built.reason);
  const common = { owned, scope, dirs, csharpBuild: built.value };
  const believed3c = {};
  for (const limitMiB of LIMITS) believed3c[limitMiB] = await dotnetBelieves({ ...common, limitMiB, policy: POLICY_3C, extraEnv: extraEnvFor(POLICY_3C, 'csharp', limitMiB) });
  const runs = [];
  for (const one of matrix) {
    for (let n = 1; n <= RUNS; n += 1) {
      const run = await runOnce({ ...common, ...one });
      runs.push({ ...run, n });
      say(`${one.policy} ${one.mode} ${one.stack} ${one.limitMiB} MiB run ${n}: ${run.result} (${run.ended}), peak ${run.peakBytes === null ? '?' : Math.round(run.peakBytes / 1048576)} MiB`);
    }
  }
  const whole = JSON.parse(readFileSync(record, 'utf8'));
  whole.followUp2 = { note: '(3c) = view (3) + DOTNET_GCHeapHardLimit=<limit bytes, hex>, .NET only; near-limit = live 80 % + 1 GiB short-lived. Box module and policy file unchanged.', believed3c, table: summarise(runs), runs };
  writeFileSync(record, `${JSON.stringify(whole, null, 2)}\n`);
  for (const row of whole.followUp2.table) process.stdout.write(`${row.mode.padEnd(15)} ${row.stack.padEnd(6)} ${String(row.limitMiB).padStart(3)} MiB ${row.policy.padEnd(40)} ${row.results.join(' / ')}  [${[...new Set(row.ended)].join(' | ')}]  peak MiB ${row.peakMiB.join('/')}\n`);
  for (const limit of LIMITS) process.stdout.write(`(3c) .NET at ${limit} MiB: TotalAvailableMemoryBytes ${believed3c[limit]?.totalAvailableBytes}\n`);
  process.stdout.write(`opk-experiment-round3: appended to ${record}\n`);
} finally {
  await scope.close();
}
