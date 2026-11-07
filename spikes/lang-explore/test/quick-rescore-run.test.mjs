// `rescore()` itself, end to end, with fakes and a temporary home (a rehearsal-named series, so its held-out cases are the fixture and the pins are not hashed): the order of the writes, a failed scoring writes nothing, the ledger line,
// the spend unchanged, a refusal for a moved pin, the series read only after the lock, and a wait that loses nothing another run wrote. The run context, the evidence, the record and the pair are fakes; no process starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { appendLedger, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { readRunMap, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { createHash } from 'node:crypto';
import { runnerPinNow } from '../lib/harness-pin.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { planSpend } from '../lib/pause.mjs';
import { quickPlan } from '../lib/quick.mjs';
import { rescore } from '../lib/rescore.mjs';
import { createSeries, ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { pinsOfTask, resolveTask } from '../lib/tasks.mjs';

import { EXPLORE, PLAN, TASK, VOID, world } from './quick-series-fixture.mjs';

const read = file => readFileSync(file, 'utf8');
const runMapOf = dir => readRunMap(runMapFileOf(dir)).value;

/** The io of a rescore: fakes that log what they see at the moment they are called. */
function ioFor(w, { now = { class: 'completed', data: true, flags: [] }, scored = ok({ build: { ok: true }, explore: { cases: [] } }), log = [], scoreArgs = [], extra = {} } = {}) {
  const ctx = { runMapFile: runMapFileOf(w.dir), ledgerFile: ledgerFileOf(w.dir), sessionsRoot: path.join(w.dir, 's') };
  return {
    home: w.home, say: () => {}, now: () => new Date('2026-11-05T18:00:00.000Z'), log,
    worldFn: async () => ok({ explore: EXPLORE, paths: { claudeBinary: '/x/claude' }, snapshots: {}, quick: true, filesFor: () => ({}), scoreFn: async args => { log.push('score'); scoreArgs.push(args); return scored; } }),
    tokenFn: async () => ok('fake-token'),
    contextFn: async () => ok(ctx),
    collectFn: () => ok({ outcome: now }),
    recordFn: ({ entry }) => { log.push(['record', runMapOf(w.dir).sessions.a1.status, readLedger(ledgerFileOf(w.dir)).map(l => l.event ?? 'charge')]); writeFileSync(path.join(w.layout.dir, 'record.json'), JSON.stringify({ rid: entry.rid })); return ok({}); },
    finalizeFn: () => { log.push('finalize'); return { problems: [], pairFile: path.join(w.dir, 'pairs', 'p1.json') }; },
    ...extra,
  };
}

const run = (w, io, waitMs = 0) => rescore({ task: TASK, seriesDir: w.dir, rid: 'a1', io, waitMs });

test('A_rescore_scores_first_then_writes_the_run_map_the_ledger_event_and_the_record_in_that_order_and_changes_no_spend', async () => {
  const w = world();
  const spend = planSpend({ home: w.home, explore: EXPLORE, plan: PLAN });
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 0, r.message);
  assert.deepEqual(io.log, ['score', ['record', 'scored', ['charge', 'rescore']], 'finalize'], 'the scoring came before every write; the run-map and the ledger were written before the record');
  const entry = runMapOf(w.dir).sessions.a1;
  assert.deepEqual([entry.status, entry.class, entry.data], ['scored', 'completed', true]);
  assert.deepEqual(entry.reclassifiedFrom.map(x => [x.class, x.data, x.at]), [['void: contaminated', false, '2026-11-05T18:00:00.000Z']]);
  for (const key of ['rid', 'pair', 'stack', 'dummy', 'run', 'mask']) assert.deepEqual(entry[key], VOID[key], `${key} is kept`);
  const events = readLedger(ledgerFileOf(w.dir)).filter(l => l.event !== undefined);
  assert.deepEqual(events.map(e => [e.event, e.rid, e.from, e.to, e.t]), [['rescore', 'a1', 'void: contaminated', 'completed', '2026-11-05T18:00:00.000Z']]);
  assert.deepEqual(planSpend({ home: w.home, explore: EXPLORE, plan: PLAN }), spend, 'the plan spends exactly what it did');
  assert.match(read(path.join(w.layout.dir, 'score', 'score.json')), /"build"/, 'the score is kept beside the session');
});

test('A_scoring_that_fails_leaves_the_run_map_the_ledger_and_the_records_as_they_were_and_exits_3', async () => {
  const w = world();
  const before = [read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))];
  const io = ioFor(w, { scored: fail('the layer halted: memory-not-certifiable', { kind: 'environment' }) });
  const r = await run(w, io);
  assert.equal(r.exit, 3);
  assert.match(r.message, /nothing was changed/);
  assert.deepEqual([read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))], before);
  assert.deepEqual(io.log, ['score'], 'no record and no pair record was written');
});

test('Evidence_that_still_reads_a_void_changes_nothing_and_is_not_an_error', async () => {
  const w = world();
  const before = [read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))];
  const io = ioFor(w, { now: { class: 'void: contaminated', data: false, flags: [] } });
  const r = await run(w, io);
  assert.equal(r.exit, 0);
  assert.match(r.message, /stays void: contaminated/);
  assert.deepEqual([read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))], before);
  assert.deepEqual(io.log, [], 'not even scored');
});

test('A_series_whose_frozen_pins_moved_since_it_was_made_is_refused_before_anything_is_read_or_scored', async () => {
  const w = world({ taskPins: { ...pinsOfTask(TASK), spec: { ...TASK.spec, sha256: '0'.repeat(64) } } });
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 4);
  assert.match(r.message, /pins of task .* changed since series .* was made \(spec\)/);
  assert.deepEqual(io.log, []);
});

test('A_session_of_any_other_class_is_refused_before_anything_is_scored', async () => {
  const w = world();
  writeRunMap(runMapFileOf(w.dir), { ...runMapOf(w.dir), sessions: { a1: { ...VOID, class: 'stop: security' } } });
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 4);
  assert.match(r.message, /only a void: contaminated session/);
  assert.deepEqual(io.log, []);
});

test('The_run_map_is_read_after_the_lock_so_a_session_a_run_wrote_during_the_wait_survives_the_rescore', async () => {
  const w = world();
  const holder = acquireSeriesLock({ seriesDir: w.dir });
  assert.equal(holder.ok, true, holder.reason);
  const waits = [];
  const io = ioFor(w, {
    extra: {
      sleepFn: async ms => {
        waits.push(ms);
        // the run that holds the series writes while the rescore waits: it adds a session, then ends
        writeRunMap(runMapFileOf(w.dir), { ...runMapOf(w.dir), sessions: { ...runMapOf(w.dir).sessions, z9: { ...VOID, rid: 'z9', pair: 2, status: 'scored', class: 'completed', data: true } } });
        holder.value.release();
      },
    },
  });
  const r = await run(w, io, 60_000);
  assert.equal(r.exit, 0, r.message);
  assert.equal(waits.length, 1, 'it waited once');
  const sessions = runMapOf(w.dir).sessions;
  assert.deepEqual(Object.keys(sessions).sort(), ['a1', 'z9'], 'the session written during the wait is still there');
  assert.equal(sessions.a1.status, 'scored');
});

test('A_series_that_a_live_run_holds_is_refused_at_once_without_a_wait', async () => {
  const w = world();
  const holder = acquireSeriesLock({ seriesDir: w.dir });
  try {
    const io = ioFor(w);
    const r = await run(w, io, 0);
    assert.equal(r.exit, 4);
    assert.deepEqual(io.log, []);
  } finally {
    holder.value.release();
  }
});

test('A_series_made_under_another_runner_pin_or_under_none_scores_with_harness_drift_possible_and_one_made_under_this_pin_does_not', async () => {
  for (const [name, over, expected] of [['no pin recorded', {}, true], ['another pin', { runnerPin: { version: '0.0.0', manifestSha256: 'f'.repeat(64) } }, true], ['this pin', { runnerPin: runnerPinNow() }, false]]) {
    const w = world(over);
    const scoreArgs = [];
    const r = await run(w, ioFor(w, { scoreArgs }));
    assert.equal(r.exit, 0, `${name}: ${r.message}`);
    assert.equal(scoreArgs[0].driftPossible, expected, name);
  }
});

test('A_rescore_gives_the_scoring_the_history_of_its_series_so_that_template_files_are_judged_by_their_bytes', async () => {
  const bytes = 'the old build.sh\n';
  const w = world({ templateHashes: { 'node/web/build.sh': createHash('sha256').update(bytes).digest('hex') } });
  const scoreArgs = [];
  const r = await run(w, ioFor(w, { scoreArgs }));
  assert.equal(r.exit, 0, r.message);
  assert.equal(typeof scoreArgs[0].history, 'function', 'the scoring is given the series\' history');
  assert.deepEqual(scoreArgs[0].history({ stack: 'node', kind: 'web', rel: 'build.sh' }), { known: true, sha256: createHash('sha256').update(bytes).digest('hex') }, 'and it answers from the map the series recorded');
  assert.equal(scoreArgs[0].templateDriftPossible, undefined, 'the flag of the earlier design is gone: the bytes decide');
});

test('A_stop_signal_ends_the_wait_for_a_series_lock_and_changes_nothing', async () => {
  const w = world();
  const holder = acquireSeriesLock({ seriesDir: w.dir });
  const ctl = new AbortController();
  const before = [read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))];
  const waits = [];
  const io = ioFor(w, { extra: { signal: ctl.signal, abort: { reason: 'SIGTERM', exit: 143 }, sleepFn: async ms => { waits.push(ms); ctl.abort(); } } });
  try {
    const r = await run(w, io, 60_000);
    assert.equal(r.exit, 143, r.message);
    assert.match(r.message, /stopped by SIGTERM while waiting for the series/);
    assert.equal(waits.length, 1, 'it did not wait again after the signal');
    assert.deepEqual([read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))], before);
    assert.deepEqual(io.log, []);
  } finally {
    holder.value.release();
  }
});

test('A_stop_signal_during_the_scoring_leaves_the_run_map_the_ledger_and_the_records_as_they_were', async () => {
  const w = world();
  const ctl = new AbortController();
  const before = [read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))];
  const io = ioFor(w, { extra: { signal: ctl.signal, abort: { reason: 'SIGINT', exit: 130 } } });
  const world0 = io.worldFn;
  io.worldFn = async a => { const x = await world0(a); return ok({ ...x.value, scoreFn: async args => { ctl.abort(); return x.value.scoreFn(args); } }); };
  const r = await run(w, io);
  assert.equal(r.exit, 130, r.message);
  assert.match(r.message, /stopped by SIGINT.*nothing was changed/);
  assert.deepEqual([read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))], before);
  assert.deepEqual(io.log, ['score'], 'the scoring ran, no write followed');
});
