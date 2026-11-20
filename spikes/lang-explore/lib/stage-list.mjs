// The sha256 list of a series folder (F3.S1 (a), PR 4; the archive's identity and what `stage-free` proves a file against): one line per file, `sha256  path`, paths from the series root with `/` and in code-point order. A link is recorded by the
// hash of its target text (`link:<sha256>  path`) and never followed. The walk is `workspaces-tree.mjs`'s `walkEntries` (a streaming `lstat` walk) and every file is hashed in 1 MiB chunks, so a series of many gigabytes is read without holding a file.
// The series' own `series.lock` and `archive.json` are not in the list: the lock is transient and the archive record is written after the list it names.
import { createHash } from 'node:crypto';
import { lstatSync, readlinkSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { countedSaying, mapSaying } from './stage-progress.mjs';
import { byRel, sha256OfFile, streamEntries } from './workspaces-tree.mjs';

export const LIST_SKIP = Object.freeze(['series.lock', 'archive.json']);
const LINE = /^(link:[0-9a-f]{64}|[0-9a-f]{64}) {2}(.+)$/;

export const sha256Text = text => createHash('sha256').update(text).digest('hex');
const hashOfLink = target => `link:${sha256Text(target)}`;

/** The hash of what is at `file` now: a file's sha256, a link's `link:<sha256 of its target>`, null when nothing is there or it is neither (a link is never followed). */
export function hashAt(file) {
  const st = lstatSync(file, { throwIfNoEntry: false });
  if (st === undefined) return null;
  if (st.isSymbolicLink()) return hashOfLink(readlinkSync(file));
  return st.isFile() ? sha256OfFile(file) : null;
}

/** `ok([{ rel, kind, size, mode, target, hash }])` of every file and link of the series folder (`report` = `{ say, every }` says `listing: started` before the walk, a line every N entries while it walks, then the hashing's progress), or a `fail` naming an entry that is neither (an archive records neither a socket nor a device). */
export function listSeries(series, report) {
  const entries = [...countedSaying({ items: streamEntries(series, { skip: rel => LIST_SKIP.includes(rel) }), phase: 'listing', report })].sort(byRel);
  const odd = entries.find(e => e.kind === 'other');
  if (odd !== undefined) return fail(`${odd.rel} is neither a regular file nor a link: the archive records neither`);
  return ok(mapSaying({ items: entries, phase: 'listing', report, fn: e => ({ ...e, hash: e.kind === 'link' ? hashOfLink(e.target) : sha256OfFile(path.join(series, e.rel)) }) }));
}

/** The text of a list: `sha256  path`, one line each, a newline after the last. */
export const listText = entries => `${entries.map(e => `${e.hash}  ${e.rel}`).join('\n')}\n`;

/** `Map(path -> hash)` of a list's text; a line that is no `hash  path` is skipped (the list's own hash decides whether the text is the recorded one). */
export const parseList = text => new Map(text.split('\n').map(line => LINE.exec(line)).filter(Boolean).map(m => [m[2], m[1]]));

/** The sentences for every entry of `entries` that `root` does not hold with the listed hash: `<path>: missing or differs in the copy`. */
export const copyProblems = ({ entries, root, report }) => mapSaying({ items: entries, phase: 'verifying', report, fn: e => (hashAt(path.join(root, e.rel)) === e.hash ? null : `${e.rel}: missing from the copy, or it differs from the source`) }).filter(line => line !== null);
