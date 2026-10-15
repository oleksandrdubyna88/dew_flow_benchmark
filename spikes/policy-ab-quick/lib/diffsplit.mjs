// Pure: split a unified git diff into per-file sections and route them.
const HEADER = /^diff --git a\/(.+?) b\/(.+)$/;

export function splitSections(diffText) {
  const sections = [];
  for (const line of diffText.split('\n')) {
    const m = HEADER.exec(line);
    if (m) sections.push({ path: m[2], lines: [line] });
    else if (sections.length > 0) sections.at(-1).lines.push(line);
  }
  return sections.map(s => ({ path: s.path, text: s.lines.join('\n') }));
}

/** Overlay paths and documentation: not part of the candidate code. */
export function isExcludedPath(p, extraPrefixes = []) {
  return /^(\.claude|todo|docs|research)\//.test(p) || /\.md$/i.test(p) || extraPrefixes.some(x => p.startsWith(`${x}/`));
}

export function routeDiff(diffText, extraPrefixes = []) {
  const sections = splitSections(diffText);
  const excludedPath = p => isExcludedPath(p, extraPrefixes);
  const join = list => list.map(s => s.text).join('\n');
  return {
    candidate: join(sections.filter(s => !excludedPath(s.path))),
    excluded: join(sections.filter(s => excludedPath(s.path))),
    candidateFiles: sections.filter(s => !excludedPath(s.path)).map(s => s.path),
    excludedFiles: sections.filter(s => excludedPath(s.path)).map(s => s.path),
  };
}
