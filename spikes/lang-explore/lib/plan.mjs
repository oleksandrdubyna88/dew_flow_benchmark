// The session plan of the pilot (plan D-X2, D-X5, D-X7): v2 section 4.5's bare prompt as written, the pilot's caps ($50, 75 min, 45 min stall), no host allowed, the locale, and the extra environment keys.
// The plan is `bareSessionPlan`'s (v1), changed in exactly the fields below; `over` is still checked by v1 (it takes only the test seams). Pure but for reading the registered prompts.
import { createHash, randomBytes } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { bareSessionPlan } from '../../minigit-bench/lib/bare-plan.mjs';
import { dummyToken } from '../../minigit-bench/lib/sandbox.mjs';
import { INPUTS } from './paths.mjs';
import { stackSentence } from './workspace.mjs';

/** v2 section 4.5, "Bare": the template, byte for byte (a test compares it with the plan's text). */
export const PROMPT_TEMPLATE = [
  'Implement the program described in STATEMENT.md in this directory, in {STACK}. {STACK_SENTENCE}',
  'STATEMENT.md, samples/, runner/, build.sh, run.sh, PACKAGES.md and the toolchain pin files of the stack are frozen inputs:',
  'do not change them. The program is built by `bash build.sh` and started by `bash run.sh` exactly as STATEMENT.md says;',
  '`python3 -I runner/run_samples.py samples/` runs the visible samples the way the hidden tests will. Hidden tests will run',
  'it under registered time and memory limits, never below the floors STATEMENT.md guarantees. Only the packages listed in',
  'PACKAGES.md are available;',
  'there is no network. Work autonomously; nobody answers questions. End with a summary of what you built and the checks',
  'you ran.',
].join('\n');

export const STACK_NAMES = Object.freeze({ csharp: 'C#', node: 'TypeScript' });
const sha256 = text => createHash('sha256').update(text).digest('hex');

/** The prompt of a stack: the template with `{STACK}` and `{STACK_SENTENCE}` filled. */
export const promptText = ({ template = PROMPT_TEMPLATE, stack, sentence }) => template.replace('{STACK}', STACK_NAMES[stack]).replace('{STACK_SENTENCE}', sentence);

/** `inputs/prompts.json` as the code computes it: the template, its sha256, and each stack's text with its sha256. */
export function buildPrompts(explore) {
  return {
    source: 'todo/PLAN_lang_comparison_v2.md section 4.5, Bare; stack sentences with SDK and TypeScript patch as this pilot runs them (todo/PLAN_lang_explore_task1.md D-X7)',
    template: PROMPT_TEMPLATE, templateSha256: sha256(PROMPT_TEMPLATE),
    prompts: Object.fromEntries(Object.keys(STACK_NAMES).map(stack => {
      const sentence = stackSentence(explore, stack);
      const text = promptText({ stack, sentence });
      return [stack, { stackSentence: sentence, text, sha256: sha256(text) }];
    })),
  };
}

/** The registered prompts. */
export const loadPrompts = (file = path.join(INPUTS, 'prompts.json')) => JSON.parse(readFileSync(file, 'utf8'));

/**
 * The `ctx.planFor` of the launch phase (seam W1): v1's bare plan with the pilot's prompt, no allowed host (`allow: []`, v2 D17), the locale (seam W2) and the pilot's caps. `ctx` carries `prompts`,
 * `locale`, `caps` and `envExtra`; `item` is the phase's `{ session, rid, layout, ... }`. A fresh dummy credential per session.
 */
export function planForSession({ ctx, item }) {
  const stack = item.session.stack;
  const base = bareSessionPlan({
    layout: item.layout, stack, phase: 'v1', claudeBinary: ctx.claudeBinary, cliVersion: ctx.cliVersion, nodeDir: ctx.nodeDir, tsDir: ctx.tsDir, token: ctx.token, dummy: dummyToken(randomBytes(6).toString('hex')),
    prices: ctx.prices, operatorHome: ctx.home, harnessSources: ctx.harnessSources, caps: ctx.caps, envExtra: ctx.envExtra ?? {}, over: ctx.planOver ?? {},
  });
  return { ...base, prompt: ctx.prompts.prompts[stack].text, allow: [], locale: ctx.locale };
}
