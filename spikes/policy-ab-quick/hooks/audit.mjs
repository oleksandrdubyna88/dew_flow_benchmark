#!/usr/bin/env node
/**
 * Observe-only tool log. Appends one JSON line per event to the log, exits 0, never blocks. Prints nothing on stdout; when a record
 * could not be written it says why in one line on stderr.
 */
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';

const STDIN_LIMIT = 1024 * 1024;
const STDIN_DEADLINE_MS = 2000;
const str = v => (typeof v === 'string' ? v : null);
const countLines = text => (typeof text === 'string' && text !== '' ? text.split('\n').length : 0);

/** Pure: the tool-input summary for one payload. */
export function inputSummary(toolName, input) {
  const i = input && typeof input === 'object' ? input : {};
  switch (toolName) {
    case 'Bash': return { command: i.command ?? '', run_in_background: typeof i.run_in_background === 'boolean' ? i.run_in_background : null };
    case 'PowerShell': return { command: i.command ?? '' };
    case 'NotebookEdit': return { file_path: i.notebook_path ?? '', added: countLines(i.new_source), removed: 0 };
    case 'Read': return { file_path: i.file_path ?? '' };
    case 'Edit': return { file_path: i.file_path ?? '', added: countLines(i.new_string), removed: countLines(i.old_string) };
    case 'Write': return { file_path: i.file_path ?? '', added: countLines(i.content), removed: 0 };
    case 'MultiEdit': {
      const edits = Array.isArray(i.edits) ? i.edits : [];
      return { file_path: i.file_path ?? '', added: edits.reduce((n, e) => n + countLines(e?.new_string), 0), removed: edits.reduce((n, e) => n + countLines(e?.old_string), 0) };
    }
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
    tool_use_id: str(payload.tool_use_id),
    session_id: str(payload.session_id),
    duration_ms: Number.isFinite(payload.duration_ms) ? payload.duration_ms : null,
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

/** Pure-ish: the git dir for a tree root. `.git` may be a FILE (`gitdir: <path>`) in a worktree child. */
export function gitDirFor(root, fsApi = fs) {
  const dotGit = path.join(root, '.git');
  if (!fsApi.statSync(dotGit).isFile()) return dotGit;
  const m = /^gitdir:\s*(.+)$/m.exec(fsApi.readFileSync(dotGit, 'utf8'));
  return m ? path.resolve(root, m[1].trim()) : dotGit;
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

/** Pure: the log file, from `--out <absolute file>` (strict: an unknown option or a relative path throws) or the tree's own git dir when absent. */
export function logPathFor(argv, fallbackDir) {
  const { values } = parseArgs({ args: argv, options: { out: { type: 'string' } }, strict: true, allowPositionals: false });
  if (values.out === undefined) return path.join(fallbackDir(), 'audit.ndjson');
  if (!path.isAbsolute(values.out)) throw new Error(`--out must be an absolute path, got '${values.out}'`); // a relative one would depend on the hook's cwd
  return values.out;
}

if (process.argv[1]?.endsWith('audit.mjs')) {
  try {
    const payload = JSON.parse(await readStdin());
    const root = fileURLToPath(new URL('../../', import.meta.url));
    const logPath = logPathFor(process.argv.slice(2), () => gitDirFor(root));
    fs.appendFileSync(logPath, `${JSON.stringify(auditRecord(payload, new Date().toISOString(), sha256File))}\n`);
  } catch (e) {
    // observe-only: never fail the tool call, but say why this event left no line
    process.stderr.write(`audit: no record written: ${e.message}\n`);
  }
  process.exit(0);
}
