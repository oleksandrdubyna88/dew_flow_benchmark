// F3.S1 (b), PR 5: the pre-registration guard over Stage 1's lists (scenarios 65, 69 to 76, 79a to 79e of the stories document). Every test builds a temporary repository with a bare origin in the shape of the pre-registration
// (test/stage-prereg-fixture.mjs): the series' own branch is cut at the tag, `main` moves on the origin, and `stageSync` is the one thing that brings registered files from `main` to the series' branch. Nothing is real.
import assert from 'node:assert/strict';
import { chmodSync, cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { test } from 'node:test';
import { harnessIdentity, preregGuard } from '../../minigit-bench/lib/prereg-guard.mjs';
import { checkResumable, openSeries, readRunMap, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { AMENDABLE_PATHS, PREREG_DIR, STAGE_HARNESS_DIRS, STAGE_HARNESS_LISTS, STAGE_HARNESS_TOP_DIRS, STAGE_PREREG_FILES } from '../lib/stage-lists.mjs';
import { parseStageAmendments } from '../lib/stage-amendments.mjs';
import { STAGE_PREREG_CONFIG } from '../lib/stage-prereg.mjs';
import { branchProblems } from '../lib/stage-branch.mjs';
import { acceptedHarness } from '../lib/stage-prereg-guard.mjs';
import * as runCtx from '../lib/run-ctx.mjs';
import { REPO } from '../lib/paths.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import path from 'node:path';
import { main } from '../explore.mjs';
import { cmdStageSync } from '../lib/stage-prereg-commands.mjs';
import { stageSync } from '../lib/stage-branch.mjs';
import { AMENDMENTS, AMENDMENTS_FILE, EXPLORE, EXPLORE_FILE, POLICY, POLICY_FILE, PRICE_FILE, QUIET_FILE, SEED, SEED_FILE, TAG, git, headBlob, onMain, onOtherClone, onSeries, pathsOf, sha256, stageFixture, write } from './stage-prereg-fixture.mjs';
import { commit } from '../../minigit-bench/test/prereg-fixtures.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { makeSeries } from './stage-series-fixture.mjs';
import { runRig } from './stage-run-fixture.mjs';
import { stageSeriesDirs } from '../lib/stage-series.mjs';
import { runMapFileOf } from '../lib/series.mjs';
import { stageRun } from '../lib/stage-run.mjs';

const guard = (f, over = {}) => preregGuard({ repo: f.repo, runMap: f.runMap, paid: false, requireSeed: false, config: STAGE_PREREG_CONFIG, ...over });
const paid = (f, over = {}) => guard(f, { paid: true, ...over });
const CAPS_LINE = '2026-11-25 | caps | wallMs:5400000 | stallMs:3000000 | the A/A showed the longest quiet gap (G-18)';
const withCaps = (wallMs, stallMs, over = {}) => `${JSON.stringify({ ...EXPLORE, ...over, caps: { ...EXPLORE.caps, wallMs, stallMs, ...(over.caps ?? {}) } })}\n`;

test('The_stage_lists_name_every_registered_file_of_the_table_and_the_pinned_ones_outside_its_folder', () => {
  const names = STAGE_PREREG_FILES.filter(entry => typeof entry === 'string');
  const inFolder = ['statement.md', 'analysis-contract.md', 'prediction.md', 'prompts-lang.json', 'schedule.json', 'manifest.json', 'crosscheck-policy.json', 'pilot-rules.json', 'adjudication-rules.json', 'amendments.md', 'seed.sha256'];
  for (const name of inFolder) assert.ok(names.includes(`${PREREG_DIR}/${name}`), `${name} is registered`);
  const outside = ['spikes/lang-explore/inputs/explore.json', 'spikes/lang-explore/inputs/memory-limit.json', 'spikes/lang-explore/inputs/packages.json', 'spikes/lang-explore/inputs/heldout-frozen.json', 'spikes/lang-explore/inputs/growth.json', 'spikes/lang-explore/inputs/package-flags.json',
    'spikes/lang-bench/inputs/specs.json', 'spikes/lang-bench/inputs/hashes-lang.txt', 'spikes/lang-bench/inputs/cache-snapshots-lang.json', 'spikes/lang-bench/inputs/cache-recipe-lang.json', 'spikes/lang-bench/inputs/package-matrix.json', 'spikes/minigit-bench/inputs/quiet.json'];
  for (const file of outside) assert.ok(names.includes(file), `${file} is pinned by path`);
  assert.equal(names.length, inFolder.length + outside.length, 'and no other plain file');
  const folders = STAGE_PREREG_FILES.filter(entry => typeof entry !== 'string');
  assert.deepEqual(folders.map(e => [e.dir, e.exactlyOne === true]), [[PREREG_DIR, true], ['spikes/lang-bench/inputs/specs.d', false]], 'the one dated price file, and every spec file of specs.d');
  assert.ok(folders[0].pattern.test('prices-2026-11-19.json') && !folders[0].pattern.test('prices-2026-11-19.json.bak') && !folders[0].pattern.test('prices-x.json'));
  assert.deepEqual([...AMENDABLE_PATHS].sort(), [AMENDMENTS_FILE, EXPLORE_FILE, 'spikes/lang-explore/inputs/package-flags.json', POLICY_FILE, QUIET_FILE, SEED_FILE].sort(), 'the closed list of the series\' branch (stories document section 3)');
});

// ---- 69, 65: the tag and the seed -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_guard_refuses_a_tag_that_is_not_on_the_remote_or_peels_to_another_commit', async () => {
  const f = await stageFixture();
  assert.equal((await guard(f)).ok, true, (await guard(f)).reason);
  assert.equal((await paid(f)).ok, true, (await paid(f)).reason);
  git(f.repo, 'push', '-q', '--delete', 'origin', TAG);
  assert.match((await guard(f)).reason, /the tag prereg\/lang-v2-20261120 is not on origin: push it before any paid launch/);
  git(f.repo, 'push', '-q', 'origin', TAG);
  assert.match((await guard(f, { runMap: { ...f.runMap, preregCommit: 'f'.repeat(40) } })).reason, /peels to/);
});

test('The_seed_is_in_the_tree_as_a_hash_only_until_it_is_revealed_and_a_seed_at_the_tag_is_refused', async () => {
  const f = await stageFixture();
  assert.equal(headBlob(f.repo, `${PREREG_DIR}/seed.sha256`).length, 40, 'the hash is registered');
  assert.equal((await guard(f)).ok, true, 'a pilot or a dry run needs no seed');
  assert.match((await guard(f, { requireSeed: true })).reason, /seed\.txt is required for a data pair and is absent at HEAD: run seed-reveal after the A\/A/);
  const atTag = await stageFixture({ extra: repo => write(repo, SEED_FILE, `${SEED}\n`) });
  assert.match((await guard(atTag)).reason, /seed\.txt exists at the tagged commit: the seed must be revealed AFTER the tag/);
});

// ---- 73: a data slot needs the revealed seed, the pilot does not ------------------------------------------------------------------------------------------------------------------------------------

test('A_data_slot_needs_the_revealed_seed_and_the_pilot_does_not', async () => {
  const f = await stageFixture();
  assert.equal((await paid(f)).ok, true, 'a paid pilot launch: no seed');
  assert.equal((await paid(f, { requireSeed: true })).ok, false, 'a paid data slot: seed absent');
  onMain(f, () => write(f.repo, SEED_FILE, 'lv2-ffffffffffffffff\n'), 'a wrong seed');
  assert.equal((await stageSync({ repo: f.repo })).ok, true);
  assert.match((await paid(f, { requireSeed: true })).reason, /seed\.txt does not hash to seed\.sha256/);
  onMain(f, () => write(f.repo, SEED_FILE, `${SEED}\n`), 'the revealed seed');
  assert.equal((await stageSync({ repo: f.repo })).ok, true);
  assert.equal((await paid(f, { requireSeed: true })).ok, true, (await paid(f, { requireSeed: true })).reason);
});

// ---- 70, 71, 72: the registered files, the policy and the caps --------------------------------------------------------------------------------------------------------------------------------------

test('A_registered_file_that_differs_from_the_tag_refuses_the_launch_and_an_appended_amendment_line_does_not', async () => {
  const f = await stageFixture();
  for (const [file, label] of [[`${PREREG_DIR}/statement.md`, 'statement.md'], [`${PREREG_DIR}/analysis-contract.md`, 'analysis-contract.md'], [`${PREREG_DIR}/manifest.json`, 'manifest.json'], [PRICE_FILE, 'prices-2026-11-19.json'], [`${PREREG_DIR}/pilot-rules.json`, 'pilot-rules.json'],
    ['spikes/lang-explore/inputs/packages.json', 'spikes/lang-explore/inputs/packages.json'], ['spikes/lang-explore/inputs/growth.json', 'spikes/lang-explore/inputs/growth.json'], ['spikes/lang-bench/inputs/hashes-lang.txt', 'spikes/lang-bench/inputs/hashes-lang.txt'],
    ['spikes/lang-bench/inputs/specs.d/runner.json', 'spikes/lang-bench/inputs/specs.d/runner.json']]) {
    const g = await stageFixture();
    assert.equal((await guard(g)).ok, true, `control: ${file}`);
    onSeries(g, () => write(g.repo, file, 'changed after the tag\n'), `edit ${file}`);
    const refused = await guard(g);
    assert.equal(refused.ok, false, file);
    assert.ok(refused.reason.includes(`${label} differs from the tagged commit`), `${file}: ${refused.reason}`);
  }
  onSeries(f, () => write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | a reason (G-3)\n`), 'append');
  assert.equal((await guard(f)).ok, true, 'a line appended to the amendments is accepted');
  onSeries(f, () => write(f.repo, AMENDMENTS_FILE, '# rewritten\n'), 'rewrite');
  assert.match((await guard(f)).reason, /amendments\.md at HEAD is not the tagged text with lines appended/);
});

test('A_registered_file_deleted_or_added_to_a_registered_folder_after_the_tag_refuses_the_launch', async () => {
  const f = await stageFixture();
  git(f.repo, 'rm', '-q', 'spikes/lang-bench/inputs/specs.d/format.json');
  commit(f.repo, 'delete a spec file');
  assert.match((await guard(f)).reason, /spikes\/lang-bench\/inputs\/specs\.d\/format\.json is deleted at HEAD/);
  const g = await stageFixture();
  onSeries(g, () => write(g.repo, 'spikes/lang-bench/inputs/specs.d/new.json', '{}\n'), 'a new spec file');
  assert.match((await guard(g)).reason, /specs\.d\/new\.json is missing at the tagged commit/);
  const h = await stageFixture();
  onSeries(h, () => write(h.repo, `${PREREG_DIR}/prices-2026-11-21.json`, '{}\n'), 'a second price file');
  assert.match((await guard(h)).reason, /prices-2026-11-21\.json is missing at the tagged commit/);
  const none = await stageFixture();
  git(none.repo, 'rm', '-q', PRICE_FILE);
  commit(none.repo, 'no price file');
  assert.match((await guard(none)).reason, /prices-2026-11-19\.json is deleted at HEAD/);
});

const policyText = over => `${JSON.stringify({ ...POLICY, ...over })}\n`;
const policyLine = text => `2026-11-25 | instrument | input:crosscheck-policy.json:${sha256(text).slice(0, 12)} | the A/A asked for it (G-3)\n`;

test('The_policy_may_change_in_its_free_fields_only', async () => {
  const f = await stageFixture();
  const text = policyText({ t3: 'fallback-t2', t5: 'stop', decidedOn: '2026-11-25', reason: 'the A/A asked for it' });
  onSeries(f, () => { write(f.repo, POLICY_FILE, text); write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}${policyLine(text)}`); }, 'free fields, with their dated line');
  assert.equal((await guard(f)).ok, true, (await guard(f)).reason);
  for (const [field, value] of [['t1', 'investigate'], ['t2', 'stop'], ['join', 'other'], ['t3OutputSource', 'other']]) {
    const g = await stageFixture();
    onSeries(g, () => write(g.repo, POLICY_FILE, policyText({ [field]: value })), `weaken ${field}`);
    const refused = await guard(g);
    assert.equal(refused.ok, false, field);
    assert.ok(refused.reason.includes(`crosscheck-policy.json differs from the tag in ${field}: only t3, t5 and their dated reason may change`), `${field}: ${refused.reason}`);
  }
});

test('A_change_of_the_policys_free_fields_needs_its_dated_input_line_naming_the_new_content', async () => {
  const f = await stageFixture();
  const text = policyText({ t3: 'fallback-t2', decidedOn: '2026-11-25', reason: 'the A/A asked for it' });
  onSeries(f, () => write(f.repo, POLICY_FILE, text), 'free fields, no line');
  const bare = await guard(f);
  assert.equal(bare.ok, false);
  assert.ok(bare.reason.includes(`crosscheck-policy.json differs from the tagged commit and no dated amendments.md line names it: add \`YYYY-MM-DD | instrument | input:crosscheck-policy.json:${sha256(text).slice(0, 12)} | <reason, with its G-id>\``), bare.reason);
  const wrong = await stageFixture();
  onSeries(wrong, () => { write(wrong.repo, POLICY_FILE, text); write(wrong.repo, AMENDMENTS_FILE, `${AMENDMENTS}${policyLine('another text\n')}`); }, 'a line that names other content');
  assert.match((await guard(wrong)).reason, /no dated amendments\.md line names it/);
});

test('A_policy_that_is_missing_at_the_tag_or_at_head_or_is_not_JSON_is_a_refusal_and_never_a_throw', async () => {
  const gone = await stageFixture();
  onSeries(gone, () => rmSync(`${gone.repo}/${POLICY_FILE}`), 'the policy is deleted');
  assert.match((await guard(gone)).reason, /crosscheck-policy\.json is deleted at HEAD/);
  const untagged = await stageFixture({ extra: repo => rmSync(`${repo}/${POLICY_FILE}`) });
  onSeries(untagged, () => write(untagged.repo, POLICY_FILE, policyText({})), 'a policy the tag never had');
  assert.match((await guard(untagged)).reason, /crosscheck-policy\.json is missing at the tagged commit/);
  const damaged = await stageFixture();
  onSeries(damaged, () => write(damaged.repo, POLICY_FILE, '{not json'), 'a damaged policy');
  assert.match((await guard(damaged)).reason, /crosscheck-policy\.json at HEAD is not JSON: the policy cannot be compared with the tag/);
  const torn = await stageFixture({ extra: repo => write(repo, POLICY_FILE, '{torn') });
  assert.match((await guard(torn)).reason, /crosscheck-policy\.json at the tagged commit is not JSON/);
});

test('The_wall_and_the_stall_may_change_once_by_their_dated_line_and_the_cap_in_dollars_never', async () => {
  const f = await stageFixture();
  onSeries(f, () => { write(f.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000)); write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}${CAPS_LINE}\n`); }, 'the wall and the stall, with their line');
  assert.equal((await guard(f)).ok, true, (await guard(f)).reason);
  const bare = await stageFixture();
  onSeries(bare, () => write(bare.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000)), 'no line');
  assert.match((await guard(bare)).reason, /explore\.json differs from the tagged commit in caps\.wallMs or caps\.stallMs and no dated amendments line names them: add `YYYY-MM-DD \| caps \| wallMs:<n> \| stallMs:<n> \| <reason, with G-18>`/);
  const other = await stageFixture();
  onSeries(other, () => { write(other.repo, EXPLORE_FILE, withCaps(5_400_000, 3_100_000)); write(other.repo, AMENDMENTS_FILE, `${AMENDMENTS}${CAPS_LINE}\n`); }, 'values that are not the line\'s');
  assert.match((await guard(other)).reason, /the caps line says wallMs 5400000 and stallMs 3000000, spikes\/lang-explore\/inputs\/explore\.json holds wallMs 5400000 and stallMs 3100000/);
  for (const [name, over] of [['capUsd', { caps: { capUsd: 60 } }], ['locale', { locale: 'en_US.UTF-8' }], ['cli', { cli: { version: '9.9.9' } }]]) {
    const g = await stageFixture();
    onSeries(g, () => { write(g.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000, over)); write(g.repo, AMENDMENTS_FILE, `${AMENDMENTS}${CAPS_LINE}\n`); }, `change ${name}`);
    const refused = await guard(g);
    assert.equal(refused.ok, false, name);
    assert.match(refused.reason, /explore\.json differs from the tag outside caps\.wallMs and caps\.stallMs \(the cap in dollars and every other field are frozen\)/, name);
  }
  const twice = await stageFixture();
  onSeries(twice, () => { write(twice.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000)); write(twice.repo, AMENDMENTS_FILE, `${AMENDMENTS}${CAPS_LINE}\n2026-11-27 | caps | wallMs:5600000 | stallMs:3200000 | a second change (G-18)\n`); }, 'two lines');
  assert.match((await guard(twice)).reason, /2 caps lines: the wall and the stall change once/);
});

test('A_caps_line_is_read_only_with_a_real_date_two_numbers_and_a_reason_by_the_one_parser_of_dated_lines_and_every_other_line_is_ignored', () => {
  const sans = ({ line: _line, ...entry }) => entry;
  assert.deepEqual(parseStageAmendments(`${AMENDMENTS}${CAPS_LINE}\n`).caps.map(sans), [{ date: '2026-11-25', wallMs: 5_400_000, stallMs: 3_000_000, reason: 'the A/A showed the longest quiet gap (G-18)' }]);
  const bad = ['2026-02-30 | caps | wallMs:1 | stallMs:2 | r', '2026-11-25 | caps | wallMs:x | stallMs:2 | r', '2026-11-25 | caps | wallMs:0 | stallMs:2 | r', '2026-11-25 | caps | wallMs:1 | stallMs:2 |', '2026-11-25 | caps | stallMs:2 | wallMs:1 | r', 'caps | wallMs:1 | stallMs:2 | r'];
  const read = parseStageAmendments(`${bad.join('\n')}\n2026-11-25 | G-1 | holds | abc123 | ~/x | me\n`);
  assert.deepEqual(read.caps, []);
  assert.equal(read.malformed.length, bad.length - 1, 'a line that starts like a caps line and is not one is reported; one with no date at all is no line');
  assert.deepEqual(parseStageAmendments('').caps, []);
  assert.deepEqual(parseStageAmendments(undefined).caps, []);
});

// ---- 74, 75, 76: the tree, the harness identity ----------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_dirty_tree_under_spikes_refuses_a_paid_launch', async () => {
  const f = await stageFixture();
  write(f.repo, 'spikes/lang-explore/scratch.txt', 'untracked\n');
  assert.equal((await guard(f)).ok, true, 'a dry run does not mind');
  assert.match((await paid(f)).reason, /the tree under spikes\/ is not clean \(\?\? spikes\/lang-explore\/scratch\.txt\)/);
});

test('A_harness_identity_that_is_not_the_series_refuses_a_paid_launch_until_a_dated_line_accepts_it_and_never_after_the_first_data_session', async () => {
  const f = await stageFixture();
  onSeries(f, () => write(f.repo, 'spikes/lang-explore/lib/x.mjs', '// a harness change\n'), 'harness');
  assert.match((await paid(f)).reason, /harnessIdentity [0-9a-f]{12} is not the run-map's [0-9a-f]{12} and no amendment accepted it \(put the fix on origin\/main, add the dated line `YYYY-MM-DD \| instrument \| harness:<12 hex> \| commit:<40 hex> <reason, with its G-id>` to .*amendments\.md on origin\/main, then run stage-sync --accept-harness <12 hex>\)/);
  const now = (await harnessIdentity(f.repo, STAGE_PREREG_CONFIG)).value;
  const accepted = { ...f.runMap, harnessAccepted: [now] };
  assert.equal((await paid(f, { runMap: accepted })).ok, true, (await paid(f, { runMap: accepted })).reason);
  // v1's acceptance of that identity: a dated instrument line, and no data session yet
  const map = openSeries({ seriesId: 'x', harnessIdentity: f.runMap.harnessIdentity });
  const line = `2026-11-25 | instrument | harness:${now.slice(0, 12)} | a fix before the first data session (G-3)\n`;
  const before = checkResumable({ map, current: { ...map, harnessIdentity: now }, acceptHarness: { identity: now, amendmentDate: '2026-11-25' }, amendments: line });
  assert.equal(before.ok, true, before.reason);
  const withData = openSeries({ seriesId: 'x', harnessIdentity: f.runMap.harnessIdentity, sessions: { a1b2c3: { rid: 'a1b2c3', kind: 'data', status: 'ran' } } });
  const after = checkResumable({ map: { ...withData, sessions: { a1b2c3: { rid: 'a1b2c3', kind: 'data', status: 'ran' } } }, current: { ...withData, harnessIdentity: now }, acceptHarness: { identity: now, amendmentDate: '2026-11-25' }, amendments: line });
  assert.equal(after.ok, false);
  assert.match(after.reason, /after a data session exists in this series/);
});

test('The_stage_harness_identity_moves_with_a_byte_of_lang_explore_lang_bench_or_v1_and_with_no_other_file', async () => {
  const f = await stageFixture();
  const identity = async () => (await harnessIdentity(f.repo, STAGE_PREREG_CONFIG)).value;
  const moves = [...STAGE_HARNESS_DIRS.map(dir => `${dir}/x.mjs`), ...STAGE_HARNESS_DIRS.map(dir => `${dir}/deeper/y.mjs`), ...STAGE_HARNESS_TOP_DIRS.map(dir => `${dir}top.mjs`), 'spikes/lang-bench/top-new.mjs'];
  for (const file of moves) {
    const before = await identity();
    onSeries(f, () => write(f.repo, file, `// changed ${file}\n`), `change ${file}`);
    assert.notEqual(await identity(), before, `${file} is part of the harness`);
  }
  const still = await identity();
  for (const file of ['spikes/lang-explore/test/t.mjs', 'spikes/lang-explore/inputs/probes.json', 'spikes/lang-explore/inputs/explore.json', 'spikes/lang-bench/inputs/hashes-lang.txt', 'spikes/lang-bench/test/new.test.mjs', `${PREREG_DIR}/statement.md`, AMENDMENTS_FILE, 'spikes/lang-explore/README.md', 'docs/notes.md', 'research/x.md']) {
    onSeries(f, () => write(f.repo, file, `changed ${file}\n`), `change ${file}`);
    assert.equal(await identity(), still, `${file} is no part of the harness`);
  }
  assert.deepEqual(STAGE_HARNESS_DIRS.slice(0, 6), ['spikes/minigit-bench/lib', 'spikes/minigit-bench/harness', 'spikes/minigit-bench/capture', 'spikes/minigit-bench/prereg', 'spikes/policy-ab-quick/lib', 'spikes/policy-ab-quick/hooks'], 'v1\'s six folders first');
  assert.deepEqual(STAGE_HARNESS_DIRS.slice(6), ['spikes/lang-explore/lib', 'spikes/lang-explore/prereg', 'spikes/lang-explore/rehearsal', 'spikes/lang-explore/runner', 'spikes/lang-explore/templates', 'spikes/lang-bench/lib', 'spikes/lang-bench/runner', 'spikes/lang-bench/templates', 'spikes/lang-bench/tools', '.agents/conventions'], 'the plan\'s list, and the two folders of the olympiad judge and templates that no identity of PR 4 reads');
  assert.deepEqual(STAGE_HARNESS_TOP_DIRS, ['spikes/minigit-bench/', 'spikes/lang-explore/', 'spikes/lang-bench/']);
});

// ---- 79a to 79e: the series' own branch ---------------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_series_branch_that_differs_from_the_tag_in_a_harness_file_or_any_path_off_the_list_refuses_the_launch', async () => {
  const f = await stageFixture();
  assert.equal((await paid(f)).ok, true, 'the branch as the tag: nothing differs');
  for (const file of ['docs/notes.md', 'spikes/lang-explore/inputs/probes.json', 'spikes/lang-explore/test/t.mjs']) {
    const g = await stageFixture();
    onSeries(g, () => write(g.repo, file, 'changed\n'), `change ${file}`);
    const refused = await paid(g);
    assert.equal(refused.ok, false, file);
    assert.ok(refused.reason.includes(`the series' branch differs from the tag in ${file}, which is not an amendable path`), `${file}: ${refused.reason}`);
  }
  const h = await stageFixture();
  onSeries(h, () => write(h.repo, 'spikes/lang-explore/lib/x.mjs', '// harness\n'), 'harness change');
  const harness = await paid(h);
  assert.ok(harness.reason.includes('the series\' branch differs from the tag in spikes/lang-explore/lib/x.mjs, which is not an amendable path'), harness.reason);
  assert.equal((await guard(h)).ok, true, 'a dry run does not look at the branch');
});

test('A_registered_line_on_the_series_branch_that_is_not_origin_mains_blob_refuses_the_launch', async () => {
  const f = await stageFixture();
  const line = `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | a reason (G-3)\n`;
  onSeries(f, () => write(f.repo, AMENDMENTS_FILE, line), 'a line only on the series branch');
  const refused = await paid(f);
  assert.match(refused.reason, /amendments\.md at HEAD is not origin\/main's blob: a dated line the series acts on is public first, so take it from main with stage-sync/);
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, line), 'the same line on main');
  assert.equal((await paid(f)).ok, true, 'the same bytes on main: the blob is main\'s');
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, `${line}2026-11-27 | instrument | harness:bbbbbbbbbbbb | another (G-3)\n`), 'main has a newer line');
  assert.match((await paid(f)).reason, /amendments\.md at HEAD is not origin\/main's blob/, 'main moved on: the series\' file is stale until stage-sync');
});

test('The_guard_fetches_before_it_compares_with_origin_main_so_a_line_pushed_from_another_clone_is_seen', async () => {
  const f = await stageFixture();
  assert.equal((await paid(f)).ok, true);
  onOtherClone(f, other => write(other, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-28 | instrument | harness:eeeeeeeeeeee | a line pushed elsewhere (G-3)\n`));
  assert.match((await paid(f)).reason, /amendments\.md at HEAD is not origin\/main's blob/, 'this clone had not fetched: the guard fetches first');
  const synced = await stageSync({ repo: f.repo });
  assert.deepEqual(synced.value.changed, [AMENDMENTS_FILE], 'stage-sync fetches too');
  assert.equal((await paid(f)).ok, true, (await paid(f)).reason);
});

test('A_harness_change_merged_to_main_after_the_tag_neither_reaches_the_series_nor_stops_it', async () => {
  const f = await stageFixture();
  onMain(f, () => { write(f.repo, 'spikes/lang-explore/lib/x.mjs', '// a harness change on main\n'); write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:cccccccccccc | a line (G-3)\n`); }, 'main: a harness change and a dated line');
  assert.match((await paid(f)).reason, /amendments\.md at HEAD is not origin\/main's blob/, 'before the sync the line is on main only');
  const synced = await stageSync({ repo: f.repo });
  assert.equal(synced.ok, true, synced.reason);
  assert.deepEqual(synced.value.changed, [AMENDMENTS_FILE]);
  assert.equal(git(f.repo, 'show', `HEAD:${AMENDMENTS_FILE}`).includes('harness:cccccccccccc'), true, 'the branch holds the line');
  assert.equal(git(f.repo, 'show', 'HEAD:spikes/lang-explore/lib/x.mjs'), '// spikes/lang-explore/lib', 'and not the harness change');
  assert.equal((await paid(f)).ok, true, (await paid(f)).reason);
});

test('stage_sync_takes_the_listed_files_from_origin_main_and_nothing_else_and_never_pushes', async () => {
  const f = await stageFixture();
  onMain(f, () => {
    write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | r (G-3)\n${CAPS_LINE}\n`);
    write(f.repo, SEED_FILE, `${SEED}\n`);
    write(f.repo, QUIET_FILE, '{"windowMs":1}\n');
    write(f.repo, 'spikes/lang-explore/inputs/package-flags.json', '{"flag":1}\n');
    write(f.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000));
    write(f.repo, POLICY_FILE, `${JSON.stringify({ ...POLICY, t3: 'fallback-t2' })}\n`);
    write(f.repo, `${PREREG_DIR}/statement.md`, 'rewritten on main (not amendable)\n');
    write(f.repo, 'docs/notes.md', 'main notes\n');
    write(f.repo, 'spikes/lang-explore/lib/x.mjs', '// main harness\n');
  }, 'main moves');
  const remoteBefore = git(f.repo, 'ls-remote', 'origin');
  const branchBefore = git(f.repo, 'rev-parse', 'HEAD');
  const synced = await stageSync({ repo: f.repo });
  assert.equal(synced.ok, true, synced.reason);
  assert.deepEqual([...synced.value.changed].sort(), [...AMENDABLE_PATHS].sort(), 'exactly the six listed paths');
  for (const file of AMENDABLE_PATHS) assert.equal(headBlob(f.repo, file), git(f.repo, 'rev-parse', `origin/main:${file}`), `${file} is main's blob`);
  for (const file of [`${PREREG_DIR}/statement.md`, 'docs/notes.md', 'spikes/lang-explore/lib/x.mjs']) assert.equal(headBlob(f.repo, file), git(f.repo, 'rev-parse', `${f.sha}:${file}`), `${file} is still the tag's`);
  assert.equal(git(f.repo, 'rev-list', '--count', `${branchBefore}..HEAD`), '1', 'one commit on the series\' branch');
  assert.match(git(f.repo, 'log', '-1', '--format=%s'), /^chore\(series\): stage-sync takes 6 registered files from origin\/main/);
  assert.equal(git(f.repo, 'ls-remote', 'origin'), remoteBefore, 'nothing was pushed: the origin holds exactly what it held');
  assert.deepEqual(pathsOf(f, 'HEAD').sort(), [...AMENDABLE_PATHS].sort(), 'the branch differs from the tag in the listed paths only');
  const again = await stageSync({ repo: f.repo });
  assert.deepEqual([again.ok, again.value.changed], [true, []], 'a second sync finds nothing and makes no commit');
  assert.equal(git(f.repo, 'rev-list', '--count', `${branchBefore}..HEAD`), '1');
});

test('A_listed_file_main_does_not_have_is_removed_from_the_branch_and_a_dirty_tree_or_the_main_branch_is_refused', async () => {
  const f = await stageFixture();
  onSeries(f, () => write(f.repo, SEED_FILE, `${SEED}\n`), 'a seed only here');
  assert.equal((await stageSync({ repo: f.repo })).value.changed.join(), SEED_FILE, 'main has none, so the branch drops it');
  assert.equal(git(f.repo, 'ls-files', SEED_FILE), '');
  write(f.repo, 'spikes/lang-explore/scratch.txt', 'x\n');
  assert.match((await stageSync({ repo: f.repo })).reason, /the tree is not clean: spikes\/lang-explore\/scratch\.txt is staged or changed: stage-sync commits only what it takes/);
  git(f.repo, 'clean', '-fdq', '--', 'spikes/');
  git(f.repo, 'checkout', '-q', 'main');
  assert.match((await stageSync({ repo: f.repo })).reason, /stage-sync runs on the series' branch, not on main/);
});

// ---- a harness acceptance, epoch 11: the fix is on main first, its dated line names the commit, `stage-sync --accept-harness <12 hex>` takes the harness folders of exactly that commit -------------------------------------------

import { FIX_FILE, NEW_FILE, NOTES_FILE, OLD_FILE, REASON, accept, datedLine, fixChange, fixedOnMain, fixedSubmodule, seriesOf, stateOf, submoduleFixture } from './stage-accept-fixture.mjs';


test('A_harness_fix_on_main_with_its_dated_line_is_taken_by_stage_sync_accept_harness_and_a_paid_launch_then_passes', async () => {
  const { f, commit: fix, identity, hex } = await fixedOnMain();
  const [head, remoteBefore] = [git(f.repo, 'rev-parse', 'HEAD'), git(f.repo, 'ls-remote', 'origin')];
  const taken = await accept(f, hex);
  assert.equal(taken.code, 0, taken.lines);
  assert.match(taken.lines, new RegExp(`^stage-sync: fetching origin \\.\\.\\.\\|stage-sync --accept-harness ${hex}: committed [0-9a-f]{12}: 3 harness files taken from commit ${fix.slice(0, 12)}$`));
  assert.equal(git(f.repo, 'rev-list', '--count', `${head}..HEAD`), '1', 'one commit on the series\' branch');
  assert.match(git(f.repo, 'log', '-1', '--format=%s'), new RegExp(`^chore\\(series\\): stage-sync --accept-harness ${hex} takes the harness folders of ${fix.slice(0, 12)} from origin/main`));
  assert.deepEqual(pathsOf(f, 'HEAD').sort(), [AMENDMENTS_FILE, FIX_FILE, NEW_FILE, OLD_FILE].sort(), 'the dated line and the three harness files, nothing else');
  assert.equal(readFileSync(path.join(f.repo, FIX_FILE), 'utf8'), '// the fix, reviewed on main\n', 'the edited file is the fix\'s');
  assert.equal([existsSync(path.join(f.repo, OLD_FILE)), existsSync(path.join(f.repo, NEW_FILE))].join(), 'false,true', 'the deleted file is gone and the added one is there, in the tree and in the commit');
  assert.equal(headBlob(f.repo, NOTES_FILE), git(f.repo, 'rev-parse', `${f.sha}:${NOTES_FILE}`), 'a file of the fix commit outside the harness folders is not taken');
  assert.equal((await harnessIdentity(f.repo, STAGE_PREREG_CONFIG)).value, identity, 'the branch holds exactly the harness of the fix commit');
  assert.equal(git(f.repo, 'status', '--porcelain', '--untracked-files=all'), '', 'a clean tree');
  assert.equal(git(f.repo, 'ls-remote', 'origin'), remoteBefore, 'nothing was pushed');
  const text = git(f.repo, 'show', `HEAD:${AMENDMENTS_FILE}`);
  assert.deepEqual(acceptedHarness({ text, map: {} }), [hex], 'the line the branch holds accepts that identity');
  assert.match((await paid(f)).reason, /harnessIdentity [0-9a-f]{12} is not the run-map's [0-9a-f]{12} and no amendment accepted it \(put the fix on origin\/main, add the dated line/, 'without the acceptance in the run-map the new harness is refused');
  const accepted = { ...f.runMap, harnessAccepted: [identity] };
  assert.equal((await paid(f, { runMap: accepted })).ok, true, (await paid(f, { runMap: accepted })).reason);
  const again = await accept(f, hex);
  assert.deepEqual([again.code, again.lines.split('|').at(-1)], [0, `stage-sync --accept-harness ${hex}: nothing to take: the series' branch holds the harness folders of ${fix.slice(0, 12)}`], 'a second call finds nothing and commits nothing');
});

test('A_fix_commit_that_is_not_reachable_from_origin_main_is_refused_and_nothing_is_taken', async () => {
  const { f, commit: fix, hex } = await fixedOnMain({ merged: false });
  const before = stateOf(f);
  const refused = await accept(f, hex);
  assert.equal(refused.code, 4, refused.lines);
  assert.match(refused.lines, new RegExp(`commit ${fix.slice(0, 12)} is not reachable from origin/main: the fix must be merged on main first`));
  assert.deepEqual(stateOf(f), before, 'nothing was taken, nothing is staged, the tree is clean');
});

const TOKEN_CASES = Object.freeze([
  ['a_line_with_no_commit_token', () => REASON, /does not begin its reason with `commit:<40 hex>`/],
  ['a_line_whose_token_is_not_the_beginning_of_its_reason', ({ commit: sha }) => `${REASON} commit:${sha}`, /does not begin its reason with `commit:<40 hex>`/],
  ['a_line_whose_token_is_a_short_sha', ({ commit: sha }) => `commit:${sha.slice(0, 12)} ${REASON}`, /does not begin its reason with `commit:<40 hex>`/],
  ['a_line_with_two_commit_tokens', ({ commit: sha }) => `commit:${sha} and commit:${'a'.repeat(40)} ${REASON}`, /holds more than one `commit:` token/],
]);

for (const [name, reasonOf, sentence] of TOKEN_CASES) {
  test(`stage_sync_accept_harness_refuses_${name}_and_nothing_is_taken`, async () => {
    const { f, hex } = await fixedOnMain({ line: fix => datedLine(fix, reasonOf(fix)) });
    const before = stateOf(f);
    const refused = await accept(f, hex);
    assert.equal(refused.code, 4, refused.lines);
    assert.match(refused.lines, sentence);
    assert.deepEqual(stateOf(f), before);
  });
}

test('stage_sync_accept_harness_refuses_an_identity_no_dated_line_names_and_one_two_lines_name', async () => {
  const none = await fixedOnMain();
  const missing = await accept(none.f, 'ffffffffffff');
  assert.equal(missing.code, 4, missing.lines);
  assert.match(missing.lines, /no dated line `YYYY-MM-DD \| instrument \| harness:ffffffffffff \| commit:<40 hex> <reason>` in spikes\/lang-explore\/inputs\/prereg\/amendments\.md on origin\/main/);
  const twice = await fixedOnMain({ line: fix => `${datedLine(fix)}\n${datedLine(fix)}` });
  const ambiguous = await accept(twice.f, twice.hex);
  assert.equal(ambiguous.code, 4, ambiguous.lines);
  assert.match(ambiguous.lines, new RegExp(`2 dated lines name harness:${twice.hex}: one line names one commit`));
});

test('A_fix_whose_folders_do_not_have_the_identity_of_the_line_is_refused_and_the_tree_is_restored', async () => {
  const { f, hex, commit: fix } = await fixedOnMain({ line: fix => datedLine({ ...fix, identity: 'aaaaaaaaaaaa'.padEnd(64, '0') }) });
  const before = stateOf(f);
  const refused = await accept(f, 'aaaaaaaaaaaa');
  assert.equal(refused.code, 4, refused.lines);
  assert.match(refused.lines, new RegExp(`the harness folders of commit ${fix.slice(0, 12)} have the identity ${hex}, not the aaaaaaaaaaaa the dated line names: nothing was taken`));
  assert.deepEqual(stateOf(f), before, 'the files the take had written and staged are back as the branch had them');
  assert.deepEqual([existsSync(path.join(f.repo, OLD_FILE)), existsSync(path.join(f.repo, NEW_FILE))], [true, false], 'the deleted file is back and the added one is gone');
  assert.equal((await accept(f, hex)).code, 4, 'the identity the folders do have is no line of the branch: still refused, still clean');
  assert.deepEqual(stateOf(f), before);
});

/** The run-map of the series in `dir` with `sessions` added (and `over` merged into the map). */
function withSessions(dir, sessions, over = {}) {
  const file = runMapFileOf(dir);
  writeRunMap(file, { ...readRunMap(file).value, ...over, sessions: { ...readRunMap(file).value.sessions, ...sessions } });
}

test('stage_sync_accept_harness_is_refused_once_a_data_session_has_left_planned_or_the_acceptance_is_frozen', async () => {
  const cases = [
    [{ a1b2c3: { rid: 'a1b2c3', kind: 'data', status: 'ran' } }, {}, /a data session has left planned in the series .*: the instrument is frozen with the first data session, so no harness is accepted now: start a new series/],
    [{ a1b2c3: { rid: 'a1b2c3', kind: 'data', status: 'planned' } }, { harnessAtFirstData: 'bbbbbbbbbbbb' }, /the first data plan of the series .* froze the harness identity bbbbbbbbbbbb: no other harness is accepted now/],
  ];
  for (const [sessions, over, why] of cases) {
    const { f, hex } = await fixedOnMain();
    const series = await seriesOf(f);
    withSessions(series.dir, sessions, over);
    const before = stateOf(f);
    const refused = await accept(f, hex, { home: series.io.home });
    assert.equal(refused.code, 4, refused.lines);
    assert.match(refused.lines, why);
    assert.deepEqual(stateOf(f), before);
    assert.equal(existsSync(`${series.dir}/series.lock`), false, 'the locks are released');
  }
  const { f, hex } = await fixedOnMain();
  const control = await seriesOf(f);
  assert.equal((await accept(f, hex, { home: control.io.home })).code, 0, 'the control: a series with no data session accepts');
});

test('stage_sync_accept_harness_takes_the_series_locks_and_a_unit_that_holds_one_refuses_it', async () => {
  const { f, hex } = await fixedOnMain();
  const { io, dir } = await seriesOf(f);
  const before = stateOf(f);
  const unit = acquireSeriesLock({ seriesDir: dir });
  assert.equal(unit.ok, true, unit.reason);
  const refused = await accept(f, hex, { home: io.home });
  unit.value.release();
  assert.equal(refused.code, 4, refused.lines);
  assert.match(refused.lines, new RegExp(`^stage-sync does not run while a unit runs: the series ${path.basename(dir)} is in use by pid ${process.pid}`));
  assert.deepEqual(stateOf(f), before);
  assert.equal((await accept(f, hex, { home: io.home })).code, 0, 'and it runs once the unit is done');
});

test('stage_sync_accept_harness_takes_the_commit_of_the_shared_rules_submodule_and_moves_its_checkout', async () => {
  const { f, sub, fixed, identity } = await fixedSubmodule();
  assert.notEqual(git(f.repo, 'ls-tree', 'HEAD', '.agents/conventions').split(/\s+/)[2], fixed, 'the branch records the old rules commit');
  const taken = await accept(f, identity.slice(0, 12));
  assert.equal(taken.code, 0, taken.lines);
  assert.equal(git(f.repo, 'ls-tree', 'HEAD', '.agents/conventions').split(/\s+/)[2], fixed, 'the branch records the new rules commit');
  assert.equal(git(sub, 'rev-parse', 'HEAD'), fixed, 'and the submodule is checked out there');
  assert.equal((await paid(f, { runMap: { ...f.runMap, harnessAccepted: [identity] } })).ok, true, (await paid(f, { runMap: { ...f.runMap, harnessAccepted: [identity] } })).reason);
});

test('stage_sync_accept_harness_with_no_value_or_a_value_that_is_not_12_hex_is_refused_with_a_sentence_naming_the_value_it_needs', async () => {
  const f = await stageFixture(); // the repository and the home are the temporary ones: a mutation of the check reaches no real remote
  for (const argv of [['stage-sync', '--accept-harness'], ['stage-sync', '--accept-harness='], ['stage-sync', '--accept-harness', 'ABCDEF012345'], ['stage-sync', '--accept-harness=0123456789a'], ['stage-sync', '--accept-harness', 'aaaaaaaaaaaa', '--accept-harness', 'not-hex']]) {
    const lines = [];
    assert.equal(await main(argv, { say: l => lines.push(l), repo: f.repo, home: tmpDir('stage-home') }), 4, argv.join(' '));
    assert.deepEqual(lines.length, 1, `${argv.join(' ')}: refused before any fetch: ${lines.join('|')}`);
    assert.match(lines[0], /^--accept-harness needs a value: the 12 hex \(0-9, a-f\) of the harness identity that a dated line `YYYY-MM-DD \| instrument \| harness:<12 hex> \| commit:<40 hex> <reason>` names in inputs\/prereg\/amendments\.md on origin\/main, as in `stage-sync --accept-harness 0123456789ab`/, argv.join(' '));
  }
});

test('stage_sync_without_the_flag_takes_the_registered_files_only_and_never_a_harness_folder', async () => {
  const f = await stageFixture();
  onMain(f, () => { write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | commit:${'1'.repeat(40)} r (G-3)\n`); write(f.repo, FIX_FILE, '// main harness\n'); }, 'main: a line and a harness change');
  const plain = [];
  assert.equal(await cmdStageSync({}, { repo: f.repo, say: l => plain.push(l) }), 0, plain.join('|'));
  assert.match(plain.join('|'), /stage-sync: committed [0-9a-f]{12}: .*amendments\.md/, 'without the flag the same call takes the line');
  assert.equal(headBlob(f.repo, FIX_FILE), git(f.repo, 'rev-parse', `${f.sha}:${FIX_FILE}`), 'and leaves the harness as the tag has it');
  assert.equal(await cmdStageSync({}, { repo: f.repo, say: l => plain.push(l) }), 0);
  assert.match(plain.at(-1), /nothing to take/);
  assert.equal(await cmdStageSync({ series: '/no/such/series' }, { repo: f.repo, say: l => plain.push(l) }), 4, 'a --series that is no Stage 1 series is a refusal');
});

test('stage_sync_says_it_is_fetching_before_the_fetch_and_then_its_result_line', async () => {
  const f = await stageFixture();
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | r (G-3)\n`), 'a line on main');
  const lines = [];
  assert.equal(await cmdStageSync({}, { repo: f.repo, say: l => lines.push(l) }), 0, lines.join('|'));
  assert.equal(lines.length, 2, lines.join('|'));
  assert.equal(lines[0], 'stage-sync: fetching origin ...', 'the line comes before the fetch, which may wait up to 120 s');
  assert.match(lines[1], /^stage-sync: committed [0-9a-f]{12}: .*amendments\.md$/);
  const again = [];
  assert.equal(await cmdStageSync({}, { repo: f.repo, say: l => again.push(l) }), 0);
  assert.deepEqual(again, ['stage-sync: fetching origin ...', 'stage-sync: nothing to take: the series\' branch holds origin/main\'s registered files']);
});

// ---- a failed stage-sync leaves the tree as it was (coai code round 1, PR 5, D4) ------------------------------------------------------------------------------------------------------------------------

/** A pre-commit hook that refuses every commit, in a hooks folder of its own: the commit of `stage-sync` fails after its files are taken. Returns the function that removes it. */
function refuseCommits(repo) {
  const hooks = path.join(repo, '.git', 'refusing-hooks');
  mkdirSync(hooks, { recursive: true });
  writeFileSync(path.join(hooks, 'pre-commit'), '#!/bin/sh\necho "the hook says no" >&2\nexit 1\n');
  chmodSync(path.join(hooks, 'pre-commit'), 0o755);
  git(repo, 'config', 'core.hooksPath', hooks);
  return () => git(repo, 'config', '--unset', 'core.hooksPath');
}

const mainTakes = f => onMain(f, () => { write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | r (G-3)\n`); write(f.repo, SEED_FILE, `${SEED}\n`); write(f.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000)); }, 'main: a line, the seed and new caps');

test('A_stage_sync_whose_commit_fails_restores_every_file_and_index_entry_and_a_retry_succeeds', async () => {
  const f = await stageFixture();
  mainTakes(f);
  const head = git(f.repo, 'rev-parse', 'HEAD');
  const allow = refuseCommits(f.repo);
  const failed = await stageSync({ repo: f.repo });
  assert.equal(failed.ok, false);
  assert.match(failed.reason, /the hook says no/);
  assert.deepEqual([git(f.repo, 'status', '--porcelain', '--untracked-files=all'), git(f.repo, 'diff', '--cached', '--name-only'), git(f.repo, 'rev-parse', 'HEAD')], ['', '', head], 'the tree is clean, nothing is staged, nothing is committed');
  allow();
  const retry = await stageSync({ repo: f.repo });
  assert.equal(retry.ok, true, retry.reason);
  assert.deepEqual([...retry.value.changed].sort(), [AMENDMENTS_FILE, EXPLORE_FILE, SEED_FILE].sort());
});

test('A_stage_sync_whose_commit_fails_brings_back_a_file_it_had_removed', async () => {
  const f = await stageFixture();
  onMain(f, () => write(f.repo, SEED_FILE, `${SEED}\n`), 'main reveals the seed');
  assert.equal((await stageSync({ repo: f.repo })).ok, true);
  onMain(f, () => { rmSync(path.join(f.repo, SEED_FILE)); write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-27 | instrument | harness:bbbbbbbbbbbb | r (G-3)\n`); }, 'main drops the seed and adds a line');
  const head = git(f.repo, 'rev-parse', 'HEAD');
  const allow = refuseCommits(f.repo);
  assert.equal((await stageSync({ repo: f.repo })).ok, false);
  assert.deepEqual([git(f.repo, 'status', '--porcelain', '--untracked-files=all'), git(f.repo, 'rev-parse', 'HEAD'), headBlob(f.repo, SEED_FILE).length], ['', head, 40], 'the removed seed file is back and the tree is clean');
  allow();
  assert.deepEqual((await stageSync({ repo: f.repo })).value.changed.sort(), [AMENDMENTS_FILE, SEED_FILE].sort());
});

test('A_stage_sync_that_cannot_compute_one_file_writes_none_of_the_others', async () => {
  const f = await stageFixture();
  onMain(f, () => { write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | r (G-3)\n`); write(f.repo, EXPLORE_FILE, '{"cli":{"version":"2.1.330"}}\n'); }, 'main: a line, and an explore.json with no caps');
  const head = git(f.repo, 'rev-parse', 'HEAD');
  const refused = await stageSync({ repo: f.repo });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /explore\.json cannot be read with caps\.wallMs and caps\.stallMs at HEAD and at origin\/main: nothing was taken/);
  assert.deepEqual([git(f.repo, 'status', '--porcelain', '--untracked-files=all'), git(f.repo, 'rev-parse', 'HEAD')], ['', head], 'the amendments file that came first was not written either');
});

// ---- stage-sync takes the series locks for its whole run (coai code round 1, PR 5, D3) -------------------------------------------------------------------------------------------------------------------

test('A_stage_run_started_in_the_middle_of_a_stage_sync_is_refused_naming_the_holder_and_runs_after_it', async () => {
  const f = await stageFixture();
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | r (G-3)\n`), 'main has a line');
  const rig = await runRig();
  const middle = [];
  const syncFn = async args => {
    middle.push(await stageRun({ values: { series: rig.dir, what: 'next' }, io: rig.io }), existsSync(`${rig.dir}/series.lock`));
    return stageSync({ ...args, series: undefined }); // the rig's series records no branch: this test is of the locks
  };
  const lines = [];
  assert.equal(await cmdStageSync({}, { repo: f.repo, home: rig.io.home, say: l => lines.push(l), syncFn }), 0, lines.join('|'));
  assert.equal(middle[0].exit, 4, 'the unit is refused in the middle of the sync');
  assert.match(middle[0].message, new RegExp(`in use by pid ${process.pid}`), 'the holder is named');
  assert.equal(middle[1], true, 'the sync holds the lock while it works');
  assert.equal(rig.log.length, 0, 'the refused call launched nothing');
  assert.equal(existsSync(`${rig.dir}/series.lock`), false, 'the lock is released when the sync ends');
  assert.equal((await stageRun({ values: { series: rig.dir, what: 'next' }, io: rig.io })).exit, 0, 'and a unit runs after it');
});

test('A_stage_sync_that_cannot_take_every_series_lock_releases_the_ones_it_took_and_refuses_naming_the_holder', async () => {
  const f = await stageFixture();
  const first = await makeSeries();
  const second = { dir: path.join(path.dirname(first.dir), 'x-20261121-cd34') };
  cpSync(first.dir, second.dir, { recursive: true });
  assert.deepEqual(stageSeriesDirs(first.io.home).sort(), [first.dir, second.dir].sort(), 'two Stage 1 series under the explore root');
  for (const [held, other] of [[first.dir, second.dir], [second.dir, first.dir]]) {
    const unit = acquireSeriesLock({ seriesDir: held });
    assert.equal(unit.ok, true, unit.reason);
    const [lines, ran] = [[], []];
    const code = await cmdStageSync({}, { repo: f.repo, home: first.io.home, say: l => lines.push(l), syncFn: async () => { ran.push(true); return { ok: true, value: { changed: [], commit: null } }; } });
    assert.equal(code, 4, lines.join('|'));
    assert.match(lines.at(-1), new RegExp(`^stage-sync does not run while a unit runs: the series ${path.basename(held)} is in use by pid ${process.pid}`));
    assert.equal(existsSync(`${other}/series.lock`), false, 'the lock it had taken is released');
    assert.deepEqual(ran, [], 'the sync did not start');
    unit.value.release();
  }
});

test('A_stage_sync_releases_every_series_lock_when_the_sync_refuses_and_when_it_throws', async () => {
  const f = await stageFixture();
  const { io, dir } = await makeSeries();
  const sync = syncFn => cmdStageSync({}, { repo: f.repo, home: io.home, say: () => {}, syncFn });
  assert.equal(await sync(async () => ({ ok: false, reason: 'refused by a test' })), 4);
  assert.equal(existsSync(`${dir}/series.lock`), false, 'released after a refusal');
  await assert.rejects(sync(async () => { throw new Error('a crash'); }), /a crash/);
  assert.equal(existsSync(`${dir}/series.lock`), false, 'released after a throw');
  const again = acquireSeriesLock({ seriesDir: dir });
  assert.equal(again.ok, true, again.reason);
  again.value.release();
});

// ---- a failed fetch is a named refusal, never a check against a stale origin/main (coai plan round 2, PR 5, finding F) ---------------------------------------------------------------------------------

test('A_fetch_that_exits_non_zero_refuses_the_guard_and_stage_sync_naming_the_fetch', async () => {
  const f = await stageFixture();
  git(f.repo, 'config', 'remote.origin.fetch', '+refs/heads/no-such-branch:refs/remotes/origin/no-such-branch'); // the tags are still readable: only the fetch fails
  assert.equal(STAGE_PREREG_CONFIG.branch, branchProblems, 'the guard\'s branch check is the one with the fetch');
  assert.match((await paid(f)).reason, /cannot fetch origin: git fetch exited 128/);
  const synced = await stageSync({ repo: f.repo });
  assert.equal(synced.ok, false);
  assert.match(synced.reason, /^cannot fetch origin: git fetch exited 128/);
});

test('A_fetch_that_does_not_end_is_stopped_by_its_timeout_and_refuses_the_guard_and_stage_sync_naming_the_fetch_and_the_timeout', async () => {
  const f = await stageFixture();
  git(f.repo, 'config', 'protocol.ext.allow', 'always');
  git(f.repo, 'remote', 'set-url', 'origin', 'ext::sleep 5');
  const guarded = await branchProblems(f.repo, f.runMap, { fetchMs: 300 });
  assert.equal(guarded.length, 1);
  assert.match(guarded[0], /^cannot fetch origin: git fetch timed out after 300 ms and was stopped$/);
  const synced = await stageSync({ repo: f.repo, fetchMs: 300 });
  assert.equal(synced.ok, false);
  assert.match(synced.reason, /^cannot fetch origin: git fetch timed out after 300 ms and was stopped$/);
});

// ---- stage-sync takes the two caps of explore.json and nothing else of it (Opus review, PR 5, V4) -------------------------------------------------------------------------------------------------------

test('Another_change_of_explore_json_on_main_neither_reaches_the_series_nor_stops_it', async () => {
  const f = await stageFixture();
  const tagged = headBlob(f.repo, EXPLORE_FILE);
  onMain(f, () => write(f.repo, EXPLORE_FILE, withCaps(4_500_000, 2_700_000, { cli: { version: '2.1.999' }, locale: 'en_US.UTF-8' })), 'main re-pins the cli and the locale');
  assert.equal((await paid(f)).ok, true, `before the sync: only the two caps are compared with main's: ${(await paid(f)).reason}`);
  const synced = await stageSync({ repo: f.repo });
  assert.deepEqual([synced.ok, synced.value.changed, synced.value.commit], [true, [], null], 'nothing is taken: the two caps are the tag\'s');
  assert.equal(headBlob(f.repo, EXPLORE_FILE), tagged, 'every other byte of explore.json is the tag\'s');
});

test('A_cap_change_on_main_with_its_dated_line_is_taken_as_the_two_values_in_the_tags_own_bytes', async () => {
  const f = await stageFixture();
  const tagText = git(f.repo, 'show', `HEAD:${EXPLORE_FILE}`);
  onMain(f, () => { write(f.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000, { cli: { version: '2.1.999' } })); write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}${CAPS_LINE}\n`); }, 'main: the caps with their line, and a cli re-pin');
  const synced = await stageSync({ repo: f.repo });
  assert.equal(synced.ok, true, synced.reason);
  assert.deepEqual([...synced.value.changed].sort(), [AMENDMENTS_FILE, EXPLORE_FILE].sort());
  assert.equal(git(f.repo, 'show', `HEAD:${EXPLORE_FILE}`), tagText.replace('"wallMs":4500000', '"wallMs":5400000').replace('"stallMs":2700000', '"stallMs":3000000'), 'the tag\'s bytes but the two values: the cli pin stays the tag\'s');
  assert.equal((await paid(f)).ok, true, (await paid(f)).reason);
  onMain(f, () => write(f.repo, EXPLORE_FILE, withCaps(5_400_000, 3_000_000, { cli: { version: '2.1.1000' } })), 'main re-pins the cli again');
  assert.equal((await paid(f)).ok, true, 'the caps are main\'s: the other change does not stop the series');
  onMain(f, () => write(f.repo, EXPLORE_FILE, withCaps(5_400_000, 3_100_000)), 'main moves a cap with no line');
  assert.match((await paid(f)).reason, /explore\.json: caps\.wallMs and caps\.stallMs at HEAD are not origin\/main's/);
});

// ---- stage-sync commits what it took and nothing else (Opus review, PR 5, V5) ---------------------------------------------------------------------------------------------------------------------

test('A_file_staged_or_changed_anywhere_in_the_repo_refuses_stage_sync_and_nothing_is_committed', async () => {
  const f = await stageFixture();
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | r (G-3)\n`), 'main has a line');
  write(f.repo, 'todo/plan.md', 'a plan the operator staged\n');
  git(f.repo, 'add', 'todo/plan.md');
  const head = git(f.repo, 'rev-parse', 'HEAD');
  const staged = await stageSync({ repo: f.repo });
  assert.equal(staged.ok, false);
  assert.match(staged.reason, /^the tree is not clean: todo\/plan\.md is staged or changed: stage-sync commits only what it takes from origin\/main, so a file of a person's is never swept into its commit/);
  assert.deepEqual([git(f.repo, 'rev-parse', 'HEAD'), git(f.repo, 'diff', '--cached', '--name-only')], [head, 'todo/plan.md'], 'nothing was committed and the index is as it was');
  git(f.repo, 'reset', '-q', 'todo/plan.md');
  write(f.repo, 'docs/notes.md', 'changed, not staged\n');
  assert.match((await stageSync({ repo: f.repo })).reason, /docs\/notes\.md is staged or changed/);
  git(f.repo, 'checkout', '-q', '--', 'docs/notes.md');
  write(f.repo, 'docs/scratch.txt', 'untracked, outside spikes/\n');
  const synced = await stageSync({ repo: f.repo });
  assert.equal(synced.ok, true, synced.reason);
  assert.deepEqual(git(f.repo, 'show', '--name-only', '--format=', 'HEAD').split('\n'), [AMENDMENTS_FILE], 'the commit holds the taken file and nothing else');
});

// ---- stage-sync does not run while a unit runs (Opus review, PR 5, V7) ------------------------------------------------------------------------------------------------------------------------------

test('Stage_sync_refuses_while_a_Stage_1_series_of_the_explore_root_holds_its_series_lock_and_runs_after_the_lock_is_released', async () => {
  const f = await stageFixture();
  onMain(f, () => write(f.repo, AMENDMENTS_FILE, `${AMENDMENTS}2026-11-26 | instrument | harness:aaaaaaaaaaaa | r (G-3)\n`), 'main has a line');
  const { io, dir } = await seriesOf(f);
  const lines = [];
  const sync = () => cmdStageSync({}, { repo: f.repo, home: io.home, say: l => lines.push(l) });
  const lock = acquireSeriesLock({ seriesDir: dir });
  assert.equal(lock.ok, true, lock.reason);
  const head = git(f.repo, 'rev-parse', 'HEAD');
  assert.equal(await sync(), 4);
  assert.match(lines.at(-1), new RegExp(`^stage-sync does not run while a unit runs: the series ${path.basename(dir)} is in use by pid ${process.pid} \\(started .*\\): another run, resume or settle holds ${dir}/series\\.lock`));
  assert.equal(git(f.repo, 'rev-parse', 'HEAD'), head, 'nothing was fetched into the branch, nothing committed');
  lock.value.release();
  assert.equal(await sync(), 0, lines.join('|'));
  writeFileSync(`${dir}/series.lock`, JSON.stringify({ pid: 2 ** 22 + 1, startTime: 'gone' }));
  assert.equal(await sync(), 0, 'a lock whose owner is gone is stale and stops nothing');
  writeFileSync(`${dir}/series.lock`, 'half a stamp');
  assert.equal(await sync(), 4, 'an unreadable lock may be a runner in the middle of writing it: it counts as held');
  assert.match(lines.at(-1), /^stage-sync does not run while a unit runs: .*series\.lock exists and is unreadable: it may be a runner that is writing it/);
});

// ---- the harness code outside the folders: lang-bench/tools and the shared rules' submodule (Opus review, PR 5, V6) --------------------------------------------------------------------------------------

/** A fixture whose `.agents/conventions` is a real submodule (a small repository made beside it): `{ f, sub }`. */

test('The_shared_rules_submodule_and_lang_bench_tools_are_in_the_stage_harness_identity_by_name', () => {
  assert.ok(STAGE_HARNESS_DIRS.includes('spikes/lang-bench/tools'), 'the harness imports it (workspace-quick.mjs, freeze-plan.mjs)');
  assert.ok(STAGE_HARNESS_DIRS.includes('.agents/conventions'), 'the scoring code imports the submodule (owned.mjs, pass-seams.mjs, package-quick.mjs): its commit is part of the identity');
});

test('A_paid_launch_refuses_a_submodule_that_is_not_at_the_commit_the_superproject_records_or_has_local_changes', async () => {
  const { f, sub } = await submoduleFixture();
  const identity = async () => (await harnessIdentity(f.repo, STAGE_PREREG_CONFIG)).value;
  assert.equal((await paid(f)).ok, true, (await paid(f)).reason);
  const before = await identity();
  writeFileSync(path.join(sub, 'rules.md'), 'edited in the checkout\n');
  const dirty = await paid(f);
  assert.match(dirty.reason, /the submodule \.agents\/conventions has local changes: M rules\.md: a paid launch runs the scoring code of the commit the superproject records, as it is/);
  git(sub, 'checkout', '-q', '--', 'rules.md');
  assert.equal((await paid(f)).ok, true, 'clean again');
  write(sub, 'rules2.md', 'a commit made inside the submodule only\n');
  const moved = commit(sub, 'a local commit');
  const other = await paid(f);
  assert.match(other.reason, new RegExp(`the submodule \\.agents/conventions is at ${moved.slice(0, 12)}, the superproject records [0-9a-f]{12}`));
  assert.equal(await identity(), before, 'the identity is the RECORDED commit: only a commit of the superproject moves it');
  git(f.repo, 'add', '.agents/conventions');
  commit(f.repo, 'the superproject records the new rules commit');
  assert.notEqual(await identity(), before, 'recording another submodule commit moves the identity');
});

test('The_stage_context_records_the_stage_harness_identity_for_a_stage_series_and_v1s_for_any_other', async () => {
  const stage = await runCtx.harnessIdentityOf({ stage: 1 });
  assert.equal(stage.value, (await harnessIdentity(REPO, STAGE_HARNESS_LISTS)).value);
  const v1 = await runCtx.harnessIdentityOf({ seriesId: 'x' });
  assert.equal(v1.value, (await harnessIdentity(REPO)).value);
  assert.notEqual(stage.value, v1.value, 'the two identities are not the same text');
});
