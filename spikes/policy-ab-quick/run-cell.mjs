#!/usr/bin/env node
// Launch ONE prepared cell under claude -p and collect stream, stderr, argv names, candidate diff.
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { parseCli, requireValues } from './lib/args.mjs';
import { SPIKE_DIR } from './lib/consts.mjs';
import { routeDiff } from './lib/diffsplit.mjs';
import { buildChildEnv, buildClaudeArgs } from './lib/env.mjs';
import { AUTHOR_ENV, git, gitEnv } from './lib/git.mjs';
import { findOnPath, killTree } from './lib/proc.mjs';
import { PINNED_EFFORT } from './lib/settings.mjs';

const SPEC = {
  cell: { type: 'string', help: 'A1 | B | A2' },
  'runs-root': { type: 'string', help: 'runs root' },
  cap: { type: 'string', default: '40', help: 'max budget USD' },
  'wall-min': { type: 'string', default: '120', help: 'wall limit in minutes' },
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

function launch(exe, args, { cwd, env, prompt, streamPath, stderrPath, wallMs }) {
  return new Promise((resolve, reject) => {
    const out = fs.createWriteStream(streamPath);
    const err = fs.createWriteStream(stderrPath);
    const child = spawn(exe, args, { cwd, env, shell: false, windowsHide: true });
    let timedOut = false;
    const timer = setTimeout(() => { timedOut = true; killTree(child.pid); }, wallMs);
    child.stdout.pipe(out);
    child.stderr.pipe(err);
    child.on('error', reject);
    child.on('close', code => { clearTimeout(timer); resolve({ code, timedOut, pid: child.pid }); });
    child.stdin.on('error', () => {});
    child.stdin.end(prompt);
  });
}

async function collectCandidate(treeDir, cellDir) {
  const env = gitEnv({ ...AUTHOR_ENV, GIT_CONFIG_GLOBAL: path.join(path.dirname(cellDir), 'empty.gitconfig') });
  await git(treeDir, ['-c', 'core.autocrlf=false', 'add', '-A'], env);
  const diff = (await git(treeDir, ['-c', 'core.autocrlf=false', 'diff', '--cached', '--binary', 'HEAD'], env)).toString('utf8');
  const routed = routeDiff(diff);
  fs.writeFileSync(path.join(cellDir, 'candidate.diff'), routed.candidate);
  fs.writeFileSync(path.join(cellDir, 'excluded.diff'), routed.excluded);
  return { candidateFiles: routed.candidateFiles, excludedFiles: routed.excludedFiles };
}

async function main(v) {
  const root = path.resolve(v['runs-root']);
  const cellDir = path.join(root, v.cell);
  const cellJson = JSON.parse(fs.readFileSync(path.join(cellDir, 'cell.json'), 'utf8'));
  const treeDir = path.join(cellDir, 'tree');
  const files = ensureSharedFiles(root);
  const env = buildChildEnv(process.env, { arm: cellJson.arm, emptyGitConfig: files.gitconfig });
  const args = buildClaudeArgs({ settingsPath: files.settings, mcpPath: files.mcp, cap: v.cap });
  const pathKey = Object.keys(env).find(k => k.toLowerCase() === 'path');
  const exe = findOnPath('claude', process.env[Object.keys(process.env).find(k => k.toLowerCase() === 'path')], ['.exe']);
  if (!exe) throw new Error('claude.exe not found on PATH');
  fs.writeFileSync(path.join(cellDir, 'argv.json'), `${JSON.stringify({ exe: path.basename(exe), args, envVarNames: Object.keys(env).sort(), pathKey }, null, 2)}\n`);
  const startedUtc = new Date().toISOString();
  const t0 = Date.now();
  const result = await launch(exe, args, {
    cwd: treeDir, env, prompt: fs.readFileSync(path.join(SPIKE_DIR, 'prompt.md'), 'utf8'),
    streamPath: path.join(cellDir, 'stream.ndjson'), stderrPath: path.join(cellDir, 'stderr.txt'), wallMs: Number(v['wall-min']) * 60_000,
  });
  const run = { startedUtc, endedUtc: new Date().toISOString(), wallMs: Date.now() - t0, exitCode: result.code, timedOut: result.timedOut, pid: result.pid };
  const candidate = await collectCandidate(treeDir, cellDir);
  const auditSrc = path.join(treeDir, '.git', 'policy-audit.ndjson');
  if (fs.existsSync(auditSrc)) fs.copyFileSync(auditSrc, path.join(cellDir, 'policy-audit.ndjson'));
  fs.writeFileSync(path.join(cellDir, 'run.json'), `${JSON.stringify({ ...run, ...candidate }, null, 2)}\n`);
  console.log(`cell ${v.cell}: exit ${run.exitCode}, wall ${run.wallMs} ms${run.timedOut ? ' (WALL TIMEOUT)' : ''}`);
}

const { help, values } = parseCli(process.argv.slice(2), SPEC, 'node run-cell.mjs --cell <A1|B|A2> --runs-root <dir> [--cap 40] [--wall-min 120]');
if (help) console.log(help);
else {
  requireValues(values, ['cell', 'runs-root']);
  main(values).catch(e => { console.error(e.message); process.exit(1); });
}
