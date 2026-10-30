// `inputs/amendments.md` read by code (E3b.S3 header; S4, S5 and the resume check use it). Two kinds of dated line, and nothing else counts:
//   a by-hand verdict   YYYY-MM-DD | G-<n> | holds|refuted|explained | <rid> | <evidence path> | <who>
//   an instrument line  YYYY-MM-DD | instrument | harness:<12 hex of the accepted identity> | <reason, with its G-id>
// A line that starts like one of them and is not valid is REPORTED (`malformed`, with why), never silently dropped and never counted. Prose is ignored.
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const VERDICTS = Object.freeze(['holds', 'refuted', 'explained']);

const candidate = line => /^\d/.test(line) || /^G-\d+ \|/.test(line);

function parseLine(line) {
  const f = line.split(' | ').map(x => x.trim());
  if (!DATE.test(f[0])) return { malformed: 'the line has no valid YYYY-MM-DD date' };
  if (f[1] === 'instrument') {
    const identity = /^harness:([0-9a-f]{12})$/.exec(f[2] ?? '')?.[1];
    return identity && f[3] ? { instrument: { date: f[0], identity, reason: f.slice(3).join(' | ') } } : { malformed: 'an instrument line is: date | instrument | harness:<12 hex> | reason' };
  }
  if (!/^G-\d+$/.test(f[1] ?? '')) return { malformed: `the second field must be G-<n> or 'instrument', got '${f[1] ?? ''}'` };
  if (!VERDICTS.includes(f[2])) return { malformed: `the verdict must be one of ${VERDICTS.join(', ')}, got '${f[2] ?? ''}'` };
  if (f.length < 6 || f.slice(3, 6).some(x => x === '')) return { malformed: 'a verdict line has six fields: date | G-n | verdict | rid | evidence | who' };
  return { verdict: { date: f[0], g: f[1], verdict: f[2], rid: f[3], evidence: f[4], who: f[5] } };
}

/** The text -> `{ verdicts, instrument, malformed: [{ line, why }] }`. */
export function parseAmendments(text) {
  const parsed = text.split('\n').map(l => l.trim()).filter(candidate).map(line => ({ line, ...parseLine(line) }));
  return {
    verdicts: parsed.flatMap(p => (p.verdict ? [p.verdict] : [])),
    instrument: parsed.flatMap(p => (p.instrument ? [p.instrument] : [])),
    malformed: parsed.flatMap(p => (p.malformed ? [{ line: p.line, why: p.malformed }] : [])),
  };
}

/** The valid by-hand verdicts about one session: the `handVerdicts` input of `bareOutcome`. */
export const handVerdictsFor = (text, rid) => parseAmendments(text).verdicts.filter(v => v.rid === rid);
