// Template drift on a kept tree, judged per file by BYTES (F1.S3 (b), review round). A frozen file that is a template file (a script or a pin file) or the derived `PACKAGES.md` differs from today's bytes in two ways: the harness moved
// since the session began, or the agent changed it. It is the harness's only when the tree's bytes equal the bytes the file had when the session began; every other difference, a deletion included, is the agent's. Where the old
// bytes cannot be read the file is undecided, and the deviations are then unknown, never a known count. Invented fixtures only.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { harnessPinsNow } from '../lib/harness-pin.mjs';
import { exploreSection, finalizeSessionRecord } from '../lib/records.mjs';
import { packagesMdHashes } from '../lib/workspace-quick.mjs';
import { templateHistory } from '../lib/template-drift.mjs';
import { templateHashes } from '../../lang-bench/lib/templates-identity.mjs';
import { frozenFiles, ran, rec, scoreWith, scoringArea } from './quick-score-fixture.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { recordOf, scoreOf } from './support.mjs';

const sha = text => createHash('sha256').update(text).digest('hex');
const f = text => ({ bytes: Buffer.from(text), mode: 0o644 });
const passAll = () => ran(['s01', 'h001', 'h002'].map(id => rec(id)));
/** The bytes the files had when the session began, as the history seam answers for them; a file not in the table is one whose old bytes cannot be read. */
const historyOf = table => ({ rel }) => (rel in table ? { known: true, sha256: sha(table[rel]) } : { known: false, why: `no old bytes for ${rel}` });
const OLD = Object.freeze({ '.npmrc': 'offline=true\n', 'build.sh': '#!/bin/bash\n# the build of an older template\n' });
const TODAY = () => ({ ...frozenFiles(), '.npmrc': f('offline=true\nignore-scripts=true\n') });

function treeWith(t, bytes) {
  const a = scoringArea(t);
  for (const [rel, text] of Object.entries(bytes)) {
    if (text === null) rmSync(path.join(a.source, rel), { force: true });
    else writeFileSync(path.join(a.source, rel), text, { mode: rel.endsWith('.sh') ? 0o755 : 0o644 });
  }
  return a;
}

test('Under_a_moved_template_a_changed_template_file_whose_bytes_were_the_old_templates_is_harness_drift_and_not_a_deviation', async t => {
  const a = treeWith(t, { ...OLD, 'samples/s01.in': 'the agent changed a sample\n' });
  const drifted = await scoreWith(a, { runFn: passAll(), files: TODAY(), history: historyOf(OLD) });
  assert.equal(drifted.ok, true, drifted.reason);
  assert.deepEqual(drifted.value.explore.harnessDrift.map(c => c.path).sort(), ['.npmrc', 'build.sh'], 'the files that are the old template\'s bytes are the harness\'s');
  assert.deepEqual(drifted.value.explore.frozenChanges.map(c => c.path), ['samples/s01.in'], 'the sample the agent changed is still the agent\'s');
  assert.deepEqual(exploreSection({ score: drifted.value, audit: [] }).deviations.value, { count: 1, paths: ['samples/s01.in'] }, 'and so is the only deviation');
  assert.deepEqual(exploreSection({ score: drifted.value, audit: [] }).harnessDrift.value.map(c => c.path).sort(), ['.npmrc', 'build.sh'], 'the record shows the drift it did not charge');
  const plain = await scoreWith(a, { runFn: passAll(), files: TODAY() });
  assert.deepEqual(plain.value.explore.frozenChanges.map(c => c.path).sort(), ['.npmrc', 'build.sh', 'samples/s01.in'], 'with no history to read all three are the agent\'s, as before');
  assert.equal('harnessDrift' in plain.value.explore, false);
});

test('Under_a_moved_template_a_build_sh_whose_bytes_were_never_a_templates_is_still_a_deviation_and_that_is_the_point', async t => {
  const a = treeWith(t, { 'build.sh': '#!/bin/bash\nexit 0\n', '.npmrc': OLD['.npmrc'], 'tsconfig.json': null });
  const files = { ...TODAY(), 'tsconfig.json': f('{"strict":true}\n') };
  writeFileSync(path.join(a.source, 'tsconfig.json'), '{"strict":false}\n');
  const made = await scoreWith(a, { runFn: passAll(), files, history: historyOf({ ...OLD, 'tsconfig.json': '{"strict":true}\n' }) });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(made.value.explore.harnessDrift.map(c => c.path), ['.npmrc'], 'only the file that is the old template\'s bytes');
  assert.deepEqual(made.value.explore.frozenChanges.map(c => c.path).sort(), ['build.sh', 'tsconfig.json'], 'a rewritten build.sh and strict turned off are the agent\'s though the template moved');
  assert.equal(exploreSection({ score: made.value, audit: [] }).deviations.value.count, 2);
});

test('Under_a_moved_template_a_deleted_template_file_is_the_agents_whatever_the_old_bytes_were', async t => {
  const a = treeWith(t, { 'build.sh': null });
  const made = await scoreWith(a, { runFn: passAll(), files: frozenFiles(), history: () => ({ known: false, why: 'unreadable' }) });
  assert.deepEqual(made.value.explore.frozenChanges, [{ path: 'build.sh', state: 'absent' }], 'a file that is gone is a deletion; the old bytes are not needed to say so');
  assert.equal('undecidedDrift' in made.value.explore, false);
});

test('Where_the_old_bytes_cannot_be_read_the_deviations_are_unknown_never_a_known_count', async t => {
  const a = treeWith(t, { 'build.sh': '#!/bin/bash\n# some bytes\n', 'samples/s01.in': 'the agent changed a sample\n' });
  const made = await scoreWith(a, { runFn: passAll(), files: frozenFiles(), history: historyOf({}) });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(made.value.explore.undecidedDrift.map(c => c.path), ['build.sh'], 'the file is named, with the reason');
  assert.match(made.value.explore.undecidedDrift[0].why, /no old bytes for build\.sh/);
  assert.deepEqual(made.value.explore.frozenChanges.map(c => c.path), ['samples/s01.in'], 'the undecided file is not charged to the agent');
  const dev = exploreSection({ score: made.value, audit: [] }).deviations;
  assert.equal(dev.known, false, 'unknown, not 1');
  assert.match(dev.why, /build\.sh/);
  assert.deepEqual(dev.counted, { count: 1, paths: ['samples/s01.in'] }, 'what is decided is still said');
});

test('PACKAGES_md_rendered_from_the_series_own_record_is_judged_by_its_bytes_alike_for_both_stacks', async t => {
  for (const stack of ['csharp', 'node']) {
    const a = treeWith(t, { 'PACKAGES.md': 'the file as the series\' own record rendered it\n' });
    const files = { ...frozenFiles(), 'PACKAGES.md': f('the file as today\'s record renders it\n') };
    const drifted = await scoreWith(a, { stack, runFn: passAll(), files, history: historyOf({ 'PACKAGES.md': 'the file as the series\' own record rendered it\n' }) });
    assert.deepEqual(drifted.value.explore.harnessDrift.map(c => c.path), ['PACKAGES.md'], `${stack}: the derived file is the harness's`);
    assert.deepEqual(drifted.value.explore.frozenChanges, [], `${stack}: and no deviation`);
    const edited = treeWith(t, { 'PACKAGES.md': 'a line the agent typed\n' });
    const charged = await scoreWith(edited, { stack, runFn: passAll(), files, history: historyOf({ 'PACKAGES.md': 'the file as the series\' own record rendered it\n' }) });
    assert.deepEqual(charged.value.explore.frozenChanges.map(c => c.path), ['PACKAGES.md'], `${stack}: a typed line is the agent's`);
  }
});

// ---- the history of a series -----------------------------------------------------------------------------------------------------------

const COMMIT = 'a'.repeat(40);

test('A_series_with_a_recorded_map_reads_the_old_bytes_from_it_and_one_without_reads_them_from_git_at_its_commit', () => {
  const recorded = templateHistory({ inputs: { subject: { sha: COMMIT }, templateHashes: { 'node/web/build.sh': sha('recorded') }, packagesMdSha256: { node: sha('packages') } }, readAt: () => { throw new Error('git is not asked when the map has it'); } });
  assert.deepEqual(recorded({ stack: 'node', kind: 'web', rel: 'build.sh' }), { known: true, sha256: sha('recorded') });
  assert.deepEqual(recorded({ stack: 'node', kind: 'web', rel: 'PACKAGES.md' }), { known: true, sha256: sha('packages') });
  const asked = [];
  const git = templateHistory({ inputs: { subject: { sha: COMMIT } }, readAt: (commit, rel) => { asked.push([commit, rel]); return rel.endsWith('csharp/web/build.sh') ? Buffer.from('old build') : null; } });
  assert.deepEqual(git({ stack: 'csharp', kind: 'web', rel: 'build.sh' }), { known: true, sha256: sha('old build') });
  assert.deepEqual(asked, [[COMMIT, 'spikes/lang-bench/templates/csharp/web/build.sh']]);
  const missing = git({ stack: 'csharp', kind: 'web', rel: 'global.json' });
  assert.equal(missing.known, false, 'a file git cannot show is unknown, not a hash of nothing');
  assert.match(missing.why, /global\.json/);
  for (const subject of [undefined, { sha: 'unknown' }, { sha: 'main' }]) {
    const none = templateHistory({ inputs: { subject }, readAt: () => Buffer.from('x') })({ stack: 'node', kind: 'web', rel: 'build.sh' });
    assert.equal(none.known, false, `a series with no usable commit (${JSON.stringify(subject)}) cannot be read from git`);
  }
});

test('For_a_series_made_before_the_matrix_PACKAGES_md_is_rendered_from_its_own_record_and_recipe_with_every_package_offered', () => {
  const recipe = { stacks: { node: { packages: [{ name: 'pino', spec: '10.4.0', purpose: 'Structured logging' }] }, csharp: { packages: [] } } };
  const record = { node: { treeSha256: 'b'.repeat(64), madeOn: '2026-11-04', resolved: {}, packages: [{ ecosystem: 'npm', name: 'pino', version: '10.4.0', integrity: 'sha512-x' }] }, csharp: { treeSha256: 'c'.repeat(64), madeOn: '2026-11-04', resolved: {}, packages: [] } };
  const readAt = (commit, rel) => (rel.endsWith('cache-snapshots-lang.json') ? Buffer.from(JSON.stringify(record)) : rel.endsWith('cache-recipe-lang.json') ? Buffer.from(JSON.stringify(recipe)) : null);
  const old = templateHistory({ inputs: { subject: { sha: COMMIT } }, readAt })({ stack: 'node', kind: 'olympiad', rel: 'PACKAGES.md' });
  assert.equal(old.known, true, old.why);
  assert.match(old.sha256, /^[0-9a-f]{64}$/);
  const none = templateHistory({ inputs: { subject: { sha: COMMIT } }, readAt: () => null })({ stack: 'node', kind: 'olympiad', rel: 'PACKAGES.md' });
  assert.equal(none.known, false, 'no record at that commit: unknown');
});

test('A_series_records_the_bytes_of_every_template_file_and_the_PACKAGES_md_of_its_kind_at_init', () => {
  const pins = harnessPinsNow();
  assert.deepEqual(Object.keys(pins).sort(), ['runnerPin', 'templateHashes', 'templatesIdentity']);
  assert.deepEqual(pins.templateHashes, templateHashes(), 'a sha256 for every file of every template');
  assert.ok(pins.templateHashes['node/web/.npmrc'] && pins.templateHashes['csharp/olympiad/build.sh']);
  const hashes = packagesMdHashes({ kind: 'web' });
  assert.equal(hashes.ok, true, hashes.reason);
  assert.deepEqual(Object.keys(hashes.value).sort(), ['csharp', 'node']);
  assert.match(hashes.value.node, /^[0-9a-f]{64}$/);
  assert.notEqual(packagesMdHashes({ kind: 'olympiad' }).value.csharp, hashes.value.csharp, 'the file depends on the kind (JwtBearer is not offered in the olympiad kind)');
});

test('The_record_carries_the_harness_drift_beside_the_deviations', () => {
  const score = scoreOf();
  const withDrift = { ...score, explore: { ...score.explore, harnessDrift: [{ path: 'build.sh', state: 'changed' }] } };
  const dir = tmpDir('record-drift');
  writeFileSync(path.join(dir, 'record.json'), JSON.stringify(recordOf()));
  const written = finalizeSessionRecord({ sessionDir: dir, score: withDrift, audit: [] });
  assert.deepEqual(written.explore.harnessDrift, { known: true, value: [{ path: 'build.sh', state: 'changed' }] });
  assert.deepEqual(exploreSection({ score, audit: [] }).harnessDrift, { known: true, value: [] }, 'none is a known empty list');
});
