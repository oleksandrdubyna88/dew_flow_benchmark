// The two files the harness GENERATES into a workspace, judged like the template files (F1.S2 and F1.S3 meet): `STATEMENT.md` and `samples/load.json` are made from the one registered memory value, so a kept tree of an older
// series differs from today's bytes though the agent touched nothing. `init` records the sha256 of each (`generatedSha256`, per stack; `null` for a file the workspace does not have). For a series made before that, the bytes are
// made again as they were at the commit its subject recorded: before the memory value existed the statement had no memory and heap lines and the load file did not exist at all. Where that cannot be done the file is undecided.
import { createHash } from 'node:crypto';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { quickExplore } from './quick.mjs';
import { frameworkSentence, quickStackSentence, quickStatement, quickWorkspaceFiles } from './workspace-quick.mjs';

export const GENERATED_FILES = Object.freeze(['STATEMENT.md', 'samples/load.json']);
/** The file that came with the memory value: a commit that does not have it made workspaces without the memory lines and the load file. */
export const MEMORY_LIMIT_AT = 'spikes/lang-explore/inputs/memory-limit.json';
const EXPLORE_AT = 'spikes/lang-explore/inputs/explore.json';
const RECORD_AT = 'spikes/lang-bench/inputs/cache-snapshots-lang.json';
const STACKS = Object.freeze(['csharp', 'node']);

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const unknown = why => ({ known: false, why });
const jsonOf = bytes => { try { return bytes === null ? null : JSON.parse(bytes.toString('utf8')); } catch { return null; } };

/** The sha256 of each generated file among one stack's workspace files; `null` for one the workspace does not have. */
export const generatedHashes = files => Object.fromEntries(GENERATED_FILES.map(rel => [rel, files[rel] === undefined ? null : sha256(files[rel].bytes)]));

/** What `init` records: `ok({ csharp: { 'STATEMENT.md': sha256, 'samples/load.json': sha256 }, node: { ... } })` from the workspace files of today, or a `fail`. `explore` is the quick profile. */
export function generatedHashesNow({ explore, task, sources, inputs }) {
  const out = {};
  for (const stack of STACKS) {
    const built = quickWorkspaceFiles({ explore, task, stack, sources, inputs });
    if (!built.ok) return fail(`the workspace files of ${stack}/${task.kind}: ${built.reason}`);
    out[stack] = generatedHashes(built.value.files);
  }
  return ok(out);
}

/** The statement as an older series made it: the spec, the stack sentence of the commit's pins, the framework sentence of the commit's record, and no memory and heap lines. -> the bytes | null. */
function statementAt({ world, commit, stack, kind, readAt }) {
  const base = jsonOf(readAt(commit, EXPLORE_AT));
  if (base === null || typeof world?.sources?.spec !== 'string') return null;
  const explore = quickExplore(base, world.home);
  const record = jsonOf(readAt(commit, RECORD_AT));
  return Buffer.from(quickStatement({ spec: world.sources.spec, kind, stackSentence: quickStackSentence(explore, stack), frameworkSentence: frameworkSentence(explore, stack, record), memory: null }));
}

/** Whether the spec this host reads is the one the series was made with: the series recorded the task's pins at `init`. */
const specIsTheSeriess = ({ world, inputs }) => JSON.stringify(inputs?.taskPins?.spec) === JSON.stringify(world?.task?.spec);

/**
 * -> `({ commit, stack, kind, rel, readAt }) => { known: true, sha256 } | { known: true, absent: true } | { known: false, why }`: the bytes a generated file had in a series that recorded none. `world` is the loaded world of the
 * task (its spec text and pins), `inputs` the series' inputs.
 */
export function legacyGenerated({ world, inputs }) {
  return ({ commit, stack, kind, rel, readAt }) => {
    if (readAt(commit, MEMORY_LIMIT_AT) !== null) return unknown(`the series recorded no bytes for ${rel}, and its commit already generated it from the memory value`);
    return rel === 'samples/load.json' ? { known: true, absent: true } : legacyStatement({ world, inputs, commit, stack, kind, readAt });
  };
}

/** The statement as the series saw it, or why it cannot be made again. */
function legacyStatement({ world, inputs, commit, stack, kind, readAt }) {
  if (!specIsTheSeriess({ world, inputs })) return unknown('the spec this host reads is not the one the series was made with, so STATEMENT.md cannot be made again');
  const bytes = statementAt({ world, commit, stack, kind, readAt });
  return bytes === null ? unknown(`STATEMENT.md cannot be made again as the series saw it (the pins or the spec of commit ${commit} cannot be read)`) : { known: true, sha256: sha256(bytes) };
}
