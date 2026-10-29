// The bare-session smoke as files: staging a session directory into the flat, capture-shaped layout (so E1's loaders and the sanitiser read
// it) and loading it back. I/O at the edge; the verdicts over what is loaded are pure (smoke-verdicts.mjs).
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';

/** session-dir relative path -> flat name. `otel/otel-raw.ndjson` is the file sink's line shape under the capture's name. */
export const STAGED = Object.freeze({
  'stream.ndjson': 'stream.ndjson', 'stream.stamped.ndjson': 'stream.stamped.ndjson', 'otel/otel-raw.ndjson': 'otlp-raw.ndjson', 'broker.ndjson': 'broker.ndjson',
  'egress.ndjson': 'egress.ndjson', 'box/out/audit.ndjson': 'audit.ndjson', 'watchdog.ndjson': 'watchdog.ndjson', 'argv.json': 'argv.json', 'meta.json': 'meta.json',
  'readback.json': 'readback.json', 'checks.json': 'checks.json', 'egress.json': 'egress.json', 'broker.json': 'broker.json', 'stderr.txt': 'stderr.txt',
});
export const REQUIRED_STAGED = Object.freeze(['stream.ndjson', 'otlp-raw.ndjson', 'argv.json', 'meta.json', 'broker.ndjson', 'egress.ndjson', 'readback.json', 'checks.json']);

const walk = dir => (existsSync(dir) ? readdirSync(dir).flatMap(n => {
  const full = path.join(dir, n);
  return statSync(full).isDirectory() ? walk(full) : [full];
}) : []);

/** Copy the smoke's evidence from a session dir into `to` (flat names, `transcripts/` kept). Refuses a missing required file. */
export function stageSmoke(from, to) {
  const missing = Object.entries(STAGED).filter(([rel, flat]) => REQUIRED_STAGED.includes(flat) && !existsSync(path.join(from, rel))).map(([rel]) => rel);
  if (missing.length > 0) return fail(`${from} lacks ${missing.join(', ')}: is it a smoke session dir?`);
  mkdirSync(to, { recursive: true });
  const copied = [];
  for (const [rel, flat] of Object.entries(STAGED)) {
    if (!existsSync(path.join(from, rel))) continue;
    copyFileSync(path.join(from, rel), path.join(to, flat));
    copied.push(flat);
  }
  for (const f of walk(path.join(from, 'transcripts'))) {
    const rel = path.join('transcripts', path.relative(path.join(from, 'transcripts'), f));
    mkdirSync(path.dirname(path.join(to, rel)), { recursive: true });
    copyFileSync(f, path.join(to, rel));
    copied.push(rel);
  }
  return ok(copied);
}

const readText = (dir, name) => (existsSync(path.join(dir, name)) ? readFileSync(path.join(dir, name), 'utf8') : '');

function parseLines(name, text) {
  const out = [];
  for (const [i, line] of text.split('\n').entries()) {
    if (line.trim() === '') continue;
    try { out.push(JSON.parse(line)); } catch (e) { return fail(`${name} line ${i + 1} is not JSON: ${e.message}`); }
  }
  return ok(out);
}

/** A staged (or sanitised) smoke directory as the verdict rules read it. */
export function loadSmoke(dir) {
  const missing = REQUIRED_STAGED.filter(f => !existsSync(path.join(dir, f)));
  if (missing.length > 0) return fail(`${dir} lacks ${missing.join(', ')}`);
  const names = ['stream.ndjson', 'stream.stamped.ndjson', 'otlp-raw.ndjson', 'broker.ndjson', 'egress.ndjson', 'audit.ndjson', 'watchdog.ndjson'];
  const parsed = names.map(n => [n, parseLines(n, readText(dir, n))]);
  const bad = parsed.find(([, p]) => !p.ok);
  if (bad) return bad[1];
  const [events, stamped, sink, brokerLines, egressLines, audit, watchdog] = parsed.map(([, p]) => p.value);
  const transcripts = walk(path.join(dir, 'transcripts')).map(f => ({ relPath: path.relative(path.join(dir, 'transcripts'), f).split(path.sep).join('/'), text: readFileSync(f, 'utf8') }));
  const json = n => JSON.parse(readText(dir, n) || 'null');
  return ok({ events, stamped, sink, brokerLines, egressLines, audit, watchdog, transcripts, hooks: [], meta: json('meta.json'), argv: json('argv.json'), readback: json('readback.json'), checks: json('checks.json') });
}
