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
const STACK_WORDS_FILE = fileURLToPath(new URL('../inputs/stack-words-lang.json', import.meta.url));
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

/** `ok(f(value))` for an ok result, the fail otherwise. */
const mapOk = (result, make) => (result.ok ? ok(make(result.value)) : result);

/** The steps run in order on a state: a step returns `ok(more)` to add to the state or a fail, which ends the run. */
const pipeline = (steps, state) => steps.reduce((acc, step) => (acc.ok ? mapOk(step(acc.value), more => ({ ...acc.value, ...more })) : acc), ok(state));

const readDocument = (bytes, file) => {
  const parsed = parseFormat(bytes);
  return parsed.ok ? ok({ format: parsed.format }) : fail(`${file} is not a readable FORMAT: line ${parsed.line}: ${parsed.reason}`);
};

/** ok({ format, facts, sentences, known, stackWords, literals }): everything read from outside the spec, or the first fail. FORMAT's own faults are format errors (exit 4). */
const loadReferences = (options, stackWordsFile) => pipeline([
  () => mapOk(readFile(options.format), bytes => ({ bytes })),
  ({ bytes }) => readDocument(bytes, options.format),
  ({ format }) => mapOk(readFacts(format), facts => ({ facts })),
  ({ format }) => mapOk(readSentences(format), sentences => ({ sentences })),
  () => mapOk(loadWordFile(NEEDLES_FILE), known => ({ known })),
  () => mapOk(loadWordFile(stackWordsFile), stackWords => ({ stackWords })),
  () => mapOk(options.needles === undefined ? ok(undefined) : readNeedles(options.needles), literals => ({ literals })),
], {});

/** ok(matchers) from a needle file of the push check's shape (`words`, `substrings`), or a fail naming the file. */
function loadWordFile(file) {
  const text = readFile(file);
  if (!text.ok) return text;
  const parsed = parseKnownNeedles(parseJson(text.value.toString('utf8')));
  return parsed.ok ? parsed : fail(`${file}: ${parsed.reason}`);
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

const needsRunner = ctx => ctx.kind !== null && ctx.kind !== 'olympiad';
const runnerArgs = (ctx, dir, seams, tmp) => ({ dir, kind: ctx.kind, tmp, python: seams.python, runner: seams.runner, timeoutMs: seams.timeoutMs });

/** The runner's run in a TMPDIR of its own, made here and removed after. */
async function inOwnTemp(ctx, dir, seams) {
  const own = mkdtempSync(path.join(os.tmpdir(), 'conformance-run-'));
  try {
    return await parseSamples(runnerArgs(ctx, dir, seams, own));
  } finally {
    rmSync(own, { recursive: true, force: true });
  }
}

/** ok([finding]) of the runner's parse of the pairs; an olympiad task or an unknown kind has none to parse. */
function runnerFindings(ctx, dir, seams) {
  if (!needsRunner(ctx)) return ok([]);
  return seams.tmp === undefined ? inOwnTemp(ctx, dir, seams) : parseSamples(runnerArgs(ctx, dir, seams, seams.tmp));
}

/** The spec and the samples folder of the task, read; the spec's bytes may still be refused later. */
function readInputs(options) {
  const repo = path.resolve(options.repo);
  const dir = path.join(repo, 'tasks', options.task, 'samples');
  const specBytes = readFile(path.join(repo, 'todo', 'tasks', `${options.task}.md`));
  const folder = readSamplesFolder(dir);
  return specBytes.ok ? mapOk(folder, value => ({ dir, specBytes: specBytes.value, folder: value })) : specBytes;
}

/** A spec the block reader refuses is a deviation of the file (exit 1), not a usage error. */
function readSpec(specBytes) {
  const spec = parseSpec(specBytes);
  return spec.ok ? ok({ spec: spec.value }) : fail(`line ${spec.line}: ${spec.reason}; the spec is not read further`, { deviation: true });
}

const prepare = (argv, seams) => pipeline([
  () => mapOk(parseOptions(argv), options => ({ options })),
  ({ options }) => mapOk(loadReferences(options, seams.stackWordsFile ?? STACK_WORDS_FILE), references => ({ references })),
  ({ options }) => readInputs(options),
  ({ specBytes }) => readSpec(specBytes),
], {});

const refused = prepared => (prepared.deviation ? { exit: EXIT_DEVIATION, lines: [`FAIL file — ${prepared.reason}`], errors: [] } : usage(prepared.reason));

function finish(ctx, found) {
  const lines = reportLines(ctx, { samples: found });
  return { exit: lines.every(line => line.startsWith('ok ')) ? 0 : EXIT_DEVIATION, lines, errors: [] };
}

/**
 * `{ exit, lines, errors }` for the command's arguments. `seams` (tests only): `python`, `runner`, `timeoutMs`, `tmp` for the runner's run, `envLists` for the env check, `stackWordsFile`.
 */
export async function run(argv, seams = {}) {
  const prepared = prepare(argv, seams);
  if (!prepared.ok) return refused(prepared);
  const ctx = { ...contextOf(prepared.value), envLists: seams.envLists };
  const parsed = await runnerFindings(ctx, prepared.value.dir, seams);
  if (!parsed.ok) return usage(parsed.reason);
  return finish({ ...ctx, texts: textsOf(ctx, prepared.value.specBytes.toString('utf8')) }, parsed.value);
}
