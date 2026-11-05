// E1.S8, check 10 and the stack words: the implementation languages, runtimes, frameworks and flags of the series are public words kept in inputs/stack-words-lang.json (the shape and the matching rule
// of needles-lang.json). The check always reads that file and names a hit by its word. The real FORMAT and the real spec 01 are checked against the same file where the task repository is at hand.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { matchersOf, parseWordList, wordFindings } from '../lib/conformance-needles.mjs';
import { check, failsOf, fixtureRepo, isOk, REAL_FORMAT } from './support/conformance-fixtures.mjs';

const FILE = fileURLToPath(new URL('../inputs/stack-words-lang.json', import.meta.url));
const LISTED = ['C#', '.NET', 'dotnet', 'TypeScript', 'JavaScript', 'Node', 'Node.js', 'nodejs', 'V8', 'Fastify', 'ASP.NET', 'Kestrel', 'typebox', 'xunit', 'NuGet', 'npm', 'pnpm', 'tsc', 'tsx', 'MSBuild', 'Roslyn', 'CLR', 'DOTNET_', 'NODE_OPTIONS', 'max-old-space', '--max_old_space_size', 'GCHeapHardLimit', 'COMPlus_', 'node_modules', '.csproj', 'package.json', 'Program.cs'];
const stackWords = () => JSON.parse(readFileSync(FILE, 'utf8'));
const plant = word => ({ spec: [['**R2.** For n = 1', `**R2.** Use ${word} here. For n = 1`]] });

test('The_stack_words_file_holds_the_words_of_the_series_in_the_shape_of_the_known_task_needles', () => {
  const file = stackWords();
  assert.equal(file.schema, 1);
  assert.equal(typeof file.rule, 'string');
  assert.equal(parseWordList(file).ok, true);
  assert.deepEqual(file.caseSensitive, ['Node']);
  assert.deepEqual([...file.words, ...file.substrings].sort(), [...LISTED].sort());
  assert.match(file.rule, /case-insensitive/);
});

for (const word of LISTED) {
  test(`The_stack_word_${word.replace(/[^A-Za-z0-9]/g, '_')}_in_a_requirement_is_found_by_the_no_needle_check_and_named`, async t => {
    assert.ok(isOk(await check(fixtureRepo(t, 'olympiad')), 'no-needle'), 'the unchanged spec is ok');
    const result = await check(fixtureRepo(t, 'olympiad', plant(word)));
    assert.ok(failsOf(result, 'no-needle').some(line => line.endsWith(`needle ${word}`)), result.lines.join('\n'));
    assert.equal(result.exit, 1);
  });
}

test('A_stack_word_in_a_sample_file_is_found_and_a_word_inside_another_word_is_not', async t => {
  const hit = await check(fixtureRepo(t, 'daemon', { 's01.in': [['connect c1 tcp', '# uses Fastify\nconnect c1 tcp']] }));
  assert.match(failsOf(hit, 'no-needle').join('\n'), /samples\/s01\.in line 4: needle Fastify/);
  const inside = await check(fixtureRepo(t, 'olympiad', plant('Nodes and npmx')));
  assert.ok(isOk(inside, 'no-needle'), 'Nodes and npmx are other words');
});

test('A_retyped_stack_word_list_would_miss_a_word_added_to_the_file_and_that_is_the_point', async t => {
  const added = path.join(fixtureRepo(t, 'olympiad').repo, 'stack-words.json');
  writeFileSync(added, JSON.stringify({ ...stackWords(), words: [...stackWords().words, 'Zigzag'] }));
  const fixture = fixtureRepo(t, 'olympiad', plant('Zigzag'));
  const retyped = matchersOf(parseWordList({ schema: 1, rule: 'x', words: ['Node'], substrings: [] }).value);
  assert.deepEqual(wordFindings('x', 'Use Zigzag here', retyped), [], 'a retyped list does not know the new word');
  assert.ok(isOk(await check(fixture), 'no-needle'), 'with the file as it is, Zigzag is no stack word');
  const result = await check(fixture, [], { stackWordsFile: added });
  assert.ok(failsOf(result, 'no-needle').some(line => line.endsWith('needle Zigzag')), result.lines.join('\n'));
});

test('A_stack_words_file_that_is_missing_or_not_valid_is_exit_4', async t => {
  const fixture = fixtureRepo(t, 'olympiad');
  const bad = path.join(fixture.repo, 'bad.json');
  writeFileSync(bad, '{ "schema": 2 }');
  for (const file of [bad, path.join(fixture.repo, 'none.json')]) {
    const result = await check(fixture, [], { stackWordsFile: file });
    assert.equal(result.exit, 4);
    assert.deepEqual(result.lines, []);
  }
});

test('No_stack_word_stands_in_the_real_FORMAT_or_the_real_spec_01', async t => {
  const repo = process.env.LANG_V2_TASK_REPO;
  if (repo === undefined || repo === '') {
    if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error('BENCH_REQUIRE_SANDBOX=1 and LANG_V2_TASK_REPO is not set');
    t.skip('LANG_V2_TASK_REPO is not set');
    return;
  }
  const words = matchersOf(parseWordList(stackWords()).value);
  for (const text of [readFileSync(REAL_FORMAT, 'utf8'), readFileSync(`${repo}/todo/tasks/01-descent-to-one.md`, 'utf8')]) {
    assert.deepEqual(wordFindings('real', text, words), []);
  }
});

