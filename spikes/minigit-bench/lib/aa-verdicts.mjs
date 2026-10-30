// `bare-runner.mjs aa-verdicts --series <dir>` (E3b.S4): the G table G-1..G-14 of contract section 10 and its GO definition, COMPUTED from the series' records, run-map, ledger, rehearsal-v2.json and the dated
// by-hand lines of `inputs/amendments.md`; nothing is typed. A row is `holds`, `refuted`, `explained` (G-6, G-5, G-8's refused hosts: a dated line), `recorded` (G-9, never gating) or `not exercised`. GO needs every row to
// hold, except that G-4's subagent half and G-12 may be `not exercised` WITH their replacing evidence named; a by-hand G (G-10, G-13) with no dated line is `not exercised`, is not in that list and is NO GO. A refuted
// G-2, G-8 or G-12, or alias drift (G-7), is a STOP. Reads only: the one file written is `<series>/aa-verdicts.json`, by the command.
import { mkdirSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { EXIT } from './bare-exit.mjs';
import { readSeriesFacts } from './aa-facts.mjs';
import { SESSION_ROWS } from './aa-rules.mjs';
import { g10, g12, g13, g14, g4, g7, g9 } from './aa-rules-twins.mjs';
import { ok } from './result.mjs';

export { t3Rule } from './aa-rules-twins.mjs';

const ALLOWED_NOT_EXERCISED = Object.freeze(['G-4', 'G-12']);
const PASSING = Object.freeze(['holds', 'explained', 'recorded']);

/** The rows in table order: each rule gets the context and returns its row. */
const ROWS = Object.freeze([
  SESSION_ROWS['G-1'], SESSION_ROWS['G-2'], SESSION_ROWS['G-3'], g4, SESSION_ROWS['G-5'], SESSION_ROWS['G-6'], g7, SESSION_ROWS['G-8'], g9, g10, SESSION_ROWS['G-11'], g12, g13, g14,
]);

/** A row may stand in a GO: it holds, or it is one of the two `not exercised` rows the contract lists AND it names its replacing evidence. */
const passes = row => PASSING.includes(row.verdict) || (row.verdict === 'not exercised' && ALLOWED_NOT_EXERCISED.includes(row.id) && Boolean(row.replacing));

/** The row as written: `stop` set when the row is a stop rule that is refuted; the internal marker is dropped. */
const finish = ({ stopIfRefuted, ...row }) => ({ ...row, observed: row.observed || '(nothing observed)', stop: Boolean(stopIfRefuted) && row.verdict === 'refuted', replacing: row.replacing ?? null });

/** `GO` | `NO GO` | `STOP` of the rows. */
function verdictOf(rows) {
  if (rows.some(r => r.stop)) return 'STOP';
  return rows.every(passes) ? 'GO' : 'NO GO';
}

/**
 * `{ seriesDir, inputsDir, smoke, now }` -> `ok(report)` | `fail(reason)` (no run-map). `smoke` is the committed smoke fixture's B-T1 (`{ ok, cli, fixture, observed }`; null when it cannot be read).
 * `report` = `{ seriesId, harnessIdentity, computedAt, verdict, stops, t3Rule, rows, malformed, subjects }`.
 */
export function aaVerdicts({ seriesDir, inputsDir, smoke = null, now = () => new Date() }) {
  const read = readSeriesFacts({ seriesDir, inputsDir });
  if (!read.ok) return read;
  const f = read.value;
  const ctx = { map: f.map, ledger: f.ledger, baseline: f.baseline, rehearsal: f.rehearsal, quiet: f.quiet, amendments: f.amendments, aa: f.subjects.aa, pilot: f.subjects.pilot, all: [...f.subjects.aa, ...f.subjects.pilot], smoke };
  const rows = ROWS.map(rule => finish(rule(ctx)));
  const g4Row = rows.find(r => r.id === 'G-4');
  return ok({
    seriesId: f.map.seriesId, harnessIdentity: f.map.harnessIdentity, computedAt: now().toISOString(), verdict: verdictOf(rows), stops: rows.filter(r => r.stop).map(r => r.id), t3Rule: g4Row.t3Rule,
    rows: rows.map(({ t3Rule: _t3, ...row }) => row), malformed: f.amendments.malformed, subjects: { aa: ctx.aa.map(s => s.rid), pilot: ctx.pilot.map(s => s.rid) },
  });
}

/** One row, whole: an observation or a replacing evidence is never cut (a clipped line would hide the very session or alias a refusal names). */
const rowLines = r => [`${r.id.padEnd(5)} ${r.verdict.padEnd(13)} ${r.title}`, `        ${r.observed}`, ...(r.replacing ? [`        replacing evidence: ${r.replacing}`] : [])];

/** The printed table: one line per G (and its observation), then the T3 rule, the stops, the lines that were not valid and the verdict. */
export function renderTable(report) {
  return [
    `aa-verdicts for ${report.seriesId} (harness ${report.harnessIdentity.slice(0, 12)}); A/A twins ${report.subjects.aa.join(', ') || 'none'}; Node pilot ${report.subjects.pilot.join(', ') || 'none'}`,
    ...report.rows.flatMap(rowLines), `T3 rule: ${report.t3Rule}`, ...(report.stops.length > 0 ? [`STOPS: ${report.stops.join(', ')}`] : []),
    ...report.malformed.map(m => `not counted: ${m.line} (${m.why})`), `VERDICT: ${report.verdict}`,
  ];
}

/** Write `<series>/aa-verdicts.json` (beside the file and moved over). */
export function writeReport(seriesDir, report) {
  const file = path.join(seriesDir, 'aa-verdicts.json');
  mkdirSync(seriesDir, { recursive: true });
  writeFileSync(`${file}.${process.pid}.tmp`, `${JSON.stringify(report, null, 2)}\n`);
  renameSync(`${file}.${process.pid}.tmp`, file);
  return file;
}

/** The exit code of a report: 0 GO, 2 NO GO or STOP. A refusal (4) is the command's own. */
export const exitOf = report => (report.verdict === 'GO' ? EXIT.done : EXIT.stop);
