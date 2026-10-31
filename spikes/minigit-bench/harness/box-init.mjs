// The agent box's supervisor (E2.S2 D-4). Runs INSIDE the box, node builtins only, one file (copied into <session>/harness/ at launch).
// Usage: node box-init.mjs [<port>:<unix socket path> ...] -- <exe> [argv ...]
// Starts one 127.0.0.1 forwarder per spec (the box has its own network namespace, so its loopback reaches nothing but this), then runs
// the command with inherited stdio and exits with its code; a child ended by a signal exits 128 + that signal's number.
// A SIGTERM aimed at this process is ignored on purpose: the stop (janitor.endCell) signals every inner process by its marker, the
// command included, and a second, forwarded SIGTERM would be a double delivery. Forwarders die with this process.
// THIS DEPENDS ON endCell: ignoring the signal is only safe because the stop always signals the command itself. A caller that signalled
// this process alone (not through janitor.endCell) would leave the command running with nothing to end it but bwrap's death.
import { spawn } from 'node:child_process';
import { constants } from 'node:os';
import net from 'node:net';

const sep = process.argv.indexOf('--');
const specs = sep < 0 ? [] : process.argv.slice(2, sep);
const [exe, ...args] = sep < 0 ? [] : process.argv.slice(sep + 1);

function fatal(message, code = 1) {
  process.stderr.write(`box-init: ${message}\n`);
  process.exit(code);
}

async function listenForwarder(spec) {
  const i = spec.indexOf(':');
  const port = Number(spec.slice(0, i));
  const target = spec.slice(i + 1);
  if (!Number.isInteger(port) || port < 1 || target === '') fatal(`bad forwarder spec '${spec}': want <port>:<unix socket path>`, 2);
  const server = net.createServer(client => {
    const upstream = net.connect(target);
    client.pipe(upstream);
    upstream.pipe(client);
    client.on('error', () => upstream.destroy());
    upstream.on('error', () => client.destroy());
  });
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(port, '127.0.0.1', resolve); })
    .catch(e => fatal(`could not listen on 127.0.0.1:${port}: ${e.code ?? e.message}`));
}

if (exe === undefined) fatal('usage: box-init.mjs [<port>:<socket> ...] -- <exe> [argv ...]', 2);
for (const spec of specs) await listenForwarder(spec);
for (const signal of ['SIGTERM', 'SIGINT', 'SIGHUP']) process.on(signal, () => {});
const child = spawn(exe, args, { stdio: 'inherit' });
child.on('error', e => fatal(`could not run ${exe}: ${e.code ?? e.message}`, 127));
child.on('exit', (code, signal) => process.exit(code ?? (signal ? 128 + (constants.signals[signal] ?? 1) : 1)));
