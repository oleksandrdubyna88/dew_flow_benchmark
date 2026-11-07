// The workspace of a quick-run session (tasks 04 05 06 07 08 10): the template of the stack and kind from `spikes/lang-bench/templates/<stack>/<kind>/` as merged, the runner of `spikes/lang-bench/runner/`, the grammar
// of the task's FORMAT, `PACKAGES.md` derived from the registered v2 snapshot, the spec as `STATEMENT.md` with the stack sentence (and, for a web task, the framework sentence) on their marked lines, and the
// visible samples of the spec's section 6. Pure but for reading the templates, the runner and the lang-bench inputs. A file map `{ relPath: { bytes, mode } }` is the same shape the olympiad workspace uses, so
// `fillWorkspace`, `frozenChanges` and `restoreFrozen` serve both.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { extractGrammar } from '../../lang-bench/tools/grammar-extract.mjs';
import { loadInputs } from '../../lang-bench/lib/workspace-build.mjs';
import { renderPackagesMd } from '../../lang-bench/lib/packages-md.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { parseScenarioSamples } from './samples-sd.mjs';

const BENCH = fileURLToPath(new URL('../../lang-bench/', import.meta.url));
export const BENCH_TEMPLATES = path.join(BENCH, 'templates');
export const BENCH_RUNNER = path.join(BENCH, 'runner');
const SCRIPTS = new Set(['build.sh', 'run.sh']);

/** The files the agent was told are frozen, besides the template's scripts: the stack's pin files. The project file (`App.csproj`), `package.json` and `package-lock.json` are the agent's to edit (D65). */
export const PIN_FILES = Object.freeze({
  csharp: Object.freeze(['global.json', 'nuget.config', 'Directory.Build.props']),
  node: Object.freeze(['.npmrc', 'tsconfig.json', 'eslint.config.mjs']),
});

/** The stack sentence of the plan's section 4.5 as D67 (5) amended it: no word about pinned project files. */
export function quickStackSentence(explore, stack) {
  if (stack === 'csharp') return `Use C# 14 on .NET 10 (SDK ${explore.dotnet.sdk} is installed).`;
  return `Use TypeScript ${explore.typescript.version.split('.').slice(0, 2).join('.')} with \`strict\`, run by Node ${explore.node.version.split('.')[0]} with native type stripping (no build step), with \`tsc --noEmit\` as the type check.`;
}

/** The framework sentence of a web task (plan section 4.5): C# Minimal API, TypeScript Fastify at the version the registered record holds. */
export function frameworkSentence(explore, stack, record) {
  if (stack === 'csharp') return `Use ASP.NET Core Minimal API (the shared framework of SDK ${explore.dotnet.sdk}) for the HTTP server.`;
  const fastify = (record?.node?.packages ?? []).find(p => p.name === 'fastify');
  return `Use Fastify (fastify ${fastify?.version ?? 'unrecorded'}, listed in PACKAGES.md) for the HTTP server.`;
}

/** The statement: the spec's bytes, a blank line, the marked stack sentence and, for a web task, the marked framework sentence. */
export function quickStatement({ spec, kind, stackSentence, frameworkSentence: framework }) {
  const base = `${spec}${spec.endsWith('\n') ? '' : '\n'}\n<!-- stack-sentence -->\n${stackSentence}\n`;
  return kind === 'web' ? `${base}\n<!-- framework-sentence -->\n${framework}\n` : base;
}

const listFiles = (root, rel = '') => readdirSync(path.join(root, rel), { withFileTypes: true }).flatMap(e => {
  const r = path.join(rel, e.name);
  return e.isDirectory() ? listFiles(root, r) : [r];
});

/** `{ relPath: { bytes, mode } }` of a directory's regular files; `modeOf(rel)` gives each file's mode. */
function filesOf(root, prefix, modeOf) {
  return Object.fromEntries(listFiles(root).filter(rel => !rel.includes('__pycache__') && !rel.endsWith('.pyc')).map(rel => [`${prefix}${rel}`, { bytes: readFileSync(path.join(root, rel)), mode: modeOf(rel) }]));
}

/** The registered inputs of the language comparison v2 that `PACKAGES.md` is derived from, loaded once. -> `ok({ record, recipe, matrix })` | `fail`. */
export function packagesInputs() {
  const loaded = loadInputs();
  return loaded.ok ? ok({ record: loaded.value.record, recipe: loaded.value.recipe, matrix: loaded.value.matrix }) : loaded;
}

/**
 * The files of one workspace. `explore` is the quick profile (`quickExplore`), `task` the task's pins entry (its `kind`), `sources` the spec and FORMAT texts, `inputs` is `packagesInputs()`'s value,
 * `templates` and `runner` default to the merged ones of lang-bench (tests may pass copies). -> `ok({ files, frozen })` | `fail`; `frozen` is the subset the agent must not change.
 */
export function quickWorkspaceFiles({ explore, task, stack, sources, inputs, templates = BENCH_TEMPLATES, runner = BENCH_RUNNER }) {
  const parts = parsedParts({ stack, kind: task.kind, sources, inputs });
  if (!parts.ok) return parts;
  const files = filesFrom({ explore, task, stack, sources, inputs, templates, runner, ...parts.value });
  return ok({ files, frozen: frozenOf(files, stack) });
}

/** What the files are made from: the visible samples, the grammar of FORMAT.md and the rendered PACKAGES.md. -> `ok({ samples, grammar, packages })` | `fail`. */
function parsedParts({ stack, kind, sources, inputs }) {
  const samples = parseScenarioSamples(sources.spec);
  if (!samples.ok) return samples;
  const grammar = extractGrammar(Buffer.from(sources.format, 'utf8'));
  if (!grammar.ok) return fail(`the grammar of FORMAT.md: ${grammar.reason}`);
  const packages = renderPackagesMd({ stack, kind, record: inputs.record, recipe: inputs.recipe, matrix: inputs.matrix });
  return packages.ok ? ok({ samples: samples.value, grammar, packages: packages.value }) : fail(`PACKAGES.md: ${packages.reason}`);
}

function filesFrom({ explore, task, stack, sources, inputs, templates, runner, samples, grammar, packages }) {
  const statement = quickStatement({ spec: sources.spec, kind: task.kind, stackSentence: quickStackSentence(explore, stack), frameworkSentence: frameworkSentence(explore, stack, inputs.record) });
  const files = {
    ...filesOf(path.join(templates, stack, task.kind), '', rel => (SCRIPTS.has(rel) ? 0o755 : 0o644)),
    ...filesOf(runner, 'runner/', () => 0o644),
    'runner/GRAMMAR.md': { bytes: grammar.bytes, mode: 0o644 },
    'PACKAGES.md': { bytes: Buffer.from(packages), mode: 0o644 },
    'STATEMENT.md': { bytes: Buffer.from(statement), mode: 0o644 },
  };
  for (const s of samples) {
    files[`samples/${s.id}.in`] = { bytes: Buffer.from(s.input), mode: 0o644 };
    files[`samples/${s.id}.out`] = { bytes: Buffer.from(s.output), mode: 0o644 };
  }
  return files;
}

/** The frozen subset of a file map: the scripts, the statement, the packages list, the samples, the runner and the stack's pin files. */
export function frozenOf(files, stack) {
  const pin = new Set([...SCRIPTS, ...PIN_FILES[stack], 'STATEMENT.md', 'PACKAGES.md']);
  return Object.fromEntries(Object.entries(files).filter(([rel]) => pin.has(rel) || rel.startsWith('samples/') || rel.startsWith('runner/')));
}

/** The visible samples as the scoring needs them: `[{ id, input, output }]`. */
export const quickSamples = spec => { const r = parseScenarioSamples(spec); if (!r.ok) throw new Error(r.reason); return r.value; };

export const modeOfTemplate = rel => statSync(path.join(BENCH_TEMPLATES, rel)).mode & 0o777;
