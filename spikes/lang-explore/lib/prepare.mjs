// `explore.mjs prepare`: the ONLY step of the pilot that uses the network (plan D-X4). In order: read `typescript@6.0.3`'s integrity from the registry (never the operator's ~/.npm copy: npm gets a fresh cache
// directory), install it read-only through v1's `preflight({ install: true })`, check the installed package-lock carries the same integrity, make the Node cache snapshot in a box behind the proxy with the
// registry host list, re-hash v1's C# snapshot, then calibrate the spin probe on a quiet machine. Each step is skipped when its result is already on disk and still checks out, so a rerun finishes what stopped.
// Failures are values; nothing here reads the token (the preflight's token check looks at a throw-away fake token file).
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { loadRecipe, makeSnapshot, registerSnapshot, snapshotRecord, treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { preflight } from '../../minigit-bench/lib/preflight.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { TOKEN_FILE_PARTS } from '../../minigit-bench/capture/capture-env.mjs';
import { loadExplore, pinnedPaths, stacksOf } from './inputs.mjs';
import { INPUTS, V1, exploreRoot, snapshotWorkDir, spinFile, toolchainsDir, underHome } from './paths.mjs';
import { calibrateSpinIfQuiet, pilotQuiet } from './machine.mjs';

/** The files `prepare` reads and writes; a test hands it copies. */
export const FILES = Object.freeze({ explore: path.join(INPUTS, 'explore.json'), snapshots: path.join(INPUTS, 'cache-snapshots.json'), recipe: path.join(INPUTS, 'cache-recipe.json'), v1Snapshots: path.join(V1, 'inputs', 'cache-snapshots.json') });

const INTEGRITY = /^sha512-[A-Za-z0-9+/]+={0,2}$/;
const NPM_MS = 120_000;
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

/** A directory of this process, named for the test-leftovers rule, removed by the caller. */
const scratch = what => mkdtempSync(path.join(os.tmpdir(), `testsToDelete-lex-${what}-`));

/** `npm view typescript@<version> dist.integrity` with a FRESH npm cache (so the operator's ~/.npm is not consulted). -> `ok('sha512-...')` | `fail`. */
export async function registryIntegrity({ version, cacheDir, run = runBounded }) {
  const r = await run('npm', ['view', `typescript@${version}`, 'dist.integrity'], { timeoutMs: NPM_MS, env: { ...process.env, npm_config_cache: cacheDir } });
  if (!r.ok) return r;
  const text = r.value.stdout.trim();
  return r.value.code === 0 && INTEGRITY.test(text) ? ok(text) : fail(`npm view typescript@${version} dist.integrity printed ${JSON.stringify(text.slice(0, 80))} (exit ${r.value.code}): ${r.value.stderr.slice(-200)}`);
}

/** Write the integrity into `inputs/explore.json` (text replacement, so the file keeps its shape). A recorded value that differs is a fail: a pin never moves silently. */
export function recordIntegrity({ file = FILES.explore, integrity }) {
  const loaded = loadExplore(file);
  if (!loaded.ok) return loaded;
  const recorded = loaded.value.typescript.integrity;
  if (recorded === integrity) return ok({ changed: false });
  if (recorded !== null) return fail(`explore.json records ${recorded.slice(0, 24)}..., the registry says ${integrity.slice(0, 24)}...: a pin is amended by a dated line, never overwritten`);
  return fillIntegrity(file, integrity);
}

function fillIntegrity(file, integrity) {
  const text = readFileSync(file, 'utf8');
  if (!text.includes('"integrity": null')) return fail('explore.json has no `"integrity": null` to fill');
  writeFileSync(file, text.replace('"integrity": null', `"integrity": "${integrity}"`));
  return ok({ changed: true });
}

/** The integrity npm wrote into the toolchain's package-lock for `typescript`, or null. */
export function installedIntegrity(tsDir) {
  try { return readJson(path.join(tsDir, 'package-lock.json')).packages?.['node_modules/typescript']?.integrity ?? null; } catch { return null; }
}

/** A home that holds a fake token file (mode 600 in a 700 directory): the preflight asks whether a token file EXISTS, and the pilot never touches the real one. */
function fakeHome() {
  const home = scratch('home');
  const file = path.join(home, ...TOKEN_FILE_PARTS);
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, 'sk-ant-oat01-not-a-real-token\n', { mode: 0o600 });
  chmodSync(path.dirname(file), 0o700);
  return home;
}

/** Install TypeScript read-only (when absent) through v1's preflight and check the host's pins in a box; then compare the installed integrity with the recorded one. */
export async function installToolchain({ explore, home = os.homedir(), preflightFn = preflight }) {
  const stacks = stacksOf(explore);
  if (!stacks.ok) return stacks;
  const paths = pinnedPaths(explore, home);
  mkdirSync(path.dirname(paths.tsDir), { recursive: true, mode: 0o700 });
  const cache = scratch('npm');
  const fake = fakeHome();
  const before = process.env.npm_config_cache;
  process.env.npm_config_cache = cache; // the install child inherits it: the operator's ~/.npm is not consulted
  try {
    const pre = await preflightFn({ home: fake, nodeDir: paths.nodeDir, tsDir: paths.tsDir, claudeBinary: paths.claudeBinary, stacks: stacks.value, install: true });
    return pre.ok ? checkedIntegrity({ explore, tsDir: paths.tsDir, facts: pre.value.facts }) : fail(`${pre.reason}: ${(pre.problems ?? []).join('; ')}`);
  } finally {
    if (before === undefined) delete process.env.npm_config_cache; else process.env.npm_config_cache = before;
    rmSync(cache, { recursive: true, force: true });
    rmSync(fake, { recursive: true, force: true });
  }
}

/** The installed toolchain's integrity against the recorded one. */
function checkedIntegrity({ explore, tsDir, facts }) {
  const seen = installedIntegrity(tsDir);
  return seen === explore.typescript.integrity ? ok({ facts, integrity: seen }) : fail(`the installed typescript carries integrity ${seen ?? 'none'}, the recorded one is ${explore.typescript.integrity}`);
}

const registeredIn = file => (existsSync(file) ? readJson(file) : {});

/** The directory of a registered snapshot under `toolchains/`: `cache-<stack>-<12 hex>`. */
const snapshotDir = (stack, record, home) => path.join(toolchainsDir(home), `cache-${stack}-${record.treeSha256.slice(0, 12)}`);

/** A registered snapshot on disk, accepted while it still hashes to its record. */
function acceptRegistered({ have, dir }) {
  return treeHash(dir) === have.treeSha256 ? ok({ stack: 'node', record: have, dir, made: false }) : fail(`the registered node snapshot ${dir} no longer hashes to ${have.treeSha256.slice(0, 12)}`);
}

/** Make the Node snapshot (or accept the one already registered and on disk). -> `ok({ stack, record, dir, made })` | `fail`. */
export async function nodeSnapshot({ home = os.homedir(), makeFn = makeSnapshot, files = FILES }) {
  const reused = reuseRegistered({ home, files });
  if (reused) return reused;
  const recipe = loadRecipe(readJson(files.recipe));
  return recipe.ok ? makeAndRegister({ recipe: recipe.value, home, makeFn, files }) : recipe;
}

/** The registered node snapshot when it is on disk (accepted while it still hashes to its record), else null. */
function reuseRegistered({ home, files }) {
  const have = registeredIn(files.snapshots).node;
  const dir = have ? snapshotDir('node', have, home) : null;
  return dir && existsSync(dir) ? acceptRegistered({ have, dir }) : null;
}

/** Run the recipe in a box and register the result by its tree hash. */
async function makeAndRegister({ recipe, home, makeFn, files }) {
  const made = await makeFn({ stack: 'node', recipe, root: snapshotWorkDir(home), out: toolchainsDir(home) });
  if (!made.ok) return made;
  const record = snapshotRecord(made.value);
  const reg = registerSnapshot({ file: files.snapshots, stack: 'node', record, madeOn: new Date().toISOString().slice(0, 10) });
  return reg.ok ? ok({ stack: 'node', record, dir: made.value.dir, made: true }) : reg;
}

/** Re-hash v1's registered C# snapshot and register it in the pilot's own file. -> `ok({ stack, record, dir, made })` | `fail`. */
export function csharpSnapshot({ explore, home = os.homedir(), files = FILES }) {
  const dir = underHome(explore.csharpSnapshot.dirParts, home);
  if (!existsSync(dir)) return fail(`v1's C# snapshot ${dir} is not there`);
  const hash = treeHash(dir);
  if (hash !== explore.csharpSnapshot.treeSha256) return fail(`v1's C# snapshot hashes to ${hash.slice(0, 12)}, explore.json pins ${explore.csharpSnapshot.treeSha256.slice(0, 12)}`);
  const have = registeredIn(files.snapshots).csharp;
  return have ? ok({ stack: 'csharp', record: have, dir, made: false }) : registerCsharp({ files, dir, hash });
}

/** Write v1's C# record, with the re-hash, into the pilot's registered snapshots. */
function registerCsharp({ files, dir, hash }) {
  const { madeOn, ...rest } = readJson(files.v1Snapshots).csharp;
  const reg = registerSnapshot({ file: files.snapshots, stack: 'csharp', record: { ...rest, rehashedOn: new Date().toISOString().slice(0, 10), rehashedTreeSha256: hash }, madeOn });
  return reg.ok ? ok({ stack: 'csharp', record: reg.value.csharp, dir, made: true }) : reg;
}

/** The spin calibration on a quiet machine, written to `~/rsd/lang-v2/explore/spin.json`; a rerun keeps an existing one. -> `ok(spin)` | `fail(reason)` (the machine is not quiet: rerun `prepare`). */
export async function calibrateSpin({ home = os.homedir(), calibrateFn = calibrateSpinIfQuiet }) {
  const file = spinFile(home);
  if (existsSync(file)) return ok(readJson(file));
  const quiet = pilotQuiet();
  if (!quiet.ok) return quiet;
  const done = await calibrateFn({ thresholds: quiet.value, home });
  if (!done.ok) return done;
  mkdirSync(exploreRoot(home), { recursive: true, mode: 0o700 });
  const spin = { ...done.value.calibration, madeOn: new Date().toISOString() };
  writeFileSync(file, `${JSON.stringify(spin, null, 2)}\n`, { mode: 0o600 });
  return ok(spin);
}

// ---- the steps ------------------------------------------------------------------------------------------------------------------

/** Step 1: the registry's integrity of the pinned typescript, recorded in explore.json (or confirmed against the recorded one). */
async function stepIntegrity({ say, deps }) {
  const files = deps.files ?? FILES;
  const explore = loadExplore(files.explore);
  if (!explore.ok) return explore;
  const cacheDir = scratch('view');
  try {
    const seen = await (deps.registryIntegrity ?? registryIntegrity)({ version: explore.value.typescript.version, cacheDir });
    return seen.ok ? recordedStep({ files, version: explore.value.typescript.version, integrity: seen.value, say }) : seen;
  } finally {
    rmSync(cacheDir, { recursive: true, force: true });
  }
}

function recordedStep({ files, version, integrity, say }) {
  const recorded = recordIntegrity({ file: files.explore, integrity });
  if (recorded.ok) say(`typescript@${version} integrity ${integrity.slice(0, 24)}... (${recorded.value.changed ? 'recorded' : 'matches the record'})`);
  return recorded.ok ? ok({ typescriptIntegrity: integrity }) : recorded;
}

/** Step 2: the toolchain installed read-only, its integrity compared, the pins probed in a box. */
async function stepToolchain({ home, say, deps }) {
  const explore = loadExplore((deps.files ?? FILES).explore);
  if (!explore.ok) return explore;
  const installed = await installToolchain({ explore: explore.value, home, preflightFn: deps.preflight });
  if (installed.ok) say(`toolchain installed read-only; the pins hold in the box: ${JSON.stringify(installed.value.facts)}`);
  return installed.ok ? ok({ toolchainFacts: installed.value.facts }) : installed;
}

/** Step 3: the Node snapshot. */
async function stepNode({ home, say, deps }) {
  const node = await nodeSnapshot({ home, makeFn: deps.makeSnapshot, files: deps.files });
  if (node.ok) say(`node snapshot ${node.value.record.treeSha256.slice(0, 12)} (${node.value.made ? 'made' : 'already registered'}); @types/node ${JSON.stringify(node.value.record.resolved)}`);
  return node.ok ? ok({ nodeSnapshotTreeSha256: node.value.record.treeSha256, nodeSnapshotResolved: node.value.record.resolved }) : node;
}

/** Step 4: v1's C# snapshot, re-hashed. */
function stepCsharp({ home, say, deps }) {
  const files = deps.files ?? FILES;
  const explore = loadExplore(files.explore);
  if (!explore.ok) return explore;
  const csharp = csharpSnapshot({ explore: explore.value, home, files });
  if (csharp.ok) say(`csharp snapshot ${csharp.value.record.treeSha256.slice(0, 12)} re-hashed: it is v1's registered one`);
  return csharp.ok ? ok({ csharpSnapshotTreeSha256: csharp.value.record.treeSha256 }) : csharp;
}

/** Step 5: the spin calibration (the only step that needs a quiet machine). */
async function stepSpin({ home, say, deps }) {
  const spin = await calibrateSpin({ home, calibrateFn: deps.calibrateSpin });
  if (spin.ok) say(`spin probe calibrated: median ${spin.value.medianMs} ms over ${spin.value.runs} runs`);
  return spin.ok ? ok({ spin: spin.value }) : spin;
}

const STEPS = Object.freeze([['integrity', stepIntegrity], ['toolchain', stepToolchain], ['node snapshot', stepNode], ['csharp snapshot', stepCsharp], ['spin', stepSpin]]);

/**
 * All the steps in order, saying each. -> `ok(facts)` when every one is done, else `fail(sentence)` naming the step; a failed spin step (the machine was not quiet) is `fail` with `resumable: true`
 * and `facts` holding everything before it: rerun `prepare` once the machine is quiet.
 */
export async function prepare({ home = os.homedir(), say = () => {}, deps = {} }) {
  const facts = {};
  for (const [name, step] of STEPS) {
    const r = await step({ home, say, deps });
    if (!r.ok) return fail(`${name}: ${r.reason}`, { step: name, facts, resumable: name === 'spin' });
    Object.assign(facts, r.value);
  }
  return ok(facts);
}
