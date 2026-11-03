import { renameSync, writeFileSync } from 'node:fs';
import net from 'node:net';

const server = net.createServer(socket => {
  socket.once('data', data => socket.end(`${data.toString().trim().toUpperCase()}\n`));
  socket.on('error', () => {});
});
server.listen(0, '127.0.0.1', () => {
  writeFileSync('ready.port.tmp', String(server.address().port));
  renameSync('ready.port.tmp', 'ready.port');
});
