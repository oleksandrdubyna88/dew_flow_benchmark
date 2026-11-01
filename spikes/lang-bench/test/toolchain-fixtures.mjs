// Test plumbing for the toolchain tests: folders from the owned-name helper (`common.test-leftovers`, token `bench`), removed when the
// test ends. Without the conventions mount (CI checks no submodule out) the same names are made and removed here.
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ownedUrl = new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
const helper = existsSync(fileURLToPath(ownedUrl)) ? (await import(ownedUrl.href)).ownedFor({ token: 'bench' }) : null;

/** A fresh folder named `testsToDelete-u<uid>-bench-<what>-<id>` in the temp root; `t.after` removes it. `t` is a node:test context. */
export function ownedFolder(t, what) {
  if (helper) return helper.ownedDir(t, what);
  const dir = mkdtempSync(path.join(os.tmpdir(), `testsToDelete-u${process.getuid?.() ?? 0}-bench-${what}-`));
  t.after(() => rmSync(dir, { recursive: true, force: true }));
  return dir;
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

