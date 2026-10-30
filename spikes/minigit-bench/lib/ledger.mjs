// The series' spend ledger: `ledger.ndjson`, one JSON line per charge, appended the moment a session's spend is known (paid work is written as it is earned).
// `kind` is one of spend-gate.mjs's LEDGER_KINDS; `prior` lines are seeded by `init` from inputs/spend-prior.json; a line tagged `fromSeries` was carried by `init` from an earlier series' ledger (bare-init.mjs).
import { appendFileSync, existsSync, readFileSync } from 'node:fs';

/** Every line of the ledger, in order; an absent ledger is an empty one and a line that is not JSON is a thrown error (a corrupt ledger must stop the series). */
export const readLedger = file => (existsSync(file) ? readFileSync(file, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : []);

/** Append one charge `{ kind, usd, what, rid?, t }`. */
export const appendLedger = (file, line) => appendFileSync(file, `${JSON.stringify(line)}\n`);

/** `inputs/spend-prior.json` entries as `prior` ledger lines. */
export const priorLines = entries => entries.map(e => ({ kind: 'prior', usd: e.usd, what: e.what, evidence: e.evidence, date: e.date, ...(e.approx ? { approx: true } : {}) }));

/** A spend line this series wrote itself: not an event line, and not a copy `init` carried from an earlier series (`fromSeries`: that series' charge, whose rids equal this one's because every series reuses the registered seed). */
export const isOwnCharge = l => l.event === undefined && l.fromSeries === undefined;

/** Whether the ledger holds this series' OWN charge for `rid`. */
export const isCharged = (ledger, rid) => ledger.some(l => isOwnCharge(l) && l.rid === rid);

/**
 * Append a charge unless the ledger already holds THIS series' charge for the same rid (a spend line it wrote itself, not an event and not a line carried from an earlier series): a session is charged ONCE per series and rid.
 * A crash between this append and the run-map write leaves the ledger ahead of the map; the resume settles the session again and must not charge it again. A carried line is the earlier series' spend: it does not
 * stand for this series' session of the same rid (series b82e, pair 1: the carried lines hid USD 0.9569538 of its own). -> whether a line was appended.
 */
export function appendChargeOnce(file, line) {
  if (isCharged(readLedger(file), line.rid)) return false;
  appendLedger(file, line);
  return true;
}
