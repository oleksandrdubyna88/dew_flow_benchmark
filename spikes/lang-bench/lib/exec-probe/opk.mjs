// E1.S8b OP-K experiment (the team lead's, 2026-11-02): do the runtimes, left to their own view of memory, meet the kernel's OOM kill with realistic live data — and does a read-only cgroup
// view of the program's OWN cgroup change that? Two policies are run through the real layer and the runner, UNCHANGED: (1) today's box (no cgroup visible) and (3) the same box plus
// `--unshare-cgroup` (so /proc/self/cgroup shows `0::/`) and a read-only bind of `prog/` at /sys/fs/cgroup. (3) is a variant of the layer's own prefix built here; the box module and the
// policy file are not touched. Toys only, never a task of the series.
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { cleanup } from '../exec-layer/cleanup.mjs';
import { driverEnv } from '../exec-layer/env.mjs';
import { launchPrefix, openRun, programArgv, runnerArgv, stableRunsParent } from '../exec-layer/run.mjs';
import { progTemplate, userEnv } from '../exec-layer/scope.mjs';
import { probeTree, programTree } from './programs.mjs';

const MIB = 1024 * 1024;
const WALL_MS = 120_000;
const TIME_LIMIT = { timeS: 100 };
export const POLICIES = Object.freeze(['today', 'cgroup-view-own']);
/** (3b): view (3) plus a hard limit of 100 % (hexadecimal, as .NET reads it) in the .NET environment only. */
export const POLICY_3B = 'cgroup-view-own+gchl100';
const EXTRA_ENV = Object.freeze({ [POLICY_3B]: Object.freeze({ csharp: Object.freeze({ DOTNET_GCHeapHardLimitPercent: '0x64' }) }) });
/** (3c): view (3) plus `DOTNET_GCHeapHardLimit=<the case's limit in bytes, hexadecimal>` in the .NET environment only. */
export const POLICY_3C = 'cgroup-view-own+gchardlimit-case-limit';

/** Fix batch 1, O4 (measurement only): the layer's OWN box as decided (the view and .NET's hard limit are in it), and the same with `NODE_OPTIONS=--max-old-space-size=<limit MiB>` for Node only. */
export const POLICY_DECIDED_RUN = 'decided';
export const POLICY_NODE_FLAG = 'decided+node-max-old-space';

/** (1c), measurement only: NO cgroup view in the box (the layer's prefix without `--unshare-cgroup` and the bind of `prog/` at /sys/fs/cgroup), plus .NET's hard limit from the layer's own .NET environment, Node with no flag. */
export const POLICY_1C = 'no-view+dotnet-hardlimit';

/** The layer's prefix without its own-cgroup view; throws when it has none. */
export function withoutCgroupView(prefix) {
  const bind = prefix.findIndex((arg, i) => arg === '--ro-bind' && prefix[i + 2] === '/sys/fs/cgroup');
  if (bind < 0) throw new Error('the prefix has no cgroup view to take out');
  return prefix.filter((arg, i) => arg !== '--unshare-cgroup' && !(i >= bind && i < bind + 3));
}

/**
 * The consultants' two settings (measurement only; the default box and the policy file are unchanged). A = the own-cgroup view (the layer's box) + .NET `DOTNET_GCHeapHardLimit` = the limit + Node
 * `NODE_OPTIONS=--max-old-space-size=<limit in MiB>`; `A@90` and `A@85` set BOTH at that percentage of the limit (.NET in bytes, Node in MiB). C = the view and NOTHING else: each runtime's own container defaults.
 */
export const POLICY_A = 'A';
export const POLICY_C = 'C';
const A_PERCENT = /^A(?:@(\d{1,3}))?$/;
const percentOf = policy => { const found = A_PERCENT.exec(policy); return found === null ? null : Number(found[1] ?? 100); };

/** `NODE_OPTIONS` a policy gives a stack's program, or null: the decided+flag variant, or A at a percentage of the limit. */
export function nodeOptionsFor(policy, stack, limitMiB) {
  if (stack !== 'node') return null;
  if (policy === POLICY_NODE_FLAG) return `--max-old-space-size=${limitMiB}`;
  const percent = percentOf(policy);
  return percent === null ? null : `--max-old-space-size=${Math.floor((limitMiB * percent) / 100)}`;
}

/** The `DOTNET_GCHeapHardLimit` (hexadecimal bytes) A@<pct> sets for .NET when it is not 100 %, or null (at 100 % the layer's own .NET environment already holds the limit). */
export function dotnetHardLimitFor(policy, stack, limitMiB) {
  const percent = percentOf(policy);
  return stack === 'csharp' && percent !== null && percent !== 100 ? `0x${Math.floor((limitMiB * 1024 * 1024 * percent) / 100).toString(16)}` : null;
}

/** The `stack` option the layer is opened with: C is built as for no stack, so the box environment holds no `DOTNET_GCHeapHardLimit`. */
export const layerStackFor = (policy, stack) => (policy === POLICY_C ? undefined : stack);

/** The prefix with the value of `--setenv <key>` replaced; throws when the prefix has none. */
export function setEnvValue(prefix, key, value) {
  const at = prefix.findIndex((arg, i) => arg === '--setenv' && prefix[i + 1] === key);
  if (at < 0) throw new Error(`the prefix has no --setenv ${key} to replace`);
  return [...prefix.slice(0, at + 2), value, ...prefix.slice(at + 3)];
}

/** The prefix with the value of `--setenv NODE_OPTIONS` replaced (the harness never sets it: this is an experiment env); throws when the prefix has none. */
export function setNodeOptions(prefix, value) {
  const at = prefix.findIndex((arg, i) => arg === '--setenv' && prefix[i + 1] === 'NODE_OPTIONS');
  if (at < 0) throw new Error('the prefix has no --setenv NODE_OPTIONS to replace');
  return [...prefix.slice(0, at + 2), value, ...prefix.slice(at + 3)];
}

/** The extra environment keys a policy gives a stack's program for a case at `limitMiB` (through the per-stack allow-list; the box module is not changed). */
export function extraEnvFor(policy, stack, limitMiB) {
  if (policy === POLICY_3C && stack === 'csharp') return { DOTNET_GCHeapHardLimit: `0x${(limitMiB * MIB).toString(16)}` };
  return EXTRA_ENV[policy]?.[stack] ?? {};
}
/** Whether a policy puts the read-only view of the program's own cgroup in the box. */
export const viewFor = policy => !['today', POLICY_DECIDED_RUN, POLICY_NODE_FLAG, POLICY_1C, POLICY_C].includes(policy) && percentOf(policy) === null;

const DOTNET_FRACTION = 0.75;
const ABORTED = Object.freeze({ v8: 'V8 heap-limit abort (exit 134, runtimeAbort v8)', dotnet: '.NET runtime abort (exit 134, runtimeAbort dotnet)' });
const OOM_NET = /OutOfMemoryException/;
const OOM_NATIVE = /^Out of memory\.$/m;
const OOM_V8 = /JavaScript heap out of memory|Reached heap limit/;

/** How a run ended, from what the runner recorded: `{ result, ended }`. A kernel `oom_kill` wins over whatever the dying runtime printed. */
export function classify({ status, reason, memory, stderrTail }) {
  if (status === 'PASS') return { result: 'PASS', ended: 'exit 0' };
  if (reason === 'memory limit') return { result: 'memory limit', ended: ABORTED[memory.runtimeAbort] ?? `kernel oom_kill (${memory.oomKill})` };
  if (OOM_NET.test(stderrTail)) return { result: 'runtime error', ended: `.NET OutOfMemoryException (${reason})` };
  if (OOM_NATIVE.test(stderrTail)) return { result: 'runtime error', ended: `.NET runtime abort "Out of memory." (${reason}), not a kernel oom_kill` };
  if (OOM_V8.test(stderrTail)) return { result: 'runtime error', ended: `V8 heap-limit abort (${reason})` };
  return { result: 'runtime error', ended: reason };
}

/**
 * The layer's launch prefix with a read-only view of the program's own cgroup: `--unshare-cgroup` and `--ro-bind <prog> /sys/fs/cgroup` put in front of `--unshare-pid`. `prog` may hold
 * `{case}`. Nothing else changes.
 */
export function cgroupViewPrefix(prefix, prog) {
  const at = prefix.indexOf('--unshare-pid');
  if (at < 0) throw new Error('the prefix has no --unshare-pid to put the cgroup view before');
  return [...prefix.slice(0, at), '--unshare-cgroup', '--ro-bind', prog, '/sys/fs/cgroup', ...prefix.slice(at)];
}

function tweakFor(policy, run, stack) {
  const limitMiB = run.options.memoryMaxBytes / MIB;
  const flags = nodeOptionsFor(policy, stack, limitMiB);
  const hard = dotnetHardLimitFor(policy, stack, limitMiB);
  if (flags !== null) return prefix => setNodeOptions(prefix, flags);
  if (hard !== null) return prefix => setEnvValue(prefix, 'DOTNET_GCHeapHardLimit', hard);
  if (policy === POLICY_1C) return withoutCgroupView;
  return viewFor(policy) ? prefix => cgroupViewPrefix(prefix, progTemplate(run.unitPrefix)) : prefix => prefix;
}

function samples(owned, scope) {
  const dir = owned.ownedDir(scope, 'opksamples');
  writeFileSync(path.join(dir, 'p1.in'), 'x\n');
  writeFileSync(path.join(dir, 'p1.out'), 'OK\n');
  const limits = path.join(owned.ownedDir(scope, 'opklimits'), 'limits.json');
  writeFileSync(limits, JSON.stringify(TIME_LIMIT));
  return { dir, limits };
}

const tailOf = entry => entry?.lifecycle?.stderr?.tail ?? '';

/**
 * One run of `mode` of `stack` at `limitMiB` under `policy`, through the layer's run and the real runner (its prefix swapped for the policy's variant): `{ mode, stack, limitMiB, policy, result,
 * ended, peakBytes, stderrTail, wallMs }`. The program is given the limit as its argument.
 */
export async function runOnce({ owned, scope, dirs, csharpBuild, stack, mode, limitMiB, policy }) {
  const given = samples(owned, scope);
  const tree = programTree({ owned, scope, stack, mode: `${mode} ${limitMiB}`, csharpBuild });
  const opened = await openRun({ owned, parent: stableRunsParent(), tree, samplesDir: given.dir, caseIds: ['p1'], dirs, memoryMaxBytes: limitMiB * MIB, caseWallMs: WALL_MS, runCeilingMs: 2 * WALL_MS, limitsFile: given.limits, extraEnv: extraEnvFor(policy, stack, limitMiB), stack: layerStackFor(policy, stack) });
  if (!opened.ok) return { mode, stack, limitMiB, policy, result: 'not run', ended: opened.reason, peakBytes: null, stderrTail: '' };
  const run = opened.value;
  try {
    const argv = runnerArgv(run, { cgroupOf: progTemplate(run.unitPrefix), jsonOut: path.join(run.dir, 'record.json') });
    const at = argv.indexOf('--launch-prefix') + 1;
    argv[at] = JSON.stringify(tweakFor(policy, run, stack)(JSON.parse(argv[at])));
    const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs: 2 * WALL_MS, env: driverEnv({ dirs, tmp: run.dir }) });
    return describe({ mode, stack, limitMiB, policy }, ran, run);
  } finally {
    await cleanup(run);
  }
}

function describe(base, ran, run) {
  let record = null;
  try {
    record = JSON.parse(readFileSync(path.join(run.dir, 'record.json'), 'utf8'));
  } catch { /* the runner wrote none */ }
  const entry = record?.cases?.[0];
  const memory = entry?.lifecycle?.memory ?? { known: false, oomKill: 0 };
  if (entry === undefined) return { ...base, result: 'not run', ended: ran.ok ? ran.value.stderr.trim().slice(-200) : ran.reason, peakBytes: null, stderrTail: '' };
  return { ...base, ...classify({ status: entry.status, reason: entry.reason, memory, stderrTail: tailOf(entry) }), peakBytes: memory.peakBytes ?? null, stderrTail: tailOf(entry).trim(), wallMs: entry.wallMs ?? null };
}

/** One row per mode, stack, limit and policy: the runs' results, how each ended, and each peak in MiB. */
export function summarise(runs) {
  const rows = new Map();
  for (const run of runs) {
    const key = [run.mode, run.stack, run.limitMiB, run.policy].join('|');
    const row = rows.get(key) ?? { mode: run.mode, stack: run.stack, limitMiB: run.limitMiB, policy: run.policy, results: [], ended: [], peakMiB: [] };
    row.results.push(run.result);
    row.ended.push(run.ended);
    row.peakMiB.push(run.peakBytes === null ? null : Math.round(run.peakBytes / MIB));
    rows.set(key, row);
  }
  return [...rows.values()];
}

const CGVIEW = [
  '#!/bin/bash',
  "p() { printf '%s\\t%s\\n' \"$1\" \"$2\"; }",
  'p proc-self-cgroup "$(cat /proc/self/cgroup)"',
  'p sys "$(ls -A /sys | paste -sd,)"',
  'p sys-fs "$(ls -A /sys/fs | paste -sd,)"',
  'p cgroup-dirs "$(find /sys/fs/cgroup -mindepth 1 -maxdepth 1 -type d | paste -sd,)"',
  'p memory-max "$(cat /sys/fs/cgroup/memory.max 2>&1)"',
  'p writable "$( (echo 1 > /sys/fs/cgroup/memory.max) 2>&1 | head -1 )"',
  'p parent-visible "$(ls -A /sys/fs/cgroup/.. | paste -sd,)"',
  'p mountinfo "$(grep -c cgroup /proc/self/mountinfo)"',
  '',
].join('\n');

async function launchIn(run, policy, caseId, program, stack) {
  const prefix = tweakFor(policy, run, stack)(launchPrefix(run, { outside: true }));
  const argv = [...prefix.map(arg => arg.replaceAll('{case}', caseId)), ...program];
  const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs: WALL_MS, env: userEnv() });
  return ran.ok ? ran.value : { code: null, stdout: '', stderr: ran.reason };
}

const factsOf = text => Object.fromEntries(text.split('\n').filter(line => line.includes('\t')).map(line => [line.slice(0, line.indexOf('\t')), line.slice(line.indexOf('\t') + 1)]));

/**
 * Before the (3) runs: inside the variant box, what do the runtimes believe and what can the program see? `{ ok, problems, facts, dotnet, node }`. It must show the limit in .NET's
 * `TotalAvailableMemoryBytes` and Node's `constrainedMemory()`, a cgroup path of `0::/`, a read-only bind holding no child cgroup, and no sibling cgroup or host path.
 */
export async function verifyCgroupView({ owned, scope, dirs, csharpBuild, limitMiB, policy = 'cgroup-view-own', dotnetFraction = DOTNET_FRACTION }) {
  const tree = probeTree({ owned, scope, sentinel: '/nonexistent', csharpBuild });
  writeFileSync(path.join(tree, 'cgview.sh'), CGVIEW);
  const opened = await openRun({ owned, parent: stableRunsParent(), tree, caseIds: ['cg', 'dn', 'nd'], dirs, memoryMaxBytes: limitMiB * MIB, caseWallMs: WALL_MS, runCeilingMs: 2 * WALL_MS, extraEnv: extraEnvFor(policy, 'csharp', limitMiB) });
  if (!opened.ok) return { ok: false, problems: [opened.reason] };
  const run = opened.value;
  try {
    const facts = factsOf((await launchIn(run, policy, 'cg', ['bash', 'cgview.sh'])).stdout);
    const dotnet = JSON.parse((await launchIn(run, policy, 'dn', ['dotnet', 'bin/mem.dll', 'report'])).stdout || 'null');
    const node = JSON.parse((await launchIn(run, policy, 'nd', ['node', 'mem.mjs', 'report'])).stdout || 'null');
    return judgeView({ facts, dotnet, node, limitBytes: limitMiB * MIB, dotnetFraction });
  } finally {
    await cleanup(run);
  }
}

/** The (3) view judged: the limit picked up by both runtimes (.NET sizes its GC to 75 % of a cgroup limit, so that is what it reports), an own-cgroup-only view. */
export function judgeView({ facts, dotnet, node, limitBytes, dotnetFraction = DOTNET_FRACTION }) {
  const problems = [];
  if (facts['proc-self-cgroup'] !== '0::/') problems.push(`/proc/self/cgroup is "${facts['proc-self-cgroup']}", not 0::/`);
  if (Number(facts['memory-max']) !== limitBytes) problems.push(`/sys/fs/cgroup/memory.max says "${facts['memory-max']}", not ${limitBytes}`);
  if (facts['cgroup-dirs'] !== '') problems.push(`child or sibling cgroup directories are visible: ${facts['cgroup-dirs']}`);
  if (facts.sys !== 'fs' || facts['sys-fs'] !== 'cgroup') problems.push(`/sys holds "${facts.sys}" and /sys/fs holds "${facts['sys-fs']}", not only fs/cgroup`);
  if (dotnetFraction !== null && dotnet?.totalAvailableBytes !== limitBytes * dotnetFraction) problems.push(`.NET TotalAvailableMemoryBytes is ${dotnet?.totalAvailableBytes}, not ${dotnetFraction * 100} % of the limit`);
  if (node?.constrainedBytes !== limitBytes) problems.push(`Node constrainedMemory() is ${node?.constrainedBytes}, not the limit`);
  return { ok: problems.length === 0, problems, facts, dotnet, node };
}

/**
 * What .NET believes under `policy` with `extraEnv` (the `report` mode of the C# program, run once in the variant box): `{ totalAvailableBytes, heapHardLimit, cgroupVisible }` or null. A side measurement
 * for the (3b) record: which values of `DOTNET_GCHeapHardLimitPercent` the runtime takes.
 */
export async function dotnetBelieves({ owned, scope, dirs, csharpBuild, limitMiB, policy, extraEnv }) {
  const tree = probeTree({ owned, scope, sentinel: '/nonexistent', csharpBuild });
  const opened = await openRun({ owned, parent: stableRunsParent(), tree, caseIds: ['dn'], dirs, memoryMaxBytes: limitMiB * MIB, caseWallMs: WALL_MS, runCeilingMs: 2 * WALL_MS, extraEnv });
  if (!opened.ok) return null;
  try {
    const ran = await launchIn(opened.value, policy, 'dn', ['dotnet', 'bin/mem.dll', 'report']);
    return JSON.parse(ran.stdout || 'null');
  } finally {
    await cleanup(opened.value);
  }
}

/** What `stack`'s `report` mode prints under `policy` at `limitMiB` (.NET's `totalAvailableBytes`; Node's `constrainedBytes` and `heapSizeLimitBytes`), or null. */
export async function believesUnder({ owned, scope, dirs, csharpBuild, limitMiB, policy, stack }) {
  const tree = probeTree({ owned, scope, sentinel: '/nonexistent', csharpBuild });
  const opened = await openRun({ owned, parent: stableRunsParent(), tree, caseIds: ['rp'], dirs, memoryMaxBytes: limitMiB * MIB, caseWallMs: WALL_MS, runCeilingMs: 2 * WALL_MS, extraEnv: extraEnvFor(policy, stack, limitMiB), stack: layerStackFor(policy, stack) });
  if (!opened.ok) return null;
  try {
    const program = stack === 'csharp' ? ['dotnet', 'bin/mem.dll', 'report'] : ['node', 'mem.mjs', 'report'];
    return JSON.parse((await launchIn(opened.value, policy, 'rp', program, stack)).stdout || 'null');
  } finally {
    await cleanup(opened.value);
  }
}
