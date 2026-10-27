#!/usr/bin/env node
// The H arms' filler: the same interface and log as ask.mjs, but each prompt goes to Haiku through a headless Claude Code
// call that sees ONLY the prompt — no tools, a short fixed system prompt, no project settings or hooks (user settings only,
// so its effort is the same `high` as every other model), no MCP servers, no saved session, an empty working folder.
//   node ask-haiku.mjs [--no-think] [--max-tokens N] <prompt.md> [<prompt.md> ...]   (both options accepted and ignored)
// Output for X.md: X.answer.md, X.meta.json; one JSON line per request in $COAI_EXP_VLLM_LOG, with the host's own cost.
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { appendFileSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';

const LIMIT = 4;
const LOG = process.env.COAI_EXP_VLLM_LOG ?? '/home/jinx/git/coai-exp/vllm/requests.jsonl';
const CWD = '/home/jinx/git/coai-exp/haiku-cwd';
const SYSTEM = 'You write code. Answer exactly as the user message asks, in the format it asks for.';
const files = [];
const args = process.argv.slice(2);
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--max-tokens') i++;
  else if (args[i] !== '--no-think') files.push(resolve(args[i]));
}
if (files.length === 0) { console.error('usage: node ask-haiku.mjs <prompt.md> [<prompt.md> ...]'); process.exit(1); }
mkdirSync(CWD, { recursive: true });
const stem = (file) => join(dirname(file), basename(file).replace(/\.md$/i, ''));

function run(prompt) {
  return new Promise((done) => {
    const child = spawn('claude', ['-p', '--model', 'haiku', '--output-format', 'json', '--tools', '', '--system-prompt', SYSTEM,
      '--setting-sources', 'user', '--strict-mcp-config', '--no-session-persistence'], { cwd: CWD, stdio: ['pipe', 'pipe', 'pipe'] });
    const out = []; const err = [];
    child.stdout.on('data', (d) => out.push(d));
    child.stderr.on('data', (d) => err.push(d));
    child.on('close', (code) => done({ code, text: Buffer.concat(out).toString('utf8'), err: Buffer.concat(err).toString('utf8') }));
    child.stdin.end(prompt);
  });
}

async function ask(file) {
  const prompt = readFileSync(file, 'utf8');
  const started = new Date();
  const reply = await run(prompt);
  const ended = new Date();
  let r = null;
  try { r = JSON.parse(reply.text); } catch { /* recorded below */ }
  const u = r?.usage ?? {};
  const meta = {
    ts_start_utc: started.toISOString(), ts_end_utc: ended.toISOString(), wall_s: Math.round((ended - started) / 100) / 10,
    prompt_file: file, prompt_sha256: createHash('sha256').update(prompt).digest('hex'), prompt_chars: prompt.length,
    model: Object.keys(r?.modelUsage ?? {}).join(','), think: null, exit: reply.code,
    finish_reason: r && !r.is_error ? 'stop' : null,
    prompt_tokens: (u.input_tokens ?? 0) + (u.cache_read_input_tokens ?? 0) + (u.cache_creation_input_tokens ?? 0),
    completion_tokens: u.output_tokens ?? null, cost_usd: r?.total_cost_usd ?? null,
    error: r && !r.is_error ? null : (r?.result ?? (reply.err || reply.text)).toString().slice(0, 500),
  };
  if (r && !r.is_error) writeFileSync(`${stem(file)}.answer.md`, r.result ?? '');
  writeFileSync(`${stem(file)}.meta.json`, JSON.stringify(meta, null, 2));
  mkdirSync(dirname(LOG), { recursive: true });
  appendFileSync(LOG, JSON.stringify(meta) + '\n');
  console.log(`${basename(file)}: ${meta.finish_reason ?? 'ERROR'} ${meta.completion_tokens ?? '-'} tok ${meta.wall_s}s $${meta.cost_usd ?? '?'}` +
    (meta.error ? ` — ${meta.error}` : ''));
  return meta.finish_reason === 'stop';
}

const queue = [...files];
const results = [];
await Promise.all(Array.from({ length: Math.min(LIMIT, files.length) }, async () => {
  while (queue.length) results.push(await ask(queue.shift()));
}));
process.exit(results.every(Boolean) ? 0 : 2);
