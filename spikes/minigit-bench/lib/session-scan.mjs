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
import { statSync } from 'node:fs';
import path from 'node:path';
import { MAX_AGENT_FILE_BYTES, agentEntries, readAgentFileUnder, scanAgentFileWindows } from './agent-files.mjs';
import { classifyLink } from './link-class.mjs';
import { ownValueHits, tokenHits } from './token-scan.mjs';

export const MAX_SCAN_BYTES = MAX_AGENT_FILE_BYTES;
/** The limits of one scan: a file up to `maxScanBytes` is read whole; above it, in windows of `windowBytes` (+ `overlapBytes`) up to `maxStreamBytes`. */
export const SCAN_LIMITS = Object.freeze({ maxScanBytes: MAX_SCAN_BYTES, windowBytes: 8 * 1024 * 1024, overlapBytes: 1024 * 1024, maxStreamBytes: 8 * 1024 * 1024 * 1024 });

/** Every entry under `root` that is not a directory, as `{ rel, size, kind }` (`file`, `symlink` or `other`); links and devices are listed, not followed. */
export const filesUnder = agentEntries;

/**
 * Pure: `texts` with the dummy masked (same length), scanned for `real` and every credential shape. `[{ source, kind, offset }]`. A hit where the real token itself stands is `own-value` even
 * when the token has the shape of a credential (it does: `sk-ant-`, and a shape wins the span in `tokenHits`); every other hit is its shape (review N8: only the real token is the security stop).
 */
export function realTokenHits(texts, { real, dummy = null }) {
  const masked = texts.map(t => ({ source: t.source, text: dummy ? String(t.text).replaceAll(dummy, '#'.repeat(dummy.length)) : String(t.text) }));
  const own = new Set(ownValueHits(masked, [real]).map(h => `${h.source}\u0000${h.offset}`));
  return tokenHits(masked, [real]).map(h => (own.has(`${h.source}\u0000${h.offset}`) ? { ...h, kind: 'own-value' } : h));
}

/** The hits of one file read whole, or null when it was not a regular file any more (a swap after the listing). `scanText(text)` -> `[{ kind, offset, ... }]`. */
function scanWhole(root, rel, scanText, { limits, hooks }) {
  hooks.beforeRead?.(rel);
  const bytes = readAgentFileUnder(root, rel, limits.maxScanBytes);
  if (!Buffer.isBuffer(bytes)) return null;
  return scanText(bytes.toString('latin1'), rel).map(h => ({ ...h, source: rel }));
}

/** The hits of one big file read in windows -> `{ hits } | { unreadable }`; only the hits that start in a window's own part are kept. */
function scanWindows(root, rel, scanText, { limits, hooks }) {
  hooks.beforeRead?.(rel);
  const hits = [];
  const read = scanAgentFileWindows(root, rel, { windowBytes: limits.windowBytes, overlapBytes: limits.overlapBytes, maxBytes: limits.maxStreamBytes }, (bytes, { base, ownBytes }) => {
    const own = scanText(bytes.toString('latin1'), rel).filter(h => h.offset < ownBytes);
    hits.push(...own.map(h => ({ ...h, source: rel, offset: h.offset + base })));
  });
  return read === null || read.unreadable ? { unreadable: read?.unreadable ?? 'absent' } : { hits };
}

/**
 * Run a pure text scan over the named files of `root`, one at a time, whatever their size: whole up to `maxScanBytes`, in overlapping windows above it up to `maxStreamBytes` (never skipped: a
 * token in a big file is the case an agent would choose). `scanText(latin1Text)` -> `[{ kind, offset, ... }]`. -> `{ hits: [{ source, kind, offset, ... }], unreadable: [{ rel, why }] }`. An absent
 * file is skipped; one that cannot be scanned (a link, too large, swapped while it was read) is `unreadable`, never a quiet pass. The session scan's own loop is built on the same two readers.
 */
export function scanFiles(root, rels, scanText, limits = SCAN_LIMITS, hooks = {}) {
  const hits = [];
  const unreadable = [];
  for (const rel of rels) {
    const size = statSync(path.join(root, rel), { throwIfNoEntry: false })?.size;
    if (size === undefined) continue;
    const found = size <= limits.maxScanBytes ? scanWhole(root, rel, scanText, { limits, hooks }) : size <= limits.maxStreamBytes ? scanWindows(root, rel, scanText, { limits, hooks }) : { unreadable: `larger than ${limits.maxStreamBytes} bytes` };
    if (found === null) unreadable.push({ rel, why: 'swapped after the listing' });
    else if (found.unreadable) unreadable.push({ rel, why: found.unreadable });
    else hits.push(...(Array.isArray(found) ? found : found.hits));
  }
  return { hits, unreadable };
}

/**
 * Scan a directory -> `{ hits, extra, unreadable, scanned, streamed: [rel], tooLarge: [rel], links: [{ rel, kind, severity: 'high', why? }], insideLinks: [{ link, target }] }`. Files are read as
 * latin1, so a binary file (an environ dump) is searchable. `limits` and `hooks` (`afterList`, `beforeRead`) are test seams.
 * ONE pass over every file (review C2): `extra` is `[{ name, applies(rel), scanText(text) }]`, further text scans that ride on the same read of the files they apply to (the bare collector's
 * canary scan over the evidence files); their hits come back in `extra[name]`. `unreadable` lists `{ rel, why }` for every file no scan could read (too large, swapped after the listing).
 */
export function scanSessionDir(root, { real, dummy = null, binds = [], limits = SCAN_LIMITS, hooks = {}, extra = [] }) {
  const entries = filesUnder(root);
  hooks.afterList?.(); // a test seam: the agent runs while the scan does, and can change the tree between the listing and the reads
  const finding = (f, kind = f.kind, extra = {}) => ({ rel: f.rel, kind, severity: 'high', ...extra });
  const scanText = (text, rel) => [
    ...realTokenHits([{ source: '', text }], { real, dummy }).map(h => ({ ...h, scan: '' })),
    ...extra.filter(x => x.applies(rel)).flatMap(x => x.scanText(text).map(h => ({ ...h, scan: x.name }))),
  ];
  const ctx = { limits, hooks };
  const all = [];
  const streamed = [];
  const tooLarge = [];
  const problems = [];
  const unreadable = [];
  let scanned = 0;
  for (const f of entries.filter(e => e.kind === 'file')) {
    if (f.size <= limits.maxScanBytes) {
      const found = scanWhole(root, f.rel, scanText, ctx);
      // a regular file that is no longer one when it is opened (or whose directories were swapped for a link) was changed after the listing: that is a finding too
      if (found === null) { problems.push(finding(f, 'swapped')); unreadable.push({ rel: f.rel, why: 'swapped after the listing' }); continue; }
      all.push(...found);
    } else {
      const found = f.size <= limits.maxStreamBytes ? scanWindows(root, f.rel, scanText, ctx) : { unreadable: `larger than ${limits.maxStreamBytes} bytes` };
      if (found.unreadable) { tooLarge.push(f.rel); unreadable.push({ rel: f.rel, why: found.unreadable }); problems.push(finding(f, found.unreadable.startsWith('larger') ? 'unscanned' : 'swapped', { why: found.unreadable })); continue; }
      streamed.push(f.rel);
      all.push(...found.hits);
    }
    scanned += 1;
  }
  const classified = entries.filter(f => f.kind === 'symlink').map(f => ({ f, c: classifyLink({ root, rel: f.rel, binds }) }));
  const strip = ({ scan, ...hit }) => hit;
  return {
    hits: all.filter(h => h.scan === '').map(strip), extra: Object.fromEntries(extra.map(x => [x.name, all.filter(h => h.scan === x.name).map(strip)])), unreadable, scanned, streamed, tooLarge,
    links: [
      ...entries.filter(f => f.kind === 'other').map(f => finding(f)),
      ...classified.filter(x => x.c.severity === 'high').map(x => finding(x.f, 'symlink', { why: x.c.why })),
      ...problems,
    ],
    insideLinks: classified.filter(x => x.c.severity === 'inside').map(x => ({ link: x.c.link, target: x.c.target })),
  };
}
