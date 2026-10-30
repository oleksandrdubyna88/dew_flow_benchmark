// The pre-registration guard (E3b.S1 step 10): no paid launch unless the tagged pre-registration is the one on origin, every registered file is as tagged,
// the tree is clean and the harness is the one the run-map pinned. Over a real temporary repository with a real bare remote.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { PREREG_FILES, harnessIdentity, preregGuard } from '../lib/prereg-guard.mjs';
import { sdkCheckOfTexts, sdkProblem } from '../lib/prereg-consistency.mjs';
import { tmpDir } from './helpers.mjs';
import { promptsText, stacksText } from './prereg-fixtures.mjs';

const TAG = 'prereg/minigit-bare-20261030';
const INPUTS = 'spikes/minigit-bench/inputs';
const sha256 = text => createHash('sha256').update(text).digest('hex');
const SEED = 'mg-0123456789abcdef';
function git(cwd, ...args) {
  const r = spawnSync('git', args, { cwd, encoding: 'utf8', env: { ...process.env, GIT_AUTHOR_NAME: 't', GIT_AUTHOR_EMAIL: 't@t', GIT_COMMITTER_NAME: 't', GIT_COMMITTER_EMAIL: 't@t' } });
  assert.equal(r.status, 0, `git ${args.join(' ')}: ${r.stderr}`);
  return r.stdout.trim();
}
const write = (repo, rel, text) => { mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true }); writeFileSync(path.join(repo, rel), text); };
const commit = (repo, msg) => { git(repo, 'add', '-A'); git(repo, 'commit', '-q', '-m', msg); return git(repo, 'rev-parse', 'HEAD'); };

/** A repo with every registered file, harness code, one commit, an annotated tag on it and a bare origin holding both. */
function fixture({ push = true, sdk = '10.0.114', pin = sdk } = {}) {
  const remote = tmpDir('remote');
  git(remote, 'init', '-q', '--bare');
  const repo = tmpDir('repo');
  git(repo, 'init', '-q', '-b', 'main');
  git(repo, 'remote', 'add', 'origin', remote);
  for (const f of new Set([...PREREG_FILES, 'stacks.json'])) write(repo, `${INPUTS}/${f}`, f === 'seed.sha256' ? `${sha256(SEED)}\n` : f === 'crosscheck-policy.json' ? '{"t1":"stop","t2":"otel-incomplete","t3":"investigate","t5":"investigate","join":"request_id","t3OutputSource":"transcript","decidedOn":"2026-10-29","reason":"a"}\n' : f === 'amendments.md' ? '# Dated amendments\n' : f === 'stacks.json' ? stacksText(pin) : f === 'prompts-bare.json' ? promptsText(sdk) : `registered ${f}\n`);
  for (const d of ['lib', 'harness', 'capture', 'prereg']) write(repo, `spikes/minigit-bench/${d}/x.mjs`, `// ${d}\n`);
  write(repo, 'spikes/minigit-bench/top.mjs', '// top\n');
  for (const d of ['lib', 'hooks']) write(repo, `spikes/policy-ab-quick/${d}/y.mjs`, `// ${d}\n`);
  const sha = commit(repo, 'prereg');
  git(repo, 'tag', '-a', TAG, '-m', 'prereg', sha);
  if (push) { git(repo, 'push', '-q', 'origin', 'main', TAG); }
  return { repo, remote, sha, runMap: { preregTag: TAG, preregCommit: sha, harnessIdentity: null, harnessAccepted: [] } };
}
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
