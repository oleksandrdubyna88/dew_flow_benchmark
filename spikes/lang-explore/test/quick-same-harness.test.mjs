// A series is run on ONE harness (final seam review of F1, item 3; parent D70). `run` gives its scoring no way to tell the harness's moves from the agent's changes, so when the runner pin, the templates' identity or the bytes of
// the generated files (and the derived packages list) differ from what the series recorded at `init`, the scoring is a recorded, resumable halt `harness-moved`: no build is made and nothing is scored. A rescore reads the kept tree by
// the series' history instead, and `run` hands its scoring the same `{ driftPossible, history }` as a rescore does. Invented fixtures only; no box, no layer, no process.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { runnerPinNow } from '../lib/harness-pin.mjs';
import { harnessMovedForStacks, harnessMovedProblem } from '../lib/harness-moved.mjs';
import { templatesIdentity } from '../lib/contract-identity.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { scoreSessionDep } from '../lib/run-ctx.mjs';
import { harness } from './quick-passes-harness.mjs';
import { world } from './quick-series-fixture.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';

const sha = text => createHash('sha256').update(text).digest('hex');
const file = text => ({ bytes: Buffer.from(text), mode: 0o644 });
const FILES = { 'STATEMENT.md': file('the statement\n'), 'samples/load.json': file('{}\n'), 'PACKAGES.md': file('the packages\n'), 'run.sh': file('#!/bin/bash\n') };

/** The inputs of a series made on today's harness, for the files `FILES` of a `node` workspace. */
const inputsNow = () => ({
  runnerPin: runnerPinNow(), templatesIdentity: templatesIdentity(),
  generatedSha256: { node: { 'STATEMENT.md': sha('the statement\n'), 'samples/load.json': sha('{}\n') } }, packagesMdSha256: { node: sha('the packages\n') },
});
const problem = (inputs, files = FILES) => harnessMovedProblem({ inputs, stack: 'node', files });

test('A_series_run_on_the_harness_it_was_made_with_has_no_problem', () => {
  assert.equal(problem(inputsNow()), null);
});

test('A_moved_runner_pin_a_moved_templates_identity_a_moved_generated_file_and_a_moved_packages_list_are_each_named', () => {
  const base = inputsNow();
  const cases = [
    ['the runner pin', { ...base, runnerPin: { ...base.runnerPin, manifestSha256: 'a'.repeat(64) } }, FILES],
    ['the templates', { ...base, templatesIdentity: 'b'.repeat(64) }, FILES],
    ['STATEMENT.md', base, { ...FILES, 'STATEMENT.md': file('another statement\n') }],
    ['samples/load.json', base, { ...FILES, 'samples/load.json': file('{"x":1}\n') }],
    ['PACKAGES.md', base, { ...FILES, 'PACKAGES.md': file('other packages\n') }],
  ];
  for (const [what, inputs, files] of cases) {
    const sentence = problem(inputs, files);
    assert.match(sentence ?? '', /^harness-moved: /, `${what}: a sentence that starts with the halt's name`);
    assert.ok(sentence?.includes(what), `${what}: and names what moved`);
  }
});

test('A_file_the_workspace_has_not_got_is_a_recorded_null_and_the_same_null_is_no_move', () => {
  const inputs = { ...inputsNow(), generatedSha256: { node: { 'STATEMENT.md': sha('the statement\n'), 'samples/load.json': null } } };
  const { 'samples/load.json': _gone, ...without } = FILES;
  assert.equal(problem(inputs, without), null, 'the series had no load file and the workspace has none');
  assert.match(problem(inputs) ?? '', /samples\/load\.json/, 'the control: the workspace now has one');
});

test('A_series_that_recorded_nothing_cannot_be_told_from_a_moved_one_and_is_refused_too', () => {
  for (const key of ['runnerPin', 'templatesIdentity', 'generatedSha256', 'packagesMdSha256']) {
    const { [key]: _dropped, ...inputs } = inputsNow();
    assert.match(problem(inputs) ?? '', /^harness-moved: .*recorded no/, `${key}: what was not recorded cannot be seen, so it is not judged`);
  }
});

// ---- the halt in the pass driver --------------------------------------------------------------------------------------------------------------------

const eventKinds = work => (existsSync(path.join(work, 'events')) ? readdirSync(path.join(work, 'events')).map(f => JSON.parse(readFileSync(path.join(work, 'events', f), 'utf8')).kind) : []);

test('A_harness_that_moved_is_a_recorded_resumable_halt_before_any_build_and_the_same_scoring_goes_on_once_it_is_back', async () => {
  let moved = 'harness-moved: the runner pin moved';
  const h = harness({ a: { harnessProblem: () => moved } });
  const halted = await scorePasses(h.a);
  assert.equal(halted.kind, 'environment', 'a halt, not a verdict');
  assert.match(halted.reason, /^harness-moved: /);
  assert.deepEqual([h.calls.prepare.length, h.calls.build.length, h.calls.pass.length], [0, 0, 0], 'nothing is built or run');
  assert.equal(existsSync(path.join(h.work, 'build.json')), false, 'no completion record');
  assert.deepEqual(eventKinds(h.work), ['harness-moved'], 'the halt is an event file');
  moved = null;
  const resumed = await scorePasses(h.a);
  assert.equal(resumed.kind, 'scored', resumed.reason);
  h.release();
});

// ---- `run` hands its scoring the series' inputs ----------------------------------------------------------------------------------------------------------

const seamsOf = h => Object.fromEntries(Object.entries(h.a).filter(([k]) => !['work', 'seriesDir', 'pinnedRunner', 'now', 'signal'].includes(k)));
/** The series lock `run` and `rescore` hold while they score. */
const locked = w => { const l = acquireSeriesLock({ seriesDir: w.dir }); assert.equal(l.ok, true, l.reason); return w; };
const call = w => ({ session: w.layout.dir, source: w.layout.work, stack: 'node', nugetCache: '/n', npmCache: '/p' });

test('Run_hands_its_scoring_the_runner_drift_the_history_and_the_check_of_the_harness_a_rescore_hands_its_own_and_a_moved_harness_halts', async () => {
  const w = locked(world(inputsNow()));
  const h = harness({ holdSeries: false });
  const asked = [];
  const fake = { filesFor: () => FILES, passSeams: args => { asked.push(args); return { ...seamsOf(h), harnessProblem: args.harnessProblem }; } };
  const dep = scoreSessionDep({ world: fake, calibration: { limits: h.a.limits }, heldout: { casesDir: '/x', traps: [] }, home: w.home, sameHarness: true });
  const scored = await dep(call(w));
  assert.equal(scored.kind, 'scored', scored.reason);
  assert.deepEqual([typeof asked[0].history, asked[0].driftPossible], ['function', false], 'the series recorded its runner pin and it has not moved');
  assert.equal(asked[0].harnessProblem(), null);
  const moved = locked(world({ ...inputsNow(), templatesIdentity: 'b'.repeat(64) }));
  const stopped = await scoreSessionDep({ world: fake, calibration: { limits: h.a.limits }, heldout: { casesDir: '/x', traps: [] }, home: moved.home, sameHarness: true })(call(moved));
  assert.equal(stopped.kind, 'environment', 'the loop halts resumably');
  assert.match(stopped.reason, /^harness-moved: .*the templates/);
  const rescore = scoreSessionDep({ world: fake, calibration: { limits: h.a.limits }, heldout: { casesDir: '/x', traps: [] }, home: moved.home, scoreExtra: { driftPossible: true, history: () => ({ known: false, why: 'x' }) } });
  assert.equal((await rescore(call(moved))).kind, 'scored', 'the control: a rescore over moved bytes goes on, judging them by the history');
});

test('The_run_command_scores_its_sessions_on_the_same_harness_only', () => {
  const text = readFileSync(new URL('../lib/run.mjs', import.meta.url), 'utf8');
  const wired = source => /scoreSessionDep\(\{[^}]*sameHarness: true/.test(source);
  assert.equal(wired(text), true, 'run.mjs asks for the check');
  assert.equal(wired(text.replace('sameHarness: true', 'sameHarness: false')), false, 'the scan can fail');
});

// ---- the code round: the refusal says what differs and what to do; `now` is injectable and read once ------------------------------------------------------------------

test('The_refusal_names_what_differs_recorded_against_current_by_short_hashes_and_says_what_to_do', () => {
  const base = { ...inputsNow(), subject: { sha: 'abcdef0123456789abcdef0123456789abcdef01' } };
  const moved = { ...base, runnerPin: { ...base.runnerPin, manifestSha256: 'a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90' } };
  const sentence = problem(moved) ?? '';
  assert.match(sentence, /the runner pin moved since the series was made \(recorded a1b2c3d4e5f6, now [0-9a-f]{12}\)/, 'recorded against current, twelve characters each');
  assert.ok(!sentence.includes('a1b2c3d4e5f60718'), 'never the whole hash');
  assert.ok(sentence.includes('abcdef012345'), 'the recorded subject commit, short');
  assert.match(sentence, /run it on the harness it recorded/);
  assert.match(sentence, /`rescore`.*against the series' history/);
  const { runnerPin: _dropped, ...without } = base;
  assert.match(problem(without) ?? '', /the series recorded nothing for the runner pin \(now [0-9a-f]{12}\)/, 'what was not recorded says so, with the current value');
  const none = { ...inputsNow() };
  assert.match(problem({ ...none, templatesIdentity: 'b'.repeat(64) }) ?? '', /no subject commit was recorded/, 'and a series with no subject says that instead of a commit');
});

test('The_stacks_check_takes_an_injected_now_so_a_test_can_mock_the_current_harness', () => {
  const inputs = { ...inputsNow(), runnerPin: { manifestSha256: 'f'.repeat(64) }, templatesIdentity: 'e'.repeat(64) };
  const world = { filesFor: () => FILES };
  const now = { runnerPin: { manifestSha256: 'f'.repeat(64) }, templatesIdentity: 'e'.repeat(64) };
  assert.equal(harnessMovedForStacks({ inputs, world, stacks: ['node', 'node'], now }), null, 'the injected harness is the one compared with');
  assert.match(harnessMovedForStacks({ inputs, world, stacks: ['node'], now: { ...now, templatesIdentity: '1'.repeat(64) } }) ?? '', /the templates/);
});
