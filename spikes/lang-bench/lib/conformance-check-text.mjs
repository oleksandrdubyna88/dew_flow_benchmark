// Check 10 (E1.S8): no needle, `⏎` only in section 6, no reading block.
// The needles are E1.S1's: the push check's rules for a text an agent can see (plan ids, links, the names of the known tasks, the family's names), the stack words of inputs/stack-words-lang.json (public, named by the word) and the optional list of literal needles the
// spec author keeps outside git (`--needles`, the format of the leak-token file), both reused from lib/ and run over the spec and over the sample files.
import { collapsed } from './conformance-blocks.mjs';
import { aidNeedles, findAidHits, findKnownNeedles } from './push-needles.mjs';
import { findVisibleRules } from './push-visible.mjs';

const GLYPH = '⏎';
export const READING_BLOCK_KEYWORDS = 8;

/** ok(needles) for the text of a needles file (one literal per line, the leak-token reader's form), or a fail naming the line. */
export const literalNeedles = text => aidNeedles({ aids: [], tokensText: text });

/** The findings of E1.S1's rules and of the author's literals over one text: `line N: what`. A literal is named by its number, never by its words. */
export function needleFindings(path, text, { known, stackWords, literals }) {
  const rules = findVisibleRules(path, text, { known, assumeVisible: true, links: true });
  const stack = findKnownNeedles(path, text, stackWords);
  const hits = literals === undefined ? [] : findAidHits(path, text, literals, { mode: 'self' });
  return [...rules, ...stack, ...hits].sort((a, b) => a.line - b.line).map(hit => `${path} line ${hit.line}: ${hit.what}`);
}

function glyphFindings(spec) {
  return spec.lines.filter(line => line.text.includes(GLYPH) && spec.sectionAt(line.no) !== 6).map(line => `line ${line.no}: the glyph ${GLYPH} stands outside section 6`);
}

/** A paragraph of prose that names at least eight of the script's keywords in code spans is a copied summary of the grammar (the reading block of FORMAT v1). */
function readingBlockFindings(spec, keywords) {
  const known = new Set(keywords);
  return spec.paragraphs.filter(paragraph => !paragraph.isTable).flatMap(paragraph => {
    const spans = new Set([...collapsed(paragraph.lines.map(line => line.text).join(' ')).matchAll(/`([^`]+)`/g)].map(hit => hit[1]).filter(word => known.has(word)));
    return spans.size >= READING_BLOCK_KEYWORDS ? [`line ${paragraph.startNo}: a paragraph that names ${spans.size} keywords of the scenario script in code spans is a copied summary of the grammar; the spec names the runner and its GRAMMAR.md instead`] : [];
  });
}

/** Check 10 over the spec and the sample files. `ctx.texts` is `[{ path, text }]`: the spec first, then each sample file. */
export function textFindings(ctx) {
  const findings = ctx.texts.flatMap(entry => needleFindings(entry.path, entry.text, { known: ctx.known, stackWords: ctx.stackWords, literals: ctx.literals }));
  return [...findings, ...glyphFindings(ctx.spec), ...readingBlockFindings(ctx.spec, ctx.facts.keywords)];
}
