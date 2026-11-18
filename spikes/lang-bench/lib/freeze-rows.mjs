// The `Freeze tag` row of a spec's header table (FORMAT 1.2) and the one edit the freeze commit makes (F2.S2 (d)): the row's value becomes `` `spec-frozen-B-<yyyymmdd>` ``, every other byte stays. The header table is read by the
// conformance checker's own readers (`parseSpec`, `headerRows`); the tag's form is the checker's pattern (`FREEZE_TAG_VALUE`), narrowed to group B and to a real calendar day.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { readFacts } from './conformance-facts.mjs';
import { FREEZE_TAG_VALUE, headerRows, isRealDate, laterForm } from './conformance-check-structure.mjs';
import { parseSpec } from './conformance-spec.mjs';
import { parseFormat } from './format-structure.mjs';

export const TAG_LABEL = 'Freeze tag';
export const GATE_LABEL = 'Gate verdict';
const B_TAG = /^spec-frozen-B-(\d{8})$/;
const ROW = /^(\|\s*Freeze tag\s*\|\s*)(.*?)(\s*\|\s*)$/;

/** The date (`yyyymmdd`) of a freeze tag's name when it is `spec-frozen-B-<a real day>`, else null. */
export function tagDate(name) {
  const date = B_TAG.exec(name)?.[1];
  return date !== undefined && isRealDate(date) ? date : null;
}

/** The checker's own pattern accepts the name written as a row value (the one pattern, `FREEZE_TAG_VALUE`); this is the one place that says so. */
export const isCheckerTag = name => FREEZE_TAG_VALUE.test(`\`${name}\``);

/** ok(facts): what FORMAT's bytes state about the header (placeholders, verdicts), read by the checker's own reader. */
export function factsOf(formatBytes) {
  const parsed = parseFormat(formatBytes);
  if (!parsed.ok) return fail(`FORMAT cannot be read: line ${parsed.line}: ${parsed.reason}`);
  return readFacts(parsed.format);
}

/** ok({ no, value, lines, spec, rows }) for a spec's bytes: the line number of its `Freeze tag` row and the value of it, the lines of the file and the header rows. */
export function readRows(bytes) {
  const spec = parseSpec(bytes);
  if (!spec.ok) return fail(`line ${spec.line}: ${spec.reason}`);
  const header = headerRows(spec.value);
  const row = header.rows.find(entry => entry.label === TAG_LABEL);
  if (row === undefined) return fail('the header table has no Freeze tag row');
  return ok({ no: row.no, value: row.value, lines: bytes.toString('utf8').split('\n'), rows: header.rows });
}

/** The row's value as a tag name when it is a code span `` `spec-frozen-B-<date>` ``, else null. */
export const tagOfValue = value => /^`([^`]+)`$/.exec(value)?.[1] ?? null;

/** ok(Buffer): the spec with its `Freeze tag` row's value replaced by `` `<tag>` `` and every other byte kept (the cell's padding included). */
export function withTag(bytes, tag) {
  const read = readRows(bytes);
  if (!read.ok) return read;
  const line = read.value.lines[read.value.no - 1];
  const matched = ROW.exec(line);
  if (matched === null) return fail(`line ${read.value.no} is not a Freeze tag row of the form | Freeze tag | <value> |`);
  const lines = [...read.value.lines];
  lines[read.value.no - 1] = `${matched[1]}\`${tag}\`${matched[3]}`;
  return ok(Buffer.from(lines.join('\n'), 'utf8'));
}

/** The 1-based numbers of the lines that differ between two texts, or null when they have a different number of lines (the whole file then differs). */
export function differingLines(before, after) {
  const [left, right] = [before.toString('utf8').split('\n'), after.toString('utf8').split('\n')];
  if (left.length !== right.length) return null;
  return left.flatMap((line, index) => (line === right[index] ? [] : [index + 1]));
}

/** True when `value` (a row's value) is the Gate verdict's later form (a verdict and a date), by FORMAT's own statement of it. */
export const gateFilled = (value, facts) => laterForm(GATE_LABEL, facts)(value);

/** True when `value` is the Freeze tag row's placeholder, by FORMAT's own statement of it. */
export const freezePlaceholder = (value, facts) => value === facts.header.placeholders[TAG_LABEL];
