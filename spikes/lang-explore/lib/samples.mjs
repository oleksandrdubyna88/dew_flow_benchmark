// The visible samples of an olympiad spec, read from its section 6 table (FORMAT 3.4; plan D-O4): `| Id | Standard input | Expected standard output |`, one row per sample, each of the two byte cells ONE code span,
// every line feed shown as `⏎`, a `|` written `\|`, a cell with a backtick or a margin space in two-backtick form. The generator refuses a table it cannot parse: no guess, no partial list. Pure: text in, `ok(samples)` | `fail` out.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const LF_MARK = '⏎'; // the symbol the spec uses for a line feed
const HEADER = '| Id | Standard input | Expected standard output |';
const SEPARATOR = '|---|---|---|';
const PIPE_OR = String.raw`(?:\\\||[^|`; // inside a span: `\|` stands for one pipe, any other character but a bare pipe stands for itself
const ONE_TICK = `\`${PIPE_OR}\`])+\``;
const TWO_TICKS = `\`\` ${PIPE_OR}\`]|\`(?!\`))+ \`\``;
const CELL = `(?:${TWO_TICKS}|${ONE_TICK})`;
const ROW = new RegExp(`^\\| \`(s\\d\\d)\` \\| (${CELL}) \\| (${CELL}) \\|$`);
const SHOWABLE = /^[\x20-\x7e⏎]*$/u;

const idOf = i => `s${String(i + 1).padStart(2, '0')}`;

/** The lines of section 6 of the spec (`## 6. ...` up to the next `## ` heading), or `[]`. */
function section6(spec) {
  const lines = spec.split('\n');
  const from = lines.findIndex(l => /^## 6\. /.test(l));
  if (from < 0) return [];
  const to = lines.findIndex((l, i) => i > from && /^## /.test(l));
  return lines.slice(from + 1, to < 0 ? lines.length : to);
}

/** The data lines after the separator, up to the first line that is not a table line. */
function dataLines(lines, at) {
  const rest = lines.slice(at + 2);
  const end = rest.findIndex(l => !l.startsWith('|'));
  return end < 0 ? rest : rest.slice(0, end);
}

const headerCount = spec => spec.split('\n').filter(l => l === HEADER).length;

/** Where the header line sits among the lines of section 6, or -1 when it is absent or not followed by the separator. */
const headerAt = lines => { const at = lines.indexOf(HEADER); return at >= 0 && lines[at + 1] === SEPARATOR ? at : -1; };

/** The table's data lines: `ok([line, ...])` | `fail`. The header must occur once in the whole spec, inside section 6, and be followed by the separator. */
function tableLines(spec) {
  if (headerCount(spec) > 1) return fail('the spec holds more than one samples table: section 6 shows the visible samples once');
  const lines = section6(spec);
  const at = headerAt(lines);
  if (at < 0) return fail(`no samples table (\`${HEADER}\` and \`${SEPARATOR}\`) in section 6 of the spec`);
  const rows = dataLines(lines, at);
  return rows.length > 0 ? ok(rows) : fail('the samples table has no sample row');
}

/** One code span's bytes: the span's content without its delimiters (and the margin space of the two-backtick form), `\|` read as `|`, `⏎` as a line feed. -> `ok(text)` | `fail`. */
function decode(span, n) {
  const inner = span.startsWith('``') ? span.slice(3, -3) : span.slice(1, -1);
  return SHOWABLE.test(inner) ? ok(inner.replaceAll('\\|', '|').replaceAll(LF_MARK, '\n')) : fail(`row ${n} shows a character that is not printable ASCII, the space or the line-feed mark`);
}

/** One row -> `ok({ id, input, output })` | `fail`. */
function rowOf(line, n) {
  const m = ROW.exec(line);
  if (!m) return fail(`row ${n} cannot be read as an id and two code spans (a cell that says "<size> bytes, see samples/..." has no bytes to copy)`);
  const [input, output] = [decode(m[2], n), decode(m[3], n)];
  const bad = [input, output].find(r => !r.ok);
  return bad ?? ok({ id: m[1], input: input.value, output: output.value });
}

/** Every row read, or the first failure. */
function rowsOf(lines) {
  const samples = [];
  for (const [i, line] of lines.entries()) {
    const r = rowOf(line, i + 1);
    if (!r.ok) return r;
    samples.push(r.value);
  }
  return ok(samples);
}

/** The visible samples of a spec text: `ok([{ id, input, output }])` with ids `s01`, `s02`, ... in order | `fail`. */
export function parseSamples(spec) {
  const lines = tableLines(spec);
  const rows = lines.ok ? rowsOf(lines.value) : lines;
  if (!rows.ok) return rows;
  const wrong = rows.value.find((s, i) => s.id !== idOf(i));
  return wrong ? fail(`the sample ids must be s01, s02, ... in order, one row each (found ${rows.value.map(s => s.id).join(', ')})`) : rows;
}
