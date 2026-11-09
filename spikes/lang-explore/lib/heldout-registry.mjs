// The held-out registry, `inputs/heldout-frozen.json` (F2.S1 items 3 and 4, bench PR 4a): ONE record of the sealed quick tasks, `{ "tasks": { "NN": entry } }`, counts and hashes and one relative path and one date, never a case id,
// a trap name, a case text or a key (D41). The reader of the readers (`heldout-quick.mjs`) accepts the flat layout too; the writer writes this one. An entry is written once: whole, by a temporary file of its owner and a rename, after the
// archive it names exists; an equal entry is a no-op, another one for the same task is refused (a re-seal is a new identity, F2.S1's package spec).
//
//   entry = { archive: "heldout-NN/suite.enc", archiveSha256, treeSha256, caseCount, trapCount, manifestSha256, keySha256, sealedOn }
import * as nodeFs from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { entryInRecord, entryOf, readRecord } from './heldout-quick.mjs';
import { tempNameOf, writeFlushed } from './publish-once.mjs';

export { entryInRecord, entryOf, readRecord };

const ENTRY_FIELDS = Object.freeze(['archive', 'archiveSha256', 'caseCount', 'keySha256', 'manifestSha256', 'sealedOn', 'trapCount', 'treeSha256']);

/** The entry of a seal: the fields in a fixed order, so two writers of one seal write one text. */
export const entryOfSeal = fields => Object.freeze(Object.fromEntries(ENTRY_FIELDS.map(k => [k, fields[k]])));

const sameEntry = (a, b) => ENTRY_FIELDS.every(k => a[k] === b[k]);
const tasksOf = record => (record === null ? {} : (record.tasks ?? record));
const withTask = ({ record, id, entry }) => {
  const tasks = Object.fromEntries(Object.entries({ ...tasksOf(record), [id]: entry }).sort(([a], [b]) => (a < b ? -1 : 1)));
  return { ...(record?.tasks === undefined ? {} : record), tasks };
};

/** The record file replaced by `text`: a temporary file of this process beside it, flushed, then renamed over it. A failure in the middle leaves the temporary file, which nothing reads. */
function replaceFile({ file, text, fs }) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temp = tempNameOf(file);
  writeFlushed({ temp, text, fs });
  try {
    fs.renameSync(temp, file);
  } catch (e) {
    fs.rmSync(temp, { force: true });
    throw e;
  }
}

/** The registered entry of a task: `ok(entry)` or `ok(undefined)` (none), or a `fail` when the record cannot be read (it may hold the entry, so nothing may be written over it). */
export function registeredEntry({ frozenFile, task }) {
  const record = readRecord(frozenFile);
  if (!record.ok) return fail(`the held-out record ${path.basename(frozenFile)} cannot be read (${record.reason}): no entry is written or trusted over it (task ${task.id})`);
  return ok(entryInRecord(record, task.id));
}

/** What an entry that is already there means for a new one: `ok('unchanged')` for an equal entry, a `fail` for another, null when there is none. */
function settled({ have, entry, frozenFile, task }) {
  if (have === undefined) return null;
  return sameEntry(have, entry) ? ok('unchanged') : fail(`${path.basename(frozenFile)} already holds another entry for task ${task.id}: a re-seal is a new archive and a new identity, which is not made here`);
}

/**
 * Register `entry` for `task` in the record at `frozenFile`. -> `ok('written')` | `ok('unchanged')` (an equal entry is there) | `fail` (an entry that differs is there, or the record cannot be read).
 */
export function writeEntry({ frozenFile, task, entry, fs = nodeFs }) {
  const have = registeredEntry({ frozenFile, task });
  if (!have.ok) return have;
  const done = settled({ have: have.value, entry, frozenFile, task });
  if (done !== null) return done;
  replaceFile({ file: frozenFile, text: `${JSON.stringify(withTask({ record: readRecord(frozenFile).value, id: task.id, entry }), null, 2)}\n`, fs });
  return ok('written');
}

/** `packages.json`'s entry of a task: its `archiveSha256`, or undefined (the file may be laid out as `{ tasks }`, `{ packages }` or flat). */
const packageHash = (packages, id) => (packages.tasks ?? packages.packages ?? packages)[id]?.archiveSha256;

/** `ok(parsed)` for a `packages.json` that parses, else a `fail` saying the check could not be made. */
function readPackages({ task, packagesFile }) {
  try {
    return ok(JSON.parse(nodeFs.readFileSync(packagesFile, 'utf8')) ?? {});
  } catch {
    return fail(`${path.basename(packagesFile)} cannot be read: whether it holds task ${task.id} is not known, so the archive hash is not checked against the registry`);
  }
}

const agrees = (theirs, mine) => theirs === undefined || theirs === mine;

/**
 * The sentence refusing a `packages.json` entry whose `archiveSha256` is not the registry's (`registerPackage`, F2.S1, calls this), or null: no `packages.json`, no entry for the task, or an equal one is no problem. A
 * `packages.json` that cannot be read is a refusal, never "no check".
 */
export function registryMismatchProblem({ task, packagesFile, frozenFile }) {
  if (!nodeFs.existsSync(packagesFile)) return null;
  const packages = readPackages({ task, packagesFile });
  if (!packages.ok) return packages.reason;
  const theirs = packageHash(packages.value, task.id);
  const mine = entryInRecord(readRecord(frozenFile), task.id)?.archiveSha256;
  return agrees(theirs, mine) ? null : `${path.basename(packagesFile)} records archiveSha256 ${String(theirs).slice(0, 12)} for task ${task.id} but ${path.basename(frozenFile)} records ${String(mine).slice(0, 12)}: the archive's identity is the registry's, so one of the two files is wrong`;
}
