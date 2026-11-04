// The strict readers of T-L58 (E1.S9): each reads ONE figure or phrase out of FORMAT.md or out of a source file of the runner and the execution layer. A reader is a
// regular expression that tolerates whitespace, wrapping and quoting variants and still demands exactly ONE match: zero or two is the reader's own error, never a
// silent pick of one of them. A reader that cannot find its subject must say so, or the comparison it feeds would pass on nothing.
import { headingByNumber, parseFormat, sliceFrom } from '../../lib/format-structure.mjs';

const MIB = 1024 * 1024;
const ok = value => ({ ok: true, value });
const fail = reason => ({ ok: false, reason });

/** The one match of `pattern` in `text`, or a reader error naming `what` and how many matches there were. */
function exactlyOne(text, pattern, what) {
  const found = [...text.matchAll(new RegExp(pattern.source, `${pattern.flags.replace('g', '')}g`))];
  return found.length === 1 ? ok(found[0]) : fail(`${what}: ${found.length} matches, exactly one is required`);
}

/** The text of the subsection with this number (`'2.2'`) of a FORMAT, or a reader error. */
export function subsection(formatText, number) {
  const parsed = parseFormat(Buffer.from(formatText));
  if (!parsed.ok) return fail(`FORMAT is not read: line ${parsed.line}: ${parsed.reason}`);
  const heading = headingByNumber(parsed.format, number);
  if (heading === undefined) return fail(`FORMAT has no subsection ${number}`);
  const { start, end } = sliceFrom(parsed.format, heading, 3);
  return ok(parsed.format.text.slice(start, end));
}

/** `read` over the text of subsection `number`: the reader's value, or the error of the section or of the reader. */
function inSubsection(formatText, number, read) {
  const section = subsection(formatText, number);
  return section.ok ? read(section.value) : section;
}

const mibFigure = (text, lead, what) => {
  const found = exactlyOne(text, new RegExp(String.raw`${lead}\s+writes?\s+more\s+than\s+\`?(\d+)\`?\s*MiB\`?\s+to\s+standard\s+error`), what);
  return found.ok ? ok(Number(found.value[1]) * MIB) : found;
};

/** F-STDERR's figure in 2.2, in bytes (`a run that writes more than 256 MiB to standard error`). */
export const formatStderrSentence = formatText => inSubsection(formatText, '2.2', text => mibFigure(text, String.raw`a\s+run\s+that`, 'F-STDERR in 2.2'));

/** The figure of 4.6, in bytes (`A program that writes more than 256 MiB to standard error`). */
export const formatStderrBound = formatText => inSubsection(formatText, '4.6', text => mibFigure(text, String.raw`A\s+program\s+that`, '4.6 standard error bound'));

/** F-ENV's `TZ` value in 2.2 (`` `TZ` is `UTC` ``). */
export const formatTz = formatText => inSubsection(formatText, '2.2', text => {
  const found = exactlyOne(text, /`TZ`\s+is\s+`([^`]+)`/, 'F-ENV TZ in 2.2');
  return found.ok ? ok(found.value[1]) : found;
});

/** F-PROCS's figure in 2.2 (`at most 512 processes and threads together`). */
export const formatProcs = formatText => inSubsection(formatText, '2.2', text => {
  const found = exactlyOne(text, /may\s+hold\s+at\s+most\s+`?(\d+)`?\s+processes\s+and\s+threads\s+together/, 'F-PROCS in 2.2');
  return found.ok ? ok(Number(found.value[1])) : found;
});

const ENV_CLAUSES = Object.freeze({
  settable: /a\s+variable\s+the\s+base\s+environment\s+\(F-ENV\)\s+does\s+not\s+have,\s+or\s+`([A-Za-z_][A-Za-z0-9_]*)`/,
  anySpelling: /No\s+other\s+variable\s+of\s+the\s+base\s+environment,\s+in\s+any\s+spelling\s+of\s+its\s+case/,
  heap: /and\s+not\s+the\s+per-test\s+setting\s+by\s+which\s+the\s+test\s+machine\s+caps\s+a\s+runtime's\s+heap,\s+may\s+be\s+set/,
});

/** What 4.2 says an `env` line may set: the one name besides the variables the base environment does not have; it also checks that the two refusal clauses are there (once each). */
export const formatEnvSettable = formatText => inSubsection(formatText, '4.2', text => {
  const found = Object.entries(ENV_CLAUSES).map(([clause, pattern]) => [clause, exactlyOne(text, pattern, `4.2 env clause "${clause}"`)]);
  const broken = found.find(([, read]) => !read.ok);
  return broken === undefined ? ok(found.find(([clause]) => clause === 'settable')[1].value[1]) : broken[1];
});

/** The product of the integers of `STDERR_KILL_BYTES = 256 * 1024 * 1024` (one assignment, `*` between integers, underscores allowed) in rs_exec.py. */
export function sourceStderrKill(text) {
  const found = exactlyOne(text, /^STDERR_KILL_BYTES\s*=\s*(\d[\d_]*(?:\s*\*\s*\d[\d_]*)*)\s*(?:#.*)?$/m, 'STDERR_KILL_BYTES in rs_exec.py');
  return found.ok ? ok(found.value[1].split('*').map(part => Number(part.replaceAll('_', '').trim())).reduce((a, b) => a * b, 1)) : found;
}

/** The `TZ` of the layer's base environment in env.mjs (`TZ: 'UTC'`, either quote). */
export function sourceBaseTz(text) {
  const found = exactlyOne(text, /^\s*TZ\s*:\s*(['"])([^'"]*)\1\s*,?\s*$/m, 'TZ in the base environment of env.mjs');
  return found.ok ? ok(found.value[2]) : found;
}

/** The figure written to the scope's `prog/pids.max` in scope-entry.sh: `echo 512 > "$cg/prog/pids.max"`, the path quoted either way or not. */
export function sourcePidsMax(text) {
  const found = exactlyOne(text, /^\s*echo\s+(\d+)\s*>\s*(['"]?)\$\{?cg\}?\/prog\/pids\.max\2\s*$/m, 'pids.max in scope-entry.sh');
  return found.ok ? ok(Number(found.value[1])) : found;
}

/** The two values compared: `{ ok: true }`, `{ ok: false, kind: 'reader', reason }` (a reader could not find its subject) or `{ ok: false, kind: 'mismatch', reason }`. */
export function compareReads(label, formatRead, sourceRead) {
  for (const read of [formatRead, sourceRead]) if (!read.ok) return { ok: false, kind: 'reader', reason: read.reason };
  return formatRead.value === sourceRead.value ? { ok: true } : { ok: false, kind: 'mismatch', reason: `${label}: FORMAT says ${formatRead.value}, the source says ${sourceRead.value}` };
}

/**
 * The 4.2 mapping, read against behaviour: every key in `keys` except the one name FORMAT lets a scenario set is refused by `boxEnv`, and that name is accepted.
 * `boxEnv` is the layer's own function (the test passes the real one; a stand-in that changes its behaviour must turn this red). `keys` come from the layer's own exports.
 */
export function compareEnvRefusals(settableRead, keys, boxEnv, dirs) {
  if (!settableRead.ok) return { ok: false, kind: 'reader', reason: settableRead.reason };
  const settable = settableRead.value;
  const wrong = [];
  const attempt = key => {
    try {
      boxEnv({ dirs, extra: { [key]: 'x' } });
      return true;
    } catch {
      return false;
    }
  };
  if (!attempt(settable)) wrong.push(`${settable} is refused, FORMAT lets a scenario set it`);
  for (const key of keys) if (key.toLowerCase() !== settable.toLowerCase() && attempt(key)) wrong.push(`${key} is accepted, FORMAT says an env line may not set it`);
  return wrong.length === 0 ? { ok: true } : { ok: false, kind: 'mismatch', reason: wrong.join('; ') };
}
