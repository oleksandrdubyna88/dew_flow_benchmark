// What `freeze.mjs record` is given besides the repository (F2.S2 (d), amendments 1 and 3): the evidence file, and the engine's workspaces check. Both are READ, never called: lang-bench imports nothing of lang-explore.
//   --evidence <file>           JSON `{ provisionalFrom, workspacesCheck: { commit, exit, date }, leakcheck: { freezeCommit, scratchWorkspace, workspaceCommit }, state? }`, each result `{ commit, exit: 0, date }`:
//                               the three clean `leakcheck` results of (e) steps 5, 6 and 9, the engine check's result at W, and the date of the push. `state`, when given, must be `accepted`: the file holds one state only.
//   --workspaces-check <file>   the output of `explore.mjs workspaces --check --root <checkout at W>`, whose LAST line is exactly `workspaces-check ok commit <W> tasks 04,05,06,07,08,10 stacks csharp,node`.
import { readFileSync } from 'node:fs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { SIX } from './freeze-inputs.mjs';

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

/** The sentences for one `{ commit, exit, date }` result named `name`: a 40-hex commit, exit 0, a day. */
function resultProblems(name, entry) {
  if (!isObject(entry)) return [`the evidence has no ${name} result`];
  const found = [];
  if (typeof entry.commit !== 'string' || !HEX40.test(entry.commit)) found.push(`${name}.commit is not a 40-hex commit`);
  if (entry.exit !== 0) found.push(`${name}.exit is ${JSON.stringify(entry.exit)}, not 0: only a clean result is recorded`);
  if (typeof entry.date !== 'string' || !DAY.test(entry.date)) found.push(`${name}.date is not a yyyy-mm-dd day`);
  return found;
}

/** The sentences for the whole evidence against the commits it must name (`commit` is W, `freeze` is C). */
function evidenceProblems({ evidence, commit, freeze }) {
  const leak = isObject(evidence.leakcheck) ? evidence.leakcheck : {};
  const results = [...KEYS.flatMap(key => resultProblems(`leakcheck.${key}`, leak[key])), ...resultProblems('workspacesCheck', evidence.workspacesCheck)];
  const named = [[leak.freezeCommit, freeze, 'leakcheck.freezeCommit', 'C'], [leak.workspaceCommit, commit, 'leakcheck.workspaceCommit', 'W'], [evidence.workspacesCheck, commit, 'workspacesCheck', 'W']];
  const wrong = named.filter(([entry, expected]) => isObject(entry) && entry.commit !== expected).map(([, expected, name, what]) => `${name}.commit is not ${what} (${expected})`);
  const day = typeof evidence.provisionalFrom === 'string' && DAY.test(evidence.provisionalFrom) ? [] : ['provisionalFrom is not the push\'s yyyy-mm-dd day'];
  const state = evidence.state === undefined || evidence.state === 'accepted' ? [] : [`state is ${JSON.stringify(evidence.state)}: the record holds one state only, "accepted"`];
  return [...results, ...wrong, ...day, ...state];
}

/** ok(evidence) read from `file` and judged against W (`commit`) and C (`freeze`); a fail carries every sentence as `problems`. */
export function readEvidence({ file, commit, freeze }) {
  const text = readText(file, 'the evidence file');
  if (!text.ok) return fail(text.reason, { problems: [text.reason] });
  let evidence;
  try {
    evidence = JSON.parse(text.value);
  } catch (error) {
    return fail(`the evidence file ${file} is not JSON (${error.message})`, { problems: [`the evidence file ${file} is not JSON (${error.message})`] });
  }
  const problems = isObject(evidence) ? evidenceProblems({ evidence, commit, freeze }) : [`the evidence file ${file} is not one JSON object`];
  return problems.length === 0 ? ok(evidence) : fail(problems[0], { problems });
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
