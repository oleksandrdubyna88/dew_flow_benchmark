#!/usr/bin/env node
// E1.S3: the workspace tools of the language comparison v2.
//
//   node spikes/lang-bench/build-workspaces.mjs --make-locks [--force-locks] [--caches <dir>]
//     Generates the `package-lock.json` of each Node template, offline, in the build box of the registered Node snapshot, and checks it against the manifest. Absent: written. Equal to the committed file: `unchanged`.
//     Different: refused (exit 3) unless --force-locks.
//
// Exit 0 done · 1 a box command failed · 2 usage · 3 refused (a lock that differs, a lock outside the manifest)
// Lowered before anything else runs (common.local-cpu-budget), as run-tests.mjs does.
await import('./lib/lower-priority.mjs');

import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { cleanupScope, ownedFor } from '../../.agents/conventions/tools/lib/owned.mjs';
import { endTrackedChildren } from './lib/exec-layer/cleanup.mjs';
import { storeDirs } from './lib/preflight-box.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';
import { defaultCaches, inBuildBox } from './lib/workspace-box.mjs';
import { lockProblems } from './lib/workspace-build.mjs';

const SPIKE = fileURLToPath(new URL('.', import.meta.url));
const EXIT = Object.freeze({ done: 0, failed: 1, usage: 2, refused: 3 });
const KINDS = Object.freeze(['olympiad', 'daemon', 'web']);
const OPTIONS = { 'make-locks': { type: 'boolean' }, 'force-locks': { type: 'boolean' }, caches: { type: 'string' } };
const LOCK_COMMAND = Object.freeze(['npm', 'install', '--package-lock-only', '--offline', '--ignore-scripts', '--no-audit', '--no-fund']);
const say = line => process.stdout.write(`${line}\n`);
const complain = line => process.stderr.write(`${line}\n`);
const stop = (code, line) => { complain(line); return code; };
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

let signalled = null;
for (const [signal, code] of [['SIGINT', 130], ['SIGTERM', 143]]) {
  process.on(signal, () => { signalled = code; endTrackedChildren(); });
}

/** `{ config }` or `{ error }`. */
function readArgs(argv) {
  let v;
  try { v = parseArgs({ args: argv, options: OPTIONS, strict: true }).values; } catch (e) { return { error: e.message }; }
  if (v['make-locks'] !== true) return { error: 'ask for --make-locks' };
  return { config: { forceLocks: v['force-locks'] === true, caches: path.resolve(v.caches ?? defaultCaches()) } };
}

// ---- the locks ---------------------------------------------------------------------------------------------------------------------

const templateFile = (kind, name) => path.join(SPIKE, 'templates', 'node', kind, name);

/** Generate the lock of one kind in a fresh owned folder holding only the template's `package.json` and `.npmrc`. -> `{ code, lock }` where `lock` is the text npm wrote. */
async function generateLock({ kind, owned, dirs, record, config }) {
  const scope = cleanupScope();
  try {
    const view = owned.ownedDir(scope, 'lock', { parent: workRoot() });
    for (const name of ['package.json', '.npmrc']) copyFileSync(templateFile(kind, name), path.join(view, name));
    const ran = await inBuildBox({ owned, view, dirs, stack: 'node', record, caches: config.caches, command: LOCK_COMMAND });
    if (!ran.ok) return { code: EXIT.failed, message: `${kind}: ${ran.reason}` };
    if (ran.value.code !== 0 || ran.value.timedOut) return { code: EXIT.failed, message: `${kind}: npm exited ${ran.value.code}${ran.value.timedOut ? ' (timed out)' : ''}: ${ran.value.stderr.slice(-1500)}` };
    return { code: EXIT.done, lock: readFileSync(path.join(view, 'package-lock.json'), 'utf8') };
  } finally {
    await scope.close();
  }
}

function workRoot() {
  const dir = path.join(os.homedir(), '.cache', 'lang-v2', 'w');
  mkdirSync(dir, { recursive: true, mode: 0o700 });
  return dir;
}

/** Compare the generated lock with the committed one: absent -> written, equal -> unchanged, different -> refused unless forced. */
function settle({ kind, lock, force }) {
  const target = templateFile(kind, 'package-lock.json');
  if (!existsSync(target)) {
    writeFileSync(target, lock);
    say(`${kind}: written`);
    return EXIT.done;
  }
  if (readFileSync(target, 'utf8') === lock) {
    say(`${kind}: unchanged`);
    return EXIT.done;
  }
  if (!force) return stop(EXIT.refused, `${kind}: the generated lock differs from the committed ${target}; --force-locks replaces it`);
  writeFileSync(target, lock);
  say(`${kind}: replaced`);
  return EXIT.done;
}

async function lockOne({ kind, ...rest }) {
  const made = await generateLock({ kind, ...rest });
  if (made.code !== EXIT.done) return stop(made.code, made.message);
  const problems = lockProblems(JSON.parse(made.lock), rest.record.node.packages, readJson(templateFile(kind, 'package.json')));
  if (problems.length > 0) return stop(EXIT.refused, `${kind}: the generated lock is not what the manifest allows:\n  ${problems.join('\n  ')}`);
  return settle({ kind, lock: made.lock, force: rest.config.forceLocks });
}

async function makeLocks(config) {
  const record = readJson(path.join(SPIKE, 'inputs', 'cache-snapshots-lang.json'));
  const stacks = loadStacksLang(readJson(path.join(SPIKE, 'inputs', 'stacks-lang.json')));
  if (!stacks.ok) return stop(EXIT.usage, stacks.reason);
  const dirs = storeDirs(stacks.value, path.join(os.homedir(), ...stacks.value.storeRoot));
  const owned = ownedFor({ token: 'bench' });
  for (const kind of KINDS) {
    const code = signalled === null ? await lockOne({ kind, owned, dirs, record, config }) : signalled;
    if (code !== EXIT.done) return code;
  }
  return EXIT.done;
}

// ---- main --------------------------------------------------------------------------------------------------------------------------

async function main(argv) {
  const parsed = readArgs(argv);
  if (parsed.error) return stop(EXIT.usage, parsed.error);
  return makeLocks(parsed.config);
}

const code = await main(process.argv.slice(2));
process.exitCode = signalled ?? code;
