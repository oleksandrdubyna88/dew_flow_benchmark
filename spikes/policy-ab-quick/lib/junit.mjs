// Pure: node --test junit output -> counts and per-case status.
const unescape = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');

export function parseJunit(xml) {
  const cases = xml.split('<testcase ').slice(1).map(chunk => {
    const name = /^name="([^"]*)"/.exec(chunk)?.[1] ?? '';
    const body = chunk.split('</testcase>')[0];
    const status = body.includes('<failure') || body.includes('<error') ? 'fail' : body.includes('<skipped') ? 'skipped' : 'pass';
    return { name: unescape(name), status };
  });
  const count = status => cases.filter(c => c.status === status).length;
  return { tests: cases.length, passed: count('pass'), failed: count('fail'), skipped: count('skipped'), cases };
}

/** The verdict for one run: pass / fail, never inferred from an absent report. */
export function verdictOf(parsed, exitCode) {
  if (parsed.tests === 0) return 'no-report';
  return parsed.failed === 0 && exitCode === 0 ? 'pass' : 'fail';
}
