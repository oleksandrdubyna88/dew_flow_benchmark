// The secret shapes a push must not carry (consult D2), by name. A finding says the shape's name, the path and the line - never the
// matched text, not even a prefix: the report that lists the findings is itself public.
//
// The list is this check's own, not v1's `token-scan.mjs`: that one is built to MASK what a session wrote and has a fine-grained
// token only from 60 characters on, no cloud key id and no private key block. Here a shape is a reason to refuse a push, so the
// fine-grained token is held to the consult's `{20,}` and the two other families are added.
import { lineIndex } from './push-text.mjs';

export const SECRET_SHAPES = Object.freeze([
  // base62 and random: a digit and a capital are always among 20 characters, a snake_case name made of words has neither
  { name: 'github-fine-grained-pat', re: /github_pat_(?=[A-Za-z0-9_]*[0-9])(?=[A-Za-z0-9_]*[A-Z])[A-Za-z0-9_]{20,}/g },
  { name: 'github-classic-token', re: /ghp_[A-Za-z0-9]{36}/g },
  { name: 'github-oauth-token', re: /gho_[A-Za-z0-9]{36}/g },
  { name: 'github-server-token', re: /ghs_[A-Za-z0-9]{36}/g },
  { name: 'anthropic-api-key', re: /sk-ant-[A-Za-z0-9_-]{20,}/g },
  { name: 'private-key-block', re: /BEGIN (?:OPENSSH|RSA|EC) PRIVATE KEY/g },
  { name: 'aws-access-key-id', re: /AKIA[0-9A-Z]{16}/g },
]);

/** `[{ path, line, what }]` for every shape found in `text`, in order of position. */
export function findSecrets(path, text, shapes = SECRET_SHAPES) {
  const lineOf = lineIndex(text);
  return shapes
    .flatMap(({ name, re }) => [...text.matchAll(re)].map(m => ({ path, line: lineOf(m.index), what: name, at: m.index })))
    .sort((a, b) => a.at - b.at)
    .map(({ at, ...finding }) => finding);
}
