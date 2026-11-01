// inputs/specs.json is the one record the parent plan names (D37); it is GENERATED from the writers' files in inputs/specs.d/ and
// never edited by hand: sorted keys at every depth, LF, two-space indent, a final newline. A merge conflict in it is resolved by
// running `node spikes/lang-bench/specs-record.mjs` again. Each writer owns one file and writes only that file.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

const ok = value => ({ ok: true, ...value });
const fail = reason => ({ ok: false, reason });

/** The four writers' files (E1.S8a, E1.S8b, E1.S9, E1.S2): the only names allowed in specs.d/. */
export const WRITER_FILES = Object.freeze(['runner.json', 'exec-layer.json', 'format.json', 'spec-states.json']);

function sortedCopy(value) {
  if (Array.isArray(value)) return value.map(sortedCopy);
  if (value === null || typeof value !== 'object') return value;
  return Object.fromEntries(Object.keys(value).sort().map(key => [key, sortedCopy(value[key])]));
}

/** The canonical text of a value: sorted keys, two-space indent, LF, one final newline. */
export const canonicalJson = value => `${JSON.stringify(sortedCopy(value), null, 2)}\n`;

function readWriter(dir, name) {
  try {
    const parsed = JSON.parse(readFileSync(path.join(dir, name), 'utf8'));
    return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed) ? ok({ value: parsed }) : fail(`${name} is not one JSON object`);
  } catch (error) {
    return fail(`${name} cannot be read as JSON (${error.message})`);
  }
}

function merge(parts) {
  const record = {};
  for (const { name, value } of parts) {
    const clash = Object.keys(value).find(key => key in record);
    if (clash) return fail(`${name} and another writer both claim the key \`${clash}\``);
    Object.assign(record, value);
  }
  return ok({ record });
}

/** `{ ok, text }` of the record assembled from `<inputs>/specs.d/*`, or `{ ok: false, reason }`. */
export function assemble(inputsDir) {
  const dir = path.join(inputsDir, 'specs.d');
  const stray = readdirSync(dir).find(name => !WRITER_FILES.includes(name));
  if (stray) return fail(`specs.d/${stray} is not one of the writers' files (${WRITER_FILES.join(', ')})`);
  const parts = [];
  for (const name of WRITER_FILES.filter(n => existsSync(path.join(dir, n)))) {
    const read = readWriter(dir, name);
    if (!read.ok) return read;
    parts.push({ name, value: read.value });
  }
  const merged = merge(parts);
  return merged.ok ? ok({ text: canonicalJson(merged.record) }) : merged;
}

/** `{ ok: true }` when `<inputs>/specs.json` is byte for byte the assembly of its writers. */
export function checkRecord(inputsDir) {
  const built = assemble(inputsDir);
  if (!built.ok) return built;
  const file = path.join(inputsDir, 'specs.json');
  const have = existsSync(file) ? readFileSync(file, 'utf8') : null;
  return have === built.text ? { ok: true } : fail('inputs/specs.json differs from the assembly of inputs/specs.d: it was edited by hand or a writer changed; regenerate it with node spikes/lang-bench/specs-record.mjs');
}
