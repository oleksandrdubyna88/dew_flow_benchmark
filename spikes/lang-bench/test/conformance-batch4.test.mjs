// E1.S8 batch 4, N1 to N8: narrowings of rules that gave a false FAIL on honest specs. Each narrowing has its cases in two lists: what must now PASS, and the true deviation next to it that must still FAIL,
// so none of them reopens a false pass. The cases are synthetic; no draft's text is used.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { run } from '../lib/conformance.mjs';
import { argsOf, check, failsOf, fixtureRepo, isOk, phrase, recordFile } from './support/conformance-fixtures.mjs';

const inR2 = text => ({ spec: [['**R2.** For n = 1', `**R2.** ${text} For n = 1`]] });
const spans = words => words.map(word => `\`${word}\``).join(', ');
const TIM_GRACE = 'The series default for the grace period is 5 seconds; this task uses 8 seconds because the program flushes a journal before it exits.';
const TIM_READY = 'The series default for readiness is 10 seconds; this task uses 12 seconds because the program loads its data first.';

// [name, kind, edits, check]: the narrowed rule no longer fires
const PASSES = [
  ['N1_a_wrapped_cross_reference_that_ends_a_sentence', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** The rule is the one in\nR12.\nFor n = 1']] }, 'r-ids'],
  ['N2_a_code_span_that_wraps_to_a_line_starting_with_a_tag_like_word', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** The files are named (`samples/\n<id>.in`) and so on. For n = 1']] }, 'file'],
  ['N3_a_value_in_a_code_span_that_looks_like_a_reference', 'olympiad', inR2('The name is `R100` here.'), 'r-ids'],
  ['N3_an_olympiad_cell_that_shows_sample_bytes_like_a_reference', 'olympiad', { spec: [['| `s02` | `1⏎` | `1⏎` |', '| `s02` | `R99⏎` | `1⏎` |']], 's02.in': [['1\n', 'R99\n']] }, 'r-ids'],
  ['N4_op_code', 'olympiad', inR2('The op-code is read.'), 'no-needle'],
  ['N4_cp1252', 'olympiad', inR2('The cp1252 table is used.'), 'no-needle'],
  ['N4_f1_and_f2', 'olympiad', inR2('The keys f1 and f2 are free.'), 'no-needle'],
  ['N5_a_double_index_in_prose', 'olympiad', inR2('Read a[i][j] and a[i](x) as indexes.'), 'no-needle'],
  ['N5_a_double_index_in_a_table_cell', 'olympiad', { spec: [['| 1 | `1` |', '| m[i][j] | `1` |']] }, 'no-needle'],
  ['N6_a_table_of_ordinary_words_that_are_also_keywords', 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** Words.\n\n| word |\n|---|\n| ${spans(['start', 'end', 'count', 'lines', 'bytes', 'file', 'body', 'default'])} |\n\nFor n = 1`]] }, 'no-needle'],
];

for (const [name, kind, edits, name_] of PASSES) {
  test(`${name}_is_no_finding_of_the_${name_}_check`, async t => {
    const result = await check(fixtureRepo(t, kind, edits));
    assert.ok(isOk(result, name_), result.lines.join('\n'));
  });
}

// [name, kind, edits, check, message]: the true deviation still fails
const FAILS = [
  ['N1_a_plain_id_with_a_sentence_after_it_still_fails', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1\nR3. Another sentence']] }, 'r-ids', /looks like a requirement id/],
  ['N1_the_limit_R1_then_it_stops_still_fails', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** For n = 1\nR1. Then it stops.']] }, 'r-ids', /looks like a requirement id/],
  ['N2_inline_HTML_at_the_start_of_a_line_still_fails', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** Words\n<b>x</b> text\nFor n = 1']] }, 'file', /raw inline HTML/],
  ['N2_a_block_tag_on_its_own_line_is_still_refused', 'olympiad', { spec: [['**R2.** For n = 1', '**R2.** Words\n\n<div>\n\nFor n = 1']] }, 'file', /HTML block start/],
  ['N3_a_reference_to_R99_in_prose_still_fails', 'olympiad', inR2('See R99 for it.'), 'r-ids', /names R99/],
  ['N3_a_reference_to_R0_in_prose_still_fails', 'olympiad', inR2('See R0 for it.'), 'r-ids', /names R0/],
  ['N4_CP1_still_fails', 'olympiad', inR2('See CP1 for it.'), 'no-needle', /plan id CP1/],
  ['N4_OP_3_still_fails', 'olympiad', inR2('See OP-3 for it.'), 'no-needle', /plan id OP-3/],
  ['N4_F12_still_fails', 'olympiad', inR2('See F12 for it.'), 'no-needle', /plan id F12/],
  ['N5_an_inline_link_still_fails', 'olympiad', inR2('See [text](url) here.'), 'no-needle', /: link$/],
  ['N5_a_reference_link_still_fails', 'olympiad', inR2('See [text][ref] here.'), 'no-needle', /: link$/],
  ['N6_a_table_of_step_forms_still_fails', 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** Steps.\n\n| step |\n|---|\n| ${spans(['connect <c> tcp', 'send <c> <s>', 'recv <c> bytes <n>', 'expect <c> quiet', 'close <c>', 'shutdown <c>', 'sleep <d>', 'signal TERM'])} |\n\nFor n = 1`]] }, 'no-needle', /names 8 keywords of the scenario script/],
  ['N6_a_reading_block_in_prose_still_fails', 'olympiad', inR2(`The steps: ${spans(['env', 'file', 'start', 'connect', 'send', 'request', 'recv', 'expect', 'close', 'wait-exit'])}.`), 'no-needle', /names 10 keywords of the scenario script/],
  ['N6_a_reading_block_in_a_fence_still_fails', 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** The steps follow.\n\n\`\`\`text\nThe steps: ${spans(['env', 'file', 'start', 'connect', 'send', 'request', 'recv', 'expect', 'wait-exit'])}.\n\`\`\`\n\nFor n = 1`]] }, 'no-needle', /names 9 keywords of the scenario script/],
  ['N6_eight_keywords_with_three_script_only_words_still_fails', 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** Steps.\n\n| step |\n|---|\n| ${spans(['connect <c> tcp', 'recv <c> bytes <n>', 'expect <c> quiet', 'send <c> <s>', 'close <c>', 'sleep <d>', 'signal TERM', 'start ready port'])} |\n\nFor n = 1`]] }, 'no-needle', /names 8 keywords of the scenario script/],
];

for (const [name, kind, edits, name_, message] of FAILS) {
  test(`${name}`, async t => {
    const result = await check(fixtureRepo(t, kind, edits));
    const lines = failsOf(result, name_).map(line => line.slice(`FAIL ${name_} — `.length));
    assert.ok(lines.some(line => message.test(line)), `${message} not in:\n${result.lines.join('\n')}`);
  });
}

test('N6_a_table_with_fewer_than_three_script_only_words_passes_even_with_eight_keywords', async t => {
  const table = spans(['connect <c> tcp', 'recv <c> bytes <n>', 'send <c> <s>', 'close <c>', 'sleep <d>', 'signal TERM', 'start ready port', 'request a GET']);
  const result = await check(fixtureRepo(t, 'olympiad', { spec: [['**R2.** For n = 1', `**R2.** Steps.\n\n| step |\n|---|\n| ${table} |\n\nFor n = 1`]] }));
  assert.ok(isOk(result, 'no-needle'), 'two script-only words (connect, recv) are not enough to call it a reading block');
});

// N7: the grace period second, in the wrong requirement, while readiness is right
test('N7_readiness_right_in_R3_and_the_grace_period_second_in_the_wrong_requirement_fails', async t => {
  const spec = [
    phrase(TIM_GRACE, ''),
    phrase('at most 10 seconds', 'at most 12 seconds'),
    phrase('fails that test.\n\n**R4.**', `fails that test. ${TIM_READY}\n\n**R4.**`),
    phrase('is answered as R1 says, and hidden tests check it.', `is answered as R1 says, and hidden tests check it. ${TIM_GRACE}`),
  ];
  const result = await check(fixtureRepo(t, 'web', { spec }));
  const lines = failsOf(result, 'sentences').join('\n');
  assert.match(lines, /F-TIMING \(line \d+\) for the grace period must stand in the same requirement as F-STOP/);
  assert.doesNotMatch(lines, /F-TIMING \(line \d+\) for readiness must stand/, 'the first one is right where it stands');
});

// N8: the gate state of the record on the ok format line
test('N8_the_ok_format_line_says_gate_none_when_the_record_has_no_gate_and_names_the_gate_when_it_has_one', async t => {
  const fixture = fixtureRepo(t, 'olympiad');
  const none = await run(argsOf(fixture, ['--record', recordFile(fixture)]));
  assert.match(none.lines[0], /^ok format sha256 [0-9a-f]{64} \(record A, gate none\)$/);
  const gated = path.join(fixture.repo, 'gated.json');
  const sha = /[0-9a-f]{64}/.exec(none.lines[0])[0];
  writeFileSync(gated, JSON.stringify({ A: { sha256: sha, gate: 'inputs/gates/FORMAT-v2.md' } }));
  const result = await run(argsOf(fixture, ['--record', gated]));
  assert.match(result.lines[0], /^ok format sha256 [0-9a-f]{64} \(record A, gate inputs\/gates\/FORMAT-v2\.md\)$/);
});

test('N8_the_plan_id_finding_tells_the_author_the_way_out', async t => {
  const result = await check(fixtureRepo(t, 'olympiad', inR2('See E5 for it.')));
  assert.match(failsOf(result, 'no-needle').join('\n'), /plan id E5 \(a value of the task goes in a code span\)/);
});
