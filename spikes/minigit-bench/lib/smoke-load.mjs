// The bare-session smoke as files: staging a session directory into the flat, capture-shaped layout (so E1's loaders and the sanitiser read
// it) and loading it back. I/O at the edge; the verdicts over what is loaded are pure (smoke-verdicts.mjs).
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { agentEntries, readAgentFileUnder } from './agent-files.mjs';
import { fail, ok } from './result.mjs';

/** session-dir relative path -> flat name. `otel/otel-raw.ndjson` is the file sink's line shape under the capture's name. */
export const STAGED = Object.freeze({
  'stream.ndjson': 'stream.ndjson', 'stream.stamped.ndjson': 'stream.stamped.ndjson', 'otel/otel-raw.ndjson': 'otlp-raw.ndjson', 'broker.ndjson': 'broker.ndjson',
  'egress.ndjson': 'egress.ndjson', 'box/out/audit.ndjson': 'audit.ndjson', 'watchdog.ndjson': 'watchdog.ndjson', 'argv.json': 'argv.json', 'meta.json': 'meta.json',
  'readback.json': 'readback.json', 'checks.json': 'checks.json', 'egress.json': 'egress.json', 'broker.json': 'broker.json', 'stderr.txt': 'stderr.txt',
});
export const REQUIRED_STAGED = Object.freeze(['stream.ndjson', 'otlp-raw.ndjson', 'argv.json', 'meta.json', 'broker.ndjson', 'egress.ndjson', 'readback.json', 'checks.json']);

/** The `{ rel, dest, required }` rows of what a session dir stages: the named files, then every file under `transcripts/`. A non-regular transcript entry is `odd`. */
function stagingPlan(from) {
  const named = Object.entries(STAGED).map(([rel, flat]) => ({ rel, dest: flat, required: REQUIRED_STAGED.includes(flat) }));
  const dir = path.join(from, 'transcripts');
  const entries = existsSync(dir) ? agentEntries(dir) : [];
  return {
    rows: [...named, ...entries.filter(e => e.kind === 'file').map(e => ({ rel: path.join('transcripts', e.rel), dest: path.join('transcripts', e.rel), required: true }))],
    odd: entries.filter(e => e.kind !== 'file').map(e => `transcripts/${e.rel}: ${e.kind}`),
  };
}

/**
 * Copy the smoke's evidence from a session dir into `to` (flat names, `transcripts/` kept). The session dir is agent-written (box/out holds
 * the audit log, the transcripts come from box/config), so every file is read through lib/agent-files.mjs FIRST: a link, a pipe or a link
 * among the directories refuses the whole staging, naming it, and nothing is written. A missing required file is a refusal too.
 */
export function stageSmoke(from, to) {
  const { rows, odd } = stagingPlan(from);
  const read = rows.map(r => ({ ...r, bytes: readAgentFileUnder(from, r.rel) }));
  const refused = [...odd, ...read.filter(r => r.bytes?.unreadable).map(r => `${r.rel}: ${r.bytes.unreadable}`)];
  if (refused.length > 0) return fail(`the session dir holds ${refused.length} path(s) that are not plain regular files (${refused.join('; ')}): nothing was staged`);
  const missing = read.filter(r => r.required && r.bytes === null).map(r => r.rel);
  if (missing.length > 0) return fail(`${from} lacks ${missing.join(', ')}: is it a smoke session dir?`);
  const present = read.filter(r => r.bytes !== null);
  for (const r of present) {
    mkdirSync(path.dirname(path.join(to, r.dest)), { recursive: true });
    writeFileSync(path.join(to, r.dest), r.bytes);
  }
  return ok(present.map(r => r.dest));
}

/** `{ text }` of a file under `dir` read through the one reader (`''` when absent), or `{ refused }` naming a link, a pipe, a link among its directories or an oversize file. */
function readText(dir, name) {
  const r = readAgentFileUnder(dir, name);
  if (r === null) return { text: '' };
  return r.unreadable ? { refused: `${name}: ${r.unreadable}` } : { text: r.toString('utf8') };
}

function parseLines(name, text) {
  const out = [];
  for (const [i, line] of text.split('\n').entries()) {
    if (line.trim() === '') continue;
    try { out.push(JSON.parse(line)); } catch (e) { return fail(`${name} line ${i + 1} is not JSON: ${e.message}`); }
  }
  return ok(out);
}

/** A staged (or sanitised) smoke directory as the verdict rules read it. Every read goes through lib/agent-files.mjs: a link or a pipe is refused, never followed. */
export function loadSmoke(dir) {
  const missing = REQUIRED_STAGED.filter(f => !existsSync(path.join(dir, f)));
  if (missing.length > 0) return fail(`${dir} lacks ${missing.join(', ')}`);
  const names = ['stream.ndjson', 'stream.stamped.ndjson', 'otlp-raw.ndjson', 'broker.ndjson', 'egress.ndjson', 'audit.ndjson', 'watchdog.ndjson'];
  const jsonNames = ['meta.json', 'argv.json', 'readback.json', 'checks.json'];
  const reads = [...names, ...jsonNames].map(n => [n, readText(dir, n)]);
  const tdir = path.join(dir, 'transcripts');
  const entries = existsSync(tdir) ? agentEntries(tdir) : [];
  const transcriptReads = entries.map(e => [path.join('transcripts', e.rel), e.kind === 'file' ? readText(dir, path.join('transcripts', e.rel)) : { refused: `transcripts/${e.rel}: ${e.kind}` }]);
  const refused = [...reads, ...transcriptReads].filter(([, r]) => r.refused).map(([, r]) => r.refused);
  if (refused.length > 0) return fail(`the smoke directory holds path(s) that are not plain regular files (${refused.join('; ')}): nothing was loaded`);
  const text = Object.fromEntries(reads.map(([n, r]) => [n, r.text]));
  const parsed = names.map(n => [n, parseLines(n, text[n])]);
  const bad = parsed.find(([, p]) => !p.ok);
  if (bad) return bad[1];
  const [events, stamped, sink, brokerLines, egressLines, audit, watchdog] = parsed.map(([, p]) => p.value);
  const transcripts = transcriptReads.map(([rel, r]) => ({ relPath: path.relative('transcripts', rel).split(path.sep).join('/'), text: r.text }));
  const json = n => JSON.parse(text[n] || 'null');
  return ok({ events, stamped, sink, brokerLines, egressLines, audit, watchdog, transcripts, hooks: [], meta: json('meta.json'), argv: json('argv.json'), readback: json('readback.json'), checks: json('checks.json') });
}
