// F3.S1 (b), PR 5b: the price read meets the real page. Its first real read (2026-11-20) was a refusal: the table header was written before the page had been seen, and some price cells end with a footnote mark.
// These tests read an EXCERPT of that page (`fixtures/pricing-page-2026-11-20.excerpt.md`: the price table, its footnote lines and one table with another header). No test reaches the network.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { PRICE_HEADER, buildPrices, main as pricesMain, parsePricingPage } from '../prereg/read-prices.mjs';
import { REPO } from '../lib/paths.mjs';

const PAGE = readFileSync(fileURLToPath(new URL('./fixtures/pricing-page-2026-11-20.excerpt.md', import.meta.url)), 'utf8');
const BASE_FILE = path.join(REPO, 'spikes', 'minigit-bench', 'inputs', 'prices-2026-10-29.json');
const BASE = JSON.parse(readFileSync(BASE_FILE, 'utf8'));
const REAL_HEADER = ['Model', 'Base input tokens', '5m cache writes', '1h cache writes', 'Cache hits and refreshes', 'Output tokens'];
const OLD_HEADER = ['Model', 'Base Input Tokens', '5m Cache Writes', '1h Cache Writes', 'Cache Hits & Refreshes', 'Output Tokens'];
const SERIES = ['claude-opus-5-5', 'claude-sonnet-5-5', 'claude-haiku-4-5-20251001', 'claude-fable-5-1', 'claude-opus-4-8'];
const FIGURES = ['input', 'output', 'cacheRead', 'cacheWrite5m', 'cacheWrite1h'];

const run = async (main, argv, ...rest) => { const lines = []; const code = await main(argv, line => lines.push(line), ...rest); return { code, lines }; };
const built = (page, extra = {}) => buildPrices({ base: BASE, page: parsePricingPage(page).value, day: '2026-11-20', cliVersion: '2.1.310', ...extra });
const cacheRow = 'Claude Opus 5.5';
/** The real page with Opus 5.5's cache-hit cell (`$0.20 / MTok<sup>2</sup>`) replaced. */
const withOpusCacheCell = cell => PAGE.replace('$0.20 / MTok<sup>2</sup>', cell);

test('The_real_header_of_the_pricing_page_is_found_and_the_fast_mode_table_with_another_header_is_not_taken_for_it', () => {
  assert.deepEqual([...PRICE_HEADER], REAL_HEADER);
  const page = parsePricingPage(PAGE);
  assert.equal(page.ok, true, page.reason);
  assert.deepEqual(page.value.prices.get(cacheRow), { input: 4, cacheWrite5m: 5, cacheWrite1h: 8, cacheRead: 0.2, output: 20 }, 'the row of the price table, not the $8 of the fast-mode table');
  assert.equal(page.value.prices.has('Claude Opus 5 / Claude Opus 4.8'), false, 'a row of the other table is not a price row');
});

test('Each_model_of_the_base_file_is_read_with_exactly_its_five_figures_from_the_real_page', () => {
  const result = built(PAGE);
  assert.equal(result.ok, true, result.reason);
  assert.deepEqual(result.value.models.map(m => m.modelId), SERIES);
  assert.deepEqual(result.value.models[0], { modelId: 'claude-opus-5-5', input: 4, output: 20, cacheRead: 0.2, cacheWrite5m: 5, cacheWrite1h: 8 });
  for (const id of SERIES) {
    const wanted = BASE.models.find(m => m.modelId === id);
    const got = result.value.models.find(m => m.modelId === id);
    assert.deepEqual(FIGURES.map(k => got[k]), FIGURES.map(k => wanted[k]), `${id}: the page's figures are the base file's`);
  }
});

test('A_footnote_mark_on_a_rate_is_recorded_with_the_footnote_sentence_it_points_to_and_the_price_is_the_cell', () => {
  const result = built(PAGE);
  const notes = result.value.notes.filter(n => n.startsWith('footnote '));
  assert.equal(notes.length, 2, notes.join('|'));
  assert.equal(notes[0], 'footnote 1 (claude-fable-5-1 cacheRead): Cache hits and refreshes on Claude Fable 5.2, Claude Mythos 5.2, Claude Fable 5.1, and Claude Mythos 5.1 are priced at 0.025x the base input price.');
  assert.equal(notes[1], 'footnote 2 (claude-opus-5-5 cacheRead, claude-sonnet-5-5 cacheRead): Cache hits and refreshes on Claude Opus 5.5 and Claude Sonnet 5.5 are priced at 0.05x the base input price.');
  assert.equal(result.value.models.find(m => m.modelId === 'claude-sonnet-5-5').cacheRead, 0.1, 'the number in the cell is the price; the mark only explains it');
  assert.equal(notes.some(n => n.startsWith('footnote 3')), false, 'a footnote of a model the base file does not use (footnote 3, Sonnet 5) is not recorded');
});

test('A_mark_whose_footnote_sentence_is_not_on_the_page_is_refused_naming_the_model_the_rate_and_the_mark', () => {
  const lines = PAGE.split('\n').filter(l => !l.startsWith('*<sup>2 '));
  const refused = built(lines.join('\n'));
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /claude-opus-5-5 \(Claude Opus 5\.5\): its cacheRead carries footnote mark 2 and the page has no footnote 2/);
  assert.match(refused.reason, /claude-sonnet-5-5 \(Claude Sonnet 5\.5\): its cacheRead carries footnote mark 2/);
});

test('A_price_cell_with_any_other_trailing_text_than_one_footnote_mark_is_refused_naming_the_cell', () => {
  for (const cell of ['$0.20 / MTok<sup>2</sup><sup>1</sup>', '$0.20 / MTok <sup>2</sup>', '$0.20 / MTok<sup>x</sup>', '$0.20 / MTok (cached)', '$0.20 / MTok<sup>2</sup>*', '$0.20 / MTok<sup></sup>']) {
    const refused = built(withOpusCacheCell(cell));
    assert.equal(refused.ok, false, cell);
    assert.ok(refused.reason.includes(`claude-opus-5-5 (Claude Opus 5.5): its row cannot be read: Cache hits and refreshes is '${cell}', not a price in the form $5 / MTok`), `${cell}: ${refused.reason}`);
  }
});

test('The_previous_header_is_now_refused_and_so_are_a_folded_case_and_an_ampersand_so_the_match_is_still_exact', () => {
  const header = cells => `| ${cells.join(' | ')} |`;
  const real = PAGE.split('\n').find(l => l.startsWith('| Model ') && l.includes('Base input tokens')).replace(/\s+/g, ' ');
  assert.equal(real, header(REAL_HEADER), 'the page\'s padded header row collapses to the real header');
  for (const [what, other] of [['the previous header', OLD_HEADER], ['the real header folded to lower case', REAL_HEADER.map(c => c.toLowerCase())], ['an ampersand for "and"', REAL_HEADER.map(c => c.replace(' and ', ' & '))]]) {
    const refused = parsePricingPage(PAGE.split('\n').map(l => (l.startsWith('| Model ') && l.includes('Base input tokens') ? header(other) : l)).join('\n'));
    assert.equal(refused.ok, false, what);
    assert.match(refused.reason, /^the pricing page has no table with the exact header row \| Model \| Base input tokens \| 5m cache writes \| 1h cache writes \| Cache hits and refreshes \| Output tokens \|: it holds the tables /, what);
  }
});

test('Rows_with_a_note_in_the_name_never_break_the_read_of_the_models_the_base_file_uses', () => {
  const twice = PAGE.split('\n').filter(l => l.includes('Claude Haiku 5.5') || l.includes('Claude Opus 4.1') || l.includes('Claude Mythos 5.2'));
  assert.equal(twice.length >= 4, true, 'the excerpt keeps the rows with a note: two Haiku 5.5 rows with different prices, a retired row, a limited-availability row');
  const page = parsePricingPage(PAGE);
  assert.equal(page.ok, true, page.reason);
  assert.equal(page.value.prices.has('Claude Haiku 5.5'), false, 'a row with a note in its name is left unread');
  assert.equal(built(PAGE).ok, true);
  const only = buildPrices({ base: { models: [{ modelId: 'claude-haiku-5-5' }] }, page: page.value, day: 'x', cliVersion: '2.1.310' });
  assert.match(only.reason, /claude-haiku-5-5 \(Claude Haiku 5\.5\): the page also gives the row 'Claude Haiku 5\.5 \(for prompts up to 100,000 tokens\)' and 'Claude Haiku 5\.5 \(for prompts over 100,000 tokens\)': the pricing is tiered/, 'a base model that has only noted rows is a refusal (tiered), and the noted rows are named');
  const other = buildPrices({ base: { models: [{ modelId: 'claude-haiku-5' }] }, page: page.value, day: 'x', cliVersion: '2.1.310' });
  assert.match(other.reason, /claude-haiku-5 \(Claude Haiku 5\): no row \(the rows it found: .*Claude Haiku 5\.5 \(for prompts up to 100,000 tokens\)/, 'a base model with no row at all is a refusal naming the rows found');
  assert.equal(page.value.unreadable.get('Claude Mythos 5.2 (limited availability)'), 'its name carries a note, so the row is not read');
});

test('The_check_from_the_saved_snapshot_reproduces_the_bytes_of_the_price_file_and_records_the_footnotes', async () => {
  const dir = tmpDir('prices-real');
  const explore = path.join(dir, 'explore.json');
  writeFileSync(explore, '{"cli":{"version":"2.1.310"}}\n');
  const saved = path.join(dir, 'page.md');
  writeFileSync(saved, PAGE);
  const never = async () => { throw new Error('the network was used'); };
  const out = path.join(dir, 'prices-2026-11-20.json');
  const args = ['--explore', explore, '--base', BASE_FILE, '--from-file', saved, '--out'];
  const clock = () => new Date('2026-11-20T10:00:00Z');
  const wrote = await run(pricesMain, [...args, out], { fetchFn: never, clock });
  assert.equal(wrote.code, 0, wrote.lines.join('|'));
  const checked = await run(pricesMain, ['--check', '--explore', explore, '--base', BASE_FILE, '--out', out], { fetchFn: never });
  assert.equal(checked.code, 0, checked.lines.join('|'));
  mkdirSync(path.join(dir, 'again'));
  const again = path.join(dir, 'again', 'prices-2026-11-20.json');
  const second = await run(pricesMain, [...args, again], { fetchFn: never, clock });
  assert.equal(second.code, 0, second.lines.join('|'));
  assert.equal(readFileSync(again, 'utf8'), readFileSync(out, 'utf8'), 'the same page gives the same bytes');
  assert.equal(readFileSync(path.join(dir, 'prices-2026-11-20.source.md'), 'utf8'), PAGE, 'the snapshot is the page as read');
  const file = JSON.parse(readFileSync(out, 'utf8'));
  assert.equal(file.notes.filter(n => n.startsWith('footnote ')).length, 2);
  writeFileSync(out, readFileSync(out, 'utf8').replace('0.05x', '0.06x'));
  assert.equal((await run(pricesMain, ['--check', '--explore', explore, '--base', BASE_FILE, '--out', out], { fetchFn: never })).code, 1, 'an edited footnote note is not what the tool writes');
});

// ---- coai plan round, PR 5b: a tier row of a base model (N1) and the exact footnote syntax (N2) -------------------------------------------------------------------------------------------------------------

const NOTES = [
  'generated by spikes/lang-explore/prereg/read-prices.mjs on 2026-11-20 from https://platform.claude.com/docs/en/about-claude/pricing; the models are those of the base file, each read from its own row of the page; a CLI rate is kept only where it was observed on the pinned CLI version 2.1.310',
  'footnote 1 (claude-fable-5-1 cacheRead): Cache hits and refreshes on Claude Fable 5.2, Claude Mythos 5.2, Claude Fable 5.1, and Claude Mythos 5.1 are priced at 0.05x the base input price.'.replace('0.05x', '0.025x'),
  'footnote 2 (claude-opus-5-5 cacheRead, claude-sonnet-5-5 cacheRead): Cache hits and refreshes on Claude Opus 5.5 and Claude Sonnet 5.5 are priced at 0.05x the base input price.',
];
const TIER = '| Claude Opus 5.5 (for prompts over 100,000 tokens) | $8 / MTok | $10 / MTok | $16 / MTok | $0.40 / MTok | $40 / MTok |';
const withRow = row => PAGE.replace('| Claude Opus 5 ', `${row}\n| Claude Opus 5 `);
/** The page's footnote 2 line. */
const FOOTNOTE_2 = PAGE.split('\n').find(l => l.startsWith('*<sup>2 '));

test('A_noted_row_that_is_a_tier_of_a_base_model_is_refused_naming_the_model_and_the_row_because_the_pricing_is_ambiguous', () => {
  const page = parsePricingPage(withRow(TIER));
  assert.equal(page.ok, true, 'the page itself is readable');
  const refused = buildPrices({ base: BASE, page: page.value, day: 'x', cliVersion: '2.1.310' });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /claude-opus-5-5 \(Claude Opus 5\.5\): the page also gives the row 'Claude Opus 5\.5 \(for prompts over 100,000 tokens\)': the pricing is tiered, so no price is chosen/);
  assert.equal(refused.reason.includes('claude-sonnet-5-5'), false, 'only the model with a tier row is named');
});

test('A_noted_row_of_a_model_the_base_file_does_not_use_still_passes_beside_the_clean_row_of_a_base_model', () => {
  const other = withRow('| Claude Opus 5 (for prompts over 100,000 tokens) | $9 / MTok | $10 / MTok | $16 / MTok | $0.40 / MTok | $40 / MTok |');
  assert.equal(built(other).ok, true);
  assert.equal(built(PAGE).ok, true, 'the real page: noted rows of Haiku 5.5, Mythos and the retired models');
});

test('The_notes_of_the_price_file_from_the_real_page_excerpt_are_exactly_these_lines', () => {
  assert.deepEqual(built(PAGE).value.notes, NOTES);
});

test('A_footnote_line_is_one_line_star_sup_number_one_space_sentence_and_nothing_that_is_not_that_is_taken_for_one', () => {
  const extra = PAGE.replace('## Fast mode', '1. A numbered list item.\n2. Cache hits and refreshes are another list item.\n* **MTok:** Million tokens.\n<sup>3</sup> alone\n\n## Fast mode');
  assert.equal(parsePricingPage(extra).ok, true, parsePricingPage(extra).reason);
  assert.deepEqual(built(extra).value.notes, NOTES, 'a list item, a bullet and a bare sup are not footnotes; the unnumbered "All other models" line is not recorded (never guessed)');
  const twice = parsePricingPage(`${PAGE}\n*<sup>2 Another sentence for the same number.</sup>*\n`);
  assert.equal(twice.ok, false);
  assert.match(twice.reason, /^the pricing page gives footnote 2 twice with different sentences: a footnote is never chosen between two$/);
  const same = parsePricingPage(`${PAGE}\n${FOOTNOTE_2}\n`);
  assert.equal(same.ok, true, 'the same sentence twice is one footnote');
});

test('A_footnote_that_spans_lines_or_has_text_after_its_closing_is_refused_naming_the_line', () => {
  const spans = FOOTNOTE_2.replace(' are priced', '\nare priced');
  const broken = parsePricingPage(PAGE.replace(FOOTNOTE_2, spans));
  assert.equal(broken.ok, false);
  assert.match(broken.reason, /^the pricing page has a footnote line that is not `\*<sup>N sentence<\/sup>\*` on one line: \*<sup>2 Cache hits and refreshes on Claude Opus 5\.5 and Claude Sonnet 5\.5$/);
  const trailing = parsePricingPage(PAGE.replace(FOOTNOTE_2, `${FOOTNOTE_2} more`));
  assert.equal(trailing.ok, false, 'text after the closing');
  const nospace = parsePricingPage(PAGE.replace(FOOTNOTE_2, FOOTNOTE_2.replace('2 Cache', '2Cache')));
  assert.equal(nospace.ok, false, 'no space after the number');
});
