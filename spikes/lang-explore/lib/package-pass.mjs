// The passes of a package run, from the seams of the quick world (`pass-seams.mjs`): what wraps a pass and the one pass a deterministic witness and a twin get (F2.S1, P5 and P3). The pass driver (`passes.mjs`) is called, never
// copied, for the ten passes; the ONE pass is the same steps of the same launchers: `commitBuild` (the build through `buildAndRestore`, in the stack's box), then `runPass` (`runPassChild`: the pass child under `taskset`, which kills
// its whole process tree at its bound; the runner kills each case's cgroup). No spawn path is written here.
//
// Progress: a pass prints a line when it starts, a heartbeat every 60 s while it runs and a line when it ends, with the ordinal, the pass, the profile, the wall, the outcome and the COUNT of failing cases: never a case id, a trap or
// a path (the count of cases ended so far is not observable from outside the pass child, so the heartbeat says the wall only). A pass whose enforced memory limit, read back by the runner, is not the registered value is a HALT with the
// two numbers, never a verdict.
import path from 'node:path';
import { affinityProblem } from './affinity.mjs';
import { commitBuild } from './build-commit.mjs';
import { casesOfRecord } from './score-quick.mjs';
import { PROFILES } from './verdict.mjs';

export const HEARTBEAT_MS = 60_000;

const recordCases = body => body.record?.cases ?? [];
const failingCount = body => recordCases(body).filter(c => c.status !== 'PASS').length;
const outcomeOf = body => (body.halt ? 'halted' : (body.kind === 'completed' ? (failingCount(body) === 0 ? 'clean' : 'failed') : 'environment'));
const seconds = ms => Math.round(ms / 1000);

/** The cases of a completed body whose read-back limit is not `memoryBytes`: `[bytes read, or null]`, empty when every case is enforced under the registered value. */
const readBackOf = (body, memoryBytes) => [...new Set(recordCases(body).map(c => c.lifecycle?.memory?.maxBytes ?? null))].filter(v => v !== memoryBytes);

/** The halt body of a completed pass whose limit is not the registered one (the two numbers), or the body itself. */
function guarded(body, memoryBytes) {
  const wrong = body.kind === 'completed' && memoryBytes !== undefined ? readBackOf(body, memoryBytes) : [];
  return wrong.length === 0 ? body : { halt: 'memory-limit-not-registered', kind: 'environment', reason: `the memory limit enforced on a case was read back as ${wrong[0] ?? 'nothing'} bytes and inputs/memory-limit.json registers ${memoryBytes} bytes: the pass is an environment attempt, never a verdict` };
}

/** `runPass` with the start line, the 60 s heartbeat, the end line and the memory read-back guard. `label` is the ordinal (`witness 3`, `reference csharp`); the lines hold counts and the wall only. */
export function wrapPass({ runPass, label, say, memoryBytes, heartbeatMs = HEARTBEAT_MS, clock = Date.now }) {
  return async spec => {
    const t0 = clock();
    const tag = `${label} pass ${spec.n} (${spec.profile})`;
    say(`${tag} started`);
    const timer = setInterval(() => say(`${tag} still running after ${seconds(clock() - t0)} s`), heartbeatMs);
    timer.unref?.();
    try {
      const body = guarded(await runPass(spec), memoryBytes);
      say(`${tag} ended after ${seconds(clock() - t0)} s: ${outcomeOf(body)}, ${failingCount(body)} failing case(s)`);
      return body;
    } finally {
      clearInterval(timer);
    }
  };
}

const environment = (reason, kind = 'halt') => ({ kind, reason });

/** The answer of a body that is no verdict (a halt, an environment attempt), or null. */
const kindAnswer = body => (body.halt ? environment(body.reason) : (body.kind === 'completed' ? null : environment(body.reason, 'environment')));

/** The sentence of a completed body that cannot be judged: a mask that is not the profile, or a memory claim that is not certified; or null. */
const bodyProblem = (body, spec) => affinityProblem({ wanted: spec.cpus, observed: body.observedCpus }) ?? (body.memoryCertified === true ? null : 'the memory claim of the pass is not certified: a pass whose peaks cannot be read is no verdict');

/** The pass that has been run, judged: a halt or an environment body is no verdict; a completed body is its cases. */
function judgedBody({ body, spec }) {
  const early = kindAnswer(body);
  if (early !== null) return early;
  const problem = bodyProblem(body, spec);
  if (problem !== null) return environment(problem);
  const cases = casesOfRecord({ record: body.record, expected: spec.caseIds });
  return cases.ok ? { kind: 'completed', cases: cases.value, observedCpus: body.observedCpus } : environment(`the record of the pass is not a judgement of the cases: ${cases.reason}`);
}

/** The pass spec of the driver's shape (`passes.mjs` `specOf`) for one pass of `profile` over the committed tree and the case folder. */
const specOf = ({ seams, work, folder, profile }) => ({
  n: 1, attempt: 1, profile: profile.name, cpus: profile.cpus, tree: path.join(work, 'tree'), samplesDir: folder.dir, caseIds: folder.ids,
  caseWallMs: seams.spec.caseWallMs, holdSeconds: seams.spec.holdSeconds, runCeilingMs: seams.spec.runCeilingMs, limits: seams.limits,
});

/** The build commit and the case folder of a pass: `{ folder }`, or `{ answer }` (a build that never began, one that failed, a case folder that cannot be made). */
async function builtAndCases({ seams, work, runnerPin, now }) {
  const built = await commitBuild({ work, prepare: seams.prepare, build: seams.build, runnerPin, now });
  if (!built.ok) return { answer: environment(built.reason) };
  if (built.value.ok === false) return { answer: { kind: 'build-failed', tail: built.value.tail } };
  const folder = seams.cases({ dir: path.join(work, 'cases') });
  return folder.ok ? { folder: folder.value } : { answer: environment(folder.reason) };
}

/**
 * ONE pass of the seams on `profile` (the 2-CPU one by default): the build commit, then the pass child. -> `{ kind: 'completed', cases, ms }` | `{ kind: 'build-failed', tail }` (the build ran and exited non-zero) | `{ kind: 'halt', reason }`
 * (nothing was judged: a build that never began, a mask that was not applied, a read-back limit that is not the registered one) | `{ kind: 'environment', reason }`. `ran` is the pass child's answer wrapper (`wrapPass`).
 */
export async function onePass({ seams, ran = seams.runPass, work, profile = PROFILES[0], runnerPin = null, now = () => new Date() }) {
  const prep = await builtAndCases({ seams, work, runnerPin, now });
  if (prep.answer !== undefined) return prep.answer;
  const spec = specOf({ seams, work, folder: prep.folder, profile });
  const started = now();
  const body = await ran(spec).catch(e => ({ kind: 'environment', reason: `the pass child stopped on an unexpected ${e?.name ?? 'error'}: ${e?.message ?? e}` }));
  return { ...judgedBody({ body, spec }), ms: now() - started };
}
