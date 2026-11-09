// The bounded reads of the two-repository leak check (build step 2b, code round): a commit's tree and a history as TEXTS to scan, in batches bounded by count and by bytes, so a repository larger than one batch is never a refusal for its
// size alone. Files are read by `git cat-file --batch` over ids whose sizes `ls-tree -l` gave (a batch holds at most `blobs` files and `blobBytes` bytes, and never more than the read limit); a history is read per range of commits
// (`log --no-walk --stdin`), and a range whose output the read limit cut is split in halves and read again, down to one commit (one commit's output beyond the limit is a refusal naming that commit). Every object asked for must come
// back exactly once: a missing object, a cut answer and an answer that ended early are each a refusal with a reason of its own. Progress is one line per phase and per batch, in counts only.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { failed, finished, git, gitText } from './leak-git.mjs';

/** The default limits: files and bytes per `cat-file` batch, commits per `log` range, and the most bytes one git answer may hold. */
export const DEFAULT_LIMITS = Object.freeze({ blobs: 2000, blobBytes: 64 * 1024 * 1024, commits: 200, readBytes: 256 * 1024 * 1024 });

/** What one object costs in a batch beyond its bytes: its header line. */
const HEADER_COST = 128;

const stop = (kind, text) => fail(`git's answer ${text}`, { kind });

/** `ok({ id, type, size, next })` of the header line at `at`, or the failure: the answer ended, or was cut inside the header. */
function headerAt(bytes, at) {
  if (at >= bytes.length) return stop('ended-early', 'ended before every object was answered');
  const end = bytes.indexOf(0x0a, at);
  if (end === -1) return stop('truncated', 'was cut inside a header line');
  const [id, type, size] = bytes.toString('latin1', at, end).split(' ');
  return ok({ id, type, size: Number(size), next: end + 1 });
}

/** `ok({ next, body })` for an object of `size` bytes at `start` that the answer holds whole and closes with a line feed, else a cut answer. */
const bodyAt = (bytes, start, size) => (Number.isInteger(size) && bytes[start + size] === 0x0a ? ok({ next: start + size + 1, body: bytes.subarray(start, start + size) }) : stop('truncated', 'was cut inside an object'));

/** One answer of `git cat-file --batch`, for the object `oid` that was asked at this place: `ok({ next, body })` or the failure with its kind. */
function readOne(bytes, at, oid) {
  const head = headerAt(bytes, at);
  if (!head.ok) return head;
  if (head.value.id !== oid) return stop('mismatch', `named object ${head.value.id.slice(0, 12)} where ${oid.slice(0, 12)} was asked`);
  return head.value.type === 'missing' ? fail(`object ${oid.slice(0, 12)} is missing from the repository`, { kind: 'missing' }) : bodyAt(bytes, head.value.next, head.value.size);
}

/**
 * The answers of `git cat-file --batch` for `oids`, in order: `ok(Map(oid -> Buffer))`, or a `fail` with a `kind` of its own: `missing` (an object the repository does not hold), `truncated` (an answer cut inside a header or an
 * object), `ended-early` (fewer answers than objects asked), `mismatch` (another object than the one asked) and `extra` (more bytes than the objects asked). Every id is accounted for exactly once, or the whole is refused.
 */
export function parseBatch(bytes, oids) {
  const found = new Map();
  let at = 0;
  for (const oid of oids) {
    const one = readOne(bytes, at, oid);
    if (!one.ok) return one;
    found.set(oid, one.value.body);
    at = one.value.next;
  }
  return at === bytes.length ? ok(found) : stop('extra', 'holds more than the objects that were asked');
}

/** True when `item` still fits the open batch (`batch` is undefined when there is none yet): under the count and the byte budget. */
const fits = (batch, item, { count, bytes }) => batch !== undefined && batch.items.length < count && batch.sum + item.cost <= bytes;

/** The batches of `items` (`{ id, cost }`, in order): at most `count` of them and `bytes` in all, each cost being its size and its header; an item over the byte budget stands alone. */
export function planBatches(items, limits) {
  const batches = [];
  for (const item of items) {
    const last = batches.at(-1);
    if (fits(last, item, limits)) { last.items.push(item); last.sum += item.cost; } else batches.push({ items: [item], sum: item.cost });
  }
  return batches.map(b => b.items);
}

const parseEntry = entry => {
  const tab = entry.indexOf('\t');
  const [, type, oid, size] = entry.slice(0, tab).split(/ +/);
  return { type, oid, size: Number(size), file: entry.slice(tab + 1) };
};

/** `git ls-tree -r -l -z` output as `{ blobs: [{ oid, size, file }], modules: [file] }` (a `commit` entry is a submodule's root: another repository, scanned by its own checks). */
export function parseTree(listing) {
  const entries = listing.split('\0').filter(Boolean).map(parseEntry);
  return { blobs: entries.filter(e => e.type === 'blob'), modules: entries.filter(e => e.type === 'commit').map(e => e.file) };
}

/** `ok({ blobs, modules })` of a commit's tree, each blob with its size. */
async function listTree(repo, commit) {
  const listed = await gitText(repo, ['ls-tree', '-r', '-l', '-z', '--full-tree', commit, '--'], 'the file list of the tree');
  return listed.ok ? ok(parseTree(listed.value)) : listed;
}

/** True when the command ran and the read limit cut its output. */
const cutByLimit = r => r.ok && r.value.truncated;

/** The answers of a batch, or a cut answer when the read limit cut the output and the objects were not all there. */
function parsedOrCut(r, ids) {
  const parsed = parseBatch(r.value.stdoutBytes, ids);
  return cutByLimit(r) && !parsed.ok ? stop('truncated', 'was cut by the read limit') : parsed;
}

/** `ok(Map(oid -> Buffer))` of one batch of blobs: one `git cat-file --batch`, every id accounted for. */
async function readBlobBatch({ repo, ids, limits }) {
  const r = await git(repo, ['cat-file', '--batch'], { input: ids.map(id => `${id}\n`).join(''), bytes: true, maxBytes: limits.readBytes });
  if (!r.ok || r.value.code !== 0) return failed('the files of the tree', r);
  return parsedOrCut(r, ids);
}

/** `ok(Map)` of every batch of blobs read, one progress line each; the first failure stops. */
async function readAllBlobs({ repo, batches, limits, say }) {
  const bodies = new Map();
  for (const [i, batch] of batches.entries()) {
    say(`tree batch ${i + 1} of ${batches.length} (${batch.length} files)`);
    const got = await readBlobBatch({ repo, ids: batch.map(b => b.id), limits });
    if (!got.ok) return fail(`the files of the tree: ${got.reason}`, { kind: got.kind });
    for (const [id, body] of got.value) bodies.set(id, body);
  }
  return ok(bodies);
}

/**
 * The files of a commit's tree as texts, in bounded batches: `ok({ texts, unreadable, modules })`. A name is `<label>:<path>`. A file larger than the read limit is `unreadable` (an incomplete scan, never a clean one); any other
 * file git cannot give whole is a refusal. `who` is the progress line's prefix, `what` what is read (`the tree at HEAD`); `say` receives the lines.
 */
export async function treeTexts({ repo, commit, label, limits = DEFAULT_LIMITS, who = '', what = 'the tree', progress = () => {} }) {
  const listed = await listTree(repo, commit);
  if (!listed.ok) return listed;
  const sizeless = listed.value.blobs.filter(b => !Number.isFinite(b.size));
  if (sizeless.length > 0) return fail(`the files of the tree: ${sizeless.length} object(s) are missing from the repository (git cannot give their size)`, { kind: 'missing' });
  const unique = [...new Map(listed.value.blobs.map(b => [b.oid, { id: b.oid, cost: b.size + HEADER_COST }])).values()];
  const readable = unique.filter(item => item.cost <= limits.readBytes);
  const batches = planBatches(readable, { count: limits.blobs, bytes: Math.min(limits.blobBytes, limits.readBytes) });
  const say = line => progress(`${who}: ${line}`);
  say(`reading ${what} (${listed.value.blobs.length} files in ${batches.length} batch(es))`);
  const bodies = await readAllBlobs({ repo, batches, limits, say });
  if (!bodies.ok) return bodies;
  const named = listed.value.blobs.map(b => ({ name: `${label}:${b.file}`, body: bodies.value.get(b.oid) }));
  return ok({ texts: named.filter(n => n.body !== undefined).map(n => ({ name: n.name, raw: n.body.toString('latin1'), diff: false })), unreadable: named.filter(n => n.body === undefined).map(n => n.name), modules: listed.value.modules });
}

/** `ok([commit id])` of the history `walk` names, in git's order. */
async function commitsOf(repo, walk) {
  const r = await gitText(repo, ['rev-list', ...walk, '--'], 'the commits of the history');
  return r.ok ? ok(r.value.split('\n').filter(Boolean)) : r;
}

/** The two halves of a range, each read again: `ok([{ ids, text }])`. */
async function splitRange({ repo, ids, args, limits }) {
  const half = Math.ceil(ids.length / 2);
  const first = await logRange({ repo, ids: ids.slice(0, half), args, limits });
  const second = first.ok ? await logRange({ repo, ids: ids.slice(half), args, limits }) : first;
  return second.ok ? ok([...first.value, ...second.value]) : second;
}

/** `ok([{ ids, text }])` for the commits `ids` read with `args` (`log --no-walk=unsorted --stdin`): a range whose output the read limit cut is split in halves and read again; one commit alone over the limit is a refusal naming it. */
async function logRange({ repo, ids, args, limits }) {
  const r = await git(repo, ['log', '--no-walk=unsorted', '--stdin', ...args, '--'], { input: `${ids.join('\n')}\n`, maxBytes: limits.readBytes });
  if (finished(r)) return ok([{ ids, text: r.value.stdout }]);
  if (!cutByLimit(r)) return failed('the history', r);
  return ids.length === 1 ? fail(`the output for commit ${ids[0].slice(0, 12)} is larger than the read limit`) : splitRange({ repo, ids, args, limits });
}

const PATCH_ARGS = Object.freeze(['-p', '--cc', '--no-color', '--no-ext-diff', '--no-textconv', '--format=%H']);

/** The texts of one range of commits: their messages, then their patches. `from` is the 0-based index of the first, `total` the number of commits in all. */
async function rangeTexts({ repo, ids, from, total, limits }) {
  const texts = [];
  for (const [kind, args, diff] of [['commit messages', ['--format=%B'], false], ['patches', PATCH_ARGS, true]]) {
    const got = await logRange({ repo, ids, args, limits });
    if (!got.ok) return got;
    let at = from;
    for (const part of got.value) { texts.push({ name: `${kind} of the local branches (commits ${at + 1}-${at + part.ids.length} of ${total})`, raw: part.text, diff }); at += part.ids.length; }
  }
  return ok(texts);
}

/** The commit messages and every patch of the history `walk` names, as texts to scan, in ranges of `limits.commits` commits. `--cc` adds the lines a merge changed that no parent had. `ok([texts])` | `fail`. */
export async function historyTexts({ repo, walk, limits = DEFAULT_LIMITS, who = '', progress = () => {} }) {
  const commits = await commitsOf(repo, walk);
  if (!commits.ok) return commits;
  const ranges = Array.from({ length: Math.ceil(commits.value.length / limits.commits) }, (_, i) => commits.value.slice(i * limits.commits, (i + 1) * limits.commits));
  progress(`${who}: reading the history (${commits.value.length} commits in ${ranges.length} batch(es))`);
  const texts = [];
  for (const [i, ids] of ranges.entries()) {
    progress(`${who}: history batch ${i + 1} of ${ranges.length} (${ids.length} commits)`);
    const got = await rangeTexts({ repo, ids, from: i * limits.commits, total: commits.value.length, limits });
    if (!got.ok) return got;
    texts.push(...got.value);
  }
  return ok(texts);
}

/** The staged changes as one diff text (an unborn branch compares with the empty tree). */
export async function stagedText(repo, limits = DEFAULT_LIMITS) {
  const r = await git(repo, ['diff', '--cached', '--no-color', '--no-ext-diff', '--no-textconv', '--'], { maxBytes: limits.readBytes });
  return finished(r) ? ok({ name: 'staged changes', raw: r.value.stdout, diff: true }) : failed('the staged changes', r);
}

/** `ok([path])` of what git would commit from the working tree: tracked and untracked files that are not ignored. */
export async function workingFiles(repo) {
  const r = await gitText(repo, ['ls-files', '--cached', '--others', '--exclude-standard', '-z', '--'], 'the file list of the working tree');
  return r.ok ? ok([...new Set(r.value.split('\0').filter(Boolean))].sort()) : r;
}
