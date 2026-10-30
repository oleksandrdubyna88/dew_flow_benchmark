// Scan a session directory for the REAL token (E2.S2 B-S3): every file under it is read and handed to S1's `tokenHits` with the real token
// as an own secret. The box holds a DUMMY of the same shape on purpose, so the dummy is masked first (by a mask of its own length, so an offset is true);
// what is left is the real token or a credential of another shape. Values are never returned.
//   - One file at a time: a file is read, scanned and let go before the next; only hits and counters are kept.
//   - A file over the whole-read limit is NOT skipped (an agent would choose a big file to hide a token in): it is read in windows that overlap by
//     more than a token (`limits.overlapBytes`), each window reporting only the hits that START in its own part, so a token on a boundary is found
//     once. Such a file is listed in `streamed`. A token broken by more whitespace than the overlap is the one thing a window cannot see.
//   - A file no scan can read (larger than `maxStreamBytes`, or one that grew past it while it was read) is a HIGH finding (`links`, kind `unscanned`)
//     that refutes B-S3, and stays listed in `tooLarge`: absent is not zero.
// The tree is agent-written: a link or any other non-regular entry in it is a HIGH-severity finding (`links`), never a skip, and is never read through.
// With the box's bind table (`binds`) a link whose target string maps INSIDE the agent-writable tree is `inside` (lib/link-class.mjs): listed apart in
// `insideLinks` as box paths, not a finding. Without one every link is high.
import path from 'node:path';
import { MAX_AGENT_FILE_BYTES, agentEntries, readAgentFileUnder, scanAgentFileWindows } from './agent-files.mjs';
import { classifyLink } from './link-class.mjs';
import { tokenHits } from './token-scan.mjs';

export const MAX_SCAN_BYTES = MAX_AGENT_FILE_BYTES;
/** The limits of one scan: a file up to `maxScanBytes` is read whole; above it, in windows of `windowBytes` (+ `overlapBytes`) up to `maxStreamBytes`. */
export const SCAN_LIMITS = Object.freeze({ maxScanBytes: MAX_SCAN_BYTES, windowBytes: 8 * 1024 * 1024, overlapBytes: 1024 * 1024, maxStreamBytes: 8 * 1024 * 1024 * 1024 });

/** Every entry under `root` that is not a directory, as `{ rel, size, kind }` (`file`, `symlink` or `other`); links and devices are listed, not followed. */
export const filesUnder = agentEntries;

/** Pure: `texts` with the dummy masked (same length), scanned for `real` and every credential shape. `[{ source, kind, offset }]`. */
export function realTokenHits(texts, { real, dummy = null }) {
  const masked = texts.map(t => ({ source: t.source, text: dummy ? String(t.text).replaceAll(dummy, '#'.repeat(dummy.length)) : String(t.text) }));
  return tokenHits(masked, [real]);
}

/** The hits of one file read whole, or null when it was not a regular file any more (a swap after the listing). */
function scanWhole(root, rel, size, { real, dummy, limits, hooks }) {
  hooks.beforeRead?.(rel);
  const bytes = readAgentFileUnder(root, rel, limits.maxScanBytes);
  if (!Buffer.isBuffer(bytes)) return null;
  const hits = realTokenHits([{ source: rel, text: bytes.toString('latin1') }], { real, dummy });
  return hits;
}

/** The hits of one big file read in windows -> `{ hits } | { unreadable }`; only the hits that start in a window's own part are kept. */
function scanWindows(root, rel, { real, dummy, limits, hooks }) {
  hooks.beforeRead?.(rel);
  const hits = [];
  const read = scanAgentFileWindows(root, rel, { windowBytes: limits.windowBytes, overlapBytes: limits.overlapBytes, maxBytes: limits.maxStreamBytes }, (bytes, { base, ownBytes }) => {
    const own = realTokenHits([{ source: rel, text: bytes.toString('latin1') }], { real, dummy }).filter(h => h.offset < ownBytes);
    hits.push(...own.map(h => ({ ...h, offset: h.offset + base })));
  });
  return read === null || read.unreadable ? { unreadable: read?.unreadable ?? 'absent' } : { hits };
}

/**
 * Scan a directory -> `{ hits, scanned, streamed: [rel], tooLarge: [rel], links: [{ rel, kind, severity: 'high', why? }], insideLinks: [{ link, target }] }`. Files are read as
 * latin1, so a binary file (an environ dump) is searchable. `limits` and `hooks` (`afterList`, `beforeRead`) are test seams.
 */
export function scanSessionDir(root, { real, dummy = null, binds = [], limits = SCAN_LIMITS, hooks = {} }) {
  const entries = filesUnder(root);
  hooks.afterList?.(); // a test seam: the agent runs while the scan does, and can change the tree between the listing and the reads
  const finding = (f, kind = f.kind, extra = {}) => ({ rel: f.rel, kind, severity: 'high', ...extra });
  const ctx = { real, dummy, limits, hooks };
  const hits = [];
  const streamed = [];
  const tooLarge = [];
  const problems = [];
  let scanned = 0;
  for (const f of entries.filter(e => e.kind === 'file')) {
    if (f.size <= limits.maxScanBytes) {
      const found = scanWhole(root, f.rel, f.size, ctx);
      // a regular file that is no longer one when it is opened (or whose directories were swapped for a link) was changed after the listing: that is a finding too
      if (found === null) { problems.push(finding(f, 'swapped')); continue; }
      hits.push(...found);
    } else {
      const found = f.size <= limits.maxStreamBytes ? scanWindows(root, f.rel, ctx) : { unreadable: `larger than ${limits.maxStreamBytes} bytes` };
      if (found.unreadable) { tooLarge.push(f.rel); problems.push(finding(f, found.unreadable.startsWith('larger') ? 'unscanned' : 'swapped', { why: found.unreadable })); continue; }
      streamed.push(f.rel);
      hits.push(...found.hits);
    }
    scanned += 1;
  }
  const classified = entries.filter(f => f.kind === 'symlink').map(f => ({ f, c: classifyLink({ root, rel: f.rel, binds }) }));
  return {
    hits, scanned, streamed, tooLarge,
    links: [
      ...entries.filter(f => f.kind === 'other').map(f => finding(f)),
      ...classified.filter(x => x.c.severity === 'high').map(x => finding(x.f, 'symlink', { why: x.c.why })),
      ...problems,
    ],
    insideLinks: classified.filter(x => x.c.severity === 'inside').map(x => ({ link: x.c.link, target: x.c.target })),
  };
}
