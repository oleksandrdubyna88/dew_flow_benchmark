#!/usr/bin/env node
// The sha256 of every raw minigit record, kept so a later reader can tell that the records the data files were drawn from are the ones that exist.
//
//   node research/minigit/tools/manifest.mjs [write] [--runs <dir>] [--data <dir>] [--private <file>]
//   node research/minigit/tools/manifest.mjs verify [--runs <dir>] [--data <dir>] [--manifest <dir>]
//   node research/minigit/tools/manifest.mjs --self-check
//
// --runs defaults to $MINIGIT_RUNS or ~/rsd/minigit. --data defaults to research/minigit/data next to this tool (never the working directory).
// Every path in a manifest is relative to --runs, so ~/rsd/minigit and an extracted copy named `minigit` give the same lines.
//
// The walk does not follow symbolic links. A regular file gives its sha256 (streamed) and its size in bytes. A symbolic link gives the sha256 of
// its target text and `link` in the size column. A directory is implied by the paths below it. A socket and a FIFO hold no bytes: they are skipped,
// not recorded, and counted in the summary (`skipped`, over the whole tree and per top-level folder). A device, any other type, or a name that holds
// a tab, a CR, an LF or bytes that are not UTF-8, stops the run with an error that names the entry's index in the walk and never its path.
// An entry whose path starts with `heldout-src/` is held out: it goes to the private list only. Each list is sorted by the bytes of the path, and a
// line is `sha256<TAB>size|link<TAB>path<LF>`.
//
// write (the default): writes <data>/raw-manifest.tsv (the public list) and <data>/raw-manifest.json (counts per top-level folder, the sha256 of the
// public list, the count and sha256 of the private list, and the skipped counts). Both go to *.tmp beside the targets, the pair is re-read, then the tsv is renamed, then
// the json. --private <file> also writes the private list there; the path may not be inside this repository (exit 2, nothing written). The private list is
// staged beside its target first and renamed LAST, after the public pair: a failed publish deletes the staged file and keeps the old private list.
// verify: first reads <manifest>/raw-manifest.tsv and .json (a missing or unreadable one fails before --runs is looked at), then recomputes both lists over
// --runs and compares them with the tsv (<manifest> defaults to <data>): `missing`, `changed` and `extra` lines name public paths, and a summary difference is
// named by field (`public.bytes`, `topLevel["<folder>"].sha256`). The summary must hold the sha256 of that tsv (a mixed generation fails), and a --manifest folder other than <data> must hold
// the same two files as <data>, byte for byte. The `skipped` counts are not compared (a tar copy drops sockets). The held-out list is compared by count and sha256 only; when <manifest>/raw-manifest-heldout.tsv exists
// its bytes must hash to the summary's value and the entries are compared too, but a difference prints as three counts, never a name.
// Both modes then check every <data>/s-*.json: each extractedFrom.sources hash must equal the manifest's hash of <seriesId>/<file>.
//
// Exit codes: 0 identical | 1 a difference, a failed check or an entry that cannot be recorded | 2 usage, or a refused --private path.
//
// What it does NOT do: it writes nothing under --runs, opens no file for any reason but its hash, reads no token, ~/.config or score/ contents
// beyond hashing them as bytes, and prints no held-out path (the private list is the only place one is written, and only where --private says).
// It records no timestamps, owners or modes, so a copy that keeps the bytes keeps the manifest. A symbolic link's target is hashed as text, and tar
// keeps that text, so the hash survives a move; a link with an absolute target may then point at nothing, which the manifest does not check.
// --self-check builds a synthetic runs root and data folder in a temp folder, runs the tool on them, and exits 1 on any unexpected answer.
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { isUtf8 } from 'node:buffer';
import { chmodSync, createReadStream, existsSync, lstatSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, readlinkSync, realpathSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { basename, dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const SELF = fileURLToPath(import.meta.url);
const HERE = dirname(SELF);
const DATA = resolve(HERE, '..', 'data');
const REPO = resolve(HERE, '..', '..', '..');
const TOOL = 'research/minigit/tools/manifest.mjs';
const HELDOUT_PREFIX = 'heldout-src/';
const TSV = 'raw-manifest.tsv';
const SUMMARY = 'raw-manifest.json';
const HELDOUT_TSV = 'raw-manifest-heldout.tsv';

/** An expected failure: its message is written by this tool and never holds a path of an entry. */
class Fail extends Error {}
/** A wrong command line: exit 2. */
class Usage extends Fail {}

const sha256 = data => createHash('sha256').update(data).digest('hex');
/** The operator's home prefix written as `~`, so no absolute path of the machine reaches a committed file. */
const tilde = (text, home) => (home ? text.split(home).join('~') : text);

/** What may be printed about an error: ours as written, a system error as call and code (Node puts the path in its message), anything else by name. */
export function describe(e) {
  if (e instanceof Fail) return e.message;
  if (e?.syscall && e?.code) return `${e.syscall} failed (${e.code})`;
  return `${e?.name ?? 'Error'}${e?.code ? ` (${e.code})` : ''}`;
}

function args(argv) {
  const out = { mode: 'write', runs: process.env.MINIGIT_RUNS || join(homedir(), 'rsd', 'minigit'), data: DATA, manifest: null, private: null, selfCheck: false };
  const valued = { '--runs': 'runs', '--data': 'data', '--manifest': 'manifest', '--private': 'private' };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--self-check') out.selfCheck = true;
    else if (a === 'write' || a === 'verify') out.mode = a;
    else if (Object.hasOwn(valued, a)) {
      if (argv[i + 1] === undefined) throw new Usage(`${a} needs a value`);
      out[valued[a]] = resolve(argv[++i]);
    } else throw new Usage(`unknown argument ${a}; legal: write | verify, --runs <dir>, --data <dir>, --manifest <dir> (verify), --private <file> (write), --self-check`);
  }
  if (out.manifest && out.mode !== 'verify') throw new Usage('--manifest is for verify');
  if (out.private && out.mode !== 'write') throw new Usage('--private is for write');
  return { ...out, runs: resolve(out.runs) };
}

// ---- the walk ------------------------------------------------------------------------------------------------------------------------------

const SLASH = Buffer.from('/');
const KINDS = [['isDirectory', 'directory'], ['isSymbolicLink', 'link'], ['isFile', 'file'], ['isSocket', 'socket'], ['isFIFO', 'fifo'], ['isCharacterDevice', 'character device'], ['isBlockDevice', 'block device']];
/** The two kinds that hold no bytes and are counted, never recorded: a runtime leftover, and tar drops a socket. */
const SKIPPED = ['socket', 'fifo'];
const kindOf = st => (KINDS.find(([method]) => st[method]()) ?? [null, 'unknown'])[1];

/** Every entry under `dirAbs`, depth first, the names of a folder in byte order. Names stay Buffers: a name that is not UTF-8 must not be turned into text here. */
function walk(dirAbs, dirRel) {
  return readdirSync(dirAbs, { encoding: 'buffer' }).sort(Buffer.compare).flatMap(name => {
    const abs = Buffer.concat([dirAbs, SLASH, name]);
    const rel = dirRel.length ? Buffer.concat([dirRel, SLASH, name]) : name;
    const st = lstatSync(abs);
    const here = { abs, rel, kind: kindOf(st), size: st.size };
    return here.kind === 'directory' ? [here, ...walk(abs, rel)] : [here];
  });
}

/** The entry at `index`, or a Fail that names the index and the problem, never the path. */
export function checked(entry, index) {
  if (!isUtf8(entry.rel)) throw new Fail(`entry ${index}: its path is not valid UTF-8`);
  if (/[\t\r\n]/.test(entry.rel.toString('utf8'))) throw new Fail(`entry ${index}: its path holds a tab, a CR or an LF`);
  if (!['directory', 'file', 'link', ...SKIPPED].includes(entry.kind)) throw new Fail(`entry ${index}: it is a ${entry.kind}; only regular files and symbolic links are recorded (a socket and a FIFO are skipped and counted)`);
  return entry;
}

async function hashFile(abs) {
  const h = createHash('sha256');
  let bytes = 0;
  for await (const chunk of createReadStream(abs)) { h.update(chunk); bytes += chunk.length; }
  return { hash: h.digest('hex'), bytes };
}

async function record(entry, index) {
  const path = entry.rel.toString('utf8');
  if (entry.kind === 'link') return { path, hash: sha256(readlinkSync(entry.abs, { encoding: 'buffer' })), size: 'link', bytes: 0 };
  const { hash, bytes } = await hashFile(entry.abs);
  if (bytes !== entry.size) throw new Fail(`entry ${index}: its size changed while it was read`);
  return { path, hash, size: String(bytes), bytes };
}

/** The items sorted by the UTF-8 bytes of `keyOf(item)`: each key is turned into bytes once, then compared as bytes (never as text). */
const sortedByBytes = (items, keyOf) => items.map(item => [Buffer.from(keyOf(item)), item]).sort(([a], [b]) => Buffer.compare(a, b)).map(([, item]) => item);
const isHeldout = e => e.path.startsWith(HELDOUT_PREFIX);

const countSkipped = entries => Object.fromEntries(SKIPPED.map(k => [k, entries.filter(e => e.kind === k).length]));
const firstOf = entry => entry.rel.toString('utf8').split('/')[0];

/** The two sorted lists of a runs root, and the sockets and FIFOs it skipped (`skipped` over the tree, `skippedBy` per top-level name): `{ pub, held, skipped, skippedBy }`. */
export async function scan(runs) {
  const all = walk(Buffer.from(runs), Buffer.alloc(0)).map(checked);
  const recorded = [];
  for (const [index, entry] of all.entries()) if (['file', 'link'].includes(entry.kind)) recorded.push(await record(entry, index));
  const sorted = sortedByBytes(recorded, e => e.path);
  const skipped = all.filter(e => SKIPPED.includes(e.kind));
  const skippedBy = Object.fromEntries([...new Set(skipped.map(firstOf))].map(top => [top, countSkipped(skipped.filter(e => firstOf(e) === top))]));
  return { pub: sorted.filter(e => !isHeldout(e)), held: sorted.filter(isHeldout), skipped: countSkipped(skipped), skippedBy };
}

// ---- the lists and the summary -------------------------------------------------------------------------------------------------------------

const lineOf = e => `${e.hash}\t${e.size}\t${e.path}\n`;
const textOf = entries => entries.map(lineOf).join('');
const totalBytes = entries => entries.reduce((a, e) => a + e.bytes, 0);
const topOf = e => e.path.split('/')[0];

export function summaryOf(runs, { pub, held, skipped, skippedBy }) {
  const folders = [...new Set(pub.map(topOf))];
  const folder = name => { const own = pub.filter(e => topOf(e) === name); return [name, { entries: own.length, bytes: totalBytes(own), sha256: sha256(textOf(own)), skipped: skippedBy[name] ?? countSkipped([]) }]; };
  return {
    runsRoot: tilde(runs, homedir()),
    public: { entries: pub.length, bytes: totalBytes(pub) },
    topLevel: Object.fromEntries(folders.map(folder)),
    publicSha256: sha256(textOf(pub)),
    heldout: { entries: held.length, sha256: sha256(textOf(held)) },
    skipped,
    tool: TOOL,
  };
}

const jsonText = data => `${JSON.stringify(data, null, 1)}\n`;

/** A manifest text as `path -> "hash<TAB>size"`; a malformed line is named by number, never shown (a private list holds held-out paths). */
function parseList(text, what) {
  if (text !== '' && !text.endsWith('\n')) throw new Fail(`${what} does not end with a line end`);
  const pairs = text.split('\n').slice(0, -1).map((line, i) => {
    const parts = line.split('\t');
    if (parts.length !== 3) throw new Fail(`${what} line ${i + 1} is not sha256, size and path`);
    return [parts[2], `${parts[0]}\t${parts[1]}`];
  });
  if (new Set(pairs.map(p => p[0])).size !== pairs.length) throw new Fail(`${what} names a path twice`);
  return new Map(pairs);
}

/** The paths only in the manifest (missing), in both with another hash or size (changed), and only in the tree (extra). */
function compare(committed, entries) {
  const now = new Map(entries.map(e => [e.path, `${e.hash}\t${e.size}`]));
  const paths = ms => sortedByBytes([...ms.keys()], p => p);
  return {
    missing: paths(committed).filter(p => !now.has(p)),
    changed: paths(committed).filter(p => now.has(p) && now.get(p) !== committed.get(p)),
    extra: paths(now).filter(p => !committed.has(p)),
  };
}

// ---- the cross-check against the data files ------------------------------------------------------------------------------------------------

/** For each <data>/s-*.json: the problems found (empty when its sources match the manifest), and how many files were read. */
export function crossCheck(data, pub) {
  const files = existsSync(data) ? readdirSync(data).filter(f => /^s-.*\.json$/.test(f)).sort() : [];
  const hashes = new Map(pub.map(e => [e.path, e.hash]));
  const problems = files.flatMap(f => {
    const j = readJsonFile(join(data, f));
    const id = j?.series?.seriesId;
    if (typeof id !== 'string') return [`${f}: has no series.seriesId`];
    return Object.entries(j?.extractedFrom?.sources ?? {}).flatMap(([file, want]) => {
      const have = hashes.get(`${id}/${file}`);
      if (have === want) return [];
      return [`series ${id}: ${file} ${have === undefined ? 'is not in the manifest' : 'has another hash than the data file recorded'}`];
    });
  });
  return { files: files.length, problems };
}

function readJsonFile(file, bytes = null) {
  try { return JSON.parse((bytes ?? readFileSync(file)).toString('utf8')); } catch (e) {
    if (e instanceof SyntaxError) throw new Fail(`${basename(file)} is not valid JSON`);
    throw e;
  }
}

const crossLines = (data, cross) => (cross.files === 0 ? [`note: no s-*.json in ${data}; the source hashes were not cross-checked`] : cross.problems);

// ---- write ----------------------------------------------------------------------------------------------------------------------------------

/** The nearest existing ancestor, resolved through links, plus the rest: where a file that does not exist yet would really land. */
function landing(p) {
  try { return realpathSync(p); } catch { return p === dirname(p) ? p : join(landing(dirname(p)), basename(p)); }
}

export function insideRepo(file, repo = REPO) {
  const rel = relative(landing(repo), landing(resolve(file)));
  return rel === '' || (rel !== '..' && !rel.startsWith(`..${sep}`) && !isAbsolute(rel));
}

/** Two generations never mix: both files are written aside and re-read before either name changes, the json (the one a reader opens first) last. */
export function publish({ data, tsvText, summary, privatePath, privateText }) {
  const tmp = f => `${f}.tmp`;
  const targets = [join(data, TSV), join(data, SUMMARY), ...(privatePath ? [privatePath] : [])];
  mkdirSync(data, { recursive: true });
  if (privatePath) mkdirSync(dirname(privatePath), { recursive: true });
  try {
    if (privatePath) writeFileSync(tmp(privatePath), privateText, { mode: 0o600 });
    writeFileSync(tmp(targets[0]), tsvText);
    writeFileSync(tmp(targets[1]), jsonText(summary));
    const back = JSON.parse(readFileSync(tmp(targets[1]), 'utf8'));
    if (back.publicSha256 !== sha256(readFileSync(tmp(targets[0])))) throw new Fail('the summary and the list written aside do not match; nothing was published');
    renameSync(tmp(targets[0]), targets[0]);
    renameSync(tmp(targets[1]), targets[1]);
    if (privatePath) renameSync(tmp(privatePath), privatePath); // only after the public pair: a failed publish keeps the old private list
  } finally {
    targets.forEach(t => rmSync(tmp(t), { force: true }));
  }
}

/** --runs must be a folder that exists; checked when the run needs it, so a missing manifest is named first. */
function requireRuns(a) {
  if (!existsSync(a.runs)) throw new Fail(`--runs ${a.runs} does not exist`);
}

async function writeMode(a) {
  if (a.private && insideRepo(a.private)) throw new Usage('--private may not be a path inside this repository; nothing was written');
  requireRuns(a);
  const scanned = await scan(a.runs);
  const { pub, held } = scanned;
  const cross = crossCheck(a.data, pub);
  if (cross.problems.length) return { lines: [...cross.problems, `FAILED: ${cross.problems.length} source hash(es) differ; nothing was written`], code: 1 };
  const summary = summaryOf(a.runs, scanned);
  publish({ data: a.data, tsvText: textOf(pub), summary, privatePath: a.private, privateText: textOf(held) });
  return {
    lines: [...crossLines(a.data, cross), `wrote ${join(a.data, TSV)} and ${SUMMARY}: ${pub.length} public entries (${summary.public.bytes} bytes), ${held.length} held-out entries kept out, skipped ${summary.skipped.socket} socket(s) and ${summary.skipped.fifo} FIFO(s), publicSha256 ${summary.publicSha256}${a.private ? '; private list written' : ''}`],
    code: 0,
  };
}

// ---- verify ---------------------------------------------------------------------------------------------------------------------------------

const readOrNull = file => (existsSync(file) ? readFileSync(file) : null);
const named = (kind, paths) => paths.map(p => `${kind} ${p}`);

/** The held-out checks: the summary's count and sha256, and (when the private list is beside the manifest) its bytes and entries, as counts only. */
function heldoutProblems({ dir, summary, held }) {
  const sha = sha256(textOf(held));
  const recorded = summary.heldout;
  const own = recorded?.entries === held.length && recorded?.sha256 === sha ? [] : [`heldout: ${held.length} entries with sha256 ${sha}, the summary has ${recorded?.entries} entries with sha256 ${recorded?.sha256}`];
  const bytes = readOrNull(join(dir, HELDOUT_TSV));
  if (bytes === null) return own;
  const hashed = sha256(bytes) === recorded?.sha256 ? [] : [`heldout: ${HELDOUT_TSV} does not hash to the summary's heldout sha256`];
  const d = compare(parseList(bytes.toString('utf8'), HELDOUT_TSV), held);
  const counted = d.missing.length + d.changed.length + d.extra.length === 0 ? [] : [`heldout: ${d.missing.length} missing, ${d.changed.length} changed, ${d.extra.length} extra`];
  return [...own, ...hashed, ...counted];
}

/** The folders' fields without `skipped`: a tar copy drops sockets, so the counts of what was skipped are not part of what verify compares. */
const withoutSkipped = topLevel => Object.fromEntries(Object.entries(topLevel ?? {}).map(([name, { skipped, ...rest }]) => [name, rest]));
const comparable = summary => ({ public: summary.public, topLevel: withoutSkipped(summary.topLevel) });

const differs = (path, mine, theirs) => (JSON.stringify(mine) === JSON.stringify(theirs) ? [] : [`${path} differs`]);

/** Where the committed summary and the recomputed one differ, named by field (`public.bytes`, `topLevel["docs"].sha256`). Only public names can appear. */
function summaryProblems(summary, current) {
  const [mine, theirs] = [comparable(summary), comparable(current)];
  const folders = [...new Set([...Object.keys(mine.topLevel), ...Object.keys(theirs.topLevel)])];
  const folder = name => {
    const path = `topLevel[${JSON.stringify(name)}]`;
    if (!Object.hasOwn(mine.topLevel, name)) return [`${path} is not in the summary`];
    if (!Object.hasOwn(theirs.topLevel, name)) return [`${path} is in the summary but not in the tree`];
    return ['entries', 'bytes', 'sha256'].flatMap(f => differs(`${path}.${f}`, mine.topLevel[name][f], theirs.topLevel[name][f]));
  };
  return [...['entries', 'bytes'].flatMap(f => differs(`public.${f}`, mine.public?.[f], theirs.public[f])), ...folders.flatMap(folder)];
}

/** The two manifest files of `dir`, read and parsed, before anything else is done: a missing or unreadable one is a plain answer, not a scan error. */
function readManifest(dir) {
  const read = f => {
    const file = join(dir, f);
    if (!existsSync(file)) throw new Fail(`${f} is missing in ${dir}`);
    try { return readFileSync(file); } catch { throw new Fail(`${f} in ${dir} cannot be read`); }
  };
  const [tsv, json] = [read(TSV), read(SUMMARY)];
  return { tsv, summary: readJsonFile(join(dir, SUMMARY), json), committed: parseList(tsv.toString('utf8'), TSV) };
}

async function verifyMode(a) {
  const dir = a.manifest ?? a.data;
  const { tsv, summary, committed } = readManifest(dir);
  requireRuns(a);
  const scanned = await scan(a.runs);
  const { pub, held } = scanned;
  const cross = crossCheck(a.data, pub);
  const apart = dir === a.data ? [] : [TSV, SUMMARY].filter(f => !(readOrNull(join(a.data, f))?.equals(readOrNull(join(dir, f))))).map(f => `${f} in ${dir} differs from ${a.data}`);
  const mixed = sha256(tsv) === summary.publicSha256 ? [] : [`mixed generation: ${SUMMARY} holds another publicSha256 than the sha256 of ${TSV}`];
  const d = compare(committed, pub);
  const problems = [
    ...apart, ...mixed, ...named('missing', d.missing), ...named('changed', d.changed), ...named('extra', d.extra),
    ...summaryProblems(summary, summaryOf(a.runs, scanned)), ...heldoutProblems({ dir, summary, held }), ...cross.problems,
  ];
  if (problems.length) return { lines: [...crossLines(a.data, cross), ...problems, `FAILED: ${problems.length} difference(s)`], code: 1 };
  return { lines: [...crossLines(a.data, cross), `ok: ${pub.length} public entries (${totalBytes(pub)} bytes), ${held.length} held-out entries, ${cross.files} series file(s) cross-checked`], code: 0 };
}

// ---- self-check -----------------------------------------------------------------------------------------------------------------------------

const SERIES = 's-20990101-abcd';
const SECRET = 'secret-case-name';
const RUN_MAP = JSON.stringify({ seriesId: SERIES });

/** One synthetic world: a runs root under a fake home, a data folder beside it, and the tool run on them as a child process. */
function world(base, name) {
  const root = join(base, name);
  const home = join(root, 'home');
  const runs = join(home, 'minigit');
  const data = join(root, 'data');
  const env = { ...Object.fromEntries(Object.entries(process.env).filter(([k]) => k !== 'MINIGIT_RUNS')), HOME: home };
  const put = (rel, content) => { const f = join(runs, rel); mkdirSync(dirname(f), { recursive: true }); writeFileSync(f, content); };
  const cli = (...argv) => { const r = spawnSync(process.execPath, [SELF, ...argv], { encoding: 'utf8', env }); return { code: r.status, out: `${r.stdout}${r.stderr}` }; };
  const w = {
    root, runs, data, put, cli,
    write: (...more) => cli('write', '--runs', runs, '--data', data, ...more),
    verify: (...more) => cli('verify', '--runs', runs, '--data', data, ...more),
    tsv: () => readFileSync(join(data, TSV), 'utf8'),
    putSeries: sources => { mkdirSync(data, { recursive: true }); writeFileSync(join(data, `${SERIES}.json`), JSON.stringify({ series: { seriesId: SERIES }, extractedFrom: { sources } })); },
  };
  put(`${SERIES}/run-map.json`, RUN_MAP);
  put('docs/a-file', 'a\n');
  put('docs/B-file', 'b\n');
  put('docs/z～', 'bmp\n');
  put('docs/z\u{1F600}', 'astral\n');
  put('notes.txt', 'notes\n');
  put(`heldout-src/${SECRET}.py`, 'print(1)\n');
  symlinkSync('run-map.json', join(runs, SERIES, 'link'));
  symlinkSync(SERIES, join(runs, 'dirlink'));
  w.putSeries({ 'run-map.json': sha256(RUN_MAP) });
  return w;
}

const clean = text => !text.includes(SECRET);

/** [name, (world) -> list of what went wrong]. Each case builds its own world, so no case depends on another. */
const CASES = [
  ['write then verify on an untouched tree exits 0', w => {
    const a = w.write(); const b = w.verify();
    return [...(a.code === 0 ? [] : [`write exit ${a.code}: ${a.out}`]), ...(b.code === 0 && /^ok: /m.test(b.out) ? [] : [`verify exit ${b.code}: ${b.out}`])];
  }],
  ['the public list, the summary and every output hold no held-out name', w => {
    const outs = [w.write().out, w.verify().out, w.tsv(), readFileSync(join(w.data, SUMMARY), 'utf8')];
    return [...outs.flatMap(o => (clean(o) && !o.includes('heldout-src') ? [] : ['a held-out name or folder leaked'])), ...(JSON.parse(outs[3]).heldout.entries === 1 ? [] : ['the summary does not count the held-out entry'])];
  }],
  ['a symbolic link is recorded as link with the hash of its target text and is not followed', w => {
    w.write();
    const lines = w.tsv().split('\n');
    const link = lines.find(l => l.endsWith(`\t${SERIES}/link`));
    const dirlink = lines.find(l => l.endsWith('\tdirlink'));
    return [
      ...(link === `${sha256('run-map.json')}\tlink\t${SERIES}/link` ? [] : [`link line is ${link}`]),
      ...(dirlink === `${sha256(SERIES)}\tlink\tdirlink` ? [] : [`dirlink line is ${dirlink}`]),
      ...(lines.some(l => l.includes('\tdirlink/')) ? ['a link to a folder was followed'] : []),
    ];
  }],
  ['the lists are in byte order, not locale order', w => {
    w.write();
    const paths = w.tsv().split('\n').filter(Boolean).map(l => l.split('\t')[2]);
    const at = p => paths.indexOf(p);
    return [...(at('docs/B-file') < at('docs/a-file') ? [] : ['B-file does not come before a-file']), ...(at('docs/z～') < at('docs/z\u{1F600}') ? [] : ['a BMP name does not come before an astral one'])];
  }],
  ['one changed byte of a public file exits 1 and names it', w => {
    w.write(); w.put('docs/a-file', 'x\n');
    const r = w.verify();
    return r.code === 1 && r.out.includes('changed docs/a-file') ? [] : [`exit ${r.code}: ${r.out}`];
  }],
  ['a removed public file exits 1 and is named missing', w => {
    w.write(); rmSync(join(w.runs, 'notes.txt'));
    const r = w.verify();
    return r.code === 1 && r.out.includes('missing notes.txt') ? [] : [`exit ${r.code}: ${r.out}`];
  }],
  ['an added public file exits 1 and is named extra', w => {
    w.write(); w.put('docs/new-file', 'n\n');
    const r = w.verify();
    return r.code === 1 && r.out.includes('extra docs/new-file') ? [] : [`exit ${r.code}: ${r.out}`];
  }],
  ['a changed link target exits 1 and names the link', w => {
    w.write(); rmSync(join(w.runs, 'dirlink')); symlinkSync('docs', join(w.runs, 'dirlink'));
    const r = w.verify();
    return r.code === 1 && r.out.includes('changed dirlink') ? [] : [`exit ${r.code}: ${r.out}`];
  }],
  ['a changed held-out file exits 1 and the output names no held-out path', w => {
    w.write(); w.put(`heldout-src/${SECRET}.py`, 'print(2)\n');
    const r = w.verify();
    return [...(r.code === 1 && r.out.includes('heldout:') ? [] : [`exit ${r.code}: ${r.out}`]), ...(clean(r.out) ? [] : ['the held-out name leaked'])];
  }],
  ['with the private list beside the manifest a held-out difference prints as counts only', w => {
    const priv = join(w.root, 'private', 'heldout.tsv');
    w.write('--private', priv);
    const dir = join(w.root, 'manifest');
    mkdirSync(dir);
    for (const f of [TSV, SUMMARY]) writeFileSync(join(dir, f), readFileSync(join(w.data, f)));
    writeFileSync(join(dir, HELDOUT_TSV), readFileSync(priv));
    const same = w.verify('--manifest', dir);
    w.put(`heldout-src/${SECRET}.py`, 'print(2)\n'); w.put(`heldout-src/${SECRET}-two.py`, 'two\n');
    const r = w.verify('--manifest', dir);
    return [
      ...(same.code === 0 ? [] : [`untouched private list: exit ${same.code}: ${same.out}`]),
      ...(r.code === 1 && r.out.includes('heldout: 0 missing, 1 changed, 1 extra') ? [] : [`exit ${r.code}: ${r.out}`]),
      ...(clean(r.out) ? [] : ['the held-out name leaked']),
    ];
  }],
  ['--private outside the repository writes the private list and the summary hashes it', w => {
    const priv = join(w.root, 'private', 'heldout.tsv');
    const r = w.write('--private', priv);
    const bytes = existsSync(priv) ? readFileSync(priv) : Buffer.alloc(0);
    const summary = JSON.parse(readFileSync(join(w.data, SUMMARY), 'utf8'));
    return [...(r.code === 0 && clean(r.out) ? [] : [`exit ${r.code}: ${r.out}`]), ...(bytes.toString('utf8').includes(`heldout-src/${SECRET}.py`) && sha256(bytes) === summary.heldout.sha256 ? [] : ['the private list is not the one the summary hashes'])];
  }],
  ['--private inside the repository exits 2 and writes nothing', w => {
    const inside = join(REPO, `.manifest-self-check-${process.pid}.tsv`);
    try {
      const r = w.write('--private', inside);
      return [...(r.code === 2 ? [] : [`exit ${r.code}: ${r.out}`]), ...(existsSync(inside) || existsSync(join(w.data, TSV)) ? ['something was written'] : [])];
    } finally { rmSync(inside, { force: true }); }
  }],
  ['a tab in a name fails the write, names the entry by index and writes nothing', w => {
    w.put(`heldout-src/${SECRET}\tx.py`, 'x\n');
    const r = w.write();
    return [...(r.code === 1 && /entry \d+/.test(r.out) ? [] : [`exit ${r.code}: ${r.out}`]), ...(clean(r.out) ? [] : ['the name leaked']), ...(existsSync(join(w.data, TSV)) ? ['a list was written'] : [])];
  }],
  ['a name that is not valid UTF-8 fails the write and writes nothing', w => {
    writeFileSync(Buffer.concat([Buffer.from(`${w.runs}/docs/`), Buffer.from([0x62, 0x61, 0x64, 0xff])]), 'x\n');
    const r = w.write();
    return [...(r.code === 1 && /entry \d+: its path is not valid UTF-8/.test(r.out) ? [] : [`exit ${r.code}: ${r.out}`]), ...(existsSync(join(w.data, TSV)) ? ['a list was written'] : [])];
  }],
  ['a FIFO is skipped and counted, and write then verify gives 0', w => {
    const made = [join(w.runs, 'docs', 'a-pipe'), join(w.runs, 'heldout-src', `${SECRET}.fifo`)].map(f => spawnSync('mkfifo', [f]).status);
    const r = w.write(); const v = w.verify();
    const summary = JSON.parse(readFileSync(join(w.data, SUMMARY), 'utf8'));
    const lines = w.tsv();
    return [
      ...(made.every(c => c === 0) ? [] : ['mkfifo failed']),
      ...(r.code === 0 && v.code === 0 && clean(r.out + v.out) ? [] : [`write ${r.code}: ${r.out} verify ${v.code}: ${v.out}`]),
      ...(summary.skipped?.fifo === 2 && summary.skipped?.socket === 0 ? [] : [`skipped is ${JSON.stringify(summary.skipped)}`]),
      ...(summary.topLevel.docs?.skipped?.fifo === 1 ? [] : [`docs skipped is ${JSON.stringify(summary.topLevel.docs?.skipped)}`]),
      ...(lines.includes('a-pipe') || lines.includes(`${SECRET}.fifo`) ? ['a FIFO was recorded'] : []),
    ];
  }],
  ['verify gives 0 when a skipped FIFO is removed after write', w => {
    const fifo = join(w.runs, 'docs', 'a-pipe');
    spawnSync('mkfifo', [fifo]);
    w.write(); rmSync(fifo);
    const v = w.verify();
    return v.code === 0 ? [] : [`verify ${v.code}: ${v.out}`];
  }],
  ['a device or any other non-regular type still fails the run, by index', () => {
    let message = '';
    try { checked({ rel: Buffer.from(`heldout-src/${SECRET}`), kind: 'character device' }, 3); } catch (e) { message = e instanceof Fail ? e.message : 'not a Fail'; }
    return /^entry 3: it is a character device/.test(message) && clean(message) ? [] : [`message was ${message}`];
  }],
  ['a data file whose source hash differs fails both modes and names the series and the file', w => {
    w.write(); w.putSeries({ 'run-map.json': sha256('another') });
    const v = w.verify();
    rmSync(join(w.data, TSV)); rmSync(join(w.data, SUMMARY));
    const k = w.write();
    return [v, k].flatMap((r, i) => (r.code === 1 && r.out.includes(SERIES) && r.out.includes('run-map.json') ? [] : [`${i === 0 ? 'verify' : 'write'} exit ${r.code}: ${r.out}`]))
      .concat(existsSync(join(w.data, TSV)) ? ['write published although the cross-check failed'] : []);
  }],
  ['a data folder with no s-*.json gets a note and no crash', w => {
    rmSync(join(w.data, `${SERIES}.json`));
    const r = w.write(); const v = w.verify();
    return r.code === 0 && v.code === 0 && /no s-\*\.json/.test(v.out) ? [] : [`write ${r.code}: ${r.out} verify ${v.code}: ${v.out}`];
  }],
  ['a summary of another generation than the list exits 1', w => {
    w.write(); writeFileSync(join(w.data, TSV), `${w.tsv()}${'0'.repeat(64)}\t1\tzzz\n`);
    const r = w.verify();
    return r.code === 1 && r.out.includes('mixed generation') ? [] : [`exit ${r.code}: ${r.out}`];
  }],
  ['a --manifest folder that differs from the data folder exits 1, an equal one exits 0', w => {
    w.write();
    const same = join(w.root, 'same'); const other = join(w.root, 'other');
    for (const d of [same, other]) { mkdirSync(d); for (const f of [TSV, SUMMARY]) writeFileSync(join(d, f), readFileSync(join(w.data, f))); }
    writeFileSync(join(other, SUMMARY), `${readFileSync(join(w.data, SUMMARY), 'utf8')} `);
    const a = w.verify('--manifest', same); const b = w.verify('--manifest', other);
    return [...(a.code === 0 ? [] : [`equal folder: exit ${a.code}: ${a.out}`]), ...(b.code === 1 && b.out.includes('differs from') ? [] : [`other folder: exit ${b.code}: ${b.out}`])];
  }],
  ['a summary whose counts or folder hash were edited exits 1 and the message names the field', w => {
    w.write();
    const summary = JSON.parse(readFileSync(join(w.data, SUMMARY), 'utf8'));
    const docs = summary.topLevel.docs;
    writeFileSync(join(w.data, SUMMARY), jsonText({ ...summary, public: { ...summary.public, entries: summary.public.entries + 1 }, topLevel: { ...summary.topLevel, docs: { ...docs, sha256: '0'.repeat(64) } } }));
    const r = w.verify();
    return [
      ...(r.code === 1 && r.out.includes('public.entries differs') ? [] : [`exit ${r.code}: ${r.out}`]),
      ...(r.out.includes('topLevel["docs"].sha256 differs') ? [] : ['the message does not name the folder and the field']),
      ...(r.out.includes('topLevel["notes.txt"]') || !clean(r.out) ? ['an unrelated folder or a held-out name was named'] : []),
    ];
  }],
  ['verify checks the manifest files before it scans the runs root', w => {
    const empty = join(w.root, 'empty');
    mkdirSync(empty);
    const gone = join(w.root, 'no-such-runs');
    const a = w.cli('verify', '--runs', gone, '--data', w.data, '--manifest', empty);
    const unreadable = join(w.root, 'unreadable');
    mkdirSync(join(unreadable, TSV), { recursive: true });
    writeFileSync(join(unreadable, SUMMARY), '{}');
    const b = w.cli('verify', '--runs', gone, '--data', w.data, '--manifest', unreadable);
    return [
      ...(a.code === 1 && a.out.includes(`${TSV} is missing in ${empty}`) && !a.out.includes('does not exist') ? [] : [`empty folder: exit ${a.code}: ${a.out}`]),
      ...(b.code === 1 && b.out.includes(`${TSV} in ${unreadable} cannot be read`) && !b.out.includes('does not exist') ? [] : [`unreadable file: exit ${b.code}: ${b.out}`]),
    ];
  }],
  ['a failed public publish keeps the old private list and leaves no temp file', w => {
    const priv = join(w.root, 'private', 'heldout.tsv');
    mkdirSync(dirname(priv));
    writeFileSync(priv, 'OLD\n');
    mkdirSync(join(w.data, TSV, 'in-the-way'), { recursive: true });
    const a = w.write('--private', priv);
    const leftovers = d => readdirSync(d).filter(f => f.endsWith('.tmp'));
    const asRoot = process.getuid?.() === 0;
    rmSync(join(w.data, TSV), { recursive: true });
    chmodSync(w.data, 0o500);
    const b = asRoot ? { code: 1 } : w.write('--private', priv);
    chmodSync(w.data, 0o700);
    return [
      ...(a.code !== 0 ? [] : ['the write succeeded although the list could not be replaced']),
      ...(b.code !== 0 ? [] : ['the write succeeded in a read-only data folder']),
      ...(readFileSync(priv, 'utf8') === 'OLD\n' ? [] : ['the old private list was replaced']),
      ...(leftovers(w.data).length + leftovers(dirname(priv)).length === 0 ? [] : ['a temp file was left']),
    ];
  }],
  ['a publish whose pair does not match changes no file and leaves no temp file', w => {
    w.write();
    const before = [TSV, SUMMARY].map(f => readFileSync(join(w.data, f)));
    const bad = { runsRoot: '~', public: { entries: 0, bytes: 0 }, topLevel: {}, publicSha256: '0'.repeat(64), heldout: { entries: 0, sha256: '' }, tool: TOOL };
    let threw = false;
    try { publish({ data: w.data, tsvText: 'x\n', summary: bad, privatePath: null, privateText: '' }); } catch (e) { threw = e instanceof Fail; }
    const after = [TSV, SUMMARY].map(f => readFileSync(join(w.data, f)));
    return [...(threw ? [] : ['no Fail was thrown']), ...(before.every((b, i) => b.equals(after[i])) ? [] : ['a target changed']), ...(readdirSync(w.data).some(f => f.endsWith('.tmp')) ? ['a temp file was left'] : [])];
  }],
  ['a private list that does not hash to the summary exits 1 without a name', w => {
    const priv = join(w.root, 'private', 'heldout.tsv');
    w.write('--private', priv);
    const dir = join(w.root, 'manifest');
    mkdirSync(dir);
    for (const f of [TSV, SUMMARY]) writeFileSync(join(dir, f), readFileSync(join(w.data, f)));
    writeFileSync(join(dir, HELDOUT_TSV), `${readFileSync(priv, 'utf8')}${'0'.repeat(64)}\t1\theldout-src/${SECRET}-other.py\n`);
    const r = w.verify('--manifest', dir);
    return [...(r.code === 1 && r.out.includes('does not hash to the summary') ? [] : [`exit ${r.code}: ${r.out}`]), ...(clean(r.out) ? [] : ['the held-out name leaked'])];
  }],
];

/** Exit status of the self-check: 0 when every case gets the answer it names. */
export function selfCheck() {
  const base = mkdtempSync(join(tmpdir(), `testsToDelete-u${process.getuid?.() ?? 0}-manifest-selfcheck-`));
  try {
    const failures = CASES.flatMap(([name, run], i) => {
      let found;
      try { found = run(world(base, `case${i}`)); } catch (e) { found = [`threw ${describe(e)}`]; }
      return found.map(f => `FAILED ${name}: ${f}`);
    });
    failures.forEach(f => console.log(f));
    console.log(failures.length ? `self-check FAILED: ${failures.length} problem(s) in ${CASES.length} cases` : 'self-check ok');
    return failures.length ? 1 : 0;
  } finally {
    rmSync(base, { recursive: true, force: true });
  }
}

async function main() {
  try {
    const a = args(process.argv.slice(2));
    if (a.selfCheck) process.exit(selfCheck());
    const { lines, code } = await (a.mode === 'verify' ? verifyMode(a) : writeMode(a));
    lines.forEach(l => console.log(l));
    process.exit(code);
  } catch (e) {
    console.error(`manifest: ${describe(e)}`);
    process.exit(e instanceof Usage ? 2 : 1);
  }
}

if (process.argv[1] && resolve(process.argv[1]) === SELF) main();
