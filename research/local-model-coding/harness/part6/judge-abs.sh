#!/usr/bin/env bash
# Part 6: the absolute blind judge — every arm's diff scored ALONE against the fixed rubric, by its own Opus session, twice (two
# runs, fresh opaque labels). The key goes to judge-abs/key.json and is never shown to a judge; each session's folder holds only its
# own diff and objective file. Words that would name a scheme or a model are redacted and counted. 4 at a time; re-runnable.
set -u
E=/home/jinx/git/coai-exp2; J=$E/judge-abs; mkdir -p "$J"
# shellcheck source=/dev/null
source "$E/exp.env"
node - "$E" "$J" "$MAP" "$BASE" <<'NODE'
const fs=require('fs'), {execSync}=require('child_process'), crypto=require('crypto'); const [E,J,MAP,BASE]=process.argv.slice(2);
const map=JSON.parse(fs.readFileSync(MAP,'utf8'));
const key=fs.existsSync(J+'/key.json')?JSON.parse(fs.readFileSync(J+'/key.json','utf8')):{redactions:{},sessions:{}};
const RE=/\b(Haiku|haiku|Sonnet|sonnet|Opus|opus|Fable|fable|task t\d+|tasks? (?:document|list|split)|split into tasks|fresh reviewer|reviewer \(Haiku\))\b|\(t\d+\)/g;
for (const [arm,tok] of Object.entries(map)) {
  if (arm in key.redactions) continue;
  const o=JSON.parse(fs.readFileSync(`${E}/r/${tok}/out/objective.json`,'utf8')); delete o.arm;
  const d=execSync(`git -C ${E}/r/${tok}/arm diff ${BASE}..HEAD`,{encoding:'utf8',maxBuffer:1<<28});
  key.redactions[arm]=(d.match(RE)??[]).length;
  for (const run of ['run1','run2']) {
    const label='D-'+crypto.randomBytes(3).toString('hex'); const dir=`${J}/${label}`; fs.mkdirSync(dir,{recursive:true});
    fs.writeFileSync(`${dir}/diff.patch`, d.replace(RE,'[redacted]')); fs.writeFileSync(`${dir}/objective.json`, JSON.stringify(o,null,2));
    const tpl=fs.readFileSync(E+'/briefs/5-judge-abs.template.md','utf8');
    fs.writeFileSync(`${dir}/brief.md`, tpl.replaceAll('{{LABEL}}',label).replaceAll('{{DIFF}}',`${dir}/diff.patch`).replaceAll('{{OBJECTIVE}}',`${dir}/objective.json`));
    key.sessions[label]={arm,run};
  }
}
fs.writeFileSync(J+'/key.json',JSON.stringify(key,null,2)); console.log('sessions',Object.keys(key.sessions).length,'redactions',JSON.stringify(key.redactions));
NODE
judge_one() {
  local dir=$1
  [ -s "$dir/verdict.json" ] && return 0
  (cd "$E/base" && TMPDIR=$dir timeout -s INT -k 60 2700 claude -p --model opus --output-format stream-json --verbose \
     --strict-mcp-config --mcp-config '{"mcpServers":{}}' --permission-mode default --disallowedTools Edit Write NotebookEdit --add-dir "$dir" \
     --allowedTools Read Glob Grep "Bash(git *)" "Bash(ls *)" "Bash(cat *)" "Bash(grep *)" "Bash(rg *)" "Bash(head *)" "Bash(tail *)" "Bash(wc *)" "Bash(sed *)" \
     < "$dir/brief.md" > "$dir/stream.jsonl" 2> "$dir/err.log")
  node -e '
    const fs=require("fs"); const d=process.argv[1];
    const r=fs.readFileSync(d+"/stream.jsonl","utf8").trim().split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop();
    const m=(r?.result??"").match(/\{[\s\S]*\}/); if (m && !r.is_error) fs.writeFileSync(d+"/verdict.json", m[0]);
    console.log(d.split("/").pop(), m && !r.is_error ? "ok" : "NO VERDICT "+String(r?.result??"").slice(0,100), "$"+(r?.total_cost_usd??0).toFixed(2), Math.round((r?.duration_ms??0)/60000)+"m");' "$dir"
}
export -f judge_one; export E
ls -d "$J"/D-* | xargs -P 4 -I{} bash -c 'judge_one "$@"' _ {}
node -e '
  const fs=require("fs"); const J=process.argv[1]; const key=JSON.parse(fs.readFileSync(J+"/key.json","utf8")); const per={};
  for (const [label,{arm,run}] of Object.entries(key.sessions)) { const f=`${J}/${label}/verdict.json`; if (!fs.existsSync(f)) continue; (per[arm]??=[]).push({run,...JSON.parse(fs.readFileSync(f,"utf8"))}); }
  fs.writeFileSync(J+"/scores.json", JSON.stringify(per,null,2));
  for (const [arm,vs] of Object.entries(per)) console.log(arm.padEnd(5), "overall", vs.map(v=>v.overall).join(" / "), "| corr", vs.map(v=>v.correctness).join("/"), "tests", vs.map(v=>v.tests).join("/"), "conv", vs.map(v=>v.conventions).join("/"), "simp", vs.map(v=>v.simplicity).join("/"), "| defects", vs.map(v=>(v.defects??[]).map(d=>({blocking:"B",major:"M",minor:"m"})[d.severity]??"?").join("")).join(" / "));' "$J"
