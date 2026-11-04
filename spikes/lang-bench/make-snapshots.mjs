#!/usr/bin/env node
// E1.S4: make, register, supersede and prune the two cache snapshots of the language comparison v2 (parent D5). The recipe is `inputs/cache-recipe-lang.json`; each snapshot is made ONCE per stack inside v1's agent
// box (E1.S5's toolchains bound read-only, the distribution's dotnet hidden), through the egress proxy with the bare host list, hashed, made read-only and registered in `inputs/cache-snapshots-lang.json`.
//
//   node spikes/lang-bench/make-snapshots.mjs [--stack csharp] [--stack node]       build and register (default: both, csharp first)
//   node spikes/lang-bench/make-snapshots.mjs --supersede <stack> --reason "<text>"  move a registered record to superseded[] (before the pre-registration tag only)
//   node spikes/lang-bench/make-snapshots.mjs --prune-superseded                     remove superseded snapshot folders (after the pre-registration tag only)
//   node spikes/lang-bench/make-snapshots.mjs --print-packages-md <stack>            print the stack's PACKAGES.md, derived from the record
//     --caches <dir>     default ~/rsd/lang-v2/caches (refused inside a git worktree)     --record <file>   default inputs/cache-snapshots-lang.json
//     --recipe <file>    default inputs/cache-recipe-lang.json                           --stacks <file>   default inputs/stacks-lang.json
//     --work <dir>       parent of the build's owned work folder, default ~/.cache/lang-v2/w
//     --gate-lock <file> default ~/.cache/lang-v2/gate.lock
//     --tags-remote <url> where supersede and prune look for the pre-registration tag besides the local tags (default: origin's url, a GitHub SSH form read over https; no remote is a refusal)
// Exit 0 done · 1 a build failed (named on stderr) · 2 usage (or a caches folder inside a git worktree) · 3 refused (gate lock held, already registered, preflight, tag rule, claim exists)
//
// A build, a supersede and a prune each take the gate lock first (released in `finally`). A build sweeps what a killed earlier run left (the owned work folders, then its staging folders, each by the conventions' owner rules), refuses a stack that is already registered BEFORE any
// build, runs the preflight, and makes one snapshot per stack. A failed build registers nothing. Every build prints `egress <stack>: <JSON>`: the hosts the proxy allowed and refused, from the work folder's log.
// Lowered before anything else runs (common.local-cpu-budget), as run-tests.mjs does: the build and its children never compete with the person at the machine.
await import('./lib/lower-priority.mjs');

import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { cleanupScope, ownedFor, removeClaimIfStill, writeOwner } from '../../.agents/conventions/tools/lib/owned.mjs';
import { sweepLeftovers } from '../../.agents/conventions/tools/lib/leftovers-sweep.mjs';
import { makeSnapshot, treeHash } from '../minigit-bench/lib/cache-snapshot.mjs';
import { egressSummary } from '../minigit-bench/lib/egress.mjs';
import { refuseInsideWorktree } from '../minigit-bench/lib/git-guard.mjs';
import { fail, ok } from '../minigit-bench/lib/result.mjs';
import { loadRecipeLang } from './lib/cache-recipe-lang.mjs';
import { boxOptionsFor } from './lib/cache-offline.mjs';
import { pruneSuperseded, recordAtTag, registerBuilt, supersede, sweepStaging, tagsOf } from './lib/cache-store.mjs';
import { takeGateLock } from './lib/gate-lock.mjs';
import { storeDirs } from './lib/preflight-box.mjs';
import { preflightLang } from './lib/preflight-lang.mjs';
import { renderPackagesMd } from './lib/packages-md.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';

const SPIKE = fileURLToPath(new URL('.', import.meta.url));
const REPO = path.resolve(SPIKE, '..', '..');
const STACK_NAMES = Object.freeze(['csharp', 'node']);
const EXIT = Object.freeze({ done: 0, failed: 1, usage: 2, refused: 3 });
const OPTIONS = {
  stack: { type: 'string', multiple: true }, supersede: { type: 'string' }, reason: { type: 'string' }, 'prune-superseded': { type: 'boolean' }, 'print-packages-md': { type: 'string' }, 'tags-remote': { type: 'string' },
  caches: { type: 'string' }, record: { type: 'string' }, recipe: { type: 'string' }, stacks: { type: 'string' }, work: { type: 'string' }, 'gate-lock': { type: 'string' },
};
const say = line => process.stdout.write(`${line}\n`);
const complain = line => process.stderr.write(`${line}\n`);
const stop = (code, line) => { complain(line); return code; };

// ---- the arguments ---------------------------------------------------------------------------------------------------------------

const MODE_TESTS = Object.freeze([
  ['supersede', v => v.supersede !== undefined], ['prune', v => v['prune-superseded'] === true], ['print', v => v['print-packages-md'] !== undefined], ['build', v => v.stack !== undefined],
]);
const modeOf = v => MODE_TESTS.filter(([, isAsked]) => isAsked(v)).map(([name]) => name);
/** Every stack name the flags carry that is not a legal one (the stacks of `--stack` and the one of `--print-packages-md`). */
const unknownStacks = v => [...(v.stack ?? []), v['print-packages-md']].filter(s => s !== undefined && !STACK_NAMES.includes(s));

/** The flags with their defaults (every folder the command touches is a flag, so a test can point all of them elsewhere). */
function configOf(v, mode) {
  const home = os.homedir();
  const at = (value, ...fallback) => path.resolve(value ?? path.join(...fallback));
  return {
    mode, stackNames: v.stack ?? [...STACK_NAMES], supersede: v.supersede, reason: v.reason, print: v['print-packages-md'], tagsRemote: v['tags-remote'],
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
  if (modes.length > 1) return { error: 'ask for one of: a build (--stack), --supersede <stack> --reason <text>, --prune-superseded, or --print-packages-md <stack>' };
  return unknown.length > 0 ? { error: `--stack names ${unknown.join(', ')}; the legal names are ${STACK_NAMES.join(', ')}` } : { config: configOf(v, modes[0] ?? 'build') };
}

/** A flag's JSON file: `ok({ bytes, json })` | `fail` with ONE line naming the flag and the file (missing, unreadable or not JSON). */
function readJsonFlag(flag, file) {
  try {
    const bytes = readFileSync(file);
    return ok({ bytes, json: JSON.parse(bytes.toString('utf8')) });
  } catch (error) {
    return fail(`${flag} ${file}: ${error.message.replace(/\s+/g, ' ').slice(0, 300)}`);
  }
}

/** Both input files, or the first refusal. */
function readInputFiles(config) {
  const [stacksFile, recipeFile] = [readJsonFlag('--stacks', config.stacks), readJsonFlag('--recipe', config.recipe)];
  return stacksFile.ok ? (recipeFile.ok ? ok({ stacksFile: stacksFile.value, recipeFile: recipeFile.value }) : recipeFile) : stacksFile;
}

/** The recipe (with the hash of its bytes) and the toolchain pins, checked against each other. -> `ok({ recipe, recipeSha256, stacks })` | `fail`. */
function loadInputs(config) {
  const files = readInputFiles(config);
  if (!files.ok) return files;
  const { stacksFile, recipeFile } = files.value;
  const stacks = loadStacksLang(stacksFile.json);
  const recipe = stacks.ok ? loadRecipeLang(recipeFile.json, stacks.value) : stacks;
  return recipe.ok ? ok({ recipe: recipe.value, recipeSha256: createHash('sha256').update(recipeFile.bytes).digest('hex'), stacks: stacks.value }) : recipe;
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

/** Make one snapshot, its record, and register it (a failure after the maker removes the made folder, see `registerBuilt`). -> `ok(record)` | `fail`; the egress line is printed either way. */
async function buildOne({ stack, config, inputs, dirs, workDir }) {
  const spec = inputs.recipe.stacks[stack];
  const made = await makeSnapshot({ stack, recipe: inputs.recipe, root: workDir, out: config.caches, nodeDir: dirs.node, box: boxOptionsFor(dirs), workSeed: seedOf(spec), dirName: (s, hash) => `${s}-${hash}` });
  say(egressLine(workDir, stack));
  if (!made.ok) return made;
  const seed = seedOf(spec);
  const record = registerBuilt({ made: made.value, stack, recipeSha256: inputs.recipeSha256, projectSha256: seed === null ? null : treeHash(seed), file: config.record, madeOn: new Date().toISOString().slice(0, 10) });
  if (record.ok) say(`registered ${stack} ${record.value.dirName} (${record.value.sizeBytes} bytes, ${record.value.packages.length} packages)`);
  return record;
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

/** The record as the sweep needs it: `{}` when there is none yet, the parsed record, or `undefined` when it cannot be read (then no snapshot folder is judged). */
function recordsForSweep(file) {
  try { return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : {}; } catch { return undefined; }
}

/** The folders a build writes in, swept first: the work root (the conventions' own sweep, token `bench`), then the caches root's staging folders. */
function prepareFolders(config) {
  mkdirSync(config.work, { recursive: true, mode: 0o700 });
  mkdirSync(config.caches, { recursive: true, mode: 0o700 });
  const swept = sweepLeftovers({ repo: REPO, tmp: config.work, token: 'bench' });
  complain(`leftovers in ${config.work}: removed ${swept.removed.length}, preserved ${swept.preserved.length}${swept.errors.length > 0 ? `, ${swept.errors.length} error(s): ${swept.errors.slice(0, 3).join('; ')}` : ''}`);
  sweepStaging({ caches: config.caches, log: complain, records: recordsForSweep(config.record) });
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

/** Every mode that changes the record or the caches (a build, supersede, prune) runs under the one gate lock, released in `finally`: a build never meets a supersede or a prune half way. */
async function underGateLock(config, run) {
  const lock = takeGateLock({ file: config.gateLock });
  if (!lock.ok) return stop(EXIT.refused, lock.reason);
  try {
    return await run();
  } finally {
    lock.value.release();
  }
}

// ---- supersede and prune ---------------------------------------------------------------------------------------------------------

function supersedeMode(config) {
  const done = supersede({ file: config.record, stack: config.supersede, reason: config.reason, tagsOf: () => tagsOf(REPO, { remote: config.tagsRemote }) });
  if (!done.ok) return stop(EXIT.refused, done.reason);
  say(`superseded ${done.value.stack} on ${done.value.supersededOn}: its record moved to superseded[] in ${config.record}`);
  return EXIT.done;
}

function printPruned({ removed, kept, failed }) {
  for (const f of failed) complain(`could not remove ${f.name}: ${f.why}`);
  for (const r of removed) say(`removed ${r.name} (superseded ${r.supersededOn}: ${r.reason})`);
  for (const k of kept) say(`kept ${k.name}: ${k.why}`);
}

function pruneMode(config) {
  if (!existsSync(config.caches)) return stop(EXIT.refused, `there is no caches folder at ${config.caches}: nothing to prune`);
  const pruned = pruneSuperseded({ caches: config.caches, tagsOf: () => tagsOf(REPO, { remote: config.tagsRemote }), recordAtTag: tag => recordAtTag(REPO, tag), log: complain });
  if (!pruned.ok) return stop(EXIT.refused, pruned.reason);
  printPruned(pruned.value);
  return pruned.value.failed.length === 0 ? EXIT.done : EXIT.failed;
}

function printMode(config) {
  const inputs = loadInputs(config);
  if (!inputs.ok) return stop(EXIT.usage, inputs.reason);
  const rendered = renderPackagesMd({ stack: config.print, record: JSON.parse(existsSync(config.record) ? readFileSync(config.record, 'utf8') : '{}'), recipe: inputs.value.recipe });
  if (!rendered.ok) return stop(EXIT.refused, rendered.reason);
  process.stdout.write(rendered.value);
  return EXIT.done;
}

// ---- main --------------------------------------------------------------------------------------------------------------------------

/** The modes that work on the caches folder: refused inside a git worktree, then prune or build. */
function cachesModes(config) {
  const safe = refuseInsideWorktree(config.caches, 'the caches folder');
  if (!safe.ok) return stop(EXIT.usage, safe.reason);
  if (config.mode === 'prune') return underGateLock(config, () => pruneMode(config));
  const inputs = loadInputs(config);
  return inputs.ok ? underGateLock(config, () => buildLocked(config, inputs.value)) : stop(EXIT.usage, inputs.reason);
}

async function main(argv) {
  const parsed = readArgs(argv);
  if (parsed.error) return stop(EXIT.usage, parsed.error);
  const { config } = parsed;
  if (config.mode === 'supersede') return underGateLock(config, () => supersedeMode(config));
  return (config.mode === 'print' ? printMode : cachesModes)(config);
}

process.exitCode = await main(process.argv.slice(2));
