// The session scan reads every file once, one at a time, and keeps only hits and counters. A file over the whole-read limit is scanned in windows that
// overlap by more than a token (never skipped: a token in a big file is the case an agent would choose), and a file nobody can scan is a high
// finding that refutes B-S3, not a quiet list entry.
import assert from 'node:assert/strict';
import { mkdirSync, truncateSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import v8 from 'node:v8';
import vm from 'node:vm';
import { scanSessionDir } from '../lib/session-scan.mjs';
import { fakeToken } from './broker-stub.mjs';
import { tmpDir } from './helpers.mjs';

const LIMITS = { maxScanBytes: 1000, windowBytes: 256, overlapBytes: 64, maxStreamBytes: 10_000 };
const write = (dir, rel, text) => { const f = path.join(dir, rel); mkdirSync(path.dirname(f), { recursive: true }); writeFileSync(f, text, 'latin1'); return f; };
/** `size` bytes of filler with each `[offset, text]` placed in it. */
const filled = (size, ...puts) => { const b = Buffer.alloc(size, '.'); for (const [at, text] of puts) b.write(text, at, 'latin1'); return b.toString('latin1'); };

test('A_file_over_the_whole_read_limit_is_scanned_in_windows_and_a_token_anywhere_in_it_is_found_exactly_once_at_its_offset', () => {
  const real = fakeToken();
  const dummy = 'sk-ant-oat01-dummy-abc123def';
  // the offsets that matter for windows of 256 with an overlap of 64: the start, inside a window, straddling the first and second window ends, inside an overlap, the last bytes
  for (const at of [0, 100, 256 - real.length / 2, 256 - 1, 256, 512 - real.length + 3, 700, 4000 - real.length]) {
    const dir = tmpDir('scan-win');
    write(dir, 'box/out/big.log', filled(4000, [Math.floor(at), real]));
    const scan = scanSessionDir(dir, { real, limits: LIMITS });
    assert.deepEqual(scan.hits.map(h => [h.source, h.kind, h.offset]), [[path.join('box', 'out', 'big.log'), 'own-value', Math.floor(at)]], `token at ${at}`);
    assert.deepEqual(scan.streamed, [path.join('box', 'out', 'big.log')], 'a file read in windows is listed as such');
    assert.deepEqual(scan.tooLarge, []);
    assert.equal(scan.scanned, 1);
  }
  // the dummy straddling a window end is masked whole and is no hit; a real token next to it still is one
  const dir = tmpDir('scan-dummy');
  write(dir, 'big.log', filled(3000, [256 - 10, dummy], [1500, real]));
  const scan = scanSessionDir(dir, { real, dummy, limits: LIMITS });
  assert.deepEqual(scan.hits.map(h => h.offset), [1500], 'the dummy across a window end is masked, the real token is found');
});

test('A_file_the_scan_cannot_read_even_in_windows_is_a_high_finding_that_refutes_the_scan_not_a_quiet_list_entry', () => {
  const dir = tmpDir('scan-huge');
  const f = write(dir, 'box/out/huge.bin', 'x');
  truncateSync(f, LIMITS.maxStreamBytes + 1); // sparse: no real bytes are written
  const scan = scanSessionDir(dir, { real: fakeToken(), limits: LIMITS });
  const finding = scan.links.find(l => l.rel === path.join('box', 'out', 'huge.bin'));
  assert.equal(finding?.severity, 'high', JSON.stringify(scan));
  assert.equal(finding.kind, 'unscanned');
  assert.match(finding.why, /larger than/);
  assert.deepEqual(scan.tooLarge, [path.join('box', 'out', 'huge.bin')], 'and it stays listed');
});

test('A_small_file_is_read_whole_and_the_scan_reads_one_file_at_a_time_keeping_only_hits_and_counters', () => {
  v8.setFlagsFromString('--expose_gc');
  const gc = vm.runInNewContext('gc');
  const MIB = 1024 * 1024;
  const dir = tmpDir('scan-one');
  const real = fakeToken();
  for (let i = 0; i < 5; i += 1) write(dir, `f${i}.txt`, filled(6 * MIB, [10, i === 2 ? real : 'nothing-secret-here']));
  const external = [];
  const seen = [];
  const scan = scanSessionDir(dir, {
    real,
    limits: { ...LIMITS, maxScanBytes: 64 * MIB },
    hooks: { beforeRead: rel => { gc(); seen.push(rel); external.push(process.memoryUsage().arrayBuffers); } },
  });
  assert.equal(seen.length, 5, 'the hook saw every file, before its read');
  // before each read the earlier files' bytes are collectable: what is still held is at most about one file (6 MiB), never the 2-4 already scanned
  assert.ok(Math.max(...external) - external[0] < 8 * MIB, `array buffer bytes before each read: ${external.map(n => Math.round(n / MIB))} MiB`);
  assert.deepEqual(scan.hits.map(h => h.source), ['f2.txt']);
  assert.deepEqual(scan.streamed, []);
  assert.equal(scan.scanned, 5);
  assert.equal(JSON.stringify(scan).includes('nothing-secret-here'), false, 'no file content is in the result');
});
