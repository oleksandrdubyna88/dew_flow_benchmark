// The schedule of one bare pair (E3b.S1 step 8): the barrier, and the void / re-run table. Pure over the pair's session states.
//
// A pair state is `{ pair, sessions: [{ stack, phase, attempt, status, class?, data?, label? }] }`; `status` is `planned | launching | ran | scored`.
// `ran` means the session ended and was classified (`class`, `data`); only a data session is scored (a void has nothing to score).
//
// The table (parent 11.1: a void is re-run ONCE, in a fresh namespace `.a2`; a re-run is paired with its partner's only when the partner voided
// too, otherwise it runs alone and is labelled `ran unpaired`; re-runs come after the pair's own phases and before the next pair):
//   v1 both data  -> the v2 pair                      v1 both void -> the v1 re-run pair (paired) -> v2 for those with data
//   v1 one void   -> the partner's v2 ALONE -> the re-run v1 ALONE -> its v2 ALONE              a void a2 -> that cell is `missing`
//   v2 one void   -> that v2 a2 ALONE (from the recorded v1 tree)                               v2 both void -> the v2 re-run pair
// The barrier: no v2 phase starts while a v1 data session is unscored. A `stop:` class halts everything.
export const STACKS = Object.freeze(['csharp', 'node']);

const at = (st, stack, phase, attempt) => st.sessions.find(s => s.stack === stack && s.phase === phase && s.attempt === attempt);
const ended = s => s !== undefined && (s.status === 'ran' || s.status === 'scored');
const dataOf = s => ended(s) && s.data === true;
const voidOf = s => ended(s) && s.data === false;

const launch = (sessions, label) => ({ action: 'launch', sessions, label, phase: sessions[0].phase });
const ref = (stack, phase, attempt) => ({ stack, phase, attempt });
const scoreStep = launched => ({ action: 'score', sessions: launched.sessions });

const complete = (step, st) => {
  const states = step.sessions.map(r => at(st, r.stack, r.phase, r.attempt));
  if (step.action === 'launch') return states.every(ended);
  return states.every(s => s === undefined || s.data !== true || s.status === 'scored');
};

/** The stacks of `refs` whose session has data (to be scored, or eligible for the next phase). */
const withData = (st, phase, attempt, stacks = STACKS) => stacks.filter(s => dataOf(at(st, s, phase, attempt)));

function* v1Phase(st) {
  const first = launch(STACKS.map(s => ref(s, 'v1', 1)), 'paired');
  yield first;
  yield scoreStep(first);
  const voids = STACKS.filter(s => voidOf(at(st, s, 'v1', 1)));
  if (voids.length === 2) {
    const again = launch(STACKS.map(s => ref(s, 'v1', 2)), 'paired');
    yield again;
    yield scoreStep(again);
    return withData(st, 'v1', 2).map(stack => ({ stack, v1: 2 }));
  }
  if (voids.length === 0) return STACKS.map(stack => ({ stack, v1: 1 }));
  const [x] = voids;
  const y = STACKS.find(s => s !== x);
  const partnerV2 = launch([ref(y, 'v2', 1)], 'ran unpaired');
  yield partnerV2;
  yield scoreStep(partnerV2);
  const rerun = launch([ref(x, 'v1', 2)], 'ran unpaired');
  yield rerun;
  yield scoreStep(rerun);
  return [{ stack: y, v1: 1, v2Done: true }, ...(dataOf(at(st, x, 'v1', 2)) ? [{ stack: x, v1: 2 }] : [])];
}

function* v2Phase(st, eligible) {
  const todo = eligible.filter(e => !e.v2Done).map(e => e.stack);
  if (todo.length > 0) {
    const step = launch(todo.map(s => ref(s, 'v2', 1)), todo.length === 2 ? 'paired' : 'ran unpaired');
    yield step;
    yield scoreStep(step);
  }
  const voids = eligible.map(e => e.stack).filter(s => voidOf(at(st, s, 'v2', 1)));
  if (voids.length > 0) {
    const again = launch(voids.map(s => ref(s, 'v2', 2)), voids.length === 2 ? 'paired' : 'ran unpaired');
    yield again;
    yield scoreStep(again);
  }
}

function* plan(st) {
  const eligible = yield* v1Phase(st);
  yield* v2Phase(st, eligible);
}

/** A score step names only the data sessions still unscored: a void has nothing to score. */
const unscored = (step, st) => ({ ...step, sessions: step.sessions.filter(r => { const s = at(st, r.stack, r.phase, r.attempt); return s?.data === true && s.status !== 'scored'; }) });

/** The next thing to do: `{ action: 'launch' | 'score', sessions: [{ stack, phase, attempt }], label? }`, `{ action: 'halt', reason }` or `{ action: 'done' }`. */
export function nextStep(state) {
  const stopped = state.sessions.find(s => ended(s) && String(s.class).startsWith('stop:'));
  if (stopped) return { action: 'halt', reason: `${stopped.class} in ${stopped.stack} ${stopped.phase} a${stopped.attempt}: the series stops` };
  for (const step of plan(state)) {
    if (!complete(step, state)) return step.action === 'score' ? unscored(step, state) : step;
  }
  return { action: 'done' };
}

/** What the pair holds for its record: the cells that are `missing` and why, and the stacks that hold a `ran unpaired` session. */
export function pairSummary(state) {
  const missing = STACKS.flatMap(stack => {
    const v1Dead = voidOf(at(state, stack, 'v1', 1)) && voidOf(at(state, stack, 'v1', 2));
    const v2Dead = voidOf(at(state, stack, 'v2', 1)) && voidOf(at(state, stack, 'v2', 2));
    if (v1Dead) return [{ stack, phase: 'v1', why: 'a second void' }, { stack, phase: 'v2', why: 'its v1 is missing' }];
    return v2Dead ? [{ stack, phase: 'v2', why: 'a second void' }] : [];
  });
  const unpairedStacks = [...new Set(state.sessions.filter(s => s.label === 'ran unpaired').map(s => s.stack))];
  return { missing, unpairedStacks };
}
