#!/usr/bin/env node
// Build one cell's tree. Two layouts (stories/<id>/story.json):
//  - native   (creds): P + the conventions pin archived into .claude/rules/shared + the arm's two overlaid rule files + hooks.
//             Claude discovers the rules itself, so the mount is trimmed to rule directories.
//  - resolver (coai):  P + a one-commit git repository mounted at .agents/conventions, built from P's PINNED conventions (the story's
//             own `npm run compile` runs the conventions' resolver, which demands a clean checkout whose HEAD equals the outer
//             gitlink). Arm B adds the release's guard files (story.policyFiles) and nothing else; arm A is the pin unchanged; the
//             delta is asserted. The tree's adapter hooks and the `hooks` key of its settings are re-synced from that mount, then
//             ours are added beside them. Verified before use; --verify-only also runs P's own suite so the arms can be compared.
import fs from 'node:fs';
import path from 'node:path';
import { hookFilesOf, syncHooksKey, withoutUserGuard } from './lib/adapter.mjs';
import { parseCli, requireValues } from './lib/args.mjs';
import { ARM_SHAS, CONV_CLONE, GUARD_SOURCE, OVERLAY_RULES, SPIKE_DIR } from './lib/consts.mjs';
import { AUTHOR_ENV, git, gitEnv, gitShow } from './lib/git.mjs';
import { npmInvocation, run } from './lib/proc.mjs';
import { updateRunMap } from './lib/runmap.mjs';
import { applyOverlaySettings, existingHooksPreserved } from './lib/settings.mjs';
import { loadStory, runsRootOf, STORY_OPTION } from './lib/story.mjs';
import { parseStruck, remainingModels, strikeTargets, withholdModels } from './lib/strike.mjs';
import { selectRemovals } from './lib/trim.mjs';
import { ownSuiteTargets } from './lib/commands.mjs';
import { rerunTargets, settleFailures } from './lib/probe.mjs';
import { nodeTest } from './lib/suite.mjs';
import { assertMountPinned, buildArmMount, extractBase, installResolver, mountDirOf, npmCi, sha256, stageMountGitlink, writeFileMaking } from './lib/tree.mjs';

const SPEC = {
  cell: { type: 'string', help: 'neutral cell name: c1..c9 (verify-only: default probe-<arm>)' },
  arm: { type: 'string', help: 'A | B' },
  story: STORY_OPTION,
  'runs-root': { type: 'string', help: "runs root (default: the story's defaultRunsRoot)" },
  'skip-npm': { type: 'boolean', help: 'skip npm ci and the resolver-layout verification (tests only)' },
  'verify-only': { type: 'boolean', help: 'resolver layout: build a probe tree, run every check a cell must pass (rules check, compile, adapter, SessionStart), write nothing to the run map' },
};

export function validate(v) {
  if (!/^c[1-9]$/.test(v.cell)) throw new Error(`cell must be c1..c9, got ${v.cell}`);
  if (!['A', 'B'].includes(v.arm)) throw new Error(`arm must be A or B, got ${v.arm}`);
}

async function writeOverlay(treeDir, arm, story) {
  const overlay = {};
  for (const rule of OVERLAY_RULES) {
    const bytes = await gitShow(CONV_CLONE, ARM_SHAS[arm], rule);
    const rel = `${story.mount}/${rule}`;
    writeFileMaking(path.join(treeDir, rel), bytes);
    overlay[rel] = sha256(bytes);
  }
  return overlay;
}

/** Resolver layout: nothing is written into the mount; the record is the sha of the two rule files the arm's checkout holds. */
function overlayFromMount(treeDir, story) {
  return Object.fromEntries(OVERLAY_RULES.map(rule => {
    const rel = `${story.mount}/${rule}`;
    return [rel, sha256(fs.readFileSync(path.join(treeDir, rel)))];
  }));
}

/** Strike the statement's model lines, then withhold every remaining model name in the plan file. */
export function preparePlanText(planText, statementMd) {
  const struck = strikeTargets(planText, parseStruck(statementMd));
  if (struck.notFound.length > 0) throw new Error(`struck lines not found in the plan: ${struck.notFound.join(' | ')}`);
  const withheld = withholdModels(struck.text);
  const left = remainingModels(withheld.text);
  if (left.length > 0) throw new Error(`model names remain in the plan: ${left.join(', ')}`);
  return { text: withheld.text, modelWordsWithheld: withheld.count };
}

function strikePlan(treeDir, story) {
  const statement = fs.readFileSync(path.join(story.inputsDir, story.struckFrom), 'utf8');
  const planPath = path.join(treeDir, story.planFile);
  const { text, modelWordsWithheld } = preparePlanText(fs.readFileSync(planPath, 'utf8'), statement);
  fs.writeFileSync(planPath, text);
  return { planSha256: sha256(Buffer.from(text)), modelWordsWithheld };
}

/** Delete everything under the mount that is not inside a rule directory; refuse if the guard rule is gone. */
export function trimMount(treeDir, mountRel) {
  const mount = path.join(treeDir, mountRel);
  const all = listFiles(mount).map(f => path.relative(mount, f).replace(/\\/g, '/'));
  const doomed = selectRemovals(all);
  let bytes = 0;
  for (const rel of doomed) { bytes += fs.statSync(path.join(mount, rel)).size; fs.rmSync(path.join(mount, rel)); }
  pruneEmptyDirs(mount);
  if (!fs.existsSync(path.join(mount, 'common/subagent-models.md'))) throw new Error('common/subagent-models.md is missing after the trim');
  return { files: doomed.length, bytes };
}

/** Native layout trims the mount to its rule directories; the resolver layout leaves the checkout whole (a trim would dirty it). */
export function trimFor(treeDir, story) {
  if (story.layout === 'resolver') return { skipped: true, reason: 'resolver layout: the mount is a clean checkout; only the rules the resolver selects load' };
  return trimMount(treeDir, story.mount);
}

function listFiles(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(e => (e.isDirectory() ? listFiles(path.join(dir, e.name)) : [path.join(dir, e.name)]));
}

function pruneEmptyDirs(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true }).filter(x => x.isDirectory())) {
    pruneEmptyDirs(path.join(dir, e.name));
    if (fs.readdirSync(path.join(dir, e.name)).length === 0) fs.rmdirSync(path.join(dir, e.name));
  }
}

const readJsonIf = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {});

/** Our hooks: the audit log for every arm, plus arm B's test-only edit bound. (The model guard is arm B's release, not ours, in the resolver layout.) */
function putOurHooks(treeDir, arm, hooks) {
  const put = (name, bytes) => { writeFileMaking(path.join(treeDir, '.claude/hooks', name), bytes); hooks[name] = sha256(bytes); };
  put('audit.mjs', fs.readFileSync(path.join(SPIKE_DIR, 'hooks', 'audit.mjs')));
  if (arm === 'B') for (const [src, dst] of [['hooks/edit-bound.mjs', 'edit-bound.mjs'], ['lib/edit-bound.mjs', 'edit-bound-core.mjs']]) put(dst, fs.readFileSync(path.join(SPIKE_DIR, src)));
}

async function writeHooks(treeDir, arm) {
  const hooks = {};
  const put = (name, bytes) => { writeFileMaking(path.join(treeDir, '.claude/hooks', name), bytes); hooks[name] = sha256(bytes); };
  put('audit.mjs', fs.readFileSync(path.join(SPIKE_DIR, 'hooks', 'audit.mjs')));
  if (arm === 'B') put('subagent-model.mjs', await gitShow(CONV_CLONE, ARM_SHAS.B, GUARD_SOURCE));
  if (arm === 'B') for (const [src, dst] of [['hooks/edit-bound.mjs', 'edit-bound.mjs'], ['lib/edit-bound.mjs', 'edit-bound-core.mjs']]) put(dst, fs.readFileSync(path.join(SPIKE_DIR, src)));
  const file = path.join(treeDir, '.claude/settings.json');
  const existing = readJsonIf(file);
  const next = applyOverlaySettings(existing, arm);
  if (!existingHooksPreserved(existing, next)) throw new Error('the merge changed a hook the project already had');
  const merged = `${JSON.stringify(next, null, 2)}\n`;
  fs.writeFileSync(file, merged);
  return { hooks, settingsSha256: sha256(Buffer.from(merged)), projectHooksKept: Object.fromEntries(Object.entries(existing.hooks ?? {}).map(([k, v]) => [k, v.length])) };
}

/** Run the mount's own adapter-check on the tree: the synced adapter must be exactly the reference. */
async function adapterCheck(treeDir, story) {
  const r = await run(process.execPath, [path.join(mountDirOf(treeDir, story), 'tools/adapter-check.mjs'), treeDir], { cwd: treeDir, env: process.env, timeoutMs: 60_000 });
  if (r.code !== 0) throw new Error(`adapter-check failed after the sync:\n${r.stdout.toString('utf8').slice(0, 800)}`);
}

/** Resolver layout hooks: sync the adapter from the mount (adapter-check must pass), drop the user-guard installer, add ours. */
async function writeHooksResolver(treeDir, arm, story) {
  const mount = mountDirOf(treeDir, story);
  const hookDir = path.join(mount, 'settings/hooks');
  const rels = hookFilesOf(listFiles(hookDir).map(f => path.relative(hookDir, f)));
  const hooks = {};
  const changedFromP = [];
  for (const rel of rels) {
    const target = path.join(treeDir, '.claude/hooks', rel);
    const bytes = fs.readFileSync(path.join(hookDir, rel));
    if (!fs.existsSync(target) || sha256(fs.readFileSync(target)) !== sha256(bytes)) changedFromP.push(rel);
    writeFileMaking(target, bytes);
    hooks[rel] = sha256(bytes);
  }
  const file = path.join(treeDir, '.claude/settings.json');
  const atP = readJsonIf(file);
  const synced = syncHooksKey(atP, readJsonIf(path.join(mount, 'settings/settings.json')));
  fs.writeFileSync(file, `${JSON.stringify(synced, null, 2)}\n`);
  await adapterCheck(treeDir, story);
  const { settings: pruned, removed } = withoutUserGuard(synced);
  putOurHooks(treeDir, arm, hooks);
  const next = applyOverlaySettings(pruned, arm, { guardWired: true });
  if (!existingHooksPreserved(pruned, next)) throw new Error('the merge changed a hook the adapter wired');
  const merged = `${JSON.stringify(next, null, 2)}\n`;
  fs.writeFileSync(file, merged);
  return { hooks, settingsSha256: sha256(Buffer.from(merged)), adapter: { filesSynced: rels, changedFromP, userGuardRemoved: removed, adapterCheck: 'ok', hooksKeyAtP: Object.fromEntries(Object.entries(atP.hooks ?? {}).map(([k, v]) => [k, v.length])) } };
}

const noCrlf = ['-c', 'core.autocrlf=false'];
const treeEnv = treeDir => gitEnv({ ...AUTHOR_ENV, GIT_CONFIG_GLOBAL: path.join(treeDir, '.git', 'empty.gitconfig') });
const text = buf => buf.toString('utf8').trim();

/** git init + stage; returns the tree id of the content before the overlay (resolver layout: the mount's gitlink is staged first). */
async function initAndBaseTree(treeDir, story) {
  await git(treeDir, ['init', '-q', '-b', 'main'], gitEnv());
  const emptyConfig = path.join(treeDir, '.git', 'empty.gitconfig');
  fs.writeFileSync(emptyConfig, '');
  if (story.layout === 'resolver') await stageMountGitlink(treeDir, story, emptyConfig);
  await git(treeDir, [...noCrlf, 'add', '-A'], treeEnv(treeDir));
  return text(await git(treeDir, ['write-tree'], treeEnv(treeDir)));
}

async function commitTree(treeDir) {
  const env = treeEnv(treeDir);
  await git(treeDir, [...noCrlf, 'add', '-A'], env);
  await git(treeDir, [...noCrlf, 'commit', '-q', '-m', 'cell base'], env);
  return { baseCommit: text(await git(treeDir, ['rev-parse', 'HEAD'], env)), commitTree: text(await git(treeDir, ['rev-parse', 'HEAD^{tree}'], env)) };
}

/** The installs make the tree runnable; they must leave it clean (node_modules ignored), or run-cell would refuse it later. */
async function assertClean(treeDir) {
  const status = text(await git(treeDir, [...noCrlf, 'status', '--porcelain'], treeEnv(treeDir)));
  if (status !== '') throw new Error(`the tree is not clean after the installs:\n${status.slice(0, 600)}`);
}

/** Resolver layout: run the tree's own SessionStart hook once, before any model. It must not report itself INCOMPLETE. */
async function probeSessionStart(treeDir) {
  const hook = path.join(treeDir, '.claude/hooks/load-instructions.mjs');
  if (!fs.existsSync(hook)) throw new Error('resolver layout, but the tree has no .claude/hooks/load-instructions.mjs');
  const r = await run(process.execPath, [hook], { cwd: treeDir, env: { ...process.env, CLAUDE_PROJECT_DIR: treeDir }, timeoutMs: 180_000 });
  const out = r.stdout.toString('utf8');
  if (r.code !== 0 || out.includes('Instruction loading is INCOMPLETE')) throw new Error(`the SessionStart hook did not load the rules (exit ${r.code}):\n${out.slice(0, 600)}${r.stderr.toString('utf8').slice(-300)}`);
  return { exitCode: r.code, chars: out.length, sha256: sha256(r.stdout), mentionsRule: out.includes('subagent-models') };
}

const tailOf = r => `${r.stdout.toString('utf8')}${r.stderr.toString('utf8')}`.slice(-900);

/** What an agent in the cell will hit first: the resolver's `check`, then the story's own `npm run compile` (which runs it again). */
async function verifyTree(treeDir, story) {
  const mount = mountDirOf(treeDir, story);
  const rules = await run(process.execPath, [path.join(mount, 'tools/rules.mjs'), 'check', '--repo', treeDir], { cwd: treeDir, env: process.env, timeoutMs: 120_000 });
  if (rules.code !== 0) throw new Error(`rules check failed (exit ${rules.code}):\n${tailOf(rules)}`);
  const { exe, args } = npmInvocation(['run', 'compile']);
  const compile = await run(exe, args, { cwd: path.join(treeDir, story.srcDir), env: process.env, timeoutMs: 900_000 });
  if (compile.code !== 0) throw new Error(`npm run compile failed on the prepared tree (exit ${compile.code}):\n${tailOf(compile)}`);
  return { rulesCheck: 'ok', compile: 'ok' };
}

async function installAndVerify(treeDir, story, resolver) {
  await npmCi(treeDir, story);
  if (!resolver) { await assertClean(treeDir); return { sessionStart: null, verified: null }; }
  await installResolver(treeDir, story);
  await assertClean(treeDir);
  const emptyConfig = path.join(treeDir, '.git', 'empty.gitconfig');
  await assertMountPinned(treeDir, story, emptyConfig);
  const sessionStart = await probeSessionStart(treeDir);
  const verified = await verifyTree(treeDir, story);
  await assertClean(treeDir);
  await assertMountPinned(treeDir, story, emptyConfig);
  return { sessionStart, verified };
}

/** Probe only: P's own suite on the prepared tree, as the cell's agent would run it. The failing set must match across arms. */
async function probeSuite(treeDir, story, cellDir) {
  const commands = readJsonIf(path.join(story.inputsDir, 'commands.json'));
  const web = path.join(treeDir, story.srcDir);
  const first = await nodeTest(web, ownSuiteTargets(commands), path.join(cellDir, 'suite.xml'));
  // A failure that does not repeat when its file runs alone is load-dependent (a timing test while the whole suite runs): settle it.
  const rerun = first.failedCases.length === 0 ? { failedCases: [] } : await nodeTest(web, rerunTargets(first.failedCases), path.join(cellDir, 'suite-rerun.xml'));
  const settled = settleFailures(first.failedCases, rerun.failedCases);
  const suite = { ...first, failedCases: settled.failedCases, flaky: settled.flaky };
  fs.writeFileSync(path.join(cellDir, 'suite.json'), `${JSON.stringify({ tests: suite.tests, failedFirstRun: first.failed, failedCases: suite.failedCases, flaky: suite.flaky, verdict: suite.verdict }, null, 2)}\n`);
  return suite;
}

async function main(v) {
  const story = loadStory(v.story);
  const probe = Boolean(v['verify-only']);
  if (probe && story.layout !== 'resolver') throw new Error('--verify-only is for the resolver layout');
  if (probe && !v.cell) v.cell = `probe-${v.arm}`;
  if (probe) { if (!['A', 'B'].includes(v.arm)) throw new Error(`arm must be A or B, got ${v.arm}`); } else validate(v);
  const cellDir = path.join(runsRootOf(v, story), v.cell);
  const treeDir = path.join(cellDir, 'tree');
  if (fs.existsSync(treeDir)) throw new Error(`refusing: ${treeDir} already exists`);
  fs.mkdirSync(cellDir, { recursive: true });
  const resolver = story.layout === 'resolver';
  const submodulePin = await extractBase(cellDir, story);
  const armMount = resolver ? await buildArmMount(treeDir, story, v.arm, submodulePin, ARM_SHAS) : null;
  const plan = strikePlan(treeDir, story);
  const baseTree = await initAndBaseTree(treeDir, story);
  const overlay = resolver ? overlayFromMount(treeDir, story) : await writeOverlay(treeDir, v.arm, story);
  const trimmed = trimFor(treeDir, story);
  const hooks = resolver ? await writeHooksResolver(treeDir, v.arm, story) : await writeHooks(treeDir, v.arm);
  const committed = await commitTree(treeDir);
  const checks = v['skip-npm'] ? { sessionStart: null, verified: null } : await installAndVerify(treeDir, story, resolver);
  // The arm and everything that names it live in the run map, OUTSIDE the runs root - never in cell.json, which sits beside the tree.
  const suite = probe && !v['skip-npm'] ? await probeSuite(treeDir, story, cellDir) : null;
  // cell.json sits BESIDE the tree the model can reach: it holds nothing that tells the arms apart. What does (the overlay's shas, the hook
  // inventory, the adapter diff, the mount commit) goes to the run map outside the runs root, next to the arm.
  const privateRecord = { overlay, treeSha256: sha256(Buffer.from(JSON.stringify(overlay))), trimmedMount: trimmed, ...hooks, ...(armMount ? { mountHead: armMount.head, policyDelta: armMount.delta } : {}) };
  if (!probe) updateRunMap({ cells: { [v.cell]: { arm: v.arm, armSha: ARM_SHAS[v.arm], private: privateRecord } } }, story.runMapFile);
  const record = { cell: v.cell, story: story.id, layout: story.layout, P: story.P, submodulePin, baseTree, ...committed, ...plan, ...(probe ? { private: privateRecord } : {}), sessionStartProbe: checks.sessionStart, verified: checks.verified, probe, suite: suite && { tests: suite.tests, failed: suite.failed }, createdUtc: new Date().toISOString() };
  fs.writeFileSync(path.join(cellDir, 'cell.json'), `${JSON.stringify(record, null, 2)}\n`);
  console.log(`${probe ? 'verified probe' : 'prepared'} ${v.cell} at ${treeDir}`);
}

if (process.argv[1]?.endsWith('prepare-cell.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node prepare-cell.mjs --cell <c1..c9> --arm <A|B> [--story creds-176] [--runs-root <dir>]   |   --verify-only --arm <A|B> --story coai-751');
  if (help) console.log(help);
  else {
    requireValues(values, ['arm']);
    if (!values['verify-only']) requireValues(values, ['cell']);
    main(values).catch(e => { console.error(e.message); process.exit(1); });
  }
}
