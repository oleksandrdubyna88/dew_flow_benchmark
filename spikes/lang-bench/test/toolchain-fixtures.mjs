// Test plumbing for the toolchain tests: folders from the owned-name helper (`common.test-leftovers`, token `bench`), removed when the
// test ends. Without the conventions mount (CI checks no submodule out) the same names are made and removed here.
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { makeWritable } from '../../minigit-bench/lib/cache-snapshot.mjs';

const ownedUrl = new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const helper = existsSync(fileURLToPath(ownedUrl)) ? (await import(ownedUrl.href)).ownedFor({ token: 'bench' }) : null;

/** A fresh folder named `testsToDelete-u<uid>-bench-<what>-<id>` in `parent` (the temp root by default); `t.after` removes it. `t` is a node:test context. */
export function ownedFolder(t, what, parent = os.tmpdir()) {
  // A sealed (read-only) tree inside cannot be removed, so a test that failed before it unsealed would leave the folder behind. This hook is registered
  // BEFORE the folder exists and `after` hooks run first-registered-first, so the tree is writable again by the time the owned cleanup removes it.
  const holder = { dir: null };
  t.after(() => { if (holder.dir && existsSync(holder.dir)) makeWritable(holder.dir); });
  holder.dir = helper ? helper.ownedDir(t, what, { parent }) : mkdtempSync(path.join(parent, `testsToDelete-u${process.getuid?.() ?? 0}-bench-${what}-`));
  if (!helper) t.after(() => rmSync(holder.dir, { recursive: true, force: true }));
  return holder.dir;
}

/** `files` is `{ 'relative/path': 'text' }`; written under `root`, parents created. Returns `root`. */
export function writeTree(root, files) {
  for (const [rel, text] of Object.entries(files)) {
    const file = path.join(root, rel);
    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, text);
  }
  return root;
}


/** A `.tar.xz` at `archive` holding `files` (`{ 'dir/name': 'text' }`) as written by the real `tar`/`xz`; the source tree is a sibling owned folder. */
export function makeTarXz(t, archive, files) {
  const source = writeTree(ownedFolder(t, 'tarsrc'), files);
  const top = [...new Set(Object.keys(files).map(rel => rel.split('/')[0]))];
  const made = spawnSync('tar', ['-cJf', archive, '-C', source, ...top], { encoding: 'utf8' });
  if (made.status !== 0) throw new Error(`tar failed building a fixture archive: ${made.stderr}`);
  return archive;
}
