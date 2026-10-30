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

const flush = acc => ({ ...acc, out: [...acc.out, placeholder(acc.data.length)], data: [...acc.data, acc.body.join('\n')], body: [] });

/** A line outside any body: kept (less its here-strings); the heredocs it opens are pending. */
const open = (acc, line) => ({ ...acc, out: [...acc.out, withoutHereStrings(line)], pending: markersOf(line) });

/** A line inside a body: it is the data, or the terminator of the first pending heredoc (the body is closed when the last one ends). */
function inside(acc, line) {
  const ends = closes(line, acc.pending[0]);
  const next = { ...acc, pending: ends ? acc.pending.slice(1) : acc.pending, body: ends ? acc.body : [...acc.body, line] };
  return next.pending.length === 0 ? flush(next) : next;
}

/**
 * `{ text, data }`: the command without its heredoc bodies (each one placeholder line) and without its here-string operands, and the bodies' texts by placeholder number. A body with no terminator runs to the end, as in a shell.
 */
export function stripData(text) {
  const done = text.split('\n').reduce((acc, line) => (acc.pending.length === 0 ? open(acc, line) : inside(acc, line)), { out: [], pending: [], body: [], data: [] });
  const last = done.pending.length === 0 ? done : flush(done);
  return { text: last.out.join('\n'), data: last.data };
}
