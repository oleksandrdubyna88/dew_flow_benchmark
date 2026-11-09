// The record of the FORMAT the checker may read (E1.S8 batch 3, C2): `inputs/specs.d/format.json`, `{ A: { sha256, tasks }, B: { sha256, tasks } }` as the FORMAT tools read it. The checker reads a FORMAT only when
// its raw sha256 is one of the recorded ones, so a FORMAT that changed after the gate (a deleted row, a narrowed cell) is a refusal to run, never a pass; the sha256 is printed on an `ok` line for the gate record.
// The entry that answers for a task is the one whose `tasks` list names the task AND whose sha256 is the FORMAT's (F2's `B` entry: `A` and `B` share one sha256, so the sha256 alone names no group).
import { randomBytes } from 'node:crypto';
import { readFileSync, realpathSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { extractGrammar } from '../tools/grammar-extract.mjs';

export const DEFAULT_RECORD = fileURLToPath(new URL('../inputs/specs.d/format.json', import.meta.url));
const SHA = /^[0-9a-f]{64}$/;

const parseJson = text => {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
};

const readText = file => {
  try {
    return ok(readFileSync(file, 'utf8'));
  } catch (error) {
    return fail(`the record ${file} cannot be read (${error.code ?? error.message})`);
  }
};

const entriesOf = json => Object.entries(json ?? {}).filter(([, entry]) => SHA.test(entry?.sha256 ?? ''));

const tasksOf = entry => (Array.isArray(entry.tasks) ? entry.tasks.filter(task => typeof task === 'string') : []);

/** True when `task` is a number the list holds (`04`) or a folder name that starts with it (`04-slug`); `04` never names `040-slug`. */
export const namesTask = (list, task) => typeof task === 'string' && list.some(number => task === number || task.startsWith(`${number}-`));

/** ok([[label, sha256, gate, tasks]]) from the record file: every top-level entry that holds a `sha256` of 64 hexadecimal digits (`tasks`: the strings of its list, `[]` when it has none). */
export function readRecord(file) {
  const text = readText(file);
  if (!text.ok) return text;
  const entries = entriesOf(parseJson(text.value));
  return entries.length > 0 ? ok(entries.map(([label, entry]) => [label, entry.sha256, entry.gate ?? null, tasksOf(entry)])) : fail(`the record ${file} holds no \`sha256\` of 64 hexadecimal digits`);
}

const answerOf = ([label, , gate]) => ok({ label, gate });
const labelsOf = list => list.map(([label]) => label).join(', ');

const noListNames = task => (task === undefined ? 'no task given, so no entry\'s list names one' : `the task ${task} is in no entry's list`);

const notRecorded = (entries, sha256, file) => fail(`this FORMAT (sha256 ${sha256}) is not the FORMAT the record names (${entries.map(([label, sha]) => `${label} ${sha}`).join(', ')} in ${file})`);

/** The task is in a list: the listed entry with this FORMAT's sha256 answers; a listed task whose FORMAT hash differs gets no record (a changed FORMAT is never reported as another entry's). */
function listedAnswer({ listed, sha256, task, file }) {
  const found = listed.find(([, recorded]) => recorded === sha256);
  return found === undefined ? fail(`this FORMAT (sha256 ${sha256}) is not the FORMAT the record names for the task ${task}: it is listed under ${labelsOf(listed)} in ${file}, whose sha256 is ${listed.map(([label, sha]) => `${label} ${sha}`).join(', ')}, so it gets no record`) : answerOf(found);
}

/** The task is in no list: the sha256 alone answers only when exactly one entry has it; with two or more the answer is unresolved, naming the task and the entries, never the first. */
function unlistedAnswer({ entries, sha256, task, file }) {
  const same = entries.filter(([, recorded]) => recorded === sha256);
  if (same.length === 0) return notRecorded(entries, sha256, file);
  return same.length === 1 ? answerOf(same[0]) : fail(`${noListNames(task)} (${file}), and this FORMAT (sha256 ${sha256}) is the sha256 of the entries ${labelsOf(same)}: the record cannot say which one answers for it; list the task in one entry`);
}

/**
 * ok({ label, gate }) for `task` and the FORMAT's `sha256`, else a fail naming the record (`gate` is the record's gate file, or null for a FORMAT that is recorded and not gated). The entry whose `tasks` list names
 * the task answers when its sha256 is the FORMAT's; a task in no list falls back to the sha256 alone, only when exactly one entry has it.
 */
export function recordedLabel(entries, sha256, file, task) {
  const listed = entries.filter(([, , , tasks]) => namesTask(tasks, task));
  return listed.length > 0 ? listedAnswer({ listed, sha256, task, file }) : unlistedAnswer({ entries, sha256, task, file });
}

// ---- the writer of an entry (F2.S2 (a)) ---------------------------------------------------------------------------------------------------------------
// `B` is the same FORMAT as `A` (the amended one) for the six tasks that are in no row of `A`. No value of it is typed: the sha256 is computed from FORMAT's bytes at the pinned commit of the task repository, the grammar's sha256
// through the grammar extractor, and the version and the gate are `A`'s. A repository that is missing, shallow or without the commit is a refusal; the writer never writes from a guess.

/** The entries the writer can make, with the tasks of each: a list of intended data, not a value to compute. */
export const ENTRY_TASKS = Object.freeze({ B: Object.freeze(['04', '05', '06', '07', '08', '10']) });
export const FORMAT_PATH = 'todo/tasks/FORMAT.md';
const GIT_TIMEOUT_MS = 60_000;
const refuse = reason => fail(reason, { refusal: true });

/** `git -C repo <args>` in a closed environment, no search above the repository's own folder (a plain folder inside another worktree is not that worktree): `repo` is a real path, so the ceiling is the parent of the folder git really starts in. -> ok({ stdout, stdoutBytes }) for exit 0, else a fail. */
async function git(repo, args, bytes = false) {
  const env = { PATH: '/usr/bin:/bin', HOME: '/nonexistent', GIT_CONFIG_NOSYSTEM: '1', GIT_CONFIG_GLOBAL: '/dev/null', GIT_TERMINAL_PROMPT: '0', GIT_CEILING_DIRECTORIES: path.dirname(repo) };
  const ran = await runBounded('git', ['-C', repo, ...args], { env, timeoutMs: GIT_TIMEOUT_MS, bytes });
  if (!ran.ok) return ran;
  const { code, timedOut, stdout, stdoutBytes } = ran.value;
  return code === 0 ? ok({ stdout, stdoutBytes }) : fail(timedOut ? 'git timed out' : `git exited ${code}`);
}

/** ok(the real path of the repository) | a refusal: a link is followed before git is asked, so a link to a plain folder inside a repository is that plain folder. */
function realRepo(repo) {
  try {
    return ok(realpathSync(repo));
  } catch (error) {
    return refuse(`the task repository ${repo} is not a git repository that git can read (${error.code ?? error.message})`);
  }
}

/** ok(repo) for a repository that is whole and holds `commit`, else a refusal in one sentence. */
async function wholeRepo({ repo, commit }) {
  const shallow = await git(repo, ['rev-parse', '--is-shallow-repository']);
  if (!shallow.ok) return refuse(`the task repository ${repo} is not a git repository that git can read (${shallow.reason})`);
  if (shallow.value.stdout.trim() !== 'false') return refuse(`the task repository ${repo} is shallow, so its history may lack ${commit}: use a full clone`);
  const known = await git(repo, ['rev-parse', '--verify', '--quiet', `${commit}^{commit}`]);
  return known.ok ? ok(repo) : refuse(`the task repository ${repo} does not hold the commit ${commit}`);
}

/** ok(FORMAT's bytes at `commit` of the repository) | a refusal. */
async function formatAt({ repo: given, commit }) {
  const resolved = realRepo(given);
  if (!resolved.ok) return resolved;
  const repo = resolved.value;
  const whole = await wholeRepo({ repo, commit });
  if (!whole.ok) return whole;
  const shown = await git(repo, ['show', `${commit}:${FORMAT_PATH}`], true);
  return shown.ok ? ok(shown.value.stdoutBytes) : refuse(`the commit ${commit} of ${repo} holds no ${FORMAT_PATH} that git can show (${shown.reason})`);
}

const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const isRecord = record => isObject(record) && Object.values(record).length > 0 && Object.values(record).every(isObject);

/** ok(record) from the record file: one JSON object whose every top-level value is an entry (an object). */
export function readEntries(file) {
  const text = readText(file);
  if (!text.ok) return text;
  const record = parseJson(text.value);
  return isRecord(record) ? ok(record) : refuse(`the record ${file} is not one JSON object of entries`);
}

/** The sentence for a FORMAT whose `fields` differ from entry A's: what its bytes give, what A records, and that nothing is written. */
const staleSentence = ({ commit, fields, computed, recorded }) => `FORMAT at ${commit} has ${fields.map(field => `${field} ${computed[field]}, but entry A records ${recorded[field]}`).join('; ')}: B must equal A, so the writer writes nothing`;

/** ok(the entry `label`) computed from FORMAT's `bytes`: `A`'s fields, the sha256 and the grammar's sha256 of the bytes, the tasks of the label; a refusal when the bytes are not the FORMAT `A` records. */
function entryFrom({ bytes, record, tasks, commit }) {
  const grammar = extractGrammar(bytes);
  if (!grammar.ok) return refuse(`FORMAT at ${commit} has no grammar to hash: ${grammar.reason}`);
  if (record.A === undefined) return refuse('the record has no entry A for B to equal, so the writer writes nothing');
  const computed = { sha256: sha256Of(bytes), grammarSha256: sha256Of(grammar.bytes) };
  const fields = Object.keys(computed).filter(field => record.A[field] !== computed[field]);
  return fields.length === 0 ? ok({ ...record.A, ...computed, tasks: [...tasks] }) : refuse(staleSentence({ commit, fields, computed, recorded: record.A }));
}

/** The sentence about a request that cannot be answered before git is asked, else null: an entry the writer does not make, or no task repository. */
function requestProblem({ label, repo }) {
  if (ENTRY_TASKS[label] === undefined) return `there is no entry ${JSON.stringify(label)} to write: the entries are ${Object.keys(ENTRY_TASKS).join(', ')}`;
  return repo === undefined || repo === '' ? 'the task repository is --task-repo or LANG_V2_TASK_REPO: neither is set' : null;
}

/**
 * `computeEntry({ label, repo, commit, record })` -> ok(entry) | a refusal. `record` is the parsed record file (`readEntries`). Nothing is written.
 */
export async function computeEntry({ label, repo, commit, record }) {
  const problem = requestProblem({ label, repo });
  if (problem !== null) return refuse(problem);
  const bytes = await formatAt({ repo, commit });
  return bytes.ok ? entryFrom({ bytes: bytes.value, record, tasks: ENTRY_TASKS[label], commit }) : bytes;
}

const show = value => (value === undefined ? 'absent' : JSON.stringify(value));

/** What differs between the entry the record holds and the entry computed: one sentence per field, `[]` when equal. A recorded entry that is null or not an object is `{}`: every field differs. */
export function entryDifferences(recorded, computed) {
  if (recorded === undefined) return ['the entry is not in the record'];
  const have = isObject(recorded) ? recorded : {};
  const fields = [...new Set([...Object.keys(have), ...Object.keys(computed)])];
  return fields.filter(field => show(have[field]) !== show(computed[field])).map(field => `${field} is ${show(have[field])} in the record, ${show(computed[field])} computed`);
}

const inline = value => (Array.isArray(value) ? `[${value.map(show).join(', ')}]` : show(value));
const entryText = ([label, entry]) => `  ${show(label)}: {\n${Object.entries(entry).map(([key, value]) => `    ${show(key)}: ${inline(value)}`).join(',\n')}\n  }`;

/** The record file's text: two-space indent, one field per line, a list of strings on one line, in the entries' own order (so `A` is the bytes it was), LF and a final newline. */
export const renderRecord = record => `{\n${Object.entries(record).map(entryText).join(',\n')}\n}\n`;

const defaultStatDev = folder => statSync(folder).dev;

/** The sentence when the temporary folder and the destination's folder are not on one file system (a rename across them is a copy, not atomic), else null. */
function apartProblem({ file, tmpFolder, statDev }) {
  try {
    return statDev(tmpFolder) === statDev(path.dirname(file)) ? null : `${file} cannot be written through ${tmpFolder}: that folder is on another file system than the destination's folder, and a rename across them is not atomic`;
  } catch (error) {
    return `${file} cannot be written (${error.code ?? error.message})`;
  }
}

/** The temporary file removed; a failure of that is dropped, so the error that made the removal necessary stays the one reported. */
function removeQuietly(tmp, remove) {
  try {
    remove(tmp, { force: true });
  } catch {
    // the first error is the refusal
  }
}

/**
 * Writes `text` to `file` through a new file in `tmpFolder` and a rename, so a reader never sees half a file: both folders must be on one file system (else a refusal, nothing written), and the temporary file is
 * removed on failure. `io` replaces `statDev`, `writeFileSync`, `renameSync` or `rmSync` (tests). -> ok(file) | a refusal.
 */
export function writeAtomicFile(file, text, tmpFolder, io = {}) {
  const ops = { statDev: defaultStatDev, writeFileSync, renameSync, rmSync, ...io };
  const apart = apartProblem({ file, tmpFolder, statDev: ops.statDev });
  if (apart !== null) return refuse(apart);
  const tmp = path.join(tmpFolder, `.${path.basename(file)}.${process.pid}-${randomBytes(4).toString('hex')}.tmp`);
  try {
    ops.writeFileSync(tmp, text, { flag: 'wx' });
    ops.renameSync(tmp, file);
    return ok(file);
  } catch (error) {
    removeQuietly(tmp, ops.rmSync);
    return refuse(`${file} could not be written (${error.code ?? error.message})`);
  }
}
