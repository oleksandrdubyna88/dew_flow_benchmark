// The bytes of the workspace templates, per file, and the identity they make (F1.S3 (b), review round). `templateHashes()` is `{ '<stack>/<kind>/<file>': sha256 }` for every file of every template folder of a stack (not
// `stack-files.json`, which is the push check's list and no part of a workspace). A series records this map at `init`: a later scoring tells a template that moved from the agent's change by the BYTES of each file, not by the
// path alone. `templatesIdentity()` is a sha256 over its sorted lines: the package matrix is measured against it, and a series records it too.
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO = path.resolve(fileURLToPath(new URL('../../..', import.meta.url)));
export const TEMPLATES_FOLDER = 'spikes/lang-bench/templates';
const STACK_FOLDERS = Object.freeze(['csharp', 'node']);
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

/** `{ '<stack>/<kind>/<file>': sha256 }` of the template files under `root` (the repository; a test passes a copy). */
export function templateHashes({ root = REPO } = {}) {
  const base = path.join(root, TEMPLATES_FOLDER);
  return Object.fromEntries(STACK_FOLDERS.flatMap(stack => readdirSync(path.join(base, stack), { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => {
    const abs = path.join(e.parentPath, e.name);
    return [path.relative(base, abs).split(path.sep).join('/'), sha256(readFileSync(abs))];
  })).sort(([a], [b]) => (a < b ? -1 : 1)));
}

/** The identity of the templates: a sha256 over the sorted `key<TAB>sha256` lines of `templateHashes`. */
export const templatesIdentity = ({ root = REPO } = {}) => sha256(Object.entries(templateHashes({ root })).map(([key, hash]) => `${key}\t${hash}`).join('\n'));
