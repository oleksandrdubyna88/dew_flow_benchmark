// The shape of a top-level function, measured the way the shared rule common.coding-style states it (E3b coai-1): fewer than 50 lines and a cyclomatic complexity of at most 4
// (1 + the decision points: `if`, `for`, `while`, `case`, `catch`, `&&`, `||` and the ternary `?:`; `??` and `?.` are not counted). No JS parser is available to a spike, so a small
// scanner blanks comments and string contents first (template literals with nested `${ }` included): a word inside a message is never a decision.

/** The source with comments and the CONTENT of strings and templates replaced by spaces (newlines kept), so a scan sees code only. */
export function codeOnly(source) {
  const out = [];
  const stack = []; // the open `${` of templates: the brace depth at which each was opened
  let depth = 0;
  let i = 0;
  const blank = ch => (ch === '\n' ? '\n' : ' ');
  const skipQuoted = quote => {
    out.push(' ');
    i += 1;
    while (i < source.length && source[i] !== quote) { out.push(blank(source[i])); i += source[i] === '\\' ? 2 : 1; }
    out.push(' ');
    i += 1;
  };
  const template = (fresh = true) => {
    if (fresh) { out.push(' '); i += 1; }
    while (i < source.length && source[i] !== '`') {
      if (source[i] === '$' && source[i + 1] === '{') { stack.push(depth); depth += 1; out.push('  '); i += 2; return; }
      out.push(blank(source[i]));
      i += source[i] === '\\' ? 2 : 1;
    }
    out.push(' ');
    i += 1;
  };
  while (i < source.length) {
    const c = source[i];
    const two = source.slice(i, i + 2);
    if (two === '//') { while (i < source.length && source[i] !== '\n') { out.push(' '); i += 1; } } else if (two === '/*') {
      while (i < source.length && source.slice(i, i + 2) !== '*/') { out.push(blank(source[i])); i += 1; }
      out.push('  ');
      i += 2;
    } else if (c === '\'' || c === '"') skipQuoted(c);
    else if (c === '`') template();
    else if (c === '}' && stack.length > 0 && stack.at(-1) === depth - 1) { stack.pop(); depth -= 1; out.push(' '); i += 1; template(false); } else {
      if (c === '{') depth += 1;
      if (c === '}') depth -= 1;
      out.push(c);
      i += 1;
    }
  }
  return out.join('');
}

const DECISIONS = /\bif\b|\bfor\b|\bwhile\b|\bcase\b|\bcatch\b|&&|\|\||(?<!\?)\?(?![.?])/g;

/** `{ name, lines, complexity }` of every top-level function of `source` (a declaration that starts at column 0 and ends at the next `}` at column 0). */
export function functionShapes(source) {
  const lines = source.split('\n');
  const code = codeOnly(source).split('\n');
  const shapes = [];
  for (let start = 0; start < lines.length; start += 1) {
    const head = /^(?:export )?(?:async )?function\*? ([A-Za-z0-9_$]+)\(/.exec(lines[start]);
    if (!head) continue;
    const count = (text, ch) => text.split(ch).length - 1;
    const oneLiner = count(code[start], '{') > 0 && count(code[start], '{') === count(code[start], '}');
    const end = oneLiner ? start : code.findIndex((_, n) => n > start && /^}/.test(lines[n]));
    const body = code.slice(start, end + 1).join('\n');
    shapes.push({ name: head[1], lines: end - start + 1, complexity: 1 + (body.match(DECISIONS) ?? []).length });
  }
  return shapes;
}
