#!/usr/bin/env node
// Replays the contamination scan over every recorded session stream with the CURRENT lib (read-only evidence for the results record).
//
//   node research/minigit/tools/replay.mjs [--runs <dir>] [--home <dir>] [--out <file>]
//   node research/minigit/tools/replay.mjs --self-check
//
// --runs defaults to $MINIGIT_RUNS or ~/rsd/minigit; every directory `s-*` under it is a series. --home is the operator's home as the
// scan sees it (a protected root; default: the real home). --out defaults to research/minigit/data/replay.json.
//
// For each session of a series' run-map that has a `stream.ndjson` it rebuilds the context exactly as spikes/minigit-bench/lib/bare-collect.mjs
// does (`contaminationPart`): `allow`, `skeleton`, `boxDir` and `maps` from the session's OWN `argv.json` through `boxView`, the layout's
// work/home/harness/net paths, `arm: 'bare'`, and the calls of the stream through `callsOf` (so the per-agent shell directory is tracked by
// the lib, not here). It then runs `contaminationHits` and counts the high and the low hits.
//
// What it does NOT do: it reads no token and no secret (`ownSecrets` is `[]`; the token reader is never imported), it opens only
// `run-map.json`, `argv.json` and `stream.ndjson` of each session, never `score/` (held-out cases), `~/.config`, the broker or the egress
// logs, and it writes nothing under the runs root. Partner directories (the other sessions of the same pair, phase and attempt, the ones
// the runner passed while the phase ran) are rebuilt from the run-map, since a rebuilt run keeps no list of them.
// `--self-check` runs the same code over a synthetic runs root in a fake home (one clean session, one that reaches into the fake home) and
// exits 1 unless it finds 0 and 1 high hits; it opens nothing of the real home.
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, readlinkSync, rmSync, writeFileSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readAgentFileUnder } from '../../../spikes/minigit-bench/lib/agent-files.mjs';
import { contaminationHits } from '../../../spikes/minigit-bench/lib/contamination.mjs';
import { boxView, layoutPaths } from '../../../spikes/minigit-bench/lib/sandbox.mjs';
import { callsOf } from '../../../spikes/minigit-bench/lib/stream-calls.mjs';
import { tidyPath } from './session.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const LIB = resolve(HERE, '..', '..', '..', 'spikes', 'minigit-bench', 'lib');
const OUT = resolve(HERE, '..', 'data', 'replay.json');
/** The lib files the replay's answer depends on, named in the output with a short hash so a later reader knows which lib produced it. */
const LIB_FILES = ['agent-files.mjs', 'egress-hosts.mjs', 'link-class.mjs', 'sandbox.mjs', 'stream-calls.mjs', 'token-scan.mjs'];

const isLink = p => { try { return lstatSync(p).isSymbolicLink(); } catch { return false; } };
const readLink = p => { try { return readlinkSync(p); } catch { return null; } };
const sha12 = bytes => createHash('sha256').update(bytes).digest('hex').slice(0, 12);

function libIdentity() {
  const names = [...LIB_FILES, ...readdirSync(LIB).filter(f => f.startsWith('contamination') && f.endsWith('.mjs'))].sort();
  return Object.fromEntries(names.map(n => [n, sha12(readFileSync(join(LIB, n)))]));
}

function args(argv) {
  const out = { runs: process.env.MINIGIT_RUNS || join(homedir(), 'rsd', 'minigit'), home: homedir(), out: OUT, selfCheck: false };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--runs') out.runs = argv[++i];
    else if (argv[i] === '--home') out.home = argv[++i];
    else if (argv[i] === '--out') out.out = resolve(argv[++i]);
    else if (argv[i] === '--self-check') out.selfCheck = true;
    else throw new Error(`unknown argument ${argv[i]}; legal: --runs <dir>, --home <dir>, --out <file>, --self-check`);
  }
  return out;
}

/** The text of a file of a session directory, or null when it is absent; a link, a pipe or an oversize file is an error (never followed). */
function textOf(dir, rel) {
  const r = readAgentFileUnder(dir, rel);
  if (r === null) return null;
  if (r.unreadable) throw new Error(`${join(dir, rel)} was not read: ${r.unreadable}`);
  return r.toString('utf8');
}

/** The stream's events; a line that is not JSON is counted, not guessed at. */
function eventsOf(text) {
  const events = [];
  let unparsed = 0;
  for (const line of text.split('\n')) {
    if (line.trim() === '') continue;
    try { events.push(JSON.parse(line)); } catch { unparsed += 1; }
  }
  return { events, unparsed };
}

/** A credential shape is masked in what the output keeps, whatever the lib already did: no token of any kind leaves this tool. */
const maskShapes = text => text.replace(/sk-ant-[A-Za-z0-9_-]+/g, 'sk-ant-***');

/** The other sessions the runner had in hand while this one ran: same pair, phase and attempt (and a rehearsal's twin). */
const partnersOf = (entries, e, sessionsDir) => entries.filter(x => x.rid !== e.rid && x.pair === e.pair && x.phase === e.phase && x.attempt === e.attempt).map(x => join(sessionsDir, x.rid));

/** One session -> its replay row, or `{ skipped }` when it has no stream. */
export function replaySession({ seriesDir, entry, entries, home }) {
  const sessionsDir = join(seriesDir, 's');
  const layout = layoutPaths(sessionsDir, entry.rid);
  const stream = textOf(layout.dir, 'stream.ndjson');
  const ids = { series: seriesDir.split('/').pop(), rid: entry.rid, kind: entry.kind, stack: entry.stack, phase: entry.phase, attempt: entry.attempt, pair: entry.pair, recordedClass: entry.class ?? null };
  if (stream === null) return { skipped: { ...ids, why: 'no stream.ndjson' } };
  const argvText = textOf(layout.dir, 'argv.json');
  const argv = argvText === null ? null : JSON.parse(argvText);
  const view = boxView({ argv: argv?.bwrap ?? [], layout, home }); // a session with no readable argv.json allows nothing, as in the collection
  const ctx = { rid: layout.rid, treeRoot: layout.work, home, boxHome: layout.home, harnessPaths: [layout.harness, layout.net], partnerPaths: partnersOf(entries, entry, sessionsDir), arm: 'bare', ownSecrets: [], allow: view.allow, skeleton: view.skeleton, boxDir: view.boxDir, maps: view.maps, isLink, readLink };
  const { events, unparsed } = eventsOf(stream);
  const calls = callsOf(events);
  const hits = contaminationHits({ calls, ctx });
  const high = hits.filter(h => h.severity === 'high');
  return {
    ...ids, argvRead: argv !== null, calls: calls.length, unparsedLines: unparsed,
    high: high.length, low: hits.length - high.length,
    highHits: high.map(h => ({ rule: h.rule, where: h.where, excerpt: tidyPath(maskShapes(h.excerpt ?? ''), home) })),
  };
}

/** The entries of a series' run-map, with only the fields the replay needs (the entry also holds a dummy token and canary values: never copied). */
function entriesOf(seriesDir) {
  const map = JSON.parse(readFileSync(join(seriesDir, 'run-map.json'), 'utf8'));
  return Object.values(map.sessions).map(e => ({ rid: e.rid, pair: e.pair, stack: e.stack, phase: e.phase, attempt: e.attempt, kind: e.kind, class: e.class }));
}

export function replayRuns(runs, home) {
  const series = readdirSync(runs).filter(d => /^s-/.test(d) && existsSync(join(runs, d, 'run-map.json'))).sort();
  const rows = [];
  const skipped = [];
  for (const id of series) {
    const seriesDir = join(runs, id);
    const entries = entriesOf(seriesDir);
    for (const entry of entries.slice().sort((a, b) => a.rid.localeCompare(b.rid))) {
      const r = replaySession({ seriesDir, entry, entries, home });
      if (r.skipped) skipped.push(r.skipped); else rows.push(r);
    }
  }
  return { series, rows, skipped };
}

const count = (xs, f) => xs.filter(f).length;
const sum = (xs, f) => xs.reduce((a, x) => a + f(x), 0);

/** Totals per series, the sessions with a high hit, and the sessions the run-map recorded as `void: contaminated`. A rehearsal's model is a stub; every other kind is a real model session. */
export function summarise({ series, rows, skipped }) {
  const real = r => r.kind !== 'rehearsal';
  return {
    perSeries: series.map(id => {
      const own = rows.filter(r => r.series === id);
      return { series: id, sessions: own.length, realModel: count(own, real), withHigh: count(own, r => r.high > 0), realWithHigh: count(own, r => real(r) && r.high > 0), withLow: count(own, r => r.low > 0), high: sum(own, r => r.high), low: sum(own, r => r.low), calls: sum(own, r => r.calls) };
    }),
    sessionsReplayed: rows.length, sessionsSkipped: skipped.length,
    realModelSessions: count(rows, real), realModelWithHigh: count(rows, r => real(r) && r.high > 0),
    recordedVoids: rows.filter(r => r.recordedClass === 'void: contaminated').map(r => ({ series: r.series, rid: r.rid, stack: r.stack, phase: r.phase, recordedClass: r.recordedClass, high: r.high, low: r.low })),
    notCompleted: rows.filter(r => real(r) && r.recordedClass !== 'completed' && r.recordedClass !== 'void: contaminated').map(r => ({ series: r.series, rid: r.rid, recordedClass: r.recordedClass })),
  };
}

export function build(runs, home) {
  const result = replayRuns(runs, home);
  return {
    extractedFrom: { runsRoot: tidyPath(runs, home), tool: 'research/minigit/tools/replay.mjs', lib: libIdentity(), reads: ['run-map.json', 'argv.json', 'stream.ndjson'], secretsRead: false },
    summary: summarise(result), skipped: result.skipped, sessions: result.rows,
  };
}

/** What the tool prints: one line per series, then the voids. */
export function printable(data) {
  const s = data.summary;
  return [
    ...s.perSeries.map(p => `${p.series}: ${p.sessions} sessions (${p.realModel} real model), high ${p.high} in ${p.withHigh} session(s), low ${p.low} in ${p.withLow} session(s), ${p.calls} calls`),
    `total: ${s.sessionsReplayed} replayed, ${s.sessionsSkipped} without a stream; real model sessions with a high hit: ${s.realModelWithHigh} of ${s.realModelSessions}`,
    ...s.recordedVoids.map(v => `recorded void: ${v.series} ${v.rid} (${v.stack} ${v.phase}) -> high ${v.high}, low ${v.low}`),
    ...(s.notCompleted.length ? [`real model sessions not recorded as completed: ${s.notCompleted.map(n => `${n.series} ${n.rid} (${n.recordedClass})`).join(', ')}`] : []),
  ].join('\n');
}

const toolCall = (id, command) => JSON.stringify({ type: 'assistant', message: { content: [{ type: 'tool_use', id, name: 'Bash', input: { command } }] } });

/** A synthetic runs root under a fake home: a clean session and one that reaches a file of the home outside its box. -> the fake home. */
function writeFixture(base) {
  const home = join(base, 'home');
  const runs = join(home, 'rsd', 'minigit');
  const seriesDir = join(runs, 's-selfcheck');
  const sessions = { aaaa01: { stack: 'csharp', command: 'ls .' }, bbbb02: { stack: 'node', command: `cat ${join(home, 'rsd', 'elsewhere', 'file.txt')}` } };
  mkdirSync(seriesDir, { recursive: true });
  writeFileSync(join(seriesDir, 'run-map.json'), JSON.stringify({ seriesId: 's-selfcheck', sessions: Object.fromEntries(Object.entries(sessions).map(([rid, s]) => [rid, { rid, pair: 0, stack: s.stack, phase: 'v1', attempt: 1, kind: 'data', class: 'completed' }])) }));
  for (const [rid, s] of Object.entries(sessions)) {
    const layout = layoutPaths(join(seriesDir, 's'), rid);
    mkdirSync(layout.work, { recursive: true });
    mkdirSync(layout.home, { recursive: true });
    writeFileSync(layout.files.argv, JSON.stringify({ bwrap: ['--bind', layout.work, layout.work, '--bind', layout.home, layout.home] }));
    writeFileSync(layout.files.stream, `${toolCall('t1', s.command)}\n`);
  }
  return home;
}

/** Exit status of the self-check: 0 when the clean session gives no high hit and the reaching one gives exactly one `outside-path`. */
export function selfCheck() {
  const base = mkdtempSync(join(tmpdir(), 'testsToDelete-replay-selfcheck-'));
  try {
    const home = writeFixture(base);
    const data = build(join(home, 'rsd', 'minigit'), home);
    const by = rid => data.sessions.find(r => r.rid === rid);
    const ok = by('aaaa01')?.high === 0 && by('bbbb02')?.high === 1 && by('bbbb02').highHits[0].rule === 'outside-path' && data.skipped.length === 0
      && !JSON.stringify(data).includes(base);
    console.log(`replay self-check: ${ok ? 'ok' : 'FAILED'} (clean session high ${by('aaaa01')?.high}, reaching session high ${by('bbbb02')?.high})`);
    return ok ? 0 : 1;
  } finally {
    rmSync(base, { recursive: true, force: true });
  }
}

function main() {
  const a = args(process.argv.slice(2));
  if (a.selfCheck) process.exit(selfCheck());
  const data = build(a.runs, a.home);
  mkdirSync(dirname(a.out), { recursive: true });
  writeFileSync(a.out, `${JSON.stringify(data, null, 1)}\n`);
  console.log(`${printable(data)}\n-> ${a.out}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
