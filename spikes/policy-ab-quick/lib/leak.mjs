// Pure: which tool inputs reach outside the cell tree. Approximation: relative paths resolve against the tree root.
// Two flavours: 'win32' (the default, every existing caller) and 'posix' (the Linux box: absolute /paths, ~, $HOME, ../ chains).
import path from 'node:path';

const win = path.win32;
const posix = path.posix;
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
const POSIX_PATTERNS = [
  new RegExp(`(?<![\\w.:/\\\\'"\x60}-])/${TAIL.replace('*', '+')}`, 'g'), // absolute: not inside a URL, a word or a regex
  new RegExp('(?<=^|\\s)/(?=\\s|$)', 'g'), // a lone `/`
  new RegExp(`(?:\\$\\{?HOME\\}?|(?<![\\w.])~)(?=[/\\s"'\x60;|&)]|$)${TAIL}`, 'g'),
  new RegExp(`(?<![\\w.])(?:\\.\\.[/])+${TAIL}`, 'g'),
  new RegExp('(?<![\\w./])\\.\\.(?=\\s|$|["\'])', 'g'),
];
const ALLOW = [/^c:\\program files( \(x86\))?\\/i, /^c:\\windows\\/i, /\\appdata\\local\\npm-cache(\\|$)/i, /\\appdata\\roaming\\npm(\\|$)/i];

/** Pure: one raw path-ish string -> an absolute Windows path (home forms and git-bash drives expanded). */
export function resolvePath(raw, { treeRoot, home }) {
  let p = raw.replace(/^["']|["']$/g, '');
  p = p.replace(/^(\$env:USERPROFILE|%USERPROFILE%|\$\{?HOME\}?|~)(?=[\\/]|$)/i, home);
  p = p.replace(/^\/([a-zA-Z])(?=\/|$)/, (_, d) => `${d.toUpperCase()}:`);
  return win.resolve(treeRoot, p.replace(/\//g, '\\'));
}

/** Pure: one raw path-ish string -> an absolute POSIX path (`~`, `$HOME`, `${HOME}` expanded, relative paths against the tree). */
export function resolvePosix(raw, { treeRoot, home }) {
  const p = raw.replace(/^["']|["']$/g, '').replace(/^(\$\{?HOME\}?|~)(?=\/|$)/, home);
  return posix.resolve(treeRoot, p);
}

const lower = p => win.normalize(p).toLowerCase();
export const isInside = (resolved, root) => lower(resolved) === lower(root) || lower(resolved).startsWith(`${lower(root)}\\`);
const isInsidePosix = (resolved, root) => resolved === root || resolved.startsWith(root === '/' ? '/' : `${root}/`);
const allowed = resolved => ALLOW.some(re => re.test(resolved));
/** An `allow` entry is a directory prefix (`/usr/`) or one exact path (`/dev/null`); a prefix ends at a path boundary. */
const allowedPosix = (resolved, allow) => allow.some(a => {
  const bare = a.length > 1 ? a.replace(/\/$/, '') : a;
  return resolved === bare || resolved.startsWith(`${bare}/`);
});

const QUOTED_DRIVE = /["']([A-Za-z]:[\\/][^"']*)["']/g;

export function pathsIn(text) {
  const quoted = [...text.matchAll(QUOTED_DRIVE)].map(m => m[1]);
  const rest = text.replace(QUOTED_DRIVE, ' ');
  return [...new Set([...quoted, ...PATTERNS.flatMap(re => [...rest.matchAll(re)].map(m => m[0]))])];
}

// a quoted absolute path (it may hold spaces): `/` then a name, never a lone `/` or a quoted substitution such as 's/a/b/'
const QUOTED_POSIX = /["'](\/[^"'\s/][^"']*)["']/g;

/** The POSIX counterpart of `pathsIn`: quoted absolute paths first (as `QUOTED_DRIVE` does), then the unquoted shapes. */
export function posixPathsIn(text) {
  const quoted = [...text.matchAll(QUOTED_POSIX)].map(m => m[1]);
  const rest = text.replace(QUOTED_POSIX, ' ');
  return [...new Set([...quoted, ...POSIX_PATTERNS.flatMap(re => [...rest.matchAll(re)].map(m => m[0]))])];
}

function candidatePaths(tool, input, extract) {
  if (SHELL.has(tool)) return extract(input?.command ?? '');
  if (FILE_TOOLS.has(tool)) return [input?.file_path ?? input?.notebook_path].filter(Boolean);
  if (SEARCH_TOOLS.has(tool)) return [input?.path, ...extract(input?.pattern ?? '')].filter(Boolean);
  return [];
}

const hit = (c, raw, resolved) => ({ agent: c.agent, tool: c.tool, path: raw.slice(0, 200), resolved });

function outsideWin32(calls, { treeRoot, home }) {
  const root = win.resolve(treeRoot);
  return calls.flatMap(c => candidatePaths(c.tool, c.input, pathsIn).flatMap(raw => {
    const resolved = resolvePath(raw, { treeRoot: root, home });
    return isInside(resolved, root) || allowed(resolved) ? [] : [hit(c, raw, resolved)];
  }));
}

function outsidePosix(calls, { treeRoot, home, allow }) {
  const root = posix.resolve(treeRoot);
  return calls.flatMap(c => candidatePaths(c.tool, c.input, posixPathsIn).flatMap(raw => {
    const resolved = resolvePosix(raw, { treeRoot: root, home });
    return isInsidePosix(resolved, root) || allowedPosix(resolved, allow) ? [] : [hit(c, raw, resolved)];
  }));
}

/**
 * calls: [{agent, tool, input}] (audit summaries or raw tool_use inputs). `flavour` is 'win32' (default) or 'posix'; `allow` is the
 * POSIX allowed prefixes, always passed by the caller (a default would hide a reach); the win32 flavour keeps its own list.
 */
export function findOutsideAccesses(calls, { treeRoot, home, flavour = 'win32', allow = [] }) {
  if (flavour === 'posix') return outsidePosix(calls, { treeRoot, home, allow });
  if (flavour === 'win32') return outsideWin32(calls, { treeRoot, home });
  throw new Error(`findOutsideAccesses: flavour '${flavour}' is not one of win32, posix`);
}
