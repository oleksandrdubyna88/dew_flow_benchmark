// E1.S8b brief B: the memory view, ASSERTED (T-L43; coai risk 19ae7208; parent D51). The box mounts no `/sys/fs/cgroup`, and .NET finds its memory limit through the cgroup mount, so the
// two stacks may believe different limits under one `MemoryMax`. The probe records what each runtime believes at 256 and 512 MiB and checks it against the host's `prog/memory.max`
// by the policy D15 records. OP-K — which policy — is the team lead's to decide on that record: `inputs/specs.d/memory-policy.json` is `{ policy, decided, by }`, `policy` null while
// undecided. A probe that only printed a mismatch and exited 0 would let the stacks meet different bounds, so: undecided is exit 3 `memory-policy-undecided`, a mismatch is exit 3
// `memory-view-mismatch`, and exit 0 only when a policy holds. The judgement is pure: observations in, `{ ok, halt, problems }` out.
import { readFileSync } from 'node:fs';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';

const MIB = 1024 * 1024;
/** The decision of OP-K (parent D15, 2026-11-02): each program sees its OWN cgroup read-only, and .NET gets `DOTNET_GCHeapHardLimit` = the case's limit, so both stacks believe `prog/memory.max`. */
export const POLICY_DECIDED = 'cgroup-view-own+gchl-limit';
export const POLICY_NAMES = Object.freeze(['runtime-view-accepted', 'dotnet-gc-hard-limit-percent:<n>', 'cgroup-view-readonly', POLICY_DECIDED]);
const PERCENT = /^dotnet-gc-hard-limit-percent:([1-9][0-9]?|100)$/;
const mib = bytes => `${Math.round(bytes / MIB)} MiB`;

const isKnown = policy => ['runtime-view-accepted', 'cgroup-view-readonly', POLICY_DECIDED].includes(policy) || PERCENT.test(policy ?? '');
const isDecided = ({ decided, by }) => typeof decided === 'string' && typeof by === 'string' && decided !== '' && by !== '';

/** `ok({ policy, decided, by })` or `fail(reason)` naming the legal values. A decided policy says when and by whom. */
export function validatePolicy(file) {
  const entry = file ?? {};
  if (entry.policy === null) return ok({ policy: null, decided: entry.decided ?? null, by: entry.by ?? null });
  if (!isKnown(entry.policy)) return fail(`${JSON.stringify(entry.policy)} is not a memory policy: the policy is null (undecided) or one of ${POLICY_NAMES.join(', ')} (n from 1 to 100)`);
  if (!isDecided(entry)) return fail('a decided memory policy needs `decided` (a date) and `by`');
  return ok({ policy: entry.policy, decided: entry.decided, by: entry.by, ...(Array.isArray(entry.evidence) ? { evidence: entry.evidence } : {}) });
}

/** `ok(policy file)` read from `file`, or `fail`: a file that is absent or malformed is a failure, never "undecided". */
export function readPolicy(file) {
  try {
    return validatePolicy(JSON.parse(readFileSync(file, 'utf8')));
  } catch (error) {
    return fail(`${file} cannot be read as the memory policy (${error.code ?? error.message})`);
  }
}

const hardLimit = seen => Number(seen.heapHardLimit);

function behaviourOf(stack, at) {
  const where = mib(at.memoryMaxBytes);
  const over = at.overLimit[stack];
  const out = at.churn[stack] === 'PASS' ? [] : [`${stack} at ${where}: the allocation-churn program with bounded live data did not pass (${at.churn[stack]}): it meets the kernel's OOM kill under the runtime's own view`];
  if (over.reason !== 'memory limit' || !(over.oomKill >= 1 || over.runtimeAbort)) out.push(`${stack} at ${where}: the over-limit program ended "${over.reason}" with oom_kill ${over.oomKill}, not memory limit (a kernel oom_kill or a runtime's own abort, change 5.1)`);
  else if (!over.eventsKept || !(over.peakBytes > 0)) out.push(`${stack} at ${where}: the over-limit program's memory.peak and memory.events were not kept`);
  return out;
}

const behaviour = at => ['csharp', 'node'].flatMap(stack => behaviourOf(stack, at));

function percentRule(at) {
  const limit = hardLimit(at.csharp);
  return limit > 0 && limit <= at.memoryMaxBytes ? [] : [`csharp at ${mib(at.memoryMaxBytes)}: no GC hard limit inside prog/memory.max (heapHardLimit "${at.csharp.heapHardLimit}")`];
}

function cgroupRule(at) {
  const where = mib(at.memoryMaxBytes);
  const out = [];
  if (!at.csharp.cgroupVisible || !at.node.cgroupVisible) out.push(`no cgroup is visible in the box at ${where}`);
  if (at.csharp.totalAvailableBytes !== at.memoryMaxBytes) out.push(`csharp at ${where}: TotalAvailableMemoryBytes is ${mib(at.csharp.totalAvailableBytes)}, not prog/memory.max`);
  if (at.node.constrainedBytes !== at.memoryMaxBytes) out.push(`node at ${where}: constrainedMemory is ${mib(at.node.constrainedBytes)}, not prog/memory.max`);
  return out;
}

const ownOnly = seen => seen.cgroupSelf === '0::/' && seen.cgroupChildren === 0;

/** The decided policy: both runtimes believe `prog/memory.max` itself (.NET's hard limit is the limit), in a cgroup view that is only the program's own. */
function ownRule(at) {
  const where = mib(at.memoryMaxBytes);
  const out = cgroupRule(at);
  if (!ownOnly(at.csharp) || !ownOnly(at.node)) out.push(`the cgroup visible in the box at ${where} is not only its own (/proc/self/cgroup must be 0::/ and no child cgroup may show)`);
  return out;
}

const RULES = Object.freeze({ 'cgroup-view-readonly': cgroupRule, [POLICY_DECIDED]: ownRule });
const ruleFor = policy => (PERCENT.test(policy) ? percentRule : RULES[policy] ?? (() => []));

function problemsAt(policy, at) {
  const asked = at.hostMemoryMax === at.memoryMaxBytes ? [] : [`prog/memory.max is ${at.hostMemoryMax} at ${mib(at.memoryMaxBytes)}: the scope does not carry the limit asked for`];
  return [...asked, ...ruleFor(policy)(at), ...behaviour(at)];
}

/**
 * `{ ok, halt, problems }`: `halt` is `'memory-policy-undecided'` for a null policy (never ok), `'memory-view-mismatch'` when the policy is not met at some limit, else null.
 * `observations.limits[]`: `{ memoryMaxBytes, hostMemoryMax, csharp, node, churn, overLimit }` as the probe records them.
 */
export function assertMemoryView({ policy, observations }) {
  if (policy === null) return { ok: false, halt: 'memory-policy-undecided', problems: [] };
  const problems = observations.limits.flatMap(at => problemsAt(policy, at));
  return { ok: problems.length === 0, halt: problems.length === 0 ? null : 'memory-view-mismatch', problems };
}

/** The probe's exit code for a verdict: 0 only when it is ok. */
export const exitCodeOf = verdict => (verdict.ok ? 0 : 3);
