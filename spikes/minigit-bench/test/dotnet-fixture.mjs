// An invented host for the dotnet hide list (E2 amendment 2026-11-19): a listing of directories as `agentBoxPrefix` takes it (`exists` and `listDir`), with no folder on disk. `twoSdkHost` has the shape
// and the version names of the tree this machine had on 2026-11-19 (SDK 10.0.114, the pin, beside 11.0.100; four of its seven packs); the tests of every path that builds a box read the same one.
import path from 'node:path';

const selfAndAncestors = p => p.split('/').filter(Boolean).map((_, i, parts) => `/${parts.slice(0, i + 1).join('/')}`);

/** `{ exists, listDir }` over the given directories and their ancestors; `listDir` names the sub-directories of one, sorted. A directory that is not there lists nothing. */
export function fakeDirs(dirs) {
  const all = new Set(dirs.flatMap(selfAndAncestors));
  return Object.freeze({ exists: p => all.has(p), listDir: dir => [...all].filter(p => path.dirname(p) === dir && p !== dir).map(p => path.basename(p)).sort() });
}

export const PIN = '10.0.114';
export const ROOT = '/usr/lib/dotnet';

/** What one SDK brings under a root: `{ sdk, runtime, templates, bands, aot }` are its folder names. */
const sdkDirs = (root, { sdk, runtime, templates, bands, aot }) => [
  `${root}/sdk/${sdk}`,
  ...['Microsoft.NETCore.App', 'Microsoft.AspNetCore.App'].map(f => `${root}/shared/${f}/${runtime}`),
  ...['Microsoft.NETCore.App.Ref', 'Microsoft.AspNetCore.App.Ref', 'Microsoft.NETCore.App.Host.ubuntu.26.04-x64'].map(p => `${root}/packs/${p}/${runtime}`),
  `${root}/packs/runtime.ubuntu.26.04-x64.Microsoft.DotNet.ILCompiler/${aot}`,
  `${root}/templates/${templates}`,
  ...bands.map(b => `${root}/sdk-manifests/${b}`),
  `${root}/host/fxr/${runtime}`,
];

export const SDK_10 = Object.freeze({ sdk: PIN, runtime: '10.0.14', templates: '10.0.15', bands: ['10.0.100'], aot: '10.0.15' });
export const SDK_11 = Object.freeze({ sdk: '11.0.100', runtime: '11.0.0', templates: '11.0.0', bands: ['11.0.100', '11.0.100-preview.6'], aot: '11.0.0' });

/** A host that holds only the pinned SDK (this machine before 2026-11-18). */
export const oneSdkHost = (root = ROOT) => sdkDirs(root, SDK_10);
/** A host that holds the pinned SDK and SDK 11 (this machine on 2026-11-19); `/etc/dotnet` holds files only. */
export const twoSdkHost = (root = ROOT) => [...sdkDirs(root, SDK_10), ...sdkDirs(root, SDK_11), '/etc/dotnet'];

/** The folders of SDK 11 a box on the pin must not show: everything it brought but `host/fxr`. */
export const otherVersionDirs = (root = ROOT) => sdkDirs(root, SDK_11).filter(p => !p.startsWith(`${root}/host/`));

/** The `--tmpfs` paths of a bwrap argv, in order. */
export const tmpfsOf = argv => argv.flatMap((a, i) => (a === '--tmpfs' ? [argv[i + 1]] : []));
