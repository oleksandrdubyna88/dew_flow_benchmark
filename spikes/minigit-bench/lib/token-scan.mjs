// Credential shapes in a session's records. A hit says WHERE (source, offset) and WHAT KIND, never the matched text - not even a
// prefix: the report that lists the hits is itself public. (`scanTokens` in policy-ab-quick prints six characters of a canary;
// it is for canaries and is not used for secrets.)

const SHAPES = Object.freeze([
  { kind: 'gh-classic', re: /ghp_[A-Za-z0-9]{36}/g },
  { kind: 'gh-fine-grained', re: /github_pat_[A-Za-z0-9_]{60,}/g },
  { kind: 'gh-token', re: /gh[ousr]_[A-Za-z0-9]{36}/g },
  { kind: 'anthropic', re: /sk-ant-[A-Za-z0-9_-]{20,}/g },
]);
const MIN_OWN_LENGTH = 8;
export const SECRET_MASK = '[secret]';

const spansOfShape = (text, { kind, re }) => [...text.matchAll(re)].map(m => ({ kind, start: m.index, end: m.index + m[0].length }));

function spansOfValue(text, value) {
  const spans = [];
  for (let at = text.indexOf(value); at !== -1; at = text.indexOf(value, at + value.length)) spans.push({ kind: 'own-value', start: at, end: at + value.length });
  return spans;
}

/** Pure: every secret span of `text`, in order of position, none overlapping an earlier one (a shape wins over an own value at the same place). */
function secretSpans(text, ownSecrets) {
  const own = [...new Set(ownSecrets)].filter(v => typeof v === 'string' && v.length >= MIN_OWN_LENGTH);
  const all = [...SHAPES.flatMap(shape => spansOfShape(text, shape)), ...own.flatMap(v => spansOfValue(text, v))].sort((a, b) => a.start - b.start);
  return all.reduce((kept, span) => (kept.length > 0 && span.start < kept.at(-1).end ? kept : [...kept, span]), []);
}

/** Pure: `texts` is `[{ source, text }]`; returns `[{ source, kind, offset }]` - the offset is a UTF-16 index into that text. */
export function tokenHits(texts = [], ownSecrets = []) {
  return texts.flatMap(({ source, text }) => secretSpans(String(text ?? ''), ownSecrets).map(s => ({ source, kind: s.kind, offset: s.start })));
}

/** Pure: `text` with every secret span replaced by `[secret]` (what a report excerpt may show). */
export function maskTokens(text, ownSecrets = []) {
  const value = String(text ?? '');
  const spans = secretSpans(value, ownSecrets);
  const pieces = spans.map((s, i) => `${value.slice(i === 0 ? 0 : spans[i - 1].end, s.start)}${SECRET_MASK}`);
  return pieces.join('') + value.slice(spans.length === 0 ? 0 : spans.at(-1).end);
}
