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

export const isAgentVisible = file => AGENT_VISIBLE.some(prefix => file.startsWith(prefix));

const ruleHits = (lines, { what, re }) => lines.flatMap((text, i) => (re.test(text) ? [{ line: i + 1, col: text.search(re), what }] : []));

function familyHits(lines) {
  return lines.flatMap((text, i) => [...text.matchAll(FAMILY)]
    .filter(m => m[0] !== OWN_NAME)
    .map(m => ({ line: i + 1, col: m.index, what: `needle ${m[0]}` })))
    .filter((h, i, all) => all.findIndex(o => o.line === h.line && o.what === h.what) === i);
}

/**
 * The rules of one file's text (README text arrives with its attribution section emptied). Plan ids, links and the known-task
 * needles apply to the agent-visible folders and the README; the family's names apply to every file, because the whole tree except
 * the README's section is what a run base carries (consult D5), and `.gitmodules` is the one path excluded.
 */
export function findVisibleRules(file, text, { known }) {
  const lines = text.split('\n');
  const visible = isAgentVisible(file) || file === 'README.md';
  const rules = [
    ...(visible ? visibleRules(file, text, lines, known) : []),
    ...(file === GITMODULES ? [] : familyHits(lines)),
  ];
  return rules.sort((a, b) => a.line - b.line || a.col - b.col).map(({ line, what }) => ({ path: file, line, what }));
}

function visibleRules(file, text, lines, known) {
  return [
    ...ID_RULES.flatMap(rule => ruleHits(lines, rule)),
    ...(PIN_FILES.has(path.posix.basename(file)) ? [] : ruleHits(lines, { what: 'link', re: LINK })),
    ...findKnownNeedles(file, text, known).map(h => ({ line: h.line, col: lines[h.line - 1].length, what: h.what })),
  ];
}

const templatePattern = entry => new RegExp(`^${entry.split('<t>').map(part => part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('[^/]+')}$`);

/**
 * The files under `csharp/` and `node/` that the template set does not allow (consult C6a). `templateFiles` is the template's file
 * set with `<t>` for the task folder; absent, only `.gitkeep` is allowed (E1.S3 has not landed).
 */
export function findStackFolderFiles(paths, { templateFiles = [] } = {}) {
  const allowed = templateFiles.map(templatePattern);
  return paths
    .filter(p => p.startsWith('csharp/') || p.startsWith('node/'))
    .filter(p => path.posix.basename(p) !== '.gitkeep' && !allowed.some(re => re.test(p)))
    .map(p => ({ path: p, line: null, what: templateFiles.length === 0 ? 'stack folder holds a file before the template exists' : 'stack folder file outside the template set' }));
}
