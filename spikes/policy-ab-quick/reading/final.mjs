import path from 'path'; import {fileURLToPath} from 'url';
const HERE=path.dirname(fileURLToPath(import.meta.url));
const REPO=process.env.BENCH_REPO||path.resolve(HERE,'../../..').split(path.sep).join('/');
import {loadStory} from '../lib/story.mjs';
const STORY=loadStory(process.env.STORY||'creds-176');
const CREDS=process.env.CREDS_REPO||STORY.repoClone;
import fs from 'fs';
import {machineScrubber,scrubStrings} from '../lib/scrub.mjs';
const Q=path.resolve(process.argv[2]||process.env.RUNS_ROOT||'.').split(path.sep).join('/')+'/';const res=JSON.parse(fs.readFileSync(Q+'assess-results.json','utf8'));const key=JSON.parse(fs.readFileSync(Q+'assess-key.json','utf8'));
const m=(res[Object.keys(res)[0]].attempts[0].stderrTail.match(/model: *(\S+)/)||[])[1]||null;
const out={codexVersion:'codex-cli 0.160.0',codexModel:m,sanityGate:null,sources:{}};
for(const [id,r] of Object.entries(res)){const v=r.parsed.verdicts;const c={met:0,partial:0,not_met:0,unresolved:0};v.forEach(x=>c[x.verdict]++);
 out.sources[r.source]={counts:c,wallSeconds:r.wallSeconds,attempts:r.attempts.length,verdicts:v.map(({id,verdict,evidence,note})=>({id,verdict,evidence,note}))}}
const met=out.sources.reference.counts.met;const threshold=JSON.parse(fs.readFileSync(STORY.inputsDir+'/checklist.json','utf8')).sanityThreshold??9;out.sanityGate={referenceMet:met,threshold,pass:met>=threshold};
const clean=scrubStrings(out,machineScrubber({runsRoot:Q.slice(0,-1),creds:CREDS,label:STORY.scrubLabel}));
fs.writeFileSync(REPO+'/research/bench/'+(STORY.id==='creds-176'?'quick-pilot':'policy-ab-'+STORY.id)+'-reading-'+(process.argv[3]||'run')+'.json',JSON.stringify(clean,null,1));
for(const [s,o] of Object.entries(out.sources)){console.log(s,JSON.stringify(o.counts),o.wallSeconds);o.verdicts.filter(x=>x.verdict==='not_met').forEach(x=>console.log('  NOT_MET',x.id,x.evidence,'|',x.note))}
console.log(out.sanityGate,m);
