// Sanitising a capture so it can be public, and the fail-closed scan that proves it. Pure except `localContext` (reads the machine).
// A secret is replaced in EVERY spelling it can take in a file: plain, JSON-escaped, slash-escaped, URL-escaped (either case) and the
// CLI's flattened project-directory form (every non-alphanumeric character becomes '-'). `leakScan` then re-reads the output for the
// same things plus credential shapes; a finding names the file and the pattern and never repeats the matched text.
import { execFileSync } from 'node:child_process';
import os from 'node:os';

const FLATTEN = /[^A-Za-z0-9]/g;
const ID_ATTRS = Object.freeze(['user.id', 'user.account_uuid', 'organization.id']);
const EMAIL_ATTR = 'user.email';
const MIN_ID_LENGTH = 4;
export const PLACEHOLDER = Object.freeze({ out: '/cap', home: '/home/user', user: 'user', host: 'host', email: 'user@example.invalid', id: 'redacted' });

const urlLower = s => encodeURIComponent(s).replace(/%[0-9A-F]{2}/g, m => m.toLowerCase());
const SPELLINGS = Object.freeze([
  s => s,
  s => s.replaceAll('/', '\\/'),
  s => JSON.stringify(s).slice(1, -1),
  s => encodeURIComponent(s),
  urlLower,
]);

const escapeRe = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** One entry -> every `[from, to]` spelling pair, longest `from` first matters to the caller. */
function variants(entry) {
  const forms = [...SPELLINGS, ...(entry.path ? [s => s.replace(FLATTEN, '-')] : [])];
  const pairs = forms.map(f => [f(entry.from), f(entry.to)]);
  return [...new Map(pairs.map(p => [p[0], p])).values()].map(([from, to]) => ({ from, to, bounded: entry.bounded }));
}

/** The ordered replacement map. A field that is missing, or already equal to its placeholder, adds nothing. */
export function buildMap({ out = null, home = null, user = null, host = null, email = null, idValues = [], extraEmails = [] }) {
  const trim = p => (p && p.length > 1 ? p.replace(/[/\\]+$/, '') : p);
  const entries = [
    { from: trim(out), to: PLACEHOLDER.out, path: true },
    { from: trim(home), to: PLACEHOLDER.home, path: true },
    { from: user, to: PLACEHOLDER.user, bounded: true },
    { from: host, to: PLACEHOLDER.host, bounded: true },
    ...[email, ...extraEmails].map(e => ({ from: e, to: PLACEHOLDER.email })),
    ...idValues.filter(v => typeof v === 'string' && v.length >= MIN_ID_LENGTH).map(v => ({ from: v, to: PLACEHOLDER.id })),
  ].filter(e => typeof e.from === 'string' && e.from !== '' && e.from !== e.to);
  return Object.freeze(entries.flatMap(variants).sort((a, b) => b.from.length - a.from.length));
}

function replaceOne(text, { from, to, bounded }) {
  if (!bounded) return text.split(from).join(to);
  return text.replace(new RegExp(`(?<![A-Za-z0-9])${escapeRe(from)}(?![A-Za-z0-9])`, 'g'), to);
}

/** `text` with every mapped secret replaced in every spelling. */
export const sanitiseText = (text, map) => map.reduce(replaceOne, text);

/** A relative path through the same map (directory names carry the flattened cwd). */
export const sanitisePath = (relPath, map) => sanitiseText(relPath, map);

/** The account ids and e-mails an OTLP sink file (`otlp-raw.ndjson` text) carries as attributes. `session.id` is deliberately not one. */
export function collectAttrSecrets(sinkText) {
  const ids = new Set();
  const emails = new Set();
  for (const line of sinkText.split('\n').filter(l => l.trim() !== '')) {
    for (const attr of attributesOf(line)) {
      const v = attr?.value?.stringValue;
      if (typeof v !== 'string' || v === '') continue;
      if (ID_ATTRS.includes(attr.key)) ids.add(v);
      if (attr.key === EMAIL_ATTR) emails.add(v);
    }
  }
  return { ids: [...ids], emails: [...emails] };
}

function attributesOf(line) {
  try {
    const doc = JSON.parse(JSON.parse(line).body);
    return (doc.resourceLogs ?? []).flatMap(rl => [
      ...(rl.resource?.attributes ?? []),
      ...(rl.scopeLogs ?? []).flatMap(sl => (sl.logRecords ?? []).flatMap(r => r.attributes ?? [])),
    ]);
  } catch {
    return [];
  }
}

// ---- the scan ---------------------------------------------------------------------------------------------------------------

const CREDENTIALS = Object.freeze([
  ['credential shape sk-ant-', /sk-ant-[A-Za-z0-9_-]{20,}/],
  ['credential shape jwt', /eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{10,}/],
  ['foreign home directory', /(?:\/home|\/Users)\/(?!user(?:[/\\"'\s]|$))[A-Za-z0-9._-]+/],
]);

function literalPatterns({ out, home, user, host, email, token }) {
  const lit = (name, value, { path = false, bounded = false } = {}) => (typeof value === 'string' && value.length >= 3
    ? [{ name, variants: variants({ from: value, to: value, path, bounded }).map(v => ({ text: v.from, bounded })) }] : []);
  return [
    ...lit('home directory', home, { path: true }), ...lit('raw out path', out, { path: true }), ...lit('user name', user, { bounded: true }),
    ...lit('host name', host, { bounded: true }), ...lit('e-mail address', email), ...lit('the token value', token),
  ];
}

const present = (haystack, { text, bounded }) => (bounded
  ? new RegExp(`(?<![A-Za-z0-9])${escapeRe(text)}(?![A-Za-z0-9])`).test(haystack) : haystack.includes(text));

/** `[{ file, pattern }]`: every file (path or content) that holds this machine's identity, the out path, the token, or a credential shape. */
export function leakScan(files, ctx) {
  const lits = literalPatterns(ctx);
  return files.flatMap(f => {
    const haystacks = [f.relPath, f.text];
    const hits = lits.filter(p => p.variants.some(v => haystacks.some(h => present(h, v)))).map(p => p.name);
    const shapes = CREDENTIALS.filter(([, re]) => haystacks.some(h => re.test(h))).map(([name]) => name);
    return [...new Set([...hits, ...shapes])].map(pattern => ({ file: f.relPath, pattern }));
  });
}

/** This machine's identity as the scan sees it. `gitEmail` is injectable; a missing git config is `null`, not an error. */
export function localContext({ gitEmail = readGitEmail } = {}) {
  return { out: null, home: os.homedir(), user: os.userInfo().username, host: os.hostname(), email: gitEmail(), token: null };
}

function readGitEmail() {
  try {
    return execFileSync('git', ['config', 'user.email'], { encoding: 'utf8', timeout: 5000, stdio: ['ignore', 'pipe', 'ignore'] }).trim() || null;
  } catch {
    return null; // git absent or no user.email: there is no e-mail to scan for
  }
}
