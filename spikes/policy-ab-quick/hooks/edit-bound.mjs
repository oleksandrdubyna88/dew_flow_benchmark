#!/usr/bin/env node
/** Per-request bound on the main session's edits. Thin I/O around edit-bound-core.mjs; fails open. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { decide } from './edit-bound-core.mjs';

const SCRIPT_ROOT = fileURLToPath(new URL('../../', import.meta.url));

async function readStdin() {
  const chunks = [];
  let size = 0;
  const read = (async () => {
    for await (const chunk of process.stdin) {
      chunks.push(chunk);
      size += chunk.length;
      if (size > 1024 * 1024) break;
    }
  })();
  let timer;
  await Promise.race([read, new Promise(resolve => { timer = setTimeout(resolve, 2000); })]);
  clearTimeout(timer);
  return Buffer.concat(chunks).toString('utf8');
}

const existingLines = file => { try { return fs.readFileSync(file, 'utf8').split('\n').length; } catch { return 0; } };

function gitDir(root) {
  const dotGit = path.join(root, '.git');
  if (!fs.statSync(dotGit).isFile()) return dotGit;
  const m = /^gitdir:\s*(.+)$/m.exec(fs.readFileSync(dotGit, 'utf8'));
  return m ? path.resolve(root, m[1].trim()) : dotGit;
}

const readState = file => { try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return {}; } };

function respond(payload) {
  const root = process.env.CLAUDE_PROJECT_DIR || payload.cwd || SCRIPT_ROOT;
  const stateFile = path.join(gitDir(SCRIPT_ROOT), 'edit-bound.json');
  const verdict = decide({
    event: payload.hook_event_name, tool: payload.tool_name, input: payload.tool_input, agentId: payload.agent_id,
    sessionId: payload.session_id ?? 'main', root, state: readState(stateFile), existingLinesOf: existingLines,
  });
  if (verdict.action === 'reset') { fs.rmSync(stateFile, { force: true }); return ''; }
  if (verdict.action === 'allow') { fs.writeFileSync(stateFile, JSON.stringify(verdict.state)); return ''; }
  return `${JSON.stringify({ hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: 'deny', permissionDecisionReason: verdict.reason } })}\n`;
}

if (process.argv[1]?.endsWith('edit-bound.mjs')) {
  let out = '';
  try { out = respond(JSON.parse(await readStdin())); } catch { out = ''; }
  if (out === '') process.exit(0);
  process.stdout.on('error', () => process.exit(0));
  process.stdout.write(out, () => process.exit(0));
}
