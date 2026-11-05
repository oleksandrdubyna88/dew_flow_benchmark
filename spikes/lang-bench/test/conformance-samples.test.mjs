// E1.S8, checks 7 to 10: the samples against section 6 (and the runner's parse of each pair), CP1's F11 forms, the `env` lines against the layer's lists, and the needles. One failing fixture each.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { check, failsOf, fixtureRepo, isOk, phrase } from './support/conformance-fixtures.mjs';

const ROW2 = '| `s02` | `1⏎` | `1⏎` |';
const KEYWORDS = ['env', 'file', 'start', 'connect', 'send', 'request', 'recv', 'expect', 'close', 'sleep'].map(word => `\`${word}\``).join(', ');
const NEEDLE = 'zz-needle-token-91';

// [name, kind, edits, check, message]
const CASES = [
  ['a_missing_sample_file', 'olympiad', { remove: ['samples/s02.out'] }, 'samples', /samples\/s02\.out is missing: section 6 shows it/],
  ['an_extra_file_in_samples', 'olympiad', { add: { 'samples/notes.txt': 'x\n' } }, 'samples', /samples\/notes\.txt is not one of the sample pairs/],
  ['a_cell_that_differs_from_the_file', 'olympiad', { spec: [[ROW2, '| `s02` | `2⏎` | `1⏎` |']] }, 'samples', /line \d+: sample s02 standard input: the cell shows 2 bytes, samples\/s02\.in holds 2; first difference at byte 1/],
  ['a_cell_without_the_line_feed_glyph', 'olympiad', { spec: [[ROW2, '| `s02` | `1` | `1⏎` |']] }, 'samples', /the cell shows 1 bytes, samples\/s02\.in holds 2/],
  ['a_size_fallback_for_bytes_that_can_be_shown', 'olympiad', { spec: [[ROW2, '| `s02` | `1⏎` | 2 bytes, see samples/s02.out |']] }, 'samples', /can be shown as bytes/],
  ['a_size_fallback_with_the_wrong_size', 'olympiad', { spec: [[ROW2, '| `s02` | `1⏎` | 9 bytes, see samples/s02.out |']] }, 'samples', /the cell says 9 bytes, and samples\/s02\.out holds 2/],
  ['a_cell_that_is_not_a_code_span', 'olympiad', { spec: [[ROW2, '| `s02` | 1⏎ | `1⏎` |']] }, 'samples', /is not one code span/],
  ['a_sample_table_with_another_header', 'olympiad', { spec: [['| Id | Standard input | Expected standard output |', '| Id | Input | Output |']] }, 'samples', /the table must start with/],
  ['a_section_6_that_shows_a_gap_in_the_ids', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |', '| `s03` | `1⏎` | `1⏎` |']] }, 'samples', /they must be s01 … s02 without a gap/],
  ['a_block_that_differs_from_the_file', 'daemon', { 's01.in': [['connect c1 tcp', 'connect c1 tcp  ']] }, 'samples', /the block of samples\/s01\.in shows \d+ bytes, the file holds \d+; first difference at byte \d+/],
  ['a_block_without_its_heading_line', 'daemon', { spec: [['`samples/s01.out`', 'The expected transcript:']] }, 'samples', /must be headed by the line `samples\/s01\.out`/],
  ['a_pair_that_does_not_parse', 'daemon', { 's01.in': [['scenario 2', 'scenario 3']] }, 'samples', /the runner refuses the pairs — s01\.in:1: format version 3/],
  ['a_transcript_that_does_not_match_its_script', 'web', { 's02.out': [['reply a 200', 'reply a 200\n  body empty\nreply a 200']] }, 'samples', /the runner refuses the pairs — /],
  ['a_check_before_the_last_entry', 'daemon', { 's01.out': [['exit 0', 'check equal a 1\nexit 0']] }, 'sample-forms', /sample s01\.out line \d+: an entry after a check/],
  ['a_request_target_without_the_slash', 'web', { 's01.in': [['request a GET "/hello"', 'request a GET "hello"']] }, 'sample-forms', /sample s01\.in line \d+: the target of a request begins with "\/"/],
  ['a_sample_id_of_one_digit', 'daemon', { spec: [['### s02', '### s2']] }, 'sample-forms', /sample id "s2" in section 6 is not s<two digits>/],
  ['a_json_body_without_F_JSON', 'web', { spec: [phrase('The body of every reply to `GET /items` is compared as a JSON value: the members of an object may come in any order, numbers are compared by their exact decimal value with no rounding (so `2` equals `2.0` and `-0` equals `0`, and two numbers that differ in any digit differ), and whitespace outside strings is ignored; arrays keep their order unless a requirement says otherwise.', 'Nothing.')] }, 'sample-forms', /a JSON comparison, and no requirement carries F-JSON/],
  ['an_env_line_that_sets_LANG', 'daemon', { 's02.in': [['env TZ "Europe/Berlin"', 'env LANG "C"']] }, 'sample-env', /sample s02\.in line 3: the env line sets LANG/],
  ['an_env_line_in_another_case_of_a_base_key', 'daemon', { 's02.in': [['env TZ "Europe/Berlin"', 'env lc_all "C"']] }, 'sample-env', /sets lc_all/],
  ['an_env_line_that_sets_the_dotnet_heap_limit', 'daemon', { 's02.in': [['env TZ "Europe/Berlin"', 'env COMPlus_GCHeapHardLimitPercent "50"']] }, 'sample-env', /sets COMPlus_GCHeapHardLimitPercent/],
  ['an_env_line_that_sets_the_node_options', 'daemon', { 's02.in': [['env TZ "Europe/Berlin"', 'env NODE_OPTIONS "--max-old-space-size=64"']] }, 'sample-env', /sets NODE_OPTIONS/],
  ['an_env_line_that_sets_PATH', 'web', { 's02.in': [['default within 2s', 'default within 2s\nenv Path "/bin"']] }, 'sample-env', /sets Path/],
  ['a_link_in_a_requirement', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** See https://example.com. For n = 1']] }, 'no-needle', /todo\/tasks\/01-count-down\.md line \d+: link/],
  ['a_plan_id_in_a_requirement', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 (story E1.S8)']] }, 'no-needle', /plan story id/],
  ['a_known_task_name_in_a_requirement', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** The CSES way: for n = 1']] }, 'no-needle', /needle CSES/],
  ['the_glyph_outside_section_6', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1 ⏎']] }, 'no-needle', /the glyph ⏎ stands outside section 6/],
  ['a_reading_block', 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** The steps: ${KEYWORDS}. For n = 1`]] }, 'no-needle', /names 10 keywords of the scenario script/],
];

/** A needles file in the fixture repository (outside samples/): one literal per line. */
function needlesFile(fixture) {
  const file = path.join(fixture.repo, 'needles.txt');
  writeFileSync(file, `${NEEDLE}\n`);
  return file;
}

function plant(fixture, edits) {
  for (const key of edits.remove ?? []) fixture.remove(key);
  for (const [key, text] of Object.entries(edits.add ?? {})) fixture.write(key, text);
}

for (const [name, kind, edits, name_, message] of CASES) {
  test(`${name}_is_found_by_the_${name_}_check`, async t => {
    assert.ok(isOk(await check(fixtureRepo(t, kind)), name_), 'the unchanged spec is ok for this check');
    const { remove, add, ...file } = edits;
    const fixture = fixtureRepo(t, kind, file);
    plant(fixture, edits);
    const result = await check(fixture);
    const lines = failsOf(result, name_).map(line => line.slice(`FAIL ${name_} — `.length));
    assert.ok(lines.some(line => message.test(line)), `${message} not in:\n${result.lines.join('\n')}`);
    assert.equal(result.exit, 1);
  });
}

test('The_checker_still_finds_a_known_deviation_in_a_sample_table', async t => {
  const result = await check(fixtureRepo(t, 'olympiad', { spec: [[ROW2, '| `s02` | `2⏎` | `1⏎` |']] }));
  assert.equal(failsOf(result, 'samples').length, 1);
  assert.equal(result.exit, 1);
});

test('A_needle_from_the_needles_file_is_found_and_named_by_its_number_never_by_its_words', async t => {
  const fixture = fixtureRepo(t, 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** The ${NEEDLE} case: for n = 1`]] });
  const needles = needlesFile(fixture);
  const result = await check(fixture, ['--needles', needles]);
  const fails = failsOf(result, 'no-needle');
  assert.equal(fails.length, 1, result.lines.join('\n'));
  assert.match(fails[0], /leak token 1/);
  assert.ok(!result.lines.join('\n').includes(NEEDLE), 'the needle is never printed');
  assert.ok(isOk(await check(fixtureRepo(t, 'olympiad'), ['--needles', needles]), 'no-needle'), 'a spec without it passes');
});

test('A_needle_in_a_sample_file_is_found', async t => {
  const fixture = fixtureRepo(t, 'daemon', { 's01.in': [['connect c1 tcp', `# ${NEEDLE}\nconnect c1 tcp`]] });
  const result = await check(fixture, ['--needles', needlesFile(fixture)]);
  assert.match(failsOf(result, 'no-needle').join('\n'), /samples\/s01\.in line 4: leak token 1/);
});

test('An_env_line_that_adds_a_key_outside_the_lists_or_replaces_TZ_passes', async t => {
  const fixture = fixtureRepo(t, 'daemon', { 's02.in': [['env TZ "Europe/Berlin"', 'env TZ "Asia/Tokyo"\nenv MY_FLAG "1"']] });
  assert.ok(isOk(await check(fixture), 'sample-env'));
});

test('The_samples_check_finds_the_olympiad_table_forms_of_FORMAT_3_4', async t => {
  const fixture = fixtureRepo(t, 'olympiad', {
    spec: [[ROW2, '| `s02` | `1⏎` | `1⏎` |\n| `s03` | `` a`b `` | 0 bytes, see samples/s03.out |']],
  });
  fixture.write('samples/s03.in', 'a`b');
  fixture.write('samples/s03.out', '');
  const result = await check(fixture);
  assert.deepEqual(failsOf(result, 'samples'), [], 'a two-backtick cell and the size form of an empty file are read as FORMAT 3.4 gives them');
});
