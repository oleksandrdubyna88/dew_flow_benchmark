import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { decide, lineCount } from '../lib/edit-bound.mjs';
import { overlayEntries } from '../lib/settings.mjs';
import { machineScrubber, scrubStrings } from '../lib/scrub.mjs';
import { summarize } from '../lib/summary.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(os.tmpdir(), 'pab-tree');
const lines = n => Array.from({ length: n }, (_, i) => `l${i}`).join('\n');
const ask = (over, state = {}) => decide({ event: 'PreToolUse', tool: 'Edit', input: { file_path: path.join(root, 'a.ts'), old_string: '', new_string: lines(10) }, sessionId: 's', root, state, existingLinesOf: () => 0, ...over });

test('first edit within one file and 50 lines is allowed and counted', () => {
  const r = ask({ input: { file_path: path.join(root, 'a.ts'), old_string: lines(20), new_string: lines(30) } });
  assert.equal(r.action, 'allow');
  assert.equal(r.state.s.lines, 50);
});

test('a second file is denied, with the reason shape', () => {
  const first = ask({});
  const r = ask({ input: { file_path: path.join(root, 'b.ts'), old_string: 'x', new_string: 'y' } }, first.state);
  assert.equal(r.action, 'deny');
  assert.ok(r.reason.startsWith('subagent-models.md: the main session edits at most one file and 50 lines'));
  assert.ok(Buffer.byteLength(r.reason) <= 300);
  assert.match(r.reason, /2 files/);
  assert.deepEqual(r.state, first.state, 'deny leaves state unchanged');
});

test('the 51st line is denied', () => {
  const first = ask({ input: { file_path: path.join(root, 'a.ts'), old_string: '', new_string: lines(50) } });
  assert.equal(first.action, 'allow');
  assert.equal(ask({ input: { file_path: path.join(root, 'a.ts'), old_string: '', new_string: 'one more' } }, first.state).action, 'deny');
  assert.equal(ask({ input: { file_path: path.join(root, 'a.ts'), old_string: '', new_string: lines(51) } }).action, 'deny');
});

test('a subagent (agent_id) is never bounded', () => {
  assert.equal(ask({ agentId: 'ag1', input: { file_path: path.join(root, 'a.ts'), old_string: '', new_string: lines(500) } }).action, 'allow');
});

test('a path outside the tree is allowed and not counted', () => {
  const r = ask({ input: { file_path: path.join(os.tmpdir(), 'elsewhere', 'x.ts'), old_string: '', new_string: lines(500) } });
  assert.equal(r.action, 'allow');
  assert.deepEqual(r.state, {});
});

test('UserPromptSubmit resets', () => {
  const first = ask({});
  assert.equal(decide({ event: 'UserPromptSubmit', sessionId: 's', root, state: first.state }).action, 'reset');
});

test('malformed input is allowed', () => {
  assert.equal(decide({ event: 'PreToolUse', tool: 'Edit', input: null, sessionId: 's', root, state: {} }).action, 'allow');
  assert.equal(decide({}).action, 'allow');
});

test('Write counts the content lines plus the existing file lines; MultiEdit and notebooks sum', () => {
  const w = ask({ tool: 'Write', input: { file_path: path.join(root, 'a.ts'), content: lines(30) }, existingLinesOf: () => 25 });
  assert.equal(w.action, 'deny');
  assert.equal(ask({ tool: 'Write', input: { file_path: path.join(root, 'a.ts'), content: lines(30) }, existingLinesOf: () => 20 }).state.s.lines, 50);
  const m = ask({ tool: 'MultiEdit', input: { file_path: path.join(root, 'a.ts'), edits: [{ old_string: lines(10), new_string: lines(10) }, { old_string: 'a', new_string: 'b' }] } });
  assert.equal(m.state.s.lines, 22);
  assert.equal(ask({ tool: 'NotebookEdit', input: { notebook_path: path.join(root, 'n.ipynb'), new_source: lines(7) } }).state.s.lines, 7);
  assert.equal(lineCount(''), 0);
});

test('the hook process: denies the second file, resets on a prompt, fails open on garbage', () => {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'pab-eb-'));
  try {
    fs.mkdirSync(path.join(tmp, '.git'));
    fs.mkdirSync(path.join(tmp, '.claude', 'hooks'), { recursive: true });
    for (const [src, dst] of [['../hooks/edit-bound.mjs', 'edit-bound.mjs'], ['../lib/edit-bound.mjs', 'edit-bound-core.mjs']]) fs.copyFileSync(path.join(here, src), path.join(tmp, '.claude', 'hooks', dst));
    const hook = path.join(tmp, '.claude', 'hooks', 'edit-bound.mjs');
    const call = (payload, input = JSON.stringify(payload)) => spawnSync(process.execPath, [hook], { input, env: { ...process.env, CLAUDE_PROJECT_DIR: tmp } });
    const edit = f => ({ hook_event_name: 'PreToolUse', tool_name: 'Edit', session_id: 's', tool_input: { file_path: path.join(tmp, f), old_string: 'a', new_string: 'b' } });
    assert.equal(call(edit('a.ts')).stdout.length, 0);
    assert.match(call(edit('b.ts')).stdout.toString(), /"permissionDecision":"deny"/);
    assert.ok(fs.existsSync(path.join(tmp, '.git', 'edit-bound.json')));
    call({ hook_event_name: 'UserPromptSubmit', session_id: 's' });
    assert.ok(!fs.existsSync(path.join(tmp, '.git', 'edit-bound.json')));
    assert.equal(call(edit('b.ts')).stdout.length, 0);
    const bad = call(null, 'not json');
    assert.equal(bad.status, 0);
    assert.equal(bad.stdout.length, 0);
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
});

test('only arm B wires the edit bound', () => {
  assert.ok(!JSON.stringify(overlayEntries('A')).includes('edit-bound'));
  const b = overlayEntries('B');
  assert.ok(JSON.stringify(b.PreToolUse).includes('edit-bound.mjs'));
  assert.ok(JSON.stringify(b.UserPromptSubmit).includes('edit-bound.mjs'));
  assert.equal(overlayEntries('A').UserPromptSubmit, undefined);
});

test('summary counts edit-bound denials from the stream', () => {
  const ev = [
    { type: 'assistant', parent_tool_use_id: null, message: { model: 'o', content: [{ type: 'tool_use', id: 'e1', name: 'Edit', input: {} }] } },
    { type: 'user', parent_tool_use_id: null, message: { content: [{ type: 'tool_result', tool_use_id: 'e1', is_error: true, content: 'subagent-models.md: the main session edits at most one file and 50 lines per request (this would be 2 files / 3 lines).' }] } },
  ];
  assert.equal(summarize({ events: ev, audit: [], cellJson: {}, treeDir: '' }).editBoundDenials, 1);
});

test('machine scrubber maps profile, runs root and creds clone', () => {
  const f = machineScrubber({ runsRoot: 'D:/rsd/_q2', creds: 'D:/rsd/dew_flow_creds_for_devs' });
  assert.equal(f('C:\\Users\\bob\\x and C:\\\\Users\\\\bob\\\\y and /c/Users/bob/z and c:/Users/bob/w'), '~\\x and ~\\\\y and ~/z and ~/w');
  assert.equal(f('D:\\rsd\\_q2\\c1\\tree and D:/rsd/_q2/c2'), '<runs>\\c1\\tree and <runs>/c2');
  assert.equal(f('git -C D:/rsd/dew_flow_creds_for_devs show; cd /d/rsd/_q2/c1'), 'git -C <creds> show; cd <runs>/c1');
  assert.deepEqual(scrubStrings({ a: ['C:\\Users\\bob'], n: 1 }, f), { a: ['~'], n: 1 });
});
