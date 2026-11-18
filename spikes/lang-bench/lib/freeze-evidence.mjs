// What `freeze.mjs record` is given besides the repository (F2.S2 (d), amendments 1 and 3): the evidence file, and the engine's workspaces check. Both are READ, never called: lang-bench imports nothing of lang-explore.
//   --evidence <file>           JSON `{ provisionalFrom, workspacesCheck: { commit, exit, date }, leakcheck: { freezeCommit, scratchWorkspace, workspaceCommit }, state? }`, each result `{ commit, exit: 0, date }`:
//                               the three clean `leakcheck` results of (e) steps 5, 6 and 9, the engine check's result at W, and the date of the push. `state`, when given, must be `accepted`: the file holds one state only.
//   --workspaces-check <file>   the output of `explore.mjs workspaces --check --root <checkout at W>`, whose LAST line is exactly `workspaces-check ok commit <W> tasks 04,05,06,07,08,10 stacks csharp,node`.
import { readFileSync } from 'node:fs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { SIX, unless } from './freeze-inputs.mjs';
import { tagDate } from './freeze-rows.mjs';

const KEYS = Object.freeze(['freezeCommit', 'scratchWorkspace', 'workspaceCommit']);
const DAY = /^\d{4}-\d{2}-\d{2}$/;
const HEX40 = /^[0-9a-f]{40}$/;
const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);

/** The text of a file, or a fail naming it. */
function readText(file, what) {
  try {
    return ok(readFileSync(file, 'utf8'));
  } catch (error) {
    return fail(`${what} ${file} cannot be read (${error.code ?? error.message})`);
  }
}

/** The rules of one `{ commit, exit, date }` result: [holds, the sentence for a result that breaks it]. */
const RESULT_RULES = Object.freeze([
  [entry => typeof entry.commit === 'string' && HEX40.test(entry.commit), name => `${name}.commit is not a 40-hex commit`],
  [entry => entry.exit === 0, (name, entry) => `${name}.exit is ${JSON.stringify(entry.exit)}, not 0: only a clean result is recorded`],
  [entry => typeof entry.date === 'string' && DAY.test(entry.date), name => `${name}.date is not a yyyy-mm-dd day`],
]);

/** The sentences for one `{ commit, exit, date }` result named `name`: a 40-hex commit, exit 0, a day. */
function resultProblems(name, entry) {
  if (!isObject(entry)) return [`the evidence has no ${name} result`];
  return RESULT_RULES.filter(([holds]) => !holds(entry)).map(([, sentence]) => sentence(name, entry));
}

const dayOfTag = tag => {
  const date = tagDate(tag);
  return date === null ? null : `${date.slice(0, 4)}-${date.slice(4, 6)}-${date.slice(6)}`;
};

/** The sentences for the scratch W″: a commit that is neither C nor W (it is the proof made before the tag, on a commit that is never pushed). */
function scratchProblems({ leak, commit, freeze }) {
  const scratch = leak.scratchWorkspace?.commit;
  if (scratch === freeze) return ['leakcheck.scratchWorkspace.commit is C: the scratch W″ is a different commit, made of C and the workspaces before the tag is pushed'];
  return scratch === commit ? ['leakcheck.scratchWorkspace.commit is W: the scratch W″ is the proof made before the push, never the merged workspace commit'] : [];
}

/** The sentences for the order of the dates: the leak check of C, the scratch proof, the push, then the checks of W; and the push is not before the tag's own date. */
function orderProblems({ evidence, tag }) {
  const leak = evidence.leakcheck;
  const steps = [['leakcheck.freezeCommit.date', leak.freezeCommit?.date], ['leakcheck.scratchWorkspace.date', leak.scratchWorkspace?.date], ['provisionalFrom', evidence.provisionalFrom], ['leakcheck.workspaceCommit.date', leak.workspaceCommit?.date]];
  const early = steps.slice(1).flatMap(([name, date], index) => (date < steps[index][1] ? [`${name} (${date}) is before ${steps[index][0]} (${steps[index][1]}): the steps run in the order the freeze procedure gives (the leak check of C, the scratch proof, the push, the leak check of W)`] : []));
  const tagDay = dayOfTag(tag);
  return tagDay !== null && evidence.provisionalFrom < tagDay ? [...early, `provisionalFrom (${evidence.provisionalFrom}) is before the tag's own date (${tagDay})`] : early;
}

/** The sentences about the commits the results name: C for the leak check of the freeze commit, W for the leak check of W and the workspaces check. */
function namedProblems({ evidence, commit, freeze }) {
  const leak = evidence.leakcheck;
  const named = [[leak.freezeCommit, freeze, 'leakcheck.freezeCommit', 'C'], [leak.workspaceCommit, commit, 'leakcheck.workspaceCommit', 'W'], [evidence.workspacesCheck, commit, 'workspacesCheck', 'W']];
  return named.filter(([entry, expected]) => isObject(entry) && entry.commit !== expected).map(([, expected, name, what]) => `${name}.commit is not ${what} (${expected})`);
}

/** The sentences about the push's day and the state field. */
function fieldProblems(evidence) {
  const day = unless(typeof evidence.provisionalFrom === 'string' && DAY.test(evidence.provisionalFrom), 'provisionalFrom is not the push\'s yyyy-mm-dd day');
  const state = unless(evidence.state === undefined || evidence.state === 'accepted', `state is ${JSON.stringify(evidence.state)}: the record holds one state only, "accepted"`);
  return [...day, ...state];
}

/** The sentences of the shape of every result and of the fields; the cross-checks only run once these hold. */
function ownProblems({ evidence, commit, freeze }) {
  const leak = evidence.leakcheck;
  const results = [...KEYS.flatMap(key => resultProblems(`leakcheck.${key}`, leak[key])), ...resultProblems('workspacesCheck', evidence.workspacesCheck)];
  return [...results, ...namedProblems({ evidence, commit, freeze }), ...fieldProblems(evidence)];
}

/** The sentences for the whole evidence against the commits it must name (`commit` is W, `freeze` is C) and the tag. */
function evidenceProblems({ evidence, commit, freeze, tag }) {
  const given = isObject(evidence.leakcheck) ? evidence : { ...evidence, leakcheck: {} };
  const own = ownProblems({ evidence: given, commit, freeze });
  return own.length > 0 ? own : [...scratchProblems({ leak: given.leakcheck, commit, freeze }), ...orderProblems({ evidence: given, tag })];
}

/** ok(the parsed evidence object) from the file's text, or a fail that carries its sentence as `problems`. */
function parseEvidence({ file, text }) {
  try {
    const evidence = JSON.parse(text);
    return isObject(evidence) ? ok(evidence) : fail(`the evidence file ${file} is not one JSON object`, { problems: [`the evidence file ${file} is not one JSON object`] });
  } catch (error) {
    return fail(`the evidence file ${file} is not JSON (${error.message})`, { problems: [`the evidence file ${file} is not JSON (${error.message})`] });
  }
}

/** ok(evidence) read from `file` and judged against W (`commit`), C (`freeze`) and the tag; a fail carries every sentence as `problems`. */
export function readEvidence({ file, commit, freeze, tag }) {
  const text = readText(file, 'the evidence file');
  if (!text.ok) return fail(text.reason, { problems: [text.reason] });
  const parsed = parseEvidence({ file, text: text.value });
  if (!parsed.ok) return parsed;
  const problems = evidenceProblems({ evidence: parsed.value, commit, freeze, tag });
  return problems.length === 0 ? ok(parsed.value) : fail(problems[0], { problems });
}

/** ok(true) when the engine's check output ends in the exact success line for W; a fail names the file and what the last line was. */
export function readWorkspacesCheck({ file, commit }) {
  const text = readText(file, 'the workspaces-check output');
  if (!text.ok) return fail(`workspaces-check: ${text.reason}`);
  const lines = text.value.split('\n').filter(line => line !== '');
  const expected = `workspaces-check ok commit ${commit} tasks ${SIX.join(',')} stacks csharp,node`;
  const last = lines.at(-1) ?? '';
  return last === expected ? ok(true) : fail(`workspaces-check: the last line of ${file} is ${JSON.stringify(last)}, not ${JSON.stringify(expected)}: the engine's check must have passed at W`);
}
