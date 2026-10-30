// The masking rules of the blind packets: a judge classing a failure must not learn which stack or arm produced it. Each rule is named,
// and the list is printed with the packets so the judge knows what was hidden. Order matters: frames and paths go before names.
const RUNTIME = new RegExp([
  String.raw`\b(?:dotnet|\.NET|nodejs|node(?:\.js)?|NODE_[A-Z_]+|csharp|c#|npm|nuget|mono|clr|v8)\b`,
  String.raw`node:\S+`, String.raw`file:\/\/\S*`, String.raw`\bSystem(?:\.\w+)+`, String.raw`\b[\w.-]+\.dll\b`,
  String.raw`Unhandled exception\.?`, String.raw`Could not (?:find|load)\b[^\n]*`, String.raw`Aborted(?: \(core dumped\))?`, String.raw`core dumped`, String.raw`Segmentation fault`,
  String.raw`\b(?:ENOENT|EACCES|EISDIR|ENOTDIR|EEXIST|EPERM)\b`, String.raw`\b(?:syscall|errno)\b`, String.raw`\bv\d+\.\d+\.\d+\b`,
].join('|'), 'gi');
const EXCEPTIONS = /\b(?:\w+Exception|(?:Type|Reference|Syntax|Range)Error)\b/g;

export const maskRules = Object.freeze([
  { name: 'stack-frames', replacement: '<frame>', pattern: /^[ \t]*at .*$|^[ \t]*-+ End of stack trace.*$/gm },
  { name: 'paths', replacement: '<path>', pattern: /(?:[A-Za-z]:\\|\/)[\w.@+-]+(?:[\\/][\w.@+-]+)+|\b[\w-]+\.(?:cs|csproj|mjs|js|ts):(?:line )?\d+(?::\d+)?/g },
  { name: 'runtime-names', replacement: '<runtime>', pattern: RUNTIME },
  { name: 'exception-names', replacement: '<runtime>', pattern: EXCEPTIONS },
  { name: 'arm-names', replacement: '<arm>', pattern: /\b(?:process|bare)\b/gi },
]);

const MARKER = /<(?:frame|path|runtime|cell|arm)>/;
const RUNTIME_OUTPUT = '<runtime output>';
const CARET_LINE = /^[ \t]*\^+[ \t]*$/gm;

const escapeRegExp = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** Mask `text`; `literals` (the cell's own id, session id, arm and stack) are masked as `<cell>` first. Returns the masked text. */
export function maskText(text, literals = []) {
  const named = [...new Set(literals.filter(l => typeof l === 'string' && l.length >= 3))].sort((a, b) => b.length - a.length);
  const afterLiterals = named.reduce((t, l) => t.replace(new RegExp(escapeRegExp(l), 'gi'), '<cell>'), text);
  return maskRules.reduce((t, rule) => t.replace(rule.pattern, rule.replacement), afterLiterals);
}

/**
 * stderr: a plain one-line message (the SPEC'd error lines) survives. Anything longer is the runtime talking -- a trace, a source excerpt, a
 * version banner -- and is replaced whole, as is a first line that needed masking: the shape of the noise tells the stack.
 */
export function maskStderr(text, literals = []) {
  const lines = text.replace(/\n+$/, '').split('\n');
  const first = maskText(lines[0], literals);
  if (lines.length > 1 || MARKER.test(first)) return RUNTIME_OUTPUT;
  return `${first}${text.endsWith('\n') ? '\n' : ''}`;
}

/** An exit code other than 0 and 1 (a signal, a runtime's abort code) tells the stack: it is normalised. */
export const maskExit = exit => (exit === 0 || exit === 1 || exit === null || exit === undefined ? exit ?? null : '<abnormal>');

/** The captured invocations of one case, every string masked. */
export const maskCaptured = (captured, literals) => (captured ?? []).map(c => ({
  argv: (c.argv ?? []).map(a => maskText(String(a), literals)),
  exit: maskExit(c.exit),
  stdout: maskText(String(c.stdout ?? '').replace(CARET_LINE, '<frame>'), literals),
  stderr: maskStderr(String(c.stderr ?? ''), literals),
}));
