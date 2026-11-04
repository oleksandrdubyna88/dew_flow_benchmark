// The spec conformance check (E1.S8): reads a spec, its visible samples and FORMAT.md and names every deviation, one line per check, `ok <check>` or `FAIL <check> — <what, where>`.
// Exit 0 every check ok; 1 a deviation; 4 a usage or format error (a missing task, spec or samples folder, a FORMAT the checker cannot read or that lacks a sentence it expects, python3 or
// the runner missing, the runner timing out): never a pass. The checks are in the conformance-check-*.mjs files; FORMAT's sentences and facts are read from the document itself.
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { envFindings, formFindings, sampleFindings } from './conformance-check-samples.mjs';
import { sentenceFindings, timeFindings } from './conformance-check-sentences.mjs';
import { fileFindings, headerFindings, idFindings, kindOf, sectionFindings } from './conformance-check-structure.mjs';
import { literalNeedles, textFindings } from './conformance-check-text.mjs';
import { readFacts } from './conformance-facts.mjs';
import { locateSentences } from './conformance-locate.mjs';
import { parseSamples } from './conformance-runner.mjs';
import { readSentences } from './conformance-sentences.mjs';
import { readSamplesFolder, shownSamples } from './conformance-shown.mjs';
import { parseSpec } from './conformance-spec.mjs';
import { parseFormat } from './format-structure.mjs';
import { parseKnownNeedles } from './push-needles.mjs';

const EXIT_DEVIATION = 1;
const EXIT_USAGE = 4;
const NEEDLES_FILE = fileURLToPath(new URL('../inputs/needles-lang.json', import.meta.url));
const OPTIONS = { repo: { type: 'string' }, task: { type: 'string' }, format: { type: 'string' }, needles: { type: 'string' } };
const USAGE = 'usage: conformance.mjs --repo <task repository> --task NN-slug --format <FORMAT.md> [--needles <file>]';
const NEEDS_KIND = new Set(['sections', 'sentences', 'T/G', 'samples', 'sample-forms', 'sample-env']);

const usage = reason => ({ exit: EXIT_USAGE, lines: [], errors: [`conformance: ${reason}`, USAGE] });

const readFile = file => {
  try {
    return ok(readFileSync(file));
  } catch (error) {
    return fail(`${file} cannot be read (${error.code ?? error.message})`);
  }
};

function parseOptions(argv) {
  try {
    const { values } = parseArgs({ args: argv, options: OPTIONS, strict: true, allowPositionals: false });
    const missing = ['repo', 'task', 'format'].filter(name => values[name] === undefined);
    if (missing.length > 0) return fail(`missing --${missing.join(', --')}`);
    return /^[A-Za-z0-9][A-Za-z0-9._-]*$/.test(values.task) ? ok(values) : fail(`--task ${JSON.stringify(values.task)} is not a task name like 01-slug`);
  } catch (error) {
    return fail(error.message);
  }
}

/** ok({ format, facts, sentences, known, literals }): everything read from outside the spec, or the first fail. FORMAT's own faults are format errors (exit 4). */
function loadReferences(options) {
  const bytes = readFile(options.format);
  if (!bytes.ok) return bytes;
  const parsed = parseFormat(bytes.value);
  if (!parsed.ok) return fail(`${options.format} is not a readable FORMAT: line ${parsed.line}: ${parsed.reason}`);
  const facts = readFacts(parsed.format);
  if (!facts.ok) return facts;
  const sentences = readSentences(parsed.format);
  if (!sentences.ok) return sentences;
  const needles = readFile(NEEDLES_FILE);
  const known = needles.ok ? parseKnownNeedles(parseJson(needles.value.toString('utf8'))) : needles;
  if (!known.ok) return known;
  const literals = options.needles === undefined ? ok(undefined) : readNeedles(options.needles);
  return literals.ok ? ok({ format: parsed.format, facts: facts.value, sentences: sentences.value, known: known.value, literals: literals.value }) : literals;
}

const parseJson = text => {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
};

function readNeedles(file) {
  const text = readFile(file);
  if (!text.ok) return text;
  const needles = literalNeedles(text.value.toString('utf8'));
  return needles.ok ? ok(needles.value) : fail(`${file}: ${needles.reason}`);
}

const byId = (folder, suffix) => new Map([...folder.files].filter(([name]) => name.endsWith(suffix)).map(([name, bytes]) => [name.slice(0, -suffix.length), bytes]));

/** The context every check reads: the spec, the references, the kind and what follows from them. */
function contextOf({ options, references, spec, folder }) {
  const kind = kindOf(spec, references.facts);
  const common = { ...references, task: options.task, spec, folder, kind, maxId: Math.max(0, ...spec.requirements.map(entry => entry.n)), scenarios: byId(folder, '.in'), transcripts: byId(folder, '.out') };
  if (kind === null) return { ...common, found: new Map(), shown: { ids: [], rows: [], problems: [] } };
  const found = locateSentences({ spec, sentences: references.sentences, kind });
  const shown = shownSamples(spec, kind);
  return { ...common, found, shown, shownIds: shown.ids };
}

function textsOf(ctx, specText) {
  const samples = [...ctx.folder.files].map(([name, bytes]) => ({ path: `samples/${name}`, text: bytes.toString('utf8') }));
  return [{ path: `todo/tasks/${ctx.task}.md`, text: specText }, ...samples];
}

const CHECKS = Object.freeze([
  ['file', fileFindings],
  ['header', headerFindings],
  ['sections', sectionFindings],
  ['r-ids', idFindings],
  ['sentences', sentenceFindings],
  ['T/G', timeFindings],
  ['samples', sampleFindings],
  ['sample-forms', formFindings],
  ['sample-env', envFindings],
  ['no-needle', textFindings],
]);

/** The lines of every check: `ok <name>` or one `FAIL <name> — <finding>` per finding. `extra` adds findings to a check by name. */
function reportLines(ctx, extra) {
  return CHECKS.flatMap(([name, check]) => {
    const findings = ctx.kind === null && NEEDS_KIND.has(name) ? ['not checked: the Kind row names none of the three kinds'] : [...check(ctx), ...(extra[name] ?? [])];
    return findings.length === 0 ? [`ok ${name}`] : findings.map(text => `FAIL ${name} — ${text}`);
  });
}

async function runnerFindings(ctx, dir, seams) {
  if (ctx.kind === null || ctx.kind === 'olympiad') return ok([]);
  const own = seams.tmp === undefined ? mkdtempSync(path.join(os.tmpdir(), 'conformance-run-')) : null;
  try {
    return await parseSamples({ dir, kind: ctx.kind, tmp: seams.tmp ?? own, python: seams.python, runner: seams.runner, timeoutMs: seams.timeoutMs });
  } finally {
    if (own !== null) rmSync(own, { recursive: true, force: true });
  }
}

/**
 * `{ exit, lines, errors }` for the command's arguments. `seams` (tests only): `python`, `runner`, `timeoutMs`, `tmp` for the runner's run, `envLists` for the env check.
 */
export async function run(argv, seams = {}) {
  const options = parseOptions(argv);
  if (!options.ok) return usage(options.reason);
  const references = loadReferences(options.value);
  if (!references.ok) return usage(references.reason);
  const repo = path.resolve(options.value.repo);
  const specPath = path.join(repo, 'todo', 'tasks', `${options.value.task}.md`);
  const dir = path.join(repo, 'tasks', options.value.task, 'samples');
  const specBytes = readFile(specPath);
  const folder = readSamplesFolder(dir);
  if (!specBytes.ok || !folder.ok) return usage((specBytes.ok ? folder : specBytes).reason);
  const spec = parseSpec(specBytes.value);
  if (!spec.ok) return { exit: EXIT_DEVIATION, lines: [`FAIL file — line ${spec.line}: ${spec.reason}; the spec is not read further`], errors: [] };
  const ctx = { ...contextOf({ options: options.value, references: references.value, spec: spec.value, folder: folder.value }), envLists: seams.envLists };
  const parsed = await runnerFindings(ctx, dir, seams);
  if (!parsed.ok) return usage(parsed.reason);
  const full = { ...ctx, texts: textsOf(ctx, specBytes.value.toString('utf8')) };
  const lines = reportLines(full, { samples: parsed.value });
  return { exit: lines.every(line => line.startsWith('ok ')) ? 0 : EXIT_DEVIATION, lines, errors: [] };
}
