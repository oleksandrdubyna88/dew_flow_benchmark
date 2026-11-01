// The note per task (plan research/PLAN_lang_explore_olympiad.md D-O8): the task is in the note's title and its spec pin, the predictions are the plan's section 4 row for THAT task, the banner and the closing sentence name
// the ungated draft, the two draft differences from v2 are stated, and task 1's note keeps its own sentences. Counts and rates only, as before.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { loadExplore } from '../lib/inputs.mjs';
import { DIFFERENCES, predictionRows, renderNote, sessionCells } from '../lib/note.mjs';
import { bannerOf, extraDifferencesOf } from '../lib/note-tasks.mjs';
import { cpcBoth, exploreSection, pairRecordOf } from '../lib/records.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { MIB, recordOf, resultOf } from './support.mjs';
import { scoreShape } from '../lib/score.mjs';

const EXPLORE = loadExplore().value;
const task = id => resolveTask(EXPLORE, id).value;

function session(pair, stack, usd, failing = []) {
  const score = scoreShape({ result: resultOf(failing), traps: ['k003', 'k005'] });
  const base = recordOf({ score });
  const record = { ...base, ids: { ...base.ids, stack }, cost: { ...base.cost, headlineUsd: { known: true, value: { usd } }, stopRuleUsd: { known: true, value: usd }, cliTotalUsd: { known: true, value: usd }, brokerUsd: { known: true, value: usd } } };
  const withExplore = { ...record, explore: exploreSection({ score, audit: [] }) };
  return { entry: { rid: `r${pair}${stack[0]}`, pair, stack, attempt: 1, mask: '0-5', label: 'paired', class: 'completed', data: true, run: { startSkewMs: 10, spawnStampMs: 1, flags: [] } }, record: withExplore };
}

const sessionsOf = (failNode = []) => [1, 2, 3].flatMap(pair => [session(pair, 'csharp', 0.3 + pair / 100), session(pair, 'node', 0.4 + pair / 100, pair === 1 ? failNode : [])]);

function inputFor(id, sessions) {
  const pairs = [...new Set(sessions.map(s => s.entry.pair))].map(pair => pairRecordOf({ pair, entries: sessions.filter(s => s.entry.pair === pair).map(s => s.entry), recordOf: rid => sessions.find(s => s.entry.rid === rid)?.record ?? null }));
  const cpc = Object.fromEntries(['csharp', 'node'].map(stack => [stack, cpcBoth(sessions.filter(s => s.entry.stack === stack).map(s => ({ record: s.record, accepted: s.record.explore.accepted?.value === true })))]));
  return { date: '2026-11-02', task: task(id), seriesId: 'x-20261102-ab12', rehearsal: false, subject: { branch: 'b', sha: 'a'.repeat(40), harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: 'i'.repeat(12), pilotIdentity: 'j'.repeat(40) }, pins: EXPLORE, limits: { timeMs: 2000, memoryBytes: 512 * MIB }, sessions, pairs, cpc };
}

/** The plan's section 4 table, written here from the plan: the predicted text of each measure per task. */
const PLAN_SECTION_4 = {
  '02': ['$0.20 to $0.80', '1 to 5 min', '3 per stack', '0 to 1 per stack', 'C# > TS in most pairs (the build)'],
  '03': ['$0.50 to $3.00', '5 to 25 min', '1 to 3 per stack', '1 to 3 per stack', 'no prediction'],
  '09': ['$1.00 to $6.00', '10 to 45 min', '0 to 3 per stack', '1 to 3 per stack', 'no prediction'],
};

for (const id of ['02', '03', '09']) {
  test(`The_note_of_task_${id}_has_the_task_in_its_title_its_own_predictions_and_the_draft_differences`, () => {
    const text = renderNote(inputFor(id, sessionsOf()));
    const t = task(id);
    assert.match(text.split('\n')[0], new RegExp(`^# Exploratory pilot: task ${id} on the v1 bare harness, 6 session\\(s\\) \\(2026-11-02\\)$`));
    assert.ok(text.includes(bannerOf(t)));
    assert.match(bannerOf(t), new RegExp(`olympiad task ${id} \\(${t.title}, an ungated draft spec\\)`));
    assert.match(text, new RegExp(`- Task: spec ${id} at \`3f115d1\` \\(sha256 \`${t.spec.sha256.slice(0, 12)}\`\\), FORMAT v2\\.1 at \`3f115d1\` \\(sha256 \`4a4e86c48a70\`\\)\\.`));
    const rows = predictionRows(sessionsOf().map(sessionCells), inputFor(id, sessionsOf()).pairs, t);
    assert.deepEqual(rows.map(r => r[0]), ['cost per session', 'wall per session', 'accepted (of the sessions per stack)', 'trap hits (per session; detail private)', 'active - model_api', 'harness']);
    assert.deepEqual(rows.slice(0, 5).map(r => r[1]), PLAN_SECTION_4[id], 'the predictions are the plan\'s section 4 row of this task');
    for (const d of [...DIFFERENCES, ...extraDifferencesOf(t)]) assert.ok(text.includes(d), d);
    assert.match(text, /ungated draft \(v2 freezes gated specs/);
    assert.match(text, /held-out suite is the pilot's own/);
    assert.match(text, /over an ungated draft spec, license sentences about this run only\.$/m);
    assert.ok(!text.includes('easiest task'), 'the first task\'s words are not in another task\'s note');
  });
}

test('The_observed_column_is_computed_per_stack_from_the_sessions', () => {
  const rows = predictionRows(sessionsOf(['k003']).map(sessionCells), inputFor('03', sessionsOf(['k003'])).pairs, task('03'));
  assert.equal(rows[2][2], 'C# 3 of 3, TypeScript 2 of 3');
  assert.equal(rows[3][2], 'C# 0 to 0, TypeScript 0 to 1');
  assert.match(rows[5][2], /^0 void\(s\); T1-T5 pass in 6 of 6/);
});

test('Task_1_keeps_the_sentences_of_its_recorded_note_and_no_prediction_of_another_task', () => {
  const text = renderNote(inputFor('01', sessionsOf()));
  assert.match(text.split('\n')[0], /^# Exploratory pilot: task 1 on the v1 bare harness, 6 session\(s\)/);
  assert.ok(text.includes('Three pairs of the easiest task on a harness that differs from v2'));
  assert.equal(extraDifferencesOf(task('01')).length, 0);
  assert.ok(!/\$0\.20 to \$0\.80|ungated draft/.test(text));
});

test('A_toy_task_without_predictions_says_so_and_never_borrows_another_task_s', () => {
  const rows = predictionRows(sessionsOf().map(sessionCells), [], { id: '77', noteLabel: '77', title: 'Toy', draft: false });
  assert.deepEqual(rows.slice(0, 5).map(r => r[1]), Array(5).fill('no prediction recorded'));
});

// ---- the note's date comes from the injected clock (coai 5) --------------------------------------------------------------------------------------

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { EXIT, cmdNote } from '../lib/commands.mjs';
import { noteFor } from '../lib/rehearse.mjs';
import { createSeries } from '../lib/series.mjs';

const CALIBRATION = { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } };

test('A_note_is_dated_by_the_injected_clock_and_noteFor_refuses_to_guess_a_date', async () => {
  const home = tmpDir('o-note-date');
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02', date: new Date('2026-11-01T08:00:00Z') });
  const out = path.join(tmpDir('o-note-out'), 'note.md');
  const c = { home, said: [], say: l => c.said.push(l), worldFn: async () => ok({ explore: EXPLORE }), subject: { branch: 'b', sha: 'a'.repeat(40), harnessPath: 'p', harnessIdentity: 'i', pilotIdentity: 'j' }, now: () => new Date('2031-02-03T04:05:06Z') };
  assert.equal(await cmdNote({ series: made.value.dir, task: '02', out }, c), EXIT.done, c.said.join('\n'));
  assert.match(readFileSync(out, 'utf8').split('\n')[0], /\(2031-02-03\)$/, 'the note date is the injected clock\'s day, not the day the test runs');
  const series = { seriesId: 'x', createdAt: '2026-11-01T08:00:00Z' };
  const dir = tmpDir('o-note-dir');
  mkdirSync(path.join(dir, 'pairs'));
  const args = { series, seriesDir: dir, map: { sessions: {} }, world: { calibration: { limits: CALIBRATION.limits } }, pins: EXPLORE, task: task('02'), rehearsal: true, subject: c.subject };
  assert.throws(() => noteFor(args), /needs the note's date/);
  assert.match(noteFor({ ...args, date: '2031-02-03' }).split('\n')[0], /\(2031-02-03\)$/);
  writeFileSync(path.join(dir, 'x'), '');
});
