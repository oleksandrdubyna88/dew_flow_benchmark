// The ONE memory value of the quick run (parent D72): `inputs/memory-limit.json` holds it, and the statement the agent reads, the visible load test's configuration (`samples/load.json`) and the scorer's limit are all
// generated from it here. No other file of the harness writes the figure; a scan test holds that. The value is a positive whole number of MiB (the layer derives the stacks' heap settings from whole MiB, D57), checked
// when the inputs load. The heap sentences come from the execution layer's own `heapKeys`, never from a typed hexadecimal.
import { readFileSync } from 'node:fs';
import path from 'node:path';
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

/** The sentence saying why `json` is not a registered memory value, or null. */
const limitProblem = json => LIMIT_CHECKS.find(([holds]) => !holds(json))?.[1](json) ?? null;

/** `ok({ bytes, mib, decidedOn, by })` from the registered file, or a `fail` naming the file and the rule. */
export function memoryLimit({ file = MEMORY_LIMIT_FILE } = {}) {
  let json;
  try { json = JSON.parse(readFileSync(file, 'utf8')); } catch (e) { return fail(`${file} is unreadable: ${e.message}: the memory limit has one registered value and it is read from there`); }
  const problem = limitProblem(json);
  return problem ? fail(`${file}: ${problem} (the memory limit is a positive whole number of MiB)`) : ok(Object.freeze({ bytes: json.bytes, mib: json.mib, decidedOn: json.decidedOn, by: json.by }));
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
