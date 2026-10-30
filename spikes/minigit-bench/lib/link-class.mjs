// Classifying a link found in the agent-writable tree (E2.S2 B-S3). The CLI itself makes some (`tmp/claude-<uid>/.../tasks/<agent id>.output`
// -> the subagent's own transcript under `config/projects/`), so "any link is a finding" refuted B-S3 on a harmless one. The rule:
//   - the verdict comes from the link's TARGET STRING and the lstat of its path (never a read of the target): the link is never followed;
//   - a link is `inside` ONLY when ALL of these hold:
//       1. the target string has no `..` segment (the kernel resolves `..` AFTER following the links before it, so `sub/../x` with
//          `sub -> /tmp` is not `<dir>/x`; a lexical normalisation is a different path);
//       2. no component of the target path before the last is itself a link (lstat of each, from the host side of the mapping), because a
//          directory link in the path redirects it;
//       3. the target maps under a read-write bind of this session whose box path and host path are THE SAME path (`work`, `home`, `config`,
//          `nuget`, `npm`, `out`). The box's `/tmp` is `box/tmp` on the host: an absolute `/tmp/...` target is the HOST's `/tmp` for the
//          reader on the host, so it is never `inside`; the same holds for any bind where the two paths differ;
//       4. the target exists (lstat; a dangling one is `high`).
//     A relative target is first resolved against the link's own box directory (with no `..` that is exact).
//   - everything else is `high`: a target outside every bind, one that maps to no bind (unmappable), one with a `..`, one below a link, one
//     under the box `/tmp`, a dangling one.
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

const high = (why, extra = {}) => ({ severity: 'high', why, ...extra });

const exists = file => { try { lstatSync(file); return true; } catch { return false; } };

/** The first link among the components of `hostTarget` BELOW the bind's host root, up to but not including the last component: its path, or null. */
function linkBelow(bindHost, hostTarget) {
  const parts = path.relative(bindHost, hostTarget).split(path.sep).filter(Boolean).slice(0, -1);
  for (let i = 1; i <= parts.length; i += 1) {
    const here = path.join(bindHost, ...parts.slice(0, i));
    try { if (lstatSync(here).isSymbolicLink()) return here; } catch { return null; } // a component that is absent leaves the target dangling, said by `exists`
  }
  return null;
}

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
  if (raw.split('/').includes('..')) return high('the target holds a ".." segment: the kernel resolves it after following the links before it, so no lexical reading is that path', { link, target });
  const held = holding(binds, 'box', target);
  if (held === null) return high('the target maps to no read-write bind (outside the agent-writable tree, or unmappable)', { link, target });
  if (held.bind.box !== held.bind.host) return high(`the target lies under the box path ${held.bind.box}, which is a different place on the host: a host reader resolves it differently`, { link, target });
  const hostTarget = held.bind.host + held.rest;
  const through = linkBelow(held.bind.host, hostTarget);
  if (through !== null) return high('a directory in the target path is itself a link, which redirects the path', { link, target });
  if (!exists(hostTarget)) return high('the target does not exist (dangling)', { link, target });
  return { severity: 'inside', link, target };
}
