// What the generators of the pre-registration share (F3.S1 (b), PR 5): every file is written WHOLE and ATOMICALLY (to a sibling file, then renamed over the target; a failed write leaves the target as it was and removes the sibling),
// and `--check` writes nothing: it exits 1 when the committed bytes are not what the generator would write, 0 when they are. A generator is `build(flags) -> ok(text) | ok({ text, siblings: [{ file, text }] }) | fail(reason)`; `runGenerator` does the rest.
// Exit codes: 0 written, or checked and equal | 1 `--check` and the committed file differs (or is absent) | 4 usage | 5 the input cannot be read or lacks what the generator reads.
import { existsSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const EXIT = Object.freeze({ done: 0, differs: 1, usage: 4, input: 5 });

/** `ok(values)` of the flags (`out`, `check` and the generator's own `extra`), or `fail(usage error)`. */
export function parseFlags(argv, extra = {}) {
  try {
    return ok(parseArgs({ args: argv, options: { out: { type: 'string' }, check: { type: 'boolean' }, ...extra }, allowPositionals: false }).values);
  } catch (e) {
    return fail(`usage error: ${e.message}`);
  }
}

/** Writes `text` to `file` whole and atomically: -> `ok(file)` | `fail`. The sibling is removed when the write or the rename fails. */
export function writeAtomically(file, text) {
  const sibling = path.join(path.dirname(file), `.${path.basename(file)}.writing-${process.pid}`);
  try {
    writeFileSync(sibling, text);
    renameSync(sibling, file);
    return ok(file);
  } catch (e) {
    rmSync(sibling, { force: true });
    return fail(`cannot write ${file}: ${e.code ?? e.message}`);
  }
}

/** The committed text of `file`, or `null` when it is not there. */
export const committedText = file => (existsSync(file) ? readFileSync(file, 'utf8') : null);

/** `--check`: -> the exit code and the line to say. */
function checked(file, text) {
  const committed = committedText(file);
  if (committed === text) return { code: EXIT.done, line: `check ok ${file}` };
  return { code: EXIT.differs, line: committed === null ? `check: ${file} is absent: the generator would write it` : `check: ${file} is not what the generator would write (${committed.length} bytes committed, ${text.length} would be written)` };
}

/** Says `line` and answers `code`. */
export function said(say, line, code) {
  say(line);
  return code;
}

/** `--check` of a generator's text against the committed file: -> the exit code, the line said. */
function checkRun({ out, text, say }) {
  const result = checked(out, text);
  return said(say, result.line, result.code);
}

/** The text of a generator's answer: the answer itself, or its `text` when it also has `siblings`. */
const textOfBuilt = value => (typeof value === 'string' ? value : value.text);
/** The files a generator writes beside its own file (`[{ file, text }]`): the page a price file was read from. They are written first, so a file that names them never exists without them. */
const siblingsOfBuilt = value => (typeof value === 'string' ? [] : value.siblings ?? []);

/** Writes every sibling and then the generator's text, each whole and atomically: -> the first `fail` or `ok`. */
function writtenAll({ out, built }) {
  const siblings = siblingsOfBuilt(built).map(({ file, text }) => writeAtomically(file, text));
  return siblings.find(r => !r.ok) ?? writeAtomically(out, textOfBuilt(built));
}

/** Writes a generator's text whole and atomically (and its siblings): -> the exit code, the line said. */
function writeRun({ out, built, say }) {
  const written = writtenAll({ out, built });
  return said(say, written.ok ? `written ${out}` : written.reason, written.ok ? EXIT.done : EXIT.input);
}

/** Where the file is: `--out`, else the default (a path, or a function of the flags). */
const outOf = (values, defaultOut) => values.out ?? (typeof defaultOut === 'function' ? defaultOut(values) : defaultOut);

/** Runs one generator over `argv`: `build({ ...flags, out })` gives the text of its file (`ok(text)`) or a `fail` (exit 5); `defaultOut` is where the file lives unless `--out` says otherwise (a path, or a function of the flags). -> the exit code. */
export async function runGenerator({ argv, extra = {}, build, defaultOut, say }) {
  const flags = parseFlags(argv, extra);
  if (!flags.ok) return said(say, flags.reason, EXIT.usage);
  const out = outOf(flags.value, defaultOut);
  const built = await build({ ...flags.value, out });
  if (!built.ok) return said(say, built.reason, built.exit ?? EXIT.input);
  return flags.value.check ? checkRun({ out, text: textOfBuilt(built.value), say }) : writeRun({ out, built: built.value, say });
}
