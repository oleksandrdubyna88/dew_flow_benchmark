// The ten passes through the REAL box and the real pass child (section 2.6): the child runs under `taskset -c <profile>`, the layer starts the program in its delegated scope and box, and the program reads its own `Cpus_allowed_list`.
// The programs are invented toys (python3 is in the box): a daemon that answers wrongly only when its mask is the 2-CPU profile, and one that answers right everywhere. The build is a fake (the toy needs none); everything from the pass
// child on is real. (No fake home here: the pinned toolchains are found under the real one.) Behind `needs` (bwrap, the user bus, the pinned toolchains): skipped with the reason, a failure under BENCH_REQUIRE_SANDBOX=1.
import assert from 'node:assert/strict';
import { chmodSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { holdForCaseWall } from '../../lang-bench/lib/exec-layer/prefix.mjs';
import { needs, toolchains } from '../../lang-bench/test/support/exec-layer-host.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { probeMask } from '../lib/affinity.mjs';
import { dirBytes } from '../lib/measure.mjs';
import { memoryLimit } from '../lib/memory-limit.mjs';
import { runPassChild } from '../lib/pass-run.mjs';
import { scorePasses } from '../lib/passes.mjs';

const SCENARIO = word => `scenario 2
default within 2s
start ready port within 10s
connect c tcp
send c "${word}"
recv c bytes 2
signal TERM
wait-exit within 5s
`;
const TRANSCRIPT = 'transcript 2\nready\nrecv c "ok"\nexit 0\n';
const IDS = [['s01', 'hi'], ['h001', 'ho'], ['h002', 'hu']];

/** A python daemon that answers "ok", or (when `wrongOn26`) "bad" when its own affinity list is exactly CPUs 6 and 7. */
const daemon = wrongOn26 => `#!/bin/bash
exec python3 -I -u -c '
import os, re, signal, socket
def cpus():
    text = open("/proc/self/status").read()
    listed = re.search(r"^Cpus_allowed_list:\\s*(\\S+)", text, re.M).group(1)
    found = set()
    for part in listed.split(","):
        lo, _, hi = part.partition("-")
        found.update(range(int(lo), int(hi or lo) + 1))
    return found
srv = socket.socket(); srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
srv.bind(("127.0.0.1", int(os.environ["PORT"]))); srv.listen(5)
signal.signal(signal.SIGTERM, lambda *a: os._exit(0))
while True:
    c, _ = srv.accept()
    c.recv(100)
    c.sendall(b"bad" if ${wrongOn26 ? 'cpus() == {6, 7}' : 'False'} else b"ok")
'
`;

function sessionFor({ wrongOn26, stack }) {
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
    prepare: async dest => { mkdirSync(dest, { recursive: true }); for (const [name, mode] of [['build.sh', '#!/bin/bash\nexit 0\n'], ['run.sh', daemon(wrongOn26)]]) { writeFileSync(path.join(dest, name), mode); chmodSync(path.join(dest, name), 0o755); } return ok({ frozen: [], drift: [], left: [] }); },
    cases: ({ dir }) => { mkdirSync(dir, { recursive: true }); for (const [id, word] of IDS) { writeFileSync(path.join(dir, `${id}.in`), SCENARIO(word)); writeFileSync(path.join(dir, `${id}.out`), TRANSCRIPT); } return ok({ dir, ids: IDS.map(([id]) => id) }); },
    build: async () => ok({ ok: true, exit: 0, timedOut: false, wallMs: 1, tail: '' }),
    runPass: s => runPassChild({ spec: s, context }),
  };
  return { a, work, release: () => lock.value.release(), limit };
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
    assert.equal(body.observedCpus.replaceAll(',', ',').length > 0, true, 'every attempt names the affinity its child observed');
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
    assert.equal(r.score.explore.passes, 10);
    assert.ok(r.score.explore.cases.every(c => c.memoryMaxBytes === s.limit.bytes), 'every case reads back the one registered limit');
    t.diagnostic(`${stack}: scoring time ${Date.now() - started} ms, retained ${dirBytes(s.work)} bytes (printed, not asserted)`);
  });
}
