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
    'node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4',
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
