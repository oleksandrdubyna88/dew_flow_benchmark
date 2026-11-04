import fastify from 'fastify';

const app = fastify();
app.get('/hello', () => ({ hello: 'world' }));
process.on('SIGTERM', () => {
  void app.close().then(() => process.exit(0));
});
await app.listen({ host: '127.0.0.1', port: Number(process.env.PORT) });
