import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { startSink } from '../capture/otlp-sink.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';
import { parseLines } from './synth.mjs';

const hook = path.join(spikeDir, 'capture', 'stamp-hook.mjs');
const runHook = (stdin, file) => spawnSync(process.execPath, [hook, file], { input: stdin, encoding: 'utf8', timeout: 10000 });

test('The_stamp_hook_appends_one_line_with_the_fields_the_audit_pairs_by', () => {
  const file = path.join(tmpDir('hook'), 'hooks.ndjson');
  const payload = { hook_event_name: 'PreToolUse', tool_name: 'Bash', tool_use_id: 'toolu_1', agent_id: 'a1', session_id: 's1', tool_input: { command: 'secret' } };
  const before = Date.now();
  const r = runHook(JSON.stringify(payload), file);
  assert.equal(r.status, 0);
  const [line] = parseLines(readFileSync(file, 'utf8'));
  assert.deepEqual(Object.keys(line).sort(), ['agent_id', 'hook_event_name', 'session_id', 't', 'tool_name', 'tool_use_id']);
  assert.equal(line.tool_use_id, 'toolu_1');
  assert.ok(line.t >= before && line.t <= Date.now());
  runHook(JSON.stringify({ ...payload, hook_event_name: 'PostToolUse' }), file);
  assert.deepEqual(parseLines(readFileSync(file, 'utf8')).map(l => l.hook_event_name), ['PreToolUse', 'PostToolUse']);
});

test('The_stamp_hook_exits_zero_whatever_happened', () => {
  const dir = tmpDir('hook-bad');
  const garbage = runHook('not json', path.join(dir, 'h.ndjson'));
  assert.equal(garbage.status, 0);
  assert.equal(parseLines(readFileSync(path.join(dir, 'h.ndjson'), 'utf8'))[0].hook_event_name, null, 'unreadable input is a line of nulls, not a zero');
  assert.equal(runHook('{}', path.join(dir, 'no', 'such', 'dir', 'h.ndjson')).status, 0);
  assert.equal(spawnSync(process.execPath, [hook], { input: '{}', timeout: 10000 }).status, 0, 'no file argument');
});

test('The_file_sink_keeps_every_body_raw_and_parses_nothing', async t => {
  const dir = tmpDir('sink');
  const sink = await startSink({ dir });
  t.after(() => sink.close());
  assert.equal(sink.server.address().address, '127.0.0.1');
  const base = `http://127.0.0.1:${sink.port}`;
  const a = await fetch(`${base}/v1/logs`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: '{"resourceLogs":[]}' });
  const b = await fetch(`${base}/v1/metrics`, { method: 'POST', headers: { 'content-type': 'application/x-protobuf' }, body: 'not even json' });
  assert.deepEqual([a.status, b.status], [200, 200]);
  assert.deepEqual(await a.json(), {});
  const lines = parseLines(readFileSync(path.join(dir, 'otlp-raw.ndjson'), 'utf8'));
  assert.deepEqual(lines.map(l => [l.path, l.contentType, l.body]), [['/v1/logs', 'application/json', '{"resourceLogs":[]}'], ['/v1/metrics', 'application/x-protobuf', 'not even json']]);
  assert.ok(lines.every(l => Number.isInteger(l.receivedAt)));
});

test('The_file_sink_refuses_other_paths_and_oversized_bodies_and_stays_up', async t => {
  const dir = tmpDir('sink-refuse');
  const sink = await startSink({ dir });
  t.after(() => sink.close());
  const base = `http://127.0.0.1:${sink.port}`;
  assert.equal((await fetch(`${base}/v1/traces`, { method: 'POST', body: '{}' })).status, 404);
  assert.equal((await fetch(`${base}/v1/logs`, { method: 'POST', body: 'x'.repeat(9 * 1024 * 1024) })).status, 413);
  assert.equal((await fetch(`${base}/v1/logs`, { method: 'POST', body: '{}' })).status, 200);
  assert.equal(parseLines(readFileSync(path.join(dir, 'otlp-raw.ndjson'), 'utf8')).length, 1);
  assert.equal(existsSync(path.join(dir, 'otel.ndjson')), false);
});

test('The_file_sink_is_at_most_sixty_lines', () => {
  const lines = readFileSync(path.join(spikeDir, 'capture', 'otlp-sink.mjs'), 'utf8').split('\n').length;
  assert.ok(lines <= 61, `otlp-sink.mjs has ${lines} lines`);
});
