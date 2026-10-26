#!/usr/bin/env node
// How much of the local model's fill survives in a Q arm.   node survival.mjs <arm>
// For every filled region (results/<arm>/fill-state.json holds the exact code that was spliced in), count its meaningful
// lines — trimmed, not only braces or punctuation — that still appear unchanged in the same file after the polish step and
// in the final commit. Also the size of the polish diff and of the fixes diff. Writes results/<arm>/survival.json.
// An automatic metric: hand-check a sample of regions before reporting it (measurement.md §2); the per-region lists below
// are there for that check.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const arm = process.argv[2];
const E = process.env.COAI_EXP_ROOT ?? '/home/jinx/git/coai-exp';
const DIR = `${E}/arm-${arm}`;
const OUT = `${E}/results/${arm}`;
const git = (...a) => { try { return execFileSync('git', ['-C', DIR, ...a], { encoding: 'utf8', maxBuffer: 1 << 28 }); } catch { return ''; } };
const timing = readFileSync(`${OUT}/timing.jsonl`, 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
const headAfter = (step) => timing.filter((t) => t.step === step && t.kind === 'end').pop()?.head ?? null;
const commits = { skeleton: headAfter('1-skeleton'), fillTest: headAfter('2a-fill'), fillProd: headAfter('2b-fill'),
  polish: headAfter('3-polish'), final: git('rev-parse', '--short', 'HEAD').trim() };
const meaningful = (l) => l.trim().length > 3 && !/^[{}()[\];,\s]*$/.test(l.trim());
const state = JSON.parse(readFileSync(`${OUT}/fill-state.json`, 'utf8'));

const at = (commit, file) => new Set(git('show', `${commit}:${file}`).split('\n').map((l) => l.trim()));
// The main measure (checked by hand 2026-10-26): a line counts as kept if, with all whitespace removed, it appears in ANY
// file the arm changed — the polish moved a filled helper class to another file unchanged, which the same-file measure
// called dropped. The strict measure (same file, exact trimmed line) stays as a lower bound.
const norm = (l) => l.replace(/\s+/g, '');
const anyFile = (commit) => new Set(git('diff', '--name-only', 'b4e2ea96', commit).split('\n').filter(Boolean)
  .flatMap((file) => git('show', `${commit}:${file}`).split('\n').map(norm)));
const anyPolished = commits.polish ? anyFile(commits.polish) : new Set();
const anyFinal = anyFile(commits.final);
const regions = Object.entries(state.filled).map(([id, r]) => {
  const lines = r.code.split('\n').filter(meaningful).map((l) => l.trim());
  const polished = commits.polish ? at(commits.polish, r.file) : new Set();
  const final = at(commits.final, r.file);
  const kept = (set) => lines.filter((l) => set.has(l)).length;
  const keptAny = (set) => lines.filter((l) => set.has(norm(l))).length;
  return { id, kind: r.kind, file: r.file, lines: lines.length, keptAfterPolish: keptAny(anyPolished), keptFinal: keptAny(anyFinal),
    strictAfterPolish: kept(polished), strictFinal: kept(final), droppedFinal: lines.filter((l) => !anyFinal.has(norm(l))) };
});
const sum = (rs, k) => rs.reduce((n, r) => n + r[k], 0);
const byKind = Object.fromEntries(['test', 'prod'].map((k) => {
  const rs = regions.filter((r) => r.kind === k);
  const n = sum(rs, 'lines');
  return [k, { regions: rs.length, lines: n, afterPolish: n ? sum(rs, 'keptAfterPolish') / n : null, final: n ? sum(rs, 'keptFinal') / n : null,
    strictFinal: n ? sum(rs, 'strictFinal') / n : null }];
}));
const numstat = (a, b) => (a && b ? git('diff', '--numstat', a, b) : '').split('\n').filter(Boolean)
  .reduce((n, l) => { const [x, y] = l.split('\t'); return n + (+x || 0) + (+y || 0); }, 0);
const fills = Object.fromEntries(['test', 'prod'].map((k) => [k, existsSync(`${OUT}/fill-${k}.json`) ? JSON.parse(readFileSync(`${OUT}/fill-${k}.json`, 'utf8')) : null]));
const out = {
  arm, commits, byKind,
  fill: Object.fromEntries(Object.entries(fills).map(([k, f]) => [k, f && { regions: f.regions.length,
    filled: f.regions.filter((r) => r.filled).length, rejected: f.regions.filter((r) => r.rejected).map((r) => `${r.id}: ${r.rejected}`),
    secondRound: f.regions.filter((r) => r.rounds === 2).length, compileErrors: f.build.compileErrors, tests: f.tests?.summary ?? null }])),
  diffLines: { skeleton: numstat('b4e2ea96', commits.skeleton), fill: numstat(commits.skeleton, commits.fillProd),
    polish: numstat(commits.fillProd, commits.polish), fixes: numstat(commits.polish, commits.final) },
  regions,
};
writeFileSync(`${OUT}/survival.json`, JSON.stringify(out, null, 2));
const pct = (x) => (x == null ? 'n/a' : `${Math.round(x * 100)} %`);
console.log(`${arm}: test ${byKind.test.regions} regions/${byKind.test.lines} lines → kept ${pct(byKind.test.afterPolish)} after polish, ${pct(byKind.test.final)} final; ` +
  `prod ${byKind.prod.regions}/${byKind.prod.lines} → ${pct(byKind.prod.afterPolish)}, ${pct(byKind.prod.final)}; strict final test ${pct(byKind.test.strictFinal)} prod ${pct(byKind.prod.strictFinal)}; diff lines ${JSON.stringify(out.diffLines)}`);
