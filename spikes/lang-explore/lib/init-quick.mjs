// What `init` records for a quick task besides the pilot's own: the prompts of the task's stacks (the bare prompt of v2 section 4.5 with the quick stack sentences) and the identity of the held-out set (its tree hash
// and size), which every later pair is checked against, and the runner pin it was made under (a later rescore or replay of a kept tree tells harness drift from the agent's changes by it). `init` refuses when the held-out set is absent or the real runner does not accept its pairs.
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runnerPinNow } from './harness-pin.mjs';
import { loadHeldoutQuick, parseProblem } from './heldout-quick.mjs';
import { PROMPT_TEMPLATE, STACK_NAMES, promptText } from './plan.mjs';
import { quickExplore } from './quick.mjs';
import { BENCH_RUNNER, quickStackSentence } from './workspace-quick.mjs';

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

/** -> `ok({ prompts, heldoutQuick })` | `fail`. `explore` is the base pins (the quick profile is made here). */
export async function quickInitExtra({ explore, task, home }) {
  const held = loadHeldoutQuick(home, task);
  if (!held.ok) return fail(held.reason);
  const parsed = await parseProblem({ casesDir: held.value.casesDir, kind: task.kind, runner: path.join(BENCH_RUNNER, 'run_samples.py') });
  if (!parsed.ok) return fail(parsed.reason);
  return ok({ prompts: quickPrompts(quickExplore(explore, home)), runnerPin: runnerPinNow(), heldoutQuick: { treeSha256: held.value.treeSha256, caseCount: held.value.caseIds.length, trapCount: held.value.traps.length, parsed: parsed.value } });
}
