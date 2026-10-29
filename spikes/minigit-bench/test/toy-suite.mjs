// A SYNTHETIC stand-in for the held-out suite, written from JS strings into a test's own directory: a tiny program ("toy") with four
// commands, a runner that speaks the real runner protocol, two references and two mutants with a registry. Nothing here relates to
// minigit's real cases; it exists so the tooling that knows only the suite's SHAPE can be tested without the real suite.
import { chmodSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const TOY_PROGRAM = [
  '#!/usr/bin/python3 -I',
  'import os, signal, sys, time',
  'a = sys.argv[1:]',
  "if a[:1] == ['ping']: print('pong')",
  "elif a[:1] == ['sum']: print(int(a[1]) + int(a[2]))",
  "elif a[:1] == ['shout']: print(a[1].upper())",
  "elif a[:1] == ['sleep']: time.sleep(60)",
  "elif a[:1] == ['crash']: os.kill(os.getpid(), signal.SIGSEGV)",
  "else: print('unknown'); sys.exit(1)",
  '',
].join('\n');

/** Reference B: the same behaviour written another way. */
const TOY_PROGRAM_B = TOY_PROGRAM.replace("print(a[1].upper())", "print(''.join(chr(ord(c) - 32) if 'a' <= c <= 'z' else c for c in a[1]))");

export const TOY_CASES = Object.freeze([
  { id: 'H1-01', group: 'H1', scored: true, spec: 'TOY:1', needs: [], argv: ['ping'], want: 'pong' },
  { id: 'H1-02', group: 'H1', scored: true, spec: 'TOY:2', needs: ['H1-01'], argv: ['sum', '2', '3'], want: '5' },
  { id: 'H1-03', group: 'H1', scored: true, spec: 'TOY:3', needs: ['H1-01'], argv: ['sum', '0', '0'], want: '0' },
  { id: 'H1-04', group: 'H1', scored: true, spec: 'TOY:4', needs: ['H1-01', 'H1-02'], argv: ['shout', 'hi'], want: 'HI' },
  { id: 'H2-01', group: 'H2', scored: true, spec: 'TOY:5', needs: ['H1-01'], argv: ['sum', '10', '5'], want: '15' },
]);

const RUNNER = [
  'import argparse, json, os, shutil, subprocess, sys',
  `CASES = json.loads(${JSON.stringify(JSON.stringify(TOY_CASES))})`,
  'ap = argparse.ArgumentParser()',
  "for f in ('phase', 'binary', 'work', 'box', 'timeout', 'out'): ap.add_argument('--' + f)",
  'o = ap.parse_args()',
  'box = json.loads(o.box)',
  "cases = [c for c in CASES if o.phase == 'v2' or c['group'] == 'H1']",
  "exists = os.path.isfile(o.binary) and os.access(o.binary, os.X_OK)",
  'status, out = {}, []',
  'for c in cases:',
  "    rec = {'id': c['id'], 'group': c['group'], 'scored': True, 'spec': c['spec'], 'captured': []}",
  "    blocker = next((n for n in c['needs'] if status.get(n) != 'pass'), None)",
  "    if not exists: rec.update(status='fail', reason='missing')",
  "    elif blocker: rec.update(status='blocked', blockedBy=blocker)",
  '    else:',
  "        shutil.rmtree(os.path.join(o.work, c['id']), ignore_errors=True)",
  "        repo = os.path.join(o.work, c['id'], 'repo'); os.makedirs(repo)",
  '        try:',
  "            p = subprocess.run(box + [o.binary] + c['argv'], cwd=repo, stdin=subprocess.DEVNULL, capture_output=True, timeout=float(o.timeout))",
  "            rec['captured'].append({'argv': c['argv'], 'exit': p.returncode, 'stdout': p.stdout.decode(), 'stderr': p.stderr.decode()})",
  "            if p.returncode not in (0, 1): rec.update(status='fail', reason='crash')",
  "            elif p.stdout.decode().strip() == c['want']: rec['status'] = 'pass'",
  "            else: rec.update(status='fail', reason='mismatch')",
  '        except subprocess.TimeoutExpired:',
  "            rec.update(status='fail', reason='timeout')",
  "    status[c['id']] = rec['status']; out.append(rec)",
  "json.dump({'format': 1, 'phase': o.phase, 'python': sys.version.split()[0], 'cases': out, 'latency': {'known': False, 'why': 'toy runner'}}, open(o.out, 'w'))",
  '',
].join('\n');

const mutant = (id, find, replace, expected) => ({ id, clause: `toy clause ${id}`, file: 'refs/a/minigit', find, replace, expected });
const BLOCKED_BY_PING = { v1: ['H1-02', 'H1-03', 'H1-04'], v2: ['H1-02', 'H1-03', 'H1-04', 'H2-01'] };

export const TOY_MUTANTS = Object.freeze([
  mutant('T01', 'print(int(a[1]) + int(a[2]))', 'print(int(a[1]) * int(a[2]))', { v1: { fail: ['H1-02'], blocked: ['H1-04'] }, v2: { fail: ['H1-02', 'H2-01'], blocked: ['H1-04'] } }),
  mutant('T02', "print('pong')", "print('pong!')", {
    v1: { fail: ['H1-01'], blocked: BLOCKED_BY_PING.v1 }, v2: { fail: ['H1-01'], blocked: BLOCKED_BY_PING.v2 } }),
]);

const put = (root, rel, text, mode = 0o644) => {
  const file = path.join(root, rel);
  mkdirSync(path.dirname(file), { recursive: true });
  writeFileSync(file, text, { mode });
  chmodSync(file, mode);
};

/** Writes the toy suite under `root` (the shape of the real one) and returns its registry. `registryEdit` can bend the registry. */
export function writeToySuite(root, { registryEdit = r => r } = {}) {
  put(root, 'runner.py', RUNNER);
  put(root, 'refs/a/minigit', TOY_PROGRAM, 0o755);
  put(root, 'refs/b/minigit', TOY_PROGRAM_B, 0o755);
  for (const m of TOY_MUTANTS) put(root, `mutants/${m.id}.json`, JSON.stringify({ id: m.id, clause: m.clause, file: m.file, find: m.find, replace: m.replace }));
  put(root, 'leak-tokens.txt', 'toy-token-quokka-7421\nanother-toy-token-zebra\n');
  const registry = registryEdit({
    salt: 'toysalt0',
    cases: TOY_CASES.map(({ id, group, scored, spec, needs }) => ({ id, group, scored, spec, needs })),
    mutants: TOY_MUTANTS.map(({ id, clause, expected }) => ({ id, clause, expected })),
  });
  put(root, 'registry.json', JSON.stringify(registry, null, 2));
  return registry;
}
