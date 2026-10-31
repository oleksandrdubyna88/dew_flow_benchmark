// The workspace of a session (plan D-X6, spec 01's R13): exactly the frozen files, per stack. The spec and the format texts are read from the local task repository at their pinned shas (`git show`) and
// checked against their sha256; they are never committed here. Everything is a pure function of its inputs except `loadSources` (reads git) and `fillWorkspace` / `fillerFor` (write the files).
import { chmodSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { SPIKE } from './paths.mjs';

export const STACKS = Object.freeze(['csharp', 'node']);
const TEMPLATES = path.join(SPIKE, 'templates');
export const JUDGE_SOURCE = path.join(SPIKE, 'runner', 'run_samples.py');
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const LF_MARK = '⏎'; // the symbol the spec uses for a line feed

/** The sentence that varies by stack (plan section 4.5 of v2, with the SDK and TypeScript patch this pilot runs). */
export function stackSentence(explore, stack) {
  if (stack === 'csharp') return `Use C# 14 on .NET 10 (SDK ${explore.dotnet.sdk} is installed; the project files in this folder are pinned).`;
  return `Use TypeScript ${explore.typescript.version} with \`strict\`, run by Node 26 with native type stripping (no build step), with \`tsc --noEmit\` as the type check.`;
}

/** The directories of the task repository this machine has: beside the repository root, beside the main checkout of a worktree, under `~/git`. */
export function candidateRepos({ repoName, repoRoot, commonRoot, home = os.homedir() }) {
  return [...new Set([repoRoot, commonRoot].filter(Boolean).map(r => path.resolve(r, '..', repoName)).concat(path.join(home, 'git', repoName)))];
}

async function gitShow(repo, sha, file) {
  const r = await runBounded('git', ['-C', repo, 'show', `${sha}:${file}`], { timeoutMs: 30_000 });
  return r.ok && r.value.code === 0 ? ok(Buffer.from(r.value.stdout, 'utf8')) : fail(`git show ${sha}:${file} in ${repo} failed: ${r.ok ? r.value.stderr.slice(-200) : r.reason}`);
}

/** One pinned text: read at its sha and checked against the registered sha256. */
async function pinnedText(repo, pin, what) {
  const got = await gitShow(repo, pin.sha, pin.path);
  if (!got.ok) return got;
  const hash = sha256(got.value);
  return hash === pin.sha256 ? ok(got.value.toString('utf8')) : fail(`${what} at ${pin.sha} hashes to ${hash.slice(0, 12)}, the pins say ${pin.sha256.slice(0, 12)}`);
}

/** The spec and the FORMAT text from the first candidate repository that has both at their pins. -> `ok({ spec, format, repo })` | `fail`. */
export async function loadSources({ explore, candidates }) {
  const problems = [];
  for (const repo of candidates) {
    const both = await sourcesIn(repo, explore);
    if (both.ok) return both;
    problems.push(both.reason);
  }
  return fail(`the task repository was not found at its pins: ${problems.join(' | ') || 'no candidate directory'}`);
}

/** The spec and FORMAT text of one repository at their pins. */
async function sourcesIn(repo, explore) {
  const spec = await pinnedText(repo, explore.specs.spec01, 'the spec');
  if (!spec.ok) return spec;
  const format = await pinnedText(repo, explore.specs.format, 'FORMAT.md');
  return format.ok ? ok({ spec: spec.value, format: format.value, repo }) : format;
}

/** `runner/GRAMMAR.md`: FORMAT.md's sections 3 to 5, verbatim (FORMAT.md's own rule). */
export function grammarOf(format) {
  const from = format.indexOf('\n## 3. Visible samples\n');
  const to = format.indexOf('\n## 6. Worked examples\n');
  if (from < 0 || to < from) throw new Error('FORMAT.md has no sections 3 to 5 in the expected place');
  return `${format.slice(from + 1, to).replace(/\n+$/, '')}\n`;
}

/** The visible samples as the spec's section 6 table shows them: `[{ id, input, output }]` with every `⏎` a line feed. */
export function samplesOf(spec) {
  const rows = [...spec.matchAll(/^\| `(s\d\d)` \| `([^`]*)` \| `([^`]*)` \|$/gm)];
  return rows.map(m => ({ id: m[1], input: m[2].replaceAll(LF_MARK, '\n'), output: m[3].replaceAll(LF_MARK, '\n') }));
}

/** `STATEMENT.md`: the spec's bytes, a blank line, then the stack sentence. */
export const statementOf = (spec, sentence) => `${spec}${spec.endsWith('\n') ? '' : '\n'}\n${sentence}\n`;

const filesUnder = (root, rel = '') => readdirSync(path.join(root, rel), { withFileTypes: true }).flatMap(e => {
  const r = path.join(rel, e.name);
  return e.isDirectory() ? filesUnder(root, r) : [r];
});

/** `{ relPath: { bytes, mode } }` of the frozen files of a stack's workspace; `typesNode` fills the `{TYPES_NODE}` slots of the Node template. Pure but for reading the templates and the judge. */
export function workspaceFiles({ explore, stack, sources, typesNode }) {
  const dir = path.join(TEMPLATES, stack);
  const files = {};
  for (const rel of filesUnder(dir)) {
    const text = readFileSync(path.join(dir, rel), 'utf8').replaceAll('{TYPES_NODE}', typesNode ?? '{TYPES_NODE}');
    files[rel] = { bytes: Buffer.from(text), mode: statSync(path.join(dir, rel)).mode & 0o777 };
  }
  files['STATEMENT.md'] = { bytes: Buffer.from(statementOf(sources.spec, stackSentence(explore, stack))), mode: 0o644 };
  for (const s of samplesOf(sources.spec)) {
    files[`samples/${s.id}.in`] = { bytes: Buffer.from(s.input), mode: 0o644 };
    files[`samples/${s.id}.out`] = { bytes: Buffer.from(s.output), mode: 0o644 };
  }
  files['runner/GRAMMAR.md'] = { bytes: Buffer.from(grammarOf(sources.format)), mode: 0o644 };
  files['runner/run_samples.py'] = { bytes: readFileSync(JUDGE_SOURCE), mode: 0o644 };
  return files;
}

/** Write the files into an existing empty directory. -> `ok({ files: [relPath...] })`. */
export function fillWorkspace({ files, work }) {
  for (const [rel, f] of Object.entries(files)) {
    const to = path.join(work, rel);
    mkdirSync(path.dirname(to), { recursive: true });
    writeFileSync(to, f.bytes, { mode: f.mode });
    chmodSync(to, f.mode);
  }
  return ok({ files: Object.keys(files).sort(), flags: [] });
}

/** The `ctx.fill` of the launch phase (seam W1): this item's stack workspace. */
export const fillerFor = ({ explore, sources, typesNode }) => ({ item, layout }) => {
  const stack = item.session.stack;
  if (!STACKS.includes(stack)) return fail(`stack '${stack}': the legal stacks are ${STACKS.join(', ')}`);
  return fillWorkspace({ files: workspaceFiles({ explore, stack, sources, typesNode }), work: layout.work });
};

/** The frozen files of a workspace the agent changed or removed: `[{ path, state: 'changed' | 'absent' }]` (the file list is `workspaceFiles`'s, minus the sources the agent writes). */
export function frozenChanges({ files, work }) {
  return Object.entries(files).flatMap(([rel, f]) => {
    let got = null;
    try { got = readFileSync(path.join(work, rel)); } catch { return [{ path: rel, state: 'absent' }]; }
    return got.equals(f.bytes) ? [] : [{ path: rel, state: 'changed' }];
  });
}

