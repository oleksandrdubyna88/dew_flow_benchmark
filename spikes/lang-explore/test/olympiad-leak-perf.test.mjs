// The leak scan's size test (brief item 9): a synthetic case folder of 5 million numeric lines (task 09's shape) and a 25 MB haystack of 250 files, the whole check (material built, every tier, the history) in under 60 seconds.
// Skipped unless LEAK_PERF=1 (it writes about 100 MB and takes seconds): `LEAK_PERF=1 node --test spikes/lang-explore/test/olympiad-leak-perf.test.mjs`.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { buildMaterial } from '../lib/leak-material.mjs';
import { scanRepository } from '../lib/leak-scan.mjs';
import { commitFiles, repoWith } from './leak-scan-fixture.mjs';

const LINES = 5_000_000;
const PER_FILE = 100_000;

/** The case folder: 50 files of 100,000 lines of two 9-digit numbers each (a deterministic generator). -> `{ dir, casesDir, sample }` (`sample` is three consecutive lines of one file). */
function syntheticHeldout() {
  const dir = tmpDir('o-perf-held');
  const casesDir = path.join(dir, 'cases');
  mkdirSync(casesDir, { recursive: true });
  let x = 12345;
  const next = () => { x = (Math.imul(x, 1664525) + 1013904223) >>> 0; return 100000000 + (x % 900000000); };
  let sample = [];
  for (let f = 0; f < LINES / PER_FILE; f += 1) {
    const lines = new Array(PER_FILE);
    for (let i = 0; i < PER_FILE; i += 1) lines[i] = `${next()} ${next()}`;
    if (f === 20) sample = lines.slice(500, 503);
    writeFileSync(path.join(casesDir, `h${String(f).padStart(3, '0')}.in`), `${lines.join('\n')}\n`);
  }
  return { dir, casesDir, sample };
}

test('The_whole_leak_check_over_5_million_case_lines_and_a_25_MB_haystack_finishes_in_under_60_seconds_and_finds_a_planted_window', { skip: process.env.LEAK_PERF !== '1', timeout: 300_000 }, async t => {
  const held = syntheticHeldout();
  const files = {};
  for (let i = 0; i < 250; i += 1) files[`docs/f${i}.md`] = `${Array.from({ length: 2000 }, (_, k) => `public sentence ${i} number ${k} with some more words to make it a line`).join('\n')}\n`;
  const repo = repoWith(files);
  commitFiles(repo, { 'leak/pasted.md': `the failing input:\n\n${held.sample.join('\n')}\n` }, 'a paste');
  const started = performance.now();
  const built = buildMaterial({ tasks: [{ id: '09', heldout: { dir: held.dir, casesDir: held.casesDir }, shinglesText: '', baselineCommit: null }] });
  const builtAt = performance.now();
  assert.equal(built.ok, true, built.reason);
  const r = await scanRepository({ repoRoot: repo, material: built.value, range: null });
  const done = performance.now();
  t.diagnostic(`material ${((builtAt - started) / 1000).toFixed(1)} s, scan ${((done - builtAt) / 1000).toFixed(1)} s, total ${((done - started) / 1000).toFixed(1)} s`);
  assert.equal(r.ok, false);
  assert.deepEqual(r.findings.map(f => [f.tier, f.file, f.task, f.caseFile]), [['window', 'leak/pasted.md', '09', 'cases/h020.in']], 'the planted window, and only it');
  assert.ok(done - started < 60_000, `${((done - started) / 1000).toFixed(1)} s is over the 60 s ceiling`);
});
