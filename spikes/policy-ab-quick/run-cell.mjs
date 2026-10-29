#!/usr/bin/env node
// Launch ONE prepared cell under claude -p and collect stream, stderr, argv names, candidate diff.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { composePrompt } from './lib/prompt.mjs';
import { loadStory, runsRootOf, STORY_OPTION } from './lib/story.mjs';
import { routeDiff } from './lib/diffsplit.mjs';
import { buildChildEnv, buildClaudeArgs } from './lib/env.mjs';
import { AUTHOR_ENV, git, gitEnv } from './lib/git.mjs';
import { findOnPath, killTree } from './lib/proc.mjs';
import { parseNdjson } from './lib/summary.mjs';
import { rateLimitSignal } from './lib/ratelimit.mjs';
import { CostMeter, outcomeOf, splitLines } from './lib/watchdog.mjs';
import { PINNED_EFFORT } from './lib/settings.mjs';
import { sha256 } from './lib/tree.mjs';

const SPEC = {
  cell: { type: 'string', help: 'c1..c9' },
  story: STORY_OPTION,
  'runs-root': { type: 'string', help: "runs root (default: the story's defaultRunsRoot)" },
  cap: { type: 'string', default: '40', help: 'max budget USD' },
  'wall-min': { type: 'string', default: '120', help: 'wall limit in minutes' },
  'prompt-file': { type: 'string', default: '', help: "instrument probe only: a prompt other than the story's" },
};

function ensureSharedFiles(root) {
  const settings = path.join(root, 'pinned-effort.json');
  const mcp = path.join(root, 'empty-mcp.json');
  const gitconfig = path.join(root, 'empty.gitconfig');
  if (!fs.existsSync(settings)) fs.writeFileSync(settings, `${JSON.stringify(PINNED_EFFORT)}\n`);
  if (!fs.existsSync(mcp)) fs.writeFileSync(mcp, '{"mcpServers":{}}\n');
  if (!fs.existsSync(gitconfig)) fs.writeFileSync(gitconfig, '');
  return { settings, mcp, gitconfig };
}

const treeGitEnv = root => gitEnv({ ...AUTHOR_ENV, GIT_CONFIG_GLOBAL: path.join(root, 'empty.gitconfig') });

/** Refuse a rerun or a dirty tree: a cell is one generation of one clean tree. */
export async function assertFresh(cellDir, treeDir, root) {
  if (fs.existsSync(path.join(cellDir, 'stream.ndjson'))) throw new Error(`refusing: ${cellDir} already has a stream.ndjson (a cell runs once)`);
  const status = (await git(treeDir, ['status', '--porcelain'], treeGitEnv(root))).toString('utf8').trim();
  if (status !== '') throw new Error(`refusing: tree is not clean before launch:\n${status.slice(0, 600)}`);
}

/**
 * Launch and watch. Two kills, both of the process tree of the PID started here (never by image name): the wall limit, and the
 * cost watchdog - the stream is metered with the fallback rates and the tree is killed at cap x 1.1, because the CLI's own budget
 * is checked between turns and parallel children can overshoot it.
 */
function launch(exe, args, { cwd, env, prompt, streamPath, stderrPath, wallMs, capUsd }) {
  return new Promise((resolve, reject) => {
    const out = fs.createWriteStream(streamPath);
    const err = fs.createWriteStream(stderrPath);
    // detached on POSIX: the timers below kill the child's process GROUP (lib/proc.mjs killTree).
    const child = spawn(exe, args, { cwd, env, shell: false, windowsHide: true, detached: process.platform !== 'win32' });
    const meter = new CostMeter();
    let timedOut = false;
    let overCap = false;
    let carry = '';
    const timer = setTimeout(() => { timedOut = true; killTree(child.pid); }, wallMs);
    child.stdout.pipe(out);
    child.stdout.on('data', chunk => {
      const split = splitLines(carry, chunk.toString('utf8'));
      carry = split.carry;
      for (const line of split.lines) meter.feed(line);
      if (!overCap && meter.overCap(capUsd)) { overCap = true; killTree(child.pid); }
    });
    child.stderr.pipe(err);
    child.on('error', reject);
    child.on('close', code => { clearTimeout(timer); resolve({ code, timedOut, overCap, meterUsd: meter.usd, pid: child.pid }); });
    child.stdin.on('error', () => {});
    child.stdin.end(prompt);
  });
}

/** The candidate is everything since the PREPARED commit, so a session that commits still yields its work. */
async function collectCandidate(treeDir, cellDir, root, baseCommit, story) {
  const env = treeGitEnv(root);
  await git(treeDir, ['-c', 'core.autocrlf=false', 'add', '-A'], env);
  const diff = (await git(treeDir, ['-c', 'core.autocrlf=false', 'diff', '--cached', '--binary', baseCommit], env)).toString('utf8');
  const routed = routeDiff(diff, story.layout === 'resolver' ? ['.agents'] : []);
  fs.writeFileSync(path.join(cellDir, 'candidate.diff'), routed.candidate);
  fs.writeFileSync(path.join(cellDir, 'excluded.diff'), routed.excluded);
  return { candidateFiles: routed.candidateFiles, excludedFiles: routed.excludedFiles };
}

async function main(v) {
  const story = loadStory(v.story);
  const root = runsRootOf(v, story);
  const cellDir = path.join(root, v.cell);
  const cellJson = JSON.parse(fs.readFileSync(path.join(cellDir, 'cell.json'), 'utf8'));
  const treeDir = path.join(cellDir, 'tree');
  const files = ensureSharedFiles(root);
  await assertFresh(cellDir, treeDir, root);
  const env = buildChildEnv(process.env, { emptyGitConfig: files.gitconfig });
  const args = buildClaudeArgs({ settingsPath: files.settings, mcpPath: files.mcp, cap: v.cap, hookEvents: story.layout === 'resolver' });
  const exe = findOnPath('claude', process.env[Object.keys(process.env).find(k => k.toLowerCase() === 'path')], ['.exe']);
  if (!exe) throw new Error('claude.exe not found on PATH');
  fs.writeFileSync(path.join(cellDir, 'argv.json'), `${JSON.stringify({ exe: path.basename(exe), args, envVarNames: Object.keys(env).sort() }, null, 2)}\n`);
  const startedUtc = new Date().toISOString();
  const t0 = Date.now();
  const result = await launch(exe, args, {
    cwd: treeDir, env, prompt: composePrompt(fs.readFileSync(v['prompt-file'] || story.promptFile, 'utf8'), story.parallelHint),
    streamPath: path.join(cellDir, 'stream.ndjson'), stderrPath: path.join(cellDir, 'stderr.txt'), wallMs: Number(v['wall-min']) * 60_000, capUsd: Number(v.cap),
  });
  const limit = rateLimitSignal(parseNdjson(fs.readFileSync(path.join(cellDir, 'stream.ndjson'), 'utf8')));
  const run = { story: story.id, startedUtc, endedUtc: new Date().toISOString(), wallMs: Date.now() - t0, exitCode: result.code, timedOut: result.timedOut, overCap: result.overCap, outcome: outcomeOf({ exitCode: result.code, timedOut: result.timedOut, overCap: result.overCap, rateLimited: limit.void }), ...(limit.void ? { rateLimit: limit } : {}), meterUsdEstimated: result.meterUsd, pid: result.pid, wallMin: Number(v['wall-min']), capUsd: Number(v.cap), pinnedEffortSha256: sha256(fs.readFileSync(files.settings)) };
  const candidate = await collectCandidate(treeDir, cellDir, root, cellJson.baseCommit, story);
  const auditSrc = path.join(treeDir, '.git', 'audit.ndjson');
  if (fs.existsSync(auditSrc)) fs.copyFileSync(auditSrc, path.join(cellDir, 'audit.ndjson'));
  fs.writeFileSync(path.join(cellDir, 'run.json'), `${JSON.stringify({ ...run, ...candidate }, null, 2)}\n`);
  console.log(`cell ${v.cell}: ${run.outcome}, exit ${run.exitCode}, wall ${run.wallMs} ms, meter ~$${run.meterUsdEstimated}`);
}

if (process.argv[1]?.endsWith('run-cell.mjs')) {
  const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node run-cell.mjs --cell <c1..c9> [--story creds-176] [--runs-root dir] [--cap 40] [--wall-min 120]');
  if (help) console.log(help);
  else {
    requireValues(values, ['cell']);
    main(values).catch(e => { console.error(e.message); process.exit(1); });
  }
}
