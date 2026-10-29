#!/usr/bin/env node
// The held-out suite's operator tool. It knows the suite's SHAPE (registry, runner protocol, sealed archive), never its cases.
//
//   node spikes/minigit-bench/heldout-tool.mjs keygen    [--key-file P]
//   node spikes/minigit-bench/heldout-tool.mjs seal      --src <clear dir> [--key-file P] [--out-dir D] [--hashes F]
//   node spikes/minigit-bench/heldout-tool.mjs calibrate [--src <clear dir>] [--key-file P] [--out-dir D] [--work-root D] [--inputs-repo U]
//   node spikes/minigit-bench/heldout-tool.mjs leakcheck [--key-file P] [--out-dir D] [--repo D] [--range A..B] [--work-root D]
//   node spikes/minigit-bench/heldout-tool.mjs counts    [--key-file P] [--out-dir D] [--work-root D]
//   node spikes/minigit-bench/heldout-tool.mjs sweep     [--work-root D] [--max-age-hours N]
//
// `calibrate --src` is the authoring mode (the clear sources, nothing written); without `--src` it decrypts the sealed archive and, on
// success, writes calibration.json. `--direct` (tests only) runs the runner without the scoring pipeline. `--key-file` exists for tests
// only: the key is a file, never an environment variable. Exit codes: 0 ok | 1 a check failed (a mismatch, a leak, a refusal) |
// 3 environment (no key, no suite, no bwrap, inputs unreachable) | 4 usage | 5 the tool itself failed.
import { randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { removeUnder } from '../policy-ab-quick/lib/safe-rm.mjs';
import { calibrate, directScorer, readRegistry } from './lib/heldout-calibrate.mjs';
import { defaultKeyPath, generateKey, readKey, unseal } from './lib/heldout-crypto.mjs';
import { leakcheck } from './lib/heldout-leak.mjs';
import { pipelineScorer } from './lib/heldout-pipeline.mjs';
import { assertWorkRoot, defaultWorkRoot, makeHeldoutDep, sweepLeftovers, withClearSuite } from './lib/heldout-run.mjs';
import { HELDOUT_DIR, countsOf, readSealedFiles, sealSuite, sha256Hex } from './lib/heldout-seal.mjs';
import { fetchInputs } from './lib/fetch-inputs.mjs';
import { readFileOrNull } from './lib/inputs.mjs';
import { runBounded } from './lib/run.mjs';

const OPTIONS = {
  'key-file': { type: 'string' }, src: { type: 'string' }, 'out-dir': { type: 'string' }, hashes: { type: 'string' }, 'work-root': { type: 'string' },
  repo: { type: 'string' }, range: { type: 'string' }, direct: { type: 'boolean' }, 'max-age-hours': { type: 'string' }, 'inputs-repo': { type: 'string' },
};
const EXIT = Object.freeze({ ok: 0, check: 1, env: 3, usage: 4, broke: 5 });
const BWRAP_PATH = '/usr/bin/bwrap';

function parseCli(argv) {
  try {
    const { values, positionals } = parseArgs({ args: argv, options: OPTIONS, allowPositionals: true });
    return positionals.length === 1 ? { ok: true, command: positionals[0], o: values } : { ok: false, reason: 'exactly one command is expected' };
  } catch (e) {
    if (e.code?.startsWith('ERR_PARSE_ARGS')) return { ok: false, reason: e.message };
    throw e;
  }
}

const settings = o => ({
  keyFile: o['key-file'] ?? defaultKeyPath(),
  outDir: path.resolve(o['out-dir'] ?? HELDOUT_DIR),
  workRoot: o['work-root'] ?? defaultWorkRoot(),
});

async function repoRoot(o) {
  if (o.repo) return ok0(path.resolve(o.repo));
  const r = await runBounded('git', ['rev-parse', '--show-toplevel'], { cwd: path.dirname(fileURLToPath(import.meta.url)), timeoutMs: 20_000 });
  return r.ok && r.value.code === 0 ? ok0(r.value.stdout.trim()) : { ok: false, reason: 'could not find this repository with git rev-parse --show-toplevel' };
}
const ok0 = value => ({ ok: true, value });

function cmdKeygen(o, write) {
  const made = generateKey(settings(o).keyFile);
  write(made.ok ? `key written: ${made.value} (mode 600; keep a copy outside this machine on purpose, it cannot be regenerated)` : made.reason);
  return made.ok ? EXIT.ok : EXIT.check;
}

function cmdSeal(o, write) {
  if (!o.src) { write('usage error: seal needs --src <clear dir>'); return EXIT.usage; }
  const s = settings(o);
  const sealed = sealSuite({ src: path.resolve(o.src), keyFile: s.keyFile, outDir: s.outDir, ...(o.hashes ? { hashesFile: path.resolve(o.hashes) } : {}) });
  if (!sealed.ok) { write(`seal refused: ${sealed.reason}`); return sealed.env ? EXIT.env : EXIT.check; }
  const m = sealed.value;
  write(`sealed: N1 = ${m.n1}, N2 = ${m.n2}, M = ${m.m}, plaintextSha256 ${m.plaintextSha256}`);
  return EXIT.ok;
}

async function cmdCounts(o, write) {
  const s = settings(o);
  const files = readSealedFiles(s.outDir);
  if (!files.ok) { write(`counts: ${files.reason}`); return EXIT.check; }
  const m = files.value.manifest;
  write(`manifest: N1 = ${m.n1}, N2 = ${m.n2}, M = ${m.m}, plaintextSha256 ${m.plaintextSha256}`);
  const read = await withClearSuite({ suiteEnc: path.join(s.outDir, 'suite.enc'), keyFile: s.keyFile, workRoot: s.workRoot, rid: 'counts' },
    dir => readRegistry(dir));
  if (!read.ok) { write(`counts not cross-checked against the archive: ${read.reason}`); return EXIT.env; }
  if (!read.value.ok) { write(`counts: ${read.value.reason}`); return EXIT.check; }
  const real = countsOf(read.value.value);
  const same = real.n1 === m.n1 && real.n2 === m.n2 && real.m === m.m;
  write(same ? 'the archive agrees with the manifest' : `the archive holds N1 = ${real.n1}, N2 = ${real.n2}, M = ${real.m}: the manifest is stale`);
  return same ? EXIT.ok : EXIT.check;
}

async function cmdLeakcheck(o, write) {
  const s = settings(o);
  const root = await repoRoot(o);
  if (!root.ok) { write(root.reason); return EXIT.env; }
  const ran = await withClearSuite({ suiteEnc: path.join(s.outDir, 'suite.enc'), keyFile: s.keyFile, workRoot: s.workRoot, rid: 'leak' },
    dir => leakcheck({ repoRoot: root.value, tokensText: readFileOrNull(path.join(dir, 'leak-tokens.txt'))?.toString('utf8') ?? '',
      shinglesText: readFileOrNull(path.join(dir, 'leak-shingles.txt'))?.toString('utf8') ?? '', ...(o.range ? { range: o.range } : {}) }));
  if (!ran.ok) { write(`leakcheck could not decrypt the suite: ${ran.reason}`); return EXIT.env; }
  write(ran.value.ok ? `leakcheck clean: ${ran.value.scanned} files of ${root.value} and ${ran.value.history.join(' + ') || 'no history'} scanned` : `leakcheck FAILED: ${ran.value.reason}`);
  return ran.value.ok ? EXIT.ok : EXIT.check;
}

function cmdSweep(o, write) {
  const hours = Number(o['max-age-hours'] ?? 24);
  if (!Number.isFinite(hours) || hours <= 0) { write('usage error: --max-age-hours must be a positive number'); return EXIT.usage; }
  const s = settings(o);
  const swept = sweepLeftovers({ workRoot: s.workRoot, olderThanMs: hours * 3600 * 1000 });
  write(`sweep: removed ${swept.removed.length}, kept ${swept.kept.length}, failed ${swept.failed.length}${swept.failed.map(f => ` [${f.name}: ${f.reason}]`).join('')}`);
  return swept.failed.length === 0 ? EXIT.ok : EXIT.check;
}

/** Scores the subjects with the real pipeline (build, visible scripts, held-out, upstream) or, for tests, with the bare runner. */
async function makeScorer(o, { suiteDir, base, dep }) {
  if (o.direct) return ok0({ scoreSubject: directScorer({ suiteDir, workRoot: path.join(base, 'direct'), timeoutS: 10 }), requireVisible: false });
  if (!existsSync(BWRAP_PATH)) return { ok: false, reason: `${BWRAP_PATH} is missing: the scoring box cannot run here` };
  const fetched = await fetchInputs({ repoUrl: o['inputs-repo'] });
  if (!fetched.ok) return { ok: false, reason: fetched.reason };
  return ok0({ scoreSubject: pipelineScorer({ inputs: fetched.value, heldout: dep, sessionRoot: path.join(base, 'sessions') }), requireVisible: true });
}

const reportLines = (r, write) => {
  write(`calibration ${r.ok ? 'OK' : 'FAILED'}`);
  r.problems.forEach(p => write(`  ${p}`));
};

/** Runs calibrate over `suiteDir` inside a scratch directory under the work root (home), removed at the end. */
async function calibrateIn({ o, suiteDir, dep, workRoot }) {
  const root = assertWorkRoot(workRoot);
  if (!root.ok) return root;
  const scratchBase = path.join(root.value, 'score-tmp');
  const base = path.join(scratchBase, `calibrate-${randomBytes(4).toString('hex')}`);
  mkdirSync(base, { recursive: true, mode: 0o700 });
  try {
    const scorer = await makeScorer(o, { suiteDir, base, dep });
    if (!scorer.ok) return scorer;
    const trees = path.join(base, 'trees');
    mkdirSync(trees, { mode: 0o700 });
    return ok0(await calibrate({ suiteDir, workRoot: trees, ...scorer.value }));
  } finally {
    removeUnder(base, scratchBase);
  }
}

async function cmdCalibrate(o, write) {
  const s = settings(o);
  if (o.src) {
    const clear = path.resolve(o.src);
    const done = await calibrateIn({ o, suiteDir: clear, dep: makeHeldoutDep({ clearDir: clear }), workRoot: s.workRoot });
    if (!done.ok) { write(`calibrate: ${done.reason}`); return EXIT.env; }
    reportLines(done.value, write);
    return done.value.ok ? EXIT.ok : EXIT.check;
  }
  return calibrateSealed(o, s, write);
}

async function calibrateSealed(o, s, write) {
  const files = readSealedFiles(s.outDir);
  if (!files.ok) { write(`calibrate: ${files.reason}`); return EXIT.env; }
  const key = readKey(s.keyFile);
  if (!key.ok) { write(`calibrate: ${key.reason}`); return EXIT.env; }
  const plain = unseal(files.value.sealed, key.value);
  if (!plain.ok) { write(`calibrate: ${plain.reason}`); return EXIT.env; }
  const suiteEnc = path.join(s.outDir, 'suite.enc');
  const dep = makeHeldoutDep({ suiteEnc, keyFile: s.keyFile, workRoot: s.workRoot });
  const ran = await withClearSuite({ suiteEnc, keyFile: s.keyFile, workRoot: s.workRoot, rid: 'calibrate-src' },
    suiteDir => calibrateIn({ o, suiteDir, dep, workRoot: s.workRoot }));
  if (!ran.ok) { write(`calibrate: ${ran.reason}`); return EXIT.env; }
  if (!ran.value.ok) { write(`calibrate: ${ran.value.reason}`); return EXIT.env; }
  const result = ran.value.value;
  const m = files.value.manifest;
  const stale = sha256Hex(plain.value) !== m.plaintextSha256 ? ['the sealed archive does not hash to manifest.plaintextSha256'] : [];
  const count = result.calibration ? result.calibration : null;
  const countDrift = count && (count.n1 !== m.n1 || count.n2 !== m.n2 || count.m !== m.m) ? ['the registry counts differ from manifest.json'] : [];
  const problems = [...result.problems, ...stale, ...countDrift];
  reportLines({ ok: problems.length === 0, problems }, write);
  if (problems.length > 0) return EXIT.check;
  const doc = { ...result.calibration, plaintextSha256: m.plaintextSha256, ok: true };
  writeFileSync(path.join(s.outDir, 'calibration.json'), `${JSON.stringify(doc, null, 2)}\n`);
  write(`calibration.json written: N1 = ${doc.n1}, N2 = ${doc.n2}, M = ${doc.m}`);
  return EXIT.ok;
}

const COMMANDS = Object.freeze({ keygen: cmdKeygen, seal: cmdSeal, calibrate: cmdCalibrate, leakcheck: cmdLeakcheck, counts: cmdCounts, sweep: cmdSweep });

/** The whole tool; `write` receives each output line. Resolves to the exit code. */
export async function main(argv, write = line => process.stdout.write(`${line}\n`)) {
  const cli = parseCli(argv);
  if (!cli.ok) { write(`usage error: ${cli.reason}`); return EXIT.usage; }
  const run = COMMANDS[cli.command];
  if (!run) { write(`usage error: unknown command ${JSON.stringify(cli.command)}; one of ${Object.keys(COMMANDS).join(', ')}`); return EXIT.usage; }
  try {
    return await run(cli.o, write);
  } catch (e) {
    write(`heldout-tool failed: ${e.message}`);
    return EXIT.broke;
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = await main(process.argv.slice(2));
}
