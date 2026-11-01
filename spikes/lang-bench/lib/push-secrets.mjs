// The secret shapes a push must not carry (consult D2), by name. A finding says the shape's name, the path and the line - never the
// matched text, not even a prefix: the report that lists the findings is itself public.
//
// The list is this check's own, not v1's `token-scan.mjs`: that one is built to MASK what a session wrote and has a fine-grained
// token only from 60 characters on, no cloud key id and no private key block. Here a shape is a reason to refuse a push, so the
// fine-grained token is held to the consult's `{20,}` and the two other families are added.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { lineIndex } from './push-text.mjs';

export const SECRET_SHAPES = Object.freeze([
  // base62 and random: a digit and a capital are always among 20 characters, a snake_case name made of words has neither
  { name: 'github-fine-grained-pat', re: /github_pat_(?=[A-Za-z0-9_]*[0-9])(?=[A-Za-z0-9_]*[A-Z])[A-Za-z0-9_]{20,}/g },
  { name: 'github-token', re: /gh[pousr]_[A-Za-z0-9]{36}/g },
  { name: 'anthropic-api-key', re: /sk-ant-[A-Za-z0-9_-]{20,}/g },
  { name: 'private-key-block', re: /BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY/g },
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

const MIN_OWN_LENGTH = 16;

/**
 * ok([{ label, value }]) from the real secret files (`[{ label, text }]`, read by the caller at run time, by path): every line of at
 * least 16 characters that is not a PEM header or footer, because a copied key is copied line by line. A file with no such line would
 * check nothing, so it is a fail naming its label.
 */
export function parseOwnSecrets(files) {
  const values = [];
  for (const { label, text } of files) {
    const own = [...new Set(text.split('\n').map(l => l.trim()).filter(l => l.length >= MIN_OWN_LENGTH && !l.startsWith('-----')))];
    if (own.length === 0) return fail(`the secret file ${label} holds no value of at least ${MIN_OWN_LENGTH} characters: it would check nothing`);
    values.push(...own.map(value => ({ label, value })));
  }
  return ok(values);
}

/** `text` without its blanks, and for each remaining character where it stood: a value broken over a line is found in the copy. */
function withoutBlanks(text) {
  const map = [];
  let stripped = '';
  for (let i = 0; i < text.length; i += 1) if (!/\s/.test(text[i])) { stripped += text[i]; map.push(i); }
  return { stripped, map };
}

/** `[{ path, line, what }]` for every own value found in `text`, as it stands or with line breaks inside it; never the value. */
export function findOwnSecrets(path, text, own) {
  const lineOf = lineIndex(text);
  const { stripped, map } = withoutBlanks(text);
  const found = own.flatMap(({ label, value }) => {
    const plain = text.indexOf(value);
    const broken = plain === -1 ? stripped.indexOf(value.replace(/\s+/g, '')) : -1;
    const at = plain !== -1 ? plain : (broken === -1 ? -1 : map[broken]);
    return at === -1 ? [] : [{ path, line: lineOf(at), what: `own secret value (${label})` }];
  });
  return found.filter((f, i) => found.findIndex(o => o.line === f.line && o.what === f.what) === i).sort((a, b) => a.line - b.line);
}
