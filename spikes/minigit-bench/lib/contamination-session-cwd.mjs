// The Bash tool's working directory ACROSS the calls of one session. Claude Code keeps the shell's directory between Bash calls and resets it to the project dir (the session's box/work) when a call leaves it (the
// stream says so: "Shell cwd was reset to <dir>" ends the result). The contamination scan reads each call from the directory the calls before it left, in call order, so `cd src` in one call and `cd .. && bash test-v2.sh`
// in the next judge `test-v2.sh` in the work tree, not in box/ (data session ebd0b0, series bf89: a false `void: contaminated`).
//   - The directory a call ENDS in comes from its own `cd` steps (contamination-cwd.mjs `shellRun`); null = unknown (`cd -`, a variable, a substitution). Lexical: nothing is followed on the host.
//   - It is the START of the agent's next Bash call. The main agent and each subagent (`call.agentId`, the stream's `parent_tool_use_id`) keep their own; the first Bash call of an agent starts at the tree root.
//   - An end directory outside the work tree is the work tree for the next call (the CLI's reset). An unknown end is an unknown start (a relative word counts only as the argument of a walk, as for any unknown directory).
//   - EVIDENCE: a result whose last line is "Shell cwd was reset to <dir>" (`call.reset`, stream-calls.mjs) with <dir> inside the work tree says where the next call starts, when the scan cannot place the end itself (an
//     unknown end, or one outside the tree). It never moves a start away from a placed end inside the tree: the line is plain output a command can print, and the scan does not let a printed line hide a reach.
import { shellRun } from './contamination-cwd.mjs';
import { SHELL_TOOLS, under } from './contamination-reach.mjs';

const inTree = (dir, root) => dir !== null && under(dir, root);
const evidenceDir = (reset, root) => (typeof reset === 'string' && under(reset, root) ? reset : null);

/** The directory the next Bash call of the agent starts in: `end` (the directory the call ended in, or null) kept when inside the tree, else the CLI's reset directory (evidence), else the work tree; null while unknown. */
export function nextStart(end, reset, root) {
  if (inTree(end, root)) return end;
  return evidenceDir(reset, root) ?? (end === null ? null : root);
}

const agentOf = call => `${call.agent ?? 'main'}\u0000${call.agentId ?? ''}`;
const commandOf = call => String(call.input?.command ?? '');

/** The directory each call starts in, in call order: `[string | null]` (a call that is no shell call starts at the tree root, which nothing reads). */
export function startDirs(calls, ctx) {
  const kept = new Map(); // agent -> the directory its next Bash call starts in; local to this pass
  return calls.map(call => {
    if (!SHELL_TOOLS.has(call.tool)) return ctx.treeRoot;
    const start = kept.has(agentOf(call)) ? kept.get(agentOf(call)) : ctx.treeRoot;
    kept.set(agentOf(call), nextStart(shellRun(commandOf(call), ctx, start).end, call.reset, ctx.treeRoot));
    return start;
  });
}
