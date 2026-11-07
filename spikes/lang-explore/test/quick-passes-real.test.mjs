// The ten passes through the REAL box and the real pass child (section 2.6): the child runs under `taskset -c <profile>`, the layer starts the program in its delegated scope and box, and the program reads its own `Cpus_allowed_list`.
// The programs are invented toys: a daemon that answers wrongly only when its mask is the 2-CPU profile, and one that answers right everywhere, laid on the REAL daemon templates: the C# program is built by the template's own `build.sh` in the real build box, the Node program starts as the template's `run.sh` starts it (nothing to build); everything from the pass
// child on is real. (No fake home here: the pinned toolchains are found under the real one.) Behind `needs` (bwrap, the user bus, the pinned toolchains): skipped with the reason, a failure under BENCH_REQUIRE_SANDBOX=1.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { holdForCaseWall } from '../../lang-bench/lib/exec-layer/prefix.mjs';
import { needs, toolchains } from '../../lang-bench/test/support/exec-layer-host.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { normalizeCpuList, probeMask } from '../lib/affinity.mjs';
import { dirBytes } from '../lib/measure.mjs';
import { boxBuild } from '../lib/build-quick.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { memoryLimit } from '../lib/memory-limit.mjs';
import { quickSnapshots } from '../lib/quick.mjs';
import { runPassChild } from '../lib/pass-run.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { profileOfPass } from '../lib/verdict.mjs';
import { echoScenario, echoTranscript, layDaemonTree } from './quick-real-programs.mjs';

const IDS = ['s01', 'h001', 'h002'];
const WORDS = { s01: 'hi', h001: 'ho', h002: 'hu' };

/** The stack's snapshots and directories for the real build box (the registered caches, the pinned toolchains). */
function buildSeam(stack) {
  if (stack !== 'csharp') return async () => ok({ ok: true, exit: 0, timedOut: false, wallMs: 1, tail: '' }); // the Node template starts src/main.ts as it is; nothing is built
  const snapshots = quickSnapshots({ base: loadExplore().value, home: os.homedir() });
  assert.equal(snapshots.ok, true, snapshots.reason);
  const build = boxBuild({ owned: ownedFor({ token: 'bench' }), dirs: toolchains, snapshots: snapshots.value, stack });
  return ({ tree }) => build({ tree, timeoutMs: 600_000 });
}

function sessionFor({ wrongOn26, stack }) {
  const facts = { builtDll: false, built: false };
  const seriesDir = tmpDir('quick-real-series');
  const lock = acquireSeriesLock({ seriesDir });
  const work = path.join(seriesDir, 's', 'abc123', 'score');
  mkdirSync(work, { recursive: true });
  const limit = memoryLimit().value;
  const limits = { memoryBytes: limit.bytes, buildTimeoutMs: 600_000, timeMs: 300_000 };
  const spec = { caseWallMs: 60_000, holdSeconds: holdForCaseWall(60_000), runCeilingMs: 300_000 };
  const context = { stack, kind: 'daemon', dirs: toolchains };
  const a = {
    work, seriesDir, limits, spec, traps: [], growth: { loaded: () => ok({}) }, seriesState: () => ({ launching: [] }), affinity: { probe: cpus => probeMask({ cpus }) },
    prepare: async dest => { layDaemonTree({ stack, dest }); return ok({ files: {}, frozen: [], drift: [], left: [] }); },
    cases: ({ dir }) => { mkdirSync(dir, { recursive: true }); for (const id of IDS) { writeFileSync(path.join(dir, `${id}.in`), echoScenario(WORDS[id], { wrongOn26 })); writeFileSync(path.join(dir, `${id}.out`), echoTranscript(WORDS[id])); } return ok({ dir, ids: IDS }); },
    build: async arg => { const r = await buildSeam(stack)(arg); facts.builtDll = existsSync(path.join(arg.tree, 'bin', 'Release', 'net10.0', 'App.dll')); facts.built = r.ok && r.value.ok; return r; },
    runPass: s => runPassChild({ spec: s, context }),
  };
  return { a, work, release: () => lock.value.release(), limit, facts };
}

const attemptFiles = work => readdirSync(path.join(work, 'passes')).sort();
const readAttempt = (work, name) => JSON.parse(readFileSync(path.join(work, 'passes', name), 'utf8'));

test('A_toy_program_that_fails_only_when_its_Cpus_allowed_list_is_6_7_reads_intermittently_incorrect_with_exactly_five_failing_passes_so_the_profile_reaches_the_program', { timeout: 900_000 }, async t => {
  if (!needs(t)) return;
  const s = sessionFor({ wrongOn26: true, stack: 'node' });
  const r = await scorePasses(s.a);
  s.release();
  assert.equal(r.kind, 'scored', r.reason);
  assert.equal(r.verdict.verdict, 'intermittently incorrect');
  const files = attemptFiles(s.work);
  assert.equal(files.length, 10, 'ten files: thirty attempts never happened');
  const failing = files.filter(f => readAttempt(s.work, f).record.cases.some(c => c.status !== 'PASS')).map(f => Number(f.slice(1, 3)));
  assert.deepEqual(failing, [1, 3, 5, 7, 9], 'the failing passes are exactly the 2-CPU ones');
  for (const f of files) {
    const body = readAttempt(s.work, f);
    assert.equal(body.profile, f.includes('2cpu') ? '2cpu' : '8cpu');
    assert.equal(normalizeCpuList(body.observedCpus), normalizeCpuList(body.cpus), `${f}: the mask the child observed is the profile`);
    assert.equal(normalizeCpuList(body.cpus), profileOfPass(body.n).cpus);
  }
});

for (const stack of ['node', 'csharp']) {
  test(`A_toy_program_that_passes_everywhere_reads_accepted_10_of_10_under_both_profiles_at_the_registered_limit_in_${stack}`, { timeout: 900_000 }, async t => {
    if (!needs(t)) return;
    const s = sessionFor({ wrongOn26: false, stack });
    const started = Date.now();
    const r = await scorePasses(s.a);
    s.release();
    assert.equal(r.kind, 'scored', r.reason);
    assert.equal(r.verdict.verdict, 'accepted');
    if (stack === 'csharp') assert.deepEqual([s.facts.built, s.facts.builtDll], [true, true], 'the C# program was built by the template in the build box: App.dll exists');
    assert.equal(r.score.explore.passes, 10);
    assert.ok(r.score.explore.cases.every(c => c.memoryMaxBytes === s.limit.bytes), 'every case reads back the one registered limit');
    t.diagnostic(`${stack}: scoring time ${Date.now() - started} ms, retained ${dirBytes(s.work)} bytes (printed, not asserted)`);
  });
}
