#!/usr/bin/env node
// The OP-K experiment (E1.S8b; the team lead's, 2026-11-02). Both stacks' toy programs run with REALISTIC live data — typed arrays at 45 % of the limit, an ordinary object graph at 40 % — plus 2 GiB
// of short-lived allocations, at MemoryMax 256 and 512 MiB, 3 runs each, through the real layer and the runner, under (1) today's box (no cgroup visible) and (3) the same box with a read-only view of the
// program's OWN cgroup (`--unshare-cgroup` and a read-only bind of `prog/` at /sys/fs/cgroup). Before the (3) runs it verifies what the runtimes believe and that no sibling or host path is visible;
// if they do not pick the limit up, it stops on (3). The box module and the policy file are NOT touched: (3) is a variant of the layer's own prefix built in lib/exec-probe/opk.mjs.
//
//   node spikes/lang-bench/opk-experiment.mjs [--runs 3] [--out <file>]
//
// The record goes to inputs/opk-experiment-<date>.json. Exit 0 the experiment ran; 3 the (3) view could not be verified (the (1) rows are still recorded); 2 usage or the host cannot start it.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { storeDirs } from './lib/preflight-box.mjs';
import { toolchainsOf } from './lib/exec-probe/toolchains.mjs';
import { hostFacts } from './lib/exec-probe/probe.mjs';
import { POLICIES, runOnce, summarise, verifyCgroupView } from './lib/exec-probe/opk.mjs';
import { buildCsharp, STACKS } from './lib/exec-probe/programs.mjs';

const spike = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const RUNS = Number(option('--runs', '3'));
const MODES = ['medium-buffers', 'medium-objects'];
const LIMITS = [256, 512];
const say = text => process.stderr.write(`opk-experiment: ${text}\n`);

const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const owned = (await import(url.href)).ownedFor({ token: 'bench' });
const cleanupScope = (await import(url.href)).cleanupScope;
const dirs = toolchainsOf(spike, storeDirs, os.homedir());
const scope = cleanupScope();
let code = 0;
try {
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) throw new Error(built.reason);
  const common = { owned, scope, dirs, csharpBuild: built.value };
  const verification = {};
  for (const limitMiB of LIMITS) verification[limitMiB] = await verifyCgroupView({ ...common, limitMiB });
  const viewOk = LIMITS.every(limit => verification[limit].ok);
  if (!viewOk) say(`the (3) view could not be verified: ${LIMITS.flatMap(l => verification[l].problems).join('; ')} — stopping on (3)`);
  const runs = [];
  for (const policy of POLICIES.filter(name => name === 'today' || viewOk)) {
    for (const mode of MODES) for (const stack of STACKS) for (const limitMiB of LIMITS) {
      for (let n = 1; n <= RUNS; n += 1) {
        const run = await runOnce({ ...common, stack, mode, limitMiB, policy });
        runs.push({ ...run, n });
        say(`${policy} ${mode} ${stack} ${limitMiB} MiB run ${n}: ${run.result} (${run.ended}), peak ${run.peakBytes === null ? '?' : Math.round(run.peakBytes / 1048576)} MiB`);
      }
    }
  }
  const date = new Date().toISOString().slice(0, 10);
  const record = {
    date, host: hostFacts(), layerIdentity: JSON.parse(readFileSync(path.join(spike, 'inputs', 'specs.d', 'exec-layer.json'), 'utf8')).execLayer.treeSha256,
    note: 'toys only; realistic live data; 3 policies of the OP-K discussion reduced to (1) today\'s box and (3) a read-only cgroup view of the program\'s own cgroup; the box module and the policy file were not changed',
    verification, table: summarise(runs), runs,
  };
  const out = option('--out', path.join(spike, 'inputs', `opk-experiment-${date}.json`));
  mkdirSync(path.dirname(out), { recursive: true });
  writeFileSync(out, `${JSON.stringify(record, null, 2)}\n`);
  for (const row of record.table) process.stdout.write(`${row.mode.padEnd(15)} ${row.stack.padEnd(6)} ${String(row.limitMiB).padStart(3)} MiB ${row.policy.padEnd(16)} ${row.results.join(' / ')}  [${row.ended.join(' | ')}]  peak MiB ${row.peakMiB.join('/')}\n`);
  process.stdout.write(`opk-experiment: record written to ${out}\n`);
  code = viewOk ? 0 : 3;
} finally {
  await scope.close();
}
process.exit(code);
