import net from 'node:net';

const server = net.createServer((socket) => {
  socket.on('data', (data: Buffer) => {
    socket.write(Buffer.concat([Buffer.from('echo:'), data]));
  });
  socket.on('error', () => {});
});
server.listen(Number(process.env['PORT']), '127.0.0.1');
process.on('SIGTERM', () => {
  server.close();
  process.exit(0);
});
