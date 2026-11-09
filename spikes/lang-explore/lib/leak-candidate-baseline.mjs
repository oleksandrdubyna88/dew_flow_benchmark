// The baseline exemption of a BENCH candidate (build step 2b): the same rule as the full bench scan, by the same function (`applyBaseline`, leak-baseline.mjs), over the candidate's own lines. A finding in a file of the candidate's
// tree is exempt when every one of its lines also occurs, byte for byte, in the same path at the task's baseline commit and the candidate holds it no more often than the baseline did. The candidate's lines are the blob texts the
// bounded tree reader gave (`git cat-file --batch` of the candidate's commit), NEVER the working file: a working copy is not what will be published. A file whose blob could not be read has no text and so no finding to exempt
// (the caller lists it as unreadable: exit 4). Findings in the message and in the diff are never exempt, as the history's are not in the full scan. The task repository's candidates never come here.
import { applyBaseline } from './leak-baseline.mjs';
import { splitLines } from './leak-text.mjs';

/** The lines of a blob text as git prints them (the tree reader decodes a byte as a character; a line is compared as UTF-8). */
const linesOf = raw => splitLines(Buffer.from(raw, 'latin1').toString('utf8'));

/**
 * `{ kept, exempt, problems }` for the findings `found` of a candidate: `texts` are the candidate's texts (`<label>:<path>` for its tree files), `sha` the candidate commit, `repo` its repository. `kept` are the findings that stay
 * (names as they were), `exempt` the count per task, `problems` why a task's baseline gave no exemption.
 */
export async function exemptByBaseline({ repo, sha, label, texts, found, baselines }) {
  const prefix = `${label}:`;
  const lines = new Map(texts.filter(t => t.name.startsWith(prefix)).map(t => [t.name.slice(prefix.length), linesOf(t.raw)]));
  const inTree = found.filter(f => f.file.startsWith(prefix) && lines.has(f.file.slice(prefix.length)));
  const result = await applyBaseline({ repoRoot: repo, findings: inTree.map(f => ({ ...f, file: f.file.slice(prefix.length) })), baselines, head: sha, readWorking: file => lines.get(file) });
  const kept = new Set(result.kept.map(f => `${prefix}${f.file}\0${f.line}\0${f.task}\0${f.caseFile}\0${f.fromLine}`));
  const stays = f => !inTree.includes(f) || kept.has(`${f.file}\0${f.line}\0${f.task}\0${f.caseFile}\0${f.fromLine}`);
  return { kept: found.filter(stays), exempt: result.exempt, problems: result.problems };
}
