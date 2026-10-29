import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { applyMutant, calibrate, compareCalibration, directScorer, readRegistry } from '../lib/heldout-calibrate.mjs';
import { heldoutScore } from '../lib/heldout-run.mjs';
import { pythonGate, tmpDir } from './helpers.mjs';
import { TOY_CASES, writeToySuite } from './toy-suite.mjs';

function toySuite(opts) {
  const dir = tmpDir('toy-suite');
  writeToySuite(dir, opts);
  return dir;
}

const run = async (suiteDir, scoreSubject = directScorer({ suiteDir, workRoot: tmpDir('subjects'), timeoutS: 5 })) =>
  calibrate({ suiteDir, workRoot: tmpDir('trees'), scoreSubject, requireVisible: false });

test('Both_references_pass_and_every_mutant_fails_exactly_its_registered_cases', async t => {
  if (!pythonGate(t)) return;
  const r = await run(toySuite());
  assert.equal(r.ok, true, JSON.stringify(r.problems));
  const { calibration } = r;
  assert.deepEqual(Object.keys(calibration.references), ['a', 'b']);
  for (const ref of Object.values(calibration.references)) assert.deepEqual(ref, { v1: { heldout100: true, visible100: null }, v2: { heldout100: true, visible100: null } });
  assert.deepEqual(calibration.mutants, { T01: { v1: { exact: true }, v2: { exact: true } }, T02: { v1: { exact: true }, v2: { exact: true } } });
  assert.deepEqual([calibration.n1, calibration.n2, calibration.m], [4, 1, 2]);
});

test('A_mutant_registered_with_the_wrong_set_fails_calibration_naming_it', async t => {
  if (!pythonGate(t)) return;
  const swap = reg => ({ ...reg, mutants: reg.mutants.map(m => (m.id !== 'T01' ? m
    : { ...m, expected: { ...m.expected, v1: { fail: ['H1-03'], blocked: ['H1-04'] } } })) });
  const r = await run(toySuite({ registryEdit: swap }));
  assert.equal(r.ok, false);
  assert.equal(r.problems.length, 1);
  assert.match(r.problems[0], /T01/);
  assert.match(r.problems[0], /v1/);
  assert.match(r.problems[0], /missing fail: H1-03/);
  assert.match(r.problems[0], /extra fail: H1-02/);
  assert.equal(r.calibration.mutants.T01.v1.exact, false);
  assert.equal(r.calibration.mutants.T02.v1.exact, true);
});

test('A_reference_that_fails_a_case_fails_calibration', async t => {
  if (!pythonGate(t)) return;
  const dir = toySuite();
  const refB = path.join(dir, 'refs', 'b', 'minigit');
  writeFileSync(refB, readFileSync(refB, 'utf8').replace("print('pong')", "print('pong?')"));
  const r = await run(dir);
  assert.equal(r.ok, false);
  assert.ok(r.problems.some(p => /reference b/.test(p) && /v1/.test(p)), r.problems.join('\n'));
  assert.equal(r.calibration.references.b.v1.heldout100, false);
});

test('A_runner_that_runs_other_cases_than_the_registry_names_fails_calibration', async t => {
  if (!pythonGate(t)) return;
  const dropOne = reg => ({ ...reg, cases: reg.cases.filter(c => c.id !== 'H2-01') });
  const r = await run(toySuite({ registryEdit: dropOne }));
  assert.equal(r.ok, false);
  assert.ok(r.problems.some(p => /H2-01/.test(p) && /not in the registry/.test(p)), r.problems.join('\n'));
});

test('An_edit_whose_find_does_not_occur_exactly_once_is_refused_naming_the_mutant', () => {
  const ref = 'one two two\n';
  assert.deepEqual(applyMutant(ref, { id: 'MX', find: 'one', replace: '1' }), { ok: true, value: '1 two two\n' });
  const twice = applyMutant(ref, { id: 'MX', find: 'two', replace: '2' });
  assert.equal(twice.ok, false);
  assert.match(twice.reason, /MX.*2 times|MX.*exactly once/);
  assert.match(applyMutant(ref, { id: 'MY', find: 'absent', replace: 'x' }).reason, /MY.*0 times|MY.*exactly once/);
  assert.equal(applyMutant(ref, { id: 'MZ', find: '', replace: 'x' }).ok, false);
  assert.equal(applyMutant(ref, { id: 'MZ', find: 'one', replace: 'one' }).ok, false, 'an edit that changes nothing is no mutant');
});

test('A_mutant_whose_find_is_wrong_is_reported_by_calibrate_not_run', async t => {
  if (!pythonGate(t)) return;
  const dir = toySuite();
  const file = path.join(dir, 'mutants', 'T02.json');
  writeFileSync(file, JSON.stringify({ ...JSON.parse(readFileSync(file, 'utf8')), find: 'nothing like this' }));
  const r = await run(dir);
  assert.equal(r.ok, false);
  assert.ok(r.problems.some(p => /T02/.test(p) && /exactly once/.test(p)), r.problems.join('\n'));
});

test('The_registry_is_validated_with_its_shape_named', () => {
  const dir = toySuite();
  assert.equal(readRegistry(dir).ok, true);
  assert.equal(readRegistry(dir).value.cases.length, TOY_CASES.length);
  const bad = toySuite({ registryEdit: r => ({ ...r, mutants: [{ id: 'T01', clause: 'c' }] }) });
  const r = readRegistry(bad);
  assert.equal(r.ok, false);
  assert.match(r.reason, /T01.*expected/);
});

test('compareCalibration_is_pure_and_names_blocked_differences_too', () => {
  const cases = ['H1-02', 'H1-03', 'H1-04'].map(id => ({ id, group: 'H1', scored: true, spec: 's', needs: [] }));
  const registry = { cases, mutants: [{ id: 'M1', expected: { v1: { fail: ['H1-02'], blocked: ['H1-03'] }, v2: { fail: [], blocked: [] } } }] };
  const sc = (fail, blocked) => ({ heldout: { known: true, primary100: fail.length + blocked.length === 0, cases: cases.map(({ id }) =>
    ({ id, status: fail.includes(id) ? 'fail' : blocked.includes(id) ? 'blocked' : 'pass' })) }, visible100: true });
  const opts = { requireVisible: true };
  const same = compareCalibration(registry, { references: {}, mutants: { M1: { v1: sc(['H1-02'], ['H1-03']), v2: sc([], []) } } }, opts);
  assert.deepEqual(same.problems, []);
  const off = compareCalibration(registry, { references: {}, mutants: { M1: { v1: sc(['H1-02'], []), v2: sc([], []) } } }, opts);
  assert.match(off.problems.join('\n'), /M1.*v1.*missing blocked: H1-03/);
  const unknown = compareCalibration(registry, { references: {}, mutants: { M1: { v1: { heldout: { known: false, why: 'no key' } }, v2: sc([], []) } } }, opts);
  assert.match(unknown.problems.join('\n'), /M1.*v1.*unknown.*no key/);
  const unseen = compareCalibration(registry, { references: { a: { v1: { ...sc([], []), visible100: null }, v2: sc([], []) } }, mutants: {} }, opts);
  assert.match(unseen.problems.join('\n'), /reference a.*v1.*visible.*unknown/);
  const optional = compareCalibration(registry, { references: { a: { v1: { ...sc([], []), visible100: null }, v2: sc([], []) } }, mutants: {} }, { requireVisible: false });
  assert.deepEqual(optional.problems, []);
});
