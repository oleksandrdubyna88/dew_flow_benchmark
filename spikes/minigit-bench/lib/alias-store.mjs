// The series' alias baseline (decision D12), kept in `<series>/aliases.json`. The FIRST session of a series sets it; every later one is checked against it, and a first
// use of a new alias only extends it. An alias that now resolves to another id, or a new internal model, is a stop (aliases.mjs), never an update.
import { existsSync, readFileSync, writeFileSync } from 'node:fs';

/** `{ get() -> baseline | null, update(aliasRecordResult, check) }` over `file`. Nothing is written for a stop, and an absent file is `null` (the next session sets it). */
export function aliasStore(file) {
  let current = existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null;
  return {
    get: () => current,
    update(alias, check) {
      if (!alias.ok || check.verdict === 'stop') return;
      const next = current === null ? { aliases: { ...alias.value.aliases }, internal: [...alias.value.internal] } : { ...current, aliases: { ...current.aliases, ...check.extend } };
      if (JSON.stringify(next) === JSON.stringify(current)) return;
      current = next;
      writeFileSync(file, `${JSON.stringify(current, null, 2)}\n`);
    },
  };
}
