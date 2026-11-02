// E1.S8b brief B: the facts the probe reads from INSIDE the box (the real prefix, the delegated scope, the v2 box). One script prints `name<TAB>value` lines; each probe line of the
// plan is judged on its own from them, so the probe can exit 3 naming the first line that failed. A fact the box did not print is a failed line, never a pass.
import { ALLOW_LIST, unlistedKeys } from '../exec-layer/env.mjs';

const BASH_OWN = 'PWD|OLDPWD|SHLVL|_';

/** The script the box runs. `sentinel` is a file under the host's `$HOME` that only the host can read. Every value is printed by `p`; nothing is interpolated but the sentinel path. */
export function factsScript(sentinel) {
  return [
    '#!/bin/bash',
    "p() { printf '%s\\t%s\\n' \"$1\" \"$2\"; }",
    'p ns "$(readlink /proc/self/ns/net)"',
    'p cgroup "$(head -1 /proc/self/cgroup)"',
    `if cat '${sentinel}' >/dev/null 2>&1; then p sentinel readable; else p sentinel absent; fi`,
    'if [ -e /mnt/c ]; then p mnt-c present; else p mnt-c absent; fi',
    'cmd.exe /c ver >/dev/null 2>&1; p cmd-exe $?',
    'systemd-run --user true >/dev/null 2>&1; p user-bus $?',
    'bash -c true 2>stderr.txt; p stderr-bytes "$(wc -c < stderr.txt)"',
    'p lang "${LANG-}"; p lc-all "${LC_ALL-}"',
    "if locale -a | grep -qx 'de_DE.utf8'; then p locale-de yes; else p locale-de no; fi",
    'p invariant "$(env | grep -i INVARIANT | cut -d= -f1 | head -1 | grep . || echo none)"',
    'if [ -d /sys/fs/cgroup ]; then p cgroup-visible yes; else p cgroup-visible no; fi',
    `p env "$(env -0 | tr '\\0' '\\n' | cut -d= -f1 | grep -vxE '${BASH_OWN}' | sort | paste -sd,)"`,
    '',
  ].join('\n');
}

/** `{ name: value }` from the script's output; a line without a tab is dropped. */
export function parseFacts(text) {
  const facts = {};
  for (const line of text.split('\n')) {
    const at = line.indexOf('\t');
    if (at > 0) facts[line.slice(0, at)] = line.slice(at + 1);
  }
  return facts;
}

const line = (id, passed, extra = {}) => ({ id, ok: passed, ...extra });
const said = value => value !== undefined;

/** Every line the box can answer, judged: `[{ id, ok, ... }]` in the plan's order. */
export function judgeFacts(facts) {
  const envKeys = said(facts.env) ? facts.env.split(',').filter(Boolean).sort() : null;
  const sameKeys = envKeys !== null && JSON.stringify(envKeys) === JSON.stringify([...ALLOW_LIST].sort());
  return [
    line('home-sentinel', facts.sentinel === 'absent', { seen: facts.sentinel ?? null }),
    line('mnt-c', facts['mnt-c'] === 'absent', { seen: facts['mnt-c'] ?? null }),
    line('cmd-exe', said(facts['cmd-exe']) && facts['cmd-exe'] !== '0', { exit: facts['cmd-exe'] ?? null }),
    line('user-bus', said(facts['user-bus']) && facts['user-bus'] !== '0', { exit: facts['user-bus'] ?? null }),
    line('bash-c-true-stderr', facts['stderr-bytes'] === '0', { bytes: facts['stderr-bytes'] ?? null }),
    line('culture-env', facts.lang === 'de_DE.UTF-8' && facts['lc-all'] === 'de_DE.UTF-8', { LANG: facts.lang ?? null, LC_ALL: facts['lc-all'] ?? null }),
    line('locale-a', facts['locale-de'] === 'yes'),
    line('invariant-switch', facts.invariant === 'none', { seen: facts.invariant ?? null }),
    line('env-keys', sameKeys, { envKeys: envKeys ?? [] }),
  ];
}

function keysIn(value, found = []) {
  if (Array.isArray(value)) value.forEach(item => keysIn(item, found));
  else if (value !== null && typeof value === 'object') {
    for (const [key, inner] of Object.entries(value)) {
      if (key === 'envKeys' && Array.isArray(inner)) found.push(...unlistedKeys(Object.fromEntries(inner.map(name => [name, 1]))));
      else keysIn(inner, found);
    }
  }
  return found;
}

/** What is wrong with a probe record's environment keys: every `envKeys` list, at any depth, may hold allow-listed keys only (consult C6c, D2). */
export function recordProblems(record) {
  return [...new Set(keysIn(record))].map(key => `the record holds the environment key ${key}, which is not on the allow-list`);
}
