// What a command needs of this host, read once: the pins, the registered snapshots and where they are, the spec and FORMAT texts at their pins, the python pin, and the function that scores a tree.
// The wiring of the commands (`calibrate`, `rehearse`, `run`); the decisions live in the modules it hands to.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadExplore, pinnedPaths } from './inputs.mjs';
import { INPUTS, REPO, toolchainsDir, underHome } from './paths.mjs';
import { scoreTree } from './score.mjs';
import { candidateRepos, loadSources, workspaceFiles } from './workspace.mjs';

const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

/** The directories the two snapshots are expected in. */
const snapshotDirs = ({ explore, reg, home }) => ({ csharp: underHome(explore.csharpSnapshot.dirParts, home), node: path.join(toolchainsDir(home), `cache-node-${reg.node.treeSha256.slice(0, 12)}`) });

const registered = () => (existsSync(path.join(INPUTS, 'cache-snapshots.json')) ? readJson(path.join(INPUTS, 'cache-snapshots.json')) : {});
const absentDirs = dirs => Object.entries(dirs).filter(([, d]) => !existsSync(d)).map(([s, d]) => `the ${s} snapshot ${d}`);

/** The registered snapshots and where each is: `{ csharp: { dir, parts, record }, node: {...}, typesNode }` | `fail` when `prepare` has not run. */
export function snapshotsOf({ explore, home }) {
  const reg = registered();
  if (!(reg.node && reg.csharp)) return fail('inputs/cache-snapshots.json lacks a registered stack: run `explore.mjs prepare` first');
  const dirs = snapshotDirs({ explore, reg, home });
  const missing = absentDirs(dirs);
  if (missing.length > 0) return fail(`${missing.join(', ')} is not on disk: run \`explore.mjs prepare\``);
  return ok({ csharp: { dir: dirs.csharp, parts: reg.csharp.parts, record: reg.csharp }, node: { dir: dirs.node, parts: reg.node.parts, record: reg.node }, typesNode: reg.node.resolved.TYPES_NODE });
}

/** The python pin: the interpreter the judge runs under, by sha256. */
export function pythonProblem(explore) {
  const file = explore.python3.path;
  return existsSync(file) && sha256File(file) === explore.python3.sha256 ? null : `${file} is missing or is not the pinned python3 (${explore.python3.sha256.slice(0, 12)})`;
}

/**
 * `ok(world)`: `explore`, `home`, `paths` (the pinned binaries), `snapshots`, `sources`, `filesFor(stack)` (the frozen workspace files), and `scoreFn({ stack, source, files, limits, sets, caches?, work? })` -> `ok(score)` | `fail`.
 * `fail` names the first thing missing (the pins, `prepare`, the task repository, the python pin).
 */
export async function loadWorld({ home = os.homedir(), work }) {
  const explore = loadExplore();
  if (!explore.ok) return explore;
  const snapshots = hostThings({ explore: explore.value, home });
  if (!snapshots.ok) return snapshots;
  const sources = await sourcesFor({ explore: explore.value, home });
  return sources.ok ? ok(worldOf({ explore: explore.value, home, snapshots: snapshots.value, sources: sources.value, work })) : sources;
}

/** The snapshots, and the python pin: what `prepare` and the host must hold before anything is scored. */
function hostThings({ explore, home }) {
  const bad = pythonProblem(explore);
  return bad ? fail(bad) : snapshotsOf({ explore, home });
}

/** The spec and FORMAT texts at their pins from the task repository this host has. */
const sourcesFor = ({ explore, home }) => loadSources({ explore, candidates: candidateRepos({ repoName: explore.specs.repoName, repoRoot: REPO, commonRoot: path.resolve(REPO, '..', '..', 'dew_flow_benchmark'), home }) });

function worldOf({ explore, home, snapshots, sources, work }) {
  const paths = pinnedPaths(explore, home);
  const filesFor = stack => workspaceFiles({ explore, stack, sources, typesNode: snapshots.typesNode });
  const defaultCaches = stack => ({ nuget: path.join(snapshots.csharp.dir, 'nuget'), npm: path.join(snapshots.node.dir, 'npm'), stack });
  const scoreFn = args => scoreTree({
    explore, stack: args.stack, source: args.source, files: args.files ?? filesFor(args.stack), sets: args.sets, limits: args.limits, traps: args.traps ?? [], work: args.work ?? work,
    toolchains: { dirs: [paths.nodeDir, paths.tsDir], tsDir: paths.tsDir }, caches: args.caches ?? defaultCaches(args.stack), python: explore.python3.path, operatorHome: home,
  });
  return { explore, home, paths, snapshots, sources, filesFor, scoreFn, work };
}
