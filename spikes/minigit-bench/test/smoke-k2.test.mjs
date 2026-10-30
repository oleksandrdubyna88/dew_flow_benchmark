import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SMOKE_RULES } from '../lib/smoke-verdicts.mjs';

const T = 1_800_000_000_000;
const iso = ms => new Date(ms).toISOString();
const line = (event, seq, ms) => ({ t: iso(ms), event, seq });
const payload = (receivedMs, ...names) => ({ line: { receivedAt: receivedMs }, d: { ok: true, value: names.map(name => ({ name })) } });
const rule = ({ stopped = 'wall', stoppedAt = iso(T), brokerLines = [], decoded = [] }) => SMOKE_RULES['B-K2']({ smoke: { meta: { stopped, stoppedAt }, brokerLines }, decoded });

test('B_K2_holds_only_when_an_O_record_for_a_request_in_flight_at_the_stop_arrived_after_it', () => {
  const inFlight = [line('forwarding', 1, T - 500)];
  const viaO = rule({ brokerLines: inFlight, decoded: [payload(T + 900, 'api_request', 'tool_result')] });
  assert.equal(viaO.verdict, 'holds');
  assert.match(viaO.observed, /1 O api_request record\(s\) after the SIGTERM/);
});

test('The_broker_settling_after_the_stop_is_a_separate_fact_and_never_the_grace_claim_because_the_broker_is_on_the_host_and_reads_the_upstream_whatever_the_CLI_does', () => {
  const inFlight = [line('forwarding', 1, T - 500)];
  const viaBroker = rule({ brokerLines: [...inFlight, line('settled', 1, T + 800)] });
  assert.equal(viaBroker.verdict, 'refuted', 'a settlement after the stop is not an O record: the CLI\'s grace is not shown');
  assert.match(viaBroker.observed, /0 O api_request record\(s\) after the SIGTERM/);
  assert.match(viaBroker.observed, /separate fact.*1 broker settled line\(s\) after the stop/);
  const both = rule({ brokerLines: [...inFlight, line('settled', 1, T + 800)], decoded: [payload(T + 900, 'api_request')] });
  assert.equal(both.verdict, 'holds');
  assert.match(both.observed, /1 O api_request record\(s\) after the SIGTERM.*separate fact.*1 broker settled line\(s\) after the stop/);
});

test('B_K2_is_refuted_when_a_request_was_in_flight_at_the_stop_and_no_O_record_arrived_after_it', () => {
  const r = rule({ brokerLines: [line('forwarding', 1, T - 500), line('settled', 7, T - 10)], decoded: [payload(T - 5, 'api_request')] });
  assert.equal(r.verdict, 'refuted');
  assert.match(r.observed, /1 request\(s\) in flight at the stop/);
});

test('B_K2_is_not_exercised_when_nothing_stopped_it_when_no_stop_time_was_recorded_or_when_nothing_was_in_flight', () => {
  assert.equal(rule({ stopped: null, stoppedAt: null }).verdict, 'not exercised');
  const noTime = rule({ stoppedAt: null, brokerLines: [line('forwarding', 1, T - 500)] });
  assert.equal(noTime.verdict, 'not exercised');
  assert.match(noTime.observed, /no stop time/);
  const idle = rule({ brokerLines: [line('forwarding', 1, T - 900), line('settled', 1, T - 500)], decoded: [payload(T + 300, 'api_request')] });
  assert.equal(idle.verdict, 'not exercised', 'a record after the stop proves nothing when no request was in flight at it');
  assert.match(idle.observed, /no request was in flight/);
});
