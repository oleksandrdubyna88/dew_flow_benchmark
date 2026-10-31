// Consistency of the registered files with each other (E3b review B1): a quoted text must not contradict a pin. The one check today: the .NET SDK a C# prompt
// names ("SDK 10.0.113 is installed") against `stacks.json` `pins.dotnetSdk`. The prompt is QUOTED from the parent plan and the pin is amended by a dated entry, so
// the two can drift; the model reads the prompt, so a wrong SDK in it is a wrong treatment statement. Pure over parsed values.
import { fail, ok } from './result.mjs';

const SDK = /\bSDK (\d+\.\d+\.\d+)\b/g;

/** Every SDK version a C# prompt text of `prompts-bare.json` names. */
const namedSdks = prompts => Object.values(prompts?.prompts?.csharp ?? {}).flatMap(p => [...String(p?.text ?? '').matchAll(SDK)].map(m => m[1]));

/** `null` when every SDK the C# prompts name is the pin; else a sentence naming the prompt's SDK and the pin. */
export function sdkProblem({ prompts, stacks }) {
  const pin = stacks?.pins?.dotnetSdk;
  if (typeof pin !== 'string') return 'stacks.json has no pins.dotnetSdk to check the C# prompt against';
  const wrong = [...new Set(namedSdks(prompts))].filter(v => v !== pin);
  return wrong.length === 0 ? null : `prompts-bare.json names SDK ${wrong.join(', ')} but stacks.json pins ${pin}: re-quote the prompts from a parent commit that holds the amended sentence`;
}

/** The same from two JSON texts -> `ok(null)` | `fail` (a file that is not JSON is a refusal, never a pass). */
export function sdkCheckOfTexts({ promptsText, stacksText }) {
  let parsed;
  try {
    parsed = { prompts: JSON.parse(promptsText), stacks: JSON.parse(stacksText) };
  } catch (e) {
    return fail(`prompts-bare.json or stacks.json is not JSON: ${e.message}`);
  }
  const problem = sdkProblem(parsed);
  return problem ? fail(problem) : ok(null);
}
