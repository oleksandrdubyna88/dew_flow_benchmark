// The gates before a launch step (E3b.S1), in order: pre-registration guard, stale retirement, preflight, quiet check, spend gate. Each returns `null` (go) or a `{ exit, message }` that ends the step;
// a cancel between two gates is a halt that voids nothing. A rehearsal measures nothing and pays nothing: it passes the guard, the retirement and the preflight, and nothing else.
import { EXIT, cellIdsOf, done, firstResult } from './bare-exit.mjs';
import { readLedger } from './ledger.mjs';
import { spendGate } from './spend-gate.mjs';

/** A cancel that comes OUTSIDE a launch (between steps, in a gate, while scoring): nothing is launched and no session is voided, so no re-run is spent; resume continues. */
export const cancelled = (ctx, where) => done(ctx.abortExit(), `cancelled ${where} (${ctx.abortReason() ?? 'no reason recorded'}): nothing was launched and no session was voided; resume continues from the run-map`);

const abortedAt = (ctx, signal, where) => (signal?.aborted ? cancelled(ctx, where) : null);

/** Whether the v1 phase started quiet, kept in the run-map (`pairs[k].v1Quiet`): `--accept-noisy` is refused for a v2 whose v1 was quiet, and a resume is a new process. */
function rememberV1Quiet({ ctx, h, quiet }) {
  const pairs = h.get().pairs ?? {};
  h.commit({ ...h.get(), pairs: { ...pairs, [ctx.pair]: { ...(pairs[ctx.pair] ?? {}), v1Quiet: quiet } } });
}

async function guardGate({ ctx, deps }) {
  const guard = await deps.guard({ paid: true, requireSeed: ctx.kind === 'data' });
  return guard.ok ? null : done(EXIT.config, guard.reason);
}

async function preflightGate({ deps }) {
  await deps.retireStale();
  const pre = await deps.preflight();
  return pre.ok ? null : done(EXIT.halt, `the preflight refuses: ${pre.reason}`);
}

async function quietGate({ ctx, deps, step, state, h, say, signal }) {
  const quiet = await deps.quiet({ phase: step.phase, v1Quiet: state.v1Quiet ?? null, cellIds: cellIdsOf(h), say, signal });
  if (signal?.aborted) return cancelled(ctx, 'in the quiet check');
  if (!quiet.ok) return done(EXIT.halt, quiet.reason, { verdict: quiet.verdict });
  state.quiet = quiet.value;
  if (step.phase === 'v1') rememberV1Quiet({ ctx, h, quiet: quiet.value.quiet });
  return null;
}

function spendStopGate({ ctx, step, signal }) {
  const gate = spendGate({ ledger: readLedger(ctx.ledgerFile), liveUsd: 0, caps: step.sessions.map(() => ctx.caps.capUsd), ceilingUsd: ctx.ceilingUsd, bareBudgetUsd: ctx.bareBudgetUsd });
  if (gate.stop) return done(EXIT.stop, `the spend gate stops the phase (${gate.stoppedBy.join(', ')}): ${gate.lines.join(' | ')}`, { gate });
  return abortedAt(ctx, signal, 'at the spend gate');
}

/** -> `null` (go) or a `{ exit, message }`. */
export async function gates(a) {
  const { ctx, signal } = a;
  const early = await firstResult([() => abortedAt(ctx, signal, 'before the gates'), () => guardGate(a), () => preflightGate(a), () => abortedAt(ctx, signal, 'in the gates')]);
  if (early) return early;
  return ctx.kind === 'rehearsal' ? null : firstResult([() => quietGate(a), () => spendStopGate(a)]);
}
