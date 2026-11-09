// The report lines of `leakcheck` over both repositories (leak-repos.mjs): the line of a scanned repository, FAILED with the places or clean with what was read, in counts and names only, never a text. A finding is named by its
// repository (the label in the brackets) and its place; a scan that could not read a file says so, and is never clean.
import { listOf } from './idlist.mjs';
import { describeFinding } from './leak-scan.mjs';

/** The text of the bench half's line, with the repository named. */
export const benchLine = line => (line.startsWith('leakcheck ') ? line.replace('leakcheck ', 'leakcheck [bench repository] ') : `[bench repository] ${line}`);

/** The groups of a repository's scan: every finding, and every name that could not be read. */
const findingsOf = groups => groups.flatMap(g => g.findings);
const unreadableOf = groups => groups.flatMap(g => g.unreadable);

/** The failure sentence of a repository: the places that hold private text, and/or the files the scan could not read. */
function failureOf(groups) {
  const found = findingsOf(groups);
  const gone = unreadableOf(groups);
  const leaks = found.length > 0 ? `held-out text found in ${found.length} place(s): ${listOf(found.map(describeFinding))}` : '';
  const gaps = gone.length > 0 ? `the scan is incomplete: ${gone.length} file(s) could not be read (${listOf(gone)}): a check that skipped files is not clean` : '';
  return [leaks, gaps].filter(Boolean).join('; ');
}

/** 5 for a finding, 4 for an incomplete scan, else 0. */
export const exitOfGroups = groups => (findingsOf(groups).length > 0 ? 5 : (unreadableOf(groups).length > 0 ? 4 : 0));

const binaryCount = groups => groups.reduce((n, g) => n + g.binary.length, 0);

/** The line of a scanned repository (`label` names the repository, or the candidate and its repository): FAILED with the places, or clean with what was read. `read` says what, in counts; `scope` how far the history went. */
export function scannedLine({ label, groups, read, scope = null }) {
  const problem = failureOf(groups);
  const reach = scope === null ? '' : `; history read: ${scope}`;
  return problem === '' ? `leakcheck [${label}] clean: ${read}; ${binaryCount(groups)} binary file(s): line tiers only` : `leakcheck [${label}] FAILED: ${problem}${reach}`;
}

/** What the task repository's scan read, in words and counts. */
export const taskRead = ({ counts, scope }) => `${counts.head === null ? 'no commit yet (an unborn branch), so no tree at HEAD' : `${counts.head} files at HEAD`}, ${counts.working} files of the working tree, the staged changes and history (${scope}) scanned`;

/** The line of a refused part: the repository's label and the reason. */
export const refusedLine = (label, reason) => `leakcheck [${label}] REFUSED: ${reason}`;
