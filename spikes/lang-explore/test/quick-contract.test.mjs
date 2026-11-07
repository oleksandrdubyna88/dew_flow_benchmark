// The submission contract of the quick run (F1.S3 (b)): one source for the frozen list and the folders a copy leaves out, the identity of the contract a scoring was made under, and the deviations (the frozen files the agent
// changed, as a count and paths beside `accepted`, never a verdict). Invented fixtures only; the real templates and snapshots are read where the case says so.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { excludedDirs as scorerExcludedDirs } from '../../minigit-bench/lib/score-deps.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import * as copyModule from '../lib/copy-quick.mjs';
import { AGENT_EDITABLE, DERIVED_FILES, FROZEN_FOLDERS, PIN_FILES, SCRIPTS, excludedDirs, frozenEntries, frozenOf } from '../lib/frozen-set.mjs';
import { REPO } from '../lib/paths.mjs';

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
