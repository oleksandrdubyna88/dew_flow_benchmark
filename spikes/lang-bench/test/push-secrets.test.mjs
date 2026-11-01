// The push check's secret shapes (consult D2): each shape is named, a hit says shape, path and line, never the text.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SECRET_SHAPES, findSecrets } from '../lib/push-secrets.mjs';

// Every token below is assembled at run time: no file of this repository holds a string a secret scanner would take for a credential.
const ALNUM = 'aB3dE5gH7jK9mN1pQ3sT5vW7yZ';
const fineGrained = `${'github_pat'}_${ALNUM.slice(0, 22)}_${ALNUM}`;
const classic = `${'ghp'}_${ALNUM}${ALNUM.slice(0, 10)}`;
const awsKey = `${'AKIA'}${'ABCDEFGH12345678'}`;
const privateKeyHeader = `-----BEGIN ${'OPENSSH'} PRIVATE KEY-----`;

const namesIn = text => findSecrets('x.txt', text).map(f => f.what);

test('A_secret_shape_list_without_github_pat_would_pass_a_fine_grained_token_and_that_is_the_point', () => {
  assert.deepEqual(namesIn(`token = ${fineGrained}\n`), ['github-fine-grained-pat']);
  // the refuted approach: a list of the older shapes alone sees nothing in the same text
  const olderShapes = SECRET_SHAPES.filter(shape => shape.name !== 'github-fine-grained-pat');
  assert.equal(olderShapes.some(shape => new RegExp(shape.re.source).test(fineGrained)), false, 'the older shapes do not match a fine-grained token');
});

test('Each_named_shape_is_found_with_its_name_path_and_line_and_never_its_text', () => {
  const text = ['plain line', `a ${classic}`, `b ${awsKey}`, privateKeyHeader, `c ${'sk-ant'}-${'api03-'}${ALNUM}`].join('\n');
  const found = findSecrets('config/x.txt', text);
  assert.deepEqual(found.map(f => [f.what, f.line, f.path]), [
    ['github-classic-token', 2, 'config/x.txt'],
    ['aws-access-key-id', 3, 'config/x.txt'],
    ['private-key-block', 4, 'config/x.txt'],
    ['anthropic-api-key', 5, 'config/x.txt'],
  ]);
  assert.equal(JSON.stringify(found).includes(ALNUM.slice(0, 12)), false, 'the finding carries no part of the matched text');
});

test('A_shape_shorter_than_its_length_rule_is_not_a_secret', () => {
  assert.deepEqual(namesIn(`${'ghp'}_short\n${'AKIA'}SHORT\n${'github_pat'}_tooshort\n`), []);
});

test('The_secret_scan_still_finds_a_planted_secret_after_a_reformat_over_lines', () => {
  assert.equal(namesIn(`before\n\n\t  key: "${fineGrained}"\n`).length, 1, 'the scan is alive on an indented, quoted token');
});

test('A_snake_case_identifier_that_starts_like_a_fine_grained_token_is_not_one', () => {
  // the shape is random base62: a name made of lower-case words has neither a digit nor a capital (found when the bench's own branch was checked)
  assert.deepEqual(namesIn('test(\'A_secret_shape_list_without_github_pat_would_pass_a_fine_grained_token_and_that_is_the_point\')\n'), []);
  assert.deepEqual(namesIn(`${'github_pat'}_lowercase_words_only_and_more_of_them_here\n`), []);
  assert.deepEqual(namesIn(`${'github_pat'}_${'11AAAAAAAAAAAAAAAAAAAAAA'}\n`), ['github-fine-grained-pat'], 'a digit and a capital make it a token');
});
