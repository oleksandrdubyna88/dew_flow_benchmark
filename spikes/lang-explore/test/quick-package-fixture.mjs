// The world of the package tool's tests (F2.S1): an INVENTED quick task in a temp home, with an invented spec, invented held-out cases, an invented manifest (trap table and witnesses), invented stack references and witness overlays,
// fake template folders and a fake world whose pass seams answer by a plan. Nothing real is read: no private folder, no reference, no key. Every invented case carries `TOKEN`, so a test can scan a record or a line for it (D41).
// Not a test file.
import { createHash } from 'node:crypto';
import { cpSync, mkdirSync, readFileSync, readdirSync, realpathSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { refsDirOf } from '../lib/heldout-quick.mjs';
import { taskPaths } from '../lib/tasks.mjs';
import { generateKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { mountFor } from './quick-heldout-fixture.mjs';
import { runnerRecord } from './quick-passes-fixture.mjs';
import { rec } from './quick-score-fixture.mjs';
import { EXPLORE, quickHome, quickTask } from './quick-leak-fixture.mjs';

export const LIMITS = Object.freeze({ memoryBytes: 512 * 1024 * 1024, buildTimeoutMs: 600_000, timeMs: 300_000 });
export const SPEC = Object.freeze({ caseWallMs: 660_000, holdSeconds: 720, runCeilingMs: 7_200_000 });

/** A fake clock that stands still unless `advance(ms)` moves it (the fake pass child does, so a pass's wall is exact). */
export function fakeClock(start = '2026-11-07T10:00:00.000Z') {
  let t = Date.parse(start);
  return { now: () => new Date(t), advance: ms => { t += ms; } };
}

export const TOKEN = 'invented-package-QZ9';
export const sha = bytes => createHash('sha256').update(bytes).digest('hex');
export const IDS = Object.freeze(['h001', 'h002', 'h003', 'h004']);
/** The spec's section 6 as the pass reads it (FORMAT 3.4): one `### sNN` sub-section per visible sample, each with its two headed blocks. */
const sampleSection = ids => ids.map(id => `### ${id}\n\n\`samples/${id}.in\`\n\n\`\`\`\nscenario 2\ninvented-in-${id}\n\`\`\`\n\n\`samples/${id}.out\`\n\n\`\`\`\ntranscript 2\ninvented-out-${id}\n\`\`\`\n`).join('\n');
export const SAMPLE_IDS = Object.freeze(['s01', 's02']);
export const SPEC_TEXT = `# Task 05 — Invented\n\n| | |\n|---|---|\n| Gate verdict | \`proceed\` · 2026-11-09 |\n\n## 2. Requirements\n\n${[1, 2, 3, 4, 5, 6, 7].map(n => `**R${n}.** An invented requirement number ${n}.`).join('\n\n')}\n\n## 6. Samples\n\n${sampleSection(SAMPLE_IDS)}`;
export const TRAPS = Object.freeze({ 'trap-alpha': { stack: 'both', cases: ['h001', 'h002'] }, 'trap-beta': { stack: 'node', cases: ['h003'] } });
export const WITNESSES = Object.freeze({
  'MXX-alpha-c': { trap: 'trap-alpha', stack: 'csharp', kind: 'deterministic' },
  'MXX-alpha-n': { trap: 'trap-alpha', stack: 'node', kind: 'deterministic' },
  'MXX-beta-n': { trap: 'trap-beta', stack: 'node', kind: 'race' },
});

const put = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(file, text); };
const caseOf = id => ({ id, input: `scenario 2\n${TOKEN}-in-${id}\n`, output: `transcript 2\n${TOKEN}-out-${id}\n` });

/** The fake template folders (`<stack>/daemon/`): the scripts and pin files are frozen, `App.csproj` and `package.json` are the agent's. -> the root. */
export function fakeTemplates() {
  const root = tmpDir('pkg-templates');
  for (const [rel, text] of Object.entries({ 'csharp/daemon/build.sh': '#!/bin/sh\n', 'csharp/daemon/run.sh': '#!/bin/sh\n', 'csharp/daemon/App.csproj': '<Project/>\n', 'csharp/daemon/global.json': '{}\n', 'node/daemon/build.sh': '#!/bin/sh\n', 'node/daemon/run.sh': '#!/bin/sh\n', 'node/daemon/package.json': '{}\n', 'node/daemon/tsconfig.json': '{}\n' })) put(path.join(root, rel), text);
  return root;
}

/**
 * The private side of invented task `taskId` in a fresh temp home: cases, `cases.txt`, `traps.txt` (the held-out ids of the table; a sample id is never in it), the manifest, the two references, a witness overlay per witness id, the twins of the load cases when given.
 * `over` replaces `{ manifest, refs, overlays, traps, witnesses, load }`. -> `{ home, task, dir, casesDir, refsDir, mutantsDir, manifestFile, material }`.
 */
export function writePackage({ taskId = '05', home = quickHome(), traps = TRAPS, witnesses = WITNESSES, load, over = {} } = {}) {
  const task = { ...quickTask(taskId), spec: { ...quickTask(taskId).spec, sha256: sha(SPEC_TEXT) } };
  const dir = taskPaths(task, home).heldoutDir;
  const casesDir = path.join(dir, 'cases');
  const cases = IDS.map(caseOf);
  for (const c of cases) { put(path.join(casesDir, `${c.id}.in`), c.input); put(path.join(casesDir, `${c.id}.out`), c.output); }
  put(path.join(casesDir, 'cases.txt'), `${cases.map(c => `${c.id} bytes R1 R2`).join('\n')}\n`);
  put(path.join(dir, 'traps.txt'), `${[...new Set(Object.values(traps).flatMap(t => t.cases).filter(id => id.startsWith('h')))].sort().join('\n')}\n`);
  const manifest = {
    caseCount: cases.length, cases: Object.fromEntries(cases.map(c => [c.id, { in: sha(c.input), out: sha(c.output) }])), traps, witnesses, ...(load ? { load } : {}), ...over.manifest,
  };
  const manifestFile = path.join(dir, 'manifest.json');
  put(manifestFile, `${JSON.stringify(manifest, null, 2)}\n`);
  const refsDir = refsDirOf(dir);
  put(path.join(refsDir, 'csharp', 'Program.cs'), over.csharp ?? `// ${TOKEN} the C# reference, a line long enough to be a needle\n`);
  put(path.join(refsDir, 'node', 'src', 'main.ts'), over.node ?? `// ${TOKEN} the Node reference, a line long enough to be a needle\n`);
  const mutantsDir = path.join(dir, 'mutants');
  for (const [id, w] of Object.entries(witnesses)) put(path.join(mutantsDir, id, w.stack === 'csharp' ? 'Program.cs' : path.join('src', 'main.ts')), `// WITNESS:${id}\n`);
  for (const id of load?.cases ?? []) { put(path.join(dir, 'twins', `${id}.in`), `scenario 2\n${TOKEN}-twin-${id}\n`); put(path.join(dir, 'twins', `${id}.out`), `transcript 2\n${TOKEN}-twin-out-${id}\n`); }
  put(taskPaths(task, home).notesFile, 'invented private notes\n');
  return { home, task, dir, casesDir, refsDir, mutantsDir, manifestFile, manifest };
}

/** The world a package run is given: `sources.spec`, `passSeams` over a plan, and the fake templates. `plan({ stack, id, n, spec })` -> 'clean' | 'env' | `{ failing: { id: verdict word } }` | `{ hang: [ids] }`, where `id` is the witness id read from the tree ('reference' when it is none). */
export function fakeWorld({ plan = () => 'clean', buildOk = () => true, buildFail = () => null, specText = SPEC_TEXT, observed = spec => spec.cpus, templates = fakeTemplates(), memoryMax = LIMITS.memoryBytes, log = [], delayMs = 0 } = {}) {
  const clock = fakeClock();
  return {
    templates, clock, log, limits: LIMITS,
    sources: { spec: specText, format: 'invented format' },
    passSeams: ({ stack, source, limits, sets, traps, seriesDir }) => {
      const owner = path.basename(source);
      const idOf = tree => { const marker = readdirSync(tree, { recursive: true }).map(f => String(f)).find(f => /\.(cs|ts)$/.test(f)); return /WITNESS:(\S+)/.exec(marker ? readFileSync(path.join(tree, marker), 'utf8') : '')?.[1] ?? 'reference'; };
      return {
        prepare: async dest => { cpSync(source, dest, { recursive: true }); return ok({ files: {}, frozen: [], drift: [], left: [] }); },
        cases: ({ dir }) => { mkdirSync(dir, { recursive: true }); const ids = readdirSync(sets[0].dir).filter(f => f.endsWith('.in')).map(f => f.slice(0, -3)).sort(); for (const id of ids) { cpSync(path.join(sets[0].dir, `${id}.in`), path.join(dir, `${id}.in`)); } return ok({ dir, ids }); },
        build: async ({ tree }) => (buildFail({ stack, id: idOf(tree) }) === null ? ok({ ok: buildOk({ stack, id: idOf(tree) }), exit: 0, timedOut: false, wallMs: 50, tail: '' }) : fail(buildFail({ stack, id: idOf(tree) }), { kind: 'environment' })),
        runPass: async spec => {
          log.push({ stack, owner, id: idOf(spec.tree), n: spec.n, cpus: spec.cpus, profile: spec.profile, tree: spec.tree });
          if (delayMs > 0) await new Promise(r => { setTimeout(r, delayMs); });
          clock.advance(2000);
          return bodyOf(plan({ stack, id: idOf(spec.tree), n: spec.n, spec }), spec, { observed: observed(spec), memoryMax });
        },
        affinity: { probe: async cpus => ok(cpus) }, seriesState: () => ({ launching: [] }), growth: { loaded: () => ok({}) }, limits, traps, say: () => {}, spec: SPEC, seriesDir,
      };
    },
  };
}

const REASON = Object.freeze({ 'wrong answer': 'entry 3 of 4 differs', 'memory limit': 'memory limit: oom_kill 1' });

/** The attempt body for a plan entry (see `fakeWorld`): the runner's record over `IDS` with the failing cases and their read-back limit. */
export function bodyOf(entry, spec, { observed, memoryMax }) {
  if (entry === 'env') return { kind: 'environment', reason: 'the box did not start', observedCpus: null };
  if (entry.env !== undefined) return { kind: 'environment', reason: entry.env, observedCpus: null };
  const failing = entry === 'clean' ? {} : (entry.failing ?? {});
  const hang = entry === 'clean' ? [] : (entry.hang ?? []);
  const limit = entry === 'clean' ? memoryMax : (entry.memoryMax ?? memoryMax);
  const wallOf = id => (entry === 'clean' ? 321 : (entry.wall?.[id] ?? 321));
  const withLimit = c => ({ ...c, wallMs: wallOf(c.id), lifecycle: { ...c.lifecycle, memory: { ...c.lifecycle.memory, maxBytes: limit } } });
  const judged = id => (hang.includes(id) ? rec(id, 'FAIL', 'timeout: the case ran past its test bound') : rec(id, failing[id] ? 'FAIL' : 'PASS', failing[id] ? REASON[failing[id]] : '', entry === 'clean' ? undefined : entry.peak?.[id]));
  const cases = spec.caseIds.map(id => withLimit(judged(id)));
  return { kind: 'completed', record: runnerRecord(spec.caseIds, { cases }), runner: runnerRecord().runner, memoryCertified: entry === 'clean' || entry.uncertified !== true, observedCpus: observed };
}

/** A series folder with the series lock held by this process (the folder a package run scores under). -> `{ seriesDir, release }`. */
export function heldSeries() {
  const seriesDir = tmpDir('pkg-series');
  const lock = acquireSeriesLock({ seriesDir });
  return { seriesDir, release: () => lock.value.release() };
}

/** Every file under `dir` with its bytes, by relative path. */
export function filesOf(dir) {
  return Object.fromEntries(readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => { const p = path.join(e.parentPath, e.name); return [path.relative(dir, p), readFileSync(p, 'utf8')]; }));
}

export const sizeOf = file => statSync(file).size;


/** The seams of a seal in a temp world: an invented key, a backup folder inside an invented share, `mountOf` that knows the share. -> the `io.heldout` object. */
export function sealSeams({ frozenFile, packagesFile, hooks = {} }) {
  const keyFile = path.join(tmpDir('pkg-key'), 'heldout.key');
  generateKey(keyFile);
  const shareMount = realpathSync(tmpDir('pkg-share'));
  const backupDir = path.join(shareMount, 'backup');
  mkdirSync(backupDir, { mode: 0o700 });
  return { keyFile, backupFile: path.join(backupDir, 'heldout.key.bak'), shareMount, mountOf: mountFor(shareMount), retry: { tries: 3, sleepMs: 1 }, hooks, now: () => new Date('2026-11-09T09:00:00.000Z'), frozenFile, packagesFile };
}

/**
 * Everything a command test needs: an invented package, the fake world, the pins of the invented task, and `io` for `main` (`say` collects `lines`). `over` replaces entries of `io`, `over.package` entries of `io.package`.
 * -> `{ p, world, io, lines, files }`.
 */
export function commandEnv({ plan, buildOk, buildFail, over = {}, built, ...pkgOptions } = {}) {
  const p = built ?? writePackage(pkgOptions);
  const world = fakeWorld({ plan, buildOk, buildFail });
  const lines = [];
  const files = { packagesFile: path.join(tmpDir('pkg-registry'), 'packages.json'), frozenFile: path.join(tmpDir('pkg-frozen'), 'heldout-frozen.json'), memoryFile: path.join(tmpDir('pkg-memory'), 'memory-limit.json') };
  const gateRecord = path.join(tmpDir('pkg-gate'), 'spec-05.md');
  writeFileSync(gateRecord, 'verdict proceed, 2 reviewers\n');
  const { package: packageOver, ...ioOver } = over;
  const io = {
    home: p.home, say: l => lines.push(l), worldFn: async () => ok(world), quietFn: async () => ok({ quiet: true }), exploreFn: () => ok({ ...EXPLORE, tasks: { ...EXPLORE.tasks, [p.task.id]: p.task } }), frozenFile: files.frozenFile,
    now: world.clock.now, package: { parent: tmpDir('pkg-parent'), pinsFn: () => ({ runnerSha256: 'a'.repeat(64), execLayerSha256: 'b'.repeat(64) }), packagesFile: files.packagesFile, conformance: { exit: 0, firstLine: '' }, gateRecordFile: gateRecord, memoryFile: files.memoryFile, ...packageOver }, ...ioOver,
  };
  return { p, world, io, lines, files };
}
