import path from 'path'; import {fileURLToPath} from 'url';
const HERE=path.dirname(fileURLToPath(import.meta.url));
const REPO=process.env.BENCH_REPO||path.resolve(HERE,'../../..').split(path.sep).join('/');
const CREDS=process.env.CREDS_REPO||'D:/rsd/dew_flow_creds_for_devs';
import fs from 'fs'; import {spawnSync} from 'child_process';
const Q=path.resolve(process.argv[2]||process.env.RUNS_ROOT||'.').split(path.sep).join('/')+'/';const I=REPO+'/spikes/policy-ab-quick/inputs/';
const stmt=fs.readFileSync(I+'statement.md','utf8');const cl=JSON.parse(fs.readFileSync(I+'checklist.json','utf8'));
const hold=JSON.parse(fs.readFileSync(I+'holdout.json','utf8'));
const key=JSON.parse(fs.readFileSync(Q+'assess-key.json','utf8'));
const prompt=(extra='')=>`You are assessing a candidate implementation against a frozen requirement statement. READ-ONLY. Read ONLY the files candidate.diff and summary.txt in your current directory (do not look elsewhere).

=== STATEMENT ===
${stmt}

=== CHECKLIST (R1-R12) ===
${JSON.stringify(cl.items,null,1)}

Note: R7-R10 (harness/fixture items) are judged by reading the diff, not by running tests. R12 is judged from summary.txt (and any commit text in it).

Reply with ONLY a JSON object, no prose, no code fence: {"verdicts":[{"id":"R1","verdict":"met|partial|not_met|unresolved","evidence":"file:line or summary quote","note":"<=25 words"}, ... 12 items R1..R12]}. 'met' and 'partial' require non-empty evidence.${extra}`;
const IDS=Array.from({length:12},(_,i)=>'R'+(i+1));
function check(t){let j;try{const m=t.match(/\{[\s\S]*\}/);j=JSON.parse(m[0])}catch(e){return 'not JSON'}
 const v=j.verdicts;if(!Array.isArray(v))return 'no verdicts';
 for(const id of IDS){const x=v.find(y=>y.id===id);if(!x)return 'missing '+id;
  if(!['met','partial','not_met','unresolved'].includes(x.verdict))return 'bad verdict '+id;
  if((x.verdict==='met'||x.verdict==='partial')&&!String(x.evidence||'').trim())return 'no evidence '+id}
 return j}
const out={};
for(const id of Object.keys(key)){const cwd=Q+'assess/'+id;const t0=Date.now();let attempts=[];let parsed=null;
 for(let a=0;a<2&&!parsed;a++){
  const r=spawnSync('codex',['exec','--sandbox','read-only','--skip-git-repo-check','-C',cwd,'--ephemeral','-'],{input:prompt(a?'\nYour previous reply was invalid ('+attempts.at(-1).err+'). Output ONLY the JSON object.':''),encoding:'utf8',maxBuffer:1e9,shell:true});
  const raw=r.stdout||'';const c=check(raw);attempts.push({raw,stderrTail:(r.stderr||'').slice(-1500),status:r.status,err:typeof c==='string'?c:null});
  if(typeof c!=='string')parsed=c}
 out[id]={source:key[id].source,attempts,parsed,wallSeconds:(Date.now()-t0)/1000};
 fs.writeFileSync(Q+'assess-results.json',JSON.stringify(out,null,1));console.log(id,key[id].source,parsed?'ok':'FAILED',out[id].wallSeconds)}
