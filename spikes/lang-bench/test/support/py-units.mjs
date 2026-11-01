// Starts one Python unit file under `python3 -I -B -m unittest` and registers one Node test per Python test, named as the Python
// test is (without its `test_` prefix), so the guarantee's name appears in the Node report and a failing unit fails its own entry.
// The unit runs in an owned folder given as TMPDIR (common.test-leftovers): whatever it makes dies with that folder.
import { spawnSync } from 'node:child_process';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { cleanupScope } from '../../../../.agents/conventions/tools/lib/owned.mjs';
import { owned } from './owned.mjs';

const spikeDir = fileURLToPath(new URL('../..', import.meta.url));
const RESULT_LINE = /^(test_\S+) \((\S+)\) \.\.\. (.+)$/;
const CEILING_MS = 300_000;

/** The outcome of each test in unittest's `-v` text: `{ name, status, reason }`, status `ok`, `fail` or `skip`. */
export function parseVerbose(text) {
  return text.split('\n').flatMap(line => {
    const m = RESULT_LINE.exec(line);
    if (!m) return [];
    const [, name, , tail] = m;
    if (tail.startsWith('skipped')) return [{ name, status: 'skip', reason: tail.slice('skipped '.length).replace(/^'|'$/g, '') }];
    return [{ name, status: tail === 'ok' ? 'ok' : 'fail', reason: tail }];
  });
}

/** `python3` is needed: missing, the unit skips with its reason, except under BENCH_REQUIRE_SANDBOX=1 where it fails. */
const pythonMissing = () => {
  const probe = spawnSync('python3', ['-I', '-c', 'pass'], { encoding: 'utf8' });
  return probe.error && process.env.BENCH_REQUIRE_SANDBOX !== '1' ? `python3 is not on PATH (${probe.error.code})` : false;
};

function runUnit(file, scope) {
  const tmp = owned.ownedDir(scope, 'pyunit');
  const result = spawnSync('python3', ['-I', '-B', '-m', 'unittest', 'discover', '-v', '-s', 'test/py', '-p', file], {
    cwd: spikeDir, encoding: 'utf8', timeout: CEILING_MS, env: { ...process.env, TMPDIR: tmp, TEMP: tmp, TMP: tmp },
  });
  return { result, outcomes: parseVerbose(result.stderr ?? '') };
}

/** Registers the Node tests of `file` (a name in test/py/). Call once at the top level of a Node test file. */
export function pythonUnits(file) {
  const scope = cleanupScope();
  after(() => scope.close());
  const missing = pythonMissing();
  const { result, outcomes } = missing ? { result: null, outcomes: [] } : runUnit(file, scope);
  if (missing) return test(`${file} needs python3`, { skip: missing }, () => {});
  test(`${file} ran at least one test and ended cleanly`, () => {
    assert(outcomes.length > 0, `no test found in ${file}: ${result.stderr}`);
    assert(result.status === 0 || outcomes.some(o => o.status === 'fail'), `unittest exit ${result.status}: ${result.stderr}`);
  });
  for (const o of outcomes) {
    test(o.name.replace(/^test_/, ''), { skip: o.status === 'skip' ? o.reason : false }, () => {
      if (o.status === 'fail') throw new Error(`${o.name}: ${o.reason}\n${result.stderr}`);
    });
  }
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
