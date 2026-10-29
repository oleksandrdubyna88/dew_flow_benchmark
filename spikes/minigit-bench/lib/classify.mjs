// Which time category a tool call belongs to (parent §10, E1.S4 step 3). Pure string work, no process is run.
//
// A command is split at the top level of its quoting (`&&`, `||`, `;`, `|`, newline); each segment has its leading
// shell keywords, env assignments and wrappers (`time`, `nice`, `timeout`, `npx`, the mount's low-priority launcher)
// peeled; the first rule that matches ANY segment decides, in the order build_test > sleep300 > wait > other_tool.
// Known limit: a heredoc body or a quoted script is not parsed, so `cat <<EOF` text holding a build line reads as that line.
// `wait` is the generic wait: E3's GitHub intervals later turn it into ci_wait / coderabbit_wait (spans.mjs refineWaits).
import { BUILD_TEST, COAI_GATE, CONSULTANT, CONTAINER, OTHER_TOOL, SLEEP_300, WAIT } from './categories.mjs';

const LITERAL_SLEEP = 'sleep 300';
const GATE_TOOLS = new Set(['review_plan', 'review_code', 'review_document', 'review_feature', 'resolve']);
const CONSULT_TOOLS = new Set(['consult', 'ask_consultants', 'close_consult']);
const COAI_PREFIX = 'mcp__coai__';
const CONTAINER_TOOLS = new Set(['Agent', 'Task']);
const DOTNET_WORK = new Set(['build', 'restore', 'run', 'test', 'format']);
const NPM_RUN_WORK = new Set(['test', 'typecheck', 'lint']);
const KEYWORDS = /^(?:(?:do|then|else|elif|time|!)\s+|[({]+\s*)+/;
const ENV_ASSIGNMENT = /^[A-Za-z_][A-Za-z0-9_]*=/;

/** Split at `&&`, `||`, `;`, `|` and newlines that are outside quotes. */
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
    if (ch === ';' || ch === '|' || ch === '\n') { segments.push(current); current = ''; continue; }
    current += ch;
  }
  segments.push(current);
  return segments;
}

const wordsOf = segment => (segment.trim().replace(KEYWORDS, '').replace(/[)}]+\s*$/, '').match(/"[^"]*"|'[^']*'|\S+/g) ?? []);
const isLauncher = word => /(^|\/)low-priority\.mjs$/.test(word);

/** Drop env assignments and wrappers from the front of a word list: `FOO=1 nice -n 19 dotnet build` -> `dotnet build`. */
function peel(words) {
  let w = words;
  for (;;) {
    const [head, ...rest] = w;
    if (head === undefined) return w;
    if (ENV_ASSIGNMENT.test(head) || ['time', 'env', 'command', 'exec', 'npx'].includes(head)) { w = rest; continue; }
    if (head === 'nice') { w = rest[0]?.startsWith('-') ? rest.slice(rest[0] === '-n' ? 2 : 1) : rest; continue; }
    if (head === 'timeout') { const flagless = rest.filter(x => !x.startsWith('-')); w = flagless.length > 0 ? rest.slice(rest.indexOf(flagless[0]) + 1) : []; continue; }
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

const isHarnessScript = word => /(^|\/)test-v[^/]*\.sh$/.test(word ?? '');

/** Does this peeled segment build or test? */
function isBuildTest(words) {
  const [cmd, ...rest] = words;
  if (cmd === undefined) return false;
  if (cmd === 'dotnet') return DOTNET_WORK.has(rest[0]);
  if (cmd === 'npm') return npmIsWork(rest);
  if (cmd === 'node') return nodeRunsTests(rest);
  if (cmd === 'bash' || cmd === 'sh') return isHarnessScript(rest[0]);
  if (cmd === 'make' || cmd === 'tsc') return true;
  return isHarnessScript(cmd) || /\.Tests(\.exe)?$/i.test(cmd);
}

/** Does this peeled segment wait on something? */
function isWait(words) {
  const [cmd, sub, third] = words;
  if (cmd === 'sleep') return true;
  if (cmd !== 'gh') return false;
  if (sub === 'run' && third === 'watch') return true;
  return sub === 'pr' && third === 'checks' && words.includes('--watch');
}

const startsLoop = segment => /^\s*(until|while)\b/.test(segment);

/** build_test | sleep300 | wait | other_tool. A non-string command is other_tool. */
export function classifyCommand(command) {
  if (typeof command !== 'string') return OTHER_TOOL;
  const segments = splitTopLevel(command);
  const peeled = segments.map(s => peel(wordsOf(s)));
  if (peeled.some(isBuildTest)) return BUILD_TEST;
  if (command.trim() === LITERAL_SLEEP) return SLEEP_300;
  if (peeled.some(isWait) || (segments.some(startsLoop) && /\bsleep\b/.test(command))) return WAIT;
  return OTHER_TOOL;
}

/** The category of one tool call. Agent/Task are containers (their time is their children's, never counted). */
export function classifyTool(name, input) {
  if (CONTAINER_TOOLS.has(name)) return CONTAINER;
  if (typeof name === 'string' && name.startsWith(COAI_PREFIX)) {
    const tool = name.slice(COAI_PREFIX.length);
    if (GATE_TOOLS.has(tool)) return COAI_GATE;
    return CONSULT_TOOLS.has(tool) ? CONSULTANT : OTHER_TOOL;
  }
  return name === 'Bash' ? classifyCommand(input?.command) : OTHER_TOOL;
}
