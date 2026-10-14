import path from 'path'; import {fileURLToPath} from 'url';
const HERE=path.dirname(fileURLToPath(import.meta.url));
const REPO=process.env.BENCH_REPO||path.resolve(HERE,'../../..').split(path.sep).join('/');
const CREDS=process.env.CREDS_REPO||'D:/rsd/dew_flow_creds_for_devs';
import fs from 'fs';
import {machineScrubber,scrubStrings} from '../lib/scrub.mjs';
const Q=path.resolve(process.argv[2]||process.env.RUNS_ROOT||'.').split(path.sep).join('/')+'/';const res=JSON.parse(fs.readFileSync(Q+'assess-results.json','utf8'));const key=JSON.parse(fs.readFileSync(Q+'assess-key.json','utf8'));
const m=(res[Object.keys(res)[0]].attempts[0].stderrTail.match(/model: *(\S+)/)||[])[1]||null;
const out={codexVersion:'codex-cli 0.160.0',codexModel:m,sanityGate:null,sources:{}};
for(const [id,r] of Object.entries(res)){const v=r.parsed.verdicts;const c={met:0,partial:0,not_met:0,unresolved:0};v.forEach(x=>c[x.verdict]++);
 out.sources[r.source]={counts:c,wallSeconds:r.wallSeconds,attempts:r.attempts.length,verdicts:v.map(({id,verdict,evidence,note})=>({id,verdict,evidence,note}))}}
const met=out.sources.reference.counts.met;out.sanityGate={referenceMet:met,threshold:9,pass:met>=9};
const clean=scrubStrings(out,machineScrubber({runsRoot:Q.slice(0,-1),creds:CREDS}));
fs.writeFileSync(REPO+'/research/bench/quick-pilot-reading-'+(process.argv[3]||'run')+'.json',JSON.stringify(clean,null,1));
for(const [s,o] of Object.entries(out.sources)){console.log(s,JSON.stringify(o.counts),o.wallSeconds);o.verdicts.filter(x=>x.verdict==='not_met').forEach(x=>console.log('  NOT_MET',x.id,x.evidence,'|',x.note))}
console.log(out.sanityGate,m);
