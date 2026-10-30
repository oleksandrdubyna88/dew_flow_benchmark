// What is launched inside the box (E2.S2 D-11, D-12): the CLI's argv, the settings file and the MCP config, per arm. Pure.
// Settings pin effort, allow an explicit tool list (nobody answers a prompt in `-p`), wire the audit hook to the harness's copy, and NEVER
// carry `apiKeyHelper` (the CLI would run it inside the box, where Bash can run it too: it would hand the credential to the session) or a
// `defaultMode`.
import { PINNED_EFFORT } from '../../policy-ab-quick/lib/settings.mjs';
import { shellQuote } from '../capture/capture-plan.mjs';
import { fail, ok } from './result.mjs';

export const BARE_TOOLS = Object.freeze(['Agent', 'Bash', 'Read', 'Edit', 'Write', 'Glob', 'Grep', 'TodoWrite']);
/** `Agent` and `Task` are one tool under two names (the init event reports `Task`). */
export const BARE_ALLOW = Object.freeze(['Bash', 'Read', 'Edit', 'Write', 'Glob', 'Grep', 'TodoWrite', 'Agent', 'Task']);
/** E2.S3 joins these for the process arm. */
const PROCESS_EXTRA_ALLOW = Object.freeze(['mcp__coai__*', 'ToolSearch']);
export const HOOK_EVENTS = Object.freeze(['PreToolUse', 'PostToolUse', 'PostToolUseFailure', 'SubagentStart', 'SubagentStop']);
export const FORBIDDEN_SETTINGS = Object.freeze(['apiKeyHelper', 'permissions.defaultMode']);

const ARMS = Object.freeze(['bare', 'process']);

const hookEntry = layout => [{ hooks: [{ type: 'command', command: `node ${shellQuote(layout.files.audit)} --out ${shellQuote(layout.files.auditLog)}` }] }];

/** The settings file of one arm. The hook command points at the harness copy of the audit hook; its log lands in `box/out`. */
export function settingsFor({ arm, layout }) {
  const allow = arm === 'process' ? [...BARE_ALLOW, ...PROCESS_EXTRA_ALLOW] : [...BARE_ALLOW];
  return {
    effortLevel: PINNED_EFFORT.effortLevel,
    modelSettings: structuredClone(PINNED_EFFORT.modelSettings),
    hooks: Object.fromEntries(HOOK_EVENTS.map(event => [event, hookEntry(layout)])),
    permissions: { allow },
  };
}

/** The forbidden settings (dotted paths) a settings object carries; `[]` when it carries none. */
export function forbiddenIn(settings) {
  const at = (obj, dotted) => dotted.split('.').reduce((o, k) => (o !== null && typeof o === 'object' ? o[k] : undefined), obj);
  return FORBIDDEN_SETTINGS.filter(p => at(settings, p) !== undefined);
}

/** `{ ok, value: { mcpServers } } | { ok: false, reason }`. The bare arm lists no server; the process arm's bridge is E2.S3's. */
export function mcpConfigFor(arm) {
  if (arm === 'bare') return ok({ mcpServers: {} });
  if (arm === 'process') return fail('the process arm\'s mcp.json (the coai bridge client) is E2.S3\'s');
  return fail(`unknown arm '${arm}'; legal values: ${ARMS.join(', ')}`);
}

/** The CLI argv, exe + argv: the prompt is NOT in it (stdin), persistence is ON (transcripts are the third view), the cap is the CLI's own stop beside the broker's. */
export function claudeArgv({ layout, cap, tools = BARE_TOOLS }) {
  return Object.freeze([
    '-p', '--model', 'opus', '--output-format', 'stream-json', '--verbose', '--forward-subagent-text', '--include-hook-events',
    '--setting-sources', 'project', '--settings', layout.files.settings, '--strict-mcp-config', '--mcp-config', layout.files.mcp,
    '--max-budget-usd', String(cap), '--tools', tools.join(','),
  ]);
}
