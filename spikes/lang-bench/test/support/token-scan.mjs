// M1 (fix batch 5): the repository has ONE registered token, `bench` (a row of the conventions' `## Tokens` table). A test that makes its own token (`benchsig`) makes folders, units and processes that neither
// the suite-start `sweepLeftovers` nor `sweepStale` can collect after a killed run. `foreignTokens` finds every `token: '<x>'` / `--owned-token` of a source text that is not `bench`.
const TOKEN_USE = /(?:token\s*:\s*|--owned-token['",\s]+)['"`]([^'"`$]+)['"`]/g;

/** The tokens other than `bench` a source text binds, in order (an empty list is clean). */
export const foreignTokens = text => [...text.matchAll(TOKEN_USE)].map(match => match[1]).filter(token => token !== 'bench');
