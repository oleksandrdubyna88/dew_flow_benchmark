// Lines of code of a finished tree, product against test (E3b.S1 step 9). DESCRIPTIVE ONLY, never a measure the analysis rests on: whether a file is a test is a
// path heuristic (a `test`/`tests`/`spec` directory, a `.test.`/`.spec.` name, a name ending `Test(s).cs`), and the report says so.
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';

const SOURCE = /\.(cs|ts|js|mjs|cts|mts|sh)$/;
const SKIP_DIRS = new Set(['bin', 'obj', 'node_modules', '.git']);
const TEST_FILE = /(\.(test|spec)\.[a-z]+$)|(Tests?\.cs$)/i;
const TEST_DIR = /(^|\/)(tests?|specs?|__tests__)(\/|$)/i;

export const isTestPath = rel => TEST_FILE.test(rel) || TEST_DIR.test(rel);
const countLines = text => (text === '' ? 0 : text.split('\n').length - (text.endsWith('\n') ? 1 : 0));

/** `[{ path, text }]` -> `{ product, test, files }` (lines of source files; the pinned inputs `test-v1.sh` / `SPEC-*.txt` are not source). */
export function countLoc(files) {
  const source = files.filter(f => SOURCE.test(f.path) && !/^test-v[12]\.sh$/.test(f.path) && !f.path.split('/').some(seg => SKIP_DIRS.has(seg)));
  const sum = list => list.reduce((a, f) => a + countLines(f.text), 0);
  return { product: sum(source.filter(f => !isTestPath(f.path))), test: sum(source.filter(f => isTestPath(f.path))), files: source.length };
}

function walk(root, rel = '') {
  return readdirSync(path.join(root, rel), { withFileTypes: true }).flatMap(e => {
    if (e.isDirectory()) return SKIP_DIRS.has(e.name) ? [] : walk(root, path.join(rel, e.name));
    return e.isFile() ? [path.join(rel, e.name)] : [];
  });
}

/** Read a tree (build output and dependencies skipped) and count it. */
export const locOfTree = root => countLoc(walk(root).filter(rel => SOURCE.test(rel)).map(rel => ({ path: rel.split(path.sep).join('/'), text: readFileSync(path.join(root, rel), 'utf8') })));
