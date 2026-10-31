import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SECRET_MASK, maskTokens, tokenHits } from '../lib/token-scan.mjs';

// Built from pieces at run time: this file carries no string a secret scanner would take for a credential.
const rep = (c, n) => c.repeat(n);
const SHAPES = Object.freeze({
  'gh-classic': `${'gh'}p_${rep('A1', 18)}`,
  'gh-fine-grained': `${'github'}_pat_${rep('b2_', 24)}`,
  'gh-token': `${'gh'}s_${rep('Z9', 18)}`,
  anthropic: `${'sk'}-ant-${rep('x-1_', 8)}`,
});
const OWN = `${'own'}-secret-value-${rep('7', 6)}`;
const sources = text => [{ source: 'stream', text }];

test('Every_token_shape_is_found_and_never_echoed', () => {
  for (const [kind, token] of Object.entries(SHAPES)) {
    const text = `before ${token} after`;
    const hits = tokenHits(sources(text), []);
    assert.deepEqual(hits, [{ source: 'stream', kind, offset: 7 }], kind);
    assert.equal(JSON.stringify(hits).includes(token.slice(0, 6)), false, `${kind}: no prefix in the hit`);
  }
  const own = tokenHits(sources(`x ${OWN} y`), [OWN]);
  assert.deepEqual(own, [{ source: 'stream', kind: 'own-value', offset: 2 }]);
  const all = JSON.stringify(tokenHits(sources(Object.values(SHAPES).join(' ') + ` ${OWN}`), [OWN]));
  for (const planted of [...Object.values(SHAPES), OWN]) {
    assert.equal(all.includes(planted), false);
    assert.equal(all.includes(planted.slice(0, 6)), false, `a 6-char prefix of ${planted.length}-char value leaked`);
  }
});

test('Near_misses_are_not_tokens_and_a_short_own_secret_is_ignored', () => {
  const near = [`${'gh'}p_${rep('A', 35)}`, `${'github'}_pat_${rep('b', 59)}`, `${'sk'}-ant-${rep('x', 19)}`, `${'gh'}x_${rep('A', 36)}`, 'ghp_', 'sk-ant'];
  assert.deepEqual(tokenHits(sources(near.join(' | ')), []), []);
  assert.deepEqual(tokenHits(sources('abcdefg abcdefg'), ['abcdefg', '']), [], 'under 8 chars (or empty) is never searched for');
  assert.equal(tokenHits(sources('abcdefgh'), ['abcdefgh']).length, 1);
});

test('Hits_name_their_source_in_order_and_repeats_are_all_found', () => {
  const t = SHAPES['gh-classic'];
  const hits = tokenHits([{ source: 'audit', text: `${t} ${t}` }, { source: 'mcp', text: `no secret` }, { source: 'stream', text: `  ${t}` }], []);
  assert.deepEqual(hits.map(h => [h.source, h.offset]), [['audit', 0], ['audit', t.length + 1], ['stream', 2]]);
});

test('Masking_replaces_each_secret_and_keeps_the_rest', () => {
  const text = `curl -H "Authorization: ${SHAPES.anthropic}" and ${OWN} done`;
  const masked = maskTokens(text, [OWN]);
  assert.equal(masked, `curl -H "Authorization: ${SECRET_MASK}" and ${SECRET_MASK} done`);
  assert.equal(maskTokens('plain text', [OWN]), 'plain text');
});

test('No_input_is_changed_and_an_empty_scan_is_empty', () => {
  const secrets = Object.freeze([OWN]);
  const texts = Object.freeze([Object.freeze({ source: 'a', text: OWN })]);
  assert.doesNotThrow(() => tokenHits(texts, secrets));
  assert.deepEqual(tokenHits([], []), []);
  assert.deepEqual(tokenHits(undefined, undefined), []);
});

test('The_gh_o_u_s_r_family_is_one_kind', () => {
  for (const letter of ['o', 'u', 's', 'r']) {
    const hits = tokenHits(sources(`${'gh'}${letter}_${rep('K3', 18)}`), []);
    assert.deepEqual(hits.map(h => h.kind), ['gh-token'], letter);
  }
});

// ---- a token broken up by the medium (E2.S1 review) ---------------------------------------------------------------------------

test('A_token_split_by_a_newline_or_blanks_is_found_with_its_offset_in_the_original_text', () => {
  const t = SHAPES['gh-classic'];
  const cut = 20;
  for (const gap of ['\n', ' ', '\r\n  ', '\t']) {
    const text = `x ${t.slice(0, cut)}${gap}${t.slice(cut)} y`;
    assert.deepEqual(tokenHits(sources(text), []), [{ source: 'stream', kind: 'gh-classic', offset: 2 }], JSON.stringify(gap));
  }
  const own = `${OWN.slice(0, 10)}\n${OWN.slice(10)}`;
  assert.deepEqual(tokenHits(sources(`a ${own}`), [OWN]), [{ source: 'stream', kind: 'own-value', offset: 2 }]);
  assert.equal(tokenHits(sources(`${t.slice(0, cut)}\n${t.slice(cut)}`), []).length, 1, 'found once, not once per copy');
});

test('A_token_split_across_two_consecutive_records_of_one_stream_is_found', () => {
  const t = SHAPES.anthropic;
  const chunks = [{ source: 'stream', text: `hello ${t.slice(0, 15)}` }, { source: 'stream', text: `${t.slice(15)} bye` }];
  assert.deepEqual(tokenHits(chunks, []), [{ source: 'stream', kind: 'anthropic', offset: 6 }], 'the offset is into the joined stream');
  const apart = [{ source: 'stream', text: t.slice(0, 15) }, { source: 'audit', text: t.slice(15) }];
  assert.deepEqual(tokenHits(apart, []), [], 'two different sources are two streams');
  const interleaved = [{ source: 'stream', text: t.slice(0, 15) }, { source: 'audit', text: 'x' }, { source: 'stream', text: t.slice(15) }];
  assert.deepEqual(tokenHits(interleaved, []), [], 'only CONSECUTIVE records of a source are one stream');
});

test('Blanks_between_ordinary_words_do_not_make_a_token_and_a_split_token_is_masked_whole', () => {
  assert.deepEqual(tokenHits(sources('ghp _ and sk - ant - words with spaces'), []), []);
  const t = SHAPES['gh-classic'];
  const masked = maskTokens(`use ${t.slice(0, 12)}\n${t.slice(12)} now`, []);
  assert.equal(masked, `use ${SECRET_MASK} now`);
  assert.equal(masked.includes(t.slice(0, 6)), false);
});
