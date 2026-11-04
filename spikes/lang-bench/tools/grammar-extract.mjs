#!/usr/bin/env node
// Extracts the grammar of FORMAT.md for every workspace's runner/GRAMMAR.md (E1.S9).
//
//   node spikes/lang-bench/tools/grammar-extract.mjs <FORMAT.md> <out>
//
// Checks FORMAT's bytes and structure (UTF-8, LF only, no byte order mark; `## 1.` to `## 7.` once each, their subsections in order), then writes <out>: one header line
//   `# Scenario and sample grammar (from FORMAT.md v<version>, sha256 <hex of FORMAT's raw bytes>)`, one empty line, and the raw text from the `## 3.` heading line to the
//   byte before the `## 6.` heading line. Nothing is normalised and the output is deterministic. <out> is written through a temporary file in its folder and linked into
//   place, so a reader never sees half a file and an existing file is never overwritten. A temporary file that cannot be removed is reported on stderr (never silently left); the exit code stays the write's.
// Exit 0 done; 4 usage or a format error (one line, naming the line). Nothing else is ever written.
import { randomBytes } from 'node:crypto';
import { linkSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { headingByNumber, parseFormat, preambleSlice, versionOf } from '../lib/format-structure.mjs';

const EXIT_FORMAT = 4;
const EXIT_USAGE = 4;
const fail = reason => ({ ok: false, reason });

/** `{ ok: true, bytes }` — the GRAMMAR.md bytes for FORMAT's bytes — or `{ ok: false, reason }`. */
export function extractGrammar(formatBytes) {
  const parsed = parseFormat(formatBytes);
  if (!parsed.ok) return fail(`FORMAT.md line ${parsed.line}: ${parsed.reason}`);
  const { format } = parsed;
  const version = versionOf(format);
  if (version === null) return fail(`FORMAT.md line 1 to ${format.text.slice(0, preambleSlice(format).end).split('\n').length}: no \`Format version: <v>\` header row`);
  const from = headingByNumber(format, '3').start;
  const to = headingByNumber(format, '6').start;
  const header = `# Scenario and sample grammar (from FORMAT.md v${version}, sha256 ${format.sha256})\n\n`;
  return { ok: true, bytes: Buffer.from(header + format.text.slice(from, to), 'utf8') };
}

const REAL_OPS = { writeFileSync, linkSync, unlinkSync };

/** Removes the temporary file: only "it was never made" (ENOENT) is silent, any other error is one line on `stderr`. */
function removeTemporary(ops, temporary, stderr) {
  try {
    ops.unlinkSync(temporary);
  } catch (error) {
    if (error.code !== 'ENOENT') stderr.write(`grammar-extract: the temporary file ${temporary} could not be removed (${error.message})\n`);
  }
}

const writeFailure = (out, error) => (error.code === 'EEXIST' ? `${out} exists: it is never overwritten` : `${out} cannot be written (${error.message})`);

/**
 * Writes `bytes` to `out` through a temporary file beside it; refuses an existing `out`. Returns an error text or null. The temporary file is removed at the end: only "it was
 * never made" (ENOENT) is silent, any other error is one line on `stderr`, and the result stays the write's.
 */
function writeNew(out, bytes, stderr, ops) {
  const temporary = `${out}.${process.pid}.${randomBytes(4).toString('hex')}.tmp`;
  try {
    ops.writeFileSync(temporary, bytes, { flag: 'wx' });
    ops.linkSync(temporary, out);
    return null;
  } catch (error) {
    return writeFailure(out, error);
  } finally {
    removeTemporary(ops, temporary, stderr);
  }
}

const isUsageError = argv => argv.length !== 2 || argv.some(arg => arg.startsWith('--'));

/** `{ bytes }` of the file, or `{ failed: true }` after one line on `stderr`. */
function readInput(file, stderr) {
  try {
    return { bytes: readFileSync(file) };
  } catch (error) {
    stderr.write(`grammar-extract: ${file} cannot be read (${error.message})\n`);
    return { failed: true };
  }
}

/** Writes the problem (if any) on `stderr` and returns the exit code. */
function finish(problem, stderr) {
  if (problem === null) return 0;
  stderr.write(`grammar-extract: ${problem}\n`);
  return EXIT_FORMAT;
}

/** The command: returns the exit code and writes its one line. */
export function main(argv, stderr = process.stderr, ops = REAL_OPS) {
  if (isUsageError(argv)) {
    stderr.write('grammar-extract: usage: grammar-extract.mjs <FORMAT.md> <out>\n');
    return EXIT_USAGE;
  }
  const input = readInput(argv[0], stderr);
  if (input.failed) return EXIT_FORMAT;
  const extracted = extractGrammar(input.bytes);
  return finish(extracted.ok ? writeNew(argv[1], extracted.bytes, stderr, ops) : extracted.reason, stderr);
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) process.exit(main(process.argv.slice(2)));
