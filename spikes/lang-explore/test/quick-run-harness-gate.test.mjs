// `run` refuses a pair on a moved harness BEFORE it launches anything (final seam review of F1, the lead's addition to item 3): the same comparison as the scoring step's (`lib/harness-moved.mjs`), made for both stacks of the series
// right after the pair's prerequisites are in hand and before the pair is planned into the run-map, so no session starts and nothing is spent. The refusal is exit 3 (resumable), and its reason is a `harness-moved` event line in the
// ledger (an event, never a charge). The scoring step keeps its own check as the second line of defence. Fakes for the prerequisites and the loop; a temp home; nothing starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { readRunMap, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { templatesIdentity } from '../lib/contract-identity.mjs';
import { runnerPinNow } from '../lib/harness-pin.mjs';
import { planSpend } from '../lib/pause.mjs';
import { runPair } from '../lib/run.mjs';
import { ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { EXPLORE, PLAN, TASK, world as baseWorld } from './quick-series-fixture.mjs';

const sha = text => createHash('sha256').update(text).digest('hex');
const file = text => ({ bytes: Buffer.from(text), mode: 0o644 });
const FILES = { 'STATEMENT.md': file('the statement\n'), 'samples/load.json': file('{}\n'), 'PACKAGES.md': file('the packages\n') };
const generated = { 'STATEMENT.md': sha('the statement\n'), 'samples/load.json': sha('{}\n') };

/** The inputs of a series made on today's harness, for both stacks of the fake workspace. */
const inputsNow = (over = {}) => ({ runnerPin: runnerPinNow(), templatesIdentity: templatesIdentity(), generatedSha256: { csharp: generated, node: generated }, packagesMdSha256: { csharp: sha('the packages\n'), node: sha('the packages\n') }, ...over });

/** A canonical series of the quick task with no session yet: pair 1 is to be planned. */
function freshSeries(over) {
  const w = baseWorld({ prompts: { prompts: {} }, ...over }, { prefix: 'x' });
  writeRunMap(runMapFileOf(w.dir), { ...readRunMap(runMapFileOf(w.dir)).value, sessions: {} });
  return w;
}

function ioFor(w, log) {
  const fake = { explore: EXPLORE, paths: {}, snapshots: {}, quick: true, filesFor: () => FILES };
  return {
    home: w.home, say: () => {}, now: () => new Date('2026-11-07T09:00:00.000Z'), seriesNameOk: () => true,
    runSeams: { prerequisitesFn: async () => ok({ calibration: {}, heldout: {}, world: fake, token: 'fake-token', prompts: {} }), loopFn: async () => { log.push('LAUNCH'); return { exit: 0, message: 'done' }; } },
  };
}

const run = (w, io) => runPair({ seriesDir: w.dir, pair: 1, io, explore: EXPLORE, task: TASK });
const snapshot = w => readFileSync(runMapFileOf(w.dir), 'utf8');
const events = w => readLedger(ledgerFileOf(w.dir)).filter(l => l.event === 'harness-moved');

test('A_pair_on_a_harness_that_moved_is_refused_with_exit_3_before_anything_is_launched_planned_or_spent_and_the_reason_is_recorded', async () => {
  for (const [what, over] of [['the templates', { templatesIdentity: 'b'.repeat(64) }], ['the runner pin', { runnerPin: { ...runnerPinNow(), manifestSha256: 'a'.repeat(64) } }], ['STATEMENT.md', { generatedSha256: { csharp: { ...generated, 'STATEMENT.md': 'c'.repeat(64) }, node: generated } }]]) {
    const w = freshSeries(inputsNow(over));
    const [log, before, spend] = [[], snapshot(w), planSpend({ home: w.home, explore: EXPLORE, plan: PLAN })];
    const r = await run(w, ioFor(w, log));
    assert.equal(r.exit, 3, `${what}: a resumable refusal`);
    assert.match(r.message, /^harness-moved: .*/);
    assert.ok(r.message.includes(what), `${what}: named`);
    assert.deepEqual(log, [], `${what}: the launch seam is never called`);
    assert.equal(snapshot(w), before, `${what}: the pair is not planned into the run-map`);
    assert.deepEqual(planSpend({ home: w.home, explore: EXPLORE, plan: PLAN }), spend, `${what}: nothing is spent`);
    assert.deepEqual(events(w).map(e => [e.event, e.pair, e.reason, e.t, e.usd]), [['harness-moved', 1, r.message, '2026-11-07T09:00:00.000Z', undefined]], `${what}: an event line, never a charge`);
  }
});

test('A_series_that_recorded_no_harness_is_refused_too_and_one_on_the_harness_it_was_made_with_launches', async () => {
  const bare = freshSeries({});
  const refusedLog = [];
  assert.equal((await run(bare, ioFor(bare, refusedLog))).exit, 3, 'nothing recorded cannot be seen');
  assert.deepEqual(refusedLog, []);
  const w = freshSeries(inputsNow());
  const log = [];
  const r = await run(w, ioFor(w, log));
  assert.deepEqual([r.exit, log], [0, ['LAUNCH']], 'the control: an unmoved harness goes on to the launch');
  assert.deepEqual(events(w), [], 'and records nothing');
});

test('Only_one_of_the_two_stacks_moved_is_enough_to_refuse', async () => {
  const w = freshSeries(inputsNow({ generatedSha256: { csharp: generated, node: { ...generated, 'samples/load.json': 'd'.repeat(64) } } }));
  const log = [];
  const r = await run(w, ioFor(w, log));
  assert.deepEqual([r.exit, log.length], [3, 0]);
  assert.match(r.message, /samples\/load\.json/);
});
