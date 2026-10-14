// Pure: which tool inputs reach outside the cell tree. Approximation: relative paths resolve against the tree root.
import path from 'node:path';

const win = path.win32;
const SHELL = new Set(['Bash', 'PowerShell']);
const FILE_TOOLS = new Set(['Read', 'Edit', 'Write', 'NotebookEdit']);
const SEARCH_TOOLS = new Set(['Glob', 'Grep']);
const TAIL = '[^\\s"\'`;|&<>()]*';
const PATTERNS = [
  new RegExp(`[A-Za-z]:[\\\\/]${TAIL}`, 'g'),
  new RegExp(`(?<![\\w.:/\\\\-])/[a-zA-Z]/${TAIL}`, 'g'),
  new RegExp(`(?:\\$env:USERPROFILE|%USERPROFILE%|\\$\\{?HOME\\}?|(?<![\\w.])~)(?=[\\\\/\\s"'\`;|&)]|$)${TAIL}`, 'gi'),
  new RegExp(`(?<![\\w.])(?:\\.\\.[\\\\/])+${TAIL}`, 'g'),
  new RegExp('(?<![\\w./\\\\])\\.\\.(?=\\s|$|["\'])', 'g'),
];
const ALLOW = [/^c:\\program files( \(x86\))?\\/i, /^c:\\windows\\/i, /\\appdata\\local\\npm-cache(\\|$)/i, /\\appdata\\roaming\\npm(\\|$)/i];

/** Pure: one raw path-ish string -> an absolute Windows path (home forms and git-bash drives expanded). */
export function resolvePath(raw, { treeRoot, home }) {
  let p = raw.replace(/^["']|["']$/g, '');
  p = p.replace(/^(\$env:USERPROFILE|%USERPROFILE%|\$\{?HOME\}?|~)(?=[\\/]|$)/i, home);
  p = p.replace(/^\/([a-zA-Z])(?=\/|$)/, (_, d) => `${d.toUpperCase()}:`);
  return win.resolve(treeRoot, p.replace(/\//g, '\\'));
}

const lower = p => win.normalize(p).toLowerCase();
export const isInside = (resolved, root) => lower(resolved) === lower(root) || lower(resolved).startsWith(`${lower(root)}\\`);
const allowed = resolved => ALLOW.some(re => re.test(resolved));

const QUOTED_DRIVE = /["']([A-Za-z]:[\\/][^"']*)["']/g;

export function pathsIn(text) {
  const quoted = [...text.matchAll(QUOTED_DRIVE)].map(m => m[1]);
  const rest = text.replace(QUOTED_DRIVE, ' ');
  return [...new Set([...quoted, ...PATTERNS.flatMap(re => [...rest.matchAll(re)].map(m => m[0]))])];
}

function candidatePaths(tool, input) {
  if (SHELL.has(tool)) return pathsIn(input?.command ?? '');
  if (FILE_TOOLS.has(tool)) return [input?.file_path ?? input?.notebook_path].filter(Boolean);
  if (SEARCH_TOOLS.has(tool)) return [input?.path, ...pathsIn(input?.pattern ?? '')].filter(Boolean);
  return [];
}

/** calls: [{agent, tool, input}] (audit summaries or raw tool_use inputs). */
export function findOutsideAccesses(calls, { treeRoot, home }) {
  const root = win.resolve(treeRoot);
  return calls.flatMap(c => candidatePaths(c.tool, c.input).flatMap(raw => {
    const resolved = resolvePath(raw, { treeRoot: root, home });
    return isInside(resolved, root) || allowed(resolved) ? [] : [{ agent: c.agent, tool: c.tool, path: raw.slice(0, 200), resolved }];
  }));
}
