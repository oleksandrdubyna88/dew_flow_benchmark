// The quick run's profile (owner's order of 2026-11-05): tasks 04 05 06 07 08 10, daemon and web, on the language comparison v2's toolchains and registered caches. Every function here is pure over the pins
// (`inputs/explore.json`, the `quick` block) except `quickSnapshots`, which reads the v2 record and looks at the disk. The olympiad pilot's code reaches this module only through `isQuick(task)`.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { pinnedPaths } from './inputs.mjs';
import { memoryLimit } from './memory-limit.mjs';
import { underHome } from './paths.mjs';

const BENCH = fileURLToPath(new URL('../../lang-bench/', import.meta.url));
export const BENCH_INPUTS = path.join(BENCH, 'inputs');

/** The distribution's .NET locations: a pinned-SDK box must not find them (the apphost reads them when DOTNET_ROOT is unset). */
export const DISTRIBUTION_DOTNET = Object.freeze(['/usr/lib/dotnet', '/usr/share/dotnet', '/etc/dotnet']);

export const isQuick = task => task?.quick === true;
/** `olympiad` (the pilot's own tasks), `daemon` or `web`. */
export const kindOf = task => task?.kind ?? 'olympiad';

/** The pins the quick tasks run under, as an `explore`-shaped object: the pilot's pins with the v2 toolchain versions and directories. Pure. */
export function quickExplore(base, home) {
  const q = base.quick;
  const dotnetDir = underHome(q.dotnet.dirParts, home);
  return Object.freeze({
    ...base,
    dotnet: { sdk: q.dotnet.sdk, runtime: q.dotnet.runtime, note: 'the language comparison v2 toolchain (quick run)' },
    node: { version: q.node.version, dirParts: q.node.dirParts },
    typescript: { ...base.typescript, version: q.typescript.version, toolchainDirParts: q.typescript.toolchainDirParts },
    envExtra: { ...base.envExtra, DOTNET_ROOT: dotnetDir },
    quickProfile: true,
  });
}

/** The pinned directories of the quick profile: the CLI copy, Node, TypeScript, the .NET SDK. */
export const quickPaths = (explore, home) => Object.freeze({ ...pinnedPaths(explore, home), dotnetDir: underHome(explore.quick.dotnet.dirParts, home) });

/** The toolchain directories as the execution layer names them. */
export const layerDirs = paths => ({ dotnetSdk: paths.dotnetDir, node: paths.nodeDir, typescript: paths.tsDir });

/**
 * The v2 registered snapshots: `{ csharp: { dir, parts, record }, node: {...}, typesNode, recordFile }` | `fail`. The record is `lang-bench/inputs/cache-snapshots-lang.json`; the folders are under
 * `<home>/<quick.cachesDirParts>`. Nothing is re-hashed here (`pins.mjs` does, once per pair).
 */
export function quickSnapshots({ base, home, recordFile = path.join(BENCH_INPUTS, 'cache-snapshots-lang.json') }) {
  const record = readRecord(recordFile);
  if (!record.ok) return record;
  const root = underHome(base.quick.cachesDirParts, home);
  const csharp = snapshotOf({ record: record.value, stack: 'csharp', root });
  const node = snapshotOf({ record: record.value, stack: 'node', root });
  if (!csharp.ok) return csharp;
  return node.ok ? ok({ csharp: csharp.value, node: node.value, typesNode: record.value.node.resolved.TYPES_NODE, recordFile }) : node;
}

function readRecord(file) {
  try { return ok(JSON.parse(readFileSync(file, 'utf8'))); } catch (e) { return fail(`${file} is unreadable: ${e.message}`); }
}

/** One stack's snapshot: its folder under `root`, which must be on disk. -> `ok({ dir, parts, record })` | `fail`. */
function snapshotOf({ record, stack, root }) {
  const r = record[stack];
  const dir = typeof r?.dirName === 'string' ? path.join(root, r.dirName) : null;
  return dir !== null && existsSync(dir) ? ok({ dir, parts: r.parts, record: r }) : fail(`the v2 ${stack} snapshot ${dir ?? '(unrecorded)'} is not on disk`);
}

/**
 * The fixed limits of the quick tasks, shaped as a calibration (no oracle, no mutants): `{ quick: true, limits }`. The memory limit is the one registered value (`inputs/memory-limit.json`, D72), not a key of the pins.
 * The same bytes every time, so a series' own copy always equals it.
 */
export function quickCalibration(explore, limit = memoryLimit()) {
  if (!limit.ok) throw new Error(limit.reason);
  return Object.freeze({ quick: true, note: 'fixed limits of the quick run; no calibration was made', limits: { ...explore.quick.limits, memoryBytes: limit.value.bytes } });
}

/** `quick.pausePoint` and `quick.planTasks` in the shape `pauseVerdict` reads from the olympiad block. */
export const quickPlan = explore => ({ planTasks: explore.quick.planTasks, pausePoint: explore.quick.pausePoint });
