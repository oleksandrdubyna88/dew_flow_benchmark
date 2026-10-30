// Classifying a link found in the agent-writable tree (E2.S2 B-S3). The CLI itself makes some (`tmp/claude-<uid>/.../tasks/<agent id>.output`
// -> the subagent's own transcript under `config/projects/`), and `npm install` makes `node_modules/.bin/tsc -> ../typescript/bin/tsc`, so "any link
// is a finding" refuted B-S3 on harmless ones (and would have refuted a TypeScript session where a C# one passes). The rule:
//   - the verdict comes from the link's TARGET STRING and the lstat of the directories on its way (never a read of the target): the link is never followed;
//   - the kernel resolves `..` AFTER following the links before it, so a lexical reading of a path equals the kernel's only when no directory the
//     kernel traverses is a link. Every verdict below rests on lstat-verifying exactly those directories;
//   - an ABSOLUTE target is `inside` only when: it has no `..` segment; it maps under a read-write bind whose box path and host path are THE SAME path
//     (`work`, `home`, `config`, `nuget`, `npm`, `out`; the box's `/tmp` is `box/tmp` on the host, so an absolute `/tmp/...` is the HOST's `/tmp` for a host reader
//     and is never inside); no component before the last is a link; and the target exists;
//   - a RELATIVE target (`..` allowed) is resolved from the link's own directory, and is `inside` only when: every directory above the link, and every
//     directory the walk of the target string steps through (before its last component, a `..` taken from a verified directory), is a real directory (not a
//     link); every step stays within the SAME bind the link lies in; and the target exists. This holds in the box's `/tmp` bind too: a relative link and its
//     target move together between the box path and the host path, so the host reads what the box reads (only an absolute `/tmp` path differs);
//   - everything else is `high`: a target outside every bind or mapping to none, a `..` that leaves the bind, a directory on the way that is a link, an
//     absolute target with a `..` or under the box `/tmp`, a dangling one.
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

/** The first link among the directories from `bindHost` down to `dir` (both host paths, `dir` inside the bind), or null; a component that is absent ends the walk. */
function linkAmongDirs(bindHost, dir) {
  const parts = path.relative(bindHost, dir).split(path.sep).filter(Boolean);
  for (let i = 1; i <= parts.length; i += 1) {
    const here = path.join(bindHost, ...parts.slice(0, i));
    try { if (lstatSync(here).isSymbolicLink()) return here; } catch { return null; }
  }
  return null;
}

/** The part of a relative target string worth walking: its components without empty ones and `.`. */
const stepsOf = raw => raw.split('/').filter(c => c !== '' && c !== '.');

/** A relative target -> `{ ok: true, hostTarget }` or `{ why }`: the walk from the link's directory, verifying every directory it steps through. */
function walkRelative(hostLink, raw, bind) {
  if (linkAmongDirs(bind.host, path.dirname(hostLink)) !== null) return { why: 'a directory above the link is itself a link, which redirects every relative target' };
  const steps = stepsOf(raw);
  let cur = path.dirname(hostLink);
  for (const [i, step] of steps.entries()) {
    cur = step === '..' ? path.dirname(cur) : path.join(cur, step);
    if (!within(cur, bind.host)) return { why: 'a ".." takes the target out of the bind the link lies in, so what it reaches is not this session\'s tree' };
    if (i === steps.length - 1) break;
    let st;
    try { st = lstatSync(cur); } catch { return { why: 'the target does not exist (dangling)' }; }
    if (st.isSymbolicLink()) return { why: 'a directory on the way to the target is itself a link, which redirects the path (and a ".." after it)' };
  }
  return exists(cur) ? { ok: true, hostTarget: cur } : { why: 'the target does not exist (dangling)' };
}

/** An absolute target -> `{ ok: true, bind }` or `{ why }`, by the rules at the top of this file. */
function checkAbsolute(binds, target, raw) {
  if (raw.split('/').includes('..')) return { why: 'the target holds a ".." segment: the kernel resolves it after following the links before it, so no lexical reading is that path' };
  const held = holding(binds, 'box', target);
  if (held === null) return { why: 'the target maps to no read-write bind (outside the agent-writable tree, or unmappable)' };
  if (held.bind.box !== held.bind.host) return { why: `the target lies under the box path ${held.bind.box}, which is a different place on the host: a host reader resolves it differently` };
  const hostTarget = held.bind.host + held.rest;
  if (linkBelow(held.bind.host, hostTarget) !== null) return { why: 'a directory in the target path is itself a link, which redirects the path' };
  return exists(hostTarget) ? { ok: true } : { why: 'the target does not exist (dangling)' };
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
  const verdict = raw.startsWith('/') ? checkAbsolute(binds, target, raw) : walkRelative(hostLink, raw, at.bind);
  return verdict.why === undefined ? { severity: 'inside', link, target } : high(verdict.why, { link, target });
}
