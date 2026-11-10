// The defect witnesses of a package (F2.S1, section 2.2, scenarios 7 to 15, and the progress lines of the plan round): the overlay, the two verdict rules, the one pass and the ten, over INVENTED witnesses and fake seams.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadPackage } from '../lib/package-quick.mjs';
import { trapTableProblems } from '../lib/package-suite.mjs';
import { assembleWitness, runWitness, witnessVerdict } from '../lib/package-witness.mjs';
import { LIMITS, fakeTemplates, fakeWorld, filesOf, heldSeries, writePackage } from './quick-package-fixture.mjs';

const put = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };
const folder = files => { const dir = tmpDir('pkg-overlay'); for (const [rel, text] of Object.entries(files)) put(path.join(dir, rel), text); return dir; };

test('A_witness_is_the_reference_with_its_overlay_applied_and_an_overlay_onto_a_frozen_file_is_refused', async t => {
  const templates = fakeTemplates();
  const refDir = folder({ 'src/main.ts': 'the reference\n', 'src/util.ts': 'util\n' });
  const parent = tmpDir('pkg-parent');
  const scope = cleanupScope();
  t.after(() => scope.close());
  const make = overlay => assembleWitness({ templates, stack: 'node', refDir, overlayDir: folder(overlay), scope, parent });
  const r = make({ 'src/main.ts': 'the defect\n' });
  assert.equal(r.ok, true, r.reason);
  assert.equal(filesOf(r.value.dir)['src/main.ts'], 'the defect\n');
  assert.equal(filesOf(r.value.dir)['src/util.ts'], 'util\n', 'the rest is the reference\'s');
  const before = readdirSync(parent).length;
  const frozen = make({ 'src/main.ts': 'x\n', 'run.sh': 'echo\n' });
  assert.equal(frozen.ok, false);
  assert.ok(frozen.reason.includes('run.sh') && /frozen/.test(frozen.reason), frozen.reason);
  const whole = make(Object.fromEntries(readdirSync(path.join(templates, 'node', 'daemon')).map(n => [n, 'copy\n'])));
  assert.equal(whole.ok, false);
  assert.ok(/not an overlay/.test(whole.reason), whole.reason);
  assert.equal(readdirSync(parent).length, before, 'a refused overlay assembles nothing');
});

test('A_witness_identical_to_its_reference_is_refused', async t => {
  const refDir = folder({ 'src/main.ts': 'the reference\n' });
  const scope = cleanupScope();
  t.after(() => scope.close());
  const same = assembleWitness({ templates: fakeTemplates(), stack: 'node', refDir, overlayDir: folder({ 'src/main.ts': 'the reference\n' }), scope, parent: tmpDir('pkg-parent') });
  assert.equal(same.ok, false);
  assert.ok(/changes nothing/.test(same.reason), same.reason);
  const empty = assembleWitness({ templates: fakeTemplates(), stack: 'node', refDir, overlayDir: folder({}), scope, parent: tmpDir('pkg-parent') });
  assert.ok(empty.ok === false && /changes nothing/.test(empty.reason), 'an empty overlay changes nothing either');
});

const CASES = ids => ids.map(id => ({ id, verdict: 'wrong answer', reason: 'entry 3 of 4 differs' }));
const clean = ['h001', 'h002', 'h003', 'h004'].map(id => ({ id, verdict: 'pass', reason: '' }));
const pass = (n, failing, hang = []) => ({ n, profile: n % 2 === 1 ? '2cpu' : '8cpu', cases: clean.map(c => (hang.includes(c.id) ? { ...c, verdict: 'wrong answer', reason: 'timeout: the case ran past its test bound' } : (failing.includes(c.id) ? CASES([c.id])[0] : c))) });

test('A_deterministic_witness_is_caught_when_in_its_one_pass_every_registered_case_fails_and_no_other_case_does', () => {
  const caught = witnessVerdict({ kind: 'deterministic', registered: ['h001', 'h002'], passes: [pass(1, ['h001', 'h002'])] });
  assert.equal(caught.caught, true, caught.sentence);
  const missing = witnessVerdict({ kind: 'deterministic', registered: ['h001', 'h002'], passes: [pass(1, ['h001'])] });
  assert.equal(missing.caught, false);
  assert.deepEqual([missing.counts.registered, missing.counts.failedRegistered, missing.counts.missing], [2, 1, 1]);
  assert.ok(/registered 2, failed 1, missing 1/.test(missing.sentence), missing.sentence);
});

test('An_unregistered_failure_is_a_mapping_to_fix_and_the_witness_is_not_caught_until_the_table_says_so', () => {
  const run = (registered, kind = 'deterministic') => witnessVerdict({ kind, registered, passes: [pass(1, ['h001', 'h002', 'h004'])] });
  const beyond = run(['h001', 'h002']);
  assert.equal(beyond.caught, false);
  assert.ok(/one case beyond the table/.test(beyond.sentence) && beyond.counts.beyond === 1, beyond.sentence);
  assert.deepEqual(beyond.failingSets[0], ['h001', 'h002', 'h004'], 'the actual failing set is kept for the record');
  assert.equal(run(['h001', 'h002', 'h004']).caught, true, 'after the table registers the case the same run reads caught');
  const race = witnessVerdict({ kind: 'race', registered: ['h001', 'h002'], passes: [pass(1, ['h001', 'h002', 'h004']), pass(2, [])] });
  assert.ok(race.caught === false && /one case beyond the table/.test(race.sentence), 'the same for a race witness');
});

test('A_race_witness_gets_ten_passes_and_is_judged_on_its_failing_passes_only', () => {
  const ten = failing => Array.from({ length: 10 }, (_, i) => pass(i + 1, failing[i + 1] ?? []));
  const caught = witnessVerdict({ kind: 'race', registered: ['h003'], passes: ten({ 1: ['h003'], 3: ['h003'], 5: ['h003'] }) });
  assert.equal(caught.caught, true, caught.sentence);
  assert.equal(caught.counts.failingPasses, 3);
  assert.equal(caught.counts.cleanPasses, 7, 'the seven clean passes are counted and kept');
  const outside = witnessVerdict({ kind: 'race', registered: ['h003'], passes: ten({ 1: ['h003'], 3: ['h001'] }) });
  assert.equal(outside.caught, false, 'one failing pass with a case outside the registered set');
  const none = witnessVerdict({ kind: 'race', registered: ['h003'], passes: ten({}) });
  assert.equal(none.caught, false);
  assert.ok(/a trap with no catching case/.test(none.sentence), none.sentence);
  assert.equal(witnessVerdict({ kind: 'race', registered: ['h003'], passes: ten({ 2: ['h003'] }) }).caught, true, 'a majority of clean passes does not decide it');
});

// ---- a witness pass also runs the visible samples (parent entry 10.1): the verdict judges every case of a pass, a registered sample included ----------------

/** A pass of the four held-out cases and the two samples `s01`, `s02`, failing the ids given. */
const withSamples = (n, failing) => ({ n, profile: n % 2 === 1 ? '2cpu' : '8cpu', cases: [...clean, { id: 's01', verdict: 'pass', reason: '' }, { id: 's02', verdict: 'pass', reason: '' }].map(c => (failing.includes(c.id) ? CASES([c.id])[0] : c)) });

test('A_deterministic_witness_failing_the_held_out_case_of_its_row_and_its_registered_sample_is_caught', () => {
  const registered = ['h001', 's01'];
  const caught = witnessVerdict({ kind: 'deterministic', registered, passes: [withSamples(1, ['h001', 's01'])] });
  assert.equal(caught.caught, true, caught.sentence);
  assert.deepEqual(caught.failingSets[0], ['h001', 's01']);
  const missing = witnessVerdict({ kind: 'deterministic', registered, passes: [withSamples(1, ['h001'])] });
  assert.ok(missing.caught === false && missing.counts.missing === 1, 'a registered sample that passes is a registered case that did not fail');
});

test('A_witness_failing_also_a_sample_the_row_does_not_register_is_not_caught_beyond_the_table', () => {
  const beyond = witnessVerdict({ kind: 'deterministic', registered: ['h001', 's01'], passes: [withSamples(1, ['h001', 's01', 's02'])] });
  assert.equal(beyond.caught, false);
  assert.equal(beyond.reason, 'beyond the table');
  assert.ok(/one case beyond the table/.test(beyond.sentence) && beyond.counts.beyond === 1, beyond.sentence);
  assert.deepEqual(beyond.failingSets[0], ['h001', 's01', 's02'], 'the actual failing set, the unregistered sample included, is kept');
});

test('A_race_witness_over_ten_passes_is_caught_when_its_failing_passes_fail_the_registered_held_out_case_and_the_registered_sample', () => {
  const registered = ['h003', 's02'];
  const ten = failing => Array.from({ length: 10 }, (_, i) => withSamples(i + 1, failing[i + 1] ?? []));
  const caught = witnessVerdict({ kind: 'race', registered, passes: ten({ 2: ['h003', 's02'], 6: ['h003', 's02'], 9: ['h003', 's02'] }) });
  assert.equal(caught.caught, true, caught.sentence);
  assert.deepEqual([caught.counts.failingPasses, caught.counts.cleanPasses], [3, 7]);
  const outside = witnessVerdict({ kind: 'race', registered, passes: ten({ 2: ['h003', 's02'], 6: ['h003', 's01'] }) });
  assert.equal(outside.caught, false, 'a failing pass that fails an unregistered sample');
  assert.ok(/one case beyond the table/.test(outside.sentence), outside.sentence);
});

test('A_timeout_or_a_hang_of_a_registered_case_counts_as_that_case_failing', () => {
  for (const kind of ['deterministic', 'race']) {
    const hung = witnessVerdict({ kind, registered: ['h003'], passes: [pass(1, [], ['h003'])] });
    assert.equal(hung.caught, true, `${kind}: a hang of the registered case is that case failing`);
    const outside = witnessVerdict({ kind, registered: ['h003'], passes: [pass(1, ['h003'], ['h001'])] });
    assert.equal(outside.caught, false, `${kind}: a hang outside the trap is a failure outside the registered cases`);
    assert.ok(/a hang outside the trap/.test(outside.sentence) && outside.counts.hangsBeyond === 1, outside.sentence);
  }
});

/** A witness of the invented package run through the fake world: -> `{ answer, world, pkg, lines }`. */
async function ranWitness({ id, plan, buildOk, observed, heartbeatMs, delayMs, patchManifest }) {
  const p = writePackage();
  const world = fakeWorld({ plan, buildOk, observed, delayMs });
  const loaded = loadPackage({ home: p.home, task: p.task, world });
  assert.equal(loaded.ok, true, loaded.reason);
  const pkg = patchManifest ? { ...loaded.value, manifest: patchManifest(loaded.value.manifest) } : loaded.value;
  const witness = pkg.manifest.witnesses[id];
  const scope = cleanupScope();
  const tree = assembleWitness({ templates: world.templates, stack: witness.stack, refDir: pkg.refs[witness.stack].dir, overlayDir: path.join(pkg.mutantsDir, id), scope, parent: tmpDir('pkg-parent') });
  assert.equal(tree.ok, true, tree.reason);
  const series = heldSeries();
  const lines = [];
  try {
    const answer = await runWitness({ world, pkg, id, ordinal: 2, source: tree.value.dir, work: path.join(series.seriesDir, 'work'), seriesDir: series.seriesDir, limits: LIMITS, memoryBytes: LIMITS.memoryBytes, say: l => lines.push(l), now: world.clock.now, heartbeatMs });
    return { answer, world, pkg, lines };
  } finally {
    series.release();
    await scope.close();
  }
}

const planFor = (id, entry, otherwise = 'clean') => ({ id: who, n }) => (who === id ? (typeof entry === 'function' ? entry(n) : entry) : otherwise);

test('A_witness_whose_build_fails_is_not_caught_and_that_is_the_point', async () => {
  const r = await ranWitness({ id: 'MXX-alpha-n', plan: () => ({ failing: { h001: 'wrong answer', h002: 'wrong answer' } }), buildOk: ({ id }) => id !== 'MXX-alpha-n' });
  assert.equal(r.answer.caught, false, 'every case would have failed, and the witness is still not caught');
  assert.equal(r.answer.reason, 'does not build');
  assert.equal(r.world.log.length, 0, 'no pass ran for a build that failed');
  const built = await ranWitness({ id: 'MXX-alpha-n', plan: planFor('MXX-alpha-n', { failing: { h001: 'wrong answer', h002: 'wrong answer' } }) });
  assert.equal(built.answer.caught, true, 'control: the same witness that builds is caught');
});

test('A_witness_pass_is_the_pass_child_with_the_affinity_read_back_on_its_profile', async () => {
  const det = await ranWitness({ id: 'MXX-alpha-c', plan: planFor('MXX-alpha-c', { failing: { h001: 'wrong answer', h002: 'wrong answer' } }) });
  assert.deepEqual(det.world.log.map(l => [l.cpus, l.profile]), [['6-7', '2cpu']], 'a deterministic witness gets ONE pass, on the 2-CPU profile');
  const race = await ranWitness({ id: 'MXX-beta-n', plan: planFor('MXX-beta-n', n => (n % 4 === 1 ? { failing: { h003: 'wrong answer' } } : 'clean')) });
  assert.deepEqual(race.world.log.map(l => l.cpus), ['6-7', '0-7', '6-7', '0-7', '6-7', '0-7', '6-7', '0-7', '6-7', '0-7'], 'a race witness alternates the two profiles like a session');
  const wrong = await ranWitness({ id: 'MXX-alpha-c', plan: () => 'clean', observed: () => '0-7' });
  assert.equal(wrong.answer.halted, true, 'another mask is a halt, never a verdict');
  assert.ok(/affinity-not-applied/.test(wrong.answer.sentence) && wrong.answer.caught === undefined, wrong.answer.sentence);
});

test('A_race_witness_is_run_through_the_driver_and_its_clean_passes_are_kept_in_the_record', async () => {
  const r = await ranWitness({ id: 'MXX-beta-n', plan: planFor('MXX-beta-n', n => (n === 1 || n === 5 ? { failing: { h003: 'wrong answer' } } : 'clean')) });
  assert.equal(r.answer.caught, true, r.answer.sentence);
  assert.equal(r.answer.record.passes.length, 10);
  assert.equal(r.answer.record.passes.filter(p => p.failing.length === 0).length, 8, 'the eight clean passes are kept');
  assert.equal(r.answer.record.kind, 'race');
  assert.ok(r.answer.record.cost.wallMs >= 20_000 && r.answer.record.cost.retainedBytes > 0, 'the cost: the walls and the retained size');
});

test('Every_trap_and_stack_row_of_the_table_names_an_existing_case_and_exactly_one_witness', () => {
  const caseIds = ['h001', 'h002', 'h003'];
  const base = { traps: { a: { stack: 'both', cases: ['h001'] } }, witnesses: { Wc: { trap: 'a', stack: 'csharp', kind: 'deterministic' }, Wn: { trap: 'a', stack: 'node', kind: 'race' } } };
  assert.deepEqual(trapTableProblems({ manifest: base, caseIds, sampleIds: [] }), [], 'a complete manifest is green');
  const oneWitness = trapTableProblems({ manifest: { ...base, witnesses: { Wc: base.witnesses.Wc } }, caseIds, sampleIds: [] });
  assert.ok(oneWitness.length === 1 && /trap a on node has no witness/.test(oneWitness[0]), oneWitness.join('|'));
  const missingCase = trapTableProblems({ manifest: { ...base, traps: { a: { stack: 'both', cases: ['h009'] } } }, caseIds, sampleIds: [] });
  assert.ok(missingCase.some(s => s.includes('h009') && /not in cases/.test(s)), missingCase.join('|'));
  const two = trapTableProblems({ manifest: { ...base, witnesses: { ...base.witnesses, Wc2: { trap: 'a', stack: 'csharp', kind: 'race' } } }, caseIds, sampleIds: [] });
  assert.ok(two.some(s => /2 witnesses/.test(s)), two.join('|'));
  const noCase = trapTableProblems({ manifest: { ...base, traps: { a: { stack: 'both', cases: [] } } }, caseIds, sampleIds: [] });
  assert.ok(noCase.some(s => /names no case/.test(s)), noCase.join('|'));
  const stray = trapTableProblems({ manifest: { ...base, witnesses: { ...base.witnesses, Wz: { trap: 'zz', stack: 'node', kind: 'race' } } }, caseIds, sampleIds: [] });
  assert.ok(stray.some(s => s.includes('Wz') && /no row/.test(s)), stray.join('|'));
});

test('Each_pass_of_a_witness_prints_a_start_line_a_heartbeat_and_an_end_line_with_counts_only', async () => {
  const r = await ranWitness({ id: 'MXX-alpha-c', plan: planFor('MXX-alpha-c', { failing: { h001: 'wrong answer', h002: 'wrong answer' } }), heartbeatMs: 15, delayMs: 80 });
  const text = r.lines.join('\n');
  assert.ok(/witness 2 pass 1 \(2cpu\) started/.test(text), text);
  assert.ok(/witness 2 pass 1 \(2cpu\) still running after/.test(text), 'a heartbeat while the pass runs');
  assert.ok(/witness 2 pass 1 \(2cpu\) ended after \d+ s: failed, 2 failing case\(s\)/.test(text), text);
  assert.equal(/h\d{3}|trap|MXX|alpha|\/tmp|testsToDelete/.test(text), false, 'never a case id, a trap, a witness name or a path');
  assert.equal(r.lines.at(-1).includes('ended'), true, 'the end line is the last');
});

test('A_pass_whose_memory_claim_is_not_certified_is_a_halt_never_a_pass_and_never_a_caught_witness', async () => {
  const r = await ranWitness({ id: 'MXX-alpha-c', plan: () => ({ failing: { h001: 'wrong answer', h002: 'wrong answer' }, uncertified: true }) });
  assert.equal(r.answer.halted, true);
  assert.ok(/not certified/.test(r.answer.sentence), r.answer.sentence);
  assert.equal(r.answer.caught, undefined);
});

test('The_record_of_a_failing_pass_keeps_the_verdict_word_of_each_failing_case', async () => {
  const r = await ranWitness({ id: 'MXX-alpha-c', plan: planFor('MXX-alpha-c', { failing: { h001: 'memory limit', h002: 'memory limit' } }) });
  assert.deepEqual(r.answer.record.passes[0].verdicts, { h001: 'memory limit', h002: 'memory limit' }, 'the memory bound is told apart from a wrong answer');
});
