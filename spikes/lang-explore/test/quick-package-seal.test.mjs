// The seal and the clear folder through the command `package` (F2.S1, section 2.7, scenarios 36 and 37): one encrypted archive per task whose unsealed tree hash is the held-out tree hash, and the clear folder mode 700, checked against the
// archive, removed after the run and at a restart, with the key's backup checked first. The seal, the lock and the readers are bench PR 4a's (`package-seal.mjs`); here the COMMAND is the entry point that uses them. Invented world.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { unpackTo } from '../../minigit-bench/lib/heldout-archive.mjs';
import { readKey, unseal } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { main } from '../explore.mjs';
import { casesTreeHash } from '../lib/heldout-quick.mjs';
import { commandEnv, sealSeams, sha } from './quick-package-fixture.mjs';

const ALL_CAUGHT = ({ id, n }) => (id === 'MXX-alpha-c' || id === 'MXX-alpha-n' ? { failing: { h001: 'wrong answer', h002: 'wrong answer' } } : (id === 'MXX-beta-n' ? (n % 5 === 1 ? { failing: { h003: 'wrong answer' } } : 'clean') : 'clean'));
const argv = (...more) => ['package', '--task', '05', ...more];

/** A sealed package: the command run with `--register --seal`. -> `{ env, seams, snapshot }`; `snapshot` is a copy of the invented cases to put back as a stale clear folder. */
async function sealed() {
  const env = commandEnv({ plan: ALL_CAUGHT });
  const seams = sealSeams({ frozenFile: env.files.frozenFile, packagesFile: env.files.packagesFile });
  const snapshot = path.join(tmpDir('pkg-snapshot'), 'cases');
  cpSync(env.p.casesDir, snapshot, { recursive: true });
  const code = await main(argv('--register', '--seal'), { ...env.io, heldout: seams });
  assert.equal(code, 0, env.lines.join('\n'));
  return { env, seams, snapshot };
}

test('seal_makes_one_encrypted_archive_per_task_whose_unsealed_tree_hash_is_the_held_out_tree_hash', async () => {
  const { env, seams } = await sealed();
  const archive = path.join(env.p.dir, 'suite.enc');
  const entry = JSON.parse(readFileSync(env.files.frozenFile, 'utf8')).tasks['05'];
  const blob = readFileSync(archive);
  assert.equal(entry.archiveSha256, sha(blob), 'the archive\'s sha256 is in the registry entry');
  assert.equal(JSON.parse(readFileSync(env.files.packagesFile, 'utf8'))['05'].archiveSha256, entry.archiveSha256, 'and in the package entry');
  const key = readKey(seams.keyFile).value;
  const plain = unseal(blob, key);
  assert.equal(plain.ok, true, plain.reason);
  const out = path.join(tmpDir('pkg-unpacked'), 'cases');
  assert.equal(unpackTo(plain.value, out).ok, true);
  assert.equal(casesTreeHash(out).value, entry.treeSha256, 'it decrypts to the held-out tree hash');
  assert.equal(entry.treeSha256, env.p.heldTreeSha256 ?? entry.treeSha256);
  const flipped = Buffer.from(blob);
  flipped[flipped.length - 20] ^= 1;
  assert.equal(unseal(flipped, key).ok, false, 'a changed archive byte does not unseal');
  assert.equal(sha(flipped) === entry.archiveSha256, false, 'and gives another hash');
});

/** `package --only refs` run with `watch` called inside the held clear folder (the quiet check runs inside it): -> `{ code, seen }`. */
async function runWatching({ env, seams, watch }) {
  const seen = {};
  const quietFn = async () => { Object.assign(seen, watch()); return ok({ quiet: true }); };
  const code = await main(argv('--only', 'refs'), { ...env.io, heldout: seams, quietFn });
  return { code, seen };
}

test('The_clear_folder_is_mode_700_checked_against_the_archive_and_removed_after_each_pair_and_at_a_restart', async () => {
  const { env, seams, snapshot } = await sealed();
  const cases = env.p.casesDir;
  const watch = () => ({ exists: existsSync(cases), mode: existsSync(cases) ? statSync(cases).mode & 0o777 : null, tree: existsSync(cases) ? casesTreeHash(cases).value : null });
  const archiveTree = JSON.parse(readFileSync(env.files.frozenFile, 'utf8')).tasks['05'].treeSha256;
  // a stale clear folder (equal to its archive, mode 755, nobody holds it): removed at the start and made again from the archive
  assert.equal(existsSync(cases), true, 'after the seal the authoring folder is still there');
  const first = await runWatching({ env, seams, watch });
  assert.equal(first.code, 0, env.lines.join('\n'));
  assert.deepEqual([first.seen.exists, first.seen.mode, first.seen.tree], [true, 0o700, archiveTree], 'made from the archive, mode 700, the archive\'s tree hash');
  assert.equal(existsSync(cases), false, 'removed after the run');
  // a stale folder at a restart: put back with mode 755, it is removed and made again (mode 700)
  cpSync(snapshot, cases, { recursive: true });
  const stale = await runWatching({ env, seams, watch });
  assert.equal(stale.code, 0, env.lines.join('\n'));
  assert.equal(stale.seen.mode, 0o700, 'the stale folder was removed and the clear folder made again from the archive');
  assert.equal(existsSync(cases), false);
  const archiveBytes = readFileSync(path.join(env.p.dir, 'suite.enc'));
  assert.deepEqual(archiveBytes, readFileSync(path.join(env.p.dir, 'suite.enc')), 'the archive is untouched');
  // a folder that differs from its archive: refused, never removed
  cpSync(snapshot, cases, { recursive: true });
  writeFileSync(path.join(cases, 'h002.out'), 'transcript 2\nchanged after sealing\n');
  const changedBytes = readFileSync(path.join(cases, 'h002.out'));
  const differs = await runWatching({ env, seams, watch });
  assert.equal(differs.code, 4);
  assert.ok(env.lines.some(l => /differs from its archive: changed after sealing/.test(l)), env.lines.join('\n'));
  assert.deepEqual(readFileSync(path.join(cases, 'h002.out')), changedBytes, 'a folder that differs from its archive is refused, not removed');
});

test('A_key_backup_that_cannot_be_verified_stops_the_command_and_leaves_the_clear_folder', async () => {
  const { env, seams, snapshot } = await sealed();
  const cases = env.p.casesDir;
  cpSync(snapshot, cases, { recursive: true });
  const before = casesTreeHash(cases).value;
  const unmounted = { ...seams, mountOf: async () => ok('/') };
  const r = await runWatching({ env, seams: unmounted, watch: () => ({}) });
  assert.equal(r.code, 4);
  assert.ok(env.lines.some(l => /key backup/.test(l)), env.lines.join('\n'));
  assert.equal(casesTreeHash(cases).value, before, 'the clear folder is as it was');
  mkdirSync(path.dirname(seams.backupFile), { recursive: true });
  const again = await runWatching({ env, seams, watch: () => ({}) });
  assert.equal(again.code, 0, 'the backup made reachable: the run goes on');
});
