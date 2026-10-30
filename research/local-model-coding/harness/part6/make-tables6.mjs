#!/usr/bin/env node
// Part 6 tables, from ../../data/part6 only (no access to the machine the experiment ran on is needed).
//   node research/local-model-coding/harness/part6/make-tables6.mjs     → prints the Part 6 section on stdout
// ../make-tables.mjs imports part6Tables() and appends the section to ../../tables.md.
// Inputs: metrics6.json, judge-abs/{scores,key,handcheck}.json, coai-findings.json, coai-cost.json, judge-coai/{key.json,F-*/verdict.json},
// arms/<ARM>/{objective,review,gate,mutation}.json.
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const D = join(ROOT, 'data', 'part6');
const read = (f) => JSON.parse(readFileSync(join(D, f), 'utf8'));
export const ARMS = ['C1', 'C2', 'C3', 'CR1', 'CR2', 'CR3', 'SRS1', 'SRS2', 'SRS3'];
export const SCHEMES = [['C', ['C1', 'C2', 'C3']], ['CR', ['CR1', 'CR2', 'CR3']], ['SRS', ['SRS1', 'SRS2', 'SRS3']]];
const SCHEME_NAME = { C: 'Sonnet codes → Opus reviews → Sonnet fixes → coai gate', CR: 'C + a fresh Haiku review-and-fix before Opus', SRS: 'Sonnet splits → Haiku codes each task → fresh Haiku review-and-fix → Opus → Sonnet fixes → coai gate' };
export const welch = (xs, ys) => {
  const m = (a) => a.reduce((n, x) => n + x, 0) / a.length;
  const v = (a) => a.reduce((n, x) => n + (x - m(a)) ** 2, 0) / (a.length - 1);
  const se2 = v(xs) / xs.length + v(ys) / ys.length;
  return { diff: m(xs) - m(ys), t: (m(xs) - m(ys)) / Math.sqrt(se2),
    df: se2 ** 2 / ((v(xs) / xs.length) ** 2 / (xs.length - 1) + (v(ys) / ys.length) ** 2 / (ys.length - 1)) };
};
// two-sided 5 % limit of Student's t, interpolated between the usual table values (df 1 to 6)
export const tcrit = (df) => { const T = [[1, 12.706], [2, 4.303], [3, 3.182], [4, 2.776], [5, 2.571], [6, 2.447]]; const d = Math.min(Math.max(df, 1), 6); const i = Math.min(Math.floor(d), 5); return T[i - 1][1] + (T[i][1] - T[i - 1][1]) * (d - i); };
const mean = (xs) => (xs.length ? xs.reduce((s, x) => s + x, 0) / xs.length : null);
const f = (v, d = 1) => (v == null || Number.isNaN(v) ? 'n/a' : Number(v).toFixed(d));
const usd = (v) => (v == null ? 'n/a' : `$${v.toFixed(2)}`);
const mm = (xs, d = 2) => `${f(Math.min(...xs), d)}–${f(Math.max(...xs), d)}`;
const stepOrder = (n) => Number(n.match(/^(\d+)/)[1]) * 100 + (Number((n.match(/-t(\d+)-/) ?? [])[1]) || 0);

export function part6Tables() {
  const out = [];
  const p = (s = '') => out.push(s);
  const M = read('metrics6.json');
  const SC = read('judge-abs/scores.json');
  const KEY = read('judge-abs/key.json');
  const HC = read('judge-abs/handcheck.json');
  const CF = read('coai-findings.json');
  const JK = read('judge-coai/key.json');
  const CC = read('coai-cost.json');
  const M1 = read('metrics6.v1-summed-attempts.json');
  const inv = Object.fromEntries(Object.entries(JK).map(([l, a]) => [a, l]));
  const arm = (a, f2) => read(`arms/${a}/${f2}.json`);

  // ---- derived per arm ----
  const rev = Object.fromEntries(ARMS.map((a) => {
    const r = arm(a, 'review'); const c = (s) => r.findings.filter((x) => x.severity === s).length;
    return [a, { verdict: r.verdict, blocking: c('blocking'), major: c('major'), minor: c('minor'), total: r.findings.length }];
  }));
  const kindCost = (a, kinds) => Object.values(M[a].steps).filter((s) => kinds.includes(s.kind)).reduce((x, s) => x + s.cost, 0);
  const kindMin = (a, kinds) => Math.round(Object.values(M[a].steps).filter((s) => kinds.includes(s.kind)).reduce((x, s) => x + s.wall_s, 0) / 60);
  const hasKind = (a, k) => Object.values(M[a].steps).some((s) => s.kind === k);
  const sevCount = (r, s) => r.defects.filter((d) => d.severity === s).length;
  const judge = Object.fromEntries(ARMS.map((a) => {
    const rs = SC[a]; const crit = (k) => mean(rs.map((r) => r[k]));
    return [a, { overall: mean(rs.map((r) => r.overall)), correctness: crit('correctness'), tests: crit('tests'), conventions: crit('conventions'), simplicity: crit('simplicity'),
      majors: rs.reduce((n, r) => n + sevCount(r, 'major') + sevCount(r, 'blocking'), 0) }];
  }));
  // hand-checked distinct majors: a finding that the hand check confirmed as major (the verdict text starts "major" or is "real" with severity "major")
  const realMajors = Object.fromEntries(ARMS.map((a) => [a, HC.majors.filter((m) => m.arm === a && m.verdict === 'real' && /^major/.test(m.severity)).length]));
  const judged = Object.fromEntries(ARMS.map((a) => {
    const v = read(`judge-coai/${inv[a]}/verdict.json`);
    const code = CF[a].filter((r) => r.stage === 'code');
    const rows = code.flatMap((r) => r.findings.map((x) => ({ round: r.number, ...x, j: v.findings.find((y) => y.round === r.number && y.index === x.index) })));
    const cnt = (k) => rows.filter((x) => x.j.verdict === k).length;
    const acc = rows.filter((x) => x.decision === 'accept');
    return [a, { v, code, rows, n: rows.length, real: cnt('real'), partly: cnt('partly'), not: cnt('not_real'), score: v.score,
      u2: rows.filter((x) => x.j.useful === 2).length, u1: rows.filter((x) => x.j.useful === 1).length, u0: rows.filter((x) => x.j.useful === 0).length,
      acc: acc.length, accReal: acc.filter((x) => x.j.verdict === 'real').length, accPartly: acc.filter((x) => x.j.verdict === 'partly').length }];
  }));

  p('## 10. Part 6 — three schemes on a second task, each ending in the coai code gate (C, CR, SRS × 3)');
  p();
  p('> Generated by `harness/part6/make-tables6.mjs` from `data/part6/` only. A different task from parts 1–5 (coai issues #878 and the quota part of');
  p('> #765, plan `todo/PLAN_a_spent_quota_cools_the_model_down.md` at `6786582e`, 43 scenarios), so nothing here is mixed into §1–§9. The narrative is');
  p('> [results.md](results.md) §7d. **Cost** here is Claude-only cost: Claude Code\'s own `total_cost_usd`, for the Claude models only. A step costs, per session, the result of its LAST attempt, summed over its sessions (a resumed attempt reports the session\'s running total; corrected 2026-10-30, [incidents.md](incidents.md) §2h):');
  p('> the cost of the coai providers (other vendors) was **not measured** and is not in any figure, so the total cost of a scheme that ends in the gate cannot be compared from these tables (§10.6 gives the tokens the coai server reported). **Active min** is the sum of the attempts\' own run times, so limit waits are not counted; in SRS the');
  p('> Haiku task sessions of one batch ran at the same time, so SRS active minutes are a sum of work, not elapsed time. **Elapsed min** is the first step\'s start to the');
  p('> arm\'s end, waits included (†: the arm of round 1 waited out the overnight spend limit, [incidents.md](incidents.md) §2g).');
  p();

  // ---- 10.1 ----
  p('### 10.1 Cost, time and attempts per arm (Claude only)');
  p();
  p('| Arm | Scheme | Claude-only $ | Active min | Elapsed min | Attempts |');
  p('|---|---|---|---|---|---|');
  for (const a of ARMS) {
    const m = M[a]; const sch = a.replace(/\d+$/, '');
    p(`| ${a} | ${SCHEME_NAME[sch]} | ${usd(m.cost)}${a === 'SRS3' ? ' ‡' : ''} | ${m.active_min} | ${m.span_min}${/1$/.test(a) ? ' †' : ''} | ${m.attempts} |`);
  }
  p();
  p(`‡ SRS3\'s Haiku review-and-fix step started two subagents; ${usd(M.SRS3.steps['3-hreview'].byModel.opus + M.SRS3.steps['3-hreview'].byModel.sonnet)} of the arm\'s cost is Opus and Sonnet, not Haiku (§10.2; [incidents.md](incidents.md) §2g). The scheme means below keep that cost, and give the mean without it beside it.`);
  p();
  p('| Scheme | Arms | Claude-only $ mean (min–max) | Active min mean (min–max) | Elapsed min, round 2 and 3 arms only (min–max) |');
  p('|---|---|---|---|---|');
  for (const [s, as] of SCHEMES) {
    const c = as.map((a) => M[a].cost); const t = as.map((a) => M[a].active_min); const e = as.filter((a) => !/1$/.test(a)).map((a) => M[a].span_min);
    const sub = s === 'SRS' ? ` — ${usd(mean([M.SRS1.cost, M.SRS2.cost, M.SRS3.cost - (M.SRS3.steps['3-hreview'].byModel.opus + M.SRS3.steps['3-hreview'].byModel.sonnet)]))} if SRS3\'s Opus and Sonnet subagents are left out` : '';
    p(`| ${s} | ${as.join(', ')} | ${usd(mean(c))} (${usd(Math.min(...c))}–${usd(Math.max(...c))})${sub} | ${f(mean(t), 0)} (${Math.min(...t)}–${Math.max(...t)}) | ${Math.min(...e)}–${Math.max(...e)} |`);
  }
  p();

  // ---- 10.2 ----
  const subs = M.SRS3.steps['3-hreview'].byModel.opus + M.SRS3.steps['3-hreview'].byModel.sonnet;
  const srsAdj = mean([M.SRS1.cost, M.SRS2.cost, M.SRS3.cost - subs]);
  p('How the cost was counted, and what the first count got wrong. The first version of `metrics6.mjs` summed the result of every attempt. A RESUMED attempt reports the session\'s running total (tokens and dollars of the earlier attempts too), so every resumed step was counted twice in part. The corrected script takes, per session, the last attempt\'s result. Only the three arms that were resumed change (`data/part6/metrics6.v1-summed-attempts.json` keeps the first count):');
  p();
  p('| Arm | First count (sum of attempts) | Corrected | Difference | Step that changed |');
  p('|---|---|---|---|---|');
  for (const a of ARMS) {
    const d = M1[a].cost - M[a].cost; if (Math.abs(d) < 0.005) continue;
    const st = Object.keys(M[a].steps).filter((n) => Math.abs(M1[a].steps[n].cost - M[a].steps[n].cost) > 0.005).map((n) => `${n}: ${usd(M1[a].steps[n].cost)} → ${usd(M[a].steps[n].cost)}`).join('; ');
    p(`| ${a} | ${usd(M1[a].cost)} | ${usd(M[a].cost)} | −${usd(d)} | ${st} |`);
  }
  p();
  const SC3 = Object.fromEntries(SCHEMES.map(([s2, as]) => [s2, as.map((a) => M[a].cost)]));
  const srsAdjArr = [M.SRS1.cost, M.SRS2.cost, M.SRS3.cost - subs];
  p('| Scheme | First-count mean | Corrected mean (primary) | Range (corrected) |');
  p('|---|---|---|---|');
  for (const [s2, as] of SCHEMES) p(`| ${s2} | ${usd(mean(as.map((a) => M1[a].cost)))} | **${usd(mean(SC3[s2]))}**${s2 === 'SRS' ? ` (${usd(srsAdj)} if SRS3\'s Opus and Sonnet subagents are left out)` : ''} | ${usd(Math.min(...SC3[s2]))}–${usd(Math.max(...SC3[s2]))} |`);
  p();
  const w = (x, y) => welch(x, y);
  const wr = (nm, x, y) => { const r = w(x, y); return `| ${nm} | ${r.diff >= 0 ? '+' : '−'}${usd(Math.abs(r.diff))} | ${f(r.t, 2)} | ${f(r.df, 1)} | ${f(tcrit(r.df), 1)} |`; };
  p('Is any difference in the means measured? Welch\'s t test on the three arms of each scheme (a |t| below the 5 % limit is a difference that the spread of the runs can make alone):');
  p();
  p('| Pair (first minus second) | Difference of means | t | df | 5 % limit of \\|t\\| |');
  p('|---|---|---|---|---|');
  p(wr('CR − C', SC3.CR, SC3.C)); p(wr('SRS − C', SC3.SRS, SC3.C)); p(wr('SRS − CR', SC3.SRS, SC3.CR));
  p(wr('SRS (without SRS3\'s subagents) − C', srsAdjArr, SC3.C));
  p();
  p('### 10.2 Per step: model, cost, active minutes, attempts (from `data/part6/metrics6.json`)');
  p();
  p('An attempt that ended on the overnight spend limit is counted as an attempt; its cost is what the host reported for it (0 for most; a part of a step for C1\'s Opus review, CR1\'s Haiku review and SRS1\'s task t4).');
  p();
  p('| Arm | Step | Model | Cost (Claude only) | Active min | Attempts |');
  p('|---|---|---|---|---|---|');
  for (const a of ARMS) {
    for (const [name, s] of Object.entries(M[a].steps).sort((x, y) => stepOrder(x[0]) - stepOrder(y[0]))) {
      p(`| ${a} | ${name} | ${s.model} | ${usd(s.cost)} | ${Math.round(s.wall_s / 60)} | ${s.attempts} |`);
    }
  }
  p();
  p('By role (Claude-only cost; active min in brackets):');
  p();
  p('| Arm | Sonnet code or split | Haiku task coding | Haiku review-and-fix | Opus review | Sonnet fix | Sonnet coai gate | Total (Claude only) |');
  p('|---|---|---|---|---|---|---|---|');
  const cell = (a, ks) => (ks.some((k) => hasKind(a, k)) ? `${usd(kindCost(a, ks))} (${kindMin(a, ks)})` : '—');
  for (const a of ARMS) p(`| ${a} | ${cell(a, ['code', 'split'])} | ${cell(a, ['t-code'])} | ${cell(a, ['hreview'])}${a === 'SRS3' ? ' ‡' : ''} | ${cell(a, ['review'])} | ${cell(a, ['fix'])} | ${cell(a, ['gate'])} | ${usd(M[a].cost)} |`);
  p();
  p('‡ contaminated: the step is not a Haiku review (see below).');
  p();
  p();
  p('Mean by role per scheme (Claude only; the cost of the step kinds above):');
  p();
  p('| Scheme | Sonnet code or split | Haiku task coding | Haiku review-and-fix | Opus review | Sonnet fix | Sonnet coai gate | Total |');
  p('|---|---|---|---|---|---|---|---|');
  const rm = (as, ks) => (as.some((a) => ks.some((k) => hasKind(a, k))) ? usd(mean(as.map((a) => kindCost(a, ks)))) : '—');
  for (const [s2, as] of SCHEMES) p(`| ${s2} | ${rm(as, ['code', 'split'])} | ${rm(as, ['t-code'])} | ${rm(as, ['hreview'])}${s2 === 'SRS' ? ` (${usd(mean(as.map((a) => kindCost(a, ['hreview']) - (a === 'SRS3' ? subs : 0))))} without the subagents)` : ''} | ${rm(as, ['review'])} | ${rm(as, ['fix'])} | ${rm(as, ['gate'])} | ${usd(mean(as.map((a) => M[a].cost)))} |`);
  p();
  p('Subagents: a session that starts a subagent shows the subagent\'s model in `byModel`. One step in all nine arms did, SRS3\'s Haiku review-and-fix (`3-hreview`):');
  p();
  const s3 = M.SRS3.steps['3-hreview'].byModel;
  p('| Step | Haiku | Sonnet | Opus | Step total |');
  p('|---|---|---|---|---|');
  p(`| SRS3 3-hreview | ${usd(s3.haiku)} | ${usd(s3.sonnet)} | ${usd(s3.opus)} | ${usd(M.SRS3.steps['3-hreview'].cost)} |`);
  p();

  // ---- 10.3 ----
  p('### 10.3 Objective gate (on each arm\'s final commit)');
  p();
  p('Build and format (on the changed files) and both suites. "Present" = the scenario\'s test name occurs in the test sources; "passing" = a test of that name (as a whole word) passed in the report.');
  p('The 43 scenarios are in `harness/part6/scenarios.json`.');
  p();
  p('| Arm | Build | Format | MCP suite (total / passed / skipped / new failures) | TS suite (total / new failures) | Scenarios (of 43) present / passing | Commits | Diff | Changed lines: tests / src / docs |');
  p('|---|---|---|---|---|---|---|---|---|');
  for (const a of ARMS) {
    const o = arm(a, 'objective');
    p(`| ${a} | ${o.build ? 'ok' : 'FAIL'} | ${o.format ? 'ok' : 'FAIL'} | ${o.mcp.total} / ${o.mcp.passed} / ${o.mcp.skipped} / ${o.mcp.failedNew.length} | ${o.ts.total} / ${o.ts.failedNew.length} | ${o.scenariosPresentCount} / ${o.scenariosPassingCount} | ${o.commits} | ${o.diff.shortstat} | ${o.diff.changedLines.tests} / ${o.diff.changedLines.src} / ${o.diff.changedLines.docs} |`);
  }
  p();
  const c1 = arm('C1', 'objective');
  const notPassing = Object.entries(c1.scenarios).filter(([, v]) => v.passing !== 'passed');
  p(`Scenarios not reported as passing: ${notPassing.length ? notPassing.map(([id, v]) => `C1 ${id} (\`${v.name}\`: "${v.passing}")`).join('; ') : 'none'}. ` +
    'C1 names that test `AnUnknownModel_HasNoGroup_AndEveryKnownOneHasItsKey` (a theory with 8 cases, all passed in its report); the pass check looks for the exact name as a whole word, and a longer name does not match it. A naming artefact, not a failure; see [results.md](results.md) §7d.');
  p();

  // ---- 10.4 ----
  p('### 10.4 The Opus review (before the Sonnet fixes), from `notes/review.json`');
  p();
  p('| Arm | Findings | Blocking / major / minor | Verdict |');
  p('|---|---|---|---|');
  for (const a of ARMS) p(`| ${a} | ${rev[a].total} | ${rev[a].blocking} / ${rev[a].major} / ${rev[a].minor} | ${rev[a].verdict} |`);
  p();
  p('| Scheme | Blocking + major per arm | Mean | All findings per arm | Mean |');
  p('|---|---|---|---|---|');
  for (const [s, as] of SCHEMES) p(`| ${s} | ${as.map((a) => rev[a].blocking + rev[a].major).join(' / ')} | ${f(mean(as.map((a) => rev[a].blocking + rev[a].major)), 2)} | ${as.map((a) => rev[a].total).join(' / ')} | ${f(mean(as.map((a) => rev[a].total)), 1)} |`);
  p();

  // ---- 10.5 ----
  p('### 10.5 Quality — the absolute blind judge (one Opus session per diff, anchored rubric, 2 runs)');
  p();
  p('Defects per run (B = blocking, M = major, m = minor), as the judge named them. The words that name a scheme or a model were redacted from each diff first (count in the last column).');
  p();
  p('| Arm | Overall (runs) | Correctness | Tests | Conventions | Simplicity | Defects per run | Redacted words |');
  p('|---|---|---|---|---|---|---|---|');
  const dstr = (r) => r.defects.map((d) => ({ blocking: 'B', major: 'M', minor: 'm' }[d.severity])).sort().join('') || '—';
  for (const a of ARMS) {
    const rs = SC[a]; const pair = (k) => rs.map((r) => r[k]).join(' / ');
    p(`| ${a} | **${f(judge[a].overall)}** (${pair('overall')}) | ${pair('correctness')} | ${pair('tests')} | ${pair('conventions')} | ${pair('simplicity')} | ${rs.map(dstr).join(' / ')} | ${KEY.redactions[a]} |`);
  }
  p();
  p('| Scheme | Overall mean | Correctness | Tests | Conventions | Simplicity | Majors named by the judge, per arm (both runs added) | Distinct majors left after the hand check, per arm |');
  p('|---|---|---|---|---|---|---|---|');
  for (const [s, as] of SCHEMES) {
    const g = (k) => f(mean(as.map((a) => judge[a][k])), 2);
    p(`| ${s} | **${g('overall')}** | ${g('correctness')} | ${g('tests')} | ${g('conventions')} | ${g('simplicity')} | ${as.map((a) => judge[a].majors).join(' / ')} (${as.reduce((n, a) => n + judge[a].majors, 0)}) | ${as.map((a) => realMajors[a]).join(' / ')} (${as.reduce((n, a) => n + realMajors[a], 0)}) |`);
  }
  p();
  p('The hand check (`data/part6/judge-abs/handcheck.json`, 2026-10-29, against the arm code) opened four major findings:');
  p();
  p('| Arm | Runs | Finding | Verdict | Severity after the check | Evidence |');
  p('|---|---|---|---|---|---|');
  for (const m of HC.majors) p(`| ${m.arm} | ${m.runs} | ${m.finding} | ${m.verdict} | ${m.severity} | ${m.evidence} |`);
  p();

  // ---- 10.6 ----
  p('### 10.6 The coai code gate — what ran, what it found, and what the gate agent did');
  p();
  p('Two providers answered every round (antigravity, codex); a code round asks 8 reviewers. The plan round only unlocks the code gate: all 49 plan findings were rejected as "frozen"');
  p('(`coai-findings.json`, stage `plan`). The table counts the CODE findings only.');
  p();
  p('| Arm | Code rounds (verdicts) | Findings per round | Severity as coai gave it (B / M / m) | Accepted by the gate agent | Rejected | Fix commit for an accepted finding |');
  p('|---|---|---|---|---|---|---|');
  for (const a of ARMS) {
    const j = judged[a]; const g = arm(a, 'gate');
    const sev = (s) => j.rows.filter((x) => x.severity === s).length;
    const fixes = [...new Set(g.rounds.filter((r) => r.stage === 'code').flatMap((r) => r.findings.filter((x) => x.decision === 'accept').map((x) => x.fixedIn)).filter(Boolean))];
    p(`| ${a} | ${j.code.length} (${j.code.map((r) => r.verdict).join(', ')}) | ${j.code.map((r) => r.findings.length).join(' + ')} | ${sev('Blocking')} / ${sev('Major')} / ${sev('Minor')} | ${j.acc} | ${j.n - j.acc} | ${fixes.length ? fixes.join(', ') : '—'} |`);
  }
  p();
  p('The blind judge of those findings (one Opus session per arm; each finding settled by opening the code at the commit its round reviewed; the gate agent\'s decisions and the providers were not shown):');
  p();
  p('| Arm | Findings | Real | Partly | Not real | Judge score (1–10) | Useful 2 / 1 / 0 | Accepted by the gate agent: real / partly / not real |');
  p('|---|---|---|---|---|---|---|---|');
  for (const a of ARMS) {
    const j = judged[a];
    p(`| ${a} | ${j.n} | ${j.real} | ${j.partly} | ${j.not} | ${j.score} | ${j.u2} / ${j.u1} / ${j.u0} | ${j.acc}: ${j.accReal} / ${j.accPartly} / ${j.acc - j.accReal - j.accPartly} |`);
  }
  const tot = (k) => ARMS.reduce((n, a) => n + judged[a][k], 0);
  p(`| **All nine** | **${tot('n')}** | **${tot('real')}** | **${tot('partly')}** | **${tot('not')}** | mean ${f(mean(ARMS.map((a) => judged[a].score)), 2)} | ${tot('u2')} / ${tot('u1')} / ${tot('u0')} | ${tot('acc')}: ${tot('accReal')} / ${tot('accPartly')} / ${tot('acc') - tot('accReal') - tot('accPartly')} |`);
  p();
  p('| Scheme | Findings per arm | Real + partly per arm | Judge score per arm | Mean score | Accepted (real) per arm |');
  p('|---|---|---|---|---|---|');
  for (const [s, as] of SCHEMES) p(`| ${s} | ${as.map((a) => judged[a].n).join(' / ')} | ${as.map((a) => judged[a].real + judged[a].partly).join(' / ')} | ${as.map((a) => judged[a].score).join(' / ')} | ${f(mean(as.map((a) => judged[a].score)), 2)} | ${as.map((a) => `${judged[a].acc} (${judged[a].accReal})`).join(' / ')} |`);
  p();
  p('Tokens that the coai server reported for the providers\' reviews (`cost.tokensIn` and `cost.tokensOut` in each `review_plan` and `review_code` reply, from the gate stream; `data/part6/coai-cost.json`). They are TOKENS, not dollars, and the reply gives one pair for the whole round, not one per provider, so there is no split by provider. The price per token of the providers is not known here.');
  p();
  p('| Arm | Plan round in / out | Code round 1 in / out | Code round 2 in / out | Total in | Total out |');
  p('|---|---|---|---|---|---|');
  const tk = (n) => n.toLocaleString('en-US');
  const sumC = { in: 0, out: 0 };
  for (const a of ARMS) {
    const rs = CC[a]; const pick = (st, no) => rs.find((r) => r.stage === st && r.number === no);
    const pr = (r) => (r ? `${tk(r.tokensIn)} / ${tk(r.tokensOut)}` : '—');
    const ti = rs.reduce((n, r) => n + r.tokensIn, 0); const to = rs.reduce((n, r) => n + r.tokensOut, 0); sumC.in += ti; sumC.out += to;
    p(`| ${a} | ${pr(pick('plan', 1))} | ${pr(pick('code', 1))} | ${pr(pick('code', 2))} | ${tk(ti)} | ${tk(to)} |`);
  }
  p(`| **All nine** | | | | **${tk(sumC.in)}** | **${tk(sumC.out)}** |`);
  p();
  p('The ten findings the judge called **real**, with the judge\'s severity and whether the Opus review named the same point (checked by hand against each arm\'s `review.json`):');
  p();
  p('| Arm | Round / index | Title (as coai gave it) | coai severity | Judge: severity right | Gate agent | Named by the Opus review |');
  p('|---|---|---|---|---|---|---|');
  const named = { 'C3 1/2': 'only the cancellation case (R4); this finding is the wider one, left by its fix', 'SRS2 1/12': 'no: the fix of R4 (the lock wait) introduced the Task.Run' };
  for (const a of ARMS) for (const x of judged[a].rows.filter((y) => y.j.verdict === 'real')) {
    p(`| ${a} | ${x.round} / ${x.index} | ${x.title} | ${x.severity} | ${x.j.severity_right} | ${x.decision} | ${named[`${a} ${x.round}/${x.index}`] ?? 'no'} |`);
  }
  p();

  // ---- 10.7 ----
  p('### 10.7 Behaviour — the frozen mutation checks (data/part6/arms/<arm>/mutation.json, harness/part6/mutate6.mjs)');
  p();
  p('Eight mutants, frozen on 2026-10-29 before any was applied; each breaks one decision of the plan in the arm\'s final code, and the test classes that the arm changed, the classes that hold a scenario test and the classes of the area are re-run against it. **Killed** = some test that passed unmutated failed (confirmed on a re-run).');
  p('Per intended scenario test: **+** it failed (caught), **−** it exists by that name but still PASSED (missed). A **killedBy** count above the intended tests means other tests caught it too.');
  p();
  const mj = Object.fromEntries(ARMS.map((a) => [a, arm(a, 'mutation')]));
  const ids = mj.C1.mutants.map((x) => x.id);
  p('| Mutant | Decision | What it breaks | Intended scenarios |');
  p('|---|---|---|---|');
  for (const x of mj.C1.mutants) p(`| ${x.id.split('-')[0]} ${x.id.split('-').slice(1).join('-')} | ${x.decision} | ${x.breaks} | ${x.intendedScenarios.join(', ')} |`);
  p();
  p(`| Arm | Killed | Tests in scope (baseline) | ${ids.map((i) => i.split('-')[0]).join(' | ')} |`);
  p(`|---|---|---|${ids.map(() => '---').join('|')}|`);
  for (const a of ARMS) {
    const m = mj[a];
    const cellOf = (x) => `${x.verdict.toLowerCase()} (${x.intended.map((i) => `${i.scenario} ${i.present ? (i.caught ? '+' : '−') : '?'}`).join(', ')}; ${x.killedBy.length} test${x.killedBy.length === 1 ? '' : 's'})`;
    p(`| ${a} | ${m.summary.killed}/${m.summary.applicable} | ${m.baseline.tests} | ${m.mutants.map(cellOf).join(' | ')} |`);
  }
  p();
  const notCaught = ARMS.flatMap((a) => mj[a].mutants.flatMap((x) => x.intended.filter((i) => !i.caught).map((i) => `${a} ${x.id.split('-')[0]} ${i.scenario} (\`${i.test}\`)`)));
  p(`Killed in all: ${ARMS.reduce((n, a) => n + mj[a].summary.killed, 0)} of ${ARMS.reduce((n, a) => n + mj[a].summary.applicable, 0)} applicable mutants (${ARMS.reduce((n, a) => n + mj[a].summary.notApplicable.length, 0)} not applicable). Intended tests that did not catch their mutant: ${notCaught.length ? notCaught.join('; ') : 'none'}.`);
  p();
  return out.join('\n');
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1] && existsSync(D)) console.log(part6Tables());
