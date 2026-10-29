import assert from 'node:assert/strict';
import { test } from 'node:test';
import { disposerStack } from '../lib/disposers.mjs';
import { emptyCounts, sampleOnce } from '../lib/proc-sampler.mjs';
import { streamTap } from '../lib/stream-tap.mjs';

test('Disposers_run_lifo_once_a_failure_does_not_skip_the_next_and_a_second_run_runs_nothing', async () => {
  const calls = [];
  const stack = disposerStack();
  stack.push('a', () => calls.push('a'));
  stack.push('b', async () => { calls.push('b'); throw new Error('b failed'); });
  stack.push('c', () => calls.push('c'));
  assert.deepEqual(stack.names, ['a', 'b', 'c']);
  const first = await stack.run();
  assert.deepEqual(calls, ['c', 'b', 'a']);
  assert.deepEqual(first, [{ name: 'b', error: 'b failed' }]);
  assert.equal(await stack.run(), first);
  assert.deepEqual(calls, ['c', 'b', 'a'], 'nothing ran twice');
  assert.throws(() => stack.push('late', () => {}), /after the stack ran/);
});

test('The_sampler_counts_processes_holding_each_value_and_never_returns_a_value', () => {
  const REAL = 'sk-ant-fake-REALVALUE';
  const DUMMY = 'sk-ant-oat01-dummy-xyz';
  const table = {
    1: { cmdline: 'bwrap\0--x', environ: `A=1\0CLAUDE_CODE_OAUTH_TOKEN=${DUMMY}\0` },
    2: { cmdline: `node\0--secret=${REAL}`, environ: `X=1\0` },
    3: { cmdline: 'x', environ: `T=${REAL}\0` },
    4: { cmdline: `y ${DUMMY}`, environ: '' },
  };
  const proc = {
    pids: () => [...Object.keys(table), '5', '6'],
    read: (pid, file) => {
      if (pid === '5') throw Object.assign(new Error('gone'), { code: 'ENOENT' });
      if (pid === '6') throw Object.assign(new Error('denied'), { code: 'EACCES' });
      return table[pid][file];
    },
  };
  const once = sampleOnce(emptyCounts(), proc, { real: REAL, dummy: DUMMY });
  assert.deepEqual(once, { samples: 1, realInCmdline: 1, realInEnviron: 1, dummyInCmdline: 1, dummyInEnviron: 1, unreadable: 1 });
  assert.deepEqual(sampleOnce(once, proc, { real: REAL, dummy: DUMMY }).samples, 2);
  assert.equal(JSON.stringify(once).includes(REAL), false);
  assert.deepEqual(sampleOnce(emptyCounts(), proc, { real: null, dummy: null }).realInCmdline, 0, 'no secret given: nothing to count');
});

test('The_stream_tap_stamps_every_complete_line_keeps_the_raw_bytes_and_survives_a_line_split_across_chunks', () => {
  const raw = [];
  const stamped = [];
  const seen = [];
  let t = 1000;
  const tap = streamTap({ clock: () => (t += 10), writeRaw: c => raw.push(c.toString()), writeStamped: s => stamped.push(s), onEvent: (ev, at) => seen.push([ev.type, at]) });
  tap.push(Buffer.from('{"type":"system","sub'));
  tap.push(Buffer.from('type":"init"}\nnot json at all\n{"type":"result"}'));
  tap.flush();
  assert.equal(raw.join(''), '{"type":"system","subtype":"init"}\nnot json at all\n{"type":"result"}', 'the raw bytes are untouched');
  const lines = stamped.map(s => JSON.parse(s));
  assert.deepEqual(lines.map(l => l.line), ['{"type":"system","subtype":"init"}', 'not json at all', '{"type":"result"}']);
  assert.ok(lines.every((l, i) => i === 0 || l.t > lines[i - 1].t), 't increases');
  assert.deepEqual(seen.map(s => s[0]), ['system', 'result'], 'a line that is not JSON is stamped and never inspected');
  assert.equal(tap.events.length, 2);
});
