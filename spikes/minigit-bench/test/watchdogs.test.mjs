import assert from 'node:assert/strict';
import { test } from 'node:test';
import { watchdogDecision } from '../lib/watchdogs.mjs';

const T0 = 1_000_000;
const base = { now: T0 + 60_000, startedAt: T0, lastStreamAt: T0 + 59_000, lastOtelAt: null, lastBrokerAt: null, cpuTicks: 10, prevCpuTicks: 10, liveUsd: null, brokerUsd: null, meterUsd: null, capUsd: 0.2, wallMs: 300_000, stallMs: 120_000 };
const decide = over => watchdogDecision({ ...base, ...over });

test('Watchdogs_kill_for_wall_cost_and_stall_and_only_then', () => {
  const rows = [
    ['a healthy session', {}, { kill: false }],
    ['wall reached', { now: T0 + 300_000, lastStreamAt: T0 + 299_000 }, { kill: true, reason: 'wall' }],
    ['one ms before the wall', { now: T0 + 299_999, lastStreamAt: T0 + 299_000 }, { kill: false }],
    ['the broker at the cap (settled + reserved)', { brokerUsd: 0.2 }, { kill: true, reason: 'cost' }],
    ['the broker under the cap', { brokerUsd: 0.19 }, { kill: false }],
    ['the broker over the cap while O is silent', { brokerUsd: 0.25, liveUsd: null }, { kill: true, reason: 'cost' }],
    ['O over 1.1 x the cap with no broker figure', { liveUsd: 0.221 }, { kill: true, reason: 'cost' }],
    ['O between the cap and 1.1 x it', { liveUsd: 0.21 }, { kill: false }],
    ['the meter over 1.1 x the cap when broker and O are silent', { meterUsd: 0.23 }, { kill: true, reason: 'cost' }],
    ['the meter under it', { meterUsd: 0.2 }, { kill: false }],
    ['no stream, no OTel, no broker, no CPU growth for stallMs', { lastStreamAt: T0 + 60_000 - 120_000 + 0, now: T0 + 120_000 + 1, startedAt: T0 }, { kill: true, reason: 'stall' }],
    ['a stall reset by a broker line alone', { lastStreamAt: T0, now: T0 + 130_000, lastBrokerAt: T0 + 100_000 }, { kill: false }],
    ['a stall reset by an OTel record alone', { lastStreamAt: T0, now: T0 + 130_000, lastOtelAt: T0 + 100_000 }, { kill: false }],
    ['a stall reset by CPU growth alone', { lastStreamAt: T0, now: T0 + 130_000, cpuTicks: 11, prevCpuTicks: 10 }, { kill: false }],
    ['no CPU growth is not growth', { lastStreamAt: T0, now: T0 + 130_000, cpuTicks: 10, prevCpuTicks: 10 }, { kill: true, reason: 'stall' }],
    ['a session that never spoke stalls from its start', { lastStreamAt: null, now: T0 + 130_000 }, { kill: true, reason: 'stall' }],
    ['wall beats cost beats stall', { now: T0 + 400_000, lastStreamAt: T0, brokerUsd: 9 }, { kill: true, reason: 'wall' }],
    ['cost beats stall', { now: T0 + 130_000, lastStreamAt: T0, brokerUsd: 9 }, { kill: true, reason: 'cost' }],
  ];
  for (const [name, over, expected] of rows) {
    const d = decide(over);
    assert.equal(d.kill, expected.kill, name);
    if (expected.kill) assert.equal(d.reason, expected.reason, name);
  }
});

test('A_decision_carries_the_figures_it_rests_on_for_the_watchdog_log', () => {
  const d = decide({ brokerUsd: 0.25 });
  assert.equal(d.kill, true);
  assert.match(d.detail, /broker 0\.25/);
  assert.equal(typeof decide({}).detail, 'string');
});
