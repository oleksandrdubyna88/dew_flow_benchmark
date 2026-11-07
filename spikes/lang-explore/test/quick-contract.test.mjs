// The submission contract of the quick run (F1.S3 (b)): one source for the frozen list and the folders a copy leaves out, the identity of the contract a scoring was made under, and the deviations (the frozen files the agent
// changed, as a count and paths beside `accepted`, never a verdict). Invented fixtures only; the real templates and snapshots are read where the case says so.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { excludedDirs as scorerExcludedDirs } from '../../minigit-bench/lib/score-deps.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import * as copyModule from '../lib/copy-quick.mjs';
import { AGENT_EDITABLE, DERIVED_FILES, FROZEN_FOLDERS, PIN_FILES, SCRIPTS, excludedDirs, frozenEntries, frozenOf } from '../lib/frozen-set.mjs';
import { CONTRACT_MODULES, SNAPSHOT_RECORD, TEMPLATES_FOLDER, contractIdentity, templatesIdentity } from '../lib/contract-identity.mjs';
import { exploreSection, finalizeSessionRecord, missingPaths } from '../lib/records.mjs';
import { harnessPinsNow, templateDriftPossible } from '../lib/harness-pin.mjs';
import { REPO } from '../lib/paths.mjs';
import { acceptedOf } from '../lib/score.mjs';
import { scoreQuick } from '../lib/score-quick.mjs';
import { frozenFiles, ran, rec, score, scoreWith, scoringArea } from './quick-score-fixture.mjs';
import { recordOf, scoreOf } from './support.mjs';

const read = rel => readFileSync(path.join(REPO, rel), 'utf8');
const STACKS = Object.freeze(['csharp', 'node']);
const KINDS = Object.freeze(['olympiad', 'daemon', 'web']);

// ---- one source -----------------------------------------------------------------------------------------------------------------

/** The `.mjs` files under the given folders (relative to the repository), as `{ rel, text }`. */
const sourcesUnder = folders => folders.flatMap(folder => readdirSync(path.join(REPO, folder), { recursive: true }).filter(f => f.endsWith('.mjs')).map(f => ({ rel: path.join(folder, f), text: read(path.join(folder, f)) })));

/** True when a text holds every pin-file name of some stack as a quoted literal: a second list of them, whatever it is called. */
const holdsPinList = text => STACKS.some(stack => PIN_FILES[stack].every(name => [`'${name}'`, `"${name}"`, `\`${name}\``].some(q => text.includes(q))));

const LIB_FOLDERS = ['spikes/lang-explore/lib', 'spikes/lang-bench/lib'];

test('The_frozen_list_and_the_excluded_folders_have_one_source', () => {
  // the scan: no file of the two libraries but the source holds the pin-file names of a stack as literals ...
  const holders = sourcesUnder(LIB_FOLDERS).filter(f => holdsPinList(f.text)).map(f => f.rel);
  assert.deepEqual(holders, ['spikes/lang-explore/lib/frozen-set.mjs'], 'the one source, and no second list');
  // ... and the same scan finds the source, so it can match (a planted second list is found too)
  assert.equal(holdsPinList(read('spikes/lang-explore/lib/frozen-set.mjs')), true);
  assert.equal(holdsPinList(`const copy = ['global.json', 'nuget.config', 'Directory.Build.props'];`), true, 'the scan can fail');
  assert.equal(holdsPinList(`const copy = ['global.json', 'nuget.config'];`), false, 'a part of a list is another list (the push check\'s)');
  // the builder, the copy and lang-bench's template test read the one list
  assert.match(read('spikes/lang-explore/lib/workspace-quick.mjs'), /from '\.\/frozen-set\.mjs'/);
  assert.match(read('spikes/lang-explore/lib/copy-quick.mjs'), /from '\.\/frozen-set\.mjs'/);
  assert.match(read('spikes/lang-bench/test/templates.test.mjs'), /lang-explore\/lib\/frozen-set\.mjs/);
  assert.match(read('spikes/lang-bench/lib/workspace-diff.mjs'), /lang-explore\/lib\/frozen-set\.mjs/);
  // the folders a copy leaves out are the scorer's own list, the same function
  assert.equal(excludedDirs, scorerExcludedDirs, 'one copy of the folder list, not two');
  assert.deepEqual(excludedDirs('csharp'), ['bin', 'obj', 'node_modules']);
  assert.equal(copyModule.excludedDirs, undefined, 'the copy imports the list and does not offer a second name for it');
});

test('The_frozen_files_of_a_file_map_are_the_entries_of_the_one_list_and_nothing_else', () => {
  const files = Object.fromEntries(['build.sh', 'run.sh', 'STATEMENT.md', 'PACKAGES.md', 'samples/a.in', 'runner/x.py', 'App.csproj', 'global.json', 'src/main.ts', '.npmrc', 'package.json'].map(rel => [rel, { bytes: Buffer.from(rel), mode: 0o644 }]));
  assert.deepEqual(Object.keys(frozenOf(files, 'csharp')).sort(), ['PACKAGES.md', 'STATEMENT.md', 'build.sh', 'global.json', 'run.sh', 'runner/x.py', 'samples/a.in']);
  assert.deepEqual(Object.keys(frozenOf(files, 'node')).sort(), ['.npmrc', 'PACKAGES.md', 'STATEMENT.md', 'build.sh', 'run.sh', 'runner/x.py', 'samples/a.in']);
  for (const stack of STACKS) for (const name of AGENT_EDITABLE[stack]) assert.equal(PIN_FILES[stack].includes(name), false, `${stack}: ${name} is the agent's to edit and so not frozen`);
  assert.deepEqual([...SCRIPTS, ...DERIVED_FILES, ...FROZEN_FOLDERS].sort(), ['PACKAGES.md', 'STATEMENT.md', 'build.sh', 'run.sh', 'runner/', 'samples/']);
});

/** The names a build.sh lists after "pin files ...:" in its comment line. */
const pinNamesIn = text => (text.split('\n').find(line => line.startsWith('#') && /pin files/.test(line)) ?? '').replace(/^.*:\s*/, '').replace(/\.$/, '').split(', ').filter(Boolean).sort();

test('The_scripts_comment_lines_name_exactly_the_pin_files_of_the_list_for_each_stack', () => {
  for (const stack of STACKS) {
    for (const kind of KINDS) assert.deepEqual(pinNamesIn(read(`spikes/lang-bench/templates/${stack}/${kind}/build.sh`)), [...PIN_FILES[stack]].sort(), `${stack}/${kind}`);
  }
  assert.deepEqual(pinNamesIn('# These files are this stack\'s pin files and stay as they are: a, b.'), ['a', 'b'], 'the reader can read a comment line');
  assert.deepEqual(pinNamesIn('# nothing here'), [], 'and finds none where there is none');
});

test('The_module_doc_sentence_of_the_frozen_list_names_exactly_its_entries', () => {
  const doc = read('research/module_lang_bench.md');
  const sentence = doc.split('\n').flatMap(line => line.split('. ')).find(part => part.includes('The frozen list (one source, `spikes/lang-explore/lib/frozen-set.mjs`'));
  assert.ok(sentence !== undefined, 'the doc has the sentence');
  const named = [...sentence.matchAll(/`([^`]+)`/g)].map(m => m[1]).filter(name => name !== 'spikes/lang-explore/lib/frozen-set.mjs').sort();
  assert.deepEqual(named, [...new Set(frozenEntries())].sort(), 'the sentence names every entry of the list and no other');
});

test('The_copy_leaves_out_the_folders_of_the_one_list_at_any_depth', () => {
  const root = tmpDir('contract-copy');
  const [source, dest] = [path.join(root, 'tree'), path.join(root, 'copy')];
  for (const dir of ['bin', 'obj', 'node_modules', 'src/node_modules', 'src/keep']) mkdirSync(path.join(source, dir), { recursive: true });
  const out = copyModule.quickPristineCopy({ source, dest, stack: 'csharp', files: {} });
  assert.deepEqual(out.changed, []);
  assert.deepEqual(readdirSync(dest, { recursive: true }).sort(), ['src', 'src/keep']);
  assert.equal(existsSync(path.join(dest, 'bin')), false);
});

// ---- the identity of the contract ---------------------------------------------------------------------------------------------------

const HEX64 = /^[0-9a-f]{64}$/;
/** A tree of its own holding what the identity reads (the contract modules, the templates, the snapshot record) and two files it must not read (the scorer and the builder of the statement). */
function contractRoot(label) {
  const root = path.join(tmpDir(label), 'repo');
  for (const rel of [...CONTRACT_MODULES, TEMPLATES_FOLDER, SNAPSHOT_RECORD, 'spikes/lang-explore/lib/score-quick.mjs', 'spikes/lang-explore/lib/workspace-quick.mjs']) {
    mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    cpSync(path.join(REPO, rel), path.join(root, rel), { recursive: true });
  }
  return root;
}
const touch = (root, rel, mutate) => writeFileSync(path.join(root, rel), mutate(readFileSync(path.join(root, rel), 'utf8')));

test('The_contract_identity_moves_with_a_template_a_snapshot_or_a_contract_module_and_with_nothing_else', () => {
  const root = contractRoot('contract-id');
  const base = contractIdentity({ root });
  assert.match(base, HEX64);
  assert.equal(contractIdentity({ root }), base, 'the same tree gives the same identity');
  assert.equal(contractIdentity({ root: contractRoot('contract-id-2') }), base, 'and so does a copy of it somewhere else');
  const moves = [
    ['a template file', () => touch(root, `${TEMPLATES_FOLDER}/node/web/.npmrc`, t => `${t}# moved\n`)],
    ['a registered snapshot', () => touch(root, SNAPSHOT_RECORD, t => t.replace(/("csharp": \{\s*"treeSha256": ")[0-9a-f]{4}/, '$10000'))],
    ...CONTRACT_MODULES.map(rel => [rel, () => touch(root, rel, t => `${t}// moved\n`)]),
  ];
  const seen = new Set([base]);
  for (const [what, change] of moves) {
    change();
    const now = contractIdentity({ root });
    assert.equal(seen.has(now), false, `${what} moves the identity`);
    seen.add(now);
  }
  const after = contractIdentity({ root });
  touch(root, 'spikes/lang-explore/lib/score-quick.mjs', t => `${t}// a fix to the scoring\n`);
  touch(root, 'spikes/lang-explore/lib/workspace-quick.mjs', t => t.replace('stack sentence', 'another sentence'));
  assert.equal(contractIdentity({ root }), after, 'a change to score-quick.mjs or to the statement does not move it');
  touch(root, SNAPSHOT_RECORD, t => t.replace(/"madeOn": "[^"]+"/, '"madeOn": "2030-01-01"'));
  assert.equal(contractIdentity({ root }), after, 'a field of the snapshot record other than its tree hashes does not move it');
});

test('The_templates_identity_is_the_templates_alone', () => {
  const root = contractRoot('templates-id');
  const base = templatesIdentity({ root });
  assert.match(base, HEX64);
  touch(root, CONTRACT_MODULES[0], t => `${t}// moved\n`);
  assert.equal(templatesIdentity({ root }), base, 'a contract module is not a template');
  touch(root, `${TEMPLATES_FOLDER}/csharp/web/build.sh`, t => `${t}# moved\n`);
  assert.notEqual(templatesIdentity({ root }), base, 'a template file is');
});

test('Every_record_json_carries_the_contract_identity_of_the_scoring_that_made_it', async t => {
  const made = await score(t, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal(made.ok, true, made.reason);
  assert.match(made.value.explore.contractIdentity, HEX64, 'the score names the identity it was made under');
  assert.equal(made.value.explore.contractIdentity, contractIdentity(), 'it is the identity of the contract in force');
  const explore = exploreSection({ score: made.value, audit: [] });
  assert.deepEqual(explore.contractIdentity, { known: true, value: made.value.explore.contractIdentity });
  const dir = tmpDir('record-identity');
  writeFileSync(path.join(dir, 'record.json'), JSON.stringify(recordOf()));
  const written = finalizeSessionRecord({ sessionDir: dir, score: { ...scoreOf(), explore: { ...scoreOf().explore, contractIdentity: 'a'.repeat(64) } }, audit: [] });
  assert.equal(written.explore.contractIdentity.value, 'a'.repeat(64), 'a record finished later names the identity in the score, not the one of the day it is finished');
  assert.equal(JSON.parse(readFileSync(path.join(dir, 'record.json'), 'utf8')).explore.contractIdentity.value, 'a'.repeat(64), 'and it is in record.json on disk');
  assert.deepEqual(missingPaths(written, true).filter(p => p.includes('contractIdentity')), []);
  const old = exploreSection({ score: scoreOf(), audit: [] });
  assert.equal(old.contractIdentity.known, false, 'a score made before the identity existed says so and is never given today\'s identity');
  assert.match(old.contractIdentity.why, /no contract identity/);
});

// ---- deviations ---------------------------------------------------------------------------------------------------------------------

test('A_frozen_file_the_agent_changed_is_one_deviation_with_its_path_beside_accepted', async t => {
  const a = scoringArea(t);
  writeFileSync(path.join(a.source, 'samples', 's01.in'), 'the agent changed a sample\n');
  const made = await scoreWith(a, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal(made.ok, true, made.reason);
  const section = exploreSection({ score: made.value, audit: [] });
  assert.deepEqual(section.deviations, { known: true, value: { count: 1, paths: ['samples/s01.in'] } });
  assert.deepEqual(section.frozenChanges.value, [{ path: 'samples/s01.in', state: 'changed' }], 'frozenChanges stays as it was (D34)');
  assert.equal(section.accepted.value, true, 'beside accepted, and not in it');
  writeFileSync(path.join(a.source, 'build.sh'), '#!/bin/bash\necho changed\n', { mode: 0o755 });
  const two = exploreSection({ score: (await scoreWith(a, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) })).value, audit: [] });
  assert.deepEqual(two.deviations.value, { count: 2, paths: ['build.sh', 'samples/s01.in'] }, 'one deviation per file, the paths sorted');
});

test('An_untouched_tree_has_zero_deviations_known_never_unknown', async t => {
  const a = scoringArea(t);
  writeFileSync(path.join(a.source, 'tampered'), 'x');
  const files = { 'samples/s01.in': frozenFiles()['samples/s01.in'], 'samples/s01.out': frozenFiles()['samples/s01.out'] };
  const made = await scoreWith(a, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))), files });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(exploreSection({ score: made.value, audit: [] }).deviations, { known: true, value: { count: 0, paths: [] } });
  assert.deepEqual(exploreSection({ score: scoreOf(), audit: [] }).deviations, { known: true, value: { count: 0, paths: [] } }, 'also from a score that names no change at all');
  const changed = await scoreWith(scoringArea(t), { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.deepEqual(exploreSection({ score: changed.value, audit: [] }).deviations.value.paths, changed.value.explore.frozenChanges.map(c => c.path).sort(), 'the control: a tree that changed a frozen file lists it');
});

test('Deviations_never_change_accepted_and_that_is_the_point', () => {
  const clean = scoreOf();
  const deviating = { ...clean, explore: { ...clean.explore, frozenChanges: [{ path: 'build.sh', state: 'changed' }, { path: 'global.json', state: 'missing' }] } };
  assert.equal(acceptedOf(clean), true);
  assert.equal(acceptedOf(deviating), true, 'a tree with deviations and every case passed stays accepted');
  const failing = { ...scoreOf(['k001']), explore: { ...scoreOf(['k001']).explore, frozenChanges: [] } };
  assert.equal(acceptedOf(failing), false, 'the control: a failing case is what makes a tree not accepted');
  assert.equal(exploreSection({ score: deviating, audit: [] }).accepted.value, true);
  assert.equal(exploreSection({ score: deviating, audit: [] }).deviations.value.count, 2);
});

// ---- template drift -----------------------------------------------------------------------------------------------------------------

test('Under_a_moved_template_a_changed_template_file_is_harness_drift_and_not_a_deviation', async t => {
  const a = scoringArea(t);
  writeFileSync(path.join(a.source, '.npmrc'), 'offline=true\n');
  writeFileSync(path.join(a.source, 'build.sh'), '#!/bin/bash\n# the build of an older template\n', { mode: 0o755 });
  writeFileSync(path.join(a.source, 'samples', 's01.in'), 'the agent changed a sample\n');
  const files = { ...frozenFiles(), '.npmrc': { bytes: Buffer.from('offline=true\nignore-scripts=true\n'), mode: 0o644 } };
  const common = { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))), files };
  const drifted = await scoreWith(a, { ...common, templateDriftPossible: true });
  assert.deepEqual(drifted.value.explore.harnessDrift.map(c => c.path).sort(), ['.npmrc', 'build.sh'], 'the template files that differ are the harness\'s');
  assert.deepEqual(drifted.value.explore.frozenChanges.map(c => c.path), ['samples/s01.in'], 'the sample the agent changed is still the agent\'s');
  assert.deepEqual(exploreSection({ score: drifted.value, audit: [] }).deviations.value, { count: 1, paths: ['samples/s01.in'] }, 'and so is the only deviation');
  const plain = await scoreWith(a, common);
  assert.deepEqual(plain.value.explore.frozenChanges.map(c => c.path).sort(), ['.npmrc', 'build.sh', 'samples/s01.in'], 'without drift all three are the agent\'s, as before');
  assert.equal('harnessDrift' in plain.value.explore, false);
});

test('A_series_records_the_runner_pin_and_the_templates_identity_at_init_and_the_drift_rule_reads_them', () => {
  const pins = harnessPinsNow();
  assert.deepEqual(Object.keys(pins).sort(), ['runnerPin', 'templatesIdentity']);
  assert.equal(pins.templatesIdentity, templatesIdentity(), 'the identity of the templates of the day');
  assert.equal(templateDriftPossible({ inputs: pins }), false, 'a series made under these templates: no drift');
  assert.equal(templateDriftPossible({ inputs: { ...pins, templatesIdentity: 'f'.repeat(64) } }), true, 'another identity: the templates moved');
  assert.equal(templateDriftPossible({ inputs: { runnerPin: pins.runnerPin } }), true, 'none recorded: made before the identity existed, so the drift is possible');
  assert.match(readFileSync(path.join(REPO, 'spikes/lang-explore/lib/init-quick.mjs'), 'utf8'), /harnessPinsNow\(\)/, 'init records both');
});
