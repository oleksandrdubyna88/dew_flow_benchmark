// Pure helpers over inputs/holdout.json. Accepts an array, or an object holding the array under entries/holdout/tests.
export function holdoutEntries(json) {
  const list = Array.isArray(json) ? json : (json?.files ?? json?.entries ?? json?.holdout ?? json?.tests ?? []);
  return list.filter(e => (e?.kind ?? 'test') === 'test' && (e.path ?? e.file));
}

export const entryPath = e => String(e.path ?? e.file).replace(/\\/g, '/');

export const fileStem = p => p.split('/').pop().replace(/\.[^.]+$/, '').replace(/[^A-Za-z0-9._-]/g, '_');

/** src_vs_code/src/test/x.test.ts -> out/test/x.test.js (relative to the extension folder). */
export function compiledTestPath(repoRelative, webDir = 'src_vs_code') {
  const rel = repoRelative.startsWith(`${webDir}/`) ? repoRelative.slice(webDir.length + 1) : repoRelative;
  return rel.replace(/^src\//, 'out/').replace(/\.ts$/, '.js');
}

/** The repo-relative path in the extension folder (S's blob lives under the folder). */
export const repoPathOf = (p, webDir = 'src_vs_code') => (p.startsWith(`${webDir}/`) ? p : `${webDir}/${p}`);
