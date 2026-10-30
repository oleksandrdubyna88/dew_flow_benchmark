// The bare-session smoke as a function (E2.S2 step 7): preflight, the token into the broker only, a layout, one launch, then the host-side
// checks that need the real token (so the sanitised fixture never has to hold it) written to checks.json. The CLI (smoke-session.mjs) wraps it.
import { existsSync, readFileSync, readdirSync, realpathSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { contaminationHits } from './contamination.mjs';
import { EGRESS_HOSTS } from './egress-hosts.mjs';
import { cellProcesses, endCell, realProcApi } from './janitor.mjs';
import { findOnPath } from './find-on-path.mjs';
import { loadStacks } from './host-checks.mjs';
import { preflight } from './preflight.mjs';
import { loadPrices } from './prices.mjs';
import { fail, ok } from './result.mjs';
import { dummyToken, sessionLayout, writableBinds } from './sandbox.mjs';
import { readSecretFile } from './secrets.mjs';
import { launchSession } from './session-launch.mjs';
import { scanSessionDir } from './session-scan.mjs';
import { retireStale } from './session-state.mjs';
import { tokenHits } from './token-scan.mjs';
import { tokenTxtFact } from './token-txt.mjs';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { TOKEN_FILE_PARTS } from '../capture/capture-env.mjs';

export const SPIKE = fileURLToPath(new URL('..', import.meta.url));
// The prompt asks for tool and subagent work only. An in-model probe of the credential (read the CLI's environ, write it to a file) was tried and
// RETIRED: the models refuse it even framed as a test (smoke 20261030T002834Z: Opus 5.5, then the fallback Opus 4.8), and the refusal took the
// subagent with it. It must not be reworded to get round a refusal; B-S3 with its positive control covers the CLI's environ from the host.
export const SMOKE_PROMPT = 'Launch one subagent with the Agent tool (`model: "sonnet"`, subagent_type general-purpose) told to run `ls -A` in the current directory and reply with what it printed. '
  + 'When it has returned, run `pwd > where.txt` with Bash yourself and reply DONE.';
export const SMOKE_CAP_USD = 0.2;
const SMOKE_WALL_MS = 5 * 60 * 1000;
const SMOKE_STALL_MS = 2 * 60 * 1000;
const readJson = p => JSON.parse(readFileSync(p, 'utf8'));

/** The stream's tool_use blocks as the contamination scan's `calls`: `[{ agent, tool, input }]`. */
export function callsOf(events) {
  return events.filter(e => e.type === 'assistant').flatMap(e => (e.message?.content ?? [])
    .filter(b => b.type === 'tool_use').map(b => ({ agent: e.parent_tool_use_id ? 'child' : 'main', tool: b.name, input: b.input ?? {} })));
}

/** The host-side facts that need a secret to compute, written so the sanitised fixture can hold them without it. */
export function checksOf({ layout, plan, result }) {
  const events = existsSync(layout.files.stream) ? readFileSync(layout.files.stream, 'utf8').split('\n').filter(Boolean).flatMap(l => { try { return [JSON.parse(l)]; } catch { return []; } }) : [];
  const ctx = { rid: layout.rid, treeRoot: layout.work, home: os.homedir(), harnessPaths: [layout.harness, layout.net], partnerPaths: [], arm: 'bare', ownSecrets: [plan.token, plan.dummy] };
  const scan = scanSessionDir(layout.dir, { real: plan.token, dummy: plan.dummy, binds: writableBinds(layout) });
  const state = readJson(layout.files.state);
  return {
    tokenTxt: tokenTxtFact(layout.work, plan.dummy),
    realTokenScan: { scanned: scan.scanned, hits: scan.hits, tooLarge: scan.tooLarge, links: scan.links, insideLinks: scan.insideLinks },
    positiveControl: result.ok ? { samples: result.value.meta.samplers.samples, dummyInEnviron: result.value.meta.samplers.dummyInEnviron } : null,
    survivors: cellProcesses(plan.cellId, realProcApi).pids.length,
    statePhase: state.phase,
    socketsLeft: Object.values(layout.sockets).filter(existsSync).length,
    contamination: contaminationHits({ calls: callsOf(events), ctx }),
    tokenHits: tokenHits(scan.scanned > 0 ? [{ source: 'stream.ndjson', text: existsSync(layout.files.stream) ? readFileSync(layout.files.stream, 'latin1') : '' }].map(t => ({ ...t, text: t.text.replaceAll(plan.dummy, '[dummy]') })) : [], [plan.token]),
    outcome: result.ok ? result.value.meta.outcome.label : result.reason,
  };
}

/** Everything the smoke needs from the host, as values: `{ ok, value: { token, claudeBinary, prices, stacks } } | fail`. */
export function smokeInputs({ home = os.homedir(), env = process.env, claude = null } = {}) {
  const token = readSecretFile({ home, parts: TOKEN_FILE_PARTS, label: 'token' });
  if (!token.ok) return token;
  const onPath = claude ?? findOnPath('claude', env.PATH);
  if (!onPath) return fail('the claude executable is not on PATH');
  const stacks = loadStacks(readJson(path.join(SPIKE, 'inputs', 'stacks.json')));
  if (!stacks.ok) return stacks;
  const prices = loadPrices(readJson(path.join(SPIKE, 'inputs', 'prices-2026-10-29.json')));
  if (!prices.ok) return prices;
  return ok({ token: token.value, claudeBinary: realpathSync(onPath), prices: prices.value, stacks: stacks.value, tsDir: path.join(home, ...stacks.value.typescriptToolchainDir) });
}

/** Retire stale sessions, preflight, claim the layout, launch, write checks.json -> `ok({ layout, plan, result })` or `fail(reason, { exit })`. */
export async function runSmoke({ out, inputs, home = os.homedir(), signal, deps = {}, over = {}, say = () => {}, preflightFn = preflight }) {
  const root = path.dirname(out);
  const retired = await retireStale({ root, now: Date.now(), endCell: a => endCell({ ...a, killTree, procApi: realProcApi }) });
  say(`stale check over ${root}: ${retired.filter(r => r.verdict === 'stale').length} retired, ${retired.filter(r => r.verdict === 'live').length} live`);
  const nodeDir = path.dirname(path.dirname(process.execPath));
  const pre = await preflightFn({ home, nodeDir, tsDir: inputs.tsDir, claudeBinary: inputs.claudeBinary, stacks: inputs.stacks, install: true });
  if (!pre.ok) return fail(`${pre.reason}: ${pre.problems.join('; ')}`, { exit: 3 });
  const made = sessionLayout(root, path.basename(out));
  if (!made.ok) return fail(made.reason, { exit: 3 });
  const layout = made.value;
  for (const f of ['a.txt', 'b.txt']) writeFileSync(path.join(layout.work, f), `${f}\n`);
  const plan = {
    layout, arm: 'bare', cellId: `smoke-${Math.random().toString(36).slice(2, 10)}`, prompt: SMOKE_PROMPT, capUsd: SMOKE_CAP_USD, wallMs: SMOKE_WALL_MS, stallMs: SMOKE_STALL_MS,
    claudeBinary: inputs.claudeBinary, cliVersion: inputs.stacks.pins.claude, nodeDir, tsDir: inputs.tsDir, toolchains: [inputs.tsDir], token: inputs.token, dummy: dummyToken(Math.random().toString(36).slice(2, 14)),
    prices: inputs.prices, allow: EGRESS_HOSTS.bare, operatorHome: home,
    harnessSources: { boxInit: path.join(SPIKE, 'harness', 'box-init.mjs'), audit: path.join(SPIKE, '..', 'policy-ab-quick', 'hooks', 'audit.mjs') }, ...over,
  };
  say(`launching ${layout.rid} (cell ${plan.cellId}); this is the one paid session`);
  const result = await launchSession(plan, deps, { signal });
  if (!existsSync(layout.dir)) return fail(result.reason ?? 'the session dir is gone', { exit: result.exit ?? 5 });
  writeFileSync(path.join(layout.dir, 'checks.json'), `${JSON.stringify(checksOf({ layout, plan, result }), null, 2)}\n`);
  return ok({ layout, plan, result });
}

/** Whether a directory has any entry (for the CLI's out-dir check). */
export const isNonEmptyDir = p => existsSync(p) && readdirSync(p).length > 0;
