// The third kind of the person-reviewed list of the leak check: `chunk`. A public text can hold an exact 64-character slice of a long private case line (a sample transcript in a task spec uses the same JSON shapes as the
// hidden cases). A `line` entry would blind the check to the WHOLE line; a `chunk` entry exempts only the one aligned 64-character slice of the normalised line, hashed exactly (boundary spaces included), and every other part of
// the line keeps its key. The private side is a fixture of INVENTED content in a temp home; nothing real is read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { test } from 'node:test';
import { scanBenchHalf } from './bench-half-seam.mjs';
import { caseLinesOf } from '../lib/leak-case-files.mjs';
import { CHUNK, chunkOffsets } from '../lib/leak-chunks.mjs';
import { loadReviewed, parseReviewed, reviewHash } from '../lib/leak-reviewed.mjs';
import { scanText } from '../lib/leak-scan.mjs';
import { normLine, polyHash } from '../lib/leak-text.mjs';
import { verifyCandidates, verifyChunks } from '../lib/leak-verify.mjs';
import { repoWith } from './leak-scan-fixture.mjs';
import { EXPLORE, presentMaterial, quickHome, quickTask, reviewedEntry, writeQuickHeldout, writeReviewed } from './quick-leak-fixture.mjs';

const ALPHABET = 'bcdfghjklmnpqrstvwxz0123456789';
/** An invented space-free text of `length` characters, the same for the same seed. */
const piece = (seed, length) => Array.from({ length }, (_, i) => ALPHABET[(seed * 7 + i * i * 3 + i * seed) % ALPHABET.length]).join('');

const C0 = piece(1, 64);
const C1 = ` ${piece(2, 62)} `; // starts and ends with a space: the spaces are part of the chunk
const C2 = piece(3, 64);
const C3 = piece(4, 64);
const C4 = piece(5, 64);
const H1 = C0 + C1 + C2 + C3 + C4; // 320 characters, already in normal form
const V1 = `${C1.slice(0, 10)}${C1[10] === 'z' ? 'y' : 'z'}${C1.slice(11)}`; // C1 with one character changed
const H2 = piece(6, 64) + V1 + piece(7, 64) + piece(8, 64); // 256 characters: a long line of its own that holds V1 as an aligned chunk
const sha = text => createHash('sha256').update(text, 'utf8').digest('hex');

function homeWith(list = null, cases = [{ id: 'h001', input: 'short\n', output: `${H1}\n${H2}\n` }], notes = 'invented notes about the order of invented events') {
  const home = quickHome();
  writeQuickHeldout({ home, task: quickTask('04'), traps: [], refs: {}, notes, cases });
  if (list !== null) writeReviewed(home, list);
  return home;
}

const materialOf = home => {
  const r = presentMaterial({ home, publicText: '' });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};

/** What the tiers find in `raw` (a public file), verified. */
function hitsIn(material, raw, diff = false) {
  const r = scanText({ name: 'notes/x.md', raw, diff, material });
  return [...r.findings, ...verifyCandidates(r.pending, material)];
}

/** `text` quoted in the middle of a public line, at an unaligned position, with a non-space character on each side (a boundary space of the text survives the normal form only so). */
const quoted = text => `some public words\nquoted:${text}(cut)\nmore public words\n`;
/** `text` as a line of its own among public lines. */
const plain = text => `some public words\n${text}\nmore public words\n`;
const tiersOf = hits => [...new Set(hits.map(h => h.tier))].sort();

test('A_reviewed_chunk_is_not_found_and_a_control_without_the_list_finds_it', () => {
  const control = materialOf(homeWith()).material;
  assert.deepEqual(hitsIn(control, quoted(C1)).map(h => [h.tier, h.caseFile, h.fromLine]), [['chunk', 'cases/h001.out', 1]], 'without the list the chunk is a chunk-tier finding');
  const m = materialOf(homeWith([reviewedEntry('chunk', C1)])).material;
  assert.deepEqual(hitsIn(m, quoted(C1)), []);
});

test('Another_aligned_chunk_of_the_same_line_and_a_chunk_one_character_different_are_still_found', () => {
  const m = materialOf(homeWith([reviewedEntry('chunk', C1)])).material;
  assert.deepEqual(tiersOf(hitsIn(m, quoted(C2))), ['chunk'], 'the next chunk of the same hidden line keeps its key');
  assert.deepEqual(tiersOf(hitsIn(m, quoted(C0))), ['chunk'], 'and so does the first');
  const variant = hitsIn(m, quoted(V1));
  assert.deepEqual(variant.map(h => [h.tier, h.fromLine]), [['chunk', 2]], 'the variant is an aligned chunk of the second hidden line, so it is found');
  assert.notEqual(V1, C1);
});

test('The_whole_line_pasted_is_still_a_line_tier_finding_when_one_or_all_of_its_chunks_are_reviewed', () => {
  const one = materialOf(homeWith([reviewedEntry('chunk', C1)])).material;
  assert.ok(tiersOf(hitsIn(one, `${H1}\n`)).includes('line'), 'one chunk reviewed');
  const all = [C0, C1, C2, C3, C4].map(c => reviewedEntry('chunk', c));
  const m = materialOf(homeWith(all)).material;
  assert.deepEqual(tiersOf(hitsIn(m, `${H1}\n`)), ['line'], 'every chunk reviewed: the line tier alone still finds the whole line');
});

const SIX = `${['tangerine', 'blossoms', 'wandering', 'across', 'silent'].join(' ')} ${'mountainsmountainsmoun'.slice(0, 21)}`; // six words, 64 characters

test('A_chunk_entry_never_exempts_the_same_text_as_a_standalone_line_a_note_phrase_or_a_window_and_a_line_entry_still_does', () => {
  assert.equal(SIX.length, CHUNK);
  const line = materialOf(homeWith([reviewedEntry('chunk', SIX)], [{ id: 'h001', input: 'short\n', output: `${SIX}\n` }])).material;
  assert.ok(hitsIn(line, plain(SIX)).length >= 1, 'a standalone hidden line of 64 characters');
  const phrase = materialOf(homeWith([reviewedEntry('chunk', SIX)], undefined, SIX)).material;
  assert.deepEqual(tiersOf(hitsIn(phrase, plain(SIX))), ['shingle'], 'a note phrase of the same six words');
  const lines = [`${SIX.slice(0, 63)}1`, `${SIX.slice(0, 63)}2`, `${SIX.slice(0, 63)}3`];
  const win = materialOf(homeWith(lines.map(l => reviewedEntry('chunk', l)), [{ id: 'h001', input: 'short\n', output: `${lines.join('\n')}\n` }])).material;
  assert.equal(win.windowKeys.size, 1, 'the window keeps its key');
  assert.ok(tiersOf(hitsIn(win, `${lines.join('\n')}\n`)).includes('window'));
  const asLine = materialOf(homeWith([reviewedEntry('line', H1)])).material;
  assert.deepEqual(hitsIn(asLine, quoted(C2)), [], 'a line entry removes the whole line from every tier, as before');
});

test('A_reviewed_chunk_is_dropped_from_the_keys_and_only_that_chunk', () => {
  const all = materialOf(homeWith()).material;
  const some = materialOf(homeWith([reviewedEntry('chunk', C1)])).material;
  assert.equal(all.chunkKeys.size - some.chunkKeys.size, 1, 'exactly one key less');
  assert.equal(some.lineKeys.size, all.lineKeys.size, 'the line tier is untouched');
});

// ---- the exact slice ------------------------------------------------------------------------------------------------------------------------------

test('The_chunk_hash_is_the_sha256_of_the_exact_64_characters_with_their_boundary_spaces_and_other_lengths_are_refused', () => {
  assert.equal(C1.length, CHUNK);
  assert.equal(reviewHash('chunk', C1), sha(C1));
  assert.notEqual(reviewHash('chunk', C1), sha(C1.trim()), 'the boundary spaces are part of the text');
  assert.notEqual(reviewHash('chunk', C1), reviewHash('chunk', ` ${C1.slice(1, 63)}x`), 'one character different is another entry');
  for (const length of [0, 1, 63, 65, 128]) assert.throws(() => reviewHash('chunk', 'q'.repeat(length)), /chunk.*64/, `length ${length}`);
  assert.throws(() => reviewHash('chunk', 'a-secret-text-that-is-not-sixty-four-characters'), error => !/secret/.test(error.message), 'the refusal never echoes the text');
  assert.throws(() => reviewHash('word', 'x'), /line, shingle, chunk/, 'the legal kinds are named');
});

test('The_line_is_normalised_before_it_is_sliced_so_the_entry_is_the_slice_of_the_normal_form_and_not_of_the_raw_line', () => {
  const raw = `- ${C0.toUpperCase()} |  ${C1.trim()}   ${C2.toUpperCase()} ${C3}\t${C4}  `;
  const n = normLine(raw);
  assert.equal(n.length >= 256, true);
  const normalForm = n.slice(64, 128);
  const rawForm = raw.slice(64, 128);
  assert.equal(rawForm.length, 64);
  assert.notEqual(rawForm, normalForm);
  const home = homeWith([reviewedEntry('chunk', normalForm)], [{ id: 'h001', input: 'short\n', output: `${raw}\n` }]);
  const m = materialOf(home).material;
  assert.deepEqual(hitsIn(m, quoted(normalForm)), [], 'the slice of the normal form is exempt');
  const control = materialOf(homeWith([reviewedEntry('chunk', rawForm)], [{ id: 'h001', input: 'short\n', output: `${raw}\n` }])).material;
  assert.ok(hitsIn(control, quoted(normalForm)).length >= 1, 'the slice of the raw line exempts nothing');
});

test('A_chunk_with_high_latin1_bytes_goes_through_the_real_case_file_read_and_the_real_hash_and_only_that_exact_chunk_is_exempt', () => {
  const accent = 'ÉéüßÀ£'; // bytes 0xC9 0xE9 0xFC 0xDF 0xC0 0xA3: one JS character each when read as latin1
  const raw = `${piece(11, 40)}${accent}${piece(12, 18)}${piece(13, 64)}${accent}${piece(14, 50)}${accent}${piece(15, 14)}${piece(16, 70)}`;
  const home = homeWith(null, [{ id: 'h001', input: 'short\n', output: Buffer.from(`${raw}\n`, 'latin1') }]);
  const material = materialOf(home).material;
  const casesDir = material.caseSources[0].casesDir;
  const line = caseLinesOf(casesDir, 'h001.out')[0];
  assert.equal(line, raw, 'the case file is read one character per byte');
  const n = normLine(line);
  const offsets = chunkOffsets(n);
  const at = offsets.find(o => /[^\x00-\x7f]/.test(n.slice(o, o + CHUNK)));
  assert.notEqual(at, undefined, 'a chunk with a high byte');
  const slice = n.slice(at, at + CHUNK);
  assert.equal(slice.length, CHUNK);
  assert.equal(reviewHash('chunk', slice), createHash('sha256').update(Buffer.from(slice, 'utf8')).digest('hex'), 'the hash is over the UTF-8 encoding of the JS string');
  const listed = materialOf(homeWith([reviewedEntry('chunk', slice)], [{ id: 'h001', input: 'short\n', output: Buffer.from(`${raw}\n`, 'latin1') }])).material;
  assert.deepEqual(hitsIn(listed, quoted(slice)), []);
  const others = offsets.filter(o => o !== at).map(o => n.slice(o, o + CHUNK));
  assert.ok(others.length >= 2);
  for (const other of others) assert.deepEqual(tiersOf(hitsIn(listed, quoted(other))), ['chunk'], 'every other aligned chunk is still found');
});

test('A_reviewed_chunk_is_exempt_at_an_unaligned_position_in_a_table_cell_and_in_git_log_diff_text_and_found_there_without_the_entry', () => {
  const control = materialOf(homeWith()).material;
  const m = materialOf(homeWith([reviewedEntry('chunk', C1), reviewedEntry('chunk', C2)])).material;
  const places = [['after 17 characters', `${'p'.repeat(17)}${C1}${'q'.repeat(5)}\n`, false], ['a table cell', `| a | ${C2} |\n`, false], ['an added diff line', `+see ${C2} end\n`, true], ['an added diff line with boundary spaces', `+see${C1}end\n`, true],
    ['a context diff line', ` see${C1}end\n`, true], ['a removed diff line', `-see${C1}end\n`, true]];
  for (const [what, raw, diff] of places) {
    assert.ok(hitsIn(control, raw, diff).length >= 1, `${what}: found without the entry`);
    assert.deepEqual(hitsIn(m, raw, diff), [], `${what}: exempt`);
  }
});

// ---- the verify step ------------------------------------------------------------------------------------------------------------------------------

const sourcesOf = home => materialOf(home).material.caseSources;
const candidate = (text, key) => ({ tier: 'chunk', file: 'x.md', line: 1, key, text, raws: [] });

test('A_reviewed_text_is_never_a_finding_even_when_an_unreviewed_chunk_shares_its_key_and_the_unreviewed_one_is', () => {
  const sources = sourcesOf(homeWith([reviewedEntry('chunk', C1)]));
  const everything = () => 7; // a seam: every chunk of every case line has the key 7
  const pending = [candidate(C1, 7), candidate(C2, 7)];
  assert.deepEqual(verifyChunks(pending, sources, everything).map(f => [f.fromLine, f.toLine]), [[1, 1]], 'one finding, for the unreviewed text');
  assert.deepEqual(verifyChunks([candidate(C1, 7)], sources, everything), [], 'the reviewed text alone is no finding');
  const bare = sources.map(s => ({ ...s, reviewed: undefined }));
  assert.equal(verifyChunks(pending, bare, everything).length, 2, 'without the list both texts are findings, so the list is what removed the first');
});

test('A_candidate_whose_own_text_is_a_reviewed_chunk_is_dropped_before_any_case_file_is_read', () => {
  const [source] = sourcesOf(homeWith([reviewedEntry('chunk', C1)]));
  const unreadable = { ...source, casesDir: path.join(source.casesDir, 'there-is-no-such-folder') };
  assert.deepEqual(verifyChunks([candidate(C1, polyHash(C1, 0, 64))], [unreadable]), []);
  assert.throws(() => verifyChunks([candidate(C2, polyHash(C2, 0, 64))], [unreadable]), /ENOENT/, 'control: any other candidate does read the folder');
});

test('A_chunk_reviewed_for_one_task_is_still_found_in_a_task_whose_side_has_no_such_review', () => {
  const [first] = sourcesOf(homeWith([reviewedEntry('chunk', C1)]));
  const second = { ...first, id: '09', reviewed: undefined };
  const found = verifyChunks([candidate(C1, polyHash(C1, 0, 64))], [first, second]);
  assert.deepEqual(found.map(f => f.task), ['09'], 'the list of the first task exempts it there only');
});

// ---- the file -----------------------------------------------------------------------------------------------------------------------------------------

test('The_loader_accepts_chunk_entries_counts_them_and_still_refuses_unknown_kinds_unknown_fields_and_a_malformed_file', () => {
  const good = reviewedEntry('chunk', C1);
  const home = homeWith([good, reviewedEntry('line', H1)]);
  const loaded = loadReviewed({ home });
  assert.equal(loaded.ok, true);
  assert.deepEqual(loaded.value.counts, { lines: 1, shingles: 0, chunks: 1 });
  assert.equal(loaded.value.hasChunk(C1), true);
  assert.equal(loaded.value.hasChunk(C2), false);
  assert.equal(loaded.value.hasChunk(C1.slice(0, 63)), false, 'only exactly 64 characters');
  assert.equal(loaded.value.hasLine(C1), false, 'a chunk entry is not a line entry');
  const J = JSON.stringify;
  for (const [what, content] of Object.entries({ 'an unknown kind': J({ entries: [{ ...good, kind: 'slice' }] }), 'a text field': J({ entries: [{ ...good, text: C1 }] }), 'a repeat': J({ entries: [good, good] }), 'not json': '{' })) {
    assert.equal(parseReviewed(content).ok, false, what);
  }
  assert.equal(parseReviewed(J({ entries: [good, { ...good, kind: 'line' }] })).ok, true, 'the same hash as a chunk and as a line are two entries');
  assert.deepEqual(loadReviewed({ home: homeWith() }).value.counts, { lines: 0, shingles: 0, chunks: 0 });
});

test('The_result_line_names_the_chunks_exempt_by_a_reading_and_never_what_they_were', async () => {
  const said = [];
  const run = (home, repoRoot) => scanBenchHalf({ values: { task: '04' }, io: { home, repoRoot, progress: () => {} }, explore: EXPLORE, say: l => said.push(l) });
  const home = homeWith([reviewedEntry('chunk', C1), reviewedEntry('line', H2)]);
  assert.deepEqual(materialOf(home).reviewed, { lines: 1, shingles: 0, chunks: 1 });
  assert.equal((await run(home, repoWith())).exit, 0);
  assert.match(said.join('\n'), /1 private line\(s\), 1 chunk\(s\) and 0 phrase\(s\) exempt by a person's reading/);
  said.length = 0;
  assert.equal((await run(home, repoWith({ 'docs/oops.md': quoted(C2) }))).exit, 5);
  const out = said.join('\n');
  assert.match(out, /1 chunk\(s\)/, 'a failing run says it too');
  for (const text of [C1, C2, H1]) assert.equal(out.includes(text), false, 'no text');
});
