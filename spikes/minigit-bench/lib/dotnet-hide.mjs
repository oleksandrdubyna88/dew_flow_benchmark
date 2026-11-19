// The dotnet version folders a box on the DISTRIBUTION's dotnet must not show (E2 amendment 2026-11-19, D-9). The preflight's rule is that the box lists exactly one SDK, the pin
// (`host-checks.mjs`); a host may hold several (this one holds 10.0.114 and 11.0.100 since 2026-11-18). So the box hides, as an empty tmpfs each, every folder of a distribution root that
// belongs to another version than the pin's, and the rule stays as it is. One list: `agentBoxPrefix` computes it WHEN THE BOX IS BUILT, so an SDK installed between a preflight and a
// session is hidden too. Pure but for the default listing.
//
// What is hidden, per root that exists: `sdk/<v>` for every version `v` that is not the pin; `shared/<framework>/<v>`, `packs/<pack>/<v>`, `templates/<v>` and `sdk-manifests/<band>` whose
// MAJOR is not the pin's. What is not: `host/fxr` (the muxer resolves through it: it is the host's own resolver, and the newest one answers for every SDK), a folder whose name is not a
// version, and anything that is no directory (`library-packs/*.nupkg`: a tmpfs mounts over a directory only).
import { existsSync, readdirSync } from 'node:fs';

/** The distribution's .NET locations: a pinned-SDK box hides them whole (the apphost reads them when DOTNET_ROOT is unset); a box on the distribution's dotnet hides the other versions inside them. */
export const DISTRIBUTION_DOTNET = Object.freeze(['/usr/lib/dotnet', '/usr/share/dotnet', '/etc/dotnet']);

/** Version folders directly below: `templates/<v>`, `sdk-manifests/<band>`. */
const FLAT = Object.freeze(['templates', 'sdk-manifests']);
/** Version folders one name below: `shared/<framework>/<v>`, `packs/<pack>/<v>`. */
const BY_NAME = Object.freeze(['shared', 'packs']);

const VERSION = /^(\d+)\.\d+\.\d+/;
const majorOf = name => VERSION.exec(name)?.[1] ?? null;

/** The names of the sub-directories of `dir`, read from the disk. A link is not followed: a tmpfs cannot be mounted over one, so it is left, and the preflight's probe still counts what it shows. */
export const subdirsOf = dir => readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => e.name);

function majorOfPin(pin) {
  const major = typeof pin === 'string' ? majorOf(pin) : null;
  // no silent skip: a box built on the distribution's dotnet with no pin would show every SDK of the host, and a measured cell could then be built with another one
  if (major === null) throw new Error(`dotnetPin ${typeof pin === 'string' ? `'${pin.slice(0, 40)}'` : String(pin)} is not an SDK version (x.y.z, for example 10.0.114): a box on the distribution's dotnet hides every other version, so it must be told the pinned one`);
  return major;
}

/**
 * The folders to hide, in a stable order: `{ pin, roots, exists, listDir }` -> a frozen list of absolute paths, `[]` on a host that holds only the pin's versions. `exists(path)` and
 * `listDir(dir)` (the names of a directory's sub-directories) are injected, as `agentBoxPrefix` injects `exists`; a root or a group the host lacks is never listed (each folder is asked for before it is read).
 */
export function otherDotnetVersions({ pin, roots = DISTRIBUTION_DOTNET, exists = existsSync, listDir = subdirsOf }) {
  const major = majorOfPin(pin);
  const below = dir => (exists(dir) ? listDir(dir).map(name => ({ name, dir: `${dir}/${name}` })) : []);
  const versionsBelow = dir => below(dir).filter(e => majorOf(e.name) !== null);
  const inRoot = root => [
    ...versionsBelow(`${root}/sdk`).filter(e => e.name !== pin),
    ...[...BY_NAME.flatMap(group => below(`${root}/${group}`).map(e => e.dir)), ...FLAT.map(group => `${root}/${group}`)].flatMap(versionsBelow).filter(e => majorOf(e.name) !== major),
  ];
  return Object.freeze(roots.flatMap(inRoot).map(e => e.dir));
}
