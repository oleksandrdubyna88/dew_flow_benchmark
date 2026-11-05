// E1.S8 batch 3, C3 and C4: the stack words, the known-task needles and the author's literals are matched without regard to case (the word Node excepted), and a needle wrapped over two lines is found.
// The conformance check has its own matcher (lib/conformance-needles.mjs); the shared push rules are not changed. No false hit on the real FORMAT's fixed sentences, the real spec 01 or the fixtures.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readFacts } from '../lib/conformance-facts.mjs';
import { readSentences } from '../lib/conformance-sentences.mjs';
import { parseFormat } from '../lib/format-structure.mjs';
import { check, failsOf, fixtureRepo, isOk, REAL_FORMAT } from './support/conformance-fixtures.mjs';

const plant = text => ({ spec: [['**R2.** For n = 1', `**R2.** ${text} For n = 1`]] });
const hitsOf = async (t, text, extra = []) => failsOf(await check(fixtureRepo(t, 'olympiad', plant(text)), extra), 'no-needle');

const CASED = ['FASTIFY', 'typescript', 'Javascript', 'DOTNET', 'Kestrel', 'XUNIT', 'NUGET', 'NPM', 'PNPM', 'TSC', 'Tsx', 'msbuild', 'ROSLYN', 'clr', 'V8', 'v8'];
for (const word of CASED) {
  test(`The_stack_word_${word}_is_found_in_any_case`, async t => {
    const fails = await hitsOf(t, `We use ${word} here.`);
    assert.ok(fails.length >= 1 && fails.some(line => /needle /.test(line)), fails.join('\n'));
  });
}

const NEW_FORMS = ['nodejs', 'NodeJS', 'Node.js', 'node_modules', 'COMPLUS_', 'comPlus_x', '--max_old_space_size=64', '--MAX-OLD-SPACE-SIZE', 'App.csproj', 'package.json', 'Program.cs', 'dotnet_gcServer', 'node_options'];
for (const form of NEW_FORMS) {
  test(`The_stack_form_${form.replace(/[^A-Za-z0-9]/g, '_')}_is_found`, async t => {
    assert.equal((await hitsOf(t, `See ${form} please.`)).length >= 1, true);
  });
}

test('Node_is_case_sensitive_because_the_English_word_node_exists', async t => {
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', plant('Each node of the list holds a value.'))), 'no-needle'), 'lower case node is a word of English');
  assert.equal((await hitsOf(t, 'The Node runtime.')).length, 1);
});

test('A_known_task_needle_is_found_in_any_case_and_wrapped_over_two_lines', async t => {
  assert.equal((await hitsOf(t, 'The cses problem set.')).length, 1);
  assert.equal((await hitsOf(t, 'The REDIS server.')).length >= 1, true);
  const wrapped = await hitsOf(t, 'The Weird\nAlgorithm of a site.');
  assert.match(wrapped.join('\n'), /needle Weird Algorithm/);
});

test('A_literal_of_the_needles_file_is_found_in_any_case_and_wrapped_over_two_lines', async t => {
  const fixture = fixtureRepo(t, 'olympiad', plant('An Alpha-Needle\nphrase here.'));
  const file = path.join(fixture.repo, 'needles.txt');
  writeFileSync(file, 'alpha-needle phrase\n');
  const fails = failsOf(await check(fixture, ['--needles', file]), 'no-needle');
  assert.equal(fails.length, 1, fails.join('\n'));
  assert.match(fails[0], /leak token 1/);
  assert.ok(!fails[0].toLowerCase().includes('alpha'), 'never the words');
});

test('A_word_inside_another_word_is_no_hit_and_the_hit_is_named_by_the_word', async t => {
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad', plant('Tsxyz and clrs and npmx stand.'))), 'no-needle'));
  assert.match((await hitsOf(t, 'Use typescript.')).join('\n'), /needle TypeScript/);
});

test('The_stack_words_give_no_false_hit_on_the_real_FORMATs_sentences_the_real_spec_01_or_the_fixtures', async t => {
  const repo = process.env.LANG_V2_TASK_REPO;
  if (repo === undefined || repo === '') {
    if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error('BENCH_REQUIRE_SANDBOX=1 and LANG_V2_TASK_REPO is not set');
    t.skip('LANG_V2_TASK_REPO is not set');
    return;
  }
  const { wordFindings, matchersOf, parseWordList } = await import('../lib/conformance-needles.mjs');
  const list = parseWordList(JSON.parse(readFileSync(new URL('../inputs/stack-words-lang.json', import.meta.url), 'utf8'))).value;
  const known = parseWordList(JSON.parse(readFileSync(new URL('../inputs/needles-lang.json', import.meta.url), 'utf8'))).value;
  const matchers = [...matchersOf(list), ...matchersOf(known)];
  const format = parseFormat(readFileSync(REAL_FORMAT)).format;
  assert.equal(readFacts(format).ok, true);
  const sentences = [...readSentences(format).value.values()].flatMap(entry => entry.variants.flatMap(variant => variant.parts.map(part => part.template)));
  const texts = [...sentences, readFileSync(`${repo}/todo/tasks/01-descent-to-one.md`, 'utf8'), ...['olympiad', 'daemon', 'web'].map(kind => fixtureRepo(t, kind).read('spec'))];
  for (const text of texts) assert.deepEqual(wordFindings('x', text, matchers), [], text.slice(0, 80));
});
