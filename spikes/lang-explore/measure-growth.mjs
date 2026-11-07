#!/usr/bin/env node
// The first measurement of what ten scoring passes cost (F1.S2, section 4): FREE (no model is called), a scoring of KEPT trees on the scoring CPUs, under the bootstrap's own guard (`lib/growth.mjs`). It reads the kept trees of a
// finished series and the held-out set of its task, writes only inside `~/rsd/lang-v2/explore/measure-f1s2-<yyyymmdd>/` and, last, `spikes/lang-explore/inputs/growth.json` (counts, times and sizes only).
//
//   node spikes/lang-explore/measure-growth.mjs --task NN --series <dir> --defective <rid> --accepted-csharp <rid> --accepted-node <rid> [--accept-noisy <why>]
//
// Exit codes: 0 done | 3 a resumable halt (the machine is not quiet, the write ceiling, an environment halt: every file is kept) | 4 refused (the free-disk floor, usage, a missing input).
// It re-executes itself under `taskset -c 6,7`, as `run`, `rescore` and `replay` do.
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { acquireSeriesLock } from '../minigit-bench/lib/series-lock.mjs';
import { layoutPaths } from '../minigit-bench/lib/sandbox.mjs';
import { systemClock } from './lib/clock.mjs';
import { loadHeldout } from './lib/heldout.mjs';
import { loadExplore } from './lib/inputs.mjs';
import { loadCalibration } from './lib/limits.mjs';
import { measureGuard } from './lib/growth.mjs';
import { dirBytes, freeBytesOf, runMeasurement } from './lib/measure.mjs';
import { machineFor, pilotQuiet } from './lib/machine.mjs';
import { scorePasses } from './lib/passes.mjs';
import { exploreRoot } from './lib/paths.mjs';
import { resolveTask } from './lib/tasks.mjs';
import { loadWorld } from './lib/world.mjs';

const OPTIONS = { task: { type: 'string' }, series: { type: 'string' }, defective: { type: 'string' }, 'accepted-csharp': { type: 'string' }, 'accepted-node': { type: 'string' }, 'accept-noisy': { type: 'string' } };
const say = line => process.stdout.write(`${line}\n`);

/** The subjects, from the series' kept trees: `[{ name, stack, source }]`, the defective one first (the worst case is measured on it). */
function subjectsOf({ values, seriesDir }) {
  const source = rid => layoutPaths(path.join(seriesDir, 's'), rid).work;
  return [
    { name: 'defective', stack: 'csharp', source: source(values.defective) },
    { name: 'accepted-csharp', stack: 'csharp', source: source(values['accepted-csharp']) },
    { name: 'accepted-node', stack: 'node', source: source(values['accepted-node']) },
  ];
}

const missingInputs = ({ values, subjects }) => {
  const absent = ['task', 'series', 'defective', 'accepted-csharp', 'accepted-node'].filter(k => !values[k]);
  return absent.length > 0 ? `missing: ${absent.map(k => `--${k}`).join(', ')}` : subjects.filter(s => !existsSync(s.source)).map(s => `the kept tree of ${s.name} is not there`)[0] ?? null;
};

/** The quiet check of `explore.mjs quiet`: the numbers are a measurement and are only good on a quiet machine. -> a sentence if it is not quiet and no override was given, else null. */
async function quietProblem({ home, folder, acceptNoisy, signal }) {
  const thresholds = pilotQuiet();
  const spin = JSON.parse(readFileSync(path.join(exploreRoot(home), 'spin.json'), 'utf8'));
  const machine = machineFor({ thresholds: thresholds.value, calibratedMs: spin.medianMs, seriesDir: folder, acceptNoisy, home });
  const r = await machine.quiet({ cellIds: [], say, signal });
  say(r.ok ? `the machine is ${r.value.quiet ? 'quiet' : `NOT quiet, started anyway (${acceptNoisy})`}` : `the machine is not quiet: ${r.reason}`);
  return r.ok ? null : r.reason;
}

/** The driver call of one subject over the quick world of the task. */
function scoreFor({ world, heldout, calibration, folder, signal }) {
  return (subject, { work, afterAttempt, severe }) => {
    const seams = world.passSeams({ stack: subject.stack, source: subject.source, limits: calibration.limits, sets: [{ name: 'heldout', dir: heldout.casesDir }], traps: heldout.traps, say: () => {}, driftPossible: true, seriesDir: folder });
    const inner = seams.runPass;
    // the worst case: the first two attempts of every pass are environment ones, through the same code path (no child is started for them)
    const runPass = severe ? async spec => (spec.attempt < 3 ? { kind: 'environment', reason: 'the first two attempts of every pass are made environment ones for the worst-case measurement' } : inner(spec)) : inner;
    return scorePasses({ ...seams, runPass, work, seriesDir: folder, bootstrap: true, afterAttempt, signal, now: systemClock });
  };
}

async function main(argv) {
  let values;
  try { values = parseArgs({ args: argv, options: OPTIONS, allowPositionals: false }).values; } catch (e) { say(`usage error: ${e.message}`); return 4; }
  const home = os.homedir();
  const seriesDir = values.series;
  const subjects = values.series ? subjectsOf({ values, seriesDir }) : [];
  const problem = missingInputs({ values, subjects });
  if (problem) { say(problem); return 4; }
  const stamp = systemClock().toISOString().slice(0, 10).replaceAll('-', '');
  const folder = path.join(exploreRoot(home), `measure-f1s2-${stamp}`);
  const start = measureGuard({ freeBytes: freeBytesOf(exploreRoot(home)), folderBytes: 0 });
  if (!start.go) { say(`the measurement does not start: ${start.reason}`); return 4; }
  return measureIn({ values, home, folder, subjects });
}

async function measureIn({ values, home, folder, subjects }) {
  const explore = loadExplore();
  const task = explore.ok ? resolveTask(explore.value, values.task) : explore;
  if (!task.ok) { say(task.reason); return 4; }
  mkdirSync(folder, { recursive: true, mode: 0o700 });
  const lock = acquireSeriesLock({ seriesDir: folder });
  if (!lock.ok) { say(lock.reason); return 4; }
  const ctl = new AbortController();
  process.on('SIGINT', () => ctl.abort());
  process.on('SIGTERM', () => ctl.abort());
  try {
    return await withParts({ values, home, folder, subjects, task: task.value, signal: ctl.signal });
  } finally {
    lock.value.release();
  }
}

async function withParts({ values, home, folder, subjects, task, signal }) {
  const heldout = loadHeldout(home, task);
  const calibration = loadCalibration(home, task);
  const world = await loadWorld({ home, work: path.join(folder, 'private'), task });
  const failed = [heldout, calibration, world].find(x => !x.ok);
  if (failed) { say(failed.reason); return 4; }
  const noisy = await quietProblem({ home, folder, acceptNoisy: values['accept-noisy'] ?? null, signal });
  if (noisy) return 3;
  const io = { freeBytes: () => freeBytesOf(exploreRoot(home)), folderBytes: () => dirBytes(folder), say };
  const r = await runMeasurement({ folder, subjects, score: scoreFor({ world: world.value, heldout: heldout.value, calibration: calibration.value, folder, signal }), io });
  say(r.message);
  if (r.growth) say(`growth: ${JSON.stringify(r.growth)}`);
  return r.exit;
}

/** Re-execute under `taskset -c 6,7` unless the marker says this IS the re-executed process. */
function reexec() {
  const child = spawn('taskset', ['-c', '6,7', process.execPath, ...process.argv.slice(1)], { env: { ...process.env, EXPLORE_PINNED: '1' }, stdio: 'inherit' });
  for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => child.kill(sig));
  child.on('exit', (code, signal) => process.exit(code ?? (signal === 'SIGINT' ? 130 : 143)));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.env.EXPLORE_PINNED === '1') process.exitCode = await main(process.argv.slice(2));
  else reexec();
}
