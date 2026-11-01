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

/** An ISO 8601 time in UTC: a date, a time, optional fractions of a second, and `Z` (common.utc-timestamps). */
const UTC_TIME = /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?Z$/;
const pin = p => sha(p?.sha256) && typeof p?.sha === 'string' && typeof p?.path === 'string';
const parts = a => Array.isArray(a) && a.length > 0 && a.every(x => typeof x === 'string');
const taskShape = k => `tasks.${k}`;
const sameJson = (a, b) => JSON.stringify(a) === JSON.stringify(b);

/** What one entry of the `tasks` map must hold, as `[holds(entry), the sentence saying what is missing]`. */
const ENTRY_CHECKS = Object.freeze([
  [t => pin(t?.spec) && pin(t?.format) && typeof t?.format?.label === 'string', 'a spec and a FORMAT pin (sha, path, sha256; the FORMAT a label)'],
  [t => typeof t?.privateNotes?.relativePath === 'string' && sha(t?.privateNotes?.sha256), 'privateNotes (relativePath, sha256)'],
  [t => parts(t?.heldoutDirParts) && parts(t?.calibrationParts), 'heldoutDirParts and calibrationParts as path parts under the home'],
  [t => typeof t?.noteLabel === 'string' && typeof t?.title === 'string' && typeof t?.draft === 'boolean', 'title, noteLabel and draft'],
  [t => /^[0-9a-f]{40}$/.test(t?.baselineCommit ?? '') && UTC_TIME.test(t?.heldoutBorn ?? '') && Number.isFinite(Date.parse(t.heldoutBorn)), 'baselineCommit (a 40-hex commit made before the held-out folder was born) and heldoutBorn (a UTC time: an ISO string ending in Z)'],
]);

const entryProblems = (k, t) => ENTRY_CHECKS.filter(([holds]) => !holds(t)).map(([, why]) => `${taskShape(k)} lacks ${why}`);

/** Task 01 is built from the recorded task-1 fields and must repeat them exactly (D-O2: nothing of task 1 moves). */
const taskOneProblems = e => [
  [sameJson(e.tasks['01']?.spec, e.specs?.spec01), 'spec is not specs.spec01'],
  [sameJson({ ...e.tasks['01']?.format, label: null }, { ...e.specs?.format, label: null }), 'format is not specs.format'],
  [sameJson(e.tasks['01']?.privateNotes, e.privateNotes), 'privateNotes is not privateNotes'],
].filter(([holds]) => !holds).map(([, why]) => `tasks.01 ${why}`);

/** The sentences saying what in the `tasks` map is not what it must be (`[]` when it is sound). */
export function taskProblems(e) {
  if (e?.tasks === null || typeof e?.tasks !== 'object' || Object.keys(e.tasks)[0] !== '01') return ['tasks must be a map whose first entry is task 01'];
  return [...Object.entries(e.tasks).flatMap(([k, t]) => entryProblems(k, t)), ...taskOneProblems(e)];
}

const pausePositive = point => [point?.usd, point?.projectedFloorUsd, point?.projectedFactor, point?.pairSessions].every(positive);
const planTasksNamed = e => Array.isArray(e?.olympiad?.planTasks) && e.olympiad.planTasks.length > 0 && e.olympiad.planTasks.every(k => Object.hasOwn(e.tasks ?? {}, k) && k !== '01');

/** The sentences saying what in the `olympiad` block is not what it must be. */
const materialPositive = m => [m?.minCases, m?.minMutants].every(positive);

export const pauseProblems = e => [
  ...(planTasksNamed(e) ? [] : ['planTasks must name tasks of the map, never task 01']),
  ...(pausePositive(e?.olympiad?.pausePoint) ? [] : ['pausePoint needs positive numbers']),
  ...(materialPositive(e?.olympiad?.material) ? [] : ['material needs positive minCases and minMutants']),
];

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
  [e => taskProblems(e).length === 0, 'tasks: every task pins a spec, a FORMAT, private notes, a held-out folder, a calibration file, a note label, a baselineCommit and a UTC heldoutBorn, and task 01 repeats the recorded task-1 pins'],
  [e => pauseProblems(e).length === 0, 'olympiad: planTasks name tasks of the map and pausePoint holds positive numbers'],
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
