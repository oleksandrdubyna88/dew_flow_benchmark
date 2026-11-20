// The one way the recovery path writes a file under a root it does not control (F3.S1 (a), PR 4, coai code round 3: the link audit): the backup copy, a session folder after a free and the series folder can all hold a link that a
// reader of this repository planted at a name the code can predict. Nothing here follows one:
//   - `linkOnWay` `lstat`s every folder from the root down to a folder (the root itself only when asked) and names the first link;
//   - `placeFile` and `placeLink` refuse a link on the way before any folder is made, write the file to an EXCLUSIVE temporary file beside the target (a link at that name is removed first, and the file is made with `wx` or
//     `COPYFILE_EXCL`) and `rename` it over the target, so a link at the target is replaced, never followed.
// Both give `ok(target)` or `fail(sentence)`; neither throws for a link.
import { chmodSync, constants, copyFileSync, lstatSync, mkdirSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const lstatOf = file => { try { return lstatSync(file); } catch { return undefined; } };

const prefixesOf = folder => (folder === '.' ? [] : folder.split('/').map((_, i, parts) => parts.slice(0, i + 1).join('/')));

/** The first link on the way from `root` down to each of `folders` (paths from the root with `/`; every folder on the way is `lstat`ed, none is followed): its path, or null. `withRoot` looks at the root itself too. */
export function linkOnWay({ root, folders, withRoot = false }) {
  const all = [...new Set(folders.flatMap(prefixesOf))].sort();
  return [...(withRoot ? ['.'] : []), ...all].map(folder => path.join(root, folder)).find(folder => lstatOf(folder)?.isSymbolicLink()) ?? null;
}

const linkSentence = link => `${link} is a link on the way to a file this verb writes: nothing is written through a link`;

/** The refusal for a link on the way to `rel` under `root`, or null. */
const wayProblem = ({ root, rel, withRoot }) => {
  const link = linkOnWay({ root, folders: [path.posix.dirname(rel)], withRoot });
  return link === null ? null : linkSentence(link);
};

/**
 * Place the file `rel` (a path from `root` with `/`) under `root`: `text` is written, or `copyFrom` is copied; `mode` (optional) is set before the rename. A link on the way refuses (the root only with `withRoot`); the temporary file is exclusive.
 * -> `ok(target)` | `fail(sentence)`.
 */
export function placeFile({ root, rel, text, copyFrom, mode, dirMode, withRoot = false }) {
  const problem = wayProblem({ root, rel, withRoot });
  if (problem !== null) return fail(problem);
  const target = path.join(root, rel);
  mkdirSync(path.dirname(target), { recursive: true, mode: dirMode });
  const tmp = `${target}.${process.pid}.tmp`;
  rmSync(tmp, { force: true });
  if (copyFrom === undefined) writeFileSync(tmp, text, { flag: 'wx' });
  else copyFileSync(copyFrom, tmp, constants.COPYFILE_EXCL);
  if (mode !== undefined) chmodSync(tmp, mode);
  renameSync(tmp, target);
  return ok(target);
}

/** Place the link `rel` under `root` pointing at `linkTo`, as `placeFile` places a file: a link on the way refuses; a link or file at the target is removed and replaced (the link is made exclusively, `symlink` refuses an existing name). -> `ok(target)` | `fail(sentence)`. */
export function placeLink({ root, rel, linkTo, dirMode, withRoot = false }) {
  const problem = wayProblem({ root, rel, withRoot });
  if (problem !== null) return fail(problem);
  const target = path.join(root, rel);
  mkdirSync(path.dirname(target), { recursive: true, mode: dirMode });
  rmSync(target, { force: true });
  symlinkSync(linkTo, target);
  return ok(target);
}
