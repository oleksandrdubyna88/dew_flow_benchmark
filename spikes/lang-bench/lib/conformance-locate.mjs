// Where the fixed sentences stand in a spec (E1.S8): each sentence FORMAT requires or allows for the kind, searched in the places its row of 2.1 names, after whitespace is collapsed.
// A sentence found whole is a hit with its slot values; a sentence that is not found whole but whose first words stand somewhere is reported as altered, with the line.
import { collapse } from './conformance-blocks.mjs';
import { requirementAt } from './conformance-spec.mjs';
import { findAll, variantFor } from './conformance-sentences.mjs';

const lonely = (paragraph, name) => ({ name, section: null, r: null, ...collapse(paragraph.lines) });

/** The paragraphs before the first section (F-REQ stands in one of them). */
function preambleDomains(spec) {
  const firstHeading = spec.sections[0]?.no ?? spec.lastNo + 1;
  return spec.paragraphs.filter(p => !p.isTable && p.startNo > 1 && p.startNo < firstHeading).map(p => lonely(p, 'preamble'));
}

/** The first paragraph of section 6 (F-SAMPLES stands in it). */
function firstParagraphDomains(spec) {
  const six = spec.sections.find(section => section.number === 6);
  if (six === undefined) return [];
  const end = spec.headings.find(heading => heading.no > six.no)?.no ?? spec.lastNo + 1;
  return spec.paragraphs.filter(p => !p.isTable && p.startNo > six.no && p.startNo < end).slice(0, 1).map(p => lonely(p, 'first-paragraph'));
}

/** The paragraphs outside a requirement that a sentence may stand in alone: the preamble (F-REQ) and the first paragraph of section 6. */
const lonelyDomains = spec => [...preambleDomains(spec), ...firstParagraphDomains(spec)];

/** The texts to search: one per requirement, then the lonely paragraphs. */
export function domainsOf(spec) {
  const lonely = lonelyDomains(spec);
  return [...spec.requirements.map(r => ({ name: 'r', section: r.section, r, text: r.text, lineAt: r.lineAt })), ...lonely, ...strayDomains(spec, lonely)];
}

const whole = (domain, hit) => hit.index === 0 && hit.end === domain.text.length;

/** The hits of one compiled sentence in the domains its placement allows. */
function hitsOf(compiled, domains, mode) {
  return domains.filter(domain => domain.name === wantedName(mode)).flatMap(domain => findAll(compiled, domain.text)
    .filter(hit => domain.name === 'r' || domain.name === 'stray' || whole(domain, hit))
    .map(hit => ({ ...hit, domain, r: domain.r, section: domain.section, line: domain.lineAt(hit.index) })));
}

const wantedName = mode => (['preamble', 'first-paragraph', 'stray'].includes(mode) ? mode : 'r');

/** The line of the first place one of the fingerprints stands in the domains the mode allows, or null. */
function alteredAt(fingerprints, domains, mode) {
  for (const domain of domains.filter(entry => entry.name === wantedName(mode))) {
    const at = fingerprints.map(text => domain.text.indexOf(text)).find(index => index !== -1);
    if (at !== undefined) return domain.lineAt(at);
  }
  return null;
}

/** The paragraphs that lie in no requirement and are not the lonely paragraphs a sentence may stand in: a fixed sentence there is outside every span. */
function strayDomains(spec, lonely) {
  const used = new Set(lonely.map(domain => domain.text));
  return spec.paragraphs.filter(p => !p.isTable && p.startNo > 1 && requirementAt(spec, p.startNo) === undefined && spec.sectionAt(p.startNo) !== 8)
    .map(p => ({ name: 'stray', section: null, r: null, ...collapse(p.lines) })).filter(domain => !used.has(domain.text));
}

const missingPart = (variant, partHits) => variant.parts.find((_, index) => partHits[index].length === 0);

function alteredOf(variant, partHits, domains, mode) {
  const part = missingPart(variant, partHits);
  return part === undefined ? null : alteredAt(part.fingerprints, domains, mode);
}

function locateOne(entry, kind, domains) {
  const variant = variantFor(entry, kind) ?? entry.variants[0];
  const partHits = variant.parts.map(part => hitsOf(part, domains, entry.where.mode));
  const stray = variant.parts.flatMap(part => hitsOf(part, domains, 'stray'));
  return { entry, variant, partHits, hits: partHits[0], altered: alteredOf(variant, partHits, domains, entry.where.mode), stray, applies: entry.kinds.has(kind) };
}

/**
 * For every sentence of 2.1: `{ entry, variant, partHits, hits, altered, stray, applies }`. `applies` is whether the row lists the kind; a sentence the row does not list is located too, so that a spec
 * carrying it can be told. `hits` are the whole matches of the first part; `partHits[i]` of part i; `altered` is the line where the sentence's first words stand although it was not found whole (null
 * when it was found or its words are nowhere); `stray` are whole matches outside every requirement.
 */
export function locateSentences({ spec, sentences, kind }) {
  const domains = domainsOf(spec);
  return new Map([...sentences.values()].map(entry => [entry.id, locateOne(entry, kind, domains)]));
}
