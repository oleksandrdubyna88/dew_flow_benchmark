#!/usr/bin/env node
// Part 6: every coai finding of every arm's gate step, from the gate session's own stream — the review_plan / review_code
// replies (full findings: severity, category, file, line, title, why, fix, providers, role, gating) joined with the gate
// agent's resolve decisions (index → accept/reject + reason). Writes coai-findings.json at the experiment root.
//   node coai-findings.mjs
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
const E = '/home/jinx/git/coai-exp2';
const env = readFileSync(`${E}/exp.env`, 'utf8'); const MAP = env.match(/^MAP=(.+)$/m)[1].trim();
const map = JSON.parse(readFileSync(MAP, 'utf8'));
const textOf = (c) => Array.isArray(c) ? c.map((x) => x.text ?? '').join('') : String(c ?? '');
const out = {};
for (const [arm, tok] of Object.entries(map)) {
  const dir = `${E}/r/${tok}/out`;
  const streams = readdirSync(dir).filter((f) => /-gate\.a\d+\.jsonl$/.test(f)).sort();
  const calls = {}; const rounds = []; let pending = null;
  for (const s of streams) for (const line of readFileSync(`${dir}/${s}`, 'utf8').split('\n')) {
    let j; try { j = JSON.parse(line); } catch { continue; }
    for (const p of j.message?.content ?? []) {
      if (p.type === 'tool_use' && /mcp__coai__(review_plan|review_code|resolve)/.test(p.name)) calls[p.id] = { name: p.name.replace('mcp__coai__', ''), input: p.input };
      if (p.type !== 'tool_result' || !calls[p.tool_use_id]) continue;
      const call = calls[p.tool_use_id]; let r; try { r = JSON.parse(textOf(p.content)); } catch { continue; }
      if (call.name === 'resolve' && pending) {
        let ds = []; try { ds = JSON.parse(call.input.decisions); } catch { /* none */ }
        for (const d of ds) if (pending.findings[d.finding]) Object.assign(pending.findings[d.finding], { decision: d.action, reason: d.reason ?? '' });
        pending = null; continue;
      }
      if (call.name === 'resolve' || r.error) continue;
      const stage = call.name === 'review_plan' ? 'plan' : 'code';
      const round = { stage, number: rounds.filter((x) => x.stage === stage).length + 1, verdict: r.verdict, gating: r.gatingCount,
        threshold: r.threshold, reviewers: r.reviewers, findings: (r.findings ?? []).map((f, i) => ({ index: i, ...f })) };
      rounds.push(round); pending = round;
    }
  }
  out[arm] = rounds;
}
writeFileSync(`${E}/coai-findings.json`, JSON.stringify(out, null, 1));
for (const [arm, rs] of Object.entries(out)) {
  const code = rs.filter((r) => r.stage === 'code'); const fs = code.flatMap((r) => r.findings);
  const by = (k) => fs.reduce((o, f) => ({ ...o, [f[k]]: (o[f[k]] ?? 0) + 1 }), {});
  console.log(arm.padEnd(5), `code rounds ${code.length} (${code.map((r) => r.verdict).join(', ')})`, `findings ${fs.length}`, JSON.stringify(by('severity')),
    'decided', fs.filter((f) => f.decision).length, 'accepted', fs.filter((f) => f.decision === 'accept').length);
}
