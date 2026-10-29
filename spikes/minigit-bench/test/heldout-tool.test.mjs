import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, statSync, utimesSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { packDir } from '../lib/heldout-archive.mjs';
import { runBounded } from '../lib/run.mjs';
import { main } from '../heldout-tool.mjs';
import { homeDir, pythonGate, tmpDir } from './helpers.mjs';
import { writeToySuite } from './toy-suite.mjs';

const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const HASH_LINE = /^([0-9a-f]{64}) {2}heldout\/suite\.enc$/m;

/** Runs the tool in-process; resolves { code, out }. */
async function tool(...argv) {
  const lines = [];
  const code = await main(argv, line => lines.push(line));
  return { code, out: lines.join('\n') };
}

function world() {
  const root = tmpDir('tool');
  const src = path.join(root, 'src');
  writeToySuite(src);
  const keyFile = path.join(root, 'k', 'heldout.key');
  const outDir = path.join(root, 'heldout');
  const hashes = path.join(root, 'hashes.txt');
  writeFileSync(hashes, `${'a'.repeat(64)}  upstream/one.txt\n`);
  return { root, src, keyFile, outDir, hashes, workRoot: homeDir('tool') };
}

async function sealedWorld() {
  const w = world();
  assert.equal((await tool('keygen', '--key-file', w.keyFile)).code, 0);
  const sealed = await tool('seal', '--src', w.src, '--key-file', w.keyFile, '--out-dir', w.outDir, '--hashes', w.hashes);
  assert.equal(sealed.code, 0, sealed.out);
  return w;
}

test('Keygen_writes_a_key_once_and_refuses_the_second_time_with_the_key_untouched', async () => {
  const w = world();
  const first = await tool('keygen', '--key-file', w.keyFile);
  assert.equal(first.code, 0, first.out);
  assert.equal(statSync(w.keyFile).mode & 0o777, 0o600);
  const before = readFileSync(w.keyFile);
  const second = await tool('keygen', '--key-file', w.keyFile);
  assert.equal(second.code, 1);
  assert.match(second.out, /already exists/);
  assert.deepEqual(readFileSync(w.keyFile), before);
  assert.equal(first.out.includes(before.toString('hex')), false, 'the key is never printed');
});

test('Seal_writes_the_ciphertext_its_hash_the_manifest_and_one_hashes_line', async () => {
  const w = await sealedWorld();
  const enc = readFileSync(path.join(w.outDir, 'suite.enc'));
  assert.equal(readFileSync(path.join(w.outDir, 'suite.enc.sha256'), 'utf8'), `${sha(enc)}  suite.enc\n`);
  const manifest = JSON.parse(readFileSync(path.join(w.outDir, 'manifest.json'), 'utf8'));
  assert.deepEqual(Object.keys(manifest).sort(), ['format', 'm', 'n1', 'n2', 'plaintextSha256', 'sealedOn']);
  assert.equal(manifest.plaintextSha256, sha(packDir(w.src).value));
  assert.deepEqual([manifest.n1, manifest.n2, manifest.m], [4, 1, 2]);
  assert.match(manifest.sealedOn, /^\d{4}-\d{2}-\d{2}$/);
  const hashes = readFileSync(w.hashes, 'utf8');
  assert.equal(HASH_LINE.exec(hashes)[1], sha(enc));
  assert.ok(hashes.startsWith(`${'a'.repeat(64)}  upstream/one.txt\n`), 'the other lines are kept');
  const again = await tool('seal', '--src', w.src, '--key-file', w.keyFile, '--out-dir', w.outDir, '--hashes', w.hashes);
  assert.equal(again.code, 0, again.out);
  assert.equal(readFileSync(w.hashes, 'utf8').split('\n').filter(l => l.includes('heldout/suite.enc')).length, 1, 're-sealing replaces the line');
  assert.equal(JSON.parse(readFileSync(path.join(w.outDir, 'manifest.json'), 'utf8')).plaintextSha256, manifest.plaintextSha256, 'same tree, same plaintext hash');
  assert.equal(statSync(path.join(w.outDir, 'suite.enc')).mode & 0o777, 0o644);
});

test('Seal_refuses_an_incomplete_source_directory_naming_what_is_missing', async () => {
  const w = world();
  await tool('keygen', '--key-file', w.keyFile);
  mkdirSync(path.join(w.root, 'half'));
  writeFileSync(path.join(w.root, 'half', 'registry.json'), '{}');
  const r = await tool('seal', '--src', path.join(w.root, 'half'), '--key-file', w.keyFile, '--out-dir', w.outDir, '--hashes', w.hashes);
  assert.equal(r.code, 1);
  assert.match(r.out, /registry/);
  assert.equal(existsSync(path.join(w.outDir, 'suite.enc')), false, 'nothing sealed');
  const noKey = await tool('seal', '--src', w.src, '--key-file', path.join(w.root, 'absent.key'), '--out-dir', w.outDir, '--hashes', w.hashes);
  assert.equal(noKey.code, 3);
  assert.match(noKey.out, /absent\.key/);
});

test('Counts_reads_the_manifest_and_a_flipped_ciphertext_byte_fails_it', async () => {
  const w = await sealedWorld();
  const ok = await tool('counts', '--out-dir', w.outDir, '--key-file', w.keyFile);
  assert.equal(ok.code, 0, ok.out);
  assert.match(ok.out, /N1 = 4/);
  assert.match(ok.out, /N2 = 1/);
  assert.match(ok.out, /M = 2/);
  const file = path.join(w.outDir, 'suite.enc');
  const bytes = readFileSync(file);
  bytes[bytes.length - 3] ^= 1;
  writeFileSync(file, bytes);
  const bad = await tool('counts', '--out-dir', w.outDir, '--key-file', w.keyFile);
  assert.equal(bad.code, 1);
  assert.match(bad.out, /suite\.enc\.sha256/);
});

test('Calibrate_from_the_sealed_archive_writes_calibration_json_with_ids_and_booleans_only', async t => {
  if (!pythonGate(t)) return;
  const w = await sealedWorld();
  const r = await tool('calibrate', '--key-file', w.keyFile, '--out-dir', w.outDir, '--work-root', w.workRoot, '--direct');
  assert.equal(r.code, 0, r.out);
  const doc = JSON.parse(readFileSync(path.join(w.outDir, 'calibration.json'), 'utf8'));
  assert.equal(doc.ok, true);
  assert.equal(doc.plaintextSha256, JSON.parse(readFileSync(path.join(w.outDir, 'manifest.json'), 'utf8')).plaintextSha256);
  assert.deepEqual(Object.keys(doc.mutants), ['T01', 'T02']);
  assert.deepEqual([doc.n1, doc.n2, doc.m], [4, 1, 2]);
  assert.equal(JSON.stringify(doc).includes('pong'), false);
});

test('Calibrate_in_clear_mode_prints_and_writes_nothing_and_a_mismatch_exits_1', async t => {
  if (!pythonGate(t)) return;
  const w = world();
  const good = await tool('calibrate', '--src', w.src, '--out-dir', w.outDir, '--work-root', w.workRoot, '--direct');
  assert.equal(good.code, 0, good.out);
  assert.equal(existsSync(path.join(w.outDir, 'calibration.json')), false);
  const reg = path.join(w.src, 'registry.json');
  const doc = JSON.parse(readFileSync(reg, 'utf8'));
  doc.mutants[1].expected.v2.fail = ['H1-01', 'H2-01'];
  writeFileSync(reg, JSON.stringify(doc));
  const bad = await tool('calibrate', '--src', w.src, '--out-dir', w.outDir, '--work-root', w.workRoot, '--direct');
  assert.equal(bad.code, 1);
  assert.match(bad.out, /T02 v2: missing fail: H2-01/);
});

test('Leakcheck_decrypts_the_suite_and_scans_the_repository_naming_files_not_tokens', async () => {
  const w = await sealedWorld();
  const repo = tmpDir('tool-repo');
  const git = async (...args) => { const r = await runBounded('git', args, { cwd: repo, timeoutMs: 20_000 }); assert.equal(r.value.code, 0, r.value.stderr); };
  await git('init', '-q');
  writeFileSync(path.join(repo, 'notes.txt'), 'harmless\n');
  const clean = await tool('leakcheck', '--key-file', w.keyFile, '--out-dir', w.outDir, '--repo', repo, '--work-root', w.workRoot);
  assert.equal(clean.code, 0, clean.out);
  writeFileSync(path.join(repo, 'leaked.txt'), 'x toy-token-quokka-7421 y\n');
  const dirty = await tool('leakcheck', '--key-file', w.keyFile, '--out-dir', w.outDir, '--repo', repo, '--work-root', w.workRoot);
  assert.equal(dirty.code, 1);
  assert.match(dirty.out, /leaked\.txt/);
  assert.equal(dirty.out.includes('quokka'), false);
});

test('Sweep_removes_old_clear_leftovers_and_usage_errors_exit_4', async () => {
  const w = world();
  const old = path.join(w.workRoot, 'score-tmp', 'old-run');
  mkdirSync(old, { recursive: true, mode: 0o700 });
  const longAgo = new Date(Date.now() - 30 * 3600 * 1000);
  utimesSync(old, longAgo, longAgo);
  const swept = await tool('sweep', '--work-root', w.workRoot);
  assert.equal(swept.code, 0, swept.out);
  assert.match(swept.out, /removed 1/);
  assert.equal(existsSync(old), false);
  assert.equal((await tool('nonsense')).code, 4);
  assert.equal((await tool()).code, 4);
  assert.equal((await tool('seal')).code, 4);
  assert.equal((await tool('seal', '--bogus')).code, 4);
  assert.equal(fileURLToPath(new URL('../heldout-tool.mjs', import.meta.url)).endsWith('heldout-tool.mjs'), true);
});
