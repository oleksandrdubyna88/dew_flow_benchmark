// The registered wall is spawn to the CLI process's exit (analysis contract section 5): the harness's own settle, drain and seal after the exit are outside it.
// A stub session whose settle is long (2 s, on a clock the settle advances) shows the wall without it, for a session that ended itself and for one the wall watchdog killed.
import './fake-home.mjs'; // first: nothing here may read the operator's files
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { aaVerdicts } from '../lib/aa-verdicts.mjs';
import { readSeriesFacts } from '../lib/aa-facts.mjs';
import { launchSession } from '../lib/session-launch.mjs';
import { partitionWall } from '../lib/smoke-verdicts.mjs';
import { partition } from '../lib/timeline.mjs';
import { GOOD_LINES, SMOKE_OK, T0, known, makeInputs, makeSeries } from './aa-fixture.mjs';
import { newSession } from './box-helpers.mjs';
import { tmpDir } from './helpers.mjs';
import { RESULT, fakeDeps, goodInit, planFor, say } from './launch-fakes.mjs';

const SETTLE_MS = 2000;
const at = iso => Date.parse(iso);

/** A launch whose box runs `holdMs` of real time, with a settle of 2 s that costs no real time: the deps' clock moves by what `sleep` is asked for. */
async function stubLaunch({ holdMs, over = {} }) {
  const layout = newSession({ root: tmpDir('wall') });
  const skew = { ms: 0 };
  const fake = fakeDeps([], layout, { script: (c, l) => { say(c, goodInit(l)); setTimeout(() => { say(c, RESULT); c.finish(0); }, holdMs); } });
  const deps = { ...fake.deps, now: () => Date.now() + skew.ms, sleep: async ms => { skew.ms += ms; } };
  const result = await launchSession(planFor(layout, { settleMs: SETTLE_MS, tickMs: 40, ...over }), deps);
  assert.equal(result.ok, true, result.reason);
  return { layout, meta: result.value.meta };
}

test('The_recorded_wall_is_spawn_to_the_process_exit_and_the_harness_settle_after_it_is_not_in_it', async () => {
  const { meta } = await stubLaunch({ holdMs: 300 });
  assert.ok(meta.wallMs >= 300 && meta.wallMs < 1500, `the wall is the box's 300 ms, not 2300 ms with the settle (${meta.wallMs})`);
  assert.equal(at(meta.exitedAt) - at(meta.startedAt), meta.wallMs, 'the wall is exitedAt minus startedAt, to the ms');
  assert.ok(at(meta.endedAt) - at(meta.exitedAt) >= SETTLE_MS, 'endedAt stays the end of the harness\'s own work: the settle is between the exit and it');
});

test('The_wall_of_a_session_the_watchdog_killed_is_spawn_to_the_exit_after_the_kill', async () => {
  const layout = newSession({ root: tmpDir('wall') });
  const skew = { ms: 0 };
  const fake = fakeDeps([], layout, { script: (c, l) => say(c, goodInit(l)) });
  const deps = { ...fake.deps, now: () => Date.now() + skew.ms, sleep: async ms => { skew.ms += ms; } };
  const result = await launchSession(planFor(layout, { settleMs: SETTLE_MS, tickMs: 40, wallMs: 150 }), deps);
  assert.equal(result.ok, true, result.reason);
  const { meta } = result.value;
  assert.equal(meta.stopped, 'wall');
  assert.ok(meta.wallMs >= 150 && meta.wallMs < 1500, `the wall is the watchdog's 150 ms plus the kill, without the settle (${meta.wallMs})`);
  assert.ok(at(meta.exitedAt) >= at(meta.stoppedAt), 'the exit is the one after the kill');
  assert.equal(at(meta.exitedAt) - at(meta.startedAt), meta.wallMs);
});

test('G6_holds_within_5_percent_on_a_stub_whose_settle_is_long_with_no_explained_line', async () => {
  const { layout, meta } = await stubLaunch({ holdMs: 1500 });
  const stamped = readFileSync(layout.files.streamStamped, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  const wall = partitionWall(meta, stamped);
  assert.equal(wall.endMs - wall.startMs, meta.wallMs, 'the partition is cut from the registered wall');
  const spans = [{ category: 'model_api', agentId: 'main', startMs: stamped[0].t, endMs: stamped.at(-1).t }];
  const part = partition(wall, spans);
  assert.equal(part.known, true);
  const record = { time: { wallMs: known(meta.wallMs), partition: known({ categories: part.categories, unattributed: part.unattributed, waits: part.waits }) } };
  const r = aaVerdicts({ seriesDir: makeSeries({ records: { aa0001: record } }), inputsDir: makeInputs(GOOD_LINES), smoke: SMOKE_OK, now: () => new Date(T0) });
  assert.equal(r.ok, true, r.reason);
  const g6 = r.value.rows.find(x => x.id === 'G-6');
  assert.equal(g6.verdict, 'holds', g6.observed);
  assert.equal(GOOD_LINES.some(l => l.includes('| G-6 |')), false, 'no explained line carries it');
});

test('The_wall_ends_at_the_exit_event_not_at_the_close_that_waits_for_the_pipes', async () => {
  const layout = newSession({ root: tmpDir('wall') });
  const fake = fakeDeps([], layout, { script: (c, l) => { say(c, goodInit(l), RESULT); setTimeout(() => c.emit('exit', 0, null), 100); setTimeout(() => c.finish(0), 700); } });
  const result = await launchSession(planFor(layout, { settleMs: 0 }), fake.deps);
  assert.equal(result.ok, true, result.reason);
  const { meta } = result.value;
  assert.ok(meta.wallMs >= 100 && meta.wallMs < 500, `the wall stops at the process's exit (100 ms), not at the pipes' close (700 ms): ${meta.wallMs}`);
});

test('The_samplers_window_of_a_session_is_its_wall_and_a_meta_of_an_earlier_harness_keeps_its_own_end', () => {
  const meta = extra => ({ 'meta.json': `${JSON.stringify({ rid: 'aa0001', startedAt: new Date(T0).toISOString(), endedAt: new Date(T0 + 50000).toISOString(), wallMs: 50000, stopped: null, samplers: {}, mask: '0-5', ...extra })}\n` });
  const inWindow = extra => {
    const series = makeSeries({ files: { aa0001: meta(extra) } });
    const facts = readSeriesFacts({ seriesDir: series, inputsDir: makeInputs(GOOD_LINES) });
    assert.equal(facts.ok, true, facts.reason);
    return facts.value.subjects.aa.find(f => f.rid === 'aa0001').samples.length;
  };
  assert.equal(inWindow({ exitedAt: new Date(T0 + 20000).toISOString(), wallMs: 20000 }), 5, 'the samples of the first 20 s: the harness\'s 30 s after the exit are not the session\'s');
  assert.equal(inWindow({}), 11, 'no exitedAt: the window ends at endedAt, as it did');
});

test('A_meta_without_an_exit_instant_cuts_the_partition_from_the_stamped_streams_span_as_before', () => {
  const stamped = [{ t: 1000 }, { t: 5000 }];
  assert.deepEqual(partitionWall({ startedAt: new Date(0).toISOString() }, stamped), { startMs: 1000, endMs: 5000 });
  assert.deepEqual(partitionWall({ startedAt: new Date(0).toISOString(), exitedAt: new Date(7000).toISOString() }, stamped), { startMs: 0, endMs: 7000 });
  assert.equal(partitionWall(null, [{ t: 1 }]), null, 'one stamp is no wall');
});
