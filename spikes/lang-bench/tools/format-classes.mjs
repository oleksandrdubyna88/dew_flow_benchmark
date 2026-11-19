#!/usr/bin/env node
// The class checks of FORMAT.md (E1.S9, T-L56).
//
//   node spikes/lang-bench/tools/format-classes.mjs --self <FORMAT>
//       The class table of the document itself: every row names an existing heading by its whole text, with one of the five classes; every `##` section has exactly one row,
//       or, where it mixes kinds of task, each of its `###` has exactly one row (the lead text of a mixed section is `common`).
//       It also reports the restricted grammar over the unprotected parts and the whole-file rules (exit 1), so a FORMAT that would fail as a frozen reference shows it early.
//   node spikes/lang-bench/tools/format-classes.mjs --frozen <the frozen FORMAT> --candidate <the candidate FORMAT> [--record <format.json>] [--states <spec-states.json>]
//       After the freeze of the first group of tasks the `common` and `olympiad` parts of FORMAT do not change. The protected set comes from the FROZEN file's class table only.
//       The frozen file must be the one the record names, and there is no other way to name it. With no group A freeze, B's is the first real freeze (F2.S2 (d)): once
//       `inputs/specs.d/spec-states.json` exists (beside the record, or named by --states) the reference is its `freeze.formatSha256`; before it exists, `A.sha256` of the record
//       (`inputs/specs.d/format.json`). A states file that is there without a valid `freeze.formatSha256` is a format error, never a fall back to A.
//
// Exit 0 passes; 1 refuses (one line per refusal: the heading, A's class, the line, the rule); 4 usage or a format error (bytes, headings, the class table cannot be read).
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';
import { checkSelf } from '../lib/format-class-table.mjs';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';
import { selfGrammar } from '../lib/format-grammar.mjs';

const EXIT_USAGE = 4;
const DEFAULT_RECORD = path.join(fileURLToPath(new URL('..', import.meta.url)), 'inputs', 'specs.d', 'format.json');
const OPTIONS = { self: { type: 'string' }, frozen: { type: 'string' }, candidate: { type: 'string' }, record: { type: 'string' }, states: { type: 'string' } };
const SHA256_TEXT = /^[0-9a-f]{64}$/;
const STATES_NAME = 'spec-states.json';
const usage = reason => ({ exit: EXIT_USAGE, lines: [`format-classes: ${reason}`, 'usage: format-classes.mjs --self <FORMAT> | --frozen <FORMAT> --candidate <FORMAT> [--record <format.json>] [--states <spec-states.json>]'] });

function parse(argv) {
  try {
    return { values: parseArgs({ args: argv, options: OPTIONS, strict: true, allowPositionals: false }).values };
  } catch (error) {
    return { error: error.message };
  }
}

const readBytes = file => {
  try {
    return { bytes: readFileSync(file) };
  } catch (error) {
    return { error: `${file} cannot be read (${error.message})` };
  }
};

/** The record's `A.sha256`, or an error text. */
function recordedReference(file) {
  try {
    const sha = JSON.parse(readFileSync(file, 'utf8'))?.A?.sha256;
    return typeof sha === 'string' && SHA256_TEXT.test(sha) ? { sha, label: 'A' } : { error: `${file} has no \`A.sha256\` (64 hexadecimal digits)` };
  } catch (error) {
    return { error: `${file} cannot be read as JSON (${error.message})` };
  }
}

/** The frozen FORMAT's sha256 of the states file (`freeze.formatSha256`), or an error text naming the file and the field. */
function frozenReference(file) {
  try {
    const sha = JSON.parse(readFileSync(file, 'utf8'))?.freeze?.formatSha256;
    return typeof sha === 'string' && SHA256_TEXT.test(sha) ? { sha, label: 'B' } : { error: `${file} has no \`freeze.formatSha256\` (64 hexadecimal digits): the frozen FORMAT is not named` };
  } catch (error) {
    return { error: `${file} cannot be read as JSON (${error.message})` };
  }
}

/** The reference of a comparison: the states file when it was named or sits beside the record, else the record's A. `{ sha, label }` or `{ error }`. */
function referenceOf({ record, states }) {
  const file = states ?? path.join(path.dirname(record), STATES_NAME);
  return states !== undefined || existsSync(file) ? frozenReference(file) : recordedReference(record);
}

const selfWithOthers = values => values.frozen !== undefined || values.candidate !== undefined || values.record !== undefined || values.states !== undefined;
const compareMissing = values => values.frozen === undefined || values.candidate === undefined;
const badCombination = (values, self) => (self ? selfWithOthers(values) : compareMissing(values));

/** `{ exit, lines }` for FORMAT's bytes: the class table of the document itself, then its restricted grammar (the whole of `--self`, for a caller that holds the bytes). */
export function selfRunBytes(bytes) {
  const own = checkSelf(bytes);
  if (own.exit !== 0) return own;
  const grammar = selfGrammar(bytes);
  return grammar.exit === 0 ? own : grammar;
}

/** `--self <FORMAT>`: the class table of the document itself, then its restricted grammar. */
function selfRun(file) {
  const bytes = readBytes(file);
  return bytes.error === undefined ? selfRunBytes(bytes.bytes) : usage(bytes.error);
}

/** `--frozen <A> --candidate <B>`: the candidate against the frozen file the record names. */
function compareRun(values) {
  const reference = referenceOf({ record: values.record ?? DEFAULT_RECORD, states: values.states });
  const frozen = readBytes(values.frozen);
  const candidate = readBytes(values.candidate);
  const problem = reference.error ?? frozen.error ?? candidate.error;
  return problem === undefined ? checkFrozenCandidate({ frozen: frozen.bytes, candidate: candidate.bytes, reference: reference.sha, label: reference.label }) : usage(problem);
}

/** `{ exit, lines }` for the command's arguments. */
export function run(argv) {
  const { values, error } = parse(argv);
  if (error !== undefined) return usage(error);
  const self = values.self !== undefined;
  if (badCombination(values, self)) return usage('give --self <FORMAT>, or --frozen <A> and --candidate <B>');
  return self ? selfRun(values.self) : compareRun(values);
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const result = run(process.argv.slice(2));
  process.stdout.write(result.exit === 0 ? `${result.lines.join('\n')}\n` : '');
  process.stderr.write(result.exit === 0 ? '' : `${result.lines.join('\n')}\n`);
  process.exit(result.exit);
}
