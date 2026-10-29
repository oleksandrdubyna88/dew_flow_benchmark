import fs from 'node:fs';
import path from 'node:path';

export const DROP = Object.freeze([/^CLAUDE/i, /^ANTHROPIC_/i, /^MCP_/i, /^OTEL_/i, /^TRACEPARENT$/i, /^AI_AGENT$/i, /^SUBAGENT_MODEL_GUARD$/i,
  /^GH_TOKEN$/i, /^GITHUB_TOKEN$/i, /^GH_ENTERPRISE_TOKEN$/i]);
// Allowed back when present. ANTHROPIC_API_KEY / ANTHROPIC_AUTH_TOKEN are authentication, not model selection:
// dropping them would break an API-key login, so they are the one deviation from the ^ANTHROPIC_ deny.
export const KEEP = new Set(['CLAUDE_CODE_GIT_BASH_PATH', 'CLAUDE_CODE_USE_POWERSHELL_TOOL', 'CLAUDE_CONFIG_DIR', 'ANTHROPIC_API_KEY', 'ANTHROPIC_AUTH_TOKEN']);

/** The git identity and prompts every child gets, fixed and never inherited. `GIT_CONFIG_GLOBAL` stays a parameter of the caller. */
export const FIXED_GIT = Object.freeze({
  GIT_CONFIG_NOSYSTEM: '1',
  GIT_TERMINAL_PROMPT: '0',
  GIT_AUTHOR_NAME: 'bench', GIT_AUTHOR_EMAIL: 'bench@invalid',
  GIT_COMMITTER_NAME: 'bench', GIT_COMMITTER_EMAIL: 'bench@invalid',
});

/** Pure: a copy of `parent` without session, model-selection, telemetry and token variables (`KEEP` is allowed back). */
export const scrubEnv = parent => Object.fromEntries(Object.entries(parent).filter(([k]) => KEEP.has(k) || !DROP.some(re => re.test(k))));

/** Pure: PATH value without directories that `hasGh(dir)` flags. */
export function filterPath(pathValue, hasGh) {
  return pathValue.split(path.delimiter).filter(dir => dir !== '' && !hasGh(dir)).join(path.delimiter);
}

const dirHasGh = dir => ['gh.exe', 'gh'].some(n => { try { return fs.statSync(path.join(dir, n)).isFile(); } catch { return false; } });

/**
 * Pure (hasGh injected): the child environment of one cell. IDENTICAL for both arms (`arm` is accepted and ignored): a variable
 * present in one arm only would tell the model which arm it is in. The arm A tree wires no guard hook, and `--setting-sources project`
 * keeps a user-level guard (~/.claude) out, so no opt-out variable is needed.
 */
export function buildChildEnv(parent, { emptyGitConfig, hasGh = dirHasGh }) {
  const kept = scrubEnv(parent);
  const pathKey = Object.keys(kept).find(k => k.toLowerCase() === 'path');
  const filtered = pathKey ? { [pathKey]: filterPath(kept[pathKey], hasGh) } : {};
  return {
    ...kept,
    ...filtered,
    CLAUDE_CODE_DISABLE_BACKGROUND_TASKS: '1',
    // Fixed, never inherited: measured 2026-10-14, without an entrypoint the CLI's own command checks fail on this
    // machine (Bash "requires approval", PowerShell "The command line is too long") and no cell can build or test.
    CLAUDE_CODE_ENTRYPOINT: 'claude-vscode',
    GIT_CONFIG_GLOBAL: emptyGitConfig,
    ...FIXED_GIT,
  };
}

/** Pure: the claude argv (the prompt goes on stdin). `hookEvents` adds the stream's hook lifecycle events (resolver layout: the SessionStart output is a signal). */
export function buildClaudeArgs({ settingsPath, mcpPath, cap, hookEvents = false }) {
  return ['-p', '--model', 'opus', '--output-format', 'stream-json', '--verbose',
    '--setting-sources', 'project', '--settings', settingsPath,
    '--strict-mcp-config', '--mcp-config', mcpPath,
    '--no-session-persistence', '--forward-subagent-text', '--max-budget-usd', String(cap),
    ...(hookEvents ? ['--include-hook-events'] : []),
    '--tools', 'Task,Bash,PowerShell,Read,Edit,Write,Glob,Grep,TodoWrite'];
}
