import fs from 'node:fs';
import path from 'node:path';

const DROP = [/^CLAUDE/i, /^ANTHROPIC_/i, /^MCP_/i, /^OTEL_/i, /^TRACEPARENT$/i, /^AI_AGENT$/i, /^SUBAGENT_MODEL_GUARD$/i,
  /^GH_TOKEN$/i, /^GITHUB_TOKEN$/i, /^GH_ENTERPRISE_TOKEN$/i];
// Allowed back when present. ANTHROPIC_API_KEY / ANTHROPIC_AUTH_TOKEN are authentication, not model selection:
// dropping them would break an API-key login, so they are the one deviation from the ^ANTHROPIC_ deny.
const KEEP = new Set(['CLAUDE_CODE_GIT_BASH_PATH', 'CLAUDE_CODE_USE_POWERSHELL_TOOL', 'CLAUDE_CONFIG_DIR', 'ANTHROPIC_API_KEY', 'ANTHROPIC_AUTH_TOKEN']);

/** Pure: PATH value without directories that `hasGh(dir)` flags. */
export function filterPath(pathValue, hasGh) {
  return pathValue.split(path.delimiter).filter(dir => dir !== '' && !hasGh(dir)).join(path.delimiter);
}

const dirHasGh = dir => ['gh.exe', 'gh'].some(n => { try { return fs.statSync(path.join(dir, n)).isFile(); } catch { return false; } });

/** Pure (hasGh injected): the child environment of one cell. */
export function buildChildEnv(parent, { arm, emptyGitConfig, hasGh = dirHasGh }) {
  const kept = Object.fromEntries(Object.entries(parent).filter(([k]) => KEEP.has(k) || !DROP.some(re => re.test(k))));
  const pathKey = Object.keys(kept).find(k => k.toLowerCase() === 'path');
  const filtered = pathKey ? { [pathKey]: filterPath(kept[pathKey], hasGh) } : {};
  return {
    ...kept,
    ...filtered,
    CLAUDE_CODE_DISABLE_BACKGROUND_TASKS: '1',
    GIT_CONFIG_GLOBAL: emptyGitConfig,
    GIT_CONFIG_NOSYSTEM: '1',
    GIT_TERMINAL_PROMPT: '0',
    GIT_AUTHOR_NAME: 'bench', GIT_AUTHOR_EMAIL: 'bench@invalid',
    GIT_COMMITTER_NAME: 'bench', GIT_COMMITTER_EMAIL: 'bench@invalid',
    ...(arm === 'A' ? { SUBAGENT_MODEL_GUARD: 'off' } : {}),
  };
}

/** Pure: the claude argv (the prompt goes on stdin). */
export function buildClaudeArgs({ settingsPath, mcpPath, cap }) {
  return ['-p', '--model', 'opus', '--output-format', 'stream-json', '--verbose',
    '--setting-sources', 'project', '--settings', settingsPath,
    '--strict-mcp-config', '--mcp-config', mcpPath,
    '--no-session-persistence', '--forward-subagent-text', '--max-budget-usd', String(cap),
    '--tools', 'Task,Bash,PowerShell,Read,Edit,Write,Glob,Grep,TodoWrite'];
}
