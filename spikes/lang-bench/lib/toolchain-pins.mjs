// E1.S5: what the pinned toolchains are, read from inputs/stacks-lang.json, and what each official source says about them. Pure: JSON and
// text in, `{ ok, value } | { ok: false, reason }` out. Fetching is install-toolchains.mjs; running probes in a box is preflight-lang.mjs.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const PIN_KEYS = Object.freeze(['dotnetSdk', 'dotnetRuntime', 'node', 'typescript']);
const PROBE_KEYS = Object.freeze(['node', 'dotnetSdks', 'dotnetRuntimes', 'tsc', 'pythonSha256', 'locales']);
const DIRECTORY_KEYS = Object.freeze(['dotnetSdk', 'node', 'typescript']);
const PLAIN_VERSION = /^\d+\.\d+\.\d+$/;
const INSTALL_SCRIPT_URL = /^https:\/\/raw\.githubusercontent\.com\/dotnet\/install-scripts\/[0-9a-f]{40}\/src\/dotnet-install\.sh$/;
const SHA256_HEX = /^[0-9a-f]{64}$/;

/** The directory name each pin installs into, derived from the pin: the one place that spells these names. */
export const directoryNamesFor = pins => Object.freeze({
  dotnetSdk: `dotnet-sdk-${pins.dotnetSdk}`,
  node: `node-v${pins.node}-linux-x64`,
  typescript: `typescript-${pins.typescript}`,
});

const isNonEmptyString = x => typeof x === 'string' && x !== '';
const isArgv = x => Array.isArray(x) && x.length > 0 && x.every(isNonEmptyString);

function pinProblems(json) {
  const problems = PIN_KEYS.filter(k => !PLAIN_VERSION.test(json.pins?.[k] ?? '')).map(k => `pins.${k} must be a plain x.y.z version, got ${JSON.stringify(json.pins?.[k])}`);
  const line = json.typescriptLine;
  if (!isNonEmptyString(line) || problems.length > 0) return problems.length > 0 ? problems : ['typescriptLine must name the pinned line (for example 6.0)'];
  return json.pins.typescript.startsWith(`${line}.`) ? [] : [`pins.typescript ${json.pins.typescript} is outside the ${line} line (typescriptLine): the type-aware lint supports only ${line}.x`];
}

function directoryProblems(json) {
  const expected = directoryNamesFor(json.pins);
  return DIRECTORY_KEYS.filter(k => json.directories?.[k] !== expected[k]).map(k => `directories.${k} must be ${expected[k]} (it carries its pin), got ${JSON.stringify(json.directories?.[k])}`);
}

function artifactProblems(json) {
  const a = json.artifacts ?? {};
  return [
    ...(SHA256_HEX.test(a.dotnetInstallScript?.sha256 ?? '') ? [] : ['artifacts.dotnetInstallScript.sha256 must be 64 hex characters']),
    ...(INSTALL_SCRIPT_URL.test(a.dotnetInstallScript?.url ?? '') ? [] : ['artifacts.dotnetInstallScript.url must be pinned at a commit (https://raw.githubusercontent.com/dotnet/install-scripts/<40-hex commit>/src/dotnet-install.sh): a moving URL changes under the recorded sha256']),
    ...(/^[0-9a-f]{128}$/.test(a.dotnetSdk?.sha512 ?? '') ? [] : ['artifacts.dotnetSdk.sha512 must be 128 hex characters']),
    ...(SHA256_HEX.test(a.node?.sha256 ?? '') ? [] : ['artifacts.node.sha256 must be 64 hex characters']),
    ...(/^sha512-[A-Za-z0-9+/]{86}==$/.test(a.typescript?.integrity ?? '') ? [] : ['artifacts.typescript.integrity must be an npm sha512 integrity string']),
  ];
}

function treeProblems(json) {
  if (json.treeSha256 === undefined) return [];
  return Object.entries(json.treeSha256).filter(([name, hash]) => !DIRECTORY_KEYS.includes(name) || !SHA256_HEX.test(hash))
    .map(([name]) => `treeSha256.${name} must be 64 hex characters and name a toolchain directory (${DIRECTORY_KEYS.join(', ')})`);
}

function otherProblems(json) {
  return [
    ...PROBE_KEYS.filter(k => !isArgv(json.probes?.[k])).map(k => `probes.${k} must be an argv array (exe + argv, never a shell string)`),
    ...(SHA256_HEX.test(json.python?.sha256 ?? '') && isNonEmptyString(json.python?.path) ? [] : ['python needs path and a 64-hex sha256']),
    ...(isNonEmptyString(json.locale) ? [] : ['locale must name the locale the box needs']),
    ...(isArgv(json.storeRoot) ? [] : ['storeRoot must be the path parts under the home']),
  ];
}

/** Validate `inputs/stacks-lang.json` -> ok(frozen copy) | fail naming every problem found (all of them, not the first). */
export function loadStacksLang(json) {
  if (!json || typeof json !== 'object') return fail('stacks-lang.json is a JSON object');
  const pins = pinProblems(json);
  const problems = [...pins, ...(pins.length === 0 ? directoryProblems(json) : []), ...artifactProblems(json), ...treeProblems(json), ...otherProblems(json)];
  return problems.length === 0 ? ok(Object.freeze({ ...json })) : fail(`stacks-lang.json: ${problems.join('; ')}`, { problems });
}

/** From the .NET release metadata: the archive `name` for `rid` of the SDK `version` -> ok({ url, sha512 }) | fail naming the SDK. */
export function dotnetOfficial(metadata, { version, rid, name }) {
  const sdks = (metadata?.releases ?? []).flatMap(release => [release.sdk, ...(release.sdks ?? [])]).filter(sdk => sdk?.version === version);
  const file = sdks.flatMap(sdk => sdk.files ?? []).find(f => f.rid === rid && f.name === name);
  if (!file) return fail(`the .NET release metadata lists no ${name} for ${rid} in SDK ${version}`);
  return /^[0-9a-f]{128}$/.test(file.hash ?? '') ? ok({ url: file.url, sha512: file.hash }) : fail(`the .NET release metadata gives ${name} of SDK ${version} no sha512 hash`);
}

/** From Node's SHASUMS256.txt: the sha256 of exactly `file` -> ok(hex) | fail naming the file. */
export function nodeOfficial(shasumsText, file) {
  for (const line of shasumsText.split('\n')) {
    const m = /^([0-9a-f]{64}) {2}(\S+)$/.exec(line.trim());
    if (m && m[2] === file) return ok(m[1]);
  }
  return fail(`SHASUMS256.txt has no line for ${file}`);
}

/** From npm's packument: the tarball URL and `dist.integrity` of exactly `version` -> ok({ tarball, integrity }) | fail. */
export function typescriptOfficial(packument, version) {
  const dist = packument?.versions?.[version]?.dist;
  if (!dist?.tarball || !dist?.integrity) return fail(`npm lists no typescript@${version} with a tarball and an integrity`);
  return ok({ tarball: dist.tarball, integrity: dist.integrity });
}

