// E1.S8b OP-K experiment (the team lead's, 2026-11-02): do the runtimes, left to their own view of memory, meet the kernel's OOM kill with realistic live data — and does a read-only cgroup
// view of the program's OWN cgroup change that? Two policies are run through the real layer and the runner, UNCHANGED: (1) today's box (no cgroup visible) and (3) the same box plus
// `--unshare-cgroup` (so /proc/self/cgroup shows `0::/`) and a read-only bind of `prog/` at /sys/fs/cgroup. (3) is a variant of the layer's own prefix built here; the box module and the
// policy file are not touched. Toys only, never a task of the series.
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { cleanup } from '../exec-layer/cleanup.mjs';
import { driverEnv } from '../exec-layer/env.mjs';
import { launchPrefix, openRun, programArgv, runnerArgv } from '../exec-layer/run.mjs';
import { progTemplate, userEnv } from '../exec-layer/scope.mjs';
import { probeTree, programTree } from './programs.mjs';

const MIB = 1024 * 1024;
const WALL_MS = 120_000;
const TIME_LIMIT = { timeS: 100 };
export const POLICIES = Object.freeze(['today', 'cgroup-view-own']);
/** (3b): view (3) plus a hard limit of 100 % (hexadecimal, as .NET reads it) in the .NET environment only. */
export const POLICY_3B = 'cgroup-view-own+gchl100';
const EXTRA_ENV = Object.freeze({ [POLICY_3B]: Object.freeze({ csharp: Object.freeze({ DOTNET_GCHeapHardLimitPercent: '0x64' }) }) });

/** The extra environment keys a policy gives a stack's program (through the per-stack allow-list; the box module is not changed). */
export const extraEnvFor = (policy, stack) => EXTRA_ENV[policy]?.[stack] ?? {};
/** Whether a policy puts the read-only view of the program's own cgroup in the box. */
export const viewFor = policy => policy !== 'today';

const DOTNET_FRACTION = 0.75;
const OOM_NET = /OutOfMemoryException/;
const OOM_NATIVE = /^Out of memory\.$/m;
const OOM_V8 = /JavaScript heap out of memory|Reached heap limit/;

/** How a run ended, from what the runner recorded: `{ result, ended }`. A kernel `oom_kill` wins over whatever the dying runtime printed. */
export function classify({ status, reason, memory, stderrTail }) {
  if (status === 'PASS') return { result: 'PASS', ended: 'exit 0' };
  if (reason === 'memory limit') return { result: 'memory limit', ended: `kernel oom_kill (${memory.oomKill})` };
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

const tweakFor = (policy, run) => (viewFor(policy) ? prefix => cgroupViewPrefix(prefix, progTemplate(run.unitPrefix)) : prefix => prefix);

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
  const opened = await openRun({ owned, parent: owned.ownedDir(scope, 'opkruns'), tree, samplesDir: given.dir, caseIds: ['p1'], dirs, memoryMaxBytes: limitMiB * MIB, caseWallMs: WALL_MS, runCeilingMs: 2 * WALL_MS, limitsFile: given.limits, extraEnv: extraEnvFor(policy, stack) });
  if (!opened.ok) return { mode, stack, limitMiB, policy, result: 'not run', ended: opened.reason, peakBytes: null, stderrTail: '' };
  const run = opened.value;
  try {
    const argv = runnerArgv(run, { cgroupOf: progTemplate(run.unitPrefix), jsonOut: path.join(run.dir, 'record.json') });
    const at = argv.indexOf('--launch-prefix') + 1;
    argv[at] = JSON.stringify(tweakFor(policy, run)(JSON.parse(argv[at])));
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

async function launchIn(run, policy, caseId, program) {
  const prefix = tweakFor(policy, run)(launchPrefix(run, { outside: true }));
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
  const opened = await openRun({ owned, parent: owned.ownedDir(scope, 'opkruns'), tree, caseIds: ['cg', 'dn', 'nd'], dirs, memoryMaxBytes: limitMiB * MIB, caseWallMs: WALL_MS, runCeilingMs: 2 * WALL_MS, extraEnv: extraEnvFor(policy, 'csharp') });
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
  const opened = await openRun({ owned, parent: owned.ownedDir(scope, 'opkruns'), tree, caseIds: ['dn'], dirs, memoryMaxBytes: limitMiB * MIB, caseWallMs: WALL_MS, runCeilingMs: 2 * WALL_MS, extraEnv });
  if (!opened.ok) return null;
  try {
    const ran = await launchIn(opened.value, policy, 'dn', ['dotnet', 'bin/mem.dll', 'report']);
    return JSON.parse(ran.stdout || 'null');
  } finally {
    await cleanup(opened.value);
  }
}
