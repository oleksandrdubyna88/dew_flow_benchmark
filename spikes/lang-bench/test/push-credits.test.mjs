// credits-lang.json holds the owner's credit terms of the known tasks (2026-11-01); the push check refuses a branch carrying a
// known-task text unless its terms are recorded and the README's attribution section carries every piece they require.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkCreditTerms, knownSpecPaths, parseCredits } from '../lib/push-credits.mjs';

const CREDITS_TEXT = readFileSync(fileURLToPath(new URL('../inputs/credits-lang.json', import.meta.url)), 'utf8');
const credits = parseCredits(CREDITS_TEXT);
const entries = credits.value?.known ?? [];
const entry = task => entries.find(e => e.task === task);

/** A README whose attribution section carries every piece the terms require. */
function readmeWith({ without = [] } = {}) {
  const lines = [
    ...entries.map(e => e.credit),
    'The task files todo/tasks/01-descent-to-one.md and todo/tasks/02-open-areas.md are licensed CC BY-NC-SA 4.0, an exception to the MIT licence of this repository.',
    entry('06').upstreamNotice.text,
  ].filter(line => !without.includes(line));
  return `# Title\n\nIntro.\n\n## Attribution\n\n${lines.join('\n\n')}\n`;
}
const check = (paths, readmeText) => checkCreditTerms({ credits, paths, readmeText });

test('The_credit_terms_file_holds_the_owners_five_known_tasks_in_the_plans_shape', () => {
  assert.equal(credits.ok, true, credits.reason);
  assert.deepEqual(entries.map(e => e.task), ['01', '02', '05', '06', '09']);
  assert.deepEqual(knownSpecPaths(credits.value), entries.map(e => e.spec));
  assert.deepEqual(entries.filter(e => e.fileLicence).map(e => [e.task, e.fileLicence.spdx]), [['01', 'CC-BY-NC-SA-4.0'], ['02', 'CC-BY-NC-SA-4.0']]);
  assert.equal(entry('09').credit, 'based on IOI 2018 Werewolf');
  assert.equal(entry('09').licenceFound, false);
  assert.equal(entry('06').upstreamNotice.spdx, 'MIT');
});

test('A_credit_terms_file_with_a_placeholder_or_a_missing_field_is_refused_naming_the_entry', () => {
  const mutate = (task, change) => { const copy = JSON.parse(CREDITS_TEXT); change(copy.known.find(e => e.task === task)); return parseCredits(JSON.stringify(copy)); };
  assert.match(mutate('01', e => { e.credit = '<one line>'; }).reason, /task 01.*credit/);
  assert.match(mutate('02', e => { e.source.url = '<problem page>'; }).reason, /task 02.*source\.url/);
  assert.match(mutate('06', e => { e.upstreamNotice = null; }).reason, /task 06.*upstreamNotice/);
  assert.match(mutate('09', e => { delete e.licenceFound; }).reason, /task 09.*licenceFound/);
  assert.match(mutate('05', e => { e.copiedText = 'adapted'; }).reason, /task 05.*copiedText/);
  assert.match(mutate('01', e => { e.fileLicence = null; }).reason, /task 01.*fileLicence/);
  assert.match(parseCredits('{').reason, /not JSON/);
  assert.match(parseCredits(JSON.stringify({ ...JSON.parse(CREDITS_TEXT), known: JSON.parse(CREDITS_TEXT).known.slice(1) })).reason, /task 01/);
});

test('The_credit_check_refuses_known_task_text_without_credit_terms', () => {
  const noTerms = checkCreditTerms({ credits: { ok: false, reason: 'no terms file' }, paths: ['todo/tasks/05-key-value-server.md'], readmeText: readmeWith() });
  assert.deepEqual(noTerms.map(f => [f.path, f.what]), [['todo/tasks/05-key-value-server.md', 'known-task text without its credit terms (no terms file)']]);
  const otherPath = check(['todo/tasks/09-werewolf.md'], readmeWith());
  assert.match(otherPath[0].what, /^known-task text without its credit terms/);
  assert.deepEqual(check(['todo/tasks/FORMAT.md', 'tasks/03/x.md', 'todo/tasks/03-wrapping-arithmetic.md'], null), [], 'a branch with no known-task text needs no terms');
});

test('Push_check_accepts_a_known_task_text_whose_terms_and_credit_lines_are_all_in_the_readme', () => {
  assert.deepEqual(check(entries.map(e => e.spec), readmeWith()), []);
});

test('Push_check_refuses_a_known_task_whose_credit_line_is_missing_from_the_attribution_section', () => {
  const missing = check(['todo/tasks/05-key-value-server.md'], readmeWith({ without: [entry('05').credit] }));
  assert.deepEqual(missing.map(f => [f.path, f.what]), [['README.md', 'credit line of task 05 is not in the attribution section']]);
  // a credit that stands outside the section does not count
  const outside = `${entry('05').credit}\n\n${readmeWith({ without: [entry('05').credit] })}`;
  assert.equal(check(['todo/tasks/05-key-value-server.md'], outside).length, 1);
  assert.equal(check(['todo/tasks/05-key-value-server.md'], null)[0].what, 'the README is missing: the credit terms cannot be checked');
});

test('Push_check_names_the_cc_exception_sentence_and_the_mit_notice_when_they_are_missing', () => {
  const noException = readmeWith().replace(/.*CC BY-NC-SA 4\.0, an exception.*\n\n/, '');
  assert.deepEqual(check(['todo/tasks/01-descent-to-one.md'], noException).map(f => f.what), ['the CC BY-NC-SA 4.0 exception sentence (naming todo/tasks/01-descent-to-one.md, todo/tasks/02-open-areas.md) is not in the attribution section']);
  const noNotice = readmeWith({ without: [entry('06').upstreamNotice.text] });
  assert.deepEqual(check(['todo/tasks/06-blog-api.md'], noNotice).map(f => f.what), ['the MIT notice of task 06 is not in the attribution section']);
  const backticked = readmeWith().replace('todo/tasks/01-descent-to-one.md and todo/tasks/02-open-areas.md', '`todo/tasks/01-descent-to-one.md` and `todo/tasks/02-open-areas.md`');
  assert.deepEqual(check(['todo/tasks/01-descent-to-one.md'], backticked), [], 'markdown code spans around the paths do not hide the sentence');
});

test('A_notice_that_differs_by_one_word_is_not_the_notice', () => {
  const altered = readmeWith().replace('WITHOUT WARRANTY OF ANY KIND', 'WITH WARRANTY OF ANY KIND');
  assert.equal(check(['todo/tasks/06-blog-api.md'], altered).length, 1);
});
