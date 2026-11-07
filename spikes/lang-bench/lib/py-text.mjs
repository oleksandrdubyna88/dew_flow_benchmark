// The comments and the docstring-like strings of a Python source, read without Python (the push check is plain Node and reads one file's text at a time). It is a small lexer, not a parser: comments, string
// literals (single and triple quoted, with their prefixes and escapes) and bracket depth. A string that is a whole statement of its own (first on its line at depth 0, and nothing but space or a comment after it)
// is "docstring-like": every docstring is one, and so is a bare string statement, which is the same kind of prose. A test holds it against Python's own `tokenize` and `ast` on every file of the runner.
const PREFIX = /^(?:[rR][bBfF]?|[bBfF][rR]?|[uU])?$/;
const START = /([A-Za-z]{0,2})("""|'''|"|')/y;
const WORD = /[A-Za-z0-9_\u0080-￿]+/y;
const REST_OF_LINE = /[ \t\r\f]*(?:\n|#|$)/y;
const QUOTES = ["'", '"'];
/** The body of a string up to its closer, by quote kind: an escaped character never ends it, a single-quoted one also ends before a newline (it is unterminated), a triple-quoted one at the end of the text. */
const BODY = Object.fromEntries(QUOTES.flatMap(q => [
  [q, new RegExp(`(?:[^\\\\${q}\\n]|\\\\[\\s\\S])*(?:${q}|(?=\\n)|$)`, 'y')],
  [q.repeat(3), new RegExp(`(?:[^\\\\]|\\\\[\\s\\S])*?(?:${q.repeat(3)}|$)`, 'y')],
]));
const isSpace = ch => ' \t\r\f'.includes(ch);
const countLines = text => (text.match(/\n/g) ?? []).length;

/** The sticky `re` matched at `at` in `text`: the match, or null. */
function matchAt(re, text, at) {
  re.lastIndex = at;
  return re.exec(text);
}

/** `{ quote, bodyFrom }` when a string literal (with an optional prefix) starts at `i`, else null. */
function stringStart(text, i) {
  const found = matchAt(START, text, i);
  return found !== null && PREFIX.test(found[1]) ? { quote: found[2], bodyFrom: i + found[0].length } : null;
}

/** `{ end, body, lines }` of the string whose body starts at `from`: where it stops, its raw body and the newlines it spans. */
function stringEnd(text, from, closer) {
  const matched = matchAt(BODY[closer], text, from)[0];
  const closed = matched.endsWith(closer) && matched.length >= closer.length;
  return { end: from + matched.length, body: closed ? matched.slice(0, -closer.length) : matched, lines: countLines(matched) };
}

const newline = (text, state) => { state.line += 1; state.atStart = state.depth === 0; state.i += 1; };
const continuation = (text, state) => { state.line += 1; state.atStart = false; state.i += 2; };
const skipOne = (text, state) => { state.i += 1; };

function readComment(text, state, out) {
  const end = text.indexOf('\n', state.i);
  const stop = end === -1 ? text.length : end;
  out.comments.push({ line: state.line, text: text.slice(state.i, stop).replace(/\r$/, '') });
  state.i = stop;
}

function readString(text, state, out) {
  const started = stringStart(text, state.i);
  const alone = state.atStart && state.depth === 0;
  const read = stringEnd(text, started.bodyFrom, started.quote);
  if (alone && matchAt(REST_OF_LINE, text, read.end) !== null) out.docstrings.push({ line: state.line, text: read.body });
  state.line += read.lines;
  state.atStart = false;
  state.i = read.end;
}

function readWord(text, state) {
  state.i += matchAt(WORD, text, state.i)[0].length;
  state.atStart = false;
}

function readOther(text, state) {
  const ch = text[state.i];
  state.depth = Math.max(0, state.depth + Number('([{'.includes(ch)) - Number(')]}'.includes(ch)));
  state.atStart = false;
  state.i += 1;
}

/** The first rule that applies to the character at `state.i` decides how it is read. */
const RULES = Object.freeze([
  [(text, state) => text[state.i] === '\n', newline],
  [(text, state) => text[state.i] === '\\' && text[state.i + 1] === '\n', continuation],
  [(text, state) => isSpace(text[state.i]), skipOne],
  [(text, state) => text[state.i] === '#', readComment],
  [(text, state) => stringStart(text, state.i) !== null, readString],
  [(text, state) => matchAt(WORD, text, state.i) !== null, readWord],
  [() => true, readOther],
]);

/** `{ comments: [{ line, text }], docstrings: [{ line, text }] }`: the comments (with their `#`) and the docstring-like strings (their raw body), each at its starting line. */
export function pythonTextItems(text) {
  const out = { comments: [], docstrings: [] };
  const state = { i: 0, line: 1, depth: 0, atStart: true };
  while (state.i < text.length) RULES.find(([applies]) => applies(text, state))[1](text, state, out);
  return out;
}
