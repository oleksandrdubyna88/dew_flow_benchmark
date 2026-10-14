import { npmInvocation } from './proc.mjs';

const DEFAULT_COMPILE = Object.freeze({ exe: 'node', args: ['node_modules/typescript/bin/tsc', '-p', './'] });

/** Pure: inputs/commands.json `commands.compile` ({exe,args}), a string, or an array; default tsc via node. */
export function resolveCompile(json) {
  const raw = json?.commands?.compile ?? json?.compile?.command ?? json?.compile ?? DEFAULT_COMPILE;
  if (Array.isArray(raw)) return toInvocation(raw.map(String));
  if (typeof raw === 'string') return toInvocation(raw.trim().split(/\s+/));
  return toInvocation([raw.exe, ...(raw.args ?? [])]);
}

export function toInvocation(tokens) {
  const [head, ...rest] = tokens;
  if (/^npm(\.cmd)?$/i.test(head)) return npmInvocation(rest);
  return { exe: /^node(\.exe)?$/i.test(head) ? process.execPath : head, args: rest };
}
