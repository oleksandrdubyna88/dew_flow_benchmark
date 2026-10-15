// The only recursive deletes in the spike go through here. A target is removable only when it is a real directory (not a symlink or
// a Windows junction), its parent resolves to a place under the runs root, and the runs root itself is not a drive or a top folder.
import fs from 'node:fs';
import path from 'node:path';

/** Pure: segments below the filesystem root. "D:\" has 0, "D:\rsd" has 1. */
export const depthOf = p => path.relative(path.parse(path.resolve(p)).root, path.resolve(p)).split(path.sep).filter(Boolean).length;

/** A runs root shallower than two segments could be a drive root or a top folder: refuse it. */
export function assertRunsRoot(root) {
  if (depthOf(root) < 2) throw new Error(`refusing runs root ${path.resolve(root)}: it must be at least two folders deep`);
  return path.resolve(root);
}

const inside = (child, root) => {
  const rel = path.relative(root, child);
  return rel !== '' && !rel.startsWith('..') && !path.isAbsolute(rel);
};

/**
 * Throws when `target` may not be deleted under `boundary`. Returns false when it does not exist (nothing to do), true when it is
 * removable. lstat, so a junction or symlink AT the target is refused; realpath of the parent, so a link higher up that leads out
 * of the boundary is refused too.
 */
export function assertRemovable(target, boundary, fsApi = fs) {
  let st;
  try { st = fsApi.lstatSync(target); } catch (e) { if (e.code === 'ENOENT') return false; throw e; }
  if (st.isSymbolicLink()) throw new Error(`refusing to delete ${target}: it is a link or junction`);
  if (!st.isDirectory()) throw new Error(`refusing to delete ${target}: it is not a directory`);
  const parent = fsApi.realpathSync(path.dirname(target));
  const root = fsApi.realpathSync(boundary);
  if (!inside(parent, root) && parent !== root) throw new Error(`refusing to delete ${target}: its parent resolves outside ${boundary}`);
  return true;
}

/**
 * The first link, junction or other reparse point anywhere inside a directory tree, or ''. Every entry is lstat'ed (a junction reports as
 * a symlink); a recursive delete follows none of them on current Node, but a link is exactly what a deleted-by-mistake tree looks like
 * when it leads somewhere that is not ours, so the walk REFUSES rather than trusting that.
 */
export function findLinkInside(dir, fsApi = fs) {
  for (const name of fsApi.readdirSync(dir)) {
    const p = path.join(dir, name);
    const st = fsApi.lstatSync(p);
    if (st.isSymbolicLink()) return p;
    if (st.isDirectory()) { const hit = findLinkInside(p, fsApi); if (hit) return hit; }
  }
  return '';
}

/** Delete one directory tree after assertRemovable agreed AND a walk found no link anywhere inside it. Returns whether it deleted anything. */
export function removeUnder(target, boundary, fsApi = fs) {
  if (!assertRemovable(target, boundary, fsApi)) return false;
  const link = findLinkInside(target, fsApi);
  if (link) throw new Error(`refusing to delete ${target}: it contains a link or junction at ${link}`);
  fsApi.rmSync(target, { recursive: true, force: true });
  return true;
}
