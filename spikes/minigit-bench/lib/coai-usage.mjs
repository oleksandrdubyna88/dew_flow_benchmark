// The coai reviewers' consumption: the usage ledger (<COAI_DATA_DIR>/usage.jsonl) and the gate rounds (coai.db).
//
// The line shape is dew_flow_connect_other_ais · src_mcp/runners/Reviewers/UsageLedger.cs:76-145 (camelCase). Facts this
// reader is built on, each read from that file:
//   - TokensCached is "how many of TokensIn" the vendor served from cache: cached is INSIDE in, so input = in, never in + cached.
//   - CostUsd is nullable: a subscription CLI reports no money. null is UNKNOWN, never 0, and never a Claude dollar.
//   - UsageNote === 'usage not captured' marks a launch that ended before the vendor reported: its tokens are a LOWER BOUND.
//   - Every trailing field is defaulted, so an old line simply lacks it: absent is unknown, not 0 (an absent kind is a review).
// The summary has no numeric field that could be added to Claude's dollars (parent §9: consumption in tokens, cost either
// a number or unknown). Fixtures for this reader are SYNTHETIC until E1.S3's step 0 captures a real ledger.
import { existsSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import { COAI_GATE } from './categories.mjs';
import { fail, ok } from './result.mjs';

export const UNKNOWN_SUBSCRIPTION = 'unknown (subscription)';
export const USAGE_NOT_CAPTURED = 'usage not captured';
const CACHED_ABOVE_INPUT = 'cached exceeds input';
const DEFAULT_KIND = 'review';
const GROUP_KEYS = ['provider', 'model', 'kind', 'role', 'stage'];
/** The UsageEntry fields (camelCase) this reader reads; a test holds them against UsageLedger.cs. */
export const READER_FIELDS = Object.freeze([
  'utc', 'provider', 'model', 'role', 'stage', 'seconds', 'tokensIn', 'tokensOut', 'costUsd', 'kind', 'tokensCached', 'costNote',
  'tokensReasoning', 'usageNote',
]);
/** Providers known to report cached input. For any other, a cached 0 may mean "did not say" (UsageLedger.cs:108), so it is a lower bound. */
const REPORTS_CACHED = new Set(['claude', 'codex']);
export const SECONDS_LABEL = 'seconds are summed launch seconds (work volume across parallel reviewers), not elapsed time';

const unknown = why => ({ known: false, why });
const isCount = v => Number.isFinite(v) && v >= 0;
const round9 = n => Math.round(n * 1e9) / 1e9;
const isRecord = v => v !== null && typeof v === 'object' && !Array.isArray(v);

/** An optional token field: absent is unknown (an old line), present must be a count. */
function optionalCount(obj, field) {
  if (obj[field] === undefined) return ok(unknown(`line has no ${field}`));
  return isCount(obj[field]) ? ok(obj[field]) : fail(`${field} is not a token count`);
}

/** The money field: absent or null is "no cost reported" (null), a number is a price, anything else is a finding. */
function costOf(obj) {
  if (obj.costUsd === undefined || obj.costUsd === null) return ok(null);
  return isCount(obj.costUsd) ? ok(obj.costUsd) : fail('costUsd is neither null nor a non-negative number');
}

function requiredFailure(obj) {
  // The model may be empty: a CLI on its own default names none (UsageLedger.cs:376-378). The provider may not.
  if (typeof obj.provider !== 'string' || obj.provider === '') return 'provider is missing';
  for (const f of ['tokensIn', 'tokensOut']) if (!isCount(obj[f])) return `${f} is not a token count`;
  return isCount(obj.seconds) ? null : 'seconds is not a number';
}

const uncachedOf = (tokensIn, cached) => {
  if (cached.known === false) return unknown(cached.why);
  return cached > tokensIn ? unknown(CACHED_ABOVE_INPUT) : tokensIn - cached;
};

/** One parsed object to a row, or a failure naming the field. Pure. */
function toRow(obj, line) {
  const bad = requiredFailure(obj);
  if (bad) return fail(bad);
  const cached = optionalCount(obj, 'tokensCached');
  const reasoning = optionalCount(obj, 'tokensReasoning');
  const cost = costOf(obj);
  const failed = [cached, reasoning, cost].find(r => !r.ok);
  if (failed) return failed;
  const flags = typeof cached.value === 'number' && cached.value > obj.tokensIn ? [CACHED_ABOVE_INPUT] : [];
  return ok(
    Object.freeze({
      line, utc: obj.utc ?? '', provider: obj.provider, model: typeof obj.model === 'string' ? obj.model : '', kind: obj.kind || DEFAULT_KIND, role: obj.role ?? '',
      stage: obj.stage ?? '', seconds: obj.seconds, tokensIn: obj.tokensIn, tokensOut: obj.tokensOut, costUsd: cost.value,
      costNote: obj.costNote ?? '', cached: cached.value, uncached: uncachedOf(obj.tokensIn, cached.value), reasoning: reasoning.value,
      notCaptured: obj.usageNote === USAGE_NOT_CAPTURED, flags: Object.freeze(flags),
    }),
  );
}

function parseLine(text, line) {
  let obj;
  try { obj = JSON.parse(text); } catch (e) { return fail(`not valid JSON: ${e.message}`); }
  return isRecord(obj) ? toRow(obj, line) : fail('not a JSON object');
}

/** Pure: { rows, findings }. Blank lines are skipped; an unreadable line is a finding with its line number, never dropped. */
export function readLedger(text) {
  const rows = [];
  const findings = [];
  text.split('\n').forEach((raw, index) => {
    if (raw.trim() === '') return;
    const parsed = parseLine(raw, index + 1);
    if (parsed.ok) rows.push(parsed.value);
    else findings.push({ line: index + 1, reason: parsed.reason });
  });
  return { rows, findings };
}

// ---- summary --------------------------------------------------------------------------------------------------------

/**
 * Σ of a per-row token field that may be unknown: the total over the known rows, how many rows could not say, and how many
 * rows are only a bound (`boundKey`: lowerBoundRows for cached, upperBoundRows for uncached). Exact only when none of either.
 */
function tokenTotal(rows, pick, boundKey = null) {
  const values = rows.map(pick);
  const unknownRows = values.filter(v => typeof v !== 'number').length;
  const total = values.filter(v => typeof v === 'number').reduce((a, b) => a + b, 0);
  if (boundKey === null) return { total, unknownRows, exact: unknownRows === 0 };
  const bound = rows.filter(r => typeof pick(r) === 'number' && !REPORTS_CACHED.has(r.provider)).length;
  return { total, unknownRows, [boundKey]: bound, exact: unknownRows === 0 && bound === 0 };
}

/** The cached value as the summary counts it: a flagged one (cached > in) is unknown, not a number to add. */
const cachedForTotal = r => (r.flags.length > 0 ? unknown(CACHED_ABOVE_INPUT) : r.cached);

/** All null: the unknown string. Otherwise the known dollars and the count that could not be priced. Never a 0 for a null. */
function usdOf(rows) {
  const priced = rows.filter(r => r.costUsd !== null);
  if (priced.length === 0) return UNKNOWN_SUBSCRIPTION;
  return { knownUsd: round9(priced.reduce((a, r) => a + r.costUsd, 0)), unknownCount: rows.length - priced.length };
}

const notCapturedOf = rows => {
  const lost = rows.filter(r => r.notCaptured);
  return { rows: lost.length, tokensIn: lost.reduce((a, r) => a + r.tokensIn, 0), tokensOut: lost.reduce((a, r) => a + r.tokensOut, 0) };
};

function groupOf(rows) {
  const notCaptured = notCapturedOf(rows);
  return {
    ...Object.fromEntries(GROUP_KEYS.map(k => [k, rows[0][k]])),
    launches: rows.length,
    seconds: round9(rows.reduce((a, r) => a + r.seconds, 0)),
    tokens: {
      in: rows.reduce((a, r) => a + r.tokensIn, 0),
      out: rows.reduce((a, r) => a + r.tokensOut, 0),
      cached: tokenTotal(rows, cachedForTotal, 'lowerBoundRows'),
      uncached: tokenTotal(rows, r => r.uncached, 'upperBoundRows'),
      reasoning: tokenTotal(rows, r => r.reasoning),
    },
    usd: usdOf(rows),
    costNotes: [...new Set(rows.map(r => r.costNote).filter(Boolean))].sort(),
    notCaptured,
    lowerBound: notCaptured.rows > 0,
  };
}

const flaggedOf = rows => rows.filter(r => r.flags.length > 0)
  .map(r => ({ line: r.line, provider: r.provider, model: r.model, role: r.role, stage: r.stage, flags: r.flags }));

/** Pure: per provider, model, kind, role, stage. `findings` (readLedger's) are rows that could not be read: their tokens are missing, so the summary is inexact. No field here is a number of dollars: USD is a string or { knownUsd, unknownCount }. */
export function ledgerSummary(rows, findings = []) {
  const keyOf = r => JSON.stringify(GROUP_KEYS.map(k => r[k]));
  const groups = [...Map.groupBy(rows, keyOf).entries()].sort(([a], [b]) => (a < b ? -1 : 1)).map(([, g]) => groupOf(g));
  return { rows: rows.length, findings: findings.length, exact: findings.length === 0, secondsLabel: SECONDS_LABEL, groups, flaggedRows: flaggedOf(rows), notCaptured: notCapturedOf(rows) };
}

// ---- coai.db rounds -> coai_gate spans -----------------------------------------------------------------------------

const ROUNDS_SQL = 'SELECT stage, number, verdict, started_utc, completed_utc FROM rounds ORDER BY started_utc, id';

/** Open read-only and read the rows, or say why not. The one I/O edge of this module. */
function readRounds(dbPath) {
  if (!existsSync(dbPath)) return fail(`coai db ${dbPath} does not exist`);
  let db;
  try {
    db = new DatabaseSync(dbPath, { readOnly: true });
    return ok(db.prepare(ROUNDS_SQL).all());
  } catch (e) {
    return fail(`cannot read the rounds table of ${dbPath}: ${e.message}`);
  } finally {
    db?.close();
  }
}

function gateSpan(r) {
  const label = `${r.stage}#${r.number} ${r.verdict}`;
  const startMs = Date.parse(r.started_utc);
  const endMs = Date.parse(r.completed_utc);
  if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs < startMs) return fail(`${label}: unreadable or inverted instants`);
  return ok({ category: COAI_GATE, startMs, endMs, agentId: 'main', source: 'coai.rounds', label });
}

/**
 * `coai_gate` spans, one per round: started_utc .. completed_utc (Schema.cs:52-77). Read from the VACUUM INTO copy E3 makes,
 * never the live db. A missing file or table is { known: false, why }, never an empty gate; a round whose instants cannot be
 * read is listed in `skipped`.
 */
export function roundsSpans(dbPath) {
  const read = readRounds(dbPath);
  if (!read.ok) return unknown(read.reason);
  const built = read.value.map(gateSpan);
  return { known: true, spans: built.filter(b => b.ok).map(b => b.value), skipped: built.filter(b => !b.ok).map(b => b.reason) };
}
