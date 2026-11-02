// E1.S8b brief B: the probe's lines that need a launch THROUGH the layer — the network namespace the driver and the program share, the program's and the driver's cgroups, ping over
// 127.0.0.1, the delegated scope (line (f): `prog/` and `memory.max`, then a workload and `memory.peak` READ after its exit), the culture probes of both stacks, the empty program's launch
// time. The facts read from inside the box (`facts.mjs`) are one launch. Every launch is `programArgv` of an open run: the real prefix, the delegated scope, the v2 box.
import { spawn } from 'node:child_process';
import { readFileSync, readlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { unsafeBinds } from '../exec-layer/box.mjs';
import { cleanup } from '../exec-layer/cleanup.mjs';
import { nsenterArgv } from '../exec-layer/netns.mjs';
import { launchPrefix, openRun, programArgv, runCases } from '../exec-layer/run.mjs';
import { progCgroup, readProg, userEnv } from '../exec-layer/scope.mjs';
import { buildCsharp, probeTree } from './programs.mjs';
import { judgeFacts, parseFacts } from './facts.mjs';

const MIB = 1024 * 1024;
const WALL_MS = 60_000;
const LIMIT_BYTES = 256 * MIB;
const CASES = Object.freeze(['facts', 'net', 'work', 'dotnet', 'node']);
const CLIENT = 'import socket,sys\ntry:\n s=socket.create_connection(("127.0.0.1",int(sys.argv[1])),timeout=5);s.sendall(b"ping\\n");print(s.makefile().readline().strip())\nexcept OSError as e:\n print("ERR",e.__class__.__name__)';
const text = file => { try { return readFileSync(file, 'utf8'); } catch { return null; } };
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

/** Launch `program` of `caseId` through the prefix from outside the namespaces and wait for its exit: `{ code, stdout, stderr }` (code null when it could not run). */
async function launch(run, caseId, program) {
  const argv = programArgv(run, caseId, program);
  const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs: WALL_MS, env: userEnv() });
  return ran.ok ? ran.value : { code: null, stdout: '', stderr: ran.reason };
}

/**
 * The probe's run: a sentinel under `$HOME` only the host can read, the built C# program, one tree, one run with a view per probe case at 256 MiB. `{ run, sentinel }`, or `{ problem }`.
 * The caller cleans up (`closeProbeRun`).
 */
export async function openProbeRun({ owned, scope, dirs, sentinelHome, csharpBuild }) {
  const folder = owned.ownedDir(scope, 'sentinel', { parent: sentinelHome });
  const sentinel = path.join(folder, 'secret.txt');
  writeFileSync(sentinel, 'HOME-SENTINEL');
  const tree = probeTree({ owned, scope, sentinel, csharpBuild });
  const opened = await openRun({ owned, parent: owned.ownedDir(scope, 'proberuns'), tree, caseIds: [...CASES], dirs, memoryMaxBytes: LIMIT_BYTES, caseWallMs: WALL_MS, runCeilingMs: 5 * WALL_MS });
  return opened.ok ? { run: opened.value, sentinel } : { problem: opened.reason };
}

export const closeProbeRun = run => cleanup(run);

/** The facts script's lines (`home-sentinel` … `env-keys`) and the raw facts. */
export async function boxFactLines(run) {
  const ran = await launch(run, 'facts', ['bash', 'facts.sh']);
  const facts = parseFacts(ran.stdout);
  return { lines: judgeFacts(facts), facts };
}

/** The bind list of the box this run launches, with the scan of its sources: `{ id: 'bind-list', ok, binds }`. */
export function bindLine(run, home) {
  const argv = launchPrefix(run);
  const binds = argv.flatMap((arg, i) => (['--bind', '--ro-bind'].includes(arg) ? [{ mode: arg, source: argv[i + 1] }] : []));
  const problems = unsafeBinds(argv, { home });
  return { id: 'bind-list', ok: problems.length === 0, binds, problems };
}

async function spawnDriver(run) {
  const [exe, ...args] = [...nsenterArgv(run.holder, run.tools.nsenter), 'python3', '-I', '-c', 'import time; time.sleep(60)'];
  const child = spawn(exe, args, { stdio: 'ignore', detached: true, env: userEnv() });
  for (let waited = 0; waited < 5000 && text(`/proc/${child.pid}/comm`)?.trim() !== 'python3'; waited += 25) await pause(25);
  return child;
}

async function programPid(unitProg) {
  for (let waited = 0; waited < 15_000; waited += 25) {
    const found = (readProg(unitProg).procs ?? []).find(pid => /^python3/.test(text(`/proc/${pid}/comm`) ?? ''));
    if (found !== undefined) return found;
    await pause(25);
  }
  return null;
}

/** The port the server printed on its first line of standard output (the box's workspace is an overlay: a file it writes does not land on the host), or null within 15 s. */
function readyPort(child) {
  return new Promise(resolve => {
    let said = '';
    child.stdout.on('data', chunk => { said += chunk; if (said.includes('\n')) resolve(Number(said.split('\n')[0])); });
    setTimeout(() => resolve(null), 15_000).unref();
  });
}

const detached = (argv, stdio = 'ignore') => spawn(argv[0], argv.slice(1), { stdio, detached: true, env: userEnv() });
const stop = child => { try { process.kill(-child.pid, 'SIGKILL'); } catch { /* gone */ } };
const procText = (pid, name) => (pid === null ? '' : text(`/proc/${pid}/${name}`) ?? '');
const nsOf = pid => (pid === null ? null : readlinkSync(`/proc/${pid}/ns/net`));

async function ping(run, port) {
  if (port === null) return '';
  const join = nsenterArgv(run.holder, run.tools.nsenter);
  const ran = await runBounded(join[0], [...join.slice(1), 'python3', '-I', '-c', CLIENT, String(port)], { timeoutMs: 20_000, env: userEnv() });
  return ran.ok ? ran.value.stdout.trim() : '';
}

/** `netns`, `program-in-prog` and `ping-over-loopback`: the driver and the program share a namespace that is not the host's; the program is in `prog/` and the driver is not; ping works. */
export async function networkLines(run) {
  const server = detached(programArgv(run, 'net', ['python3', 'server.py']), ['ignore', 'pipe', 'ignore']);
  const unit = `${run.unitPrefix}-net`;
  const driver = await spawnDriver(run);
  try {
    const pid = await programPid(progCgroup(unit));
    const port = await readyPort(server);
    const reply = await ping(run, port);
    const ns = { host: readlinkSync('/proc/self/ns/net'), driver: nsOf(driver.pid), program: nsOf(pid) };
    const cgroups = { program: procText(pid, 'cgroup').trim(), driver: procText(driver.pid, 'cgroup').trim() };
    return [
      { id: 'netns', ok: ns.program !== null && ns.driver === ns.program && ns.program !== ns.host, ns },
      { id: 'program-in-prog', ok: cgroups.program.endsWith(`${unit}.scope/prog`) && !cgroups.driver.includes(run.unitPrefix), cgroups },
      { id: 'ping-over-loopback', ok: reply === 'PING', port },
    ];
  } finally {
    [server, driver].forEach(stop);
  }
}

const numberOf = file => {
  const body = text(file);
  return body === null ? null : Number(body.trim());
};

/**
 * Line (f): a delegated user scope creates `prog/` and writes `memory.max` and `memory.swap.max` = 0; THEN a workload runs in `prog/`, and after its exit `prog/memory.peak` is READ and is
 * above zero. A host where it cannot be read is `halt: 'memory-not-certifiable'`. `deps.readPeak` is the seam the test replaces.
 */
export async function runDelegatedScopeLine({ owned, scope, dirs, csharpBuild, deps = {} }) {
  const readPeak = deps.readPeak ?? (dir => readProg(dir).peakBytes);
  const tree = probeTree({ owned, scope, sentinel: '/nonexistent', csharpBuild: csharpBuild ?? await csharpOf({ owned, scope, dirs }) });
  const opened = await openRun({ owned, parent: owned.ownedDir(scope, 'proberuns'), tree, caseIds: ['work'], dirs, memoryMaxBytes: LIMIT_BYTES, caseWallMs: WALL_MS, runCeilingMs: 5 * WALL_MS });
  if (!opened.ok) return { id: 'delegated-scope', ok: false, problem: opened.reason };
  try {
    const ran = await launch(opened.value, 'work', ['python3', 'work.py']);
    const prog = progCgroup(`${opened.value.unitPrefix}-work`);
    return judgeScope({ memoryMax: numberOf(path.join(prog, 'memory.max')), swapMax: numberOf(path.join(prog, 'memory.swap.max')), peakBytes: readPeak(prog), workloadExit: ran.code });
  } finally {
    await cleanup(opened.value);
  }
}

/** Line (f) judged: `memory.max` is the limit, `memory.swap.max` is 0, the workload exited 0 and `memory.peak` was read above zero; an unreadable peak is `memory-not-certifiable`. */
function judgeScope(seen) {
  const good = seen.peakBytes > 0 && seen.memoryMax === LIMIT_BYTES && seen.swapMax === 0 && seen.workloadExit === 0;
  return { id: 'delegated-scope', ok: good, ...seen, ...(seen.peakBytes === null ? { halt: 'memory-not-certifiable' } : {}) };
}

async function csharpOf({ owned, scope, dirs }) {
  const built = await buildCsharp({ owned, scope, dirs });
  if (!built.ok) throw new Error(built.reason);
  return built.value;
}

/** `dotnet-culture` (`de-DE`) and `node-culture` (starts with `de`): the toy programs of both stacks, run in the box. */
export async function cultureLines(run) {
  const dotnet = await launch(run, 'dotnet', ['dotnet', 'bin/mem.dll', 'culture']);
  const node = await launch(run, 'node', ['node', 'mem.mjs', 'culture']);
  return [
    { id: 'dotnet-culture', ok: dotnet.stdout.trim() === 'de-DE', seen: dotnet.stdout.trim() },
    { id: 'node-culture', ok: node.stdout.trim().startsWith('de'), seen: node.stdout.trim() },
  ];
}

/** The pilot's F1 control: an empty program timed by the runner through the same prefix (`record.emptyProgram`), and whether the memory read certified. */
export async function launchTimeLine({ owned, scope, dirs, tree, samplesDir }) {
  const result = await runCases({ owned, parent: owned.ownedDir(scope, 'proberuns'), tree, samplesDir, caseIds: ['p1'], dirs, memoryMaxBytes: LIMIT_BYTES, caseWallMs: WALL_MS, runCeilingMs: 5 * WALL_MS, minSweepAgeMs: 0 });
  const empty = result.record?.emptyProgram ?? {};
  return { id: 'empty-program-launch', ok: result.exitCode === 0 && Number.isFinite(empty.launchMs), launchMs: empty.launchMs ?? null, emptyProgramMs: empty.ms ?? null, memoryCertified: result.memoryCertified };
}
