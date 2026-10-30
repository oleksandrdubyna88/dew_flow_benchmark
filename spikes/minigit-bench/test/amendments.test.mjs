// `inputs/amendments.md`, read by code: dated by-hand verdicts and dated instrument amendments (E3b.S3 header, S4 and S5 use). A verdict is never typed into a record.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { handVerdictsFor, parseAmendments } from '../lib/amendments.mjs';

const TEXT = `# Dated amendments

Dated amendments; instrument only after the A/A (parent 11). By-hand verdicts: YYYY-MM-DD | G-<n> | holds|refuted|explained | <rid> | <evidence path> | <who>.

2026-11-02 | G-1 | refuted | abc123 | s/abc123/argv.json | oleksandr
2026-11-02 | G-13 | holds | abc123 | s/abc123/stream.ndjson | opus-judge
2026-11-02 | instrument | harness:999999999999 | the sampler interval was fixed (G-11)
G-4 | holds | abc123 | no date on this one | x
2026-11-xx | G-2 | holds | abc123 | e | me
2026-11-03 | G-2 | maybe | abc123 | e | me
2026-11-03 | G-9 | holds | abc123 | e
`;

test('By_hand_verdicts_are_read_from_dated_lines', () => {
  const r = parseAmendments(TEXT);
  assert.deepEqual(r.verdicts.map(v => [v.date, v.g, v.verdict, v.rid]), [['2026-11-02', 'G-1', 'refuted', 'abc123'], ['2026-11-02', 'G-13', 'holds', 'abc123']]);
  assert.equal(r.verdicts[0].evidence, 's/abc123/argv.json');
  assert.equal(r.verdicts[0].who, 'oleksandr');
  assert.deepEqual(r.instrument.map(i => [i.date, i.identity]), [['2026-11-02', '999999999999']]);
  assert.equal(r.malformed.length, 4, 'an undated line, a bad date, a bad verdict word and a line missing its last field are reported');
  assert.ok(r.malformed.every(m => typeof m.why === 'string' && m.line.length > 0));
  assert.match(r.malformed.find(m => m.line.startsWith('G-4')).why, /date/);
  assert.match(r.malformed.find(m => m.line.includes('maybe')).why, /holds, refuted, explained/);
});

test('Only_the_verdicts_of_one_session_reach_its_outcome_and_prose_is_ignored', () => {
  assert.deepEqual(handVerdictsFor(TEXT, 'abc123').map(v => v.g), ['G-1', 'G-13']);
  assert.deepEqual(handVerdictsFor(TEXT, 'zzzzzz'), []);
  assert.deepEqual(parseAmendments('# only a header\n\nsome prose that is not a line\n').malformed, []);
});

test('A_budget_raise_line_is_parsed_and_a_malformed_one_is_reported', () => {
  const parsed = parseAmendments('2026-11-05 | budget-raise | pair:2 | n:2 | usd:13.2 | the re-runs of pair 2\n2026-11-05 | budget-raise | pair:9 | n:2 | usd:13.2 | x\n2026-11-05 | budget-raise | pair:1 | n:2 | 13.2 | x');
  assert.deepEqual(parsed.raises, [{ date: '2026-11-05', pair: 2, n: 2, usd: 13.2, reason: 'the re-runs of pair 2' }]);
  assert.equal(parsed.malformed.length, 2, 'a pair outside 1-3 and a missing usd: field are reported, never counted');
});

test('An_input_amendment_line_is_parsed_and_a_malformed_one_is_reported', () => {
  const parsed = parseAmendments('2026-11-02 | instrument | input:quiet.json:0123456789ab | the sampler window (G-11)\n2026-11-02 | instrument | input:quiet.json:xyz | bad hash\n2026-11-02 | instrument | input:a/b.json:0123456789ab | path');
  assert.deepEqual(parsed.inputs, [{ date: '2026-11-02', file: 'quiet.json', hash12: '0123456789ab', reason: 'the sampler window (G-11)' }]);
  assert.equal(parsed.malformed.length, 2);
});
