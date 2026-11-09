// The command line of `specs-record.mjs`, as a function a test can call: `runSpecsRecord(argv, { env, inputs, commit })` -> `{ exit, stdout, stderr }`. It writes nothing but what the arguments ask for.
//   (no flag)                                              write inputs/specs.json from the writers' files
//   --check                                                exit 1 when inputs/specs.json differs from the assembly, change nothing
//   --format-entry B [--task-repo <path>]                  write the entry B of inputs/specs.d/format.json, computed from FORMAT's bytes at the pinned commit of the task repository, then inputs/specs.json
//   --format-entry B [--task-repo <path>] --check          compute the entry again and report every field that differs, change nothing
// The task repository is --task-repo, else LANG_V2_TASK_REPO, else a refusal. Exit 0 done / equal; 1 --check found a difference; 2 a refusal (a writer's file is unreadable, claims a taken key or does not belong; the
// arguments are wrong; the task repository is missing, shallow or without the pinned commit): nothing is written.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { computeEntry, entryDifferences, readEntries, renderRecord, writeAtomicFile } from './conformance-record.mjs';
import { assemble, checkRecord } from './specs-record.mjs';
import { PINNED_TASK_SHA } from './task-repo.mjs';

export const INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const OPTIONS = { check: { type: 'boolean' }, 'format-entry': { type: 'string' }, 'task-repo': { type: 'string' } };
const USAGE = 'usage: specs-record.mjs [--check] | --format-entry <B> [--task-repo <path>] [--check]';
const done = (exit, stdout, stderr) => ({ exit, stdout, stderr });
const refusal = reason => done(2, '', `specs-record: ${reason}\n`);
const formatFile = inputs => path.join(inputs, 'specs.d', 'format.json');

function parse(argv) {
  try {
    return ok(parseArgs({ args: argv, options: OPTIONS, strict: true, allowPositionals: false }).values);
  } catch (error) {
    return fail(`${error.message}; ${USAGE}`);
  }
}

const checkedAssembly = inputs => {
  const verdict = checkRecord(inputs);
  return verdict.ok ? done(0, '', '') : done(1, '', `specs-record: ${verdict.reason}\n`);
};

function writtenAssembly(inputs, text) {
  const written = writeAtomicFile(path.join(inputs, 'specs.json'), text, inputs);
  return written.ok ? done(0, `specs-record: wrote inputs/specs.json from ${text.length} bytes of writers\n`, '') : refusal(written.reason);
}

/** The assembly of the writers' files checked (`--check`) or written. */
function assembly({ inputs, checking }) {
  const built = assemble(inputs);
  if (!built.ok) return refusal(built.reason);
  return checking ? checkedAssembly(inputs) : writtenAssembly(inputs, built.text);
}

/** `--format-entry --check`: every field of the entry that differs from the computed one, and the assembly held equal to its writers. */
function checkedEntry({ label, record, entry, inputs }) {
  const assembled = checkRecord(inputs);
  const problems = [...entryDifferences(record[label], entry).map(line => `entry ${label}: ${line}`), ...(assembled.ok ? [] : [assembled.reason])];
  return problems.length === 0 ? done(0, `specs-record: entry ${label} is what FORMAT's bytes give\n`, '') : done(1, '', problems.map(line => `specs-record: ${line}\n`).join(''));
}

/** Both files written, format.json first; when specs.json cannot be written, format.json is put back as it was. -> `done(...)`. */
function writeBoth({ inputs, text, specsText, entry, label }) {
  const file = formatFile(inputs);
  const before = readFileSync(file, 'utf8');
  const wrote = writeAtomicFile(file, text, inputs);
  if (!wrote.ok) return refusal(wrote.reason);
  const specs = writeAtomicFile(path.join(inputs, 'specs.json'), specsText, inputs);
  if (specs.ok) return done(0, `specs-record: entry ${label} of inputs/specs.d/format.json holds sha256 ${entry.sha256} for tasks ${entry.tasks.join(', ')}; wrote inputs/specs.json from ${specsText.length} bytes of writers\n`, '');
  writeAtomicFile(file, before, inputs);
  return refusal(`${specs.reason}; inputs/specs.d/format.json was put back as it was`);
}

/** The entry's new `format.json` text and the `specs.json` assembled from it, both made in memory; nothing is written unless the assembly succeeds. */
function writtenEntry({ label, record, entry, inputs, assembleWith }) {
  const text = renderRecord({ ...record, [label]: entry });
  const built = assembleWith(inputs, { 'format.json': text });
  return built.ok ? writeBoth({ inputs, text, specsText: built.text, entry, label }) : refusal(built.reason);
}

/** ok(record) of `format.json` once every writer's file reads (a stray or a taken key is found before anything else). */
function readRecordFile(inputs, assembleWith) {
  const built = assembleWith(inputs);
  return built.ok ? readEntries(formatFile(inputs)) : fail(built.reason);
}

/** ok({ label, record, entry, inputs }): the entry computed from FORMAT's bytes, nothing written. */
async function computedEntry({ values, env, inputs, commit, assembleWith }) {
  const record = readRecordFile(inputs, assembleWith);
  if (!record.ok) return record;
  const label = values['format-entry'];
  const entry = await computeEntry({ label, repo: values['task-repo'] ?? env.LANG_V2_TASK_REPO, commit, record: record.value });
  return entry.ok ? ok({ label, record: record.value, entry: entry.value, inputs, assembleWith }) : entry;
}

/** The entry computed (nothing is written before every refusal is past), then checked or written. */
async function formatEntry(args) {
  const computed = await computedEntry(args);
  if (!computed.ok) return refusal(computed.reason);
  return args.values.check === true ? checkedEntry(computed.value) : writtenEntry(computed.value);
}

/** `{ exit, stdout, stderr }` for the command's arguments. `env`, `inputs`, `commit` and `assemble` are seams for the tests; the commit is the task repository's pinned one. */
export async function runSpecsRecord(argv, { env = process.env, inputs = INPUTS, commit = PINNED_TASK_SHA, assemble: assembleWith = assemble } = {}) {
  const parsed = parse(argv);
  if (!parsed.ok) return refusal(parsed.reason);
  const values = parsed.value;
  if (values['format-entry'] !== undefined) return formatEntry({ values, env, inputs, commit, assembleWith });
  if (values['task-repo'] !== undefined) return refusal(`--task-repo goes with --format-entry; ${USAGE}`);
  return assembly({ inputs, checking: values.check === true });
}
