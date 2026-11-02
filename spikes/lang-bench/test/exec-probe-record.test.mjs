// T-L53 (parent D59): a probe record is NEVER overwritten. The default output is `inputs/exec-probe-<date>[-<n>].json` — the first of the day takes no suffix, the second `-2`, the third `-3` — and a run
// pointed at an existing path refuses (exit 3) and changes no byte. The two records of 2026-11-02 stay as they were, with their layer identities.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { nextRecordPath, RecordExists, recordFileName } from '../lib/exec-probe/record-path.mjs';
import { writeRecord } from '../lib/exec-probe/probe.mjs';
import { spikeDir } from './support/exec-layer-host.mjs';
import { owned } from './support/owned.mjs';

const sha = file => createHash('sha256').update(readFileSync(file)).digest('hex');

test('The_default_record_name_is_the_date_then_dash_2_then_dash_3_and_never_an_existing_file', () => {
  assert.equal(recordFileName('2026-11-02', 1), 'exec-probe-2026-11-02.json');
  assert.equal(recordFileName('2026-11-02', 2), 'exec-probe-2026-11-02-2.json');
  const taken = new Set();
  const exists = file => taken.has(file);
  const next = () => { const file = nextRecordPath({ inputsDir: '/i', date: '2026-11-03', exists }); taken.add(file); return path.basename(file); };
  assert.deepEqual([next(), next(), next(), next()], ['exec-probe-2026-11-03.json', 'exec-probe-2026-11-03-2.json', 'exec-probe-2026-11-03-3.json', 'exec-probe-2026-11-03-4.json']);
  const gap = new Set(['/i/exec-probe-2026-11-04.json', '/i/exec-probe-2026-11-04-3.json']);
  assert.equal(path.basename(nextRecordPath({ inputsDir: '/i', date: '2026-11-04', exists: file => gap.has(file) })), 'exec-probe-2026-11-04-2.json', 'the first free number');
  assert.throws(() => nextRecordPath({ inputsDir: '/i', date: '11/04/2026', exists }), /date/);
});

test('A_record_is_written_exclusively_so_an_existing_file_keeps_every_byte_and_the_write_refuses', t => {
  const dir = owned.ownedDir(t, 'records');
  const file = path.join(dir, 'exec-probe-2026-11-05.json');
  writeRecord(file, { first: true });
  const before = sha(file);
  assert.throws(() => writeRecord(file, { second: true }), error => error instanceof RecordExists && /exec-probe-2026-11-05\.json/.test(error.message));
  assert.equal(sha(file), before, 'not one byte changed');
  assert.deepEqual(readdirSync(dir), ['exec-probe-2026-11-05.json'], 'and no temporary file is left');
});

test('A_probe_pointed_at_an_existing_path_refuses_with_exit_3_before_it_runs_and_changes_no_byte', t => {
  const dir = owned.ownedDir(t, 'existing');
  const file = path.join(dir, 'record.json');
  writeFileSync(file, '{"keep":"me"}\n');
  const before = sha(file);
  const ran = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', file, '--no-specs'], { encoding: 'utf8', timeout: 20_000 });
  assert.equal(ran.status, 3, ran.stderr);
  assert.match(ran.stderr, /exec-probe: .*record\.json already exists.*never overwritten/);
  assert.equal(sha(file), before, 'the record is byte-equal');
  assert.equal(ran.stdout, '', 'nothing was probed');
});

test('The_two_records_of_2026_11_02_are_byte_equal_with_their_original_layer_identities', () => {
  const probe = path.join(spikeDir, 'inputs', 'exec-probe-2026-11-02.json');
  const experiment = path.join(spikeDir, 'inputs', 'opk-experiment-2026-11-02.json');
  assert.equal(JSON.parse(readFileSync(probe, 'utf8')).layerIdentity.slice(0, 4), '2697');
  assert.match(JSON.parse(readFileSync(probe, 'utf8')).layerIdentity, /^2697325850ae/);
  assert.match(readFileSync(experiment, 'utf8'), /79b85a37/);
  assert.equal(sha(probe), PINNED_SHA['exec-probe-2026-11-02.json'], 'the first probe record is the committed bytes of 2026-11-02');
  assert.equal(sha(experiment), PINNED_SHA['opk-experiment-2026-11-02.json'], 'the OP-K experiment record is the committed bytes of 2026-11-02');
});

const PINNED_SHA = Object.freeze({
  'exec-probe-2026-11-02.json': '23da32e06dafc67a6fbfd874c5e7cd13446f610d1be04492b6c7dff18fffad5a',
  'opk-experiment-2026-11-02.json': '72abb42aedb2a072f050d4eecfa88615cb592ce249a0724c3bad40ab1ff1696d',
});
