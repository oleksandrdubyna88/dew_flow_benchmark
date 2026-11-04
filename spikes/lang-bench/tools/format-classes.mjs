#!/usr/bin/env node
// The class checks of FORMAT.md (E1.S9, T-L56).
//
//   node spikes/lang-bench/tools/format-classes.mjs --self <FORMAT>
//       The class table of the document itself: every row names an existing heading by its whole text, with one of the five classes; every `##` section has exactly one row,
//       or, where it mixes kinds of task, each of its `###` has exactly one row (the lead text of a mixed section is `common`).
//   node spikes/lang-bench/tools/format-classes.mjs --frozen <A's FORMAT> --candidate <B's FORMAT> [--record <format.json>]
//       After the freeze of the first group of tasks the `common` and `olympiad` parts of FORMAT do not change. The protected set comes from the FROZEN file's class table only.
//       The frozen file must be the one the record names (`A.sha256` of inputs/specs.d/format.json, the one source of truth for the reference): there is no other way to name it.
//
// Exit 0 passes; 1 refuses (one line per refusal: the heading, A's class, the line, the rule); 4 usage or a format error (bytes, headings, the class table cannot be read).
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';
import { checkSelf } from '../lib/format-class-table.mjs';
import { checkFrozenCandidate } from '../lib/format-compare.mjs';

const EXIT_USAGE = 4;
const DEFAULT_RECORD = path.join(fileURLToPath(new URL('..', import.meta.url)), 'inputs', 'specs.d', 'format.json');
const OPTIONS = { self: { type: 'string' }, frozen: { type: 'string' }, candidate: { type: 'string' }, record: { type: 'string' } };
const usage = reason => ({ exit: EXIT_USAGE, lines: [`format-classes: ${reason}`, 'usage: format-classes.mjs --self <FORMAT> | --frozen <A> --candidate <B> [--record <format.json>]'] });

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
    return typeof sha === 'string' && /^[0-9a-f]{64}$/.test(sha) ? { sha } : { error: `${file} has no \`A.sha256\` (64 hexadecimal digits)` };
  } catch (error) {
    return { error: `${file} cannot be read as JSON (${error.message})` };
  }
}

/** `{ exit, lines }` for the command's arguments. */
export function run(argv) {
  const { values, error } = parse(argv);
  if (error !== undefined) return usage(error);
  const self = values.self !== undefined;
  if (self ? values.frozen !== undefined || values.candidate !== undefined || values.record !== undefined : values.frozen === undefined || values.candidate === undefined) return usage('give --self <FORMAT>, or --frozen <A> and --candidate <B>');
  if (self) {
    const bytes = readBytes(values.self);
    return bytes.error === undefined ? checkSelf(bytes.bytes) : usage(bytes.error);
  }
  const reference = recordedReference(values.record ?? DEFAULT_RECORD);
  const frozen = readBytes(values.frozen);
  const candidate = readBytes(values.candidate);
  const problem = reference.error ?? frozen.error ?? candidate.error;
  return problem === undefined ? checkFrozenCandidate({ frozen: frozen.bytes, candidate: candidate.bytes, reference: reference.sha }) : usage(problem);
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const result = run(process.argv.slice(2));
  process.stdout.write(result.exit === 0 ? `${result.lines.join('\n')}\n` : '');
  process.stderr.write(result.exit === 0 ? '' : `${result.lines.join('\n')}\n`);
  process.exit(result.exit);
}
