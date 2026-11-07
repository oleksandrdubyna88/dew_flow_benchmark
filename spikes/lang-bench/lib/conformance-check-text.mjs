// Check 10 (E1.S8): no needle, `⏎` only in section 6, no reading block.
// The needles are E1.S1's: the push check's rules for a text an agent can see (plan ids, links, the names of the known tasks, the family's names), the stack words of inputs/stack-words-lang.json (public, named by the word) and the optional list of literal needles the
// spec author keeps outside git (`--needles`, the format of the leak-token file), both reused from lib/ and run over the spec and over the sample files.
import { blankSpans, collapsed } from './conformance-blocks.mjs';
import { aidNeedles } from './push-needles.mjs';
import { literalFindings, matchersOf, wordFindings } from './conformance-needles.mjs';
import { findVisibleRules } from './push-visible.mjs';

const GLYPH = '⏎';
export const READING_BLOCK_KEYWORDS = 8;
/** The shared rules read no known-task needles here: the conformance check matches them itself, without regard to case. */
const NO_KNOWN = Object.freeze({ matchers: [] });

/** ok(needles) for the text of a needles file (one literal per line, the leak-token reader's form), or a fail naming the line. */
export const literalNeedles = text => aidNeedles({ aids: [], tokensText: text });

/** A scheme with `://` or `www.` anywhere: a link of any kind (FORMAT 1.5), in any case, in the spec and in the samples. */
const SCHEME_LINK = /\b[A-Za-z][A-Za-z0-9+.-]*:\/\/|\bwww\./i;
const schemeLinks = text => text.split('\n').flatMap((line, index) => (SCHEME_LINK.test(line) ? [{ line: index + 1, what: 'link' }] : []));

/** The findings of E1.S1's rules, the known-task needles and the stack words, and of the author's literals over one text: `path line N: what`. A literal is named by its number, never by its words. */
export function needleFindings(path, text, { known, stackWords, literals }) {
  const rules = findVisibleRules(path, text, { known: NO_KNOWN, assumeVisible: true, links: true });
  const words = wordFindings(path, text, [...matchersOf(known), ...matchersOf(stackWords)]);
  const own = literals === undefined ? [] : literalFindings(path, text, literals.literals);
  return [...rules, ...schemeLinks(text), ...words, ...own].sort((a, b) => a.line - b.line).map(hit => `${path} line ${hit.line}: ${hit.what}`);
}

/** Markup links in prose, code spans left out: `[text](url)`, `[text][ref]`, a reference definition `[ref]: url`, an autolink `<scheme:…>`. */
const MARKUP_LINK = /(?<![A-Za-z0-9_)\]])\[[^\]]*\]\([^)]*\)|(?<![A-Za-z0-9_)\]])\[[^\]]+\]\[[^\]]*\]|^\s*\[[^\]]+\]:\s*\S|<[A-Za-z][A-Za-z0-9+.-]*:[^>\s]*>/;
/** The plan's identifiers (FORMAT 1.5), in any case: `E<n>`, `D<n>`, `T-L<n>` and `V<n>`; `S<n>`, `CP<n>`, `OP-<x>` and `F<n>` in upper case only: `s01` is a sample id, and `op-code`, `cp1252`, `f1` are words. */
const PLAN_ID = /\b(?:E\d+(?:\.S\d+[ab]?)?|D\d{1,3}|T-L\d+|V\d+)\b/gi;
const PLAN_STORY = /\b(?:S\d+[ab]?|CP\d+|OP-[A-Za-z0-9]+|F\d+)\b/g;

const markupHits = line => (MARKUP_LINK.test(blankSpans(line.text)) ? [{ line: line.no, what: 'link' }] : []);
const idHits = line => [...blankSpans(line.text).matchAll(PLAN_ID), ...blankSpans(line.text).matchAll(PLAN_STORY)].map(found => ({ line: line.no, what: `plan id ${found[0]} (a value of the task goes in a code span)` }));

/** Links and plan ids in the spec's prose (its unfenced lines, code spans left out): the shared rules read them in one case and one form only. */
const proseFindings = (path, spec) => spec.kept.flatMap(line => [...markupHits(line), ...idHits(line)]).map(hit => `${path} line ${hit.line}: ${hit.what}`);

function glyphFindings(spec) {
  return spec.lines.filter(line => line.text.includes(GLYPH) && spec.sectionAt(line.no) !== 6).map(line => `line ${line.no}: the glyph ${GLYPH} stands outside section 6`);
}

/** The words only the scenario script has: a reading block that copies the steps names at least three of them. */
const SCRIPT_ONLY = Object.freeze(['recv', 'wait-exit', 'connect', 'expect', 'capture', 'together']);
const SCRIPT_ONLY_MINIMUM = 3;
const firstWord = span => span.trim().split(/\s+/)[0];

/** The distinct keywords that open a code span of `text` (`connect <c> tcp` counts as `connect`). */
const keywordsIn = (text, known) => new Set([...text.matchAll(/`([^`\n]+)`/g)].map(hit => firstWord(hit[1])).filter(word => known.has(word)));
const isReadingBlock = words => words.size >= READING_BLOCK_KEYWORDS && SCRIPT_ONLY.filter(word => words.has(word)).length >= SCRIPT_ONLY_MINIMUM;

const readingMessage = (no, count) => `line ${no}: a paragraph that names ${count} keywords of the scenario script in code spans is a copied summary of the grammar; the spec names the runner and its GRAMMAR.md instead`;

/** A paragraph, a table or a fenced block outside section 6 that names at least eight of the script's keywords as the first word of a code span, three of them words only the script has, is a copied summary of the grammar (the reading block of FORMAT v1). */
function readingBlockFindings(spec, keywords) {
  const known = new Set(keywords);
  const units = [
    ...spec.paragraphs.map(paragraph => ({ no: paragraph.startNo, text: collapsed(paragraph.lines.map(line => line.text).join(' ')) })),
    ...spec.blocks.filter(block => spec.sectionAt(block.openNo) !== 6).map(block => ({ no: block.openNo, text: block.content })),
  ];
  return units.map(unit => ({ no: unit.no, words: keywordsIn(unit.text, known) })).filter(unit => isReadingBlock(unit.words)).map(unit => readingMessage(unit.no, unit.words.size));
}

/** Check 10 over the spec and the sample files. `ctx.texts` is `[{ path, text }]`: the spec first, then each sample file. */
export function textFindings(ctx) {
  const findings = ctx.texts.flatMap(entry => needleFindings(entry.path, entry.text, { known: ctx.known, stackWords: ctx.stackWords, literals: ctx.literals }));
  const prose = proseFindings(ctx.texts[0].path, ctx.spec);
  return [...new Set([...findings, ...prose])].concat(glyphFindings(ctx.spec), readingBlockFindings(ctx.spec, ctx.facts.keywords));
}
