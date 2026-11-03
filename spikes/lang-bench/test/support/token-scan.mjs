// M1 and S3 (fix batches 5 and 6): the repository has ONE registered token, `bench` (a row of the conventions' `## Tokens` table). A test that makes its own token (`benchsig`) makes folders, units and processes
// that neither the suite-start `sweepLeftovers` nor `sweepStale` can collect after a killed run. `foreignTokens` finds every way a source text binds a token that is not the literal `'bench'`:
//   - a `token:` value that is not the literal 'bench' / "bench" (a variable, a template and an expression included);
//   - a shorthand `{ token }` (its value is a variable);
//   - a `TOKEN`-style constant (`const TOKEN = …`, `OWNED_TOKEN = …`) other than 'bench';
//   - every value after `--owned-token`, whatever it is (the option no longer exists).
const BENCH = /^(['"`])bench\1$/;
const VALUE = String.raw`([^,}\n;]+?)\s*(?=[,}\n;]|$)`;
const FORMS = [
  [new RegExp(String.raw`\btoken\s*:\s*` + VALUE, 'g'), match => (BENCH.test(match[1].trim()) ? null : `token: ${match[1].trim()}`)],
  [/\{\s*token\s*[,}]/g, () => 'a shorthand { token }'],
  [new RegExp(String.raw`\b[A-Za-z_]*TOKEN[A-Za-z_]*\s*=\s*` + VALUE, 'g'), match => (BENCH.test(match[1].trim()) ? null : `${match[0].split('=')[0].trim()} = ${match[1].trim()}`)],
  [/--owned-token['"\s,]+([^'"\s,\]]+)/g, match => `--owned-token ${match[1]}`],
];

const withoutComments = text => text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:'"`])\/\/.*$/gm, '$1'); // a comment may say `token: …` in prose

/** The token bindings of a source text that are not the literal `'bench'`, as short descriptions, in the order of the forms (an empty list is clean). */
export const foreignTokens = source => FORMS.flatMap(([pattern, describe]) => [...withoutComments(source).matchAll(pattern)].map(describe).filter(found => found !== null));
