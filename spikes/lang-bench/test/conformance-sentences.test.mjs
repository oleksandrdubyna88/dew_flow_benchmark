// E1.S8: the fixed sentences are FORMAT's own. The checker reads them from FORMAT's `text` blocks by id (2.1's table maps id, kinds and place), so a one-word change in FORMAT changes what it demands,
// and a FORMAT that lacks a sentence, a placement or a slot the checker expects makes it refuse to run (exit 4). The real FORMAT and the real spec 01 are read only where the task repository is at hand.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { numberOf } from '../lib/conformance-check-sentences.mjs';
import { readFacts } from '../lib/conformance-facts.mjs';
import { parseKinds, parsePlacement, readSentences } from '../lib/conformance-sentences.mjs';
import { parseFormat } from '../lib/format-structure.mjs';
import { check, CHECK_NAMES, failsOf, fixtureRepo, isOk, phrase, REAL_FORMAT } from './support/conformance-fixtures.mjs';
import { edit } from './support/format-fixtures.mjs';

const OFFLINE_OLD = 'The tests run offline, on a pristine copy of the workspace, after the build.';
const OFFLINE_NEW = 'The tests run strictly offline, on a pristine copy of the workspace, after the build.';

/** The task repository, or null: skipped with its reason, and a failure under BENCH_REQUIRE_SANDBOX=1. */
function taskRepo(t) {
  const repo = process.env.LANG_V2_TASK_REPO;
  if (repo !== undefined && repo !== '') return repo;
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error('BENCH_REQUIRE_SANDBOX=1 and LANG_V2_TASK_REPO is not set');
  t.skip('LANG_V2_TASK_REPO is not set: the real FORMAT and the real spec 01 are not at hand');
  return null;
}

/** The fixture with FORMAT's offline sentence changed by one word, in FORMAT and, when `inSpec`, in the spec. */
function withChangedFormat(t, { inSpec }) {
  return fixtureRepo(t, 'olympiad', { format: [[OFFLINE_OLD, OFFLINE_NEW]], spec: inSpec ? [phrase(OFFLINE_OLD, OFFLINE_NEW)] : [] });
}

test('A_retyped_sentence_list_would_pass_a_spec_after_FORMAT_changed_and_that_is_the_point', async t => {
  // a checker with a literal list of the sentences (the sentence as it stood) passes the old spec whatever FORMAT says now
  const retyped = text => text.replace(/\s+/g, ' ').includes(OFFLINE_OLD);
  const stale = withChangedFormat(t, { inSpec: false });
  assert.equal(retyped(stale.read('spec')), true, 'the retyped list is satisfied by the old spec');
  // the checker reads FORMAT: the old spec no longer conforms to it, and names the sentence
  const result = await check(stale);
  assert.match(failsOf(result, 'sentences').join('\n'), /F-OFFLINE is altered/);
  assert.equal(result.exit, 1);
  // and a spec that follows the changed FORMAT conforms
  assert.equal((await check(withChangedFormat(t, { inSpec: true }))).exit, 0);
});

test('The_checker_follows_a_one_word_change_of_FORMAT_for_every_kind', async t => {
  for (const [kind, from, to] of [
    ['daemon', 'Standard output is compared only where', 'Standard output is judged only where'],
    ['web', 'Every reply is compared on its status code', 'Every reply is judged on its status code'],
  ]) {
    const unchanged = fixtureRepo(t, kind);
    const changed = fixtureRepo(t, kind, { format: [[from, to]] });
    assert.equal((await check(unchanged)).exit, 0);
    assert.match(failsOf(await check({ ...unchanged, format: changed.format }), 'sentences').join('\n'), /is altered/, `${kind}: the old spec fails under the changed FORMAT`);
  }
});

const FORMAT_FAULTS = [
  ['a_sentence_with_no_text_block', text => edit(text, /F-OFFLINE:\n\n```text\n[^]*?\n```\n/.exec(text)[0], 'F-OFFLINE:\n\nNo block here.\n'), /no `text` block for the sentence F-OFFLINE/],
  ['a_slot_the_checker_does_not_know', text => edit(text, 'A test waits at most {T} seconds', 'A test waits at most {T} seconds {mystery}'), /the slot \{mystery\}/],
  ['a_placement_the_checker_cannot_read', text => edit(text, '| F-OFFLINE | all | section 5, in an R | none |', '| F-OFFLINE | all | somewhere | none |'), /where F-OFFLINE stands \("somewhere"\)/],
  ['a_kinds_cell_with_an_unknown_word', text => edit(text, '| F-OFFLINE | all |', '| F-OFFLINE | everyone |'), /the kinds of F-OFFLINE/],
  ['no_table_of_fixed_sentences', text => edit(text, '| Id | Kinds | Where | Slots |', '| Name | Kinds | Where | Slots |'), /no table of fixed sentences/],
  ['no_series_defaults', text => edit(text, '**T = 10 seconds**', 'T is ten seconds'), /series defaults/],
  ['no_table_of_kind_phrases', text => edit(text, '| Kind | Phrase |', '| Sort | Phrase |'), /three kind phrases/],
  ['no_keyword_list', text => text.replace(/The complete list of the script's words:[^]*?\.\n/, 'None.\n'), /closed keyword list/],
  ['no_file_name_pattern', text => edit(text, 'pattern `^[0-9]{2}', 'pattern ^[0-9]{2}'), /file-name pattern/],
];
for (const [name, mutate, message] of FORMAT_FAULTS) {
  test(`A_FORMAT_with_${name}_makes_the_checker_refuse_with_exit_4`, async t => {
    const fixture = fixtureRepo(t, 'olympiad');
    writeFileSync(fixture.format, mutate(readFileSync(fixture.format, 'utf8')));
    const result = await check(fixture);
    assert.equal(result.exit, 4, result.lines.join('\n'));
    assert.deepEqual(result.lines, [], 'nothing is passed');
    assert.match(result.errors.join('\n'), message);
  });
}

test('A_FORMAT_that_is_not_a_readable_document_is_exit_4', async t => {
  const fixture = fixtureRepo(t, 'olympiad');
  writeFileSync(fixture.format, readFileSync(fixture.format, 'utf8').replace('\n', '\r\n'));
  const result = await check(fixture);
  assert.equal(result.exit, 4);
  assert.match(result.errors.join('\n'), /not a readable FORMAT: line 1: a carriage return/);
});

test('Slot_values_are_checked_against_what_the_slot_allows', async t => {
  const long = await check(fixtureRepo(t, 'daemon', { spec: [['at most 10 seconds', 'at most 601 seconds']] }));
  assert.match(failsOf(long, 'sentences').join('\n'), /\{T\} is 601 s, and a duration is at most 600 s/);
  const zero = await check(fixtureRepo(t, 'daemon', { spec: [['at most 10 seconds', 'at most 0 seconds']] }));
  assert.match(failsOf(zero, 'sentences').join('\n'), /F-READY is altered/);
});

test('The_reader_reads_the_kinds_the_placements_and_the_numbers_FORMAT_gives', () => {
  assert.deepEqual([...parseKinds('daemon and web that listen on a port').kinds], ['daemon', 'web']);
  assert.equal(parseKinds('olympiad, the default').required, true);
  assert.equal(parseKinds('olympiad, instead of F-BYTES-O when the output is normalised').required, false);
  assert.equal(parseKinds('all, unless the task defines errors for every invalid input').required, false);
  assert.equal(parseKinds('everyone'), null);
  assert.deepEqual(parsePlacement('section 2 (olympiad) or 3 (daemon, web), in an R').sections, { olympiad: 2, daemon: 3, web: 3 });
  assert.deepEqual(parsePlacement('section 4, in the same R as F-MEMORY, after it').sameAs, ['F-MEMORY']);
  assert.equal(parsePlacement('as F-INVALID').alias, 'F-INVALID');
  assert.equal(parsePlacement('nowhere in particular'), null);
  assert.deepEqual(['3', 'three', 'twenty-one', 'ninety', 'twenty-ten', 'x'].map(numberOf), [3, 3, 21, 90, null, null]);
});

test('Every_id_of_the_real_FORMATs_table_has_its_sentence_and_the_checker_runs_on_it', t => {
  const repo = taskRepo(t);
  if (repo === null) return;
  const parsed = parseFormat(readFileSync(REAL_FORMAT));
  assert.equal(parsed.ok, true, parsed.reason);
  const sentences = readSentences(parsed.format);
  assert.equal(sentences.ok, true, sentences.reason);
  const table = [...parsed.format.text.matchAll(/^\| (F-[A-Z0-9-]+) \|/gm)].map(hit => hit[1]);
  assert.ok(table.length >= 30, `the table holds ${table.length} ids`);
  assert.deepEqual([...sentences.value.keys()], table, 'one reader entry per row, in the table\'s order');
  for (const entry of sentences.value.values()) assert.ok(entry.variants.length > 0 && entry.variants.every(variant => variant.parts.length > 0), entry.id);
  assert.equal(readFacts(parsed.format).ok, true);
});

test('The_checker_on_the_real_spec_01_names_what_it_lacks_a_recorded_observation_not_a_required_pass', async t => {
  const repo = taskRepo(t);
  if (repo === null) return;
  const fixture = fixtureRepo(t, 'olympiad');
  const real = { ...fixture, task: '01-descent-to-one', format: REAL_FORMAT };
  writeFileSync(path.join(fixture.repo, 'todo', 'tasks', '01-descent-to-one.md'), readFileSync(`${repo}/todo/tasks/01-descent-to-one.md`));
  mkdirSync(path.join(fixture.repo, 'tasks', '01-descent-to-one', 'samples'), { recursive: true });
  const result = await check(real);
  t.diagnostic(`spec 01 under the real FORMAT (the samples folder is empty, as the task repository's is):\n${result.lines.join('\n')}`);
  assert.ok([0, 1].includes(result.exit), result.errors.join('\n'));
  for (const name of CHECK_NAMES) assert.ok(isOk(result, name) || failsOf(result, name).length > 0, `a line for ${name}`);
});
