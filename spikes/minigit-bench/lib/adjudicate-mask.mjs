// The masking rules of the blind packets: a judge classing a failure must not learn which stack or arm produced it. Each rule is named,
// and the list is printed with the packets so the judge knows what was hidden. Order matters: frames and paths go before names.
const RUNTIME = /\b(?:dotnet|\.NET|nodejs|node(?:\.js)?|NODE_[A-Z_]+|csharp|c#|npm|nuget|mono|clr|v8)\b|node:internal\S*|\bSystem(?:\.\w+)+/gi;
const EXCEPTIONS = /\b(?:\w+Exception|(?:Type|Reference|Syntax|Range)Error)\b/g;

export const maskRules = Object.freeze([
  { name: 'stack-frames', replacement: '<frame>', pattern: /^[ \t]*at .*$|^[ \t]*-+ End of stack trace.*$/gm },
  { name: 'paths', replacement: '<path>', pattern: /(?:[A-Za-z]:\\|\/)[\w.@+-]+(?:[\\/][\w.@+-]+)+|\b[\w-]+\.(?:cs|csproj|mjs|js|ts):(?:line )?\d+(?::\d+)?/g },
  { name: 'runtime-names', replacement: '<runtime>', pattern: RUNTIME },
  { name: 'exception-names', replacement: '<runtime>', pattern: EXCEPTIONS },
  { name: 'arm-names', replacement: '<arm>', pattern: /\b(?:process|bare)\b/gi },
]);

const escapeRegExp = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** Mask `text`; `literals` (the cell's own id, session id, arm and stack) are masked as `<cell>` first. Returns the masked text. */
export function maskText(text, literals = []) {
  const named = [...new Set(literals.filter(l => typeof l === 'string' && l.length >= 3))].sort((a, b) => b.length - a.length);
  const afterLiterals = named.reduce((t, l) => t.replace(new RegExp(escapeRegExp(l), 'gi'), '<cell>'), text);
  return maskRules.reduce((t, rule) => t.replace(rule.pattern, rule.replacement), afterLiterals);
}

/** The captured invocations of one case, every string masked. */
export const maskCaptured = (captured, literals) => (captured ?? []).map(c => ({
  argv: (c.argv ?? []).map(a => maskText(String(a), literals)),
  exit: c.exit ?? null,
  stdout: maskText(String(c.stdout ?? ''), literals),
  stderr: maskText(String(c.stderr ?? ''), literals),
}));
