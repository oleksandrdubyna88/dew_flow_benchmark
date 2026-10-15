// Pure: the run prompt. The parallel-children sentence is identical for both arms; a story opts in with parallelHint.
export const PARALLEL_SENTENCE = 'Independent pieces of work may be done concurrently — launch several subagents in one message when they do not depend on each other.';

/** The prompt with PARALLEL_SENTENCE as its own paragraph after the autonomy paragraph (appended when there is none). */
export function composePrompt(base, parallelHint) {
  if (!parallelHint) return base;
  if (base.includes(PARALLEL_SENTENCE)) throw new Error('the prompt already carries the parallel sentence');
  const trailing = base.endsWith('\n') ? '\n' : '';
  const paragraphs = base.replace(/\n$/, '').split(/\n{2,}/);
  const at = paragraphs.findIndex(p => p.startsWith('Work autonomously'));
  const next = at < 0 ? [...paragraphs, PARALLEL_SENTENCE] : [...paragraphs.slice(0, at + 1), PARALLEL_SENTENCE, ...paragraphs.slice(at + 1)];
  return `${next.join('\n\n')}${trailing}`;
}
