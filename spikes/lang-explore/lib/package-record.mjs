// The private records of a package run (F2.S1, section 3, and the plan round's finding 4): `~/rsd/lang-v2/explore/package-NN/<yyyymmddTHHMMSSZ>-<gen>/` holds `record.json` (a few KiB, written ONCE at the end of a complete run), one file per
// finished step (`step-<name>.json`, written once: the reference rows, a witness's record with every pass's failing set and its cost), `halt-<n>.json` for a resumable halt, and the scoring folders `<step>/` (the attempt files of one scoring,
// whole, written once; the built trees are removed when a scoring ends). The folders are APPEND-ONLY and never pruned (the repository's rule: results are only added): expect tens of runs per task, each well under 1 MiB, so a task's whole
// folder stays in the low tens of MiB. They hold case ids and trap names: the leak checks do not read them and nothing publishes from them.
//
// A run that is killed leaves a folder with no `record.json`; the next run names it `incomplete` in its own record and never reads it as a result (its scoring trees are removed). A run that HALTS (exit 3) writes `halt-<n>.json` and the next
// run of the same material RESUMES that folder: it re-reads the finished steps and the attempt files and runs only what is missing. Owned temporary folders (the assembled trees) of a dead run are swept at the start of a run.
//
// S, the largest expected reply body of a suite, is kept apart from registration in `package-NN/s.json` (the plan round's finding 2): one entry per suite tree hash, appended and never dropped.
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync } from 'node:fs';
import path from 'node:path';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { exploreRoot } from './paths.mjs';
import { publishNamed, publishOnce } from './publish-once.mjs';
import { stampOf } from './repartition-store.mjs';
import { removeArea } from './score.mjs';
import { OWNED_TOKEN } from './score-quick.mjs';

export const RECORD_FILE = 'record.json';
export const packageRoot = (home, task) => path.join(exploreRoot(home), `package-${task.id}`);

const RUN_NAME = /^\d{8}T\d{6}Z-[0-9a-f]{4,}$/;
const readJson = file => { try { return JSON.parse(readFileSync(file, 'utf8')); } catch { return null; } };
const halts = dir => readdirSync(dir).filter(n => /^halt-\d+\.json$/.test(n)).sort();
const runFolders = root => (existsSync(root) ? readdirSync(root).filter(n => RUN_NAME.test(n)).sort() : []);

/** The folders of earlier runs with no `record.json`: `[{ name, halts: [file] }]`, oldest first. */
export const incompleteRuns = root => runFolders(root).filter(n => !existsSync(path.join(root, n, RECORD_FILE))).map(name => ({ name, halts: halts(path.join(root, name)) }));

/** Remove what a scoring that never ended left in a run folder: its built tree, its case folder and the pass child's files (all reproducible). The attempt files stay. */
function removeScoringTrees(dir) {
  for (const sub of readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory())) {
    ['tree', 'cases', 'child'].forEach(d => removeArea(path.join(dir, sub.name, d)));
    readdirSync(path.join(dir, sub.name)).filter(n => n.startsWith('build-')).forEach(n => removeArea(path.join(dir, sub.name, n)));
  }
}

/** Does the halted folder `name` belong to this material and to a run that has the step it halted in? A halt file names the sha256 of the material it stopped over and the step (`--only suite` has no reference step, so it never resumes a halt in one). */
function resumable({ root, run, materialSha, steps }) {
  const halt = run.halts.length > 0 ? readJson(path.join(root, run.name, run.halts.at(-1))) : null;
  return halt !== null && halt.materialSha === materialSha && steps.includes(halt.step);
}

/** A halted run of this material may still be resumed by a run that has its step, so its built trees stay; a killed run's (or another material's) are removed. */
const keepsTrees = ({ root, run, materialSha }) => run.halts.length > 0 && readJson(path.join(root, run.name, run.halts.at(-1)))?.materialSha === materialSha;

/**
 * Open the run: the newest incomplete folder with a halt of THIS material is resumed; every other incomplete folder is named in `incomplete` (not read) and its scoring trees are removed; else a new folder is made.
 * -> `ok({ dir, name, resumed, incomplete: [names] })`. `genId` and `now` are seams.
 */
export function openRun({ home, task, materialSha, steps, now = systemClock, genId = () => Math.random().toString(16).slice(2, 10) }) {
  const root = packageRoot(home, task);
  mkdirSync(root, { recursive: true, mode: 0o700 });
  const open = incompleteRuns(root);
  const back = [...open].reverse().find(run => resumable({ root, run, materialSha, steps }));
  const others = open.filter(run => run !== back).map(run => run.name);
  open.filter(run => run !== back && !keepsTrees({ root, run, materialSha })).forEach(run => removeScoringTrees(path.join(root, run.name)));
  if (back !== undefined) return ok({ dir: path.join(root, back.name), name: back.name, resumed: true, incomplete: others });
  const name = `${stampOf(now())}-${genId()}`;
  mkdirSync(path.join(root, name), { mode: 0o700 });
  return ok({ dir: path.join(root, name), name, resumed: false, incomplete: others });
}

const stepFile = (run, name) => path.join(run.dir, `step-${name}.json`);

/** The record of a finished step, or `null`. */
export const readStep = (run, name) => (existsSync(stepFile(run, name)) ? readJson(stepFile(run, name)) : null);

/** Write a finished step's record, once (`wx` by a hard link). An existing one is never replaced: the result is `ok('exists')` and the file is as it was. */
export function writeStep(run, name, body) {
  try { publishOnce({ file: stepFile(run, name), text: `${JSON.stringify(body, null, 2)}\n` }); return ok('written'); } catch (e) { if (e.code === 'EEXIST') return ok('exists'); throw e; }
}

/** Write a halt of the run, `halt-<n>.json`, once each, naming the material the run stopped over. */
export function writeHalt(run, { reason, detail = reason, step, materialSha }) {
  return publishNamed({ dir: run.dir, names: n => `halt-${n}.json`, text: `${JSON.stringify({ reason, detail, step, materialSha }, null, 2)}\n` });
}

/** The complete record of the run, written once at its end (a second write fails: `fail`). */
export function writeRecord(run, record) {
  try { publishOnce({ file: path.join(run.dir, RECORD_FILE), text: `${JSON.stringify(record, null, 2)}\n` }); return ok(path.join(run.dir, RECORD_FILE)); } catch (e) { if (e.code === 'EEXIST') return fail(`${RECORD_FILE} of ${run.name} exists: a record is written once`); throw e; }
}

/** The record of the newest complete run (`record.json`), or `null` (the folders without one are not results). */
export function latestRecord({ home, task }) {
  const root = packageRoot(home, task);
  const done = runFolders(root).filter(n => existsSync(path.join(root, n, RECORD_FILE)));
  return done.length === 0 ? null : readJson(path.join(root, done.at(-1), RECORD_FILE));
}

// ---- S ----------------------------------------------------------------------------------------------------------------------------------------------

const sFile = (home, task) => path.join(packageRoot(home, task), 's.json');

/** The S counts of a task: `[{ suiteTreeSha256, samplesTreeSha256, sBytes, countedOn }]`, oldest first (a file that is not there or not valid is none). */
export const sCountsOf = ({ home, task }) => (readJson(sFile(home, task))?.counts ?? []);

/** The S counted for the suite whose tree hash is `treeSha256`, or `null`: an S recorded for an older suite does not count. */
export const sOfSuite = ({ home, task, treeSha256 }) => sCountsOf({ home, task }).find(c => c.suiteTreeSha256 === treeSha256)?.sBytes ?? null;

/** Record S for a suite once: appended to `package-NN/s.json` (the whole file replaced, every earlier entry kept); a suite already counted is a no-op. -> `ok('recorded' | 'unchanged')`. */
export function recordS({ home, task, treeSha256, samplesTreeSha256 = null, sBytes, now = systemClock }) {
  if (sOfSuite({ home, task, treeSha256 }) !== null) return ok('unchanged');
  const counts = [...sCountsOf({ home, task }), { suiteTreeSha256: treeSha256, samplesTreeSha256, sBytes, countedOn: now().toISOString().slice(0, 10) }];
  mkdirSync(packageRoot(home, task), { recursive: true, mode: 0o700 });
  const next = `${sFile(home, task)}.next`;
  rmSync(next, { force: true });
  publishOnce({ file: next, text: `${JSON.stringify({ counts }, null, 2)}\n` });
  renameSync(next, sFile(home, task));
  return ok('recorded');
}

// ---- the owned temporary trees of a dead run -----------------------------------------------------------------------------------------------------------

const owned = ownedFor({ token: OWNED_TOKEN });
const stateOf = file => owned.ownerState(owned.readOwner(file, 'folder'), undefined, { nameOwner: owned.classifyName(path.basename(file)).owner, kernelMine: owned.isMine(file) });

/**
 * The assembled trees of earlier runs (`testsToDelete-<owner>-bench-tree-<id>` in `parent`, with their claims): `{ removed, live, unknown }` (paths). A folder is removed, with its claim, only when its owner is `gone`; a live run's
 * folder and one whose owner cannot be told are left exactly as they are. Run at the start of every run.
 */
export function sweepOwnedTrees({ parent = owned.tempRoot() } = {}) {
  const prefix = owned.ownedPrefix('tree');
  const found = readdirSync(parent).filter(n => n.startsWith(prefix) && !n.endsWith('.owner')).map(n => path.join(parent, n)).filter(f => lstatSync(f).isDirectory());
  const states = found.map(file => [file, stateOf(file)]);
  const removed = states.filter(([, st]) => st === 'gone').map(([f]) => f);
  for (const f of removed) { rmSync(f, { recursive: true, force: true }); owned.removeClaim(f); }
  return { removed, live: states.filter(([, st]) => st === 'live').map(([f]) => f), unknown: states.filter(([, st]) => !['gone', 'live'].includes(st)).map(([f]) => f) };
}
