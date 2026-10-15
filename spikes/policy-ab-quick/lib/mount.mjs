// Pure: the two arms' conventions mounts are built from ONE base tree (P's pinned conventions) and differ only in the declared
// policy files. Everything else a whole-commit checkout would change (a second loaded rule, user-guard hooks, tools, docs that
// describe the experiment, git history) is kept out by construction, and asserted here.

/** `git ls-tree -r` text -> Map(path -> "mode:sha"). Submodule entries and trees are not files and are skipped. */
export function parseLsTree(text) {
  const map = new Map();
  for (const line of text.split('\n')) {
    const m = /^(\d{6}) blob ([0-9a-f]{40,64})\t(.+)$/.exec(line.trim());
    if (m) map.set(m[3], `${m[1]}:${m[2]}`);
  }
  return map;
}

/** Paths whose entry differs between two maps (added, removed or changed), sorted. */
export function mapDelta(a, b) {
  return [...new Set([...a.keys(), ...b.keys()])].filter(p => a.get(p) !== b.get(p)).sort();
}

const sameSet = (x, y) => x.length === y.length && x.every((p, i) => p === y[i]);

/**
 * The mount of `arm` against the base pin: A must be the base byte for byte, B must differ in exactly the declared policy files.
 * Throws with the unexpected and the missing paths named.
 */
export function assertPolicyDelta(arm, delta, policyFiles) {
  const expected = arm === 'B' ? [...policyFiles].sort() : [];
  if (sameSet([...delta].sort(), expected)) return delta;
  const extra = delta.filter(p => !expected.includes(p));
  const missing = expected.filter(p => !delta.includes(p));
  throw new Error(`arm ${arm}'s mount differs from the base in the wrong files - unexpected: [${extra.join(', ')}] missing: [${missing.join(', ')}]`);
}

/** Two arms' mounts against each other: exactly the declared policy files. */
export function assertArmsDifferOnlyInPolicy(mapA, mapB, policyFiles) {
  const delta = mapDelta(mapA, mapB);
  if (!sameSet(delta, [...policyFiles].sort())) throw new Error(`the arms' mounts differ in [${delta.join(', ')}], declared policy files are [${[...policyFiles].sort().join(', ')}]`);
  return delta;
}

const runsGuard = hook => [hook?.command, ...(hook?.args ?? [])].join(' ').includes('subagent-model.mjs');

/** The hook entries of a settings object that run the subagent-model guard, event by event. */
export function guardEntriesOf(settings) {
  return Object.fromEntries(Object.entries(settings?.hooks ?? {}).flatMap(([event, entries]) => {
    const own = entries.filter(e => (e.hooks ?? []).some(runsGuard)).map(e => ({ ...e, hooks: e.hooks.filter(runsGuard) }));
    return own.length > 0 ? [[event, own]] : [];
  }));
}

/** The base settings plus the arm's guard entries (and nothing else of the arm's release: no user-guard installer). */
export function withGuardEntries(baseSettings, armSettings) {
  const hooks = { ...(baseSettings?.hooks ?? {}) };
  for (const [event, entries] of Object.entries(guardEntriesOf(armSettings))) hooks[event] = [...(hooks[event] ?? []), ...entries];
  return { ...baseSettings, hooks };
}
