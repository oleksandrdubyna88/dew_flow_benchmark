// The command and tool classifier (E1.S4, T-T2). Commands are written the way the series' agents write them.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { classifyCommand, classifyTool } from '../lib/classify.mjs';

const all = (commands, expected) => {
  for (const c of commands) assert.equal(classifyCommand(c), expected, c);
};

test('The_literal_sleep_300_is_its_own_category', () => {
  assert.equal(classifyCommand('sleep 300'), 'sleep300');
  assert.equal(classifyCommand('  sleep 300 \n'), 'sleep300');
  assert.equal(classifyCommand('sleep 300 && gh pr checks 7 --watch'), 'wait');
  assert.equal(classifyCommand('sleep 30'), 'wait');
});

test('Only_the_exact_literal_is_sleep300', () => {
  all(['sleep 3000', 'sleep 300; echo done', 'sleep 300s', 'sleep  300'], 'wait');
  assert.equal(classifyCommand('echo sleep 300'), 'other_tool');
});

test('Build_and_test_commands_are_work', () => {
  all([
    'dotnet build -c Release -m:4',
    'dotnet restore',
    'dotnet run --project hosts/Cli',
    'dotnet test',
    'dotnet format --verify-no-changes',
    './tests/X.Tests/bin/Release/net10.0/X.Tests',
    './tests/X.Tests/bin/Release/net10.0/X.Tests --filter-method "*Foo*"',
    'npm run typecheck',
    'npm run test -- --watch=false',
    'npm run lint',
    'npm ci',
    'npm test',
    'node --test',
    'node --test test/*.test.mjs',
    'node scripts/run-tests.mjs',
    'node spikes/minigit-bench/run-tests.mjs test/classify.test.mjs',
    'bash test-v2.sh',
    './test-v1.sh',
    'make -s',
    'make',
    'tsc --noEmit',
    'npx tsc -p .',
  ], 'build_test');
});

test('Build_commands_behind_a_wrapper_or_a_chain_are_still_work', () => {
  all([
    'node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4',
    'cd src && dotnet build',
    'git status && dotnet test | tail -20',
    'FOO=1 nice -n 19 dotnet build',
    'timeout 600 dotnet test',
    'time make',
    '(cd app && npm ci)',
    'for i in 1 2; do dotnet build; done',
  ], 'build_test');
});

test('A_chain_containing_a_build_is_work_even_next_to_a_wait', () => {
  assert.equal(classifyCommand('sleep 300 && dotnet build'), 'build_test');
  assert.equal(classifyCommand('dotnet build && gh pr checks 3 --watch'), 'build_test');
});

test('A_watch_on_ci_is_a_wait', () => {
  all(['gh pr checks 12 --watch', 'gh run watch 99', 'gh pr checks 12 --watch --interval 30 | tail -3'], 'wait');
});

test('Polling_loops_and_plain_sleeps_are_waits', () => {
  all([
    'sleep 5',
    'sleep 60 && gh pr view 4 --json state',
    'until gh run view 9 --json conclusion | grep -q success; do sleep 10; done',
    'while ! gh pr checks 3 | grep -q pass; do sleep 20; done',
  ], 'wait');
});

test('Other_commands_are_other_tool', () => {
  all(['git status', 'ls -la', 'cat src/a.cs', 'gh pr checks 12', 'grep -rn "dotnet build" docs', 'echo "node --test"', 'npm install', ''], 'other_tool');
});

test('Operators_inside_quotes_do_not_split_a_command', () => {
  assert.equal(classifyCommand('git commit -m "fix: a && dotnet build"'), 'other_tool');
  assert.equal(classifyCommand("echo 'a; make'"), 'other_tool');
});

test('A_command_that_is_not_text_is_other_tool', () => {
  assert.equal(classifyCommand(undefined), 'other_tool');
  assert.equal(classifyCommand(7), 'other_tool');
});

test('Tools_map_to_their_categories', () => {
  const gates = ['mcp__coai__review_plan', 'mcp__coai__review_code', 'mcp__coai__resolve', 'mcp__coai__review_document', 'mcp__coai__review_feature'];
  for (const name of gates) assert.equal(classifyTool(name, {}), 'coai_gate', name);
  for (const name of ['mcp__coai__consult', 'mcp__coai__ask_consultants', 'mcp__coai__close_consult']) assert.equal(classifyTool(name, {}), 'consultant', name);
  assert.equal(classifyTool('Agent', { description: 'x' }), 'container');
  assert.equal(classifyTool('Task', {}), 'container');
  for (const name of ['Read', 'Edit', 'Write', 'Grep', 'mcp__coai__providers', 'mcp__coai__open', 'ToolSearch']) assert.equal(classifyTool(name, {}), 'other_tool', name);
});

test('A_Bash_tool_is_classified_by_its_command', () => {
  assert.equal(classifyTool('Bash', { command: 'dotnet build' }), 'build_test');
  assert.equal(classifyTool('Bash', { command: 'sleep 300' }), 'sleep300');
  assert.equal(classifyTool('Bash', { command: 'gh run watch 1' }), 'wait');
  assert.equal(classifyTool('Bash', { command: 'ls' }), 'other_tool');
  assert.equal(classifyTool('Bash', {}), 'other_tool');
  assert.equal(classifyTool('Bash', undefined), 'other_tool');
});

// ---- review fixes: B1, M1, M2, M5, m1, m2 -----------------------------------------------------------------------------------

test('Every_template_test_entry_point_of_either_stack_is_build_test', () => {
  // The C# template runs scripts/run-tests.sh, the TS template scripts/run-tests.mjs (parent §5, template sections): a stack
  // whose runner reads as other_tool would bias its build_test down and its active time with it.
  const csharp = ['bash scripts/run-tests.sh', './scripts/run-tests.sh', 'sh scripts/run-tests.sh', 'dotnet build -c Release', 'dotnet test', 'bash test-v1.sh'];
  const typescript = ['node scripts/run-tests.mjs', 'npm test', 'npm run typecheck', 'node --test', 'tsc --noEmit', 'bash test-v1.sh'];
  all(csharp, 'build_test');
  all(typescript, 'build_test');
  const launcher = 'node .agents/conventions/tools/low-priority.mjs';
  all([...csharp, ...typescript].filter(c => !c.startsWith('node ')).map(c => `${launcher} ${c}`), 'build_test');
  assert.equal(classifyCommand('cat scripts/run-tests.sh'), 'other_tool');
});

test('The_old_mount_path_of_the_launcher_is_still_peeled_so_old_transcripts_classify', () => {
  // Transcripts recorded before the mount moved from .claude/rules/shared to .agents/conventions carry the old path.
  all([
    'node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4',
    'node ./some/other/mount/tools/low-priority.mjs dotnet test',
  ], 'build_test');
  assert.equal(classifyCommand('node .claude/rules/shared/tools/low-priority.mjs cat scripts/run-tests.sh'), 'other_tool');
});

test('A_shell_run_with_option_flags_still_finds_its_script', () => {
  all(['bash -e test-v1.sh', 'bash -x ./test-v2.sh', 'bash -eu -o pipefail scripts/run-tests.sh', 'sh -x scripts/run-tests.sh'], 'build_test');
});

test('A_shell_c_argument_is_unwrapped_and_classified_recursively', () => {
  all(['bash -lc "dotnet test"', "bash -c 'npm run typecheck'", 'sh -c "make"', 'bash -e -c "node --test"', 'bash -lc "cd x && dotnet build"'], 'build_test');
  assert.equal(classifyCommand('bash -lc "sleep 300"'), 'sleep300');
  assert.equal(classifyCommand('bash -c "gh run watch 1"'), 'wait');
  assert.equal(classifyCommand('bash -c "ls"'), 'other_tool');
  assert.equal(classifyCommand('bash -c "echo hi" && bash -c "dotnet build"'), 'build_test');
});

test('ask_human_blocks_so_it_is_a_gate_wait', () => {
  assert.equal(classifyTool('mcp__coai__ask_human', {}), 'coai_gate');
});

test('A_background_bash_launch_is_its_own_unmeasured_category', () => {
  assert.equal(classifyTool('Bash', { command: 'dotnet build', run_in_background: true }), 'background');
  assert.equal(classifyTool('Bash', { command: 'dotnet build', run_in_background: false }), 'build_test');
  assert.equal(classifyTool('Agent', { run_in_background: true }), 'container');
});

test('Timeout_and_its_option_values_are_peeled', () => {
  all(['timeout -k 5 600 dotnet test', 'timeout -s KILL 600 dotnet build', 'timeout --kill-after=5 600 make', 'timeout --foreground 60 npm ci'], 'build_test');
});

test('More_keywords_wrappers_and_separators_are_understood', () => {
  all([
    'if dotnet test; then echo ok; fi',
    'while dotnet build; do echo again; done',
    'until npm test; do :; done',
    'echo start & dotnet build',
    'ionice -c 3 dotnet build',
    'ionice -c2 -n 7 make',
    'env -i PATH=/bin dotnet test',
    'time -p dotnet build',
  ], 'build_test');
  all(['gh pr checks 12 --watch=true', 'watch -n 30 gh pr checks 12'], 'wait');
  assert.equal(classifyCommand('gh pr checks 12 --watch=false'), 'other_tool');
  assert.equal(classifyCommand('dotnet build 2>&1 | tail -5'), 'build_test');
  assert.equal(classifyCommand('echo a >&2'), 'other_tool');
  assert.equal(classifyCommand('ls &> /dev/null'), 'other_tool');
});
