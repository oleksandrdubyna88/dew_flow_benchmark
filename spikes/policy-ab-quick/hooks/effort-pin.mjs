#!/usr/bin/env node
/**
 * Effort pin (policy A/B pilot). PreToolUse Agent|Task: deny a launch whose `effort` argument is
 * present and not "high". Every other call is allowed silently. Fails OPEN on any error.
 */
export const DENY_MARKER = 'policy-effort-pin:';

/** Pure: the hook's stdout for one payload ('' = allow). */
export function effortPinOutput(payload) {
  const effort = payload?.tool_input?.effort;
  if (effort === undefined || effort === null || effort === 'high') return '';
  const reason = `${DENY_MARKER} this harness pins every agent to effort "high"; the launch asked for ${JSON.stringify(effort)}. Relaunch without an effort argument.`;
  return `${JSON.stringify({ hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: 'deny', permissionDecisionReason: reason } })}\n`;
}

async function readStdin() {
  const chunks = [];
  let size = 0;
  const read = (async () => {
    for await (const chunk of process.stdin) {
      chunks.push(chunk);
      size += chunk.length;
      if (size > 256 * 1024) break;
    }
  })();
  let timer;
  await Promise.race([read, new Promise(resolve => { timer = setTimeout(resolve, 2000); })]);
  clearTimeout(timer);
  return Buffer.concat(chunks).toString('utf8');
}

if (process.argv[1]?.endsWith('effort-pin.mjs')) {
  let out = '';
  try { out = effortPinOutput(JSON.parse(await readStdin())); } catch { out = ''; }
  if (out === '') process.exit(0);
  process.stdout.on('error', () => process.exit(0));
  process.stdout.write(out, () => process.exit(0));
}
