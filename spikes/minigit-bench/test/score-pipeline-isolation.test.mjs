// The scoring pipeline's tests wire every dependency they exercise. This runs them as a child process whose HOME is an empty
// directory holding a decoy key, and shows that the real held-out dependency (which would read the key and create its work root
// under the home) was never reached. It lives in its own file because the child runs score-pipeline.test.mjs.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, symlinkSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { generateKey } from '../lib/heldout-crypto.mjs';
import { defaultWorkRoot } from '../lib/heldout-run.mjs';
import { homeDir } from './helpers.mjs';

const PIPELINE_TESTS = fileURLToPath(new URL('./score-pipeline.test.mjs', import.meta.url));
const walk = dir => (existsSync(dir) ? readdirSync(dir, { withFileTypes: true }).flatMap(e => (e.isDirectory() ? [dir, ...walk(path.join(dir, e.name))] : [path.join(dir, e.name)])) : []);

test('No_score_pipeline_test_touches_the_real_held_out_key_or_the_real_work_root', () => {
  // under the real home, not /tmp: a work root under the temp directory is refused before anything is created, which would hide the touch
  const home = homeDir('isolated-home');
  const keyFile = path.join(home, '.config', 'minigit-bench', 'heldout.key');
  mkdirSync(path.dirname(keyFile), { recursive: true, mode: 0o700 });
  generateKey(keyFile);
  const keyBefore = readFileSync(keyFile);
  // the fetched inputs are a public cache, shared read-only so the child needs no network
  const cache = path.join(os.homedir(), '.cache', 'minigit-bench', 'inputs');
  if (existsSync(cache)) {
    mkdirSync(path.join(home, '.cache', 'minigit-bench'), { recursive: true });
    symlinkSync(cache, path.join(home, '.cache', 'minigit-bench', 'inputs'));
  }
  const before = walk(home).filter(p => !p.includes(`${path.sep}.cache${path.sep}`)).sort();
  const { NODE_TEST_CONTEXT: _inherited, ...env } = process.env; // a child that inherits the runner's context reports nothing
  const child = spawnSync(process.execPath, ['--test', PIPELINE_TESTS], { encoding: 'utf8', timeout: 240000, env: { ...env, HOME: home } });
  assert.equal(child.status, 0, `the pipeline tests must pass with an empty home:\n${child.stdout.slice(-3000)}`);
  assert.match(child.stdout, /ℹ tests \d+/, 'the child really ran them');
  assert.equal(existsSync(defaultWorkRoot(home)), false, `the held-out work root ${defaultWorkRoot(home)} was created: a test reached the real held-out dependency`);
  assert.deepEqual(walk(home).filter(p => !p.includes(`${path.sep}.cache${path.sep}`)).sort(), before, 'nothing appeared under the home');
  assert.deepEqual(readFileSync(keyFile), keyBefore, 'the key is untouched');
});
