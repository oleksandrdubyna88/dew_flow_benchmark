#!/usr/bin/env node
// E1.S4: make, register, supersede and prune the two cache snapshots of the language comparison v2 (parent D5). The recipe is `inputs/cache-recipe-lang.json`; each snapshot is made ONCE per stack inside v1's agent
// box (E1.S5's toolchains bound read-only, the distribution's dotnet hidden), through the egress proxy with the bare host list, hashed, made read-only and registered in `inputs/cache-snapshots-lang.json`.
//
//   node spikes/lang-bench/make-snapshots.mjs [--stack csharp] [--stack node]       build and register (default: both, csharp first)
//   node spikes/lang-bench/make-snapshots.mjs --supersede <stack> --reason "<text>"  move a registered record to superseded[] (before the pre-registration tag only)
//   node spikes/lang-bench/make-snapshots.mjs --prune-superseded                     remove superseded snapshot folders (after the pre-registration tag only)
//     --caches <dir>     default ~/rsd/lang-v2/caches (refused inside a git worktree)     --record <file>   default inputs/cache-snapshots-lang.json
//     --recipe <file>    default inputs/cache-recipe-lang.json                           --stacks <file>   default inputs/stacks-lang.json
//     --work <dir>       parent of the build's owned work folder, default ~/.cache/lang-v2/w
//     --gate-lock <file> default ~/.cache/lang-v2/gate.lock
// Exit 0 done · 1 a build failed (named on stderr) · 2 usage (or a caches folder inside a git worktree) · 3 refused (gate lock held, already registered, preflight, tag rule, claim exists)
//
// A build takes the gate lock first, sweeps what a killed earlier run left (the owned work folders, then its staging folders, each by the conventions' owner rules), refuses a stack that is already registered BEFORE any
// build, runs the preflight, and makes one snapshot per stack. A failed build registers nothing. Every build prints `egress <stack>: <JSON>`: the hosts the proxy allowed and refused, from the work folder's log.
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { cleanupScope, ownedFor, removeClaimIfStill, writeOwner } from '../../.agents/conventions/tools/lib/owned.mjs';
import { sweepLeftovers } from '../../.agents/conventions/tools/lib/leftovers-sweep.mjs';
import { makeSnapshot, registerSnapshot, treeHash } from '../minigit-bench/lib/cache-snapshot.mjs';
import { egressSummary } from '../minigit-bench/lib/egress.mjs';
import { refuseInsideWorktree } from '../minigit-bench/lib/git-guard.mjs';
import { fail, ok } from '../minigit-bench/lib/result.mjs';
import { loadRecipeLang } from './lib/cache-recipe-lang.mjs';
import { boxOptionsFor } from './lib/cache-offline.mjs';
import { buildRecord, checkManifestMatches, manifestOf, pruneSuperseded, supersede, sweepStaging, tagsOf } from './lib/cache-store.mjs';
import { takeGateLock } from './lib/gate-lock.mjs';
import { storeDirs } from './lib/preflight-box.mjs';
import { preflightLang } from './lib/preflight-lang.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';

const SPIKE = fileURLToPath(new URL('.', import.meta.url));
const REPO = path.resolve(SPIKE, '..', '..');
const STACK_NAMES = Object.freeze(['csharp', 'node']);
const EXIT = Object.freeze({ done: 0, failed: 1, usage: 2, refused: 3 });
const OPTIONS = {
  stack: { type: 'string', multiple: true }, supersede: { type: 'string' }, reason: { type: 'string' }, 'prune-superseded': { type: 'boolean' },
  caches: { type: 'string' }, record: { type: 'string' }, recipe: { type: 'string' }, stacks: { type: 'string' }, work: { type: 'string' }, 'gate-lock': { type: 'string' },
};
const say = line => process.stdout.write(`${line}\n`);
const complain = line => process.stderr.write(`${line}\n`);
const stop = (code, line) => { complain(line); return code; };

// ---- the arguments ---------------------------------------------------------------------------------------------------------------

const MODE_TESTS = Object.freeze([
  ['supersede', v => v.supersede !== undefined], ['prune', v => v['prune-superseded'] === true], ['build', v => v.stack !== undefined],
]);
const modeOf = v => MODE_TESTS.filter(([, isAsked]) => isAsked(v)).map(([name]) => name);
/** Every stack name the flags carry that is not a legal one (the stacks of `--stack`). */
const unknownStacks = v => (v.stack ?? []).filter(s => !STACK_NAMES.includes(s));

/** The flags with their defaults (every folder the command touches is a flag, so a test can point all of them elsewhere). */
function configOf(v, mode) {
  const home = os.homedir();
  const at = (value, ...fallback) => path.resolve(value ?? path.join(...fallback));
  return {
    mode, stackNames: v.stack ?? [...STACK_NAMES], supersede: v.supersede, reason: v.reason,
    caches: at(v.caches, home, 'rsd', 'lang-v2', 'caches'), record: at(v.record, SPIKE, 'inputs', 'cache-snapshots-lang.json'), recipe: at(v.recipe, SPIKE, 'inputs', 'cache-recipe-lang.json'),
    stacks: at(v.stacks, SPIKE, 'inputs', 'stacks-lang.json'), work: at(v.work, home, '.cache', 'lang-v2', 'w'), gateLock: at(v['gate-lock'], home, '.cache', 'lang-v2', 'gate.lock'),
  };
}

/** `{ config }` or `{ error }`: the flags, their defaults, and the one mode they ask for. */
function readArgs(argv) {
  let v;
  try { v = parseArgs({ args: argv, options: OPTIONS, strict: true }).values; } catch (e) { return { error: e.message }; }
  const modes = modeOf(v);
  const unknown = unknownStacks(v);
  if (modes.length > 1) return { error: 'ask for one of: a build (--stack), --supersede <stack> --reason <text>, or --prune-superseded' };
  return unknown.length > 0 ? { error: `--stack names ${unknown.join(', ')}; the legal names are ${STACK_NAMES.join(', ')}` } : { config: configOf(v, modes[0] ?? 'build') };
}

/** The recipe (with the hash of its bytes) and the toolchain pins, checked against each other. -> `ok({ recipe, recipeSha256, stacks })` | `fail`. */
function loadInputs(config) {
  const stacks = loadStacksLang(JSON.parse(readFileSync(config.stacks, 'utf8')));
  if (!stacks.ok) return stacks;
  const bytes = readFileSync(config.recipe);
  const recipe = loadRecipeLang(JSON.parse(bytes.toString('utf8')), stacks.value);
  return recipe.ok ? ok({ recipe: recipe.value, recipeSha256: createHash('sha256').update(bytes).digest('hex'), stacks: stacks.value }) : recipe;
}

// ---- one stack ---------------------------------------------------------------------------------------------------------------

/** Claim the staging folder v1's maker will use (`.building-<stack>-<pid>`): the claim is written BEFORE the maker runs, so a killed build leaves something the next sweep can judge. */
function claimStaging(caches, stack) {
  const dir = path.join(caches, `.building-${stack}-${process.pid}`);
  try {
    return ok({ dir, written: writeOwner(dir) });
  } catch (error) {
    if (error.code === 'EEXIST') return fail(`the staging claim ${dir}.owner already exists: another run may be building, or an earlier one was killed (look at it, remove it by hand when no build is going)`);
    throw error;
  }
}

/** `egress <stack>: <JSON>` from the work layout's log (`<work folder>/snapshot-<stack>-1/egress.ndjson`); a log that cannot be read is said, never left out. */
function egressLine(workDir, stack) {
  const file = path.join(workDir, `snapshot-${stack}-1`, 'egress.ndjson');
  if (!existsSync(file)) return `egress ${stack}: no log at ${file}`;
  const lines = readFileSync(file, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  return `egress ${stack}: ${JSON.stringify(egressSummary(lines))}`;
}

const seedOf = spec => (spec.project === undefined ? null : path.join(SPIKE, spec.project));

/** The manifest of the made snapshot, checked against the maker's own list, and the record built from both. -> `ok(record)` | `fail`. */
function recordOf({ made, inputs, spec }) {
  const manifest = manifestOf(made.dir);
  if (!manifest.ok) return manifest;
  const matched = checkManifestMatches(manifest.value, made.packages);
  if (!matched.ok) return matched;
  const seed = seedOf(spec);
  return ok(buildRecord({ snapshot: made, manifest: manifest.value, recipeSha256: inputs.recipeSha256, projectSha256: seed === null ? null : treeHash(seed) }));
}

function register({ config, stack, record }) {
  const registered = registerSnapshot({ file: config.record, stack, record, madeOn: new Date().toISOString().slice(0, 10) });
  if (registered.ok) say(`registered ${stack} ${record.dirName} (${record.sizeBytes} bytes, ${record.packages.length} packages)`);
  return registered.ok ? ok(record) : registered;
}

/** Make one snapshot, its record, and register it. -> `ok(record)` | `fail`; the egress line is printed either way. */
async function buildOne({ stack, config, inputs, dirs, workDir }) {
  const spec = inputs.recipe.stacks[stack];
  const made = await makeSnapshot({ stack, recipe: inputs.recipe, root: workDir, out: config.caches, nodeDir: dirs.node, box: boxOptionsFor(dirs), workSeed: seedOf(spec), dirName: (s, hash) => `${s}-${hash}` });
  say(egressLine(workDir, stack));
  if (!made.ok) return made;
  const record = recordOf({ made: made.value, inputs, spec });
  return record.ok ? register({ config, stack, record: record.value }) : record;
}

/** The wall time of one stack's build, said after the egress line (the docs record it). */
const timed = async run => { const started = Date.now(); const result = await run(); return { result, seconds: Math.round((Date.now() - started) / 1000) }; };

async function buildStack(args) {
  const claim = claimStaging(args.config.caches, args.stack);
  if (!claim.ok) return stop(EXIT.refused, claim.reason);
  try {
    const { result, seconds } = await timed(() => buildOne(args));
    if (result.ok) say(`built ${args.stack} in ${seconds} s`);
    return result.ok ? EXIT.done : stop(EXIT.failed, `${args.stack}: ${result.reason}`);
  } finally {
    removeClaimIfStill(claim.value.dir, claim.value.written);
  }
}

// ---- the build -------------------------------------------------------------------------------------------------------------------

/** The folders a build writes in, swept first: the work root (the conventions' own sweep, token `bench`), then the caches root's staging folders. */
function prepareFolders(config) {
  mkdirSync(config.work, { recursive: true, mode: 0o700 });
  mkdirSync(config.caches, { recursive: true, mode: 0o700 });
  const swept = sweepLeftovers({ repo: REPO, tmp: config.work, token: 'bench' });
  complain(`leftovers in ${config.work}: removed ${swept.removed.length}, preserved ${swept.preserved.length}${swept.errors.length > 0 ? `, ${swept.errors.length} error(s): ${swept.errors.slice(0, 3).join('; ')}` : ''}`);
  sweepStaging({ caches: config.caches, log: complain });
}

/** The requested stacks that are already registered are refused BEFORE any build (exit code), else `null`. */
function registeredRefusal(config) {
  const registered = JSON.parse(existsSync(config.record) ? readFileSync(config.record, 'utf8') : '{}');
  const taken = config.stackNames.filter(name => registered[name] !== undefined);
  return taken.length === 0 ? null : stop(EXIT.refused, `${taken.join(', ')} ${taken.length === 1 ? 'is' : 'are'} already registered in ${config.record}: a registered snapshot is never replaced (supersede it first, before the pre-registration tag)`);
}

async function preflightRefusal(inputs, storeRoot) {
  const pre = await preflightLang({ stacks: inputs.stacks, storeRoot });
  if (pre.ok) return null;
  pre.problems.forEach(complain);
  return stop(EXIT.refused, `${pre.reason}: no snapshot is made`);
}

/** One owned work folder (closed at the end), one snapshot per requested stack in order; the first failure stops the run. */
async function buildAll({ config, inputs, storeRoot }) {
  const scope = cleanupScope();
  try {
    const workDir = ownedFor({ token: 'bench' }).ownedDir(scope, 'snapw', { parent: config.work });
    for (const stack of config.stackNames) {
      const code = await buildStack({ stack, config, inputs, dirs: storeDirs(inputs.stacks, storeRoot), workDir });
      if (code !== EXIT.done) return code;
    }
    return EXIT.done;
  } finally {
    await scope.close();
  }
}

async function buildLocked(config, inputs) {
  prepareFolders(config);
  const storeRoot = path.join(os.homedir(), ...inputs.stacks.storeRoot);
  const refused = registeredRefusal(config) ?? await preflightRefusal(inputs, storeRoot);
  return refused ?? buildAll({ config, inputs, storeRoot });
}

async function build(config, inputs) {
  const lock = takeGateLock({ file: config.gateLock });
  if (!lock.ok) return stop(EXIT.refused, lock.reason);
  try {
    return await buildLocked(config, inputs);
  } finally {
    lock.value.release();
  }
}

// ---- supersede and prune ---------------------------------------------------------------------------------------------------------

function supersedeMode(config) {
  const done = supersede({ file: config.record, stack: config.supersede, reason: config.reason, tagsOf: () => tagsOf(REPO) });
  if (!done.ok) return stop(EXIT.refused, done.reason);
  say(`superseded ${done.value.stack} on ${done.value.supersededOn}: its record moved to superseded[] in ${config.record}`);
  return EXIT.done;
}

function printPruned({ removed, kept }) {
  for (const r of removed) say(`removed ${r.name} (superseded ${r.supersededOn}: ${r.reason})`);
  for (const k of kept) say(`kept ${k.name}: ${k.why}`);
}

function pruneMode(config) {
  if (!existsSync(config.caches)) return stop(EXIT.refused, `there is no caches folder at ${config.caches}: nothing to prune`);
  const pruned = pruneSuperseded({ file: config.record, caches: config.caches, tagsOf: () => tagsOf(REPO) });
  if (!pruned.ok) return stop(EXIT.refused, pruned.reason);
  printPruned(pruned.value);
  return EXIT.done;
}

// ---- main --------------------------------------------------------------------------------------------------------------------------

/** The modes that work on the caches folder: refused inside a git worktree, then prune or build. */
function cachesModes(config) {
  const safe = refuseInsideWorktree(config.caches, 'the caches folder');
  if (!safe.ok) return stop(EXIT.usage, safe.reason);
  if (config.mode === 'prune') return pruneMode(config);
  const inputs = loadInputs(config);
  return inputs.ok ? build(config, inputs.value) : stop(EXIT.usage, inputs.reason);
}

async function main(argv) {
  const parsed = readArgs(argv);
  if (parsed.error) return stop(EXIT.usage, parsed.error);
  return parsed.config.mode === 'supersede' ? supersedeMode(parsed.config) : cachesModes(parsed.config);
}

process.exitCode = await main(process.argv.slice(2));
