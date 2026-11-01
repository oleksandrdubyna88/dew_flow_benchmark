// The $0 rehearsal per task, end to end (plan research/PLAN_lang_explore_olympiad.md T-O6), and a stub pair past the $60 pause point by `--allow-over` (D-O7's record). The task material is a TOY task in a temp directory (see
// `olympiad-fixture.mjs`): the entries of tasks 02, 03 and 09 are replaced in a copy of the pins by toy ones, so the real toolchains, boxes, broker and judge run while no real `heldout-NN/` is read. Each series made
// is removed. The tests of this file run one after the other: each uses the two CPU masks. A host that lacks the toolchains, bwrap or the snapshots is `hostLacks`.
import { realHome } from '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { TOKEN_FILE_PARTS } from '../../minigit-bench/capture/capture-env.mjs';
import { appendLedger, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { stubClaude } from '../../minigit-bench/lib/stub-cli.mjs';
import { chunksOf, sseAnswer, sseBody, stubUpstream } from '../../minigit-bench/lib/stub-upstream.mjs';
import { hostLacks, sandboxGate, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { machineFor, pilotQuiet } from '../lib/machine.mjs';
import { bannerOf } from '../lib/note-tasks.mjs';
import { loadHeldout } from '../lib/heldout.mjs';
import { loadPrompts } from '../lib/plan.mjs';
import { referenceFill, rehearse } from '../lib/rehearse.mjs';
import { runPair } from '../lib/run.mjs';
import { removeArea } from '../lib/score.mjs';
import { createSeries, drawMask, ledgerFileOf, loadSeries, runMapFileOf, seriesDirs } from '../lib/series.mjs';
import { planSeriesDirs } from '../lib/pause.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { toyExplore, writeToyHeldout } from './olympiad-fixture.mjs';

const REAL = loadExplore().value;
const subject = { branch: 'test', sha: 'a'.repeat(40), harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: 'b'.repeat(12), pilotIdentity: 'c'.repeat(40) };

/** A toy world for `ids`, or null (`hostLacks`) when this host cannot score here (no bwrap, or the real toolchains and snapshots are not prepared). */
async function toyOrNull(t, ids, planTasks) {
  if (!sandboxGate(t)) return null;
  const toy = toyExplore({ real: REAL, home: realHome, ids, planTasks });
  const probe = await toy.worldFn({ home: realHome, work: tmpDir('o-toy-world'), task: resolveTask(toy.explore, ids[0]).value });
  if (!probe.ok) return hostLacks(t, probe.reason) && null;
  for (const id of ids) writeToyHeldout({ home: realHome, task: resolveTask(toy.explore, id).value });
  return toy;
}

test('The_rehearsal_runs_a_pair_end_to_end_for_free_for_each_of_tasks_02_03_and_09_over_that_task_s_own_material', { timeout: 1_200_000 }, async t => {
  const toy = await toyOrNull(t, ['02', '03', '09']);
  if (!toy) return;
  for (const id of ['02', '03', '09']) {
    const task = resolveTask(toy.explore, id).value;
    const said = [];
    const r = await rehearse({ task, io: { home: realHome, say: l => said.push(l), abort: { reason: null, exit: 130 }, signal: undefined, worldFn: toy.worldFn, subject, now: () => new Date('2031-02-03T04:05:06Z') } });
    t.after(() => r.seriesDir && removeArea(r.seriesDir));
    assert.equal(r.exit, 0, `${id}: ${r.message}\n${JSON.stringify(r.checks)}\n${said.join('\n')}`);
    assert.deepEqual(r.checks.map(c => c.ok), [true, true, true, true, true], id);
    assert.deepEqual(r.problems, [], id);
    assert.equal(loadSeries(r.seriesDir).value.state.task, id, 'the rehearsal series records its task');
    assert.ok(readLedger(path.join(r.seriesDir, 'ledger.ndjson')).every(l => l.kind === 'rehearsal' && l.usd === 0), `${id}: a rehearsal costs nothing and says so`);
    const note = readFileSync(path.join(r.seriesDir, 'rehearsal-note.md'), 'utf8');
    assert.ok(note.startsWith(`# Exploratory pilot: task ${id} on the v1 bare harness, 2 session(s) (2031-02-03)`), `${id}: the task and the injected clock's day are in the note's title`);
    assert.ok(note.includes(bannerOf(task)), `${id}: the note carries its task's banner`);
    assert.match(note, new RegExp(`- Task: spec ${id} at `), `${id}: the note names the task's spec pin`);
  }
});

/** A home with a fake token file, for the preflight's existence check. */
function fakeHome() {
  const home = tmpDir('o-rehearse-home');
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, 'sk-ant-oat01-fake\n', { mode: 0o600 });
  chmodSync(path.dirname(file), 0o700);
  return home;
}

test('A_stub_pair_past_the_pause_point_runs_only_with_allow_over_and_the_owner_s_text_is_in_the_pair_record', { timeout: 600_000 }, async t => {
  const planBefore = planSeriesDirs(realHome, REAL);
  const toy = await toyOrNull(t, ['77'], ['77']);
  if (!toy) return;
  const task = resolveTask(toy.explore, '77').value;
  const calibration = JSON.parse(readFileSync(path.join(toy.root, 'calibration-77.json'), 'utf8'));
  const made = createSeries({ home: realHome, explore: toy.explore, calibration, task: '77', draw: drawMask(() => 1), inputsExtra: { test: true, prompts: loadPrompts() } });
  const dir = made.value.dir;
  t.after(() => removeArea(dir));
  assert.deepEqual(planSeriesDirs(realHome, REAL), planBefore, 'the test series is of a toy task: it never sits among the real plan\'s series (review F2)');
  assert.deepEqual(seriesDirs(realHome, '02').filter(d => d === dir), []);
  for (let i = 0; i < 58; i += 1) appendLedger(ledgerFileOf(dir), { kind: 'pilot', usd: 1, what: 'completed', rid: `old${i}`, t: '2026-11-01T00:00:00Z' });
  const stub = await stubUpstream(sseAnswer(chunksOf(sseBody(), 3), 20));
  t.after(() => stub.close());
  const said = [];
  const quiet = { hostCpus: 0.1, harnessCpus: 0.05, foreignCpus: 0, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 };
  const machine = machineFor({ thresholds: pilotQuiet().value, calibratedMs: 20, seriesDir: dir, samplerFn: async () => async () => quiet, sleepFn: async () => {}, now: (() => { let n = 0; return () => (n += 7000); })() });
  const seams = { fill: referenceFill({ world: { filesFor: (await toy.worldFn({ home: realHome, work: tmpDir('o-toy-w2'), task })).value.filesFor }, heldout: loadHeldout(realHome, task).value }), claudeBinary: stubClaude(tmpDir('o-toy-stub')), planOver: { upstream: stub.upstream, tickMs: 500, sampleMs: 500, settleMs: 500, graceMs: 2000 }, preflightHome: fakeHome(), stub: true, machine };
  const io = (over = {}) => ({ home: realHome, say: l => said.push(l), abort: { reason: null, exit: 130 }, worldFn: toy.worldFn, tokenFn: async () => ({ ok: true, value: 'sk-ant-fake-for-the-test' }), seams, ...over });
  const before = readFileSync(runMapFileOf(dir), 'utf8');
  const stopped = await runPair({ seriesDir: dir, pair: 1, explore: toy.explore, task, io: io() });
  assert.equal(stopped.exit, 2, stopped.message);
  assert.match(stopped.message, /the plan's spend is \$58\.0000/);
  assert.equal(readFileSync(runMapFileOf(dir), 'utf8'), before, 'nothing was planned or launched');
  const passed = await runPair({ seriesDir: dir, pair: 1, explore: toy.explore, task, io: io({ allowOver: 'the owner, 2026-11-01: go on' }) });
  assert.equal(passed.exit, 0, `${passed.message}\n${said.join('\n')}`);
  const pair = JSON.parse(readFileSync(path.join(dir, 'pairs', 'p1.json'), 'utf8'));
  assert.deepEqual(pair.pauseOverrides.map(o => [o.text, o.needed, o.spentUsd, o.pausePointUsd]), [['the owner, 2026-11-01: go on', true, 58, 60]], 'the owner\'s words are in the pair record');
  assert.equal(existsSync(path.join(dir, 'pairs', 'p1.json')), true);
});
