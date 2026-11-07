// The verdict rule of a quick-run session (parent D71): ten passes, each with at most three attempts, in; one of four states out. Pure, no I/O.
//
//   accepted                  the build passed, all ten passes completed, every case passed in all ten
//   rejected                  the build failed; or a completed pass is failing and no completed pass is clean
//   intermittently incorrect  a completed pass is failing and a completed pass is clean
//   unscored: environment     no completed pass is failing and some pass has no completed attempt after its three attempts
//
// THE ONE TERMINAL RULE: if any completed pass holds a genuine functional failure the session is `rejected` (or `intermittently incorrect` when some pass is clean), whatever any other pass did and with or without
// retries; it is never `unscored: environment`. While no completed pass is failing and a pass still has an attempt left the answer is `pending`, the driver's working state and never a recorded verdict.
//
// An attempt is `{ attempt, kind: 'completed', cases }` or `{ attempt, kind: 'environment', reason }`. An environment attempt holds no case verdict at all (a half-written record is no evidence of anything), so
// one that carries `cases` is a wrong fixture and throws. The answer has NO finality field: whether the session is final is the run-map's to say (scoring exhaustion is not session finality).

export const PROFILES = Object.freeze([Object.freeze({ name: '2cpu', cpus: '6-7' }), Object.freeze({ name: '8cpu', cpus: '0-7' })]);
export const PASS_COUNT = 10;
export const ATTEMPTS_PER_PASS = 3;
export const VERDICTS = Object.freeze(['accepted', 'rejected', 'intermittently incorrect', 'unscored: environment']);
/** The words of a case verdict: the one list the runner's record is mapped to (`score-quick.mjs`) and the verdict rule reads. */
export const CASE_VERDICT = Object.freeze({ pass: 'pass', wrongAnswer: 'wrong answer', memoryLimit: 'memory limit', buildFailed: 'build failed' });

/** The profile of pass `n` (1 to 10): the odd passes are on the 2-CPU profile, the even ones on the 8-CPU profile, so a change of the host halfway cannot be told from a profile. */
export const profileOfPass = n => PROFILES[n % 2 === 1 ? 0 : 1];

/** Every case of a completed pass passed. */
export const isClean = cases => cases.every(c => c.verdict === CASE_VERDICT.pass);

const SHAPE_CHECKS = Object.freeze([
  [a => ['completed', 'environment'].includes(a?.kind), 'an attempt needs a kind, completed or environment'],
  [a => a.kind !== 'environment' || !('cases' in a), 'an environment attempt holds no case verdict'],
  [a => a.kind !== 'completed' || Array.isArray(a.cases), 'a completed attempt needs its cases'],
]);

function checkAttempt(attempt) {
  const bad = SHAPE_CHECKS.find(([holds]) => !holds(attempt));
  if (bad) throw new Error(`verdict: ${bad[1]} (got ${JSON.stringify(attempt)?.slice(0, 120)})`);
}

const byAttempt = (a, b) => a.attempt - b.attempt;
const completedOf = pass => [...pass.attempts].sort(byAttempt).find(a => a.kind === 'completed');

/** `'clean' | 'failing' | 'open' | 'exhausted'`: a completed attempt decides clean or failing; without one, the attempts used decide open or exhausted. */
export function passState(pass) {
  pass.attempts.forEach(checkAttempt);
  const done = completedOf(pass);
  if (done) return isClean(done.cases) ? 'clean' : 'failing';
  return pass.attempts.length >= ATTEMPTS_PER_PASS ? 'exhausted' : 'open';
}

const COUNT_KEYS = Object.freeze(['clean', 'failing', 'open', 'exhausted']);
const countOf = states => Object.freeze(Object.fromEntries(COUNT_KEYS.map(key => [key, states.filter(s => s === key).length])));
const NO_COUNTS = Object.freeze({ clean: 0, failing: 0, open: 0, exhausted: 0 });

/** Ten passes, numbered: a pass nobody has tried yet is an open pass with no attempt. */
function tenPasses(passes) {
  const byN = new Map(passes.map(p => [p.n, p]));
  return Array.from({ length: PASS_COUNT }, (_, i) => byN.get(i + 1) ?? { n: i + 1, attempts: [] });
}

/** The rules in order: `[when(counts), verdict, scoringState, the sentence]`. The first that holds answers. */
const RULES = Object.freeze([
  [c => c.failing > 0 && c.clean > 0, 'intermittently incorrect', 'decided', c => `${c.failing} completed pass(es) failing and ${c.clean} clean`],
  [c => c.failing > 0, 'rejected', 'decided', c => `${c.failing} completed pass(es) failing and no clean pass`],
  [c => c.clean === PASS_COUNT, 'accepted', 'decided', () => `all ${PASS_COUNT} passes completed and clean`],
  [c => c.open > 0, 'pending', 'open', c => `${c.open} pass(es) still have an attempt left and none is failing`],
  [() => true, 'unscored: environment', 'exhausted', c => `${c.exhausted} pass(es) used every attempt without a completed run and no completed pass is failing`],
]);

const answer = (verdict, scoringState, counts, reason) => Object.freeze({ verdict, scoringState, counts, reason });

/** The build step's own answer: exhausted (every try of a build that never began is used) or a build that really exited non-zero. */
const buildAnswer = build => {
  if (build.exhausted === true) return answer('unscored: environment', 'exhausted', NO_COUNTS, 'the build never began after its tries');
  return build.ok === false ? answer('rejected', 'decided', NO_COUNTS, 'the build failed: no pass ran') : null;
};

/** `{ verdict, scoringState, counts, reason }` from the build record and the passes `[{ n, attempts }]` (a missing pass is an open one). Throws on a wrong shape. */
export function verdictOf({ build, passes }) {
  const early = buildAnswer(build);
  if (early) return early;
  const counts = countOf(tenPasses(passes).map(passState));
  const [, verdict, state, reason] = RULES.find(([when]) => when(counts));
  return answer(verdict, state, counts, reason(counts));
}

/** A case of a build that failed: every case is `build failed` and nothing ran. */
export const badBuildCase = id => ({
  id, set: id.startsWith('s') ? 'visible' : 'heldout', verdict: CASE_VERDICT.buildFailed, wallMs: null, peakBytes: null, exit: null, events: null, reason: 'build failed', rIds: [], comparison: null, firstMismatch: null,
});

const completedPasses = passes => passes.map(p => ({ n: p.n, done: completedOf(p) })).filter(p => p.done).sort((a, b) => a.n - b.n);
const numbers = (entries, key) => entries.map(e => e.c[key]).filter(Number.isFinite);
const largest = list => (list.length === 0 ? null : Math.max(...list));

/** One case over the completed passes: the first failing pass's evidence, the largest peak and wall. */
function worstCase(entries) {
  const base = entries.find(e => e.c.verdict !== CASE_VERDICT.pass)?.c ?? entries[0].c;
  return Object.freeze({ ...base, peakBytes: largest(numbers(entries, 'peakBytes')), wallMs: largest(numbers(entries, 'wallMs')), memoryMaxBytes: entries[0].c.memoryMaxBytes ?? null });
}

const mergedCases = done => {
  const groups = Object.groupBy(done.flatMap(p => p.done.cases.map(c => ({ n: p.n, c }))), e => e.c.id);
  return Object.values(groups).map(worstCase).sort((a, b) => (a.id < b.id ? -1 : 1));
};

const buildRecordOf = b => ({ ran: true, ok: b.ok, status: b.exit, wallMs: b.wallMs, timedOut: b.timedOut, tail: b.tail });

/**
 * The one score of the session in the result shape `scoreShape` takes: per case the first failing pass's verdict and evidence (else the first pass's), the largest `peakBytes` and `wallMs` over the completed passes,
 * `memoryMaxBytes` the common read-back, and `passes` the number of completed passes. A failed build has no pass: `ids` are the cases, all `build failed`.
 */
export function rollupOf({ build, passes, limits, ids = [] }) {
  const done = completedPasses(passes);
  const cases = build.ok === false ? ids.map(badBuildCase) : mergedCases(done);
  return Object.freeze({
    build: buildRecordOf(build), limits, cases, runner: done[0]?.done.runner, memoryCertified: done.length > 0 && done.every(p => p.done.memoryCertified === true), passes: done.length,
  });
}
