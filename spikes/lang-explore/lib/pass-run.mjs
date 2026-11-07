// The pass child from the driver's side (F1.S2, section 3.1 `runPassChild`, and the wall bound of the epic's plan round). ONE pass runs in a child process started under `taskset -c <profile>`: it inherits the affinity, and so
// do the scope, the namespaces, the box and every program (the execution layer is not changed; the affinity is applied outside it). Three things are checked here, none of them trusted:
//   - the child's mask is READ BACK right after the spawn (`Cpus_allowed_list`, compared as a set); another mask kills the child and is a HALT (`affinity-not-applied`), never an attempt;
//   - the child has a WALL BOUND: the layer's own ceiling for the case set plus one stated margin for the runner's start and end; at the deadline the child's whole process group is killed, awaited, and the attempt is an
//     environment one (`pass-child timeout`) that counts toward the pass's three;
//   - a child that ends without an answer is an environment attempt naming how it ended.
import { spawn as nodeSpawn } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { maskObserved } from '../../minigit-bench/lib/cpu-sampler.mjs';
import { realProcApi } from '../../minigit-bench/lib/janitor.mjs';
import { affinityProblem } from './affinity.mjs';

/** The time the child may take beyond the layer's ceiling for the case set: the runner's start (the startup sweep, the holder) and its end (the cleanup). Stated once. */
export const PASS_CHILD_MARGIN_MS = 120_000;
/** How long a killed child may take to be gone before the answer is given anyway. */
const GONE_WAIT_MS = 10_000;
const CHILD_FILE = fileURLToPath(new URL('./pass-child.mjs', import.meta.url));

/** The whole child's deadline: the layer's ceiling for the case set (`runCeilingMs`) plus the margin. */
export const passChildDeadlineMs = ({ runCeilingMs }) => runCeilingMs + PASS_CHILD_MARGIN_MS;

const after = ms => new Promise(resolve => { setTimeout(resolve, ms); });

/** A promise of how the child ended: `{ code, signal }`. */
const endOf = child => new Promise(resolve => { child.on('close', (code, signal) => resolve({ code, signal })); child.on('error', e => resolve({ code: null, signal: null, error: e.message })); });

/** The last bytes the child said on standard error. */
function tailOf(child) {
  const chunks = [];
  child.stderr?.on('data', d => { chunks.push(String(d)); });
  return () => chunks.join('').slice(-300);
}

/** The folder of the child's files (beside the built copy), the arguments file written, and where the answer goes. */
function argsFor({ spec, context }) {
  const dir = path.join(path.dirname(spec.tree), 'child');
  mkdirSync(dir, { recursive: true });
  const stem = `p${spec.n}-a${spec.attempt}`;
  const out = path.join(dir, `${stem}.answer.json`);
  const argsFile = path.join(dir, `${stem}.args.json`);
  writeFileSync(argsFile, JSON.stringify({ spec, context, out }));
  return { dir, out, argsFile };
}

/** The sentence naming the mask the child has against the profile, or null when it is the profile. */
function maskNow({ child, wanted, procApi }) {
  const seen = maskObserved({ pids: [child.pid], mask: wanted, procApi }).perPid[0]?.allowed ?? null;
  return affinityProblem({ wanted, observed: seen });
}

/** Wait for the mask the profile asks for to show in `/proc/<pid>/status` (the `taskset` process sets it just before it becomes the child; a few polls allow for that): the sentence if it never does, or null. */
async function maskProblem({ polls, pollMs, ...mask }) {
  const problem = maskNow(mask);
  if (problem === null || polls <= 1) return problem;
  await after(pollMs);
  return maskProblem({ ...mask, polls: polls - 1, pollMs });
}

async function killAndWait({ child, ended, killGroup }) {
  killGroup(child.pid);
  await Promise.race([ended, after(GONE_WAIT_MS)]);
}

const environment = reason => ({ kind: 'environment', reason });

const endNote = ended => `exit ${ended.code ?? 'none'}${ended.signal ? `, signal ${ended.signal}` : ''}${ended.error ? `, ${ended.error}` : ''}`;

/** The answer file, or the environment attempt for a child that wrote none. */
function answerOf({ out, ended, tail }) {
  try { return JSON.parse(readFileSync(out, 'utf8')); } catch { return environment(`the pass child ended without an answer (${endNote(ended)}): ${tail() || 'no output'}`); }
}

/**
 * ONE pass in a child under `taskset -c <spec.cpus>`. `spec` is the driver's (`tree`, `samplesDir`, `caseIds`, `cpus`, `n`, `attempt`, the bounds), `context` what the child needs to rebuild the layer's call (`stack`, `kind`, `dirs`,
 * `caseWallMs`, `runCeilingMs`). The seams (`spawn`, `procApi`, `killGroup`, `pollMs`, `polls`) are for tests. -> the child's body `{ kind, ... }`, or `{ halt: 'affinity', kind: 'environment', reason }` (no attempt).
 */
export async function runPassChild({ spec, context, deadlineMs = passChildDeadlineMs(spec), spawn = nodeSpawn, procApi = realProcApi, killGroup = killTree, pollMs = 25, polls = 40, nodePath = process.execPath, childFile = CHILD_FILE }) {
  const files = argsFor({ spec, context });
  const child = spawn('taskset', ['-c', spec.cpus, nodePath, childFile, files.argsFile], { detached: true, stdio: ['ignore', 'ignore', 'pipe'], env: process.env });
  const tail = tailOf(child);
  const ended = endOf(child);
  try {
    const wrong = await maskProblem({ child, wanted: spec.cpus, procApi, pollMs, polls });
    if (wrong) { await killAndWait({ child, ended, killGroup }); return { halt: 'affinity', ...environment(wrong) }; }
    const result = await Promise.race([ended, after(deadlineMs).then(() => null)]);
    if (result === null) { await killAndWait({ child, ended, killGroup }); return environment(`pass-child timeout: no answer within ${deadlineMs} ms (the layer's ceiling plus ${PASS_CHILD_MARGIN_MS} ms); its process group was killed`); }
    return answerOf({ out: files.out, ended: result, tail });
  } finally {
    rmSync(files.dir, { recursive: true, force: true });
  }
}
