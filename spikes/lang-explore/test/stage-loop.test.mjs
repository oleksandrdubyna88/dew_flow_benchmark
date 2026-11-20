// The real loop of v1 driven by Stage 1's own pieces (F3.S1 (a), PR 3): the unit's `stepOf`, the kinds in the run-map and the ledger, the records' arm, model, task, round and kind, the alias baseline, the context of a Stage 1 pair and the absence of a spend ceiling.
// v1's test rig (`bare-loop-harness.mjs`: a fake launch that lays the bare-smoke fixture, a fake scorer) stands for the box and the model; the plan, the step and the records are Stage 1's.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { EXIT, runLoop } from '../../minigit-bench/lib/bare-series.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { aliasStore } from '../../minigit-bench/lib/alias-store.mjs';
import { hasDataSession, readRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { harness } from '../../minigit-bench/test/bare-loop-harness.mjs';
import { missingPaths } from '../lib/records.mjs';
import { contextOf } from '../lib/run-ctx.mjs';
import { STAGE_PILOT } from '../lib/stage-lists.mjs';
import { finalizerOf } from '../lib/stage-final.mjs';
import { STAGE_ROUNDS } from '../lib/stage-schedule.mjs';
import { planUnit, stepOfUnit, unitOf } from '../lib/stage-unit.mjs';
import { SEED, SLOTS } from './stage-run-fixture.mjs';
import { EXPLORE } from './stage-series-fixture.mjs';

const GATES = { seed: SEED, seedRevealed: true, pilotGo: true, readbackLines: [] };
const unitFor = ({ what, map }) => unitOf({ what, state: { pilot: STAGE_PILOT }, map, gates: GATES }).value;
const COUNTER = (() => { let n = 0; return () => () => `00${(n += 1)}`; })();

/** Plan `what` into `map` and run the loop over it with the unit's step; `rig` is v1's harness (fresh unless given). */
async function runUnitIn({ rig, what, map = rig.map, over = {} }) {
  const unit = unitFor({ what, map });
  const plan = planUnit({ map, unit, rand: COUNTER() });
  assert.equal(plan.ok, true, plan.reason);
  const ctx = { ...rig.ctx, kind: unit.kind, pair: unit.pair, seed: unit.kind === 'data' ? SEED : null, stepOf: stepOfUnit(unit), ...over };
  const result = await runLoop({ ctx, deps: rig.deps, map: plan.value.map });
  return { unit, plan: plan.value, result, ctx };
}

test('Both_sessions_of_a_slot_start_together_and_the_scoring_starts_only_after_both_ended', async () => {
  const rig = harness();
  const { unit, result } = await runUnitIn({ rig, what: 'next' });
  assert.equal(result.exit, EXIT.done, result.message);
  const order = rig.events.filter(e => e.startsWith('launch') || e.startsWith('score')).map(e => e.split(' ')[0]);
  assert.deepEqual(order, ['launch', 'launch', 'score', 'score'], 'both launched, THEN both scored: no scoring while a session runs');
  const rids = unit.slot.rids;
  const scored = rig.events.filter(e => e.startsWith('score ')).map(e => e.split(' ')[1]);
  assert.deepEqual(scored, unit.order.map(stack => rids[stack][0]), 'the scoring order is the slot\'s: the stack the seed names first comes first');
  assert.ok(Object.values(result.map.sessions).every(s => s.status === 'scored' && s.kind === 'data' && s.pair === unit.pair && s.round === unit.round && s.task === unit.task && s.arm === 'bare'), 'the entries carry kind, task, round and arm');
});

test('A_data_session_is_charged_as_data_or_void_and_a_pilot_or_A_A_session_under_its_own_kind', async () => {
  const slot = SLOTS[0];
  const voided = harness({ variants: { [slot.rids.csharp[0]]: 'upstream' } });
  const first = await runUnitIn({ rig: voided, what: 'next' });
  assert.equal(first.result.exit, EXIT.done, first.result.message);
  assert.equal(hasDataSession(first.result.map), true, 'a data session that left planned freezes the instrument');
  const kindsOf = () => readLedger(voided.ctx.ledgerFile).filter(l => l.event === undefined).map(l => [l.rid, l.kind]);
  assert.deepEqual(kindsOf().sort(), [[slot.rids.csharp[0], 'void'], [slot.rids.node[0], 'data']].sort(), 'a data pair: data and void');
  const rerun = await runUnitIn({ rig: voided, what: 'next', map: first.result.map });
  assert.equal(rerun.result.exit, EXIT.done, rerun.result.message);
  assert.deepEqual(rerun.plan.planned.map(p => [p.rid, p.attempt, p.label]), [[slot.rids.csharp[1], 2, 'ran unpaired']], 'the re-run is that stack alone, attempt 2');
  assert.deepEqual(kindsOf().filter(([rid]) => rid === slot.rids.csharp[1]), [[slot.rids.csharp[1], 'data']]);
  const pilot = harness();
  const aa = await runUnitIn({ rig: pilot, what: 'aa' });
  assert.equal(aa.result.exit, EXIT.done, aa.result.message);
  assert.equal(hasDataSession(aa.result.map), false, 'the A/A is an instrument check, not data');
  const second = await runUnitIn({ rig: pilot, what: 'pilot:10', map: aa.result.map });
  assert.equal(second.result.exit, EXIT.done, second.result.message);
  assert.deepEqual(readLedger(pilot.ctx.ledgerFile).filter(l => l.event === undefined).map(l => l.kind).sort(), ['aa', 'aa', 'pilot', 'pilot']);
  assert.deepEqual(aa.plan.planned.map(p => [p.twin, p.mask]), [['a', '0-5'], ['b', '8-13']]);
});

test('The_record_of_a_stage_session_carries_arm_model_task_round_and_kind_each_known_or_saying_why', async () => {
  const rig = harness();
  const { unit, result } = await runUnitIn({ rig, what: 'next' });
  assert.equal(result.exit, EXIT.done, result.message);
  mkdirSync(path.join(rig.ctx.seriesDir, 'pairs'), { recursive: true });
  const finalize = finalizerOf({ unit, go: { date: '2026-11-21', scope: { from: 1, to: 5 }, words: 'the owner' }, alias: 'opus' });
  const fin = finalize({ seriesDir: rig.ctx.seriesDir, map: result.map, pair: unit.pair, pause: null });
  const records = Object.values(result.map.sessions).map(s => JSON.parse(readFileSync(path.join(rig.ctx.sessionsRoot, s.rid, 'record.json'), 'utf8')));
  assert.equal(records.length, 2);
  for (const record of records) {
    assert.deepEqual([record.explore.arm, record.explore.task, record.explore.round, record.explore.kind].map(x => x.value), ['bare', unit.task, unit.round, 'data']);
    assert.equal(record.explore.model.known, true, JSON.stringify(record.explore.model));
    assert.match(record.explore.model.value, /^claude-/, 'the id the alias record resolved for the main model');
    assert.deepEqual(missingPaths(record, false, true).filter(p => /^explore\.(arm|model|task|round|kind)$/.test(p)), []);
  }
  for (const r of records) assert.equal(existsSync(path.join(rig.ctx.sessionsRoot, r.ids.rid, 'stage-facts.json')), true, 'the finalizer stores the session\'s facts for the readers after the tag');
  const pair = JSON.parse(readFileSync(fin.pairFile, 'utf8'));
  assert.deepEqual([pair.stage.slot, pair.stage.task, pair.stage.round, pair.stage.masks, pair.stage.ownerGo.date], [unit.pair, unit.task, unit.round, unit.slot.masks, '2026-11-21'], 'the pair record gains the slot\'s task, round, masks and the go line');
  const unknownModel = finalizerOf({ unit, go: null, alias: 'nonesuch' })({ seriesDir: rig.ctx.seriesDir, map: result.map, pair: unit.pair, pause: null });
  assert.deepEqual(unknownModel.problems.filter(p => /explore\.(arm|model|task|round|kind)$/.test(p)), [], 'the leaves are there even when the model is unknown');
  const again = JSON.parse(readFileSync(path.join(rig.ctx.sessionsRoot, records[0].ids.rid, 'record.json'), 'utf8'));
  assert.deepEqual([again.explore.model.known, typeof again.explore.model.why], [false, 'string'], 'an alias the record never resolved says why');
  const old = records[0];
  delete old.explore;
  assert.deepEqual(missingPaths(old, false, false).filter(p => p.startsWith('explore.arm')), [], 'an older record is not asked for the stage leaves');
  assert.ok(missingPaths({ ...old, explore: {} }, false, true).includes('explore.arm'), 'a stage record without them is incomplete');
});

test('A_pilot_unit_has_no_pair_record_and_its_session_says_why_it_has_no_round', async () => {
  const rig = harness();
  const { unit, result } = await runUnitIn({ rig, what: 'aa' });
  assert.equal(result.exit, EXIT.done, result.message);
  const fin = finalizerOf({ unit, go: null, alias: 'opus' })({ seriesDir: rig.ctx.seriesDir, map: result.map, pair: unit.pair, pause: null });
  assert.equal(fin.pairFile, null);
  assert.equal(existsSync(path.join(rig.ctx.seriesDir, 'pairs', 'p0.json')), false);
  const record = JSON.parse(readFileSync(path.join(rig.ctx.sessionsRoot, Object.keys(result.map.sessions)[0], 'record.json'), 'utf8'));
  assert.equal(record.explore.round.known, false);
  assert.match(record.explore.round.why, /an A\/A or pilot session belongs to no round/);
  assert.equal(record.explore.kind.value, 'aa');
});

const fakeWorld = { explore: EXPLORE, paths: { nodeDir: '/n', tsDir: '/t' }, snapshots: { csharp: { dir: '/s', parts: ['x'], record: {} }, node: { dir: '/s', parts: ['x'], record: {} } }, quick: true };

/** The run context of a Stage 1 unit as `loopAndFinalize` builds it. */
function stageContext(seriesDir, stage = {}) {
  return contextOf({ world: fakeWorld, calibration: {}, series: {}, seriesDir, kind: 'data', pair: 1, token: 'fake', claudeBinary: '/c', fill: () => {}, abort: { reason: null, exit: 130 }, home: '/h', policy: { table: {}, policy: {} }, identity: 'id', prompts: {}, stage });
}

test('The_first_session_of_the_series_sets_the_alias_baseline_and_a_later_one_resolving_to_another_id_stops_the_series', async () => {
  const rig = harness();
  const file = path.join(rig.ctx.seriesDir, 'aliases.json');
  const store = stageContext(rig.ctx.seriesDir).aliasStore;
  assert.equal(typeof store.get, 'function', 'a Stage 1 context holds the alias store of the series');
  const { aliasBaseline: _drop, ...ctxOver } = rig.ctx;
  void ctxOver;
  const first = await runUnitIn({ rig, what: 'next', over: { aliasStore: aliasStore(file), aliasBaseline: undefined } });
  assert.equal(first.result.exit, EXIT.done, first.result.message);
  assert.equal(existsSync(file), true, 'the first session of the series set the baseline');
  const baseline = JSON.parse(readFileSync(file, 'utf8'));
  assert.equal(typeof Object.values(baseline.aliases)[0], 'string');
  const drift = harness();
  const driftFile = path.join(drift.ctx.seriesDir, 'aliases.json');
  writeFileSync(driftFile, `${JSON.stringify({ aliases: { opus: 'claude-another-model' }, internal: baseline.internal })}\n`);
  const stopped = await runUnitIn({ rig: drift, what: 'next', over: { aliasStore: aliasStore(driftFile), aliasBaseline: undefined } });
  assert.equal(stopped.result.exit, EXIT.stop, stopped.result.message);
  assert.match(readRunMap(drift.ctx.runMapFile).value.stopped.rule, /^stop:/);
  assert.equal(JSON.parse(readFileSync(driftFile, 'utf8')).aliases.opus, 'claude-another-model', 'a stop never changes the baseline');
});

test('The_context_of_a_stage_pair_carries_the_revealed_seed_the_registered_tag_the_hand_verdicts_and_the_step_and_has_no_spend_ceiling', () => {
  const dir = path.join(path.dirname(harness().ctx.seriesDir), 'ctx-series');
  const stepOf = () => ({ action: 'done' });
  const ctx = stageContext(dir, { seed: SEED, preregTag: 'prereg/lang-v2-20261120', stepOf, handVerdictsFor: rid => [rid] });
  assert.deepEqual([ctx.seed, ctx.preregTag, ctx.stepOf, ctx.handVerdictsFor('r1')], [SEED, 'prereg/lang-v2-20261120', stepOf, ['r1']]);
  const plain = stageContext(dir);
  assert.deepEqual([plain.seed, plain.preregTag, plain.stepOf], [null, null, undefined], 'a quick pair\'s context is as it was');
  assert.equal(ctx.ceilingUsd, 1e9, 'no total ceiling (D7, D53): the cap of a session is the only guard');
  assert.equal(ctx.bareBudgetUsd, 1e9);
});

test('No_total_ceiling_stops_a_slot_whatever_the_ledger_holds_and_five_rounds_stay_planned', async () => {
  const rig = harness();
  writeFileSync(rig.ctx.ledgerFile, `${JSON.stringify({ kind: 'data', usd: 5000, what: 'completed', rid: 'big001', t: '2026-11-20T10:00:00Z' })}\n`);
  const { result } = await runUnitIn({ rig, what: 'next', over: { ceilingUsd: 1e9, bareBudgetUsd: 1e9 } });
  assert.equal(result.exit, EXIT.done, `$5000 already spent and the slot still runs (${result.message})`);
  assert.equal(STAGE_ROUNDS, 5);
  assert.equal(SLOTS.length, 30);
  const killed = { ...Object.values(result.map.sessions)[0], class: 'killed: cap', data: true, status: 'ran' };
  const map = { ...result.map, sessions: { ...result.map.sessions, [killed.rid]: killed } };
  const unit = unitFor({ what: 'next', map });
  assert.deepEqual([unit.action, unit.todo.map(t => [t.rid, t.step])], ['resume', [[killed.rid, 'score']]], 'a session the cap ended is a data attempt: it is scored, never re-run');
});

test('A_session_halted_while_launching_gets_no_facts_file_and_once_it_has_ended_its_facts_are_complete', async () => {
  const rig = harness();
  const { unit, result } = await runUnitIn({ rig, what: 'next' });
  assert.equal(result.exit, EXIT.done, result.message);
  mkdirSync(path.join(rig.ctx.seriesDir, 'pairs'), { recursive: true });
  const [halted, other] = Object.values(result.map.sessions);
  const factsOf = s => path.join(rig.ctx.sessionsRoot, s.rid, 'stage-facts.json');
  const midway = { ...result.map, sessions: { ...result.map.sessions, [halted.rid]: { ...halted, status: 'launching' } } };
  const finalize = finalizerOf({ unit, go: null, alias: 'opus' });
  finalize({ seriesDir: rig.ctx.seriesDir, map: midway, pair: unit.pair, pause: null });
  assert.equal(existsSync(factsOf(halted)), false, 'a launching session may be reset and launched again: its facts wait for its end');
  assert.equal(existsSync(factsOf(other)), true, 'an ended session of the same unit has its facts');
  finalize({ seriesDir: rig.ctx.seriesDir, map: result.map, pair: unit.pair, pause: null });
  assert.equal(existsSync(factsOf(halted)), true, 'once it has ended the one write happens');
  assert.equal(JSON.parse(readFileSync(factsOf(halted), 'utf8')).launch.affinity.known, true, 'with the final evidence');
});
