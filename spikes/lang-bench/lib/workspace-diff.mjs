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

/** Map(relative path -> { abs, link }) of every file under `dir`, links included but not followed; a missing folder is empty. */
function filesUnder(dir, base = dir) {
  let entries;
  try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return new Map(); }
  return new Map(entries.flatMap(entry => {
    const abs = path.join(dir, entry.name);
    const link = lstatSync(abs).isSymbolicLink();
    return entry.isDirectory() && !link ? [...filesUnder(abs, base)] : [[path.relative(base, abs).split(path.sep).join('/'), { abs, link }]];
  }));
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
  const [x, y] = [readFileSync(a.abs), readFileSync(b.abs)];
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
  const [cs, nd] = [filesUnder(csharp), filesUnder(node)];
  const [csFiles, ndFiles] = [withoutPins(cs, CSHARP_PINS), withoutPins(nd, NODE_PINS)];
  const names = [...new Set([...csFiles.keys(), ...ndFiles.keys()])].sort();
  const findings = [...linkFindings(cs), ...linkFindings(nd), ...names.map(rel => pathFinding(rel, csFiles.get(rel), ndFiles.get(rel))).filter(f => f !== null)];
  const unique = findings.filter((f, i) => findings.findIndex(o => o.path === f.path) === i).sort((a, b) => a.path.localeCompare(b.path, 'en'));
  return { ok: unique.length === 0, findings: unique };
}

const taskFolders = dir => { try { return readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => e.name); } catch { return []; } };

/** `{ ok, findings }` for every task folder of `<root>/csharp` and `<root>/node`: a folder on one side only is a finding, paths are task-prefixed. */
export function diffAllTasks({ root }) {
  const [cs, nd] = [taskFolders(path.join(root, 'csharp')), taskFolders(path.join(root, 'node'))];
  const findings = [...new Set([...cs, ...nd])].sort().flatMap(task => {
    if (!cs.includes(task) || !nd.includes(task)) return [{ path: `${cs.includes(task) ? 'csharp' : 'node'}/${task}`, what: `task folder only in ${cs.includes(task) ? 'csharp' : 'node'}` }];
    return diffTaskPair({ csharp: path.join(root, 'csharp', task), node: path.join(root, 'node', task) }).findings.map(f => ({ ...f, path: `${task}/${f.path}` }));
  });
  return { ok: findings.length === 0, findings };
}
