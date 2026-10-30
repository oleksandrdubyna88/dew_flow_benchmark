// Part 6 metrics: per arm and step, the cost (per model, from each process's last result — the totals are cumulative within a
// process, one process per attempt), the active time (sum of the attempts' wall_s from timing.jsonl, so limit waits between
// attempts are not counted), attempts, and the arm's span (first start to arm-done, waits included). Subagent use inside a step
// shows as a model other than the step's own. coai providers' own cost is NOT here (other vendors, not in the claude streams).
// Usage: node metrics6.mjs <map.json>  → metrics6.json and a table on stdout.
import fs from 'node:fs';

const map = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const results = file => fs.readFileSync(file, 'utf8').split('\n')
  .map(l => { try { return JSON.parse(l); } catch { return null; } })
  .filter(x => x && x.type === 'result');
const kind = step => step.replace(/^\d+-/, '').replace(/^t\d+-/, 't-');
const short = m => m.replace(/^claude-/, '').replace(/-\d.*$/, '');

const out = {};
for (const [arm, tok] of Object.entries(map)) {
  const dir = `r/${tok}/out`;
  const steps = {};
  const timing = fs.readFileSync(`${dir}/timing.jsonl`, 'utf8').trim().split('\n').map(l => JSON.parse(l));
  // 2026-10-30: a RESUMED attempt's result carries the SESSION's cumulative cost and usage (measured: CHH1 6-fix a2, one turn,
  // reports a1's tokens exactly; a14 reports a1's plus its own). So a step costs, per session, the result of its LAST attempt,
  // summed over its sessions — never the sum of every attempt (the first version of this script did that and over-counted every
  // resumed step, Part 6's included).
  const sessionOf = Object.fromEntries(timing.filter(t => t.kind === 'start').map(t => [`${t.step}.a${t.attempt}`, t.session]));
  const lastPerSession = {};
  for (const f of fs.readdirSync(dir).filter(f => /\.a\d+\.jsonl$/.test(f))) {
    const step = f.replace(/\.a\d+\.jsonl$/, ''); const attempt = +f.match(/\.a(\d+)\.jsonl$/)[1];
    const s = steps[step] ??= { kind: kind(step), cost: 0, byModel: {}, attempts: 0, sessions: 0, wall_s: 0, model: null };
    s.attempts++;
    const last = results(`${dir}/${f}`).pop();
    if (!last) continue;
    const key = `${step}|${sessionOf[`${step}.a${attempt}`] ?? f}`;
    if (!lastPerSession[key] || lastPerSession[key].attempt < attempt) lastPerSession[key] = { step, attempt, last };
  }
  for (const { step, last } of Object.values(lastPerSession)) {
    const s = steps[step]; s.sessions++;
    s.cost += last.total_cost_usd ?? 0;
    for (const [m, u] of Object.entries(last.modelUsage ?? {})) s.byModel[short(m)] = (s.byModel[short(m)] ?? 0) + (u.costUSD ?? 0);
  }
  for (const t of timing) {
    if (t.kind === 'start' && steps[t.step]) steps[t.step].model = t.model;
    if (t.kind === 'end' && steps[t.step]) steps[t.step].wall_s += t.wall_s ?? 0;
  }
  const starts = timing.filter(t => t.kind === 'start').map(t => Date.parse(t.start_utc));
  const done = timing.find(t => t.kind === 'arm-done');
  const foreign = Object.entries(steps).flatMap(([n, s]) => Object.entries(s.byModel)
    .filter(([m, c]) => m !== s.model && c > 0.05).map(([m, c]) => ({ step: n, model: m, cost: +c.toFixed(2) })));
  out[arm] = {
    steps,
    cost: +Object.values(steps).reduce((a, s) => a + s.cost, 0).toFixed(2),
    active_min: Math.round(Object.values(steps).reduce((a, s) => a + s.wall_s, 0) / 60),
    span_min: done ? Math.round((Date.parse(done.done_utc) - Math.min(...starts)) / 60000) : null,
    attempts: Object.values(steps).reduce((a, s) => a + s.attempts, 0),
    foreign,
  };
}
fs.writeFileSync('metrics6.json', JSON.stringify(out, null, 2));

const kinds = ['split', 'code', 't-code', 'hreview', 'hA', 'hB', 'hfix', 'review', 'fix', 'gate'];
console.log(['arm', 'cost$', 'active m', 'span m', 'att', ...kinds.map(k => k + ' $/m')].join('\t'));
for (const [arm, a] of Object.entries(out)) {
  const cell = k => {
    const ss = Object.values(a.steps).filter(s => s.kind === k);
    return ss.length ? `${ss.reduce((x, s) => x + s.cost, 0).toFixed(2)}/${Math.round(ss.reduce((x, s) => x + s.wall_s, 0) / 60)}` : '-';
  };
  console.log([arm, a.cost, a.active_min, a.span_min, a.attempts, ...kinds.map(cell)].join('\t'));
}
console.log('\nsubagent models inside a step (> $0.05, model other than the step\'s own):');
for (const [arm, a] of Object.entries(out)) for (const f of a.foreign) console.log(`  ${arm} ${f.step}: ${f.model} $${f.cost}`);
