// The inputs a push check takes, built from fixtures: a synthetic aid (never the real ones) and the repository's own needle and credit files.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { aidNeedles, parseKnownNeedles } from '../../lib/push-needles.mjs';
import { parseCredits } from '../../lib/push-credits.mjs';

const inputsFile = name => readFileSync(fileURLToPath(new URL(`../../inputs/${name}`, import.meta.url)), 'utf8');

export const realCredits = () => parseCredits(inputsFile('credits-lang.json'));

/** `inputs` for checkBranch / checkText / checkSelf: one synthetic aid called synthetic-aid.md, the committed needles and credits. */
export function loadInputsForTest({ aidText, tokensText = '' }) {
  const aids = aidNeedles({ aids: [{ name: 'synthetic-aid.md', text: aidText }], tokensText });
  if (!aids.ok) throw new Error(aids.reason);
  return { aids: aids.value, known: parseKnownNeedles(JSON.parse(inputsFile('needles-lang.json'))).value, credits: realCredits(), templateFiles: [] };
}
