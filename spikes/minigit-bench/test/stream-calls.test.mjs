// callsOf: the stream's tool_use blocks as the contamination scan's `calls` (moved out of smoke-run.mjs so the runner and the smoke share one).
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { callsOf } from '../lib/stream-calls.mjs';
import { spikeDir } from './helpers.mjs';
import { stripComments } from './scan.mjs';

const use = (name, input, parent) => ({ type: 'assistant', ...(parent ? { parent_tool_use_id: parent } : {}), message: { content: [{ type: 'text', text: 'x' }, { type: 'tool_use', name, input }] } });

test('Calls_are_the_tool_uses_of_the_assistant_events_main_or_child', () => {
  const calls = callsOf([{ type: 'system' }, use('Bash', { command: 'ls' }), use('Read', { file_path: '/a' }, 'toolu_1'), { type: 'user' }, use('Glob', undefined)]);
  assert.deepEqual(calls, [{ agent: 'main', tool: 'Bash', input: { command: 'ls' } }, { agent: 'child', tool: 'Read', input: { file_path: '/a' } }, { agent: 'main', tool: 'Glob', input: {} }]);
  assert.deepEqual(callsOf([]), []);
});

test('callsOf_is_defined_in_one_module_and_the_smoke_and_the_runner_import_it', () => {
  const lib = name => stripComments(readFileSync(path.join(spikeDir, 'lib', name), 'utf8'));
  assert.match(lib('stream-calls.mjs'), /export function callsOf/);
  assert.doesNotMatch(lib('smoke-run.mjs'), /function callsOf/, 'the smoke no longer carries its own copy');
  for (const f of ['smoke-run.mjs', 'bare-collect.mjs']) assert.match(lib(f), /from '\.\/stream-calls\.mjs'/, `${f} imports it`);
});
