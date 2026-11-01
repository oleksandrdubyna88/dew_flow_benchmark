// workspace-diff (E1.S1 brief item 3): per task, `csharp/<t>/` and `node/<t>/` hold the same files, byte for byte, except each stack's pin
// files (which are that stack's own) and the two marked sentences of STATEMENT.md. A finding is `{ path, what }`; the result is
// `{ ok, findings }`, findings sorted by path. Nothing here changes anything.
import { lstatSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';

export const CSHARP_PINS = Object.freeze(['global.json', 'nuget.config', 'Directory.Build.props', '*.csproj']);
export const NODE_PINS = Object.freeze(['package.json', 'package-lock.json', 'tsconfig.json', '.npmrc', 'eslint.config.mjs']);
export const MARKERS = Object.freeze(['<!-- stack-sentence -->', '<!-- framework-sentence -->']);
const STATEMENT = 'STATEMENT.md';

const isPin = (name, pins) => pins.some(pin => (pin.startsWith('*') ? name.endsWith(pin.slice(1)) : name === pin));

const readProblem = (rel, error) => ({ path: rel, what: `cannot be read (${error.code ?? error.message})` });

/**
 * `{ files: Map(relative path -> { abs, link }), problems }` for everything under `dir`, links included but not followed. ONLY a missing folder
 * (ENOENT) is empty; any other read error (EACCES, ENOTDIR, ...) is a problem, so an unreadable tree can never read as a clean diff.
 */
function filesUnder(dir, base = dir) {
  let entries;
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch (error) {
    return { files: new Map(), problems: error.code === 'ENOENT' ? [] : [readProblem(path.relative(base, dir).split(path.sep).join('/') || '.', error)] };
  }
  const parts = entries.map(entry => {
    const abs = path.join(dir, entry.name);
    const link = lstatSync(abs).isSymbolicLink();
    if (entry.isDirectory() && !link) return filesUnder(abs, base);
    return { files: new Map([[path.relative(base, abs).split(path.sep).join('/'), { abs, link }]]), problems: [] };
  });
  return { files: new Map(parts.flatMap(p => [...p.files])), problems: parts.flatMap(p => p.problems) };
}

const withoutPins = (files, pins) => new Map([...files].filter(([rel]) => !isPin(path.posix.basename(rel), pins)));

/** The statement's lines with each marked sentence (the line after a marker) replaced by one placeholder. */
const maskSentences = text => text.split('\n').map((line, i, lines) => (MARKERS.includes(lines[i - 1]) ? '<sentence>' : line));

/** The first line (1-based) where two statements differ outside a marked sentence, or 0 when they do not. */
function firstStatementDifference(a, b) {
  const [left, right] = [maskSentences(a), maskSentences(b)];
  const at = left.findIndex((line, i) => line !== right[i]);
  return left.length !== right.length && at === -1 ? Math.min(left.length, right.length) + 1 : at + 1;
}

function sameBytes(rel, a, b) {
  let x;
  let y;
  try { [x, y] = [readFileSync(a.abs), readFileSync(b.abs)]; } catch (error) { return readProblem(rel, error).what; }
  if (rel !== STATEMENT) return x.equals(y) ? null : 'bytes differ';
  const line = firstStatementDifference(x.toString('utf8'), y.toString('utf8'));
  return line === 0 ? null : `differs outside a marked sentence, line ${line}`;
}

const linkFindings = files => [...files].filter(([, f]) => f.link).map(([rel]) => ({ path: rel, what: 'symlink' }));

const missingSide = cs => (cs === undefined ? 'only in node' : 'only in csharp');

/** The finding for a path on both sides: different bytes, or null (links are reported on their own). */
function bothFinding(rel, cs, nd) {
  const what = cs.link || nd.link ? null : sameBytes(rel, cs, nd);
  return what === null ? null : { path: rel, what };
}

/** The finding for one path: present on one side only, or on both with different bytes; null when it agrees. */
function pathFinding(rel, cs, nd) {
  if (cs === undefined || nd === undefined) return { path: rel, what: missingSide(cs) };
  return bothFinding(rel, cs, nd);
}

/** `{ ok, findings }` for one task: the `csharp` folder against the `node` folder. */
export function diffTaskPair({ csharp, node }) {
  const [csAll, ndAll] = [filesUnder(csharp), filesUnder(node)];
  const [cs, nd] = [csAll.files, ndAll.files];
  const [csFiles, ndFiles] = [withoutPins(cs, CSHARP_PINS), withoutPins(nd, NODE_PINS)];
  const names = [...new Set([...csFiles.keys(), ...ndFiles.keys()])].sort();
  const findings = [...csAll.problems, ...ndAll.problems, ...linkFindings(cs), ...linkFindings(nd), ...names.map(rel => pathFinding(rel, csFiles.get(rel), ndFiles.get(rel))).filter(f => f !== null)];
  const seen = new Set();
  const unique = findings.filter(f => (seen.has(f.path) ? false : seen.add(f.path) && true)).sort((a, b) => a.path.localeCompare(b.path, 'en'));
  return { ok: unique.length === 0, findings: unique };
}

/** `{ names, problem }`: the task folders under `dir`; only a missing `dir` (ENOENT) is empty, any other error is the problem. */
function taskFolders(dir) {
  try { return { names: readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => e.name), problem: null }; } catch (error) {
    return { names: [], problem: error.code === 'ENOENT' ? null : readProblem(path.basename(dir), error) };
  }
}

/** `{ ok, findings }` for every task folder of `<root>/csharp` and `<root>/node`: a folder on one side only is a finding, paths are task-prefixed. */
export function diffAllTasks({ root }) {
  const [csDir, ndDir] = [taskFolders(path.join(root, 'csharp')), taskFolders(path.join(root, 'node'))];
  const [cs, nd] = [csDir.names, ndDir.names];
  const problems = [csDir.problem, ndDir.problem].filter(p => p !== null);
  const found = [...new Set([...cs, ...nd])].sort().flatMap(task => {
    if (!cs.includes(task) || !nd.includes(task)) return [{ path: `${cs.includes(task) ? 'csharp' : 'node'}/${task}`, what: `task folder only in ${cs.includes(task) ? 'csharp' : 'node'}` }];
    return diffTaskPair({ csharp: path.join(root, 'csharp', task), node: path.join(root, 'node', task) }).findings.map(f => ({ ...f, path: `${task}/${f.path}` }));
  });
  const findings = [...problems, ...found];
  return { ok: findings.length === 0, findings };
}
