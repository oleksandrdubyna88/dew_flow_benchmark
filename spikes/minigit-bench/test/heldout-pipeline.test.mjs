import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { packDir } from '../lib/heldout-archive.mjs';
import { generateKey, readKey, seal } from '../lib/heldout-crypto.mjs';
import { makeHeldoutDep } from '../lib/heldout-run.mjs';
import { runBounded } from '../lib/run.mjs';
import { defaultDeps } from '../lib/score-deps.mjs';
import { runScoring, scoringPlan } from '../lib/score-pipeline.mjs';
import { homeDir, inputs, pythonGate, sandboxGate, tmpDir } from './helpers.mjs';
import { writeToySuite } from './toy-suite.mjs';

/** A toy suite sealed with a throwaway key, and the toy "reference" as a source tree. */
function toyWorld() {
  const root = tmpDir('pipe-toy');
  const suite = path.join(root, 'suite');
  writeToySuite(suite);
  const keyFile = path.join(root, 'k', 'heldout.key');
  generateKey(keyFile);
  const suiteEnc = path.join(root, 'suite.enc');
  writeFileSync(suiteEnc, seal(packDir(suite).value, readKey(keyFile).value));
  const source = path.join(root, 'tree');
  mkdirSync(source);
  cpSync(path.join(suite, 'refs', 'a', 'minigit'), path.join(source, 'minigit'));
  return { suiteEnc, keyFile, source };
}

test('The_pipeline_runs_the_sealed_suite_through_the_scoring_box_and_leaves_no_clear_copy', async t => {
  if (!sandboxGate(t) || !pythonGate(t)) return;
  const { suiteEnc, keyFile, source } = toyWorld();
  const workRoot = homeDir('pipe');
  const sessionDir = tmpDir('pipe-session');
  const plan = scoringPlan({ arm: 'bare', stack: 'node', phase: 'v2', sourceDir: source, sessionDir });
  const deps = defaultDeps({ inputs: await inputs(), heldout: makeHeldoutDep({ suiteEnc, keyFile, workRoot }) });
  const r = await runScoring(plan.value, deps);
  assert.equal(r.ok, true, r.reason);
  const { heldout } = r.value;
  assert.equal(heldout.known, true, heldout.why);
  assert.deepEqual(heldout.totals, { total: 5, passed: 5, failed: 0, blocked: 0, percent: 100 });
  assert.equal(heldout.primary100, true);
  assert.equal(existsSync(heldout.resultPath), true);
  assert.equal(heldout.resultPath.startsWith(path.join(sessionDir, 'score')), true);
  assert.equal(JSON.parse(readFileSync(heldout.resultPath, 'utf8')).phase, 'v2');
  assert.deepEqual(r.value.trace.filter(l => l.startsWith('heldout')), ['heldout: primary100=true']);
  assert.deepEqual(readdirSync(path.join(sessionDir, 'score', 'cases')), [], 'the cases\' repositories hold case text: removed after scoring');
  assert.equal(statSync(heldout.resultPath).mode & 0o777, 0o600);
});

test('A_missing_key_makes_the_held_out_slot_unknown_never_a_pass_or_a_zero', async t => {
  if (!sandboxGate(t) || !pythonGate(t)) return;
  const { suiteEnc, source } = toyWorld();
  const sessionDir = tmpDir('pipe-nokey');
  const plan = scoringPlan({ arm: 'bare', stack: 'node', phase: 'v1', sourceDir: source, sessionDir });
  const dep = makeHeldoutDep({ suiteEnc, keyFile: path.join(tmpDir('nokey'), 'absent.key'), workRoot: homeDir('pipe-nokey') });
  const r = await runScoring(plan.value, { ...defaultDeps({ inputs: await inputs() }), heldout: dep });
  assert.equal(r.value.heldout.known, false);
  assert.match(r.value.heldout.why, /absent\.key/);
});

test('The_held_out_box_binds_the_cell_s_directory_read_only_and_the_cases_read_write', async t => {
  if (!sandboxGate(t)) return;
  const { source } = toyWorld();
  const sessionDir = tmpDir('pipe-ro');
  const plan = scoringPlan({ arm: 'bare', stack: 'node', phase: 'v1', sourceDir: source, sessionDir });
  let probe;
  const heldout = async ({ binary, box, casesDir }) => {
    const [exe, ...prefix] = box;
    const dir = path.dirname(binary);
    const write = (target) => runBounded(exe, [...prefix, 'sh', '-c', `echo x > ${target}`], { timeoutMs: 10_000 });
    probe = { intoCopy: (await write(path.join(dir, 'planted'))).value.code, intoCases: (await write(path.join(casesDir, 'ok'))).value.code,
      runsBinary: (await runBounded(exe, [...prefix, binary, 'ping'], { timeoutMs: 10_000 })).value };
    return { known: false, why: 'probe only' };
  };
  await runScoring(plan.value, { ...defaultDeps({ inputs: await inputs() }), heldout });
  assert.notEqual(probe.intoCopy, 0, 'the held-out step could write into the cell\'s own directory');
  assert.equal(probe.intoCases, 0);
  assert.match(probe.runsBinary.stdout, /pong/, 'the binary still runs from its read-only directory');
});
