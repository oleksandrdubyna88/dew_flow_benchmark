// Scoring exhaustion apart from session finality (section 2.12, with P10 and P11): the verdict file says whether SCORING is open, decided or exhausted and never whether the session is final; finality is the run-map's. An
// exhausted scoring leaves the session `ran` and unscored (a void the series may re-run once); a decided one makes it `scored` and final. The entries are made by the loop's own function (`unscoredEntry`).
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { unscoredEntry } from '../../minigit-bench/lib/bare-series.mjs';
import { fail } from '../../minigit-bench/lib/result.mjs';
import { exploreSection, cellOfSession, finalOf, pairRecordOf } from '../lib/records.mjs';
import { SPIKE } from '../lib/paths.mjs';
import { scoreShape } from '../lib/score.mjs';
import { VOID_UNSCORED, pairComplete, planPair, stackState } from '../lib/series.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { harness } from './quick-passes-harness.mjs';
import { recordOf, resultOf } from './support.mjs';

const entry = (rid, attempt, over = {}) => ({ rid, pair: 1, stack: 'csharp', phase: 'v1', attempt, kind: 'pilot', label: attempt === 1 ? 'paired' : 'ran unpaired', status: 'ran', class: 'completed', data: true, mask: '0-5', ...over });
const mapOf = (...entries) => ({ sessions: Object.fromEntries(entries.map(e => [e.rid, e])) });
const voided = (e, reason = 'pass 6 used its three attempts') => unscoredEntry({ entry: e, result: { class: VOID_UNSCORED, reason }, at: '2026-11-07T10:00:00.000Z' });
const scored = (e, over = {}) => ({ ...e, status: 'scored', data: true, class: 'completed', ...over });

test('The_verdict_file_carries_a_scoring_state_and_no_finality_and_every_consumer_reads_finality_from_the_run_map_alone', async () => {
  const h = harness();
  const r = await scorePasses(h.a);
  h.release();
  const file = JSON.parse(readFileSync(path.join(h.work, 'verdict.json'), 'utf8'));
  assert.equal(file.scoringState, 'decided');
  assert.deepEqual(Object.keys(file).filter(k => /^(terminal|final|reRun)$/i.test(k)), []);
  for (const [verdict, state] of [['accepted', 'decided'], ['rejected', 'decided'], ['intermittently incorrect', 'decided'], ['unscored: environment', 'exhausted']]) {
    const score = { ...scoreShape({ result: resultOf(), traps: [] }) };
    score.explore = { ...score.explore, verdict, scoringState: state, passes: 10 };
    const section = exploreSection({ score, audit: [] });
    assert.deepEqual([section.verdict.value, section.scoringState.value, section.accepted.value], [verdict, state, verdict === 'accepted']);
    assert.deepEqual(Object.keys(section).filter(k => /^(terminal|final|reRun|class|data)$/i.test(k)), [], 'the explore section never writes a finality key');
  }
  assert.equal(r.kind, 'scored');
  // the scan: no module reads a finality key on a verdict object, and the one sanctioned read of scoringState is there, so the scan is alive
  const READ_OF_FINALITY = /\bverdict\w*\??\.(terminal|final|isFinal|reRun)\b/;
  const SANCTIONED = /\.scoringState\b/;
  const sources = readdirSync(path.join(SPIKE, 'lib')).filter(f => f.endsWith('.mjs')).map(f => [f, readFileSync(path.join(SPIKE, 'lib', f), 'utf8')]);
  assert.deepEqual(sources.filter(([, text]) => READ_OF_FINALITY.test(text)).map(([f]) => f), []);
  assert.ok(sources.some(([, text]) => SANCTIONED.test(text)), 'the sanctioned read of scoringState is found');
  assert.ok(READ_OF_FINALITY.test('const done = verdict.final;'), 'the scan finds a read when there is one');
});

test('An_unscored_session_is_scoring_exhausted_and_not_final_and_its_finality_comes_from_the_series_after_the_one_void_re_run', () => {
  const first = voided(entry('a1', 1));
  assert.deepEqual([first.status, first.data, first.class], ['ran', false, VOID_UNSCORED], 'the entry stays ran');
  assert.equal(stackState(mapOf(first), 1, 'csharp'), 'void-rerunnable');
  assert.deepEqual(first.reclassifiedFrom.map(x => [x.class, x.data]), [['completed', true]]);
  const planned = planPair({ map: mapOf(first), pair: 1, pairs: 3, draw: { csharpHigh: true }, rerun: 'csharp', rand: () => 'ab12cd34' });
  assert.equal(planned.ok, true, planned.reason);
  assert.equal(planned.value.planned[0].label, 'ran unpaired');
  // the re-run scored accepted: the cell's final session is the re-run
  const rerun = scored(entry('a2', 2));
  assert.equal(stackState(mapOf(first, rerun), 1, 'csharp'), 'done');
  assert.equal(finalOf([first, rerun]).rid, 'a2');
  // the re-run exhausted too: void-final, the cell has no data session and the pair record says so per stack
  const second = voided(entry('a2', 2));
  assert.equal(stackState(mapOf(first, second), 1, 'csharp'), 'void-final');
  assert.equal(finalOf([first, second]).rid, 'a2');
  assert.equal(finalOf([first, second]).data, false);
  assert.equal(pairComplete(mapOf(first, second, scored(entry('b1', 1, { stack: 'node' }))), 1), true);
  const pair = pairRecordOf({ pair: 1, entries: [first, second], recordOf: () => null });
  assert.equal(pair.cells.csharp.missing, true);
  assert.equal(pair.cells.node.missing, true);
});

test('A_rejected_or_intermittently_incorrect_session_is_scored_and_final_and_the_series_offers_no_re_run', () => {
  for (const verdict of ['rejected', 'intermittently incorrect']) {
    const final = scored(entry('a1', 1));
    assert.equal(stackState(mapOf(final), 1, 'csharp'), 'done', verdict);
    const again = planPair({ map: mapOf(final), pair: 1, pairs: 3, draw: { csharpHigh: true }, rerun: 'csharp' });
    assert.equal(again.ok, false, verdict);
    assert.match(again.reason, /only a session that ended as a void/);
    const record = { ...recordOf({}), class: 'completed', data: true, ids: { ...recordOf({}).ids, label: 'paired' } };
    const cell = cellOfSession(record);
    assert.equal(cell.missing, false);
    assert.equal(cell.costUsd.known, true, 'its cost is in the numerator like any other session');
  }
  assert.equal(fail('x').ok, false);
});
