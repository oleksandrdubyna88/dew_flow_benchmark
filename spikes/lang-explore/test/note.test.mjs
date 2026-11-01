// The note (plan section 5, T-X9): generated from the records, banner first, every prediction row against its observation, every difference from v2 named, the pins and the subject by sha; and nothing of the
// held-out side: no case id, no requirement id, no input or expected value, no trap name, only counts and rates.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { INPUTS } from '../lib/paths.mjs';
import { BANNER, DIFFERENCES, predictionRows, renderNote, sessionCells } from '../lib/note.mjs';
import { cpcOf, exploreSection, pairRecordOf } from '../lib/records.mjs';
import { MIB, c, recordOf, resultOf } from './support.mjs';
import { scoreShape } from '../lib/score.mjs';

const PINS = JSON.parse(readFileSync(path.join(INPUTS, 'explore.json'), 'utf8'));
const SECRET_SEQUENCE = 'ZEBRA-9 QUAIL-8 MANGO-7 PLUM-6 FIG-5 KIWI-4';
const SECRET_ID = 'k777';

/** A session record whose held-out cases carry things the note must never print. */
function sessionRecord({ stack, usd, cls = 'completed', failing = [], label = 'paired' }) {
  const result = resultOf(failing);
  result.cases = result.cases.map(x => (x.set === 'heldout' ? { ...x, id: `${SECRET_ID}-${x.id}`, stderrTail: SECRET_SEQUENCE, detail: `expected ${SECRET_SEQUENCE}` } : x));
  const score = scoreShape({ result, traps: result.cases.filter(x => x.id.endsWith('k003') || x.id.endsWith('k005')).map(x => x.id) });
  const base = recordOf({ score, label });
  const record = { ...base, class: cls, ids: { ...base.ids, stack }, cost: { ...base.cost, headlineUsd: { known: true, value: { usd } }, stopRuleUsd: { known: true, value: usd }, cliTotalUsd: { known: true, value: usd }, brokerUsd: { known: true, value: usd } } };
  return { ...record, explore: exploreSection({ score, audit: [] }) };
}

const entryOf = (pair, stack, attempt, record, over = {}) => ({ rid: `r${pair}${stack[0]}${attempt}`, pair, stack, attempt, mask: '0-5', label: record.ids.label, class: record.class, data: record.data, run: { startSkewMs: 10, spawnStampMs: 1, flags: [] }, ...over });

function inputFor(sessions) {
  const pairs = [...new Set(sessions.map(s => s.entry.pair))].map(pair => pairRecordOf({ pair, entries: sessions.filter(s => s.entry.pair === pair).map(s => s.entry), recordOf: rid => sessions.find(s => s.entry.rid === rid)?.record ?? null }));
  const cpc = Object.fromEntries(['csharp', 'node'].map(stack => [stack, cpcOf(sessions.filter(s => s.entry.stack === stack).map(s => ({ record: s.record, accepted: s.record.explore.accepted?.value === true })))]));
  return {
    date: '2026-11-01', seriesId: 'x-20261101-ab12', rehearsal: false, subject: { branch: 'feat/lang-explore-task1', sha: 'a'.repeat(40), harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: 'b'.repeat(12), pilotIdentity: 'c'.repeat(40) },
    pins: PINS, limits: { timeMs: 2000, memoryBytes: 512 * MIB }, sessions, pairs, cpc,
  };
}

const PLAN = [['csharp', 0.2], ['node', 0.3]];
function sixSessions() {
  return [1, 2, 3].flatMap(pair => PLAN.map(([stack, usd]) => {
    const record = sessionRecord({ stack, usd: usd + pair / 100 });
    return { entry: entryOf(pair, stack, 1, record), record };
  }));
}

test('The_note_opens_with_the_banner_and_has_every_section_in_order', () => {
  const text = renderNote(inputFor(sixSessions()));
  assert.ok(text.includes(BANNER));
  assert.match(text.split('\n')[0], /^# Exploratory pilot: task 1 on the v1 bare harness, 6 session\(s\)/);
  const headings = [...text.matchAll(/^## (.+)$/gm)].map(m => m[1]);
  assert.deepEqual(headings.map(h => h.split(':')[0].split(',')[0]), ['What was run', 'Prediction against observation (recorded before the run)', 'Cost', 'Time', 'The time partition', 'Quality', 'Pairs', 'Cost per correct solution', 'Voids', 'Differences from v2\'s harness', 'What this does not settle']);
  assert.ok(text.indexOf(BANNER) < text.indexOf('## What was run'), 'the banner comes before everything else');
});

test('Every_prediction_row_of_the_plan_is_stated_against_an_observation_computed_from_the_sessions', () => {
  const cells = sixSessions().map(sessionCells);
  const rows = predictionRows(cells, inputFor(sixSessions()).pairs);
  assert.deepEqual(rows.map(r => r[0]), ['cost per session', 'wall per session', 'active - model_api', 'visible pass rate', 'held-out', 'harness']);
  assert.match(rows[0][2], /\$0\.21 to \$0\.33, median \$0\.31/);
  assert.equal(rows[3][2], '6 of 6 session(s) at 100%');
  assert.match(rows[4][2], /accepted in 6 of 6 scored session\(s\); trap hits C# 0 session\(s\) with a hit, TypeScript 0 session\(s\) with a hit/);
  assert.match(rows[5][2], /^0 void\(s\); T1-T5 pass in 6 of 6; the partition sums to the wall in \d of 6; the most expensive session cost \$0\.3300$/);
  const failing = sixSessions();
  const record = sessionRecord({ stack: 'node', usd: 0.9, failing: ['k003'] });
  failing[1] = { entry: entryOf(1, 'node', 1, record), record };
  const second = predictionRows(failing.map(sessionCells), inputFor(failing).pairs);
  assert.match(second[4][2], /accepted in 5 of 6 scored session\(s\); trap hits C# 0 session\(s\) with a hit, TypeScript 1 session\(s\) with a hit/, 'one failing trap case is one trap hit, as a count');
  assert.match(second[0][2], /\$0\.21 to \$0\.90/);
  assert.match(renderNote(inputFor(failing)), /\| 1 \| TypeScript \| 100% \| 80% \| no \| 1 of 2 trap cases \|/, 'the quality table says it: the held-out rate, not accepted, one trap hit');
  const visibleFails = sixSessions();
  const slipped = sessionRecord({ stack: 'csharp', usd: 0.2, failing: ['s02'] });
  visibleFails[0] = { entry: entryOf(1, 'csharp', 1, slipped), record: slipped };
  assert.equal(predictionRows(visibleFails.map(sessionCells), [])[3][2], '5 of 6 session(s) at 100%', 'the visible row is computed: one session slipped');
});

test('A_void_a_kill_and_a_flag_are_listed_and_a_rerun_is_labelled_ran_unpaired', () => {
  const sessions = sixSessions().slice(0, 4);
  const voided = sessionRecord({ stack: 'csharp', usd: 0.1, cls: 'void: upstream' });
  sessions.push({ entry: entryOf(2, 'csharp', 1, { ...voided, data: false }, { class: 'void: upstream', data: false }), record: { ...voided, data: false, flags: ['upstream 429 2'] } });
  const rerun = sessionRecord({ stack: 'csharp', usd: 0.25, label: 'ran unpaired' });
  sessions.push({ entry: entryOf(2, 'csharp', 2, rerun, { label: 'ran unpaired' }), record: rerun });
  const text = renderNote(inputFor(sessions));
  assert.match(text, /pair 2 C# attempt 1: void: upstream; flags: upstream 429 2/);
  assert.match(text, /\| 2 \| C# \| completed \(ran unpaired\)/);
  assert.match(predictionRows(sessions.map(sessionCells), [])[5][2], /^1 void\(s\)/);
  assert.match(renderNote(inputFor(sixSessions())), /No void, no kill, no timeout and no flagged session\./);
});

test('The_note_holds_no_case_id_no_requirement_id_no_input_no_expected_value_and_no_trap_name', () => {
  const text = renderNote(inputFor(sixSessions()));
  for (const needle of [SECRET_ID, SECRET_SEQUENCE, 'ZEBRA-9 QUAIL', 'k003', 'k005', 'M-0']) assert.ok(!text.includes(needle), `the note must not contain ${needle}`);
  assert.doesNotMatch(text, /\b[hk]\d{3}\b/, 'no case id');
  assert.doesNotMatch(text, /\bs0\d\b/, 'no sample id');
  assert.doesNotMatch(text, /\bR\d{1,2}\b/, 'no requirement id');
  assert.doesNotMatch(text, /\bn\s*=\s*\d{3,}/, 'no input value');
  assert.match(text, /trap hit \(a count; detail private\)/);
  assert.match(text, /0 of 2 trap cases/, 'a trap hit is a count');
  assert.ok(sixSessions()[0].record.explore.cases.value.some(x => x.id.startsWith(SECRET_ID)), 'the control: the record DOES hold the ids, so the absence above is the note\'s doing');
});

test('Every_number_comes_from_the_records_so_changing_a_record_changes_the_note_and_an_unknown_is_a_dash', () => {
  const sessions = sixSessions();
  const before = renderNote(inputFor(sessions));
  sessions[0] = { ...sessions[0], record: { ...sessions[0].record, cost: { ...sessions[0].record.cost, brokerUsd: { known: true, value: 0.1234 } } } };
  const after = renderNote(inputFor(sessions));
  assert.ok(after.includes('$0.1234') && !before.includes('$0.1234'));
  sessions[1] = { ...sessions[1], record: { ...sessions[1].record, cost: { ...sessions[1].record.cost, brokerUsd: { known: false, why: 'no ledger' } } } };
  const unknown = renderNote(inputFor(sessions));
  const row = unknown.split('\n').find(l => l.startsWith('| 1 | TypeScript | completed'));
  assert.match(row, /\| — \|/, 'an unknown broker figure is a dash');
  assert.ok(!/\$0\.0000/.test(row), 'and never a zero');
});

test('The_note_names_the_subject_by_sha_the_harness_by_path_and_identity_the_pins_the_date_and_every_difference_from_v2', () => {
  const text = renderNote(inputFor(sixSessions()));
  for (const must of ['a'.repeat(40), 'spikes/lang-explore/explore.mjs', 'b'.repeat(12), 'c'.repeat(40), '2026-11-01', PINS.cli.sha256.slice(0, 12), PINS.python3.sha256.slice(0, 12), PINS.specs.spec01.sha, PINS.specs.format.sha, 'de_DE.UTF-8', 'time limit 2000 ms']) {
    assert.ok(text.includes(must), `the note names ${must}`);
  }
  for (const d of DIFFERENCES) assert.ok(text.includes(d), `the note states the difference: ${d}`);
  assert.ok(DIFFERENCES.length >= 8);
  assert.match(text, /series `x-20261101-ab12`\./);
  assert.match(renderNote({ ...inputFor(sixSessions()), rehearsal: true }), /a REHEARSAL: stub CLI, fake token, \$0/);
});

test('CPC_is_stated_per_stack_with_no_correct_solution_and_its_spend_when_none_was_accepted', () => {
  const sessions = sixSessions().filter(s => s.entry.stack === 'csharp');
  const failed = sessionRecord({ stack: 'node', usd: 0.5, failing: ['k001'] });
  const all = [...sessions, { entry: entryOf(1, 'node', 1, failed), record: failed }];
  const text = renderNote(inputFor(all));
  assert.match(text, /- C#: 3 accepted of 3 session\(s\), spend \$0\.6600, cost per correct solution \$0\.2200/);
  assert.match(text, /- TypeScript: 0 accepted of 1 session\(s\), spend \$0\.5000, cost per correct solution no correct solution \(spend \$0\.5000\)/);
  assert.equal(c('visible', 's01', 'pass').verdict, 'pass');
});
