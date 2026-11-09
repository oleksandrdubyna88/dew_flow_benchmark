// F2.S2 (d) and F2.S1 items 3 to 5, built by bench PR 4a: the seal of a quick task's clear held-out folder into ONE archive `heldout-NN/suite.enc`, registered in `inputs/heldout-frozen.json` as counts and hashes only; the registry
// writer (archive first, entry second, both resumable); and `registryMismatchProblem`, the check `packages.json`'s archive hash has to pass. The world is invented: a temp home, a key in a 700 folder, a backup folder inside an
// invented share; nothing real is read. The open, close and stale scenarios are in the neighbouring files.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import * as nodeFs from 'node:fs';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { sealTree } from '../../minigit-bench/lib/heldout-seal.mjs';
import { readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { entryOfSeal, writeEntry } from '../lib/heldout-registry.mjs';
import { registryMismatchProblem, sealPackage } from '../lib/package-seal.mjs';
import { quickTask } from './quick-leak-fixture.mjs';
import { filesUnder, TOKEN, world } from './quick-heldout-fixture.mjs';

const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const entryOf = w => JSON.parse(readFileSync(w.frozenFile, 'utf8')).tasks[w.taskId];
const archiveOf = w => path.join(w.heldoutDir, 'suite.enc');
const leftovers = dir => readdirSync(dir).filter(n => /^testsToDelete-|\.owner$|\.tmp$|^\.clear\.lock/.test(n));

/** A different valid archive at the archive's name: other cases, the same key. */
function plantOtherArchive(w) {
  const src = path.join(tmpDir('q-other'), 'cases');
  mkdirSync(src);
  writeFileSync(path.join(src, 'h001.in'), 'another invented case\n');
  writeFileSync(archiveOf(w), sealTree({ src, key: readKey(w.keyFile).value }).value.sealed);
}

test('The_archive_is_heldout_NN_suite_enc_and_no_other_name', async () => {
  const w = world({ over: { outDir: '/nowhere/suite.enc', archiveFile: '/nowhere/other.enc' } });
  const before = readdirSync(w.heldoutDir).sort();
  const r = await sealPackage(w.args);
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(readdirSync(w.heldoutDir).filter(n => !before.includes(n)), ['suite.enc'], 'the one new file of heldout-NN/ is the archive');
  assert.equal(entryOf(w).archive, 'heldout-04/suite.enc');
  assert.equal(readFileSync(w.manifestFile, 'utf8'), '{"coverage":"invented"}\n', 'the seal does not write manifest.json: it is F2.S1\'s');
  assert.equal(existsSync('/nowhere'), false, 'the seal takes no output path argument');
  assert.deepEqual(readdirSync(w.heldoutDir).filter(n => /sha256|hashes/.test(n)), [], 'none of v1\'s files: no suite.enc.sha256 and no hashes.txt line');
  assert.equal(statSync(archiveOf(w)).mode & 0o777, 0o644);
  assert.equal(entryOf(w).archiveSha256, sha(readFileSync(archiveOf(w))));
});

test('Sealing_over_an_existing_archive_of_other_bytes_is_refused', async () => {
  const w = world();
  assert.equal((await sealPackage(w.args)).ok, true);
  plantOtherArchive(w);
  const [planted, record] = [readFileSync(archiveOf(w)), readFileSync(w.frozenFile)];
  const r = await sealPackage(w.args);
  assert.equal(r.ok, false);
  assert.ok(r.reason.includes('task 04') && r.reason.includes(archiveOf(w)), `names the task and the archive path: ${r.reason}`);
  assert.deepEqual(readFileSync(archiveOf(w)), planted, 'the planted file is not touched');
  assert.deepEqual(readFileSync(w.frozenFile), record, 'the entry is unchanged');
  const bare = world({ taskId: '05' });
  writeFileSync(path.join(bare.heldoutDir, 'suite.enc'), planted);
  const none = await sealPackage(bare.args);
  assert.equal(none.ok, false, 'an archive of other bytes with NO entry is no archive of these cases either');
  assert.match(none.reason, /task 05/);
});

test('Sealing_publishes_the_archive_once_then_writes_the_registry_entry', async () => {
  const calls = [];
  const fs = Object.fromEntries(Object.entries(nodeFs).filter(([, v]) => typeof v === 'function').map(([name, fn]) => [name, (...a) => { calls.push([name, ...a.filter(x => typeof x === 'string')]); return fn(...a); }]));
  const w = world();
  let atHook;
  const r = await sealPackage({ ...w.args, fs, hooks: { afterArchive: () => { atHook = [existsSync(archiveOf(w)), existsSync(w.frozenFile)]; } } });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(atHook, [true, false], 'the archive exists when the entry is not yet written');
  const at = (name, test) => calls.findIndex(c => c[0] === name && test(c));
  const link = at('linkSync', c => c[2] === archiveOf(w));
  const tmpOpen = at('openSync', c => /\/\.heldout-frozen\.json\.\d+\.[0-9a-f]{8}\.tmp$/.test(c[1]));
  const flush = calls.findIndex((c, i) => i > tmpOpen && c[0] === 'fsyncSync');
  const rename = at('renameSync', c => c[2] === w.frozenFile);
  assert.ok(link >= 0 && tmpOpen > link && flush > tmpOpen && rename > flush, `archive hard link, then the registry temp file, flushed, then renamed: ${[link, tmpOpen, flush, rename]}`);
  assert.equal(entryOf(w).archiveSha256, sha(readFileSync(archiveOf(w))));
  assert.deepEqual(readdirSync(path.dirname(w.frozenFile)), ['heldout-frozen.json'], 'no temporary file is left beside the registry');
});

test('A_seal_killed_after_the_archive_and_before_the_entry_resumes_by_writing_the_entry_when_the_archive_unseals_to_todays_cases', async () => {
  const w = world();
  await assert.rejects(sealPackage({ ...w.args, hooks: { afterArchive: () => { throw new Error('planted fault'); } } }), /planted fault/);
  assert.equal(existsSync(archiveOf(w)), true, 'the archive was published');
  assert.equal(existsSync(w.frozenFile), false, 'and there is no entry');
  assert.equal(existsSync(path.join(w.heldoutDir, '.clear.lock')), false, 'the lock was released');
  const archive = readFileSync(archiveOf(w));
  const r = await sealPackage(w.args);
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.state, 'resumed');
  assert.deepEqual(readFileSync(archiveOf(w)), archive, 'the archive is the same bytes');
  assert.equal(entryOf(w).archiveSha256, sha(archive));
  assert.ok(w.spies.unseal >= 1, 'the resume opened the archive to compare it with today\'s cases');
  assert.deepEqual(leftovers(w.heldoutDir), [], 'no temporary folder, claim or lock is left');
});

test('A_seal_killed_after_the_archive_and_before_the_entry_refuses_and_names_the_task_when_it_does_not', async () => {
  const w = world();
  await assert.rejects(sealPackage({ ...w.args, hooks: { afterArchive: () => { throw new Error('planted fault'); } } }), /planted fault/);
  const [archive, before] = [readFileSync(archiveOf(w)), readFileSync(path.join(w.casesDir, 'h001.in'))];
  writeFileSync(path.join(w.casesDir, 'h001.in'), 'one pair changed between the kill and the resume\n');
  const changed = readFileSync(path.join(w.casesDir, 'h001.in'));
  const r = await sealPackage(w.args);
  assert.equal(r.ok, false);
  assert.match(r.reason, /task 04/);
  assert.deepEqual(readFileSync(archiveOf(w)), archive, 'the archive is unchanged');
  assert.equal(existsSync(w.frozenFile), false, 'no entry is written');
  assert.deepEqual(readFileSync(path.join(w.casesDir, 'h001.in')), changed, 'cases/ is unchanged');
  assert.notDeepEqual(changed, before);
  assert.deepEqual(leftovers(w.heldoutDir), [], 'the temporary folder of the comparison is removed');
  rmSync(path.join(w.casesDir), { recursive: true });
  const gone = await sealPackage(w.args);
  assert.equal(gone.ok, false, 'with no cases/ the resume cannot compare: it refuses naming the task, and never trusts the archive by its authentication alone');
  assert.match(gone.reason, /task 04/);
});

test('A_second_seal_with_an_equal_entry_is_a_no_op', async () => {
  const w = world();
  assert.equal((await sealPackage(w.args)).ok, true);
  const [archive, mtime, record] = [readFileSync(archiveOf(w)), statSync(archiveOf(w)).mtimeMs, readFileSync(w.frozenFile)];
  const again = await sealPackage({ ...w.args, now: () => new Date('2027-01-01T00:00:00.000Z') });
  assert.equal(again.ok, true, again.reason);
  assert.equal(again.value.state, 'unchanged');
  assert.deepEqual(readFileSync(archiveOf(w)), archive);
  assert.equal(statSync(archiveOf(w)).mtimeMs, mtime, 'the archive\'s mtime is unchanged');
  assert.deepEqual(readFileSync(w.frozenFile), record, 'the registry file\'s bytes are unchanged (the entry keeps its first date)');
  assert.deepEqual(readdirSync(path.dirname(w.frozenFile)), ['heldout-frozen.json']);
  writeFileSync(path.join(w.casesDir, 'h003.out'), 'changed after sealing\n');
  const moved = await sealPackage(w.args);
  assert.equal(moved.ok, false, 'control: the same entry with cases that changed is a re-seal, which is a new identity and is not built here');
  assert.match(moved.reason, /task 04/);
  assert.deepEqual(readFileSync(archiveOf(w)), archive);
});

test('heldout_check_refuses_a_packages_json_entry_whose_archiveSha256_differs_from_the_registry_and_accepts_an_equal_one_or_none', async () => {
  const w = world();
  assert.equal((await sealPackage(w.args)).ok, true);
  const real = entryOf(w).archiveSha256;
  const dir = tmpDir('q-packages');
  const packagesFile = path.join(dir, 'packages.json');
  const ask = () => registryMismatchProblem({ task: w.task, packagesFile, frozenFile: w.frozenFile });
  assert.equal(ask(), null, 'no packages.json: no check');
  writeFileSync(packagesFile, JSON.stringify({ tasks: { 99: { archiveSha256: 'b'.repeat(64) } } }));
  assert.equal(ask(), null, 'no entry for the task: no check');
  writeFileSync(packagesFile, JSON.stringify({ tasks: { '04': { archiveSha256: real } } }));
  assert.equal(ask(), null, 'an equal entry');
  const bent = `${real.slice(0, 63)}${real.endsWith('0') ? '1' : '0'}`;
  writeFileSync(packagesFile, JSON.stringify({ tasks: { '04': { archiveSha256: bent } } }));
  const problem = ask();
  assert.match(problem, /task 04/);
  assert.ok(problem.includes('packages.json') && problem.includes('heldout-frozen.json') && problem.includes('archiveSha256'), `names both files and the field: ${problem}`);
  writeFileSync(packagesFile, '{ not json');
  assert.match(ask(), /packages\.json.*cannot be read/, 'a packages.json that cannot be read is a refusal, never "no check"');
});

test('The_seal_refuses_a_cases_folder_that_holds_a_link_or_a_non_regular_file_and_writes_no_archive', async () => {
  const w = world();
  const keyBefore = readFileSync(w.keyFile);
  symlinkSync(w.keyFile, path.join(w.casesDir, 'h004.in'));
  const r = await sealPackage(w.args);
  assert.equal(r.ok, false);
  assert.match(r.reason, /h004\.in is a link/, 'the entry is named');
  assert.equal(existsSync(archiveOf(w)), false, 'no archive');
  assert.equal(existsSync(w.frozenFile), false, 'no entry');
  assert.deepEqual(readFileSync(w.keyFile), keyBefore, 'the key file is untouched');
  assert.deepEqual(leftovers(w.heldoutDir), []);
});

test('The_record_holds_counts_and_hashes_and_no_case_id_or_trap_name', async () => {
  const w = world();
  assert.equal((await sealPackage(w.args)).ok, true);
  const text = readFileSync(w.frozenFile, 'utf8');
  const entry = entryOf(w);
  assert.deepEqual(Object.keys(entry).sort(), ['archive', 'archiveSha256', 'caseCount', 'keySha256', 'manifestSha256', 'sealedOn', 'trapCount', 'treeSha256']);
  for (const [k, v] of Object.entries(entry)) {
    const shape = /Sha256$/.test(k) ? /^[0-9a-f]{64}$/ : k === 'archive' ? /^heldout-04\/suite\.enc$/ : k === 'sealedOn' ? /^\d{4}-\d{2}-\d{2}$/ : /^\d+$/;
    assert.match(String(v), shape, k);
  }
  assert.deepEqual([entry.caseCount, entry.trapCount, entry.sealedOn], [3, 1, '2026-11-09']);
  for (const secret of [TOKEN, 'h001', 'h002', 'h003', readKey(w.keyFile).value.toString('hex'), readKey(w.keyFile).value.toString('base64')]) assert.equal(text.includes(secret), false, 'the record holds neither a case id, a trap name, the invented token nor the key');
  assert.equal(entry.keySha256, sha(readKey(w.keyFile).value));
  assert.deepEqual(filesUnder(path.dirname(w.frozenFile)), ['heldout-frozen.json']);
});

test('A_seal_without_its_manifest_is_refused_and_the_entry_carries_the_manifests_hash', async () => {
  const w = world();
  const manifest = readFileSync(w.manifestFile);
  rmSync(w.manifestFile);
  const r = await sealPackage(w.args);
  assert.equal(r.ok, false);
  assert.ok(r.reason.includes(w.manifestFile) && /task 04/.test(r.reason), r.reason);
  assert.deepEqual([existsSync(archiveOf(w)), existsSync(w.frozenFile), existsSync(w.backupFile)], [false, false, false], 'no archive, no entry, no key copy');
  writeFileSync(w.manifestFile, manifest);
  assert.equal((await sealPackage(w.args)).ok, true, 'control: the manifest is there');
  assert.equal(entryOf(w).manifestSha256, sha(manifest));
});

test('The_first_seal_copies_the_key_to_the_backup_before_the_archive_and_refuses_a_copy_it_cannot_trust', async () => {
  const w = world();
  const [elsewhere, misplaced] = [world(), world({ taskId: '05' })];
  const wrongMount = await sealPackage({ ...elsewhere.args, mountOf: async () => ({ ok: true, value: '/' }) });
  assert.equal(wrongMount.ok, false);
  assert.match(wrongMount.reason, /not on the mounted share/);
  assert.deepEqual([existsSync(path.join(elsewhere.heldoutDir, 'suite.enc')), existsSync(elsewhere.backupFile), existsSync(elsewhere.frozenFile)], [false, false, false], 'a folder off the share gets no copy, no archive and no entry');
  writeFileSync(misplaced.backupFile, 'not the key at all');
  const differs = await sealPackage(misplaced.args);
  assert.equal(differs.ok, false, 'a copy that is there and differs is verified, refused and not overwritten');
  assert.equal(readFileSync(misplaced.backupFile, 'utf8'), 'not the key at all');
  assert.equal(existsSync(path.join(misplaced.heldoutDir, 'suite.enc')), false);
  const r = await sealPackage(w.args);
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(readFileSync(w.backupFile), readKey(w.keyFile).value, 'control: the copy is the key');
  assert.equal(statSync(w.backupFile).mode & 0o777, 0o600);
  assert.equal(w.spies.mounts >= 1, true, 'the mount was asked before the copy');
});

test('The_registry_writer_writes_an_entry_once_keeps_the_others_and_refuses_another_entry_or_a_record_it_cannot_read', () => {
  const file = path.join(tmpDir('q-registry'), 'heldout-frozen.json');
  const entry = (n, over = {}) => entryOfSeal({ archive: `heldout-0${n}/suite.enc`, archiveSha256: 'a'.repeat(64), treeSha256: 'b'.repeat(64), caseCount: 3, trapCount: 1, manifestSha256: 'c'.repeat(64), keySha256: 'd'.repeat(64), sealedOn: '2026-11-09', ...over });
  assert.deepEqual(writeEntry({ frozenFile: file, task: quickTask('05'), entry: entry(5) }), { ok: true, value: 'written' });
  assert.deepEqual(writeEntry({ frozenFile: file, task: quickTask('04'), entry: entry(4) }), { ok: true, value: 'written' });
  assert.deepEqual(Object.keys(JSON.parse(readFileSync(file, 'utf8')).tasks), ['04', '05'], 'both tasks, in task order');
  const text = readFileSync(file, 'utf8');
  assert.deepEqual(writeEntry({ frozenFile: file, task: quickTask('04'), entry: entry(4) }), { ok: true, value: 'unchanged' });
  const other = writeEntry({ frozenFile: file, task: quickTask('04'), entry: entry(4, { archiveSha256: 'e'.repeat(64) }) });
  assert.equal(other.ok, false);
  assert.match(other.reason, /task 04/);
  assert.equal(readFileSync(file, 'utf8'), text, 'the record is unchanged by the refusal');
  writeFileSync(file, '{ not json');
  const unreadable = writeEntry({ frozenFile: file, task: quickTask('06'), entry: entry(6) });
  assert.equal(unreadable.ok, false, 'a record that cannot be read is never overwritten: it may hold the entry');
  assert.equal(readFileSync(file, 'utf8'), '{ not json');
  const flat = path.join(tmpDir('q-registry-flat'), 'heldout-frozen.json');
  writeFileSync(flat, JSON.stringify({ '04': entry(4) }));
  assert.equal(writeEntry({ frozenFile: flat, task: quickTask('05'), entry: entry(5) }).ok, true);
  assert.deepEqual(Object.keys(JSON.parse(readFileSync(flat, 'utf8')).tasks), ['04', '05'], 'the flat layout is read, and written in the one layout of the writer');
});
