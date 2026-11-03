// The test plumbing's own checks (fix batch 1, O6): a status line is collected until its newline, whatever the chunking, before JSON.parse — the pipe may deliver `{"child-` and `pid": 1}` apart under load.
import assert from 'node:assert/strict';
import { PassThrough } from 'node:stream';
import { test } from 'node:test';
import { readJsonLine } from './support/exec-chain.mjs';

test('A_status_line_split_across_chunks_is_collected_until_its_newline', async () => {
  const stream = new PassThrough();
  const pending = readJsonLine(stream);
  stream.write('{"child-');
  await new Promise(resolve => setTimeout(resolve, 20));
  stream.write('pid": 12, "mnt-namespace": 3}');
  await new Promise(resolve => setTimeout(resolve, 20));
  stream.write('\n{"exit-code": 0}\n');
  assert.deepEqual(await pending, { 'child-pid': 12, 'mnt-namespace': 3 });
});

test('A_stream_that_ends_without_a_complete_line_is_a_failure_and_not_a_parse_of_a_fragment', async () => {
  const stream = new PassThrough();
  const pending = readJsonLine(stream);
  stream.end('{"child-pid": 1');
  await assert.rejects(pending, /no complete line/);
});
