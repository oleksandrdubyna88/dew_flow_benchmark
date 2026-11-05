process.on('SIGTERM', () => {
  process.stdout.write('TERM\n', () => process.exit(0));
});
process.stdout.write('ready\n');
setInterval(() => {}, 1000);
