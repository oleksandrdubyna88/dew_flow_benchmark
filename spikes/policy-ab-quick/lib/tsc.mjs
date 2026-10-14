// Pure: tsc output -> diagnostics, and which of them belong to the hold-out file.
const LINE = /^(.+?)\((\d+),\d+\): error (TS\d+): (.*)$/;
const norm = p => p.replace(/\\/g, '/').replace(/^\.\//, '');

export function parseTscDiagnostics(output) {
  return output.split('\n').flatMap(l => {
    const m = LINE.exec(l.trim());
    return m ? [{ file: norm(m[1]), line: Number(m[2]), code: m[3], message: m[4] }] : [];
  });
}

const keyOf = d => `${d.file}|${d.code}|${d.message}`;

/** holdout = diagnostics located IN the hold-out file; otherNew = elsewhere and absent from the baseline compile. */
export function attributeDiagnostics(diags, holdoutRel, baseline) {
  const seen = new Set(baseline.map(keyOf));
  const target = norm(holdoutRel);
  return {
    holdout: diags.filter(d => d.file === target),
    otherNew: diags.filter(d => d.file !== target && !seen.has(keyOf(d))),
  };
}
