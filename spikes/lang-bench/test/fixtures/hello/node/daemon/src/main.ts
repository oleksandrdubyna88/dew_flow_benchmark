import { createServer } from 'node:net';

const server = createServer(socket => {
  socket.setEncoding('utf8');
  socket.on('error', () => {});
  socket.on('data', (line: string) => {
    socket.write(`hello ${line}`);
  });
});
process.on('SIGTERM', () => {
  process.exit(0);
});
server.listen(Number(process.env.PORT), '127.0.0.1');
