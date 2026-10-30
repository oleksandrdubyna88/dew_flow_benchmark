// A LIVE session directory (`<series>/s/<rid>/`, the launcher's own layout) read as the shape E1's views and the smoke's rules take (`smokeViews`).
// Written beside `smoke-load.mjs` on purpose: that file is E2.S2's and under review, so this one is not a widening of it. After E2 merges the two loaders
// should be one (`loadSmoke` taking a path mapper); until then the duplication is the cost of not editing a file another branch owns.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';

const walk = dir => (existsSync(dir) ? readdirSync(dir).flatMap(n => {
  const full = path.join(dir, n);
  return statSync(full).isDirectory() ? walk(full) : [full];
}) : []);

function parseLines(file) {
  if (!existsSync(file)) return ok([]);
  const out = [];
  for (const [i, line] of readFileSync(file, 'utf8').split('\n').entries()) {
    if (line.trim() === '') continue;
    try { out.push(JSON.parse(line)); } catch (e) { return fail(`${file} line ${i + 1} is not JSON: ${e.message}`); }
  }
  return ok(out);
}

const readJson = file => (existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null);

/** `ok({ events, stamped, sink, brokerLines, egressLines, audit, watchdog, transcripts, hooks, meta, argv, readback, checks })` | `fail`. A missing file is an empty list, never a throw. */
export function loadSessionDir(layout) {
  const lists = [layout.files.stream, layout.files.streamStamped, path.join(layout.otel, 'otel-raw.ndjson'), layout.files.brokerLog, layout.files.egressLog, layout.files.auditLog, layout.files.watchdog].map(parseLines);
  const bad = lists.find(l => !l.ok);
  if (bad) return bad;
  const [events, stamped, sink, brokerLines, egressLines, audit, watchdog] = lists.map(l => l.value);
  const transcripts = walk(layout.transcripts).map(f => ({ relPath: path.relative(layout.transcripts, f).split(path.sep).join('/'), text: readFileSync(f, 'utf8') }));
  return ok({
    events, stamped, sink, brokerLines, egressLines, audit, watchdog, transcripts, hooks: [],
    meta: readJson(layout.files.meta), argv: readJson(layout.files.argv), readback: readJson(layout.files.readback), checks: null,
  });
}
