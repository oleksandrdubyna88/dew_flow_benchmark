// What `init` records for a quick task besides the pilot's own: the prompts of the task's stacks (the bare prompt of v2 section 4.5 with the quick stack sentences) and the identity of the held-out set (its tree hash
// and size), which every later pair is checked against, and what the harness was when it was made: the runner pin, the templates' identity, the sha256 of every template file, of the `PACKAGES.md` and of the two generated files (`STATEMENT.md`, `samples/load.json`) of each stack (a later rescore or replay of a kept tree tells harness drift from the agent's changes by the bytes of each file). `init` refuses when the held-out set is absent or the real runner does not accept its pairs.
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { generatedHashesNow } from './generated-drift.mjs';
import { harnessPinsNow } from './harness-pin.mjs';
import { parseProblem } from './heldout-quick.mjs';
import { readerHeldout } from './heldout-reader.mjs';
import { PROMPT_TEMPLATE, STACK_NAMES, promptText } from './plan.mjs';
import { quickExplore } from './quick.mjs';
import { taskSources } from './workspace.mjs';
import { BENCH_RUNNER, packagesInputs, packagesMdHashes, quickStackSentence } from './workspace-quick.mjs';

const sha256 = text => createHash('sha256').update(text).digest('hex');

/** `inputs/prompts.json`'s shape for the quick tasks: the template and each stack's text with its sha256. */
export function quickPrompts(explore) {
  return {
    source: 'todo/PLAN_lang_comparison_v2.md section 4.5, Bare; stack sentences as D67 (5) amended them, with the toolchains of the quick run (the owner\'s order of 2026-11-05)',
    template: PROMPT_TEMPLATE, templateSha256: sha256(PROMPT_TEMPLATE),
    prompts: Object.fromEntries(Object.keys(STACK_NAMES).map(stack => {
      const sentence = quickStackSentence(explore, stack);
      const text = promptText({ stack, sentence });
      return [stack, { stackSentence: sentence, text, sha256: sha256(text) }];
    })),
  };
}

/** The sha256 of the statement and the load file of each stack, from the workspace the series would be given. `sourcesFn` is the seam for the task repository. -> `ok({ csharp: {...}, node: {...} })` | `fail`. */
export async function generatedOf({ explore, task, home, sourcesFn }) {
  const [sources, inputs] = [await sourcesFn({ explore, task, home }), packagesInputs()];
  if (!sources.ok) return sources;
  return inputs.ok ? generatedHashesNow({ explore: quickExplore(explore, home), task, sources: sources.value, inputs: inputs.value }) : inputs;
}

/** The identity of the held-out set and its parse by the real runner. -> `ok({ heldoutQuick })` | `fail`. */
async function heldoutOf({ task, home, heldout }) {
  const held = await readerHeldout({ home, task, heldout });
  if (!held.ok) return fail(held.reason);
  const parsed = await parseProblem({ casesDir: held.value.casesDir, kind: task.kind, runner: path.join(BENCH_RUNNER, 'run_samples.py') });
  return parsed.ok ? ok({ heldoutQuick: { treeSha256: held.value.treeSha256, caseCount: held.value.caseIds.length, trapCount: held.value.traps.length, parsed: parsed.value } }) : fail(parsed.reason);
}

/** The bytes the series is given, recorded: the `PACKAGES.md` of each stack and the two generated files. -> `ok({ packagesMdSha256, generatedSha256 })` | `fail`. */
async function bytesOf({ explore, task, home, sourcesFn }) {
  const packages = packagesMdHashes({ kind: task.kind });
  if (!packages.ok) return fail(packages.reason);
  const generated = await generatedOf({ explore, task, home, sourcesFn });
  return generated.ok ? ok({ packagesMdSha256: packages.value, generatedSha256: generated.value }) : fail(generated.reason);
}

/** -> `ok({ prompts, heldoutQuick, ... })` | `fail`. `explore` is the base pins (the quick profile is made here). */
export async function quickInitExtra({ explore, task, home, sourcesFn = taskSources, heldout }) {
  const held = await heldoutOf({ task, home, heldout });
  const bytes = held.ok ? await bytesOf({ explore, task, home, sourcesFn }) : held;
  return bytes.ok ? ok({ prompts: quickPrompts(quickExplore(explore, home)), ...harnessPinsNow(), ...bytes.value, ...held.value }) : bytes;
}
