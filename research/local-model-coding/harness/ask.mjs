#!/usr/bin/env node
// Sends prompt files to the local vLLM (Windows localhost:8000, reached through curl.exe) at most 4 at a time,
// writes each answer beside the prompt, and appends one JSON line per request to the experiment's log.
//
//   node ask.mjs [--max-tokens N] [--no-think] <prompt.md> [<prompt.md> ...]
//
// Output for prompt X.md: X.answer.md (the answer), X.reasoning.md (the thinking), X.meta.json (usage, timing).
// Exit code: 0 when every request answered with finish_reason "stop", 2 otherwise, 1 on a usage error.
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { appendFileSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';

const CURL = '/mnt/c/Windows/System32/curl.exe';
const URL = 'http://localhost:8000/v1/chat/completions';
const MODEL = 'qwen3.6-35b-a3b';
const LIMIT = 4;
const LOG = process.env.COAI_EXP_VLLM_LOG ?? '/home/jinx/git/coai-exp/vllm/requests.jsonl';
// Arm VT pins thinking ON in the harness: --no-think is then ignored, and each request logs that it asked for it.
const FORCE_THINK = process.env.COAI_EXP_FORCE_THINK === '1';

const args = process.argv.slice(2);
let maxTokens = 14000;
let think = true;
let noThinkIgnored = false;
const files = [];
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--max-tokens') maxTokens = Number(args[++i]);
  else if (args[i] === '--no-think') { if (FORCE_THINK) noThinkIgnored = true; else think = false; }
  else files.push(resolve(args[i]));
}
if (noThinkIgnored) console.error('--no-think is ignored in this run: thinking is always on');
if (files.length === 0 || !Number.isInteger(maxTokens) || maxTokens <= 0) {
  console.error('usage: node ask.mjs [--max-tokens N] [--no-think] <prompt.md> [<prompt.md> ...]');
  process.exit(1);
}

const stem = (file) => join(dirname(file), basename(file).replace(/\.md$/i, ''));

function post(body) {
  return new Promise((done) => {
    const child = spawn(CURL, ['-s', '-m', '1800', '-H', 'Content-Type: application/json; charset=utf-8',
      '--data-binary', '@-', URL], { stdio: ['pipe', 'pipe', 'pipe'] });
    const out = [];
    const err = [];
    child.stdout.on('data', (d) => out.push(d));
    child.stderr.on('data', (d) => err.push(d));
    child.on('close', (code) => done({ code, text: Buffer.concat(out).toString('utf8'), err: Buffer.concat(err).toString('utf8') }));
    child.stdin.end(Buffer.from(JSON.stringify(body), 'utf8'));
  });
}

// curl: 7 = could not connect, 52 = empty reply, 56 = connection lost — vLLM is down or restarting, not a bad prompt.
const DOWN = new Set([7, 52, 56]);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function healthy() {
  const r = await new Promise((done) => {
    const c = spawn(CURL, ['-s', '-m', '5', '-o', '/dev/null', '-w', '%{http_code}', URL.replace('/v1/chat/completions', '/health')]);
    const out = [];
    c.stdout.on('data', (d) => out.push(d));
    c.on('close', () => done(Buffer.concat(out).toString()));
  });
  return r === '200';
}

async function postWhileUp(body) { // waits up to 15 min for vLLM to come back, at most 3 tries
  let reply;
  let retries = 0;
  for (let attempt = 1; attempt <= 3; attempt++) {
    reply = await post(body);
    if (!DOWN.has(reply.code)) break;
    retries++;
    console.error(`vLLM is not answering (curl ${reply.code}); waiting for it to come back before retrying — do not work around it`);
    const until = Date.now() + 15 * 60_000;
    while (Date.now() < until && !(await healthy())) await sleep(15_000);
  }
  return { ...reply, retries };
}

async function ask(file) {
  const prompt = readFileSync(file, 'utf8');
  const body = { model: MODEL, messages: [{ role: 'user', content: prompt }], max_tokens: maxTokens,
    ...(think ? {} : { chat_template_kwargs: { enable_thinking: false } }) };
  const started = new Date();
  const reply = await postWhileUp(body);
  const ended = new Date();
  let parsed = null;
  try { parsed = JSON.parse(reply.text); } catch { /* recorded below as an error */ }
  const choice = parsed?.choices?.[0];
  const meta = {
    ts_start_utc: started.toISOString(), ts_end_utc: ended.toISOString(),
    wall_s: Math.round((ended - started) / 100) / 10,
    prompt_file: file, prompt_sha256: createHash('sha256').update(prompt).digest('hex'), prompt_chars: prompt.length,
    think, no_think_ignored: noThinkIgnored, max_tokens: maxTokens, curl_exit: reply.code, down_retries: reply.retries,
    finish_reason: choice?.finish_reason ?? null,
    prompt_tokens: parsed?.usage?.prompt_tokens ?? null, completion_tokens: parsed?.usage?.completion_tokens ?? null,
    error: choice ? null : (parsed?.error?.message ?? parsed?.message ?? (reply.err || reply.text).slice(0, 500)),
  };
  if (choice) {
    writeFileSync(`${stem(file)}.answer.md`, choice.message?.content ?? '');
    writeFileSync(`${stem(file)}.reasoning.md`, choice.message?.reasoning ?? choice.message?.reasoning_content ?? '');
  }
  writeFileSync(`${stem(file)}.meta.json`, JSON.stringify(meta, null, 2));
  mkdirSync(dirname(LOG), { recursive: true });
  appendFileSync(LOG, JSON.stringify(meta) + '\n');
  console.log(`${basename(file)}: ${meta.finish_reason ?? 'ERROR'} ${meta.completion_tokens ?? '-'} tok ${meta.wall_s}s` +
    (meta.error ? ` — ${meta.error}` : ` → ${stem(file)}.answer.md`));
  return meta.finish_reason === 'stop';
}

const queue = [...files];
const results = [];
await Promise.all(Array.from({ length: Math.min(LIMIT, files.length) }, async () => {
  while (queue.length) results.push(await ask(queue.shift()));
}));
process.exit(results.every(Boolean) ? 0 : 2);
