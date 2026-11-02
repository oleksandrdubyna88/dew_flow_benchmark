// T-L53 (parent D59): a probe record is NEVER overwritten. The default output is `inputs/exec-probe-<date>[-<n>].json` — the first of the day takes no suffix, the second `-2`, the third `-3` — and a run
// pointed at an existing path refuses (exit 3) and changes no byte. The two records of 2026-11-02 stay as they were, with their layer identities.
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { nextRecordPath, RecordExists, recordFileName, releaseRecord, reserveNextRecord, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { computeIdentity, LAYER_DIR } from '../lib/exec-layer/identity.mjs';
import { printable } from '../lib/exec-probe/printable.mjs';
import { writeRecord } from '../lib/exec-probe/probe.mjs';
import { needs, spikeDir } from './support/exec-layer-host.mjs';
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

// P1 (coai round, fix batch 2): the record path is RESERVED before the probe starts, so two probes started together never both run the full workload toward one path.
test('A_record_path_is_reserved_exclusively_before_probing_and_a_failed_probe_releases_it', t => {
  const dir = owned.ownedDir(t, 'reserve');
  const file = path.join(dir, 'exec-probe-2026-11-06.json');
  const held = reserveRecord(file);
  assert.equal(statSync(file).size, 0, 'an empty placeholder holds the path');
  assert.throws(() => reserveRecord(file), RecordExists, 'the second reservation is refused');
  assert.throws(() => writeRecord(file, { x: 1 }), RecordExists, 'and the exclusive write still refuses a path that is held without the reservation');
  writeRecord(file, { x: 1 }, held);
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), { x: 1 }, 'the holder fills its own placeholder');
  releaseRecord(held);
  assert.equal(existsSync(file), true, 'a written record is never removed by a release');
  const failed = reserveRecord(path.join(dir, 'failed.json'));
  releaseRecord(failed);
  assert.equal(existsSync(path.join(dir, 'failed.json')), false, 'an empty placeholder is removed on failure: no empty record is left');
});

test('The_next_default_path_is_reserved_at_once_so_two_default_probes_take_two_names', t => {
  const dir = owned.ownedDir(t, 'reservenext');
  const first = reserveNextRecord({ inputsDir: dir, date: '2026-11-07' });
  const second = reserveNextRecord({ inputsDir: dir, date: '2026-11-07' });
  const third = reserveNextRecord({ inputsDir: dir, date: '2026-11-07' });
  assert.deepEqual([first, second, third].map(h => path.basename(h.file)), ['exec-probe-2026-11-07.json', 'exec-probe-2026-11-07-2.json', 'exec-probe-2026-11-07-3.json']);
});

test('Two_probes_started_together_never_both_probe_toward_one_path_the_second_is_refused_exit_3_before_probing_and_a_failed_probe_leaves_no_record', async t => {
  if (!needs(t)) return;
  const dir = owned.ownedDir(t, 'race');
  const out = path.join(dir, 'record.json');
  const pin = path.join(dir, 'exec-layer.json');
  writeFileSync(pin, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } })); // the live tree: this test is about the reservation, not the pin
  const first = spawn(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, '--no-specs'], { stdio: ['ignore', 'pipe', 'pipe'] });
  t.after(() => first.kill('SIGKILL'));
  const deadline = Date.now() + 15_000;
  while (!existsSync(out) && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 50));
  assert.ok(existsSync(out), 'the first probe reserved its path at the start');
  const second = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, '--no-specs'], { encoding: 'utf8', timeout: 20_000 });
  assert.equal(second.status, 3, second.stderr);
  assert.match(second.stderr, /already exists|reserved/);
  assert.equal(second.stdout, '', 'refused before probing: not one probe line');
  assert.doesNotMatch(second.stderr, /: start$/m, 'and no progress line of a started probe');
  const closed = new Promise(resolve => first.once('close', resolve));
  first.kill('SIGTERM');
  await closed;
  assert.equal(existsSync(out), false, 'a probe stopped before it wrote leaves no empty record');
  const missingPolicy = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, '--policy', path.join(dir, 'absent.json'), '--no-specs'], { encoding: 'utf8', timeout: 60_000 });
  assert.equal(missingPolicy.status, 2, missingPolicy.stderr);
  assert.equal(existsSync(out), false, 'a probe that failed to start leaves no empty record either');
});

// P2: a control character from a path or a program must not reach the terminal raw.
test('Progress_and_refusal_text_carries_no_control_character_ESC_and_CR_included', t => {
  assert.equal(printable('ok'), 'ok');
  assert.equal(printable('a\u001b[31mred\rb\u009bX\u0000Y\u007fZ\u2028'), 'a[31mredbXYZ');
  assert.equal(printable('tab\there\nline'), 'tabhereline', 'tab and newline too: a line is one line');
  assert.equal(printable('héllo ✓ 日本'), 'héllo ✓ 日本', 'printable text is kept');
  const dir = owned.ownedDir(t, 'escpath');
  const evil = path.join(dir, 'rec\u001b[2J\rFAKE.json');
  writeFileSync(evil, '{}\n');
  const ran = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', evil, '--no-specs'], { encoding: 'utf8', timeout: 20_000 });
  assert.equal(ran.status, 3);
  assert.doesNotMatch(ran.stderr, /[\u0000-\u0009\u000b-\u001f\u007f-\u009f]/, 'no control character in the refusal');
  assert.match(ran.stderr, /FAKE\.json/, 'the visible text of the path is kept');
});
