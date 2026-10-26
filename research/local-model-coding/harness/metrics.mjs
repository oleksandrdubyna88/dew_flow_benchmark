#!/usr/bin/env node
// Reads the experiment's raw records and prints time, cost and tokens per arm, step, attempt and model, plus vLLM usage.
//   node metrics.mjs [--json]
// Sources: results/<arm>/timing.jsonl and <step>.a<N>.jsonl (the host's own stream: the `result` event carries
// duration, total_cost_usd and modelUsage), vllm/requests-<arm>.jsonl (one line per local-model request).
// An attempt with no `result` event (interrupted) has no host cost: its tokens are summed from its assistant messages,
// one count per message id (its last event), and its cost is reported as null — never guessed.
import { existsSync, readFileSync, statSync } from 'node:fs';
import { execSync } from 'node:child_process';

const E = '/home/jinx/git/coai-exp';
const ARMS = ['V', 'C', 'C2', 'VT', 'C3', 'C4', 'Q1', 'Q2', 'Q3', 'Q4', 'H1', 'H2', 'H3', 'H4'].filter((a) => existsSync(`${E}/results/${a}/timing.jsonl`));
// each arm's steps, in the order they first started (classic: code, review, fix; Q: skeleton, fills, polish, review, fixes)
const stepsOf = (timing) => [...new Set(timing.filter((t) => t.kind === 'start').map((t) => t.step))];
const lines = (f) => existsSync(f) ? readFileSync(f, 'utf8').split('\n').filter(Boolean).flatMap((l) => {
  try { return [JSON.parse(l)]; } catch { return []; }
}) : [];

const zero = () => ({ input: 0, output: 0, cacheRead: 0, cacheWrite: 0 });
const add = (a, b) => ({ input: a.input + b.input, output: a.output + b.output, cacheRead: a.cacheRead + b.cacheRead,
  cacheWrite: a.cacheWrite + b.cacheWrite });
const total = (t) => t.input + t.output + t.cacheRead + t.cacheWrite;

function attemptOf(stream, window) {
  const events = lines(stream);
  const result = events.filter((e) => e.type === 'result').pop();
  const byModel = {};
  let cost = null;
  if (result) {
    cost = result.total_cost_usd;
    for (const [model, u] of Object.entries(result.modelUsage ?? {})) {
      byModel[model] = { input: u.inputTokens, output: u.outputTokens, cacheRead: u.cacheReadInputTokens,
        cacheWrite: u.cacheCreationInputTokens, cost: u.costUSD };
    }
  } else {
    // The stream keeps only a message's START usage (output 955 where the host counted 44 090 — checked on C's step 1),
    // so an interrupted attempt is read from its session transcript, inside the attempt's own time window.
    const transcript = window && execSync(`ls ${process.env.HOME}/.claude/projects/*/${window.session}.jsonl 2>/dev/null || true`, { encoding: 'utf8' }).trim().split('\n')[0];
    const source = transcript ? lines(transcript).filter((e) => {
      const t = Date.parse(e.timestamp ?? ''); return t >= window.from && t <= window.to;
    }) : events;
    const last = new Map(); // a message may appear in several events: its LAST one counts
    for (const e of source) if (e.type === 'assistant' && e.message?.usage) last.set(e.message.id, e);
    for (const e of last.values()) {
      const u = e.message.usage;
      const m = byModel[e.message.model] ??= { ...zero(), cost: null };
      Object.assign(m, add(m, { input: u.input_tokens ?? 0, output: u.output_tokens ?? 0,
        cacheRead: u.cache_read_input_tokens ?? 0, cacheWrite: u.cache_creation_input_tokens ?? 0 }));
    }
  }
  const stamps = events.map((e) => Date.parse(e.timestamp ?? '')).filter(Number.isFinite);
  // the stream's last write, not only its last stamped event: a tool result (a long test run) carries no timestamp
  const lastEventMs = Math.max(...stamps, existsSync(stream) ? statSync(stream).mtimeMs : -Infinity);
  return { complete: !!result, cost, byModel, lastEventMs, turns: result?.num_turns ?? null, durationMs: result?.duration_ms ?? null,
    subagents: result?.subagent_stats?.spawned ?? null, denials: result?.permission_denials?.length ?? null,
    terminal: result?.terminal_reason ?? 'interrupted' };
}

function vllmOf(arm) {
  const reqs = lines(`${E}/vllm/requests-${arm}.jsonl`);
  if (reqs.length === 0) return null;
  const spans = reqs.map((r) => [Date.parse(r.ts_start_utc), Date.parse(r.ts_end_utc)]).sort((a, b) => a[0] - b[0]);
  let busy = 0; let [s, e] = spans[0];
  for (const [a, b] of spans.slice(1)) { if (a > e) { busy += e - s; [s, e] = [a, b]; } else e = Math.max(e, b); }
  busy += e - s;
  const count = (k) => reqs.reduce((n, r) => n + (r[k] ?? 0), 0);
  const by = (k) => reqs.reduce((o, r) => ({ ...o, [r[k]]: (o[r[k]] ?? 0) + 1 }), {});
  return { requests: reqs.length, promptTokens: count('prompt_tokens'), completionTokens: count('completion_tokens'),
    busySeconds: Math.round(busy / 1000), sumWallSeconds: Math.round(count('wall_s')), finish: by('finish_reason'),
    think: by('think'), downRetries: count('down_retries') };
}

const report = {};
for (const arm of ARMS) {
  const timing = lines(`${E}/results/${arm}/timing.jsonl`);
  const steps = {};
  for (const step of stepsOf(timing)) {
    const starts = timing.filter((t) => t.step === step && t.kind === 'start');
    steps[step] = starts.map((s) => {
      const end = timing.find((t) => t.step === step && t.kind === 'end' && t.attempt === s.attempt);
      const next = starts.find((t) => t.attempt === s.attempt + 1);
      const a = attemptOf(s.stream, { session: s.session, from: Date.parse(s.start_utc),
        to: next ? Date.parse(next.start_utc) : Date.now() });
      // wall time: the runner's own end line; else the host's duration (finished, runner died); else the last event (crashed)
      let wallSeconds = end?.wall_s ?? null; let wallSource = 'runner';
      if (wallSeconds == null && a.durationMs != null) { wallSeconds = Math.round(a.durationMs / 1000); wallSource = 'host duration'; }
      if (wallSeconds == null && !end && a.lastEventMs != null && a.lastEventMs - Date.parse(s.start_utc) < 6 * 3600_000 &&
          timing.some((t) => t.step === step && t.kind === 'start' && t.attempt > s.attempt)) {
        wallSeconds = Math.round((a.lastEventMs - Date.parse(s.start_utc)) / 1000); wallSource = 'est. (start → last event)';
      }
      if (s.how === 'script') { // a fill step: no Claude session; its cost is the filler's requests inside its time window
        const from = Date.parse(s.start_utc); const to = end ? Date.parse(end.end_utc) : Date.now();
        const reqs = lines(`${E}/vllm/requests-${arm}.jsonl`).filter((q) => { const t = Date.parse(q.ts_start_utc); return t >= from && t <= to; });
        const byModel = {};
        for (const q of reqs.filter((x) => x.cost_usd != null)) {
          const m = byModel[q.model] ??= { ...zero(), cost: 0 };
          m.input += q.prompt_tokens ?? 0; m.output += q.completion_tokens ?? 0; m.cost += q.cost_usd;
        }
        return { attempt: s.attempt, how: s.how, model: s.model, start: s.start_utc, end: end?.end_utc ?? null, wallSeconds, wallSource,
          exit: end?.exit ?? null, headBefore: s.head, headAfter: end?.head ?? null, complete: true, byModel, requests: reqs.length,
          cost: Object.values(byModel).reduce((n, u) => n + u.cost, 0), turns: null, subagents: null, denials: null, terminal: 'script' };
      }
      return { attempt: s.attempt, how: s.how, model: s.model, start: s.start_utc, end: end?.end_utc ?? null,
        wallSeconds, wallSource, exit: end?.exit ?? null, headBefore: s.head, headAfter: end?.head ?? null, ...a };
    });
  }
  report[arm] = { steps, vllm: vllmOf(arm) };
}

// Unit prices per model, fitted on the attempts whose host cost is known (least squares over output, cache read and
// cache write + input), and checked against those same attempts before any estimate uses them.
const prices = {};
{
  const rows = {};
  for (const r of Object.values(report)) for (const a of Object.values(r.steps).flat()) if (a.complete && a.how !== 'script') { // filler requests carry their own host cost
    for (const [m, u] of Object.entries(a.byModel)) if (u.cost > 0) (rows[m] ??= []).push([[u.output, u.cacheRead, u.cacheWrite + u.input], u.cost]);
  }
  for (const [m, rs] of Object.entries(rows)) {
    if (rs.length < 3) continue;
    const A = [[0, 0, 0], [0, 0, 0], [0, 0, 0]]; const b = [0, 0, 0];
    for (const [x, y] of rs) for (let i = 0; i < 3; i++) { b[i] += x[i] * y; for (let j = 0; j < 3; j++) A[i][j] += x[i] * x[j]; }
    for (let i = 0; i < 3; i++) for (let k2 = i + 1; k2 < 3; k2++) { const f = A[k2][i] / A[i][i]; for (let j = i; j < 3; j++) A[k2][j] -= f * A[i][j]; b[k2] -= f * b[i]; }
    const p = [0, 0, 0]; for (let i = 2; i >= 0; i--) { p[i] = (b[i] - [0, 1, 2].filter((j) => j > i).reduce((s, j) => s + A[i][j] * p[j], 0)) / A[i][i]; }
    const maxRelErr = Math.max(...rs.map(([x, y]) => Math.abs(x[0] * p[0] + x[1] * p[1] + x[2] * p[2] - y) / y));
    prices[m] = { perMtok: { output: p[0] * 1e6, cacheRead: p[1] * 1e6, cacheWriteAndInput: p[2] * 1e6 }, fittedOn: rs.length, maxRelErr };
  }
}
const estimate = (m, u) => { const p = prices[m]?.perMtok; return p ? (u.output * p.output + u.cacheRead * p.cacheRead + (u.cacheWrite + u.input) * p.cacheWriteAndInput) / 1e6 : null; };

for (const r of Object.values(report)) {
  const all = Object.values(r.steps).flat();
  for (const a of all) if (!a.complete) {
    for (const [m, u] of Object.entries(a.byModel)) u.costEst = estimate(m, u);
    a.costEst = Object.values(a.byModel).reduce((n, u) => (n == null || u.costEst == null ? null : n + u.costEst), 0);
  }
  const models = {};
  for (const a of all) for (const [m, u] of Object.entries(a.byModel)) {
    const t = models[m] ??= { ...zero(), cost: 0, costEstimated: false };
    Object.assign(t, add(t, u));
    if (u.cost != null) t.cost += u.cost; else if (u.costEst != null) { t.cost += u.costEst; t.costEstimated = true; } else t.cost = NaN;
  }
  Object.assign(r, { wallSeconds: all.reduce((n, a) => n + (a.wallSeconds ?? 0), 0),
    wallEstimated: all.some((a) => a.wallSource !== 'runner'),
    cost: all.reduce((n, a) => n + (a.cost ?? a.costEst ?? NaN), 0), costEstimated: all.some((a) => a.cost == null),
    tokens: Object.values(models).reduce((t, u) => add(t, u), zero()), models });
}

if (process.argv.includes('--json')) { console.log(JSON.stringify({ prices, report }, null, 2)); process.exit(0); }
const usd = (n) => n == null || Number.isNaN(n) ? 'n/a' : `$${n.toFixed(2)}`;
const k = (n) => `${(n / 1000).toFixed(1)}k`;
console.log('Fitted unit prices ($ per Mtok; checked on the attempts with a host cost):');
for (const [m, p] of Object.entries(prices)) console.log(`  ${m}: out ${p.perMtok.output.toFixed(2)}, cacheRead ${p.perMtok.cacheRead.toFixed(3)}, cacheWrite+in ${p.perMtok.cacheWriteAndInput.toFixed(2)} — fitted on ${p.fittedOn}, max error ${(p.maxRelErr * 100).toFixed(1)} %`);
for (const [arm, r] of Object.entries(report)) {
  console.log(`\n=== Arm ${arm}: ${Math.round(r.wallSeconds / 60)} min work${r.wallEstimated ? ' (part est.)' : ''}, ${usd(r.cost)}${r.costEstimated ? ' (part est.)' : ''}, ${k(total(r.tokens))} Claude tokens`);
  for (const [step, attempts] of Object.entries(r.steps)) for (const a of attempts) {
    const wall = a.wallSeconds == null ? 'running' : `${Math.round(a.wallSeconds / 60)} min${a.wallSource === 'runner' ? '' : ` [${a.wallSource}]`}`;
    console.log(`  ${step} a${a.attempt} (${a.how}, ${a.model}): ${wall}, ` +
      `${a.cost != null ? usd(a.cost) : `${usd(a.costEst)} est.`}, turns ${a.turns ?? '-'}, subagents ${a.subagents ?? '-'}, denials ${a.denials ?? '-'}, ${a.terminal}, ${a.headBefore}→${a.headAfter ?? '?'}`);
    for (const [m, u] of Object.entries(a.byModel)) {
      console.log(`      ${m}: in ${k(u.input)} out ${k(u.output)} cacheRead ${k(u.cacheRead)} cacheWrite ${k(u.cacheWrite)} ${u.cost != null ? usd(u.cost) : `${usd(u.costEst)} est.`}`);
    }
  }
  if (r.vllm) console.log(`  vLLM: ${r.vllm.requests} requests, prompt ${k(r.vllm.promptTokens)} + completion ${k(r.vllm.completionTokens)} tokens, ` +
    `busy ${Math.round(r.vllm.busySeconds / 60)} min (sum of request times ${Math.round(r.vllm.sumWallSeconds / 60)} min), finish ${JSON.stringify(r.vllm.finish)}, think ${JSON.stringify(r.vllm.think)}, down-retries ${r.vllm.downRetries}`);
}
