// The tiers over texts of a repository that is not the bench's files and history: a commit's blobs, a diff, the commit messages, the working tree. One group of texts in, `{ findings, binary, unreadable }` out, every finding
// naming its repository and its place and never the text (the haystack lines a finding was seen in are dropped here). The bench's own scan (leak-scan.mjs) keeps its baseline exemption; the other repositories have none: their
// text was never in the bench's baseline commit, so every line that matches is a finding.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { workingFiles } from './leak-git-read.mjs';
import { publicFinding, scanFiles, scanText, uniqueFindings } from './leak-scan.mjs';
import { verifyCandidates } from './leak-verify.mjs';

/** A finding as it is reported: without its haystack lines, with the repository it was found in. */
const reported = (repo, rename = file => file) => f => ({ ...publicFinding(f), repo, file: rename(f.file) });

/** Texts `[{ name, raw, diff }]` through the tiers and the verify step: `{ findings, binary, unreadable }`. `unreadable` is what the caller could not read before this (names). */
export function groupOfTexts({ texts, material, repo, unreadable = [] }) {
  const done = texts.map(t => scanText({ name: t.name, raw: t.raw, diff: t.diff, material }));
  const pending = done.flatMap(d => d.pending);
  const found = uniqueFindings([...done.flatMap(d => d.findings), ...verifyCandidates(pending, material)]);
  return { findings: found.map(reported(repo)), binary: texts.filter((_, i) => done[i].binary).map(t => t.name), unreadable };
}

/** The working tree of `root` (what git would commit: tracked and untracked files that are not ignored) from the disk, as a group: `ok({ group, count })` | `fail`. A file listed and gone is `unreadable`. */
export async function groupOfWorkingTree({ root, material, repo, progress = () => {} }) {
  const files = await workingFiles(root);
  if (!files.ok) return fail(files.reason);
  progress(`${repo}: reading the working tree (${files.value.length} files)`);
  const scanned = scanFiles({ root, files: files.value, material });
  const found = uniqueFindings([...scanned.findings, ...verifyCandidates(scanned.pending, material)]);
  const rename = file => `${file} (working tree)`;
  return ok({ group: { findings: found.map(reported(repo, rename)), binary: scanned.binary, unreadable: scanned.unreadable.map(rename) }, count: files.value.length - scanned.unreadable.length - scanned.skippedDirs.length });
}
