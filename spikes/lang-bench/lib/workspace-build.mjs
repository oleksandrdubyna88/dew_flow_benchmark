// E1.S3: the pure and file parts of the workspace builder: the check of a generated `package-lock.json` against the registered manifest, and the destination guard that the builder and the bare copy share.
import { lstatSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { gitWorktreeOf } from '../../minigit-bench/lib/git-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { outsideManifest, usedNpm } from './cache-offline.mjs';

const MODULES = 'node_modules/';
const nameOf = key => key.slice(key.lastIndexOf(MODULES) + MODULES.length);
const isPackage = ([key, entry]) => key.includes(MODULES) && entry.link !== true && typeof entry.version === 'string';
const sameKeys = (a, b) => JSON.stringify(Object.entries(a ?? {}).sort()) === JSON.stringify(Object.entries(b ?? {}).sort());

/**
 * What is wrong with a `package-lock.json` (parsed) against the registered npm manifest and the template's `package.json` (parsed): a package outside the manifest, an integrity that is not the manifest's,
 * an install script, a root that does not list what `package.json` lists. `[]` is clean; each problem names the package.
 */
export function lockProblems(lock, manifest, packageJson) {
  const npm = manifest.filter(row => row.ecosystem === 'npm');
  const integrity = new Map(npm.map(row => [`${row.name}@${row.version}`, row.integrity]));
  const entries = Object.entries(lock.packages ?? {}).filter(isPackage);
  const wrongIntegrity = entries.filter(([key, entry]) => integrity.has(`${nameOf(key)}@${entry.version}`) && integrity.get(`${nameOf(key)}@${entry.version}`) !== entry.integrity).map(([key, entry]) => `${nameOf(key)}@${entry.version} has the integrity ${entry.integrity}, not the manifest's`);
  const scripts = entries.filter(([, entry]) => entry.hasInstallScript === true).map(([key, entry]) => `${nameOf(key)}@${entry.version} has an install script`);
  const root = lock.packages?.[''] ?? {};
  const rootProblems = ['dependencies', 'devDependencies'].filter(field => !sameKeys(root[field], packageJson[field])).map(field => `the lock's root ${field} differ from package.json`);
  return [...outsideManifest(usedNpm(lock), npm, 'npm').map(id => `${id} is not in the registered manifest`), ...wrongIntegrity, ...scripts, ...rootProblems];
}

// ---- the destination ------------------------------------------------------------------------------------------------------------------

/** `lstat` of `target`, or null when nothing is there. Any other error is thrown: only "absent" is an expected answer. */
function lstatOrNull(target) {
  try {
    return lstatSync(target);
  } catch (error) {
    if (error.code === 'ENOENT') return null;
    throw error;
  }
}

/**
 * Where a workspace may be built (accepted finding a): never inside a git worktree of any state (clean or dirty, the folder itself or a path nested under it, existing or not yet existing, a main checkout or a
 * linked worktree whose `.git` is a file), and a git that cannot answer is a refusal too. Outside every worktree: `out` is not a link, and if it exists it is an empty folder.
 * `worktreeOf` is `gitWorktreeOf`, a parameter so a test can make git unable to answer. -> `ok(resolved out)` | `fail(reason)` with `refusal: true` for the cases that exit 3.
 */
export function checkDestination(out, worktreeOf = gitWorktreeOf) {
  const target = path.resolve(out);
  if (lstatOrNull(target)?.isSymbolicLink()) return fail(`the destination ${target} is a link`, { refusal: true });
  const inside = worktreeOf(target);
  if (!inside.ok) return fail(`the destination ${target} cannot be judged: ${inside.reason}`, { refusal: true });
  if (inside.value !== null) return fail(`the destination ${target} is inside the git worktree ${inside.value}: a workspace is never built where a stray \`git add\` reaches it`, { refusal: true });
  const found = lstatOrNull(target);
  if (found === null) return ok(target);
  if (!found.isDirectory()) return fail(`the destination ${target} exists and is not a folder`, { refusal: true });
  return readdirSync(target).length === 0 ? ok(target) : fail(`the destination ${target} exists and is not empty`, { refusal: true });
}
