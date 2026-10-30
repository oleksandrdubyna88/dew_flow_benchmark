// Launch, watch, stop and collect ONE agent session (E2.S2 D-14, D-20, D-21). Everything the session touches outside its box is created
// here and torn down here, on every exit path, by a LIFO stack of idempotent disposers (disposers.mjs).
//
// startUp:   state.json -> harness files -> receiver (socket) -> broker (socket, the cell's budget) -> egress proxy (socket)
// supervise: bwrap spawned detached with the DUMMY credential in its env (never --setenv); the stream tapped and stamped; the init read
//            back before the first paid turn completes; watchdogs and heartbeat every tick; samplers of the real token every second
// finish:    3 s for the last OTel batch, the janitor, the transcripts, argv/egress/broker/meta/readback written, state 'closed'
// Paid work is written as it is earned and never deleted by the thing that failed: a dir is removed only when it holds nothing paid.
import { spawn } from 'node:child_process';
import { appendFileSync, copyFileSync, existsSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { performance } from 'node:perf_hooks';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { streamMeterUsd, streamView } from './claude-usage.mjs';
import { DRAIN_MS, brokerView, brokerServer } from './broker.mjs';
import { disposerStack } from './disposers.mjs';
import { egressProxy, egressSummary } from './egress.mjs';
import { cellProcesses, cpuTicksOf, endCell, endSurvivors, realProcApi } from './janitor.mjs';
import { BARE_RESERVE_USD } from './budget.mjs';
import { BARE_TOOLS, claudeArgv, forbiddenIn, mcpConfigFor, settingsFor } from './launch.mjs';
import { startReceiver } from './otel-receiver.mjs';
import { priceFor } from './prices.mjs';
import { emptyCounts, sampleOnce } from './proc-sampler.mjs';
import { readBackInit, sessionOutcome } from './readback.mjs';
import { fail, ok } from './result.mjs';
import { agentBoxArgv, agentEnv } from './sandbox.mjs';
import { heartbeat, processStartTime, writeState } from './session-state.mjs';
import { streamTap } from './stream-tap.mjs';
import { mapClock } from './timeline.mjs';
import { copyTranscripts } from './transcripts.mjs';
import { watchdogDecision } from './watchdogs.mjs';

const sleepMs = ms => new Promise(r => setTimeout(r, ms));
const sha256 = text => createHash('sha256').update(text).digest('hex');
const jsonLine = o => `${JSON.stringify(o)}\n`;
const METER_OUTPUT_FLOOR = 1;

const DEFAULT_DEPS = Object.freeze({
  startReceiver, brokerServer, egressProxy, endCell, endSurvivors, killTree, procApi: realProcApi, now: Date.now, sleep: sleepMs,
  spawnBox: (argv, env) => spawn(argv[0], argv.slice(1), { env, stdio: ['pipe', 'pipe', 'pipe'], detached: true }),
});

/** Whether the broker's log holds a `forwarding` line: a request went (or was about to go) to the provider, so it was reserved against the budget whether or not anything came back. */
function brokerForwarded(layout) {
  if (!existsSync(layout.files.brokerLog)) return false;
  return readFileSync(layout.files.brokerLog, 'utf8').split('\n').some(line => /"event"\s*:\s*"forwarding"/.test(line));
}

/** Whether the dir holds anything paid: a stream line, an OTel record, anything in box/out or box/config/projects, or a request the broker forwarded. */
export function holdsPaidWork(layout) {
  const nonEmpty = p => existsSync(p) && (statSync(p).isDirectory() ? readdirSync(p).length > 0 : statSync(p).size > 0);
  return [layout.files.stream, path.join(layout.otel, 'otel.ndjson'), layout.out, path.join(layout.config, 'projects')].some(nonEmpty) || brokerForwarded(layout);
}

// ---- startup --------------------------------------------------------------------------------------------------------------------

function installHarness(plan) {
  const { layout, arm } = plan;
  copyFileSync(plan.harnessSources.boxInit, layout.files.boxInit);
  copyFileSync(plan.harnessSources.audit, layout.files.audit);
  const settings = settingsFor({ arm, layout });
  const forbidden = forbiddenIn(settings);
  if (forbidden.length > 0) return fail(`the settings carry ${forbidden.join(', ')}, which a session must never have`);
  const mcp = mcpConfigFor(arm);
  if (!mcp.ok) return mcp;
  const settingsText = `${JSON.stringify(settings, null, 2)}\n`;
  const mcpText = `${JSON.stringify(mcp.value, null, 2)}\n`;
  writeFileSync(layout.files.settings, settingsText);
  writeFileSync(layout.files.mcp, mcpText);
  writeFileSync(layout.files.gitconfig, '');
  return ok({ settings, settingsSha256: sha256(settingsText), mcpSha256: sha256(mcpText) });
}

/** Start the host-side servers; each is pushed on the stack the moment it exists. A throw here is a startup failure. */
async function startServers(plan, d, stack, rec) {
  const { layout } = plan;
  const append = file => line => appendFileSync(file, jsonLine(line));
  [layout.files.egressLog, layout.files.brokerLog, layout.files.watchdog, layout.files.stream, layout.files.streamStamped, layout.files.stderr, path.join(layout.otel, 'otel-raw.ndjson'), path.join(layout.otel, 'otel.ndjson')].forEach(f => appendFileSync(f, '')); // an empty log is a fact; a missing one is a gap
  rec.receiver = await d.startReceiver({ dir: layout.otel, port: 0, socketPath: layout.sockets.otel });
  stack.push('receiver', () => rec.receiver.close());
  const brokerLog = append(layout.files.brokerLog);
  rec.broker = await d.brokerServer({
    socketPath: layout.sockets.api, token: plan.token, upstream: plan.upstream, budget: { capUsd: plan.capUsd, reserveUsd: plan.reserveUsd }, prices: plan.prices, cliVersion: plan.cliVersion,
    log: line => { rec.lastBrokerAt = d.now(); brokerLog(line); },
  });
  stack.push('broker', () => rec.broker.close());
  rec.proxy = await d.egressProxy({ socketPath: layout.sockets.proxy, allow: plan.allow, log: append(layout.files.egressLog) });
  stack.push('proxy', () => rec.proxy.close());
  stack.push('sockets', () => Object.values(layout.sockets).forEach(s => rmSync(s, { force: true })));
}

// ---- supervision ----------------------------------------------------------------------------------------------------------------

function expectedInit(plan, settings) {
  return { cwd: plan.layout.work, tools: plan.tools ?? BARE_TOOLS, mcpServers: 0, cliVersion: plan.cliVersion, configDir: plan.layout.config, priced: m => m !== undefined && priceFor(plan.prices, m).known, settings };
}

/** The costs the watchdog reads: the broker's SETTLED spend, O's live sum, the stream meter; `null` = silent, never zero. */
function costsNow(plan, rec, events) {
  const b = rec.broker.summary();
  const live = rec.receiver.liveSum();
  const meter = streamMeterUsd(streamView(events), plan.prices, { outputFloor: METER_OUTPUT_FLOOR });
  return { brokerSpentUsd: b.spentUsd, liveUsd: live.requests > 0 ? live.usd : null, meterUsd: meter.usd ?? null };
}

function lastOtelAt(plan) {
  try { return statSync(`${plan.layout.otel}/otel-raw.ndjson`).mtimeMs; } catch { return null; }
}

/** One watchdog tick: decide, log, heartbeat. -> the decision. */
function tick({ plan, d, rec, tap }) {
  const procs = cellProcesses(plan.cellId, d.procApi).pids;
  const cpuTicks = cpuTicksOf(procs, d.procApi);
  const now = d.now();
  const decision = watchdogDecision({
    now, startedAt: rec.startedAtMs, lastStreamAt: rec.lastStreamAt, lastOtelAt: lastOtelAt(plan), lastBrokerAt: rec.lastBrokerAt, cpuTicks, prevCpuTicks: rec.cpuTicks,
    ...costsNow(plan, rec, tap.events), capUsd: plan.capUsd, wallMs: plan.wallMs, stallMs: plan.stallMs,
  });
  rec.cpuTicks = cpuTicks;
  appendFileSync(plan.layout.files.watchdog, jsonLine({ t: now, ...decision, cpuTicks }));
  heartbeat(plan.layout.files.state, now);
  return decision;
}

/** Handle one parsed stream event: the init is read back; the result is kept. */
function onEvent({ plan, rec, stop }, ev) {
  if (ev.type === 'system' && ev.subtype === 'init' && rec.init === null) {
    rec.init = ev;
    rec.readback = readBackInit(ev, expectedInit(plan, rec.settings));
    writeFileSync(plan.layout.files.readback, `${JSON.stringify(rec.readback, null, 2)}\n`);
    if (!rec.readback.ok) stop('readback');
  }
  if (ev.type === 'result') rec.result = ev;
}

/** Spawn the box and resolve with its exit once it has closed (or fail when it could not start). Timers and the abort listener are disposers. */
function supervise({ plan, d, stack, rec, signal }) {
  const { layout } = plan;
  if (!existsSync(plan.claudeBinary)) throw new Error(`the claude binary ${plan.claudeBinary} does not exist`);
  const claude = [plan.claudeBinary, ...claudeArgv({ layout, cap: plan.capUsd, tools: plan.tools })];
  const argv = agentBoxArgv({ layout, arm: plan.arm, toolchains: plan.toolchains, claudeBinary: plan.claudeBinary, nodeDir: plan.nodeDir, operatorHome: plan.operatorHome, command: claude });
  const env = agentEnv({ layout, dummyToken: plan.dummy, cellId: plan.cellId, nodeDir: plan.nodeDir, tsDir: plan.tsDir, extra: plan.envExtra });
  writeFileSync(layout.files.argv, `${JSON.stringify({ bwrap: argv, claude, envKeys: Object.keys(env).sort(), settingsSha256: rec.settingsSha256, mcpSha256: rec.mcpSha256 }, null, 2)}\n`);
  const clock = mapClock({ hrAtSpawn: performance.now(), epochAtSpawn: d.now() });
  return new Promise((resolve, reject) => {
    const child = d.spawnBox(argv, env);
    rec.child = child;
    let closed = false;
    let stopping = null;
    const stop = reason => {
      if (rec.stopped === null) { rec.stopped = reason; rec.stoppedAtMs = d.now(); } // the first stop names the reason and the instant
      stopping ??= d.endCell({ cellId: plan.cellId, bwrapPid: child.pid, graceMs: plan.graceMs, killTree: d.killTree, procApi: d.procApi });
      return stopping;
    };
    rec.stop = stop;
    const tap = streamTap({
      clock: () => clock.toEpoch(performance.now()), writeRaw: c => appendFileSync(layout.files.stream, c), writeStamped: s => appendFileSync(layout.files.streamStamped, s),
      onEvent: (ev, t) => { rec.lastStreamAt = t; onEvent({ plan, rec, stop }, ev); },
    });
    const timers = [
      setInterval(() => { try { const dec = tick({ plan, d, rec, tap }); if (dec.kill) stop(dec.reason); } catch (e) { rec.errors.push(e.message); } }, plan.tickMs),
      setInterval(() => { rec.samplers = sampleOnce(rec.samplers, d.procApi, { real: plan.token, dummy: plan.dummy }); }, plan.sampleMs),
    ];
    stack.push('timers', () => timers.forEach(clearInterval));
    stack.push('box', () => (closed ? undefined : stop(rec.stopped ?? 'disposed')));
    const onAbort = () => stop('cancelled');
    if (signal) { if (signal.aborted) onAbort(); else signal.addEventListener('abort', onAbort, { once: true }); }
    child.stdout.on('data', c => tap.push(c));
    child.stderr.on('data', c => appendFileSync(layout.files.stderr, c));
    child.stdin.on('error', () => {});
    child.stdin.end(plan.prompt);
    child.on('error', e => reject(new Error(`could not start the box: ${e.message}`)));
    child.on('close', (code, sig) => { closed = true; timers.forEach(clearInterval); tap.flush(); resolve({ code, signal: sig }); });
  });
}

// ---- collection -----------------------------------------------------------------------------------------------------------------

const readLines = file => (existsSync(file) ? readFileSync(file, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : []);

/** After the exit: the last OTel batch, the janitor, the transcripts, the summaries. -> the pieces `meta.json` is built from. */
async function collect({ plan, d, rec, exit }) {
  const { layout } = plan;
  await d.sleep(plan.settleMs);
  const survivorsEnded = await d.endSurvivors(plan.cellId, { killTree: d.killTree, procApi: d.procApi });
  // the upstream bills a request whatever the client does: wait until the broker has read every one to its end (or the ceiling) BEFORE the
  // ledger is read, or an abandoned request is written down as 0 spent
  await rec.broker.drain(plan.drainMs);
  // past the ceiling a request may still be in flight: the broker is SEALED here, before its ledger is read. B is then the settled spend plus the unsettled
  // reservations (`lowerBound: true`: the API may have billed more), the summary is the ledger's last line, and a late settlement writes nothing after broker.json
  const sealed = rec.broker.seal();
  const unsettledAtClose = sealed.unsettled;
  const copied = copyTranscripts(layout.config, layout.transcripts);
  const egressLines = readLines(layout.files.egressLog);
  const brokerLines = readLines(layout.files.brokerLog);
  writeFileSync(layout.files.egress, `${JSON.stringify(egressSummary(egressLines), null, 2)}\n`);
  const broker = { ...brokerView(brokerLines), ...sealed.summary, unsettledAtClose, lowerBound: sealed.lowerBound };
  writeFileSync(layout.files.broker, `${JSON.stringify(broker, null, 2)}\n`);
  return { survivorsEnded, unsettledAtClose, transcripts: copied.ok ? copied.value : [], transcriptsProblem: copied.ok ? null : copied.reason, broker, outcome: sessionOutcome(rec.result, exit) };
}

/** A read of a dependency at the very end: its failure is recorded in `errors`, never allowed to lose the meta of a paid session. */
function attempt(rec, what, fn, fallback) {
  try { return fn(); } catch (e) { rec.errors.push(`${what}: ${e.message}`); return fallback; }
}

function metaOf({ plan, d, rec, exit, collected, cleanup }) {
  const live = attempt(rec, 'receiver.liveSum', () => rec.receiver.liveSum(), { usd: null, requests: null });
  return {
    rid: plan.layout.rid, cell: plan.cellId, startedAt: new Date(rec.startedAtMs).toISOString(), endedAt: new Date(d.now()).toISOString(), wallMs: d.now() - rec.startedAtMs,
    exit, stopped: rec.stopped, stoppedAt: rec.stoppedAtMs === null ? null : new Date(rec.stoppedAtMs).toISOString(), outcome: rec.stopped === 'readback' ? { ok: false, label: 'void: harness', detail: 'the init read-back mismatched; see readback.json' } : collected.outcome,
    cliVersion: rec.init?.claude_code_version ?? null, sessionId: rec.init?.session_id ?? rec.result?.session_id ?? null, envelopeCostUsd: rec.result?.total_cost_usd ?? null,
    samplers: rec.samplers, janitorEnded: collected.survivorsEnded, transcripts: collected.transcripts.length, transcriptsProblem: collected.transcriptsProblem,
    broker: { spentUsd: collected.broker.spentUsd, lowerBound: collected.broker.lowerBound, upstreamRequests: collected.broker.upstreamRequests, refused: collected.broker.refused, refusedByReason: collected.broker.refusedByReason, unpricedModels: collected.broker.unpricedModels ?? [], overCap: collected.broker.overCap, overshootUsd: collected.broker.overshootUsd },
    unsettledAtClose: collected.unsettledAtClose,
    receiver: { usd: live.usd, requests: live.requests, misconfigured: rec.receiver.misconfigured, undecodable: rec.receiver.undecodable },
    errors: [...rec.errors], cleanup,
  };
}

// ---- the launch -----------------------------------------------------------------------------------------------------------------

const newRecord = (plan, d) => ({
  state: { phase: 'starting', hostPid: process.pid, hostStartTime: processStartTime(process.pid), cell: plan.cellId, startedAt: new Date(d.now()).toISOString(), heartbeatAt: new Date(d.now()).toISOString() },
  finalPhase: 'closed', startedAtMs: d.now(), lastStreamAt: null, lastBrokerAt: null, cpuTicks: 0, init: null, result: null, readback: null, stopped: null, stoppedAtMs: null,
  samplers: emptyCounts(), errors: [], receiver: null, broker: null, proxy: null, child: null, settings: null, settingsSha256: null, mcpSha256: null,
});

const DEFAULTS = Object.freeze({ reserveUsd: BARE_RESERVE_USD, tickMs: 5000, sampleMs: 1000, settleMs: 3000, graceMs: 5000, drainMs: DRAIN_MS, arm: 'bare', envExtra: {} });

/**
 * Launch one session. `plan` names everything (layout, cell, prompt, caps, binaries, the real `token` and the `dummy`); `deps` are the
 * injectable edges (default: the real ones). -> `ok({ meta, exit })` once the launch ran to its end (the outcome may still be void: the
 * envelope decides), or `fail(reason)` when it did not (a dir that holds nothing paid is removed then).
 */
export async function launchSession(given, deps = {}, { signal } = {}) {
  const plan = { ...DEFAULTS, ...given };
  const d = { ...DEFAULT_DEPS, ...deps };
  const stack = disposerStack();
  const rec = newRecord(plan, d);
  let failure = null;
  let exit = null;
  try {
    writeState(plan.layout.files.state, rec.state);
    stack.push('state', () => writeState(plan.layout.files.state, { ...rec.state, phase: rec.finalPhase, endedAt: new Date(d.now()).toISOString() }));
    const installed = installHarness(plan);
    if (!installed.ok) throw new Error(installed.reason);
    Object.assign(rec, { settings: installed.value.settings, settingsSha256: installed.value.settingsSha256, mcpSha256: installed.value.mcpSha256 });
    await startServers(plan, d, stack, rec);
    writeState(plan.layout.files.state, { ...rec.state, phase: 'running' });
    exit = await supervise({ plan, d, stack, rec, signal });
  } catch (e) {
    failure = e.message;
    rec.finalPhase = 'failed';
    rec.errors.push(e.message);
  }
  return finish({ plan, d, rec, stack, exit, failure });
}

async function finish({ plan, d, rec, stack, exit, failure }) {
  let collected = null;
  if (exit !== null && rec.broker !== null) {
    try { collected = await collect({ plan, d, rec, exit }); } catch (e) { rec.errors.push(`collect: ${e.message}`); rec.finalPhase = 'failed'; }
  }
  const cleanup = await stack.run();
  if (failure !== null || collected === null) {
    if (!holdsPaidWork(plan.layout)) rmSync(plan.layout.dir, { recursive: true, force: true });
    return fail(failure ?? (rec.errors.join('; ') || 'the launch did not complete'), { exit: 5, cleanup });
  }
  const meta = metaOf({ plan, d, rec, exit, collected, cleanup });
  writeFileSync(plan.layout.files.meta, `${JSON.stringify(meta, null, 2)}\n`);
  return ok({ meta, exit });
}
