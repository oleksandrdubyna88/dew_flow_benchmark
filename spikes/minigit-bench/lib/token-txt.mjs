// The session's own `token.txt` (what the agent persisted of its credential) as a fact the smoke can keep without the token. The file is
// agent-written, so it is read through lib/agent-files.mjs: a link is reported as unreadable and its target is never read or hashed.
import { createHash } from 'node:crypto';
import path from 'node:path';
import { readAgentFileUnder } from './agent-files.mjs';

/** The probe writes the environ line as `CLAUDE_CODE_OAUTH_TOKEN=<value>`; the value is what is compared. */
const KEY_PREFIX = /^CLAUDE_CODE_OAUTH_TOKEN=/;
const sha256 = text => createHash('sha256').update(text).digest('hex');

/** `{ present: false, equalsDummy: false } | { present: true, unreadable, equalsDummy: false } | { present: true, equalsDummy, bytes, sha256, dummySha256 }`. */
export function tokenTxtFact(workDir, dummy) {
  const read = readAgentFileUnder(workDir, 'token.txt', 1024 * 1024);
  if (read === null) return { present: false, equalsDummy: false };
  if (read.unreadable) return { present: true, unreadable: read.unreadable, equalsDummy: false };
  const text = read.toString('utf8');
  return { present: true, equalsDummy: text.trim().replace(KEY_PREFIX, '') === dummy, bytes: text.length, sha256: sha256(text.trim()), dummySha256: sha256(dummy) };
}
