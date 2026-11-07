// The entry of the pass child (F1.S2, section 3.1; the shape of `minigit-bench/score-session.mjs`): `node pass-child.mjs <args file>`, started by `runPassChild` under `taskset -c <profile>`. It reads its OWN affinity first (a mask
// that is not the profile is an environment answer naming both masks, and no case runs), then runs ONE pass of `score-quick.mjs` over the committed tree and writes its answer to the file it was given: exit 0 a completed
// pass, 3 an environment answer, 4 usage. It inherits the affinity, and so do the scope, the namespaces, the box and every program: the execution layer is not changed.
import { mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { affinityProblem, allowedListOf } from './affinity.mjs';
import { OWNED_TOKEN, runPass } from './score-quick.mjs';

/** The default pass: the layer over the committed tree and the case folder, from what the driver wrote into the arguments file. */
export function layerPass({ spec, context }) {
  const a = { stack: context.stack, limits: spec.limits, profile: { kind: context.kind, caseWallMs: spec.caseWallMs, runCeilingMs: spec.runCeilingMs } };
  return runPass({ a, copy: spec.tree, cases: { dir: spec.samplesDir, ids: spec.caseIds }, owned: ownedFor({ token: OWNED_TOKEN }), dirs: context.dirs });
}

const atomicWrite = (file, text) => { mkdirSync(path.dirname(file), { recursive: true }); writeFileSync(`${file}.part`, text); renameSync(`${file}.part`, file); };

/** The body of a pass the layer judged (every case) or could not: what the child writes and an in-process pass returns. */
export function passBody({ ran, observedCpus }) {
  if (!ran.ok) return { kind: 'environment', reason: ran.reason, observedCpus };
  return { kind: 'completed', record: ran.value.record, runner: ran.value.record.runner, memoryCertified: ran.value.ran.memoryCertified === true, observedCpus };
}

/** Run the child with its seams: `argv` (the arguments file), `readStatus` (its own `/proc/self/status`), `runPassFn({ spec, context })`, `write(file, text)`. -> the exit code. */
export async function childMain({ argv, readStatus = () => readFileSync('/proc/self/status', 'utf8'), runPassFn = layerPass, write = atomicWrite }) {
  if (argv.length !== 1) return 4;
  const { spec, context, out } = JSON.parse(readFileSync(argv[0], 'utf8'));
  const observedCpus = allowedListOf(readStatus());
  const problem = affinityProblem({ wanted: spec.cpus, observed: observedCpus });
  const body = problem ? { kind: 'environment', reason: problem, observedCpus } : passBody({ ran: await runPassFn({ spec, context }), observedCpus });
  write(out, `${JSON.stringify(body)}\n`);
  return body.kind === 'completed' ? 0 : 3;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = await childMain({ argv: process.argv.slice(2) });
