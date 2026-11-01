// Where a thing may stand in the public task repository. Findings are `{ path, line, what }`; a rule on a whole path has `line: null`.
import path from 'node:path';
import { findKnownNeedles } from './push-needles.mjs';

export const AGENT_VISIBLE = Object.freeze(['csharp/', 'node/', 'tasks/', 'runner/', 'todo/tasks/']);
export const OWN_NAME = 'dew_flow_ai_language_comparison';
const GITMODULES = '.gitmodules';
// A pin file holds registry URLs by nature, so the link rule does not apply to it.
const PIN_FILES = new Set(['nuget.config', 'package.json', 'package-lock.json', '.npmrc', 'global.json']);

const ID_RULES = Object.freeze([
  { what: 'plan story id', re: /\bE[0-9]\.S[0-9]+[ab]?\b/ },
  { what: 'probe id', re: /\bT-L[0-9]+\b/ },
  { what: 'gate id', re: /\bG-[0-9]+\b/ },
  { what: 'decision id', re: /\bD[0-9]{1,2}\b/ },
]);
const LINK = /https?:\/\//;
const FAMILY = /dew_flow_[a-z_]+|PLAN_lang_comparison/g;
/** The `group` of a finding says which named line of the push check reports it. */
export const GROUP_FAMILY = 'family-names';
export const GROUP_VISIBLE = 'run-base-text';

export const isAgentVisible = file => AGENT_VISIBLE.some(prefix => file.startsWith(prefix));

const ruleHits = (lines, { what, re }, group) => lines.flatMap((text, i) => (re.test(text) ? [{ line: i + 1, col: text.search(re), what, group }] : []));

/** An Array.filter callback that keeps the first of each key, in linear time (a Set, not a findIndex scan of the findings so far). */
const uniqueBy = keyOf => { const seen = new Set(); return item => { const key = keyOf(item); return seen.has(key) ? false : seen.add(key) && true; }; };

function familyHits(lines) {
  return lines.flatMap((text, i) => [...text.matchAll(FAMILY)]
    .filter(m => m[0] !== OWN_NAME)
    .map(m => ({ line: i + 1, col: m.index, what: `needle ${m[0]}`, group: GROUP_FAMILY })))
    .filter(uniqueBy(h => `${h.line}\0${h.what}`));
}

/**
 * The rules of one file's text (README text arrives with its attribution section emptied). A run base is the whole tree minus that
 * section (D39), so links and the known-task needles apply to EVERY path; `.gitmodules` is exempt (its one URL is the conventions
 * repository's, checked on its own) and a pin file is exempt from links only (a registry URL is its nature). Plan ids stay with the
 * agent-visible folders and the README. The family's names apply to every file but `.gitmodules` (consult D5). `assumeVisible` and
 * `links: false` are for a PR or review body (`--text`): it is public like a README, and links are what a body is for.
 */
export function findVisibleRules(file, text, { known, assumeVisible = false, links = true }) {
  const lines = text.split('\n');
  const ids = assumeVisible || isAgentVisible(file) || file === 'README.md';
  const isGitmodules = file === GITMODULES;
  const rules = [
    ...(ids ? ID_RULES.flatMap(rule => ruleHits(lines, rule, GROUP_VISIBLE)) : []),
    ...(isGitmodules ? [] : textRules(file, text, lines, { known, links })),
    ...(isGitmodules ? [] : familyHits(lines)),
  ];
  return rules.sort((a, b) => a.line - b.line || a.col - b.col).map(({ line, what, group }) => ({ path: file, line, what, group }));
}

function textRules(file, text, lines, { known, links }) {
  const isPin = PIN_FILES.has(path.posix.basename(file));
  return [
    ...(links && !isPin ? ruleHits(lines, { what: 'link', re: LINK }, GROUP_VISIBLE) : []),
    ...findKnownNeedles(file, text, known).map(h => ({ line: h.line, col: lines[h.line - 1].length, what: h.what, group: GROUP_VISIBLE })),
  ];
}

const templatePattern = entry => new RegExp(`^${entry.split('<t>').map(part => part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('[^/]+')}$`);

const EMPTY_BLOB = 'e69de29bb2d1d6434b8b29ae775ad8c2e48c5391';
const STACK_ROOTS = Object.freeze(['csharp/', 'node/']);
const STACK_GITKEEPS = new Set(['csharp/.gitkeep', 'node/.gitkeep']);
const isStack = file => STACK_ROOTS.some(root => file.startsWith(root));

const stackRefusal = (file, templateFiles, allowed) => {
  if (path.posix.basename(file) === '.gitkeep') return STACK_GITKEEPS.has(file) || allowed.some(re => re.test(file)) ? null : 'stack folder holds a .gitkeep deeper than its root';
  if (allowed.some(re => re.test(file))) return null;
  return templateFiles.length === 0 ? 'stack folder holds a file before the template exists' : 'stack folder file outside the template set';
};

/**
 * What the stack folders and the `.gitkeep` placeholders may hold (consult C6a). `entries` is Map(path -> { oid, mode }). A `.gitkeep`
 * anywhere must be the empty blob (a placeholder is not a place for code), and under `csharp/` or `node/` only at the folder's root;
 * every other file there must be in the template's file set (`templateFiles`, `<t>` for the task folder), which is empty until E1.S3.
 */
export function findStackFolderFiles(entries, { templateFiles = [] } = {}) {
  const allowed = templateFiles.map(templatePattern);
  return [...entries].flatMap(([file, { oid }]) => {
    const filled = path.posix.basename(file) === '.gitkeep' && oid !== EMPTY_BLOB;
    const what = filled ? 'a .gitkeep that is not the empty blob' : (isStack(file) ? stackRefusal(file, templateFiles, allowed) : null);
    return what === null ? [] : [{ path: file, line: null, what }];
  });
}
