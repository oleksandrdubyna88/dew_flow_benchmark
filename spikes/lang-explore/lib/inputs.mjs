// The pilot's registered inputs: `inputs/explore.json` (the pins), read once and checked for shape, and the `stacks` object v1's preflight takes, built from it and from v1's probe argv
// (v1's `inputs/stacks.json` is read, never changed). Pure over values except the two file reads.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { loadStacks } from '../../minigit-bench/lib/host-checks.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS, V1, underHome } from './paths.mjs';

const SHA256 = /^[0-9a-f]{64}$/;
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const sha = v => SHA256.test(v ?? '');
const positive = n => Number.isFinite(n) && n > 0;
const LIMIT_KEYS = ['timeFloorMs', 'memoryFloorBytes', 'calibrationCeilingMs', 'buildTimeoutMs', 'outputCapBytes', 'timeFactor', 'memoryFactor'];

/** Each check of `explore.json` as `[holds(e), the sentence saying what is wrong]`. */
const CHECKS = Object.freeze([
  [e => typeof e?.cli?.version === 'string' && sha(e.cli.sha256), 'cli: a version and a sha256'],
  [e => Array.isArray(e?.cli?.pathParts) && Array.isArray(e?.node?.dirParts), 'cli.pathParts and node.dirParts are path parts under the home'],
  [e => typeof e?.dotnet?.sdk === 'string' && typeof e?.typescript?.version === 'string', 'dotnet.sdk and typescript.version are pinned'],
  [e => e?.typescript?.integrity === null || /^sha512-/.test(e?.typescript?.integrity ?? ''), 'typescript.integrity is null (before prepare) or an sha512 integrity string'],
  [e => sha(e?.python3?.sha256) && sha(e?.specs?.spec01?.sha256) && sha(e?.specs?.format?.sha256), 'python3, spec01 and format each carry a sha256'],
  [e => sha(e?.privateNotes?.sha256), 'privateNotes.sha256'],
  [e => [e?.caps?.capUsd, e?.caps?.wallMs, e?.caps?.stallMs].every(positive), 'caps: capUsd, wallMs and stallMs are positive numbers'],
  [e => LIMIT_KEYS.every(k => positive(e?.limits?.[k])), 'limits: every field is a positive number'],
  [e => typeof e?.locale === 'string', 'locale'],
]);

/** The sentences saying what in `explore.json` is not what it must be (`[]` when it is sound). */
export const exploreProblems = e => CHECKS.filter(([holds]) => !holds(e)).map(([, why]) => why);

/** `inputs/explore.json` -> `ok(explore)` | `fail` naming what is wrong. */
export function loadExplore(file = path.join(INPUTS, 'explore.json')) {
  let json;
  try { json = readJson(file); } catch (e) { return fail(`${file} is unreadable: ${e.message}`); }
  const problems = exploreProblems(json);
  return problems.length === 0 ? ok(Object.freeze(json)) : fail(`explore.json: ${problems.join('; ')}`);
}

/** Where the pinned things are under this home: the CLI copy, the Node dir and the TypeScript toolchain. */
export const pinnedPaths = (explore, home = os.homedir()) => Object.freeze({
  claudeBinary: underHome(explore.cli.pathParts, home), nodeDir: underHome(explore.node.dirParts, home), tsDir: underHome(explore.typescript.toolchainDirParts, home),
});

/** The `stacks` object v1's `preflight` takes: the pilot's pins and v1's probe argv (a probe is exe + argv, never a shell string). -> `ok(stacks)` | `fail`. */
export function stacksOf(explore) {
  const v1 = readJson(path.join(V1, 'inputs', 'stacks.json'));
  return loadStacks({
    pins: { claude: explore.cli.version, node: explore.node.version, dotnetSdk: explore.dotnet.sdk, typescript: explore.typescript.version },
    probes: v1.probes, typescriptToolchainDir: explore.typescript.toolchainDirParts,
  });
}
