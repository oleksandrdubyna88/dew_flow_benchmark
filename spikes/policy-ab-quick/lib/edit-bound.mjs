// Pure verdict of the per-request bound on the main session's edits. Copied into the tree as
// .claude/hooks/edit-bound-core.mjs, so it imports nothing outside this file.
import path from 'node:path';

export const MAX_FILES = 1;
export const MAX_LINES = 50;
const EDIT_TOOLS = new Set(['Edit', 'Write', 'MultiEdit', 'NotebookEdit']);
const win = process.platform === 'win32';

export const lineCount = text => (typeof text === 'string' && text !== '' ? text.split('\n').length : 0);
const keyOf = p => (win ? p.toLowerCase() : p);

export function targetPath(input) {
  return input?.file_path ?? input?.notebook_path ?? '';
}

/** Pure: the absolute path when inside root (case-insensitive on Windows), else ''. */
export function insideTree(file, root) {
  if (!file || !root) return '';
  const abs = path.resolve(root, file);
  const rel = path.relative(keyOf(path.resolve(root)), keyOf(abs));
  return rel === '' || rel.startsWith('..') || path.isAbsolute(rel) ? '' : abs;
}

/** Lines this call touches: Edit old+new, MultiEdit the sum, Write content + the existing file, NotebookEdit new source. */
export function callLines(tool, input, existingLines) {
  switch (tool) {
    case 'Edit': return lineCount(input.old_string) + lineCount(input.new_string);
    case 'MultiEdit': return (input.edits ?? []).reduce((n, e) => n + lineCount(e.old_string) + lineCount(e.new_string), 0);
    case 'Write': return lineCount(input.content) + existingLines;
    case 'NotebookEdit': return lineCount(input.new_source);
    default: return 0;
  }
}

export const denyReason = (files, lines) => `subagent-models.md: the main session edits at most one file and ${MAX_LINES} lines per request (this would be ${files} files / ${lines} lines). Delegate the code to a coder subagent with model "sonnet".`;

/**
 * @returns {{action: 'allow'|'deny'|'reset', state: object, reason?: string}}
 * state: { [sessionId]: { files: string[], lines: number } }; a deny returns the state unchanged.
 */
export function decide({ event, tool, input, agentId, sessionId, root, state = {}, existingLinesOf = () => 0 }) {
  if (event === 'UserPromptSubmit') return { action: 'reset', state: {} };
  if (event !== 'PreToolUse' || !EDIT_TOOLS.has(tool) || agentId || !input || typeof input !== 'object') return { action: 'allow', state };
  const abs = insideTree(targetPath(input), root);
  if (abs === '') return { action: 'allow', state };
  const prior = state[sessionId] ?? { files: [], lines: 0 };
  const key = keyOf(abs);
  const files = prior.files.includes(key) ? prior.files : [...prior.files, key];
  const lines = prior.lines + callLines(tool, input, tool === 'Write' ? existingLinesOf(abs) : 0);
  if (files.length > MAX_FILES || lines > MAX_LINES) return { action: 'deny', state, reason: denyReason(files.length, lines) };
  return { action: 'allow', state: { ...state, [sessionId]: { files, lines } } };
}
