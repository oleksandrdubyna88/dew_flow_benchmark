#!/usr/bin/env node
// Part 6: the token counts that the coai server reported in the `cost` field of each review_plan / review_code reply of each arm's gate
// step (tokensIn, tokensOut), read from the gate session's own stream. These are TOKENS of the other vendors' models, not dollars;
// the reply gives one pair per round, not one per provider. Writes coai-cost.json next to coai-findings.json.
//   node coai-cost.mjs [out.json]
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
const E = '/home/jinx/git/coai-exp2';
const MAP = readFileSync(`${E}/exp.env`, 'utf8').match(/^MAP=(.+)$/m)[1].trim();
const map = JSON.parse(readFileSync(MAP, 'utf8'));
const textOf = (c) => Array.isArray(c) ? c.map((x) => x.text ?? '').join('') : String(c ?? '');
const out = {};
for (const [arm, tok] of Object.entries(map)) {
  const dir = `${E}/r/${tok}/out`; const calls = {}; const rounds = [];
  for (const s of readdirSync(dir).filter((f) => /-gate\.a\d+\.jsonl$/.test(f)).sort()) for (const line of readFileSync(`${dir}/${s}`, 'utf8').split('\n')) {
    let j; try { j = JSON.parse(line); } catch { continue; }
    for (const p of j.message?.content ?? []) {
      if (p.type === 'tool_use' && /mcp__coai__review_(plan|code)/.test(p.name)) calls[p.id] = p.name.replace('mcp__coai__review_', '');
      if (p.type !== 'tool_result' || !calls[p.tool_use_id]) continue;
      let r; try { r = JSON.parse(textOf(p.content)); } catch { continue; }
      if (r.error || !r.cost) continue;
      const stage = calls[p.tool_use_id]; rounds.push({ stage, number: rounds.filter((x) => x.stage === stage).length + 1, reviewers: r.reviewers, tokensIn: r.cost.tokensIn, tokensOut: r.cost.tokensOut });
    }
  }
  out[arm] = rounds;
}
const dest = process.argv[2] ?? `${E}/coai-cost.json`;
writeFileSync(dest, JSON.stringify(out, null, 1));
for (const [a, rs] of Object.entries(out)) console.log(a, rs.map((r) => `${r.stage}${r.number} in ${r.tokensIn} out ${r.tokensOut}`).join(' | '));
