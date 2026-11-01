// The two needle families of the push check: the private aids' six-word shingles and literals (reported by position, never by text)
// and the known-task needles of parent D18 with the matching rule of change 4.1 (case-sensitive, word boundaries, exact substrings).
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { aidNeedles, findAidHits, findKnownNeedles, parseKnownNeedles } from '../lib/push-needles.mjs';

// A synthetic aid: invented prose, never the real aids (the real ones are read by the tool from the private area at run time).
const AID = { name: 'synthetic-aid.md', text: 'Notes.\nA widget that frobnicates every sprocket twice before lunch will stall.\nUnrelated closing line here.\n' };
const needlesFile = JSON.parse(readFileSync(fileURLToPath(new URL('../inputs/needles-lang.json', import.meta.url)), 'utf8'));
const escape = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const known = parseKnownNeedles(needlesFile).value;

test('Push_check_reports_an_aid_shingle_by_position_and_never_by_text', () => {
  const needles = aidNeedles({ aids: [AID] }).value;
  const suspect = 'intro line\nsomething else\nthe widget that frobnicates every\nsprocket twice before lunch is fine\n';
  const found = findAidHits('csharp/x/Program.cs', suspect, needles);
  assert.deepEqual(found.map(f => [f.path, f.line, f.what]), [['csharp/x/Program.cs', 3, 'shingle of synthetic-aid.md']]);
  assert.equal(JSON.stringify(found).toLowerCase().includes('frobnicates'), false, 'the finding holds no word of the aid');
});

test('A_shingle_scan_would_miss_a_hit_split_over_two_lines_if_it_read_one_line_at_a_time_and_that_is_the_point', () => {
  const needles = aidNeedles({ aids: [AID] }).value;
  const oneLineAtATime = 'a widget that\nfrobnicates every sprocket twice\n'.split('\n').flatMap(l => findAidHits('p', l, needles));
  assert.equal(oneLineAtATime.length, 0, 'the refuted approach sees nothing');
  assert.equal(findAidHits('p', 'a widget that\nfrobnicates every sprocket twice\n', needles).length, 1);
});

test('A_text_sharing_only_five_consecutive_words_with_an_aid_is_not_a_hit', () => {
  const needles = aidNeedles({ aids: [AID] }).value;
  assert.deepEqual(findAidHits('p', 'a widget that frobnicates every cog\n', needles), []);
});

test('A_literal_token_is_found_case_insensitively_and_reported_by_its_number', () => {
  const needles = aidNeedles({ aids: [AID], tokensText: 'zz-first-token\nQuux-Marker-77\n' }).value;
  const found = findAidHits('p', 'line one\nsee QUUX-marker-77 here\n', needles);
  assert.deepEqual(found.map(f => [f.line, f.what]), [[2, 'leak token 2']]);
});

test('A_shingle_that_a_public_text_already_holds_is_not_an_aid_hit', () => {
  const publicText = 'Everything about the plan: a widget that frobnicates every sprocket twice before lunch is public here.\n';
  const needles = aidNeedles({ aids: [AID], publicTexts: [publicText] }).value;
  assert.deepEqual(findAidHits('README.md', 'a widget that frobnicates every sprocket twice before lunch\n', needles), [], 'public text cannot leak');
  const other = { name: 'other-aid.md', text: 'Quite another private sentence about gadgets and their wobbling springs today.\n' };
  const both = aidNeedles({ aids: [AID, other], publicTexts: [publicText] }).value;
  assert.equal(findAidHits('p', 'gadgets and their wobbling springs today\n', both).length, 1, 'only the shingles of the public text are dropped');
  const allPublic = aidNeedles({ aids: [AID], publicTexts: [AID.text] });
  assert.equal(allPublic.ok, false, 'an aid wholly covered by public text would check nothing');
  assert.match(allPublic.reason, /synthetic-aid\.md/);
});

test('An_aid_with_no_six_words_is_refused_instead_of_checking_nothing', () => {
  const r = aidNeedles({ aids: [{ name: 'empty.md', text: 'too short\n' }] });
  assert.equal(r.ok, false);
  assert.match(r.reason, /empty\.md/);
});

test('The_needle_scan_still_finds_a_planted_needle', () => {
  const hits = findKnownNeedles('tasks/01/x.md', 'ok line\nthe CSES set and visit https://cses.fi/x\n', known);
  assert.deepEqual(hits.map(h => [h.line, h.what]), [[2, 'needle CSES'], [2, 'needle cses.fi']]);
});

test('A_case_insensitive_needle_match_would_flag_response_and_that_is_the_point', () => {
  const text = 'THE RESPONSE BODY, the response body, a resp_code field, iOiJ in base64, Werewolves and werewolf\n';
  assert.deepEqual(findKnownNeedles('p', text, known), [], 'case and boundaries decide (change 4.1)');
  // the refuted reading of the maintenance draft: a case-insensitive substring match
  const refuted = known.words.filter(w => new RegExp(escape(w), 'i').test(text));
  assert.ok(refuted.includes('RESP') && refuted.includes('IOI'), `the refuted rule flags RESP and IOI (it flagged: ${refuted.join(', ')})`);
});

test('A_word_needle_matches_on_a_boundary_and_a_substring_needle_matches_inside_a_url', () => {
  const hits = findKnownNeedles('p', 'Werewolf is one; the Werewolf problem; RESP3 here; see redis.io/docs and node-redis\n', known).map(h => h.what);
  assert.deepEqual(hits, ['needle Werewolf', 'needle RESP3', 'needle redis', 'needle redis.io', 'needle node-redis'], 'one finding per needle per line, words before substrings');
});

test('A_needle_file_with_a_short_or_empty_entry_is_refused', () => {
  assert.equal(parseKnownNeedles({ schema: 1, words: [''], substrings: [] }).ok, false);
  assert.equal(parseKnownNeedles({ schema: 2, words: ['CSES'], substrings: [] }).ok, false);
});

test('A_leaked_passage_with_markup_or_a_slash_every_five_words_is_still_caught_because_no_token_is_dropped', () => {
  const passage = 'The quick violet engine whistles beneath seven copper bridges while the lantern keeper counts every falling marble slowly.';
  const needles = aidNeedles({ aids: [{ name: 'a.md', text: `Notes.\n${passage}\n` }] }).value;
  const words = passage.split(' ');
  const wrapped = (decorate) => `${words.map((w, i) => (i % 5 === 2 ? decorate(w) : w)).join(' ')}\n`;
  assert.equal(findAidHits('p', wrapped(w => `_${w}_`), needles).length > 0, true, 'an _emphasised_ word every five words');
  assert.equal(findAidHits('p', wrapped(w => `${w}/`), needles).length > 0, true, 'a word joined by a slash every five words');
  assert.equal(findAidHits('p', wrapped(w => `${w}\\`), needles).length > 0, true, 'a backslash');
  // the refuted approach: dropping every token that holds `_`, `/` or `\\` leaves windows too short to hold six words
  const droppedTokens = wrapped(w => `${w}/`).split(/\s+/).filter(t => !/[/\\_]/.test(t)).join(' ');
  assert.equal(findAidHits('p', droppedTokens, needles).length, 0, 'the refuted reading sees nothing');
});

test('A_multi_word_needle_wrapped_over_lines_is_found_and_reported_where_it_starts', () => {
  const text = 'intro\nThis is the Counting\nRooms problem, and Weird\n  Algorithm too, and Counting   Rooms again.\n';
  const hits = findKnownNeedles('todo/tasks/03-x.md', text, known).map(h => [h.line, h.what]);
  assert.deepEqual(hits, [[2, 'needle Counting Rooms'], [3, 'needle Weird Algorithm'], [4, 'needle Counting Rooms']]);
  // the refuted approach: one line at a time sees neither wrapped needle
  const lineByLine = text.split('\n').flatMap(l => findKnownNeedles('p', l, known));
  assert.equal(lineByLine.filter(h => h.what === 'needle Weird Algorithm').length, 0);
  assert.equal(findKnownNeedles('p', 'Counting-Rooms and CountingRooms and counting rooms\n', known).length, 0, 'only whitespace joins the words, and case still counts');
});

test('A_one_token_window_is_exempt_only_when_the_token_is_verbatim_in_the_public_texts_a_made_up_joined_passage_is_a_hit', () => {
  const aid = { name: 'a.md', text: 'Notes.\nsee todo/PLAN_lang_comparison_v2.md and a-made-up-six-word-joined-passage-here for details.\n' };
  const publicText = 'The plan is todo/PLAN_lang_comparison_v2.md in the repository.\n';
  const needles = aidNeedles({ aids: [aid], publicTexts: [publicText] }).value;
  assert.deepEqual(findAidHits('p', 'check todo/PLAN_lang_comparison_v2.md today\n', needles), [], 'a path the public texts hold verbatim is not a hit');
  assert.deepEqual(findAidHits('todo/PLAN_lang_comparison_v2.md', 'todo/PLAN_lang_comparison_v2.md', needles), [], 'nor as a path scanned as text');
  const joined = findAidHits('p', 'copied a-made-up-six-word-joined-passage-here from the aid\n', needles);
  assert.deepEqual(joined.map(h => [h.line, h.what, h.tokens]), [[1, 'shingle of a.md', 1]], 'six words joined by hyphens are ONE token and still a hit: joining is not an exemption');
  const slashed = findAidHits('p', 'a/made/up/six/word/passage/here\n', aidNeedles({ aids: [{ name: 'b.md', text: 'Notes.\na/made/up/six/word/passage/here\n' }], publicTexts: [publicText] }).value);
  assert.equal(slashed.length, 1, 'and so is a slash-joined one');
});

test('A_real_aid_sentence_copied_into_a_path_heavy_line_is_still_a_hit_because_its_windows_span_tokens', () => {
  const aid = { name: 'a.md', text: 'Notes.\nthe violet engine whistles beneath seven copper bridges at dawn.\n' };
  const needles = aidNeedles({ aids: [aid] }).value;
  const heavy = 'src/a/b/c.md: the violet engine whistles beneath seven copper bridges at dawn. see https://x.test/y/z\n';
  assert.deepEqual(findAidHits('p', heavy, needles).map(f => [f.line, f.what]), [[1, 'shingle of a.md']]);
  const words = 'the violet engine whistles beneath seven copper bridges at dawn'.split(' ');
  const marked = words.map((w, i) => (i % 5 === 2 ? `_${w}_` : i % 5 === 4 ? `${w}/` : w)).join(' ');
  assert.equal(findAidHits('p', `${marked}\n`, needles).length, 1, 'markup and slashes every few words leave windows that span tokens');
});

test('A_window_of_six_words_that_straddles_two_tokens_by_one_word_is_checked', () => {
  const aid = { name: 'a.md', text: 'Notes.\ntodo/tasks/alpha/beta/gamma/delta then more words.\n' };
  const needles = aidNeedles({ aids: [aid] }).value;
  assert.equal(findAidHits('p', 'x todo/tasks/alpha/beta/gamma/delta then more words\n', needles).length, 1, 'the one-token window is skipped, the spanning ones are not');
});

test('A_shingle_finding_says_how_many_tokens_its_window_spans_and_never_which_words', () => {
  const aid = { name: 'a.md', text: 'Notes.\nthe violet engine whistles beneath seven copper bridges at dawn.\n' };
  const needles = aidNeedles({ aids: [aid] }).value;
  const [hit] = findAidHits('p', 'the violet engine whistles beneath seven copper bridges at dawn\n', needles);
  assert.equal(hit.tokens, 6, 'six plain words are six tokens');
  const [joined] = findAidHits('p', 'the violet engine whistles beneath-seven-copper bridges at dawn\n', needles);
  assert.equal(joined.tokens, 5, 'the first window is five tokens: the, violet, engine, whistles and one joined token');
  assert.equal(JSON.stringify(hit).includes('violet'), false);
});
