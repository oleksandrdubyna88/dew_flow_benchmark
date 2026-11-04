// The packages a bare session pulled (E3b.S1 step 9, M10): every npm tarball in the cache index and every NuGet `<id>/<version>/` folder, those added
// over the seeded snapshot, and the flags of `inputs/package-flags.json`. The readers are the only I/O; `packagesOf` is pure. A flag is never a class.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';

const ECOSYSTEMS = Object.freeze(['npm', 'nuget', 'any']);
const TARBALL = /registry\.npmjs\.org\/((?:@[^/]+\/)?[^/]+)\/-\/([^/]+)\.tgz$/;

const dirs = p => (existsSync(p) ? readdirSync(p, { withFileTypes: true }).filter(e => e.isDirectory() && !e.name.startsWith('.')).map(e => e.name) : []);
const filesUnder = dir => (!existsSync(dir) ? [] : readdirSync(dir, { withFileTypes: true }).flatMap(e => (e.isDirectory() ? filesUnder(path.join(dir, e.name)) : [path.join(dir, e.name)])));

/**
 * `[{ ecosystem: 'npm', name, version }]` from the cacache index's tarball entries (a packument entry is metadata, not an installed package). With `{ hashes: true }` (E1.S4: the snapshot's manifest)
 * each package also carries `integrity`, the index entry's own `sha512-...` field (`null` when the entry has none).
 */
export function readNpmPackages(npmCacheDir, { hashes = false } = {}) {
  const seen = new Set();
  return indexEntries(npmCacheDir).flatMap(entry => {
    const pkg = tarballOf(entry);
    const id = `${pkg?.name}@${pkg?.version}`;
    if (pkg === null || seen.has(id)) return [];
    seen.add(id);
    return [{ ecosystem: 'npm', ...pkg, ...(hashes ? { integrity: entry.integrity ?? null } : {}) }];
  });
}

/** Every parsed entry of the cacache index (a line that is not JSON is skipped). */
const indexEntries = npmCacheDir => filesUnder(path.join(npmCacheDir, '_cacache', 'index-v5')).flatMap(f => readFileSync(f, 'utf8').split('\n').filter(Boolean).flatMap(line => {
  try { return [JSON.parse(line.slice(line.indexOf('\t') + 1))]; } catch { return []; }
}));

/** `{ name, version }` of an entry whose key is a registry tarball, or `null` (a packument entry is metadata). */
function tarballOf(entry) {
  const m = TARBALL.exec(entry?.key ?? '');
  if (!m) return null;
  return { name: m[1], version: m[2].slice(`${m[1].split('/').at(-1)}-`.length) };
}

/** The `sha512-<base64>` of a NuGet package folder, from its `<id>.<version>.nupkg.sha512` file; `null` when that file is absent (absent is not empty). */
const nugetIntegrity = (nugetDir, id, version) => {
  const file = path.join(nugetDir, id, version, `${id}.${version}.nupkg.sha512`);
  return existsSync(file) ? `sha512-${readFileSync(file, 'utf8').trim()}` : null;
};

/** `[{ ecosystem: 'nuget', name, version }]` from `<packages>/<id>/<version>/` (the global packages folder; dot-folders are tooling). With `{ hashes: true }` each also carries `integrity` (see `nugetIntegrity`). */
export const readNugetPackages = (nugetDir, { hashes = false } = {}) => dirs(nugetDir).flatMap(id => dirs(path.join(nugetDir, id)).map(version => ({ ecosystem: 'nuget', name: id, version, ...(hashes ? { integrity: nugetIntegrity(nugetDir, id, version) } : {}) })));

/** The registered rules, validated: -> the rules with their regexes compiled. Throws naming the rule and the legal ecosystems (a malformed registered rule is a bug). */
export function flagRules(rules) {
  return rules.map(r => {
    if (!ECOSYSTEMS.includes(r.ecosystem)) throw new Error(`rule '${r.id}': ecosystem '${r.ecosystem}' must be one of ${ECOSYSTEMS.join(', ')}`);
    try {
      return { ...r, name: new RegExp(r.nameRegex, r.nameRegexFlags ?? ''), versionNot: r.versionNotRegex ? new RegExp(r.versionNotRegex) : null };
    } catch (e) {
      throw new Error(`rule '${r.id}': ${e.message}`);
    }
  });
}

const ecosystemMatches = (rule, p) => rule.ecosystem === 'any' || rule.ecosystem === p.ecosystem;
const versionAllowed = (rule, p) => rule.versionNot === null || !rule.versionNot.test(p.version);
const matches = (rule, p) => ecosystemMatches(rule, p) && rule.name.test(p.name) && versionAllowed(rule, p);
const idOf = p => `${p.ecosystem}:${p.name}@${p.version}`;

/**
 * `{ npm, nuget, snapshot, rules }` (package lists as the readers return them; the snapshot's own list, from the same readers over its directories)
 * -> `{ packages, added, flagged: [{ rule, package }], flags: ['package: <rule id>', ...] }`. `added` is what the session pulled beyond the snapshot.
 */
export function packagesOf({ npm, nuget, snapshot, rules }) {
  const packages = [...npm, ...nuget];
  const inSnapshot = new Set(snapshot.map(idOf));
  const compiled = flagRules(rules);
  const flagged = packages.flatMap(p => compiled.filter(r => matches(r, p)).map(r => ({ rule: r.id, package: p })));
  return { packages, added: packages.filter(p => !inSnapshot.has(idOf(p))), flagged, flags: [...new Set(flagged.map(f => `package: ${f.rule}`))].sort() };
}
