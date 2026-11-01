// The one shared process launcher (minigit-bench/lib/run.mjs `runBounded`) carries every external process of the push check: git's byte
// output, the archive extraction, the batch reads. It must pass bytes through untouched, feed stdin, bound the output, and on a timeout kill
// the whole process tree - the descendants too, not only the child.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { runGitBytes } from '../lib/push-git.mjs';
import { runBatchCatFile } from '../lib/push-git.mjs';
import { commit, git, makeTaskRepo } from './support/push-fixtures.mjs';

/** Running, not merely still in the process table: a killed process waiting to be reaped is a zombie (state Z) and is gone. */
const alive = pid => {
  try {
    process.kill(pid, 0);
    return !/^\d+ \(.*\) Z/.test(readFileSync(`/proc/${pid}/stat`, 'utf8'));
  } catch { return false; }
};
const settled = async pid => { for (let i = 0; i < 30 && alive(pid); i += 1) await new Promise(r => setTimeout(r, 100)); return !alive(pid); };

test('Bytes_that_are_not_text_come_back_untouched_and_stdin_is_fed', async () => {
  const r = await runBounded('cat', [], { input: Buffer.from([0xff, 0xfe, 0, 1, 0x80]), bytes: true, timeoutMs: 10_000 });
  assert.equal(r.ok, true);
  assert.deepEqual([...r.value.stdoutBytes], [0xff, 0xfe, 0, 1, 0x80], 'a utf8 decode would have turned these into replacement characters');
});

test('A_timeout_kills_the_children_of_the_child_too_and_says_it_timed_out', async () => {
  const r = await runBounded('sh', ['-c', 'sleep 300 & echo $!; wait'], { timeoutMs: 700 });
  assert.equal(r.ok, true);
  assert.equal(r.value.timedOut, true);
  const grandchild = Number(r.value.stdout.trim());
  assert.ok(grandchild > 1, `the child printed its background pid (${r.value.stdout})`);
  assert.equal(await settled(grandchild), true, 'the whole process tree is gone, not only the child');
});

test('Output_beyond_the_ceiling_is_truncated_and_said_so_for_bytes_as_for_text', async () => {
  const r = await runBounded('sh', ['-c', 'head -c 5000 /dev/zero'], { bytes: true, maxBytes: 1000, timeoutMs: 10_000 });
  assert.equal(r.value.truncated, true);
  assert.equal(r.value.stdoutBytes.length, 1000);
});

test('Git_byte_output_goes_through_the_launcher_with_its_ceiling_and_timeout_as_values', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.bin': Buffer.from([0, 255, 1]) } });
  const blob = git(repo, 'rev-parse', 'main:a.bin');
  const got = await runGitBytes(repo, ['cat-file', 'blob', blob]);
  assert.deepEqual([...got.value], [0, 255, 1]);
  const small = await runGitBytes(repo, ['cat-file', 'blob', blob], { maxBytes: 2 });
  assert.equal(small.ok, false);
  assert.match(small.reason, /more than 2 bytes/);
  assert.equal((await runGitBytes(repo, ['no-such-subcommand'])).ok, false);
  commit(repo, 'feat: x', {});
});

test('One_cat_file_batch_process_reads_many_objects_and_names_the_missing_ones', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.txt': 'alpha\n', 'dir/b.txt': 'beta\n' } });
  const r = await runBatchCatFile(repo, ['main:a.txt', 'main:dir/b.txt', 'main:nope.txt']);
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.get('main:a.txt').toString(), 'alpha\n');
  assert.equal(r.value.get('main:dir/b.txt').toString(), 'beta\n');
  assert.equal(r.value.has('main:nope.txt'), false, 'a missing object is absent from the map');
  assert.deepEqual(r.missing, ['main:nope.txt']);
});

test('The_batch_read_refuses_an_object_beyond_the_per_object_ceiling_and_an_aggregate_beyond_the_total', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.txt': 'x'.repeat(600), 'b.txt': 'y'.repeat(600) } });
  const one = await runBatchCatFile(repo, ['main:a.txt'], { maxObjectBytes: 100 });
  assert.equal(one.ok, false);
  assert.match(one.reason, /main:a\.txt/);
  const total = await runBatchCatFile(repo, ['main:a.txt', 'main:b.txt'], { maxTotalBytes: 1000 });
  assert.equal(total.ok, false);
  assert.match(total.reason, /more than 1000 bytes/);
});

test('Bytes_mode_neither_decodes_stdout_to_text_nor_joins_the_buffer_twice', async () => {
  const BIG = 2_000_000;
  const realConcat = Buffer.concat;
  const realToString = Buffer.prototype.toString;
  const bigConcats = [];
  const bigDecodes = [];
  Buffer.concat = (list, total) => { const out = realConcat.call(Buffer, list, total); if (out.length >= BIG) bigConcats.push(out.length); return out; };
  Buffer.prototype.toString = function toString(encoding, ...rest) { if (this.length >= BIG && (encoding === undefined || encoding === 'utf8')) bigDecodes.push(this.length); return realToString.call(this, encoding, ...rest); };
  let bytes;
  let text;
  const counts = {};
  try {
    bytes = await runBounded('sh', ['-c', `head -c ${BIG} /dev/zero`], { bytes: true, maxBytes: BIG + 10, timeoutMs: 20_000 });
    counts.bytes = { concats: bigConcats.length, decodes: bigDecodes.length };
    bigConcats.length = 0; bigDecodes.length = 0;
    text = await runBounded('sh', ['-c', `head -c ${BIG} /dev/zero`], { maxBytes: BIG + 10, timeoutMs: 20_000 });
    counts.text = { concats: bigConcats.length, decodes: bigDecodes.length };
  } finally {
    Buffer.concat = realConcat;
    Buffer.prototype.toString = realToString;
  }
  assert.equal(bytes.value.stdoutBytes.length, BIG);
  assert.deepEqual(counts.bytes, { concats: 1, decodes: 0 }, 'one join of the chunks, no text decode');
  assert.equal(bytes.value.stdout, '', 'the text field is empty in bytes mode: nobody reads both');
  assert.equal(text.value.stdout.length, BIG);
  assert.deepEqual(counts.text, { concats: 1, decodes: 1 }, 'text mode is unchanged: one join, one decode');
  assert.equal('stdoutBytes' in text.value, false);
});
