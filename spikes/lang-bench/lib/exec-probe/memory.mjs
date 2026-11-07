// E1.S8b brief B: the memory view, ASSERTED (T-L43; coai risk 19ae7208; parent D51). The box mounts no `/sys/fs/cgroup`, and .NET finds its memory limit through the cgroup mount, so the
// two stacks may believe different limits under one `MemoryMax`. The probe records what each runtime believes at its two probe limits (256 and 512 mebibytes) and checks it against the host's `prog/memory.max`
// by the policy D15 records. OP-K — which policy — is the team lead's to decide on that record: `inputs/specs.d/memory-policy.json` is `{ policy, decided, by }`, `policy` null while
// undecided. A probe that only printed a mismatch and exited 0 would let the stacks meet different bounds, so: undecided is exit 3 `memory-policy-undecided`, a mismatch is exit 3
// `memory-view-mismatch`, and exit 0 only when a policy holds. The judgement is pure: observations in, `{ ok, halt, problems }` out.
import { readFileSync } from 'node:fs';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';
import { MEMORY_POLICY } from '../exec-layer/policy.mjs';

const MIB = 1024 * 1024;
/** The limits the probe observes, in MiB (T-L43): a record of the pinned policy holds exactly these two. */
export const LIMITS_MIB = Object.freeze([256, 512]);
/** The decision of D55 (2026-11-02): each program sees its OWN cgroup read-only, .NET gets `DOTNET_GCHeapHardLimit` = ⌊0.9·L⌋ and Node `--max-old-space-size` = ⌊0.9·L / 1 MiB⌋. The layer pins it (`exec-layer/policy.mjs`). */
export const POLICY_DECIDED = MEMORY_POLICY;
/** OP-K's first decision (2026-11-02, 100 %), superseded by D55: still a legal name, so its evidence record (`exec-probe-2026-11-02.json`) stays readable and judgeable; nothing pins it. */
export const POLICY_SUPERSEDED = 'cgroup-view-own+gchl-limit';
export const POLICY_NAMES = Object.freeze(['runtime-view-accepted', 'dotnet-gc-hard-limit-percent:<n>', 'cgroup-view-readonly', POLICY_SUPERSEDED, POLICY_DECIDED]);
const PERCENT = /^dotnet-gc-hard-limit-percent:([1-9][0-9]?|100)$/;
const mib = bytes => `${Math.round(bytes / MIB)} MiB`;

const isKnown = policy => ['runtime-view-accepted', 'cgroup-view-readonly', POLICY_SUPERSEDED, POLICY_DECIDED].includes(policy) || PERCENT.test(policy ?? '');
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

/** The workloads T-L43 asserts under D55 (every one a PASS), and the one it only records (T21: the mixed workload fails in Node under every setting). */
const ASSERTED_MODES = Object.freeze(['medium-buffers', 'medium-objects', 'objects-70']);
export const RECORDED_MODES = Object.freeze(['mixed']);
const STACKS = Object.freeze(['csharp', 'node']);

/**
 * Whether T-L43 asserts that `mode` passes in `stack` at `limitMiB`. Node `medium-buffers` at 256 MiB is run and recorded, not asserted: typed arrays live outside V8's heap, so the heap flag does not
 * see them and the kernel's kill does (T21; `followUp5`). The `mixed` workload is never asserted. `churn` is asserted everywhere.
 */
export const workloadAsserted = (stack, mode, limitMiB) => mode === 'churn' || (ASSERTED_MODES.includes(mode) && !(stack === 'node' && mode === 'medium-buffers' && limitMiB === 256));

/** `peak / L` to three places, or null for an unknown peak (never zero). */
export const peakRatio = (peakBytes, memoryMaxBytes) => (Number.isFinite(peakBytes) && peakBytes >= 0 ? Number((peakBytes / memoryMaxBytes).toFixed(3)) : null);

/** J2: a peak within 10 points of half the limit — |peak/L − 0.5| ≤ 0.1, by integers: 5·|2·peak − L| ≤ L — is a finding to report before CP2, never tuned around. */
export const isRatioFinding = (peakBytes, memoryMaxBytes) => Number.isFinite(peakBytes) && 5 * Math.abs(2 * peakBytes - memoryMaxBytes) <= memoryMaxBytes;

const OVER_FIXTURES = Object.freeze([['overlimit', 'overLimit'], ['overlimit-objects', 'overLimitObjects']]);

/** One row per over-limit fixture, stack and limit: the status the runner gave, the cause, `peakBytes`, `peak/L` and whether the ratio is a J2 finding. Recorded for every fixture. */
export function overLimitRows(observations) {
  return observations.limits.filter(at => !at.problem).flatMap(at => OVER_FIXTURES.flatMap(([fixture, key]) => STACKS.filter(stack => at[key]?.[stack]).map(stack => {
    const seen = at[key][stack];
    return { stack, fixture, memoryMaxBytes: at.memoryMaxBytes, reason: seen.reason, oomKill: seen.oomKill ?? null, runtimeAbort: seen.runtimeAbort ?? null, peakBytes: seen.peakBytes ?? null, ratio: peakRatio(seen.peakBytes, at.memoryMaxBytes), finding: isRatioFinding(seen.peakBytes, at.memoryMaxBytes) };
  })));
}

/** One row per workload (the churn program included), stack and limit: the status, the cause, the peak and `peak/L`; `asserted` is false for the two cells that are only recorded. */
export function workloadRows(observations) {
  return observations.limits.filter(at => !at.problem).flatMap(at => STACKS.flatMap(stack => ['churn', ...ASSERTED_MODES, ...RECORDED_MODES].map(mode => {
    const seen = mode === 'churn' ? { status: at.churn?.[stack] } : at.workloads?.[stack]?.[mode] ?? { status: 'NOT RUN' };
    const limitMiB = at.memoryMaxBytes / MIB;
    return { stack, mode, memoryMaxBytes: at.memoryMaxBytes, status: seen.status, reason: seen.reason ?? null, oomKill: seen.oomKill ?? null, runtimeAbort: seen.runtimeAbort ?? null, peakBytes: seen.peakBytes ?? null, ratio: peakRatio(seen.peakBytes, at.memoryMaxBytes), asserted: workloadAsserted(stack, mode, limitMiB) };
  })));
}

function workloadProblems(stack, at) {
  const where = mib(at.memoryMaxBytes);
  return ASSERTED_MODES.filter(mode => workloadAsserted(stack, mode, at.memoryMaxBytes / MIB)).flatMap(mode => {
    const seen = at.workloads?.[stack]?.[mode];
    if (seen === undefined) return [`${stack} at ${where}: the ${mode} workload was not run`];
    return seen.status === 'PASS' ? [] : [`${stack} at ${where}: the ${mode} workload did not pass (${seen.status}${seen.reason ? `, ${seen.reason}` : ''}): T-L43 asserts it under D55`];
  });
}

function overProblems(stack, at, [fixture, key]) {
  const where = mib(at.memoryMaxBytes);
  const over = at[key]?.[stack];
  if (over === undefined) return [`${stack} at ${where}: the ${fixture} fixture was not run`];
  if (over.reason !== 'memory limit' || !(over.oomKill >= 1 || over.runtimeAbort)) return [`${stack} at ${where}: the ${fixture} program ended "${over.reason}" with oom_kill ${over.oomKill}, not memory limit (a kernel oom_kill or a runtime's own abort, change 5.1)`];
  return over.eventsKept && over.peakBytes > 0 ? [] : [`${stack} at ${where}: the ${fixture} program's memory.peak and memory.events were not kept`];
}

function behaviourOf(stack, at) {
  const where = mib(at.memoryMaxBytes);
  const churn = at.churn[stack] === 'PASS' ? [] : [`${stack} at ${where}: the allocation-churn program with bounded live data did not pass (${at.churn[stack]}): it meets the kernel's OOM kill under the runtime's own view`];
  return [...churn, ...OVER_FIXTURES.flatMap(fixture => overProblems(stack, at, fixture)), ...workloadProblems(stack, at)];
}

const behaviour = at => STACKS.flatMap(stack => behaviourOf(stack, at));

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

/** The first decision (100 %, superseded): both runtimes believe `prog/memory.max` itself, in a cgroup view that is only the program's own. */
function ownRule(at) {
  const where = mib(at.memoryMaxBytes);
  const out = cgroupRule(at);
  if (!ownOnly(at.csharp) || !ownOnly(at.node)) out.push(`the cgroup visible in the box at ${where} is not only its own (/proc/self/cgroup must be 0::/ and no child cgroup may show)`);
  return out;
}

/** ⌊0.9·L⌋ by integer arithmetic. */
export const ninetyOf = limit => Number((BigInt(limit) * 9n) / 10n);

function dotnetNinety(at) {
  const want = ninetyOf(at.memoryMaxBytes);
  return at.csharp.totalAvailableBytes === want ? [] : [`csharp at ${mib(at.memoryMaxBytes)}: TotalAvailableMemoryBytes is ${mib(at.csharp.totalAvailableBytes)}, not 90 % of prog/memory.max (${want} bytes)`];
}

function nodeNinety(at) {
  const [low, high] = [ninetyOf(at.memoryMaxBytes), at.memoryMaxBytes];
  const heap = at.node.heapSizeLimitBytes;
  const out = at.node.constrainedBytes === at.memoryMaxBytes ? [] : [`node at ${mib(at.memoryMaxBytes)}: constrainedMemory is ${mib(at.node.constrainedBytes)}, not prog/memory.max`];
  return heap >= low && heap < high ? out : [...out, `node at ${mib(at.memoryMaxBytes)}: V8 heap_size_limit is ${heap} bytes, not in [${low}, ${high}) (the old-space flag is 90 % of prog/memory.max)`];
}

/**
 * The decided policy (D55): the cgroup is visible and only the program's own; .NET's `TotalAvailableMemoryBytes` = ⌊0.9·L⌋ (the hard limit is 90 %); Node's `constrainedMemory()` = L and V8's
 * `heap_size_limit` in [⌊0.9·L⌋, L) — the old-space flag is 90 % and V8 adds its young space, but the limit leaves room under the cgroup.
 */
function ninetyRule(at) {
  const where = mib(at.memoryMaxBytes);
  const out = [];
  if (!at.csharp.cgroupVisible || !at.node.cgroupVisible) out.push(`no cgroup is visible in the box at ${where}`);
  if (!ownOnly(at.csharp) || !ownOnly(at.node)) out.push(`the cgroup visible in the box at ${where} is not only its own (/proc/self/cgroup must be 0::/ and no child cgroup may show)`);
  return [...out, ...dotnetNinety(at), ...nodeNinety(at)];
}

const RULES = Object.freeze({ 'cgroup-view-readonly': cgroupRule, [POLICY_SUPERSEDED]: ownRule, [POLICY_DECIDED]: ninetyRule });
const ruleFor = policy => (PERCENT.test(policy) ? percentRule : RULES[policy] ?? (() => []));

const NEEDED = Object.freeze({ csharp: ['totalAvailableBytes', 'cgroupVisible'], node: ['constrainedBytes', 'heapSizeLimitBytes', 'cgroupVisible'] });
const held = (object, key) => object !== undefined && object !== null && object[key] !== undefined && object[key] !== null;

/**
 * What an observation at one limit lacks, as NAMED problems (V7, fail closed): a missing or partial observation is never a thrown error and never a pass. A stack's belief, its churn result, its workloads and
 * both over-limit fixtures must all be there; the rules run only on a whole observation.
 */
function notObserved(at) {
  const where = mib(at.memoryMaxBytes);
  return STACKS.flatMap(stack => {
    const view = NEEDED[stack].every(key => held(at[stack], key)) ? [] : [`memory view not observed for ${stack} at ${where}`];
    const churn = held(at.churn, stack) ? [] : [`churn not observed for ${stack} at ${where}`];
    const fixtures = OVER_FIXTURES.filter(([, key]) => !held(at[key], stack)).map(([fixture]) => `${fixture} not observed for ${stack} at ${where}`);
    const modes = held(at.workloads, stack) ? ASSERTED_MODES.filter(mode => !held(at.workloads[stack], mode)).map(mode => `${mode} not observed for ${stack} at ${where}`) : [`workloads not observed for ${stack} at ${where}`];
    return [...view, ...churn, ...fixtures, ...modes];
  });
}

function problemsAt(policy, at) {
  const missing = notObserved(at);
  if (missing.length > 0) return missing;
  const asked = at.hostMemoryMax === at.memoryMaxBytes ? [] : [`prog/memory.max is ${at.hostMemoryMax} at ${mib(at.memoryMaxBytes)}: the scope does not carry the limit asked for`];
  return [...asked, ...ruleFor(policy)(at), ...behaviour(at)];
}

/**
 * `{ ok, halt, problems }`: `halt` is `'memory-policy-undecided'` for a null policy (never ok), `'memory-view-mismatch'` when the policy is not met at some limit, else null.
 * `observations.limits[]`: `{ memoryMaxBytes, hostMemoryMax, csharp, node, churn, overLimit }` as the probe records them.
 */
export function assertMemoryView({ policy, observations, limits = null }) {
  if (policy === null) return { ok: false, halt: 'memory-policy-undecided', problems: [] };
  const problems = [...notObservedLimits(observations, limits), ...observations.limits.flatMap(at => problemsAt(policy, at))];
  return { ok: problems.length === 0, halt: problems.length === 0 ? null : 'memory-view-mismatch', problems };
}

/** N3 and C2: with `limits` (in MiB) given, each of them must be observed exactly once and no other limit may be observed: an empty, one-limit or extra-limit record names the difference and does not pass. */
function notObservedLimits(observations, limits) {
  if (limits === null) return [];
  const missing = limits.filter(limit => observations.limits.filter(at => at.memoryMaxBytes === limit * MIB).length !== 1).map(limit => `memory view not observed at ${limit} MiB (the record must hold exactly one observation of each of ${limits.join(' and ')} MiB)`);
  const extra = observations.limits.filter(at => !limits.some(limit => at.memoryMaxBytes === limit * MIB)).map(at => `memory view observed at ${mib(at.memoryMaxBytes)}, which is not one of ${limits.join(' and ')} MiB: a record holds the probe's limits and no other`);
  return [...missing, ...extra];
}

/**
 * The PROBE's verdict (Q2): it asserts the pinned policy only. A policy other than `POLICY_DECIDED` is exit 3 `memory-view-mismatch` naming the pinned one — the layer would refuse to run a data run under
 * it, so a probe that passed under it would certify a box nobody pinned. `assertMemoryView` still judges an OLD record by its own policy (`verdictFor`).
 */
export function probeVerdict({ policy, observations }) {
  if (policy === null || policy === POLICY_DECIDED) return assertMemoryView({ policy, observations, limits: LIMITS_MIB });
  return { ok: false, halt: 'memory-view-mismatch', problems: [`the memory policy is "${policy}", not the pinned "${POLICY_DECIDED}" (D55): the probe asserts the pinned policy only`] };
}

/** The probe's exit code for a verdict: 0 only when it is ok. */
export const exitCodeOf = verdict => (verdict.ok ? 0 : 3);
