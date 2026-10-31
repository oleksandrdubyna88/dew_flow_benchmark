import assert from 'node:assert/strict';
import { test } from 'node:test';
import { PINNED_EFFORT } from '../../policy-ab-quick/lib/settings.mjs';
import { BARE_ALLOW, BARE_TOOLS, FORBIDDEN_SETTINGS, HOOK_EVENTS, claudeArgv, forbiddenIn, mcpConfigFor, settingsFor } from '../lib/launch.mjs';
import { dummyToken, layoutPaths } from '../lib/sandbox.mjs';

const layout = layoutPaths('/r', 'a1');

test('The_settings_pin_effort_and_never_bypass', () => {
  const s = settingsFor({ arm: 'bare', layout });
  assert.equal(s.effortLevel, PINNED_EFFORT.effortLevel);
  assert.deepEqual(s.modelSettings, PINNED_EFFORT.modelSettings);
  assert.deepEqual(Object.values(s.modelSettings).map(m => m.effortLevel), ['high', 'high', 'high']);
  assert.deepEqual(s.permissions, { allow: [...BARE_ALLOW] });
  assert.deepEqual(BARE_ALLOW, ['Bash', 'Read', 'Edit', 'Write', 'Glob', 'Grep', 'TodoWrite', 'Agent', 'Task']);
  assert.equal('defaultMode' in s.permissions, false);
  assert.equal(JSON.stringify(s).includes('bypassPermissions'), false);
  assert.equal(JSON.stringify(s).includes('apiKeyHelper'), false);
  assert.deepEqual(forbiddenIn(s), []);
  assert.deepEqual(Object.keys(s.hooks).sort(), [...HOOK_EVENTS].sort());
  assert.deepEqual(HOOK_EVENTS, ['PreToolUse', 'PostToolUse', 'PostToolUseFailure', 'SubagentStart', 'SubagentStop']);
  for (const entries of Object.values(s.hooks)) {
    const cmd = entries[0].hooks[0];
    assert.equal(cmd.type, 'command');
    assert.equal(cmd.command, `node ${layout.files.audit} --out ${layout.files.auditLog}`, 'the harness copy of the hook, writing into box/out');
  }
  assert.equal(JSON.stringify(settingsFor({ arm: 'bare', layout })), JSON.stringify(s), 'deterministic');
});

test('The_process_arm_adds_coai_and_tool_search_to_the_allow_and_nothing_else_changes', () => {
  const bare = settingsFor({ arm: 'bare', layout });
  const proc = settingsFor({ arm: 'process', layout });
  assert.deepEqual(proc.permissions.allow, [...BARE_ALLOW, 'mcp__coai__*', 'ToolSearch']);
  assert.deepEqual({ ...proc, permissions: bare.permissions }, bare);
});

test('Forbidden_settings_are_found_by_dotted_path', () => {
  assert.deepEqual(FORBIDDEN_SETTINGS, ['apiKeyHelper', 'permissions.defaultMode']);
  assert.deepEqual(forbiddenIn({ apiKeyHelper: 'x', permissions: { defaultMode: 'bypassPermissions', allow: [] } }), ['apiKeyHelper', 'permissions.defaultMode']);
  assert.deepEqual(forbiddenIn({ permissions: { allow: [] } }), []);
  assert.deepEqual(forbiddenIn(null), []);
});

test('The_argv_persists_the_session_and_names_no_secret', () => {
  const argv = claudeArgv({ layout, cap: 0.2 });
  assert.equal(argv.includes('--no-session-persistence'), false, 'transcripts are the third view');
  assert.ok(argv.includes('--strict-mcp-config'));
  assert.deepEqual(argv.slice(0, 7), ['-p', '--model', 'opus', '--output-format', 'stream-json', '--verbose', '--forward-subagent-text']);
  assert.equal(argv[argv.indexOf('--max-budget-usd') + 1], '0.2');
  assert.equal(argv[argv.indexOf('--tools') + 1], BARE_TOOLS.join(','));
  assert.equal(argv[argv.indexOf('--settings') + 1], layout.files.settings);
  assert.equal(argv[argv.indexOf('--mcp-config') + 1], layout.files.mcp);
  assert.equal(argv[argv.indexOf('--setting-sources') + 1], 'project');
  assert.ok(argv.includes('--include-hook-events'));
  assert.equal(argv.some(a => a.length > 400), false, 'the prompt is not in the argv: it goes on stdin');
  assert.equal(argv.join('\0').includes(dummyToken('x').slice(0, 16)), false);
  assert.equal(argv.join(' ').includes('sk-ant'), false);
  assert.deepEqual(BARE_TOOLS, ['Agent', 'Bash', 'Read', 'Edit', 'Write', 'Glob', 'Grep', 'TodoWrite']);
  assert.deepEqual(claudeArgv({ layout, cap: 1, tools: ['Bash'] }).slice(-2), ['--tools', 'Bash']);
});

test('The_mcp_config_of_the_bare_arm_lists_no_server_and_the_process_arm_is_not_this_stories', () => {
  assert.deepEqual(mcpConfigFor('bare'), { ok: true, value: { mcpServers: {} } });
  assert.equal(mcpConfigFor('process').ok, false);
  assert.match(mcpConfigFor('nope').reason, /bare.*process|legal/);
});
