// The reference trees of a package (F2.S1, section 2.1, scenarios 1 to 6): the assembly on the template, the refusals, the ten-pass driver and the record's numbers, over INVENTED references and fake seams. No box, no private file.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { assembleReference, loadPackage } from '../lib/package-quick.mjs';
import { validateReference } from '../lib/package-refs.mjs';
import { LIMITS, TOKEN, fakeTemplates, fakeWorld, filesOf, heldSeries, writePackage } from './quick-package-fixture.mjs';

const put = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };

/** A reference folder of `files` and the scope that removes what is assembled from it. */
function refFolder(files) {
  const dir = tmpDir('pkg-ref');
  for (const [rel, text] of Object.entries(files)) put(path.join(dir, rel), text);
  return dir;
}

test('A_reference_workspace_is_the_templates_files_plus_the_reference_files_and_nothing_else', async t => {
  const templates = fakeTemplates();
  const refDir = refFolder({ 'src/main.ts': 'one\n', 'src/util.ts': 'two\n' });
  const scope = cleanupScope();
  t.after(() => scope.close());
  const r = assembleReference({ templates, stack: 'node', refDir, scope, parent: tmpDir('pkg-parent') });
  assert.equal(r.ok, true, r.reason);
  const tree = filesOf(r.value.dir);
  const expected = { ...filesOf(path.join(templates, 'node', 'daemon')), 'src/main.ts': 'one\n', 'src/util.ts': 'two\n' };
  assert.deepEqual(tree, expected, 'the union of the template and the reference, bytes equal');
  assert.deepEqual(r.value.files, Object.keys(expected).sort());
  assert.equal(tree['build.sh'], readFileSync(path.join(templates, 'node', 'daemon', 'build.sh'), 'utf8'), 'the frozen set is the template\'s');
});

test('A_reference_folder_that_holds_a_frozen_file_is_refused_naming_the_path', async t => {
  const templates = fakeTemplates();
  const parent = tmpDir('pkg-parent');
  const scope = cleanupScope();
  t.after(() => scope.close());
  for (const frozen of ['build.sh', 'samples/x.in']) {
    const r = assembleReference({ templates, stack: 'csharp', refDir: refFolder({ 'Program.cs': 'x\n', [frozen]: 'changed\n' }), scope, parent });
    assert.equal(r.ok, false);
    assert.ok(r.reason.includes(frozen), r.reason);
    assert.deepEqual(readdirSync(parent), [], 'nothing was assembled');
  }
  const editable = assembleReference({ templates, stack: 'csharp', refDir: refFolder({ 'App.csproj': '<Project>more</Project>\n', 'Program.cs': 'x\n' }), scope, parent });
  assert.equal(editable.ok, true, 'control: the project file is the agent\'s to write');
});

test('A_reference_folder_that_holds_a_link_is_refused_and_never_read_through', async t => {
  const outside = tmpDir('pkg-outside');
  put(path.join(outside, 'secret.txt'), `${TOKEN} bytes outside the reference\n`);
  const refDir = refFolder({ 'Program.cs': 'x\n' });
  symlinkSync(path.join(outside, 'secret.txt'), path.join(refDir, 'Extra.cs'));
  const parent = tmpDir('pkg-parent');
  const scope = cleanupScope();
  t.after(() => scope.close());
  const r = assembleReference({ templates: fakeTemplates(), stack: 'csharp', refDir, scope, parent });
  assert.equal(r.ok, false);
  assert.ok(r.reason.includes('Extra.cs') && /link/.test(r.reason), r.reason);
  assert.equal(r.reason.includes(TOKEN), false, 'the target\'s bytes appear nowhere');
  assert.deepEqual(readdirSync(parent), [], 'nothing was assembled');
});

/** A reference of `stack` scored through the fake world: -> the answer. */
async function scored({ plan, stack = 'csharp', over = {} }) {
  const p = writePackage();
  const world = fakeWorld({ plan });
  const pkg = loadPackage({ home: p.home, task: p.task, world });
  assert.equal(pkg.ok, true, pkg.reason);
  const scope = cleanupScope();
  const tree = assembleReference({ templates: world.templates, stack, refDir: pkg.value.refs[stack].dir, scope, parent: tmpDir('pkg-parent') });
  const series = heldSeries();
  try {
    return await validateReference({ world, pkg: pkg.value, stack, source: tree.value.dir, work: path.join(series.seriesDir, 'work'), seriesDir: series.seriesDir, limits: LIMITS, memoryBytes: LIMITS.memoryBytes, say: () => {}, now: world.clock.now, ...over });
  } finally {
    series.release();
    await scope.close();
  }
}

test('Both_references_are_scored_by_the_ten_pass_driver_and_green_means_accepted_in_both_stacks', async () => {
  for (const stack of ['csharp', 'node']) {
    const a = await scored({ stack, plan: () => 'clean' });
    assert.equal(a.ok, true, a.sentence);
    assert.equal(a.verdict, 'accepted');
    assert.deepEqual(a.rows.passes.map(r => r.profile), ['2cpu', '8cpu', '2cpu', '8cpu', '2cpu', '8cpu', '2cpu', '8cpu', '2cpu', '8cpu'], 'ten passes, five per profile, alternating');
  }
  const flaky = await scored({ stack: 'node', plan: ({ n }) => (n === 3 ? { failing: { h001: 'wrong answer' } } : 'clean') });
  assert.equal(flaky.ok, false);
  assert.equal(flaky.verdict, 'intermittently incorrect');
  assert.ok(/node/.test(flaky.sentence) && /1 failing/.test(flaky.sentence) && /9 clean/.test(flaky.sentence), flaky.sentence);
  assert.equal(/h\d{3}/.test(flaky.sentence), false, 'never a case id');
});

test('A_reference_pass_enforced_under_another_memory_limit_is_an_environment_halt_not_a_verdict', async () => {
  const a = await scored({ plan: () => ({ failing: {}, memoryMax: 1_234_567 }) });
  assert.equal(a.ok, false);
  assert.equal(a.halted, true, 'a halt (exit 3 of the command), never a rejected or accepted verdict');
  assert.ok(a.sentence.includes('1234567') && a.sentence.includes(String(LIMITS.memoryBytes)), a.sentence);
  const same = await scored({ plan: () => 'clean' });
  assert.equal(same.halted, false, 'control: the registered value is no halt');
});

test('The_record_keeps_each_references_largest_peak_slowest_wall_ten_pass_wall_and_retained_size', async () => {
  const MIB = 1024 * 1024;
  const plan = ({ n }) => ({ failing: {}, peak: { h001: (100 + n) * MIB, h002: 50 * MIB }, wall: { h001: 1000 + n * 10, h002: 5000 + (n === 4 ? 777 : 0) } });
  const a = await scored({ plan });
  assert.equal(a.ok, true, a.sentence);
  assert.equal(a.rows.maxPeakBytes, 110 * MIB, 'the largest peak over the ten passes');
  assert.equal(a.rows.slowestCaseWallMs, 5777, 'the slowest case wall');
  assert.deepEqual(a.rows.passes.map(r => r.peakBytes), Array.from({ length: 10 }, (_, i) => Math.max((101 + i) * MIB, 50 * MIB)), 'per pass, the largest peak');
  assert.equal(a.rows.passes[3].slowestCaseWallMs, 5777);
  assert.equal(a.rows.passes.every(r => r.wallMs === 2000), true, 'the fake child advances the clock 2000 ms per pass');
  assert.equal(a.rows.tenPassWallMs, 20_000, 'the ten passes\' wall');
  assert.ok(a.rows.retainedBytes > 0, 'the retained size of the scoring folder');
  assert.equal(a.rows.mostAttempts, 1);
});
