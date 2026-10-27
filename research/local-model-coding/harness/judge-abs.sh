#!/usr/bin/env bash
# The absolute blind judge: every arm's diff is scored ALONE against a fixed, anchored rubric, by its own Opus session, twice
# (two independent runs, each with fresh opaque labels). The label key goes to judge-abs/key.json and is never shown to a
# judge; each session's folder holds only its own diff and objective file. Mentions of the local model and leftover fill
# markers are redacted, and the count of redactions per arm is recorded. 4 sessions at a time. Re-runnable: a session whose
# verdict.json exists is skipped.   Usage: judge-abs.sh
set -u
E=/home/jinx/git/coai-exp; J=$E/judge-abs; mkdir -p "$J"
node - "$E" "$J" <<'EOF'
const fs = require('fs'); const { execSync } = require('child_process'); const crypto = require('crypto');
const [E, J] = process.argv.slice(2);
// incremental: an arm already in the key keeps its sessions; only arms new since the last run get theirs (scores are absolute)
const key = fs.existsSync(`${J}/key.json`) ? JSON.parse(fs.readFileSync(`${J}/key.json`, 'utf8')) : { redactions: {}, sessions: {} };
const arms = ['V', 'C', 'C2', 'VT', 'C3', 'C4', 'Q1', 'Q2', 'Q3', 'Q4', 'H1', 'H2', 'H3', 'H4', 'T1', 'T2', 'T3', 'T4', 'O1', 'D1', 'S1', 'O2', 'D2', 'S2', 'O3', 'D3', 'S3', 'O4', 'D4', 'S4', 'SR1', 'DR1', 'SR2', 'DR2', 'SR3', 'DR3', 'SR4', 'DR4']
  .filter((a) => fs.existsSync(`${E}/results/${a}/objective.json`) && !(a in key.redactions));
// widened 2026-10-26 after the skeleton arms' plan notes gave their scheme away; applies to arms judged from then on
const RE = /\b(vLLM|vllm|local model|Qwen[\w.-]*|ask\.mjs|filling author|[Ss]keleton|Haiku|haiku|task t\d+|tasks? (?:document|list|split))\b|\(t\d+\)|QWEN-FILL-\S*|coai-exp\/vllm\S*/g;
for (const a of arms) {
  const d = execSync(`git -C ${E}/arm-${a} diff b4e2ea96..HEAD`, { encoding: 'utf8', maxBuffer: 1 << 28 });
  key.redactions[a] = (d.match(RE) ?? []).length;
  const o = JSON.parse(fs.readFileSync(`${E}/results/${a}/objective.json`, 'utf8')); delete o.arm;
  for (const run of ['run1', 'run2']) {
    const label = 'D-' + crypto.randomBytes(3).toString('hex');
    const dir = `${J}/${label}`; fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(`${dir}/diff.patch`, d.replace(RE, '[redacted]'));
    fs.writeFileSync(`${dir}/objective.json`, JSON.stringify(o, null, 2));
    const tpl = fs.readFileSync(`${E}/briefs/5-judge-abs.template.md`, 'utf8');
    fs.writeFileSync(`${dir}/brief.md`, tpl.replaceAll('{{LABEL}}', label).replaceAll('{{DIFF}}', `${dir}/diff.patch`)
      .replaceAll('{{OBJECTIVE}}', `${dir}/objective.json`));
    key.sessions[label] = { arm: a, run };
  }
}
fs.writeFileSync(`${J}/key.json`, JSON.stringify(key, null, 2));
console.log('new arms:', arms.join(' ') || 'none', '| sessions:', Object.keys(key.sessions).length, 'redactions per arm:', JSON.stringify(key.redactions));
EOF
judge_one() {
  local dir=$1
  [ -s "$dir/verdict.json" ] && return 0
  (cd "$E/base" && timeout -s INT -k 60 1800 claude -p --model opus --output-format stream-json --verbose \
     --permission-mode default --disallowedTools Edit Write NotebookEdit --add-dir "$dir" \
     --allowedTools Read Glob Grep "Bash(git *)" "Bash(ls *)" "Bash(cat *)" "Bash(grep *)" "Bash(rg *)" "Bash(head *)" "Bash(tail *)" "Bash(wc *)" "Bash(sed *)" \
     < "$dir/brief.md" > "$dir/stream.jsonl" 2> "$dir/err.log")
  node -e '
    const fs=require("fs"); const d=process.argv[1];
    const r=fs.readFileSync(d+"/stream.jsonl","utf8").trim().split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop();
    const m=(r?.result??"").match(/\{[\s\S]*\}/);
    if (m) fs.writeFileSync(d+"/verdict.json", m[0]);
    console.log(d.split("/").pop(), m ? "ok" : "NO VERDICT", "cost", r?.total_cost_usd, "min", Math.round((r?.duration_ms??0)/60000));' "$dir"
}
export -f judge_one; export E
ls -d "$J"/D-* | xargs -P 4 -I{} bash -c 'judge_one "$@"' _ {}
node -e '
  const fs=require("fs"); const J=process.argv[1]; const key=JSON.parse(fs.readFileSync(J+"/key.json","utf8"));
  const per={};
  for (const [label,{arm,run}] of Object.entries(key.sessions)) {
    const f=`${J}/${label}/verdict.json`; if (!fs.existsSync(f)) continue;
    const v=JSON.parse(fs.readFileSync(f,"utf8")); (per[arm]??=[]).push({run,...v});
  }
  fs.writeFileSync(J+"/scores.json", JSON.stringify(per,null,2));
  for (const [arm,vs] of Object.entries(per)) console.log(arm.padEnd(3), "overall", vs.map(v=>v.overall).join(" / "), "| corr", vs.map(v=>v.correctness).join("/"), "tests", vs.map(v=>v.tests).join("/"), "conv", vs.map(v=>v.conventions).join("/"), "simp", vs.map(v=>v.simplicity).join("/"), "| defects", vs.map(v=>(v.defects??[]).map(d=>({blocking:"B",major:"M",minor:"m"})[d.severity]??"?").join("")).join(" / "));' "$J"
