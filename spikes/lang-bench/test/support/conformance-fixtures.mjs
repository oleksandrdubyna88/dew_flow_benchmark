// Fixtures of the conformance tests (E1.S8): a copy of one synthetic task repository in an owned folder, edited by exact needles, and the checker run on it in this process.
// The synthetic repositories are test/fixtures/conformance/<kind>/ and the FORMAT stand-in beside them; the real FORMAT and the real spec 01 are used only by the tests that name them.
import { cpSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { run } from '../../lib/conformance.mjs';
import { edit } from './format-fixtures.mjs';
import { owned } from './owned.mjs';

export const FIXTURES = fileURLToPath(new URL('../fixtures/conformance/', import.meta.url));
export const FORMAT_FIXTURE = path.join(FIXTURES, 'FORMAT.md');
export const TASKS = Object.freeze({ olympiad: '01-count-down', daemon: '02-echo-line', web: '03-hello-page' });
export const REAL_FORMAT = path.join(process.env.LANG_V2_TASK_REPO ?? '/nonexistent', 'todo', 'tasks', 'FORMAT.md');

/** The names of the ten checks, in order. */
export const CHECK_NAMES = Object.freeze(['file', 'header', 'sections', 'r-ids', 'sentences', 'T/G', 'samples', 'sample-forms', 'sample-env', 'no-needle']);

/**
 * A copy of the synthetic repository of `kind` in an owned folder: `{ repo, task, format, spec, samples, read, write }`. `edits` is `{ spec: [[from, to]…], 'samples/s01.in': [[from, to]…], format: […] }`;
 * a step is a `[from, to]` pair whose needle must occur exactly once (`edit` throws otherwise) or a function from the text to the text (`phrase`, `drop`). The FORMAT is a copy too, so a test can change one word of it.
 */
export function fixtureRepo(t, kind, edits = {}) {
  const repo = owned.ownedDir(t, `conf-${kind}`);
  cpSync(path.join(FIXTURES, kind), repo, { recursive: true });
  const task = TASKS[kind];
  const format = path.join(repo, 'FORMAT.md');
  cpSync(FORMAT_FIXTURE, format);
  const files = { spec: path.join(repo, 'todo', 'tasks', `${task}.md`), format };
  const samples = path.join(repo, 'tasks', task, 'samples');
  const target = key => files[key] ?? path.join(samples, key.replace(/^samples\//, ''));
  for (const [key, list] of Object.entries(edits)) writeFileSync(target(key), list.reduce((text, step) => (typeof step === 'function' ? step(text) : edit(text, step[0], step[1])), readFileSync(target(key), 'utf8')));
  return {
    repo, task, format, spec: files.spec, samples,
    read: key => readFileSync(target(key), 'utf8'),
    write: (key, text) => { mkdirSync(path.dirname(target(key)), { recursive: true }); writeFileSync(target(key), text); },
    remove: key => rmSync(target(key), { force: true }),
    rename: (from, to) => renameSync(target(from), target(to)),
    exists: key => existsSync(target(key)),
  };
}

/** The arguments of one checker run on a fixture repository. */
export const argsOf = (fixture, extra = []) => ['--repo', fixture.repo, '--task', fixture.task, '--format', fixture.format, ...extra];

/** The checker run on a fixture, in this process: `{ exit, lines, errors }`. The runner's TMPDIR is a folder inside the fixture, so it dies with the fixture's owned folder. */
export function check(fixture, extra = [], seams = {}) {
  const tmp = path.join(fixture.repo, '.run-tmp');
  mkdirSync(tmp, { recursive: true });
  return run(argsOf(fixture, extra), { tmp, ...seams });
}

/** The FAIL lines of a result for one check. */
export const failsOf = (result, name) => result.lines.filter(line => line.startsWith(`FAIL ${name} — `));

/** True when `result` has a line `ok <name>`. */
export const isOk = (result, name) => result.lines.includes(`ok ${name}`);

const escapeRegExp = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** A step that replaces a phrase wherever it wraps (any run of whitespace stands for the spaces of `from`); the phrase must occur exactly once. */
export function phrase(from, to) {
  const pattern = new RegExp(from.split(/\s+/).map(escapeRegExp).join('\\s+'), 'g');
  return text => {
    const count = [...text.matchAll(pattern)].length;
    if (count !== 1) throw new Error(`the phrase ${JSON.stringify(from)} occurs ${count} times, not once`);
    return text.replace(pattern, () => to);
  };
}

/** A step that removes a phrase (see `phrase`). */
export const drop = from => phrase(from, '');
