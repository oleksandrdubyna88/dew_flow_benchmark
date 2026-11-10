// The ONE memory value of the quick run (parent D72): `inputs/memory-limit.json` holds it, and the statement the agent reads, the visible load test's configuration (`samples/load.json`) and the scorer's limit are all
// generated from it here. No other file of the harness writes the figure; a scan test holds that. The value is a positive whole number of MiB (the layer derives the stacks' heap settings from whole MiB, D57), checked
// when the inputs load. The heap sentences come from the execution layer's own `heapKeys`, never from a typed hexadecimal.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { systemClock } from './clock.mjs';
import { replaceJson } from './package-write.mjs';
import { heapKeys } from '../../lang-bench/lib/exec-layer/env.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS } from './paths.mjs';

export const MEMORY_LIMIT_FILE = path.join(INPUTS, 'memory-limit.json');
const MIB = 1024 * 1024;
const text = v => typeof v === 'string' && v.trim() !== '';

const LIMIT_CHECKS = Object.freeze([
  [j => Number.isSafeInteger(j?.bytes) && j.bytes > 0 && j.bytes % MIB === 0, j => `bytes must be a positive whole number of MiB (got ${JSON.stringify(j?.bytes)})`],
  [j => j.mib === j.bytes / MIB, j => `mib must be bytes / ${MIB} (got ${JSON.stringify(j.mib)} for ${j.bytes} bytes)`],
  [j => text(j.decidedOn) && text(j.by), () => 'decidedOn and by must say when and by whom the value was decided'],
]);

/** The twelve references of a registration: six tasks, two stacks, each a distinct `task:stack` pair with a tree hash. */
const REGISTERED_TASKS = Object.freeze(['04', '05', '06', '07', '08', '10']);
const STACKS = Object.freeze(['csharp', 'node']);
const referenceOk = r => typeof r?.task === 'string' && STACKS.includes(r.stack) && /^[0-9a-f]{64}$/.test(r.treeSha256 ?? '');
const twelveDistinct = refs => Array.isArray(refs) && refs.length === 12 && refs.every(referenceOk) && new Set(refs.map(r => `${r.task}:${r.stack}`)).size === 12;
const registrationOk = reg => reg === undefined || (text(reg?.date) && twelveDistinct(reg.references));

const REGISTRATION_CHECK = [j => registrationOk(j.registration), () => 'registration must be { date, references } with twelve distinct task and stack pairs, each with a tree hash'];

/** The sentence saying why `json` is not a registered memory value, or null. */
const limitProblem = json => [...LIMIT_CHECKS, REGISTRATION_CHECK].find(([holds]) => !holds(json))?.[1](json) ?? null;

/** `ok({ bytes, mib, decidedOn, by, registration })` from the file (`registration` is `null` until F2.S1's tool writes it), or a `fail` naming the file and the rule. */
export function memoryLimit({ file = MEMORY_LIMIT_FILE } = {}) {
  let json;
  try { json = JSON.parse(readFileSync(file, 'utf8')); } catch (e) { return fail(`${file} is unreadable: ${e.message}: the memory limit has one registered value and it is read from there`); }
  const problem = limitProblem(json);
  return problem ? fail(`${file}: ${problem} (the memory limit is a positive whole number of MiB)`) : ok(Object.freeze({ bytes: json.bytes, mib: json.mib, decidedOn: json.decidedOn, by: json.by, registration: json.registration ?? null }));
}

const grouped = n => n.toLocaleString('en-US');

/** D72's sentence, exactly, both numbers formatted from the value. */
export const memorySentence = limit => `The program and its descendants run under a ${limit.mib} MiB (${grouped(limit.bytes)}-byte) cgroup memory limit, including runtime overhead. Build resources are separate.`;

/** The stack's heap setting as the execution layer derives it from the value (D55): C# `DOTNET_GCHeapHardLimit`, Node `--max-old-space-size`. */
export function heapSentence(stack, limit) {
  const keys = heapKeys(stack, limit.bytes);
  if (stack === 'csharp') return `The .NET garbage-collected heap is capped by DOTNET_GCHeapHardLimit=${keys.DOTNET_GCHeapHardLimit} (${grouped(Number.parseInt(keys.DOTNET_GCHeapHardLimit, 16))} bytes, 90 % of the limit).`;
  return `Node runs with ${/--max-old-space-size=\d+/.exec(keys.NODE_OPTIONS)[0]} (MiB, 90 % of the limit, rounded down).`;
}

/** The object written to `samples/load.json`: the limit the load test is to be aimed at, and the stack's heap setting. */
export const loadTestConfig = (stack, limit) => Object.freeze({
  memoryLimitBytes: limit.bytes, memoryLimitMiB: limit.mib, heap: heapKeys(stack, limit.bytes), note: 'the enforced memory limit of every test; the program is stopped above it',
});

// ---- the registration (D72, F2.S1 P9): the ONE writer of the `registration` field ------------------------------------------------------------------------------

const REGISTERED_NOTE = 'one registered value for all six quick tasks; the statement, samples/load.json and the scorer read it here; the registration field lists the twelve references that passed under it';

/** The first reference of the twelve (task by task, C# then Node) that has not passed at `bytes`, as a sentence naming the task and the stack, or null. `packages` is `packages.json` as an object. */
function unpassedReference({ packages, bytes }) {
  for (const task of REGISTERED_TASKS) {
    const entry = packages[task];
    for (const stack of STACKS) {
      if (entry?.referenceVerdicts?.[stack] !== 'accepted' || entry.memoryBytes !== bytes || !/^[0-9a-f]{64}$/.test(entry.refsTreeSha256?.[stack] ?? '')) return `the ${stack} reference of task ${task} has not passed every case at ${bytes} bytes (no registered package, or another value): the value is registered only when all twelve have`;
    }
  }
  return null;
}

/**
 * Write the registration field and fix the note (`chosen` until the field exists): only when `packages` (`inputs/packages.json`) holds all six tasks with both references `accepted` at the file's own `bytes`. The date and the twelve
 * `{ task, stack, treeSha256 }` are the field; nothing else in this repository writes it (a scan test holds that). A second call is a no-op. -> `ok('registered' | 'unchanged')` | `fail(sentence)`.
 */
export function registerMemoryValue({ packages, file = MEMORY_LIMIT_FILE, now = systemClock }) {
  const limit = memoryLimit({ file });
  if (!limit.ok) return limit;
  if (limit.value.registration !== null) return ok('unchanged');
  const missing = unpassedReference({ packages, bytes: limit.value.bytes });
  if (missing) return fail(missing);
  const references = REGISTERED_TASKS.flatMap(task => STACKS.map(stack => ({ task, stack, treeSha256: packages[task].refsTreeSha256[stack] })));
  const { registration: _, ...current } = JSON.parse(readFileSync(file, 'utf8'));
  replaceJson({ file, value: { ...current, note: REGISTERED_NOTE, registration: { date: now().toISOString().slice(0, 10), references } } });
  return ok('registered');
}
