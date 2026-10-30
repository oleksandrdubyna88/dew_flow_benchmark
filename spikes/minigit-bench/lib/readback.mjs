// The init read-back and the session's outcome (E2.S2 D-13). Pure. The read-back compares what the CLI REPORTED to what was asked for;
// the outcome is the envelope's word, never the exit code (a 403 from the broker exits 0 with `is_error: true`).
import { forbiddenIn } from './launch.mjs';
import { INIT_UNREPORTED } from './sandbox.mjs';

const canonicalTool = name => (name === 'Task' ? 'Agent' : name);
const toolNames = tools => (tools ?? []).map(t => canonicalTool(typeof t === 'string' ? t : t?.name));
const mismatch = (field, expected, observed) => ({ field, expected, observed });
const same = (field, expected, observed) => (expected === observed ? [] : [mismatch(field, expected, observed)]);

function toolMismatches(init, expectedTools) {
  const wanted = new Set(expectedTools.map(canonicalTool));
  const reported = new Set(toolNames(init.tools));
  const missing = [...wanted].filter(t => !reported.has(t) && !INIT_UNREPORTED.includes(t));
  const extra = [...reported].filter(t => !wanted.has(t));
  return [...missing.map(t => mismatch('tools', t, 'absent')), ...extra.map(t => mismatch('tools', 'absent', t))];
}

const underDir = (p, dir) => typeof p === 'string' && (p === dir || p.startsWith(`${dir.replace(/\/$/, '')}/`));

/** `{ ok: true } | { ok: false, mismatches: [{ field, expected, observed }] }`. Run on the init event, before the first paid turn completes. */
export function readBackInit(init, expected) {
  const mismatches = [
    ...same('cwd', expected.cwd, init.cwd),
    ...toolMismatches(init, expected.tools),
    ...same('mcp_servers', expected.mcpServers, (init.mcp_servers ?? []).length),
    ...same('permissionMode', 'default', init.permissionMode),
    ...same('claude_code_version', expected.cliVersion, init.claude_code_version),
    ...same('apiKeySource', 'none', init.apiKeySource),
    ...(underDir(init.memory_paths?.auto, expected.configDir) ? [] : [mismatch('memory_paths.auto', `under ${expected.configDir}`, init.memory_paths?.auto ?? 'absent')]),
    ...(expected.priced(init.model) ? [] : [mismatch('model', 'an id the price table prices', init.model ?? 'absent')]),
    ...forbiddenIn(expected.settings).map(p => mismatch(`settings.${p}`, 'absent', 'present')),
  ];
  return mismatches.length === 0 ? { ok: true } : { ok: false, mismatches };
}

const stopped = exit => exit.signal !== null || exit.code === 143 || exit.code === 137;

/**
 * The outcome of a session from its result envelope and its exit: `{ ok, label, detail }`. The envelope decides; with none the session was
 * stopped (a signal or the stop's own codes) or ended without a result.
 */
export function sessionOutcome(envelope, exit) {
  if (!envelope) return { ok: false, label: stopped(exit) ? 'void: stopped' : 'void: no result', detail: `no result envelope; exit ${JSON.stringify(exit)}` };
  const detail = exit.code === 0 || exit.code === null ? '' : `exit code ${exit.code}`;
  if (envelope.is_error === true && Number.isFinite(envelope.api_error_status)) return { ok: false, label: `void: api-error ${envelope.api_error_status}`, detail };
  if (envelope.is_error === true || envelope.subtype !== 'success') return { ok: false, label: `void: ${envelope.subtype ?? 'error'}`, detail };
  return { ok: true, label: 'ok', detail };
}
