// The rehearsal of a quick task (`explore.mjs rehearse --task NN`, $0): v1's stub CLI changes nothing, so the workspace is pre-filled with a HELLO program of the stack and kind (`spikes/lang-explore/rehearsal/`), and the
// held-out set is one fixture pair the hello program passes. The pipeline is the real one: the box, the broker, the build box, the execution layer and the runner. The criteria are therefore not "accepted" (a hello program
// does not solve the task) but: both sessions completed and were scored, the fixture case PASSED in both stacks (the pass path), at least one visible sample FAILED with a verdict in both (the reject path), no case is
// missing, every record leaf is known or says why.
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { casesTreeHash } from './heldout-quick.mjs';
import { fillWorkspace } from './workspace.mjs';

const HELLO = path.join(fileURLToPath(new URL('..', import.meta.url)), 'rehearsal');

const DAEMON_IN = 'scenario 2\n# The rehearsal fixture: the hello daemon answers ping with pong.\ndefault within 2s\nstart ready port within 10s\nconnect c1 tcp\nsend c1 "ping\\n"\nrecv c1 until "\\n"\nclose c1\nsignal TERM\nwait-exit within 5s\n';
const DAEMON_OUT = 'transcript 2\nready\nrecv c1 "pong\\n"\nexit 0\n';
const WEB_IN = 'scenario 2\n# The rehearsal fixture: the hello server answers GET /hello.\ndefault within 2s\nstart ready port within 10s\nconnect c http\n\nrequest c GET "/hello"\nrecv c\n\nsignal TERM\nwait-exit within 5s\n';
const WEB_OUT = 'transcript 2\nready\nreply c 200\n  body "hello"\nexit 0\n';

/** The fixture pair of a kind, with the program's failure mode (`HELLO_MODE`) written as an `env` line of the scenario; the expected transcript is always the CORRECT one, so a failing mode must not pass. */
export function fixtureOf(kind, mode = '') {
  const base = kind === 'daemon' ? { input: DAEMON_IN, output: DAEMON_OUT } : { input: WEB_IN, output: WEB_OUT };
  return mode === '' ? base : { ...base, input: base.input.replace('start ready', `env HELLO_MODE "${mode}"\nstart ready`) };
}

/** The fixture cases: h001 passes; the others fail on purpose (a wrong reply, a program killed by SIGTERM, a program that exits 3 on SIGTERM, a program that holds more memory than the registered bound). */
export const FIXTURE_CASES = Object.freeze([['h001', ''], ['h002', 'wrong'], ['h003', 'sigdie'], ['h004', 'exit3'], ['h005', 'hog']]);

/** Write the rehearsal's held-out set into `dir/cases` and return a held-out object shaped like `loadHeldoutQuick`'s. */
export function rehearsalHeldout({ dir, task }) {
  const casesDir = path.join(dir, 'cases');
  mkdirSync(casesDir, { recursive: true });
  for (const [id, mode] of FIXTURE_CASES) {
    const f = fixtureOf(task.kind, mode);
    writeFileSync(path.join(casesDir, `${id}.in`), f.input);
    writeFileSync(path.join(casesDir, `${id}.out`), f.output);
  }
  writeFileSync(path.join(casesDir, 'cases.txt'), `# the rehearsal fixtures\n${FIXTURE_CASES.map(([id]) => `${id} bytes R1`).join('\n')}\n`);
  return Object.freeze({ task: task.id, dir, casesDir, caseIds: FIXTURE_CASES.map(([id]) => id), traps: [], treeSha256: casesTreeHash(casesDir), rehearsal: true });
}

/** The hello overlay of a stack and kind: `{ relPath: { bytes, mode } }`. */
export function helloOverlay({ stack, kind }) {
  const root = path.join(HELLO, `${stack}-${kind}`);
  const walk = (rel = '') => readdirSync(path.join(root, rel), { withFileTypes: true }).flatMap(e => (e.isDirectory() ? walk(path.join(rel, e.name)) : [path.join(rel, e.name)]));
  return Object.fromEntries(walk().map(rel => [rel, { bytes: readFileSync(path.join(root, rel)), mode: 0o644 }]));
}

/** The rehearsal's `ctx.fill`: the stack's workspace with the hello program on top. */
export const quickReferenceFill = ({ world, task }) => ({ item, layout }) => {
  const stack = item.session.stack;
  const files = { ...world.filesFor(stack), ...helloOverlay({ stack, kind: task.kind }) };
  return fillWorkspace({ files, work: layout.work });
};

const check = (id, holds, detail) => ({ id, ok: holds, detail });
const casesOf = r => r?.explore?.cases?.value ?? [];

const verdictIn = (cases, id) => cases.find(c => c.id === id)?.verdict;
const notPassed = (cases, id) => verdictIn(cases, id) !== undefined && verdictIn(cases, id) !== 'pass';
const perSession = (entries, records) => entries.map(e => ({ e, cases: casesOf(records[e.rid]) }));
const heldLine = ({ e, cases }) => `${e.stack}: ${cases.filter(c => c.set === 'heldout').map(c => `${c.id}=${c.verdict}`).join(' ')}`;
const bothScored = per => per.length === 2 && per.every(({ e, cases }) => e.status === 'scored' && cases.length > 0);

/** The quick rehearsal's own criteria from the records (never typed): `[{ id, ok, detail }]`. */
export function quickScoreChecks({ entries, records }) {
  const per = perSession(entries, records);
  const everywhere = test => per.length === 2 && per.every(({ cases }) => test(cases));
  const verdicts = per.map(heldLine).join('; ');
  return [
    check('both sessions scored, every case has a verdict', bothScored(per), verdicts),
    check('the correct hello program passed (the pass path)', everywhere(cs => verdictIn(cs, 'h001') === 'pass'), verdicts),
    check('a visible sample was judged and failed (the reject path)', everywhere(cs => cs.some(c => c.set === 'visible' && c.verdict !== 'pass')), verdicts),
    check('a wrong reply, a program killed by SIGTERM and a program that exits 3 all failed, in both stacks', everywhere(cs => ['h002', 'h003', 'h004'].every(id => notPassed(cs, id))), verdicts),
    check('a program that holds more than the registered memory bound failed in both stacks (the bound is applied: Node gets the memory limit verdict from the cgroup; .NET stops at its 90 % heap limit, a runtime abort, D55)', everywhere(cs => notPassed(cs, 'h005')), verdicts),
  ];
}
