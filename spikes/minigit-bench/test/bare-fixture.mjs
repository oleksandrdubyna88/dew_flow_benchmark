// The bare-smoke fixture laid into a session's directory (and variants of it) and a fake score: what a stub `launchSession` leaves behind, shared by the tests that drive the
// runner's loop without a model.
import { appendFileSync, copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { STAGED } from '../lib/smoke-load.mjs';
import { spikeDir } from './helpers.mjs';

export const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
export const SCORE = { build: { ok: true }, visible: [{ script: 'test-v1.sh', passed: 10, failed: 0, expectedTotal: 10, summaryCaptured: true, buildFailed: false, exitCode: 0, timedOut: false, signal: null }],
  heldout: { known: true, primary100: true, totals: { total: 29, passed: 29, failed: 0, blocked: 0, percent: 100 }, defects: 0, latency: { known: false, why: 'stub' } }, upstream: [] };

/** The fixture laid into a session's directory; `variant` 'upstream' ends the session on a 529 (a registered void). */
export function lay(layout, variant) {
  for (const [rel, flat] of Object.entries(STAGED)) {
    if (!existsSync(path.join(FIXTURE, flat))) continue;
    mkdirSync(path.dirname(path.join(layout.dir, rel)), { recursive: true });
    copyFileSync(path.join(FIXTURE, flat), path.join(layout.dir, rel));
  }
  cpSync(path.join(FIXTURE, 'transcripts'), layout.transcripts, { recursive: true });
  if (variant === 'upstream') {
    const lines = readFileSync(layout.files.stream, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
    const i = lines.findLastIndex(e => e.type === 'result');
    lines[i] = { ...lines[i], is_error: true, api_error_status: 529 };
    writeFileSync(layout.files.stream, `${lines.map(l => JSON.stringify(l)).join('\n')}\n`);
    appendFileSync(layout.files.brokerLog, `${JSON.stringify({ t: '2026-10-30T10:30:00Z', seq: 99, event: 'forwarding' })}\n${JSON.stringify({ t: '2026-10-30T10:30:01Z', seq: 99, event: 'settled', status: 529, usage: null, chargedUsd: 0 })}\n`);
  }
  if (variant === 'ratelimit') { // the account's usage window is spent: the CLI ends with a 429 envelope and the limit sentence
    const lines = readFileSync(layout.files.stream, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
    const i = lines.findLastIndex(e => e.type === 'result');
    lines[i] = { ...lines[i], subtype: 'success', is_error: true, api_error_status: 429, result: 'You have hit your usage limit; it resets at 05:00' };
    writeFileSync(layout.files.stream, `${lines.map(l => JSON.stringify(l)).join('\n')}\n`);
  }
  if (variant === 't1') { // the envelope disagrees with itself: total_cost_usd is not the sum of its modelUsage
    const lines = readFileSync(layout.files.stream, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
    const i = lines.findLastIndex(e => e.type === 'result');
    lines[i] = { ...lines[i], total_cost_usd: lines[i].total_cost_usd + 1 };
    writeFileSync(layout.files.stream, `${lines.map(l => JSON.stringify(l)).join('\n')}\n`);
  }
  if (variant === 't2') { // half of the OTel records never arrived: O does not match the envelope
    const file = path.join(layout.otel, 'otel-raw.ndjson');
    const rows = readFileSync(file, 'utf8').split('\n').filter(Boolean);
    writeFileSync(file, `${rows.slice(0, Math.ceil(rows.length / 2)).join('\n')}\n`);
  }
  if (variant === 't3' || variant === 't3-t5') { // a transcript message is longer than the OTel record that names it: T at the CLI's rates disagrees with O (T3), and the broker's ledger disagrees with the envelope (T5)
    const walk = d => readdirSync(d, { withFileTypes: true }).flatMap(e => (e.isDirectory() ? walk(path.join(d, e.name)) : [path.join(d, e.name)]));
    const main = walk(layout.transcripts).find(f => f.endsWith('.jsonl') && !f.includes(`${path.sep}subagents${path.sep}`));
    writeFileSync(main, readFileSync(main, 'utf8').replace(/"output_tokens":(\d+)/, (m, n) => `"output_tokens":${Number(n) + 50000}`));
  }
  if (variant === 't5') { // the broker's sealed ledger says it spent more than the envelope reports: B is not E
    const lines = readFileSync(layout.files.brokerLog, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
    const i = lines.findLastIndex(l => l.event === 'summary' && l.sealed === true); // the sealed summary is what the ledger's view reads as B
    lines[i] = { ...lines[i], spentUsd: lines[i].spentUsd + 0.5 };
    writeFileSync(layout.files.brokerLog, `${lines.map(l => JSON.stringify(l)).join('\n')}\n`);
  }
  if (variant === 'corrupt') writeFileSync(layout.files.stream, 'this is not json\n'); // a collection that cannot read its evidence
  if (variant === 'auth') {
    appendFileSync(layout.files.brokerLog, `${JSON.stringify({ t: '2026-10-30T10:30:00Z', seq: 99, event: 'forwarding' })}\n${JSON.stringify({ t: '2026-10-30T10:30:01Z', seq: 99, event: 'settled', status: 401, usage: null, chargedUsd: 0 })}\n`);
  }
}

