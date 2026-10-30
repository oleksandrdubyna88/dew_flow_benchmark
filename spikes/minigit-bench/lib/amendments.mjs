// `inputs/amendments.md` read by code (E3b.S3 header; S4, S5 and the resume check use it). Two kinds of dated line, and nothing else counts:
//   a by-hand verdict   YYYY-MM-DD | G-<n> | holds|refuted|explained | <rid> | <evidence path> | <who>
//   an instrument line  YYYY-MM-DD | instrument | harness:<12 hex of the accepted identity> | <reason, with its G-id>
//   an input amendment YYYY-MM-DD | instrument | input:<file>:<12 hex of the sha256 of the new content> | <reason, with its G-id>   (quiet.json, package-flags.json only; the guard checks it)
//   a budget raise     YYYY-MM-DD | budget-raise | pair:<k> | n:<re-runs> | usd:<amount> | <reason>      (the ONE permitted raise of the $40; `bare-limits.mjs` checks the amount)
// A line that starts like one of them and is not valid is REPORTED (`malformed`, with why), never silently dropped and never counted. Prose is ignored.
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const VERDICTS = Object.freeze(['holds', 'refuted', 'explained']);

const candidate = line => /^\d/.test(line) || /^G-\d+ \|/.test(line);

const rest = (f, from) => f.slice(from).join(' | ');
const bad = malformed => ({ malformed });

/** An input amendment line: the file and the 12 hex are in `input`; it needs a reason. */
const parseInput = (f, input) => (f[3] ? { input: { date: f[0], file: input[1], hash12: input[2], reason: rest(f, 3) } } : bad('an input line is: date | instrument | input:<file>:<12 hex> | reason'));

/** `date | instrument | input:<file>:<12 hex> | reason`, or `date | instrument | harness:<12 hex> | reason`. */
function parseInstrument(f) {
  const input = /^input:([A-Za-z0-9_.-]+):([0-9a-f]{12})$/.exec(f[2] ?? '');
  if (input) return parseInput(f, input);
  const identity = /^harness:([0-9a-f]{12})$/.exec(f[2] ?? '')?.[1];
  return identity && f[3] ? { instrument: { date: f[0], identity, reason: rest(f, 3) } } : bad('an instrument line is: date | instrument | harness:<12 hex> | reason, or date | instrument | input:<file>:<12 hex> | reason');
}

/** `date | budget-raise | pair:<1-3> | n:<re-runs> | usd:<amount> | reason`. */
function parseRaise(f) {
  const [pair, n, usd] = [/^pair:([1-3])$/.exec(f[2] ?? ''), /^n:(\d+)$/.exec(f[3] ?? ''), /^usd:(\d+(?:\.\d+)?)$/.exec(f[4] ?? '')];
  return [pair, n, usd, f[5]].every(Boolean) ? { raise: { date: f[0], pair: Number(pair[1]), n: Number(n[1]), usd: Number(usd[1]), reason: rest(f, 5) } } : bad('a budget raise is: date | budget-raise | pair:<1-3> | n:<re-runs> | usd:<amount> | reason');
}

const sixFields = f => f.length >= 6 && f.slice(3, 6).every(x => x !== '');

/** The sentence for a by-hand verdict line that is not valid, or null. */
function verdictProblem(f) {
  if (!/^G-\d+$/.test(f[1] ?? '')) return `the second field must be G-<n>, 'instrument' or 'budget-raise', got '${f[1] ?? ''}'`;
  if (!VERDICTS.includes(f[2])) return `the verdict must be one of ${VERDICTS.join(', ')}, got '${f[2] ?? ''}'`;
  return sixFields(f) ? null : 'a verdict line has six fields: date | G-n | verdict | rid | evidence | who';
}

/** `date | G-<n> | holds|refuted|explained | rid | evidence | who`. */
function parseVerdict(f) {
  const problem = verdictProblem(f);
  return problem ? bad(problem) : { verdict: { date: f[0], g: f[1], verdict: f[2], rid: f[3], evidence: f[4], who: f[5] } };
}

/** The line kinds by their second field; anything else is a verdict line. */
const KINDS = { instrument: parseInstrument, 'budget-raise': parseRaise };

function parseLine(line) {
  const f = line.split(' | ').map(x => x.trim());
  if (!DATE.test(f[0])) return bad('the line has no valid YYYY-MM-DD date');
  return (Object.hasOwn(KINDS, f[1]) ? KINDS[f[1]] : parseVerdict)(f);
}

/** The values of `key` of the parsed lines that carry it. */
const pick = (parsed, key) => parsed.flatMap(p => (p[key] ? [p[key]] : []));

/** The text -> `{ verdicts, instrument, raises, malformed: [{ line, why }] }`. */
export function parseAmendments(text) {
  const parsed = text.split('\n').map(l => l.trim()).filter(candidate).map(line => ({ line, ...parseLine(line) }));
  return {
    verdicts: pick(parsed, 'verdict'), instrument: pick(parsed, 'instrument'), inputs: pick(parsed, 'input'), raises: pick(parsed, 'raise'),
    malformed: parsed.flatMap(p => (p.malformed ? [{ line: p.line, why: p.malformed }] : [])),
  };
}

/** The valid by-hand verdicts about one session: the `handVerdicts` input of `bareOutcome`. */
export const handVerdictsFor = (text, rid) => parseAmendments(text).verdicts.filter(v => v.rid === rid);
