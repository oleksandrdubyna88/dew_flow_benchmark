#!/usr/bin/env node
// Score a cell (or the reference tree) against the hold-out tests, and run its own suite as delivered.
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { ownSuiteTargets, resolveCompile } from './lib/commands.mjs';
import { git, gitEnv, gitShow, gitlinkAt } from './lib/git.mjs';
import { compiledTestPath, entryPath, fileStem, holdoutEntries, repoPathOf } from './lib/holdout.mjs';
import { assertRunsRoot, removeUnder } from './lib/safe-rm.mjs';
import { nodeTest } from './lib/suite.mjs';
import { run } from './lib/proc.mjs';
import { fileURLToPath } from 'node:url';
import { shedCell } from './lib/shed.mjs';
import { loadStory, runsRootOf, STORY_OPTION } from './lib/story.mjs';
import { attributeDiagnostics, parseTscDiagnostics } from './lib/tsc.mjs';
import { extractBase, extractConventionsInto, mountDirOf, npmCi, writeFileMaking } from './lib/tree.mjs';

const PREPARE_GENERATED = fileURLToPath(new URL('./lib/prepare-generated.mjs', import.meta.url));

const SPEC = {
  cell: { type: 'string', help: 'c1..c9 | reference' },
  story: STORY_OPTION,
  'runs-root': { type: 'string', help: "runs root (default: the story's defaultRunsRoot)" },
  'skip-npm': { type: 'boolean', help: 'reference only: skip npm ci (tests only)' },
  shed: { type: 'boolean', help: 'after scoring, delete this cell\'s score/ copies and its node_modules (by exact path; the records stay)' },
};

const readJson = file => (fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {});
const tail = buf => buf.toString('utf8').slice(-1500);
const MAX_DIAGS = 20;

/** The reference tree: P plus the story's source-folder part of the P..S diff. Its own folder, built once. */
async function ensureReferenceTree(cellDir, skipNpm, story) {
  const treeDir = path.join(cellDir, 'tree');
  if (fs.existsSync(treeDir)) return treeDir;
  fs.mkdirSync(cellDir, { recursive: true });
  await extractBase(cellDir, story);
  const diff = await git(story.repoClone, ['diff', '--binary', story.P, story.S, '--', story.srcDir]);
  const patch = path.join(cellDir, '_reference.patch');
  fs.writeFileSync(patch, diff);
  await git(treeDir, ['apply', '--whitespace=nowarn', patch], gitEnv());
  fs.rmSync(patch);
  if (!skipNpm) await npmCi(treeDir, story);
  return treeDir;
}

/** Copy a tree (node_modules included, .git excluded) to a fresh scoring folder. */
function copyTree(from, to) {
  if (fs.existsSync(to)) throw new Error(`refusing: ${to} already exists`);
  fs.cpSync(from, to, { recursive: true, filter: src => path.basename(src) !== '.git' });
}

/**
 * A scoring copy is the same for every cell: the resolver layout's mount (a per-arm git checkout in the cell) is replaced by P's
 * pinned conventions, so both arms are scored against one environment, and the git-ignored generated modules are written the way
 * the story's prepare-gate does (commands.prepareGenerated) - the copy has no checkout for `rules.mjs check` to accept.
 */
async function prepareCopy(copy, story, commands, pin) {
  if (story.layout === 'resolver') {
    const mount = mountDirOf(copy, story);
    removeUnder(mount, copy); // refuses a link or junction at the mount, or one above it that leaves the copy

    await extractConventionsInto(copy, story.mount, pin);
  }
  if (commands?.commands?.prepareGenerated) {
    const r = await run(process.execPath, [PREPARE_GENERATED, copy, story.srcDir], { cwd: copy, env: process.env });
    if (r.code !== 0) throw new Error(`prepareGenerated failed: ${tail(Buffer.concat([r.stdout, r.stderr]))}`);
  }
}

/** Compile in a copy whose out/ was removed first, so nothing stale can stand in for a fresh emit. */
async function compile(webDir, commands) {
  removeUnder(path.join(webDir, 'out'), webDir); // a scoring copy's own out/: through the one safe delete, never a bare recursive rm
  return compileKeepingOut(webDir, commands);
}

async function compileKeepingOut(webDir, commands) {
  const { exe, args } = resolveCompile(commands);
  const r = await run(exe, args, { cwd: webDir, env: process.env });
  const output = Buffer.concat([r.stdout, r.stderr]).toString('utf8');
  const diagnostics = parseTscDiagnostics(output);
  return { ok: r.code === 0, exitCode: r.code, diagnostics, tail: r.code === 0 ? '' : tail(Buffer.from(output)) };
}

const compileInfo = c => ({ ok: c.ok, exitCode: c.exitCode, diagnosticCount: c.diagnostics.length, diagnostics: c.diagnostics.slice(0, MAX_DIAGS), tail: c.diagnostics.length === 0 ? c.tail : '' });

/** Pure: the combined verdict. A compile error counts against the hold-out only when it is located in the hold-out file. */
export function holdoutVerdict({ holdoutDiagnostics, emitted, testVerdict }) {
  if (holdoutDiagnostics > 0 || !emitted) return 'compile-error';
  return testVerdict;
}

async function scoreHoldout(entry, treeDir, scoreDir, commands, story, pin) {
  const repoPath = repoPathOf(entryPath(entry), story.srcDir);
  const copy = path.join(scoreDir, fileStem(repoPath));
  copyTree(treeDir, copy);
  await prepareCopy(copy, story, commands, pin);
  const webDir = path.join(copy, story.srcDir);
  const candidateCompile = await compile(webDir, commands); // the candidate as delivered, before the hold-out file
  writeFileMaking(path.join(copy, repoPath), await gitShow(story.repoClone, story.S, repoPath));
  const emittedPath = path.join(webDir, compiledTestPath(repoPath, story.srcDir));
  fs.rmSync(emittedPath, { force: true });
  const withHoldout = await compileKeepingOut(webDir, commands);
  const attributed = attributeDiagnostics(withHoldout.diagnostics, repoPath.slice(story.srcDir.length + 1), candidateCompile.diagnostics);
  const emitted = fs.existsSync(emittedPath);
  const test = emitted ? await nodeTest(webDir, [compiledTestPath(repoPath, story.srcDir)], path.join(copy, 'junit.xml')) : null;
  const verdict = holdoutVerdict({ holdoutDiagnostics: attributed.holdout.length, emitted, testVerdict: test?.verdict });
  return {
    path: repoPath, verdict, testVerdict: test?.verdict ?? null, emitted,
    candidateCompile: compileInfo(candidateCompile),
    withHoldout: { exitCode: withHoldout.exitCode, holdoutDiagnostics: attributed.holdout.slice(0, MAX_DIAGS), otherNewDiagnosticCount: attributed.otherNew.length },
    test,
  };
}

async function scoreOwnSuite(treeDir, scoreDir, commands, story, pin) {
  const copy = path.join(scoreDir, '_own');
  copyTree(treeDir, copy);
  await prepareCopy(copy, story, commands, pin);
  const webDir = path.join(copy, story.srcDir);
  const built = await compile(webDir, commands);
  const test = await nodeTest(webDir, ownSuiteTargets(commands), path.join(copy, 'junit.xml'));
  return { ...test, compile: compileInfo(built) };
}

function reportShed({ removed, refused }) {
  for (const p of removed) console.log(`shed ${p}`);
  for (const r of refused) console.log(`shed REFUSED ${r.target}: ${r.reason}`);
}

async function main(v) {
  const story = loadStory(v.story);
  const root = assertRunsRoot(runsRootOf(v, story));
  const cellDir = path.join(root, v.cell);
  const treeDir = v.cell === 'reference' ? await ensureReferenceTree(cellDir, v['skip-npm'], story) : path.join(cellDir, 'tree');
  if (!fs.existsSync(treeDir)) throw new Error(`no tree at ${treeDir}`);
  const scoreDir = path.join(cellDir, 'score');
  if (fs.existsSync(scoreDir)) throw new Error(`refusing: ${scoreDir} already exists`);
  fs.mkdirSync(scoreDir, { recursive: true });
  const commands = readJson(path.join(story.inputsDir, 'commands.json'));
  const pin = await gitlinkAt(story.repoClone, story.P, story.mount);
  const holdout = [];
  for (const entry of holdoutEntries(readJson(path.join(story.inputsDir, 'holdout.json')))) holdout.push(await scoreHoldout(entry, treeDir, scoreDir, commands, story, pin));
  const ownSuite = await scoreOwnSuite(treeDir, scoreDir, commands, story, pin);
  fs.writeFileSync(path.join(cellDir, 'score.json'), `${JSON.stringify({ cell: v.cell, scoredUtc: new Date().toISOString(), holdout, ownSuite }, null, 2)}\n`);
  if (v.shed) reportShed(shedCell(root, v.cell, { srcDir: story.srcDir, mount: story.mount }));
  console.log(`scored ${v.cell}: ${holdout.map(h => `${path.basename(h.path)}=${h.verdict}`).join(', ')}; own=${ownSuite.verdict}`);
}

if (process.argv[1]?.endsWith('score-cell.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node score-cell.mjs --cell <c1..c9|reference> [--story creds-176] [--runs-root dir] [--shed]');
  if (help) console.log(help);
  else {
    requireValues(values, ['cell']);
    main(values).catch(e => { console.error(e.message); process.exit(1); });
  }
}
