// Pure helpers of the codex reading: the reader must not be able to tell which arm (or which model) wrote a candidate.
import { createHash } from 'node:crypto';
import path from 'node:path';
import { SPIKE_DIR } from './consts.mjs';

/** Where the anonymisation key lives: NOT in the runs root and not beside assess/ - the reader runs with those as its working tree. */
export const readingKeyFile = (storyId, spikeDir = SPIKE_DIR) => path.join(spikeDir, `reading-key.${storyId}.json`);

const MODEL = /\b(opus|sonnet|fable|haiku|claude-[a-z0-9.-]+)\b/gi;
const LABEL = /\b(c[1-9]|reference|arm A|arm B)\b/gi;
// The policy's own vocabulary: a summary that says "the edit bound denied", "I delegated to a coder subagent" or names the rule file
// tells the reader which arm it came from.
const POLICY = /\b(edit[- ]?bound\w*|subagent[- ]models?(?:\.md)?|delegat\w*|1 file(?: and| \/) 50 lines|PreToolUse)\b/gi;

/** Pure: replace model names and cell labels (text = a diff or a summary); counts for the key. */
export function scrubReaderText(text, { policy = false } = {}) {
  const counts = { model: 0, label: 0, policy: 0 };
  let out = text.replace(MODEL, () => { counts.model += 1; return '[model]'; });
  out = out.replace(LABEL, () => { counts.label += 1; return '[label]'; });
  if (policy) out = out.replace(POLICY, () => { counts.policy += 1; return '[policy wording]'; });
  return { text: out, counts };
}

/** Pure: an unbiased, reproducible shuffle (Fisher-Yates over a sha256 counter stream). */
export function shuffleSeeded(list, seed) {
  const out = [...list];
  const draw = (i, bound) => Number(createHash('sha256').update(`${seed}:${i}`).digest().readBigUInt64BE(0) % BigInt(bound));
  for (let i = out.length - 1; i > 0; i -= 1) {
    const j = draw(i, i + 1);
    [out[i], out[j]] = [out[j], out[i]];
  }
  return out;
}

/** The candidate id the reader sees: derived from the seed, so a rerun of the prep gives the same anonymisation. */
export const readerId = (seed, name) => createHash('sha256').update(`${seed}:id:${name}`).digest('hex').slice(0, 8);
