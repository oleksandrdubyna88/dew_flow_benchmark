// PR 5 plugged into PR 3's and PR 4's seams (F3.S1 (b), PR 5): the default `deps.preregGuard` of `stageGuard` is the pre-registration guard (the tag, the registered blobs, the clean tree, the harness identity, the seed for a data slot), the default
// `deps.readers.harness` is the identity over the Stage 1 folders, the default `deps.harnessAccepted` is read from the dated lines while no data session has left `planned`, and the default `io.stage.initGuardFn` of `stage-init` is PR 5's init guard.
// Every test goes through `stage-run` or `stage-init`, over the temporary repository of `stage-prereg-wiring-fixture.mjs` (a bare origin that holds a fake tag, a manifest, a seed hash, the price file, the registered prompts and PR 4's invented record)
// and the fakes of the existing stage tests (the loop, the identities, the launch checks). Nothing is paid, nothing real is read, no tag is pushed anywhere but to a temporary origin.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { test } from 'node:test';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { commit as commitAll } from '../../minigit-bench/test/prereg-fixtures.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { quickPrompts } from '../lib/init-quick.mjs';
import { quickExplore } from '../lib/quick.mjs';
import { ledgerFileOf, loadSeries, runMapFileOf, seriesInputs, seriesPrompts } from '../lib/series.mjs';
import * as stageSlices from '../lib/stage-slices.mjs';
import { stagePrompts } from '../lib/stage-prompts.mjs';
import { stageInit } from '../lib/stage-init.mjs';
import { POLICY_FREE_FIELDS as FROM_IDENTITY } from '../lib/stage-identity.mjs';
import { POLICY_FREE_FIELDS, PREREG_DIR, STAGE_POLICY } from '../lib/stage-lists.mjs';
import { STAGE_PREREG_CONFIG } from '../lib/stage-prereg.mjs';
import { stageRun } from '../lib/stage-run.mjs';
import { stageSync } from '../lib/stage-branch.mjs';
import { LIVE, MANIFEST } from './stage-guard-fixture.mjs';
import { AMENDMENTS_FILE, QUIET_FILE, SEED, SEED_FILE, TAG, git, onMain, onSeries, sha256, write } from './stage-prereg-fixture.mjs';
import { preregRig, promptsFile } from './stage-prereg-wiring-fixture.mjs';
import { PRE, initIo } from './stage-series-fixture.mjs';
import { fakeLoop, mapOf, readbackHolds, runRig } from './stage-run-fixture.mjs';

const next = (rig, what = 'next') => stageRun({ values: { series: rig.dir, what }, io: rig.io });
const ledgerBytes = dir => (existsSync(ledgerFileOf(dir)) ? readFileSync(ledgerFileOf(dir), 'utf8') : '');
const bytes = rig => [ledgerBytes(rig.dir), readFileSync(runMapFileOf(rig.dir), 'utf8')];
const PILOT_LINES = ['2026-11-20 | owner-go | pilot | the owner, 2026-11-20: run the pilot', '2026-11-21 | owner-go | rounds:1-5 | the owner, 2026-11-21: all five rounds', '2026-11-22 | pilot | GO | verdicts:0123456789ab | the team lead'];

/** A refused launch: its answer, and the proof that nothing changed (the run-map and the ledger byte for byte, the loop never called). */
async function refusedWith(rig, what = 'aa') {
  const [before, launches] = [bytes(rig), rig.log.length];
  const r = await next(rig, what);
  assert.deepEqual(bytes(rig), before, `${what}: the run-map and the ledger are byte for byte as they were`);
  assert.equal(rig.log.length, launches, `${what}: the loop was never called`);
  return r;
}

/**
 * A series ready to run over the complete repository: PR 4's fakes for the record's identities (`live`, `manifest`, `pinnedTaskSha`), and NONE for PR 5's three seams, so the defaults are what runs. `lines` are the registered dated lines.
 * -> `{ rig, f, w }`.
 */
async function runOver(t, { repo = {}, lines = PILOT_LINES, deps = {} } = {}) {
  const { f, w } = await preregRig(t, repo);
  const { preregGuard: _p, harnessAccepted: _a, ...fakes } = w.args.deps;
  const guardArgs = { repo: f.repo, taskRepo: w.c.dir, deps: { ...fakes, ...deps } };
  const initGuardFn = async () => ok({ ...PRE, preregTag: TAG, preregCommit: f.sha, seedSha256: sha256(SEED), registeredPrompts: promptsFile() });
  const rig = await runRig({ lines, stage: { repo: f.repo, initGuardFn, guardFn: undefined, guardArgs } });
  return { rig, f, w };
}

/** The seed revealed the way the series does it: a commit on `main` and `stage-sync`. */
async function revealSeed(f) {
  onMain(f, () => write(f.repo, SEED_FILE, `${SEED}\n`), 'the revealed seed');
  const synced = await stageSync({ repo: f.repo });
  assert.equal(synced.ok, true, synced.reason);
}

// ---- the paid launch: the default guard is PR 5's ---------------------------------------------------------------------------------------------------------------------------------------------------

test('A_paid_launch_passes_every_guard_with_a_fake_tag_manifest_and_seed_and_a_data_slot_waits_for_the_revealed_seed', async t => {
  const { rig, f } = await runOver(t);
  assert.equal((await next(rig, 'aa')).exit, 0, 'the control: a tag on the remote, the registered blobs, a clean tree, the series\' harness: the pilot step runs under the default guard');
  assert.equal(rig.log.length, 1);
  const noSeed = await refusedWith(rig, 'next');
  assert.equal(noSeed.exit, 4);
  assert.match(noSeed.message, /^\d+ problem\(s\) before a paid launch: .*seed\.txt is required for a data pair and is absent at HEAD: run seed-reveal after the A\/A/);
  await revealSeed(f);
  const data = await next(rig, 'next');
  assert.equal(data.exit, 0, data.message);
  assert.equal(rig.log.length, 2, 'the data slot ran once the seed is in the tree and is main\'s blob');
});

const REASONS = Object.freeze([
  ['the tag is not on the remote', /the tag prereg\/lang-v2-20261120 is not on origin: push it before any paid launch/, ({ f }) => git(f.repo, 'push', '-q', '--delete', 'origin', TAG)],
  ['a registered file differs from the tag', /statement\.md differs from the tagged commit/, ({ f }) => onSeries(f, () => write(f.repo, `${PREREG_DIR}/statement.md`, 'changed after the tag\n'), 'edit a registered file')],
  ['the tree under spikes is not clean', /the tree under spikes\/ is not clean \(\?\? spikes\/lang-explore\/scratch\.txt\)/, ({ f }) => write(f.repo, 'spikes/lang-explore/scratch.txt', 'untracked\n')],
  ['the harness identity is not the series\'', /harnessIdentity [0-9a-f]{12} is not the run-map's [0-9a-f]{12} and no amendment accepted it \(put the fix on origin\/main, add the dated line `YYYY-MM-DD \| instrument \| harness:<12 hex> \| commit:<40 hex> <reason, with its G-id>` to .*amendments\.md on origin\/main, then run stage-sync --accept-harness <12 hex>\)/, ({ f }) => onSeries(f, () => write(f.repo, 'spikes/lang-explore/lib/x.mjs', '// a harness change\n'), 'harness')],
  ['the branch differs from the tag off the amendable list', /the series' branch differs from the tag in docs\/notes\.md, which is not an amendable path/, ({ f }) => onSeries(f, () => write(f.repo, 'docs/notes.md', 'changed\n'), 'off the list')],
  ['a registered line is not origin/main\'s blob', /amendments\.md at HEAD is not origin\/main's blob: a dated line the series acts on is public first, so take it from main with stage-sync/, ({ f }) => onSeries(f, () => write(f.repo, AMENDMENTS_FILE, '# Dated amendments\n2026-11-26 | instrument | harness:aaaaaaaaaaaa | only on the series (G-3)\n'), 'a line only here')],
]);

for (const [reason, sentence, spoil] of REASONS) {
  test(`A_paid_launch_is_refused_when_${reason.replace(/[^A-Za-z0-9]+/g, '_')}_and_nothing_is_changed`, async t => {
    const spoiled = await runOver(t);
    await spoil(spoiled);
    const refused = await refusedWith(spoiled.rig);
    assert.equal(refused.exit, 4, refused.message);
    assert.match(refused.message, new RegExp(`^\\d+ problem\\(s\\) before a paid launch: .*${sentence.source}`), `${reason}: PR 5's own named problem`);
  });
}

test('A_rehearsal_series_is_not_under_the_guard_whatever_the_repository_holds', async t => {
  const { f } = await preregRig(t);
  git(f.repo, 'push', '-q', '--delete', 'origin', TAG);
  const io = initIo({ stage: { initGuardFn: undefined, repo: f.repo } });
  const made = await stageInit({ values: { rehearsal: true, 'task-repo': '/fake/task-repo' }, io });
  assert.equal(made.exit, 0, made.message);
  const log = [];
  io.runSeams = { prerequisitesFn: async () => ok({ calibration: {}, heldout: {}, world: { quick: false }, token: 'fake-token', prompts: {} }), loopFn: fakeLoop(log) };
  const ran = await stageRun({ values: { series: made.dir, what: 'next' }, io });
  assert.equal(ran.exit, 0, `a rehearsal runs with no tag on the remote: ${ran.message}`);
  assert.equal(log.length, 1);
});

// ---- the harness identity: the default reader and the dated lines ----------------------------------------------------------------------------------------------------------------------------------

test('The_default_reader_of_the_harness_identity_is_the_identity_over_the_stage_folders', async t => {
  const { rig, f } = await runOver(t, { deps: { live: undefined } });
  const refused = await refusedWith(rig);
  const identity = f.runMap.harnessIdentity;
  assert.match(refused.message, new RegExp(`harness differs: the manifest has ${MANIFEST.identities.harness.slice(0, 12)}, this checkout has ${identity.slice(0, 12)}`), 'the harness is read from this checkout over STAGE_HARNESS_DIRS, and it is the series\'');
  assert.doesNotMatch(refused.message, /harness cannot be read/, 'a reader is handed in by default');
});

const live = harness => async () => ({ ...LIVE, harness: { value: harness } });

test('A_harness_identity_that_differs_from_the_manifest_is_accepted_by_a_dated_line_before_the_first_data_session_and_not_without_one', async t => {
  const moved = 'e'.repeat(64);
  const line = `2026-11-25 | instrument | harness:${moved.slice(0, 12)} | the fix before the first data session (G-3)`;
  const none = await runOver(t, { deps: { live: live(moved) } });
  const refused = await refusedWith(none.rig, 'aa');
  assert.match(refused.message, new RegExp(`harness differs: the manifest has ${LIVE.harness.value.slice(0, 12)}, this checkout has eeeeeeeeeeee`), 'with no dated line the difference is refused');
  const { rig, f } = await runOver(t, { lines: [...PILOT_LINES, line], deps: { live: live(moved) } });
  await revealSeed(f);
  const first = await next(rig, 'next');
  assert.equal(first.exit, 0, `the dated line accepts it while no data session has left planned: ${first.message}`);
});

// ---- the acceptance is frozen at the first data session, never forgotten (coai plan round 2, PR 5, finding H) -------------------------------------------------------------------------------------------

test('A_harness_accepted_by_a_dated_line_in_time_is_recorded_with_the_first_data_plan_and_the_three_next_launches_pass', async t => {
  const moved = 'e'.repeat(64);
  const line = `2026-11-25 | instrument | harness:${moved.slice(0, 12)} | the fix before the first data session (G-3)`;
  const { rig, f } = await runOver(t, { lines: [...PILOT_LINES, line, ...readbackHolds()], deps: { live: live(moved) } });
  await revealSeed(f);
  assert.equal(mapOf(rig.dir).harnessAtFirstData, undefined, 'nothing is recorded before a data session is planned');
  const first = await next(rig, 'next');
  assert.equal(first.exit, 0, first.message);
  const planned = mapOf(rig.dir);
  assert.equal(planned.harnessAtFirstData, 'eeeeeeeeeeee', 'the identity the guard accepted is in the run-map the plan wrote');
  assert.ok(Object.values(planned.sessions).some(s => s.kind === 'data'), 'the same map holds the first data sessions');
  for (const n of [2, 3, 4]) {
    const later = await next(rig, 'next');
    assert.equal(later.exit, 0, `launch ${n} across the boundary: ${later.message}`);
  }
  assert.equal(mapOf(rig.dir).harnessAtFirstData, 'eeeeeeeeeeee', 'the record is written once and never moves');
});

test('A_dated_line_after_the_boundary_that_names_a_new_identity_is_refused_and_the_recorded_identity_stays_accepted', async t => {
  const [first12, second] = ['e'.repeat(12), 'd'.repeat(64)];
  const dated = hex => `2026-11-25 | instrument | harness:${hex.slice(0, 12)} | a harness fix (G-3)`;
  let harness = first12.padEnd(64, 'e');
  const { rig, f } = await runOver(t, { lines: [...PILOT_LINES, dated(first12)], deps: { live: async () => ({ ...LIVE, harness: { value: harness } }) } });
  await revealSeed(f);
  assert.equal((await next(rig, 'next')).exit, 0);
  harness = second; // the checkout moves on after the boundary, and a line dated later names the new identity
  writeFileSync(rig.files.amendmentsFile, `${readFileSync(rig.files.amendmentsFile, 'utf8')}${dated(second)}\n`);
  const refused = await refusedWith(rig, 'next');
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /harness differs: the manifest has [0-9a-f]{12}, this checkout has dddddddddddd/, 'the new line accepts nothing');
  harness = first12.padEnd(64, 'e');
  assert.equal((await next(rig, 'next')).exit, 0, 'the recorded identity is still accepted');
});

test('A_harness_change_on_the_series_branch_passes_the_guard_under_a_dated_line_before_the_first_data_session_and_never_without_it', async t => {
  const harnessChange = ({ f }) => onSeries(f, () => write(f.repo, 'spikes/lang-explore/lib/x.mjs', '// a harness fix, committed on the series\' branch\n'), 'harness fix');
  const spoiled = await runOver(t);
  harnessChange(spoiled);
  const identity = (await harnessIdentity(spoiled.f.repo, STAGE_PREREG_CONFIG)).value;
  const refused = await refusedWith(spoiled.rig, 'aa');
  assert.match(refused.message, /the series' branch differs from the tag in spikes\/lang-explore\/lib\/x\.mjs, which is not an amendable path/, 'with no dated line the branch and the identity are refused');
  const line = `2026-11-25 | instrument | harness:${identity.slice(0, 12)} | the fix before the first data session (G-3)`;
  const { rig, f } = await runOver(t, { lines: [...PILOT_LINES, line], deps: { live: live(identity) } });
  harnessChange({ f });
  await revealSeed(f);
  const first = await next(rig, 'next');
  assert.equal(first.exit, 0, `the dated line accepts the harness the branch holds while no data session has left planned: ${first.message}`);
  const second = await next(rig, 'next');
  assert.equal(second.exit, 0, `the identity accepted in time is recorded with the first data plan and stays accepted: ${second.message}`);
});

// ---- the instrument inputs freeze with the first data plan, like the harness (Opus review, PR 5, V2) -------------------------------------------------------------------------------------------------

/** A change of quiet.json made on `main` with its dated `input:` line, then taken by `stage-sync`: the way a series amends an instrument input. `n` keeps each line's date apart. */
async function amendQuiet(f, text, n) {
  const hash12 = sha256(text).slice(0, 12);
  const line = `2026-11-${20 + n} | instrument | input:quiet.json:${hash12} | the sampler fix ${n} (G-3)\n`;
  onMain(f, () => { write(f.repo, QUIET_FILE, text); write(f.repo, AMENDMENTS_FILE, `${readFileSync(`${f.repo}/${AMENDMENTS_FILE}`, 'utf8')}${line}`); }, `quiet.json ${n}`);
  const synced = await stageSync({ repo: f.repo });
  assert.equal(synced.ok, true, synced.reason);
  return hash12;
}

test('An_input_line_before_the_first_data_session_passes_and_keeps_passing_and_a_new_one_after_it_is_refused', async t => {
  const { rig, f } = await runOver(t, { lines: [...PILOT_LINES, ...readbackHolds()] });
  await revealSeed(f);
  const hash12 = await amendQuiet(f, '{"windowMs":1}\n', 1);
  assert.equal(mapOf(rig.dir).inputsAtFirstData, undefined, 'nothing is recorded before a data session is planned');
  for (const n of [1, 2, 3]) {
    const r = await next(rig, 'next');
    assert.equal(r.exit, 0, `launch ${n} under the input line: ${r.message}`);
  }
  assert.equal(mapOf(rig.dir).inputsAtFirstData['quiet.json'], hash12, 'the input the guard accepted is in the run-map the first data plan wrote');
  await amendQuiet(f, '{"windowMs":2}\n', 2);
  const refused = await refusedWith(rig, 'next');
  assert.equal(refused.exit, 4);
  assert.match(refused.message, new RegExp(`quiet\\.json changed to [0-9a-f]{12} after the first data session was planned \\(it was ${hash12} then\\): the instrument is frozen with the first data session, so no later line accepts another`));
});

test('The_policys_free_fields_are_one_list_that_the_identity_of_the_policy_and_the_guard_both_import', () => {
  assert.deepEqual([...POLICY_FREE_FIELDS], ['t3', 't5', 'decidedOn', 'reason'], 'one list: t3, t5 and their record fields');
  assert.equal(FROM_IDENTITY, POLICY_FREE_FIELDS, 'the identity of the policy reads the very list of the lists module');
  assert.equal(STAGE_POLICY.free, POLICY_FREE_FIELDS, 'and the guard\'s free fields are that same list, not a copy');
});

// ---- stage-init: the default guard is PR 5's init guard ---------------------------------------------------------------------------------------------------------------------------------------------

/** The registered prompts with one word of one task's Node prompt changed. */
function editedPrompts() {
  const file = promptsFile();
  return { ...file, tasks: { ...file.tasks, '07': { ...file.tasks['07'], node: { ...file.tasks['07'].node, text: `${file.tasks['07'].node.text} (edited)` } } } };
}

const initOver = f => initIo({ stage: { initGuardFn: undefined, repo: f.repo } });
const seriesDirs = io => (existsSync(`${io.home}/rsd/lang-v2/explore`) ? readdirSync(`${io.home}/rsd/lang-v2/explore`) : []);

test('A_paid_series_is_made_from_a_fake_tag_manifest_and_seed_and_records_what_the_pre_registration_holds', async t => {
  const { f } = await preregRig(t);
  const io = initOver(f);
  const made = await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io });
  assert.equal(made.exit, 0, made.message);
  const { state } = loadSeries(made.dir).value;
  const manifestText = git(f.repo, 'show', `HEAD:${PREREG_DIR}/manifest.json`);
  assert.deepEqual([state.preregTag, state.preregCommit, state.seedSha256, state.pilot], [TAG, f.sha, sha256(SEED), { aa: { task: '05', stack: 'node' }, daemon: '10', web: '04' }]);
  assert.equal(state.branch, 'series', 'the series records the local branch it was made on: the one stage-sync moves');
  const inputs = seriesInputs(made.dir).value;
  assert.equal(inputs.preregManifestSha256, sha256(`${manifestText}\n`), 'the manifest\'s sha256');
  assert.equal(inputs.harnessIdentity, f.runMap.harnessIdentity, 'the harness identity the manifest names is the live one');
  assert.deepEqual([inputs.policy.prices, inputs.policy.policy], [`${PREREG_DIR}/prices-2026-11-19.json`, `${PREREG_DIR}/crosscheck-policy.json`], 'the registered price file and policy are pinned, not v1\'s');
});

// ---- older series' tags stay on the remote: stage-init takes --prereg-tag (coai plan round 2, PR 5, finding T) -----------------------------------------------------------------------------------------------

const OLDER = 'prereg/lang-v2-20261001';
/** An older series' tag on the remote, at the same commit (a later series legitimately leaves it there). */
const olderTag = ({ f }) => { git(f.repo, 'tag', '-a', OLDER, '-m', 'an older series', f.sha); git(f.repo, 'push', '-q', 'origin', OLDER); };

test('Two_tags_on_the_remote_and_the_prereg_tag_flag_make_the_series_under_the_named_tag', async t => {
  const { f } = await preregRig(t);
  olderTag({ f });
  const made = await stageInit({ values: { 'task-repo': '/fake/task-repo', 'prereg-tag': TAG }, io: initOver(f) });
  assert.equal(made.exit, 0, made.message);
  const { state } = loadSeries(made.dir).value;
  assert.deepEqual([state.preregTag, state.preregCommit], [TAG, f.sha], 'the series records the tag it was told, not the other');
  const older = await stageInit({ values: { 'task-repo': '/fake/task-repo', 'prereg-tag': OLDER }, io: initOver(f) });
  assert.equal(older.exit, 0, 'the flag names either tag: it is the operator\'s choice');
  assert.equal(loadSeries(older.dir).value.state.preregTag, OLDER);
});

test('Two_tags_on_the_remote_with_no_flag_are_refused_naming_both_and_the_flag_and_nothing_is_written', async t => {
  const { f } = await preregRig(t);
  olderTag({ f });
  const io = initOver(f);
  const refused = await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io });
  assert.equal(refused.exit, 4);
  assert.match(refused.message, new RegExp(`^origin holds 2 tags named prereg/lang-v2-\\* \\(${OLDER}, ${TAG}\\): .*name the one this series is made under with --prereg-tag <name>`));
  assert.deepEqual(seriesDirs(io), []);
});

test('The_prereg_tag_flag_names_a_tag_that_is_on_the_remote_and_of_the_series_name_and_in_the_history_or_the_series_is_refused', async t => {
  const { f } = await preregRig(t);
  olderTag({ f });
  const io = initOver(f);
  const absent = await stageInit({ values: { 'task-repo': '/x', 'prereg-tag': 'prereg/lang-v2-20260101' }, io });
  assert.match(absent.message, /^origin holds no tag named prereg\/lang-v2-20260101 \(it holds: prereg\/lang-v2-20261001, prereg\/lang-v2-20261120\)/);
  const foreign = await stageInit({ values: { 'task-repo': '/x', 'prereg-tag': 'v1.0.0' }, io });
  assert.match(foreign.message, /^--prereg-tag v1\.0\.0 is not named prereg\/lang-v2-<name>/);
  git(f.repo, 'checkout', '-q', '-b', 'side', 'main');
  write(f.repo, 'docs/side.md', 'a commit off the series\n');
  const side = commitAll(f.repo, 'a commit off the series');
  git(f.repo, 'tag', '-a', '-f', OLDER, '-m', 'moved', side);
  git(f.repo, 'push', '-q', '-f', 'origin', OLDER);
  git(f.repo, 'checkout', '-q', 'series');
  const off = await stageInit({ values: { 'task-repo': '/x', 'prereg-tag': OLDER }, io });
  assert.match(off.message, /the tagged commit [0-9a-f]{12} is not in the history of this checkout/);
  assert.deepEqual(seriesDirs(io), [], 'no refusal wrote a series');
});

// ---- the engine sends the registered prompt (Opus review, PR 5, V1) ---------------------------------------------------------------------------------------------------------------------------------------

test('The_prompt_plan_gets_for_a_unit_of_a_paid_series_is_the_registered_text_byte_for_byte', async t => {
  const { f } = await preregRig(t);
  const made = await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io: initOver(f) });
  assert.equal(made.exit, 0, made.message);
  const registered = promptsFile();
  for (const task of ['05', '07']) {
    const given = seriesPrompts(made.dir, task).value; // what run.mjs hands the plan of a unit
    assert.deepEqual([given.template, given.templateSha256], [registered.template, registered.templateSha256], `${task}: the registered template, not the quick run's`);
    for (const stack of ['csharp', 'node']) assert.deepEqual([given.prompts[stack].text, given.prompts[stack].sha256], [registered.tasks[task][stack].text, registered.tasks[task][stack].sha256], `${task} ${stack}`);
  }
});

test('A_rehearsal_slice_holds_the_stage_wording_the_engine_sends_and_not_the_quick_runs', () => {
  const sent = stagePrompts(quickExplore(loadExplore().value, '/home/nobody'));
  const held = stageSlices.slicePrompts({ base: loadExplore().value, home: '/home/nobody' });
  assert.deepEqual([held.template, held.prompts.node.text], [sent.template, sent.prompts.node.text], 'a rehearsal rehearses the real wording');
  assert.notEqual(held.template, quickPrompts(quickExplore(loadExplore().value, '/home/nobody')).template, 'the quick run\'s default template is not it');
});

test('A_slice_holding_another_text_than_the_registered_one_is_refused_at_launch_naming_the_task_and_stack', async t => {
  const { rig } = await runOver(t);
  const file = `${rig.dir}/inputs.json`;
  const inputs = JSON.parse(readFileSync(file, 'utf8'));
  inputs.tasks['05'].prompts.prompts.node.text = `${inputs.tasks['05'].prompts.prompts.node.text} (another text)`;
  writeFileSync(file, `${JSON.stringify(inputs, null, 2)}\n`);
  const refused = await refusedWith(rig, 'aa');
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /the prompt frozen into task 05's slice for node is not the registered text of prompts-lang\.json: a unit sends what its slice holds/);
});

const INIT_REASONS = Object.freeze([
  ['no tag is on the remote', /^origin holds 0 tags named prereg\/lang-v2-\* \(none\)/, undefined, ({ f }) => git(f.repo, 'push', '-q', '--delete', 'origin', TAG)],
  ['two tags are on the remote', /^origin holds 2 tags named prereg\/lang-v2-\* \(prereg\/lang-v2-20261120, prereg\/lang-v2-20261121\)/, undefined, ({ f }) => { git(f.repo, 'tag', '-a', 'prereg/lang-v2-20261121', '-m', 'second', f.sha); git(f.repo, 'push', '-q', 'origin', 'prereg/lang-v2-20261121'); }],
  ['the tagged commit is not in the history of this checkout', /the tagged commit [0-9a-f]{12} is not in the history of this checkout: cut the series' branch at the tag's commit/, undefined, ({ f }) => {
    git(f.repo, 'checkout', '-q', '-b', 'side', 'main');
    write(f.repo, 'docs/side.md', 'a commit off the series\n');
    const side = commitAll(f.repo, 'a commit off the series');
    git(f.repo, 'tag', '-a', '-f', TAG, '-m', 'moved', side);
    git(f.repo, 'push', '-q', '-f', 'origin', TAG);
    git(f.repo, 'checkout', '-q', 'series');
  }],
  ['the manifest is not registered', /manifest\.json is not in this checkout: nothing is registered to make the series from/, { omit: ['manifest.json'] }],
  ['the schedule holds no pilot', /schedule\.json holds no pilot \(aa, daemon, web\): the pilot's trees are registered/, { schedule: { stage: 1 } }],
  ['the registered prompt is not the text the engine sends', /the registered prompt of task 07 in node is not the text the engine sends/, { prompts: 'edited' }],
  ['the price file is not exactly one', /must hold exactly one prices-<yyyy-mm-dd>\.json \(found 2: prices-2026-11-19\.json, prices-2026-11-21\.json\)/, { add: [[`${PREREG_DIR}/prices-2026-11-21.json`, '{}\n']] }],
  ['the manifest names another harness identity', /harnessIdentity [0-9a-f]{12} is not the run-map's f{12} and no amendment accepted it/, { manifestHarness: 'f'.repeat(64) }],
  ['a registered file differs from the tag', /statement\.md differs from the tagged commit/, undefined, ({ f }) => onSeries(f, () => write(f.repo, `${PREREG_DIR}/statement.md`, 'changed\n'), 'edit')],
  ['the tree under spikes is not clean', /the tree under spikes\/ is not clean/, undefined, ({ f }) => write(f.repo, 'spikes/lang-explore/scratch.txt', 'x\n')],
]);

for (const [reason, sentence, options, spoil] of INIT_REASONS) {
  test(`A_paid_series_is_refused_when_${reason.replace(/[^A-Za-z0-9]+/g, '_')}_and_nothing_is_written`, async t => {
    const prompts = options?.prompts === 'edited' ? editedPrompts() : undefined;
    const { f } = await preregRig(t, { ...options, ...(prompts ? { prompts } : {}) });
    const io = initOver(f);
    await spoil?.({ f });
    const refused = await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io });
    assert.equal(refused.exit, 4, `${reason}: ${refused.message}`);
    assert.match(refused.message, sentence, reason);
    assert.deepEqual(seriesDirs(io), [], 'no series directory');
  });
}
