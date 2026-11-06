// The rehearsal's web server (quick run): GET /hello answers "hello", exits 0 on SIGTERM. Not a solution of any task.
// HELLO_MODE makes it fail on purpose: wrong (a wrong body), sigdie (SIGTERM kills it), exit3 (exits 3 on SIGTERM), hog (holds 700 MiB).
import http from 'node:http';

const mode = process.env.HELLO_MODE ?? '';
const hog = mode === 'hog' ? Buffer.alloc(700 * 1024 * 1024, 1) : null;
const server = http.createServer((request, response) => {
  if (request.url === '/hello') {
    response.writeHead(200, { 'content-type': 'text/plain' });
    response.end(mode === 'wrong' ? 'hullo' : 'hello');
  } else {
    response.writeHead(404);
    response.end();
  }
});
server.listen(Number(process.env.PORT ?? '5000'), '127.0.0.1');
if (mode !== 'sigdie') {
  process.on('SIGTERM', () => {
    server.closeAllConnections();
    server.close();
    process.exit(mode === 'exit3' ? 3 : 0);
  });
}
setInterval(() => { if (hog !== null && hog.length === 0) process.exit(9); }, 60_000);
