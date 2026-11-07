// `replay`: a diagnostic reading of a rejected case on the current runner. With fakes and a temporary home: the one file it writes, what it keeps, that it changes nothing else (run-map, ledger, records, `score/`), that it takes the lock and
// refuses while a run holds it, and that a harness failure is a void run and never a verdict. No process starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { cmdReplay } from '../lib/commands.mjs';
import { createHash } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import { runnerPinNow } from '../lib/harness-pin.mjs';
import { planSpend } from '../lib/pause.mjs';
import { DIAGNOSTIC_NOTE, replay } from '../lib/replay.mjs';
import { ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { pinsOfTask } from '../lib/tasks.mjs';
import { quickScoreFn } from '../lib/world.mjs';
import { built, frozenFiles, ran, rec, scoringArea } from './quick-score-fixture.mjs';
import { EXPLORE, PLAN, TASK, world } from './quick-series-fixture.mjs';

const EVIDENCE = { expected: 'recv c "b"', group: false, omitted: 0, observed: [{ kind: 'recv', conn: 'c', text: 'recv c "x"', data: '"x"', stepLine: 4, stepText: 'invented step' }] };
const REJECT = { id: 'h003', set: 'heldout', verdict: 'wrong answer', reason: 'entry 3 of 4 differs', wallMs: 812, peakBytes: 4096, firstMismatch: { entry: 3, of: 4, part: '', detail: 'first difference at byte 0', evidence: EVIDENCE } };
const goodScore = (cases = [REJECT]) => ok({ build: { ok: true }, explore: { build: { ok: true, status: 0, wallMs: 90, timedOut: false }, cases, frozenChanges: [], runner: '0.3.2', memoryCertified: true } });

function ioFor(w, { scores = [goodScore()], calls = [], extra = {} } = {}) {
  const queue = [...scores];
  return {
    home: w.home, say: line => calls.push(['say', line]), calls, now: () => new Date('2026-11-05T19:30:15.123Z'),
    worldFn: async () => ok({ explore: EXPLORE, paths: {}, snapshots: {}, quick: true, scoreFn: async args => { calls.push(['score', args]); return queue.length > 1 ? queue.shift() : queue[0]; } }),
    ...extra,
  };
}

const snapshotOf = w => ({ runMap: readFileSync(runMapFileOf(w.dir), 'utf8'), ledger: readFileSync(ledgerFileOf(w.dir), 'utf8'), session: readdirSync(w.layout.dir).sort() });
const run = (w, io, over = {}) => replay({ task: TASK, seriesDir: w.dir, rid: 'a1', caseId: 'h003', io, ...over });

test('A_replay_writes_one_new_file_with_the_verdict_and_the_kept_evidence_and_changes_nothing_else', async () => {
  const w = world();
  const spend = planSpend({ home: w.home, explore: EXPLORE, plan: PLAN });
  const before = snapshotOf(w);
  const io = ioFor(w);
  const r = await run(w, io);
  assert.equal(r.exit, 0, r.message);
  assert.equal(r.file, path.join(w.layout.dir, 'replay', '2026-11-05T19-30-15-123Z.json'));
  const body = JSON.parse(readFileSync(r.file, 'utf8'));
  assert.equal(body.diagnostic, true);
  assert.equal(body.note, DIAGNOSTIC_NOTE);
  assert.deepEqual([body.rid, body.caseId, body.stack, body.times, body.sessionClass], ['a1', 'h003', 'node', 1, 'void: contaminated']);
  assert.deepEqual(body.runner, { ...runnerPinNow() });
  assert.deepEqual(body.runs.map(x => [x.run, x.void, x.verdict, x.reason, x.wallMs]), [[1, false, 'wrong answer', 'entry 3 of 4 differs', 812]]);
  assert.deepEqual(body.runs[0].firstMismatch.evidence, EVIDENCE, 'what the case saw is kept');
  assert.equal(body.runs[0].memoryCertified, true);
  const after = snapshotOf(w);
  assert.equal(after.runMap, before.runMap, 'the run-map is as it was');
  assert.equal(after.ledger, before.ledger, 'and the ledger');
  assert.deepEqual(after.session, [...before.session, 'replay'].sort(), 'the session folder gained only replay/; no score/ and no record');
  assert.deepEqual(readdirSync(path.join(w.layout.dir, 'replay')), ['2026-11-05T19-30-15-123Z.json'], 'the scratch folder is gone');
  assert.deepEqual(planSpend({ home: w.home, explore: EXPLORE, plan: PLAN }), spend);
  assert.deepEqual(readLedger(ledgerFileOf(w.dir)).filter(l => l.event !== undefined), []);
  assert.ok(io.calls.some(([k, l]) => k === 'say' && /run 1: wrong answer .*evidence kept/.test(l)), 'the same lines go to standard output');
});

test('A_replay_scores_exactly_the_named_case_of_the_kept_tree_and_says_whether_the_runner_may_have_moved', async () => {
  const w = world();
  const io = ioFor(w);
  await run(w, io, { times: 1 });
  const [, args] = io.calls.find(([k]) => k === 'score');
  assert.deepEqual([args.only, args.stack, args.source, args.driftPossible, typeof args.history], ['h003', 'node', w.layout.work, true, 'function'], 'the series recorded no runner pin, and the scoring is given its history');
  assert.match(args.work, /replay\/work-/, 'the scratch folder is inside replay/, never score/');
  assert.equal(existsSync(args.work), false, 'and is removed');
});

// ---- the quick world's single-pass scoring (final seam review of F1, item 2) ----------------------------------------------------------------------------------

const sha = text => createHash('sha256').update(text).digest('hex');
const OLD_LAUNCHER = '#!/bin/bash\n';

/** The quick world's `scoreFn` (the one `loadQuickWorld` builds) over the scoring area `a`: the frozen set holds a NEW `build.sh`; the tree holds the OLD one, as a tree of a session begun before the template moved does. */
function quickWorldScore(a, over = {}) {
  const files = { ...frozenFiles(), 'build.sh': { bytes: Buffer.from('#!/bin/bash\n# the template of today\n'), mode: 0o755 } };
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, caseWallMs: 1, runCeilingMs: 1 };
  const quickSeams = { buildFn: built(), runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) };
  const scoreFn = quickScoreFn({ frozenFor: () => files, work: a.work, profile, quickSeams, ...over });
  return args => scoreFn({ stack: 'node', source: a.source, sets: [{ name: 'heldout', dir: a.held }], limits: { memoryBytes: 512 * 1024 * 1024, buildTimeoutMs: 600_000, timeMs: 300_000 }, ...args });
}

test('The_quick_worlds_single_pass_scoring_judges_a_template_file_by_its_history_so_a_moved_template_is_the_harness_and_not_the_agent', async t => {
  const a = scoringArea(t);
  const old = { known: true, sha256: sha(OLD_LAUNCHER) };
  const history = ({ rel }) => (rel === 'build.sh' ? old : { known: true, sha256: 'f'.repeat(64) });
  const score = quickWorldScore(a);
  const moved = await score({ history, driftPossible: true });
  assert.equal(moved.ok, true, moved.reason);
  assert.deepEqual(moved.value.explore.frozenChanges, [], 'the tree has the bytes the file had when the series began: not the agent\'s');
  assert.deepEqual(moved.value.explore.harnessDrift.map(c => c.path), ['build.sh'], 'it is the harness\'s');
  const without = await score({ driftPossible: true });
  assert.deepEqual(without.value.explore.frozenChanges.map(c => c.path), ['build.sh'], 'the control: with no history the same change is charged to the agent, which is what replay did before');
});

test('The_quick_worlds_single_pass_scoring_hands_scoreQuick_the_history_the_drift_flag_and_the_case', async t => {
  const a = scoringArea(t);
  const seen = [];
  const history = () => ({ known: false, why: 'x' });
  const score = quickWorldScore(a, { scoreFn: async args => { seen.push(args); return ok({}); } });
  await score({ history, driftPossible: true, only: 'h001' });
  assert.deepEqual([seen[0].history === history, seen[0].driftPossible, seen[0].only], [true, true, 'h001']);
});

test('A_replay_line_names_the_files_that_could_not_be_judged_and_the_contract_the_score_was_made_under', async () => {
  const w = world();
  const undecided = [{ path: 'STATEMENT.md', state: 'changed', why: 'the bytes cannot be read' }];
  const score = ok({ build: { ok: true }, explore: { build: { ok: true, status: 0, wallMs: 90, timedOut: false }, cases: [REJECT], frozenChanges: [], undecidedDrift: undecided, contractIdentity: 'c'.repeat(64) } });
  const r = await run(w, ioFor(w, { scores: [score] }));
  const [line] = JSON.parse(readFileSync(r.file, 'utf8')).runs;
  assert.deepEqual(line.undecidedDrift, undecided, 'unknown deviations are not zero');
  assert.equal(line.contractIdentity, 'c'.repeat(64));
  const other = world();
  const plain = await run(other, ioFor(other, { scores: [goodScore()] }), { times: 1 });
  const [bare] = JSON.parse(readFileSync(plain.file, 'utf8')).runs;
  assert.deepEqual([bare.undecidedDrift, bare.contractIdentity], [[], null], 'a score that names neither says so: no undecided file, no identity');
});

test('Times_runs_the_case_that_many_times_each_one_its_own_line', async () => {
  const w = world();
  const second = ok({ build: { ok: true }, explore: { build: { ok: true }, cases: [{ ...REJECT, verdict: 'pass', reason: '', firstMismatch: null }], frozenChanges: [] } });
  const io = ioFor(w, { scores: [goodScore(), second, goodScore()] });
  const r = await run(w, io, { times: 3 });
  const runs = JSON.parse(readFileSync(r.file, 'utf8')).runs;
  assert.deepEqual(runs.map(x => [x.run, x.verdict]), [[1, 'wrong answer'], [2, 'pass'], [3, 'wrong answer']]);
  assert.equal(io.calls.filter(([k]) => k === 'score').length, 3);
});

test('A_run_that_is_the_harness_failing_is_a_void_run_in_the_file_and_never_a_verdict_and_the_exit_is_3', async () => {
  const w = world();
  const r = await run(w, ioFor(w, { scores: [fail('the layer halted: memory-not-certifiable', { kind: 'environment' })] }));
  assert.equal(r.exit, 3);
  const [line] = JSON.parse(readFileSync(r.file, 'utf8')).runs;
  assert.deepEqual([line.void, line.kind, line.verdict], [true, 'environment', undefined]);
  assert.match(line.reason, /memory-not-certifiable/);
});

test('A_replay_takes_the_series_lock_refuses_while_a_run_holds_it_writes_nothing_then_and_releases_it_afterwards', async () => {
  const w = world();
  const holder = acquireSeriesLock({ seriesDir: w.dir });
  const before = snapshotOf(w);
  const io = ioFor(w);
  const refused = await run(w, io);
  assert.equal(refused.exit, 4);
  assert.deepEqual([snapshotOf(w), io.calls.length], [before, 0], 'nothing was written or scored');
  holder.value.release();
  const lockSeen = [];
  const watching = ioFor(w, { extra: { worldFn: async () => { lockSeen.push(acquireSeriesLock({ seriesDir: w.dir }).ok); return ok({ explore: EXPLORE, paths: {}, snapshots: {}, quick: true, scoreFn: async () => goodScore() }); } } });
  assert.equal((await run(w, watching)).exit, 0);
  assert.deepEqual(lockSeen, [false], 'while it ran, the lock was its own');
  assert.equal(acquireSeriesLock({ seriesDir: w.dir }).ok, true, 'and it is released afterwards');
});

test('A_replay_refuses_a_bad_case_a_bad_times_an_unknown_session_a_session_without_a_tree_and_moved_pins_and_writes_nothing', async () => {
  const w = world();
  const before = snapshotOf(w);
  const io = ioFor(w);
  for (const [name, over, pattern] of [['a bad case', { caseId: 'x1' }, /--case names/], ['times 0', { times: 0 }, /--times is a whole number/], ['times 99', { times: 99 }, /--times/], ['no such session', { rid: 'zz' }, /no session zz/]]) {
    const r = await replay({ task: TASK, seriesDir: w.dir, rid: 'a1', caseId: 'h003', io, ...over });
    assert.equal(r.exit, 4, name);
    assert.match(r.message, pattern, name);
  }
  assert.deepEqual([snapshotOf(w), io.calls.filter(([k]) => k === 'score').length], [before, 0]);
  const moved = world({ taskPins: { ...pinsOfTask(TASK), spec: { ...TASK.spec, sha256: '0'.repeat(64) } } });
  const r = await run(moved, ioFor(moved));
  assert.equal(r.exit, 4);
  assert.match(r.message, /pins of task .* changed since series/);
  assert.equal(existsSync(path.join(moved.layout.dir, 'replay')), false);
});

test('A_replay_of_a_session_whose_workspace_is_gone_is_refused', async () => {
  const w = world();
  (await import('node:fs')).rmSync(w.layout.work, { recursive: true });
  const r = await run(w, ioFor(w));
  assert.equal(r.exit, 4);
  assert.match(r.message, /has no workspace/);
});

test('The_command_refuses_a_missing_argument_before_anything_is_read', async () => {
  const said = [];
  const code = await cmdReplay({ task: TASK.id, series: '/nowhere', rid: 'a1' }, { home: '/nowhere', say: l => said.push(l) });
  assert.equal(code, 4);
  assert.match(said.join('\n'), /replay needs --case/);
  assert.equal(await cmdReplay({ series: '/x', rid: 'a', case: 'h001' }, { home: '/nowhere', say: l => said.push(l) }), 4, 'no task');
});

test('A_stop_signal_ends_a_replay_between_runs_removes_the_scratch_folder_and_writes_no_file', async () => {
  const w = world();
  const ctl = new AbortController();
  const before = snapshotOf(w);
  const io = ioFor(w, { extra: { signal: ctl.signal, abort: { reason: 'SIGINT', exit: 130 } } });
  const scoreFn = async args => { io.calls.push(['score', args]); ctl.abort(); return goodScore(); };
  io.worldFn = async () => ok({ explore: EXPLORE, paths: {}, snapshots: {}, quick: true, scoreFn });
  const r = await run(w, io, { times: 3 });
  assert.equal(r.exit, 130, r.message);
  assert.match(r.message, /stopped by SIGINT after 1 of 3 run\(s\).*nothing was written/);
  assert.equal(io.calls.filter(([k]) => k === 'score').length, 1, 'no run started after the signal');
  assert.deepEqual(readdirSync(path.join(w.layout.dir, 'replay')), [], 'no work folder and no partial file stay behind');
  const after = snapshotOf(w);
  assert.deepEqual([after.runMap, after.ledger], [before.runMap, before.ledger]);
});

test('A_stop_signal_that_arrives_after_the_last_run_still_leaves_the_whole_file', async () => {
  const w = world();
  const ctl = new AbortController();
  const io = ioFor(w, { extra: { signal: ctl.signal, abort: { reason: 'SIGINT', exit: 130 } } });
  io.worldFn = async () => ok({ explore: EXPLORE, paths: {}, snapshots: {}, quick: true, scoreFn: async () => { ctl.abort(); return goodScore(); } });
  const r = await run(w, io, { times: 1 });
  assert.equal(r.exit, 0, r.message);
  assert.equal(readdirSync(path.join(w.layout.dir, 'replay')).length, 1, 'the complete file only');
});
