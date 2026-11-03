// D1 (fix batch 9): N real probe processes started together, each running the command's own end (`endOfProbe`) on one shared copy of the pins.
//   against ONE STALE LOCK: none of them stamps, every record says exit 2 (and names the manual step), the lock is byte-equal and the pins are unchanged;
//   with NO LOCK: exactly one stamps (the holder keeps the lock while it commits, so no late probe can take it after it), the others record exit 2 "another probe holds the stamp lock".
// Rewritten from Three_probes_interleaving_leave_at_most_one_probe_that_may_stamp (exec-probe-stamp-lock.test.mjs): the retire that test interleaved is gone.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { owned } from './support/owned.mjs';

const REAL_INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const N = 6;
const mod = name => JSON.stringify(new URL(`../lib/exec-probe/${name}.mjs`, import.meta.url).href);
const RECORD = { probedOn: '2026-11-03T00:00:00.000Z', host: { hostname: 'h', kernel: 'k' }, lines: [{ id: 'a', ok: true }], exit: { code: 0, halt: null } };

/** A private copy of the pins, and one record file and one staging folder per probe. */
function setup(t) {
  const root = owned.ownedDir(t, 'many');
  const inputsDir = path.join(root, 'inputs');
  fs.mkdirSync(path.join(inputsDir, 'specs.d'), { recursive: true });
  for (const name of fs.readdirSync(path.join(REAL_INPUTS, 'specs.d'))) fs.copyFileSync(path.join(REAL_INPUTS, 'specs.d', name), path.join(inputsDir, 'specs.d', name));
  fs.copyFileSync(path.join(REAL_INPUTS, 'specs.json'), path.join(inputsDir, 'specs.json'));
  const pins = () => ({ layer: fs.readFileSync(path.join(inputsDir, 'specs.d', 'exec-layer.json'), 'utf8'), specs: fs.readFileSync(path.join(inputsDir, 'specs.json'), 'utf8') });
  return { root, inputsDir, pins, lock: path.join(inputsDir, '.stamp.lock') };
}

/**
 * One probe process (N6: a ready handshake, no fixed start time): it prints `ready` when it is loaded, waits for `go` on its stdin, runs `endOfProbe` and prints `{ code }`. A commit rename takes 500 ms, so the
 * holder keeps the lock while the others try. `{ ready, go(), done }`: `ready` resolves when the child says so, `go()` releases it, `done` resolves with its result.
 */
function probeProcess({ s, index }) {
  const dir = path.join(s.root, `p${index}`);
  fs.mkdirSync(path.join(dir, 'stage'), { recursive: true });
  const file = path.join(dir, 'exec-probe-x.json');
  const child = spawn(process.execPath, ['--input-type=module', '-e', `
    import * as fs from 'node:fs';
    import { endOfProbe } from ${mod('command-end')};
    import { makeReservation, reserveRecord } from ${mod('record-path')};
    const reservation = makeReservation(reserveRecord(${JSON.stringify(file)}));
    const io = { ...fs, renameSync: (from, to) => { if (String(to).endsWith('exec-layer.json')) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 500); return fs.renameSync(from, to); } };
    process.stdout.write('ready\\n');
    await new Promise(resolve => process.stdin.once('data', resolve));
    const code = endOfProbe({ record: ${JSON.stringify(RECORD)}, reservation, noSpecs: false, inputsDir: ${JSON.stringify(s.inputsDir)}, stageDir: ${JSON.stringify(path.join(dir, 'stage'))}, say: () => {}, report: () => {}, io });
    process.stdout.write(JSON.stringify({ code }));
  `], { stdio: ['pipe', 'pipe', 'inherit'] });
  let out = '';
  let signalReady;
  const ready = new Promise(resolve => { signalReady = resolve; });
  child.stdout.on('data', chunk => { out += chunk; if (out.includes('ready\n')) signalReady(); });
  const done = new Promise(resolve => child.once('close', () => resolve({ ...JSON.parse(out.replace('ready\n', '') || '{"code":"no output"}'), record: JSON.parse(fs.readFileSync(file, 'utf8')) })));
  return { ready, go: () => child.stdin.end('go\n'), done };
}

/** N probes: all are loaded and ready first, then all are released together. */
async function together(s) {
  const probes = Array.from({ length: N }, (_, index) => probeProcess({ s, index }));
  await Promise.all(probes.map(probe => probe.ready));
  probes.forEach(probe => probe.go());
  return Promise.all(probes.map(probe => probe.done));
}

const tmpFolders = s => [s.inputsDir, path.join(s.inputsDir, 'specs.d')].filter(dir => fs.existsSync(path.join(dir, '.exec-probe-tmp')));

test('N_probes_started_together_against_one_stale_lock_none_stamps_every_record_says_2_and_the_lock_is_unchanged', async t => {
  const s = setup(t);
  fs.writeFileSync(s.lock, '999999 1');
  const before = s.pins();
  const results = await together(s);
  assert.deepEqual(results.map(result => result.code), Array(N).fill(2), 'every probe exits 2');
  results.forEach(result => {
    assert.equal(result.record.exit.code, 2, 'every record says exit 2');
    assert.match(result.record.exit.stampError, /a probe that was killed left the stamp lock .*\.stamp\.lock \(pid 999999, start time 1, now gone\)\. If no probe is running, remove it: rm /);
  });
  assert.equal(fs.readFileSync(s.lock, 'utf8'), '999999 1', 'the lock is byte-equal');
  assert.deepEqual(s.pins(), before, 'no pin is stamped');
  assert.deepEqual(tmpFolders(s), [], 'no tmp folder is left');
});

test('N_probes_started_together_with_no_lock_exactly_one_stamps', async t => {
  const s = setup(t);
  const before = s.pins();
  const results = await together(s);
  assert.equal(results.filter(result => result.code === 0).length, 1, `exactly one probe stamps: ${results.map(result => result.code).join(',')}`);
  results.filter(result => result.code !== 0).forEach(result => {
    assert.equal(result.code, 2);
    assert.match(result.record.exit.stampError, /another probe holds the stamp lock \(pid \d+\) at .*\.stamp\.lock/, 'the others say who holds it');
  });
  assert.match(s.pins().layer, /"probedOn": "2026-11-03T00:00:00.000Z"/, 'the one stamp is in the pin');
  assert.notEqual(s.pins().layer, before.layer);
  assert.equal(fs.existsSync(s.lock), false, 'the holder released the lock');
  assert.deepEqual(tmpFolders(s), [], 'no tmp folder is left');
});
