// The person-reviewed list of the leak check (`leak-reviewed.json`, outside git): a private line or a note phrase that a person read and found to be generic text is dropped from the needles and keys of every tier by its hash.
// Nothing else exempts: no pattern, no location. The private side is a fixture of INVENTED content in a temp home; nothing real is read. The tiers are driven through `scanText` (no git); the command tests make
// a temp git repository.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import { leakMaterial } from '../lib/leak.mjs';
import { scanBenchHalf } from './bench-half-seam.mjs';
import { SUBSTRING_NEEDLES_MAX } from '../lib/leak-material.mjs';
import { scanText } from '../lib/leak-scan.mjs';
import { BITMAP_SLOTS, buildSubstringIndex, findSubstrings } from '../lib/leak-substring.mjs';
import { normLine } from '../lib/leak-text.mjs';
import { loadReviewed, parseReviewed, reviewHash, reviewedPath } from '../lib/leak-reviewed.mjs';
import { verifyCandidates } from '../lib/leak-verify.mjs';
import { commitFiles, git, repoWith } from './leak-scan-fixture.mjs';
import { EXPLORE, presentMaterial, quickHome, quickTask, reviewedEntry, writeQuickHeldout, writeReviewed } from './quick-leak-fixture.mjs';

const LINE_A = 'recv c "invented-reviewed-answer-alpha-4471"';
const LINE_B = 'recv c "invented-reviewed-answer-bravo-4471"'; // LINE_A with one word changed
const SHORT_A = 'send c "inv-77"';
const SHORT_B = 'send c "inv-78"';
const REF_A = 'def invented_reference_for_the_list(state, event): return fold(state, event, 551)';
const REF_B = 'def invented_reference_for_the_list(state, event): return fold(state, event, 552)';
const LONG_A = Array.from({ length: 40 }, (_, i) => `grid${i}x${(i * 7) % 11}`).join(' ');
const LONG_B = Array.from({ length: 40 }, (_, i) => `cell${i}y${(i * 5) % 13}`).join(' ');
const WIN = ['invented window line one', 'invented window line two', 'invented window line three'];
const NOTES = 'alpha bravo charlie delta echo foxtrot golf hotel';

function homeWith(over = {}, list = null) {
  const home = quickHome();
  const cases = over.cases ?? [{ id: 'h001', input: 'short\n', output: `transcript 2\n${LINE_A}\n${LINE_B}\n${SHORT_A}\n${SHORT_B}\n` }, { id: 'h002', input: 'short\n', output: `${LONG_A}\n${LONG_B}\n` }];
  writeQuickHeldout({ home, task: quickTask('04'), traps: [], refs: { 'ref.py': `${REF_A}\n${REF_B}\n` }, notes: NOTES, ...over, cases });
  if (list !== null) writeReviewed(home, list);
  return home;
}

const materialOf = home => {
  const r = presentMaterial({ home, publicText: '' });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};

/** What the tiers find in `raw` (a public file), verified: `[finding]`. */
function hitsIn(material, raw) {
  const r = scanText({ name: 'notes/x.md', raw, material });
  return [...r.findings, ...verifyCandidates(r.pending, material)];
}

const sentence = text => `some public words\n${text}\nmore public words\n`;

// ---- a reviewed line is not found, and the same text with one word changed is -----------------------------------------------------------------------

test('A_reviewed_line_is_not_found_and_the_same_text_with_one_word_changed_is_found', () => {
  const m = materialOf(homeWith({}, [reviewedEntry('line', LINE_A)])).material;
  assert.deepEqual(hitsIn(m, sentence(LINE_A)), []);
  assert.ok(hitsIn(m, sentence(LINE_B)).length >= 1, 'LINE_B is not on the list');
  const control = materialOf(homeWith()).material;
  assert.ok(hitsIn(control, sentence(LINE_A)).length >= 1, 'without the list the same line is found, so the list is what removed it');
});

test('A_list_entry_for_a_changed_word_does_not_exempt_the_line_it_resembles', () => {
  const home = homeWith({ cases: [{ id: 'h001', input: 'short\n', output: `${LINE_A}\n` }] }, [reviewedEntry('line', LINE_B)]);
  assert.ok(hitsIn(materialOf(home).material, sentence(LINE_A)).length >= 1);
});

test('The_reviewed_line_is_read_in_its_normal_form_so_a_table_row_or_a_list_item_of_it_is_not_found_either', () => {
  const m = materialOf(homeWith({}, [reviewedEntry('line', `  ${LINE_A.toUpperCase()}  `)])).material;
  assert.deepEqual(hitsIn(m, `| ${LINE_A} |\n- ${LINE_A}\n`), []);
});

test('A_short_case_line_a_reference_line_and_a_long_line_that_are_reviewed_are_not_found_and_their_neighbours_are', () => {
  const list = [reviewedEntry('line', SHORT_A), reviewedEntry('line', REF_A), reviewedEntry('line', LONG_A)];
  const m = materialOf(homeWith({}, list)).material;
  for (const reviewed of [SHORT_A, REF_A, LONG_A]) assert.deepEqual(hitsIn(m, sentence(reviewed)), [], `reviewed: ${reviewed.slice(0, 12)}`);
  for (const other of [SHORT_B, REF_B, LONG_B]) assert.ok(hitsIn(m, sentence(other)).length >= 1, `not reviewed: ${other.slice(0, 12)}`);
});

test('A_reviewed_line_is_dropped_before_the_keys_of_every_tier_are_built', () => {
  const all = materialOf(homeWith()).material;
  const some = materialOf(homeWith({}, [reviewedEntry('line', LINE_A), reviewedEntry('line', LONG_A), reviewedEntry('line', REF_A)])).material;
  assert.equal(some.substrings.length, all.substrings.length - 2, 'LINE_A and REF_A are substring needles; LONG_A is over 199 characters and is none');
  assert.ok(some.lineKeys.size < all.lineKeys.size, 'exact-line keys');
  assert.ok(some.chunkKeys.size < all.chunkKeys.size, 'chunk keys');
  assert.ok(some.codeByKey.size < all.codeByKey.size, 'code keys');
});

// ---- windows ----------------------------------------------------------------------------------------------------------------------------------------------

const windowHome = list => homeWith({ cases: [{ id: 'h001', input: 'short\n', output: `${WIN.join('\n')}\n` }] }, list);

test('A_window_of_a_reviewed_line_and_new_lines_is_found_and_a_window_of_reviewed_lines_only_is_dropped', () => {
  const none = materialOf(windowHome(null)).material;
  assert.equal(none.windowKeys.size, 1);
  const one = materialOf(windowHome([reviewedEntry('line', WIN[0])])).material;
  assert.equal(one.windowKeys.size, 1, 'one reviewed line: the other two are not');
  assert.ok(hitsIn(one, WIN.join('\n')).some(f => f.tier === 'window'), 'the window is found');
  const two = materialOf(windowHome([reviewedEntry('line', WIN[0]), reviewedEntry('line', WIN[2])])).material;
  assert.equal(two.windowKeys.size, 1);
  const all = materialOf(windowHome(WIN.map(l => reviewedEntry('line', l)))).material;
  assert.equal(all.windowKeys.size, 0, 'only reviewed lines');
  assert.deepEqual(hitsIn(all, WIN.join('\n')), []);
});

test('A_window_of_FORMAT_grammar_and_reviewed_lines_is_dropped_and_one_new_line_in_it_keeps_it', () => {
  const grammar = new Set([normLine(WIN[0])]);
  const build = list => {
    const r = presentMaterial({ home: windowHome(list), publicText: '', grammarLines: grammar });
    return r.value.material;
  };
  assert.equal(build([reviewedEntry('line', WIN[1]), reviewedEntry('line', WIN[2])]).windowKeys.size, 0, 'grammar, reviewed, reviewed');
  assert.equal(build([reviewedEntry('line', WIN[1])]).windowKeys.size, 1, 'a new third line keeps the window');
});

// ---- phrases ------------------------------------------------------------------------------------------------------------------------------------------------

test('A_reviewed_phrase_is_not_found_and_a_new_phrase_is_found', () => {
  const m = materialOf(homeWith({}, [reviewedEntry('shingle', 'alpha bravo charlie delta echo foxtrot')])).material;
  assert.equal(m.shingleTexts.length, 2);
  assert.deepEqual(hitsIn(m, 'we wrote: Alpha, bravo, charlie, delta, echo, foxtrot.'), []);
  const fresh = hitsIn(m, 'we wrote: bravo charlie delta echo foxtrot golf.');
  assert.deepEqual(fresh.map(f => [f.tier, f.task, f.caseFile]), [['shingle', '04', 'private notes']]);
  assert.equal(materialOf(homeWith()).material.shingleTexts.length, 3, 'without the list there are three phrases');
});

test('A_phrase_entry_never_exempts_a_line_and_a_line_entry_never_exempts_a_phrase', () => {
  const six = 'alpha bravo charlie delta echo foxtrot';
  const asLine = materialOf(homeWith({}, [reviewedEntry('line', six)])).material;
  assert.equal(asLine.shingleTexts.length, 3, 'a line entry leaves the phrases');
  const phraseAsLine = materialOf(homeWith({ cases: [{ id: 'h001', input: 'short\n', output: `${six}\n` }] }, [reviewedEntry('shingle', six)])).material;
  assert.ok(phraseAsLine.substrings.some(n => n.text === six), 'a phrase entry leaves the line');
});

// ---- the file --------------------------------------------------------------------------------------------------------------------------------------------

test('An_absent_file_exempts_nothing_and_is_not_an_error', () => {
  const home = homeWith();
  const loaded = loadReviewed({ home });
  assert.equal(loaded.ok, true);
  assert.deepEqual([loaded.value.counts.lines, loaded.value.counts.shingles], [0, 0]);
  assert.equal(loaded.value.hasLine(normLine(LINE_A)), false);
  const m = materialOf(home);
  assert.deepEqual(m.reviewed, { lines: 0, shingles: 0 });
  assert.ok(hitsIn(m.material, sentence(LINE_A)).length >= 1);
});

test('An_empty_list_is_a_list_and_exempts_nothing', () => {
  const m = materialOf(homeWith({}, []));
  assert.deepEqual(m.reviewed, { lines: 0, shingles: 0 });
});

const GOOD = reviewedEntry('line', LINE_A);
const without = field => { const { [field]: _gone, ...rest } = GOOD; return rest; };
const J = JSON.stringify;
const MALFORMED = {
  'not json': 'not json {',
  'a bare array': '[]',
  'no entries': '{}',
  'entries that is not a list': '{"entries": {}}',
  'a hash that is not 64 hex digits': J({ entries: [{ ...GOOD, sha256: 'abc123' }] }),
  'a hash in upper case': J({ entries: [{ ...GOOD, sha256: GOOD.sha256.toUpperCase() }] }),
  'a kind that is neither line nor shingle': J({ entries: [{ ...GOOD, kind: 'word' }] }),
  'a missing field': J({ entries: [without('by')] }),
  'an empty reason': J({ entries: [{ ...GOOD, reason: '' }] }),
  'a time that is not UTC': J({ entries: [{ ...GOOD, reviewedAt: '2026-11-06T09:00:00+02:00' }] }),
  'a day instead of an instant': J({ entries: [{ ...GOOD, reviewedAt: '2026-11-06' }] }),
  'the same entry twice': J({ entries: [GOOD, GOOD] }),
  'an entry that is not an object': J({ entries: ['x'] }),
};

test('A_malformed_file_is_a_refusal_never_an_empty_list', () => {
  for (const [what, content] of Object.entries(MALFORMED)) {
    const home = homeWith({}, content);
    const r = presentMaterial({ home, publicText: '' });
    assert.equal(r.ok, false, `${what}: refused`);
    assert.match(r.reason, /leak-reviewed\.json/, what);
  }
});

test('A_malformed_file_makes_the_command_exit_4_and_a_good_one_does_not', async () => {
  const repoRoot = repoWith();
  const run = home => scanBenchHalf({ values: { task: '04' }, io: { home, repoRoot, progress: () => {} }, explore: EXPLORE, say: () => {} });
  const bad = await run(homeWith({}, '{"entries": [{"sha256": "no"}]}'));
  assert.equal(bad.exit, 4);
  assert.match(bad.message, /leak-reviewed\.json/);
  assert.equal((await run(homeWith({}, [GOOD]))).exit, 0);
});

test('The_list_can_never_hold_a_text_a_field_beyond_those_named_is_refused_and_the_refusal_does_not_echo_it', () => {
  const secret = 'a-private-line-that-must-not-be-echoed-9981';
  const cases = {
    'a text field in an entry': { entries: [{ ...GOOD, text: secret }] },
    'a line field in an entry': { entries: [{ ...GOOD, line: secret }] },
    'a field beside entries': { entries: [GOOD], [secret]: 'x' },
    'a note beside entries': { entries: [GOOD], note: secret },
  };
  for (const [what, content] of Object.entries(cases)) {
    const parsed = parseReviewed(JSON.stringify(content));
    assert.equal(parsed.ok, false, what);
    assert.doesNotMatch(parsed.reason, /must-not-be-echoed/, `${what}: the refusal names the field's place, never its value`);
  }
  const named = parseReviewed(JSON.stringify(cases['a text field in an entry']));
  assert.match(named.reason, /entry 1/);
});

test('The_hash_is_the_sha256_of_the_normal_form_the_tier_matches', () => {
  const sha = text => createHash('sha256').update(text, 'utf8').digest('hex');
  assert.equal(reviewHash('line', ' | RECV   C "X" |\t'), sha(normLine(' | RECV   C "X" |\t')));
  assert.equal(reviewHash('line', 'recv c "x"'), sha('recv c "x"'));
  assert.equal(reviewHash('shingle', 'Alpha, bravo! charlie delta echo foxtrot.'), sha('alpha bravo charlie delta echo foxtrot'));
  assert.match(reviewHash('line', 'x'), /^[0-9a-f]{64}$/);
  assert.notEqual(reviewHash('line', 'x'), reviewHash('line', 'y'));
  assert.throws(() => reviewHash('word', 'x'), /line|shingle/);
  assert.match(reviewedPath('/h'), /\/h\/rsd\/lang-v2\/explore\/leak-reviewed\.json$/);
});

// ---- the count, and the guard of the branch --------------------------------------------------------------------------------------------------------

const LIST = [reviewedEntry('line', LINE_A), reviewedEntry('line', SHORT_A), reviewedEntry('shingle', 'alpha bravo charlie delta echo foxtrot')];
const TEXTS = [LINE_A, SHORT_A, REF_A, 'alpha bravo charlie'];

test('The_result_line_says_how_many_lines_and_phrases_were_exempt_by_a_reading_and_never_what_they_were', async () => {
  const said = [];
  const run = (home, repoRoot) => scanBenchHalf({ values: { task: '04' }, io: { home, repoRoot, progress: () => {} }, explore: EXPLORE, say: l => said.push(l) });
  assert.equal((await run(homeWith({}, LIST), repoWith())).exit, 0);
  assert.match(said.join('\n'), /2 private line\(s\) and 1 phrase\(s\) exempt by a person's reading/);
  said.length = 0;
  const failing = repoWith({ 'docs/oops.md': sentence(LINE_B) });
  assert.equal((await run(homeWith({}, LIST), failing)).exit, 5);
  assert.match(said.join('\n'), /2 private line\(s\) and 1 phrase\(s\) exempt by a person's reading/, 'a failing run says it too');
  for (const text of TEXTS) assert.equal(said.join('\n').includes(text), false, 'no text');
  said.length = 0;
  await run(homeWith(), repoWith());
  assert.doesNotMatch(said.join('\n'), /exempt by a person/, 'no list, no sentence');
});

const GRAMMAR_FILE = 'spikes/lang-bench/test/hello/samples/daemon-pass/s01.in';

test('A_held_out_line_committed_under_test_hello_on_the_branch_is_still_found_when_it_is_not_on_the_list', async () => {
  const home = homeWith({ cases: [{ id: 'h001', input: 'scenario 2\n', output: `${LINE_A}\n${LINE_B}\n` }] }, [reviewedEntry('line', LINE_A)]);
  const repoRoot = repoWith({ [GRAMMAR_FILE]: 'scenario 2\ndefault within 2s\n' });
  git(repoRoot, 'update-ref', 'refs/remotes/origin/main', 'HEAD');
  commitFiles(repoRoot, { [GRAMMAR_FILE]: `scenario 2\ndefault within 2s\n${LINE_B}\n` }, 'a held-out line added to a public example on the branch');
  const said = [];
  const r = await scanBenchHalf({ values: { task: '04' }, io: { home, repoRoot, progress: () => {} }, explore: EXPLORE, say: l => said.push(l) });
  assert.equal(r.exit, 5, said.join('\n'));
  assert.equal(said.join('\n').includes('invented-reviewed-answer-bravo'), false, 'the output never holds the text');
});

// ---- the cap of the substring tier ---------------------------------------------------------------------------------------------------------------

test('The_substring_tier_misses_no_needle_at_its_cap_of_a_quarter_of_the_prefix_table', () => {
  assert.equal(SUBSTRING_NEEDLES_MAX, BITMAP_SLOTS / 4);
  const needles = Array.from({ length: SUBSTRING_NEEDLES_MAX }, (_, i) => ({ text: `${i} invented private line of number ${i * 31}` }));
  const index = buildSubstringIndex(needles);
  assert.equal(index.size, SUBSTRING_NEEDLES_MAX);
  const missed = needles.filter(n => !findSubstrings(`public words before ${n.text} and after`, index).some(hit => hit.needle === n));
  assert.deepEqual(missed.map(n => n.text), [], 'every needle is found, alone in a haystack');
  const together = findSubstrings(needles.map(n => n.text).join('\n'), index);
  assert.equal(together.length, SUBSTRING_NEEDLES_MAX, 'and all of them in one haystack');
});
