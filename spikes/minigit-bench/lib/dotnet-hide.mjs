// The dotnet version folders a box on the DISTRIBUTION's dotnet must not show (E2 amendment 2026-11-19, D-9). The preflight's rule is that the box lists exactly one SDK, the pin
// (`host-checks.mjs`); a host may hold several (this one holds 10.0.114 and 11.0.100 since 2026-11-18). So the box hides, as an empty tmpfs each, every folder of a distribution root that
// belongs to another version than the pin's, and the rule stays as it is. One list: `agentBoxPrefix` computes it WHEN THE BOX IS BUILT, so an SDK installed between a preflight and a
// session is hidden too. Pure but for the default listing.
//
// What is hidden, per root that exists: `sdk/<v>` for every version `v` that is not the pin; `shared/<framework>/<v>`, `packs/<pack>/<v>`, `templates/<v>` and `sdk-manifests/<band>` whose
// MAJOR is not the pin's. What is not: `host/fxr` (the muxer resolves through it: it is the host's own resolver, and the newest one answers for every SDK), a folder whose name is not a
// version, and anything that is no directory (`library-packs/*.nupkg`: a tmpfs mounts over a directory only). What REFUSES the box: a symbolic link where a folder to hide, or a framework or
// pack folder to look into, should be (`mountable`).
import { existsSync, readdirSync } from 'node:fs';

/** The distribution's .NET locations: a pinned-SDK box hides them whole (the apphost reads them when DOTNET_ROOT is unset); a box on the distribution's dotnet hides the other versions inside them. */
export const DISTRIBUTION_DOTNET = Object.freeze(['/usr/lib/dotnet', '/usr/share/dotnet', '/etc/dotnet']);

/** Version folders directly below: `templates/<v>`, `sdk-manifests/<band>`. */
const FLAT = Object.freeze(['templates', 'sdk-manifests']);
/** Version folders one name below: `shared/<framework>/<v>`, `packs/<pack>/<v>`. */
const BY_NAME = Object.freeze(['shared', 'packs']);

/** A version, as the WHOLE name: x.y.z, then at most a prerelease suffix (`-preview.6`, `-rc.1.25451.107`). Anchored at both ends: `10.0.114typo` is no version, as a pin or as a folder. */
const VERSION = /^(\d+)\.\d+\.\d+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?$/;
const majorOf = name => VERSION.exec(name)?.[1] ?? null;

/** Whether `v` is an SDK version (x.y.z with at most a prerelease suffix, and nothing else in the string): what a pin must be. */
export const isSdkVersion = v => typeof v === 'string' && majorOf(v) !== null;

/** The sub-directories and the symbolic links of `dir`, read from the disk, as `{ name, link }` (a link is reported, never followed: what it points at is not asked). Plain files are left out. */
export const dirEntriesOf = dir => readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory() || e.isSymbolicLink()).map(e => ({ name: e.name, link: e.isSymbolicLink() }));

/**
 * The entry, when a tmpfs can be mounted over it. A symbolic link cannot be hidden (the mount would land on what it points at, or nowhere), and following it would hide a folder this list never
 * read; so the box is not built at all, and the refusal names the path. The preflight would otherwise count whatever the link still shows.
 */
function mountable(entry) {
  if (entry.link) throw new Error(`${entry.dir} is a symbolic link: a dotnet version folder is hidden by an empty tmpfs mounted over it, and a link cannot be hidden that way (nor is it followed); the box is not built. Replace the link by the folder, remove it, or run on a pinned dotnet directory`);
  return entry;
}

function majorOfPin(pin) {
  const major = isSdkVersion(pin) ? majorOf(pin) : null;
  // no silent skip: a box built on the distribution's dotnet with no pin would show every SDK of the host, and a measured cell could then be built with another one
  if (major === null) throw new Error(`dotnetPin ${typeof pin === 'string' ? `'${pin.slice(0, 40)}'` : String(pin)} is not an SDK version (x.y.z, for example 10.0.114): a box on the distribution's dotnet hides every other version, so it must be told the pinned one`);
  return major;
}

/**
 * The folders to hide, in a stable order: `{ pin, roots, exists, listDir }` -> a frozen list of absolute paths, `[]` on a host that holds only the pin's versions. `exists(path)` and
 * `listDir(dir)` (a directory's sub-directories and links, `{ name, link }` each) are injected, as `agentBoxPrefix` injects `exists`; a root or a group the host lacks is never listed (each folder is asked for before it is read).
 * Throws for a pin that is no version, and for a link where a folder it must mount over or look into should be (the pin's own version folders are never mounted over, so a link there is left).
 */
export function otherDotnetVersions({ pin, roots = DISTRIBUTION_DOTNET, exists = existsSync, listDir = dirEntriesOf }) {
  const major = majorOfPin(pin);
  const below = dir => (exists(dir) ? listDir(dir).map(e => ({ ...e, dir: `${dir}/${e.name}` })) : []);
  const versionsBelow = dir => below(dir).filter(e => majorOf(e.name) !== null);
  const inRoot = root => [
    ...versionsBelow(`${root}/sdk`).filter(e => e.name !== pin),
    ...[...BY_NAME.flatMap(group => below(`${root}/${group}`).map(e => mountable(e).dir)), ...FLAT.map(group => `${root}/${group}`)].flatMap(versionsBelow).filter(e => majorOf(e.name) !== major),
  ];
  return Object.freeze(roots.flatMap(inRoot).map(e => mountable(e).dir));
}

/**
 * The dotnet version folders ONE box hides, read from the host NOW, when the box is built: the same decision for the agent's box (`agentBoxPrefix`) and the scoring box (`scoreBoxArgv`), so the two
 * cannot drift and no caller can hold a stale list. A box with a pinned SDK directory (`dotnetDir`: the agent's box only, the scoring box has no such switch) gets nothing here (it hides the whole distribution through its caller's `hide`); every
 * other box runs the distribution's dotnet and shows only `dotnetPin`'s versions. No pin and no pinned directory is refused (`otherDotnetVersions` throws, naming the shape): skipping would show
 * every SDK of the host, and a tree could be built, or scored, with another one.
 */
export const boxDotnetHide = ({ dotnetDir, dotnetPin, exists, listDir }) => (dotnetDir ? [] : otherDotnetVersions({ pin: dotnetPin, exists, listDir }));
