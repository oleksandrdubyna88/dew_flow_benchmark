// The ONE host-side reader of files the agent could have written (E2.S2 F4). The agent owns box/work, box/out, box/config, box/home and
// box/tmp, so any path under them may be a symlink to a host file (the review planted one at box/out/audit.ndjson: the staging copied the
// host file's content into the "evidence"), a pipe (a read would block) or a device. Every host read or copy of such a path goes through here:
//   - the path is lstat-walked below `root`, so a link among its directories is refused;
//   - the file is opened with O_NOFOLLOW | O_NONBLOCK (a link in the final component fails in the open itself, with no window between a
//     check and the read; a pipe opens without blocking), then fstat must say regular file, and its size is capped;
//   - a listing reports links and other non-regular entries as entries (`kind`), never skips them.
// Returns follow the older reader's contract (`Buffer | null | { unreadable }`) so score-deps keeps its callers.
import { closeSync, constants, fstatSync, lstatSync, mkdirSync, openSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';

/** The largest agent-written file the host will read whole (the session scan's own limit). */
export const MAX_AGENT_FILE_BYTES = 64 * 1024 * 1024;
const OPEN_FLAGS = constants.O_RDONLY | constants.O_NOFOLLOW | constants.O_NONBLOCK;
const ABSENT = new Set(['ENOENT', 'ENOTDIR']);

/** `Buffer` of a regular file of at most `maxBytes`; `null` when absent; `{ unreadable: why }` for a link, a pipe, a device, a directory or an oversize file. */
export function readAgentFile(file, maxBytes = MAX_AGENT_FILE_BYTES) {
  let fd;
  try {
    fd = openSync(file, OPEN_FLAGS);
  } catch (e) {
    if (ABSENT.has(e.code)) return null;
    if (e.code === 'ELOOP') return { unreadable: 'symlink' };
    throw e;
  }
  try {
    const st = fstatSync(fd);
    if (!st.isFile()) return { unreadable: 'not a regular file' };
    if (st.size > maxBytes) return { unreadable: `larger than ${maxBytes} bytes` };
    return readFileSync(fd);
  } finally {
    closeSync(fd);
  }
}

/** The first link among the directories between `root` and `rel`'s last component: its relative path, or null. */
function linkAmong(root, rel) {
  const parts = rel.split(path.sep).filter(Boolean).slice(0, -1);
  for (let i = 1; i <= parts.length; i += 1) {
    const here = path.join(root, ...parts.slice(0, i));
    let st;
    try { st = lstatSync(here); } catch (e) { if (ABSENT.has(e.code)) return null; throw e; }
    if (st.isSymbolicLink()) return parts.slice(0, i).join(path.sep);
  }
  return null;
}

/** `readAgentFile` of `<root>/<rel>`, also refusing a link among the directories in between (`root` itself is trusted). */
export function readAgentFileUnder(root, rel, maxBytes = MAX_AGENT_FILE_BYTES) {
  const link = linkAmong(root, rel);
  if (link !== null) return { unreadable: `a link among the directories of the path (${link})` };
  return readAgentFile(path.join(root, rel), maxBytes);
}

/** Every entry under `root` that is not a directory, as `{ rel, kind: 'file' | 'symlink' | 'other', size }`; real directories are walked, links to them are not. */
export function agentEntries(root) {
  const rows = [];
  const walk = dir => {
    for (const name of readdirSync(dir)) {
      const full = path.join(dir, name);
      const st = lstatSync(full);
      if (st.isDirectory()) walk(full);
      else rows.push({ rel: path.relative(root, full), kind: st.isFile() ? 'file' : st.isSymbolicLink() ? 'symlink' : 'other', size: st.size });
    }
  };
  walk(root);
  return rows;
}

/** Copy `<root>/<rel>` to `dest` (parents made) -> `ok(rel)`, or a failure naming the path and why it was refused. Absent is a failure too. */
export function copyAgentFile(root, rel, dest, maxBytes = MAX_AGENT_FILE_BYTES) {
  const bytes = readAgentFileUnder(root, rel, maxBytes);
  if (bytes === null) return fail(`${rel} does not exist`, { absent: true });
  if (bytes.unreadable) return fail(`${rel} was not copied: ${bytes.unreadable}`);
  mkdirSync(path.dirname(dest), { recursive: true });
  writeFileSync(dest, bytes);
  return ok(rel);
}
