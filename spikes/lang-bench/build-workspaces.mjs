#!/usr/bin/env node
// E1.S3: the workspace tools of the language comparison v2.
//
//   node spikes/lang-bench/build-workspaces.mjs --stack <csharp|node> --kind <olympiad|daemon|web> --task <name> --group <A|B> --out <dir> --statement <file> --samples <dir> [--task-repo <dir>] [--hello]
//     Builds one workspace into <dir>: the template of the stack and kind, PACKAGES.md derived from the snapshot record, the pinned runner, the grammar of the group's FORMAT, the statement and the samples.
//     The task repository (FORMAT comes from it, at the pinned commit) is --task-repo or LANG_V2_TASK_REPO. <dir> is refused inside ANY git worktree, and a task of another group is refused (T-L57). --hello: a task in no row (the fixture).
//   node spikes/lang-bench/build-workspaces.mjs --make-locks [--force-locks] [--caches <dir>]
//     Generates the `package-lock.json` of each Node template, offline, in the build box of the registered Node snapshot, and checks it against the manifest. Absent: written. Equal to the committed file: `unchanged`.
//     Different: refused (exit 3) unless --force-locks.
//
//   node spikes/lang-bench/build-workspaces.mjs --print-stack-files
//     Prints the list of file patterns the push check accepts in a stack folder (the content of templates/stack-files.json).
//
// Exit 0 done · 1 a box command failed · 2 usage · 3 refused (the destination or the group, a pin, a hash, a lock that differs or is outside the manifest)
// Lowered before anything else runs (common.local-cpu-budget), as run-tests.mjs does.
await import('./lib/lower-priority.mjs');

import { copyFileSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fail, ok } from '../minigit-bench/lib/result.mjs';
import { fileURLToPath } from 'node:url';
import { cleanupScope, ownedFor } from '../../.agents/conventions/tools/lib/owned.mjs';
import { endTrackedChildren } from './lib/exec-layer/cleanup.mjs';
import { storeDirs } from './lib/preflight-box.mjs';
import { fetchTaskRepo } from './lib/task-repo.mjs';
import { loadStacksLang } from './lib/toolchain-pins.mjs';
import { defaultCaches, inBuildBox } from './lib/workspace-box.mjs';
import { KINDS as KIND_NAMES, STACKS as STACK_NAMES, buildWorkspace, loadInputs, lockProblems, preCheck, stackFilePatterns } from './lib/workspace-build.mjs';

const SPIKE = fileURLToPath(new URL('.', import.meta.url));
const EXIT = Object.freeze({ done: 0, failed: 1, usage: 2, refused: 3 });
const OPTIONS = {
  stack: { type: 'string' }, kind: { type: 'string' }, task: { type: 'string' }, group: { type: 'string' }, out: { type: 'string' }, statement: { type: 'string' }, samples: { type: 'string' }, 'task-repo': { type: 'string' }, hello: { type: 'boolean' },
  'make-locks': { type: 'boolean' }, 'force-locks': { type: 'boolean' }, caches: { type: 'string' }, 'print-stack-files': { type: 'boolean' },
};
const BUILD_FLAGS = Object.freeze(['stack', 'kind', 'task', 'group', 'out', 'statement', 'samples']);
const LOCK_COMMAND = Object.freeze(['npm', 'install', '--package-lock-only', '--offline', '--ignore-scripts', '--no-audit', '--no-fund']);
const say = line => process.stdout.write(`${line}\n`);
const complain = line => process.stderr.write(`${line}\n`);
const stop = (code, line) => { complain(line); return code; };
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

let signalled = null;
for (const [signal, code] of [['SIGINT', 130], ['SIGTERM', 143]]) {
  process.on(signal, () => { signalled = code; endTrackedChildren(); });
}

/** The mode a set of flags asks for: `build` (any build flag), `locks`, `print`; more than one is an error. */
function modesOf(v) {
  return [['build', BUILD_FLAGS.some(flag => v[flag] !== undefined)], ['locks', v['make-locks'] === true], ['print', v['print-stack-files'] === true]].filter(([, asked]) => asked).map(([name]) => name);
}

/** The first problem of a build's flags, as a usage line, or null. */
function buildUsage(v) {
  const missing = BUILD_FLAGS.filter(flag => v[flag] === undefined);
  if (missing.length > 0) return `a build needs ${missing.map(flag => `--${flag}`).join(', ')}`;
  if (!STACK_NAMES.includes(v.stack)) return `--stack ${v.stack}: the legal names are ${STACK_NAMES.join(', ')}`;
  return KIND_NAMES.includes(v.kind) ? null : `--kind ${v.kind}: the legal names are ${KIND_NAMES.join(', ')}`;
}

/** `{ v }` of the parsed flags, or `{ error }`. */
function parseFlags(argv) {
  try {
    return { v: parseArgs({ args: argv, options: OPTIONS, strict: true }).values };
  } catch (e) {
    return { error: e.message };
  }
}

/** `{ mode }` for flags that ask for exactly one mode with a usable build, else `{ error }`. */
function modeOf(v) {
  const modes = modesOf(v);
  if (modes.length !== 1) return { error: 'ask for exactly one of: a build (--stack --kind --task --group --out --statement --samples), --make-locks, --print-stack-files' };
  const usage = modes[0] === 'build' ? buildUsage(v) : null;
  return usage === null ? { mode: modes[0] } : { error: usage };
}

function configOf(v, mode) {
  const taskRepo = v['task-repo'] ?? process.env.LANG_V2_TASK_REPO;
  const build = { stack: v.stack, kind: v.kind, task: v.task, group: v.group, out: v.out, statement: v.statement, samples: v.samples, taskRepo, hello: v.hello === true };
  return { mode, build, forceLocks: v['force-locks'] === true, caches: path.resolve(v.caches ?? defaultCaches()) };
}

/** `{ config }` or `{ error }`. */
function readArgs(argv) {
  const parsed = parseFlags(argv);
  if (parsed.error) return parsed;
  const picked = modeOf(parsed.v);
  return picked.error ? picked : { config: configOf(parsed.v, picked.mode) };
}

// ---- the locks ---------------------------------------------------------------------------------------------------------------------

const templateFile = (kind, name) => path.join(SPIKE, 'templates', 'node', kind, name);

const lockSucceeded = ran => ran.ok && ran.value.code === 0 && !ran.value.timedOut;
const lockFailure = (kind, ran) => (ran.ok ? `${kind}: npm exited ${ran.value.code}${ran.value.timedOut ? ' (timed out)' : ''}: ${ran.value.stderr.slice(-1500)}` : `${kind}: ${ran.reason}`);

/** The lock of one kind made in `view`, which holds only the template's `package.json` and `.npmrc`. -> `{ code, lock }` | `{ code, message }`. */
async function lockInBox({ kind, owned, dirs, record, config, view }) {
  for (const name of ['package.json', '.npmrc']) copyFileSync(templateFile(kind, name), path.join(view, name));
  const ran = await inBuildBox({ owned, view, dirs, stack: 'node', record, caches: config.caches, command: LOCK_COMMAND });
  return lockSucceeded(ran) ? { code: EXIT.done, lock: readFileSync(path.join(view, 'package-lock.json'), 'utf8') } : { code: EXIT.failed, message: lockFailure(kind, ran) };
}

/** Generate the lock of one kind in a fresh owned folder (removed in `finally`). */
async function generateLock(args) {
  const scope = cleanupScope();
  try {
    const view = args.owned.ownedDir(scope, 'lock', { parent: workRoot() });
    return await lockInBox({ ...args, view });
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

/** The registered record, the toolchain directories and the owned-name helper a lock generation needs. -> `ok(context)` | `fail`. */
function lockContext() {
  const record = readJson(path.join(SPIKE, 'inputs', 'cache-snapshots-lang.json'));
  const stacks = loadStacksLang(readJson(path.join(SPIKE, 'inputs', 'stacks-lang.json')));
  if (!stacks.ok) return stacks;
  return ok({ record, dirs: storeDirs(stacks.value, path.join(os.homedir(), ...stacks.value.storeRoot)), owned: ownedFor({ token: 'bench' }) });
}

async function makeLocks(config) {
  const context = lockContext();
  if (!context.ok) return stop(EXIT.usage, context.reason);
  for (const kind of KIND_NAMES) {
    const code = signalled ?? await lockOne({ kind, ...context.value, config });
    if (code !== EXIT.done) return code;
  }
  return EXIT.done;
}

// ---- the build and the list ----------------------------------------------------------------------------------------------------------------

const exitOf = failed => (failed.refusal === true ? EXIT.refused : EXIT.failed);
const stopFailed = failed => stop(failed.usage === true ? EXIT.usage : exitOf(failed), failed.reason);

/** FORMAT's bytes at the pinned commit of the local task repository. -> `ok(bytes)` | `fail` (a refusal for a rule or a hash, exit 1 for a git failure or a timeout). */
async function formatOf(taskRepo) {
  if (taskRepo === undefined) return fail('the task repository is --task-repo or LANG_V2_TASK_REPO', { usage: true });
  const fetched = await fetchTaskRepo({ repo: taskRepo });
  return fetched.ok ? ok(readFileSync(path.join(fetched.value.root, 'todo', 'tasks', 'FORMAT.md'))) : fetched;
}

const isFile = file => { try { return statSync(file).isFile(); } catch { return false; } };
const isFolder = dir => { try { return statSync(dir).isDirectory(); } catch { return false; } };

/** A usage line for a statement that is not a file or samples that are not a folder, else null. */
function inputUsage(build) {
  if (!isFile(build.statement)) return `--statement ${build.statement} is not a file`;
  return isFolder(build.samples) ? null : `--samples ${build.samples} is not a folder`;
}

/** The checks before anything is fetched: the inputs the flags name, then names, group and destination. */
function earlyChecks(args) {
  const usage = inputUsage(args);
  return usage === null ? preCheck(args) : fail(usage, { usage: true });
}

function finishBuild(args, formatBytes) {
  const made = buildWorkspace({ ...args, formatBytes, statement: readFileSync(args.statement), samplesDir: path.resolve(args.samples) });
  if (!made.ok) return stopFailed(made);
  say(`built ${args.stack}/${args.kind} task ${args.task} (group ${args.group}) into ${made.value.out}: ${made.value.files.length} files`);
  return EXIT.done;
}

async function buildMode(config) {
  const loaded = loadInputs();
  if (!loaded.ok) return stop(EXIT.usage, loaded.reason);
  const args = { ...config.build, inputs: loaded.value, helloFixture: config.build.hello };
  const early = earlyChecks(args);
  if (!early.ok) return stopFailed(early);
  const format = await formatOf(config.build.taskRepo);
  return format.ok ? finishBuild(args, format.value) : stopFailed(format);
}

function printMode() {
  const loaded = loadInputs();
  if (!loaded.ok) return stop(EXIT.usage, loaded.reason);
  process.stdout.write(`${JSON.stringify(stackFilePatterns({ specs: loaded.value.specs }), null, 2)}\n`);
  return EXIT.done;
}

// ---- main --------------------------------------------------------------------------------------------------------------------------

async function main(argv) {
  const parsed = readArgs(argv);
  if (parsed.error) return stop(EXIT.usage, parsed.error);
  const { config } = parsed;
  if (config.mode === 'build') return buildMode(config);
  return config.mode === 'print' ? printMode() : makeLocks(config);
}

const code = await main(process.argv.slice(2));
process.exitCode = signalled ?? code;
