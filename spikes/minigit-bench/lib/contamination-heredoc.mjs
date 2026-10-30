// A heredoc body and a here-string are DATA the command writes (a source file, a Makefile, a config), not commands it runs: a `//` comment, an XML tag or a `rm -rf` in it is no reach and no walk. The path, walk and link
// analysis (contamination-cwd.mjs) reads the command WITHOUT them; every other rule (the text rules, the token and canary scans, which read the whole call and the session's files) still sees the full text.
// Kept, because it is code that runs: a heredoc or a here-string whose command line names an interpreter (`bash <<EOF`, `python3 - <<EOF`, `cat <<EOF | node`, `eval`, `source`).
// A body becomes ONE placeholder line, and the text of the body is handed back in `data` (the link rule still looks in it for a script's `symlink(` call, which a later `python3 file.py` runs).

const MARKER = /(?<!<)<<(-?)\s*(["']?)([A-Za-z_][\w.-]*)\2/g;
const HERE_STRING = /<<<\s*(?:"[^"]*"|'[^']*'|[^\s;&|)]+)/g;
const EXECUTES = /(?:^|[\s;&|(])(?:bash|sh|zsh|dash|ksh|python3?|node|nodejs|deno|bun|perl|ruby|php|eval|source)(?=[\s;&|)]|$)/;

/** The placeholder line of the body number `n`; it never occurs in a command. */
export const placeholder = n => `\u0001DATA${n}\u0001`;
export const PLACEHOLDER = /^\u0001DATA(\d+)\u0001$/;

const runsBody = line => EXECUTES.test(line) || line.includes('$((');
const markersOf = line => (runsBody(line) ? [] : [...line.matchAll(MARKER)].map(m => ({ word: m[3], tabs: m[1] === '-' })));
const closes = (line, marker) => (marker.tabs ? line.replace(/^\t+/, '') : line).trimEnd() === marker.word;
const withoutHereStrings = line => (runsBody(line) ? line : line.replace(HERE_STRING, ' '));

// The accumulator is built inside `stripData` and appended to in place: a copy of it per line would make a 20,000-line heredoc quadratic. Nothing outside the call is touched; the result is new.
function flush(acc) {
  acc.out.push(placeholder(acc.data.length));
  acc.data.push(acc.body.join('\n'));
  acc.body = [];
}

/** A line outside any body: kept (less its here-strings); the heredocs it opens are pending. */
function openLine(acc, line) {
  acc.out.push(withoutHereStrings(line));
  acc.pending = markersOf(line);
}

/** A line inside a body: it is the data, or the terminator of the first pending heredoc (the body is closed when the last one ends). */
function bodyLine(acc, line) {
  if (closes(line, acc.pending[0])) acc.pending = acc.pending.slice(1);
  else acc.body.push(line);
  if (acc.pending.length === 0) flush(acc);
}

/**
 * `{ text, data }`: the command without its heredoc bodies (each one placeholder line) and without its here-string operands, and the bodies' texts by placeholder number. A body with no terminator runs to the end, as in a shell.
 */
export function stripData(text) {
  const acc = { out: [], pending: [], body: [], data: [] };
  for (const line of text.split('\n')) (acc.pending.length === 0 ? openLine : bodyLine)(acc, line);
  if (acc.pending.length > 0) flush(acc);
  return { text: acc.out.join('\n'), data: acc.data };
}
