#!/usr/bin/env node
// How much of Haiku's code survives in a T arm.   node t-survival.mjs <arm>
// Per task, from the diffs run_task saved (task-<id>-haiku.diff: what Haiku added; task-<id>-review.diff: what the Sonnet
// task reviewer changed after it): Haiku's meaningful added lines (trimmed, not only braces or punctuation), and how many
// are still present — whitespace-insensitive, in any file the arm changed — in the final commit. Also the size of each
// Sonnet task review. Writes results/<arm>/t-survival.json. Hand-check a sample before reporting it (measurement.md §2).
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const arm = process.argv[2];
const E = process.env.COAI_EXP_ROOT ?? '/home/jinx/git/coai-exp';
const DIR = `${E}/arm-${arm}`;
const OUT = `${E}/results/${arm}`;
const WORK = `${E}/vllm/work/${arm}`;
const git = (...a) => { try { return execFileSync('git', ['-C', DIR, ...a], { encoding: 'utf8', maxBuffer: 1 << 28 }); } catch { return ''; } };
const meaningful = (l) => l.trim().length > 3 && !/^[{}()[\];,\s]*$/.test(l.trim());
const norm = (l) => l.replace(/\s+/g, '');
const added = (diff) => diff.split('\n').filter((l) => l.startsWith('+') && !l.startsWith('+++')).map((l) => l.slice(1)).filter(meaningful);
const changed = (diff) => diff.split('\n').filter((l) => /^[+-]/.test(l) && !/^(\+\+\+|---)/.test(l)).length;
const final = new Set(git('diff', '--name-only', 'b4e2ea96', 'HEAD').split('\n').filter(Boolean)
  .flatMap((f) => git('show', `HEAD:${f}`).split('\n').map(norm)));
const { tasks } = JSON.parse(readFileSync(`${WORK}/batches.json`, 'utf8'));
const rows = Object.keys(tasks).map((id) => {
  const h = existsSync(`${OUT}/task-${id}-haiku.diff`) ? readFileSync(`${OUT}/task-${id}-haiku.diff`, 'utf8') : '';
  const r = existsSync(`${OUT}/task-${id}-review.diff`) ? readFileSync(`${OUT}/task-${id}-review.diff`, 'utf8') : '';
  const lines = added(h);
  const kept = lines.filter((l) => final.has(norm(l))).length;
  return { id, files: tasks[id].files, haikuLines: lines.length, keptFinal: kept, reviewChangedLines: changed(r),
    droppedFinal: lines.filter((l) => !final.has(norm(l))).map((l) => l.trim()) };
});
const reviewed = rows.some((x) => existsSync(`${OUT}/task-${x.id}-review.diff`));
const n = rows.reduce((s, x) => s + x.haikuLines, 0);
const out = { arm, haikuLines: n, keptFinal: n ? rows.reduce((s, x) => s + x.keptFinal, 0) / n : null,
  reviewChangedLines: reviewed ? rows.reduce((s, x) => s + x.reviewChangedLines, 0) : null, tasks: rows };
writeFileSync(`${OUT}/t-survival.json`, JSON.stringify(out, null, 2));
console.log(`${arm}: Haiku wrote ${n} meaningful lines in ${rows.length} tasks; ${out.keptFinal == null ? 'n/a' : Math.round(out.keptFinal * 100) + ' %'} kept in the final code; ` +
  (reviewed ? `the Sonnet task reviews changed ${out.reviewChangedLines} lines (${rows.map((x) => `${x.id} ${x.reviewChangedLines}`).join(', ')})`
    : 'no task review ran (only the story review)'));
