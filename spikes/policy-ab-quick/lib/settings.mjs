// Pure: merge the pilot's hook entries into a project's .claude/settings.json object.
const hookCmd = file => ({ type: 'command', command: 'node', args: [`\${CLAUDE_PROJECT_DIR}/.claude/hooks/${file}`] });
const entry = (matcher, file) => ({ ...(matcher ? { matcher } : {}), hooks: [hookCmd(file)] });

export function overlayEntries(arm) {
  return {
    PreToolUse: [
      entry('*', 'audit.mjs'),
      ...(arm === 'B' ? [entry('Agent|Task', 'subagent-model.mjs')] : []),
    ],
    InstructionsLoaded: [entry('', 'audit.mjs')],
  };
}

/** Keeps every existing key and hook; refuses a project that already wires one of ours. */
export function applyOverlaySettings(settings, arm) {
  const text = JSON.stringify(settings ?? {});
  if (/audit.mjs|subagent-model/.test(text)) throw new Error('project settings already wire a policy hook');
  const hooks = { ...(settings?.hooks ?? {}) };
  for (const [event, entries] of Object.entries(overlayEntries(arm))) hooks[event] = [...(hooks[event] ?? []), ...entries];
  return { ...(settings ?? {}), hooks };
}

export const PINNED_EFFORT = Object.freeze({
  effortLevel: 'high',
  modelSettings: { sonnet: { effortLevel: 'high' }, opus: { effortLevel: 'high' }, fable: { effortLevel: 'high' } },
});
