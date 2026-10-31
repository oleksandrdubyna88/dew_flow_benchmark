// A heredoc body and a here-string are DATA the command writes (a source file, a Makefile, a config), not commands it runs: a `//` comment, an XML tag or a `rm -rf` in it is no reach and no walk. The path, walk and link
// analysis (contamination-cwd.mjs) reads the command WITHOUT them; every other rule (the text rules, the token and canary scans, which read the whole call and the session's files) still sees the full text.
// Two kinds of body are not plain data, by WHO runs it:
//   a SHELL (`bash <<EOF`, `sh`, `zsh`, `dash`, `eval`, `source`, `cat <<EOF | sh`): the body is shell code and is kept in the command, read as shell like any other line;
//   an INTERPRETER (`python3 - <<EOF`, `node`, `ruby`, `perl`, `php`, `deno`, `bun`, `tsx`, `ts-node`, `cat <<EOF | node`): the body is code in ANOTHER language, so the shell rules must not tokenise it (a Python or TypeScript source holds
//   `"/"`, `//` and `grep -r` as text: series b82e, session 97168d). It is stripped like data and flagged in `code`; the reach rule judges only its quoted absolute-path literals (`pathLiterals`), as plain reaches, never walks.
// A body becomes ONE placeholder line, and the text of the body is handed back in `data` (the link rule still looks in it for a script's `symlink(` call, which a later `python3 file.py` runs).

const MARKER = /(?<!<)<<(-?)\s*(["']?)([A-Za-z_][\w.-]*)\2/g;
const HERE_STRING = /<<<\s*(?:"[^"]*"|'[^']*'|[^\s;&|)]+)/g;
// A command word, wherever it stands on the line (so a wrapper's launch is found: `env -i node`, `timeout 5 python3`, `nice -n 5 bash`, `sudo`, `exec`, `nohup`, `command`), with or without a directory in front of it
// (`/usr/bin/python3`, `./node_modules/.bin/tsx`). The word that is the target of a redirection (`> node`, `>> src/python`) runs nothing.
const COMMAND_START = '(?:^|[\\s;&|(])(?<!>\\s*)(?:[^\\s;&|()]*\\/)?';
const COMMAND_END = '(?=[\\s;&|)]|$)';
const SHELLS = new RegExp(`${COMMAND_START}(?:bash|sh|zsh|dash|ksh|eval|source)${COMMAND_END}`);
const INTERPRETERS = new RegExp(`${COMMAND_START}(?:python[\\d.]*|node|nodejs|deno|bun|perl|ruby|php|tsx|ts-node)${COMMAND_END}`);
// a quoted absolute path in a source text: `/` then a name (never a lone `/`, `//` or `/ x`), closed by the quote that opened it on the same line
const PATH_LITERAL = /(["'])(\/[^"'\s\\/][^"'\n]*)\1/g;

/** The placeholder line of the body number `n`; it never occurs in a command. */
export const placeholder = n => `\u0001DATA${n}\u0001`;
export const PLACEHOLDER = /^\u0001DATA(\d+)\u0001$/;

/** Who runs the heredocs and here-strings a line opens: `shell` (the body is shell code; a `$((` is a shift, no heredoc), `code` (an interpreter's source) or `data` (nothing runs it). A shell on the line wins. */
function kindOf(line) {
  if (SHELLS.test(line) || line.includes('$((')) return 'shell';
  return INTERPRETERS.test(line) ? 'code' : 'data';
}

const markersOf = line => (kindOf(line) === 'shell' ? [] : [...line.matchAll(MARKER)].map(m => ({ word: m[3], tabs: m[1] === '-' })));
const closes = (line, marker) => (marker.tabs ? line.replace(/^\t+/, '') : line).trimEnd() === marker.word;
const withoutHereStrings = line => (kindOf(line) === 'data' ? line.replace(HERE_STRING, ' ') : line); // what a shell or an interpreter runs from a here-string is kept

// The accumulator is built inside `stripData` and appended to in place: a copy of it per line would make a 20,000-line heredoc quadratic. Nothing outside the call is touched; the result is new.
function flush(acc) {
  acc.out.push(placeholder(acc.data.length));
  acc.data.push(acc.body.join('\n'));
  acc.code.push(acc.isCode);
  acc.body = [];
}

/** A line outside any body: kept (less its here-strings); the heredocs it opens are pending. */
function openLine(acc, line) {
  acc.out.push(withoutHereStrings(line));
  acc.pending = markersOf(line);
  acc.isCode = kindOf(line) === 'code';
}

/** A line inside a body: it is the data, or the terminator of the first pending heredoc (the body is closed when the last one ends). */
function bodyLine(acc, line) {
  if (closes(line, acc.pending[0])) acc.pending = acc.pending.slice(1);
  else acc.body.push(line);
  if (acc.pending.length === 0) flush(acc);
}

/**
 * `{ text, data, code }`: the command without its heredoc bodies (each one placeholder line) and without its here-string operands, the bodies' texts by placeholder number, and `code[n]` = whether body `n` is an interpreter's
 * source. A body a shell runs is not stripped (it is shell code). A body with no terminator runs to the end, as in a shell.
 */
export function stripData(text) {
  const acc = { out: [], pending: [], body: [], data: [], code: [], isCode: false };
  for (const line of text.split('\n')) (acc.pending.length === 0 ? openLine : bodyLine)(acc, line);
  if (acc.pending.length > 0) flush(acc);
  return { text: acc.out.join('\n'), data: acc.data, code: acc.code };
}

/** The quoted absolute paths of an interpreter's source, in order: the paths the program can open, judged as plain reaches. */
export const pathLiterals = body => [...body.matchAll(PATH_LITERAL)].map(m => m[2]);
