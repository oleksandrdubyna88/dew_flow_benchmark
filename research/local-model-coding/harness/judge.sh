#!/usr/bin/env bash
# The blind final judge: two Opus sessions, in parallel, each seeing the three arms' diffs under its own random labels.
# The label key is written to judge/key.json and never shown to a judge. Mentions of the local model are redacted in the
# diffs, and the count of redactions is recorded. Run after objective-checks.sh for every arm.   Usage: judge.sh
set -u
E=/home/jinx/git/coai-exp; J=$E/judge; mkdir -p "$J"
node - "$E" "$J" <<'EOF'
const fs = require('fs'); const { execSync } = require('child_process'); const crypto = require('crypto');
const [E, J] = process.argv.slice(2); const arms = ['V', 'C', 'C2', 'VT', 'C3', 'C4'].filter((a) => fs.existsSync(`${E}/results/${a}/objective.json`));
const shuffle = (a) => { const b = [...a]; for (let i = b.length - 1; i > 0; i--) { const j = crypto.randomInt(i + 1); [b[i], b[j]] = [b[j], b[i]]; } return b; };
const first = shuffle(arms);
let second = shuffle(arms); while (second.some((a, i) => a === first[i])) second = shuffle(arms); // every arm moves
const RE = /\b(vLLM|vllm|local model|Qwen[\w.-]*|ask\.mjs)\b|coai-exp\/vllm\S*/g;
const key = { redactions: {} };
const diffs = {};
for (const a of arms) {
  const d = execSync(`git -C ${E}/arm-${a} diff b4e2ea96..HEAD`, { encoding: 'utf8', maxBuffer: 1 << 28 });
  key.redactions[a] = (d.match(RE) ?? []).length; diffs[a] = d.replace(RE, '[redacted]');
}
[['run1', first], ['run2', second]].forEach(([run, order]) => {
  const dir = `${J}/${run}`; fs.mkdirSync(dir, { recursive: true });
  const labels = ['P', 'Q', 'R', 'S', 'T', 'U'].slice(6 - arms.length); key[run] = {};
  order.forEach((arm, i) => {
    const L = labels[i]; key[run][L] = arm;
    fs.writeFileSync(`${dir}/diff-${L}.patch`, diffs[arm]);
    const o = JSON.parse(fs.readFileSync(`${E}/results/${arm}/objective.json`, 'utf8')); delete o.arm;
    fs.writeFileSync(`${dir}/objective-${L}.json`, JSON.stringify(o, null, 2));
  });
  const tpl = fs.readFileSync(`${E}/briefs/4-judge.template.md`, 'utf8');
  fs.writeFileSync(`${dir}/brief.md`, tpl.replaceAll('{{LABELS}}', labels.slice(0, -1).join(', ') + ' and ' + labels.at(-1))
    .replaceAll('{{DIFFS}}', labels.map((L) => `\`${dir}/diff-${L}.patch\``).join(', '))
    .replaceAll('{{OBJECTIVE}}', labels.map((L) => `\`${dir}/objective-${L}.json\``).join(', ')));
});
fs.writeFileSync(`${J}/key.json`, JSON.stringify(key, null, 2));
console.log('redactions per arm:', JSON.stringify(key.redactions));
EOF
for run in run1 run2; do
  (cd "$E/base" && timeout -s INT -k 60 2400 claude -p --model opus --output-format stream-json --verbose \
     --permission-mode default --disallowedTools Edit Write NotebookEdit --add-dir "$J/$run" \
     --allowedTools Read Glob Grep "Bash(git *)" "Bash(ls *)" "Bash(cat *)" "Bash(grep *)" "Bash(rg *)" "Bash(head *)" "Bash(tail *)" "Bash(wc *)" "Bash(sed *)" \
     < "$J/$run/brief.md" > "$J/$run/stream.jsonl" 2> "$J/$run/err.log") &
done
wait
for run in run1 run2; do
  node -e '
    const fs=require("fs"); const r=fs.readFileSync(process.argv[1],"utf8").trim().split("\n").map(l=>{try{return JSON.parse(l)}catch{return null}}).filter(x=>x&&x.type==="result").pop();
    const m=(r?.result??"").match(/\{[\s\S]*\}/); fs.writeFileSync(process.argv[2], m?m[0]:JSON.stringify({unparsed:r?.result??null}));
    console.log(process.argv[3], "cost", r?.total_cost_usd, "min", Math.round((r?.duration_ms??0)/60000));' \
    "$J/$run/stream.jsonl" "$J/$run/verdict.json" "$run"
done
