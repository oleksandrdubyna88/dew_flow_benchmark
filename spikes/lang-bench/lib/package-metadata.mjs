// E1.S4: what a snapshot says about its own packages, read from the snapshot and nothing else: the licence a NuGet package declares in its `.nuspec` and the licence an npm package declares in its own
// `package.json` (read from the tarball the npm cache holds, through the index entry's integrity). The recipe's typed `licence` of every offered package is compared with these by a test, so a typed value cannot drift.
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { readNpmPackages } from '../../minigit-bench/lib/packages.mjs';

const EXPRESSION = /<license\s+type="expression"[^>]*>\s*([^<]+?)\s*<\/license>/;
const FILE = /<license\s+type="file"[^>]*>\s*([^<]+?)\s*<\/license>/;
const URL_ONLY = /<licenseUrl>\s*([^<]+?)\s*<\/licenseUrl>/;

/** The licence a NuGet package of the global packages folder declares: its SPDX expression, `file:<name>`, `url:<address>`, or `null` (a nuspec that says nothing). `null` also when the package is not there. */
export function nugetLicence(nugetDir, id, version) {
  const file = path.join(nugetDir, id.toLowerCase(), version, `${id.toLowerCase()}.nuspec`);
  if (!existsSync(file)) return null;
  const text = readFileSync(file, 'utf8');
  const [, expression] = EXPRESSION.exec(text) ?? [];
  const [, named] = FILE.exec(text) ?? [];
  const [, url] = URL_ONLY.exec(text) ?? [];
  return expression ?? (named === undefined ? (url === undefined ? null : `url:${url}`) : `file:${named}`);
}

/** `<npm cache>/_cacache/content-v2/sha512/<aa>/<bb>/<rest>` of an integrity `sha512-<base64>`. */
export function contentPath(npmDir, integrity) {
  const hex = Buffer.from(integrity.replace(/^sha512-/, ''), 'base64').toString('hex');
  return path.join(npmDir, '_cacache', 'content-v2', 'sha512', hex.slice(0, 2), hex.slice(2, 4), hex.slice(4));
}

const licenceText = value => (typeof value === 'string' ? value : value?.type ?? null);
const tar = (args, maxBuffer) => spawnSync('tar', args, { encoding: 'utf8', timeout: 20_000, maxBuffer });

/** The top-level `package.json` of a tarball: its root folder is `package/` for most and the package's own name for DefinitelyTyped, so the list is read first. `null` when there is none. */
function packageJsonOf(tarball) {
  const listed = tar(['-tzf', tarball], 16 * 1024 * 1024);
  const entry = listed.status === 0 ? listed.stdout.split('\n').find(l => /^[^/]+\/package\.json$/.test(l)) : undefined;
  const read = entry === undefined ? null : tar(['-xzOf', tarball, entry], 4 * 1024 * 1024);
  return read?.status === 0 ? JSON.parse(read.stdout) : null;
}

const cachedFile = file => (existsSync(file) ? file : null);

/** The cached tarball of `name@version`, or `null`. */
function tarballOf(npmDir, name, version) {
  const entry = readNpmPackages(npmDir, { hashes: true }).find(p => p.name === name && p.version === version);
  return entry?.integrity ? cachedFile(contentPath(npmDir, entry.integrity)) : null;
}

/** The `license` of an npm package's own `package.json`, from the tarball in the cache (`tar`, exe + argv, a ceiling): a string, or `null` when the package or its tarball is not there or declares none. */
export function npmLicence(npmDir, name, version) {
  const tarball = tarballOf(npmDir, name, version);
  return tarball === null ? null : licenceText(packageJsonOf(tarball)?.license);
}
