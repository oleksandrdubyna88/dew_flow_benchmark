// Pure: which tool inputs reach outside the cell tree. Approximation: relative paths resolve against the tree root.
// Two flavours: 'win32' (the default, every existing caller) and 'posix' (the Linux box: absolute /paths, ~, $HOME, ../ chains).
import path from 'node:path';

const win = path.win32;
const posix = path.posix;
const SHELL = new Set(['Bash', 'PowerShell']);
const FILE_TOOLS = new Set(['Read', 'Edit', 'Write', 'NotebookEdit']);
const SEARCH_TOOLS = new Set(['Glob', 'Grep']);
const TAIL_CHAR = '[^\\s"\'`;|&<>()]';
const TAIL = `${TAIL_CHAR}*`;
const PATTERNS = [
  new RegExp(`[A-Za-z]:[\\\\/]${TAIL}`, 'g'),
  new RegExp(`(?<![\\w.:/\\\\-])/[a-zA-Z]/${TAIL}`, 'g'),
  new RegExp(`(?:\\$env:USERPROFILE|%USERPROFILE%|\\$\\{?HOME\\}?|(?<![\\w.])~)(?=[\\\\/\\s"'\`;|&)]|$)${TAIL}`, 'gi'),
  new RegExp(`(?<![\\w.])(?:\\.\\.[\\\\/])+${TAIL}`, 'g'),
  new RegExp('(?<![\\w./\\\\])\\.\\.(?=\\s|$|["\'])', 'g'),
];
const POSIX_PATTERNS = [
  new RegExp(`(?<![\\w.:/\\\\'"\x60}~-])/${TAIL_CHAR}+`, 'g'), // absolute: not inside a URL, a word, a regex or after the `~` of `~/x` (the home pattern's)
  new RegExp('(?<=^|\\s)/(?=\\s|$)', 'g'), // a lone `/`
  new RegExp(`(?:\\$\\{?HOME\\}?|(?<![\\w.])~)(?=[/\\s"'\x60;|&)]|$)${TAIL}`, 'g'),
  new RegExp(`(?<![\\w.])(?<![^.]/)(?:\\.\\.[/])+${TAIL}`, 'g'), // not the `../` inside an absolute path (`/a/b/../c`): `./../x` still counts
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

/** Whether a `..` of the absolute path climbs out of a component that is a link: `isLink(<the path up to the dir the dots leave>)`. The real parent of a link's target is not the link's lexical parent. */
function dotsAfterLink(abs, isLink) {
  const parts = abs.split('/').filter(p => p !== '' && p !== '.');
  return parts.some((part, i) => part === '..' && i > 0 && isLink(`/${parts.slice(0, i).join('/')}`));
}

/**
 * Pure: one raw path-ish string -> an absolute POSIX path (`~`, `$HOME`, `${HOME}` expanded, relative paths against the tree), resolved LEXICALLY (`a/../b` is `b`) - which is the real path only when no
 * component a `..` leaves is a link. With an `isLink(path)` that says one is, the path is returned UN-normalised (its `..` kept), so no caller can take it for a path inside the tree or one it allows.
 */
export function resolvePosix(raw, { treeRoot, home, isLink }) {
  const p = raw.replace(/^["']|["']$/g, '').replace(/^(\$\{?HOME\}?|~)(?=\/|$)/, home);
  const abs = posix.isAbsolute(p) ? p : `${treeRoot}/${p}`;
  return isLink && dotsAfterLink(abs, isLink) ? abs : posix.resolve(treeRoot, p);
}

const lower = p => win.normalize(p).toLowerCase();
export const isInside = (resolved, root) => lower(resolved) === lower(root) || lower(resolved).startsWith(`${lower(root)}\\`);
const isInsidePosix = (resolved, root) => resolved === root || resolved.startsWith(root === '/' ? '/' : `${root}/`);
const keepsDotDot = resolved => /(^|\/)\.\.(\/|$)/.test(resolved); // only an un-normalised path (a `..` after a link) still holds one
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

const ROOTED = /^(?:\/|\$\{?HOME\}?(?:\/|$)|~(?:\/|$))/;
/** The raw paths of a call, with those the caller already extracted (`call.paths`); a relative one is dropped when the call's directory is unknown (`call.cwd === null`). */
const rawPathsOf = c => [...candidatePaths(c.tool, c.input, posixPathsIn), ...(c.paths ?? [])].filter(raw => c.cwd !== null || ROOTED.test(raw));

function outsidePosix(calls, { treeRoot, home, allow, isLink }) {
  const root = posix.resolve(treeRoot);
  return calls.flatMap(c => rawPathsOf(c).flatMap(raw => {
    const resolved = resolvePosix(raw, { treeRoot: c.cwd ?? root, home, isLink }); // a relative path is taken from the call's own directory (`call.cwd`), else from the tree
    const known = !keepsDotDot(resolved);
    return known && (isInsidePosix(resolved, root) || allowedPosix(resolved, allow)) ? [] : [hit(c, raw, resolved)];
  }));
}

/**
 * calls: [{agent, tool, input}] (audit summaries or raw tool_use inputs); posix only: `cwd` = the absolute directory a relative path is taken from (default the tree; null = unknown, a relative path is then skipped) and `paths` = raw paths the caller
 * extracted itself, judged with the ones read from the input. `flavour` is 'win32' (default) or 'posix'; `allow` is the
 * POSIX allowed prefixes, always passed by the caller (a default would hide a reach); the win32 flavour keeps its own list. `isLink(path)` (posix only, optional) says whether a path is a symbolic link:
 * a `..` after one is not resolved lexically (see `resolvePosix`).
 */
export function findOutsideAccesses(calls, { treeRoot, home, flavour = 'win32', allow = [], isLink }) {
  if (flavour === 'posix') return outsidePosix(calls, { treeRoot, home, allow, isLink });
  if (flavour === 'win32') return outsideWin32(calls, { treeRoot, home });
  throw new Error(`findOutsideAccesses: flavour '${flavour}' is not one of win32, posix`);
}
