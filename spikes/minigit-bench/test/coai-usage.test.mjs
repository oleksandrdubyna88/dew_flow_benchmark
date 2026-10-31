// The coai usage ledger reader (E1.S4, T-A6 and the coai half of T-A1). Every ledger fixture here is SYNTHETIC:
// no real usage.jsonl capture exists yet (E1.S3 step 0 produces one; the reader is re-checked against it then).
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import path from 'node:path';
import { test } from 'node:test';
import { ledgerSummary, readLedger, roundsSpans, UNKNOWN_SUBSCRIPTION } from '../lib/coai-usage.mjs';
import { hostLacks, siblingCheckout, spikeDir, tmpDir } from './helpers.mjs';
import { READER_FIELDS } from '../lib/coai-usage.mjs';

const fixture = name => readFileSync(path.join(spikeDir, 'test', 'fixtures', name), 'utf8');
const line = o => JSON.stringify({ utc: '2026-10-29T10:00:00Z', provider: 'p', model: 'm', role: 'r', stage: 's', seconds: 1, tokensIn: 0, tokensOut: 0, costUsd: null, outcome: 'ok', ...o });
const summaryOf = lines => ledgerSummary(readLedger(lines.map(line).join('\n')).rows);
const only = summary => {
  assert.equal(summary.groups.length, 1);
  return summary.groups[0];
};

test('Codex_shaped_rows_never_count_cached_input_twice', () => {
  const { rows } = readLedger(line({ provider: 'codex', tokensIn: 173658, tokensCached: 168320 }));
  assert.equal(rows[0].uncached, 5338);
  const g = only(ledgerSummary(rows));
  assert.equal(g.tokens.in, 173658);
  assert.notEqual(g.tokens.in, 341978);
  assert.deepEqual(g.tokens.cached, { total: 168320, unknownRows: 0, lowerBoundRows: 0, exact: true });
  assert.deepEqual(g.tokens.uncached, { total: 5338, unknownRows: 0, upperBoundRows: 0, exact: true });
});

test('A_null_cost_reads_unknown_never_zero', () => {
  const g = only(summaryOf([{ costUsd: null }, { costUsd: null }, { costUsd: null }]));
  assert.equal(UNKNOWN_SUBSCRIPTION, 'unknown (subscription)');
  assert.equal(g.usd, 'unknown (subscription)');
  assert.equal(g.launches, 3);
});

test('A_cost_field_absent_from_the_line_is_also_unknown_not_zero', () => {
  const text = '{"utc":"2026-10-29T10:00:00Z","provider":"p","model":"m","role":"r","stage":"s","seconds":1,"tokensIn":1,"tokensOut":1,"outcome":"ok"}';
  assert.equal(only(ledgerSummary(readLedger(text).rows)).usd, 'unknown (subscription)');
});

test('Mixed_costs_give_the_known_sum_and_the_unknown_count', () => {
  const g = only(summaryOf([{ costUsd: 0.12 }, { costUsd: null }, { costUsd: 0.30 }]));
  assert.deepEqual(g.usd, { knownUsd: 0.42, unknownCount: 1 });
});

test('All_priced_rows_still_say_how_many_are_unknown', () => {
  const g = only(summaryOf([{ costUsd: 0.1 }, { costUsd: 0.2 }]));
  assert.deepEqual(g.usd, { knownUsd: 0.3, unknownCount: 0 });
});

test('A_priced_zero_is_a_known_zero_and_not_unknown', () => {
  const g = only(summaryOf([{ costUsd: 0 }]));
  assert.deepEqual(g.usd, { knownUsd: 0, unknownCount: 0 });
});

test('Usage_not_captured_gives_a_count_and_a_lower_bound', () => {
  const s = summaryOf([{ tokensIn: 40, tokensOut: 0, usageNote: 'usage not captured' }, { tokensIn: 100, tokensOut: 10 }]);
  const g = only(s);
  assert.deepEqual(g.notCaptured, { rows: 1, tokensIn: 40, tokensOut: 0 });
  assert.equal(g.lowerBound, true);
  assert.equal(g.tokens.in, 140);
  assert.deepEqual(s.notCaptured, { rows: 1, tokensIn: 40, tokensOut: 0 });
});

test('A_group_with_no_uncaptured_row_is_not_a_lower_bound', () => {
  assert.equal(only(summaryOf([{ tokensIn: 5 }])).lowerBound, false);
});

test('Cached_above_input_is_flagged', () => {
  const { rows } = readLedger(line({ tokensIn: 10, tokensCached: 20 }));
  assert.deepEqual(rows[0].flags, ['cached exceeds input']);
  assert.deepEqual(rows[0].uncached, { known: false, why: 'cached exceeds input' });
  const s = ledgerSummary(rows);
  assert.equal(s.flaggedRows.length, 1);
  assert.equal(s.flaggedRows[0].line, 1);
  assert.equal(only(s).tokens.uncached.exact, false);
});

test('A_flagged_cached_value_counts_as_unknown_in_the_cached_total', () => {
  const g = only(summaryOf([{ provider: 'codex', tokensIn: 10, tokensCached: 20 }, { provider: 'codex', tokensIn: 100, tokensCached: 60 }]));
  assert.deepEqual(g.tokens.cached, { total: 60, unknownRows: 1, lowerBoundRows: 0, exact: false });
});

test('Cached_from_a_provider_that_may_not_say_is_a_lower_bound', () => {
  // UsageLedger.cs:108: TokensCached is "zero when it did not say". Only claude and codex are known to report it.
  for (const provider of ['claude', 'codex']) {
    assert.equal(only(summaryOf([{ provider, tokensIn: 100, tokensCached: 0 }])).tokens.cached.exact, true, provider);
  }
  const g = only(summaryOf([{ provider: 'gemini', tokensIn: 100, tokensCached: 0 }]));
  assert.deepEqual(g.tokens.cached, { total: 0, unknownRows: 0, lowerBoundRows: 1, exact: false });
  assert.deepEqual(g.tokens.uncached, { total: 100, unknownRows: 0, upperBoundRows: 1, exact: false });
});

test('A_line_with_an_empty_model_is_kept_and_grouped_under_the_empty_model', () => {
  // UsageLedger.cs:376-378: a CLI on its own default names no model. Its tokens must not vanish.
  const r = readLedger(line({ model: '', tokensIn: 500 }));
  assert.deepEqual(r.findings, []);
  const g = only(ledgerSummary(r.rows));
  assert.equal(g.model, '');
  assert.equal(g.tokens.in, 500);
});

test('A_line_with_no_provider_is_still_a_finding', () => {
  assert.equal(readLedger(line({ provider: '' })).findings.length, 1);
});

test('Findings_make_the_summary_inexact', () => {
  const r = readLedger(`${line({})}\n{broken`);
  const s = ledgerSummary(r.rows, r.findings);
  assert.equal(s.findings, 1);
  assert.equal(s.exact, false);
  const clean = ledgerSummary(readLedger(line({})).rows, []);
  assert.equal(clean.findings, 0);
  assert.equal(clean.exact, true);
  assert.equal(ledgerSummary(readLedger(line({})).rows).exact, true);
});

test('Ledger_seconds_are_labelled_as_summed_launch_seconds', () => {
  const s = ledgerSummary(readLedger(line({ seconds: 5 })).rows);
  assert.match(s.secondsLabel, /summed launch seconds/);
  assert.match(s.secondsLabel, /not elapsed/);
});

test('Cached_equal_to_input_is_not_flagged', () => {
  const { rows } = readLedger(line({ tokensIn: 10, tokensCached: 10 }));
  assert.deepEqual(rows[0].flags, []);
  assert.equal(rows[0].uncached, 0);
});

test('An_old_line_without_tokensCached_is_unknown_not_zero', () => {
  const { rows } = readLedger(line({ tokensIn: 9000 }));
  assert.equal(rows[0].cached.known, false);
  assert.equal(rows[0].uncached.known, false);
  assert.equal(rows[0].reasoning.known, false);
  const g = only(ledgerSummary(rows));
  assert.deepEqual(g.tokens.cached, { total: 0, unknownRows: 1, lowerBoundRows: 0, exact: false });
});

test('An_old_line_without_kind_is_read_as_a_review', () => {
  assert.equal(readLedger(line({})).rows[0].kind, 'review');
  assert.equal(readLedger(line({ kind: 'consult' })).rows[0].kind, 'consult');
});

test('An_unparseable_line_is_a_finding_with_its_line_number_never_dropped', () => {
  const r = readLedger(`${line({})}\n{not json\n${line({})}\n`);
  assert.equal(r.rows.length, 2);
  assert.equal(r.findings.length, 1);
  assert.equal(r.findings[0].line, 2);
  assert.match(r.findings[0].reason, /not valid JSON/);
});

test('A_row_with_a_non_numeric_token_count_is_a_finding_not_a_zero', () => {
  const r = readLedger(`${line({ tokensIn: 'many' })}\n[1,2]`);
  assert.equal(r.rows.length, 0);
  assert.deepEqual(r.findings.map(f => f.line), [1, 2]);
  assert.match(r.findings[0].reason, /tokensIn/);
});

test('Blank_lines_are_skipped_and_do_not_shift_line_numbers', () => {
  const r = readLedger(`\n${line({})}\n\n{bad`);
  assert.equal(r.rows[0].line, 2);
  assert.equal(r.findings[0].line, 4);
});

test('Rows_group_by_provider_model_kind_role_and_stage', () => {
  const s = summaryOf([{ role: 'a' }, { role: 'a' }, { role: 'b' }, { role: 'a', stage: 'plan' }, { kind: 'consult' }]);
  assert.equal(s.groups.length, 4);
  const g = s.groups.find(x => x.role === 'a' && x.stage === 's' && x.kind === 'review');
  assert.equal(g.launches, 2);
  assert.equal(g.seconds, 2);
});

test('The_mixed_fixture_summarises_per_group_with_every_trap_visible', () => {
  const r = readLedger(fixture('coai-usage-mixed.jsonl'));
  assert.deepEqual(r.findings, []);
  assert.equal(r.rows.length, 6);
  const s = ledgerSummary(r.rows);
  const opus = s.groups.find(g => g.model === 'claude-opus-5-5');
  assert.deepEqual(opus.usd, { knownUsd: 0.3, unknownCount: 1 });
  assert.equal(opus.tokens.in, 53000);
  assert.equal(s.groups.find(g => g.provider === 'gemini').tokens.cached.exact, false);
  assert.equal(s.groups.find(g => g.provider === 'grok').lowerBound, true);
  assert.equal(s.flaggedRows.length, 1);
  assert.equal(s.flaggedRows[0].provider, 'codex');
  assert.equal(s.notCaptured.rows, 1);
});

test('A_ledger_summary_cannot_be_added_to_claude_dollars', () => {
  const s = ledgerSummary(readLedger(fixture('coai-usage-mixed.jsonl')).rows);
  // Nested keys count too: group.usd.knownUsd is the one place a dollar figure may sit, and it is not a Claude-comparable field.
  const numericMoney = (o, trail = '') => Object.entries(o ?? {}).flatMap(([k, v]) => {
    if (v !== null && typeof v === 'object') return numericMoney(v, `${trail}${k}.`);
    return /usd|cost/i.test(k) && typeof v === 'number' ? [`${trail}${k}`] : [];
  });
  assert.equal(numericMoney(s).every(p => p.endsWith('usd.knownUsd')), true);
  for (const g of s.groups) assert.notEqual(typeof g.usd, 'number');
  assert.deepEqual(Object.keys(s).filter(k => /usd|cost/i.test(k)), []);
});

test('Summarising_does_not_change_its_input_rows', () => {
  const { rows } = readLedger(fixture('coai-usage-mixed.jsonl'));
  const before = JSON.stringify(rows);
  ledgerSummary(rows);
  assert.equal(JSON.stringify(rows), before);
});

// ---- coai rounds -> coai_gate spans --------------------------------------------------------------------------------

// The `rounds` table exactly as src_mcp/src/Store/Schema.cs:52-77 creates it (column list copied; the contract-with-two-
// implementations test below reads Schema.cs itself when the sibling checkout is present).
const ROUNDS_DDL = `CREATE TABLE rounds (
  id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, stage TEXT NOT NULL, number INTEGER NOT NULL,
  subject TEXT NOT NULL DEFAULT '', status TEXT NOT NULL, verdict TEXT NOT NULL, gating INTEGER NOT NULL DEFAULT 0,
  started_utc TEXT NOT NULL, completed_utc TEXT NOT NULL, tokens_in INTEGER NOT NULL DEFAULT 0,
  tokens_out INTEGER NOT NULL DEFAULT 0, cost_usd REAL, plan_text TEXT NOT NULL DEFAULT '', head_sha TEXT NOT NULL DEFAULT '',
  caller TEXT NOT NULL DEFAULT '', accepted INTEGER NOT NULL DEFAULT -1, rejected INTEGER NOT NULL DEFAULT -1,
  agent_log TEXT NOT NULL DEFAULT '', UNIQUE (session_id, stage, number))`;

function roundsDb(rows, ddl = ROUNDS_DDL) {
  const file = path.join(tmpDir('coai-db'), 'coai.db');
  const db = new DatabaseSync(file);
  db.exec(ddl);
  if (rows.length > 0) {
    const ins = db.prepare("INSERT INTO rounds (session_id, stage, number, status, verdict, started_utc, completed_utc) VALUES ('s1', ?, ?, 'done', ?, ?, ?)");
    for (const r of rows) ins.run(r.stage, r.number, r.verdict, r.started, r.completed);
  }
  db.close();
  return file;
}

test('Rounds_become_gate_spans_from_a_real_schema', () => {
  const file = roundsDb([
    { stage: 'plan', number: 1, verdict: 'proceed', started: '2026-10-29T10:00:00.5000000Z', completed: '2026-10-29T10:01:30.0000000Z' },
    { stage: 'code', number: 1, verdict: 'fix', started: '2026-10-29T10:05:00Z', completed: '2026-10-29T10:06:00Z' },
  ]);
  const r = roundsSpans(file);
  assert.equal(r.known, true);
  assert.equal(r.spans.length, 2);
  assert.deepEqual(r.spans[0], {
    category: 'coai_gate', startMs: Date.parse('2026-10-29T10:00:00.500Z'), endMs: Date.parse('2026-10-29T10:01:30Z'),
    agentId: 'main', source: 'coai.rounds', label: 'plan#1 proceed',
  });
  assert.equal(r.spans[1].endMs - r.spans[1].startMs, 60_000);
});

test('A_missing_database_file_is_unknown_not_an_empty_gate', () => {
  const r = roundsSpans(path.join(tmpDir('coai-none'), 'absent.db'));
  assert.equal(r.known, false);
  assert.match(r.why, /absent\.db/);
});

test('A_database_without_the_rounds_table_is_unknown', () => {
  const r = roundsSpans(roundsDb([], 'CREATE TABLE other (x INTEGER)'));
  assert.equal(r.known, false);
  assert.match(r.why, /rounds/);
});

test('A_round_with_an_unreadable_instant_is_skipped_and_listed', () => {
  const r = roundsSpans(roundsDb([
    { stage: 'plan', number: 1, verdict: 'proceed', started: '', completed: '2026-10-29T10:01:30Z' },
    { stage: 'plan', number: 2, verdict: 'proceed', started: '2026-10-29T10:00:00Z', completed: '2026-10-29T10:00:10Z' },
  ]));
  assert.equal(r.known, true);
  assert.equal(r.spans.length, 1);
  assert.equal(r.skipped.length, 1);
  assert.match(r.skipped[0], /plan#1/);
});

/** coai's source: COAI_SRC, else the sibling checkout beside this repository (or beside the main checkout of a worktree), else null. */
const coaiSource = () => process.env.COAI_SRC ?? siblingCheckout('dew_flow_connect_other_ais');

test('The_rounds_columns_the_reader_selects_exist_in_coais_own_schema', t => {
  const root = coaiSource();
  if (root === null) return hostLacks(t, 'there is no sibling dew_flow_connect_other_ais checkout (and no COAI_SRC), so the rounds columns are checked against the copied DDL only');
  const schema = path.join(root, 'src_mcp/src/Store/Schema.cs');
  let text;
  try { text = readFileSync(schema, 'utf8'); } catch (e) {
    if (e.code !== 'ENOENT') throw e;
    return hostLacks(t, `${schema} is not checked out here, so the rounds columns are checked against the copied DDL only`);
  }
  const table = /CREATE TABLE IF NOT EXISTS rounds \(([\s\S]*?)\n\s*\);/.exec(text)?.[1] ?? '';
  for (const column of ['stage', 'number', 'verdict', 'started_utc', 'completed_utc']) assert.match(table, new RegExp(`\\b${column}\\b`));
});

test('The_ledger_fields_the_reader_uses_exist_in_coais_own_UsageEntry', t => {
  const root = coaiSource();
  if (root === null) return hostLacks(t, 'there is no sibling dew_flow_connect_other_ais checkout (and no COAI_SRC), so the ledger field names are checked against the reader\'s own list only');
  const file = path.join(root, 'src_mcp/runners/Reviewers/UsageLedger.cs');
  let text;
  try { text = readFileSync(file, 'utf8'); } catch (e) {
    if (e.code !== 'ENOENT') throw e;
    return hostLacks(t, `${file} is not checked out here, so the ledger field names are checked against the reader's own list only`);
  }
  const record = /public sealed record UsageEntry\(([\s\S]*?^ {4}\S[^\n]*\)\s*$)/m.exec(text)?.[1] ?? '';
  assert.notEqual(record, '', 'UsageEntry record not found');
  const camel = name => name[0].toLowerCase() + name.slice(1);
  const declared = new Set([...record.matchAll(/^\s{4}(?:string|long|double\??|long\?|bool|int)\s+(\w+)/gm)].map(m => camel(m[1])));
  for (const field of READER_FIELDS) assert.equal(declared.has(field), true, `${field} is not a UsageEntry field`);
});
