import path from 'path'; import {fileURLToPath} from 'url';
const HERE=path.dirname(fileURLToPath(import.meta.url));
const REPO=process.env.BENCH_REPO||path.resolve(HERE,'../../..').split(path.sep).join('/');
import {loadStory} from '../lib/story.mjs';
const STORY=loadStory(process.env.STORY||'creds-176');
const CREDS=process.env.CREDS_REPO||STORY.repoClone;
import fs from 'fs'; import {execSync} from 'child_process'; import crypto from 'crypto';
const Q=path.resolve(process.argv[2]||process.env.RUNS_ROOT||'.').split(path.sep).join('/')+'/';
const scrub=s=>{const c={model:0,label:0};
 s=s.replace(/\b(opus|sonnet|fable|haiku|claude-[a-z0-9.-]+)\b/gi,()=>{c.model++;return '[model]'});
 s=s.replace(/\b(c[1-9]|reference|arm A|arm B)\b/gi,()=>{c.label++;return '[label]'});
 return [s,c]};
const refRaw=execSync(`git -C ${CREDS} diff ${STORY.P} ${STORY.S}`,{maxBuffer:1e9,encoding:'utf8'});
const parts=refRaw.split(/(?=^diff --git )/m).filter(p=>{const m=p.match(/^diff --git a\/(\S+)/);if(!m)return false;const f=m[1];return !f.startsWith('todo/')&&!f.startsWith('.claude/')&&!f.startsWith('.agents/')&&!f.endsWith('.md')});
const refDiff=parts.join('');
function resultText(c){const l=fs.readFileSync(Q+c+'/stream.ndjson','utf8').split('\n').filter(Boolean);
 for(let i=l.length-1;i>=0;i--){try{const j=JSON.parse(l[i]);if(j.type==='result'&&typeof j.result==='string')return j.result}catch{}}return ''}
const cells=['c1','c2','c3','c4','c5','c6','c7','c8','c9'].filter(c=>fs.existsSync(Q+c+'/candidate.diff'));
const src=Object.fromEntries(cells.map(c=>[c,[fs.readFileSync(Q+c+'/candidate.diff','utf8'),resultText(c)]]));
src.reference=[refDiff,execSync(`git -C ${CREDS} log -1 --format=%B ${STORY.S}`,{encoding:'utf8'})];
const names=Object.keys(src).sort(()=>crypto.randomInt(3)-1);
const key={};
for(const n of names){const id=crypto.randomBytes(4).toString('hex');const [d,cd]=scrub(src[n][0]);const [s,cs]=scrub(src[n][1]);
 fs.mkdirSync(Q+'assess/'+id,{recursive:true});fs.writeFileSync(Q+'assess/'+id+'/candidate.diff',d);fs.writeFileSync(Q+'assess/'+id+'/summary.txt',s);
 key[id]={source:n,scrub:{diff:cd,summary:cs},summaryChars:s.length}}
fs.writeFileSync(Q+'assess-key.json',JSON.stringify(key,null,1));console.log(key);
