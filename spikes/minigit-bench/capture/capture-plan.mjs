// The step 0 capture's plan, PURE: the exact argv, the exact environment, the files written before the launch. Nothing here launches
// anything; `capture-session.mjs run --dry-run` prints it, a test asserts it, and `capture-run.mjs` executes it.
import path from 'node:path';
import { otelEnv } from '../lib/otel-receiver.mjs';
import { fail, ok } from '../lib/result.mjs';

export const CAPTURE_PROMPT = 'In ONE message launch two subagents with the Agent tool: one with `model: "sonnet"` and one with `model: "fable"` '
  + '(subagent_type general-purpose), each told to run `ls` in the current directory and reply with the file names only. '
  + 'When both have returned, run `echo capture-ok` with Bash yourself and reply DONE.';

export const WALL_LIMIT_MS = 10 * 60 * 1000;
export const MAX_BUDGET_USD = '1.5';
const TOOLS = 'Agent,Bash,Read,Glob';
const EFFORT = Object.freeze({ effortLevel: 'high' });

const SAFE = /^[A-Za-z0-9_./:@%+=,-]+$/;
/** A hook command is a shell string run by the CLI, so a path with a space or a quote is single-quoted. */
export const shellQuote = s => (SAFE.test(s) ? s : `'${s.replaceAll("'", "'\\''")}'`);

/** The settings file of the capture (D17). The hook gives the stream its hook_started/hook_response pairs and the audit a tool_use_id. */
export function settingsJson({ spikeDir, out }) {
  const command = `node ${shellQuote(path.join(spikeDir, 'capture', 'stamp-hook.mjs'))} ${shellQuote(path.join(out, 'hooks.ndjson'))}`;
  const hook = { hooks: [{ type: 'command', command }] };
  return {
    effortLevel: EFFORT.effortLevel,
    modelSettings: { opus: { ...EFFORT }, sonnet: { ...EFFORT }, fable: { ...EFFORT } },
    hooks: { PreToolUse: [hook], PostToolUse: [hook] },
  };
}

const json = o => `${JSON.stringify(o, null, 2)}\n`;

/** The CLI argv, as an array: exe + argv, never a shell string. The prompt is NOT in it (stdin). */
export function claudeArgv(out) {
  return [
    '-p', '--model', 'opus', '--output-format', 'stream-json', '--verbose', '--forward-subagent-text', '--include-hook-events',
    '--setting-sources', 'project', '--settings', path.join(out, 'settings.json'), '--strict-mcp-config', '--mcp-config', path.join(out, 'mcp.json'),
    '--max-budget-usd', MAX_BUDGET_USD, '--tools', TOOLS,
  ];
}

/** A fixed allow-list, never the parent's environment. `token` is passed through unread. */
export function claudeEnv({ out, port, token, env }) {
  return {
    PATH: env.PATH, HOME: env.HOME, LANG: 'C.UTF-8', CLAUDE_CODE_OAUTH_TOKEN: token, CLAUDE_CONFIG_DIR: path.join(out, 'config'),
    CLAUDE_CODE_DISABLE_BACKGROUND_TASKS: '1', DISABLE_AUTOUPDATER: '1', ...otelEnv(port),
  };
}

/** `{ ok, value: { exe, argv, cwd, env, envKeys, stdin, timeoutMs, files } } | { ok: false, reason }`. `files` maps out-relative path -> text. */
export function capturePlan({ out, spikeDir, port, token, env }) {
  if (!path.isAbsolute(out)) return fail(`the out dir must be an absolute path, got '${out}'`);
  if (!Number.isInteger(port) || port < 1) return fail('the sink port is not known yet: start the sink before planning the launch');
  if (!env?.PATH || !env?.HOME) return fail('PATH and HOME are both needed to build the launch environment');
  const launchEnv = claudeEnv({ out, port, token, env });
  return ok({
    exe: 'claude', argv: claudeArgv(out), cwd: path.join(out, 'tree'), env: launchEnv, envKeys: Object.keys(launchEnv).sort(),
    stdin: CAPTURE_PROMPT, timeoutMs: WALL_LIMIT_MS,
    files: { 'settings.json': json(settingsJson({ spikeDir, out })), 'mcp.json': json({ mcpServers: {} }), 'tree/a.txt': 'alpha\n', 'tree/b.txt': 'bravo\n' },
  });
}
