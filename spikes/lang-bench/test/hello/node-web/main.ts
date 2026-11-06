import http from 'node:http';

const server = http.createServer((req, res) => {
  const body = JSON.stringify({ path: req.url, method: req.method });
  res.writeHead(200, { 'content-type': 'application/json', 'content-length': Buffer.byteLength(body) });
  res.end(body);
});
server.listen(Number(process.env['PORT']), '127.0.0.1');
process.on('SIGTERM', () => {
  server.close();
  process.exit(0);
});
