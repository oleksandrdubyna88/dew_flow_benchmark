// The direction of the dependency between the two spikes (architecture.md, the freeze's one contract): lang-explore imports lang-bench, never the reverse. `freeze.mjs record` reads the engine's `workspaces --check` output as a
// FILE for that reason. A static scan of every source file of this spike (the tests are the one place a cross-spike read is allowed, and none imports either).
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const spike = fileURLToPath(new URL('..', import.meta.url));
const SKIPPED = new Set(['node_modules', 'test', 'inputs', 'templates']);
const sources = (dir, found = []) => {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory() && !SKIPPED.has(entry.name)) sources(path.join(dir, entry.name), found);
    else if (entry.isFile() && entry.name.endsWith('.mjs')) found.push(path.join(dir, entry.name));
  }
  return found;
};
const IMPORT = /(?:from\s+|import\s*\(\s*|import\s+)['"][^'"]*lang-explore[^'"]*['"]/;

test('No_source_file_of_lang_bench_imports_lang_explore', () => {
  const files = sources(spike);
  assert.ok(files.length > 100 && files.some(file => file.endsWith('freeze.mjs')), `the scan sees the spike (${files.length} files)`);
  assert.deepEqual(files.filter(file => IMPORT.test(readFileSync(file, 'utf8'))).map(file => path.relative(spike, file)), []);
  assert.equal(IMPORT.test("import { x } from '../../lang-explore/lib/y.mjs';"), true, 'the pattern sees a static import');
  assert.equal(IMPORT.test("await import('../../lang-explore/lib/y.mjs')"), true, 'and a dynamic one');
  assert.equal(IMPORT.test('// the engine in lang-explore writes them'), false, 'and not a comment');
});
