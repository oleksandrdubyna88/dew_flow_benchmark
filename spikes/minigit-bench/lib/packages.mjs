// The packages a bare session pulled (E3b.S1 step 9, M10): every npm tarball in the cache index and every NuGet `<id>/<version>/` folder, those added
// over the seeded snapshot, and the flags of `inputs/package-flags.json`. The readers are the only I/O; `packagesOf` is pure. A flag is never a class.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';

const ECOSYSTEMS = Object.freeze(['npm', 'nuget', 'any']);
const TARBALL = /registry\.npmjs\.org\/((?:@[^/]+\/)?[^/]+)\/-\/([^/]+)\.tgz$/;

const dirs = p => (existsSync(p) ? readdirSync(p, { withFileTypes: true }).filter(e => e.isDirectory() && !e.name.startsWith('.')).map(e => e.name) : []);
const filesUnder = dir => (!existsSync(dir) ? [] : readdirSync(dir, { withFileTypes: true }).flatMap(e => (e.isDirectory() ? filesUnder(path.join(dir, e.name)) : [path.join(dir, e.name)])));

/** `[{ ecosystem: 'npm', name, version }]` from the cacache index's tarball entries (a packument entry is metadata, not an installed package). */
export function readNpmPackages(npmCacheDir) {
  const keys = filesUnder(path.join(npmCacheDir, '_cacache', 'index-v5')).flatMap(f => readFileSync(f, 'utf8').split('\n').filter(Boolean).flatMap(line => {
    try { return [JSON.parse(line.slice(line.indexOf('\t') + 1)).key]; } catch { return []; }
  }));
  const seen = new Set();
  return keys.flatMap(key => {
    const m = TARBALL.exec(key ?? '');
    if (!m) return [];
    const name = m[1];
    const version = m[2].slice(`${name.split('/').at(-1)}-`.length);
    const id = `${name}@${version}`;
    if (seen.has(id)) return [];
    seen.add(id);
    return [{ ecosystem: 'npm', name, version }];
  });
}

/** `[{ ecosystem: 'nuget', name, version }]` from `<packages>/<id>/<version>/` (the global packages folder; dot-folders are tooling). */
export const readNugetPackages = nugetDir => dirs(nugetDir).flatMap(id => dirs(path.join(nugetDir, id)).map(version => ({ ecosystem: 'nuget', name: id, version })));

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

const matches = (rule, p) => (rule.ecosystem === 'any' || rule.ecosystem === p.ecosystem) && rule.name.test(p.name) && (rule.versionNot === null || !rule.versionNot.test(p.version));
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
