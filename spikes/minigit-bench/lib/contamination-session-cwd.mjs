// The Bash tool's working directory ACROSS the calls of one session. Claude Code keeps the shell's directory between Bash calls and resets it to the project dir (the session's box/work) when a call leaves it (the
// stream says so: "Shell cwd was reset to <dir>" ends the result). The contamination scan reads each call from the directory the calls before it left, in call order, so `cd src` in one call and `cd .. && bash test-v2.sh`
// in the next judge `test-v2.sh` in the work tree, not in box/ (data session ebd0b0, series bf89: a false `void: contaminated`).
//   - The directory a call ENDS in comes from its own `cd` steps (contamination-cwd.mjs `shellRun`); null = unknown (`cd -`, a variable, a substitution). Lexical: nothing is followed on the host.
//   - It is the START of the agent's next Bash call. The main agent and each subagent (`call.agentId`, the stream's `parent_tool_use_id`) keep their own; the first Bash call of an agent starts at the tree root.
//   - An end directory outside the work tree is the work tree for the next call (the CLI's reset). An unknown end is an unknown start (a relative word counts only as the argument of a walk, as for any unknown directory).
//   - The stream's "Shell cwd was reset to <dir>" line is NEVER used to choose a start: a command can print it, so it would let an agent pick where the scan reads its next call from. The rule above is the CLI's own and deterministic.
import { shellRun } from './contamination-cwd.mjs';
import { SHELL_TOOLS, under } from './contamination-reach.mjs';

const inTree = (dir, root) => dir !== null && under(dir, root);

/** The directory the next Bash call of the agent starts in: `end` (the directory the call ended in, or null) kept when inside the tree, else the work tree (the CLI's reset); null while unknown. */
export function nextStart(end, root) {
  if (inTree(end, root)) return end;
  return end === null ? null : root;
}

const agentOf = call => `${call.agent ?? 'main'}\u0000${call.agentId ?? ''}`;
const commandOf = call => String(call.input?.command ?? '');

/** The directory each call starts in, in call order: `[string | null]` (a call that is no shell call starts at the tree root, which nothing reads). */
export function startDirs(calls, ctx) {
  const kept = new Map(); // agent -> the directory its next Bash call starts in; local to this pass
  return calls.map(call => {
    if (!SHELL_TOOLS.has(call.tool)) return ctx.treeRoot;
    const start = kept.has(agentOf(call)) ? kept.get(agentOf(call)) : ctx.treeRoot;
    kept.set(agentOf(call), nextStart(shellRun(commandOf(call), ctx, start).end, ctx.treeRoot));
    return start;
  });
}
