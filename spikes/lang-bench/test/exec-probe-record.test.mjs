// T-L53 (parent D59): a probe record is NEVER overwritten. The default output is `inputs/exec-probe-<date>[-<n>].json` — the first of the day takes no suffix, the second `-2`, the third `-3` — and a run
// pointed at an existing path refuses (exit 3) and changes no byte. The two records of 2026-11-02 stay as they were, with their layer identities.
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { nextRecordPath, RecordExists, recordFileName, releaseRecord, reserveNextRecord, reserveRecord, RESERVE_ATTEMPTS } from '../lib/exec-probe/record-path.mjs';
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
  assert.equal(printable('a\u001b[31mred\rb\u009bX\u0000Y\u007fZ\u2028'), 'a[31mred\\rbXYZ');
  assert.equal(printable('tab\there\nline'), 'tab\\there\\nline', 'V6: \\t, \\n and \\r become visible escapes: no raw CR can overwrite a line and no fake line can be forged');
  assert.equal(printable('a\r\nb\rc'), 'a\\r\\nb\\rc');
  assert.doesNotMatch(printable('x\r\ny\tz\u001b[2K'), /[\u0000-\u001f]/, 'nothing raw is left');
  assert.equal(printable('héllo ✓ 日本'), 'héllo ✓ 日本', 'printable text is kept');
  const dir = owned.ownedDir(t, 'escpath');
  const evil = path.join(dir, 'rec\u001b[2J\rFAKE.json');
  writeFileSync(evil, '{}\n');
  const ran = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', evil, '--no-specs'], { encoding: 'utf8', timeout: 20_000 });
  assert.equal(ran.status, 3);
  assert.doesNotMatch(ran.stderr, /[\u0000-\u0009\u000b-\u001f\u007f-\u009f]/, 'no control character in the refusal');
  assert.match(ran.stderr, /FAKE\.json/, 'the visible text of the path is kept');
});

// V2 (fix batch 3): a reservation makes its folder; a path that cannot be created is a clear error, never an uncaught ENOENT.
test('A_reservation_creates_its_missing_folder_and_a_path_that_cannot_be_made_is_a_clear_error', t => {
  const dir = owned.ownedDir(t, 'reservedir');
  const deep = path.join(dir, 'a', 'b', 'exec-probe-2026-11-08.json');
  const held = reserveRecord(deep);
  assert.equal(statSync(deep).size, 0, 'the folders were made and the placeholder holds the path');
  releaseRecord(held);
  const next = reserveNextRecord({ inputsDir: path.join(dir, 'inputs-not-yet'), date: '2026-11-08' });
  assert.equal(path.basename(next.file), 'exec-probe-2026-11-08.json', 'reserveNextRecord makes sure inputsDir exists');
  const blocker = path.join(dir, 'file');
  writeFileSync(blocker, 'x');
  assert.throws(() => reserveRecord(path.join(blocker, 'sub', 'r.json')), error => error.exitCode === 4 && error.message.includes(path.join(blocker, 'sub', 'r.json')), 'a path that cannot be created names the path and carries exit 4');
});

test('A_probe_pointed_at_a_missing_folder_reserves_it_and_a_path_that_cannot_be_made_is_exit_4_with_the_path', t => {
  const dir = owned.ownedDir(t, 'clidir');
  const stale = path.join(dir, 'exec-layer.json');
  writeFileSync(stale, JSON.stringify({ execLayer: { treeSha256: '0'.repeat(64), files: [] } }));
  const missing = path.join(dir, 'not', 'yet', 'r.json');
  const ran = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', missing, '--pinned-identity', stale, '--no-specs'], { encoding: 'utf8', timeout: 30_000 });
  assert.equal(ran.status, 3, `the reservation worked, then the stale pin refused: ${ran.stderr}`);
  assert.match(ran.stderr, /layer-identity-mismatch/);
  assert.equal(existsSync(missing), false, 'and the empty placeholder was released');
  const blocker = path.join(dir, 'file');
  writeFileSync(blocker, 'x');
  const blocked = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', path.join(blocker, 'r.json'), '--no-specs'], { encoding: 'utf8', timeout: 30_000 });
  assert.equal(blocked.status, 4, blocked.stderr);
  assert.match(blocked.stderr, /file\/r\.json/);
  assert.doesNotMatch(blocked.stderr, /at .*\.mjs:\d+|ENOENT: no such file/, 'a clear message, no stack');
});

// V3: reserveNextRecord keeps its own index and a bound.
test('reserveNextRecord_keeps_its_own_increasing_index_and_ends_with_an_error_not_a_hang', { timeout: 10_000 }, () => {
  const tried = [];
  const always = file => { tried.push(path.basename(file)); throw new RecordExists(file); };
  assert.throws(() => reserveNextRecord({ inputsDir: '/i', date: '2026-11-09', reserve: always }), new RegExp(`${RESERVE_ATTEMPTS} .*exec-probe-2026-11-09`), 'a clear error naming the attempts and the date');
  assert.equal(tried.length, RESERVE_ATTEMPTS, 'bounded');
  assert.deepEqual(tried.slice(0, 3), ['exec-probe-2026-11-09.json', 'exec-probe-2026-11-09-2.json', 'exec-probe-2026-11-09-3.json'], 'the index rises on every refusal');
  assert.equal(new Set(tried).size, tried.length, 'never the same name twice: no re-ask from 1');
  assert.ok(RESERVE_ATTEMPTS <= 1000);
  let calls = 0;
  const thirdFree = file => { calls += 1; if (calls < 3) throw new RecordExists(file); return { file }; };
  assert.equal(path.basename(reserveNextRecord({ inputsDir: '/i', date: '2026-11-09', reserve: thirdFree }).file), 'exec-probe-2026-11-09-3.json');
});

// V1: every later failure releases an unwritten placeholder.
test('A_malformed_runner_pin_or_a_failing_start_leaves_no_empty_record_file', t => {
  const dir = owned.ownedDir(t, 'v1');
  const pin = path.join(dir, 'exec-layer.json');
  writeFileSync(pin, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } }));
  const bad = path.join(dir, 'runner.json');
  writeFileSync(bad, '{ not json');
  for (const [name, extra] of [['a malformed runner pin', ['--runner-pin', bad]], ['a missing runner pin', ['--runner-pin', path.join(dir, 'absent.json')]]]) {
    const out = path.join(dir, `${name.replace(/ /g, '-')}.json`);
    const ran = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, ...extra, '--no-specs'], { encoding: 'utf8', timeout: 30_000 });
    assert.notEqual(ran.status, 0, name);
    assert.ok([2, 3].includes(ran.status), `${name}: a clean exit status, ${ran.status}: ${ran.stderr}`);
    assert.doesNotMatch(ran.stderr, /at .*\.mjs:\d+/, `${name}: no stack`);
    assert.equal(existsSync(out), false, `${name}: no empty record is left`);
  }
});

// V5: a probe sent SIGTERM mid-run cleans up what it started before it exits.
test('A_probe_sent_SIGTERM_mid_run_releases_its_placeholder_and_leaves_no_scope_holder_or_view_behind', async t => {
  if (!needs(t)) return;
  const dir = owned.ownedDir(t, 'v5');
  const out = path.join(dir, 'record.json');
  const pin = path.join(dir, 'exec-layer.json');
  writeFileSync(pin, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } }));
  const TOKEN = 'benchsig'; // this probe's own owned token: other test files run beside this one and own `bench` units; only this probe's names are looked at
  const mine = () => {
    const units = spawnSync('systemctl', ['--user', 'list-units', '--all', '--plain', '--no-legend', '--full'], { encoding: 'utf8' }).stdout.split('\n').map(line => line.trim().split(/\s+/)[0]).filter(name => new RegExp(`^testsToDelete-.*-${TOKEN}-`).test(name));
    const procs = readdirSync('/proc').filter(entry => /^\d+$/.test(entry)).flatMap(pid => { try { return [readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0')[0]]; } catch { return []; } }).filter(name => new RegExp(`^testsToDelete-.*-${TOKEN}-`).test(name));
    return new Set([...units, ...procs]);
  };
  const before = mine();
  const child = spawn(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, '--owned-token', TOKEN, '--no-specs'], { stdio: ['ignore', 'pipe', 'pipe'] });
  t.after(() => child.kill('SIGKILL'));
  let stderr = '';
  child.stderr.on('data', chunk => { stderr += chunk; });
  const deadline = Date.now() + 120_000;
  while (!/bind list: start/.test(stderr) && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 100));
  assert.match(stderr, /network: start/, 'the probe is mid-run: its probe run (holder, scope, views) is open');
  assert.ok([...mine()].some(name => !before.has(name)), 'the probe\'s own units exist mid-run (the check below is not vacuous)');
  const closed = new Promise(resolve => child.once('close', (code, signal) => resolve({ code, signal })));
  child.kill('SIGTERM');
  const ended = await closed;
  assert.equal(ended.code, 143, `exit 143 after the cleanup (${JSON.stringify(ended)}): ${stderr.slice(-300)}`);
  assert.equal(existsSync(out), false, 'the placeholder is released');
  const left = [...mine()].filter(name => !before.has(name));
  assert.deepEqual(left, [], 'no scope, holder or helper process of this probe is left');
});
