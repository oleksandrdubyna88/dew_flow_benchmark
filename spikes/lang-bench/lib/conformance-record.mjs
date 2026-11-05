// The record of the FORMAT the checker may read (E1.S8 batch 3, C2): `inputs/specs.d/format.json`, `{ A: { sha256 }, B: { sha256 } }` as the FORMAT tools read it. The checker reads a FORMAT only when
// its raw sha256 is one of the recorded ones, so a FORMAT that changed after the gate (a deleted row, a narrowed cell) is a refusal to run, never a pass; the sha256 is printed on an `ok` line for the gate record.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const DEFAULT_RECORD = fileURLToPath(new URL('../inputs/specs.d/format.json', import.meta.url));
const SHA = /^[0-9a-f]{64}$/;

const parseJson = text => {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
};

const readText = file => {
  try {
    return ok(readFileSync(file, 'utf8'));
  } catch (error) {
    return fail(`the record ${file} cannot be read (${error.code ?? error.message})`);
  }
};

const entriesOf = json => Object.entries(json ?? {}).filter(([, entry]) => SHA.test(entry?.sha256 ?? ''));

/** ok([[label, sha256, gate]]) from the record file: every top-level entry that holds a `sha256` of 64 hexadecimal digits. */
export function readRecord(file) {
  const text = readText(file);
  if (!text.ok) return text;
  const entries = entriesOf(parseJson(text.value));
  return entries.length > 0 ? ok(entries.map(([label, entry]) => [label, entry.sha256, entry.gate ?? null])) : fail(`the record ${file} holds no \`sha256\` of 64 hexadecimal digits`);
}

/** ok({ label, gate }) when `sha256` is one the record names (`gate` is the record's gate file, or null for a FORMAT that is recorded and not gated), else a fail naming the record. */
export function recordedLabel(entries, sha256, file) {
  const found = entries.find(([, recorded]) => recorded === sha256);
  return found === undefined ? fail(`this FORMAT (sha256 ${sha256}) is not the FORMAT the record names (${entries.map(([label, sha]) => `${label} ${sha}`).join(', ')} in ${file})`) : ok({ label: found[0], gate: found[2] });
}
