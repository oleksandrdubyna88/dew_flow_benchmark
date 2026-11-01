// The leak scan's tiers (lib/leak-scan.mjs, redesigned for size): each tier is tested through the scan's one entry over a temp repository, with fixture held-out folders. A finding names task, case file and line range, and
// never holds the private text. v1's matcher is not used for the held-out lines any more.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { scanRepository } from '../lib/leak-scan.mjs';
import { EXPLORE, caseWith, commitFiles, git, materialOf, repoWith } from './leak-scan-fixture.mjs';

const scan = (repo, material, range = null) => scanRepository({ repoRoot: repo, material, range });
const SECRET = '918273645501';

// ---- tier 1: substring ----------------------------------------------------------------------------------------------------------------------

test('A_12_character_numeric_case_line_pasted_mid_sentence_is_a_substring_hit_naming_task_case_and_line_not_the_text', async () => {
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['first line', SECRET, 'third line'])] } });
  const clean = await scan(repoWith(), material);
  assert.equal(clean.ok, true, clean.reason);
  const repo = repoWith({ 'notes/run.md': `the program printed ${SECRET} on our laptop, which looked wrong\n` });
  const r = await scan(repo, material);
  assert.equal(r.ok, false);
  assert.deepEqual(r.findings.map(f => [f.tier, f.file, f.line, f.task, f.caseFile, f.fromLine, f.toLine]), [['substring', 'notes/run.md', 1, '03', 'cases/h001.out', 2, 2]]);
  assert.match(r.reason, /notes\/run\.md:1 \(substring, task 03, cases\/h001\.out lines 2\)/);
  assert.doesNotMatch(r.reason, new RegExp(SECRET), 'the message holds no private text');
  assert.doesNotMatch(JSON.stringify(r.findings), new RegExp(SECRET.slice(0, 8)), 'nor does a finding');
});

test('The_haystack_is_normalised_like_the_material_a_table_cell_upper_case_and_extra_spaces_still_match', async () => {
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['alpha  beta gamma delta epsilon zeta', SECRET])] } });
  for (const [name, text] of [['a table cell', `| row | ${SECRET} |\n`], ['a list item', `- ${SECRET}\n`], ['upper case and tabs', `Alpha\tBeta  GAMMA delta Epsilon\tzeta\n`], ['a quote', `> ${SECRET}\n`]]) {
    const r = await scan(repoWith({ 'x.md': text }), material);
    assert.equal(r.ok, false, name);
  }
  assert.equal((await scan(repoWith({ 'x.md': `${SECRET.slice(0, 11)}\n` }), material)).ok, true, 'a prefix of the line is not the line');
});

test('A_binary_file_skips_the_text_tiers_is_listed_as_line_tiers_only_and_a_text_file_with_the_same_bytes_is_a_hit', async () => {
  const { material } = materialOf({ '03': { cases: [caseWith('h001', [SECRET, 'other private line'])] } });
  const blob = Buffer.concat([Buffer.from('xz\0\0\0 header '), Buffer.from(`\n${SECRET}\n`)]);
  const r = await scan(repoWith({ 'archive.bin': blob }), material);
  assert.equal(r.ok, true, 'the substring tier does not read a file with a NUL in its first 8 KB');
  assert.deepEqual(r.binary, ['archive.bin']);
  assert.equal((await scan(repoWith({ 'archive.txt': blob.subarray(10) }), material)).ok, false);
});

test('More_than_3000_substring_needles_is_a_clear_failure_and_a_task_with_over_2000_short_lines_leaves_them_to_the_other_tiers', () => {
  const lines = Array.from({ length: 2001 }, (_, i) => `line number ${i + 100000}`);
  const big = materialOf({ '09': { cases: [caseWith('h001', lines)] } });
  assert.equal(big.built.ok, true, big.built.reason);
  assert.deepEqual(big.material.skipped, [{ task: '09', lines: 2001 }], 'reported, with the count');
  assert.equal(big.material.substrings.some(n => n.source.startsWith('cases/')), false, 'none of the 2001 is a substring needle');
  const exactly = materialOf({ '03': { cases: [caseWith('h001', lines.slice(0, 2000))] } });
  assert.equal(exactly.material.substrings.filter(n => n.source.startsWith('cases/')).length, 2000, 'a task with 2000 qualifies');
  const own = k => ({ cases: [caseWith('h001', lines.slice(0, 1500).map(l => `${l} of task ${k}`))] });
  const tooMany = materialOf({ '01': own(1), '02': own(2), '03': own(3) });
  assert.equal(tooMany.built.ok, false);
  assert.match(tooMany.built.reason, /the substring tier holds \d+ needles, over its cap of 3000/);
});

test('A_private_code_line_is_a_needle_unless_the_committed_runner_has_it_and_a_hit_names_the_source_file_and_line', async () => {
  const gen = 'import os\nSHARED_LINE = path.join(base, name)  # a line the runner also has\nunique_private_generator_line = compute_the_cases_somehow(42)\n';
  const runnerLines = new Set(['shared_line = path.join(base, name) # a line the runner also has']);
  const { material } = materialOf({ '02': { gen } }, { runnerLines });
  assert.deepEqual(material.substrings.filter(n => n.source === 'gen.py').map(n => n.line), [3], 'only the line the runner lacks');
  const r = await scan(repoWith({ 'a.py': 'x = 1  # unique_private_generator_line = compute_the_cases_somehow(42)\n' }), material);
  assert.equal(r.ok, false);
  assert.deepEqual(r.findings.map(f => [f.task, f.caseFile, f.fromLine]), [['02', 'gen.py', 3]]);
});

test('The_files_and_the_history_are_each_read_once_for_every_needle_and_a_history_only_hit_is_found', async () => {
  const { material } = materialOf({ '03': { cases: [caseWith('h001', [SECRET, 'another private case line'])] } });
  const repo = repoWith();
  const base = commitFiles(repo, { 'tmp.md': `see ${SECRET}\n` }, `a message that quotes ${SECRET}`);
  commitFiles(repo, { 'tmp.md': null }, 'remove it again');
  const r = await scan(repo, material, `HEAD~2..HEAD`);
  assert.equal(r.ok, false, base);
  assert.ok(r.findings.every(f => f.file.startsWith('git log')), 'the working tree is clean; the history is not');
  assert.deepEqual(r.findings.map(f => f.file).sort(), ['git log -p HEAD~2..HEAD', 'git log messages of HEAD~2..HEAD']);
});

// ---- tier 2: exact line ----------------------------------------------------------------------------------------------------------------------


/** A case line of `n` characters made of words, so it has spaces and more than one distinct character: not a substring needle (those stop at 199). */
const longLine = (seed, n = 250) => { let s = ''; for (let i = 0; s.length < n; i += 1) s += `w${(i * 7919 + seed * 104729) % 100000} `; return s.trim().slice(0, n).trim(); };
const ROW = seed => { let x = (seed * 2654435761) >>> 0; return Array.from({ length: 1000 }, () => { x = (Math.imul(x, 1664525) + 1013904223) >>> 0; return (x >>> 16) % 2 === 0 ? '.' : '#'; }).join(''); };
const kinds = r => r.findings.map(f => [f.tier, f.file, f.line, f.task, f.caseFile, f.fromLine, f.toLine]);

test('A_case_line_of_30_characters_or_more_is_an_exact_line_hit_alone_on_its_line_in_a_list_item_a_quote_and_a_diff_hunk', async () => {
  const L = longLine(1);
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['a short first line', L, 'a short last line'])] } });
  for (const [name, text] of [['alone', `${L}\n`], ['a list item', `- ${L}\n`], ['a quote', `> ${L}\n`], ['upper case with tabs', `${L.toUpperCase().replace(/ /g, '\t')}\n`]]) {
    const r = await scan(repoWith({ 'x.md': text }), material);
    assert.deepEqual(kinds(r), [['line', 'x.md', 1, '03', 'cases/h001.out', 2, 2]], name);
    assert.doesNotMatch(r.reason, new RegExp(L.slice(0, 20)), name);
  }
  const repo = repoWith();
  commitFiles(repo, { 'tmp.md': `${L}\n` }, 'add');
  commitFiles(repo, { 'tmp.md': null }, 'remove');
  const h = await scan(repo, material, 'HEAD~2..HEAD');
  assert.deepEqual(h.findings.map(f => f.file), ['git log -p HEAD~2..HEAD'], 'the `+` line that added it and the `-` line that removed it are one candidate per text: the first place it stands, found with its prefix removed');
});

test('A_long_row_without_spaces_quoted_inline_in_prose_or_in_a_table_cell_is_a_hit_by_its_token', async () => {
  const row = ROW(2);
  const { material } = materialOf({ '02': { cases: [caseWith('h001', ['a short first line', row])] } });
  for (const [name, text] of [['in prose', `the grid ${row} was printed in full, which is long\n`], ['in a table cell', `| case | grid | note |\n|---|---|---|\n| 1 | ${row} | ok |\n`]]) {
    const r = await scan(repoWith({ 'x.md': text }), material);
    assert.deepEqual(r.findings.map(f => [f.file, f.task, f.caseFile, f.fromLine]), [['x.md', '02', 'cases/h001.out', 2]], name);
  }
});

test('A_line_of_one_distinct_character_and_a_line_under_30_characters_are_no_exact_line_needles', async () => {
  const dots = '.'.repeat(1000);
  const lines = Array.from({ length: 2001 }, (_, i) => `short case line ${i + 100000}`);
  const { material } = materialOf({ '09': { cases: [caseWith('h001', [dots, ...lines])] } });
  assert.equal((await scan(repoWith({ 'x.md': `${dots}\n` }), material)).ok, true, 'a ruler of dots is in every program');
  assert.equal((await scan(repoWith({ 'x.md': `${lines[7]}\n` }), material)).ok, true, 'one short numeric line of a task that leaves its short lines to the other tiers is not found (documented)');
});

test('An_equal_hash_with_different_text_is_verified_away_and_never_reported', async () => {
  const L = longLine(3);
  const other = longLine(4);
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['a short first line', L])] } }, { hash: () => 42 });
  assert.equal(material.lineKeys.has(42), true, 'the forged hash gives every case line the key 42');
  const r = await scan(repoWith({ 'x.md': `${other}\n` }), material);
  assert.equal(r.ok, true, 'the haystack line has the same key as the case line and a different text: the verify step finds nothing');
  assert.equal((await scan(repoWith({ 'x.md': `${L}\n` }), material)).ok, false, 'the same hash with the same text is still found');
});

// ---- tier 3: window of three lines -----------------------------------------------------------------------------------------------------------


/** `n` distinct numeric lines of two 9-digit numbers (a deterministic generator): the shape of task 09's inputs. 3000 of them leave the substring tier (over 2,000 short lines). */
const numLines = (n, seed = 1) => { let x = seed * 2654435761 % 4294967296; const next = () => { x = (Math.imul(x, 1664525) + 1013904223) >>> 0; return String(100000000 + (x % 900000000)); }; return Array.from({ length: n }, () => `${next()} ${next()}`); };
const BIG = numLines(3000, 7);
const bigMaterial = (over = {}) => materialOf({ '09': { cases: [caseWith('h001', BIG)], ...over } }).material;
const block = (from, count = 3) => BIG.slice(from - 1, from - 1 + count); // lines `from` ... of cases/h001.out (the file's last line is the empty one after the final newline)

test('Three_consecutive_case_lines_pasted_into_a_code_fence_are_a_window_hit_naming_task_case_and_the_line_range', async () => {
  const material = bigMaterial();
  assert.equal(material.skipped.length, 1, 'the short lines left the substring tier: only the window tier can see three of them');
  const r = await scan(repoWith({ 'notes/q.md': `Here is the input:\n\n\`\`\`\n${block(40).join('\n')}\n\`\`\`\n\nand it failed.\n` }), material);
  assert.deepEqual(kinds(r), [['window', 'notes/q.md', 4, '09', 'cases/h001.out', 40, 42]]);
  assert.match(r.reason, /notes\/q\.md:4 \(window, task 09, cases\/h001\.out lines 40-42\)/);
  for (const line of block(40)) for (const number of line.split(' ')) assert.doesNotMatch(r.reason, new RegExp(number), 'the message holds no number of the case');
});

test('The_same_three_lines_as_a_markdown_table_in_a_commit_message_and_in_a_diff_hunk_are_hits', async () => {
  const material = bigMaterial();
  const rows = block(500).map(l => `| ${l.split(' ').join(' | ')} |`);
  const table = `| a | b |\n|---|---|\n${rows.join('\n')}\n`;
  assert.deepEqual(kinds(await scan(repoWith({ 'n.md': table }), material)).map(k => [k[0], k[5], k[6]]), [['window', 500, 502]], 'pipes, header and separator do not hide it');
  const repo = repoWith();
  commitFiles(repo, { 'tmp.md': `${block(900).join('\n')}\n` }, `the failing input was:\n\n${block(1200).join('\n')}\n`);
  commitFiles(repo, { 'tmp.md': null }, 'remove');
  const r = await scan(repo, material, 'HEAD~2..HEAD');
  assert.deepEqual(r.findings.map(f => [f.file, f.fromLine]).sort(), [['git log -p HEAD~2..HEAD', 900], ['git log messages of HEAD~2..HEAD', 1200]], 'the commit message, and the diff (the `+` hunk and the `-` hunk are one candidate per text)');
});

test('Two_lines_or_three_lines_out_of_order_or_from_two_places_are_clean_and_a_ruler_inside_a_window_does_not_count', async () => {
  const material = bigMaterial();
  for (const [name, text] of [['only two lines', `${block(40, 2).join('\n')}\n`], ['out of order', `${[BIG[41], BIG[39], BIG[40]].join('\n')}\n`], ['from two places', `${[BIG[39], BIG[40], BIG[900]].join('\n')}\n`]]) {
    assert.equal((await scan(repoWith({ 'x.md': text }), material)).ok, true, name);
  }
  const withRuler = [BIG[39], '000000000', BIG[40], BIG[41]];
  const r = await scan(repoWith({ 'x.md': `${withRuler.join('\n')}\n` }), material);
  assert.deepEqual(kinds(r).map(k => [k[0], k[5], k[6]]), [['window', 40, 42]], 'a line of one repeated character is skipped, not a break');
});

test('A_public_table_of_10000_random_numeric_pairs_and_a_one_character_row_of_1000_are_clean', async () => {
  const material = bigMaterial();
  const tsv = numLines(10000, 99).map(l => l.replace(' ', '\t')).join('\n');
  assert.equal((await scan(repoWith({ 'data.tsv': `${tsv}\n` }), material)).ok, true);
  assert.equal((await scan(repoWith({ 'ruler.txt': `${'.'.repeat(1000)}\n${'#'.repeat(1000)}\n${'0'.repeat(1000)}\n` }), material)).ok, true);
});

test('A_window_whose_key_matches_but_whose_lines_differ_is_verified_away', async () => {
  const material = materialOf({ '09': { cases: [caseWith('h001', BIG)] } }, { hash: () => 42 }).material;
  const other = numLines(3, 123456);
  assert.equal((await scan(repoWith({ 'x.md': `${other.join('\n')}\n` }), material)).ok, true, 'every window has the same key under the forged hash; no case window has those lines: nothing is reported');
  assert.deepEqual(kinds(await scan(repoWith({ 'x.md': `${block(40).join('\n')}\n` }), material)).map(k => [k[0], k[5], k[6]]), [['window', 40, 42]], 'the real lines are still found');
});

test('Windows_do_not_run_across_files_or_across_the_two_case_files_of_a_case', async () => {
  const material = bigMaterial();
  const files = { 'a.md': `${BIG[100]}\n${BIG[101]}\n`, 'b.md': `${BIG[102]}\n` };
  assert.equal((await scan(repoWith(files), material)).ok, true, 'two lines in one file and the third in another is no window');
});

// ---- tier 4: chunk of a long line ------------------------------------------------------------------------------------------------------------

import { buildChunkKeys, CHUNK } from '../lib/leak-chunks.mjs';
import { verifyChunks } from '../lib/leak-verify.mjs';
import { polyHash } from '../lib/leak-text.mjs';

test('A_64_character_chunk_of_a_long_case_line_pasted_alone_is_a_hit_and_so_is_any_paste_of_127_characters_or_more', async () => {
  const row = ROW(5);
  const { material } = materialOf({ '02': { cases: [caseWith('h001', ['a short first line', row, 'a short last line'])] } });
  const slice = (from, length) => row.slice(from, from + length);
  for (const [name, text] of [['an aligned 64-character chunk', `the middle of the grid: ${slice(320, 64)} and then it broke\n`], ['127 characters from any offset', `${slice(101, 127)}\n`], ['an aligned chunk in a table cell', `| grid | ${slice(640, 64)} |\n`]]) {
    const r = await scan(repoWith({ 'x.md': text }), material);
    assert.deepEqual(r.findings.map(f => [f.tier, f.file, f.task, f.caseFile, f.fromLine, f.toLine]), [['chunk', 'x.md', '02', 'cases/h001.out', 2, 2]], name);
  }
  assert.equal((await scan(repoWith({ 'x.md': `${slice(101, 64)}\n` }), material)).ok, true, 'a 64-character piece that straddles two aligned chunks is not found: documented');
});

test('A_chunk_of_a_long_line_with_spaces_is_found_and_a_chunk_of_one_repeated_character_or_a_short_line_is_not_a_needle', async () => {
  const spaced = longLine(9, 400);
  const dots = '.'.repeat(1000);
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['a short first line', spaced, dots])] } });
  const chunk = spaced.slice(128, 128 + 64);
  assert.deepEqual(kinds(await scan(repoWith({ 'x.md': `quoted: ${chunk} (cut)\n` }), material)).map(k => [k[0], k[5]]), [['chunk', 2]]);
  assert.equal((await scan(repoWith({ 'x.md': `${dots}\n${'.'.repeat(5000)}\n` }), material)).ok, true, 'a ruler is in every program');
  assert.equal(material.chunkKeys.size, Math.floor(spaced.length / CHUNK), 'only the long line gives chunks, and none of them is the ruler\'s');
  const short = materialOf({ '03': { cases: [caseWith('h001', [spaced.slice(0, 255)])] } }).material;
  assert.equal(short.chunkKeys.size, 0, 'a line under 256 characters gives no chunk');
});

test('A_chunk_candidate_whose_text_differs_from_the_case_chunk_it_hashes_to_is_verified_away', () => {
  const row = ROW(6);
  const { tasks } = materialOf({ '02': { cases: [caseWith('h001', [row])] } });
  const sources = tasks.map(t => ({ id: t.id, casesDir: t.heldout.casesDir }));
  assert.equal(buildChunkKeys(sources).has(polyHash(row, 0, 64)), true);
  const forged = [{ tier: 'chunk', file: 'x.md', line: 1, key: polyHash(row, 0, 64), text: row.slice(0, 63) + (row[63] === '.' ? '#' : '.'), raws: [] }];
  assert.deepEqual(verifyChunks(forged, sources), [], 'the key equals a case chunk\'s, the text does not');
  assert.equal(verifyChunks([{ ...forged[0], text: row.slice(0, 64) }], sources).length, 1, 'the same key with the same text is a finding');
});

test('A_long_random_token_and_a_two_symbol_row_that_is_not_a_case_row_are_clean', async () => {
  const { material } = materialOf({ '02': { cases: [caseWith('h001', [ROW(5)])] } });
  const random = Array.from({ length: 5000 }, (_, i) => ((i * 2654435761) % 97 < 40 ? '.' : '#')).join('');
  assert.equal((await scan(repoWith({ 'x.md': `${random}\n${numLines(200, 5).join('\n')}\n` }), material)).ok, true);
});

// ---- the pre-material baseline: text that was in the repository before a task's private material existed is a collision ------------------------------

import { readFileSync as readText } from 'node:fs';

const SHORT = '918273645501'; // a 12-character numeric case line, task-03 shape
const TABLE_ROW = `| ${SHORT} | pass | 41 |`;

/** A repository with a baseline commit holding `files`, then `later` committed after it. -> `{ repo, baseline }`. */
function repoWithBaseline(files, later = {}) {
  const repo = repoWith(files);
  const baseline = git(repo, 'rev-parse', 'HEAD').trim();
  if (Object.keys(later).length > 0) commitFiles(repo, later, 'later work');
  return { repo, baseline };
}
const material03 = baselines => materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SHORT, 'third public-looking line'])] } }, { baselines }).material;

test('A_short_case_line_in_a_table_row_that_was_in_the_repository_at_the_baseline_is_a_collision_and_is_counted_not_reported', async () => {
  const old = `# series coai 751\n\n| case | verdict | n |\n|---|---|---|\n${TABLE_ROW}\n`;
  const { repo, baseline } = repoWithBaseline({ 'research/policy-ab/series-coai-751.md': old }, { 'other.md': 'unrelated later work\n' });
  const r = await scan(repo, material03({ '03': baseline }));
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.exempt, { '03': 1 }, 'counted per task, without the text');
  assert.doesNotMatch(JSON.stringify(r), new RegExp(SHORT));
});

test('The_same_row_added_after_the_baseline_a_new_file_a_new_line_a_changed_line_or_a_copy_at_another_path_is_a_hit', async () => {
  const old = `| case | verdict |\n|---|---|\n${TABLE_ROW}\n`;
  const cases = {
    'a new file': { 'new.md': `${TABLE_ROW}\n` },
    'a new line in the old file': { 'research/policy-ab/series.md': `${old}${TABLE_ROW}\n` },
    'a changed line': { 'research/policy-ab/series.md': old.replace('| pass |', '| fail |') },
    'a copy at another path': { 'copy/series.md': old },
  };
  for (const [name, later] of Object.entries(cases)) {
    const { repo, baseline } = repoWithBaseline({ 'research/policy-ab/series.md': old }, later);
    const r = await scan(repo, material03({ '03': baseline }));
    assert.equal(r.ok, false, name);
  }
});

test('A_baseline_that_cannot_be_read_gives_no_exemption_and_a_history_finding_is_never_exempt', async () => {
  const old = `${TABLE_ROW}\n`;
  const { repo } = repoWithBaseline({ 'research/policy-ab/series.md': old });
  for (const baseline of ['0123456789abcdef0123456789abcdef01234567', 'not-a-commit', null]) {
    const r = await scan(repo, material03({ '03': baseline }));
    assert.equal(r.ok, false, `baseline ${baseline}: no exemption (the stricter check)`);
  }
  const first = git(repo, 'rev-parse', 'HEAD').trim();
  commitFiles(repo, { 'tmp.md': `${SHORT} was here\n` }, 'quote');
  commitFiles(repo, { 'tmp.md': null }, 'remove');
  const h = await scan(repo, material03({ '03': first }), `${first}..HEAD`);
  assert.ok(h.findings.every(f => f.file.startsWith('git log')), 'the old file is exempt; the history texts are never');
  assert.equal(h.ok, false);
});

test('A_window_is_exempt_only_when_every_one_of_its_lines_was_in_the_file_at_the_baseline', async () => {
  const mat = baselines => materialOf({ '09': { cases: [caseWith('h001', BIG)] } }, { baselines }).material;
  const three = block(40);
  const { repo, baseline } = repoWithBaseline({ 'old/data.md': `${three.join('\n')}\n` });
  assert.equal((await scan(repo, mat({ '09': baseline }))).ok, true, 'all three lines were there');
  const two = repoWithBaseline({ 'old/data.md': `${three.slice(0, 2).join('\n')}\n` }, { 'old/data.md': `${three.join('\n')}\n` });
  const r = await scan(two.repo, mat({ '09': two.baseline }));
  assert.equal(r.ok, false, 'one of the three lines is new since the baseline');
});

test('Every_task_of_the_pins_names_a_baseline_commit_made_before_its_held_out_folder_was_created', () => {
  for (const id of ['01', '02', '03', '09']) {
    const entry = EXPLORE.tasks[id];
    assert.match(entry.baselineCommit, /^[0-9a-f]{40}$/, id);
    assert.ok(Number.isFinite(Date.parse(entry.heldoutBorn)), `${id}: the birth time of the held-out folder is recorded`);
  }
  assert.ok(readText(new URL('../inputs/explore.json', import.meta.url), 'utf8').includes('"baselineCommit"'));
});

// ---- an incomplete scan is never clean (coai L1) --------------------------------------------------------------------------------------------

import { chmodSync, rmSync } from 'node:fs';
import path2 from 'node:path';

test('A_listed_file_that_is_gone_or_cannot_be_examined_is_unreadable_and_the_scan_fails_closed_instead_of_crashing_or_passing', async t => {
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SECRET])] } });
  const repo = repoWith({ 'gone.md': 'a tracked file\n', 'locked/inner.md': 'another tracked file\n', 'fine.md': 'fine\n' });
  rmSync(path2.join(repo, 'gone.md'));
  chmodSync(path2.join(repo, 'locked'), 0o000);
  t.after(() => chmodSync(path2.join(repo, 'locked'), 0o755));
  const r = await scan(repo, material);
  assert.equal(r.ok, false, 'a scan that skipped files is not clean');
  assert.equal(r.incomplete, true);
  assert.deepEqual([...r.unreadable].sort(), ['gone.md', 'locked/inner.md']);
  assert.match(r.reason, /the scan is incomplete: 2 listed file\(s\) could not be read \(gone\.md, locked\/inner\.md\)/);
  assert.deepEqual(r.findings ?? [], []);
});

test('A_finding_beside_an_unreadable_file_is_still_a_finding_and_both_are_reported', async t => {
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SECRET])] } });
  const repo = repoWith({ 'gone.md': 'x\n', 'leak.md': `the number ${SECRET}\n` });
  rmSync(path2.join(repo, 'gone.md'));
  const r = await scan(repo, material);
  assert.equal(r.ok, false);
  assert.deepEqual(r.findings.map(f => f.file), ['leak.md']);
  assert.deepEqual(r.unreadable, ['gone.md']);
  t.diagnostic(r.reason);
});

// ---- a baseline commit is verified before it exempts anything (coai L2) ------------------------------------------------------------------------

const row = `${TABLE_ROW}\n`;

test('A_baseline_commit_that_does_not_exist_is_not_an_ancestor_or_is_not_older_than_the_folder_gives_no_exemption_and_says_why', async () => {
  const { repo, baseline } = repoWithBaseline({ 'research/policy-ab/series.md': row }, { 'other.md': 'later\n' });
  const good = await scan(repo, material03({ '03': baseline }));
  assert.equal(good.ok, true, 'control: a sound baseline exempts');
  assert.deepEqual(good.baselineProblems, {});
  const absent = await scan(repo, material03({ '03': '0123456789abcdef0123456789abcdef01234567' }));
  assert.equal(absent.ok, false);
  assert.match(absent.baselineProblems['03'], /baselineCommit .* does not exist in this repository/);
  git(repo, 'checkout', '-q', '-b', 'side', baseline);
  const side = commitFiles(repo, { 'side.md': 'on a side branch\n' }, 'side');
  git(repo, 'checkout', '-q', '-');
  const sideFile = await scan(repo, materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SHORT, 'third public-looking line'])] } }, { baselines: { '03': side } }).material);
  assert.match(sideFile.baselineProblems['03'], /is not an ancestor of HEAD/);
  const young = await scan(repo, materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SHORT, 'third public-looking line'])] } }, { baselines: { '03': baseline }, born: { '03': '2000-01-01T00:00:00.000Z' } }).material);
  assert.equal(young.ok, false, 'a baseline committed after the folder was born is no baseline');
  assert.match(young.baselineProblems['03'], /committed at .* is not before the held-out folder was born \(2000-01-01T00:00:00\.000Z\)/);
  const notUtc = await scan(repo, materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SHORT, 'third public-looking line'])] } }, { baselines: { '03': baseline }, born: { '03': 'yesterday' } }).material);
  assert.match(notUtc.baselineProblems['03'], /heldoutBorn .* is not a UTC time/);
  const offset = await scan(repo, materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SHORT, 'third public-looking line'])] } }, { baselines: { '03': baseline }, born: { '03': '2999-01-01T00:00:00+01:00' } }).material);
  assert.match(offset.baselineProblems['03'], /heldoutBorn 2999-01-01T00:00:00\+01:00 is not a UTC time/, 'a valid time with an offset is still not UTC');
});

// ---- the working-file side of the baseline rule (coai L3, L10, L13, L9) -----------------------------------------------------------------------

import { applyBaseline, countWanted } from '../lib/leak-baseline.mjs';
import { readFileSync as readBytes } from 'node:fs';

const finding = (task, file, raws) => ({ tier: 'substring', file, line: 1, task, caseFile: 'cases/h001.out', fromLine: 1, toLine: 1, raws });

test('One_file_with_findings_for_two_tasks_is_read_once_even_when_their_baselines_are_different_commits', async () => {
  const { repo, baseline } = repoWithBaseline({ 'shared.md': 'a line both tasks care about 111\nanother line 222\n' });
  const second = commitFiles(repo, { 'other.md': 'later\n' }, 'second baseline');
  const reads = [];
  const readWorking = file => { reads.push(file); return readBytes(path2.join(repo, file), 'utf8').split('\n'); };
  const baselines = { '02': { commit: baseline, born: '2999-01-01T00:00:00.000Z' }, '03': { commit: second, born: '2999-01-01T00:00:00.000Z' } };
  const r = await applyBaseline({ repoRoot: repo, findings: [finding('02', 'shared.md', ['a line both tasks care about 111']), finding('03', 'shared.md', ['another line 222'])], baselines, readWorking });
  assert.deepEqual(reads, ['shared.md'], 'one read for the path, not one per task or per baseline');
  assert.deepEqual(r.exempt, { '02': 1, '03': 1 });
});

test('A_working_file_that_is_not_UTF_8_counts_like_the_baseline_blob_and_an_added_duplicate_of_such_a_line_is_not_exempt', async () => {
  const bytes = Buffer.concat([Buffer.from('caf'), Buffer.from([0xe9, 0xff]), Buffer.from(' 918273645501\nplain line\n')]);
  const { repo, baseline } = repoWithBaseline({ 'bin/odd.txt': bytes });
  const raw = bytes.toString('latin1').split('\n')[0];
  const base = { '03': { commit: baseline, born: '2999-01-01T00:00:00.000Z' } };
  const same = await applyBaseline({ repoRoot: repo, findings: [finding('03', 'bin/odd.txt', [raw])], baselines: base });
  assert.deepEqual(same.exempt, { '03': 1 }, 'the same bytes, read the same way, are the same line');
  commitFiles(repo, { 'bin/odd.txt': Buffer.concat([bytes, bytes.subarray(0, bytes.indexOf(10) + 1)]) }, 'duplicate the odd line');
  const more = await applyBaseline({ repoRoot: repo, findings: [finding('03', 'bin/odd.txt', [raw])], baselines: base });
  assert.deepEqual(more.exempt, {}, 'the working file now holds the line twice: one occurrence is new');
});

test('Only_the_findings_own_lines_are_counted_never_a_map_of_every_line_of_the_file', () => {
  const counts = countWanted(['a', 'b', 'a', 'c', 'b', 'd'], new Set(['a', 'd']));
  assert.deepEqual([...counts], [['a', 2], ['d', 1]], 'two wanted lines, however many lines the file has');
  assert.equal(countWanted([], new Set(['a'])).size, 0);
});

// ---- the births are UTC (the utc-timestamps rule; coai L5) -----------------------------------------------------------------------------------------

import { loadExplore } from '../lib/inputs.mjs';
import { writeFileSync as writeText } from 'node:fs';
import { tmpDir as tmp } from '../../minigit-bench/test/helpers.mjs';

test('The_pins_store_each_held_out_birth_as_a_UTC_time_and_the_loader_refuses_a_value_with_an_offset', () => {
  const instants = { '01': '2026-10-31T22:48:23.863Z', '02': '2026-11-01T10:46:45.036Z', '03': '2026-11-01T10:47:19.386Z', '09': '2026-11-01T10:48:42.434Z' };
  for (const [id, utc] of Object.entries(instants)) assert.equal(EXPLORE.tasks[id].heldoutBorn, utc, `${id}: the same instant as stat reported, written in UTC`);
  const dir = tmp('o-explore-utc');
  for (const [name, born] of [['offset', '2026-11-01T11:46:45.036+01:00'], ['no zone', '2026-11-01T10:46:45.036'], ['not a time', 'Z']]) {
    const copy = JSON.parse(JSON.stringify(EXPLORE));
    copy.tasks['02'].heldoutBorn = born;
    const file = path2.join(dir, `${name.replace(' ', '-')}.json`);
    writeText(file, JSON.stringify(copy));
    const r = loadExplore(file);
    assert.equal(r.ok, false, name);
    assert.match(r.reason, /heldoutBorn/, name);
  }
});

// ---- chunk candidates are deduplicated per file and verified per file (coai L11) -----------------------------------------------------------------

import { chunkTier } from '../lib/leak-chunks.mjs';
import { scanText } from '../lib/leak-scan.mjs';

test('A_file_with_thousands_of_repeated_matching_chunks_keeps_one_candidate_per_distinct_chunk_and_verifies_them_itself', () => {
  const row = ROW(11);
  const { material } = materialOf({ '02': { cases: [caseWith('h001', ['a short first line', row])] } });
  const lines = Array.from({ length: 5000 }, () => `again: ${row}`);
  const pending = chunkTier({ name: 'big.md', lines, norms: lines, diff: false, material });
  const distinct = Math.floor(row.length / 64);
  assert.ok(pending.length <= distinct + 1, `${pending.length} candidates for ${5000 * (row.length / 64 | 0)} matching windows: one per distinct chunk (at most ${distinct + 1})`);
  assert.ok(pending.every(p => typeof p.text === 'string' && p.text.length === 64), 'each keeps its 64 characters for the verify step');
  const raw = `${lines.join('\n')}\n`;
  const r = scanText({ name: 'big.md', raw, material });
  assert.ok(r.pending.length <= distinct + 2, `the whole file carries ${r.pending.length} candidates in all (the whole-row token and the row's chunks, once each)`);
  assert.equal(r.pending.filter(p => p.tier === 'chunk').length, 0, 'the file\'s chunk candidates are verified inside the file, not carried to the end of the scan');
  assert.deepEqual(r.findings.filter(f => f.tier === 'chunk').map(f => [f.file, f.task, f.caseFile, f.fromLine]).slice(0, 1), [['big.md', '02', 'cases/h001.out', 2]]);
});

// ---- the key index holds each key once (coai L12) -------------------------------------------------------------------------------------------------

import { KeyIndex } from '../lib/leak-text.mjs';

test('A_frozen_key_index_compacts_duplicate_keys_and_still_answers_every_lookup', () => {
  const index = new KeyIndex();
  for (const k of [3, 1, 3, 2, 1, 3, 9007199254740991, 9007199254740991]) index.add(k);
  assert.equal(index.size, 8, 'before freeze: every add');
  index.freeze();
  assert.equal(index.size, 4, 'after freeze: one entry per distinct key');
  for (const k of [1, 2, 3, 9007199254740991]) assert.equal(index.has(k), true, String(k));
  for (const k of [0, 4, 9007199254740990]) assert.equal(index.has(k), false, String(k));
  assert.equal(new KeyIndex().freeze().has(5), false, 'an empty index');
  const one = new KeyIndex();
  one.add(7);
  one.add(7);
  assert.equal(one.freeze().size, 1);
});

// ---- progress before each phase (coai L14) -------------------------------------------------------------------------------------------------------

test('The_scan_says_what_it_is_about_to_do_before_each_phase_in_counts_never_in_text', async () => {
  const { material } = materialOf({ '03': { cases: [caseWith('h001', ['first public-looking line', SECRET])] } });
  const repo = repoWith({ 'a.md': `a public file mentioning ${SECRET}\n`, 'b.md': 'another\n' });
  commitFiles(repo, { 'c.md': 'a commit\n' }, 'a message');
  const said = [];
  const r = await scanRepository({ repoRoot: repo, material, range: 'HEAD~1..HEAD', progress: l => said.push(l) });
  assert.equal(r.ok, false);
  assert.equal(said.length, 3, said.join(' | '));
  assert.match(said[0], /^scanning 3 files$/);
  assert.match(said[1], /^scanning the git history of HEAD~1\.\.HEAD \(2 texts\)$/);
  assert.match(said[2], /^verifying \d+ candidate\(s\)$/);
  assert.doesNotMatch(said.join('\n'), new RegExp(SECRET));
});
