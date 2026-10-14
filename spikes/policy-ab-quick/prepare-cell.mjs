#!/usr/bin/env node
// Build one cell's tree: P + conventions pin + the arm's overlay + hooks + one commit + npm ci.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { ARM_SHAS, CELLS, CONV_CLONE, GUARD_SOURCE, INPUTS_DIR, MOUNT, OVERLAY_RULES, P, PLAN_FILE, SPIKE_DIR } from './lib/consts.mjs';
import { AUTHOR_ENV, git, gitEnv, gitShow } from './lib/git.mjs';
import { applyOverlaySettings } from './lib/settings.mjs';
import { parseStruck, strikeTargets } from './lib/strike.mjs';
import { extractBase, npmCi, sha256, writeFileMaking } from './lib/tree.mjs';

const SPEC = {
  cell: { type: 'string', help: 'A1 | B | A2' },
  arm: { type: 'string', help: 'A | B (must match the cell)' },
  'runs-root': { type: 'string', help: 'runs root, e.g. D:/rsd/_policy-ab-runs' },
  'skip-npm': { type: 'boolean', help: 'skip npm ci (tests only)' },
};

function validate(v) {
  if (CELLS[v.cell] !== v.arm) throw new Error(`cell ${v.cell} belongs to arm ${CELLS[v.cell] ?? '?'}, not ${v.arm}`);
}

async function writeOverlay(treeDir, arm) {
  const armSha = ARM_SHAS[arm];
  const overlay = {};
  for (const rule of OVERLAY_RULES) {
    const bytes = await gitShow(CONV_CLONE, armSha, rule);
    const rel = `${MOUNT}/${rule}`;
    writeFileMaking(path.join(treeDir, rel), bytes);
    overlay[rel] = sha256(bytes);
  }
  return overlay;
}

function strikePlan(treeDir) {
  const statement = fs.readFileSync(path.join(INPUTS_DIR, 'statement.md'), 'utf8');
  const planPath = path.join(treeDir, PLAN_FILE);
  const { text, notFound } = strikeTargets(fs.readFileSync(planPath, 'utf8'), parseStruck(statement));
  fs.writeFileSync(planPath, text);
  return { planSha256: sha256(Buffer.from(text)), struckNotFound: notFound };
}

async function writeHooks(treeDir, arm) {
  const hooks = {};
  const put = (name, bytes) => { writeFileMaking(path.join(treeDir, '.claude/hooks', name), bytes); hooks[name] = sha256(bytes); };
  for (const name of ['policy-audit.mjs', 'effort-pin.mjs']) put(name, fs.readFileSync(path.join(SPIKE_DIR, 'hooks', name)));
  if (arm === 'B') put('subagent-model.mjs', await gitShow(CONV_CLONE, ARM_SHAS.B, GUARD_SOURCE));
  const file = path.join(treeDir, '.claude/settings.json');
  const existing = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
  const merged = `${JSON.stringify(applyOverlaySettings(existing, arm), null, 2)}\n`;
  fs.writeFileSync(file, merged);
  return { hooks, settingsSha256: sha256(Buffer.from(merged)) };
}

async function commitTree(treeDir) {
  const env = gitEnv({ ...AUTHOR_ENV, GIT_CONFIG_GLOBAL: path.join(treeDir, '.git', 'empty.gitconfig') });
  await git(treeDir, ['init', '-q', '-b', 'main'], gitEnv());
  fs.writeFileSync(path.join(treeDir, '.git', 'empty.gitconfig'), '');
  await git(treeDir, ['-c', 'core.autocrlf=false', 'add', '-A'], env);
  await git(treeDir, ['-c', 'core.autocrlf=false', 'commit', '-q', '-m', 'policy-ab cell base'], env);
  return (await git(treeDir, ['rev-parse', 'HEAD'], env)).toString('utf8').trim();
}

async function main(v) {
  validate(v);
  const root = path.resolve(v['runs-root']);
  const cellDir = path.join(root, v.cell);
  const treeDir = path.join(cellDir, 'tree');
  if (fs.existsSync(treeDir)) throw new Error(`refusing: ${treeDir} already exists`);
  fs.mkdirSync(cellDir, { recursive: true });
  const submodulePin = await extractBase(cellDir);
  const overlay = await writeOverlay(treeDir, v.arm);
  const plan = strikePlan(treeDir);
  const hooks = await writeHooks(treeDir, v.arm);
  const baseCommit = await commitTree(treeDir);
  if (!v['skip-npm']) await npmCi(treeDir);
  const record = { cell: v.cell, arm: v.arm, armSha: ARM_SHAS[v.arm], P, submodulePin, baseCommit, overlay, treeSha256: sha256(Buffer.from(JSON.stringify(overlay))), ...plan, ...hooks, createdUtc: new Date().toISOString() };
  fs.writeFileSync(path.join(cellDir, 'cell.json'), `${JSON.stringify(record, null, 2)}\n`);
  console.log(`prepared ${v.cell} (arm ${v.arm}) at ${treeDir}`);
}

const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node prepare-cell.mjs --cell <A1|B|A2> --arm <A|B> --runs-root <dir>');
if (help) console.log(help);
else {
  requireValues(values, ['cell', 'arm', 'runs-root']);
  main(values).catch(e => { console.error(e.message); process.exit(1); });
}
