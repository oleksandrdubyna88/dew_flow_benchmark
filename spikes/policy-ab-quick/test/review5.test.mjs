import assert from 'node:assert/strict';
import test from 'node:test';
import { isValidScore, referencePlan } from '../lib/refscore.mjs';

const good = { holdout: [{ emitted: true, verdict: 'pass' }, { emitted: true, verdict: 'fail' }], ownSuite: { tests: 5800 } };
const bare = { holdout: [{ emitted: false, verdict: 'compile-error' }, { emitted: false, verdict: 'compile-error' }], ownSuite: { tests: 0, verdict: 'no-report' } };

test('a score is valid only when every hold-out emitted and the own suite reported', () => {
  assert.equal(isValidScore(good), true);
  assert.equal(isValidScore(bare), false, 'the bare-tree score a resume once recorded');
  assert.equal(isValidScore({ ...good, ownSuite: { tests: 0 } }), false);
  assert.equal(isValidScore({ ...good, holdout: [{ emitted: true, verdict: 'compile-error' }] }), false);
  assert.equal(isValidScore({ holdout: [], ownSuite: { tests: 9 } }), false);
  assert.equal(isValidScore(null), false);
});

test('resume keeps a valid reference score; a shed reference is re-prepared (install) and never scored bare', () => {
  assert.deepEqual(referencePlan({ score: good, treeExists: true, hasNodeModules: false, skipNpm: false }), { action: 'keep' });
  assert.deepEqual(referencePlan({ score: bare, treeExists: true, hasNodeModules: false, skipNpm: false }), { action: 'score', build: false, install: true });
  assert.deepEqual(referencePlan({ score: null, treeExists: false, hasNodeModules: false, skipNpm: false }), { action: 'score', build: true, install: true });
  assert.deepEqual(referencePlan({ score: bare, treeExists: true, hasNodeModules: true, skipNpm: false }), { action: 'score', build: false, install: false });
  assert.deepEqual(referencePlan({ score: null, treeExists: true, hasNodeModules: false, skipNpm: true }), { action: 'score', build: false, install: false });
});
