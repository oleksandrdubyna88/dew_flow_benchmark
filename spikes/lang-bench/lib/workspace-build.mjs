// E1.S3: the pure and file parts of the workspace builder. This file starts with the check of a generated `package-lock.json` against the registered manifest; the builder itself is added below it.
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
