// Where the fixed sentences stand in a spec (E1.S8): each sentence FORMAT requires or allows for the kind, searched in the places its row of 2.1 names, after whitespace is collapsed.
// A sentence found whole is a hit with its slot values; a sentence that is not found whole but whose first words stand somewhere is reported as altered, with the line.
import { collapse } from './conformance-blocks.mjs';
import { findAll, variantFor } from './conformance-sentences.mjs';

/** The paragraphs outside a requirement that a sentence may stand in alone: the preamble (F-REQ) and the first paragraph of section 6. */
function lonelyDomains(spec) {
  const firstHeading = spec.sections[0]?.no ?? spec.lastNo + 1;
  const six = spec.sections.find(section => section.number === 6);
  const sixEnd = spec.headings.find(heading => six !== undefined && heading.no > six.no)?.no ?? spec.lastNo + 1;
  const make = (paragraph, name) => ({ name, section: null, r: null, ...collapse(paragraph.lines) });
  return [
    ...spec.paragraphs.filter(p => !p.isTable && p.startNo > 1 && p.startNo < firstHeading).map(p => make(p, 'preamble')),
    ...spec.paragraphs.filter(p => !p.isTable && six !== undefined && p.startNo > six.no && p.startNo < sixEnd).slice(0, 1).map(p => make(p, 'first-paragraph')),
  ];
}

/** The texts to search: one per requirement, then the lonely paragraphs. */
export function domainsOf(spec) {
  return [...spec.requirements.map(r => ({ name: 'r', section: r.section, r, text: r.text, lineAt: r.lineAt })), ...lonelyDomains(spec)];
}

const whole = (domain, hit) => hit.index === 0 && hit.end === domain.text.length;

/** The hits of one compiled sentence in the domains its placement allows. */
function hitsOf(compiled, domains, mode) {
  const wanted = mode === 'preamble' ? 'preamble' : mode === 'first-paragraph' ? 'first-paragraph' : 'r';
  return domains.filter(domain => domain.name === wanted).flatMap(domain => findAll(compiled, domain.text)
    .filter(hit => wanted === 'r' || whole(domain, hit))
    .map(hit => ({ ...hit, domain, r: domain.r, section: domain.section, line: domain.lineAt(hit.index) })));
}

/** The line of the first place the fingerprint stands in the domains the mode allows, or null. */
function alteredAt(fingerprint, domains, mode) {
  const wanted = mode === 'preamble' ? 'preamble' : mode === 'first-paragraph' ? 'first-paragraph' : 'r';
  for (const domain of domains.filter(entry => entry.name === wanted)) {
    const at = fingerprint === '' ? -1 : domain.text.indexOf(fingerprint);
    if (at !== -1) return domain.lineAt(at);
  }
  return null;
}

/**
 * For every sentence of 2.1 that applies to `kind`: `{ entry, variant, partHits, hits, altered }`. `hits` are the whole matches of the first part; `partHits[i]` of part i; `altered` is the line
 * where the sentence's first words stand although it was not found whole (null when it was found or its words are nowhere).
 */
export function locateSentences({ spec, sentences, kind }) {
  const domains = domainsOf(spec);
  const found = new Map();
  for (const entry of sentences.values()) {
    if (!entry.kinds.has(kind)) continue;
    const variant = variantFor(entry, kind);
    if (variant === undefined) continue;
    const partHits = variant.parts.map(part => hitsOf(part, domains, entry.where.mode));
    const altered = partHits.some(hits => hits.length === 0) ? alteredAt(variant.parts.find((part, index) => partHits[index].length === 0).fingerprint, domains, entry.where.mode) : null;
    found.set(entry.id, { entry, variant, partHits, hits: partHits[0], altered });
  }
  return found;
}
