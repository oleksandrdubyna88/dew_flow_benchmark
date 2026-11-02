// E1.S8b brief B: what the probe observes at one `MemoryMax`, through the FINAL prefix (the delegated scope, the box, the runner) — never beside it. `runMode` runs a toy program of a stack
// through `runCases` (the runner judges it); `believed` starts the `report` mode through the same prefix and reads the host's `prog/memory.max` after its exit; `observeAt` puts both
// stacks' beliefs, the churn results and the over-limit results of one limit in the shape `assertMemoryView` takes.
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { cleanup } from '../exec-layer/cleanup.mjs';
import { openRun, programArgv, runCases } from '../exec-layer/run.mjs';
import { progCgroup, userEnv } from '../exec-layer/scope.mjs';
import { programTree, STACKS } from './programs.mjs';

const WALL_MS = 70_000;
const CEILING_MS = 200_000;
const PROBE_CASE = 'm';
const LIMITS_FILE = { timeS: 60 };

function samples(owned, scope, output) {
  const dir = owned.ownedDir(scope, 'psamples');
  writeFileSync(path.join(dir, 'p1.in'), 'x\n');
  writeFileSync(path.join(dir, 'p1.out'), output);
  return dir;
}

/**
 * Run the toy program of `stack` in `mode` as one olympiad case through the layer and the runner at `memoryMaxBytes`: `{ status, reason, memory, exitCode, stdout }` of the case as the
 * runner judged it. The program prints `OK` in `churn` and `overlimit` and nothing else is expected.
 */
export async function runMode({ owned, scope, dirs, csharpBuild, stack, mode, memoryMaxBytes }) {
  const limitsDir = owned.ownedDir(scope, 'plimits');
  const limitsFile = path.join(limitsDir, 'limits.json');
  writeFileSync(limitsFile, JSON.stringify(LIMITS_FILE));
  const result = await runCases({
    owned, parent: owned.ownedDir(scope, 'pruns'), tree: programTree({ owned, scope, stack, mode, csharpBuild }), samplesDir: samples(owned, scope, 'OK\n'), caseIds: ['p1'],
    dirs, memoryMaxBytes, caseWallMs: WALL_MS, runCeilingMs: CEILING_MS, limitsFile, minSweepAgeMs: 0,
  });
  const entry = result.record?.cases?.[0];
  return { status: entry?.status ?? 'NOT RUN', reason: entry?.reason ?? result.stderr.trim(), memory: entry?.lifecycle?.memory ?? { known: false }, exitCode: result.exitCode, stdout: result.stdout };
}

const numberFile = file => {
  try {
    return Number(readFileSync(file, 'utf8').trim());
  } catch {
    return null;
  }
};

/** What `report` mode printed (parsed) and the host's `prog/memory.max` of its scope: `{ value, hostMemoryMax }`, or `{ value: null, problem }`. Cleans up after itself. */
export async function believed({ owned, scope, dirs, csharpBuild, stack, memoryMaxBytes, mode = 'report' }) {
  const opened = await openRun({ owned, parent: owned.ownedDir(scope, 'pruns'), tree: programTree({ owned, scope, stack, mode, csharpBuild }), caseIds: [PROBE_CASE], dirs, memoryMaxBytes, caseWallMs: WALL_MS, runCeilingMs: CEILING_MS });
  if (!opened.ok) return { value: null, problem: opened.reason };
  const run = opened.value;
  try {
    const argv = programArgv(run, PROBE_CASE);
    const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs: WALL_MS, env: userEnv() });
    const hostMemoryMax = numberFile(path.join(progCgroup(`${run.unitPrefix}-${PROBE_CASE}`), 'memory.max'));
    if (!ran.ok || ran.value.code !== 0) return { value: null, problem: `the ${stack} ${mode} program did not run: ${ran.ok ? ran.value.stderr : ran.reason}` };
    return { value: mode === 'report' ? JSON.parse(ran.value.stdout) : ran.value.stdout.trim(), hostMemoryMax };
  } finally {
    await cleanup(run);
  }
}

const overOf = ran => ({ reason: ran.reason, oomKill: ran.memory.oomKill ?? null, peakBytes: ran.memory.peakBytes ?? null, eventsKept: ran.memory.known === true && ran.memory.events !== undefined });

/** The observation of one limit: `{ memoryMaxBytes, hostMemoryMax, csharp, node, churn, overLimit }` (`assertMemoryView`'s shape), or `{ problem }` when a program did not run. */
export async function observeAt({ owned, scope, dirs, csharpBuild, memoryMaxBytes }) {
  const common = { owned, scope, dirs, csharpBuild, memoryMaxBytes };
  const at = { memoryMaxBytes, churn: {}, overLimit: {} };
  for (const stack of STACKS) {
    const seen = await believed({ ...common, stack });
    if (seen.value === null) return { problem: seen.problem };
    Object.assign(at, { [stack]: seen.value, hostMemoryMax: seen.hostMemoryMax });
    at.churn[stack] = (await runMode({ ...common, stack, mode: 'churn' })).status;
    at.overLimit[stack] = overOf(await runMode({ ...common, stack, mode: 'overlimit' }));
  }
  return at;
}
