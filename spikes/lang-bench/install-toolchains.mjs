#!/usr/bin/env node
// E1.S5: install the pinned toolchains into the private store, or read the store back.
//
//   node spikes/lang-bench/install-toolchains.mjs [--only dotnetSdk,node,typescript] [--record]     install what is absent (downloads: only the official hosts)
//   node spikes/lang-bench/install-toolchains.mjs --verify [--deep]                                 read the store back (--deep: also every tree hash)
//   --store <dir>   the store (default ~/rsd/lang-v2/toolchains)    --stacks <file>   the pins (default inputs/stacks-lang.json)
//
// Exit 0 done / verified · 1 something is wrong (named on stderr) · 2 usage, or a store inside a git worktree.
// `--record` writes the tree hash of every directory this run installed into inputs/stacks-lang.json (a hash already recorded is never moved).
import { readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { installToolchain, TOOLCHAIN_NAMES } from './lib/toolchain-install.mjs';
import { INSTALL_UMASK, realIo } from './lib/toolchain-io.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';
import { checkStoreRoot } from './lib/toolchain-store.mjs';
import { recordTrees, verifyStore } from './lib/toolchain-verify.mjs';

process.umask(INSTALL_UMASK); // the command, not the library, may change its own umask: once, before any async work (the children are also wrapped by lib/toolchain-io.mjs)

const OPTIONS = { only: { type: 'string' }, record: { type: 'boolean' }, verify: { type: 'boolean' }, deep: { type: 'boolean' }, store: { type: 'string' }, stacks: { type: 'string' } };
const EXIT = Object.freeze({ done: 0, problem: 1, usage: 2 });
const DEFAULT_STACKS = fileURLToPath(new URL('./inputs/stacks-lang.json', import.meta.url));
const say = line => process.stdout.write(`${line}\n`);
const complain = line => process.stderr.write(`${line}\n`);

function readArgs(argv) {
  try {
    return { args: parseArgs({ args: argv, options: OPTIONS, strict: true }).values };
  } catch (e) {
    return { error: e.message };
  }
}

async function install({ stacks, stacksFile, storeRoot, only, record }) {
  const owned = await ownedHelper();
  if (!owned.ok) return owned.reason;
  const scope = owned.scope;
  try {
    const trees = {};
    for (const name of only) {
      const done = await installToolchain({ name, stacks, storeRoot, io: realIo, owned: owned.helper, log: complain, progress: complain, newFolder: (what, parent) => owned.helper.ownedDir(scope, what, { parent }) });
      if (!done.ok) return `${name}: ${done.reason}`;
      say(JSON.stringify({ name, state: done.value.state, dir: done.value.dir, treeSha256: done.value.treeSha256 }));
      if (done.value.treeSha256) trees[name] = done.value.treeSha256;
    }
    return record ? recordInto(stacksFile, trees) : null;
  } finally {
    await scope.close();
  }
}

function recordInto(stacksFile, trees) {
  if (Object.keys(trees).length === 0) return null;
  complain(`recording ${Object.keys(trees).join(', ')} tree hash(es) in ${stacksFile}`);
  const next = recordTrees(readFileSync(stacksFile, 'utf8'), trees);
  if (!next.ok) return next.reason;
  writeFileSync(stacksFile, next.value);
  return null;
}

async function ownedHelper() {
  const url = new URL('../../.agents/conventions/tools/lib/owned.mjs', import.meta.url);
  try {
    const owned = await import(url.href);
    return { ok: true, helper: owned.ownedFor({ token: 'bench' }), scope: owned.cleanupScope() };
  } catch (e) {
    return { ok: false, reason: `the conventions mount's owned-name helper is needed for the scratch folders and cannot be loaded (${e.code ?? e.message}): ${fileURLToPath(url)}` };
  }
}

function chooseOnly(value) {
  const names = value === undefined ? [...TOOLCHAIN_NAMES] : value.split(',');
  const unknown = names.filter(n => !TOOLCHAIN_NAMES.includes(n));
  return unknown.length > 0 ? { error: `--only names ${unknown.join(', ')}; the legal names are ${TOOLCHAIN_NAMES.join(', ')}` } : { names };
}

async function main(argv) {
  const parsed = readArgs(argv);
  if (parsed.error) return fail(EXIT.usage, parsed.error);
  const { args } = parsed;
  const stacksFile = path.resolve(args.stacks ?? DEFAULT_STACKS);
  const stacks = loadStacksLang(JSON.parse(readFileSync(stacksFile, 'utf8')));
  if (!stacks.ok) return fail(EXIT.usage, stacks.reason);
  const storeRoot = path.resolve(args.store ?? path.join(os.homedir(), ...stacks.value.storeRoot));
  const safe = checkStoreRoot(storeRoot);
  if (!safe.ok) return fail(EXIT.usage, safe.reason);
  if (args.verify) return verify({ stacks: stacks.value, storeRoot, deep: args.deep === true });
  const only = chooseOnly(args.only);
  if (only.error) return fail(EXIT.usage, only.error);
  const problem = await install({ stacks: stacks.value, stacksFile, storeRoot, only: only.names, record: args.record === true });
  return problem ? fail(EXIT.problem, problem) : EXIT.done;
}

function verify({ stacks, storeRoot, deep }) {
  const result = verifyStore({ stacks, storeRoot, deep });
  for (const problem of result.problems) complain(problem);
  if (result.ok) say(`the store at ${storeRoot} verifies${deep ? ' (every tree hash too)' : ''}`);
  return result.ok ? EXIT.done : EXIT.problem;
}

function fail(code, line) {
  complain(line);
  return code;
}

process.exitCode = await main(process.argv.slice(2));
