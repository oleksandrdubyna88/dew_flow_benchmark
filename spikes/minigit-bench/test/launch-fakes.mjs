// The fakes of the launch tests: the plan of a launch, and the edges of `launchSession` (the box, the servers, the janitor) replaced by scripted stand-ins.
import { EventEmitter } from 'node:events';
import path from 'node:path';
import { PassThrough } from 'node:stream';
import { EGRESS_HOSTS } from '../lib/egress-hosts.mjs';
import { dummyToken } from '../lib/sandbox.mjs';
import { fakeToken, prices } from './broker-stub.mjs';
import { nodeDir } from './box-helpers.mjs';
import { spikeDir } from './helpers.mjs';

const AUDIT = path.join(spikeDir, '..', 'policy-ab-quick', 'hooks', 'audit.mjs');
const BOX_INIT = path.join(spikeDir, 'harness', 'box-init.mjs');
export const rand = () => Math.random().toString(36).slice(2, 9);

/** The plan of a launch over `layout`; `over` varies it. The real token is a FAKE here, everywhere. */
export function planFor(layout, over = {}) {
  const real = fakeToken();
  return {
    layout, arm: 'bare', cellId: `cell-${rand()}`, prompt: 'Reply OK', capUsd: 0.2, wallMs: 60_000, stallMs: 30_000, tickMs: 150, sampleMs: 100, settleMs: 200, graceMs: 1500,
    claudeBinary: process.execPath, cliVersion: '2.1.310', nodeDir, tsDir: null, toolchains: [], token: real, dummy: dummyToken(rand()), prices: prices(),
    allow: EGRESS_HOSTS.bare, harnessSources: { boxInit: BOX_INIT, audit: AUDIT }, ...over,
  };
}

export function fakeChild() {
  const c = new EventEmitter();
  Object.assign(c, { pid: 2 ** 22 - 7, stdout: new PassThrough(), stderr: new PassThrough(), stdin: new PassThrough() });
  c.finish = (code = 0, signal = null) => { if (c.done) return; c.done = true; c.stdout.end(); setImmediate(() => c.emit('close', code, signal)); };
  return c;
}
export const INIT = over => ({
  type: 'system', subtype: 'init', cwd: null, session_id: 's', tools: ['Task', 'Bash', 'Edit', 'Glob', 'Grep', 'Read', 'Write'], mcp_servers: [], model: 'claude-sonnet-5-5', permissionMode: 'default',
  apiKeySource: 'none', claude_code_version: '2.1.310', memory_paths: { auto: null }, ...over,
});
export const RESULT = { type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.01 };

/** Fake deps. Each server pushes its name to `order` when closed; `script(child, layout)` plays the box. */
export function fakeDeps(order, layout, { script, throwOn = {}, closeThrows = null, drain = async () => ({ drained: true, inFlight: 0, reservedUsd: 0 }), unsettled = null } = {}) {
  const count = unsettled?.count ?? 0;
  const seal = () => ({ unsettled: unsettled ?? { count: 0, reservedUsd: 0 }, lowerBound: count > 0, summary: { spentUsd: unsettled?.reservedUsd ?? 0, reservedUsd: unsettled?.reservedUsd ?? 0, upstreamRequests: 0, refused: 0, overCap: false, overshootUsd: 0 } });
  const server = (name, extra = {}) => ({ ...extra, close: async () => { order.push(name); if (closeThrows === name) throw new Error(`${name} close failed`); } });
  const child = fakeChild();
  const spawned = [];
  return {
    spawned, child,
    deps: {
      startReceiver: async () => { if (throwOn.receiver) throw new Error('listen EADDRINUSE: receiver'); return server('receiver', { port: 1, misconfigured: 0, undecodable: 0, liveSum: () => { if (throwOn.liveSum) throw new Error('liveSum threw'); return { usd: 0, requests: 0, lastAt: 0 }; } }); },
      brokerServer: async () => server('broker', { drain, seal, summary: () => ({ spentUsd: 0, reservedUsd: 0, upstreamRequests: 0, refused: 0, overCap: false, overshootUsd: 0 }) }),
      egressProxy: async () => { if (throwOn.proxy) throw new Error('listen EADDRINUSE: proxy socket'); return server('proxy'); },
      spawnBox: (argv, env) => { spawned.push({ argv, env }); setImmediate(() => script(child, layout)); return child; },
      endCell: async a => { order.push('endCell'); child.finish(143, null); return { cellId: a.cellId }; },
      endSurvivors: async () => 0, killTree: () => {}, procApi: { pids: () => [], read: () => { throw Object.assign(new Error('x'), { code: 'ENOENT' }); }, signal: () => {} },
      sleep: async () => {},
    },
  };
}
export const goodInit = layout => INIT({ cwd: layout.work, memory_paths: { auto: `${layout.config}/projects/-p/memory/` } });
export const say = (child, ...evs) => evs.forEach(e => child.stdout.write(`${JSON.stringify(e)}\n`));
