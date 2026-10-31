// The cache snapshots (E3b.S1 step 2, parent 7.1): the NuGet packages and the dotnet first-run state (C#), the npm cache (Node), made ONCE inside an agent box
// through the egress proxy from `inputs/cache-recipe.json`, hashed, made read-only, and COPIED into every session so the box is identical across stacks
// except the prompt. The copy is re-hashed and must equal the registered value, part by part.
//
// The hash (`treeHash`): sha256 over every entry in sorted order -- type, relative path, mode WITHOUT its write bits, and the content hash (a link's target).
// Write bits are left out on purpose: the snapshot is read-only and its copy must be writable, and both are the same tree. mtimes are never in it.
import { createHash } from 'node:crypto';
import { appendFileSync, chmodSync, copyFileSync, cpSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, readlinkSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { egressProxy } from './egress.mjs';
import { EGRESS_HOSTS } from './egress-hosts.mjs';
import { readNpmPackages, readNugetPackages } from './packages.mjs';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';
import { agentBoxArgv, agentEnv, dummyToken, sessionLayout } from './sandbox.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const NOT_WRITE = 0o7555;

/** The sorted entries of a tree, depth first: `[{ rel, stat }]`. */
function walk(root, rel = '') {
  const dir = path.join(root, rel);
  return readdirSync(dir).sort().flatMap(name => {
    const r = path.join(rel, name);
    const stat = lstatSync(path.join(root, r));
    return [{ rel: r, stat }, ...(stat.isDirectory() ? walk(root, r) : [])];
  });
}

/** The hash of a tree (see the header). Stable under mtimes and write order; changed by a byte, a mode (execute/read bits), a name, a link target or an empty directory. */
const bodyOf = (full, stat) => (stat.isSymbolicLink() ? readlinkSync(full) : stat.isFile() ? sha256(readFileSync(full)) : '');
const typeOf = stat => (stat.isSymbolicLink() ? 'l' : stat.isDirectory() ? 'd' : 'f');

export function treeHash(root) {
  const h = createHash('sha256');
  for (const { rel, stat } of walk(root)) h.update(`${typeOf(stat)}\0${rel}\0${(stat.mode & NOT_WRITE).toString(8)}\0${bodyOf(path.join(root, rel), stat)}\n`);
  return h.digest('hex');
}

function chmodTree(root, apply) {
  for (const { rel, stat } of [...walk(root)].reverse()) if (!stat.isSymbolicLink()) chmodSync(path.join(root, rel), apply(stat.mode));
  chmodSync(root, apply(lstatSync(root).mode));
}
/** `u+w` on the whole tree (a read-only snapshot is removed or copied from through this). */
export const makeWritable = root => { if (existsSync(root)) chmodTree(root, m => m | 0o200); };
/** `a-w` on the whole tree. */
export const makeReadOnly = root => chmodTree(root, m => m & ~0o222);

/** Where each captured part of a snapshot lands in a session's layout. */
const SEED_TARGETS = Object.freeze({ nuget: l => l.nuget, dotnet: l => path.join(l.home, '.dotnet'), npm: l => l.npm });

/**
 * Copy BOTH snapshots into a session (`box/nuget`, `box/home/.dotnet`, `box/npm`) and re-hash every copied part against its registered hash.
 * `snapshots = { csharp: { dir, parts: { nuget, dotnet } }, node: { dir, parts: { npm } } }` -> `ok({ hashes })` | `fail` naming the part that differs.
 */
export function seedCaches({ layout, snapshots }) {
  const hashes = {};
  for (const snap of Object.values(snapshots)) {
    for (const [part, registered] of Object.entries(snap.parts)) {
      const target = SEED_TARGETS[part](layout);
      mkdirSync(target, { recursive: true });
      cpSync(path.join(snap.dir, part), target, { recursive: true, verbatimSymlinks: true, preserveTimestamps: true });
      makeWritable(target);
      hashes[part] = treeHash(target);
      if (hashes[part] !== registered) return fail(`the seeded ${part} cache hashes to ${hashes[part].slice(0, 12)}, the registered snapshot says ${registered.slice(0, 12)}`);
    }
  }
  return ok({ hashes });
}

// ---- the recipe -----------------------------------------------------------------------------------------------------------------

/** `inputs/cache-recipe.json` -> `ok(recipe)` | `fail` naming what is wrong (an unknown host list names the legal ones). */
const validStep = x => Array.isArray(x.argv) && x.argv.length > 0;
const hasSteps = s => Array.isArray(s.steps) && s.steps.length > 0 && s.steps.every(validStep);
const validStack = s => hasSteps(s) && Object.keys(s.capture ?? {}).length > 0;

export function loadRecipe(json) {
  if (!Object.hasOwn(EGRESS_HOSTS, json?.hosts)) return fail(`cache-recipe: hosts '${json?.hosts}' must be one of ${Object.keys(EGRESS_HOSTS).join(', ')}`);
  const bad = Object.entries(json.stacks ?? {}).find(([, s]) => !validStack(s));
  if (!json.stacks || bad) return fail(`cache-recipe: stack '${bad?.[0] ?? '(none)'}' needs at least one step with an argv and a capture`);
  return ok(json);
}

/** The highest plain `x.y.z` of `npm view ... --json` output (a string or an array); a pre-release is never picked. `null` when there is none. */
export function pickHighest(stdout) {
  let parsed;
  try { parsed = JSON.parse(stdout); } catch { return null; }
  const versions = [parsed].flat().filter(v => /^\d+\.\d+\.\d+$/.test(v));
  const key = v => v.split('.').map(Number);
  return versions.sort((a, b) => { const [x, y] = [key(a), key(b)]; return x[0] - y[0] || x[1] - y[1] || x[2] - y[2]; }).at(-1) ?? null;
}

const fill = (argv, resolved) => argv.map(a => a.replace(/\{([A-Z_]+)\}/g, (m, name) => resolved[name] ?? m));
const unresolved = argv => argv.find(a => /\{[A-Z_]+\}/.test(a));

/** Run one command in the box (the layout, the proxy and the env of a session, nothing of the model's). -> `ok({ code, stdout, stderr })` | `fail`. */
/** The environment of a step: the allow-list, the dummy token and the recipe's own variables; a variable the allow-list refuses is a `fail`. */
function boxEnv({ layout, name, nodeDir, extra }) {
  try {
    return ok(agentEnv({ layout, dummyToken: dummyToken('snapshot'), cellId: `cell-${name}`, nodeDir, extra }));
  } catch (e) {
    return fail(`the recipe's environment is refused: ${e.message}`);
  }
}

/** The result of a step run in the box: its output, or the sentence saying how it failed (not run, timed out, a non-zero exit). */
function boxResult(r, command, timeoutMs) {
  if (!r.ok) return r;
  if (r.value.timedOut) return fail(`\`${command.join(' ')}\` timed out after ${timeoutMs} ms`);
  const tail = `${r.value.stderr}\n${r.value.stdout}`.trim().slice(-600); // dotnet writes its errors to stdout
  return r.value.code === 0 ? ok(r.value) : fail(`\`${command.join(' ')}\` exited ${r.value.code}: ${tail}`);
}

async function inBox({ layout, nodeDir, command, timeoutMs, name, env: extra = {} }) {
  const env = boxEnv({ layout, name, nodeDir, extra });
  if (!env.ok) return env;
  const argv = agentBoxArgv({ layout, arm: 'bare', toolchains: [], claudeBinary: process.execPath, nodeDir, command });
  return boxResult(await runBounded(argv[0], argv.slice(1), { env: env.value, timeoutMs }), command, timeoutMs);
}

async function resolveAll({ spec = {}, ...box }) {
  const resolved = {};
  for (const [name, r] of Object.entries(spec)) {
    const ran = await inBox({ ...box, command: r.argv });
    if (!ran.ok) return ran;
    const picked = pickHighest(ran.value.stdout);
    if (picked === null) return fail(`${name}: \`${r.argv.join(' ')}\` printed no plain x.y.z version`);
    resolved[name] = picked;
  }
  return ok(resolved);
}

function copyParts({ layout, capture, staging }) {
  for (const [part, rel] of Object.entries(capture)) {
    const from = path.join(layout.dir, rel);
    if (!existsSync(from)) return fail(`the recipe captures ${part} from ${rel}, which the steps did not create`);
    cpSync(from, path.join(staging, part), { recursive: true, verbatimSymlinks: true });
  }
  return ok(null);
}

/** The bytes of every regular file under `root` (0 for an absent directory). */
export const treeSize = root => (existsSync(root) ? sizeOf(root) : 0);

const sizeOf = root => walk(root).reduce((a, { stat }) => a + (stat.isFile() ? stat.size : 0), 0);

/**
 * Make one stack's snapshot: claim a work session under `root` (`snapshot-<stack>-<n>`, outside every git tree), run the recipe in a box behind the
 * egress proxy, capture, hash, name `cache-<stack>-<hash12>` under `out`, make it read-only. -> `ok({ dir, treeSha256, parts, sizeBytes, packages, resolved, steps })` | `fail`.
 * A failing step leaves nothing in `out` (the work session stays under `root` for diagnosis).
 */
/** The first free layout `snapshot-<stack>-<n>` under `root` (a claim is exclusive and never reused), or the last refusal. */
function claimLayout(root, stack) {
  let made = fail('no layout');
  for (let n = 1; n < 10 && !made.ok; n += 1) made = sessionLayout(root, `snapshot-${stack}-${n}`);
  return made;
}

export async function makeSnapshot({ stack, recipe, root, out, nodeDir = path.dirname(path.dirname(process.execPath)) }) {
  const spec = recipe.stacks[stack];
  if (!spec) return fail(`the recipe has no stack '${stack}' (it has ${Object.keys(recipe.stacks).join(', ')})`);
  const made = claimLayout(root, stack);
  if (!made.ok) return made;
  const layout = made.value;
  copyFileSync(path.join(SPIKE, 'harness', 'box-init.mjs'), layout.files.boxInit);
  const proxy = await egressProxy({ socketPath: layout.sockets.proxy, allow: EGRESS_HOSTS[recipe.hosts], log: line => appendFileSync(layout.files.egressLog, `${JSON.stringify(line)}\n`) });
  try {
    return await runRecipe({ stack, spec, recipe, layout, nodeDir, out });
  } finally {
    await proxy.close();
  }
}

/** The step a pinned `expectStdout` refuses, as a sentence, or null. */
const pinProblem = (step, command, printed) => (step.expectStdout !== undefined && printed !== step.expectStdout
  ? `the step \`${command.join(' ')}\` printed '${printed.slice(0, 80)}', the recipe pins '${step.expectStdout}': the toolchain is not the pinned one` : null);

const stepRecord = ({ step, command, ran, printed }) => ({ argv: command, code: ran.value.code, stdout: step.expectStdout === undefined ? null : printed.slice(0, 200) });

/** Run one step of the recipe in the box. -> `ok({ argv, code, stdout })` | `fail`. Only a pinned step's output is kept: the rest names the operator's home. */
async function runStep({ step, resolved, box }) {
  const command = fill(step.argv, resolved);
  const open = unresolved(command);
  if (open) return fail(`the step \`${step.argv.join(' ')}\` still holds ${open} after resolution`);
  const ran = await inBox({ ...box, command });
  if (!ran.ok) return ran;
  const printed = ran.value.stdout.trim();
  const problem = pinProblem(step, command, printed);
  return problem ? fail(problem) : ok(stepRecord({ step, command, ran, printed }));
}

async function runRecipe({ stack, spec, recipe, layout, nodeDir, out }) {
  const box = { layout, nodeDir, timeoutMs: recipe.stepTimeoutMs, name: layout.rid, env: spec.env ?? {} };
  const resolved = await resolveAll({ ...box, spec: spec.resolve });
  if (!resolved.ok) return resolved;
  const steps = [];
  for (const step of spec.steps) {
    const ran = await runStep({ step, resolved: resolved.value, box });
    if (!ran.ok) return ran;
    steps.push(ran.value);
  }
  return finishSnapshot({ stack, spec, layout, out, resolved: resolved.value, steps });
}

function finishSnapshot({ stack, spec, layout, out, resolved, steps }) {
  mkdirSync(out, { recursive: true });
  const staging = path.join(out, `.building-${stack}-${process.pid}`);
  rmSync(staging, { recursive: true, force: true });
  mkdirSync(staging);
  const copied = copyParts({ layout, capture: spec.capture, staging });
  if (!copied.ok) { rmSync(staging, { recursive: true, force: true }); return copied; }
  const parts = Object.fromEntries(Object.keys(spec.capture).map(p => [p, treeHash(path.join(staging, p))]));
  const treeSha256 = treeHash(staging);
  const dir = path.join(out, `cache-${stack}-${treeSha256.slice(0, 12)}`);
  if (existsSync(dir)) { makeWritable(dir); rmSync(dir, { recursive: true, force: true }); }
  renameSync(staging, dir);
  const packages = [...readNpmPackages(path.join(dir, 'npm')), ...readNugetPackages(path.join(dir, 'nuget'))];
  const sizeBytes = sizeOf(dir);
  makeReadOnly(dir);
  return ok({ dir, treeSha256, parts, sizeBytes, packages, resolved, steps });
}

/** The one place a snapshot description is turned into the JSON `inputs/cache-snapshots.json` holds (no path of the operator's home in it). */
export const snapshotRecord = s => ({ treeSha256: s.treeSha256, parts: s.parts, sizeBytes: s.sizeBytes, packages: s.packages, resolved: s.resolved, steps: s.steps.map(x => ({ argv: x.argv, stdout: x.stdout })) });

/**
 * Register a snapshot in `inputs/cache-snapshots.json` (the pre-registered hashes and sizes, S3): the file keeps one record per stack and a stack is registered ONCE, because the tag
 * freezes the file. -> `ok(file content)` | `fail` for a stack that is already there.
 */
export function registerSnapshot({ file, stack, record, madeOn }) {
  const current = existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : {};
  if (current[stack]) return fail(`${stack} is already registered in ${file} (${current[stack].treeSha256.slice(0, 12)}): a registered snapshot is never replaced`);
  const next = { ...current, [stack]: { ...record, madeOn } };
  writeFileSync(file, `${JSON.stringify(next, null, 2)}\n`);
  return ok(next);
}
