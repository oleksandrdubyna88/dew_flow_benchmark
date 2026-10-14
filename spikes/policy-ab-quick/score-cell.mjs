#!/usr/bin/env node
// Score a cell (or the reference tree) against the hold-out tests, and run its own suite as delivered.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { resolveCompile } from './lib/commands.mjs';
import { CREDS_CLONE, DEFAULT_RUNS_ROOT, INPUTS_DIR, P, S, WEB_DIR } from './lib/consts.mjs';
import { git, gitEnv, gitShow } from './lib/git.mjs';
import { compiledTestPath, entryPath, fileStem, holdoutEntries, repoPathOf } from './lib/holdout.mjs';
import { parseJunit, verdictOf } from './lib/junit.mjs';
import { run } from './lib/proc.mjs';
import { extractBase, npmCi, writeFileMaking } from './lib/tree.mjs';

const SPEC = {
  cell: { type: 'string', help: 'A1 | B | A2 | reference' },
  'runs-root': { type: 'string', default: DEFAULT_RUNS_ROOT, help: 'runs root' },
  'skip-npm': { type: 'boolean', help: 'reference only: skip npm ci (tests only)' },
};

const readJson = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {});
const tail = buf => buf.toString('utf8').slice(-1500);

/** The reference tree: P plus the src_vs_code part of the P..S diff. Its own folder, built once. */
async function ensureReferenceTree(cellDir, skipNpm) {
  const treeDir = path.join(cellDir, 'tree');
  if (fs.existsSync(treeDir)) return treeDir;
  fs.mkdirSync(cellDir, { recursive: true });
  await extractBase(cellDir);
  const diff = await git(CREDS_CLONE, ['diff', '--binary', P, S, '--', WEB_DIR]);
  const patch = path.join(cellDir, '_reference.patch');
  fs.writeFileSync(patch, diff);
  await git(treeDir, ['apply', '--whitespace=nowarn', patch], gitEnv());
  fs.rmSync(patch);
  if (!skipNpm) await npmCi(treeDir);
  return treeDir;
}

/** Copy a tree (node_modules included, .git excluded) to a fresh scoring folder. */
function copyTree(from, to) {
  if (fs.existsSync(to)) throw new Error(`refusing: ${to} already exists`);
  fs.cpSync(from, to, { recursive: true, filter: src => path.basename(src) !== '.git' });
}

async function compile(webDir, commands) {
  const { exe, args } = resolveCompile(commands);
  const r = await run(exe, args, { cwd: webDir, env: process.env });
  return { ok: r.code === 0, exitCode: r.code, detail: r.code === 0 ? '' : tail(Buffer.concat([r.stdout, r.stderr])) };
}

async function nodeTest(webDir, targets, xmlPath) {
  const r = await run(process.execPath, ['--test', '--test-reporter=junit', `--test-reporter-destination=${xmlPath}`, ...targets], { cwd: webDir, env: process.env });
  const parsed = fs.existsSync(xmlPath) ? parseJunit(fs.readFileSync(xmlPath, 'utf8')) : parseJunit('');
  return { verdict: verdictOf(parsed, r.code), exitCode: r.code, tests: parsed.tests, passed: parsed.passed, failed: parsed.failed, skipped: parsed.skipped, failedCases: parsed.cases.filter(c => c.status === 'fail').map(c => c.name), detail: parsed.tests === 0 ? tail(Buffer.concat([r.stdout, r.stderr])) : '' };
}

async function scoreHoldout(entry, treeDir, scoreDir, commands) {
  const repoPath = repoPathOf(entryPath(entry));
  const copy = path.join(scoreDir, fileStem(repoPath));
  copyTree(treeDir, copy);
  writeFileMaking(path.join(copy, repoPath), await gitShow(CREDS_CLONE, S, repoPath));
  const webDir = path.join(copy, WEB_DIR);
  const emitted = path.join(webDir, compiledTestPath(repoPath));
  fs.rmSync(emitted, { force: true }); // a stale out/ must not hide a missing compile
  const built = await compile(webDir, commands);
  const compileInfo = { ok: built.ok, exitCode: built.exitCode, diagnostics: built.detail };
  if (!fs.existsSync(emitted)) return { path: repoPath, verdict: 'compile-error', compile: compileInfo, emitted: false };
  const test = await nodeTest(webDir, [compiledTestPath(repoPath)], path.join(copy, 'junit.xml'));
  // tsc emits even on errors: a failed compile is itself the hold-out failing, the test result is kept beside it.
  return { path: repoPath, verdict: built.ok ? test.verdict : 'compile-error', compile: compileInfo, emitted: true, test };
}

async function scoreOwnSuite(treeDir, scoreDir, commands) {
  const copy = path.join(scoreDir, '_own');
  copyTree(treeDir, copy);
  const webDir = path.join(copy, WEB_DIR);
  const built = await compile(webDir, commands);
  const test = await nodeTest(webDir, ['out/test/*.test.js'], path.join(copy, 'junit.xml'));
  return { ...test, verdict: built.ok ? test.verdict : 'compile-error', compile: { ok: built.ok, exitCode: built.exitCode, diagnostics: built.detail } };
}

async function main(v) {
  const root = path.resolve(v['runs-root']);
  const cellDir = path.join(root, v.cell);
  const treeDir = v.cell === 'reference' ? await ensureReferenceTree(cellDir, v['skip-npm']) : path.join(cellDir, 'tree');
  if (!fs.existsSync(treeDir)) throw new Error(`no tree at ${treeDir}`);
  const scoreDir = path.join(cellDir, 'score');
  if (fs.existsSync(scoreDir)) throw new Error(`refusing: ${scoreDir} already exists`);
  fs.mkdirSync(scoreDir, { recursive: true });
  const commands = readJson(path.join(INPUTS_DIR, 'commands.json'));
  const holdout = [];
  for (const entry of holdoutEntries(readJson(path.join(INPUTS_DIR, 'holdout.json')))) holdout.push(await scoreHoldout(entry, treeDir, scoreDir, commands));
  const ownSuite = await scoreOwnSuite(treeDir, scoreDir, commands);
  fs.writeFileSync(path.join(cellDir, 'score.json'), `${JSON.stringify({ cell: v.cell, scoredUtc: new Date().toISOString(), holdout, ownSuite }, null, 2)}\n`);
  console.log(`scored ${v.cell}: ${holdout.map(h => `${path.basename(h.path)}=${h.verdict}`).join(', ')}; own=${ownSuite.verdict}`);
}

if (process.argv[1]?.endsWith('score-cell.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node score-cell.mjs --cell <A1|B|A2|reference> [--runs-root dir]');
  if (help) console.log(help);
  else {
    requireValues(values, ['cell']);
    main(values).catch(e => { console.error(e.message); process.exit(1); });
  }
}
