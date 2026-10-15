// Pure: node --test junit output -> counts and per-case status. A case's KEY is file + enclosing suites + name: two files may
// hold tests of the same name, and a failing set keyed by name alone would call one's failure the other's.
const unescape = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');

// Attribute values are quoted and may hold a raw '>' or newlines (node does not escape '>'), so match whole quoted values.
const ATTRS = '((?:\\s+[\\w:.-]+="[^"]*")*)';
const TOKEN = new RegExp(`<(/?)testsuite\\b${ATTRS}\\s*>|<testcase\\b${ATTRS}\\s*(/?)>`, 'g');
const attr = (attrs, name) => new RegExp(`\\s${name}="([^"]*)"`).exec(attrs)?.[1] ?? '';

/** The last two path segments, forward slashes: the file as the suite names it, independent of where the copy lives. */
export const shortFile = f => unescape(f).replace(/\\/g, '/').split('/').filter(Boolean).slice(-2).join('/');

export const caseKey = c => `${c.file}::${c.suites.join(' > ')}::${c.name}`;

export function parseJunit(xml) {
  const cases = [];
  const suites = [];
  for (const m of xml.matchAll(TOKEN)) {
    if (m[0].startsWith('<testsuite') || m[0].startsWith('</testsuite')) {
      if (m[1] === '/') suites.pop();
      else if (!m[0].endsWith('/>')) suites.push(unescape(attr(m[2], 'name')));
      continue;
    }
    const attrs = m[3];
    const selfClosed = m[4] === '/';
    const body = selfClosed ? '' : xml.slice(m.index + m[0].length).split('</testcase>')[0];
    const status = body.includes('<failure') || body.includes('<error') ? 'fail' : body.includes('<skipped') ? 'skipped' : 'pass';
    const c = { name: unescape(attr(attrs, 'name')), file: shortFile(attr(attrs, 'file')), suites: [...suites], status };
    cases.push({ ...c, key: caseKey(c) });
  }
  const count = status => cases.filter(c => c.status === status).length;
  return { tests: cases.length, passed: count('pass'), failed: count('fail'), skipped: count('skipped'), cases };
}

/** The verdict for one run: pass / fail, never inferred from an absent report. */
export function verdictOf(parsed, exitCode) {
  if (parsed.tests === 0) return 'no-report';
  return parsed.failed === 0 && exitCode === 0 ? 'pass' : 'fail';
}
