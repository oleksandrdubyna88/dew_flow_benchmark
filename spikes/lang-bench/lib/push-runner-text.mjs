// The stack-word rule of the push check for the pinned runner (F1.S1 (f), parent T-L64). The runner is copied whole into the workspace of both stacks, so a stack word in a comment or a docstring of one of its files tells each agent
// what the other side is. The rule reads a file under `runner/` that ends in `.py` (the task repository's folder, or a built workspace's copy: the same relative path) with the stack words of `inputs/stack-words-lang.json` by that file's
// own rule (conformance-needles.mjs), over its comments and its docstring-like strings (py-text.mjs). A finding names the line, never the text. The string literals that are behaviour are the runner-text test's business, not this scan's.
import { wordFindings } from './conformance-needles.mjs';
import { pythonTextItems } from './py-text.mjs';

export const isRunnerPython = file => file.startsWith('runner/') && file.endsWith('.py');

const hitsOf = (items, what, matchers) => items.flatMap(item => wordFindings('', item.text, matchers).map(hit => ({ line: item.line + hit.line - 1, what })));

/** `[{ line, what }]` for a runner file's text; `matchers` are the stack words' (`matchersOf`), none gives no hit. */
export function runnerTextHits(file, text, matchers) {
  if (!isRunnerPython(file) || !Array.isArray(matchers) || matchers.length === 0) return [];
  const items = pythonTextItems(text);
  return [...hitsOf(items.comments, 'stack word in a comment', matchers), ...hitsOf(items.docstrings, 'stack word in a docstring', matchers)];
}
