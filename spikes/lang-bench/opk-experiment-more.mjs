#!/usr/bin/env node
// HISTORICAL (brief C, 2026-11-02): a record of measurements made BEFORE the own-cgroup view became the layer's box (the "today" policy was the box then); not re-runnable as written.
// The OP-K experiment's follow-up (E1.S8b; the team lead's, 2026-11-02), appended to the same record (inputs/opk-experiment-<date>.json, key `followUp`). Experiment only: the box module and the
// policy file are not touched.
//   A. the over-limit fixture (3 GiB of growing live data) under (3), both stacks, 256 and 512 MiB: how does each run END, and what does the runner's verdict say?
//   B. (3b) = view (3) plus `DOTNET_GCHeapHardLimitPercent=0x64` in the .NET environment only: medium-buffers, medium-objects and over-limit, .NET only; and what .NET believes under (3b).
// 3 runs each, through the real layer and the runner.
//
//   node spikes/lang-bench/opk-experiment-more.mjs [--runs 3] [--record <file>]
import { readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { storeDirs } from './lib/preflight-box.mjs';
import { toolchainsOf } from './lib/exec-probe/toolchains.mjs';
import { dotnetBelieves, POLICY_3B, runOnce, summarise, verifyCgroupView } from './lib/exec-probe/opk.mjs';
import { buildCsharp } from './lib/exec-probe/programs.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const RUNS = Number(option('--runs', '3'));
const LIMITS = [256, 512];
const say = text => process.stderr.write(`opk-experiment-more: ${text}\n`);
const record = option('--record', path.join(spike, 'inputs', `opk-experiment-${new Date().toISOString().slice(0, 10)}.json`));

const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const helper = await import(url.href);
const owned = helper.ownedFor({ token: 'bench' });
const scope = helper.cleanupScope();
const dirs = toolchainsOf(spike, storeDirs, os.homedir());
const matrix = [
  ...['csharp', 'node'].flatMap(stack => LIMITS.map(limitMiB => ({ policy: 'cgroup-view-own', mode: 'overlimit', stack, limitMiB }))),
  ...['medium-buffers', 'medium-objects', 'overlimit'].flatMap(mode => LIMITS.map(limitMiB => ({ policy: POLICY_3B, mode, stack: 'csharp', limitMiB }))),
];
try {
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) throw new Error(built.reason);
  const common = { owned, scope, dirs, csharpBuild: built.value };
  const believed3b = {};
  for (const limitMiB of LIMITS) believed3b[limitMiB] = await verifyCgroupView({ ...common, limitMiB, policy: POLICY_3B, dotnetFraction: null });
  const percentProbe = {};
  for (const value of ['0x64', '0x63', '0x32']) percentProbe[value] = await dotnetBelieves({ ...common, limitMiB: 256, policy: POLICY_3B, extraEnv: { DOTNET_GCHeapHardLimitPercent: value } });
  percentProbe.hardLimit256MiB = await dotnetBelieves({ ...common, limitMiB: 256, policy: POLICY_3B, extraEnv: { DOTNET_GCHeapHardLimit: '0x10000000' } });
  const runs = [];
  for (const cell of matrix) {
    for (let n = 1; n <= RUNS; n += 1) {
      const run = await runOnce({ ...common, ...cell });
      runs.push({ ...run, n });
      say(`${cell.policy} ${cell.mode} ${cell.stack} ${cell.limitMiB} MiB run ${n}: ${run.result} (${run.ended}), peak ${run.peakBytes === null ? '?' : Math.round(run.peakBytes / 1048576)} MiB`);
    }
  }
  const whole = JSON.parse(readFileSync(record, 'utf8'));
  whole.followUp = { note: 'A: over-limit under (3); B: (3b) = view (3) + DOTNET_GCHeapHardLimitPercent=0x64, .NET only. Box module and policy file unchanged.', believed3b, percentProbeAt256MiB: percentProbe, table: summarise(runs), runs };
  writeFileSync(record, `${JSON.stringify(whole, null, 2)}\n`);
  for (const row of whole.followUp.table) process.stdout.write(`${row.mode.padEnd(15)} ${row.stack.padEnd(6)} ${String(row.limitMiB).padStart(3)} MiB ${row.policy.padEnd(24)} ${row.results.join(' / ')}  [${row.ended.join(' | ')}]  peak MiB ${row.peakMiB.join('/')}\n`);
  for (const limit of LIMITS) process.stdout.write(`(3b) .NET at ${limit} MiB believes TotalAvailableMemoryBytes ${believed3b[limit].dotnet?.totalAvailableBytes}, heapHardLimit "${believed3b[limit].dotnet?.heapHardLimit}"\n`);
  for (const [value, seen] of Object.entries(percentProbe)) process.stdout.write(`.NET at 256 MiB with ${value}: TotalAvailableMemoryBytes ${seen?.totalAvailableBytes}\n`);
  process.stdout.write(`opk-experiment-more: appended to ${record}\n`);
} finally {
  await scope.close();
}
