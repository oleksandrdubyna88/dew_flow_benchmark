// The run base's tree (parent D39, E1.S1 brief items 1-2): the tree at a ref, from `git archive` (never a checkout in a working tree),
// minus the README's `## Attribution` section and minus any credit file; and the needle scan over a tree. The orphan commit of this tree
// is E1.S7's. Expected failures are values: ok(value) | fail(reason, extra).
import { lstatSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { refuseInsideWorktree } from '../../minigit-bench/lib/git-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { cutAttribution } from './attribution.mjs';
import { decodeBlob } from './push-decode.mjs';
import { runGitBytes } from './push-git.mjs';
import { parseKnownNeedles } from './push-needles.mjs';
import { sequence } from './push-sequence.mjs';
import { findVisibleRules } from './push-visible.mjs';

export { cutAttribution };
const TREE_MAX_BYTES = 256 * 1024 * 1024;
const FILE_MAX_BYTES = 16 * 1024 * 1024;
const TAR_TIMEOUT_MS = 120_000;
const CREDIT_FILE = /^(?:credits?|attribution)(?:[.\-_].*)?$/i;
const finding = (rel, line, needle) => ({ path: rel, line, needle });

/** Every entry under `root` as `{ rel, abs, stat }`, sorted by relative path (lstat: a link is not followed). */
function walk(root, dir = root) {
  return readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const abs = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(root, abs) : [{ rel: path.relative(root, abs).split(path.sep).join('/'), abs, stat: lstatSync(abs) }];
  }).sort((a, b) => (a.rel < b.rel ? -1 : 1));
}

/** The needle hits of one text, as `{ path, line, needle }`. */
const textHits = (rel, text, known) => findVisibleRules(rel, text, { known }).map(f => finding(rel, f.line, f.what));

/** A hit for an entry that is not a regular file within the ceiling, or null. */
function unreadable(entry) {
  const reasons = [[entry.stat.isSymbolicLink(), 'symlink'], [!entry.stat.isFile(), 'not a regular file'], [entry.stat.size > FILE_MAX_BYTES, 'file larger than the scan ceiling']];
  const reason = reasons.find(([bad]) => bad);
  return reason === undefined ? null : finding(entry.rel, null, reason[1]);
}

function textFindings(entry, { known, extra }) {
  const { text, nul } = decodeBlob(readFileSync(entry.abs));
  const extraHits = extra === null ? [] : textHits(entry.rel, text, extra).filter(h => h.needle.startsWith('needle '));
  const pathHits = textHits(entry.rel, entry.rel, known).map(h => ({ ...h, line: null }));
  return [...textHits(entry.rel, text, known), ...extraHits, ...pathHits, ...(nul ? [finding(entry.rel, null, 'binary file (NUL byte)')] : [])];
}

const fileHits = (entry, options) => { const bad = unreadable(entry); return bad === null ? textFindings(entry, options) : [bad]; };

const byPathThenLine = (a, b) => a.path.localeCompare(b.path, 'en') || (a.line ?? 0) - (b.line ?? 0);

/**
 * ok([{ path, line, needle }]) for every file under `root`: the D18 needles by their rule (case-sensitive, word boundaries, exact
 * substrings; `known` is parseKnownNeedles' answer), links, the family's names and, in the agent-visible folders and the README, plan ids;
 * `literals` are further words read by the caller from a file outside git. A symlink, a non-regular file, an oversize file and a NUL byte are
 * hits too: nothing in the tree goes unread. Never the matched text.
 */
export function needleScan({ root, known, literals = [] }) {
  const extra = literals.length === 0 ? ok(null) : parseKnownNeedles({ schema: 1, words: literals, substrings: [] });
  return extra.ok ? ok(walk(root).flatMap(entry => fileHits(entry, { known, extra: extra.value })).sort(byPathThenLine)) : extra;
}

const emptyOrAbsent = dir => { try { return readdirSync(dir).length === 0; } catch (error) { return error.code === 'ENOENT'; } };

/** ok(Buffer) of `git archive <ref>`, or a fail. */
async function archiveOf(repo, ref) {
  const tar = await runGitBytes(repo, ['archive', '--format=tar', `${ref}^{tree}`], { maxBytes: TREE_MAX_BYTES, timeoutMs: TAR_TIMEOUT_MS });
  return tar.ok ? tar : fail(`git archive ${ref} failed in ${repo}: ${tar.reason}`, { kind: 'config' });
}

/** ok(out) with the archive unpacked by `tar`, run through the shared launcher (exe + argv, timeout, process-tree kill). */
async function extract(tar, out) {
  mkdirSync(out, { recursive: true });
  const r = await runBounded('tar', ['-x', '--no-same-owner', '--no-same-permissions', '-C', out, '-f', '-'], { input: tar, timeoutMs: TAR_TIMEOUT_MS, env: { PATH: process.env.PATH, HOME: os.homedir(), LANG: 'C.UTF-8' } });
  if (!r.ok) return fail(`tar could not be started: ${r.reason}`);
  const { code, timedOut, stderr } = r.value;
  if (timedOut) return fail(`tar did not finish within ${TAR_TIMEOUT_MS} ms unpacking into ${out}`);
  return code === 0 ? ok(out) : fail(`tar could not unpack the archive into ${out}: ${stderr.trim().slice(-200)}`);
}

function cutReadme(out) {
  const file = path.join(out, 'README.md');
  let text;
  try { text = readFileSync(file, 'utf8'); } catch { return fail('the tree has no README.md: its attribution section cannot be cut'); }
  const cut = cutAttribution(text);
  if (cut.ok) writeFileSync(file, cut.value);
  return cut;
}

const removeCredits = out => {
  const credits = walk(out).filter(e => CREDIT_FILE.test(path.posix.basename(e.rel)));
  credits.forEach(e => rmSync(e.abs));
  return credits.map(e => e.rel);
};

const freeFolder = out => (emptyOrAbsent(out) ? ok(out) : fail(`the run-base folder ${out} exists and is not empty: it is never merged into`));

/** ok(out) with the tree unpacked and its README cut; once the folder is ours, any failure removes it. */
async function unpackAndCut(repo, ref, out) {
  const ready = await sequence([() => refuseInsideWorktree(out, 'the run-base folder'), () => freeFolder(out)]);
  if (!ready.ok) return ready;
  const built = await sequence([() => archiveOf(repo, ref), tar => extract(tar, out), () => cutReadme(out)]);
  if (!built.ok) rmSync(out, { recursive: true, force: true });
  return built;
}

const hitLine = h => `${h.path}${h.line === null ? '' : `:${h.line}`} ${h.needle}`;

/** ok({ out, removed }) for a clean scan; otherwise the tree is removed and the fail names `path:line needle` of every hit. */
function finish({ scan, out, removed }) {
  if (scan.ok && scan.value.length === 0) return ok({ out, removed });
  rmSync(out, { recursive: true, force: true });
  return scan.ok ? fail(`the needle scan found ${scan.value.length} hit(s): ${scan.value.map(hitLine).join('; ')}`, { hits: scan.value }) : scan;
}

/**
 * ok({ out, removed }): the tree at `ref` of `repo` in the empty or absent folder `out` (outside every git worktree), its README cut at
 * `## Attribution` (a README without the section or with it twice is refused), credit files removed, then `needleScan`. A hit refuses the
 * cut: the folder is removed and the fail names `path:line needle` for every hit, never the text. `literals` as in needleScan.
 */
export async function generateRunBaseTree({ repo, ref, out, needles, literals = [] }) {
  const cut = await unpackAndCut(repo, ref, out);
  if (!cut.ok) return cut;
  const removed = removeCredits(out);
  return finish({ scan: needleScan({ root: out, known: needles, literals }), out, removed });
}
