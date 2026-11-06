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
import { isQuick, quickExplore, quickPaths, quickSnapshots } from './quick.mjs';
import { packagesInputs, quickWorkspaceFiles } from './workspace-quick.mjs';
import { scoreQuick } from './score-quick.mjs';

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
 * `ok(world)` for ONE task (`task` is its pins entry, see tasks.mjs): `explore`, `task`, `home`, `paths` (the pinned binaries), `snapshots`, `sources`, `filesFor(stack)` (the frozen workspace files), and
 * `scoreFn({ stack, source, files, limits, sets, caches?, work? })` -> `ok(score)` | `fail`. `fail` names the first thing missing (the pins, `prepare`, the task repository, the python pin).
 * `exploreFn` and `candidates` are for tests (a toy task over a toy repository); a real run passes neither.
 */
export function loadWorld({ home = os.homedir(), work, task, exploreFn = loadExplore, candidates, quickSeams = {} }) {
  return isQuick(task) ? loadQuickWorld({ home, work, task, exploreFn, candidates, quickSeams }) : loadOlympiadWorld({ home, work, task, exploreFn, candidates });
}

async function loadOlympiadWorld({ home, work, task, exploreFn, candidates }) {
  const explore = exploreFn();
  if (!explore.ok) return explore;
  const snapshots = hostThings({ explore: explore.value, home });
  if (!snapshots.ok) return snapshots;
  const sources = await sourcesFor({ explore: explore.value, task, home, candidates });
  return sources.ok ? ok(worldOf({ explore: explore.value, task, home, snapshots: snapshots.value, sources: sources.value, work })) : sources;
}

/** The snapshots, and the python pin: what `prepare` and the host must hold before anything is scored. */
function hostThings({ explore, home }) {
  const bad = pythonProblem(explore);
  return bad ? fail(bad) : snapshotsOf({ explore, home });
}

/** The task's spec and FORMAT texts at their pins from the task repository this host has (`candidates` replaces the search, for tests). */
const sourcesFor = ({ explore, task, home, candidates }) => loadSources({ task, candidates: candidates ?? candidateRepos({ repoName: explore.specs.repoName, repoRoot: REPO, commonRoot: path.resolve(REPO, '..', '..', 'dew_flow_benchmark'), home }) });

function worldOf({ explore, task, home, snapshots, sources, work }) {
  const paths = pinnedPaths(explore, home);
  const filesFor = stack => workspaceFiles({ explore, stack, sources, typesNode: snapshots.typesNode });
  const defaultCaches = stack => ({ nuget: path.join(snapshots.csharp.dir, 'nuget'), npm: path.join(snapshots.node.dir, 'npm'), stack });
  const scoreFn = args => scoreTree({
    explore, stack: args.stack, source: args.source, files: args.files ?? filesFor(args.stack), sets: args.sets, limits: args.limits, traps: args.traps ?? [], work: args.work ?? work,
    toolchains: { dirs: [paths.nodeDir, paths.tsDir], tsDir: paths.tsDir }, caches: args.caches ?? defaultCaches(args.stack), python: explore.python3.path, operatorHome: home, say: args.say,
  });
  return { explore, task, home, paths, snapshots, sources, filesFor, scoreFn, work };
}

/** The workspace files of both stacks of a quick task. -> `ok({ csharp, node })` (each `{ files, frozen }`) | `fail`. */
function quickBuilt({ explore, task, sources, inputs }) {
  const built = {};
  for (const stack of ['csharp', 'node']) {
    built[stack] = quickWorkspaceFiles({ explore, task, stack, sources, inputs });
    if (!built[stack].ok) return fail(`the ${stack} workspace of task ${task.id}: ${built[stack].reason}`);
  }
  return ok(built);
}

/** The python pin and the v2 snapshots of the quick profile. -> `ok(snapshots)` | `fail`. */
function quickHost({ explore, base, home }) {
  const bad = pythonProblem(explore);
  return bad ? fail(bad) : quickSnapshots({ base, home });
}

/** The task's spec and FORMAT texts and the packages inputs of the workspace. -> `ok({ sources, inputs })` | `fail`. */
async function quickSources({ explore, task, home, candidates }) {
  const sources = await sourcesFor({ explore, task, home, candidates });
  if (!sources.ok) return sources;
  const inputs = packagesInputs();
  return inputs.ok ? ok({ sources: sources.value, inputs: inputs.value }) : inputs;
}

/** What the quick world is made from, loaded in order: the pins, the snapshots, the task sources and the packages inputs. -> `ok({ explore, snapshots, sources, inputs })` | `fail`. */
async function quickParts({ home, task, exploreFn, candidates }) {
  const base = exploreFn();
  if (!base.ok) return base;
  const explore = quickExplore(base.value, home);
  const snapshots = quickHost({ explore, base: base.value, home });
  if (!snapshots.ok) return snapshots;
  const found = await quickSources({ explore, task, home, candidates });
  return found.ok ? ok({ explore, snapshots: snapshots.value, ...found.value }) : found;
}

/**
 * The world of a QUICK task (daemon or web, see quick.mjs): the v2 toolchains and caches, the workspace of lang-bench's templates, and the scoring through the execution layer. Same shape as the olympiad world plus
 * `frozenFor(stack)` (the files the agent must not change), `profile` (what the scoring box needs) and `quick: true`. `quickSeams` (`{ buildFn, runFn }`) is for tests and the stub runner.
 */
async function loadQuickWorld({ home, work, task, exploreFn, candidates, quickSeams }) {
  const parts = await quickParts({ home, task, exploreFn, candidates });
  if (!parts.ok) return parts;
  const { explore, snapshots, sources, inputs } = parts.value;
  const built = quickBuilt({ explore, task, sources, inputs });
  if (!built.ok) return built;
  const paths = quickPaths(explore, home);
  const profile = { explore, paths, snapshots, kind: task.kind, caseWallMs: explore.quick.caseWallMs, runCeilingMs: explore.quick.runCeilingMs };
  const scoreFn = args => scoreQuick({
    stack: args.stack, source: args.source, files: args.files ?? built.value[args.stack].value.frozen, sets: args.sets, limits: args.limits, traps: args.traps ?? [], work: args.work ?? work, profile, say: args.say, driftPossible: args.driftPossible === true, only: args.only, ...quickSeams,
  });
  return ok({
    explore, task, home, paths, snapshots, sources, filesFor: stack => built.value[stack].value.files, frozenFor: stack => built.value[stack].value.frozen, scoreFn, work, profile, quick: true,
  });
}
