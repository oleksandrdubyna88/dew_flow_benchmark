#!/usr/bin/env node
// OP-K, the consultants' last checks (MEASUREMENT ONLY; the default box and the policy file are unchanged), appended to the OP-K record under `followUp5`.
//   A = the own-cgroup view (the layer's box) + .NET DOTNET_GCHeapHardLimit = the limit + Node NODE_OPTIONS=--max-old-space-size=<limit in MiB> (an experiment env, set in the prefix).
//   C = the view and NOTHING else: each runtime's own container defaults (no .NET hard limit, no Node flag).
// `mixed` = an object graph at 45 % of the limit + typed/byte arrays at 35 % (both kept to the end) + 1 GiB of short-lived allocations of both kinds. Under A and C, `mixed` and `objects-70`, both stacks, 256 and
// 512 MiB, 3 runs each; if `mixed` fails under A in either stack, A is run again at 90 % and at 85 % (.NET's hard limit in bytes and Node's MiB at that percentage of the limit). What .NET and V8 believe is recorded for each setting.
//   node spikes/lang-bench/opk-experiment-ac.mjs [--runs 3] [--record <file>]
import { readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { storeDirs } from './lib/preflight-box.mjs';
import { toolchainsOf } from './lib/exec-probe/toolchains.mjs';
import { believesUnder, POLICY_A, POLICY_C, runOnce, summarise } from './lib/exec-probe/opk.mjs';
import { buildCsharp } from './lib/exec-probe/programs.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const RUNS = Number(option('--runs', '3'));
const LIMITS = [256, 512];
const STACKS = ['csharp', 'node'];
const MODES = ['mixed', 'objects-70'];
const say = text => process.stderr.write(`opk-experiment-ac: ${text}\n`);
const record = option('--record', path.join(spike, 'inputs', `opk-experiment-${new Date().toISOString().slice(0, 10)}.json`));

const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const helper = await import(url.href);
const owned = helper.ownedFor({ token: 'bench' });
const scope = helper.cleanupScope();
const dirs = toolchainsOf(spike, storeDirs, os.homedir());

async function runPolicy(common, policy) {
  const runs = [];
  for (const stack of STACKS) for (const mode of MODES) for (const limitMiB of LIMITS) {
    for (let n = 1; n <= RUNS; n += 1) {
      const run = await runOnce({ ...common, policy, stack, mode, limitMiB });
      runs.push({ ...run, n });
      say(`${policy} ${mode} ${stack} ${limitMiB} MiB run ${n}: ${run.result} (${run.ended}), peak ${run.peakBytes === null ? '?' : Math.round(run.peakBytes / 1048576)} MiB`);
    }
  }
  return runs;
}

async function believes(common, policy) {
  const seen = {};
  for (const limitMiB of LIMITS) seen[limitMiB] = { dotnet: await believesUnder({ ...common, limitMiB, policy, stack: 'csharp' }), node: await believesUnder({ ...common, limitMiB, policy, stack: 'node' }) };
  return seen;
}

try {
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) throw new Error(built.reason);
  const common = { owned, scope, dirs, csharpBuild: built.value };
  const settings = {};
  for (const policy of [POLICY_A, POLICY_C]) settings[policy] = { believes: await believes(common, policy), runs: await runPolicy(common, policy) };
  const mixedFailsUnderA = settings[POLICY_A].runs.some(run => run.mode === 'mixed' && run.result !== 'PASS');
  say(`mixed under A ${mixedFailsUnderA ? 'FAILS in at least one stack: running A@90 and A@85' : 'passes in both stacks: A@90 and A@85 are not needed'}`);
  if (mixedFailsUnderA) for (const policy of ['A@90', 'A@85']) settings[policy] = { believes: await believes(common, policy), runs: await runPolicy(common, policy) };
  const whole = JSON.parse(readFileSync(record, 'utf8'));
  whole.followUp5 = { note: 'A = view + .NET hard limit = limit + NODE_OPTIONS --max-old-space-size=<limit MiB>; C = view only; mixed = objects 45 % + typed arrays 35 % + 1 GiB short-lived of both kinds; A@90 / A@85 at that percentage of the limit. Measurement only.', mixedFailsUnderA, settings: Object.fromEntries(Object.entries(settings).map(([policy, one]) => [policy, { believes: one.believes, table: summarise(one.runs), runs: one.runs }])) };
  writeFileSync(record, `${JSON.stringify(whole, null, 2)}\n`);
  for (const [policy, one] of Object.entries(settings)) {
    for (const row of summarise(one.runs)) process.stdout.write(`${policy.padEnd(5)} ${row.mode.padEnd(11)} ${row.stack.padEnd(6)} ${String(row.limitMiB).padStart(3)} MiB ${row.results.join(' / ')}  [${[...new Set(row.ended)].join(' | ')}]  peak MiB ${row.peakMiB.join('/')}\n`);
    for (const limit of LIMITS) process.stdout.write(`${policy} ${limit} MiB believes: .NET TotalAvailable ${one.believes[limit].dotnet?.totalAvailableBytes}; Node constrained ${one.believes[limit].node?.constrainedBytes}, V8 heap_size_limit ${one.believes[limit].node?.heapSizeLimitBytes}\n`);
  }
  process.stdout.write(`opk-experiment-ac: appended to ${record}\n`);
} finally {
  await scope.close();
}
