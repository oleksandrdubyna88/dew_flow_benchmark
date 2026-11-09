// The held-out side of a quick task, as the harness reads it: `~/rsd/lang-v2/explore/heldout-NN/cases/` (pairs `hNNN.in` / `hNNN.out` and `cases.txt`, FORMAT 3.1) and an optional `traps.txt` beside it. The shape is
// written for the authors in `~/rsd/lang-v2/briefs/quick-heldout-shape.md`. Nothing is written to this folder; its tree hash is recorded in the series at `init` and checked at every pair, so a pair changed between
// pairs is refused. Nothing it returns is ever written to a public file.
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { INPUTS } from './paths.mjs';
import { taskPaths } from './tasks.mjs';

const H_ID = /^h\d{3}$/;

const lstatOrNull = file => lstatSync(file, { throwIfNoEntry: false }) ?? null;

const KIND_NAMES = Object.freeze([['isSymbolicLink', 'a link'], ['isDirectory', 'a folder'], ['isFIFO', 'a fifo'], ['isSocket', 'a socket'], ['isCharacterDevice', 'a device'], ['isBlockDevice', 'a device']]);

/** What a file system entry is, in words (a regular file is `a file`: it is not a folder where a folder is wanted). */
const kindName = stat => KIND_NAMES.find(([method]) => stat[method]())?.[1] ?? 'a file';

/** The sentence naming every entry of `cases/` that is not a regular file (a link, a folder, a fifo, a socket: by `lstat`, so nothing is followed or read), or null. */
export function caseEntriesProblem(casesDir) {
  const odd = readdirSync(casesDir).sort().map(name => [name, lstatOrNull(path.join(casesDir, name))]).filter(([, stat]) => stat !== null && !stat.isFile());
  return odd.length === 0 ? null : `entries of cases/ that are not regular files: ${odd.map(([name, stat]) => `${name} is ${kindName(stat)}`).join(', ')}`;
}

const casesRootProblem = stat => (stat === null || stat.isDirectory() ? null : `cases/ is ${kindName(stat)}, not a folder`);
const trapsFileProblem = stat => (stat === null || stat.isFile() ? null : `traps.txt is ${kindName(stat)}, not a regular file`);

/** The sentence refusing `cases/` when it is a link or not a folder, or `traps.txt` when it is there and not a regular file, or null (a missing `cases/` is the caller's `absent`). */
const rootProblem = ({ dir, casesDir }) => casesRootProblem(lstatOrNull(casesDir)) ?? trapsFileProblem(lstatOrNull(path.join(dir, 'traps.txt')));

/** The held-out set's identity: `ok(sha256)` of every file of `cases/` (names and bytes, in name order), or `fail` naming an entry that is not a regular file. The bytes hashed for a valid folder are what they always were. */
export function casesTreeHash(casesDir) {
  const problem = caseEntriesProblem(casesDir);
  if (problem !== null) return fail(problem);
  const h = createHash('sha256');
  for (const name of readdirSync(casesDir).sort()) {
    h.update(`${name}\0`);
    h.update(readFileSync(path.join(casesDir, name)));
    h.update('\0');
  }
  return ok(h.digest('hex'));
}

function readTraps(dir, ids) {
  const file = path.join(dir, 'traps.txt');
  if (lstatOrNull(file) === null) return ok([]);
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
  if (lstatOrNull(file) === null) return ['cases.txt is missing (FORMAT 3.1: one line per case, `<id> <comparison> <R-id> ...`)'];
  const listed = listedIds(file);
  return [...listIf(ids.filter(id => !listed.includes(id)), l => `cases.txt does not list ${l}`), ...listIf(listed.filter(id => !ids.includes(id)), l => `cases.txt lists ${l}, which are not in the folder`)];
}

/** `{ ids, problems }` of a present `cases/`: an entry that is not a regular file first (nothing else is read then), else the names and the manifest. */
function folderShape({ dir, casesDir }) {
  const odd = rootProblem({ dir, casesDir }) ?? caseEntriesProblem(casesDir);
  if (odd !== null) return { ids: [], problems: [odd] };
  const shape = structureProblems(casesDir);
  return { ids: shape.ids, problems: [...shape.problems, ...manifestProblems(casesDir, shape.ids)] };
}

/** The folder of a quick task's reference solutions: `refs-NN` beside `heldout-NN` (private, outside every git tree, never bound into a box). */
export const refsDirOf = dir => path.join(path.dirname(dir), path.basename(dir).replace(/^heldout-/, 'refs-'));

/** The held-out record the freeze writes (counts and hashes, F2.S2): in the bench checkout, beside the other pins. It is absent until the first freeze. */
export const FROZEN_FILE = path.join(INPUTS, 'heldout-frozen.json');

/** The record's entry of one task, in either layout the record may take (`{ tasks: { NN: entry } }` or `{ NN: entry }`): the entry, or undefined. */
export const entryOf = (record, id) => (record.tasks ?? record)[id];

/** The entry of a task in what `readRecord` gave (`ok(record)` with a record or null, or a `fail`): the entry, or undefined when there is none or the record cannot be read. */
export const entryInRecord = (record, id) => (record.ok && record.value !== null ? entryOf(record.value, id) : undefined);

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);

/** A record is an object, and its `tasks` (when it has the key) is one too: anything else is read as unreadable, never as "no task is sealed". */
const recordShapeOk = record => isObject(record) && (record.tasks === undefined || isObject(record.tasks));

/** `ok(record)` for a record file of the right shape, `ok(null)` for no file, `fail` for a file that cannot be read or has another shape (the caller refuses on it: a record that cannot be read may hide a sealed task). */
export function readRecord(file) {
  if (lstatOrNull(file) === null) return ok(null);
  try {
    const record = JSON.parse(readFileSync(file, 'utf8'));
    return recordShapeOk(record) ? ok(record) : fail('it is not an object of tasks');
  } catch { return fail('it is not valid JSON'); }
}

/**
 * The ONE decision of the fail-closed readers (F2.S2 (d)): the sentence refusing a read of a task whose clear held-out folder is absent while a sealed archive is registered for it in `heldout-frozen.json`, or null.
 * It says `sealed: open it first` and names the task and the verb, so no caller can take the refusal for an absent task, an empty one or a clean one. An archive `heldout-NN/suite.enc` with NO entry is an interrupted seal, never
 * an absent task: the sentence says how to resume it. A task with no entry and no archive, or no record file at all, keeps today's behaviour (null). A record that cannot be read is a refusal too, whenever the folder is absent
 * (it may be hiding the task's entry). An entry of any shape registers the task: the check never guesses it away.
 */
export function sealedAbsentProblem({ home, task, frozenFile = FROZEN_FILE }) {
  const dir = taskPaths(task, home).heldoutDir;
  if (lstatOrNull(path.join(dir, 'cases')) !== null) return null;
  return registeredProblem({ record: readRecord(frozenFile), task, frozenFile, archive: lstatOrNull(path.join(dir, 'suite.enc')) !== null });
}

/** The refusal of a task whose folder is absent, read from the record: `sealed: open it first` when the record lists the task, an interrupted seal when only the archive is there, a refusal when the record cannot be read, else null. */
function registeredProblem({ record, task, frozenFile, archive }) {
  if (!record.ok) return `the held-out record ${path.basename(frozenFile)} cannot be read (${record.reason}): a task whose folder is absent may be sealed in it (task ${task.id})`;
  if (entryInRecord(record, task.id) !== undefined) return `sealed: open it first (task ${task.id}): its clear held-out folder is absent and a sealed archive is registered for it; run \`explore.mjs heldout-open --task ${task.id}\``;
  return archive ? `interrupted seal: run \`package --task ${task.id} --seal\` again to resume (task ${task.id}: its archive is there and ${path.basename(frozenFile)} has no entry for it)` : null;
}

/**
 * The sentence refusing a clear folder that is not the one its archive was sealed from, or null: the registry's entry holds the tree hash of the sealed cases, and a folder changed after sealing is read as nothing.
 * A task with no entry (an authoring folder) is not refused. `heldout` is `loadHeldoutQuick`'s value.
 */
export function openedFolderProblem({ task, heldout, frozenFile = FROZEN_FILE }) {
  const entry = entryInRecord(readRecord(frozenFile), task.id);
  return entry === undefined || entry.treeSha256 === heldout.treeSha256 ? null : differsSentence({ task, frozenFile });
}

/** The sentence of a clear folder whose tree hash is not its archive's. */
export const differsSentence = ({ task, frozenFile = FROZEN_FILE }) => `the clear folder differs from its archive: changed after sealing (task ${task.id}; the tree hash is not the one in ${path.basename(frozenFile)}); nothing is removed`;

/** The refusal of a task with no clear folder: the sealed or interrupted-seal sentence of `sealedAbsentProblem` (a fail with NO `absent` flag: nothing may read it as an absent task), else today's `absent` fail. */
function absentFail({ home, task, frozenFile, casesDir }) {
  const sealed = sealedAbsentProblem({ home, task, frozenFile });
  return sealed === null ? fail(`the held-out side is not there (${casesDir}); its shape is in ~/rsd/lang-v2/briefs/quick-heldout-shape.md`, { absent: true }) : fail(sealed);
}

/** `ok(heldout)` with `{ task, dir, casesDir, refsDir, caseIds, traps, treeSha256 }` for a quick task, or a `fail` naming what is absent or malformed. A sealed task whose folder is absent is a refusal, never `absent`. */
export function loadHeldoutQuick(home, task, frozenFile = FROZEN_FILE) {
  const dir = taskPaths(task, home).heldoutDir;
  const casesDir = path.join(dir, 'cases');
  if (lstatOrNull(casesDir) === null) return absentFail({ home, task, frozenFile, casesDir });
  const shape = folderShape({ dir, casesDir });
  if (shape.problems.length > 0) return fail(`the held-out folder ${dir} is malformed: ${shape.problems.join('; ')}`);
  const traps = readTraps(dir, shape.ids);
  const tree = casesTreeHash(casesDir);
  const unreadable = [traps, tree].find(r => !r.ok);
  if (unreadable !== undefined) return unreadable;
  return ok(Object.freeze({ task: task.id, dir, casesDir, refsDir: refsDirOf(dir), caseIds: shape.ids, traps: traps.value, treeSha256: tree.value }));
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
