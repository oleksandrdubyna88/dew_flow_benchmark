#!/usr/bin/env node
/**
 * Observe-only audit hook (policy A/B pilot). PreToolUse (every tool) and InstructionsLoaded:
 * appends ONE JSON line to <tree>/.git/policy-audit.ndjson. Prints nothing, exits 0, never blocks,
 * reads no environment variable. The log path comes from this script's own location.
 */
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const STDIN_LIMIT = 1024 * 1024;
const STDIN_DEADLINE_MS = 2000;
const countLines = text => (typeof text === 'string' && text !== '' ? text.split('\n').length : 0);

/** Pure: the tool-input summary for one payload. */
export function inputSummary(toolName, input) {
  const i = input && typeof input === 'object' ? input : {};
  switch (toolName) {
    case 'Bash':
    case 'PowerShell': return { command: i.command ?? '' };
    case 'Read': return { file_path: i.file_path ?? '' };
    case 'Edit': return { file_path: i.file_path ?? '', added: countLines(i.new_string), removed: countLines(i.old_string) };
    case 'Write': return { file_path: i.file_path ?? '', added: countLines(i.content), removed: 0 };
    case 'Agent':
    case 'Task': return { model: i.model ?? null, effort: i.effort ?? null, subagent_type: i.subagent_type ?? null, description: i.description ?? '' };
    case 'Glob':
    case 'Grep': return { pattern: i.pattern ?? '', path: i.path ?? '' };
    default: return {};
  }
}

/** Pure: the audit record. `sha256Of(path)` is injected (null when unreadable). */
export function auditRecord(payload, nowIso, sha256Of) {
  const event = payload.hook_event_name ?? '';
  const record = {
    ts: nowIso,
    event,
    agent_id: payload.agent_id ?? null,
    agent_type: payload.agent_type ?? null,
    tool_name: payload.tool_name ?? null,
    effort: payload.effort ?? null,
    file_path: payload.file_path ?? null,
    memory_type: payload.memory_type ?? null,
    load_reason: payload.load_reason ?? null,
    sha256: null,
    input: inputSummary(payload.tool_name, payload.tool_input),
  };
  if (event === 'InstructionsLoaded' && record.file_path) record.sha256 = sha256Of(record.file_path);
  return record;
}

const sha256File = file => {
  try { return createHash('sha256').update(fs.readFileSync(file)).digest('hex'); } catch { return null; }
};

async function readStdin() {
  const chunks = [];
  let size = 0;
  const read = (async () => {
    for await (const chunk of process.stdin) {
      chunks.push(chunk);
      size += chunk.length;
      if (size > STDIN_LIMIT) break;
    }
  })();
  let timer;
  await Promise.race([read, new Promise(resolve => { timer = setTimeout(resolve, STDIN_DEADLINE_MS); })]);
  clearTimeout(timer);
  return Buffer.concat(chunks).toString('utf8');
}

if (process.argv[1]?.endsWith('policy-audit.mjs')) {
  try {
    const payload = JSON.parse(await readStdin());
    const logPath = fileURLToPath(new URL('../../.git/policy-audit.ndjson', import.meta.url));
    fs.appendFileSync(logPath, `${JSON.stringify(auditRecord(payload, new Date().toISOString(), sha256File))}\n`);
  } catch { /* observe-only: never fail the tool call */ }
  process.exit(0);
}
