// Scan a session directory for the REAL token (E2.S2 B-S3): every file under it is read and handed to S1's `tokenHits` with the real token
// as an own secret. The box holds a DUMMY of the same shape on purpose, so the dummy is masked first; what is left is the real token or a
// credential of another shape. A file too large to read is listed, never skipped silently (absent is not zero). Values are never returned.
// The tree is agent-written: a link or any other non-regular entry in it is a HIGH-severity finding (`links`), never a skip, and is never read through.
// With the box's bind table (`binds`) a link whose target string maps INSIDE the agent-writable tree is `inside` (lib/link-class.mjs): listed apart in
// `insideLinks` as box paths, not a finding. Without one every link is high.
import path from 'node:path';
import { MAX_AGENT_FILE_BYTES, agentEntries, readAgentFile } from './agent-files.mjs';
import { classifyLink } from './link-class.mjs';
import { tokenHits } from './token-scan.mjs';

export const MAX_SCAN_BYTES = MAX_AGENT_FILE_BYTES;
const MASK = '[dummy]';

/** Every entry under `root` that is not a directory, as `{ rel, size, kind }` (`file`, `symlink` or `other`); links and devices are listed, not followed. */
export const filesUnder = agentEntries;

/** Pure: `texts` with the dummy masked, scanned for `real` and every credential shape. `[{ source, kind, offset }]`. */
export function realTokenHits(texts, { real, dummy = null }) {
  const masked = texts.map(t => ({ source: t.source, text: dummy ? String(t.text).replaceAll(dummy, MASK) : String(t.text) }));
  return tokenHits(masked, [real]);
}

/** Scan a directory -> `{ hits, scanned, tooLarge: [rel], links: [{ rel, kind, severity: 'high', why? }], insideLinks: [{ link, target }] }`. Files are read as latin1, so a binary file (an environ dump) is searchable. */
export function scanSessionDir(root, { real, dummy = null, binds = [] }) {
  const entries = filesUnder(root);
  const finding = (f, kind = f.kind, extra = {}) => ({ rel: f.rel, kind, severity: 'high', ...extra });
  const files = entries.filter(f => f.kind === 'file');
  const readable = files.filter(f => f.size <= MAX_SCAN_BYTES);
  const reads = readable.map(f => ({ f, bytes: readAgentFile(path.join(root, f.rel), MAX_SCAN_BYTES) }));
  const texts = reads.filter(r => Buffer.isBuffer(r.bytes)).map(r => ({ source: r.f.rel, text: r.bytes.toString('latin1') }));
  // a regular file that is no longer one when it is opened was swapped for a link after the listing: that is a finding too
  const swapped = reads.filter(r => !Buffer.isBuffer(r.bytes)).map(r => finding(r.f, 'swapped'));
  const classified = entries.filter(f => f.kind === 'symlink').map(f => ({ f, c: classifyLink({ root, rel: f.rel, binds }) }));
  return {
    hits: realTokenHits(texts, { real, dummy }), scanned: texts.length, tooLarge: files.filter(f => f.size > MAX_SCAN_BYTES).map(f => f.rel),
    links: [
      ...entries.filter(f => f.kind === 'other').map(f => finding(f)),
      ...classified.filter(x => x.c.severity === 'high').map(x => finding(x.f, 'symlink', { why: x.c.why })),
      ...swapped,
    ],
    insideLinks: classified.filter(x => x.c.severity === 'inside').map(x => ({ link: x.c.link, target: x.c.target })),
  };
}
