// The real dependencies of the scoring pipeline. Tests inject stubs for any of them (`run`, `heldout`, `now`, `delivered`, ...).
import { cpSync, existsSync, mkdirSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { assertRemovable } from '../../policy-ab-quick/lib/safe-rm.mjs';
import { makeHeldoutDep } from './heldout-run.mjs';
import { readFileOrNull, readRegularFile } from './inputs.mjs';
import { runBounded } from './run.mjs';

/** Directory names that are never copied: dependencies always, build output for the C# stack (node keeps a real `bin/` of source). */
export const excludedDirs = stack => (stack === 'csharp' ? ['bin', 'obj', 'node_modules'] : ['node_modules']);

/** Copy a tree (modes, times and links preserved; the root itself is never filtered), leaving out directories named in `skip` at any depth. */
function copyTree(src, dst, skip = []) {
  const filter = p => p === src || !(skip.includes(path.basename(p)) && statSync(p, { throwIfNoEntry: false })?.isDirectory());
  cpSync(src, dst, { recursive: true, filter, verbatimSymlinks: true, preserveTimestamps: true });
}

/** `{ isFile, isDir, mtimeMs, mode }` or null when absent. */
function stat(p) {
  const s = statSync(p, { throwIfNoEntry: false });
  return s ? { isFile: s.isFile(), isDir: s.isDirectory(), mtimeMs: s.mtimeMs, mode: s.mode } : null;
}

/**
 * Remove a scorer-owned directory under `boundary`: true when removed, false when it did not exist. assertRemovable THROWS when the
 * target is a link, not a directory, or its parent resolves outside the boundary. Links INSIDE the tree are allowed here (rmSync
 * unlinks them without following): an agent's tree may legitimately hold some, and refusing would leave a copy behind forever.
 */
function removeTree(target, boundary) {
  if (!assertRemovable(target, boundary)) return false;
  rmSync(target, { recursive: true, force: true });
  return true;
}

export const nodeFs = Object.freeze({
  copyTree,
  readFile: readFileOrNull,
  readInput: readRegularFile,
  listDir: p => (existsSync(p) ? readdirSync(p) : []),
  stat,
  mkdir: p => mkdirSync(p, { recursive: true }),
  writeFile: (p, data, mode) => writeFileSync(p, data, mode ? { mode } : undefined),
  removeTree,
  removeFile: p => rmSync(p, { force: true }),
});

/** `inputs` is the result of fetchInputs; `delivered` stays undefined until E3 can say (accepted then reads unknown, never false). */
export function defaultDeps({ inputs, toolchainDirs = [], heldout = makeHeldoutDep(), delivered } = {}) {
  return { run: runBounded, fs: nodeFs, now: Date.now, inputs, box: { toolchainDirs }, heldout, delivered };
}
