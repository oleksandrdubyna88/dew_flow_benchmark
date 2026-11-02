// E1.S8b brief B: the facts the probe reads from INSIDE the box (the real prefix, the delegated scope, the v2 box). One script prints `name<TAB>value` lines; each probe line of the
// plan is judged on its own from them, so the probe can exit 3 naming the first line that failed. A fact the box did not print is a failed line, never a pass.
import { allowFor, unlistedKeys } from '../exec-layer/env.mjs';

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
    'p cg-self "$(head -1 /proc/self/cgroup)"',
    "p cg-dirs \"$(find /sys/fs/cgroup -mindepth 1 -maxdepth 1 -type d 2>/dev/null | paste -sd,)\"",
    'p cg-write "$( (echo 1 > /sys/fs/cgroup/memory.max) 2>&1 | head -1 )"',
    'p cg-sys "$(ls -A /sys | paste -sd,)"',
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

const said = value => value !== undefined;
const sameList = (a, b) => JSON.stringify([...a].sort()) === JSON.stringify([...b].sort());
/** OP-K (decided 2026-11-02): the box shows the program's OWN cgroup, read-only, and nothing else of the host's cgroups or `/sys`. */
const ownView = f => f['cgroup-visible'] === 'yes' && f['cg-self'] === '0::/' && f['cg-dirs'] === '' && /Read-only file system/.test(f['cg-write'] ?? '') && f['cg-sys'] === 'fs';
const envKeysOf = facts => (said(facts.env) ? facts.env.split(',').filter(Boolean) : null);
const failsWith = code => said(code) && code !== '0';

/** The probe's facts lines: `[id, judge(facts) -> { ok, ...detail }]`. A fact the box did not print makes its line fail. */
const RULES = Object.freeze([
  ['home-sentinel', f => ({ ok: f.sentinel === 'absent', seen: f.sentinel ?? null })],
  ['mnt-c', f => ({ ok: f['mnt-c'] === 'absent', seen: f['mnt-c'] ?? null })],
  ['cmd-exe', f => ({ ok: failsWith(f['cmd-exe']), exit: f['cmd-exe'] ?? null })],
  ['user-bus', f => ({ ok: failsWith(f['user-bus']), exit: f['user-bus'] ?? null })],
  ['bash-c-true-stderr', f => ({ ok: f['stderr-bytes'] === '0', bytes: f['stderr-bytes'] ?? null })],
  ['culture-env', f => ({ ok: f.lang === 'de_DE.UTF-8' && f['lc-all'] === 'de_DE.UTF-8', LANG: f.lang ?? null, LC_ALL: f['lc-all'] ?? null })],
  ['locale-a', f => ({ ok: f['locale-de'] === 'yes' })],
  ['invariant-switch', f => ({ ok: f.invariant === 'none', seen: f.invariant ?? null })],
  ['cgroup-view-own', f => ({ ok: ownView(f), self: f['cg-self'] ?? null, dirs: f['cg-dirs'] ?? null, sys: f['cg-sys'] ?? null })],
  ['env-keys', f => ({ ok: envKeysOf(f) !== null && sameList(envKeysOf(f), allowFor(undefined)), envKeys: [...(envKeysOf(f) ?? [])].sort() })],
]);

/** Every line the box can answer, judged: `[{ id, ok, ... }]` in the plan's order. */
export const judgeFacts = facts => RULES.map(([id, judge]) => ({ id, ...judge(facts) }));

const isEnvKeys = (key, value) => key === 'envKeys' && Array.isArray(value);
const unlisted = names => unlistedKeys(Object.fromEntries(names.map(name => [name, 1])));
const isContainer = value => value !== null && typeof value === 'object';

function keysIn(value) {
  if (!isContainer(value)) return [];
  const entries = Array.isArray(value) ? value.map(item => [null, item]) : Object.entries(value);
  return entries.flatMap(([key, inner]) => (isEnvKeys(key, inner) ? unlisted(inner) : keysIn(inner)));
}

/** What is wrong with a probe record's environment keys: every `envKeys` list, at any depth, may hold allow-listed keys only (consult C6c, D2). */
export function recordProblems(record) {
  return [...new Set(keysIn(record))].map(key => `the record holds the environment key ${key}, which is not on the allow-list`);
}
