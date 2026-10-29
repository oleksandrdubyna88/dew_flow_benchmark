// Reading and writing capture directories (I/O at the edge): load one for the verdicts, sanitise one into a fixture directory.
import { createHash } from 'node:crypto';
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../lib/result.mjs';
import { buildMap, collectAttrSecrets, leakScan, localContext, sanitisePath, sanitiseText } from './sanitise.mjs';

const REQUIRED = Object.freeze(['stream.ndjson', 'otlp-raw.ndjson', 'argv.json', 'meta.json']);
const OPTIONAL = Object.freeze(['hooks.ndjson', 'stderr.txt']);

const walk = dir => (existsSync(dir) ? readdirSync(dir).flatMap(name => {
  const full = path.join(dir, name);
  return statSync(full).isDirectory() ? walk(full) : [full];
}) : []);

/** The files a sanitised copy holds: `[{ relPath, text }]`, or a failure naming the missing required file. */
export function readCaptureFiles(from, { optional = OPTIONAL } = {}) {
  const missing = REQUIRED.filter(f => !existsSync(path.join(from, f)));
  if (missing.length > 0) return fail(`${from} lacks ${missing.join(', ')}: is it a capture directory?`);
  const named = [...new Set([...REQUIRED, ...optional])].filter(f => existsSync(path.join(from, f)));
  const transcripts = walk(path.join(from, 'transcripts')).map(f => path.relative(from, f));
  return ok([...named, ...transcripts].map(relPath => ({ relPath, text: readFileSync(path.join(from, relPath), 'utf8') })));
}

function parseNdjson(name, text) {
  const out = [];
  for (const [i, line] of text.split('\n').entries()) {
    if (line.trim() === '') continue;
    try { out.push(JSON.parse(line)); } catch (e) { return fail(`${name} line ${i + 1} is not JSON: ${e.message}`); }
  }
  return ok(out);
}

/** A capture directory (sanitised or raw) as the verdict rules read it. */
export function loadCapture(dir) {
  const files = readCaptureFiles(dir);
  if (!files.ok) return files;
  const text = name => files.value.find(f => f.relPath === name)?.text ?? '';
  const parsed = ['stream.ndjson', 'otlp-raw.ndjson', 'hooks.ndjson'].map(n => [n, parseNdjson(n, text(n))]);
  const bad = parsed.find(([, p]) => !p.ok);
  if (bad) return bad[1];
  const [events, sink, hooks] = parsed.map(([, p]) => p.value);
  const transcripts = files.value.filter(f => f.relPath.startsWith('transcripts/')).map(f => ({ relPath: f.relPath.slice('transcripts/'.length), text: f.text }));
  return ok({ events, sink, hooks, transcripts, meta: JSON.parse(text('meta.json')), argv: JSON.parse(text('argv.json')) });
}

/**
 * Copy a raw capture into a public fixture directory through the sanitiser. Everything is sanitised and scanned IN MEMORY first; only a
 * clean result is written, and it is written into a sibling staging directory that is renamed to `to` after EVERY write succeeded.
 * A failed write removes the staging directory, so a failed sanitise leaves nothing behind and a retry is not blocked.
 * `to` must not exist or be empty; `write(file, text)` is the one write seam.
 */
export function sanitiseCapture({ from, to, token = null, ctx = localContext(), write = writeFileSync, optional = OPTIONAL }) {
  if (existsSync(to) && readdirSync(to).length > 0) return fail(`${to} is not empty: a fixture is never overwritten`);
  const raw = path.resolve(from);
  const files = readCaptureFiles(raw, { optional });
  if (!files.ok) return files;
  const sink = files.value.find(f => f.relPath === 'otlp-raw.ndjson').text;
  const secrets = collectAttrSecrets(sink);
  const map = buildMap({ ...ctx, out: raw, idValues: secrets.ids, extraEmails: secrets.emails });
  const clean = files.value.map(f => ({ relPath: sanitisePath(f.relPath, map), text: sanitiseText(f.text, map) }));
  const findings = leakScan(clean, { ...ctx, out: raw, token });
  if (findings.length > 0) return fail(`the sanitised copy still holds ${findings.length} leak(s); nothing was written`, { findings });
  return stageThenPlace({ to, files: clean, write });
}

/** Write `files` ({ relPath, text }) under a fresh sibling of `to`, then rename it onto `to`; any failure removes the staging directory. */
function stageThenPlace({ to, files, write }) {
  mkdirSync(path.dirname(to), { recursive: true });
  const staging = mkdtempSync(path.join(path.dirname(to), `.${path.basename(to)}.staging-`));
  try {
    chmodSync(staging, 0o777 & ~process.umask()); // mkdtemp makes it 0700; a fixture is an ordinary directory
    for (const f of files) {
      mkdirSync(path.dirname(path.join(staging, f.relPath)), { recursive: true });
      write(path.join(staging, f.relPath), f.text);
    }
    renameSync(staging, to);
  } catch (e) {
    rmSync(staging, { recursive: true, force: true });
    return fail(`the fixture could not be written (${e.code ?? e.name}: ${e.message}); nothing was left behind in ${path.dirname(to)}`);
  }
  return ok({ files: files.map(f => f.relPath) });
}

/** sha256 of a file's bytes (the sanitiser's, recorded in CAPTURE.md). */
export const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');
