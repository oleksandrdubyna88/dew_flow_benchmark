#!/usr/bin/env node
// The capture's hook: stamps each tool event with the local clock so the audit has a `tool_use_id` to pair by.
// Appends { t, hook_event_name, tool_name, tool_use_id, agent_id, session_id } to the file named on argv. Exits 0 whatever happened:
// a hook that fails would change the session it is measuring. An unreadable payload becomes a line of nulls.
import { appendFileSync } from 'node:fs';

const FIELDS = ['hook_event_name', 'tool_name', 'tool_use_id', 'agent_id', 'session_id'];

function read() {
  const chunks = [];
  return new Promise(resolve => {
    process.stdin.on('data', c => chunks.push(c));
    process.stdin.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    process.stdin.on('error', () => resolve(''));
  });
}

function payloadOf(text) {
  try {
    const doc = JSON.parse(text);
    return doc && typeof doc === 'object' ? doc : {};
  } catch {
    return {};
  }
}

const doc = payloadOf(await read());
const line = { t: Date.now(), ...Object.fromEntries(FIELDS.map(k => [k, typeof doc[k] === 'string' ? doc[k] : null])) };
try {
  if (process.argv[2]) appendFileSync(process.argv[2], `${JSON.stringify(line)}\n`);
} catch (e) {
  process.stderr.write(`stamp-hook: ${e.message}\n`);
}
process.exit(0);
