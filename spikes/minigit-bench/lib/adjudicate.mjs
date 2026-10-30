// The two-step blind adjudication of held-out failures (parent §5.3). Step 1 sees only a packet { blindId, caseId, spec, captured } with
// stack and arm masked; `revealFor` refuses until every packet has a step-1 verdict, and only then adds the cell's code path for step 2
// (product | harness). `revisedFor` drops every case classed ambiguous in ANY cell from ALL cells and writes the revised score beside
// the primary one, which is never rewritten. Everything lives in the series directory, outside git: the packets hold case text.
import { randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { heldoutScore } from './heldout-run.mjs';
import { maskCaptured, maskRules } from './adjudicate-mask.mjs';
import { refuseInsideWorktree } from './git-guard.mjs';
import { readFileOrNull } from './inputs.mjs';
import { fail, ok } from './result.mjs';

export { maskRules };
const ADJ = 'adjudication';
const PACKETS = 'adjudication-packets';
const STEP1 = Object.freeze(['spec-ambiguity', 'not-ambiguous']);
const STEP2 = Object.freeze(['product', 'harness']);

/** Every path under a series directory is built here, from the NORMALISED (absolute) series: a relative or unclean one never reaches a message or a join. */
const inSeries = (series, ...rest) => path.join(path.resolve(series), ...rest);
const adjPath = (series, ...rest) => inSeries(series, ADJ, ...rest);
const packetPath = (series, blindId) => inSeries(series, PACKETS, `${blindId}.json`);
const cellPath = (series, cell, ...rest) => inSeries(series, cell, ...rest);

function readJson(file) {
  const bytes = readFileOrNull(file);
  if (bytes === null) return null;
  try {
    return JSON.parse(bytes.toString('utf8'));
  } catch (e) {
    if (e instanceof SyntaxError) throw new Error(`${file} is not valid JSON: ${e.message}`);
    throw e;
  }
}
const writeJson = (file, doc) => {
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, `${JSON.stringify(doc, null, 2)}\n`, { mode: 0o600 });
};

/** The scored cells of a series: `<series>/<cell>/score/{score.json,heldout-<phase>.json}`; a cell without them is listed in `skipped`. */
export function readCells(given) {
  const series = path.resolve(given);
  const cells = [];
  const skipped = [];
  for (const name of readdirSync(series, { withFileTypes: true }).filter(e => e.isDirectory() && e.name !== ADJ && e.name !== PACKETS).map(e => e.name).sort()) {
    const dir = cellPath(series, name);
    const score = readJson(path.join(dir, 'score', 'score.json'));
    const result = score ? readJson(path.join(dir, 'score', `heldout-${score.phase}.json`)) : null;
    if (!score || !result) {
      skipped.push({ cell: name, why: score ? `no heldout-${score.phase}.json` : 'no score/score.json' });
      continue;
    }
    cells.push({ cell: name, arm: score.arm, stack: score.stack, phase: score.phase, sessionId: score.sessionId, result, codePath: readJson(path.join(dir, 'cell.json'))?.codePath ?? dir });
  }
  return { cells, skipped };
}

const freshBlindId = taken => {
  for (;;) {
    const id = randomBytes(4).toString('hex');
    if (!taken.has(id)) return id;
  }
};

/** Build the packets and the map. Pure apart from the random blind ids. */
export function buildPackets(cells) {
  const taken = new Set();
  const packets = [];
  const entries = {};
  for (const c of cells) {
    const literals = [c.cell, c.sessionId, c.arm, c.stack];
    for (const k of c.result.cases.filter(x => x.scored === true && x.status === 'fail')) {
      const blindId = freshBlindId(taken);
      taken.add(blindId);
      packets.push({ blindId, caseId: k.id, spec: k.spec, captured: maskCaptured(k.captured, literals) });
      entries[blindId] = { cell: c.cell, arm: c.arm, stack: c.stack, phase: c.phase, caseId: k.id, codePath: c.codePath };
    }
  }
  return { packets: packets.sort((a, b) => a.blindId.localeCompare(b.blindId)), entries };
}

/** Writes `<series>/adjudication-packets/<blindId>.json` (the only directory the blind judge reads) and `<series>/adjudication/map.json`. Refuses to overwrite an earlier set (the blind ids would change). */
export function packetsFor({ series: given }) {
  const series = path.resolve(given);
  const outside = refuseInsideWorktree(series, 'series directory');
  if (!outside.ok) return outside;
  if (existsSync(adjPath(series, 'map.json'))) return fail(`${adjPath(series, 'map.json')} already exists: packets and blind ids already exist for this series`);
  const { cells, skipped } = readCells(series);
  if (cells.length === 0) return fail(`no scored cell with a held-out result under ${series}`);
  const { packets, entries } = buildPackets(cells);
  for (const p of packets) writeJson(packetPath(series, p.blindId), p);
  writeJson(adjPath(series, 'map.json'), { format: 1, entries });
  return ok({ blindIds: packets.map(p => p.blindId), rules: maskRules.map(({ name, replacement }) => ({ name, replacement })), skipped });
}

const mapOf = series => readJson(adjPath(series, 'map.json'));
const verdictsOf = (series, step) => readJson(adjPath(series, `${step}.json`))?.verdicts ?? {};

function record(series, step, blindId, verdict, reason) {
  const next = { ...verdictsOf(series, step), [blindId]: { verdict, reason: reason.trim() } };
  writeJson(adjPath(series, `${step}.json`), { format: 1, verdicts: next });
}

function checkVerdict(series, blindId, verdict, allowed, reason) {
  const outside = refuseInsideWorktree(series, 'series directory');
  if (!outside.ok) return outside;
  const map = mapOf(series);
  if (!map) return fail(`no packets yet under ${series}: run "packets" first`);
  if (!map.entries[blindId]) return fail(`unknown blindId ${blindId}`);
  if (!allowed.includes(verdict)) return fail(`verdict must be one of ${allowed.join('|')}, got ${JSON.stringify(verdict)}`);
  if (typeof reason !== 'string' || reason.trim() === '') return fail('a reason is required with every verdict');
  return ok(map);
}

/** Step 1, from the packet alone. Frozen once the cells are revealed: a blind verdict is not revised after the reveal. */
export function classifyAmbiguity({ series: given, blindId, verdict, reason }) {
  const series = path.resolve(given);
  const checked = checkVerdict(series, blindId, verdict, STEP1, reason);
  if (!checked.ok) return checked;
  if (existsSync(adjPath(series, 'reveal.json'))) return fail('the cells are already revealed: step-1 verdicts are frozen');
  record(series, 'step1', blindId, verdict, reason);
  return ok({ blindId, verdict });
}

const unclassified = (map, step1) => Object.keys(map.entries).filter(id => !step1[id]).sort();

/** Refuses until every packet has a step-1 verdict (the message names each missing blindId); then adds the cell's code path. */
export function revealFor({ series: given }) {
  const series = path.resolve(given);
  const outside = refuseInsideWorktree(series, 'series directory');
  if (!outside.ok) return outside;
  const map = mapOf(series);
  if (!map) return fail(`no packets yet under ${series}: run "packets" first`);
  const missing = unclassified(map, verdictsOf(series, 'step1'));
  if (missing.length > 0) return fail(`reveal refused: ${missing.length} packet(s) have no step-1 verdict: ${missing.join(', ')}`);
  const entries = Object.entries(map.entries).sort(([a], [b]) => a.localeCompare(b)).map(([blindId, e]) => ({ blindId, ...e }));
  writeJson(adjPath(series, 'reveal.json'), { format: 1, entries });
  return ok(entries);
}

/** Step 2, after the reveal: is the failure the product's or the harness's. */
export function classifyOrigin({ series: given, blindId, klass, reason }) {
  const series = path.resolve(given);
  const checked = checkVerdict(series, blindId, klass, STEP2, reason);
  if (!checked.ok) return checked;
  if (!existsSync(adjPath(series, 'reveal.json'))) return fail('step 2 needs the reveal first: run "reveal" once every packet has its step-1 verdict');
  record(series, 'step2', blindId, klass, reason);
  return ok({ blindId, klass });
}

/** Drop every case classed ambiguous in any cell from all cells; write each revised score beside its primary and a series summary. */
export function revisedFor({ series: given }) {
  const series = path.resolve(given);
  const outside = refuseInsideWorktree(series, 'series directory');
  if (!outside.ok) return outside;
  const map = mapOf(series);
  if (!map) return fail(`no packets yet under ${series}: run "packets" first`);
  const step1 = verdictsOf(series, 'step1');
  const missing = unclassified(map, step1);
  if (missing.length > 0) return fail(`revised refused: no step-1 verdict for ${missing.join(', ')}`);
  const droppedCases = [...new Set(Object.entries(step1).filter(([, v]) => v.verdict === 'spec-ambiguity').map(([id]) => map.entries[id].caseId))].sort();
  const { cells } = readCells(series);
  const out = cells.map(c => {
    const score = heldoutScore({ ...c.result, cases: c.result.cases.filter(k => !droppedCases.includes(k.id)) });
    const blockedByDropped = c.result.cases.filter(k => k.status === 'blocked' && droppedCases.includes(k.blockedBy)).map(k => k.id);
    writeJson(cellPath(series, c.cell, 'score', `heldout-revised-${c.phase}.json`), { format: 1, droppedCases, blockedByDropped, score });
    return { cell: c.cell, phase: c.phase, known: score.known, totals: score.totals, groups: score.groups, primary100: score.primary100, blockedByDropped };
  });
  const summary = { format: 1, droppedCases, cells: out };
  writeJson(adjPath(series, 'revised.json'), summary);
  return ok(summary);
}
