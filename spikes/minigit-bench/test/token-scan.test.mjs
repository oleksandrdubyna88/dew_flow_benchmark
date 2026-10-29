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
  assert.deepEqual(tokenHits(sources(near.join(' ')), []), []);
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
