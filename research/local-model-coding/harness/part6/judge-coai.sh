#!/usr/bin/env bash
# Part 6: a blind Opus judge per arm rates every coai CODE finding (real / partly / not real, severity, usefulness) at the commit
# its round reviewed, and scores the set 1-10. The gate agent's decisions and the providers are NOT shown (no anchoring). Labels are
# random; judge-coai/key.json maps them. 3 at a time; a session with verdict.json is skipped (re-runnable).
set -u
E=/home/jinx/git/coai-exp2; J=$E/judge-coai; mkdir -p "$J"
# shellcheck source=/dev/null
source "$E/exp.env"
node - "$E" "$J" "$MAP" "$BASE" "$PLAN" <<'NODE'
const fs=require('fs'), crypto=require('crypto'); const [E,J,MAP,BASE,PLAN]=process.argv.slice(2);
const map=JSON.parse(fs.readFileSync(MAP,'utf8')); const all=JSON.parse(fs.readFileSync(E+'/coai-findings.json','utf8'));
const key=fs.existsSync(J+'/key.json')?JSON.parse(fs.readFileSync(J+'/key.json','utf8')):{};
for (const [arm,tok] of Object.entries(map)) {
  if (Object.values(key).includes(arm)) continue;
  const sess=JSON.parse(fs.readFileSync(fs.readdirSync(`${E}/r/${tok}/coai-data/wsl-ubuntu/sessions`).filter(f=>f.endsWith('.json')).map(f=>`${E}/r/${tok}/coai-data/wsl-ubuntu/sessions/${f}`)[0],'utf8'));
  const shas=sess.rounds.filter(r=>r.stage==='CodeReview').map(r=>r.sha);
  const code=all[arm].filter(r=>r.stage==='code');
  const findings=code.flatMap((r,i)=>r.findings.map(f=>({round:r.number, index:f.index, sha:shas[i], severity:f.severity, category:f.category,
    file:f.file, line:f.line, title:f.title, why:f.why, fix:f.fix})));
  const label='F-'+crypto.randomBytes(3).toString('hex'); key[label]=arm;
  const dir=`${J}/${label}`; fs.mkdirSync(dir,{recursive:true});
  fs.writeFileSync(`${dir}/findings.json`, JSON.stringify(findings,null,1));
  fs.writeFileSync(`${dir}/cwd`, `${E}/r/${tok}/arm`);
  const tpl=fs.readFileSync(E+'/briefs/judge-coai.template.md','utf8');
  fs.writeFileSync(`${dir}/brief.md`, tpl.replaceAll('{{LABEL}}',label).replaceAll('{{PLAN}}',PLAN).replaceAll('{{BASE}}',BASE).replaceAll('{{FINDINGS}}',`${dir}/findings.json`));
}
fs.writeFileSync(J+'/key.json', JSON.stringify(key,null,1)); console.log('sessions', Object.keys(key).length);
NODE
judge_one() {
  local dir=$1
  [ -s "$dir/verdict.json" ] && return 0
  (cd "$(cat "$dir/cwd")" && TMPDIR=$dir timeout -s INT -k 60 2700 claude -p --model opus --output-format stream-json --verbose \
     --strict-mcp-config --mcp-config '{"mcpServers":{}}' --permission-mode default --disallowedTools Edit Write NotebookEdit --add-dir "$dir" \
     --allowedTools Read Glob Grep "Bash(git *)" "Bash(ls *)" "Bash(cat *)" "Bash(grep *)" "Bash(rg *)" "Bash(head *)" "Bash(tail *)" "Bash(wc *)" "Bash(sed *)" \
     < "$dir/brief.md" > "$dir/stream.jsonl" 2> "$dir/err.log")
  node -e '
    const fs=require("fs"); const d=process.argv[1];
    const r=fs.readFileSync(d+"/stream.jsonl","utf8").trim().split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop();
    const m=(r?.result??"").match(/\{[\s\S]*\}/); if (m && !r.is_error) fs.writeFileSync(d+"/verdict.json", m[0]);
    console.log(d.split("/").pop(), m && !r.is_error ? "ok" : "NO VERDICT "+String(r?.result??"").slice(0,120), "$"+(r?.total_cost_usd??0).toFixed(2), Math.round((r?.duration_ms??0)/60000)+"m");' "$dir"
}
export -f judge_one
ls -d "$J"/F-* | xargs -P 3 -I{} bash -c 'judge_one "$@"' _ {}
