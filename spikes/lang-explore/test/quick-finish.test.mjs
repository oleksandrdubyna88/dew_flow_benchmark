// A run that stopped between `scored` and `record.json`: the next `run` of that pair finishes it, free, from the kept score (lib/finish.mjs). With fakes and a temporary home: only the interrupted pair is finished, every other state is refused as before,
// nothing is launched or scored or charged, the ledger's spend and the plan's spend do not move, and the event line the finish writes is accepted by every reader of the ledger. No process starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { appendLedger, isCharged, isOwnCharge, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { readRunMap, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { spendTotals } from '../../minigit-bench/lib/spend-gate.mjs';
import { FINISH_EVENT, finishedEvent, interruptedPair } from '../lib/finish.mjs';
import { planSpend } from '../lib/pause.mjs';
import { finalizePair, runPair } from '../lib/run.mjs';
import { ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { pinsOfTask } from '../lib/tasks.mjs';
import { EXPLORE, PLAN, TASK, world as baseWorld } from './quick-series-fixture.mjs';
import { recordOf } from './support.mjs';

const scored = (rid, stack) => ({ rid, pair: 1, stack, phase: 'v1', attempt: 1, kind: 'pilot', label: 'paired', status: 'scored', class: 'completed', data: true, mask: '8-13', dummy: 'sk-ant-oat01-dummy-x' });
const voided = (rid, stack) => ({ ...scored(rid, stack), status: 'ran', class: 'void: harness', data: false });

/** A canonical (non-rehearsal) series of the quick task with the sessions of pair 1 as given, each with the files `files` names (`record`, `score`), and the pair record when `pairRecord`. */
function series({ sessions, files = {}, pairRecord = false, over = {} }) {
  const w = baseWorld({ prompts: { prompts: {} }, ...over }, { prefix: 'x' });
  writeRunMap(runMapFileOf(w.dir), { ...readRunMap(runMapFileOf(w.dir)).value, sessions: Object.fromEntries(sessions.map(s => [s.rid, s])) });
  for (const s of sessions) {
    const dir = path.join(w.dir, 's', s.rid);
    mkdirSync(path.join(dir, 'score'), { recursive: true });
    if (files[s.rid]?.includes('score')) writeFileSync(path.join(dir, 'score', 'score.json'), JSON.stringify({ build: { ok: true }, explore: { cases: [] } }));
    if (files[s.rid]?.includes('record')) writeFileSync(path.join(dir, 'record.json'), '{"kept":true}');
  }
  mkdirSync(path.join(w.dir, 'pairs'), { recursive: true });
  if (pairRecord) writeFileSync(path.join(w.dir, 'pairs', 'p1.json'), '{}');
  return w;
}

/** The io: fakes for the world, the token, the hash pins, the run context, the record writer and the pair finalizer, each logging its call. */
function ioFor(w, log = []) {
  const ctx = { runMapFile: runMapFileOf(w.dir), ledgerFile: ledgerFileOf(w.dir), sessionsRoot: path.join(w.dir, 's') };
  return {
    home: w.home, say: () => {}, log, now: () => new Date('2026-11-06T08:00:00.000Z'), seriesNameOk: () => true,
    worldFn: async () => ok({ explore: EXPLORE, paths: { claudeBinary: '/x/claude' }, snapshots: {}, quick: true, filesFor: () => ({}), scoreFn: async () => { log.push('SCORED'); throw new Error('a finish scores nothing'); } }),
    tokenFn: async () => ok('fake-token'),
    finishSeams: {
      pinsFn: () => ok(null), contextFn: async () => ok(ctx),
      recordFn: ({ entry }) => { log.push(`record ${entry.rid}`); writeFileSync(path.join(w.dir, 's', entry.rid, 'record.json'), '{"written":true}'); return ok({}); },
      finalizeFn: ({ pair }) => { log.push(`pair ${pair}`); const pairFile = path.join(w.dir, 'pairs', `p${pair}.json`); writeFileSync(pairFile, '{}'); return { problems: [], pairFile }; },
    },
  };
}

const run = (w, io, over = {}) => runPair({ seriesDir: w.dir, pair: 1, io, explore: EXPLORE, task: TASK, ...over });
const snapshot = w => ({ runMap: readFileSync(runMapFileOf(w.dir), 'utf8'), ledger: readFileSync(ledgerFileOf(w.dir), 'utf8') });
const events = w => readLedger(ledgerFileOf(w.dir)).filter(l => l.event !== undefined);

test('A_pair_whose_run_stopped_after_scored_is_finished_by_the_next_run_free_with_no_session_no_scoring_and_no_charge', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score'], b1: ['score', 'record'] } });
  const spend = planSpend({ home: w.home, explore: EXPLORE, plan: PLAN });
  const totals = spendTotals(readLedger(ledgerFileOf(w.dir)).map(l => ({ ...l, kind: l.kind ?? 'pilot' })));
  const before = snapshot(w);
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 0, r.message);
  assert.match(r.message, /pair 1 finished from the kept evidence \(1 record\(s\) and the pair record\); no session, no scoring, no charge/);
  assert.deepEqual(io.log, ['record a1', 'pair 1'], 'the missing record only, then the pair record: no scoring, and the session that had its record is not rewritten');
  assert.equal(snapshot(w).runMap, before.runMap, 'the run-map is as it was: no session was planned, launched or settled');
  assert.deepEqual(events(w).map(e => [e.event, e.rid, e.t]), [[FINISH_EVENT, 'a1', '2026-11-06T08:00:00.000Z']]);
  assert.deepEqual(snapshot(w).ledger.split('\n').slice(0, -2).join('\n') + '\n', before.ledger, 'the ledger only gained the event line');
  assert.deepEqual(planSpend({ home: w.home, explore: EXPLORE, plan: PLAN }), spend, 'the plan spends what it did');
  assert.deepEqual(spendTotals(readLedger(ledgerFileOf(w.dir)).map(l => ({ ...l, kind: l.kind ?? 'pilot' }))), totals);
});

test('A_finished_pair_is_refused_as_already_run_so_a_second_run_changes_nothing', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score'], b1: ['score'] } });
  assert.equal((await run(w, ioFor(w))).exit, 0);
  const after = snapshot(w);
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 4);
  assert.match(r.message, /pair 1 has already been run/);
  assert.deepEqual([io.log, snapshot(w)], [[], after]);
});

test('Only_the_pair_record_missing_with_every_session_record_in_place_writes_the_pair_record_and_one_event_naming_the_pair', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score', 'record'], b1: ['score', 'record'] } });
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 0, r.message);
  assert.deepEqual(io.log, ['pair 1']);
  assert.deepEqual(events(w).map(e => [e.event, e.rid, e.pair]), [[FINISH_EVENT, undefined, 1]]);
});

test('A_scored_session_with_no_kept_score_cannot_be_finished_from_evidence_and_nothing_is_written', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: [], b1: ['score', 'record'] } });
  const before = snapshot(w);
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 4);
  assert.match(r.message, /a1 ended scored but kept no score\/score\.json/);
  assert.deepEqual([io.log, snapshot(w)], [[], before]);
});

test('Every_other_state_is_still_refused_a_complete_pair_a_void_a_re_run_request_and_a_pair_with_something_open', async () => {
  const complete = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score', 'record'], b1: ['score', 'record'] }, pairRecord: true });
  let io = ioFor(complete);
  assert.match((await run(complete, io)).message, /has already been run/);
  assert.deepEqual(io.log, []);

  const withVoid = series({ sessions: [voided('a1', 'node'), scored('b1', 'csharp')], files: { b1: ['score', 'record'] }, pairRecord: true });
  io = ioFor(withVoid);
  const refused = await run(withVoid, io);
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /has already been run.*--rerun/);
  assert.deepEqual(io.log, []);

  const interrupted = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score'], b1: ['score', 'record'] } });
  io = ioFor(interrupted);
  const rerun = await run(interrupted, io, { rerun: 'node' });
  assert.equal(rerun.exit, 4, 'a re-run request is not a finish');
  assert.deepEqual(io.log, []);

  const open = series({ sessions: [{ ...scored('a1', 'node'), status: 'ran' }, scored('b1', 'csharp')], files: { b1: ['score'] } });
  io = ioFor(open);
  const resumed = await run(open, io);
  assert.doesNotMatch(resumed.message, /finished from the kept evidence/, 'an open session is the resume\'s, not the finish\'s');
  assert.deepEqual(io.log, [], 'and no record is written by a finish');
});

test('A_pair_after_the_finished_one_is_not_touched_and_a_pair_with_nothing_run_is_not_interrupted', () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score'], b1: ['score'] } });
  const map = readRunMap(runMapFileOf(w.dir)).value;
  assert.equal(interruptedPair({ seriesDir: w.dir, map, pair: 2 }), null);
  assert.deepEqual(interruptedPair({ seriesDir: w.dir, map, pair: 1 }).missing.map(s => s.rid), ['a1', 'b1']);
});

test('A_series_whose_pins_moved_is_refused_before_anything_is_written', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score'], b1: ['score'] }, over: { taskPins: { ...pinsOfTask(TASK), spec: { ...TASK.spec, sha256: '0'.repeat(64) } } } });
  const before = snapshot(w);
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 4);
  assert.match(r.message, /pins of task .* changed since series/);
  assert.deepEqual([io.log, snapshot(w)], [[], before]);
});

test('A_record_that_cannot_be_written_stops_the_finish_with_exit_5_before_the_pair_record_and_the_event', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score'], b1: ['score'] } });
  const io = ioFor(w);
  io.finishSeams.recordFn = () => ({ ok: false, reason: 'the disk is full' });
  const before = snapshot(w);
  const r = await run(w, io);
  assert.equal(r.exit, 5);
  assert.match(r.message, /record of a1 could not be written: the disk is full/);
  assert.deepEqual(snapshot(w), before);
});

// ---- the event line is accepted by every reader of the ledger ----------------------------------------------------------------------------

test('The_finish_event_is_no_charge_for_any_reader_of_the_ledger_the_plans_spend_the_gates_totals_and_the_charge_once_rule', () => {
  const w = series({ sessions: [scored('a1', 'node')], files: { a1: ['score'] } });
  const ledger = ledgerFileOf(w.dir);
  const spent = planSpend({ home: w.home, explore: EXPLORE, plan: PLAN });
  appendLedger(ledger, finishedEvent({ rid: 'a1', at: '2026-11-06T08:00:00.000Z' }));
  appendLedger(ledger, finishedEvent({ pair: 1, at: '2026-11-06T08:00:01.000Z' }));
  const lines = readLedger(ledger);
  assert.equal(lines.filter(l => l.event === FINISH_EVENT).length, 2, 'the ledger is still readable line by line');
  assert.deepEqual(planSpend({ home: w.home, explore: EXPLORE, plan: PLAN }), spent, 'the pause point counts the same dollars');
  assert.deepEqual(planSpend({ home: w.home, explore: EXPLORE, plan: PLAN }).invalid, [], 'and finds no invalid line');
  assert.equal(lines.filter(isOwnCharge).length, 1, 'of four lines only the fixture\'s own charge is an own charge: neither event is');
  assert.equal(lines.length, 3, 'and the two events are the other lines');
  assert.equal(isCharged(lines, 'a1'), true, 'the charge of a1 is still the one charge (the fixture\'s)');
  assert.equal(spendTotals(lines.map(l => (l.event === undefined ? { ...l, kind: 'pilot' } : l))).bareUsd, 4.25, 'the gates sum the same spend');
  assert.equal(lines.filter(l => l.event !== undefined).some(l => 'usd' in l || 'kind' in l), false, 'the event carries no amount and no kind');
});

// ---- a scored session that kept no score is refused even when its record is there ---------------------------------------------------------------

test('A_scored_session_with_its_record_but_no_kept_score_is_refused_with_the_real_finalizer_and_its_explore_section_survives', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['record'], b1: ['record'] } });
  const kept = `${JSON.stringify({ ...recordOf(), explore: { scored: { known: true, value: true }, cases: [{ id: 's01', verdict: 'PASS' }] } }, null, 2)}\n`;
  for (const rid of ['a1', 'b1']) writeFileSync(path.join(w.dir, 's', rid, 'record.json'), kept);
  const io = ioFor(w);
  io.finishSeams.finalizeFn = finalizePair;
  const before = snapshot(w);
  const r = await run(w, io);
  assert.equal(r.exit, 4, r.message);
  assert.match(r.message, /a1, b1 ended scored but kept no score\/score\.json.*nothing was written/);
  assert.equal(readFileSync(path.join(w.dir, 's', 'b1', 'record.json'), 'utf8'), kept, 'the record keeps its explore section');
  assert.equal(readFileSync(path.join(w.dir, 's', 'a1', 'record.json'), 'utf8'), kept, 'and the other session\'s record is not touched either');
  assert.equal(existsSync(path.join(w.dir, 'pairs', 'p1.json')), false, 'no pair record');
  assert.deepEqual([io.log, snapshot(w)], [[], before], 'no record call, no event');
});

test('A_finish_that_writes_the_first_record_and_fails_on_the_second_has_logged_the_event_of_the_first_and_the_spend_has_not_moved', async () => {
  const w = series({ sessions: [scored('a1', 'node'), scored('b1', 'csharp')], files: { a1: ['score'], b1: ['score'] } });
  const io = ioFor(w);
  const write = io.finishSeams.recordFn;
  io.finishSeams.recordFn = args => (args.entry.rid === 'b1' ? { ok: false, reason: 'the disk is full' } : write(args));
  const spend = planSpend({ home: w.home, explore: EXPLORE, plan: PLAN });
  const r = await run(w, io);
  assert.equal(r.exit, 5);
  assert.match(r.message, /record of b1 could not be written: the disk is full/);
  assert.deepEqual(io.log, ['record a1'], 'a1 was written, b1 was not, and there is no pair record');
  assert.deepEqual(events(w).map(e => [e.event, e.rid]), [[FINISH_EVENT, 'a1']], 'the record that was written has its event');
  assert.deepEqual(planSpend({ home: w.home, explore: EXPLORE, plan: PLAN }), spend, 'the plan spends what it did');
  const again = await run(w, ioFor(w));
  assert.equal(again.exit, 0, 'the next run finishes the rest');
  assert.deepEqual(events(w).map(e => [e.event, e.rid]), [[FINISH_EVENT, 'a1'], [FINISH_EVENT, 'b1']], 'one event per record, each written once');
});
