#!/usr/bin/env node
// Build one cell's tree: P + conventions pin + the arm's overlay + hooks + one commit + npm ci.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { ARM_SHAS, CONV_CLONE, DEFAULT_RUNS_ROOT, GUARD_SOURCE, INPUTS_DIR, MOUNT, OVERLAY_RULES, P, PLAN_FILE, SPIKE_DIR } from './lib/consts.mjs';
import { AUTHOR_ENV, git, gitEnv, gitShow } from './lib/git.mjs';
import { updateRunMap } from './lib/runmap.mjs';
import { applyOverlaySettings } from './lib/settings.mjs';
import { parseStruck, remainingModels, strikeTargets, withholdModels } from './lib/strike.mjs';
import { selectRemovals } from './lib/trim.mjs';
import { extractBase, npmCi, sha256, writeFileMaking } from './lib/tree.mjs';

const SPEC = {
  cell: { type: 'string', help: 'neutral cell name: c1 | c2 | c3 | reference-free' },
  arm: { type: 'string', help: 'A | B' },
  'runs-root': { type: 'string', default: DEFAULT_RUNS_ROOT, help: 'runs root' },
  'skip-npm': { type: 'boolean', help: 'skip npm ci (tests only)' },
};

export function validate(v) {
  if (!/^c[1-9]$/.test(v.cell)) throw new Error(`cell must be c1..c9, got ${v.cell}`);
  if (!['A', 'B'].includes(v.arm)) throw new Error(`arm must be A or B, got ${v.arm}`);
}

async function writeOverlay(treeDir, arm) {
  const overlay = {};
  for (const rule of OVERLAY_RULES) {
    const bytes = await gitShow(CONV_CLONE, ARM_SHAS[arm], rule);
    const rel = `${MOUNT}/${rule}`;
    writeFileMaking(path.join(treeDir, rel), bytes);
    overlay[rel] = sha256(bytes);
  }
  return overlay;
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

function strikePlan(treeDir) {
  const statement = fs.readFileSync(path.join(INPUTS_DIR, 'statement.md'), 'utf8');
  const planPath = path.join(treeDir, PLAN_FILE);
  const { text, modelWordsWithheld } = preparePlanText(fs.readFileSync(planPath, 'utf8'), statement);
  fs.writeFileSync(planPath, text);
  return { planSha256: sha256(Buffer.from(text)), modelWordsWithheld };
}

/** Delete everything under the mount that is not inside a rule directory; refuse if the guard rule is gone. */
export function trimMount(treeDir) {
  const mount = path.join(treeDir, MOUNT);
  const all = listFiles(mount).map(f => path.relative(mount, f).replace(/\\/g, '/'));
  const doomed = selectRemovals(all);
  let bytes = 0;
  for (const rel of doomed) { bytes += fs.statSync(path.join(mount, rel)).size; fs.rmSync(path.join(mount, rel)); }
  pruneEmptyDirs(mount);
  if (!fs.existsSync(path.join(mount, 'common/subagent-models.md'))) throw new Error('common/subagent-models.md is missing after the trim');
  return { files: doomed.length, bytes };
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

async function writeHooks(treeDir, arm) {
  const hooks = {};
  const put = (name, bytes) => { writeFileMaking(path.join(treeDir, '.claude/hooks', name), bytes); hooks[name] = sha256(bytes); };
  for (const name of ['audit.mjs']) put(name, fs.readFileSync(path.join(SPIKE_DIR, 'hooks', name)));
  if (arm === 'B') put('subagent-model.mjs', await gitShow(CONV_CLONE, ARM_SHAS.B, GUARD_SOURCE));
  const file = path.join(treeDir, '.claude/settings.json');
  const existing = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
  const merged = `${JSON.stringify(applyOverlaySettings(existing, arm), null, 2)}\n`;
  fs.writeFileSync(file, merged);
  return { hooks, settingsSha256: sha256(Buffer.from(merged)) };
}

const noCrlf = ['-c', 'core.autocrlf=false'];
const treeEnv = treeDir => gitEnv({ ...AUTHOR_ENV, GIT_CONFIG_GLOBAL: path.join(treeDir, '.git', 'empty.gitconfig') });
const text = buf => buf.toString('utf8').trim();

/** git init + stage; returns the tree id of the arm-independent content (before the overlay). */
async function initAndBaseTree(treeDir) {
  await git(treeDir, ['init', '-q', '-b', 'main'], gitEnv());
  fs.writeFileSync(path.join(treeDir, '.git', 'empty.gitconfig'), '');
  await git(treeDir, [...noCrlf, 'add', '-A'], treeEnv(treeDir));
  return text(await git(treeDir, ['write-tree'], treeEnv(treeDir)));
}

async function commitTree(treeDir) {
  const env = treeEnv(treeDir);
  await git(treeDir, [...noCrlf, 'add', '-A'], env);
  await git(treeDir, [...noCrlf, 'commit', '-q', '-m', 'cell base'], env);
  return { baseCommit: text(await git(treeDir, ['rev-parse', 'HEAD'], env)), commitTree: text(await git(treeDir, ['rev-parse', 'HEAD^{tree}'], env)) };
}

async function main(v) {
  validate(v);
  const cellDir = path.join(path.resolve(v['runs-root']), v.cell);
  const treeDir = path.join(cellDir, 'tree');
  if (fs.existsSync(treeDir)) throw new Error(`refusing: ${treeDir} already exists`);
  fs.mkdirSync(cellDir, { recursive: true });
  const submodulePin = await extractBase(cellDir);
  const plan = strikePlan(treeDir);
  const baseTree = await initAndBaseTree(treeDir);
  const overlay = await writeOverlay(treeDir, v.arm);
  const trimmed = trimMount(treeDir);
  const hooks = await writeHooks(treeDir, v.arm);
  const committed = await commitTree(treeDir);
  if (!v['skip-npm']) await npmCi(treeDir);
  updateRunMap({ cells: { [v.cell]: { arm: v.arm, armSha: ARM_SHAS[v.arm] } } });
  const record = { cell: v.cell, P, submodulePin, baseTree, ...committed, overlay, treeSha256: sha256(Buffer.from(JSON.stringify(overlay))), ...plan, trimmedMount: trimmed, ...hooks, createdUtc: new Date().toISOString() };
  fs.writeFileSync(path.join(cellDir, 'cell.json'), `${JSON.stringify(record, null, 2)}\n`);
  console.log(`prepared ${v.cell} at ${treeDir}`);
}

if (process.argv[1]?.endsWith('prepare-cell.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node prepare-cell.mjs --cell <c1|c2|c3> --arm <A|B> [--runs-root <dir>]');
  if (help) console.log(help);
  else {
    requireValues(values, ['cell', 'arm']);
    main(values).catch(e => { console.error(e.message); process.exit(1); });
  }
}
