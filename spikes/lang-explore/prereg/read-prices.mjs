#!/usr/bin/env node
// Reads the published prices (F3.S1 (b), PR 5; closed point 11: the day the pre-registration files are generated) and writes `inputs/prereg/prices-<yyyy-mm-dd>.json` in v1's shape (`spikes/minigit-bench/lib/prices.mjs`). The ONE step of
// the pre-registration that uses the network, and it makes exactly one request: a GET of the pricing page (or, with `--from-file`, reads a saved copy and uses no network). The parser never guesses: it finds the table by its EXACT header
// row (`PRICE_HEADER`, the header of the real page as read on 2026-11-20, PR 5b: no case folding, no "&" for "and") and reads a price only in the form `$<number> / MTok`, optionally followed by exactly ONE footnote mark `<sup>N</sup>`; the number
// of the cell IS the price and the mark only explains it, so a mark is never dropped: the price file's `notes` record, per footnote, the rates that carried it and the footnote's sentence from the page (a mark whose sentence is not on the page is a
// refusal). A row with a note in the model's name (`(limited availability)`, `(retired ...)`, `(for prompts over ...)`) is not read, and never breaks the read of the others. Unless it finds that header and a clean row for every model of the
// base file (the models the series uses) it refuses, naming what it found. `cliRates` keep only the entries observed on the pinned CLI version. `--check` is offline: the committed file must be valid and exactly the form this tool writes it in.
//   node spikes/lang-explore/prereg/read-prices.mjs [--explore <explore.json>] [--base <prices file>] [--from-file <saved page>] [--out <file>] [--check]   (the CLI version is explore.json's cli.version, the day the fetch date: nothing is typed)
// Exit 0 written (or checked equal) | 1 `--check` differs | 4 usage | 5 the page cannot be read or lacks a model.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadPrices } from '../../minigit-bench/lib/prices.mjs';
import { dayOf, systemClock } from '../lib/clock.mjs';
import { INPUTS, REPO } from '../lib/paths.mjs';
import { PREREG_DIR } from '../lib/stage-lists.mjs';
import { EXIT, committedText, runGenerator } from './generator.mjs';

export const PRICING_URL = 'https://platform.claude.com/docs/en/about-claude/pricing';
/** How long the one GET of the pricing page may take, the body included: a page that does not answer in this time is a refusal that names the timeout (`AbortSignal.timeout`). */
export const FETCH_TIMEOUT_MS = 30_000;
const BASE_FILE = path.join(REPO, 'spikes', 'minigit-bench', 'inputs', 'prices-2026-10-29.json');
/** The header row of the price table, exactly (the real page's, read on 2026-11-20): the first cell and the five prices. A table with any other header is not read. */
export const PRICE_HEADER = Object.freeze(['Model', 'Base input tokens', '5m cache writes', '1h cache writes', 'Cache hits and refreshes', 'Output tokens']);
/** The rates in the order of the header's price columns. */
const RATE_KEYS = Object.freeze(['input', 'cacheWrite5m', 'cacheWrite1h', 'cacheRead', 'output']);
/** A price cell: `$<number> / MTok` and, directly after it, at most ONE footnote mark. Anything else is not a price. */
const PRICE = /^\$(\d+(?:\.\d+)?) \/ MTok(?:<sup>(\d+)<\/sup>)?$/;
/** A footnote line of the page: `*<sup>N sentence</sup>*`. */
const FOOTNOTE = /^\*<sup>(\d+) (.+)<\/sup>\*$/;
const NOTED = 'its name carries a note, so the row is not read';
const SEPARATOR = /^:?-+:?$/;

/** The name the pricing page gives a model id: `claude-opus-5-5` is `Claude Opus 5.5`; a trailing date is dropped. */
export function displayNameOf(modelId) {
  const words = modelId.replace(/-\d{8}$/, '').split('-');
  const named = words.reduce((out, word) => (/^\d+$/.test(word) && /\d$/.test(out.at(-1) ?? '') ? [...out.slice(0, -1), `${out.at(-1)}.${word}`] : [...out, /^\d+$/.test(word) ? word : `${word[0].toUpperCase()}${word.slice(1)}`]), []);
  return named.join(' ');
}

const cells = line => line.trim().split('|').slice(1, -1).map(c => c.trim());
const isTableLine = line => line.trim().startsWith('|');
const plainName = cell => cell.replace(/\[([^\]]*)\]\([^)]*\)/g, '$1').replace(/[*_`]/g, '').trim();
const isSeparator = row => row.length > 0 && row.every(c => SEPARATOR.test(c));
const sameCells = (a, b) => a.length === b.length && a.every((c, i) => c === b[i]);

/** The tables of the page: one group of rows (each a list of cells) for each run of consecutive table lines. */
const tablesOf = text => text.split('\n').reduce((groups, line) => (isTableLine(line) ? [...groups.slice(0, -1), [...groups.at(-1), cells(line)]] : [...groups, []]), [[]]).filter(g => g.length > 0);

/** Is this group a price table: the exact header row, then the separator row? */
const isPriceTable = group => sameCells(group[0], PRICE_HEADER) && group[1] !== undefined && isSeparator(group[1]);

/** The sentence saying no table has the exact header, with the headers the page does have (the tables with a separator row). */
function noTableSentence(tables) {
  const found = tables.filter(g => g[1] !== undefined && isSeparator(g[1])).slice(0, 5).map(g => `| ${g[0].join(' | ')} |`);
  return `the pricing page has no table with the exact header row | ${PRICE_HEADER.join(' | ')} |: it holds ${found.length === 0 ? 'no table' : `the tables ${found.join(' and ')}`}`;
}

/** A price cell: `{ value, mark }` (`mark` the digits of the one footnote mark, or undefined), or null when the cell is not a price. */
function priceCell(cell) {
  const found = PRICE.exec(cell);
  return found ? { value: Number(found[1]), mark: found[2] } : null;
}

/** The footnote marks of a row's price cells: `{ <rate>: '<digits>' }` for the cells that carry one. */
const marksOfCells = priced => Object.fromEntries(RATE_KEYS.flatMap((key, i) => (priced[i].mark === undefined ? [] : [[key, priced[i].mark]])));

/** One table row: `{ name, rates, marks }` when every price cell is a price, `{ name, why }` naming the first cell that is not, or `{ name, why, noted }` for a row with a note in its name (left unread, and out of every comparison). */
function rowOf(row) {
  const [cell, ...rest] = row;
  const name = plainName(cell);
  if (name.includes('(')) return { name, why: NOTED, noted: true };
  if (row.length !== PRICE_HEADER.length) return { name, why: `${row.length} cells, not ${PRICE_HEADER.length}` };
  const priced = rest.map(priceCell);
  const bad = priced.findIndex(p => p === null);
  if (bad >= 0) return { name, why: `${PRICE_HEADER[bad + 1]} is '${rest[bad]}', not a price in the form $5 / MTok` };
  return { name, rates: Object.fromEntries(RATE_KEYS.map((key, i) => [key, priced[i].value])), marks: marksOfCells(priced) };
}

/** The names the page gives twice with different content (rates and marks, or the reason a row is unreadable): the page never chooses between them. A row with a note in its name is not compared. */
const conflictsOf = rows => [...new Set(rows.map(r => r.name))].filter(name => new Set(rows.filter(r => r.name === name).map(r => JSON.stringify([r.rates ?? r.why, r.marks]))).size > 1);

/** The footnote lines of the page: a list of `[digits, sentence]`. */
const footnoteLines = text => text.split('\n').map(line => FOOTNOTE.exec(line.trim())).filter(Boolean).map(f => [f[1], f[2]]);

/** The footnote numbers the page gives twice with different sentences. */
const footnoteConflicts = lines => [...new Set(lines.map(([n]) => n))].filter(n => new Set(lines.filter(([m]) => m === n).map(([, sentence]) => sentence)).size > 1);

/**
 * The page's price tables -> `ok({ prices: Map(display name -> { input, output, cacheRead, cacheWrite5m, cacheWrite1h }), marks: Map(display name -> { <rate>: '<footnote number>' }), footnotes: Map('<number>' -> sentence), unreadable: Map(name -> why) })`
 * | `fail(reason)`. A table is found by its EXACT header row (`PRICE_HEADER`); a price is read only in the form `$<number> / MTok` with at most one footnote mark; a row with a cell in another form is `unreadable`, and the build refuses it when the
 * series needs that model; a row with a note in its name is `unreadable` too (never read, never compared); a name given twice with different content, or a footnote number given twice with different sentences, is refused.
 */
export function parsePricingPage(text) {
  const tables = tablesOf(text);
  const priced = tables.filter(isPriceTable);
  if (priced.length === 0) return fail(noTableSentence(tables));
  const rows = priced.flatMap(group => group.slice(2)).map(rowOf);
  const conflicts = conflictsOf(rows.filter(r => !r.noted));
  if (conflicts.length > 0) return fail(`the pricing page gives ${conflicts.join(', ')} twice with different content: a price is never chosen between two`);
  const lines = footnoteLines(text);
  const twice = footnoteConflicts(lines);
  if (twice.length > 0) return fail(`the pricing page gives footnote ${twice.join(', ')} twice with different sentences: a footnote is never chosen between two`);
  return ok({ prices: new Map(rows.filter(r => r.rates).map(r => [r.name, r.rates])), marks: new Map(rows.filter(r => r.rates).map(r => [r.name, r.marks])), footnotes: new Map(lines), unreadable: new Map(rows.filter(r => r.why).map(r => [r.name, r.why])) });
}

/** The sentence for the footnote marks of a priced model whose footnote is not on the page, or null: a mark is never dropped, so a mark with no sentence is a refusal. */
function markProblem({ wanted, page }) {
  const lost = Object.entries(page.marks.get(wanted.name) ?? {}).filter(([, mark]) => !page.footnotes.has(mark));
  return lost.length === 0 ? null : `${wanted.modelId} (${wanted.name}): ${lost.map(([rate, mark]) => `its ${rate} carries footnote mark ${mark} and the page has no footnote ${mark}`).join(' and ')}`;
}

/** The sentence for a model the page does not price, naming what it found, or null. */
function modelProblem({ wanted, page }) {
  if (page.prices.has(wanted.name)) return markProblem({ wanted, page });
  if (page.unreadable.has(wanted.name)) return `${wanted.modelId} (${wanted.name}): its row cannot be read: ${page.unreadable.get(wanted.name)}`;
  return `${wanted.modelId} (${wanted.name}): no row (the rows it found: ${[...page.prices.keys(), ...page.unreadable.keys()].join(', ') || 'none'})`;
}

/** One model of the price file, from its row. */
const modelOf = ({ modelId, rates }) => ({ modelId, input: rates.input, output: rates.output, cacheRead: rates.cacheRead, cacheWrite5m: rates.cacheWrite5m, cacheWrite1h: rates.cacheWrite1h });

/** The notes of the footnote marks the series' models carry, one per footnote number: the rates that carried it and the page's sentence. */
function footnoteNotes({ wanted, page }) {
  const used = wanted.flatMap(w => Object.entries(page.marks.get(w.name) ?? {}).map(([rate, mark]) => ({ mark, use: `${w.modelId} ${rate}` })));
  const numbers = [...new Set(used.map(u => u.mark))].sort((a, b) => a - b);
  return numbers.map(n => `footnote ${n} (${used.filter(u => u.mark === n).map(u => u.use).join(', ')}): ${page.footnotes.get(n)}`);
}

/** The note of a generated price file. */
const noteOf = ({ day, cliVersion }) => `generated by spikes/lang-explore/prereg/read-prices.mjs on ${day} from ${PRICING_URL}; the models are those of the base file, each read from its own row of the page; a CLI rate is kept only where it was observed on the pinned CLI version ${cliVersion}`;

/**
 * The price file of `day` from the page (`parsePricingPage`'s answer): the base file's models, each priced by its own row (a model with no row, or with a row that cannot be read, is a refusal that names what the page holds) and the base file's
 * CLI rates for `cliVersion` only, and the `provenance` of the page it was read from when one is given (`{ url, fetchedAt, snapshot, snapshotSha256, cliVersion }`). -> `ok(object)` | `fail`.
 */
export function buildPrices({ base, page, day, cliVersion, provenance }) {
  const wanted = (base.models ?? []).map(m => ({ modelId: m.modelId, name: displayNameOf(m.modelId) }));
  const problems = wanted.map(w => modelProblem({ wanted: w, page })).filter(Boolean);
  if (problems.length > 0) return fail(`the pricing page cannot price the series' models: ${problems.join('; ')}: a price is never carried over from the older file or guessed`);
  const models = wanted.map(w => modelOf({ modelId: w.modelId, rates: page.prices.get(w.name) }));
  const cliRates = (base.cliRates ?? []).filter(c => c.cliVersion === cliVersion);
  const file = { source: PRICING_URL, readOn: day, models, cliRates, notes: [noteOf({ day, cliVersion }), ...footnoteNotes({ wanted, page })], ...(provenance === undefined ? {} : { provenance }) };
  const loaded = loadPrices(file);
  return loaded.ok ? ok(file) : fail(`the price file is refused: ${loaded.reason}`);
}

const sha256 = text => createHash('sha256').update(text).digest('hex');
/** The CLI version `explore.json` pins (its `cli.version`), the one source of the version a price file is read for: `ok(version)` | `fail` (exit 5) naming the file. */
function pinnedCliVersion(file) {
  try {
    const version = JSON.parse(readFileSync(file, 'utf8')).cli?.version;
    return /^\d+\.\d+\.\d+$/.test(version ?? '') ? ok(version) : fail(`${file} pins no CLI version x.y.z in cli.version`);
  } catch (e) {
    return fail(`${file} cannot be read: ${e.code ?? e.message}`);
  }
}

const readJson = text => { try { return ok(JSON.parse(text)); } catch { return fail('not JSON'); } };
const textOf = file => `${JSON.stringify(file, null, 2)}\n`;

/** A `--check` that does not hold: it exits 1, as a committed file that is not what the tool would write does. */
const differs = reason => fail(reason, { exit: EXIT.differs });

/** The committed price file: `ok({ file })` (valid) | a `--check` that differs, naming why. */
function committedPrices(out) {
  const committed = committedText(out);
  if (committed === null) return differs(`check: ${out} is absent`);
  const parsed = readJson(committed);
  if (!parsed.ok) return differs(`check: ${out} is not JSON`);
  const loaded = loadPrices(parsed.value);
  return loaded.ok ? ok(parsed.value) : differs(`check: ${out} is refused: ${loaded.reason}`);
}

/** The snapshot the price file names in its provenance, beside it: `ok(text)` | a `--check` that differs (absent, or not the page that was read). */
function snapshotText({ out, provenance }) {
  const file = path.join(path.dirname(out), provenance.snapshot);
  const text = committedText(file);
  if (text === null) return differs(`check: the snapshot ${file} is absent: --check regenerates from it and never fetches, so the page that was read must be committed beside the price file`);
  const now = sha256(text);
  return now === provenance.snapshotSha256 ? ok(text) : differs(`check: the snapshot ${file} hashes to ${now.slice(0, 12)}, the price file's provenance says ${String(provenance.snapshotSha256).slice(0, 12)}: the page was edited after it was read`);
}

/** The price file regenerated from the snapshot named in `provenance`: `ok(text)` | a refusal. `pinned` is the CLI version explore.json pins. */
function regenerated({ out, committed, baseFile, pinned }) {
  const provenance = committed.provenance;
  if (provenance.cliVersion !== pinned) return differs(`check: the price file was read for CLI ${provenance.cliVersion} and explore.json pins ${pinned}`);
  const snapshot = snapshotText({ out, provenance });
  const base = readBase(baseFile);
  const unread = [snapshot, base].find(r => !r.ok);
  if (unread) return unread;
  const built = builtText({ base: base.value, pageText: snapshot.value, day: committed.readOn, cliVersion: provenance.cliVersion, provenance });
  return built.ok ? built : differs(`check: the snapshot no longer yields a price file: ${built.reason}`);
}

/** `--check`: the committed price file against the file this tool regenerates from the snapshot beside it (never from the network): `ok(text)` of the regeneration | a refusal. */
function checkedText({ out, baseFile, pinned }) {
  const committed = committedPrices(out);
  if (!committed.ok) return committed;
  if (committed.value.provenance === undefined) return differs(`check: ${out} holds no provenance (the snapshot, url and time of the page it was read from): this tool did not write it`);
  return regenerated({ out, committed: committed.value, baseFile, pinned });
}

/** A saved page: `ok({ text, ext })` | `fail` naming the file when it cannot be read. */
function savedPage(file) {
  try {
    return ok({ text: readFileSync(file, 'utf8'), ext: path.extname(file) === '.html' ? 'html' : 'md' });
  } catch (e) {
    return fail(`the saved pricing page ${file} cannot be read: ${e.code ?? e.message}`);
  }
}

/** The sentence for a GET that threw: the timeout named, or the error's own message. */
const fetchFailure = (e, timeoutMs) => (e?.name === 'TimeoutError' ? `the pricing page did not answer within ${timeoutMs} ms: the read timed out (${PRICING_URL})` : `the pricing page cannot be read: ${e.message}`);

/** The extension a snapshot of the page gets: the page's own (`html` for a text/html answer), else `md`. */
const extensionOf = response => (String(response.headers?.get?.('content-type') ?? '').includes('html') ? 'html' : 'md');

/** One GET of the pricing page through `fetchFn` (the seam of a test), ended by `AbortSignal.timeout(timeoutMs)`. -> `ok({ text, ext })` | `fail`. */
async function fetchedPage({ fetchFn, timeoutMs }) {
  try {
    const response = await fetchFn(PRICING_URL, { headers: { accept: 'text/markdown, text/html' }, signal: AbortSignal.timeout(timeoutMs) });
    return response.ok ? ok({ text: await response.text(), ext: extensionOf(response) }) : fail(`the pricing page answered ${response.status}`);
  } catch (e) {
    return fail(fetchFailure(e, timeoutMs));
  }
}

/** The page: a saved file, or one GET of the pricing page. -> `ok({ text, ext })` | `fail`. */
const pageText = ({ fromFile, fetchFn, timeoutMs }) => (fromFile ? savedPage(fromFile) : fetchedPage({ fetchFn, timeoutMs }));

/** The base file (v1's price file, whose models the series uses): `ok(object)` | `fail`. */
function readBase(file) {
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')));
  } catch (e) {
    return fail(`${file} cannot be read: ${e.code ?? e.message}`);
  }
}

/** The text of the price file read from a page: `ok(text)` | `fail`. */
function builtText({ base, pageText: text, day, cliVersion, provenance }) {
  const page = parsePricingPage(text);
  const built = page.ok ? buildPrices({ base, page: page.value, day, cliVersion, provenance }) : page;
  return built.ok ? ok(textOf(built.value)) : built;
}

/** The usage error of a `--cli-version` that is not the one explore.json pins (a flag may only repeat the pin; it never types another), or null. */
const typedVersionProblem = ({ values, pinned }) => (values['cli-version'] === undefined || values['cli-version'] === pinned ? null : fail(`--cli-version ${values['cli-version']} is not the CLI version explore.json pins (${pinned}): the version is taken from the pin, never typed`, { exit: 4 }));

/** Where the page is saved: beside the price file, named after it (`prices-<day>.source.<ext>`). */
const snapshotPathOf = (out, ext) => `${out.replace(/\.json$/, '')}.source.${ext}`;

/** The price file read from `page` and the snapshot to save with it: `ok({ text, siblings })` | `fail`. `fetchedAt` is the clock's time; the day of the prices is its UTC day. */
function readFrom({ page, base, values, seams, pinned }) {
  const fetchedAt = (seams.clock ?? systemClock)().toISOString();
  const file = snapshotPathOf(values.out, page.ext);
  const provenance = { url: PRICING_URL, fetchedAt, snapshot: path.basename(file), snapshotSha256: sha256(page.text), cliVersion: pinned };
  const built = builtText({ base, pageText: page.text, day: fetchedAt.slice(0, 10), cliVersion: pinned, provenance });
  return built.ok ? ok({ text: built.value, siblings: [{ file, text: page.text }] }) : built;
}

async function build({ values, seams }) {
  const pinned = pinnedCliVersion(values.explore ?? path.join(INPUTS, 'explore.json'));
  if (!pinned.ok) return pinned;
  if (values.check) return checkedText({ out: values.out, baseFile: values.base ?? BASE_FILE, pinned: pinned.value });
  const typed = typedVersionProblem({ values, pinned: pinned.value });
  if (typed) return typed;
  const page = await pageText({ fromFile: values['from-file'], fetchFn: seams.fetchFn ?? fetch, timeoutMs: seams.timeoutMs ?? FETCH_TIMEOUT_MS });
  const base = readBase(values.base ?? BASE_FILE);
  const unread = [page, base].find(r => !r.ok);
  return unread ?? readFrom({ page: page.value, base: base.value, values, seams, pinned: pinned.value });
}

/** The day the prices were read: the clock's UTC day (the fetch date; `--check` takes it from the snapshot's provenance). */
const dayOfRun = ({ seams }) => dayOf(seams.clock ?? systemClock);

/** The command: -> the exit code. `seams` (`fetchFn`, `timeoutMs`, `clock`) are for tests: no test reaches the network. */
export function main(argv, say = line => process.stdout.write(`${line}\n`), seams = {}) {
  const extra = { 'cli-version': { type: 'string' }, explore: { type: 'string' }, base: { type: 'string' }, 'from-file': { type: 'string' } };
  const defaultOut = () => path.join(REPO, PREREG_DIR, `prices-${dayOfRun({ seams })}.json`);
  return runGenerator({ argv, extra, build: values => build({ values, seams }), defaultOut, say });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await main(process.argv.slice(2));
