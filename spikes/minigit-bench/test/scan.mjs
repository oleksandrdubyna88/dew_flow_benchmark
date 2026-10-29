// Source scanning for T-C1: comments are stripped (line numbers kept) so a word in prose is never a hit, strings and code are scanned.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';

/** `src` with every comment blanked out and newlines kept. Tracks '', "", `` so a `//` inside a string is not a comment. */
export function stripComments(src) {
  let out = '';
  let quote = null;
  for (let i = 0; i < src.length; i += 1) {
    const c = src[i];
    const two = src.slice(i, i + 2);
    if (quote) {
      out += c;
      if (c === '\\') { out += src[i + 1] ?? ''; i += 1; } else if (c === quote) quote = null;
    } else if (two === '//') {
      while (i < src.length && src[i] !== '\n') i += 1;
      out += '\n';
    } else if (two === '/*') {
      const end = src.indexOf('*/', i + 2);
      const stop = end === -1 ? src.length : end + 2;
      out += src.slice(i, stop).replace(/[^\n]/g, ' ');
      i = stop - 1;
    } else {
      if (c === '\'' || c === '"' || c === '`') quote = c;
      out += c;
    }
  }
  return out;
}

const SOURCE_EXTENSIONS = Object.freeze(['.mjs', '.js', '.cjs']);

/** Every `.mjs`, `.js` and `.cjs` under `dir`, skipping `skip` directory names. */
export function sourceFiles(dir, skip = []) {
  return readdirSync(dir).flatMap(name => {
    const full = path.join(dir, name);
    if (statSync(full).isDirectory()) return skip.includes(name) || name === 'node_modules' ? [] : sourceFiles(full, skip);
    return SOURCE_EXTENSIONS.includes(path.extname(name)) ? [full] : [];
  });
}

/** Names the file declares itself (`function x`, `const x`, `class x`): an own function that merely shares a name is not the fitted path. */
export function declaredNames(text, names) {
  const code = stripComments(text);
  return names.filter(n => new RegExp(`\\b(?:function|const|let|var|class)\\s+${n}\\b`).test(code));
}

/** `[{ name, line }]` for each word-bounded occurrence of a name in the comment-stripped text; a name the file declares itself is skipped. */
export function findNames(text, names, { ownDeclarations = false } = {}) {
  const imported = importedNames(text, /./);
  const own = ownDeclarations ? declaredNames(text, names).filter(n => !imported.includes(n)) : [];
  const lines = stripComments(text).split('\n');
  return lines.flatMap((l, i) => names.filter(n => !own.includes(n) && new RegExp(`\\b${n}\\b`).test(l)).map(name => ({ name, line: i + 1 })));
}

/** Calls of one name: `name(`, with optional whitespace. */
export function findCalls(text, name) {
  return stripComments(text).split('\n').flatMap((l, i) => (new RegExp(`\\b${name}\\s*\\(`).test(l) ? [i + 1] : []));
}

/** The names imported from a module path matching `fromRe`: `import { a, b as c } from '...'` -> ['a', 'b']. */
export function importedNames(text, fromRe) {
  const out = [];
  for (const m of stripComments(text).matchAll(/import\s*\{([^}]*)\}\s*from\s*['"]([^'"]+)['"]/g)) {
    if (fromRe.test(m[2])) out.push(...m[1].split(',').map(s => s.trim().split(/\s+as\s+/)[0]).filter(Boolean));
  }
  return out;
}

/**
 * Imports from a module matching `fromRe` that are not on the allow-list: a named import outside it, or any namespace, default, dynamic
 * `import()` or `require()` form (those cannot be checked name by name, so they are refused).
 */
export function importViolations(text, fromRe, allowed) {
  const code = stripComments(text);
  const named = importedNames(text, fromRe).filter(n => !allowed.includes(n)).map(n => `imports '${n}', which is not on the allow-list`);
  const forms = [
    [/import\s*\*\s*as\s+\w+\s+from\s*['"]([^'"]+)['"]/g, 'a namespace import'],
    [/import\s+\w+\s*(?:,\s*\{[^}]*\}\s*)?from\s*['"]([^'"]+)['"]/g, 'a default import'],
    [/import\s*\(\s*['"]([^'"]+)['"]/g, 'a dynamic import'],
    [/require\s*\(\s*['"]([^'"]+)['"]/g, 'a require'],
  ].flatMap(([re, what]) => [...code.matchAll(re)].filter(m => fromRe.test(m[1])).map(m => `${what} of ${m[1]}`));
  return [...named, ...forms];
}

/** Spike-local modules reachable from `entry` through relative imports that stay inside `root`. */
export function localGraph(entry, root) {
  const seen = new Set();
  const visit = file => {
    if (seen.has(file)) return;
    seen.add(file);
    const text = stripComments(readFileSync(file, 'utf8'));
    for (const m of text.matchAll(/from\s*['"](\.[^'"]+)['"]/g)) {
      const target = path.resolve(path.dirname(file), m[1]);
      if (target.startsWith(root + path.sep)) visit(target);
    }
  };
  visit(entry);
  return [...seen];
}
