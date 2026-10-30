// The series' spend ledger: `ledger.ndjson`, one JSON line per charge, appended the moment a session's spend is known (paid work is written as it is earned).
// `kind` is one of spend-gate.mjs's LEDGER_KINDS; `prior` lines are seeded by `init` from inputs/spend-prior.json.
import { appendFileSync, existsSync, readFileSync } from 'node:fs';

/** Every line of the ledger, in order; an absent ledger is an empty one and a line that is not JSON is a thrown error (a corrupt ledger must stop the series). */
export const readLedger = file => (existsSync(file) ? readFileSync(file, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : []);

/** Append one charge `{ kind, usd, what, rid?, t }`. */
export const appendLedger = (file, line) => appendFileSync(file, `${JSON.stringify(line)}\n`);

/** `inputs/spend-prior.json` entries as `prior` ledger lines. */
export const priorLines = entries => entries.map(e => ({ kind: 'prior', usd: e.usd, what: e.what, evidence: e.evidence, date: e.date }));

/**
 * Append a charge unless the ledger already holds a charge for the same rid (a spend line, not an event): a session is charged ONCE. A crash between this append and the run-map write
 * leaves the ledger ahead of the map; the resume settles the session again and must not charge it again. -> whether a line was appended.
 */
export function appendChargeOnce(file, line) {
  if (readLedger(file).some(l => l.event === undefined && l.rid === line.rid)) return false;
  appendLedger(file, line);
  return true;
}
