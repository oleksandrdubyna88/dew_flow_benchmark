// The real dependencies of the scoring pipeline. Tests inject stubs for any of them (`run`, `heldout`, `now`, `delivered`, ...).
import { cpSync, existsSync, mkdirSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { assertRemovable } from '../../policy-ab-quick/lib/safe-rm.mjs';
import { readFileOrNull } from './inputs.mjs';
import { runBounded } from './run.mjs';

/** Directory names that are never copied: dependencies always, build output for the C# stack (node keeps a real `bin/` of source). */
const excludedDirs = stack => (stack === 'csharp' ? ['bin', 'obj', 'node_modules'] : ['node_modules']);

/** Copy a tree (modes, times and links preserved; the root itself is never filtered), leaving out `excludedDirs(stack)` at any depth. */
function copyTree(src, dst, stack) {
  const skip = excludedDirs(stack);
  const filter = p => p === src || !(skip.includes(path.basename(p)) && statSync(p, { throwIfNoEntry: false })?.isDirectory());
  cpSync(src, dst, { recursive: true, filter, verbatimSymlinks: true, preserveTimestamps: true });
}

/** `{ isFile, isDir, mtimeMs, mode }` or null when absent. */
function stat(p) {
  const s = statSync(p, { throwIfNoEntry: false });
  return s ? { isFile: s.isFile(), isDir: s.isDirectory(), mtimeMs: s.mtimeMs, mode: s.mode } : null;
}

/**
 * Remove a scorer-owned directory under `boundary`. assertRemovable refuses a link at the target and a parent that resolves outside
 * the boundary. Links INSIDE the tree are allowed here (rmSync unlinks them without following): an agent's tree may legitimately
 * hold some, and refusing would leave a copy behind forever.
 */
function removeTree(target, boundary) {
  if (!assertRemovable(target, boundary)) return false;
  rmSync(target, { recursive: true, force: true });
  return true;
}

export const nodeFs = Object.freeze({
  copyTree,
  readFile: readFileOrNull,
  listDir: p => (existsSync(p) ? readdirSync(p) : []),
  stat,
  mkdir: p => mkdirSync(p, { recursive: true }),
  writeFile: (p, data, mode) => writeFileSync(p, data, mode ? { mode } : undefined),
  removeTree,
});

const heldoutNotWired = async () => ({ known: false, why: 'the held-out suite is not wired in yet (E1.S2)' });

/** `inputs` is the result of fetchInputs; `delivered` stays undefined until E3 can say (accepted then reads unknown, never false). */
export function defaultDeps({ inputs, toolchainDirs = [], cacheDirs = [], heldout = heldoutNotWired, delivered } = {}) {
  return { run: runBounded, fs: nodeFs, now: Date.now, inputs, box: { toolchainDirs, cacheDirs }, heldout, delivered };
}
