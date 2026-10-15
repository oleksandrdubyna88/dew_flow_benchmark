// Pure: the conventions' Claude adapter rule, as the resolver layout applies it. A consumer's .claude/hooks/* are byte copies of
// the mount's settings/hooks/*, and its .claude/settings.json `hooks` key is the mount's settings/settings.json `hooks` key
// (tools/adapter-check.mjs checks exactly this). The measured run additionally never lets a hook write the real ~/.claude.
export const USER_GUARD_HOOK = 'ensure-user-guard.mjs';

/** The hook scripts a mount publishes, relative to settings/hooks (recursive; test files are not part of the adapter). */
export function hookFilesOf(relPaths) {
  return relPaths.map(p => p.replace(/\\/g, '/')).filter(p => p.endsWith('.mjs') && !p.endsWith('.test.mjs')).sort();
}

/** The consumer's settings with the reference's `hooks` key; every other key (permissions ...) stays the consumer's own. */
export function syncHooksKey(existing, reference) {
  return { ...(existing ?? {}), hooks: structuredClone(reference?.hooks ?? {}) };
}

const runsUserGuard = hook => [hook?.command, ...(hook?.args ?? [])].join(' ').includes(USER_GUARD_HOOK);

/** Settings without any hook that runs the user-guard installer; an entry left with no hook is dropped. Reports whether it removed one. */
export function withoutUserGuard(settings) {
  let removed = false;
  const hooks = Object.fromEntries(Object.entries(settings?.hooks ?? {}).map(([event, entries]) => [event, entries.flatMap(entry => {
    const kept = (entry.hooks ?? []).filter(h => !runsUserGuard(h));
    if (kept.length !== (entry.hooks ?? []).length) removed = true;
    return kept.length === 0 && (entry.hooks ?? []).length > 0 ? [] : [{ ...entry, hooks: kept }];
  })]));
  return { settings: { ...settings, hooks }, removed };
}
