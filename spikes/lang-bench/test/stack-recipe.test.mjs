// F1.S3 (b), review round: the per-stack recipe hash. `recipeSha256` of a record hashes the whole recipe file, so a one-stack rebuild or a `kinds` edit moves it for the other stack and no check can hold it equal. `stackRecipeSha256` hashes only
// what decides how that stack's snapshot is built (`env`, `resolve`, `steps`, `capture`, the project tree), and a check holds the registered value equal to today's for both stacks. A record made before the field is checked through the recipe text
// its whole-file hash names (found in the working tree or in git), or reported as NOT CHECKED, loudly; never a silent pass.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { buildRecord } from '../lib/cache-store.mjs';
import { stackRecipeProblems, stackRecipeSha256 } from '../lib/stack-recipe.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const readJson = rel => JSON.parse(readFileSync(path.join(SPIKE, rel), 'utf8'));
const recipe = () => readJson('inputs/cache-recipe-lang.json');
const copyOf = x => structuredClone(x);
const hashOf = (spec, projectSha256 = null) => stackRecipeSha256({ spec, projectSha256 });

test('The_stack_recipe_hash_moves_with_what_builds_the_snapshot_and_with_nothing_else', () => {
  const base = recipe().stacks.node;
  const same = hashOf(base);
  assert.match(same, /^[0-9a-f]{64}$/);
  const moved = { ...copyOf(base), packages: copyOf(base).packages.map(p => ({ ...p, kinds: ['web'], purpose: `${p.purpose} more`, licence: 'X' })), packagesNote: 'another note' };
  assert.equal(hashOf(moved), same, 'the kinds, purposes, licences and notes of the packages do not decide the build');
  const reordered = { ...copyOf(base), steps: copyOf(base).steps };
  assert.equal(hashOf({ capture: reordered.capture, steps: reordered.steps, resolve: reordered.resolve, env: reordered.env }), same, 'the order of the keys does not matter');
  for (const [what, change] of [['a step', s => { s.steps[1].argv.push('--x'); }], ['the env', s => { s.env.npm_config_x = '1'; }], ['a resolve line', s => { s.resolve.PINO.argv.push('x'); }], ['the capture', s => { s.capture.npm = 'box/other'; }]]) {
    const spec = copyOf(base);
    change(spec);
    assert.notEqual(hashOf(spec), same, `${what} moves it`);
  }
  assert.notEqual(hashOf(base, 'a'.repeat(64)), same, 'the project tree moves it');
  assert.notEqual(hashOf(recipe().stacks.csharp), same, 'and the two stacks differ');
});

test('A_record_built_now_carries_the_stack_recipe_hash_beside_the_whole_file_hash_which_stays_as_history', () => {
  const snapshot = { dir: `/c/node-${'a'.repeat(64)}`, treeSha256: 'a'.repeat(64), parts: { npm: 'b'.repeat(64) }, sizeBytes: 1, packages: [], resolved: {}, steps: [] };
  const record = buildRecord({ snapshot, manifest: [], recipeSha256: 'whole', stackRecipeSha256: 'stack', projectSha256: null });
  assert.equal(record.recipeSha256, 'whole', 'the earlier field is kept');
  assert.equal(record.stackRecipeSha256, 'stack');
});

const registered = { csharp: { recipeSha256: 'whole-c', projectSha256: 'p'.repeat(64) }, node: { recipeSha256: 'whole-n', projectSha256: null } };
const sha = text => createHash('sha256').update(text).digest('hex');

test('The_check_holds_the_registered_value_equal_to_todays_for_both_stacks_and_names_a_difference', () => {
  const r = recipe();
  const record = { csharp: { ...registered.csharp, stackRecipeSha256: hashOf(r.stacks.csharp, 'p'.repeat(64)) }, node: { ...registered.node, stackRecipeSha256: hashOf(r.stacks.node) } };
  const treeHashOf = () => 'p'.repeat(64);
  const clean = stackRecipeProblems({ record, recipe: r, treeHashOf, findRecipe: () => null });
  assert.deepEqual([clean.problems, clean.unchecked], [[], []]);
  assert.deepEqual(clean.verified.map(v => v.stack), ['csharp', 'node']);
  const changed = copyOf(r);
  changed.stacks.node.steps[1].argv.push('--ignore-engines');
  const bad = stackRecipeProblems({ record, recipe: changed, treeHashOf, findRecipe: () => null });
  assert.equal(bad.problems.length, 1, 'only the stack that changed');
  assert.match(bad.problems[0], /node.*registered.*today/);
  const project = stackRecipeProblems({ record, recipe: r, treeHashOf: () => 'q'.repeat(64), findRecipe: () => null });
  assert.match(project.problems.join(';'), /csharp/, 'a changed project tree is a difference too');
});

test('A_record_made_before_the_field_is_checked_through_the_recipe_text_its_hash_names_or_reported_not_checked_never_passed_in_silence', () => {
  const r = recipe();
  const old = copyOf(r);
  old.stacks.node.packages.forEach(p => { delete p.kinds; });
  const oldText = JSON.stringify(old);
  const record = { csharp: { ...registered.csharp, recipeSha256: sha(oldText) }, node: { ...registered.node, recipeSha256: sha(oldText) } };
  const treeHashOf = () => 'p'.repeat(64);
  const found = stackRecipeProblems({ record, recipe: r, treeHashOf, findRecipe: hash => (hash === sha(oldText) ? Buffer.from(oldText) : null) });
  assert.deepEqual(found.problems, [], 'the node recipe builds as it did when the older text was current: the kinds of the packages are not part of it');
  assert.deepEqual(found.verified.map(v => [v.stack, v.how]), [['csharp', 'from the recipe text of the recorded whole-file hash'], ['node', 'from the recipe text of the recorded whole-file hash']].map(([s, h]) => [s, h]));
  const drifted = copyOf(old);
  drifted.stacks.node.env.npm_config_x = '1';
  const wrong = stackRecipeProblems({ record, recipe: r, treeHashOf, findRecipe: () => Buffer.from(JSON.stringify(drifted)) });
  assert.match(wrong.problems.join(';'), /node/, 'a stack that does not build as the older text built is a difference');
  const missing = stackRecipeProblems({ record, recipe: r, treeHashOf, findRecipe: () => null });
  assert.deepEqual(missing.problems, [], 'not a failure that can never be cured: the registered snapshots are never rebuilt for it');
  assert.deepEqual(missing.unchecked.map(u => u.stack), ['csharp', 'node'], 'but it is named, once per stack');
  assert.match(missing.unchecked[0].why, /NOT CHECKED.*no stackRecipeSha256.*whole-file hash/);
});

test('The_registered_records_of_this_repository_pass_the_check_or_say_why_they_were_not_checked', () => {
  const record = readJson('inputs/cache-snapshots-lang.json');
  const spikeProject = rel => treeHash(path.join(SPIKE, rel));
  const out = stackRecipeProblems({ record, recipe: recipe(), treeHashOf: () => spikeProject(recipe().stacks.csharp.project) });
  assert.deepEqual(out.problems, [], 'the recipe still builds both registered snapshots as it did');
  assert.equal(out.verified.length + out.unchecked.length, 2, 'both stacks are accounted for');
  for (const u of out.unchecked) assert.match(u.why, /NOT CHECKED/, 'a stack that could not be checked says so in capital letters');
});

test('The_command_checks_the_recipe_against_the_record_and_exits_1_on_a_difference_and_0_otherwise', async t => {
  const root = ownedFolder(t, 'chk');
  const r = recipe();
  const csharpProject = treeHash(path.join(SPIKE, r.stacks.csharp.project));
  const good = { csharp: { recipeSha256: 'x', projectSha256: csharpProject, stackRecipeSha256: hashOf(r.stacks.csharp, csharpProject) }, node: { recipeSha256: 'x', stackRecipeSha256: hashOf(r.stacks.node) }, superseded: [] };
  const file = path.join(root, 'rec.json');
  writeFileSync(file, JSON.stringify(good));
  const run = () => runBounded(process.execPath, [path.join(SPIKE, 'make-snapshots.mjs'), '--check-recipe', '--record', file], { timeoutMs: 120_000 });
  const clean = await run();
  assert.equal(clean.value.code, 0, clean.value.stderr);
  assert.match(clean.value.stdout, /csharp: .*recorded/);
  writeFileSync(file, JSON.stringify({ ...good, node: { ...good.node, stackRecipeSha256: 'f'.repeat(64) } }));
  const bad = await run();
  assert.equal(bad.value.code, 1, 'a stack the recipe no longer builds as registered');
  assert.match(bad.value.stderr, /node snapshot was registered under a recipe hashing to f{64}/);
  writeFileSync(file, JSON.stringify({ ...good, node: { recipeSha256: 'no-such-recipe-text' } }));
  const unchecked = await run();
  assert.equal(unchecked.value.code, 0, 'a record made before the field cannot be cured, so it is not a failure ...');
  assert.match(unchecked.value.stderr, /NOT CHECKED: the node record has no stackRecipeSha256/, '... but it is said, on stderr, every time');
});
