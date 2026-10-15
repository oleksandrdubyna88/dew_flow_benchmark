// Pure: the cost watchdog and the cell outcome. The CLI's own --max-budget-usd is checked between turns and parallel children can
// overshoot it, so run-cell also meters the stream and kills the process tree (by the PID it started) at cap x 1.1.
import { costFromMessages, OUTPUT_FLOOR_PER_MESSAGE } from './pricing.mjs';

export const OVER_CAP_FACTOR = 1.1;

/**
 * Accumulates assistant messages (main and children - the stream forwards both) and prices them with the FALLBACK rates of
 * pricing.mjs. That is an ESTIMATE, never a result: the number that is reported for a cell is the envelope's total, and the meter's
 * last reading is recorded beside it so the drift between the two is visible.
 */
export class CostMeter {
  #seen = new Map();

  /** One stream line (a JSON string). Returns the running estimate in USD. */
  feed(line) {
    let ev;
    try { ev = JSON.parse(line); } catch { return this.usd; }
    if (ev?.type !== 'assistant' || !ev.message?.usage) return this.usd;
    const id = ev.message.id ?? `anon-${this.#seen.size}`;
    this.#seen.set(id, ev);
    return this.usd;
  }

  get usd() {
    return costFromMessages([...this.#seen.values()], { outputFloor: OUTPUT_FLOOR_PER_MESSAGE }).usd;
  }

  overCap(capUsd) {
    return this.usd > capUsd * OVER_CAP_FACTOR;
  }
}

/** Pure: split a chunked stream into complete lines, carrying the partial tail. */
export function splitLines(carry, chunk) {
  const parts = `${carry}${chunk}`.split('\n');
  return { lines: parts.slice(0, -1), carry: parts.at(-1) };
}

/** The cell's outcome: completed / killed: wall / killed: over cap / crashed. A kill outranks the exit code it caused. */
export function outcomeOf({ exitCode, timedOut = false, overCap = false, threw = false }) {
  if (overCap) return 'killed: over cap';
  if (timedOut) return 'killed: wall';
  if (threw || exitCode !== 0) return 'crashed';
  return 'completed';
}
