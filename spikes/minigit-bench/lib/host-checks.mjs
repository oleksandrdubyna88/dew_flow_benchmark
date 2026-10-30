// The host's facts, checked before any paid session (E2.S2 step 5, D-9, D-10). Pure: probe OUTPUTS in, verdicts out; running the probes
// inside a box is lib/preflight.mjs. Each pin is read from inputs/stacks.json, never typed here.
import path from 'node:path';
import { fail, ok } from './result.mjs';

const PIN_KEYS = Object.freeze(['claude', 'node', 'dotnetSdk', 'typescript']);
const PROBE_KEYS = Object.freeze(['claude', 'node', 'dotnetSdks', 'tsc']);

/** Validate `inputs/stacks.json` -> `{ ok, value } | { ok: false, reason }`. A probe is exe + argv, never a shell string. */
export function loadStacks(json) {
  if (!json || typeof json !== 'object') return fail('stacks.json is a JSON object');
  const missing = PIN_KEYS.filter(k => typeof json.pins?.[k] !== 'string' || json.pins[k] === '');
  if (missing.length > 0) return fail(`stacks.json pins lack ${missing.join(', ')}`);
  const bad = PROBE_KEYS.filter(k => !Array.isArray(json.probes?.[k]) || json.probes[k].length === 0 || !json.probes[k].every(a => typeof a === 'string'));
  if (bad.length > 0) return fail(`stacks.json probes for ${bad.join(', ')} must each be an argv array (exe + argv, never a shell string)`);
  if (!Array.isArray(json.typescriptToolchainDir)) return fail('stacks.json typescriptToolchainDir is the path parts under the home');
  return ok(Object.freeze({ ...json }));
}

const FIRST_VERSION = /\d+\.\d+\.\d+/;
const versionOf = text => FIRST_VERSION.exec(text ?? '')?.[0] ?? null;

function check(tool, expected, observed) {
  if (observed === null) return { tool, expected, observed: null, why: 'could not read a version from the probe output' };
  return observed === expected ? null : { tool, expected, observed, why: `${tool} is ${observed}, the pin is ${expected}` };
}

function dotnetMismatch(text, pin) {
  const lines = (text ?? '').split('\n').filter(l => l.trim() !== '');
  const versions = lines.map(versionOf);
  if (lines.length !== 1) return { tool: 'dotnetSdk', expected: `exactly one SDK, ${pin}`, observed: `${lines.length} SDK line(s)`, why: lines.length === 0 ? 'could not read an SDK from the probe output' : `the box must hold exactly one SDK (${lines.length} listed)` };
  return check('dotnetSdk', pin, versions[0]);
}

/**
 * The probes' outputs (`{ claude, node, dotnetSdks, tsc }`, each stdout text) against the pins ->
 * `{ ok, facts, mismatches: [{ tool, expected, observed, why }] }`. `facts` is what was read, for the report.
 */
export function toolchainFacts(probes, pins) {
  const facts = { claude: versionOf(probes.claude), node: versionOf(probes.node), dotnetSdks: (probes.dotnetSdks ?? '').split('\n').filter(l => l.trim() !== '').map(versionOf), typescript: versionOf(probes.tsc) };
  const mismatches = [
    check('claude', pins.claude, facts.claude), check('node', pins.node, facts.node), dotnetMismatch(probes.dotnetSdks, pins.dotnetSdk), check('typescript', pins.typescript, facts.typescript),
  ].filter(Boolean);
  return { ok: mismatches.length === 0, facts, mismatches };
}

/** `null` when the TypeScript toolchain is installed, else the steps that install it read-only (npm, then chmod; no sudo). */
export function toolchainInstallPlan({ dir, exists, version }) {
  if (exists(path.join(dir, 'node_modules', '.bin', 'tsc'))) return null;
  return { steps: [{ exe: 'npm', argv: ['install', '--prefix', dir, `typescript@${version}`] }, { exe: 'chmod', argv: ['-R', 'a-w', dir] }] };
}

/** bwrap present and user namespaces usable: `{ ok, version, problems }`. */
export function bwrapFacts({ bwrap, unshare }) {
  const problems = [
    ...(bwrap === null || bwrap.code !== 0 ? ['bwrap is not installed (or does not run): the box cannot be built'] : []),
    ...(unshare === null || unshare.code !== 0 ? ['user namespaces are not usable here (`unshare -Ur true` failed): bwrap cannot isolate'] : []),
  ];
  return { ok: problems.length === 0, version: versionOf(bwrap?.stdout), problems };
}
