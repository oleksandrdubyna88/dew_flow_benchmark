// The bare prompt of Stage 1 (F3.S1 (b), PR 5): the template of the quick run with the one sentence the owner's plan rewords (F3's closed point 10: "under the time and memory limits STATEMENT.md states", because since D72 the statement
// states the limit itself), filled with the stack sentences the engine computes (`quickPrompts`). The registered `prompts-lang.json` holds exactly these texts, and a series refuses to start when its registered text is not what the engine
// sends (`registeredPromptProblems`). The quick run's own template (`plan.mjs`) is untouched: its series are closed and its test compares it with the parent's text.
import { quickPrompts } from './init-quick.mjs';
import { PROMPT_TEMPLATE } from './plan.mjs';

/** The sentence of the quick run's template that Stage 1 rewords, and its wording. */
export const OLD_LIMITS_SENTENCE = 'registered time and memory limits, never below the floors STATEMENT.md guarantees';
export const NEW_LIMITS_SENTENCE = 'the time and memory limits STATEMENT.md states';

/** The template Stage 1 sends: the quick run's with the limits sentence reworded. */
export const STAGE_PROMPT_TEMPLATE = PROMPT_TEMPLATE.replace(OLD_LIMITS_SENTENCE, NEW_LIMITS_SENTENCE);

/** What the engine sends for the stacks of Stage 1: `quickPrompts` over Stage 1's template. `explore` is the quick profile (`quickExplore`). */
export const stagePrompts = explore => quickPrompts(explore, STAGE_PROMPT_TEMPLATE);

/** The sentences saying where the registered template is not the one the engine sends, or `[]`. */
const templateProblems = ({ registered, sent }) => (registered?.template === sent.template && registered?.templateSha256 === sent.templateSha256 ? [] : ['the registered template is not the one the engine sends']);

/** The sentences saying where the text or sha256 of a task and stack is not what the engine sends. */
const textProblems = ({ registered, sent }) => Object.entries(registered?.tasks ?? {}).flatMap(([task, stacks]) => Object.keys(sent.prompts).flatMap(stack => {
  const got = stacks?.[stack];
  return got?.text === sent.prompts[stack].text && got?.sha256 === sent.prompts[stack].sha256 ? [] : [`the registered prompt of task ${task} in ${stack} is not the text the engine sends`];
}));

/**
 * The sentences saying where a registered `prompts-lang.json` (`{ template, templateSha256, tasks: { NN: { <stack>: { text, sha256 } } } }`) is not what the engine sends (`stagePrompts(explore)`): the template, and the text and sha256 of
 * every task and stack. `[]` when it is.
 */
export function registeredPromptProblems({ registered, explore }) {
  const sent = stagePrompts(explore);
  const none = Object.keys(registered?.tasks ?? {}).length === 0 ? ['the registered prompts name no task'] : [];
  return [...templateProblems({ registered, sent }), ...none, ...textProblems({ registered, sent })];
}
