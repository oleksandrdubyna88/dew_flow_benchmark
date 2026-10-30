// Which time category a tool call belongs to (parent §10, E1.S4 step 3). Pure string work, no process is run.
//
// A command is split at the top level of its quoting (`&&`, `||`, `;`, `|`, newline); each segment has its leading
// shell keywords, env assignments and wrappers (`time`, `nice`, `timeout`, `npx`, the mount's low-priority launcher)
// peeled; the first rule that matches ANY segment decides, in the order build_test > sleep300 > wait > other_tool.
// Known limit: a heredoc body or a quoted script is not parsed, so `cat <<EOF` text holding a build line reads as that line.
// `wait` is the generic wait: E3's GitHub intervals later turn it into ci_wait / coderabbit_wait (spans.mjs refineWaits).
import { BACKGROUND, BUILD_TEST, COAI_GATE, CONSULTANT, CONTAINER, OTHER_TOOL, SLEEP_300, WAIT } from './categories.mjs';

const LITERAL_SLEEP = 'sleep 300';
// ask_human BLOCKS (up to 15 minutes by default) on a gate's question: src_mcp/src/Tools.cs:394-403.
const GATE_TOOLS = new Set(['review_plan', 'review_code', 'review_document', 'review_feature', 'resolve', 'ask_human']);
const CONSULT_TOOLS = new Set(['consult', 'ask_consultants', 'close_consult']);
const COAI_PREFIX = 'mcp__coai__';
const CONTAINER_TOOLS = new Set(['Agent', 'Task']);
const DOTNET_WORK = new Set(['build', 'restore', 'run', 'test', 'format']);
const NPM_RUN_WORK = new Set(['test', 'typecheck', 'lint']);
const KEYWORDS = /^(?:(?:do|then|else|elif|if|while|until|!)\s+|[({]+\s*)+/;
const ENV_ASSIGNMENT = /^[A-Za-z_][A-Za-z0-9_]*=/;
/** Options of a wrapper that take a separate value word. */
const VALUE_OPTIONS = {
  timeout: ['-k', '-s', '--kill-after', '--signal'],
  ionice: ['-c', '-n', '-p', '-P', '-u', '--class', '--classdata'],
  nice: ['-n', '--adjustment'],
  env: ['-u', '-C', '-S', '--unset', '--chdir'],
  shell: ['-o', '+o', '-O', '+O', '--rcfile', '--init-file'],
};
/** Wrappers whose own options are skipped and whose next word is the real command. */
const OPTION_WRAPPERS = new Set(['time', 'env', 'command', 'exec', 'npx', 'nice', 'ionice']);
const SHELLS = new Set(['bash', 'sh', 'zsh', 'dash']);

/** Split at `&&`, `||`, `;`, `|`, a single `&` and newlines that are outside quotes. Redirections (`2>&1`, `&>f`) are not separators. */
export function splitTopLevel(command) {
  const segments = [];
  let current = '';
  let quote = null;
  for (let i = 0; i < command.length; i += 1) {
    const ch = command[i];
    if (quote) { current += ch; if (ch === quote) quote = null; continue; }
    if (ch === '"' || ch === "'") { quote = ch; current += ch; continue; }
    const pair = command.slice(i, i + 2);
    if (pair === '&&' || pair === '||') { segments.push(current); current = ''; i += 1; continue; }
    const redirect = ch === '&' && (command[i - 1] === '>' || command[i - 1] === '<' || command[i + 1] === '>');
    if (ch === ';' || ch === '|' || ch === '\n' || (ch === '&' && !redirect)) { segments.push(current); current = ''; continue; }
    current += ch;
  }
  segments.push(current);
  return segments;
}

export const wordsOf = segment => (segment.trim().replace(KEYWORDS, '').replace(/[)}]+\s*$/, '').match(/"[^"]*"|'[^']*'|\S+/g) ?? []);
const isLauncher = word => /(^|\/)low-priority\.mjs$/.test(word);
export const unquote = word => (/^(["']).*\1$/s.test(word ?? '') ? word.slice(1, -1) : word);

/** Drop leading option words (and the value words of those that take one); an `--opt=value` is one word. */
function skipOptions(words, valueOptions = []) {
  let i = 0;
  while (i < words.length && words[i].startsWith('-')) i += valueOptions.includes(words[i]) ? 2 : 1;
  return words.slice(i);
}

/** Drop env assignments and wrappers from the front of a word list: `FOO=1 nice -n 19 dotnet build` -> `dotnet build`. */
function peel(words) {
  let w = words;
  for (;;) {
    const [head, ...rest] = w;
    if (head === undefined) return w;
    if (ENV_ASSIGNMENT.test(head)) { w = rest; continue; }
    if (OPTION_WRAPPERS.has(head)) { w = skipOptions(rest, VALUE_OPTIONS[head]); continue; }
    if (head === 'timeout') { w = skipOptions(rest, VALUE_OPTIONS.timeout).slice(1); continue; }
    if (head === 'node' && rest[0] && isLauncher(rest[0])) { w = rest.slice(1); continue; }
    return w;
  }
}

/** `node --test ...` or `node <path>/run-tests.mjs ...`: the first non-option word is the script. */
function nodeRunsTests(rest) {
  for (const word of rest) {
    if (word.startsWith('--test')) return true;
    if (!word.startsWith('-')) return /(^|\/)run-tests\.mjs$/.test(word);
  }
  return false;
}

function npmIsWork(rest) {
  if (rest[0] === 'ci' || rest[0] === 'test') return true;
  return rest[0] === 'run' && NPM_RUN_WORK.has(rest[1]);
}

/** The harness's own test entry points: the upstream `test-v*.sh` and the C# template's `scripts/run-tests.sh`. */
const isHarnessScript = word => /(^|\/)(test-v[^/]*|run-tests)\.sh$/.test(word ?? '');

/** For `bash [options] script` the script word; for `bash -c "cmd"` (also `-lc`) null and the command in `shellCommand`. */
function shellParts(rest) {
  for (let i = 0; i < rest.length; i += 1) {
    const w = rest[i];
    if (VALUE_OPTIONS.shell.includes(w)) { i += 1; continue; }
    if (/^-[A-Za-z]*c[A-Za-z]*$/.test(w)) return { command: unquote(rest[i + 1]) };
    if (!w.startsWith('-') && !w.startsWith('+')) return { script: w };
  }
  return {};
}

const shellCommandOf = words => (SHELLS.has(words[0]) ? shellParts(words.slice(1)).command ?? null : null);

/** Does this peeled segment build or test? (A `bash -c` argument is classified by the caller, recursively.) */
function isBuildTest(words) {
  const [cmd, ...rest] = words;
  if (cmd === undefined) return false;
  if (cmd === 'dotnet') return DOTNET_WORK.has(rest[0]);
  if (cmd === 'npm') return npmIsWork(rest);
  if (cmd === 'node') return nodeRunsTests(rest);
  if (SHELLS.has(cmd)) return isHarnessScript(shellParts(rest).script);
  if (cmd === 'make' || cmd === 'tsc') return true;
  return isHarnessScript(cmd) || /\.Tests(\.exe)?$/i.test(cmd);
}

const isWatchFlag = w => w === '--watch' || (w.startsWith('--watch=') && w !== '--watch=false');

/** Does this peeled segment wait on something? */
function isWait(words) {
  const [cmd, sub, third] = words;
  if (cmd === 'sleep' || cmd === 'watch') return true;
  if (cmd !== 'gh') return false;
  if (sub === 'run' && third === 'watch') return true;
  return sub === 'pr' && third === 'checks' && words.some(isWatchFlag);
}

const startsLoop = segment => /^\s*(until|while)\b/.test(segment);

/** build_test | sleep300 | wait | other_tool. A non-string command is other_tool. `bash -c "..."` is classified by its argument. */
export function classifyCommand(command) {
  if (typeof command !== 'string') return OTHER_TOOL;
  const segments = splitTopLevel(command);
  const peeled = segments.map(s => peel(wordsOf(s)));
  const inner = peeled.map(shellCommandOf).filter(c => c !== null).map(classifyCommand);
  if (peeled.length === 1 && inner.length === 1) return inner[0];
  if (peeled.some(isBuildTest) || inner.includes(BUILD_TEST)) return BUILD_TEST;
  if (command.trim() === LITERAL_SLEEP) return SLEEP_300;
  const inLoop = segments.some(startsLoop) && /\bsleep\b/.test(command);
  return peeled.some(isWait) || inLoop || inner.some(c => c === WAIT || c === SLEEP_300) ? WAIT : OTHER_TOOL;
}

/**
 * The category of one tool call. Agent/Task are containers (their time is their children's, never counted). A background
 * Bash call returns at launch, so its Pre->Post span is not the command's run time: it is its own, unmeasured category.
 */
export function classifyTool(name, input) {
  if (CONTAINER_TOOLS.has(name)) return CONTAINER;
  if (typeof name === 'string' && name.startsWith(COAI_PREFIX)) {
    const tool = name.slice(COAI_PREFIX.length);
    if (GATE_TOOLS.has(tool)) return COAI_GATE;
    return CONSULT_TOOLS.has(tool) ? CONSULTANT : OTHER_TOOL;
  }
  if (name !== 'Bash') return OTHER_TOOL;
  return input?.run_in_background === true ? BACKGROUND : classifyCommand(input?.command);
}
