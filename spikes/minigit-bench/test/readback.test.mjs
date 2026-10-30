import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { INIT_UNREPORTED } from '../lib/sandbox.mjs';
import { BARE_TOOLS } from '../lib/launch.mjs';
import { readBackInit, sessionOutcome } from '../lib/readback.mjs';
import { CAPTURE_DIR, parseLines } from './synth.mjs';

const CONFIG = '/s/box/config';
/** An init event as 2.1.310 reports it for the bare box (shape of the capture's first line). */
const init = (over = {}) => ({
  type: 'system', subtype: 'init', cwd: '/s/box/work', tools: ['Task', 'Bash', 'Edit', 'Glob', 'Grep', 'Read', 'Write'], mcp_servers: [], model: 'claude-opus-5-5',
  permissionMode: 'default', apiKeySource: 'none', claude_code_version: '2.1.310', memory_paths: { auto: `${CONFIG}/projects/-s-box-work/memory/` }, ...over,
});
const expected = (over = {}) => ({
  cwd: '/s/box/work', tools: BARE_TOOLS, mcpServers: 0, cliVersion: '2.1.310', configDir: CONFIG, priced: m => m === 'claude-opus-5-5', settings: { permissions: { allow: [] } }, ...over,
});
const fields = r => (r.ok ? [] : r.mismatches.map(m => m.field));

test('A_readback_mismatch_kills_before_the_work', () => {
  assert.deepEqual(readBackInit(init(), expected()), { ok: true });
  const rows = [
    ['an mcp_servers entry in bare', init({ mcp_servers: [{ name: 'x', status: 'connected' }] }), 'mcp_servers'],
    ['tools missing Bash', init({ tools: ['Task', 'Edit', 'Glob', 'Grep', 'Read', 'Write'] }), 'tools'],
    ['version 2.1.311', init({ claude_code_version: '2.1.311' }), 'claude_code_version'],
    ['another cwd', init({ cwd: '/elsewhere' }), 'cwd'],
    ['permissionMode bypass', init({ permissionMode: 'bypassPermissions' }), 'permissionMode'],
    ['a logged-in key source', init({ apiKeySource: 'ANTHROPIC_API_KEY' }), 'apiKeySource'],
    ['memory outside the config dir', init({ memory_paths: { auto: '/home/x/.claude/projects/p/memory/' } }), 'memory_paths.auto'],
    ['a model the table does not price', init({ model: 'claude-mystery-9' }), 'model'],
    ['an extra tool', init({ tools: [...init().tools, 'WebFetch'] }), 'tools'],
  ];
  for (const [name, ev, field] of rows) {
    const r = readBackInit(ev, expected());
    assert.equal(r.ok, false, name);
    assert.ok(fields(r).includes(field), `${name}: names ${field}, got ${fields(r)}`);
    assert.ok(r.mismatches.every(m => 'expected' in m && 'observed' in m));
  }
  assert.equal(readBackInit(init({ tools: ['Agent', 'Bash', 'Edit', 'Glob', 'Grep', 'Read', 'Write'] }), expected()).ok, true, 'Agent is the same tool as Task');
  assert.equal(readBackInit(init({ tools: [...init().tools, 'TodoWrite'] }), expected()).ok, true, 'TodoWrite reported is fine too');
  assert.equal(readBackInit(init({ tools: ['Task', 'Bash', 'Edit', 'Glob', 'Read', 'Write'] }), expected()).ok, false, 'Grep AND TodoWrite absent');
  const both = readBackInit(init({ tools: ['Task', 'Bash', 'Edit', 'Glob', 'Read', 'Write'] }), expected());
  assert.ok(both.mismatches.some(m => m.field === 'tools' && m.expected === 'Grep'), 'names Grep');
  assert.ok(INIT_UNREPORTED.includes('TodoWrite'));
});

test('An_api_key_helper_is_refused_and_absent', () => {
  const r = readBackInit(init(), expected({ settings: { apiKeyHelper: 'echo hi', permissions: { allow: [] } } }));
  assert.equal(r.ok, false);
  assert.ok(fields(r).includes('settings.apiKeyHelper'));
  const mode = readBackInit(init(), expected({ settings: { permissions: { defaultMode: 'bypassPermissions' } } }));
  assert.ok(fields(mode).includes('settings.permissions.defaultMode'));
});

test('The_real_capture_init_passes_the_readback_of_its_own_shape', () => {
  const real = parseLines(readFileSync(path.join(CAPTURE_DIR, 'stream.ndjson'), 'utf8'))[0];
  const r = readBackInit(real, { cwd: real.cwd, tools: ['Agent', 'Bash', 'Read', 'Glob'], mcpServers: 0, cliVersion: '2.1.310', configDir: '/cap/config', priced: () => true, settings: {} });
  assert.deepEqual(r, { ok: true });
});

test('The_outcome_is_the_envelopes_not_the_exit_code', () => {
  const p1 = { type: 'result', subtype: 'success', is_error: true, api_error_status: 403, result: 'Failed to authenticate. API Error: 403 probe-reached' };
  assert.equal(sessionOutcome(p1, { code: 0, signal: null }).label, 'void: api-error 403', 'P-1 exited 0 on a 403');
  assert.equal(sessionOutcome(p1, { code: 0, signal: null }).ok, false);
  const clean = { type: 'result', subtype: 'success', is_error: false, result: 'DONE' };
  assert.deepEqual([sessionOutcome(clean, { code: 0, signal: null }).label, sessionOutcome(clean, { code: 0, signal: null }).ok], ['ok', true]);
  assert.equal(sessionOutcome(null, { code: 143, signal: null }).label, 'void: stopped');
  assert.equal(sessionOutcome(null, { code: null, signal: 'SIGKILL' }).label, 'void: stopped');
  assert.equal(sessionOutcome(null, { code: 0, signal: null }).label, 'void: no result');
  assert.equal(sessionOutcome({ type: 'result', subtype: 'error_max_budget_usd', is_error: true }, { code: 1, signal: null }).label, 'void: error_max_budget_usd');
  assert.equal(sessionOutcome(clean, { code: 1, signal: null }).ok, true, 'an envelope that says success is success; the exit code is a detail');
  assert.match(sessionOutcome(clean, { code: 1, signal: null }).detail, /exit code 1/);
});
