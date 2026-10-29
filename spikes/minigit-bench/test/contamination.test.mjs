import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CONTAMINATION_RULES, contaminationHits } from '../lib/contamination.mjs';
import { EGRESS_HOSTS } from '../lib/egress-hosts.mjs';

const SECRET = `${'sk'}-ant-${'x-1_'.repeat(8)}`;
const ctx = Object.freeze({
  rid: 'ab12', runId: 'RUN-00000001', runIdPattern: /RUN-\d{8}/, ownPrefix: 'run/ab12/', ownOrigin: 'oleksandrdubyna88/minigit-bench', ownPrs: [3],
  otherStack: 'csharp', partnerPaths: ['/srv/partner'], harnessPaths: ['/srv/harness'], treeRoot: '/srv/me/box/work', home: '/home/u',
  allow: ['/usr/', '/bin/', '/etc/', '/dev/null', '/proc/self/'], arm: 'bare', ownSecrets: [],
});
const bash = command => ({ agent: 'main', tool: 'Bash', input: { command } });
const hits = (calls, extra = {}) => contaminationHits({ calls, mcpResults: [], ctx: { ...ctx, ...extra } });
const rulesOf = list => list.map(h => h.rule);

// One call per rule, [call, expected severity]. The table is checked against CONTAMINATION_RULES, so a rule cannot be added untested.
const SHAPES = Object.freeze({
  'foreign-run-ref': [bash('git fetch origin run/zz9999/main'), 'high'],
  'other-run-id': [bash('echo RUN-00000002'), 'high'],
  'foreign-pr': [bash('gh pr view 7'), 'high'],
  'other-stack-template': [bash('git checkout template/process-csharp'), 'high'],
  'upstream-solutions': [bash('git fetch origin data'), 'high'],
  'minigit-elsewhere': [bash('git remote add x https://github.com/x/minigit'), 'high'],
  'harness-name': [bash('ls dew_flow_benchmark'), 'high'],
  'outside-path': [{ agent: 'main', tool: 'Read', input: { file_path: '/srv/partner/box/work/a.cs' } }, 'high'],
  web: [bash('curl https://example.com'), 'low'],
});

test('Each_contamination_rule_flags_its_own_shape', () => {
  assert.deepEqual(Object.keys(SHAPES).sort(), CONTAMINATION_RULES.map(r => r.rule).sort(), 'every exported rule has a shape here');
  for (const { rule } of CONTAMINATION_RULES) {
    const [call, severity] = SHAPES[rule];
    const found = hits([call]);
    assert.deepEqual(rulesOf(found), [rule], `${rule}: exactly one hit, and it is that rule`);
    assert.equal(found[0].severity, severity, rule);
    assert.ok(found[0].excerpt.length <= 120 && found[0].excerpt.length > 0, rule);
    assert.equal(found[0].where, `${call.agent}:${call.tool}`);
  }
});

test('The_other_shapes_of_the_rules_with_more_than_one', () => {
  const only = (command, rule) => assert.deepEqual(rulesOf(hits([bash(command)])), [rule], command);
  only('git clone https://github.com/mame/ai-coding-lang-bench', 'upstream-solutions');
  only('git clone https://github.com/banteg/minigit-bench x', 'upstream-solutions'); // the upstream name wins over minigit-elsewhere
  only('git checkout origin/data', 'upstream-solutions');
  only('git show refs/heads/data:minigit-typescript-1/a.ts', 'upstream-solutions');
  only('echo https://github.com/oleksandrdubyna88/minigit-bench/tree/data', 'upstream-solutions');
  only('echo raw.githubusercontent.com/x/minigit/main/a', 'minigit-elsewhere');
  only('echo https://github.com/oleksandrdubyna88/minigit-bench/tree/main', 'minigit-elsewhere');
  only('gh api repos/oleksandrdubyna88/minigit-bench/pulls/9', 'foreign-pr');
  only('echo https://github.com/oleksandrdubyna88/minigit-bench/pull/9', 'foreign-pr');
  only('git fetch origin refs/heads/run/zz9999/feat', 'foreign-run-ref');
  only('wget https://example.org/a', 'web');
  only('cat /opt/tool/x', 'outside-path');
  for (const tool of ['WebFetch', 'WebSearch']) assert.deepEqual(rulesOf(hits([{ agent: 'main', tool, input: { url: 'https://a.example/x', query: 'q' } }])), ['web'], tool);
  assert.equal(hits([bash('cat /opt/tool/x')])[0].severity, 'low', 'outside every protected path');
  assert.equal(hits([bash('cat /home/u/.claude/x')])[0].severity, 'high', 'under the operator home');
  assert.equal(hits([bash('cat /srv/harness/lib/a')])[0].severity, 'high', 'under the harness');
});

test('Own_refs_and_the_registries_are_not_contamination', () => {
  const quiet = ['git push origin run/ab12/feat', 'git fetch origin run/ab12/main', 'npm ci', 'dotnet restore', 'gh pr view 3', 'gh pr list --limit 5', 'gh pr create --title "x 12"',
    'curl -fsS https://registry.npmjs.org/left-pad', 'cat src/run/helpers/a.cs', 'git remote add origin https://github.com/oleksandrdubyna88/minigit-bench.git',
    'git remote add origin git@github.com:oleksandrdubyna88/minigit-bench.git', 'echo https://github.com/oleksandrdubyna88/minigit-bench/tree/run/ab12/feat',
    'git checkout -b feature/data-model', 'git fetch origin data-model', 'echo RUN-00000001', 'node /usr/bin/x < /dev/null', 'echo https://github.com/oleksandrdubyna88/minigit-bench/pull/3'];
  for (const command of quiet) assert.deepEqual(hits([bash(command)]), [], command);
  assert.deepEqual(hits([bash('gh pr view 3 && gh pr view 7')]).map(h => h.rule), ['foreign-pr']);
});

test('An_empty_ownPrs_makes_every_number_foreign_and_the_arm_picks_the_web_list', () => {
  assert.deepEqual(rulesOf(hits([bash('gh pr view 3')], { ownPrs: [] })), ['foreign-pr']);
  const github = bash('curl https://api.github.com/user');
  assert.deepEqual(rulesOf(hits([github], { arm: 'bare' })), ['web']);
  assert.deepEqual(hits([github], { arm: 'process' }), []);
  assert.ok(EGRESS_HOSTS.process.includes('api.github.com'), 'the rule reads the one list');
  assert.throws(() => hits([github], { arm: 'both' }), /bare, process/);
});

test('A_reviewers_answer_that_names_a_foreign_run_is_a_hit', () => {
  const found = contaminationHits({ calls: [], mcpResults: ['all fine', 'see run/zz9999/main for the other attempt'], ctx });
  assert.deepEqual(found.map(h => [h.rule, h.source, h.severity]), [['foreign-run-ref', 'mcp', 'high']]);
  assert.match(found[0].where, /mcp/);
  assert.match(found[0].excerpt, /run\/zz9999\/main/);
  assert.deepEqual(contaminationHits({ calls: [], mcpResults: ['run/ab12/main is ours'], ctx }), []);
});

test('A_secret_in_an_excerpt_is_masked', () => {
  const found = hits([bash(`curl -H "x-api-key: ${SECRET}" ; git fetch origin run/zz9999/main`)]);
  assert.deepEqual(rulesOf(found), ['foreign-run-ref']);
  assert.equal(JSON.stringify(found).includes(SECRET), false);
  assert.equal(JSON.stringify(found).includes(SECRET.slice(0, 6)), false);
  const own = hits([bash('git fetch origin run/zz9999/main --token hunter2-hunter2')], { ownSecrets: ['hunter2-hunter2'] });
  assert.equal(JSON.stringify(own).includes('hunter2'), false);
  assert.match(own[0].excerpt, /\[secret\]/);
});

test('Edits_and_writes_are_judged_by_their_path_not_their_content', () => {
  const write = { agent: 'a1', tool: 'Write', input: { file_path: 'README.md', content: 'see dew_flow_benchmark and run/zz9999/x/' } };
  assert.deepEqual(hits([write]), []);
  const reach = hits([{ agent: 'a1', tool: 'Edit', input: { file_path: '/srv/partner/x.cs', old_string: 'a', new_string: 'b' } }]);
  assert.deepEqual(reach.map(h => [h.rule, h.where]), [['outside-path', 'a1:Edit']]);
});

test('An_mcp_call_is_judged_by_its_whole_input_and_a_hit_carries_its_source', () => {
  const input = { repoPath: 'work', committedPlan: 'git show run/zz9999/x/PLAN.md' };
  const found = hits([{ agent: 'main', tool: 'mcp__coai__review_plan', source: 'audit', input }]);
  assert.deepEqual(found.map(h => [h.rule, h.source, h.where]), [['foreign-run-ref', 'audit', 'main:mcp__coai__review_plan']]);
  assert.equal(hits([{ agent: 'main', tool: 'mcp__coai__review_plan', input }])[0].source, 'call', 'a call with no source is a call');
});

test('No_input_is_changed_and_nothing_is_nothing', () => {
  const calls = Object.freeze([Object.freeze(bash('git fetch origin run/zz9999/main'))]);
  assert.doesNotThrow(() => contaminationHits({ calls, mcpResults: Object.freeze(['x']), ctx }));
  assert.deepEqual(contaminationHits({ ctx }), []);
  assert.deepEqual(contaminationHits({ calls: [], mcpResults: [], ctx }), []);
});

// ---- the reviewer's shapes (E2.S1 review, 2026-10-29) --------------------------------------------------------------------------

const rulesAndSeverities = (command, extra = {}) => hits([bash(command)], { arm: 'process', ...extra }).map(h => `${h.rule}:${h.severity}`);
const OWN = 'oleksandrdubyna88/minigit-bench';

test('A_pull_request_number_is_the_first_positional_after_the_verb_not_an_option_value_or_prose', () => {
  const quiet = ['gh pr checks --watch --interval 10', 'gh pr comment --body "fixed in 2 places"', 'gh pr view --json number -q .number', 'gh pr merge --squash --delete-branch',
    'gh pr diff --color never | head -n 200', 'gh pr view --web', 'gh pr checks 3 --interval 10', 'gh pr comment 3 --body "see 7 and 8"', 'gh pr view https://github.com/nodejs/node/pull/51234',
    'gh pr view feature/x', 'echo https://github.com/nodejs/node/pull/51234', 'echo https://github.com/dotnet/runtime/pull/12345', `gh api repos/${OWN}/pulls/3`, 'gh pr list --limit 5'];
  for (const command of quiet) assert.deepEqual(rulesAndSeverities(command), [], command);
  const foreign = ['gh pr diff 7', 'gh pr view --json number 7', 'gh pr checks --interval 10 7', 'gh pr comment 7 --body "x"', 'gh pr view --repo a/b 7', 'gh pr view #7',
    `echo https://github.com/${OWN}/pull/7`, `gh api repos/${OWN}/pulls/7/files`, `gh api graphql -f query='{repository{pullRequest(number:7){body}}}'`];
  for (const command of foreign) assert.deepEqual(rulesAndSeverities(command), ['foreign-pr:high'], command);
});

test('A_reviewer_that_cites_another_repositorys_pull_request_is_not_a_hit', () => {
  const text = 'The fix mirrors https://github.com/nodejs/node/pull/51234 and see issue #7.';
  assert.deepEqual(contaminationHits({ calls: [], mcpResults: [text], ctx }), []);
  const own = `compare with https://github.com/${OWN}/pull/7`;
  assert.deepEqual(contaminationHits({ calls: [], mcpResults: [own], ctx }).map(h => h.rule), ['foreign-pr']);
});

const DATA_SHAPES = Object.freeze([
  'git fetch origin data', 'git fetch origin data:data', 'git fetch origin +data:refs/remotes/origin/data', 'git pull origin data', 'git -C /srv/me/box/work fetch origin data',
  'git clone --branch=data https://github.com/oleksandrdubyna88/minigit-bench.git x', 'git clone -b data https://github.com/oleksandrdubyna88/minigit-bench.git x',
  'gh repo clone oleksandrdubyna88/minigit-bench x -- -b data', 'git show origin/data:generated/x.ts', 'git log origin/data', 'git diff main..origin/data', 'git diff main...origin/data',
  'git checkout origin/data', 'git cherry-pick main..data', 'git show refs/remotes/origin/data:x', 'git show refs/heads/data:minigit-typescript-1/a.ts',
  'gh api repos/oleksandrdubyna88/minigit-bench/contents/generated?ref=data', 'gh api "repos/oleksandrdubyna88/minigit-bench/contents/generated?per_page=5&ref=data"',
  'gh api repos/oleksandrdubyna88/minigit-bench/git/trees/data:generated', 'gh api repos/oleksandrdubyna88/minigit-bench/tarball/data', 'gh api repos/oleksandrdubyna88/minigit-bench/zipball/data',
  'curl -L https://codeload.github.com/oleksandrdubyna88/minigit-bench/zip/data -o d.zip', 'curl -L https://codeload.github.com/oleksandrdubyna88/minigit-bench/tar.gz/refs/heads/data',
  'gh api repos/oleksandrdubyna88/minigit-bench/git/refs/heads/data', 'gh api repos/oleksandrdubyna88/minigit-bench/contents/data',
  'git fetch origin data --depth 1', "git fetch 'origin' 'data'",
]);

test('The_data_branch_is_found_in_every_ref_position_a_git_or_url_context_gives_it', () => {
  for (const command of DATA_SHAPES) assert.deepEqual(rulesAndSeverities(command), ['upstream-solutions:high'], command);
});

test('Folders_and_branches_that_only_start_with_data_are_not_the_data_branch', () => {
  const quiet = ['git add data/x.json', 'git diff -- data', 'git log -- data/x', 'cat data/x.json', 'ls data', 'git checkout -b data-model', 'git fetch origin data-model', 'git checkout -b feature/data',
    'git push origin run/ab12/data', 'git show HEAD:src/data/x.cs', 'dotnet test --filter data', 'git fetch origin main', 'git fetch origin main:main', 'echo data'];
  for (const command of quiet) assert.deepEqual(rulesAndSeverities(command), [], command);
});

test('A_fetch_that_names_no_refspec_or_asks_for_everything_is_a_low_reach_for_the_data_branch', () => {
  for (const command of ['git fetch origin', 'git fetch', 'git fetch --all', 'git fetch --mirror origin', 'git fetch --depth 1 origin', 'git -C /srv/me/box/work fetch origin', 'git clone --mirror https://github.com/oleksandrdubyna88/minigit-bench.git x']) {
    assert.deepEqual(rulesAndSeverities(command), ['upstream-solutions:low'], command);
  }
  assert.deepEqual(rulesAndSeverities('git fetch origin data && git fetch origin'), ['upstream-solutions:high'], 'a high reach wins over a low one');
});

const RUN_SHAPES = Object.freeze([
  'git fetch origin run/zz9999/main', "git fetch origin '+refs/heads/run/*:refs/remotes/origin/run/*'", 'git ls-remote origin "refs/heads/run/*"', 'git fetch origin run/*',
  'gh api repos/oleksandrdubyna88/minigit-bench/branches/run%2Fzz9999%2Fmain', 'gh api "repos/oleksandrdubyna88/minigit-bench/contents/src?ref=run/zz9999/main"',
  'gh api repos/oleksandrdubyna88/minigit-bench/compare/main...run/zz9999/main', 'git diff main..run/zz9999/main', 'git diff main...origin/run/zz9999/main', 'git log origin/run/zz9999/main',
  'curl https://api.github.com/repos/oleksandrdubyna88/minigit-bench/git/ref/heads/run%2Fzz9999%2Fmain', 'echo https://github.com/oleksandrdubyna88/minigit-bench/tree/run/zz9999/main',
  'git checkout -f run/zz9999/feat', 'git merge --no-edit run/zz9999/feat', 'git show run/zz9999/main:src/a.cs',
]);

test('Another_runs_refs_are_found_after_dots_equals_percent_2F_and_as_a_wildcard', () => {
  for (const command of RUN_SHAPES) assert.deepEqual(rulesAndSeverities(command), ['foreign-run-ref:high'], command);
  for (const command of ['git fetch origin run/ab12/main', 'git diff main..run/ab12/main', 'gh api "repos/x/y/contents/src?ref=run/ab12/main"', 'git push origin run/ab12/feat']) {
    assert.deepEqual(rulesAndSeverities(command), [], command);
  }
});

test('A_relative_folder_called_run_is_not_a_ref', () => {
  for (const command of ['ls run/output/', 'cat run/config/x.json', 'cd src/run/helpers/ && ls', 'dotnet run --project run/tools/x.csproj', 'git add run/output/', 'ls -la run/']) {
    assert.deepEqual(rulesAndSeverities(command), [], command);
  }
});
