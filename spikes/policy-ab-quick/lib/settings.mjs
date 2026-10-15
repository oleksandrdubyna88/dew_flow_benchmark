// Pure: merge the pilot's hook entries into a project's .claude/settings.json object.
const hookCmd = file => ({ type: 'command', command: 'node', args: [`\${CLAUDE_PROJECT_DIR}/.claude/hooks/${file}`] });
const entry = (matcher, file) => ({ ...(matcher ? { matcher } : {}), hooks: [hookCmd(file)] });

/** `guardWired`: the project's settings already carry the arm's own guard (resolver layout: the mount's release wires it). */
export function overlayEntries(arm, { guardWired = false } = {}) {
  return {
    PreToolUse: [
      entry('*', 'audit.mjs'),
      ...(arm === 'B' ? [...(guardWired ? [] : [entry('Agent|Task', 'subagent-model.mjs')]), entry('Edit|Write|MultiEdit|NotebookEdit', 'edit-bound.mjs')] : []),
    ],
    InstructionsLoaded: [entry('', 'audit.mjs')],
    ...(arm === 'B' ? { UserPromptSubmit: [entry('', 'edit-bound.mjs')] } : {}),
  };
}

/** Keeps every existing key and hook; refuses a project that already wires one of ours. */
export function applyOverlaySettings(settings, arm, opts = {}) {
  const text = JSON.stringify(settings ?? {});
  const ours = opts.guardWired ? /audit.mjs|edit-bound/ : /audit.mjs|subagent-model|edit-bound/;
  if (ours.test(text)) throw new Error('project settings already wire a policy hook');
  const hooks = { ...(settings?.hooks ?? {}) };
  for (const [event, entries] of Object.entries(overlayEntries(arm, opts))) hooks[event] = [...(hooks[event] ?? []), ...entries];
  return { ...(settings ?? {}), hooks };
}

export const PINNED_EFFORT = Object.freeze({
  effortLevel: 'high',
  modelSettings: { sonnet: { effortLevel: 'high' }, opus: { effortLevel: 'high' }, fable: { effortLevel: 'high' } },
});

const stable = value => JSON.stringify(value);

/** Pure: every hook entry the project already had is still present, in order, at the front of its event (ours go beside, after). */
export function existingHooksPreserved(existing, merged) {
  return Object.entries(existing?.hooks ?? {}).every(([event, entries]) => entries.every((e, i) => stable(merged?.hooks?.[event]?.[i]) === stable(e)));
}
