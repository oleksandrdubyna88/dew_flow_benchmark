// The command line of `freeze.mjs` as a function a test can call: `runFreeze(argv, seams)` -> `{ exit, stdout, stderr }` (F2.S2 (d)). One verb per call; each reads, prints and exits, and only `hashes --write` and `record`
// write, one file each, through an owned temporary file and one rename. Exit codes: 0 done; 1 a check is red (`check-*`, and `record`'s verifications); 2 usage; 3 a refusal (`plan`, `hashes`, and `record`'s inputs); 4 the
// environment (git, the network, a file system). `seams` (tests only): `now` (a clock), `conformance` (the checker's `run`), `io` (the writer's file operations), `needles` (the private needle file `plan` passes on).
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { checkFreezeCommit } from './freeze-commit.mjs';
import { hashList, writeHashList } from './freeze-hashes.mjs';
import { failWith, inputsDirOf, isEnv, loadPin, problemsOf } from './freeze-inputs.mjs';
import { planFreeze } from './freeze-plan.mjs';
import { recordFreeze } from './freeze-record.mjs';
import { checkLocalTag, checkRemoteTag } from './freeze-tags.mjs';
import { workspaceProblems } from './spec-freeze.mjs';

const EXIT = Object.freeze({ done: 0, red: 1, usage: 2, refused: 3, env: 4 });
const OPTIONS = {
  repo: { type: 'string' }, inputs: { type: 'string' }, record: { type: 'string' }, pins: { type: 'string' }, ref: { type: 'string' }, date: { type: 'string' }, needles: { type: 'string' }, before: { type: 'string' }, after: { type: 'string' },
  tag: { type: 'string' }, commit: { type: 'string' }, base: { type: 'string' }, 'message-file': { type: 'string' }, write: { type: 'boolean' }, evidence: { type: 'string' }, 'workspaces-check': { type: 'string' },
  supersedes: { type: 'string', multiple: true }, why: { type: 'string', multiple: true },
};
/** The flags every verb takes: where the bench's inputs are and which FORMAT record the pin is read from. */
const COMMON = Object.freeze(['inputs', 'record', 'pins']);
/** Each verb: the flags it needs, the flags it may take, and the exit of a failed rule (`record` carries its own). */
const VERBS = Object.freeze({
  plan: { needs: ['repo', 'ref', 'date'], takes: ['inputs', 'record', 'needles'], exit: EXIT.refused },
  'check-commit': { needs: ['repo', 'before', 'after'], takes: [], exit: EXIT.red },
  'check-local-tag': { needs: ['repo', 'tag', 'commit', 'message-file'], takes: [], exit: EXIT.red },
  'check-tag': { needs: ['repo', 'tag', 'commit'], takes: ['inputs', 'record', 'message-file'], exit: EXIT.red },
  'check-workspace': { needs: ['repo', 'commit'], takes: ['tag', 'base'], exit: EXIT.red },
  hashes: { needs: ['repo', 'commit'], takes: ['inputs', 'write'], exit: EXIT.refused },
  record: { needs: ['repo', 'tag', 'commit', 'evidence', 'workspaces-check'], takes: ['inputs', 'record', 'supersedes', 'why'], exit: EXIT.refused },
});
const USAGE = `usage: freeze.mjs <${Object.keys(VERBS).join('|')}> --repo <task repository> ...; see spikes/lang-bench/freeze.mjs`;

const done = (exit, stdout = '', stderr = '') => ({ exit, stdout, stderr });
const usage = reason => done(EXIT.usage, '', `freeze: ${reason}\n${USAGE}\n`);
const lines = (verb, sentences) => `${sentences.map(sentence => `freeze: ${verb}: ${sentence}`).join('\n')}\n`;
const noted = notes => (notes.length > 0 ? `${notes.map(note => `freeze: ${note}`).join('\n')}\n` : '');

/** `{ parsed }` of the arguments under the option table, or `{ error }` with the parser's own sentence. */
function readArgs(argv) {
  try {
    return { parsed: parseArgs({ args: argv, options: OPTIONS, strict: true, allowPositionals: true }) };
  } catch (error) {
    return { error: error.message };
  }
}

/** The sentence when the positional arguments are not exactly one known verb, else null. */
function verbProblem({ verb, extra }) {
  if (verb === undefined) return 'give a verb';
  if (VERBS[verb] === undefined) return `${verb} is not a verb`;
  return extra.length > 0 ? `${verb}: no positional arguments` : null;
}

/** The sentence about the flags given: one that is not the verb's, or one it needs and lacks, else null. */
function flagProblem({ verb, spec, values }) {
  const stray = Object.keys(values).filter(name => ![...spec.needs, ...spec.takes, ...COMMON].includes(name));
  const missing = spec.needs.filter(name => values[name] === undefined);
  if (stray.length > 0) return `${verb}: --${stray.join(', --')} is not a flag of it`;
  return missing.length > 0 ? `${verb}: missing --${missing.join(', --')}` : null;
}

/** `{ verb, spec, values }` for a legal command line, or `{ error }`: a verb, only its flags, none missing. */
function parse(argv) {
  const read = readArgs(argv);
  if (read.error !== undefined) return read;
  const [verb, ...extra] = read.parsed.positionals;
  const error = verbProblem({ verb, extra }) ?? flagProblem({ verb, spec: VERBS[verb], values: read.parsed.values });
  return error === null ? { verb, spec: VERBS[verb], values: read.parsed.values } : { error };
}

/** The outcome of a failed result as a CLI answer: an environment failure is exit 4, a rule the verb's own exit (or the one the result carries). */
function failed(verb, spec, result) {
  if (isEnv(result)) return done(EXIT.env, '', lines(verb, [result.reason]));
  return done(result.exit ?? spec.exit, '', lines(verb, problemsOf(result)));
}

const paths = values => ({ repo: path.resolve(values.repo), inputs: inputsDirOf(values.inputs) });
const recordFileOf = ({ inputs }, values) => path.resolve(values.record ?? path.join(inputs, 'specs.d', 'format.json'));

async function planVerb({ values, seams }) {
  const where = paths(values);
  const planned = await planFreeze({ repo: where.repo, ref: values.ref, date: values.date, record: recordFileOf(where, values), seams: { now: seams.now, conformance: seams.conformance, needles: values.needles ?? seams.needles } });
  return planned.ok ? ok({ stdout: planned.value.patch, stderr: `freeze: plan: ${planned.value.tag} in the Freeze tag row of the six specs, nothing else\n` }) : planned;
}

async function commitVerb({ values }) {
  const checked = await checkFreezeCommit({ repo: path.resolve(values.repo), before: values.before, after: values.after });
  return checked.ok ? ok({ stdout: `check-commit ok: the six Freeze tag rows say ${checked.value.tag}; no other byte of the six specs, no other path, and not FORMAT changed\n` }) : checked;
}

async function localTagVerb({ values }) {
  const checked = await checkLocalTag({ repo: path.resolve(values.repo), tag: values.tag, commit: values.commit, messageFile: path.resolve(values['message-file']) });
  return checked.ok ? ok({ stdout: `check-local-tag ok: ${values.tag} is annotated, local only, on C ${checked.value.commit}, with the intended message\n` }) : checked;
}

async function tagVerb({ values }) {
  const where = paths(values);
  const pin = loadPin(recordFileOf(where, values));
  if (!pin.ok) return failWith([pin.reason]);
  const message = values['message-file'] === undefined ? undefined : readFileSync(path.resolve(values['message-file']), 'utf8');
  const checked = await checkRemoteTag({ repo: where.repo, tag: values.tag, commit: values.commit, message, pin: pin.value });
  return checked.ok ? ok({ stdout: `check-tag ok: the remote's ${values.tag} is annotated, peels to C ${checked.value.commit} (tag object ${checked.value.tagObject}); the six specs are the gated ones with only their row set; FORMAT is the pinned ${checked.value.formatSha256}\n` }) : checked;
}

async function workspaceVerb({ values }) {
  if ((values.tag === undefined) === (values.base === undefined)) return fail('give exactly one of --tag <tag> or --base <C>', { usage: true });
  const repo = path.resolve(values.repo);
  const base = values.base ?? `refs/tags/${values.tag}`;
  const found = await workspaceProblems({ repo, base, commit: values.commit });
  if (!found.ok) return found;
  return found.value.length === 0 ? ok({ stdout: `check-workspace ok: ${values.commit} descends from ${base}, changes only the twelve workspace folders and keeps every spec and FORMAT blob\n` }) : failWith(found.value);
}

async function hashesVerb({ values, seams }) {
  const where = paths(values);
  if (values.write !== true) {
    const listed = await hashList({ repo: where.repo, commit: values.commit });
    return listed.ok ? ok({ stdout: listed.value.text }) : listed;
  }
  const written = await writeHashList({ repo: where.repo, commit: values.commit, inputs: where.inputs, io: seams.io });
  return written.ok ? ok({ stdout: `${noted(written.value.notes)}hashes: wrote ${written.value.file} (${written.value.entries} entries)\n` }) : written;
}

/** The `--supersedes` and `--why` values as pairs, in the order given; a missing partner is `undefined`, which `record` refuses. */
function pairs(values) {
  const [tags, whys] = [values.supersedes ?? [], values.why ?? []];
  return Array.from({ length: Math.max(tags.length, whys.length) }, (_, index) => ({ tag: tags[index], why: whys[index] }));
}

async function recordVerb({ values, seams }) {
  const where = paths(values);
  const flags = { evidence: path.resolve(values.evidence), check: path.resolve(values['workspaces-check']), supersedes: pairs(values) };
  const recorded = await recordFreeze({ repo: where.repo, tag: values.tag, commit: values.commit, flags, inputs: where.inputs, record: recordFileOf(where, values), io: seams.io });
  return recorded.ok ? ok({ stdout: `${noted(recorded.value.notes)}record: wrote ${recorded.value.file}\n` }) : recorded;
}

const HANDLERS = Object.freeze({ plan: planVerb, 'check-commit': commitVerb, 'check-local-tag': localTagVerb, 'check-tag': tagVerb, 'check-workspace': workspaceVerb, hashes: hashesVerb, record: recordVerb });

/** The CLI answer for a verb's result: its output, a usage line, or the failure's exit and sentences. */
function answerOf(result, parsed) {
  if (result.ok) return done(EXIT.done, result.value.stdout, result.value.stderr ?? '');
  return result.usage === true ? usage(result.reason) : failed(parsed.verb, parsed.spec, result);
}

/** `{ exit, stdout, stderr }` for the command's arguments. A thrown error is the environment's (exit 4), one line, never a stack. */
export async function runFreeze(argv, seams = {}) {
  const parsed = parse(argv);
  if (parsed.error !== undefined) return usage(parsed.error);
  try {
    return answerOf(await HANDLERS[parsed.verb]({ values: parsed.values, seams }), parsed);
  } catch (error) {
    return done(EXIT.env, '', `freeze: ${parsed.verb}: ${error?.message ?? error}\n`);
  }
}
