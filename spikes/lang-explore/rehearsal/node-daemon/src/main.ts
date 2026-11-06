// The rehearsal's daemon (quick run): answers "pong\n" to any bytes that hold "ping", exits 0 on SIGTERM. Not a solution of any task.
// HELLO_MODE (set by a rehearsal scenario's env line) makes it fail on purpose: wrong (a wrong reply), sigdie (SIGTERM kills it), exit3 (exits 3 on SIGTERM), hog (holds 700 MiB).
import net from 'node:net';

const mode = process.env.HELLO_MODE ?? '';
const reply = mode === 'wrong' ? 'pang\n' : 'pong\n';
const hog = mode === 'hog' ? Buffer.alloc(700 * 1024 * 1024, 1) : null;
const server = net.createServer(socket => {
  socket.on('data', chunk => {
    if (chunk.toString('latin1').includes('ping')) socket.write(reply);
  });
});
server.listen(Number(process.env.PORT ?? '5000'), '127.0.0.1');
if (mode !== 'sigdie') {
  process.on('SIGTERM', () => {
    server.close();
    process.exit(mode === 'exit3' ? 3 : 0);
  });
}
setInterval(() => { if (hog !== null && hog.length === 0) process.exit(9); }, 60_000);
