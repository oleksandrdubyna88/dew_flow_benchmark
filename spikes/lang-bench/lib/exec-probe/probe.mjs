// E1.S8b brief B: the host probe — every line of the plan's probe list, run THROUGH the layer (the real prefix, the delegated scope, the v2 box), then the memory view at 256 and 512 MiB
// asserted by policy. It records first and judges after: the record is complete even when a line fails; the exit is 3 naming the FIRST failed line (`halt`), then the memory verdict.
// Exit 0 only when every line passed AND a decided memory policy holds.
import * as fs from 'node:fs';
import { mkdirSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fail } from '../../../minigit-bench/lib/result.mjs';
import { observeAt } from './observe.mjs';
import { assertMemoryView, exitCodeOf, LIMITS_MIB, overLimitRows, POLICY_DECIDED, probeVerdict, readPolicy, workloadRows } from './memory.mjs';
import { recordProblems } from './facts.mjs';
import { bindLine, boxFactLines, closeProbeRun, cultureLines, launchTimeLine, networkLines, openProbeRun, runDelegatedScopeLine } from './lines.mjs';
import { buildCsharp } from './programs.mjs';
import { RecordExists } from './record-path.mjs';
import { dropTmpFolder, ensureTmpFolder, ownId, tmpPath } from './tmp-files.mjs';

const MIB = 1024 * 1024;
export { LIMITS_MIB };

/** The memory verdict of a record under a policy file's content: `{ ok, halt, problems }`. */
export const verdictFor = (record, policyFile) => assertMemoryView({ policy: policyFile.policy, observations: record.memoryView.observations });

const first = (command, args) => (spawnSync(command, args, { encoding: 'utf8' }).stdout ?? '').split('\n')[0].trim();

/** The host the probe ran on: name, kernel, cgroup version, and the tools' versions. */
export function hostFacts() {
  return { hostname: os.hostname(), kernel: os.release(), cgroup2: spawnSync('test', ['-e', '/sys/fs/cgroup/cgroup.controllers']).status === 0, systemd: first('systemctl', ['--version']), bwrap: first('bwrap', ['--version']), node: process.version };
}

async function observations({ owned, scope, dirs, csharpBuild, progress }) {
  const limits = [];
  for (const limit of LIMITS_MIB) {
    progress(`memory view at ${limit} MiB: start`);
    limits.push(await observeAt({ owned, scope, dirs, csharpBuild, memoryMaxBytes: limit * MIB, progress }));
    progress(`memory view at ${limit} MiB: done`);
  }
  return { limits };
}

/** Announce a group of probe lines, run it, and report each line it produced: `name: start`, then `<id>: ok|FAIL` per line (K3: a long probe is not silent). */
async function step(progress, name, run) {
  progress(`${name}: start`);
  const produced = [await run()].flat(2);
  for (const line of produced) progress(`${line.id}: ${line.ok ? 'ok' : 'FAIL'}`);
  return produced;
}

/** The lines of the probe, in the plan's order, as `{ id, ok, ... }`. */
async function lines({ owned, scope, dirs, csharpBuild, tree, samplesDir, home, progress }) {
  const opened = await openProbeRun({ owned, scope, dirs, sentinelHome: home, csharpBuild });
  if (opened.problem) return [{ id: 'launch', ok: false, problem: opened.problem }];
  const { run } = opened;
  try {
    const net = await step(progress, 'network', () => networkLines(run));
    const bind = await step(progress, 'bind list', () => bindLine(run, home));
    const box = await step(progress, 'box facts', async () => (await boxFactLines(run)).lines);
    const launched = await step(progress, 'empty-program launch', () => launchTimeLine({ owned, scope, dirs, tree, samplesDir }));
    const scoped = await step(progress, 'delegated scope', () => runDelegatedScopeLine({ owned, scope, dirs, csharpBuild }));
    const culture = await step(progress, 'culture', () => cultureLines(run));
    return [...net, ...bind, ...box, ...launched, ...scoped, ...culture];
  } finally {
    await closeProbeRun(run);
  }
}

/**
 * Run the probe: `{ record, exitCode, halt }`. `deps.policyFile` is the policy file's path; `deps.prepare` builds the empty-program tree (`{ tree, samplesDir }`).
 */
export async function runProbe({ owned, scope, dirs, policyPath, prepare, now = new Date(), identity, runner = null, home = os.homedir(), progress = () => {} }) {
  const policy = readPolicy(policyPath);
  if (!policy.ok) return fail(policy.reason);
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) return fail(built.reason);
  const { tree, samplesDir } = prepare();
  const found = await lines({ owned, scope, dirs, csharpBuild: built.value, tree, samplesDir, home, progress });
  const memoryView = { policy: policy.value, observations: await observations({ owned, scope, dirs, csharpBuild: built.value, progress }) };
  return { ok: true, value: finish({ found, memoryView, now, identity, runner }) };
}

/** V8's `heap_size_limit` against `prog/memory.max` at each limit. `asserted` is true exactly when the policy in force asserts it: the pinned policy's rule (`ninetyRule`) holds the range [0.9·L, L). */
function v8HeapRecord(observations, policy) {
  const asserted = policy?.policy === POLICY_DECIDED;
  return observations.limits.filter(at => at.node).map(at => ({ memoryMaxBytes: at.memoryMaxBytes, v8HeapSizeLimitBytes: at.node.heapSizeLimitBytes, ratio: Number((at.node.heapSizeLimitBytes / at.memoryMaxBytes).toFixed(3)), asserted }));
}

/** `probedOn` and `host` are stamped into the layer's identity file only by a probe that exited 0: every line passed AND the memory verdict held. */
export const shouldStampProbedOn = record => record.exit.code === 0 && record.lines.every(line => line.ok);

/** The peak/L of every over-limit fixture per stack and limit (J2) and the workloads' results: RECORDED; `findings` lists the over-limit rows within 10 points of 50 % — a finding for the team lead, never tuned around. */
function ratiosOf(observations) {
  const overLimit = overLimitRows(observations);
  return { workloads: workloadRows(observations), overLimit, findings: overLimit.filter(row => row.finding) };
}

function verdictOf(memoryView) {
  const unobserved = memoryView.observations.limits.filter(at => at.problem);
  if (unobserved.length > 0) return { ok: false, halt: 'memory-view-unobserved', problems: unobserved.map(at => at.problem) };
  return probeVerdict({ policy: memoryView.policy.policy, observations: memoryView.observations });
}

/** The record and the exit of a probe's lines and memory view: the first failed line, else the memory verdict; a record that holds a host key is itself a failure. */
export function finish({ found, memoryView, now, identity, runner = null }) {
  const failedLine = found.find(line => !line.ok);
  const verdict = verdictOf(memoryView);
  const halt = failedLine === undefined ? verdict.halt : failedLine.halt ?? failedLine.id;
  const record = { date: now.toISOString().slice(0, 10), probedOn: now.toISOString(), host: hostFacts(), layerIdentity: identity, runner, lines: found, memoryView: { ...memoryView, verdict, v8Heap: v8HeapRecord(memoryView.observations, memoryView.policy), ...ratiosOf(memoryView.observations) }, exit: { code: halt === null ? 0 : 3, halt } };
  const problems = recordProblems(record);
  return problems.length === 0 ? record : { ...record, exit: { code: 3, halt: 'record-holds-host-keys' }, recordProblems: problems };
}

/**
 * The record written ONCE with its final content (S1): to a temporary file beside the reserved placeholder and renamed over it, so a failed write (a full disk) leaves no cut-off dated record: the placeholder
 * stays empty and the temporary file is removed. `io` is the test seam.
 */
export function writeReservedRecord(file, record, held, io = fs) {
  if (held === null || held === undefined) throw new Error(`writeReservedRecord: ${file} is not held by a reservation: a record is only written into a placeholder the caller reserved (T-L53)`);
  if (io.statSync(file).size !== 0) throw new Error(`writeReservedRecord: ${file} is not an empty placeholder: a record is never overwritten (T-L53)`);
  const tmp = tmpPath(file, 'record', ownId()); // D2: in a `.exec-probe-tmp/` folder beside the placeholder
  try {
    ensureTmpFolder(file, io);
    io.writeFileSync(tmp, `${JSON.stringify(record, null, 2)}\n`);
    io.renameSync(tmp, file);
  } catch (error) {
    io.rmSync(tmp, { force: true });
    throw error;
  } finally {
    dropTmpFolder(path.dirname(file), io);
  }
}

/**
 * Write the record (a folder made as needed). Without `held` (a reservation, `reserveRecord`) the file is created EXCLUSIVELY: a path that exists is `RecordExists` and keeps every byte (T-L53). With the
 * reservation the holder fills its own empty placeholder.
 */
export function writeRecord(file, record, held = null) {
  mkdirSync(path.dirname(file), { recursive: true });
  try {
    writeFileSync(file, `${JSON.stringify(record, null, 2)}\n`, { flag: held === null ? 'wx' : 'w' });
  } catch (error) {
    throw error.code === 'EEXIST' ? new RecordExists(file) : error;
  }
}
