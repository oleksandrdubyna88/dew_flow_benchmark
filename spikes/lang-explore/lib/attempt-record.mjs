// The complete validator for the runner's record of ONE attempt (F1.S2, section 2.9). The runner writes its JSON in place, not atomically, so a kill mid-write leaves half a file; and a record can be whole JSON and still be no
// judgement (the marker keys of a finished run missing, a case missing or twice, a field of a result missing). Any of that is an ENVIRONMENT attempt, never a functional failure, even when the results the record does hold carry
// a non-pass verdict: a half-written record is no evidence of anything. Pure: the text and the expectation in, the first defect out as one sentence starting `record-invalid:`, or null.
export const REQUIRED_CASE_FIELDS = Object.freeze(['id', 'status', 'reason', 'wallMs', 'lifecycle.memory.peakBytes', 'lifecycle.memory.maxBytes', 'lifecycle.leftover']);
/** The top-level keys the pinned runner writes when it ran to its end. */
export const COMPLETION_KEYS = Object.freeze(['format', 'runner', 'kind', 'build', 'launch', 'cases']);

const invalid = why => `record-invalid: ${why}`;
const at = (o, dotted) => dotted.split('.').reduce((x, k) => (x === undefined || x === null ? undefined : x[k]), o);
const named = list => list.join(', ');

const isObject = x => x !== null && typeof x === 'object' && !Array.isArray(x);

function parseJson(text) {
  try { return JSON.parse(text); } catch (e) { return invalid(`the record does not parse (${e.message.slice(0, 80)}; ${String(text).length} bytes)`); }
}

/** The record as an object, or the sentence saying why the text is none. */
const parsed = text => { const r = parseJson(text); return typeof r === 'string' || isObject(r) ? r : invalid('the record is not a JSON object'); };

const markerProblem = ({ record }) => {
  const missing = COMPLETION_KEYS.filter(k => !Object.hasOwn(record, k));
  return missing.length === 0 ? null : invalid(`the runner's completion marker is incomplete: ${named(missing)} missing`);
};
const pinProblem = ({ record, pinnedRunner }) => {
  if (record.format !== pinnedRunner.format) return invalid(`format is ${JSON.stringify(record.format)}, the pinned runner writes ${pinnedRunner.format}`);
  return record.runner === pinnedRunner.version ? null : invalid(`runner is ${JSON.stringify(record.runner)}, the pinned runner is ${pinnedRunner.version}`);
};
const listProblem = ({ record }) => (Array.isArray(record.cases) ? null : invalid('cases is not a list'));
const idsProblem = ({ record, expected }) => {
  const ids = record.cases.map(c => c?.id);
  const extra = ids.filter(id => !expected.includes(id));
  const missing = expected.filter(id => !ids.includes(id));
  const twice = [...new Set(ids.filter((id, i) => ids.indexOf(id) !== i))];
  const parts = [[missing, 'missing'], [extra, 'unexpected'], [twice, 'duplicate']].filter(([l]) => l.length > 0).map(([l, w]) => `${named(l)} (${w})`);
  return parts.length === 0 ? null : invalid(`the record does not hold exactly the expected cases (${parts.join('; ')})`);
};
const fieldsProblem = ({ record }) => {
  const gaps = record.cases.flatMap((c, i) => REQUIRED_CASE_FIELDS.filter(f => at(c, f) === undefined).map(f => `${c?.id ?? `cases[${i}]`}.${f}`));
  return gaps.length === 0 ? null : invalid(`a result lacks a required field: ${named(gaps)}`);
};

const CHECKS = Object.freeze([markerProblem, pinProblem, listProblem, fieldsProblem, idsProblem]);

/**
 * Why the runner's record `text` is not the complete judgement of exactly the `expected` case ids, or null. `pinnedRunner` is `{ format, version }` (`harness-pin.mjs`'s `runnerRecordPinNow`): the pinned values are read, never retyped.
 * The first defect only: the driver turns any sentence into an environment attempt and never reads the cases of a record that has one.
 */
export function attemptRecordProblem({ text, expected, pinnedRunner }) {
  const record = parsed(text);
  if (typeof record === 'string') return record;
  return CHECKS.values().map(check => check({ record, expected, pinnedRunner })).find(Boolean) ?? null; // lazy: a later check reads what an earlier one proved
}
