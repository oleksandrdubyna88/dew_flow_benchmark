// The held-out side of a quick task, as the harness reads it: `~/rsd/lang-v2/explore/heldout-NN/cases/` (pairs `hNNN.in` / `hNNN.out` and `cases.txt`, FORMAT 3.1) and an optional `traps.txt` beside it. The shape is
// written for the authors in `~/rsd/lang-v2/briefs/quick-heldout-shape.md`. Nothing is written to this folder; its tree hash is recorded in the series at `init` and checked at every pair, so a pair changed between
// pairs is refused. Nothing it returns is ever written to a public file.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { taskPaths } from './tasks.mjs';

const H_ID = /^h\d{3}$/;

/** The sha256 of every file of `cases/` (names and bytes, in name order): the identity of the held-out set. */
export function casesTreeHash(casesDir) {
  const h = createHash('sha256');
  for (const name of readdirSync(casesDir).sort()) {
    h.update(`${name}\0`);
    h.update(readFileSync(path.join(casesDir, name)));
    h.update('\0');
  }
  return h.digest('hex');
}

function readTraps(dir, ids) {
  const file = path.join(dir, 'traps.txt');
  if (!existsSync(file)) return ok([]);
  const traps = readFileSync(file, 'utf8').split('\n').map(l => l.trim()).filter(l => l !== '' && !l.startsWith('#'));
  const unknown = traps.filter(t => !ids.includes(t));
  return unknown.length === 0 ? ok(traps) : fail(`traps.txt names case(s) that are not in cases/: ${unknown.join(', ')}`);
}

const PAIR_NAME = /^h\d{3}\.(in|out)$/;
const listIf = (list, text) => (list.length > 0 ? [text(list.join(', '))] : []);

function structureProblems(casesDir) {
  const names = readdirSync(casesDir);
  const stray = names.filter(n => n !== 'cases.txt' && !PAIR_NAME.test(n));
  const ids = [...new Set(names.filter(n => PAIR_NAME.test(n)).map(n => n.slice(0, 4)))].sort();
  const unpaired = ids.filter(id => !names.includes(`${id}.in`) || !names.includes(`${id}.out`));
  return { ids, problems: [...listIf(stray, l => `files that are not a pair or cases.txt: ${l}`), ...listIf(unpaired, l => `case(s) without both .in and .out: ${l}`), ...(ids.length === 0 ? ['no case h001.in / h001.out'] : [])] };
}

/** The ids `cases.txt` lists, one per line (comments and blanks left out). */
const listedIds = file => readFileSync(file, 'utf8').split('\n').map(l => l.trim()).filter(l => l !== '' && !l.startsWith('#')).map(l => l.split(/\s+/)[0]);

function manifestProblems(casesDir, ids) {
  const file = path.join(casesDir, 'cases.txt');
  if (!existsSync(file)) return ['cases.txt is missing (FORMAT 3.1: one line per case, `<id> <comparison> <R-id> ...`)'];
  const listed = listedIds(file);
  return [...listIf(ids.filter(id => !listed.includes(id)), l => `cases.txt does not list ${l}`), ...listIf(listed.filter(id => !ids.includes(id)), l => `cases.txt lists ${l}, which are not in the folder`)];
}

const isDir = p => existsSync(p) && statSync(p).isDirectory();

/** The folder of a quick task's reference solutions: `refs-NN` beside `heldout-NN` (private, outside every git tree, never bound into a box). */
export const refsDirOf = dir => path.join(path.dirname(dir), path.basename(dir).replace(/^heldout-/, 'refs-'));

/** `ok(heldout)` with `{ task, dir, casesDir, refsDir, caseIds, traps, treeSha256 }` for a quick task, or a `fail` naming what is absent or malformed. */
export function loadHeldoutQuick(home, task) {
  const dir = taskPaths(task, home).heldoutDir;
  const casesDir = path.join(dir, 'cases');
  if (!isDir(casesDir)) return fail(`the held-out side is not there (${casesDir}); its shape is in ~/rsd/lang-v2/briefs/quick-heldout-shape.md`, { absent: true });
  const shape = structureProblems(casesDir);
  const problems = [...shape.problems, ...manifestProblems(casesDir, shape.ids)];
  if (problems.length > 0) return fail(`the held-out folder ${dir} is malformed: ${problems.join('; ')}`);
  const traps = readTraps(dir, shape.ids);
  return traps.ok ? ok(Object.freeze({ task: task.id, dir, casesDir, refsDir: refsDirOf(dir), caseIds: shape.ids, traps: traps.value, treeSha256: casesTreeHash(casesDir) })) : traps;
}

/** The sentence refusing a held-out set that is not the one a series was made over, or null (a series made before the hash was recorded is not refused for lacking it). */
export function heldoutMovedProblem({ inputs, heldout }) {
  const frozen = inputs?.heldoutQuick;
  if (!frozen) return `this series recorded no held-out hash: make a new series with \`explore.mjs init\``;
  return frozen.treeSha256 === heldout.treeSha256 ? null : `the held-out cases of task ${heldout.task} changed since the series was made (${frozen.caseCount} cases then, ${heldout.caseIds.length} now): the held-out set of a series does not move between pairs; make a new series`;
}

/**
 * Run the real runner's parser over a folder of pairs (`run_samples.py --parse-only --kind <kind>`): a pair it refuses is a defect to fix BEFORE a paid pair, never a verdict. -> `ok(count)` | `fail(reasonLines)`.
 */
export async function parseProblem({ casesDir, kind, runner, python = '/usr/bin/python3' }) {
  const r = await runBounded(python, ['-I', runner, '--parse-only', '--kind', kind, casesDir], { timeoutMs: 120_000 });
  if (!r.ok) return fail(`the runner could not be started: ${r.reason}`);
  return r.value.code === 0 ? ok(r.value.stdout.trim()) : fail(`the runner refuses the held-out pairs (exit ${r.value.code}): ${(r.value.stderr || r.value.stdout).trim().split('\n').slice(0, 12).join(' | ')}`);
}
