import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../heldout-tool.mjs';
import { parseShingles, parseTokens, scanFiles } from '../lib/heldout-leak.mjs';
import { withClearSuite } from '../lib/heldout-run.mjs';
import { homeDir, keyGate, sandboxGate, spikeDir, tmpDir } from './helpers.mjs';

const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const repoRoot = path.resolve(spikeDir, '..', '..');
const HELDOUT = path.join(spikeDir, 'heldout');
const COMMITTED = ['calibration.json', 'manifest.json', 'suite.enc', 'suite.enc.sha256'];

const walk = dir => readdirSync(dir, { recursive: true, withFileTypes: true })
  .filter(e => e.isFile()).map(e => path.relative(dir, path.join(e.parentPath, e.name)).split(path.sep).join('/')).sort();

const ENV_READ = new RegExp([
  String.raw`process\s*(?:\.|\[\s*['"\x60])\s*env\b`, // process.env, process['env']
  String.raw`\{[^}]*\benv\b[^}]*\}\s*=\s*process\b`, // const { env } = process
  String.raw`import\s[^;]*\{[^}]*\benv\b[^}]*\}\s*from\s*['"](?:node:)?process['"]`, // import { env } from 'node:process'
  String.raw`require\(\s*['"](?:node:)?process['"]\s*\)\s*\.\s*env\b`,
].join('|'));
const heldoutModules = () => [
  ...readdirSync(path.join(spikeDir, 'lib')).filter(f => /^(heldout|adjudicate)/.test(f)).map(f => path.join(spikeDir, 'lib', f)),
  path.join(spikeDir, 'heldout-tool.mjs'), path.join(spikeDir, 'adjudicate.mjs'),
];
const envReaders = files => files.filter(f => ENV_READ.test(readFileSync(f, 'utf8'))).map(f => path.relative(spikeDir, f));

test('No_heldout_module_reads_process_env', () => {
  const files = heldoutModules();
  assert.ok(files.length >= 8, `the scan covers the held-out modules (${files.length})`);
  assert.deepEqual(envReaders(files), []);
});

test('The_env_scan_recognises_every_way_to_read_the_environment_and_nothing_else', () => {
  const reads = ['process.env.X', "process['env'].X", 'process [ "env" ]', 'const { env } = process;', 'const { argv, env: e } = process', "import { env } from 'node:process';",
    'import process, { env } from "process"', "require('node:process').env"];
  for (const text of reads) assert.equal(ENV_READ.test(text), true, text);
  for (const text of ['process.argv', 'const { argv } = process', "import process from 'node:process'", 'const environment = 1', 'process.exitCode = 1']) assert.equal(ENV_READ.test(text), false, text);
});

test('The_env_scan_still_finds_a_known_reader', () => {
  assert.deepEqual(envReaders([path.join(spikeDir, 'lib', 'fetch-inputs.mjs')]), ['lib/fetch-inputs.mjs']);
});

test('No_clear_heldout_source_is_in_the_spike', () => {
  const files = walk(spikeDir);
  assert.deepEqual(files.filter(f => f.endsWith('.py')), [], 'no Python file: the runner and the cases are sealed');
  const heldout = files.filter(f => f.startsWith('heldout/')).map(f => f.slice('heldout/'.length));
  assert.deepEqual(heldout.filter(f => !COMMITTED.includes(f)), [], 'heldout/ holds only the four committed files');
});

test('The_scan_sees_the_sealed_suite', () => {
  assert.ok(walk(spikeDir).includes('heldout/suite.enc'), 'the walk lists heldout/suite.enc (the suite is sealed and the scan is alive)');
});

test('The_sealed_suite_matches_its_hash_records_its_manifest_and_its_calibration', () => {
  const enc = readFileSync(path.join(HELDOUT, 'suite.enc'));
  assert.equal(readFileSync(path.join(HELDOUT, 'suite.enc.sha256'), 'utf8'), `${sha(enc)}  suite.enc\n`);
  const hashes = readFileSync(path.join(spikeDir, 'inputs', 'hashes.txt'), 'utf8').split('\n');
  assert.ok(hashes.includes(`${sha(enc)}  heldout/suite.enc`), 'inputs/hashes.txt carries suite.enc');
  const manifest = JSON.parse(readFileSync(path.join(HELDOUT, 'manifest.json'), 'utf8'));
  assert.match(manifest.plaintextSha256, /^[0-9a-f]{64}$/);
  for (const k of ['n1', 'n2', 'm']) assert.ok(Number.isInteger(manifest[k]) && manifest[k] > 0, `${k} is a positive count`);
  const cal = JSON.parse(readFileSync(path.join(HELDOUT, 'calibration.json'), 'utf8'));
  assert.equal(cal.plaintextSha256, manifest.plaintextSha256, 'the calibration is of THIS sealed suite');
  assert.deepEqual([cal.n1, cal.n2, cal.m], [manifest.n1, manifest.n2, manifest.m]);
  assert.equal(cal.ok, true);
  assert.equal(Object.keys(cal.mutants).length, manifest.m);
  const leaves = [];
  const collect = (v, key) => (v !== null && typeof v === 'object' ? Object.entries(v).forEach(([k, x]) => collect(x, k)) : leaves.push([key, v]));
  collect(cal, 'root');
  assert.deepEqual(leaves.filter(([k, v]) => typeof v === 'string' && k !== 'plaintextSha256'), [], 'ids and booleans only');
});

test('The_real_suite_leaves_no_distinctive_string_under_todo_or_spikes', async t => {
  if (!keyGate(t)) return;
  const workRoot = homeDir('leak');
  const read = await withClearSuite({ workRoot, rid: 'leak' }, dir => [readFileSync(path.join(dir, 'leak-tokens.txt'), 'utf8'), readFileSync(path.join(dir, 'leak-shingles.txt'), 'utf8')]);
  assert.equal(read.ok, true, read.reason);
  const shingles = parseShingles(read.value[1]);
  assert.equal(shingles.ok, true, shingles.reason);
  assert.ok(shingles.value.length >= 500, `the shingle list is alive (${shingles.value.length})`);
  const tokens = parseTokens(read.value[0]);
  assert.equal(tokens.ok, true, tokens.reason);
  assert.ok(tokens.value.length >= 30, `the token list is alive (${tokens.value.length} tokens)`);
  const files = ['todo', 'spikes'].flatMap(top => walk(path.join(repoRoot, top)).map(f => `${top}/${f}`));
  assert.ok(files.length > 20, 'the walk covers todo/ and spikes/');
  const { hits, unreadable } = scanFiles(repoRoot, files, tokens.value, shingles.value);
  assert.deepEqual(hits, [], 'a held-out string is in a file (hits name the file and the token NUMBER only)');
  assert.deepEqual(unreadable, []);
});

test('The_real_suite_calibrates', async t => {
  if (!keyGate(t) || !sandboxGate(t)) return;
  const out = tmpDir('real-cal');
  for (const f of ['suite.enc', 'suite.enc.sha256', 'manifest.json']) cpSync(path.join(HELDOUT, f), path.join(out, f));
  const lines = [];
  const code = await main(['calibrate', '--out-dir', out, '--work-root', homeDir('real-cal')], l => lines.push(l));
  assert.equal(code, 0, lines.join('\n'));
  const fresh = JSON.parse(readFileSync(path.join(out, 'calibration.json'), 'utf8'));
  const committed = JSON.parse(readFileSync(path.join(HELDOUT, 'calibration.json'), 'utf8'));
  assert.deepEqual(fresh, committed, 'a fresh calibration of the sealed archive equals the committed one');
  const manifest = JSON.parse(readFileSync(path.join(HELDOUT, 'manifest.json'), 'utf8'));
  assert.deepEqual([fresh.n1, fresh.n2, fresh.m], [manifest.n1, manifest.n2, manifest.m]);
  for (const ref of Object.values(fresh.references)) for (const phase of Object.values(ref)) assert.deepEqual(phase, { heldout100: true, visible100: true });
  for (const mutant of Object.values(fresh.mutants)) for (const phase of Object.values(mutant)) assert.deepEqual(phase, { exact: true });
});
