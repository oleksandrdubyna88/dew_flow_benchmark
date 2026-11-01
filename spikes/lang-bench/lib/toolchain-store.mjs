// E1.S5: the private toolchain store on disk (`~/rsd/lang-v2/toolchains/`, outside every git tree). A download is kept only when its bytes are
// the published ones; a directory is sealed read-only and its tree hash recorded; the SDK directory holds only the pinned SDK. The tree hash and
// the seal are v1's (`spikes/minigit-bench/lib/cache-snapshot.mjs`), imported and not copied.
import { createHash } from 'node:crypto';
import { createReadStream, lstatSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { pipeline } from 'node:stream/promises';
import { makeReadOnly, makeWritable, treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { refuseInsideWorktree } from '../../minigit-bench/lib/git-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const sealTree = makeReadOnly;
export const unsealTree = makeWritable;
export const treeRecord = treeHash;

/** The digest of a file's bytes, streamed (an SDK archive is hundreds of MB): `await digestOfFile(file, 'sha512', 'hex')`. */
export async function digestOfFile(file, algorithm, encoding) {
  const hash = createHash(algorithm);
  await pipeline(createReadStream(file), hash);
  return hash.digest(encoding);
}

/** ok(actual) when the file's digest equals `expected`, else a fail naming the label, the expected and the actual digest (or that the file cannot be read). */
export async function checkDigest({ file, algorithm, encoding, expected, label }) {
  let actual;
  try {
    actual = await digestOfFile(file, algorithm, encoding);
  } catch (e) {
    return fail(`${label}: ${file} cannot be read (${e.code ?? e.message}), so its ${algorithm} cannot be checked`);
  }
  return actual === expected ? ok(actual) : fail(`${label}: the ${algorithm} of ${file} is ${actual}, the publisher lists ${expected}`);
}

function entriesBelow(root, rel = '') {
  return readdirSync(path.join(root, rel)).sort().flatMap(name => {
    const r = path.join(rel, name);
    return [r, ...(lstatSync(path.join(root, r)).isDirectory() ? entriesBelow(root, r) : [])];
  });
}

const WRITE_BITS = 0o222;

/** Paths (relative to `root`; `.` is the root itself) that carry any write bit. A link is not followed: its own mode means nothing on Linux. */
export function writablePaths(root) {
  const writable = rel => (lstatSync(path.join(root, rel)).mode & WRITE_BITS) !== 0 && !lstatSync(path.join(root, rel)).isSymbolicLink();
  return ['.', ...entriesBelow(root)].filter(writable);
}

/** The SDK version directories under `<dotnetDir>/sdk`, sorted; a helper folder (`NuGetFallbackFolder`) is not one. */
export function sdkVersionsIn(dotnetDir) {
  try {
    return readdirSync(path.join(dotnetDir, 'sdk')).filter(name => /^\d+\.\d+\.\d+/.test(name)).sort();
  } catch (e) {
    if (e.code === 'ENOENT') return [];
    throw e;
  }
}

/** Per name in `dirs`: `{ name, dir, state: 'matches' | 'differs' | 'missing', actual }` against the recorded tree hash. */
export function verifyRecordedTrees({ dirs, recorded }) {
  return Object.entries(dirs).map(([name, dir]) => {
    try {
      lstatSync(dir);
    } catch (e) {
      if (e.code === 'ENOENT') return { name, dir, state: 'missing', actual: null };
      throw e;
    }
    const actual = treeRecord(dir);
    return { name, dir, state: actual === recorded[name] ? 'matches' : 'differs', actual };
  });
}

/** ok(root) when the store root is outside every git worktree (a tracked toolchain would publish it); fail otherwise. */
export const checkStoreRoot = root => refuseInsideWorktree(root, 'the toolchain store');
