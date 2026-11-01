// The real held-out side through the real judge (plan T-X6): both references pass every case, each mutant fails exactly the cases its manifest predicts, the private area is private and matches its
// manifest. The files are read at run time and are NEVER copied into this repository; a host without `~/rsd/lang-v2/explore/heldout/` is `hostLacks` (a failure under BENCH_REQUIRE_SANDBOX=1).
import { realHome } from '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { hostLacks, sandboxGate, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { runCalibration } from '../lib/calibrate.mjs';
import { loadHeldout, manifestProblems, permissionProblems } from '../lib/heldout.mjs';
import { loadWorld } from '../lib/world.mjs';

/** The held-out side, or null when this host lacks it. */
function heldoutOrNull(t) {
  const h = loadHeldout(realHome);
  return h.ok ? h.value : (h.absent ? hostLacks(t, h.reason) && null : assert.fail(h.reason));
}

test('The_private_area_is_private_matches_its_manifest_and_every_case_asserts_a_requirement', t => {
  const h = heldoutOrNull(t);
  if (!h) return;
  assert.deepEqual(permissionProblems(h.dir), []);
  assert.deepEqual(manifestProblems(h.dir, h.manifest), []);
  const lines = readFileSync(path.join(h.casesDir, 'cases.txt'), 'utf8').trim().split('\n');
  assert.equal(lines.length, h.manifest.caseCount);
  for (const line of lines) assert.match(line, /^h\d{3} R\d+(,R\d+)* byte-exact$/, 'every case: id, at least one R-id of spec 01, byte-exact');
  assert.deepEqual(lines.map(l => l.split(' ')[0]), h.caseIds);
  assert.ok(h.traps.length >= 2 && h.traps.every(id => h.caseIds.includes(id)));
});

test('Both_references_pass_every_case_and_each_mutant_fails_exactly_the_cases_its_manifest_predicts', async t => {
  if (!sandboxGate(t)) return;
  const h = heldoutOrNull(t);
  if (!h) return;
  const work = tmpDir('real-cal');
  const world = await loadWorld({ home: realHome, work });
  if (!world.ok) { hostLacks(t, world.reason); return; }
  const r = await runCalibration({ explore: world.value.explore, heldout: h, files: world.value.filesFor, scoreFn: world.value.scoreFn, work, say: () => {} });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.mutants.filter(m => !m.ok), []);
  assert.equal(r.value.mutants.length, Object.keys(h.manifest.mutants).length);
  assert.ok(r.value.limits.timeMs >= 2000 && r.value.limits.memoryBytes >= 512 * 1024 * 1024);
});
