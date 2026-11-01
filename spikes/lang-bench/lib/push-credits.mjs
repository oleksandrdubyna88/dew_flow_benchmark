// The credit terms of the known tasks (`inputs/credits-lang.json`, decided by the owner on 2026-11-01) and the check that a branch
// carrying a known-task text also carries, in the README's attribution section, every piece those terms require: the entry's
// credit line, the per-file CC BY-NC-SA sentence for the files under that licence, and the upstream MIT notice.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { attributionText } from './attribution.mjs';

export const KNOWN_TASKS = Object.freeze(['01', '02', '05', '06', '09']);
const BASES = new Set(['adapted', 'own-words', 'idea-only']);
const PLACEHOLDER = /<[^<>\n]+>/;
const HTTPS = /^https:\/\/[^\s<>]+$/;
const KNOWN_SPEC = new RegExp(`^todo/tasks/(?:${KNOWN_TASKS.join('|')})-[^/]*\\.md$`);

const isText = v => typeof v === 'string' && v.trim() !== '' && !PLACEHOLDER.test(v);
const squash = s => s.replace(/\s+/g, ' ').trim();
const unquote = s => s.split('\n').map(l => l.replace(/^\s*>\s?/, '')).join('\n');

function problemsOfSources(e) {
  const out = [];
  if (!isText(e.source?.name)) out.push('source.name');
  if (!HTTPS.test(e.source?.url ?? '')) out.push('source.url');
  if (!isText(e.credit) || e.credit.includes('\n')) out.push('credit (one non-empty line, no placeholder)');
  if (!/^todo\/tasks\/[^/]+\.md$/.test(e.spec ?? '')) out.push('spec');
  return out;
}

function problemsOfLicences(e) {
  const out = [];
  const fl = e.fileLicence;
  if (fl !== null && !(isText(fl?.spdx) && HTTPS.test(fl?.readAt ?? '') && /^\d{4}-\d{2}-\d{2}$/.test(fl?.readOn ?? ''))) out.push('fileLicence (null, or spdx + readAt + readOn)');
  if (e.upstreamNotice !== null && !(isText(e.upstreamNotice?.spdx) && typeof e.upstreamNotice?.text === 'string' && e.upstreamNotice.text.trim() !== '')) out.push('upstreamNotice (null, or spdx + text)');
  return out;
}

function problemsOfBasis(e) {
  if (!BASES.has(e.basis)) return [`basis (one of ${[...BASES].join(', ')})`];
  const out = [];
  if (e.copiedText !== (e.basis === 'adapted' ? 'adapted' : 'none')) out.push(`copiedText (must be ${e.basis === 'adapted' ? 'adapted' : 'none'} for basis ${e.basis})`);
  if (e.basis === 'adapted' && e.fileLicence === null && e.upstreamNotice === null) out.push('fileLicence or upstreamNotice (an adaptation carries its source\'s licence)');
  if (e.basis === 'own-words' && !isText(e.nameRule)) out.push('nameRule');
  if (e.basis === 'idea-only' && e.licenceFound !== false) out.push('licenceFound (false for an idea-only task)');
  return out;
}

const problemsOf = e => [...problemsOfSources(e), ...problemsOfLicences(e), ...problemsOfBasis(e)];

/** ok(credits) when the text is a complete, placeholder-free record of all five known tasks; otherwise a fail naming the entry and field. */
export function parseCredits(text) {
  let file;
  try { file = JSON.parse(text); } catch { return fail('the credit terms file is not JSON'); }
  if (file?.schema !== 1 || file.decided?.by !== 'owner' || !isText(file.decided?.on) || !isText(file.repositoryLicence) || !Array.isArray(file.known)) {
    return fail('the credit terms file lacks schema 1, `decided` by the owner, `repositoryLicence` or `known`');
  }
  for (const task of KNOWN_TASKS) {
    const e = file.known.find(k => k?.task === task);
    if (e === undefined) return fail(`the credit terms hold no entry for task ${task}`);
    const problems = problemsOf(e);
    if (problems.length > 0) return fail(`task ${task}: invalid ${problems.join(', ')}`);
  }
  return ok(Object.freeze(file));
}

/** True when any of `paths` is a known task's spec (by the `todo/tasks/NN-*.md` shape, whatever the terms file says). */
export const carriesKnownText = paths => paths.some(p => KNOWN_SPEC.test(p));

export const knownSpecPaths = credits => credits.known.map(e => e.spec);

function pieceFindings(entry, section, ccFiles) {
  const out = [];
  const lines = unquote(section).split('\n').map(squash);
  if (!lines.includes(squash(entry.credit))) out.push(`credit line of task ${entry.task} is not in the attribution section`);
  if (entry.fileLicence && !lines.some(l => ccSentence(l, ccFiles))) {
    out.push(`the CC BY-NC-SA 4.0 exception sentence (naming ${ccFiles.join(', ')}) is not in the attribution section`);
  }
  if (entry.upstreamNotice && !squash(unquote(section)).includes(squash(entry.upstreamNotice.text))) out.push(`the MIT notice of task ${entry.task} is not in the attribution section`);
  return out;
}

const ccSentence = (line, files) => { const plain = line.replaceAll('`', ''); return plain.includes('CC BY-NC-SA 4.0') && files.every(f => plain.includes(f)); };

/**
 * Findings `{ path, line, what }` for a branch whose new `paths` may carry a known-task text. `credits` is parseCredits' answer;
 * `readmeText` is the README at the branch tip, or null when there is none. A branch with no known-task text needs nothing.
 */
export function checkCreditTerms({ credits, paths, readmeText }) {
  const carriedPaths = paths.filter(p => KNOWN_SPEC.test(p));
  if (carriedPaths.length === 0) return [];
  if (!credits.ok) return carriedPaths.map(p => ({ path: p, line: null, what: `known-task text without its credit terms (${credits.reason})` }));
  const recorded = new Set(knownSpecPaths(credits.value));
  const unrecorded = carriedPaths.filter(p => !recorded.has(p)).map(p => ({ path: p, line: null, what: 'known-task text without its credit terms (no entry for this path)' }));
  const carried = credits.value.known.filter(e => carriedPaths.includes(e.spec));
  if (carried.length === 0) return unrecorded;
  if (readmeText === null) return [...unrecorded, { path: 'README.md', line: null, what: 'the README is missing: the credit terms cannot be checked' }];
  const ccFiles = credits.value.known.filter(e => e.fileLicence).map(e => e.spec);
  const section = attributionText(readmeText);
  const missing = [...new Set(carried.flatMap(e => pieceFindings(e, section, ccFiles)))];
  return [...unrecorded, ...missing.map(what => ({ path: 'README.md', line: null, what }))];
}
