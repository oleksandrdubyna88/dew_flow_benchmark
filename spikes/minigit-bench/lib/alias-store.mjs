// The series' alias baseline (decision D12), kept in `<series>/aliases.json`. The FIRST session of a series sets it; every later one is checked against it, and a first
// use of a new alias only extends it. An alias that now resolves to another id, or a new internal model, is a stop (aliases.mjs), never an update.
import { existsSync, readFileSync, writeFileSync } from 'node:fs';

/** The baseline after a session: the first session sets it, a later one only extends it with the aliases it used for the first time. */
const nextBaseline = (current, alias, check) => (current === null
  ? { aliases: { ...alias.value.aliases }, internal: [...alias.value.internal] }
  : { ...current, aliases: { ...current.aliases, ...check.extend } });

/** An alias record that could not be read, and a stop, never change the baseline. */
const leavesBaseline = (alias, check) => !alias.ok || check.verdict === 'stop';

/** `{ get() -> baseline | null, update(aliasRecordResult, check) }` over `file`. Nothing is written for a stop, and an absent file is `null` (the next session sets it). */
export function aliasStore(file) {
  let current = existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null;
  return {
    get: () => current,
    update(alias, check) {
      if (leavesBaseline(alias, check)) return;
      const next = nextBaseline(current, alias, check);
      if (JSON.stringify(next) === JSON.stringify(current)) return;
      current = next;
      writeFileSync(file, `${JSON.stringify(current, null, 2)}\n`);
    },
  };
}
