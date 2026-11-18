// F2.S2 (b): the task repository's two rulesets as data (`main`'s protection and the tag ruleset, which is two GitHub rulesets), the pure compare of a read-back against them, the reads, and the
// apply that never overwrites. Every test uses the fake GitHub of test/support/github-fixtures.mjs (the `runBounded` seam); no test makes a write to GitHub and none touches the network.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ADMIN_ROLE_ACTOR_ID, MERGE_SETTINGS, PACKAGE_TASKS, RULESETS, TASK_REPO, applyGateProblems, applyRulesets, deepFrozenCopy, mergeSettingsProblems, readMergeSettings, readRulesets, rulesetProblems, rulesetsReadBack, scrub } from '../lib/github-lang.mjs';
import { ALL_CALLS, asGitHub, brokenRunner, collector, fakeGitHub } from './support/github-fixtures.mjs';

const byName = name => RULESETS.find(ruleset => ruleset.name === name);
const MAIN = 'template-frozen';
const TAGS = 'spec-frozen-tags';
const CREATE = 'spec-frozen-tags-create';
const typesOf = ruleset => ruleset.rules.map(rule => rule.type);
/** The data with one change, as a read-back would show it. */
const changed = (name, build) => build(asGitHub(byName(name), 7));
/** `object` without `key`, as a new object. */
const omit = (object, key) => Object.fromEntries(Object.entries(object).filter(([name]) => name !== key));
/** `base` with its ref_name `which` list replaced: a new object. */
const refsOf = (base, which, list) => ({ ...base, conditions: { ...base.conditions, ref_name: { ...base.conditions.ref_name, [which]: list } } });
/** `base` with the parameters of its pull_request rule changed by `change(parameters)`: a new object. */
const withPullRequest = (base, change) => ({ ...base, rules: base.rules.map(rule => (rule.type === 'pull_request' ? { ...rule, parameters: change(rule.parameters) } : rule)) });
/** A read-back of `name` with `patch(base)` laid over it: a NEW object, nothing mutated. */
const derived = (name, patch) => { const base = asGitHub(byName(name), 7); return { ...base, ...patch(base) }; };
const readBackOf = () => RULESETS.map((ruleset, index) => asGitHub(ruleset, 100 + index));
const PASSED = { identity: 'b'.repeat(64), archiveSha256: 'a'.repeat(64), referenceVerdicts: { csharp: 'accepted', node: 'accepted' } }; // the shape of a registered, sealed, validated package in packages.json
const SHA = 'c'.repeat(64); // the sha256 of packages.json's bytes, as the command computes it
const LINE = `dry run clean 2026-11-20 packages ${SHA}`;
const GO = { packages: Object.fromEntries(['04', '05', '06', '07', '08', '10'].map(id => [id, PASSED])), packagesSha256: SHA, recordFirstLine: LINE }; // the owner's go of 2026-11-08, as applyRulesets takes it
const SECRET = 'ghp_' + 'A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6Q7r8';

// ---- the data ------------------------------------------------------------------------------------------------------------------------

test('Main_restricts_no_update_and_requires_linear_history', () => {
  const main = byName(MAIN);
  assert.equal(main.target, 'branch');
  assert.equal(main.enforcement, 'active');
  assert.deepEqual(main.conditions.ref_name, { include: ['refs/heads/main'], exclude: [] }, 'exactly this ref, never ~DEFAULT_BRANCH');
  assert.deepEqual(main.bypass_actors, []);
  assert.deepEqual(typesOf(main), ['pull_request', 'required_linear_history', 'non_fast_forward', 'deletion']);
  assert.ok(!typesOf(main).includes('update'), 'an update rule would block every spec PR before the freeze and every dated amendment after it');
  assert.deepEqual(main.rules[0].parameters, { required_approving_review_count: 0, dismiss_stale_reviews_on_push: false, require_code_owner_review: false, require_last_push_approval: false, required_review_thread_resolution: true });
  assert.ok(!('allowed_merge_methods' in main.rules[0].parameters), 'the API takes the parameter as optional, so it is left out and the repository merge settings decide');
});

test('Main_has_no_required_status_checks_in_stage_1', () => {
  for (const ruleset of RULESETS) assert.ok(!typesOf(ruleset).includes('required_status_checks'), `${ruleset.name}: the checks join with CI in Stage 2`);
});

test('The_tag_ruleset_has_no_bypass_actor_and_restricts_update_deletion_and_force_push', () => {
  const tags = byName(TAGS);
  assert.equal(tags.target, 'tag');
  assert.equal(tags.enforcement, 'active');
  assert.deepEqual(tags.conditions.ref_name, { include: ['refs/tags/spec-frozen-*'], exclude: [] });
  assert.deepEqual(typesOf(tags), ['update', 'deletion', 'non_fast_forward']);
  assert.deepEqual(tags.bypass_actors, [], 'a bypass applies to all the rules of its ruleset, so this one has none');
  assert.ok(!typesOf(tags).includes('creation'), 'creation is the other ruleset: one ruleset cannot restrict updates for everyone and let the admin create');
});

test('Only_the_creation_ruleset_has_a_bypass_and_it_is_the_admin_role', () => {
  const create = byName(CREATE);
  assert.equal(create.target, 'tag');
  assert.equal(create.enforcement, 'active');
  assert.deepEqual(create.conditions, byName(TAGS).conditions, 'the same refs as the restricting ruleset');
  assert.deepEqual(typesOf(create), ['creation']);
  assert.deepEqual(create.bypass_actors, [{ actor_id: 5, actor_type: 'RepositoryRole', bypass_mode: 'always' }]);
  assert.equal(ADMIN_ROLE_ACTOR_ID, 5, 'the repository admin role, per the integrations provider docs: GitHub\'s own REST text gives no mapping');
  assert.deepEqual(RULESETS.filter(ruleset => ruleset.bypass_actors.length > 0).map(ruleset => ruleset.name), [CREATE]);
});

// Three GitHub rulesets, but only two approvals: the owner approved `main`'s protection (`template-frozen`) and the tag ruleset. GitHub cannot restrict updates for everyone and let the
// admin create in one ruleset (a bypass applies to every rule of its ruleset), so the tag ruleset is expressed as two GitHub rulesets: `spec-frozen-tags` and `spec-frozen-tags-create`.
test('The_data_is_three_frozen_rulesets_with_distinct_names_and_the_task_repository_is_fixed', () => {
  assert.deepEqual(RULESETS.map(ruleset => ruleset.name), [MAIN, TAGS, CREATE]);
  assert.ok(Object.isFrozen(RULESETS) && Object.isFrozen(RULESETS[0]) && Object.isFrozen(RULESETS[0].rules[0].parameters) && Object.isFrozen(RULESETS[2].bypass_actors[0]));
  assert.equal(TASK_REPO, 'oleksandrdubyna88/dew_flow_ai_language_comparison');
  assert.deepEqual(MERGE_SETTINGS, { allow_merge_commit: false, allow_squash_merge: true });
});

// ---- the pure compare ----------------------------------------------------------------------------------------------------------------

test('A_read_back_equal_to_the_data_passes_and_what_GitHub_adds_is_ignored', () => {
  for (const ruleset of RULESETS) assert.deepEqual(rulesetProblems(asGitHub(ruleset, 5), ruleset), [], ruleset.name);
  const extra = changed(MAIN, base => ({ ...withPullRequest({ ...base, rules: base.rules.map(rule => (rule.type === 'required_linear_history' ? { ...rule, parameters: { anything: 1 } } : rule)) }, parameters => ({ ...parameters, allowed_merge_methods: ['squash', 'rebase'] })), conditions: { ...base.conditions, repository_name: undefined } }));
  assert.deepEqual(rulesetProblems(extra, byName(MAIN)), [], 'a parameter or a field the data does not state is not compared');
});

const KINDS = [
  ['A_missing_rule_is_named', MAIN, base => ({ ...base, rules: base.rules.filter(rule => rule.type !== 'deletion') }), 'missing rule: deletion'],
  ['An_extra_rule_is_named', MAIN, base => ({ ...base, rules: [...base.rules, { type: 'update' }] }), 'extra rule: update'],
  ['An_extra_bypass_actor_is_named', TAGS, base => ({ ...base, bypass_actors: [...base.bypass_actors, { actor_id: 5, actor_type: 'RepositoryRole', bypass_mode: 'always' }] }), 'extra bypass actor: RepositoryRole 5 always'],
  ['A_missing_bypass_actor_is_named', CREATE, base => ({ ...base, bypass_actors: [] }), 'missing bypass actor: RepositoryRole 5 always'],
  ['A_changed_bypass_mode_is_named', CREATE, base => ({ ...base, bypass_actors: base.bypass_actors.map(actor => ({ ...actor, bypass_mode: 'pull_request' })) }), 'extra bypass actor: RepositoryRole 5 pull_request'],
  ['A_changed_ref_is_named', MAIN, base => refsOf(base, 'include', ['~DEFAULT_BRANCH']), 'extra include ref: ~DEFAULT_BRANCH'],
  ['A_missing_ref_is_named_beside_the_changed_one', MAIN, base => refsOf(base, 'include', ['refs/heads/other']), 'missing include ref: refs/heads/main'],
  ['An_excluded_ref_is_named', TAGS, base => refsOf(base, 'exclude', ['refs/tags/spec-frozen-x']), 'extra exclude ref: refs/tags/spec-frozen-x'],
  ['Enforcement_that_is_not_active_is_named', MAIN, base => ({ ...base, enforcement: 'evaluate' }), 'enforcement is "evaluate", not "active"'],
  ['A_disabled_ruleset_is_named', TAGS, base => ({ ...base, enforcement: 'disabled' }), 'enforcement is "disabled", not "active"'],
  ['A_changed_target_is_named', TAGS, base => ({ ...base, target: 'branch' }), 'target is "branch", not "tag"'],
  ['A_changed_parameter_is_named', MAIN, base => withPullRequest(base, parameters => ({ ...parameters, required_review_thread_resolution: false })), 'rule pull_request parameter required_review_thread_resolution is false, not true'],
  ['An_absent_parameter_is_named', MAIN, base => withPullRequest(base, parameters => omit(parameters, 'require_last_push_approval')), 'rule pull_request parameter require_last_push_approval is absent, not false'],
  ['A_read_back_without_its_bypass_actors_is_a_problem_never_no_bypass_actors', MAIN, base => omit(base, 'bypass_actors'), 'bypass actors were not returned: the reader may lack write access to the ruleset'],
  ['A_read_back_whose_bypass_actors_are_not_a_list_is_the_same_problem', CREATE, base => ({ ...base, bypass_actors: null }), 'bypass actors were not returned: the reader may lack write access to the ruleset'],
  ['A_doubled_rule_is_named_once_as_extra', TAGS, base => ({ ...base, rules: [...base.rules, { type: 'deletion' }] }), 'extra rule: deletion'],
];
for (const [name, ruleset, change, expected] of KINDS) {
  test(name, () => assert.deepEqual(rulesetProblems(changed(ruleset, change), byName(ruleset)), [expected]));
}

test('The_check_still_finds_a_known_difference', () => {
  const read = changed(MAIN, base => ({ ...base, rules: base.rules.filter(rule => rule.type !== 'required_linear_history'), enforcement: 'disabled', bypass_actors: [{ actor_id: 1, actor_type: 'OrganizationAdmin', bypass_mode: 'always' }] }));
  assert.deepEqual(rulesetProblems(read, byName(MAIN)), ['enforcement is "disabled", not "active"', 'missing rule: required_linear_history', 'extra bypass actor: OrganizationAdmin 1 always']);
  assert.deepEqual(rulesetProblems(null, byName(TAGS)).length > 0, true, 'nothing read is not equal');
});

// ---- the reads -----------------------------------------------------------------------------------------------------------------------

test('The_rulesets_are_listed_then_each_is_read_by_id_through_gh_with_an_argv_and_no_shell', async () => {
  const github = fakeGitHub({ rulesets: readBackOf() });
  const read = await readRulesets(TASK_REPO, github.run);
  assert.ok(read.ok);
  assert.deepEqual(read.value.map(ruleset => ruleset.name), [MAIN, TAGS, CREATE]);
  assert.deepEqual(github.calls.map(call => call.args.filter(arg => arg.startsWith('repos/'))[0]), [`repos/${TASK_REPO}/rulesets?per_page=100&includes_parents=false`, `repos/${TASK_REPO}/rulesets/100`, `repos/${TASK_REPO}/rulesets/101`, `repos/${TASK_REPO}/rulesets/102`]);
  assert.ok(github.calls.every(call => call.exe === 'gh' && call.args[0] === 'api' && call.method === 'GET' && Number.isFinite(call.timeoutMs)));
  assert.deepEqual(github.writes(), []);
});

for (const kind of ['launch', 'timeout', 'exit', 'truncated', 'garbage']) {
  test(`A_gh_that_fails_is_a_failure_to_read_never_an_empty_list_${kind}`, async () => {
    const read = await readRulesets(TASK_REPO, brokenRunner(kind).run);
    assert.equal(read.ok, false);
    assert.equal(read.kind, 'env');
    const back = await rulesetsReadBack({ repo: TASK_REPO, run: brokenRunner(kind).run });
    assert.equal(back.ok, false, 'a read-back that could not read is not an equal one');
  });
}

test('A_list_that_may_have_more_pages_or_is_not_a_list_is_refused_rather_than_trusted', async () => {
  const many = fakeGitHub({}, { reply: call => (call.args.some(arg => arg.includes('per_page')) ? { ok: true, value: { code: 0, stdout: JSON.stringify(Array.from({ length: 100 }, (_, id) => ({ id: id + 1 }))), stderr: '', timedOut: false, truncated: false } } : undefined) });
  assert.equal((await readRulesets(TASK_REPO, many.run)).ok, false);
  const odd = fakeGitHub({}, { reply: () => ({ ok: true, value: { code: 0, stdout: '{"message":"x"}', stderr: '', timedOut: false, truncated: false } }) });
  assert.equal((await readRulesets(TASK_REPO, odd.run)).ok, false);
});

test('A_repository_name_that_is_not_owner_slash_name_is_refused_before_any_call', async () => {
  const github = fakeGitHub();
  for (const repo of ['', 'x', 'a/b/c', '../etc', 'o/r;rm', '-X/POST', 'o/ r']) assert.equal((await readRulesets(repo, github.run)).kind, 'config', repo);
  assert.deepEqual(github.calls, []);
});

// ---- apply ---------------------------------------------------------------------------------------------------------------------------

test('An_absent_ruleset_is_created_and_read_back', async () => {
  const github = fakeGitHub();
  const out = collector();
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: out.say, ...GO });
  assert.ok(applied.ok, applied.reason);
  assert.deepEqual(applied.value.outcomes, [{ name: MAIN, outcome: 'created' }, { name: TAGS, outcome: 'created' }, { name: CREATE, outcome: 'created' }]);
  const posts = github.writes();
  assert.deepEqual(posts.map(call => [call.exe, call.args.slice(0, 4), call.args.at(-2), call.args.at(-1)]), RULESETS.map(() => ['gh', ['api', '--method', 'POST', '-H'], '--input', '-']));
  assert.deepEqual(posts.map(call => JSON.parse(call.input)), RULESETS, 'the body is the data, nothing else');
  const order = github.calls.map(call => call.method);
  assert.deepEqual(order.slice(0, 1), ['GET'], 'the read BEFORE comes first');
  assert.deepEqual(order.slice(-4), ['GET', 'GET', 'GET', 'GET'], 'the read AFTER comes last: the list and the three');
  assert.ok(order.indexOf('POST') > 0 && order.lastIndexOf('POST') < order.length - 4);
  assert.equal(github.state.rulesets.length, 3);
  assert.ok(out.lines.some(line => line.includes(`created ${MAIN}`)), out.lines.join('|'));
});

test('A_present_equal_ruleset_is_left_alone_with_no_write', async () => {
  const github = fakeGitHub({ rulesets: readBackOf() });
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.ok(applied.ok, applied.reason);
  assert.deepEqual(applied.value.outcomes.map(entry => entry.outcome), ['unchanged', 'unchanged', 'unchanged']);
  assert.deepEqual(github.writes(), []);
});

test('Only_the_absent_one_is_created_when_the_others_are_present_and_equal', async () => {
  const github = fakeGitHub({ rulesets: [asGitHub(byName(MAIN), 11), asGitHub(byName(TAGS), 12)] });
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.ok(applied.ok, applied.reason);
  assert.deepEqual(applied.value.outcomes.map(entry => entry.outcome), ['unchanged', 'unchanged', 'created']);
  assert.deepEqual(github.writes().map(call => JSON.parse(call.input).name), [CREATE]);
});

test('A_present_different_ruleset_is_refused_and_never_written', async () => {
  const different = derived(TAGS, base => ({ bypass_actors: [...base.bypass_actors, { actor_id: 5, actor_type: 'RepositoryRole', bypass_mode: 'always' }] }));
  const github = fakeGitHub({ rulesets: [different] });
  const out = collector();
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: out.say, ...GO });
  assert.equal(applied.ok, false);
  assert.equal(applied.kind, 'refused');
  assert.match(applied.reason, /spec-frozen-tags/);
  assert.match(applied.reason, /extra bypass actor: RepositoryRole 5 always/);
  assert.deepEqual(github.writes(), [], 'not even the absent rulesets are created while one is refused: updating needs its own go');
  assert.equal(github.calls.some(call => call.method === 'PUT' || call.method === 'PATCH' || call.method === 'DELETE'), false);
});

test('Two_rulesets_of_one_name_are_refused_not_chosen_between', async () => {
  const github = fakeGitHub({ rulesets: [asGitHub(byName(MAIN), 1), asGitHub(byName(MAIN), 2)] });
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.equal(applied.kind, 'refused');
  assert.deepEqual(github.writes(), []);
});

test('A_read_back_that_differs_after_the_write_is_a_failure', async () => {
  const github = fakeGitHub({}, { onCreate: (body, id) => (body.name === CREATE ? { ...body, enforcement: 'evaluate' } : body) });
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.equal(applied.ok, false);
  assert.equal(applied.kind, 'differs');
  assert.match(applied.reason, /spec-frozen-tags-create/);
  assert.match(applied.reason, /enforcement is "evaluate", not "active"/);
  const dropped = fakeGitHub({}, { onCreate: (body, id) => (body.name === TAGS ? { ...body, rules: [] } : body) });
  const droppedApply = await applyRulesets({ repo: TASK_REPO, run: dropped.run, say: collector().say, ...GO });
  assert.equal(droppedApply.kind, 'differs');
  assert.match(droppedApply.reason, /missing rule: update/);
});

test('A_ruleset_that_is_absent_after_the_write_is_a_failure', async () => {
  const github = fakeGitHub({}, { reply: call => (call.method === 'POST' ? { ok: true, value: { code: 0, stdout: '{"id":5,"name":"x"}', stderr: '', timedOut: false, truncated: false } } : undefined) });
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.equal(applied.kind, 'differs');
  assert.match(applied.reason, /absent/);
});

test('A_failed_write_stops_the_apply_and_says_what_was_already_made', async () => {
  let posts = 0;
  const github = fakeGitHub({}, { reply: call => (call.method === 'POST' && ++posts === 2 ? { ok: true, value: { code: 1, stdout: '', stderr: 'gh: Validation Failed (HTTP 422)', timedOut: false, truncated: false } } : undefined) });
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.equal(applied.ok, false);
  assert.equal(applied.kind, 'env');
  assert.match(applied.reason, /template-frozen/, 'the one already created is named');
  assert.match(applied.reason, /--check/);
  assert.equal(github.writes().length, 2, 'no third write after the failure');
});

for (const kind of ['launch', 'timeout', 'exit', 'garbage']) {
  test(`An_apply_that_cannot_read_before_writes_nothing_${kind}`, async () => {
    const broken = brokenRunner(kind);
    const applied = await applyRulesets({ repo: TASK_REPO, run: broken.run, say: collector().say, ...GO });
    assert.equal(applied.ok, false);
    assert.equal(applied.kind, 'env');
    assert.deepEqual(broken.calls.filter(call => call.method !== 'GET'), []);
  });
}

test('A_ruleset_not_in_the_data_that_targets_the_same_refs_is_named_and_left_alone', async () => {
  const stranger = (name, target, include) => ({ name, target, enforcement: 'active', conditions: { ref_name: { include, exclude: [] } }, rules: [{ type: 'creation' }], bypass_actors: [] });
  const github = fakeGitHub({ rulesets: [asGitHub(stranger('all-tags', 'tag', ['refs/tags/*']), 31), asGitHub(stranger('default', 'branch', ['~DEFAULT_BRANCH']), 32), asGitHub(stranger('elsewhere', 'branch', ['refs/heads/release/*']), 33), asGitHub(stranger('other-tags', 'tag', ['refs/tags/v*']), 34)] });
  const out = collector();
  const back = await rulesetsReadBack({ repo: TASK_REPO, run: github.run });
  assert.deepEqual(back.value.strangers.map(entry => entry.name), ['all-tags', 'default']);
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: out.say, ...GO });
  assert.ok(applied.ok, applied.reason);
  assert.ok(out.lines.some(line => line.includes('"all-tags"') && line.includes('not in the data')), out.lines.join('|'));
  assert.ok(!out.lines.some(line => line.includes('"elsewhere"') || line.includes('"other-tags"')));
  assert.deepEqual(github.writes().map(call => JSON.parse(call.input).name), RULESETS.map(ruleset => ruleset.name), 'a stranger is named, never touched, and does not stop the apply');
});

test('The_read_back_says_equal_absent_or_different_for_each_ruleset', async () => {
  const github = fakeGitHub({ rulesets: [asGitHub(byName(MAIN), 1), derived(TAGS, base => ({ rules: base.rules.slice(0, -1) }))] });
  const back = await rulesetsReadBack({ repo: TASK_REPO, run: github.run });
  assert.ok(back.ok);
  assert.equal(back.value.equal, false);
  assert.deepEqual(back.value.states.map(state => [state.name, state.status]), [[MAIN, 'equal'], [TAGS, 'different'], [CREATE, 'absent']]);
  assert.deepEqual(back.value.states[1].problems, ['missing rule: non_fast_forward']);
  assert.deepEqual(github.writes(), []);
  const equal = await rulesetsReadBack({ repo: TASK_REPO, run: fakeGitHub({ rulesets: readBackOf() }).run });
  assert.equal(equal.value.equal, true);
});

// ---- merge settings (report only) ----------------------------------------------------------------------------------------------------

test('The_merge_settings_are_compared_and_only_reported', async () => {
  assert.deepEqual(mergeSettingsProblems({ allow_merge_commit: false, allow_squash_merge: true, allow_rebase_merge: true }), []);
  assert.deepEqual(mergeSettingsProblems({ allow_merge_commit: true, allow_squash_merge: false }), ['merge setting allow_merge_commit is true, not false', 'merge setting allow_squash_merge is false, not true']);
  assert.deepEqual(mergeSettingsProblems({}), ['merge setting allow_merge_commit is absent, not false', 'merge setting allow_squash_merge is absent, not true']);
  const github = fakeGitHub({ repository: { allow_merge_commit: true, allow_squash_merge: true } });
  const read = await readMergeSettings(TASK_REPO, github.run);
  assert.deepEqual(read.value, ['merge setting allow_merge_commit is true, not false']);
  assert.deepEqual(github.writes(), []);
  assert.equal((await readMergeSettings(TASK_REPO, brokenRunner('exit').run)).ok, false);
});

// ---- the prerequisites of the apply (the owner's go of 2026-11-08) -----------------------------------------------------------------

const packagesOf = tasks => Object.fromEntries(tasks.map(id => [id, PASSED]));
const gateOf = (extra = {}) => ({ packages: packagesOf(PACKAGE_TASKS), packagesSha256: SHA, recordFirstLine: LINE, ...extra });

test('The_gate_wants_all_six_tasks_and_a_first_line_that_says_the_dry_run_is_clean', () => {
  assert.deepEqual(PACKAGE_TASKS, ['04', '05', '06', '07', '08', '10']);
  assert.deepEqual(applyGateProblems(gateOf()), []);
  assert.deepEqual(applyGateProblems(gateOf({ packages: { ...packagesOf(PACKAGE_TASKS), '99': {} }, recordFirstLine: `dry run clean 2026-02-28 packages ${SHA}` })), [], 'a seventh entry is not a problem');
  const missing = applyGateProblems(gateOf({ packages: packagesOf(['05', '08']) }));
  assert.equal(missing.length, 1);
  assert.match(missing[0], /04, 06, 07, 10/);
  assert.deepEqual(applyGateProblems(gateOf({ packages: { 4: PASSED } })).length, 1, 'a key that is not the two-digit id is not the task');
  assert.deepEqual(applyGateProblems(gateOf({ packages: { ...packagesOf(PACKAGE_TASKS), 10: 'x' } })).length, 1, 'an entry that is not an object is not a package');
});

test('The_gate_names_each_unmet_prerequisite_and_an_unreadable_one_is_not_met', () => {
  const both = applyGateProblems({ packages: null, packagesSha256: null, recordFirstLine: null });
  assert.equal(both.length, 2);
  assert.match(both[0], /packages\.json cannot be read/);
  assert.match(both[1], /dry-run record cannot be read/);
  for (const line of ['dry run clean', 'dry run clean 2026-11-20', `dry run clean 2026-13-01 packages ${SHA}`, `dry run clean 2026-02-30 packages ${SHA}`, `dry run clean 2026-1-1 packages ${SHA}`, `dry run clean 2026-11-20 packages ${SHA} `, `DRY RUN CLEAN 2026-11-20 packages ${SHA}`, `dry run clean 2026-11-20 packages ${SHA}\r\n`, 'dry run clean 2026-11-20 packages', `dry run clean 2026-11-20 packages ${SHA.slice(1)}`, `dry run clean 2026-11-20 packages ${SHA.toUpperCase().replace(/C/g, 'C')}`, `dry run clean 2026-11-20 sha ${SHA}`, '']) {
    const problems = applyGateProblems(gateOf({ recordFirstLine: line }));
    assert.equal(problems.length, 1, JSON.stringify(line));
    assert.match(problems[0], /dry run clean <yyyy-mm-dd> packages <sha256 of packages\.json>/);
  }
  assert.deepEqual(applyGateProblems(gateOf({ recordFirstLine: `dry run clean 2024-02-29 packages ${SHA}` })), [], 'a leap day is a date');
});

test('A_task_entry_must_show_a_passed_package_not_just_be_an_object', () => {
  const lacking = (id, entry) => applyGateProblems(gateOf({ packages: { ...packagesOf(PACKAGE_TASKS), [id]: entry } }));
  const bad = {
    'an empty object': {},
    'no identity': omit(PASSED, 'identity'),
    'an identity that is not 64 hex': { ...PASSED, identity: 'b'.repeat(63) },
    'an upper-case identity': { ...PASSED, identity: 'B'.repeat(64) },
    'an identity that is not a string': { ...PASSED, identity: 12 },
    'an identity that is a one-element array of 64 hex': { ...PASSED, identity: [PASSED.identity] },
    'an archive that is a one-element array of 64 hex': { ...PASSED, archiveSha256: [PASSED.archiveSha256] },
    'an archive that is an object with a 64 hex string form': { ...PASSED, archiveSha256: { toString: () => PASSED.archiveSha256 } },
    'no archive (not sealed)': omit(PASSED, 'archiveSha256'),
    'an archive that is not 64 hex': { ...PASSED, archiveSha256: 'xyz' },
    'no reference verdicts': omit(PASSED, 'referenceVerdicts'),
    'no csharp verdict': { ...PASSED, referenceVerdicts: { node: 'accepted' } },
    'no node verdict': { ...PASSED, referenceVerdicts: { csharp: 'accepted' } },
    'a csharp reference not accepted': { ...PASSED, referenceVerdicts: { csharp: 'rejected', node: 'accepted' } },
    'a node reference pending': { ...PASSED, referenceVerdicts: { csharp: 'accepted', node: 'pending' } },
    'verdicts that are not an object': { ...PASSED, referenceVerdicts: 'accepted' },
  };
  for (const [what, entry] of Object.entries(bad)) {
    for (const id of PACKAGE_TASKS) {
      const problems = lacking(id, entry);
      assert.equal(problems.length, 1, `${what}, task ${id}`);
      assert.ok(problems[0].includes(`task ${id}`) && /passed package/.test(problems[0]), problems[0]);
    }
  }
  assert.deepEqual(lacking('05', { ...PASSED, extra: 'fields are fine', counts: {} }), [], 'what a real entry also holds is not compared');
});

test('The_dry_run_record_is_bound_to_the_registry_by_the_sha256_of_its_bytes', () => {
  assert.deepEqual(applyGateProblems(gateOf()), []);
  const other = applyGateProblems(gateOf({ packagesSha256: 'd'.repeat(64) }));
  assert.equal(other.length, 1);
  assert.match(other[0], /names packages\.json sha256 c{64} but packages\.json is d{64}/);
  assert.match(other[0], /registry changed/);
  assert.equal(applyGateProblems(gateOf({ packagesSha256: null })).length, 1, 'a registry whose hash is not known does not match');
});

// ---- a partial apply: what is confirmed, what is uncertain, what was not attempted, and a rerun ----------------------------------------------

const failedPost = { ok: true, value: { code: 1, stdout: '', stderr: 'gh: Validation Failed (HTTP 422)', timedOut: false, truncated: false } };
/** A fake whose `which`-th POST (1-based) fails once; `storedFirst` stores the ruleset before failing, as when the answer is lost on the way back. */
function failingOnce(which, { storedFirst = false } = {}) {
  let posts = 0;
  const github = fakeGitHub({}, {
    reply: call => {
      if (call.method !== 'POST' || ++posts !== which) return undefined;
      if (storedFirst) github.store(JSON.parse(call.input), 900 + posts);
      return failedPost;
    },
  });
  return github;
}

test('A_failed_create_says_which_are_confirmed_created_which_uncertain_and_which_not_attempted', async () => {
  const second = await applyRulesets({ repo: TASK_REPO, run: failingOnce(2).run, say: collector().say, ...GO });
  assert.equal(second.kind, 'env');
  assert.match(second.reason, /Confirmed created: template-frozen\./);
  assert.match(second.reason, /Uncertain: spec-frozen-tags\./);
  assert.match(second.reason, /Not attempted: spec-frozen-tags-create\./);
  const first = await applyRulesets({ repo: TASK_REPO, run: failingOnce(1).run, say: collector().say, ...GO });
  assert.match(first.reason, /Confirmed created: none\. Uncertain: template-frozen\. Not attempted: spec-frozen-tags, spec-frozen-tags-create\./);
  const last = await applyRulesets({ repo: TASK_REPO, run: failingOnce(3).run, say: collector().say, ...GO });
  assert.match(last.reason, /Confirmed created: template-frozen, spec-frozen-tags\. Uncertain: spec-frozen-tags-create\. Not attempted: none\./);
});

test('A_second_apply_after_a_transient_failure_creates_only_the_rulesets_still_absent', async () => {
  const github = failingOnce(2);
  assert.equal((await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO })).ok, false);
  assert.deepEqual(github.state.rulesets.map(ruleset => ruleset.name), [MAIN], 'only the first was stored');
  const before = github.writes().length;
  const rerun = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.ok(rerun.ok, rerun.reason);
  assert.deepEqual(rerun.value.outcomes, [{ name: MAIN, outcome: 'unchanged' }, { name: TAGS, outcome: 'created' }, { name: CREATE, outcome: 'created' }]);
  assert.deepEqual(github.writes().slice(before).map(call => JSON.parse(call.input).name), [TAGS, CREATE]);
  assert.deepEqual(github.state.rulesets.map(ruleset => ruleset.name), [MAIN, TAGS, CREATE], 'three rulesets, none twice');
});

test('A_create_whose_answer_was_lost_but_which_was_stored_is_not_created_twice_by_the_rerun', async () => {
  const github = failingOnce(2, { storedFirst: true });
  const first = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.equal(first.ok, false);
  assert.match(first.reason, /Uncertain: spec-frozen-tags\./);
  const before = github.writes().length;
  const rerun = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.ok(rerun.ok, rerun.reason);
  assert.deepEqual(rerun.value.outcomes.map(entry => entry.outcome), ['unchanged', 'unchanged', 'created']);
  assert.deepEqual(github.writes().slice(before).map(call => JSON.parse(call.input).name), [CREATE]);
});

test('Each_create_is_read_back_before_the_next_one_is_made', async () => {
  const github = fakeGitHub();
  assert.ok((await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO })).ok);
  const posts = github.calls.flatMap((call, index) => (call.method === 'POST' ? [index] : []));
  assert.equal(posts.length, 3);
  for (const [position, index] of posts.slice(0, 2).entries()) {
    const between = github.calls.slice(index + 1, posts[position + 1]).map(call => call.args.find(arg => arg.startsWith('repos/')));
    assert.ok(between.some(pathName => new RegExp(`^repos/${TASK_REPO}/rulesets/\\d+$`).test(pathName)), `after create ${position + 1} its stored ruleset is read: ${between.join(' ')}`);
  }
});

test('A_create_that_reads_back_wrong_stops_the_apply_before_the_next_create_and_says_so', async () => {
  const github = fakeGitHub({}, { onCreate: body => (body.name === TAGS ? { ...body, rules: [] } : body) });
  const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
  assert.equal(applied.kind, 'differs');
  assert.equal(github.writes().length, 2, 'the third create is not made on top of a wrong second');
  assert.match(applied.reason, /Confirmed created: template-frozen\. Uncertain: spec-frozen-tags\. Not attempted: spec-frozen-tags-create\./);
});

test('A_present_different_ruleset_stops_the_apply_before_the_first_write_even_when_the_others_are_absent', async () => {
  for (const [name, patch] of [[MAIN, () => ({ enforcement: 'disabled' })], [CREATE, () => ({ bypass_actors: [] })]]) {
    const github = fakeGitHub({ rulesets: [derived(name, patch)] });
    const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO });
    assert.equal(applied.kind, 'refused', name);
    assert.deepEqual(github.writes(), [], name);
    assert.ok(github.calls.every(call => call.method === 'GET'), 'no update, no delete either');
  }
});

// ---- code round: no mutation of an argument, no inherited host, no array as a package -----------------------------------------------------

test('The_deep_freeze_returns_a_frozen_copy_and_leaves_its_argument_alone', () => {
  const given = { name: 'x', rules: [{ type: 'a', parameters: { n: 1 } }], list: ['p'] };
  const copy = deepFrozenCopy(given);
  assert.deepEqual(copy, given);
  assert.notEqual(copy, given);
  assert.ok(!Object.isFrozen(given) && !Object.isFrozen(given.rules) && !Object.isFrozen(given.rules[0]) && !Object.isFrozen(given.rules[0].parameters) && !Object.isFrozen(given.list), 'the argument is not frozen, at any depth');
  assert.ok(Object.isFrozen(copy) && Object.isFrozen(copy.rules) && Object.isFrozen(copy.rules[0]) && Object.isFrozen(copy.rules[0].parameters) && Object.isFrozen(copy.list), 'the result is frozen at every depth');
  assert.ok(copy.rules !== given.rules && copy.rules[0] !== given.rules[0] && copy.rules[0].parameters !== given.rules[0].parameters && copy.list !== given.list, 'the copy shares no object with the argument, at any depth');
});

test('GH_HOST_is_not_inherited_and_every_gh_call_names_github_com', async () => {
  const saved = process.env.GH_HOST;
  process.env.GH_HOST = 'evil.example';
  try {
    const github = fakeGitHub();
    assert.ok((await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...GO })).ok);
    assert.ok((await readMergeSettings(TASK_REPO, github.run)).ok);
    assert.ok(github.calls.length > 10 && github.writes().length === 3);
    for (const call of github.calls) {
      assert.ok(!('GH_HOST' in call.env), 'the child does not inherit GH_HOST');
      const at = call.args.indexOf('--hostname');
      assert.ok(at > 0 && call.args[at + 1] === 'github.com', `the call names github.com: ${call.args.join(' ')}`);
      assert.ok(call.args.indexOf('--hostname') < call.args.findIndex(arg => arg.startsWith('repos/')), 'a flag before the path');
    }
  } finally {
    if (saved === undefined) delete process.env.GH_HOST; else process.env.GH_HOST = saved;
  }
});

test('A_package_entry_that_is_an_array_is_not_a_package', () => {
  for (const id of PACKAGE_TASKS) {
    const problems = applyGateProblems(gateOf({ packages: { ...packagesOf(PACKAGE_TASKS), [id]: [] } }));
    assert.equal(problems.length, 1, id);
    assert.ok(problems[0].includes(id), problems[0]);
  }
  assert.equal(applyGateProblems(gateOf({ packages: Object.fromEntries(PACKAGE_TASKS.map(id => [id, []])) })).length, 1, 'all six arrays: one sentence naming the six');
});

// ---- code round 2 -------------------------------------------------------------------------------------------------------------------

test('The_instants_of_the_fake_GitHub_are_UTC', () => {
  const read = asGitHub(byName(MAIN), 1);
  assert.match(read.created_at, /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$/);
  assert.match(read.updated_at, /Z$/);
  assert.equal(new Date(read.created_at).toISOString(), read.created_at, 'the same instant, written in UTC');
});

test('An_apply_without_the_go_refuses_before_its_first_read', async () => {
  const early = [{ ...GO, packages: { '05': PASSED } }, { ...GO, packages: { ...GO.packages, '04': {} } }, { ...GO, packages: null }, { ...GO, recordFirstLine: 'dry run dirty' }, { ...GO, recordFirstLine: null }, { ...GO, packagesSha256: 'd'.repeat(64) }, { packages: undefined, recordFirstLine: undefined }];
  for (const go of early) {
    const github = fakeGitHub();
    const applied = await applyRulesets({ repo: TASK_REPO, run: github.run, say: collector().say, ...go });
    assert.equal(applied.ok, false);
    assert.equal(applied.kind, 'refused');
    assert.match(applied.reason, /refused/);
    assert.deepEqual(github.calls, [], 'not even a read');
  }
  const met = fakeGitHub();
  assert.ok((await applyRulesets({ repo: TASK_REPO, run: met.run, say: collector().say, ...GO })).ok);
});

test('A_stderr_longer_than_the_slice_is_scrubbed_before_it_is_cut', async () => {
  for (const pad of [281, 285, 290, 296, 297, 299]) {
    const read = await readRulesets(TASK_REPO, brokenRunner('exit', { stderr: `${'x'.repeat(pad)}${SECRET}` }).run);
    assert.equal(read.ok, false);
    assert.ok(!read.reason.includes('ghp_') && !read.reason.includes('A1b2'), `a token that crosses the cut leaves no prefix (pad ${pad}): ${read.reason.slice(-40)}`);
  }
});

test('The_read_back_after_a_create_refuses_a_list_the_first_read_would_refuse', async () => {
  const listAfterPost = entries => {
    let posted = false;
    return fakeGitHub({}, {
      reply: call => {
        if (call.method === 'POST') { posted = true; return undefined; }
        return posted && call.args.some(arg => arg.includes('per_page')) ? { ok: true, value: { code: 0, stdout: JSON.stringify(entries), stderr: '', timedOut: false, truncated: false } } : undefined;
      },
    });
  };
  const full = Array.from({ length: 100 }, (_, id) => ({ id: id + 1, name: id === 0 ? MAIN : `other-${id}` }));
  const paged = await applyRulesets({ repo: TASK_REPO, run: listAfterPost(full).run, say: collector().say, ...GO });
  assert.equal(paged.kind, 'env', 'one page of 100 may have another page: the same refusal as the first read');
  assert.match(paged.reason, /one page/);
  const noId = await applyRulesets({ repo: TASK_REPO, run: listAfterPost([{ id: 'x', name: MAIN }]).run, say: collector().say, ...GO });
  assert.equal(noId.kind, 'env', 'an entry without a numeric id is the same refusal as the first read');
  assert.match(noId.reason, /without an id/);
});

/** A fake whose read of ruleset `name` by id answers with `patch(stored)` laid over it once `after` POSTs were made: the world changing between two calls, with no stored object touched. */
function drifting({ rulesets = [], name, after, patch }) {
  let posts = 0;
  const reply = call => {
    if (call.method === 'POST') { posts += 1; return undefined; }
    const path = call.args.find(arg => arg.startsWith('repos/'));
    const stored = github.state.rulesets.find(entry => `repos/${TASK_REPO}/rulesets/${entry.id}` === path && entry.name === name);
    return posts >= after && stored !== undefined ? { ok: true, value: { code: 0, stdout: JSON.stringify({ ...stored, ...patch(stored) }), stderr: '', timedOut: false, truncated: false } } : undefined;
  };
  const github = fakeGitHub({ rulesets }, { reply });
  return github;
}

test('A_name_is_never_both_confirmed_and_uncertain_and_an_untouched_ruleset_that_changed_is_said_so', async () => {
  // A created ruleset that is not equal in the final read-back is uncertain, not confirmed.
  const made = drifting({ name: MAIN, after: 2, patch: () => ({ enforcement: 'evaluate' }) });
  const first = await applyRulesets({ repo: TASK_REPO, run: made.run, say: collector().say, ...GO });
  assert.equal(first.kind, 'differs');
  assert.match(first.reason, /Confirmed created: spec-frozen-tags, spec-frozen-tags-create\. Uncertain: template-frozen\. Not attempted: none\./);
  const named = [...first.reason.matchAll(/(?:Confirmed created|Uncertain): ([^.]*)\./g)].map(match => match[1].split(', '));
  assert.deepEqual(named[0].filter(name => named[1].includes(name)), [], 'no name in both lists');
  // A ruleset this apply did not touch that differs afterwards is "changed since the read before".
  const untouched = drifting({ rulesets: [asGitHub(byName(MAIN), 11)], name: MAIN, after: 1, patch: () => ({ enforcement: 'disabled' }) });
  const second = await applyRulesets({ repo: TASK_REPO, run: untouched.run, say: collector().say, ...GO });
  assert.equal(second.kind, 'differs');
  assert.match(second.reason, /Confirmed created: spec-frozen-tags, spec-frozen-tags-create\. Uncertain: none\. Not attempted: none\. Changed since the read before: template-frozen\./);
  assert.doesNotMatch(second.reason, /Uncertain: template-frozen/);
});

// ---- code round 3 --------------------------------------------------------------------------------------------------------------------

test('Only_the_task_repository_is_read_or_written_and_any_other_repository_makes_no_call', async () => {
  for (const repo of ['octocat/hello-world', 'o/r', TASK_REPO.toUpperCase(), `${TASK_REPO}x`, `x${TASK_REPO}`, `${TASK_REPO}.git`]) {
    const github = fakeGitHub({ rulesets: readBackOf() });
    for (const call of [() => readRulesets(repo, github.run), () => readMergeSettings(repo, github.run), () => rulesetsReadBack({ repo, run: github.run }), () => applyRulesets({ repo, run: github.run, say: collector().say, ...GO })]) {
      const got = await call();
      assert.equal(got.ok, false, repo);
      assert.equal(got.kind, 'config', repo);
      assert.match(got.reason, /task repository/, repo);
    }
    assert.deepEqual(github.calls, [], `${repo}: not even a read`);
  }
});

const TAG_REFS = byName(TAGS).conditions.ref_name.include;
const withRefs = (name, which, list) => derived(name, base => ({ conditions: { ...base.conditions, ref_name: { ...base.conditions.ref_name, [which]: list } } }));

test('The_include_and_exclude_refs_are_compared_exactly_with_the_data', () => {
  const tags = byName(TAGS);
  for (const [what, include] of [['a narrowed pattern', ['refs/tags/spec-frozen-a*']], ['a narrowed pattern that keeps the prefix', ['refs/tags/spec-frozen-*foo']], ['a widened pattern', ['refs/tags/spec-*']], ['the widest pattern', ['refs/tags/*']], ['an extra ref', [...TAG_REFS, 'refs/tags/other']], ['an extra pattern of the same prefix', [...TAG_REFS, 'refs/tags/spec-frozen-**']], ['a literal tag instead', ['refs/tags/spec-frozen-1']], ['no ref', []]]) {
    assert.ok(rulesetProblems(withRefs(TAGS, 'include', include), tags).length > 0, `${what} is a problem`);
  }
  assert.deepEqual(rulesetProblems(withRefs(TAGS, 'include', [...TAG_REFS]), tags), [], 'the exact refs are equal');
  assert.deepEqual(rulesetProblems(withRefs(TAGS, 'include', [...TAG_REFS, ...TAG_REFS]), tags), [], 'a doubled ref is the same ref');
  for (const exclude of [['refs/tags/spec-frozen-x'], ['refs/tags/*'], ['refs/tags/spec-frozen-*']]) assert.ok(rulesetProblems(withRefs(TAGS, 'exclude', exclude), tags).length > 0, `an exclusion ${exclude} is a problem`);
  assert.deepEqual(rulesetProblems(withRefs(TAGS, 'exclude', []), tags), []);
});

test('A_ref_wording_that_finds_nothing_still_names_the_exact_difference', () => {
  // Whatever the pattern reading says, a read-back whose refs are not the data's is a named problem and never `equal`.
  const main = byName(MAIN);
  for (const include of [['refs/heads/ma*'], ['refs/heads/main', 'refs/heads/main-2'], ['refs/heads/*'], ['refs/heads/main*']]) {
    const problems = rulesetProblems(withRefs(MAIN, 'include', include), main);
    assert.ok(problems.length > 0 && problems.every(problem => /include ref: /.test(problem)), `${include}: ${problems.join('; ')}`);
  }
});

// ---- no secret, no network ------------------------------------------------------------------------------------------------------------

test('A_token_in_gits_words_is_removed_from_every_reason', async () => {
  assert.equal(scrub(`HTTP 401 for ${SECRET} and github_pat_${'x'.repeat(30)} and Bearer abcdefghijklmnopqrstuv0123`), 'HTTP 401 for <token removed> and <token removed> and <token removed>');
  const read = await readRulesets(TASK_REPO, brokenRunner('exit', { stderr: `bad credentials ${SECRET}` }).run);
  assert.ok(!read.reason.includes(SECRET) && read.reason.includes('bad credentials'), read.reason);
  const applied = await applyRulesets({ repo: TASK_REPO, run: brokenRunner('exit', { stderr: SECRET }).run, say: collector().say, ...GO });
  assert.ok(!JSON.stringify(applied).includes(SECRET));
});

test('The_environment_of_gh_carries_no_token_and_the_PAT_file_is_never_read', async () => {
  const saved = { GH_TOKEN: process.env.GH_TOKEN, GITHUB_TOKEN: process.env.GITHUB_TOKEN };
  process.env.GH_TOKEN = SECRET;
  process.env.GITHUB_TOKEN = SECRET;
  try {
    const github = fakeGitHub({ rulesets: readBackOf() });
    await rulesetsReadBack({ repo: TASK_REPO, run: github.run });
    for (const call of github.calls) {
      assert.ok(!JSON.stringify(call.env).includes(SECRET), 'the child gets gh\'s own login, never an environment token');
      assert.deepEqual(Object.keys(call.env).filter(key => /TOKEN|KEY|SECRET|PASSWORD/i.test(key)), []);
      assert.equal(call.env.GH_PROMPT_DISABLED, '1');
    }
  } finally {
    for (const [key, value] of Object.entries(saved)) { if (value === undefined) delete process.env[key]; else process.env[key] = value; }
  }
  const source = readFileSync(fileURLToPath(new URL('../lib/github-lang.mjs', import.meta.url)), 'utf8');
  assert.ok(!/github-pat|deploy_key|readFileSync|process\.env\.GH_TOKEN/.test(source), 'the owner\'s gh login does the admin writes; the PAT has no admin right and is not used here');
});

test('No_test_reaches_the_network', () => {
  const source = readFileSync(fileURLToPath(new URL('../lib/github-lang.mjs', import.meta.url)), 'utf8');
  assert.deepEqual([...source.matchAll(/from '([^']+)'/g)].map(match => match[1]).filter(name => /^node:(https?|net|tls|dns|dgram|http2)$/.test(name)), []);
  assert.ok(!/\bfetch\(|XMLHttpRequest|WebSocket/.test(source));
  assert.ok(ALL_CALLS.length > 20, `the fakes recorded ${ALL_CALLS.length} calls`);
  assert.deepEqual([...new Set(ALL_CALLS.map(call => call.exe))], ['gh'], 'every call of every test went to the fake runner and named gh');
  assert.ok(ALL_CALLS.every(call => call.args[0] === 'api'));
  const sent = ALL_CALLS.filter(call => call.method !== 'GET');
  assert.ok(sent.every(call => call.method === 'POST' && call.args.find(arg => arg.startsWith('repos/')) === `repos/${TASK_REPO}/rulesets`), 'the only write any test made was a POST to a fake');
});
