// Classifying a link found in the agent-writable tree (E2.S2 B-S3). The CLI itself makes some (`tmp/claude-<uid>/.../tasks/<agent id>.output`
// -> the subagent's own transcript under `config/projects/`), so "any link is a finding" refuted B-S3 on a harmless one. The rule:
//   - the verdict comes from the link's TARGET STRING only; the link is never followed or read (only readlink, and an lstat of the lexical
//     target's mapped path to tell a dangling one);
//   - the target is mapped through the box's own bind table (box path -> host path, `writableBinds` in sandbox.mjs), a relative target first
//     resolved against the link's own box directory, `..` normalised lexically;
//   - `inside` = the mapped target lies in a read-write bind of this session (its agent-writable tree). Everything else is `high`: a target
//     outside every bind, one that maps to no bind (unmappable), a dangling one.
// `inside` changes only the verdict. Every host-side reader still refuses every link (lib/agent-files.mjs, O_NOFOLLOW).
import { lstatSync, readlinkSync } from 'node:fs';
import path from 'node:path';

const posix = path.posix;
const within = (p, base) => p === base || p.startsWith(`${base}/`);

/** The bind that holds `p` (by `field`, 'host' or 'box'), longest first, and the part of `p` below it. */
function holding(binds, field, p) {
  const bind = [...binds].filter(b => within(p, b[field])).sort((a, b) => b[field].length - a[field].length)[0];
  return bind === undefined ? null : { bind, rest: p.slice(bind[field].length) };
}

/** The host path of a box path through the bind table, or null when no bind maps it. */
const toHost = (binds, boxPath) => { const h = holding(binds, 'box', boxPath); return h === null ? null : h.bind.host + h.rest; };

const high = (why, extra = {}) => ({ severity: 'high', why, ...extra });

const exists = file => { try { lstatSync(file); return true; } catch { return false; } };

/**
 * `{ severity: 'inside', link, target } | { severity: 'high', why, link?, target? }` for the symlink at `<root>/<rel>` (`link` and `target` in BOX
 * paths). `binds` is `writableBinds(layout)`; with none, every link is `high`.
 */
export function classifyLink({ root, rel, binds = [] }) {
  const hostLink = path.join(root, rel);
  const at = holding(binds, 'host', hostLink);
  if (at === null) return high('the link lies outside every read-write bind of the box');
  const link = at.bind.box + at.rest;
  const raw = readlinkSync(hostLink);
  const target = raw.startsWith('/') ? posix.normalize(raw) : posix.resolve(posix.dirname(link), raw);
  const hostTarget = toHost(binds, target);
  if (hostTarget === null) return high('the target maps to no read-write bind (outside the agent-writable tree, or unmappable)', { link, target });
  if (!exists(hostTarget)) return high('the target does not exist (dangling)', { link, target });
  return { severity: 'inside', link, target };
}
