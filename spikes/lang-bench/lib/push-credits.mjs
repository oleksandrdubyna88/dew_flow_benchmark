// The credit terms of the known tasks (`inputs/credits-lang.json`, decided by the owner on 2026-11-01) and the check that a branch
// carrying a known-task text also carries, in the README's attribution section, every piece those terms require: the entry's
// credit line, the per-file CC BY-NC-SA sentence for the files under that licence, and the upstream MIT notice.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { attributionText } from './attribution.mjs';

export const KNOWN_TASKS = Object.freeze(['01', '02', '05', '06', '09']);
const BASES = new Set(['adapted', 'own-words', 'idea-only']);
const PLACEHOLDER = /<[^<>\n]+>/;
const HTTPS = /^https:\/\/[^\s<>]+$/;
const TASKS = KNOWN_TASKS.join('|');
const SPEC_PATH = new RegExp(`^todo/tasks/(${TASKS})-[^/]*\\.md$`);
const WORKSPACE_PATH = new RegExp(`^(?:tasks|csharp|node)/(${TASKS})-[^/]+/`);

/** `{ path, task, spec }` when `path` is a known task's text (its spec, or anything under its task folder in tasks/, csharp/ or node/); else null. */
export function knownTaskOf(path) {
  const spec = SPEC_PATH.exec(path);
  const workspace = spec === null ? WORKSPACE_PATH.exec(path) : null;
  const match = spec ?? workspace;
  return match === null ? null : { path, task: match[1], spec: spec !== null };
}

const isText = v => typeof v === 'string' && v.trim() !== '' && !PLACEHOLDER.test(v);
const squash = s => s.replace(/\s+/g, ' ').trim();
const unquote = s => s.split('\n').map(l => l.replace(/^\s*>\s?/, '')).join('\n');

/** The messages of the `[good, message]` rules that do not hold: a rule table, not a chain of ifs. */
const failed = rules => rules.filter(([good]) => !good).map(([, message]) => message);

const isDate = v => /^\d{4}-\d{2}-\d{2}$/.test(v ?? '');
const hasNotice = n => isText(n?.spdx) && typeof n?.text === 'string' && n.text.trim() !== '';
const hasFileLicence = l => isText(l?.spdx) && HTTPS.test(l?.readAt ?? '') && isDate(l?.readOn);

const problemsOfSources = e => failed([
  [isText(e.source?.name), 'source.name'],
  [HTTPS.test(e.source?.url ?? ''), 'source.url'],
  [isText(e.credit) && !e.credit.includes('\n'), 'credit (one non-empty line, no placeholder)'],
  [/^todo\/tasks\/[^/]+\.md$/.test(e.spec ?? ''), 'spec'],
]);

const problemsOfLicences = e => failed([
  [e.fileLicence === null || hasFileLicence(e.fileLicence), 'fileLicence (null, or spdx + readAt + readOn)'],
  [e.upstreamNotice === null || hasNotice(e.upstreamNotice), 'upstreamNotice (null, or spdx + text)'],
]);

const COPIED = { adapted: 'adapted', 'own-words': 'none', 'idea-only': 'none' };

/** The failed rules of an entry's basis, as `[ok, message]` pairs read in order (a table, not a chain of ifs). */
const basisRules = e => [
  [e.copiedText === COPIED[e.basis], `copiedText (must be ${COPIED[e.basis]} for basis ${e.basis})`],
  [e.basis !== 'adapted' || e.fileLicence !== null || e.upstreamNotice !== null, 'fileLicence or upstreamNotice (an adaptation carries its source\'s licence)'],
  [e.basis !== 'own-words' || isText(e.nameRule), 'nameRule'],
  [e.basis !== 'idea-only' || e.licenceFound === false, 'licenceFound (false for an idea-only task)'],
];

const problemsOfBasis = e => (BASES.has(e.basis) ? failed(basisRules(e)) : [`basis (one of ${[...BASES].join(', ')})`]);

const problemsOf = e => [...problemsOfSources(e), ...problemsOfLicences(e), ...problemsOfBasis(e)];

const HEADER_REASON = 'the credit terms file lacks schema 1, `decided` by the owner, `repositoryLicence` or `known`';
const headerOk = f => f?.schema === 1 && f.decided?.by === 'owner' && isText(f.decided?.on) && isText(f.repositoryLicence) && Array.isArray(f.known);

/** The reason the entry of `task` is unusable, or null. */
function entryReason(file, task) {
  const e = file.known.find(k => k?.task === task);
  if (e === undefined) return `the credit terms hold no entry for task ${task}`;
  const problems = problemsOf(e);
  return problems.length === 0 ? null : `task ${task}: invalid ${problems.join(', ')}`;
}

/** ok(credits) when the text is a complete, placeholder-free record of all five known tasks; otherwise a fail naming the entry and field. */
export function parseCredits(text) {
  const file = parseJson(text);
  if (!headerOk(file)) return fail(file === null ? 'the credit terms file is not JSON' : HEADER_REASON);
  const reason = KNOWN_TASKS.map(task => entryReason(file, task)).find(r => r !== null);
  return reason === undefined ? ok(Object.freeze(file)) : fail(reason);
}

function parseJson(text) {
  try { return JSON.parse(text); } catch { return null; }
}

/** True when any of `paths` is a known task's text (see knownTaskOf), whatever the terms file says. */
export const carriesKnownText = paths => paths.some(p => knownTaskOf(p) !== null);

export const knownSpecPaths = credits => credits.known.map(e => e.spec);

/** The sentences of the section, whitespace squashed (a sentence wrapped over lines is one), without Markdown quote marks or code ticks. */
const sentencesOf = section => squash(unquote(section).replaceAll('`', '')).split(/(?<=[.!?])\s+/);

const ccSentence = (sentence, files) => sentence.includes('CC BY-NC-SA 4.0') && files.every(f => sentence.includes(f));

const ccMissing = (entry, section, ccFiles) => (entry.fileLicence && !sentencesOf(section).some(s => ccSentence(s, ccFiles))
  ? [`the CC BY-NC-SA 4.0 exception sentence (naming ${ccFiles.join(', ')}) is not in the attribution section`] : []);

/** What the README's attribution section lacks for one entry: its credit as a substring (a list item or a wrapped line counts), the CC sentence, the notice. */
function pieceFindings(entry, section, ccFiles) {
  const squashed = squash(unquote(section));
  return failed([
    [squashed.includes(squash(entry.credit)), `credit line of task ${entry.task} is not in the attribution section`],
    [!entry.upstreamNotice || squashed.includes(squash(entry.upstreamNotice.text)), `the MIT notice of task ${entry.task} is not in the attribution section`],
  ]).concat(ccMissing(entry, section, ccFiles));
}

/**
 * The text a reader sees of the README's HTML comments: one left-to-right scan cuts each `<!--` up to its first `-->`, and an unclosed `<!--`
 * up to the end (CommonMark hides the rest of the document behind it). One scan, so a cut can never join two halves into a new `<!--`.
 */
function withoutComments(text) {
  const open = text.indexOf('<!--');
  if (open < 0) return text;
  const close = text.indexOf('-->', open + 4);
  return close < 0 ? text.slice(0, open) : text.slice(0, open) + withoutComments(text.slice(close + 3));
}
const finding = (path, what) => ({ path, line: null, what });

/** Findings for the README's attribution section against the tasks in play: what is missing, once each. */
function readmeFindings({ credits, tasks, readmeText }) {
  if (readmeText === null) return [finding('README.md', 'the README is missing: the credit terms cannot be checked')];
  const section = attributionText(withoutComments(readmeText));
  const ccFiles = credits.known.filter(e => e.fileLicence).map(e => e.spec);
  const missing = credits.known.filter(e => tasks.has(e.task)).flatMap(e => pieceFindings(e, section, ccFiles));
  return [...new Set(missing)].map(what => finding('README.md', what));
}

/**
 * Findings `{ path, line, what }` for the known-task texts among `paths` (every path ADDED anywhere in the range, so a text removed
 * again still counts: it was published). `credits` is parseCredits' answer; `readmeText` is the README at the branch tip, or null when
 * there is none; credits inside HTML comments do not count. A branch with no known-task text needs nothing.
 */
export function checkCreditTerms({ credits, paths, readmeText }) {
  const carried = paths.map(knownTaskOf).filter(c => c !== null);
  if (carried.length === 0) return [];
  if (!credits.ok) return carried.map(c => finding(c.path, `known-task text without its credit terms (${credits.reason})`));
  const recorded = new Set(credits.value.known.map(e => e.spec));
  const unrecorded = carried.filter(c => c.spec && !recorded.has(c.path)).map(c => finding(c.path, 'known-task text without its credit terms (no entry for this path)'));
  return [...unrecorded, ...readmeFindings({ credits: credits.value, tasks: new Set(carried.map(c => c.task)), readmeText })];
}
