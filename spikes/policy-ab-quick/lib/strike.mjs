// Pure: strike the model lines named in inputs/statement.md out of the plan text.
const BT = /`([^`]+)`/g;

/**
 * One {find, replace} edit per numbered item of the "Struck" section. An item with two spans where
 * one contains the other (suffix + original heading) edits the outer span; otherwise each span is removed.
 */
export function parseStruck(statementMd) {
  const at = statementMd.search(/\*\*Struck/i);
  if (at < 0) return [];
  const items = statementMd.slice(at).split('\n').filter(l => /^\d+\.\s/.test(l));
  return items.flatMap(line => itemEdits([...line.split('(removed')[0].matchAll(BT)].map(m => m[1]).filter(s => s.trim() !== '')));
}

function itemEdits(spans) {
  const inner = spans.filter(s => !spans.some(o => o !== s && s.includes(o)));
  const outer = spans.filter(s => spans.some(o => o !== s && s.includes(o)));
  if (outer.length === 0) return inner.map(find => ({ find, replace: '' }));
  return outer.map(find => ({ find, replace: inner.reduce((t, i) => t.replace(i, ''), find) }));
}

const escapeRe = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** Apply the edits (whitespace-tolerant). Returns the new text and the finds that matched nothing. */
export function strikeTargets(planText, edits) {
  let text = planText;
  const notFound = [];
  for (const { find, replace } of edits) {
    const body = find.trim().split(/\s+/).map(escapeRe).join('\\s+');
    const re = new RegExp(`${/^\s/.test(find) ? ' ?' : ''}${body}[ \\t]*`);
    if (re.test(text)) text = text.replace(re, () => (replace === '' ? '' : replace));
    else notFound.push(find);
  }
  return { text, notFound };
}

const MODEL_WORD = /\b(opus|sonnet|fable|haiku)\b/gi;
export const WITHHELD = '[model withheld]';

/** Replace every whole-word model name; the count goes into cell.json. */
export function withholdModels(text) {
  let count = 0;
  const out = text.replace(MODEL_WORD, () => { count += 1; return WITHHELD; });
  return { text: out, count };
}

export const remainingModels = text => [...text.matchAll(MODEL_WORD)].map(m => m[0]);
