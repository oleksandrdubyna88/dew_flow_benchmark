#!/usr/bin/env node
// Checks a T arm's task list (vllm/work/<arm>/tasks.json, written by the Opus split) and computes the batches the tasks
// run in: a batch is every task whose dependencies are all done, at most 4 at a time; since each file belongs to exactly
// one task, the tasks of a batch never share a file and can run at once in separate worktrees.
//   node tasks-plan.mjs <arm>      → writes vllm/work/<arm>/batches.json and exits 0, or prints every problem and exits 1
import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const arm = process.argv[2];
const E = process.env.COAI_EXP_ROOT ?? '/home/jinx/git/coai-exp';
const WORK = `${E}/vllm/work/${arm}`;
const PLAN = 'todo/PLAN_vibe_and_kimi_runtimes_E5.md';
const MAX_PARALLEL = 4;
const problems = [];
let doc;
try { doc = JSON.parse(readFileSync(`${WORK}/tasks.json`, 'utf8')); } catch (e) { console.log(`tasks.json is missing or not JSON: ${e.message}`); process.exit(1); }
const tasks = Array.isArray(doc?.tasks) ? doc.tasks : [];
if (tasks.length < 4 || tasks.length > 6) problems.push(`there are ${tasks.length} tasks; the brief asks for 4 to 6`);
const ids = tasks.map((t) => t.id);
for (const id of ids.filter((x, i) => ids.indexOf(x) !== i)) problems.push(`task id ${id} is used twice`);
const owner = {};
for (const t of tasks) {
  if (!t.id || !/^t\d+$/.test(t.id)) problems.push(`a task id must look like t1, t2…: ${JSON.stringify(t.id)}`);
  if (!Array.isArray(t.files) || t.files.length === 0) problems.push(`${t.id} lists no files`);
  for (const f of t.files ?? []) {
    if (owner[f] && owner[f] !== t.id) problems.push(`${f} belongs to both ${owner[f]} and ${t.id}`);
    owner[f] = t.id;
  }
  for (const d of t.depends ?? []) if (!ids.includes(d)) problems.push(`${t.id} depends on ${d}, which does not exist`);
  if (!t.brief || !existsSync(`${WORK}/${t.brief}`)) problems.push(`${t.id}'s brief ${JSON.stringify(t.brief)} does not exist in ${WORK}`);
}
const deps = Object.fromEntries(tasks.map((t) => [t.id, new Set(t.depends ?? [])]));
const closure = (id, seen = new Set()) => { for (const d of deps[id] ?? []) if (!seen.has(d)) { seen.add(d); closure(d, seen); } return seen; };
for (const t of tasks) if (closure(t.id).has(t.id)) problems.push(`${t.id} depends on itself through a cycle`);
const planOwner = owner[PLAN];
if (!planOwner) problems.push(`no task owns ${PLAN} (the last task must)`);
else { const all = closure(planOwner); const missing = ids.filter((x) => x !== planOwner && !all.has(x));
  if (missing.length) problems.push(`${planOwner} owns the plan but does not depend on ${missing.join(', ')}`); }
const covered = new Set(tasks.flatMap((t) => t.scenarios ?? []));
const all12 = ['S5.5.1', 'S5.5.2', 'S5.5.3', 'S5.5.4', 'S5.6.1', 'S5.6.2', 'S5.6.3', 'S5.6.4', 'S5.6.5', 'S5.6.6', 'S5.6.7', 'S5.6.8'];
const notCovered = all12.filter((s) => !covered.has(s));
if (notCovered.length) problems.push(`no task delivers ${notCovered.join(', ')}`);
if (problems.length) { for (const p of problems) console.log(`- ${p}`); process.exit(1); }

const done = new Set(); const batches = [];
while (done.size < tasks.length) {
  const ready = tasks.filter((t) => !done.has(t.id) && [...deps[t.id]].every((d) => done.has(d))).map((t) => t.id);
  const batch = ready.slice(0, MAX_PARALLEL);
  batches.push(batch); batch.forEach((id) => done.add(id));
}
writeFileSync(`${WORK}/batches.json`, JSON.stringify({ batches, tasks: Object.fromEntries(tasks.map((t) => [t.id, { files: t.files, brief: `${WORK}/${t.brief}`, depends: t.depends ?? [], scenarios: t.scenarios ?? [] }])) }, null, 2));
console.log(`ok: ${tasks.length} tasks in ${batches.length} batches: ${batches.map((b) => `[${b.join(' ')}]`).join(' → ')}`);
