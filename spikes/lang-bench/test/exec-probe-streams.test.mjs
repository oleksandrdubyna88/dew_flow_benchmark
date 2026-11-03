// D3 (fix batch 9): the streams. `guardStreams` is the FIRST thing the command does and the command reaches it THROUGH a wiring function (`installStopHandlers`, inside `startCommand`), so removing the call turns a test
// red. An EPIPE is always ignored. After the record is written ANY error on stdout or stderr keeps the record's exit code (never an uncaught exception); before the record a non-EPIPE error is the command's failure:
// the stop with exit 2 (the cleanup, then exit 2), and the listener never rethrows. Plus the order of the start sequence: guard, stop handlers, release on exit, and only then the start sweep (D2).
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { EventEmitter } from 'node:events';
import { test } from 'node:test';
import { installStopHandlers, newState, startCommand } from '../lib/exec-probe/wiring.mjs';

const wiring = JSON.stringify(new URL('../lib/exec-probe/wiring.mjs', import.meta.url).href);
const error = code => Object.assign(new Error(`${code}: boom`), { code });
const fakeStream = writes => Object.assign(new EventEmitter(), { write: text => writes.push(text) });
const fakeProc = (writes = []) => Object.assign(new EventEmitter(), { stdout: fakeStream(writes), stderr: fakeStream(writes), exits: [] });
const options = (state, over = {}) => ({ state, say: () => {}, release: () => {}, describeLive: () => [], cleanLive: async () => [], closeScope: async () => {}, ...over });

test('The_stop_handlers_install_the_stream_guard_on_stdout_and_stderr_and_it_is_by_phase', async () => {
  for (const stream of ['stdout', 'stderr']) {
    const proc = fakeProc();
    const state = newState();
    const exits = [];
    installStopHandlers({ proc, exit: code => exits.push(code), ...options(state) });
    assert.equal(proc[stream].listenerCount('error'), 1, `${stream}: the guard is installed by installStopHandlers`);
    assert.doesNotThrow(() => proc[stream].emit('error', error('EPIPE')), `${stream}: an EPIPE is ignored`);
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(exits, [], `${stream}: and does not stop the probe`);
    state.written = 0;
    assert.doesNotThrow(() => proc[stream].emit('error', error('EIO')), `${stream}: after the record any error is ignored`);
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(exits, [], `${stream}: the record's code stays`);
    const before = fakeProc();
    const beforeExits = [];
    const stopped = new Promise(resolve => installStopHandlers({ proc: before, exit: code => { beforeExits.push(code); resolve(); }, ...options(newState()) }));
    assert.doesNotThrow(() => before[stream].emit('error', error('EIO')), `${stream}: before the record the listener does not rethrow`);
    await stopped;
    assert.deepEqual(beforeExits, [2], `${stream}: before the record a non-EPIPE error is the command's failure, exit 2 (after the cleanup)`);
  }
});

/** A real process: the command's own start (`startCommand`), a stream error emitted on stderr, and the exit code. `recorded` is the record's exit code or null (not written yet). */
function streamErrorRun(recorded) {
  const child = spawn(process.execPath, ['--input-type=module', '-e', `
    import { newState, startCommand } from ${wiring};
    const state = newState();
    startCommand({ state, say: text => process.stderr.write(text + '\\n'), release: () => {}, describeLive: () => [], cleanLive: async () => [], closeScope: async () => {}, sweep: () => {} });
    ${recorded === null ? '' : `state.written = ${recorded};`}
    process.stderr.emit('error', Object.assign(new Error('EIO: boom'), { code: 'EIO' }));
    setTimeout(() => process.exit(${recorded ?? 0}), 300); // the command's own end, when nothing stopped it
  `], { stdio: ['ignore', 'pipe', 'pipe'] });
  return new Promise(resolve => {
    let err = '';
    let out = '';
    child.stderr.on('data', chunk => { err += chunk; });
    child.stdout.on('data', chunk => { out += chunk; });
    child.once('close', code => resolve({ code, err, out }));
  });
}

test('A_non_EPIPE_stream_error_after_the_record_keeps_the_records_exit_code_with_no_uncaught_exception', async () => {
  for (const recorded of [0, 3]) {
    const ended = await streamErrorRun(recorded);
    assert.equal(ended.code, recorded, `the exit is the record's code (${recorded}): ${ended.err}`);
    assert.doesNotMatch(ended.err, /Unhandled 'error' event|node:events/, 'no uncaught exception');
  }
});

test('A_non_EPIPE_stream_error_before_the_record_is_exit_2_after_the_cleanup_with_no_rethrow', async () => {
  const ended = await streamErrorRun(null);
  assert.equal(ended.code, 2, ended.err);
  assert.match(ended.err, /exec-probe: stopping — cleaning up 0 open run\(s\)/, 'the stop ran');
  assert.match(ended.out, /^exec-probe: stream error: EIO: boom$/m, 'N3: the error is named on the OTHER stream (stdout) before the stop');
  assert.doesNotMatch(ended.err, /Unhandled 'error' event|node:events/, 'no uncaught exception');
});

test('The_start_sequence_installs_the_guard_the_stop_handlers_and_the_exit_release_before_the_sweep_writes_anything', () => {
  const proc = fakeProc();
  const seen = [];
  const release = () => {};
  startCommand({ proc, ...options(newState(), { release }), sweep: () => seen.push({
    guardOnStdout: proc.stdout.listenerCount('error'),
    guardOnStderr: proc.stderr.listenerCount('error'),
    sigint: proc.listenerCount('SIGINT'),
    sigterm: proc.listenerCount('SIGTERM'),
    releaseOnExit: proc.listeners('exit').includes(release),
  }) });
  assert.deepEqual(seen, [{ guardOnStdout: 1, guardOnStderr: 1, sigint: 1, sigterm: 1, releaseOnExit: true }], 'when the sweep runs everything is installed');
});

// N3 (coai 4, 7): a stream error before the record prints one line, best effort, to the other stream, before the stop with exit 2.
test('A_stream_error_before_the_record_prints_one_line_to_the_other_stream_before_the_stop_and_never_loops', async () => {
  for (const [broken, other] of [['stdout', 'stderr'], ['stderr', 'stdout']]) {
    const events = [];
    const proc = fakeProc();
    proc[other].write = text => events.push(`${other}: ${text.trim()}`);
    const stopped = new Promise(resolve => installStopHandlers({ proc, exit: code => { events.push(`exit ${code}`); resolve(); }, ...options(newState(), { say: text => events.push(`say: ${text}`) }) }));
    proc[broken].emit('error', error('EIO'));
    proc[broken].emit('error', error('EIO')); // a second error while stopping prints nothing more
    await stopped;
    assert.equal(events.filter(event => event.includes('stream error')).length, 1, `${broken}: one line only: ${events.join(' | ')}`);
    assert.equal(events[0], `${other}: exec-probe: stream error: EIO: boom`, `${broken}: it goes to ${other}, first`);
    assert.equal(events.at(-1), 'exit 2');
  }
  const proc = fakeProc();
  proc.stderr.write = () => { throw new Error('the other stream is broken too'); };
  const exits = [];
  const stopped = new Promise(resolve => installStopHandlers({ proc, exit: code => { exits.push(code); resolve(); }, ...options(newState()) }));
  assert.doesNotThrow(() => proc.stdout.emit('error', error('EIO')), 'best effort: a write that fails is ignored');
  await stopped;
  assert.deepEqual(exits, [2]);
});
