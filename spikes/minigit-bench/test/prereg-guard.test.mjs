// The pre-registration guard (E3b.S1 step 10): no paid launch unless the tagged pre-registration is the one on origin, every registered file is as tagged,
// the tree is clean and the harness is the one the run-map pinned. Over a real temporary repository with a real bare remote.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { PREREG_FILES, V1_PREREG_CONFIG, harnessIdentity, preregGuard } from '../lib/prereg-guard.mjs';
import { sdkCheckOfTexts, sdkProblem } from '../lib/prereg-consistency.mjs';
import { createHash } from 'node:crypto';
import { INPUTS, SEED, commit, fixture, git, promptsText, stacksText, write } from './prereg-fixtures.mjs';

async function pinned(f) { f.runMap.harnessIdentity = (await harnessIdentity(f.repo)).value; return f; }
const guard = (f, over = {}) => preregGuard({ repo: f.repo, runMap: f.runMap, paid: false, requireSeed: false, ...over });

test('The_guard_checks_tag_blobs_tree_and_harness', async () => {
  const f = await pinned(fixture());
  const clean = await guard(f);
  assert.equal(clean.ok, true, clean.reason);

  // no tag on origin
  const none = fixture({ push: false });
  none.runMap.harnessIdentity = (await harnessIdentity(none.repo)).value;
  git(none.repo, 'push', '-q', 'origin', 'main');
  const noTag = await guard(none);
  assert.equal(noTag.ok, false);
  assert.match(noTag.reason, /tag .* is not on origin/);

  // the tag on origin peels to another commit than the run-map says
  const other = await pinned(fixture());
  other.runMap.preregCommit = 'f'.repeat(40);
  const moved = await guard(other);
  assert.equal(moved.ok, false);
  assert.match(moved.reason, /peels to/);

  // a registered blob changed after the tag
  const edited = await pinned(fixture());
  write(edited.repo, `${INPUTS}/quiet.json`, 'changed\n');
  commit(edited.repo, 'edit a registered file');
  const blob = await guard(edited);
  assert.equal(blob.ok, false);
  assert.match(blob.reason, /quiet\.json/);

  // a registered file deleted after the tag
  const gone = await pinned(fixture());
  git(gone.repo, 'rm', '-q', `${INPUTS}/package-flags.json`);
  commit(gone.repo, 'delete one');
  assert.match((await guard(gone)).reason, /package-flags\.json/);
});

test('An_appended_amendment_is_accepted_and_a_rewritten_one_is_not', async () => {
  const f = await pinned(fixture());
  write(f.repo, `${INPUTS}/amendments.md`, '# Dated amendments\n2026-11-02 | instrument | harness:aaaaaaaaaaaa | a reason\n');
  commit(f.repo, 'append');
  assert.equal((await guard(f)).ok, true, 'appended: the tag\'s content is a prefix');
  write(f.repo, `${INPUTS}/amendments.md`, '# Rewritten header\n');
  commit(f.repo, 'rewrite');
  assert.match((await guard(f)).reason, /amendments\.md/);
});

test('The_seed_file_is_absent_at_the_tag_and_must_hash_to_the_committed_hash', async () => {
  const f = await pinned(fixture());
  assert.equal((await guard(f)).ok, true, 'a dry run without a seed is fine');
  const needed = await guard(f, { requireSeed: true });
  assert.equal(needed.ok, false);
  assert.match(needed.reason, /seed\.txt is required/);
  write(f.repo, `${INPUTS}/seed.txt`, 'mg-ffffffffffffffff\n');
  commit(f.repo, 'reveal the wrong seed');
  const wrong = await guard(f, { requireSeed: true });
  assert.equal(wrong.ok, false);
  assert.match(wrong.reason, /seed\.txt does not hash to seed\.sha256/);
  write(f.repo, `${INPUTS}/seed.txt`, `${SEED}\n`);
  commit(f.repo, 'reveal');
  assert.equal((await guard(f, { requireSeed: true })).ok, true);
});

test('The_crosscheck_policy_may_change_only_in_its_t2_and_t3_decisions', async () => {
  const f = await pinned(fixture());
  const policy = JSON.parse('{"t1":"stop","t2":"otel-incomplete","t3":"investigate","t5":"investigate","join":"request_id","t3OutputSource":"transcript","decidedOn":"2026-10-29","reason":"a"}');
  write(f.repo, `${INPUTS}/crosscheck-policy.json`, `${JSON.stringify({ ...policy, t3: 'stop', decidedOn: '2026-11-03', reason: 'the A/A showed coverage' })}\n`);
  commit(f.repo, 'the dated T3 commit');
  assert.equal((await guard(f)).ok, true);
  write(f.repo, `${INPUTS}/crosscheck-policy.json`, `${JSON.stringify({ ...policy, t1: 'investigate' })}\n`);
  commit(f.repo, 'weaken T1');
  const weakened = await guard(f);
  assert.equal(weakened.ok, false);
  assert.match(weakened.reason, /crosscheck-policy\.json.*t1/);
});

test('A_damaged_crosscheck_policy_is_a_refusal_naming_it_and_never_a_throw', async () => {
  const f = await pinned(fixture());
  write(f.repo, `${INPUTS}/crosscheck-policy.json`, '{not json');
  commit(f.repo, 'a damaged policy');
  const refused = await guard(f);
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /crosscheck-policy\.json at HEAD is not JSON: the policy cannot be compared with the tag/);
});

test('An_untracked_file_under_spikes_refuses_a_paid_run_only', async () => {
  const f = await pinned(fixture());
  write(f.repo, 'spikes/minigit-bench/scratch.txt', 'untracked\n');
  assert.equal((await guard(f, { paid: false })).ok, true);
  const paid = await guard(f, { paid: true });
  assert.equal(paid.ok, false);
  assert.match(paid.reason, /scratch\.txt/);
  write(f.repo, 'docs-elsewhere.txt', 'outside spikes\n');
  git(f.repo, 'clean', '-fdq', '--', 'spikes/');
  assert.equal((await guard(f, { paid: true })).ok, true, 'a file outside spikes/ is not the guard\'s business');
});

test('A_changed_harness_identity_refuses_a_paid_run_unless_accepted', async () => {
  const f = await pinned(fixture());
  write(f.repo, 'spikes/minigit-bench/lib/x.mjs', '// changed\n');
  commit(f.repo, 'a code change');
  const paid = await guard(f, { paid: true });
  assert.equal(paid.ok, false);
  assert.match(paid.reason, /harnessIdentity/);
  assert.equal((await guard(f, { paid: false })).ok, true, 'a dry run does not need the harness to match');
  f.runMap.harnessAccepted = [(await harnessIdentity(f.repo)).value];
  assert.equal((await guard(f, { paid: true })).ok, true, 'an accepted identity (step 11) passes');
});

test('Harness_identity_ignores_inputs_and_follows_code', async () => {
  const f = fixture();
  const before = (await harnessIdentity(f.repo)).value;
  assert.match(before, /^[0-9a-f]{64}$/);
  write(f.repo, `${INPUTS}/amendments.md`, '# Dated amendments\n2026-11-02 | x\n');
  commit(f.repo, 'amendment');
  assert.equal((await harnessIdentity(f.repo)).value, before, 'committing an amendment (or the seed) does not change it');
  write(f.repo, `${INPUTS}/seed.txt`, `${SEED}\n`);
  commit(f.repo, 'seed');
  assert.equal((await harnessIdentity(f.repo)).value, before);
  for (const rel of ['spikes/minigit-bench/lib/x.mjs', 'spikes/minigit-bench/harness/x.mjs', 'spikes/minigit-bench/capture/x.mjs', 'spikes/minigit-bench/prereg/x.mjs', 'spikes/minigit-bench/top.mjs', 'spikes/policy-ab-quick/lib/y.mjs', 'spikes/policy-ab-quick/hooks/y.mjs']) {
    const h = (await harnessIdentity(f.repo)).value;
    write(f.repo, rel, `// changed ${rel}\n`);
    commit(f.repo, `change ${rel}`);
    assert.notEqual((await harnessIdentity(f.repo)).value, h, `${rel} is part of the harness`);
  }
});

test('The_guard_refuses_a_csharp_prompt_that_names_another_sdk_than_the_pin', async () => {
  const same = await pinned(fixture());
  assert.equal((await guard(same)).ok, true, 'control: the prompt names the pinned SDK');
  const f = await pinned(fixture({ sdk: '10.0.113', pin: '10.0.114' }));
  const refused = await guard(f);
  assert.equal(refused.ok, false, 'the prompt says 10.0.113 and stacks.json pins 10.0.114');
  assert.match(refused.reason, /prompts-bare\.json names SDK 10\.0\.113.*10\.0\.114/);
});

test('The_sdk_check_reads_every_sentence_of_the_csharp_prompts_and_ignores_the_node_ones', () => {
  const pin = { pins: { dotnetSdk: '10.0.114' } };
  assert.equal(sdkProblem({ prompts: JSON.parse(promptsText('10.0.114')), stacks: pin }), null);
  assert.match(sdkProblem({ prompts: JSON.parse(promptsText('10.0.113')), stacks: pin }), /names SDK 10\.0\.113 but stacks\.json pins 10\.0\.114/);
  assert.match(sdkProblem({ prompts: JSON.parse(promptsText('10.0.114')), stacks: {} }), /no pins\.dotnetSdk/, 'a missing pin is a refusal, never a pass');
  assert.equal(sdkCheckOfTexts({ promptsText: 'not json', stacksText: stacksText('10.0.114') }).ok, false);
});

test('Every_input_the_run_reads_and_the_plan_calls_frozen_is_a_registered_file', async () => {
  for (const file of ['stacks.json', 'prices-2026-10-29.json', 'hashes.txt']) {
    assert.ok(PREREG_FILES.includes(file), `${file} is registered: the toolchain pins, the price table and the input hashes decide the treatment and the money`);
    const f = await pinned(fixture());
    assert.equal((await guard(f)).ok, true, `control: ${file} as tagged`);
    write(f.repo, `${INPUTS}/${file}`, file === 'stacks.json' ? `${JSON.stringify({ pins: { claude: '9.9.9', dotnetSdk: '10.0.114' } })}\n` : `edited after the tag ${file}\n`);
    commit(f.repo, `edit ${file}`);
    const refused = await guard(f);
    assert.equal(refused.ok, false, `${file} edited after the tag`);
    assert.match(refused.reason, new RegExp(`${file.replace('.', '\\.')} differs from the tagged commit`));
  }
});

// ---- quiet.json and package-flags.json: amendable only through a dated line (review m4, contract section 10) -------------------------------

const hash12 = text => createHash('sha256').update(text).digest('hex').slice(0, 12);
const AMENDED_QUIET = '{"windowMs":60000,"note":"a sampler fix after the A/A"}\n';

test('The_quiet_thresholds_and_the_package_flags_change_only_through_a_dated_amendments_line_naming_the_new_content', async () => {
  for (const file of ['quiet.json', 'package-flags.json']) {
    const f = await pinned(fixture());
    write(f.repo, `${INPUTS}/${file}`, AMENDED_QUIET);
    commit(f.repo, `edit ${file}`);
    const bare = await guard(f);
    assert.equal(bare.ok, false, `${file} edited with no line`);
    assert.match(bare.reason, new RegExp(`${file.replace('.', '\\.')} differs from the tagged commit.*input:${file.replace('.', '\\.')}:${hash12(AMENDED_QUIET)}`), 'the refusal names the line that would accept it');
    write(f.repo, `${INPUTS}/amendments.md`, `# Dated amendments\n2026-11-02 | instrument | input:${file}:${hash12(AMENDED_QUIET)} | the spin window was too short (G-11)\n`);
    commit(f.repo, 'the dated line');
    assert.equal((await guard(f)).ok, true, `${file}: the dated line naming this content accepts it`);
    write(f.repo, `${INPUTS}/${file}`, '{"windowMs":1}\n');
    commit(f.repo, 'edit again after the line');
    assert.equal((await guard(f)).ok, false, `${file}: a line names ONE content; another edit needs another line`);
  }
  const other = await pinned(fixture());
  write(other.repo, `${INPUTS}/quiet.json`, AMENDED_QUIET);
  write(other.repo, `${INPUTS}/amendments.md`, `# Dated amendments\n2026-11-02 | instrument | input:package-flags.json:${hash12(AMENDED_QUIET)} | wrong file\n`);
  commit(other.repo, 'a line for the other file');
  assert.equal((await guard(other)).ok, false, 'a line names its file');
  const frozen = await pinned(fixture());
  write(frozen.repo, `${INPUTS}/statement.md`, 'rewritten\n');
  write(frozen.repo, `${INPUTS}/amendments.md`, `# Dated amendments\n2026-11-02 | instrument | input:statement.md:${hash12('rewritten\n')} | no\n`);
  commit(frozen.repo, 'try to amend the statement');
  assert.equal((await guard(frozen)).ok, false, 'only the instrument inputs are amendable: the statement, the prompts, the prices and the pins are frozen');
});

// ---- the configuration: the lists the guard works over are an argument (language comparison v2's Stage 1 passes its own) --------------------------------------------

/** A repository shaped nothing like v1's: registered files in two folders, a folder entry, a structured exception, two harness folders and a top-level folder. */
function otherFixture() {
  const f = fixture();
  for (const [rel, text] of Object.entries({ 'docs/rules.md': 'rules\n', 'docs/limits.json': '{"cap":50,"wall":75}\n', 'data/one.json': '{"a":1}\n', 'data/two.json': '{"b":2}\n', 'ledger/amend.md': '# lines\n', 'ledger/seed.hash': `${sha256Of(SEED)}\n`, 'engine/lib/e.mjs': '// e\n', 'tools/t.mjs': '// t\n', 'tools/more/skip.mjs': '// skipped\n', 'pre/prices-2026-11-19.json': '{"p":1}\n' })) write(f.repo, rel, text);
  const sha = commit(f.repo, 'the other shape');
  git(f.repo, 'tag', '-a', 'prereg/other', '-m', 'other', sha);
  git(f.repo, 'push', '-q', 'origin', 'main', 'prereg/other');
  return { ...f, sha, runMap: { preregTag: 'prereg/other', preregCommit: sha, harnessIdentity: null, harnessAccepted: [] } };
}
const sha256Of = text => createHash('sha256').update(text).digest('hex');
const OTHER = Object.freeze({
  ...V1_PREREG_CONFIG,
  labelBase: 'nothing/',
  files: ['docs/rules.md', 'docs/limits.json', { dir: 'data', pattern: /\.json$/, what: 'the data files' }, { dir: 'pre', pattern: /^prices-\d{4}-\d{2}-\d{2}\.json$/, what: 'the price file', exactlyOne: true }],
  amendments: 'ledger/amend.md', seedFile: 'ledger/seed.txt', seedHash: 'ledger/seed.hash', amendable: {}, policy: null,
  structured: { 'docs/limits.json': async ({ repo }) => ((await git(repo, 'show', 'HEAD:docs/limits.json')).includes('"cap":50') ? null : 'the cap changed') },
  harnessDirs: ['engine/lib'], harnessTopDirs: ['tools/'], consistency: async () => [],
});
const otherGuard = (f, over = {}) => preregGuard({ repo: f.repo, runMap: f.runMap, paid: false, requireSeed: false, config: OTHER, ...over });

test('v1s_guard_over_v1s_lists_answers_as_before', async () => {
  assert.deepEqual(V1_PREREG_CONFIG.files, PREREG_FILES.map(name => `${INPUTS}/${name}`), 'the default configuration lists exactly v1\'s registered files');
  const f = await pinned(fixture());
  write(f.repo, `${INPUTS}/quiet.json`, 'changed\n');
  write(f.repo, `${INPUTS}/amendments.md`, '# rewritten\n');
  commit(f.repo, 'two problems');
  const [byDefault, byConfig] = [await guard(f), await guard(f, { config: V1_PREREG_CONFIG })];
  assert.equal(byDefault.reason, byConfig.reason, 'the same sentence with the configuration passed and without');
  const line = `quiet.json differs from the tagged commit and no dated amendments.md line names it: add \`YYYY-MM-DD | instrument | input:quiet.json:${sha256Of('changed\n').slice(0, 12)} | <reason, with its G-id>\``;
  assert.equal(byDefault.reason, `the pre-registration guard refuses: ${line}; amendments.md at HEAD is not the tagged text with lines appended (a registered amendment file is append-only)`, 'v1\'s sentences, word for word: the file names are cut at labelBase');
  assert.equal((await harnessIdentity(f.repo)).value, (await harnessIdentity(f.repo, V1_PREREG_CONFIG)).value);
});

test('The_guard_answers_over_a_configuration_of_other_files_folders_and_rules', async () => {
  const f = otherFixture();
  f.runMap.harnessIdentity = (await harnessIdentity(f.repo, OTHER)).value;
  assert.equal((await otherGuard(f)).ok, true, (await otherGuard(f)).reason);
  write(f.repo, 'docs/rules.md', 'changed\n');
  commit(f.repo, 'edit a registered file outside v1\'s folder');
  assert.match((await otherGuard(f)).reason, /docs\/rules\.md differs from the tagged commit/, 'a path that is not under labelBase is named whole');
  git(f.repo, 'checkout', '-q', 'HEAD~1', '--', 'docs/rules.md');
  write(f.repo, 'data/three.json', '{"c":3}\n');
  commit(f.repo, 'a file added to a registered folder after the tag');
  assert.match((await otherGuard(f)).reason, /data\/three\.json is missing at the tagged commit/, 'a folder entry covers the files HEAD holds too');
  git(f.repo, 'rm', '-q', 'data/three.json');
  write(f.repo, 'pre/prices-2026-11-20.json', '{"p":2}\n');
  commit(f.repo, 'a second price file');
  assert.match((await otherGuard(f)).reason, /pre\/prices-2026-11-20\.json is missing at the tagged commit/);
});

test('A_structured_exception_lets_one_registered_file_differ_the_way_it_says_and_no_other_way', async () => {
  const f = otherFixture();
  write(f.repo, 'docs/limits.json', '{"cap":50,"wall":90}\n');
  commit(f.repo, 'the wall moved');
  assert.equal((await otherGuard(f)).ok, true, (await otherGuard(f)).reason);
  write(f.repo, 'docs/limits.json', '{"cap":60,"wall":90}\n');
  commit(f.repo, 'the cap moved');
  assert.match((await otherGuard(f)).reason, /the cap changed/);
});

test('The_configuration_decides_the_paid_checks_the_harness_folders_the_top_level_files_and_the_branch', async () => {
  const f = otherFixture();
  const before = (await harnessIdentity(f.repo, OTHER)).value;
  f.runMap.harnessIdentity = before;
  write(f.repo, 'tools/more/skip.mjs', '// not a top-level file of tools/\n');
  commit(f.repo, 'a file below the top level');
  assert.equal((await harnessIdentity(f.repo, OTHER)).value, before, 'only the top-level *.mjs of a top folder count');
  for (const rel of ['engine/lib/e.mjs', 'tools/t.mjs']) {
    const last = (await harnessIdentity(f.repo, OTHER)).value;
    write(f.repo, rel, `// changed ${rel}\n`);
    commit(f.repo, `change ${rel}`);
    assert.notEqual((await harnessIdentity(f.repo, OTHER)).value, last, `${rel} is in this configuration's harness`);
  }
  assert.match((await otherGuard(f, { paid: true })).reason, /harnessIdentity/, 'the paid check reads this configuration\'s identity');
  const branch = { ...OTHER, branch: async () => ['the branch is not the tag\'s'] };
  assert.equal((await otherGuard(f, { config: branch, paid: false })).ok, true, 'the branch check is for a paid launch only');
  f.runMap.harnessAccepted = [(await harnessIdentity(f.repo, OTHER)).value];
  assert.match((await otherGuard(f, { config: branch, paid: true })).reason, /the branch is not the tag's/);
});

test('A_history_that_was_cut_at_the_tag_gives_the_identity_of_that_commit_when_a_commit_is_named', async () => {
  const f = otherFixture();
  const atTag = (await harnessIdentity(f.repo, OTHER)).value;
  write(f.repo, 'engine/lib/e.mjs', '// later\n');
  const later = commit(f.repo, 'later');
  assert.equal((await harnessIdentity(f.repo, OTHER, f.sha)).value, atTag, 'the identity at the tag\'s commit is the one it had then');
  assert.notEqual((await harnessIdentity(f.repo, OTHER, later)).value, atTag);
});

test('A_folder_entry_that_must_hold_exactly_one_file_at_the_tag_is_refused_when_the_tag_holds_two', async () => {
  const f = fixture();
  write(f.repo, 'pre/prices-2026-11-19.json', '{"p":1}\n');
  write(f.repo, 'pre/prices-2026-11-20.json', '{"p":2}\n');
  const sha = commit(f.repo, 'two price files at the tag');
  git(f.repo, 'tag', '-a', 'prereg/two', '-m', 'two', sha);
  git(f.repo, 'push', '-q', 'origin', 'main', 'prereg/two');
  const config = { ...V1_PREREG_CONFIG, files: [{ dir: 'pre', pattern: /^prices-/, what: 'the price file', exactlyOne: true }] };
  const refused = await preregGuard({ repo: f.repo, runMap: { preregTag: 'prereg/two', preregCommit: sha }, paid: false, config });
  assert.match(refused.reason, /the price file must be exactly one file of pre\/ at the tagged commit \(found 2: pre\/prices-2026-11-19\.json, pre\/prices-2026-11-20\.json\)/);
});
